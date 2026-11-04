using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using TMPro;

// Arma el mapa Residencial dentro de Unity a partir de lo que exporta pixel_town/generate_residencial_grande.py
// (128x64 tiles, mismo tileset_suelo y props_atlas que generate_unity.py; los props nuevos del mapa grande
// van agregados al final del atlas).
// Se puede ejecutar las veces que haga falta: vuelve a cortar las texturas SIEMPRE desde los JSON (nunca
// reutiliza los recortes del .meta), actualiza los tiles, repinta el suelo, reemplaza los props, los autos
// de la calle, el borde del mapa y los puntos de entrega, y mueve al repartidor al spawn. Al final verifica
// que cada sprite importado coincida con su rectángulo del JSON y deja hoja_de_props.png para revisar a ojo.
// Las cebras (de banqueta a banqueta, con isleta donde cruzan el camellón) y las líneas de alto vienen ya
// pintadas en el suelo del JSON (pixel_town/cebras.py). Arma el BloqueoMoto (lo que la moto no puede pisar), calcula las rutas de la moto a cada PuntoEntrega y crea el tráfico por rutas que entran y
// salen fuera del mapa (los autos ya no salen de "autos_calle" del JSON).
public static class ConstruirMapa
{
    // De dónde salen los datos de cada zona: la carpeta de los JSON (los genera el script de Python), las
    // mismas imágenes ya importadas en el proyecto y la carpeta de sus Tile. El Residencial es el de siempre;
    // ConstruirMapaComercial arma el nivel 2 con el suyo (Perfil.Comercial).
    internal class Perfil
    {
        public string zona, carpetaDatos, rutaTileset, rutaAtlas, carpetaTiles, archivoMapa, archivoTileset, archivoAtlas;

        public static readonly Perfil Residencial = new Perfil
        {
            zona = "Residencial",
            carpetaDatos = "C:/Users/kangu/pixel_town/unity/residencial",
            rutaTileset = "Assets/Tiles/tileset_suelo.png",
            rutaAtlas = "Assets/Sprits/Escenarios/Residencial/props_atlas.png",
            carpetaTiles = "Assets/Tiles/Residencial",
            archivoMapa = "mapa_residencial_grande.json",
            archivoTileset = "tileset_suelo.json",
            archivoAtlas = "props_atlas.json",
        };

        public static readonly Perfil Comercial = new Perfil
        {
            zona = "Comercial",
            carpetaDatos = "C:/Users/kangu/pixel_town/unity/comercial",
            rutaTileset = "Assets/Tiles/tileset_comercial.png",
            rutaAtlas = "Assets/Sprits/Escenarios/Comercial/props_comercial.png",
            carpetaTiles = "Assets/Tiles/Comercial",
            archivoMapa = "mapa_comercial.json",
            archivoTileset = "tileset_comercial.json",
            archivoAtlas = "props_comercial.json",
        };

        // Nivel 3: el mismo mapa Residencial con la variante "lluvia" de generate_lluvia_grande.py (suelo *_mojado,
        // baches con charco, charcos y ventanas); ConstruirMapaLluvia arma la escena y la noche con esto
        public static readonly Perfil Lluvia = new Perfil
        {
            zona = "Lluvia",
            carpetaDatos = "C:/Users/kangu/pixel_town/unity/lluvia",
            rutaTileset = "Assets/Tiles/tileset_lluvia.png",
            rutaAtlas = "Assets/Sprits/Escenarios/Lluvia/props_lluvia.png",
            carpetaTiles = "Assets/Tiles/Lluvia",
            archivoMapa = "mapa_lluvia.json",
            archivoTileset = "tileset_lluvia.json",
            archivoAtlas = "props_lluvia.json",
        };
    }

    // Para los constructores de cada nivel: el JSON de un perfil y los sprites de su atlas
    internal static Dictionary<string, object> LeerJsonDe(Perfil p, string archivo)
    {
        var antes = perfil;
        perfil = p;
        try { return LeerJson(archivo); }
        finally { perfil = antes; }
    }

    internal static Dictionary<string, Sprite> SpritesDe(string ruta) => CargarSprites(ruta);

    static Perfil perfil = Perfil.Residencial;
    static string CarpetaDatos => perfil.carpetaDatos;
    static string RutaTileset => perfil.rutaTileset;
    static string RutaAtlas => perfil.rutaAtlas;
    static string CarpetaTiles => perfil.carpetaTiles;
    static string ArchivoMapa => perfil.archivoMapa;
    static string ArchivoTileset => perfil.archivoTileset;
    static string ArchivoAtlas => perfil.archivoAtlas;

    // Arma el mapa de otra zona en la escena activa (sin diálogos); vuelve al Residencial al terminar
    internal static string ConstruirConPerfil(Perfil p, out bool ok)
    {
        var antes = perfil;
        perfil = p;
        try
        {
            string resumen = ConstruirTodo();
            ok = verificacionOk;
            return resumen;
        }
        finally
        {
            perfil = antes;
            EditorUtility.ClearProgressBar();
        }
    }

    // Marcador de cada PuntoEntrega: burbuja con el producto del pedido y aro en el suelo (MarcadorPedido),
    // armado por ConstruirMarcadorPedido con el arte de pixel_town/generate_marcador_pedido.py
    const string NombreAutos = "AutosCalle";
    const float GrosorBorde = 2f;

    const int PPU = 32;
    const int OrdenSuelo = -10;
    static readonly Vector2 PivoteProps = new Vector2(0.5f, 0f);   // abajo-centro, igual que el JSON

    // Captura de lo que pinta Unity, para compararla con mapa_referencia_grande.png
    const string ArchivoCaptura = "captura_unity.png";
    // Todos los sprites del atlas tal como los cortó Unity, cada uno con su nombre debajo
    const string ArchivoHoja = "hoja_de_props.png";

    // Escala común de los autos (la misma del generador, LARGO_AUTO_PX): de lado, de frente o de espaldas,
    // estacionados, en cocheras o circulando, todos miden 96 px = 3 tiles a lo largo.
    const int LargoAutoPx = 96;
    // Lagos y fuentes van completos, un solo objeto cada uno; nunca en pedazos
    static readonly string[] PiezasCompletas = { "lago", "fuente" };

    // Personajes (pixel_town/personajes/generar_personajes.py): hojas de 280x240 en celdas de 56x80,
    // fila 0 frente, 1 espaldas, 2 lado; columna 0 quieto y 1-4 caminando
    const string CarpetaPersonajes = "Assets/Sprits/Personajes";
    const string ArchivoPersonajes = "personajes.json";
    const string NombrePeatones = "Peatones";
    const string NombreDuena = "Duena";
    static readonly Vector2 TamPie = new Vector2(0.6f, 0.3f);      // BoxCollider2D en los pies
    static readonly Vector2 OffsetPie = new Vector2(0f, 0.15f);
    static readonly string[] FilasPersonaje = { "frente", "espalda", "lado" };

    static bool verificacionOk;

    // Clientas (pixel_town/personajes/generar_clientas.py), su globo de texto y la flecha guía
    const string CarpetaClientas = CarpetaPersonajes + "/Clientas";
    const string ArchivoClientas = "clientas.json";
    const string RutaMaterialGlobo = CarpetaClientas + "/GloboTexto.mat";
    const string NombreClientes = "Clientes";
    const float VelocidadClienta = 1.6f;
    const float AlturaGlobo = 2.2f;                  // tiles sobre el pivote (los pies): justo arriba de la cabeza
    const float TamLetraGlobo = 3.6f;                // TextMeshPro 3D con la cámara ortográfica de 5: letra chica pero legible
    const float ContornoLetraGlobo = 0.3f;           // contorno #2A1F40 grueso: se lee sobre pasto, banqueta y techos
    const int OrdenGlobo = 200;
    static readonly Color32 CremaGlobo = new Color32(0xFF, 0xF1, 0xD0, 0xFF);
    static readonly Color32 ContornoGlobo = new Color32(0x2A, 0x1F, 0x40, 0xFF);
    const string RutaFlecha = "Assets/Sprits/UI/flecha_guia.png";
    const string SpriteFlecha = "flecha_guia";

    // GuiaPedido: placa en el Canvas, arriba a la izquierda debajo de PlacaPedidos, con la flecha que gira
    // hacia el pedido actual y la distancia. Mismo kit que ConstruirInterfaz (píldora, x4, crema con contorno).
    const string NombreGuia = "GuiaPedido";
    const int EscalaUI = 4;                                          // 1 píxel de arte = 4 unidades del Canvas
    static readonly Vector2 PosGuia = new Vector2(30, -38 - 18 * EscalaUI - 12);   // 12 unidades bajo PlacaPedidos
    const float LadoIconoUI = 16 * 3;                                // el ícono del producto (16 px) a x3, entre flecha y distancia
    const float XIconoUI = XFlechaUI + LadoFlechaUI / 2 + 2 * EscalaUI + LadoIconoUI / 2;
    static readonly Vector2 TamGuia = new Vector2(240 + LadoIconoUI + 2 * EscalaUI, 18 * EscalaUI);
    const float LadoFlechaUI = 14 * EscalaUI;                        // la flecha de 32 px a 56 unidades: cabe en la píldora
    const float XFlechaUI = 6 * EscalaUI + LadoFlechaUI / 2;         // centro de la flecha, pasando la tapa izquierda

    struct HojaPersonaje
    {
        public string ruta;
        public string json;
        public Vector2Int tam;
        public List<DefSprite> defs;
    }

    // Franja de colisión de cada tipo de prop, medida en píxeles sobre el atlas:
    // tramos horizontales (desde el borde izquierdo del sprite, ambos inclusive) y el alto desde abajo.
    // Se usa el primer prefijo que coincida, por eso el portón va antes que "barda".
    static readonly (string prefijo, (int x0, int x1)[] tramos, int alto)[] Colisiones =
    {
        ("casa_",             new[] { (4, 155) },          16),
        ("arbol_grande",      new[] { (24, 31) },           8),
        ("arbol_mediano",     new[] { (18, 25) },           8),
        ("pino",              new[] { (17, 21) },           6),
        ("farol",             new[] { (2, 13) },            7),
        ("barda_porton",      new[] { (1, 8), (55, 62) },   8),   // solo los postes: el hueco se puede cruzar
        ("barda_poste_final", new[] { (1, 8) },             8),
        ("barda",             new[] { (0, 31) },            8),
        ("buzon_",            new[] { (7, 10) },            6),
        // props agregados para el mapa grande
        ("empresa_brenns",    new[] { (4, 315) },          22),
        ("carro_lateral_",    new[] { (2, 93) },           16),
        ("carro_frontal_",    new[] { (4, 47) },           24),
        ("carro_trasero_",    new[] { (4, 47) },           24),
        ("cerca_baja_porton", new[] { (1, 8), (87, 94) },   6),   // solo los postes: el portón se cruza
        ("cerca_baja_poste",  new[] { (0, 7) },             6),
        ("cerca_baja",        new[] { (0, 31) },            5),
        ("lago_parque",       new[] { (6, 242) },         104),   // todo el lago, no se puede entrar
        ("banca_parque",      new[] { (1, 44) },            7),
        ("hidrante",          new[] { (2, 15) },            6),
        ("bote_basura",       new[] { (2, 15) },            6),
        ("cajas_apiladas",    new[] { (0, 43) },           10),
        ("caja_carga",        new[] { (0, 19) },            8),
        // plaza y letreros con el nombre de cada calle (solo el poste choca)
        ("fuente_plaza",      new[] { (3, 88) },           24),
        ("letrero_calle_avenida",   new[] { (28, 31) },     4),
        ("letrero_calle_empedrada", new[] { (33, 36) },     4),
        ("letrero_calle_olmo",      new[] { (23, 26) },     4),
        ("letrero_calle_pino",      new[] { (22, 25) },     4),
        // relleno de la manzana del centro y semáforos
        ("columpios",         new[] { (0, 16), (56, 72) },  6),   // solo las patas
        ("resbaladilla",      new[] { (0, 60) },            8),
        ("jardinera_flores",  new[] { (0, 44) },           10),
        ("puesto_elotes",     new[] { (2, 53) },           12),
        ("semaforo_",         new[] { (12, 19) },           5),   // el poste
        // negocios pequeños (arte del Comercial) y relleno de la franja sur
        ("tienda_",           new[] { (2, 189) },          20),   // la base de la fachada
        ("camioneta_reparto", new[] { (6, 112) },          14),
        ("pizarra_panaderia", new[] { (2, 19) },            5),
        ("parquimetro",       new[] { (3, 8) },             4),
        ("cubetas_flores",    new[] { (0, 45) },            8),
        ("huerto_comunitario", new[] { (0, 127) },         56),
        ("mesa_picnic",       new[] { (0, 75) },           30),
        ("aro_basquet",       new[] { (10, 17) },           6),   // la base del poste (centrada en su pivote)
        // nivel 2 (Comercial): kioscos de la plaza y letreros de sus calles (solo el poste)
        ("kiosco_",           new[] { (3, 72) },           16),
        ("letrero_calle_comercio", new[] { (26, 29) },      4),
        ("letrero_calle_roble",    new[] { (24, 27) },      4),
        ("letrero_calle_cedro",    new[] { (24, 27) },      4),
    };

    struct DefSprite
    {
        public string nombre;
        public Rect rect;
        public Vector2 pivote;
    }

    [MenuItem("Delivery Brenn's/Construir mapa Residencial")]
    static void Construir()
    {
        try
        {
            string resumen = ConstruirTodo();
            EditorUtility.DisplayDialog("Construir mapa Residencial",
                (verificacionOk ? "Verificación sin errores.\n\n" : "LA VERIFICACIÓN ENCONTRÓ ERRORES (mira la consola).\n\n") + resumen, "OK");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Construir mapa Residencial", e.Message, "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("Delivery Brenn's/Capturar mapa Residencial (PNG)")]
    static void CapturarMenu()
    {
        try
        {
            EditorUtility.RevealInFinder(Capturar(LeerJson(ArchivoMapa)));
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Capturar mapa Residencial", e.Message, "OK");
        }
    }

    static string ConstruirTodo()
    {
        var tileset = LeerJson(ArchivoTileset);
        var atlas = LeerJson(ArchivoAtlas);
        var mapa = LeerJson(ArchivoMapa);

        EditorUtility.DisplayProgressBar("Mapa " + perfil.zona, "Cortando texturas…", 0.1f);
        // El generador copia los PNG al proyecto por fuera de Unity: se reimportan para no cortar el atlas
        // viejo con los rectángulos nuevos (así salían autos con la fuente de la plaza encima).
        AssetDatabase.Refresh();
        AssetDatabase.ImportAsset(RutaTileset, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(RutaAtlas, ImportAssetOptions.ForceUpdate);
        var defsTileset = DefsTileset(tileset, RutaTileset);
        var defsAtlas = DefsAtlas(atlas, RutaAtlas);
        CortarTextura(RutaTileset, defsTileset);
        CortarTextura(RutaAtlas, defsAtlas);
        var hojas = HojasPersonajes();
        foreach (var h in hojas)
            CortarTextura(h.ruta, h.defs);

        EditorUtility.DisplayProgressBar("Mapa " + perfil.zona, "Creando tiles…", 0.4f);
        var tiles = CrearTiles(CargarSprites(RutaTileset));

        EditorUtility.DisplayProgressBar("Mapa " + perfil.zona, "Pintando el suelo…", 0.6f);
        var suelo = PintarSuelo(mapa, tiles);

        EditorUtility.DisplayProgressBar("Mapa " + perfil.zona, "Colocando props…", 0.8f);
        var origen = suelo.transform.position;
        var spritesAtlas = CargarSprites(RutaAtlas);
        int colocados = ColocarProps(mapa, spritesAtlas, origen);
        int autos = CrearAutosCalle(mapa, spritesAtlas, origen);

        EditorUtility.DisplayProgressBar("Mapa " + perfil.zona, "Borde, repartidor y puntos de entrega…", 0.9f);
        CrearBorde(mapa, origen);
        ColocarRepartidor(mapa, origen);
        int puntos = CrearPuntosEntrega(mapa, origen);
        int peatones = CrearPeatones(mapa, origen);
        ColocarDuena(mapa, origen);
        int clientas = CrearClientes(mapa, origen);
        CrearGuiaPedido();
        // la flecha del mundo y la animación de la entrega (no dependen del marcador)
        ConstruirMarcadorPedido.CrearFlecha();
        ConstruirMarcadorPedido.CrearEntrega();
        int semaforos = CrearSemaforos(mapa, spritesAtlas, origen);
        int baches = mapa.ContainsKey("baches") ? CrearBaches(mapa, spritesAtlas, origen) : 0;

        int solidos = CrearLotesSolidos(mapa, origen);

        EditorUtility.DisplayProgressBar("Mapa " + perfil.zona, "Bloqueo de la moto y rutas…", 0.92f);
        int bloqueos = CrearBloqueoMoto(mapa, tileset, origen, out var bloqueado, out int liberadas);
        string rutasMoto = AnalizarRutasMoto(mapa, origen, bloqueado, out var tablaMoto);
        string textoPasos;
        if (mapa.ContainsKey("cruces_moto"))
            textoPasos = RevisarCrucesMoto(mapa, bloqueado);
        else
        {
            int pasosOlmo = PasosCruzables(mapa, bloqueado, true, 17, 22);
            int pasosAvenida = PasosCruzables(mapa, bloqueado, true, 43, 50);
            int pasosAvenidaVertical = PasosCruzables(mapa, bloqueado, false, 102, 109);
            if (pasosOlmo < 2 || pasosAvenida < 2)
                Debug.LogError($"Faltan pasos para cruzar en moto: calle Olmo {pasosOlmo}, avenida {pasosAvenida} (mínimo 2 cada una).");
            textoPasos = $"se cruza en moto la calle Olmo por {pasosOlmo} pasos, " +
                         $"la avenida por {pasosAvenida} y su tramo vertical por {pasosAvenidaVertical}. ";
        }
        string textoMoto = $"Moto: {bloqueos} rectángulos en BloqueoMoto ({liberadas} subceldas liberadas frente a las puertas); " +
                           textoPasos +
                           $"{tablaMoto.Count(t => t.alcanza)} de {tablaMoto.Count} puntos de entrega alcanzables, " +
                           $"hueco más angosto {(tablaMoto.Any(t => t.alcanza) ? tablaMoto.Where(t => t.alcanza).Min(t => t.hueco).ToString("0.00") : "-")} tiles.";
        Debug.Log(textoMoto + "\n" + rutasMoto);
        if (tablaMoto.Any(t => !t.alcanza))
            Debug.LogError("Puntos de entrega a los que la moto no llega: " + string.Join(", ", tablaMoto.Where(t => !t.alcanza).Select(t => t.punto)));
        var erroresAPie = new List<string>();
        string textoAPie = $"Patios: {solidos} cajas en {NombreLotes}. " + AnalizarAPie(mapa, origen, erroresAPie);
        foreach (var e in erroresAPie) Debug.LogError(e);
        string textoCamara = CameraFollow.LimitesDelMapa(out var limites)
            ? $"Cámara limitada al mapa x {limites.xMin:0.##}..{limites.xMax:0.##}, y {limites.yMin:0.##}..{limites.yMax:0.##}" +
              TextoCentroCamara(limites)
            : "Cámara: no encontré BordeMapa.";

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        int dinamicos = Lista(mapa, "dinamicos").Count;
        string resumen = $"Mapa {perfil.zona} listo ({Num(mapa["columnas"])}x{Num(mapa["filas"])} tiles): " +
                         $"{Lista(atlas, "sprites").Count} sprites en props_atlas, {colocados} props, " +
                         $"{Lista(mapa, "casas").Count} casas, {puntos} puntos de entrega, {autos} autos en la calle, " +
                         $"{peatones} peatones, la dueña en la puerta de Brenn's, {clientas} clientas, {semaforos} semáforos{(baches > 0 ? $", {baches} baches" : "")} y la guía del pedido en el Canvas. " +
                         $"{dinamicos} dinámicos (baches) quedan en el JSON para colocarlos por código.\n" +
                         textoMoto + "\n" + textoAPie + "\n" + textoCamara + "\n" + rutasMoto;
        Debug.Log(resumen);
        RevisarOrdenPorY();

        EditorUtility.DisplayProgressBar("Mapa " + perfil.zona, "Verificando recortes y armando hoja_de_props…", 0.95f);
        return resumen + "\n" + VerificarTodo(tileset, atlas, mapa, defsTileset, defsAtlas, hojas);
    }

    // ------------------------------------------------------------------ texturas

    static List<DefSprite> DefsTileset(Dictionary<string, object> json, string ruta)
    {
        int alto = AltoTextura(ruta);
        return Lista(json, "tiles").Cast<Dictionary<string, object>>()
            .Select(t => new DefSprite
            {
                nombre = (string)t["name"],
                rect = RectDesdeArriba(t, alto),
                pivote = new Vector2(0.5f, 0.5f),   // el Tilemap centra el sprite en la celda
            }).ToList();
    }

    static List<DefSprite> DefsAtlas(Dictionary<string, object> json, string ruta)
    {
        var imp = Importador(ruta);
        imp.GetSourceTextureWidthAndHeight(out int ancho, out int alto);
        if ((int)Num(json["alto"]) != alto || (int)Num(json["ancho"]) != ancho)
            throw new Exception($"{ruta} mide {ancho}x{alto} px pero props_atlas.json dice {Num(json["ancho"])}x{Num(json["alto"])}. " +
                                "Copia el props_atlas.png actual del generador.");
        var piv = Lista(json, "pivote");
        if (new Vector2((float)Num(piv[0]), (float)Num(piv[1])) != PivoteProps)
            throw new Exception($"props_atlas.json trae pivote ({Num(piv[0])}, {Num(piv[1])}); los props usan abajo-centro (0.5, 0).");
        return Lista(json, "sprites").Cast<Dictionary<string, object>>()
            .Select(s => new DefSprite { nombre = (string)s["name"], rect = RectDesdeArriba(s, alto), pivote = PivoteDe(s) })
            .ToList();
    }

    // Abajo-centro, salvo los sprites que traen su propio pivote (los aros: la base del poste)
    static Vector2 PivoteDe(Dictionary<string, object> s) =>
        s.TryGetValue("pivote", out var p) && p is List<object> l
            ? new Vector2((float)Num(l[0]), (float)Num(l[1]))
            : PivoteProps;

    // Los JSON miden desde arriba-izquierda; Unity mide los rectángulos desde abajo-izquierda.
    static Rect RectDesdeArriba(Dictionary<string, object> d, int altoTextura)
    {
        float x = (float)Num(d["x"]), y = (float)Num(d["y"]), w = (float)Num(d["w"]), h = (float)Num(d["h"]);
        return new Rect(x, altoTextura - y - h, w, h);
    }

    static int AltoTextura(string ruta)
    {
        var imp = Importador(ruta);
        imp.GetSourceTextureWidthAndHeight(out _, out int alto);
        return alto;
    }

    static TextureImporter Importador(string ruta)
    {
        var imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (imp == null) throw new Exception($"No encuentro la textura {ruta} en el proyecto.");
        return imp;
    }

    static void CortarTextura(string ruta, List<DefSprite> defs)
    {
        // 1) Ajustes de importación para pixel art
        var imp = Importador(ruta);
        var ajustes = new TextureImporterSettings();
        imp.ReadTextureSettings(ajustes);
        ajustes.textureType = TextureImporterType.Sprite;
        ajustes.spriteMode = (int)SpriteImportMode.Multiple;
        ajustes.spritePixelsPerUnit = PPU;
        ajustes.filterMode = FilterMode.Point;
        ajustes.mipmapEnabled = false;
        ajustes.alphaIsTransparency = true;
        ajustes.spriteMeshType = SpriteMeshType.FullRect;   // sin recortes de malla que dejen huecos
        imp.SetTextureSettings(ajustes);
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.GetSourceTextureWidthAndHeight(out int ancho, out int alto);
        int maximo = Mathf.Max(2048, Mathf.NextPowerOfTwo(Mathf.Max(ancho, alto)));   // que nunca se reduzca
        imp.maxTextureSize = Mathf.Max(imp.maxTextureSize, maximo);
        foreach (var plataforma in new[] { "Standalone", "Android", "WebGL", "iPhone" })
        {
            var ps = imp.GetPlatformTextureSettings(plataforma);
            if (!ps.overridden) continue;
            ps.textureCompression = TextureImporterCompression.Uncompressed;
            ps.format = TextureImporterFormat.RGBA32;
            ps.maxTextureSize = Mathf.Max(ps.maxTextureSize, maximo);
            imp.SetPlatformTextureSettings(ps);
        }

        // 2) Recortes: se arman DE CERO desde el JSON y reemplazan todo lo que hubiera en el .meta. No se lee
        //    ningún recorte anterior (ni rectángulo, ni pivote, ni contorno). El ID de cada sprite sale de su
        //    nombre, así que reconstruir dos veces da el mismo .meta y las referencias de la escena (que de
        //    todos modos se rehacen) no cambian entre corridas.
        var fabrica = new SpriteDataProviderFactories();
        fabrica.Init();
        var datos = fabrica.GetSpriteEditorDataProviderFromObject(imp);
        datos.InitSpriteEditorDataProvider();
        var nuevos = defs.Select(def => new SpriteRect
        {
            spriteID = IdSprite(ruta, def.nombre),
            name = def.nombre,
            rect = def.rect,
            alignment = SpriteAlignment.Custom,
            pivot = def.pivote,
            border = Vector4.zero,
        }).ToArray();
        datos.SetSpriteRects(nuevos);
        datos.GetDataProvider<ISpriteNameFileIdDataProvider>()
             .SetNameFileIdPairs(nuevos.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
        var contornos = datos.GetDataProvider<ISpriteOutlineDataProvider>();
        var fisica = datos.GetDataProvider<ISpritePhysicsOutlineDataProvider>();
        foreach (var s in nuevos)
        {
            contornos?.SetOutlines(s.spriteID, new List<Vector2[]>());
            fisica?.SetOutlines(s.spriteID, new List<Vector2[]>());
        }
        datos.Apply();

        // 3) Se guarda y se reimporta a la fuerza para que los Sprite cargados sean los nuevos
        imp.SaveAndReimport();
        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        Debug.Log($"{ruta}: {nuevos.Length} sprites recortados de nuevo desde el JSON.");
    }

    // ID estable por textura y nombre (MD5 de "ruta#nombre"): no depende de lo que hubiera en el .meta
    static GUID IdSprite(string ruta, string nombre)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            var h = md5.ComputeHash(Encoding.UTF8.GetBytes(ruta + "#" + nombre));
            return new GUID(string.Concat(h.Select(b => b.ToString("x2"))));
        }
    }

    static Dictionary<string, Sprite> CargarSprites(string ruta)
    {
        return AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>().ToDictionary(s => s.name);
    }

    // ------------------------------------------------------------------ tiles y suelo

    static Dictionary<string, TileBase> CrearTiles(Dictionary<string, Sprite> sprites)
    {
        if (!AssetDatabase.IsValidFolder(CarpetaTiles))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(CarpetaTiles).Replace('\\', '/'), Path.GetFileName(CarpetaTiles));

        var tiles = new Dictionary<string, TileBase>();
        foreach (var par in sprites)
        {
            string ruta = $"{CarpetaTiles}/{par.Key}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(ruta);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, ruta);
            }
            tile.sprite = par.Value;
            tile.colliderType = Tile.ColliderType.None;   // el suelo no choca; los props llevan su collider
            EditorUtility.SetDirty(tile);
            tiles[par.Key] = tile;
        }
        AssetDatabase.SaveAssets();
        return tiles;
    }

    static Tilemap PintarSuelo(Dictionary<string, object> mapa, Dictionary<string, TileBase> tiles)
    {
        // Reutiliza el Grid y el Tilemap "Suelo" de la escena si ya existen
        var grid = UnityEngine.Object.FindObjectsByType<Grid>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                       .OrderByDescending(g => g.name == "Grid").FirstOrDefault();
        if (grid == null)
        {
            grid = new GameObject("Grid", typeof(Grid)).GetComponent<Grid>();
            Undo.RegisterCreatedObjectUndo(grid.gameObject, "Construir mapa Residencial");
        }
        Undo.RecordObject(grid, "Construir mapa Residencial");
        grid.cellSize = new Vector3(1, 1, 0);   // 1 unidad = 32 px con PPU 32

        var suelo = grid.GetComponentsInChildren<Tilemap>(true).FirstOrDefault(t => t.name == "Suelo");
        if (suelo == null)
        {
            var go = new GameObject("Suelo", typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(grid.transform, false);
            Undo.RegisterCreatedObjectUndo(go, "Construir mapa Residencial");
            suelo = go.GetComponent<Tilemap>();
        }
        var render = suelo.GetComponent<TilemapRenderer>();
        Undo.RecordObject(render, "Construir mapa Residencial");
        render.sortingOrder = OrdenSuelo;

        // suelo[0] es la fila de arriba del mapa; en el Tilemap la fila 0 es la de abajo
        var filas = Lista(mapa, "suelo");
        int alto = filas.Count;
        var posiciones = new List<Vector3Int>();
        var celdas = new List<TileBase>();
        var faltantes = new HashSet<string>();
        for (int r = 0; r < alto; r++)
        {
            var fila = (List<object>)filas[r];
            for (int c = 0; c < fila.Count; c++)
            {
                if (!(fila[c] is string nombre)) continue;   // null = cielo, sin tile
                if (!tiles.TryGetValue(nombre, out var tile)) { faltantes.Add(nombre); continue; }
                posiciones.Add(new Vector3Int(c, alto - 1 - r, 0));
                celdas.Add(tile);
            }
        }
        if (faltantes.Count > 0)
            throw new Exception("El mapa usa tiles que no están en tileset_suelo: " + string.Join(", ", faltantes));

        int columnas = (int)Num(mapa["columnas"]);
        if (alto != (int)Num(mapa["filas"]) || filas.Cast<List<object>>().Any(f => f.Count != columnas))
            throw new Exception($"\"suelo\" no mide {columnas}x{Num(mapa["filas"])} tiles.");

        Undo.RegisterCompleteObjectUndo(suelo, "Construir mapa Residencial");
        suelo.ClearAllTiles();
        suelo.SetTiles(posiciones.ToArray(), celdas.ToArray());
        suelo.CompressBounds();

        // Cada celda debe tener su tile y su sprite: calles, banquetas, bordillos y pasto
        int vacias = 0;
        for (int i = 0; i < posiciones.Count; i++)
            if (suelo.GetTile(posiciones[i]) != celdas[i] || suelo.GetSprite(posiciones[i]) == null) vacias++;
        if (vacias > 0 || suelo.GetUsedTilesCount() == 0)
            throw new Exception($"El Tilemap Suelo quedó con {vacias} celdas sin pintar.");
        var conteo = celdas.GroupBy(t => t.name).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}");
        Debug.Log($"Suelo pintado: {posiciones.Count} celdas ({columnas}x{alto}). " + string.Join(", ", conteo));

        // Ningún otro Tilemap debe tapar el suelo
        foreach (var otro in UnityEngine.Object.FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (otro != render && otro.GetComponent<Tilemap>().GetUsedTilesCount() > 0)
                Debug.LogWarning($"El Tilemap \"{otro.name}\" también tiene tiles y puede tapar al Suelo.");
        return suelo;
    }

    static string TileEn(Dictionary<string, object> mapa, float x, float y)
    {
        var filas = Lista(mapa, "suelo");
        int r = filas.Count - 1 - Mathf.FloorToInt(y), c = Mathf.FloorToInt(x);
        if (r < 0 || r >= filas.Count) return null;
        var fila = (List<object>)filas[r];
        return c < 0 || c >= fila.Count ? null : fila[c] as string;
    }

    // ------------------------------------------------------------------ props

    static int ColocarProps(Dictionary<string, object> mapa, Dictionary<string, Sprite> sprites, Vector3 origen)
    {
        var props = Lista(mapa, "props").Cast<Dictionary<string, object>>().ToList();
        var faltantes = props.Select(p => (string)p["nombre"]).Where(n => !sprites.ContainsKey(n)).Distinct().ToList();
        if (faltantes.Count > 0)
            throw new Exception("El mapa usa props que no están en props_atlas: " + string.Join(", ", faltantes));
        // Las fuentes van en la plaza o en el pasto, nunca sobre la calle
        foreach (var p in props.Where(q => ((string)q["nombre"]).StartsWith("fuente")))
        {
            string bajo = TileEn(mapa, (float)Num(p["x"]), (float)Num(p["y"]) + 0.05f);
            if (bajo == null || !(bajo.StartsWith("plaza") || bajo.StartsWith("pasto") || bajo.StartsWith("flores")))
                throw new Exception($"{p["nombre"]} en ({Num(p["x"])}, {Num(p["y"])}) cae en \"{bajo}\", no en la plaza ni en el pasto.");
        }

        // Se borra el "Props" anterior para no duplicar al reconstruir
        foreach (var viejo in SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name == "Props"))
            Undo.DestroyObjectImmediate(viejo);

        var padre = new GameObject("Props");
        Undo.RegisterCreatedObjectUndo(padre, "Construir mapa Residencial");
        padre.transform.position = origen;   // alineado con el origen del Tilemap

        foreach (var p in props)
        {
            string nombre = (string)p["nombre"];
            var sprite = sprites[nombre];
            var go = new GameObject(nombre);
            go.transform.SetParent(padre.transform, false);
            // (x, y) es el pivote (abajo-centro, o la base del poste en los aros), en tiles = unidades del mundo
            var pos = new Vector2((float)Num(p["x"]), (float)Num(p["y"]));
            go.transform.localPosition = new Vector3(pos.x, pos.y, 0);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;   // el orden por Y se toma del pie del sprite
            sr.flipX = p.TryGetValue("voltear", out var v) && v is bool b && b;   // autos del carril de abajo

            AgregarColision(go, nombre, sprite.rect.width, sprite.pivot.y);
        }
        return props.Count;
    }

    // Las cajas se miden desde el pie del sprite; si el sprite tiene el pivote más arriba del pie (los aros),
    // la caja va centrada en el pivote, que es donde está la base.
    static void AgregarColision(GameObject go, string nombre, float anchoSprite, float pivoteY = 0f)
    {
        foreach (var regla in Colisiones)
        {
            if (!nombre.StartsWith(regla.prefijo)) continue;
            foreach (var (x0, x1) in regla.tramos)
            {
                float ancho = x1 - x0 + 1;
                var caja = go.AddComponent<BoxCollider2D>();
                caja.size = new Vector2(ancho, regla.alto) / PPU;
                // el pivote está en el centro de la base, así que el borde izquierdo queda en -ancho/2
                caja.offset = new Vector2(x0 + ancho / 2f - anchoSprite / 2f, pivoteY > 0f ? 0f : regla.alto / 2f) / PPU;
            }
            return;
        }
    }

    // ------------------------------------------------------------------ autos de la calle

    // Rutas de los autos, por el CENTRO del cuerpo y en tiles del mapa. Todas empiezan y terminan a FueraMapa
    // tiles del borde: con la cámara limitada al mapa nunca se ve aparecer ni desaparecer un auto. La avenida
    // es una sola ruta por sentido (entra por la izquierda, da la vuelta y sale por abajo, o al revés); la
    // empedrada y la calle Pino forman una calle vertical cuyos autos dan vuelta a la derecha en la avenida.
    // Los carriles rectos de la calle Olmo siguen usando limiteMin/limiteMax.
    const float FueraMapa = 8f;            // el cuerpo (1.5 tiles de medio largo) queda a más de 6 tiles del borde
    const float SeparacionAutos = 10f;     // mínimo entre dos autos de un carril, al empezar y al reaparecer
    const float TilesPorAutoPrincipal = 14f;
    const float TilesPorAutoSecundaria = 28f;
    const float FrenadoAutos = 2.5f;
    static readonly Vector2 CentroLado = new Vector2(0f, 0.75f), CuerpoLado = new Vector2(2.875f, 1.5f);
    static readonly Vector2 CentroVertical = new Vector2(0f, 1.5312f), CuerpoVertical = new Vector2(1.375f, 2.9375f);
    static readonly string[] ColoresAuto = { "dorado", "azul", "oliva", "teal", "rosa", "morado" };

    // Cruces (en tiles del mapa): solo entran a la vez autos que llegan por el mismo eje
    static readonly Rect CruceOlmoAvenida = Rect.MinMaxRect(102, 17, 109, 22);
    static readonly Rect CruceOlmoPino = Rect.MinMaxRect(33, 17, 36, 22);
    // donde la empedrada sale a la avenida; abarca 10 tiles del carril antes de la vuelta para ceder el paso
    // (los dos carriles de la avenida: el semáforo de ahí detiene a los dos sentidos)
    static readonly Rect CruceEmpedrada = Rect.MinMaxRect(26, 43, 38.5f, 50);

    // La vuelta de la avenida es un cuarto de círculo con centro en (96, 37): carril interior de asfalto de
    // radio 6 a 9 y exterior de 10 a 13, con el camellón entre 9 y 10 (medido sobre los píxeles del tileset).
    // Cada auto va por el centro de su carril: a 1.5 tiles del camellón y de la banqueta.
    static readonly Vector2 CentroCurva = new Vector2(96f, 37f);
    const float RadioCarrilInterior = 7.5f, RadioCarrilExterior = 11.5f;
    const int PuntosCurva = 48;
    const float FactorCurva = 0.6f;             // velocidad dentro de la curva
    // el sprite pasa a vertical a los 50° y vuelve a los 40°: con más margen el auto del carril exterior
    // se sale más sobre el bordillo del camellón (el carril mide 3 tiles y el auto no gira)
    const float HisteresisAutos = 5f;                 // por cada cuarto de vuelta (menos de 2° entre puntos)
    const float CarrilAvenidaNorte = 48.5f, CarrilAvenidaSur = 44.5f;   // centros de los carriles rectos

    class RutaAuto
    {
        public string carril, grupo;
        public bool principal, recta;
        public float velocidad;
        public int autos;
        public float fase;                  // dónde va el primer auto, en fracción del espaciado
        public Vector2? inicio;             // o el punto donde arranca (rutas de un solo auto)
        public List<Vector2> puntos;
        public Rect[] cruces;
        public List<float> inicios;         // o dónde arranca cada auto, ya calculado por el generador
        public float Largo => Enumerable.Range(1, puntos.Count - 1).Sum(i => Vector2.Distance(puntos[i - 1], puntos[i]));
    }

    // Zonas de cruce que trae el JSON ("zonas_cruce": nombre -> x, y, ancho, alto en tiles)
    static Dictionary<string, Rect> ZonasCruce(Dictionary<string, object> mapa)
    {
        var res = new Dictionary<string, Rect>();
        if (mapa.TryGetValue("zonas_cruce", out var o) && o is Dictionary<string, object> zonas)
            foreach (var par in zonas)
            {
                var z = (Dictionary<string, object>)par.Value;
                res[par.Key] = new Rect((float)Num(z["x"]), (float)Num(z["y"]), (float)Num(z["ancho"]), (float)Num(z["alto"]));
            }
        return res;
    }

    static List<RutaAuto> RutasAutos(Dictionary<string, object> mapa)
    {
        if (mapa.ContainsKey("rutas_autos"))
        {
            // las rutas del mapa (el Comercial): rectas de orilla a orilla con sus cruces y dónde arranca cada auto
            var zonas = ZonasCruce(mapa);
            return Lista(mapa, "rutas_autos").Cast<Dictionary<string, object>>().Select(r => new RutaAuto
            {
                carril = (string)r["carril"],
                principal = (bool)r["principal"],
                recta = (bool)r["recta"],
                velocidad = (float)Num(r["velocidad"]),
                autos = (int)Num(r["autos"]),
                puntos = Lista(r, "puntos").Cast<List<object>>().Select(q => new Vector2((float)Num(q[0]), (float)Num(q[1]))).ToList(),
                cruces = Lista(r, "cruces").Select(z => zonas.TryGetValue((string)z, out var rect) ? rect
                    : throw new Exception($"La ruta {r["carril"]} usa la zona \"{z}\", que no está en zonas_cruce.")).ToArray(),
                inicios = r.ContainsKey("inicios") ? Lista(r, "inicios").Select(v => (float)Num(v)).ToList() : null,
            }).ToList();
        }
        float ancho = (float)Num(mapa["columnas"]), f = FueraMapa;

        Vector2 cc = CentroCurva;
        var oeste = new List<Vector2> { new Vector2(cc.x + RadioCarrilExterior, -f), new Vector2(cc.x + RadioCarrilExterior, cc.y) };
        Arco(oeste, cc, RadioCarrilExterior, RadioCarrilExterior, 0f, 90f, PuntosCurva);
        oeste.Add(new Vector2(-f, CarrilAvenidaNorte));

        var este = new List<Vector2> { new Vector2(-f, CarrilAvenidaSur), new Vector2(cc.x, CarrilAvenidaSur) };
        Arco(este, cc, RadioCarrilInterior, RadioCarrilInterior, 90f, 0f, PuntosCurva);
        este.Add(new Vector2(cc.x + RadioCarrilInterior, -f));

        // sube por Pino y la empedrada y da vuelta a la derecha para seguir por el carril de "este"
        float giro = CarrilAvenidaSur - 2f;
        var sube = new List<Vector2> { new Vector2(35.25f, -f), new Vector2(35.25f, giro) };
        Arco(sube, new Vector2(37.25f, giro), 2f, 2f, 180f, 90f, 16);
        sube.AddRange(este.SkipWhile(p => p.x <= 37.3f));

        // viene por el carril de "este" y da vuelta a la derecha para bajar por la empedrada y Pino
        var baja = new List<Vector2> { new Vector2(-f, CarrilAvenidaSur), new Vector2(31.75f, CarrilAvenidaSur) };
        Arco(baja, new Vector2(31.75f, giro), 2f, 2f, 90f, 0f, 16);
        baja.Add(new Vector2(33.75f, -f));

        return new List<RutaAuto>
        {
            new RutaAuto { carril = "avenida_oeste", principal = true, velocidad = 2.7f, autos = 6, fase = 0.3f, puntos = oeste,
                           cruces = new[] { CruceOlmoAvenida, CruceEmpedrada } },
            new RutaAuto { carril = "avenida_este", principal = true, velocidad = 2.6f, autos = 5, fase = 0.6f, puntos = este,
                           cruces = new[] { CruceEmpedrada, CruceOlmoAvenida } },
            new RutaAuto { carril = "olmo_este", principal = true, recta = true, velocidad = 2.5f, autos = 6, fase = 0.15f,
                           puntos = new List<Vector2> { new Vector2(-f, 18f), new Vector2(ancho + f, 18f) },
                           cruces = new[] { CruceOlmoPino, CruceOlmoAvenida } },
            new RutaAuto { carril = "olmo_oeste", principal = true, recta = true, velocidad = 2.4f, autos = 6, fase = 0.55f,
                           puntos = new List<Vector2> { new Vector2(ancho + f, 21f), new Vector2(-f, 21f) },
                           cruces = new[] { CruceOlmoAvenida, CruceOlmoPino } },
            new RutaAuto { carril = "empedrada_sube", principal = false, velocidad = 2.6f, autos = 1, inicio = new Vector2(35.25f, 7f),
                           puntos = sube, cruces = new[] { CruceOlmoPino, CruceEmpedrada, CruceOlmoAvenida } },
            new RutaAuto { carril = "empedrada_baja", grupo = "avenida_este", principal = false, velocidad = 2.6f, autos = 1,
                           inicio = new Vector2(33.75f, 30f), puntos = baja, cruces = new[] { CruceEmpedrada, CruceOlmoPino } },
        };
    }

    // Arco de elipse (ángulos en grados, 0 = derecha, 90 = arriba); no repite el punto donde empieza
    static void Arco(List<Vector2> puntos, Vector2 centro, float rx, float ry, float desde, float hasta, int pasos = 24)
    {
        for (int i = 1; i <= pasos; i++)
        {
            float a = Mathf.Lerp(desde, hasta, i / (float)pasos) * Mathf.Deg2Rad;
            puntos.Add(centro + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry));
        }
    }

    static void MuestraRuta(List<Vector2> puntos, float s, out Vector2 centro, out Vector2 dir)
    {
        for (int i = 1; i < puntos.Count; i++)
        {
            float tramo = Vector2.Distance(puntos[i - 1], puntos[i]);
            if (s <= tramo || i == puntos.Count - 1)
            {
                centro = Vector2.Lerp(puntos[i - 1], puntos[i], Mathf.Clamp01(s / Mathf.Max(1e-6f, tramo)));
                dir = (puntos[i] - puntos[i - 1]).normalized;
                return;
            }
            s -= tramo;
        }
        centro = puntos[0];
        dir = Vector2.right;
    }

    static float ProyectarRuta(List<Vector2> puntos, Vector2 p)
    {
        float mejor = float.MaxValue, s = 0f, acum = 0f;
        for (int i = 1; i < puntos.Count; i++)
        {
            Vector2 a = puntos[i - 1], b = puntos[i];
            float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / Mathf.Max(1e-6f, (b - a).sqrMagnitude));
            float d = (a + (b - a) * t - p).sqrMagnitude;
            if (d < mejor) { mejor = d; s = acum + t * (b - a).magnitude; }
            acum += (b - a).magnitude;
        }
        return s;
    }

    static Rect CajaAuto(Vector2 centro, Vector2 dir, float margen)
    {
        var tam = (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y) ? CuerpoLado : CuerpoVertical) + Vector2.one * (2f * margen);
        return new Rect(centro - tam / 2f, tam);
    }

    // Dónde arranca cada auto: repartidos a lo largo de la ruta, nunca dentro de un cruce ni fuera del mapa
    static List<float> PosicionesIniciales(RutaAuto r, Dictionary<string, object> mapa)
    {
        if (r.inicios != null)
        {
            if (r.inicios.Count != r.autos) throw new Exception($"{r.carril}: trae {r.inicios.Count} inicios para {r.autos} autos.");
            return new List<float>(r.inicios);
        }
        float largo = r.Largo, paso = largo / r.autos;
        var dentro = new Rect(0, 0, (float)Num(mapa["columnas"]), (float)Num(mapa["filas"]));
        var lista = new List<float>();
        for (int i = 0; i < r.autos; i++)
        {
            float s = r.inicio.HasValue ? ProyectarRuta(r.puntos, r.inicio.Value) : paso * (i + r.fase);
            for (int k = 0; k < 200; k++)
            {
                MuestraRuta(r.puntos, s, out var c, out var d);
                bool estorba = r.cruces.Any(z => CajaAuto(c, d, 1.5f).Overlaps(z)) || !dentro.Contains(c);
                if (!estorba) break;
                s = (s + 0.5f) % largo;
            }
            lista.Add(s);
        }
        return lista;
    }

    // Un auto por cada lugar de RutasAutos: sprite según hacia dónde va, Rigidbody2D cinemático, BoxCollider2D
    // del tamaño del cuerpo y TrafficCar con su ruta (en coordenadas del mundo), sus cruces y los tres sprites
    // del mismo color. Antes se borran los de una corrida previa y cualquier TrafficCar suelto.
    static int CrearAutosCalle(Dictionary<string, object> mapa, Dictionary<string, Sprite> sprites, Vector3 origen)
    {
        var viejos = UnityEngine.Object.FindObjectsByType<TrafficCar>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(t => t.gameObject)
            .Concat(SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name == NombreAutos))
            .Distinct().ToList();
        // solo se destruyen los de más arriba; sus hijos se van con ellos
        foreach (var go in viejos.Where(g => !viejos.Any(o => o != g && g.transform.IsChildOf(o.transform))).ToList())
            Undo.DestroyObjectImmediate(go);

        var padre = new GameObject(NombreAutos);
        Undo.RegisterCreatedObjectUndo(padre, "Construir mapa Residencial");
        padre.transform.position = origen;   // alineado con el origen del Tilemap, igual que los props

        Vector2 o = origen;
        int n = 0;
        foreach (var r in RutasAutos(mapa))
        {
            var posiciones = PosicionesIniciales(r, mapa);
            for (int i = 0; i < posiciones.Count; i++)
            {
                string color = ColoresAuto[n++ % ColoresAuto.Length];
                Sprite Buscar(string vista) => sprites.TryGetValue($"carro_{vista}_{color}", out var s) ? s
                    : throw new Exception($"Falta el sprite carro_{vista}_{color} en props_atlas.");
                Sprite lado = Buscar("lateral"), frente = Buscar("frontal"), espalda = Buscar("trasero");

                MuestraRuta(r.puntos, posiciones[i], out var centro, out var dir);
                bool deLado = Mathf.Abs(dir.x) >= Mathf.Abs(dir.y);
                Vector2 pivote = centro - (deLado ? CentroLado : CentroVertical);

                var go = new GameObject($"Auto_{r.carril}_{i + 1}");
                go.transform.SetParent(padre.transform, false);
                go.transform.localPosition = new Vector3(pivote.x, pivote.y, 0);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = deLado ? lado : dir.y < 0 ? frente : espalda;
                sr.spriteSortPoint = SpriteSortPoint.Pivot;   // se ordena por Y junto con los props
                sr.flipX = deLado && dir.x < 0;

                var rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;

                var caja = go.AddComponent<BoxCollider2D>();
                caja.isTrigger = false;
                caja.size = deLado ? CuerpoLado : CuerpoVertical;
                caja.offset = deLado ? CentroLado : CentroVertical;

                var auto = new SerializedObject(go.AddComponent<TrafficCar>());
                Campo(auto, "direccion").vector2Value = dir;
                Campo(auto, "velocidad").floatValue = r.velocidad;
                var ruta = Campo(auto, "ruta");
                if (r.recta)
                {
                    // carril recto de siempre: los límites quedan FueraMapa tiles afuera del borde
                    float a = deLado ? r.puntos[0].x : r.puntos[0].y, b = deLado ? r.puntos[1].x : r.puntos[1].y;
                    float corrimiento = deLado ? o.x : o.y;
                    Campo(auto, "limiteMin").floatValue = Mathf.Min(a, b) + corrimiento;
                    Campo(auto, "limiteMax").floatValue = Mathf.Max(a, b) + corrimiento;
                    ruta.arraySize = 0;
                }
                else
                {
                    ruta.arraySize = r.puntos.Count;
                    for (int k = 0; k < r.puntos.Count; k++) ruta.GetArrayElementAtIndex(k).vector2Value = r.puntos[k] + o;
                }
                Campo(auto, "carril").stringValue = r.carril;
                Campo(auto, "grupoSalida").stringValue = r.grupo ?? "";
                Campo(auto, "separacionMinima").floatValue = SeparacionAutos;
                Campo(auto, "distanciaFrenado").floatValue = FrenadoAutos;
                Campo(auto, "factorCurva").floatValue = FactorCurva;
                Campo(auto, "histeresisGrados").floatValue = HisteresisAutos;
                var cruces = Campo(auto, "cruces");
                cruces.arraySize = r.cruces.Length;
                for (int k = 0; k < r.cruces.Length; k++)
                    cruces.GetArrayElementAtIndex(k).rectValue = new Rect(r.cruces[k].position + o, r.cruces[k].size);
                Campo(auto, "spriteLado").objectReferenceValue = lado;
                Campo(auto, "spriteFrente").objectReferenceValue = frente;
                Campo(auto, "spriteEspalda").objectReferenceValue = espalda;
                Campo(auto, "cuerpoLado").vector2Value = CuerpoLado;
                Campo(auto, "centroLado").vector2Value = CentroLado;
                Campo(auto, "cuerpoVertical").vector2Value = CuerpoVertical;
                Campo(auto, "centroVertical").vector2Value = CentroVertical;
                auto.ApplyModifiedPropertiesWithoutUndo();

                string bajo = TileEn(mapa, centro.x, centro.y);
                if (bajo != null && (bajo.StartsWith("banqueta") || bajo.StartsWith("pasto") ||
                                     bajo.StartsWith("flores") || bajo.StartsWith("plaza")))
                    throw new Exception($"{go.name} arranca sobre \"{bajo}\", fuera de la calle.");
            }
        }
        return n;
    }

    // Separación inicial por carril (mismo sentido y misma línea) y densidad de cada ruta
    static string RevisarTrafico(Dictionary<string, object> mapa, List<string> errores)
    {
        var datos = UnityEngine.Object.FindObjectsByType<TrafficCar>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(a =>
            {
                var so = new SerializedObject(a);
                var c = a.GetComponent<BoxCollider2D>();
                return (auto: a, carril: so.FindProperty("carril").stringValue,
                        centro: (Vector2)a.transform.position + c.offset,
                        dir: so.FindProperty("direccion").vector2Value.normalized);
            }).ToList();

        float minimo = float.MaxValue;
        for (int i = 0; i < datos.Count; i++)
            for (int j = i + 1; j < datos.Count; j++)
            {
                var a = datos[i]; var b = datos[j];
                if (Vector2.Dot(a.dir, b.dir) < 0.9f) continue;
                Vector2 delta = b.centro - a.centro;
                if (Mathf.Abs(a.dir.x * delta.y - a.dir.y * delta.x) > 0.6f) continue;   // otro carril
                minimo = Mathf.Min(minimo, delta.magnitude);
                if (delta.magnitude < SeparacionAutos)
                    errores.Add($"{a.auto.name} y {b.auto.name} arrancan a {delta.magnitude:0.0} tiles (mínimo {SeparacionAutos}).");
            }

        var lineas = new List<string>();
        foreach (var r in RutasAutos(mapa))
        {
            float porAuto = r.Largo / r.autos;
            float tope = r.principal ? TilesPorAutoPrincipal : TilesPorAutoSecundaria;
            if (porAuto < tope) errores.Add($"{r.carril}: un auto cada {porAuto:0.0} tiles (máximo uno cada {tope}).");
            int enEscena = datos.Count(d => d.carril == r.carril);
            if (enEscena != r.autos) errores.Add($"{r.carril}: hay {enEscena} autos y debían ser {r.autos}.");
            lineas.Add($"{r.carril} {enEscena} (1 cada {porAuto:0} tiles)");
        }
        return $"Tráfico: {datos.Count} autos — {string.Join(", ", lineas)}; separación mínima al empezar {minimo:0.0} tiles.";
    }

    // ------------------------------------------------------------------ semáforos

    const string NombreSemaforos = "Semaforos";
    const float VerdeSemaforo = 8f, AmbarSemaforo = 1.2f;
    static readonly string[] EstadosSemaforo = { "verde", "ambar", "rojo", "apagado" };

    // Los tres cruces con turno: la zona que usan los autos, la esquina de banqueta junto a la que va cada luz
    // (el vértice y hacia dónde se busca lugar) y un desfase para que no cambien todos a la vez
    static (string nombre, Rect zona, Vector2 esquinaEO, Vector2 haciaEO, Vector2 esquinaNS, Vector2 haciaNS, float desfase)[] CrucesConSemaforo(Dictionary<string, object> mapa)
    {
        if (mapa.ContainsKey("semaforos"))
        {
            var zonas = ZonasCruce(mapa);
            Vector2 V(Dictionary<string, object> d, string k) { var l = (List<object>)d[k]; return new Vector2((float)Num(l[0]), (float)Num(l[1])); }
            return Lista(mapa, "semaforos").Cast<Dictionary<string, object>>()
                .Select(d => ((string)d["nombre"], zonas[(string)d["zona"]], V(d, "esquina_eo"), V(d, "hacia_eo"), V(d, "esquina_ns"), V(d, "hacia_ns"),
                              (float)Num(d["desfase"]))).ToArray();
        }
        return new[]
        {
            ("Olmo_Avenida", CruceOlmoAvenida, new Vector2(102, 22), new Vector2(-1, 1), new Vector2(109, 17), new Vector2(1, -1), 0f),
            ("Olmo_Pino", CruceOlmoPino, new Vector2(33, 22), new Vector2(-1, 1), new Vector2(36, 17), new Vector2(1, -1), 3.1f),
            ("Empedrada_Avenida", CruceEmpedrada, new Vector2(33, 43), new Vector2(-1, -1), new Vector2(36, 43), new Vector2(1, -1), 6.2f),
        };
    }

    // Cebras cerca de un cruce, en rectángulos (una por grupo de celdas vecinas del mismo eje): las cebra_v_*
    // cruzan una calle vertical (autos norte-sur), las cebra_h_* una horizontal (autos este-oeste)
    static (List<Rect> ns, List<Rect> eo) CebrasDe(Dictionary<string, object> mapa, Rect zona)
    {
        var ns = new List<Rect>();
        var eo = new List<Rect>();
        var celdas = new Dictionary<Vector2Int, string>();
        for (int x = Mathf.FloorToInt(zona.xMin) - 5; x < Mathf.CeilToInt(zona.xMax) + 5; x++)
            for (int y = Mathf.FloorToInt(zona.yMin) - 5; y < Mathf.CeilToInt(zona.yMax) + 5; y++)
            {
                string t = TileEn(mapa, x + 0.5f, y + 0.5f);
                if (t != null && (t.StartsWith("cebra_v") || t.StartsWith("cebra_h"))) celdas[new Vector2Int(x, y)] = t.Substring(0, 7);
            }
        var vistas = new HashSet<Vector2Int>();
        foreach (var par in celdas)
        {
            if (!vistas.Add(par.Key)) continue;
            var grupo = new List<Vector2Int> { par.Key };
            var pila = new Stack<Vector2Int>(grupo);
            while (pila.Count > 0)
            {
                var p = pila.Pop();
                foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                    if (celdas.TryGetValue(p + d, out var t2) && t2 == par.Value && vistas.Add(p + d)) { grupo.Add(p + d); pila.Push(p + d); }
            }
            var r = Rect.MinMaxRect(grupo.Min(g => g.x), grupo.Min(g => g.y), grupo.Max(g => g.x) + 1, grupo.Max(g => g.y) + 1);
            (par.Value == "cebra_v" ? ns : eo).Add(r);
        }
        return (ns, eo);
    }

    // Lugar para el poste: en banqueta lisa (sin cebra, asfalto ni bordillo redondeado alrededor), lejos de los
    // PuntosEntrega, del spawn, de otros props que chocan y de los caminos de los peatones
    static Vector2 LugarSemaforo(Dictionary<string, object> mapa, Vector2 esquina, Vector2 hacia, List<Rect> estorbos,
                                 List<(Vector2 a, Vector2 b)> caminos, List<Vector2> ocupados)
    {
        var entregas = Lista(mapa, "puntos_entrega").Cast<Dictionary<string, object>>()
            .Select(p => new Vector2((float)Num(p["x"]), (float)Num(p["y"]))).ToList();
        var spawn = (Dictionary<string, object>)mapa["spawn"];
        var salida = new Vector2((float)Num(spawn["x"]), (float)Num(spawn["y"]));
        float DistSegmento(Vector2 p, Vector2 a, Vector2 b)
        {
            float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / Mathf.Max(1e-6f, (b - a).sqrMagnitude));
            return Vector2.Distance(p, a + (b - a) * t);
        }
        float DistRect(Vector2 p, Rect r)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax), dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
        for (float suma = 1.2f; suma <= 6f; suma += 0.2f)
            for (float dx = 0.6f; dx <= suma - 0.6f + 1e-3f; dx += 0.2f)
            {
                var p = esquina + new Vector2(hacia.x * dx, hacia.y * (suma - dx));
                bool liso = true;
                for (float ox = -0.7f; ox <= 0.71f && liso; ox += 0.7f)
                    for (float oy = -0.5f; oy <= 0.71f && liso; oy += 0.6f)
                        if (!(TileEn(mapa, p.x + ox, p.y + oy) ?? "").StartsWith("banqueta")) liso = false;
                if (!liso) continue;
                if (entregas.Any(e => Vector2.Distance(e, p) < 2.6f) || Vector2.Distance(salida, p) < 2f) continue;
                if (estorbos.Any(r => DistRect(p, r) < 0.8f)) continue;
                if (caminos.Any(c => DistSegmento(p, c.a, c.b) < 0.8f)) continue;
                if (ocupados.Any(o => Vector2.Distance(o, p) < 2f)) continue;
                return p;
            }
        throw new Exception($"No encontré lugar para un semáforo junto a la esquina {esquina}.");
    }

    // Dos semáforos por cruce (uno para cada eje) y un componente Semaforo por cruce con el ciclo: verde 8 s,
    // ámbar 1.2 s, luego el otro eje. Los autos (TrafficCar) consultan el semáforo de la zona antes de entrar.
    static int CrearSemaforos(Dictionary<string, object> mapa, Dictionary<string, Sprite> sprites, Vector3 origen)
    {
        BorrarViejos<Semaforo>(NombreSemaforos);
        var luces = EstadosSemaforo.ToDictionary(e => e, e => sprites.TryGetValue($"semaforo_{e}", out var s) ? s
            : throw new Exception($"Falta el sprite semaforo_{e} en props_atlas (corre generate_residencial_grande.py)."));
        Vector2 o = origen;

        // lo que ya choca en la banqueta y los caminos de los peatones, en coordenadas del mapa
        var props = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Props");
        var estorbos = props == null ? new List<Rect>() : props.GetComponentsInChildren<BoxCollider2D>(true)
            .Select(c => { var r = CajaMundo(c); r.position -= o; return r; }).ToList();
        var caminos = new List<(Vector2, Vector2)>();
        foreach (var p in Lista(mapa, "peatones").Cast<Dictionary<string, object>>())
        {
            var pts = Lista(p, "puntos").Cast<List<object>>().Select(q => new Vector2((float)Num(q[0]), (float)Num(q[1]))).ToList();
            for (int i = 1; i < pts.Count; i++) caminos.Add((pts[i - 1], pts[i]));
        }
        foreach (var c in Lista(mapa, "clientes").Cast<Dictionary<string, object>>())
            caminos.Add((PuntoMundo(c["puerta"], Vector3.zero), PuntoMundo(c["espera"], Vector3.zero)));

        var raiz = new GameObject(NombreSemaforos);
        Undo.RegisterCreatedObjectUndo(raiz, "Construir mapa Residencial");
        raiz.transform.position = origen;
        var puestos = new List<Vector2>();
        int n = 0;
        foreach (var c in CrucesConSemaforo(mapa))
        {
            var cruce = new GameObject($"Cruce_{c.nombre}");
            cruce.transform.SetParent(raiz.transform, false);
            cruce.transform.localPosition = c.zona.center;

            SpriteRenderer Luz(string nombre, Vector2 esquina, Vector2 hacia, string estado)
            {
                var lugar = LugarSemaforo(mapa, esquina, hacia, estorbos, caminos, puestos);
                puestos.Add(lugar);
                var go = new GameObject(nombre);
                go.transform.SetParent(cruce.transform, false);
                go.transform.position = new Vector3(lugar.x + o.x, lugar.y + o.y, 0);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = luces[estado];
                sr.spriteSortPoint = SpriteSortPoint.Pivot;   // se ordena por Y como los faroles
                AgregarColision(go, "semaforo_" + estado, sr.sprite.rect.width);
                n++;
                return sr;
            }
            var semaforo = cruce.AddComponent<Semaforo>();
            // en el editor muestran el estado del segundo 0 del ciclo
            string eo = Estado(c.desfase, true), ns = Estado(c.desfase, false);
            var luzEO = Luz("Semaforo_EsteOeste", c.esquinaEO, c.haciaEO, eo);
            var luzNS = Luz("Semaforo_NorteSur", c.esquinaNS, c.haciaNS, ns);

            var (pasosNS, pasosEO) = CebrasDe(mapa, c.zona);
            var so = new SerializedObject(semaforo);
            Campo(so, "zona").rectValue = new Rect(c.zona.position + o, c.zona.size);
            void Rects(string campo, List<Rect> rs)
            {
                var arr = Campo(so, campo);
                arr.arraySize = rs.Count;
                for (int i = 0; i < rs.Count; i++) arr.GetArrayElementAtIndex(i).rectValue = new Rect(rs[i].position + o, rs[i].size);
            }
            Rects("pasosNorteSur", pasosNS);
            Rects("pasosEsteOeste", pasosEO);
            Campo(so, "verde").floatValue = VerdeSemaforo;
            Campo(so, "ambar").floatValue = AmbarSemaforo;
            Campo(so, "desfase").floatValue = c.desfase;
            Campo(so, "lucesEsteOeste").arraySize = 1;
            Campo(so, "lucesEsteOeste").GetArrayElementAtIndex(0).objectReferenceValue = luzEO;
            Campo(so, "lucesNorteSur").arraySize = 1;
            Campo(so, "lucesNorteSur").GetArrayElementAtIndex(0).objectReferenceValue = luzNS;
            Campo(so, "spriteVerde").objectReferenceValue = luces["verde"];
            Campo(so, "spriteAmbar").objectReferenceValue = luces["ambar"];
            Campo(so, "spriteRojo").objectReferenceValue = luces["rojo"];
            Campo(so, "spriteApagado").objectReferenceValue = luces["apagado"];
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"Semáforo {c.nombre}: luces en {luzEO.transform.position - origen} y {luzNS.transform.position - origen}; " +
                      $"{pasosNS.Count} cebras sobre autos norte-sur y {pasosEO.Count} sobre autos este-oeste.");
        }
        return n;
    }

    // El mismo ciclo que Semaforo.Estado, para dejar el sprite correcto en el editor
    static string Estado(float t, bool esteOeste)
    {
        float c = Mathf.Repeat(t, 2f * (VerdeSemaforo + AmbarSemaforo));
        bool turnoEO = c < VerdeSemaforo + AmbarSemaforo;
        if (turnoEO != esteOeste) return "rojo";
        return (turnoEO ? c : c - (VerdeSemaforo + AmbarSemaforo)) < VerdeSemaforo ? "verde" : "ambar";
    }

    static string RevisarSemaforos(Dictionary<string, object> mapa, List<string> errores)
    {
        var semaforos = UnityEngine.Object.FindObjectsByType<Semaforo>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int cruces = CrucesConSemaforo(mapa).Length;
        if (semaforos.Length != cruces) errores.Add($"Hay {semaforos.Length} cruces con semáforo; debían ser {cruces}.");
        int lucesOk = 0;
        var zonasAutos = new HashSet<Rect>(UnityEngine.Object.FindObjectsByType<TrafficCar>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .SelectMany(a => { var p = new SerializedObject(a).FindProperty("cruces"); return Enumerable.Range(0, p.arraySize).Select(i => p.GetArrayElementAtIndex(i).rectValue); }));
        foreach (var s in semaforos)
        {
            var so = new SerializedObject(s);
            if (!zonasAutos.Contains(so.FindProperty("zona").rectValue)) errores.Add($"{s.name}: su zona no es la de ningún auto.");
            foreach (var campo in new[] { "lucesEsteOeste", "lucesNorteSur" })
            {
                var arr = so.FindProperty(campo);
                for (int i = 0; i < arr.arraySize; i++)
                    if (arr.GetArrayElementAtIndex(i).objectReferenceValue is SpriteRenderer sr && sr.sprite != null && sr.GetComponent<BoxCollider2D>() != null) lucesOk++;
                    else errores.Add($"{s.name}: {campo}[{i}] sin sprite o sin collider.");
            }
            if (Mathf.Abs(so.FindProperty("verde").floatValue - VerdeSemaforo) > 1e-3f || Mathf.Abs(so.FindProperty("ambar").floatValue - AmbarSemaforo) > 1e-3f)
                errores.Add($"{s.name}: el ciclo no es verde {VerdeSemaforo} s / ámbar {AmbarSemaforo} s.");
        }
        return $"Semáforos: {semaforos.Length} cruces, {lucesOk} postes (verde {VerdeSemaforo} s, ámbar {AmbarSemaforo} s por eje; ciclo {2 * (VerdeSemaforo + AmbarSemaforo)} s).";
    }

    // ------------------------------------------------------------------ baches (niveles 2 y 3)

    const string NombreBaches = "Baches";
    const int OrdenBache = -6;             // sobre el suelo (-10) y bajo el aro del marcador (-5)
    const int OrdenParticula = 96;         // el salpicón, encima de la moto
    const float DistanciaBache = 2f;       // a cruces, cebras, semáforos y cajones
    const float TriggerBache = 0.7f;       // el trigger es el hoyo, sin el borde roto

    // Un objeto por bache del JSON: SpriteRenderer (bache_<tamaño> o bache_charco_<tamaño>), BoxCollider2D trigger,
    // Bache y 3 partículas apagadas (polvo, o gotas en el charco)
    static int CrearBaches(Dictionary<string, object> mapa, Dictionary<string, Sprite> sprites, Vector3 origen)
    {
        BorrarViejos<Bache>(NombreBaches);
        var suelo = UnityEngine.Object.FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .FirstOrDefault(t => t.name == "Suelo");
        int capa = suelo != null ? suelo.sortingLayerID : 0;
        Sprite Buscar(string n) => sprites.TryGetValue(n, out var sp) ? sp : throw new Exception($"Falta el sprite {n} en el atlas.");

        var raiz = new GameObject(NombreBaches);
        Undo.RegisterCreatedObjectUndo(raiz, "Construir mapa " + perfil.zona);
        raiz.transform.position = origen;
        int n = 0;
        foreach (var d in Lista(mapa, "baches").Cast<Dictionary<string, object>>())
        {
            bool agua = d.TryGetValue("agua", out var a) && a is bool b && b;
            var sprite = Buscar((agua ? "bache_charco_" : "bache_") + (string)d["tamano"]);
            var particula = Buscar(agua ? "particula_gota" : "particula_polvo");

            var go = new GameObject((string)d["nombre"]);
            go.transform.SetParent(raiz.transform, false);
            go.transform.localPosition = new Vector3((float)Num(d["x"]), (float)Num(d["y"]), 0);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerID = capa;
            sr.sortingOrder = OrdenBache;
            var caja = go.AddComponent<BoxCollider2D>();
            caja.isTrigger = true;
            caja.size = sprite.bounds.size * TriggerBache;
            caja.offset = sprite.bounds.center;

            var partes = new SpriteRenderer[3];
            for (int i = 0; i < partes.Length; i++)
            {
                var pgo = new GameObject($"Particula_{i + 1}");
                pgo.transform.SetParent(go.transform, false);
                partes[i] = pgo.AddComponent<SpriteRenderer>();
                partes[i].sprite = particula;
                partes[i].sortingLayerID = capa;
                partes[i].sortingOrder = OrdenParticula;
                pgo.SetActive(false);
            }
            var so = new SerializedObject(go.AddComponent<Bache>());
            var arr = Campo(so, "particulas");
            arr.arraySize = partes.Length;
            for (int i = 0; i < partes.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = partes[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            n++;
        }
        return n;
    }

    // Cada bache sobre carril (asfalto bajo todo el trigger), a DistanciaBache o más de las zonas de cruce, de toda
    // celda de cebra, línea de alto o cajón, y de los postes de los semáforos; trigger y partículas bien armados
    static string RevisarBaches(Dictionary<string, object> mapa, List<string> errores)
    {
        var raiz = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == NombreBaches);
        var lista = Lista(mapa, "baches").Cast<Dictionary<string, object>>().ToList();
        var enEscena = UnityEngine.Object.FindObjectsByType<Bache>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (raiz == null) { errores.Add($"No hay objeto \"{NombreBaches}\" en la escena."); return "Baches: no están."; }
        if (enEscena.Length != lista.Count) errores.Add($"Hay {enEscena.Length} Bache en la escena y el JSON trae {lista.Count}.");
        Vector2 o = raiz.transform.position;
        int ancho = (int)Num(mapa["columnas"]), alto = (int)Num(mapa["filas"]);
        var prohibidas = new List<Rect>();
        for (int x = 0; x < ancho; x++)
            for (int y = 0; y < alto; y++)
            {
                string t = TileEn(mapa, x + 0.5f, y + 0.5f) ?? "";
                if (t.StartsWith("cebra") || t.StartsWith("linea_alto") || t.StartsWith("cajon")) prohibidas.Add(new Rect(x, y, 1, 1));
            }
        var zonas = ZonasCruce(mapa).Values.ToList();
        var postes = UnityEngine.Object.FindObjectsByType<Semaforo>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .SelectMany(s => s.GetComponentsInChildren<BoxCollider2D>(true)).Select(c => { var r = CajaMundo(c); r.position -= o; return r; }).ToList();
        // los autos estacionados (los que no circulan): su carrocería
        postes.AddRange(UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(sr => sr.sprite != null && sr.sprite.name.StartsWith("carro_") && sr.GetComponentInParent<TrafficCar>(true) == null)
            .Select(sr => new Rect((Vector2)sr.bounds.min - o, sr.bounds.size)));
        float Dist(Rect a, Rect b)
        {
            float dx = Mathf.Max(b.xMin - a.xMax, a.xMin - b.xMax, 0f), dy = Mathf.Max(b.yMin - a.yMax, a.yMin - b.yMax, 0f);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
        float minimo = float.MaxValue;
        int bien = 0;
        foreach (var d in lista)
        {
            string nombre = (string)d["nombre"];
            var go = raiz.transform.Find(nombre);
            var bache = go != null ? go.GetComponent<Bache>() : null;
            if (bache == null) { errores.Add($"{nombre}: no existe o no tiene Bache."); continue; }
            int antes = errores.Count;
            var sr = go.GetComponent<SpriteRenderer>();
            var caja = go.GetComponent<BoxCollider2D>();
            if (caja == null || !caja.isTrigger) errores.Add($"{nombre}: el collider no es trigger.");
            if (sr == null || sr.sprite == null || !sr.sprite.name.StartsWith("bache_")) errores.Add($"{nombre}: sin sprite de bache.");
            var parts = new SerializedObject(bache).FindProperty("particulas");
            if (parts.arraySize != 3) errores.Add($"{nombre}: {parts.arraySize} partículas, no 3.");
            if (go.GetComponentsInChildren<SpriteRenderer>(true).Skip(1).Any(p => p.gameObject.activeSelf)) errores.Add($"{nombre}: las partículas deben empezar apagadas.");
            var r = CajaMundo(caja);
            r.position -= o;
            var visible = sr != null && sr.sprite != null ? new Rect((Vector2)go.localPosition - (Vector2)sr.sprite.bounds.extents, sr.sprite.bounds.size) : r;
            bool fuera = false;
            for (float x = visible.xMin + 0.02f; x < visible.xMax && !fuera; x += 0.25f)
                for (float y = visible.yMin + 0.02f; y < visible.yMax && !fuera; y += 0.25f)
                {
                    string t = TileEn(mapa, x, y) ?? "";
                    if (t.StartsWith("asfalto") || t.StartsWith("carril") || t.StartsWith("linea_v")) continue;
                    errores.Add($"{nombre}: pisa \"{t}\" en ({x:0.00}, {y:0.00}), fuera del carril.");
                    fuera = true;
                }
            float dz = zonas.Count > 0 ? zonas.Min(z => Dist(visible, z)) : 99f;
            float dc = prohibidas.Count > 0 ? prohibidas.Min(z => Dist(visible, z)) : 99f;
            float ds = postes.Count > 0 ? postes.Min(z => Dist(visible, z)) : 99f;
            float dmin = Mathf.Min(dz, Mathf.Min(dc, ds));
            minimo = Mathf.Min(minimo, dmin);
            if (dmin < DistanciaBache)
                errores.Add($"{nombre}: a {dmin:0.00} tiles de un cruce, cebra, cajón, semáforo o auto estacionado (mínimo {DistanciaBache}).");
            if (errores.Count == antes) bien++;
        }
        return $"Baches: {bien} de {lista.Count} bien puestos (trigger, 3 partículas, sobre carril), a {minimo:0.0} tiles o más de cruces, cebras, cajones, semáforos y autos estacionados.";
    }

    // ------------------------------------------------------------------ moto: pasos, bloqueo y rutas

    const string NombreBloqueo = "BloqueoMoto";
    const int SubBloqueo = 4;               // subceldas por tile (8 px): el bloqueo sigue las curvas de la banqueta
    const float HuecoMoto = 1.2f;           // la moto (72 px, pies de 0.6x0.3) necesita huecos de 1.2 tiles
    const int CeldasRecorrido = 10;         // rejilla del recorrido: 0.1 tiles
    const float RadioLibrePuerta = 1f;      // banqueta libre alrededor de cada PuntoEntrega

    // Qué subceldas de cada tile son pasto (más de la mitad de sus píxeles verdes), leído del PNG del tileset.
    // Así las curvas, esquinas y orillas mitad banqueta, mitad pasto se bloquean solo en su parte de pasto.
    static Dictionary<string, bool[]> SubceldasDePasto(Dictionary<string, object> tileset)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!tex.LoadImage(File.ReadAllBytes(RutaTileset))) throw new Exception($"No pude leer {RutaTileset}.");
            var px = tex.GetPixels32();
            int ancho = tex.width, alto = tex.height, lado = 32 / SubBloqueo;
            var res = new Dictionary<string, bool[]>();
            foreach (var t in Lista(tileset, "tiles").Cast<Dictionary<string, object>>())
            {
                int x0 = (int)Num(t["x"]), y0 = (int)Num(t["y"]);   // arriba-izquierda
                var sub = new bool[SubBloqueo * SubBloqueo];
                for (int sy = 0; sy < SubBloqueo; sy++)              // sy = 0 es la subfila de abajo
                    for (int sx = 0; sx < SubBloqueo; sx++)
                    {
                        int verdes = 0;
                        for (int j = 0; j < lado; j++)
                            for (int i = 0; i < lado; i++)
                            {
                                int xa = x0 + sx * lado + i, ya = y0 + (SubBloqueo - 1 - sy) * lado + j;
                                var c = px[(alto - 1 - ya) * ancho + xa];
                                if (c.g > c.r + 8 && c.g > c.b + 8) verdes++;
                            }
                        sub[sy * SubBloqueo + sx] = verdes * 2 > lado * lado;
                    }
                res[(string)t["name"]] = sub;
            }
            return res;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }

    // Caja de un BoxCollider2D en coordenadas del mundo (los props no giran)
    static Rect CajaMundo(BoxCollider2D c)
    {
        Vector2 escala = c.transform.lossyScale;
        Vector2 centro = (Vector2)c.transform.position + Vector2.Scale(c.offset, escala);
        Vector2 tam = Vector2.Scale(c.size, new Vector2(Mathf.Abs(escala.x), Mathf.Abs(escala.y)));
        return new Rect(centro - tam / 2f, tam);
    }

    // Subceldas (de 1/SubBloqueo tile, en [x, y] del mapa) que son pasto o camellón. Con esto se arma el
    // BloqueoMoto y se revisa que ningún auto pise el pasto.
    static bool[,] PastoYCamellon(Dictionary<string, object> mapa, Dictionary<string, object> tileset)
    {
        var filas = Lista(mapa, "suelo").Cast<List<object>>().ToList();
        int alto = filas.Count, ancho = filas[0].Count, S = SubBloqueo;
        var pasto = SubceldasDePasto(tileset);
        var res = new bool[ancho * S, alto * S];
        for (int r = 0; r < alto; r++)
            for (int c = 0; c < ancho; c++)
            {
                string nombre = (string)filas[r][c];
                int y = alto - 1 - r;
                if (!pasto.TryGetValue(nombre, out var sub)) throw new Exception($"El tile {nombre} no está en tileset_suelo.json.");
                // camellón entero, salvo las celdas de su columna que son solo asfalto (camellon_04: la punta
                // del camellón de la avenida vertical termina antes del carril de Olmo)
                bool camellon = nombre.StartsWith("camellon") && sub.Any(v => v);
                for (int sy = 0; sy < S; sy++)
                    for (int sx = 0; sx < S; sx++)
                        res[c * S + sx, y * S + sy] = camellon || sub[sy * S + sx];
            }
        return res;
    }

    // Subceldas de "grid" que toca el rectángulo r (coordenadas del mapa)
    static int SubceldasTocadas(bool[,] grid, Rect r)
    {
        int S = SubBloqueo, w = grid.GetLength(0), h = grid.GetLength(1), n = 0;
        const float eps = 0.01f;
        for (int x = Mathf.Max(0, Mathf.FloorToInt((r.xMin + eps) * S)); x < Mathf.Min(w, Mathf.CeilToInt((r.xMax - eps) * S)); x++)
            for (int y = Mathf.Max(0, Mathf.FloorToInt((r.yMin + eps) * S)); y < Mathf.Min(h, Mathf.CeilToInt((r.yMax - eps) * S)); y++)
                if (grid[x, y]) n++;
        return n;
    }

    // Distancia mínima del centro del auto (por toda su ruta, dentro del mapa) al pasto o al camellón
    static string RevisarCurvas(Dictionary<string, object> mapa, Dictionary<string, object> tileset, List<string> errores)
    {
        var pasto = PastoYCamellon(mapa, tileset);
        int S = SubBloqueo, w = pasto.GetLength(0), h = pasto.GetLength(1);
        float Distancia(Vector2 p)
        {
            float mejor = 9f;
            int cx = Mathf.FloorToInt(p.x * S), cy = Mathf.FloorToInt(p.y * S);
            for (int x = Mathf.Max(0, cx - 3 * S); x < Mathf.Min(w, cx + 3 * S); x++)
                for (int y = Mathf.Max(0, cy - 3 * S); y < Mathf.Min(h, cy + 3 * S); y++)
                {
                    if (!pasto[x, y]) continue;
                    var r = new Rect(x / (float)S, y / (float)S, 1f / S, 1f / S);
                    float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax), dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
                    mejor = Mathf.Min(mejor, Mathf.Sqrt(dx * dx + dy * dy));
                }
            return mejor;
        }
        var dentro = new Rect(0, 0, w / (float)S, h / (float)S);
        var lineas = new List<string>();
        foreach (var ruta in RutasAutos(mapa))
        {
            float minimo = 9f, minCurva = 9f;
            for (float s = 0; s < ruta.Largo; s += 0.1f)
            {
                MuestraRuta(ruta.puntos, s, out var c, out _);
                if (!dentro.Contains(c)) continue;
                float d = Distancia(c);
                minimo = Mathf.Min(minimo, d);
                if (Vector2.Distance(c, CentroCurva) < RadioCarrilExterior + 2f && c.x > CentroCurva.x - 0.01f && c.y > CentroCurva.y - 0.01f)
                    minCurva = Mathf.Min(minCurva, d);
            }
            if (minimo < 1f) errores.Add($"{ruta.carril}: el centro del auto pasa a {minimo:0.00} tiles del pasto o camellón (mínimo 1).");
            lineas.Add($"{ruta.carril} {minimo:0.00}" + (minCurva < 9f ? $" (en la vuelta {minCurva:0.00})" : ""));
        }
        return $"Curvas: radio del carril interior {RadioCarrilInterior}, exterior {RadioCarrilExterior} (centro {CentroCurva}); " +
               $"distancia mínima del centro del auto al pasto/camellón: {string.Join(", ", lineas)}.";
    }

    // BloqueoMoto: pasto, jardines, el parque, el lago, el camellón y la base de casas y cercas, en BoxCollider2D
    // (no trigger) hijos del objeto raíz "BloqueoMoto". Las subceldas bloqueadas vecinas se fusionan en
    // rectángulos grandes: primero tramos por fila y luego se estiran por columnas mientras la fila siguiente
    // tenga el mismo tramo completo. Fuera del mapa ya lo cierra BordeMapa. La banqueta alrededor de cada
    // PuntoEntrega queda siempre libre.
    static int CrearBloqueoMoto(Dictionary<string, object> mapa, Dictionary<string, object> tileset, Vector3 origen,
                                out bool[,] bloqueado, out int liberadas)
    {
        BorrarViejos<BloqueoMoto>(NombreBloqueo);
        int S = SubBloqueo;
        bloqueado = PastoYCamellon(mapa, tileset);
        int ancho = bloqueado.GetLength(0) / S, alto = bloqueado.GetLength(1) / S;

        // base de las casas, de Brenn's y de las cercas (con su collider de siempre, en el atlas)
        var props = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Props");
        if (props != null)
            foreach (var caja in props.GetComponentsInChildren<BoxCollider2D>(true))
            {
                string n = caja.gameObject.name;
                if (!(n.StartsWith("casa_") || n.StartsWith("cerca_") || n.StartsWith("empresa_brenns") || n.StartsWith("tienda_") || n.StartsWith("aro_basquet"))) continue;
                var r = CajaMundo(caja);
                MarcarSubceldas(bloqueado, new Rect(r.position - (Vector2)origen, r.size), true);
            }

        // la banqueta frente a cada puerta: la moto tiene que llegar al PuntoEntrega
        liberadas = 0;
        foreach (var p in Lista(mapa, "puntos_entrega").Cast<Dictionary<string, object>>())
        {
            var centro = new Vector2((float)Num(p["x"]), (float)Num(p["y"]));
            for (int x = Mathf.FloorToInt((centro.x - RadioLibrePuerta) * S); x < Mathf.CeilToInt((centro.x + RadioLibrePuerta) * S); x++)
                for (int y = Mathf.FloorToInt((centro.y - RadioLibrePuerta) * S); y < Mathf.CeilToInt((centro.y + RadioLibrePuerta) * S); y++)
                {
                    if (x < 0 || y < 0 || x >= ancho * S || y >= alto * S || !bloqueado[x, y]) continue;
                    if (Vector2.Distance(new Vector2((x + 0.5f) / S, (y + 0.5f) / S), centro) > RadioLibrePuerta) continue;
                    bloqueado[x, y] = false;
                    liberadas++;
                }
        }

        var rects = FusionVoraz(bloqueado);
        var raiz = new GameObject(NombreBloqueo);
        Undo.RegisterCreatedObjectUndo(raiz, "Construir mapa Residencial");
        raiz.transform.position = origen;
        raiz.isStatic = true;
        for (int i = 0; i < rects.Count; i++)
        {
            var r = rects[i];
            var go = new GameObject($"Zona_{i + 1:000}");
            go.transform.SetParent(raiz.transform, false);
            go.isStatic = true;
            var caja = go.AddComponent<BoxCollider2D>();
            caja.isTrigger = false;
            caja.size = new Vector2(r.width, r.height) / S;
            caja.offset = new Vector2(r.x + r.width / 2f, r.y + r.height / 2f) / S;
        }
        raiz.AddComponent<BloqueoMoto>();   // después de los hijos: en Awake toma todos sus colliders
        if (liberadas > 0) Debug.Log($"BloqueoMoto: {liberadas} subceldas liberadas alrededor de los PuntosEntrega.");
        return rects.Count;
    }

    static void MarcarSubceldas(bool[,] grid, Rect r, bool valor)
    {
        int S = SubBloqueo, w = grid.GetLength(0), h = grid.GetLength(1);
        const float eps = 0.01f;   // una caja que apenas toca el borde de una subcelda no la bloquea
        for (int x = Mathf.Max(0, Mathf.FloorToInt((r.xMin + eps) * S)); x < Mathf.Min(w, Mathf.CeilToInt((r.xMax - eps) * S)); x++)
            for (int y = Mathf.Max(0, Mathf.FloorToInt((r.yMin + eps) * S)); y < Mathf.Min(h, Mathf.CeilToInt((r.yMax - eps) * S)); y++)
                grid[x, y] = valor;
    }

    // Fusión voraz: por cada fila, tramos seguidos de celdas bloqueadas; cada tramo se estira hacia arriba
    // mientras la fila siguiente tenga ese tramo completo sin usar
    static List<RectInt> FusionVoraz(bool[,] bloqueado)
    {
        int w = bloqueado.GetLength(0), h = bloqueado.GetLength(1);
        var libre = (bool[,])bloqueado.Clone();
        var rects = new List<RectInt>();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w;)
            {
                if (!libre[x, y]) { x++; continue; }
                int x1 = x;
                while (x1 < w && libre[x1, y]) x1++;
                int y1 = y + 1;
                while (y1 < h && Enumerable.Range(x, x1 - x).All(i => libre[i, y1])) y1++;
                for (int j = y; j < y1; j++)
                    for (int i = x; i < x1; i++) libre[i, j] = false;
                rects.Add(new RectInt(x, y, x1 - x, y1 - y));
                x = x1;
            }
        return rects;
    }

    // Pasos donde la moto puede cruzar una calle de lado a lado: columnas (o filas) seguidas con cebra en las
    // que ninguna subcelda de la franja de la calle está bloqueada
    static int PasosCruzables(Dictionary<string, object> mapa, bool[,] bloqueado, bool calleHorizontal, int desde, int hasta)
    {
        int S = SubBloqueo, ancho = (int)Num(mapa["columnas"]), alto = (int)Num(mapa["filas"]);
        int largo = calleHorizontal ? ancho : alto;
        int grupos = 0;
        bool antes = false;
        for (int i = 0; i < largo; i++)
        {
            bool cebra = false, libre = true;
            for (int k = desde; k < hasta && libre; k++)
            {
                int cx = calleHorizontal ? i : k, cy = calleHorizontal ? k : i;
                if ((TileEn(mapa, cx + 0.5f, cy + 0.5f) ?? "").StartsWith("cebra")) cebra = true;
                for (int s = 0; s < S * S && libre; s++)
                    if (bloqueado[cx * S + s % S, cy * S + s / S]) libre = false;
            }
            bool paso = cebra && libre;
            if (paso && !antes) grupos++;
            antes = paso;
        }
        return grupos;
    }

    // Mapas con "cruces_moto" (el Comercial): la franja del camellón (y0..y1) solo se puede cruzar en moto por
    // tramos de columnas sin ninguna subcelda bloqueada; tienen que ser exactamente los "esperados" y cada uno
    // dentro de la zona de un cruce con semáforo
    static string RevisarCrucesMoto(Dictionary<string, object> mapa, bool[,] bloqueado)
    {
        var d = (Dictionary<string, object>)mapa["cruces_moto"];
        int S = SubBloqueo, y0 = (int)Num(d["y0"]), y1 = (int)Num(d["y1"]), esperados = (int)Num(d["esperados"]);
        int ancho = bloqueado.GetLength(0);
        var zonas = ZonasCruce(mapa).Values.ToList();
        var tramos = new List<(float a, float b)>();
        int inicio = -1;
        for (int x = 0; x <= ancho; x++)
        {
            bool libre = x < ancho;
            for (int y = y0 * S; y < y1 * S && libre; y++)
                if (bloqueado[x, y]) libre = false;
            if (libre && inicio < 0) inicio = x;
            if (!libre && inicio >= 0) { tramos.Add((inicio / (float)S, x / (float)S)); inicio = -1; }
        }
        var fuera = tramos.Where(t => !zonas.Any(z => z.xMin <= t.a + 1e-3f && t.b <= z.xMax + 1e-3f)).ToList();
        string texto = string.Join(", ", tramos.Select(t => $"x {t.a:0.##}-{t.b:0.##}"));
        if (tramos.Count != esperados)
            Debug.LogError($"La avenida se cruza en moto por {tramos.Count} tramos ({texto}); debían ser {esperados}, los cruces con semáforo.");
        if (fuera.Count > 0)
            Debug.LogError("Tramos para cruzar la avenida en moto fuera de un cruce con semáforo: " + string.Join(", ", fuera.Select(t => $"x {t.a:0.##}-{t.b:0.##}")));
        return $"la avenida se cruza en moto solo por {tramos.Count} tramos ({texto}), {tramos.Count - fuera.Count} dentro de un cruce con semáforo. ";
    }

    // Recorrido de la moto (BFS por la rejilla de 0.1 tiles) desde la banqueta frente a la puerta de Brenn's
    // hasta cada PuntoEntrega, solo por celdas con hueco >= HuecoMoto. Estorban el BloqueoMoto y todo collider
    // fijo (props, BordeMapa, la dueña); no los autos, peatones ni clientas, que se mueven. Para cada punto
    // se busca la ruta con el hueco más ancho posible y se informa su tramo más angosto.
    static string AnalizarRutasMoto(Dictionary<string, object> mapa, Vector3 origen, bool[,] bloqueado,
                                    out List<(string punto, bool alcanza, float hueco)> tabla)
    {
        int F = CeldasRecorrido;
        var obst = ObstaculosFijos(mapa, origen, bloqueado, out int W, out int H, out int fijos);

        // hueco de cada celda = 2 x distancia a lo más cercano que estorba (transformada de distancia exacta)
        var d2 = DistanciaCuadrada(obst, W, H);
        int Nivel(int i) => obst[i] ? -1 : Mathf.FloorToInt((2f * (Mathf.Sqrt(d2[i]) - 0.5f) / F) * 100f + 0.5f);   // en centésimas
        int minimo = Mathf.RoundToInt(HuecoMoto * 100f);
        var nivel = new int[W * H];
        int maxNivel = 0;
        for (int i = 0; i < W * H; i++) { nivel[i] = Nivel(i); maxNivel = Mathf.Max(maxNivel, nivel[i]); }

        int Celda(Vector2 p) => (Mathf.FloorToInt(p.y * F) + 1) * W + Mathf.FloorToInt(p.x * F) + 1;
        var spawn = (Dictionary<string, object>)mapa["spawn"];
        Vector2 salida = new Vector2((float)Num(spawn["x"]), (float)Num(spawn["y"]));
        int inicio = Celda(salida);

        // la ruta de hueco más ancho: se van "encendiendo" las celdas de la más holgada a la más angosta
        // (union-find) y el hueco de cada punto es el nivel en que queda unido a la salida
        var puntos = Lista(mapa, "puntos_entrega").Cast<Dictionary<string, object>>().ToList();
        int nodos = W * H + puntos.Count;
        var padre = Enumerable.Range(0, nodos).ToArray();
        int Raiz(int a) { while (padre[a] != a) { padre[a] = padre[padre[a]]; a = padre[a]; } return a; }
        void Unir(int a, int b) { a = Raiz(a); b = Raiz(b); if (a != b) padre[a] = b; }

        var destinoDe = new Dictionary<int, List<int>>();   // celda -> puntos cuya ventana (0.5 tiles) la incluye
        for (int k = 0; k < puntos.Count; k++)
        {
            var c = new Vector2((float)Num(puntos[k]["x"]), (float)Num(puntos[k]["y"]));
            for (int dx = -F / 2; dx <= F / 2; dx++)
                for (int dy = -F / 2; dy <= F / 2; dy++)
                {
                    int i = Celda(c + new Vector2(dx, dy) / F);
                    if (!destinoDe.TryGetValue(i, out var l)) destinoDe[i] = l = new List<int>();
                    l.Add(k);
                }
        }

        var porNivel = new List<int>[maxNivel + 1];
        for (int i = 0; i < W * H; i++)
            if (nivel[i] >= minimo) (porNivel[nivel[i]] ??= new List<int>()).Add(i);
        var activa = new bool[W * H];
        var hueco = Enumerable.Repeat(-1f, puntos.Count).ToArray();
        int pendientes = puntos.Count;
        for (int lv = maxNivel; lv >= minimo && pendientes > 0; lv--)
        {
            if (porNivel[lv] == null) continue;
            foreach (int i in porNivel[lv])
            {
                activa[i] = true;
                foreach (int j in new[] { i - 1, i + 1, i - W, i + W })
                    if (activa[j]) Unir(i, j);
                if (destinoDe.TryGetValue(i, out var ks))
                    foreach (int k in ks) Unir(i, W * H + k);
            }
            if (!activa[inicio]) continue;
            for (int k = 0; k < puntos.Count; k++)
                if (hueco[k] < 0 && Raiz(W * H + k) == Raiz(inicio)) { hueco[k] = lv / 100f; pendientes--; }
        }

        tabla = puntos.Select((p, k) => ((string)p["nombre"], hueco[k] >= 0, hueco[k])).ToList();
        string inicioTxt = activa[inicio] ? "" : $" (OJO: la salida ({salida.x}, {salida.y}) no tiene {HuecoMoto} tiles de hueco)";
        var sb = new StringBuilder();
        sb.AppendLine($"Rutas de la moto desde la banqueta frente a Brenn's ({salida.x}, {salida.y}){inicioTxt}, hueco mínimo {HuecoMoto}, " +
                      $"{fijos} colliders fijos + BloqueoMoto:");
        sb.AppendLine("Punto de entrega | Alcanzable | Hueco más angosto de la ruta");
        foreach (var (punto, alcanza, h) in tabla)
            sb.AppendLine($"{punto} | {(alcanza ? "sí" : "NO")} | {(alcanza ? h.ToString("0.00") + " tiles" : "-")}");
        return sb.ToString().TrimEnd();
    }

    // Rejilla de 1/CeldasRecorrido tiles con lo que estorba, con un anillo de celdas bloqueadas alrededor del
    // mapa: el BloqueoMoto ya marcado en subceldas (si se pasa) y todo collider fijo (props, LotesSolidos,
    // BordeMapa, la dueña); no los autos, peatones, clientas ni el repartidor, que se mueven.
    static bool[] ObstaculosFijos(Dictionary<string, object> mapa, Vector3 origen, bool[,] bloqueado, out int W, out int H, out int fijos)
    {
        int F = CeldasRecorrido, S = SubBloqueo;
        int ancho = (int)Num(mapa["columnas"]), alto = (int)Num(mapa["filas"]);
        W = ancho * F + 2; H = alto * F + 2;
        int w = W, h = H;
        var obst = new bool[W * H];
        for (int x = 0; x < W; x++) { obst[x] = true; obst[(H - 1) * W + x] = true; }
        for (int y = 0; y < H; y++) { obst[y * W] = true; obst[y * W + W - 1] = true; }
        if (bloqueado != null)
            for (int x = 0; x < ancho * F; x++)
                for (int y = 0; y < alto * F; y++)
                    if (bloqueado[x * S / F, y * S / F]) obst[(y + 1) * W + x + 1] = true;

        Physics2D.SyncTransforms();
        fijos = 0;
        foreach (var c in UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!c.enabled || c.isTrigger) continue;
            if (c.GetComponentInParent<BloqueoMoto>() != null || c.GetComponentInParent<TrafficCar>() != null ||
                c.GetComponentInParent<Peaton>() != null || c.GetComponentInParent<PlayerController>() != null ||
                c.GetComponentInParent<Cliente>() != null) continue;
            Rect r = c is BoxCollider2D b ? CajaMundo(b) : new Rect(c.bounds.min, c.bounds.size);
            r.position -= (Vector2)origen;
            fijos++;
            for (int x = Mathf.Max(0, Mathf.FloorToInt(r.xMin * F)); x < Mathf.Min(ancho * F, Mathf.CeilToInt(r.xMax * F)); x++)
                for (int y = Mathf.Max(0, Mathf.FloorToInt(r.yMin * F)); y < Mathf.Min(alto * F, Mathf.CeilToInt(r.yMax * F)); y++)
                    obst[(y + 1) * w + x + 1] = true;
        }
        return obst;
    }

    // ------------------------------------------------------------------ patios privados
    // LotesSolidos: lo que cierra el patio de cada casa (setos laterales y traseros, la cerca del frente con el
    // hueco del portón cerrado y la orilla de la cochera que da al patio y su remate). Fuera de BloqueoMoto:
    // estorba siempre, a pie y en moto. Las cajas vienen del JSON ("lotes_solidos", pixel_town/props_setos.py).
    // La clienta sale por el portón sin colisiones; banquetas, cocheras, parques, plaza y bosque siguen libres.
    const string NombreLotes = "LotesSolidos";
    const float HuecoAPie = 0.6f;          // el pie del repartidor mide 0.6 de ancho
    const float HuecoFiltracion = 0.3f;    // y 0.3 de alto: por un hueco así ya se colaría de lado
    const float RadioEntrega = 2.2f;       // DeliveryPoint.radioEntrega

    static int CrearLotesSolidos(Dictionary<string, object> mapa, Vector3 origen)
    {
        foreach (var viejo in SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name == NombreLotes))
            Undo.DestroyObjectImmediate(viejo);
        var raiz = new GameObject(NombreLotes);
        Undo.RegisterCreatedObjectUndo(raiz, "Construir mapa Residencial");
        raiz.transform.position = origen;
        raiz.isStatic = true;
        var cuenta = new Dictionary<string, int>();
        foreach (var c in Lista(mapa, "lotes_solidos").Cast<Dictionary<string, object>>())
        {
            string tipo = (string)c["tipo"];
            cuenta[tipo] = cuenta.TryGetValue(tipo, out int n) ? n + 1 : 1;
            var go = new GameObject($"{char.ToUpper(tipo[0])}{tipo.Substring(1)}_{cuenta[tipo]:000}");
            go.transform.SetParent(raiz.transform, false);
            go.isStatic = true;
            var caja = go.AddComponent<BoxCollider2D>();
            var r = CajaJson(c);
            caja.size = r.size;
            caja.offset = r.center;
        }
        return raiz.transform.childCount;
    }

    static Rect CajaJson(object o)
    {
        var c = (Dictionary<string, object>)o;
        return new Rect((float)Num(c["x"]), (float)Num(c["y"]), (float)Num(c["ancho"]), (float)Num(c["alto"]));
    }

    // A pie (sin BloqueoMoto), desde el spawn y con los colliders fijos: ninguna celda alcanzable cae dentro de
    // un patio (fuera de su cochera) aunque el hueco sea de HuecoFiltracion, y con el ancho del pie se llega a
    // menos de RadioEntrega de cada PuntoEntrega (se entrega desde la banqueta).
    static string AnalizarAPie(Dictionary<string, object> mapa, Vector3 origen, List<string> errores)
    {
        int F = CeldasRecorrido;
        var obst = ObstaculosFijos(mapa, origen, null, out int W, out int H, out int fijos);
        var d2 = DistanciaCuadrada(obst, W, H);
        int Celda(Vector2 p) => (Mathf.FloorToInt(p.y * F) + 1) * W + Mathf.FloorToInt(p.x * F) + 1;
        Vector2 Centro(int i) => new Vector2((i % W - 1 + 0.5f) / F, (i / W - 1 + 0.5f) / F);
        var spawn = (Dictionary<string, object>)mapa["spawn"];
        int inicio = Celda(new Vector2((float)Num(spawn["x"]), (float)Num(spawn["y"])));

        bool[] Alcanzables(float hueco)
        {
            var libre = new bool[W * H];
            for (int i = 0; i < W * H; i++) libre[i] = !obst[i] && 2f * (Mathf.Sqrt(d2[i]) - 0.5f) / F >= hueco - 1e-4f;
            var visto = new bool[W * H];
            if (!libre[inicio]) return visto;
            var cola = new Queue<int>();
            cola.Enqueue(inicio);
            visto[inicio] = true;
            while (cola.Count > 0)
            {
                int i = cola.Dequeue();
                foreach (int j in new[] { i - 1, i + 1, i - W, i + W })
                    if (libre[j] && !visto[j]) { visto[j] = true; cola.Enqueue(j); }
            }
            return visto;
        }

        // filtraciones a los patios
        var fino = Alcanzables(HuecoFiltracion);
        var patios = Lista(mapa, "patios").Cast<Dictionary<string, object>>().ToList();
        var abiertos = new List<string>();
        foreach (var p in patios)
        {
            Rect patio = CajaJson(p["patio"]), cochera = CajaJson(p["cochera"]);
            int dentro = 0;
            Vector2 ejemplo = default;
            for (int x = Mathf.FloorToInt(patio.xMin * F); x < Mathf.CeilToInt(patio.xMax * F); x++)
                for (int y = Mathf.FloorToInt(patio.yMin * F); y < Mathf.CeilToInt(patio.yMax * F); y++)
                {
                    int i = (y + 1) * W + x + 1;
                    var c = Centro(i);
                    if (!fino[i] || !patio.Contains(c) || cochera.Contains(c)) continue;
                    if (dentro++ == 0) ejemplo = c;
                }
            if (dentro > 0) abiertos.Add($"casa {Num(p["casa"])} ({dentro} celdas, p. ej. ({ejemplo.x:0.0}, {ejemplo.y:0.0}))");
        }
        if (!fino[inicio]) errores.Add("A pie: el spawn está encerrado.");
        if (abiertos.Count > 0) errores.Add("A pie se entra a patios privados: " + string.Join("; ", abiertos));

        // entregas desde la banqueta
        var pie = Alcanzables(HuecoAPie);
        var puntos = Lista(mapa, "puntos_entrega").Cast<Dictionary<string, object>>().ToList();
        float peor = 0f;
        var lejos = new List<string>();
        foreach (var p in puntos)
        {
            var c = new Vector2((float)Num(p["x"]), (float)Num(p["y"]));
            float mejor = float.MaxValue;
            for (int x = Mathf.FloorToInt((c.x - RadioEntrega) * F); x <= Mathf.CeilToInt((c.x + RadioEntrega) * F); x++)
                for (int y = Mathf.FloorToInt((c.y - RadioEntrega) * F); y <= Mathf.CeilToInt((c.y + RadioEntrega) * F); y++)
                {
                    int i = (y + 1) * W + x + 1;
                    if (i < 0 || i >= W * H || !pie[i]) continue;
                    mejor = Mathf.Min(mejor, Vector2.Distance(Centro(i), c));
                }
            if (mejor > RadioEntrega) lejos.Add((string)p["nombre"]);
            else peor = Mathf.Max(peor, mejor);
        }
        if (lejos.Count > 0) errores.Add($"A pie no se llega a {RadioEntrega} de: " + string.Join(", ", lejos));
        return $"A pie (hueco {HuecoAPie}, {fijos} colliders fijos, sin BloqueoMoto): {puntos.Count - lejos.Count} de {puntos.Count} " +
               $"puntos de entrega a menos de {RadioEntrega} desde la banqueta (el más lejano a {peor:0.00}); " +
               $"{patios.Count - abiertos.Count} de {patios.Count} patios cerrados (sin huecos de {HuecoFiltracion}).";
    }

    // Revisa LotesSolidos ya armado: aparte de BloqueoMoto, cajas fijas, encendidas y no trigger, una por cada
    // caja del JSON
    static string RevisarLotesSolidos(Dictionary<string, object> mapa, List<string> errores)
    {
        var raiz = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == NombreLotes);
        if (raiz == null) { errores.Add($"No hay objeto {NombreLotes}."); return $"{NombreLotes}: falta"; }
        var cajas = raiz.GetComponentsInChildren<BoxCollider2D>(true);
        int esperadas = Lista(mapa, "lotes_solidos").Count;
        if (cajas.Length != esperadas) errores.Add($"{NombreLotes} tiene {cajas.Length} cajas y el JSON {esperadas}.");
        if (raiz.GetComponentInParent<BloqueoMoto>() != null || raiz.GetComponentInChildren<BloqueoMoto>(true) != null)
            errores.Add($"{NombreLotes} no debe ir dentro de BloqueoMoto.");
        if (cajas.Any(c => c.isTrigger || !c.enabled || !c.gameObject.activeInHierarchy))
            errores.Add($"{NombreLotes} tiene cajas trigger o apagadas.");
        var tipos = cajas.GroupBy(c => c.name.Split('_')[0]).Select(g => $"{g.Count()} {g.Key.ToLower()}");
        return $"{NombreLotes}: {cajas.Length} cajas ({string.Join(", ", tipos)}), siempre activas.";
    }

    // Transformada de distancia euclidiana exacta (Felzenszwalb): distancia al cuadrado, en celdas, de cada
    // celda a la celda que estorba más cercana
    static float[] DistanciaCuadrada(bool[] obst, int W, int H)
    {
        const float Inf = 1e20f;
        var d = new float[W * H];
        for (int i = 0; i < d.Length; i++) d[i] = obst[i] ? 0f : Inf;
        var f = new float[Math.Max(W, H)];
        var res = new float[Math.Max(W, H)];
        var v = new int[Math.Max(W, H)];
        var z = new float[Math.Max(W, H) + 1];
        for (int x = 0; x < W; x++)
        {
            for (int y = 0; y < H; y++) f[y] = d[y * W + x];
            Dt1D(f, H, res, v, z);
            for (int y = 0; y < H; y++) d[y * W + x] = res[y];
        }
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++) f[x] = d[y * W + x];
            Dt1D(f, W, res, v, z);
            for (int x = 0; x < W; x++) d[y * W + x] = res[x];
        }
        return d;
    }

    static void Dt1D(float[] f, int n, float[] d, int[] v, float[] z)
    {
        int k = 0;
        v[0] = 0;
        z[0] = float.NegativeInfinity;
        z[1] = float.PositiveInfinity;
        for (int q = 1; q < n; q++)
        {
            float s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2f * q - 2f * v[k]);
            while (s <= z[k])
            {
                k--;
                s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2f * q - 2f * v[k]);
            }
            k++;
            v[k] = q;
            z[k] = s;
            z[k + 1] = float.PositiveInfinity;
        }
        k = 0;
        for (int q = 0; q < n; q++)
        {
            while (z[k + 1] < q) k++;
            d[q] = (q - v[k]) * (q - v[k]) + f[v[k]];
        }
    }

    // Revisa el BloqueoMoto ya armado: que exista, que sus colliders no sean trigger y que ninguno tape un
    // PuntoEntrega ni el spawn
    static string RevisarBloqueoMoto(Dictionary<string, object> mapa, List<string> errores)
    {
        var raiz = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == NombreBloqueo);
        if (raiz == null || raiz.GetComponent<BloqueoMoto>() == null) { errores.Add("No hay objeto BloqueoMoto con su componente."); return "BloqueoMoto: falta"; }
        var cajas = raiz.GetComponentsInChildren<BoxCollider2D>(true);
        if (cajas.Any(c => c.isTrigger)) errores.Add("BloqueoMoto tiene colliders trigger.");
        if (raiz.GetComponents<Collider2D>().Length > 0) errores.Add("BloqueoMoto no debe tener colliders en la raíz, solo en los hijos.");
        var rects = cajas.Select(CajaMundo).ToList();
        Vector2 o = raiz.transform.position;
        var lugares = Lista(mapa, "puntos_entrega").Cast<Dictionary<string, object>>()
            .Select(p => ((string)p["nombre"], new Vector2((float)Num(p["x"]), (float)Num(p["y"])) + o)).ToList();
        var spawn = (Dictionary<string, object>)mapa["spawn"];
        lugares.Add(("spawn", new Vector2((float)Num(spawn["x"]), (float)Num(spawn["y"])) + o));
        foreach (var (nombre, p) in lugares)
            if (rects.Any(r => r.Contains(p))) errores.Add($"BloqueoMoto tapa {nombre} en {p - o}.");
        return $"BloqueoMoto: {cajas.Length} rectángulos (fusión voraz de subceldas de {1f / SubBloqueo} tiles).";
    }

    static SerializedProperty Campo(SerializedObject obj, string nombre)
    {
        var p = obj.FindProperty(nombre);
        if (p == null) throw new Exception($"{obj.targetObject.GetType().Name} ya no tiene el campo \"{nombre}\".");
        return p;
    }

    // ------------------------------------------------------------------ borde, spawn y entregas

    // Cuatro colliders invisibles justo afuera del mapa para que el repartidor no se salga.
    static void CrearBorde(Dictionary<string, object> mapa, Vector3 origen)
    {
        foreach (var viejo in SceneManager.GetActiveScene().GetRootGameObjects().Where(o => o.name == "BordeMapa"))
            Undo.DestroyObjectImmediate(viejo);

        float ancho = (float)Num(mapa["columnas"]), alto = (float)Num(mapa["filas"]), g = GrosorBorde;
        var borde = new GameObject("BordeMapa");
        Undo.RegisterCreatedObjectUndo(borde, "Construir mapa Residencial");
        borde.transform.position = origen;
        borde.isStatic = true;
        foreach (var (centro, tam) in new[]
                 {
                     (new Vector2(-g / 2, alto / 2), new Vector2(g, alto + 2 * g)),          // izquierda
                     (new Vector2(ancho + g / 2, alto / 2), new Vector2(g, alto + 2 * g)),   // derecha
                     (new Vector2(ancho / 2, -g / 2), new Vector2(ancho + 2 * g, g)),        // abajo
                     (new Vector2(ancho / 2, alto + g / 2), new Vector2(ancho + 2 * g, g)),  // arriba
                 })
        {
            var caja = borde.AddComponent<BoxCollider2D>();
            caja.offset = centro;
            caja.size = tam;
        }
    }

    static Vector3 Punto(Dictionary<string, object> d, Vector3 origen, float z = 0)
    {
        return new Vector3(origen.x + (float)Num(d["x"]), origen.y + (float)Num(d["y"]), z);
    }

    static void ColocarRepartidor(Dictionary<string, object> mapa, Vector3 origen)
    {
        if (!mapa.TryGetValue("spawn", out var s) || !(s is Dictionary<string, object> spawn))
            throw new Exception("Al JSON le falta \"spawn\".");
        // El repartidor arranca en la banqueta frente a la puerta de Brenn's, nunca en el pasto
        string bajo = TileEn(mapa, (float)Num(spawn["x"]), (float)Num(spawn["y"]));
        if (bajo == null || !bajo.StartsWith("banqueta"))
            throw new Exception($"El spawn ({Num(spawn["x"])}, {Num(spawn["y"])}) cae en \"{bajo}\", no en la banqueta.");
        if (mapa.TryGetValue("empresa", out var e) && e is Dictionary<string, object> empresa &&
            Math.Abs(Num(((Dictionary<string, object>)empresa["puerta"])["x"]) - Num(spawn["x"])) > 0.5)
            throw new Exception("El spawn no está frente a la puerta de Brenn's.");
        var jugador = UnityEngine.Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (jugador == null)
        {
            Debug.LogWarning("No hay ningún PlayerController en la escena; no se movió al repartidor.");
            return;
        }
        var pos = Punto(spawn, origen, jugador.transform.position.z);
        Undo.RecordObject(jugador.transform, "Construir mapa Residencial");
        jugador.transform.position = pos;
        // la mochila sin el producto que asomaba (se quitó el 2026-10-21)
        ConstruirMarcadorPedido.LimpiarMochila(jugador);
        // choques de la moto (niveles 2 y 3; en el 1 no hay motos y no hace nada)
        ConstruirImpactoMoto.Configurar(jugador);

        // La cámara arranca encima del repartidor para que no cruce todo el mapa al empezar
        var camara = UnityEngine.Object.FindFirstObjectByType<CameraFollow>(FindObjectsInactive.Include);
        if (camara != null)
        {
            Undo.RecordObject(camara.transform, "Construir mapa Residencial");
            camara.transform.position = new Vector3(pos.x, pos.y + 1.25f, camara.transform.position.z);
        }
    }

    // Borra los puntos de entrega anteriores (los PuntoEntrega_1 hechos a mano y los de una corrida previa)
    // y crea uno por cada puerta, en la banqueta frente al portón.
    static int CrearPuntosEntrega(Dictionary<string, object> mapa, Vector3 origen)
    {
        var viejos = UnityEngine.Object.FindObjectsByType<DeliveryPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(p => p.gameObject)
            .Concat(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .Where(t => t.name.StartsWith("PuntoEntrega_") || (t.parent == null && t.name == "PuntosEntrega"))
                        .Select(t => t.gameObject))
            .Distinct().ToList();
        // solo se destruyen los de más arriba; sus hijos se van con ellos
        foreach (var go in viejos.Where(g => !viejos.Any(o => o != g && g.transform.IsChildOf(o.transform))).ToList())
            Undo.DestroyObjectImmediate(go);

        ConstruirMarcadorPedido.PrepararArte();
        var padre = new GameObject("PuntosEntrega");
        Undo.RegisterCreatedObjectUndo(padre, "Construir mapa Residencial");

        int n = 0;
        foreach (var datos in Lista(mapa, "puntos_entrega").Cast<Dictionary<string, object>>())
        {
            n++;
            var punto = new GameObject((string)datos["nombre"]);
            punto.transform.SetParent(padre.transform, false);
            punto.transform.position = Punto(datos, origen);

            // DeliveryPoint toma el primer hijo como marcador (lo apaga cuando el pedido no está activo):
            // MarcadorPedido debe ser el único, en el centro del punto
            ConstruirMarcadorPedido.Crear(punto.transform);

            punto.AddComponent<DeliveryPoint>();

            string bajo = TileEn(mapa, (float)Num(datos["x"]), (float)Num(datos["y"]));
            if (bajo == null || !(bajo.StartsWith("banqueta") || bajo.StartsWith("plaza")))
                Debug.LogWarning($"{punto.name} cae en \"{bajo}\", no en la banqueta ni en la plaza.", punto);
        }
        int casas = Lista(mapa, "casas").Count;
        if (n != casas)
            throw new Exception($"Hay {casas} casas pero {n} puntos de entrega.");
        return n;
    }

    // ------------------------------------------------------------------ peatones y dueña

    // Todo lo que se corta además del tileset y el atlas: peatones, dueña y jefe, las clientas y la flecha
    static List<HojaPersonaje> HojasPersonajes()
    {
        var hojas = HojasDe(CarpetaPersonajes, ArchivoPersonajes, "generar_personajes.py");
        hojas.AddRange(HojasDe(CarpetaClientas, ArchivoClientas, "generar_clientas.py"));
        hojas.Add(HojaFlecha());
        return hojas;
    }

    // Hojas según su JSON (nombre, rectángulo y pivote abajo-centro de cada celda)
    static List<HojaPersonaje> HojasDe(string carpeta, string archivoJson, string generador)
    {
        string archivo = Path.Combine(carpeta, archivoJson);
        if (!File.Exists(archivo))
            throw new Exception($"No existe {archivo}. Ejecuta pixel_town/personajes/{generador}.");
        var json = (Dictionary<string, object>)Json.Leer(File.ReadAllText(archivo));
        var hojas = new List<HojaPersonaje>();
        foreach (var h in Lista(json, "hojas").Cast<Dictionary<string, object>>())
        {
            string ruta = $"{carpeta}/{h["imagen"]}";
            AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
            var imp = Importador(ruta);
            imp.GetSourceTextureWidthAndHeight(out int ancho, out int alto);
            var tam = new Vector2Int((int)Num(h["ancho"]), (int)Num(h["alto"]));
            if (ancho != tam.x || alto != tam.y)
                throw new Exception($"{ruta} mide {ancho}x{alto} px pero {archivoJson} dice {tam.x}x{tam.y}.");
            hojas.Add(new HojaPersonaje
            {
                ruta = ruta,
                json = archivoJson,
                tam = tam,
                defs = Lista(h, "sprites").Cast<Dictionary<string, object>>()
                    .Select(s => new DefSprite { nombre = (string)s["name"], rect = RectDesdeArriba(s, alto), pivote = PivoteProps })
                    .ToList(),
            });
        }
        return hojas;
    }

    // La flecha guía: un solo sprite de 32x32 con el pivote al centro (gira sobre sí misma)
    static HojaPersonaje HojaFlecha()
    {
        AssetDatabase.ImportAsset(RutaFlecha, ImportAssetOptions.ForceUpdate);
        Importador(RutaFlecha).GetSourceTextureWidthAndHeight(out int ancho, out int alto);
        if (ancho != 32 || alto != 32) throw new Exception($"{RutaFlecha} mide {ancho}x{alto} px, no 32x32.");
        return new HojaPersonaje
        {
            ruta = RutaFlecha,
            json = "32x32, pivote al centro",
            tam = new Vector2Int(32, 32),
            defs = new List<DefSprite> { new DefSprite { nombre = SpriteFlecha, rect = new Rect(0, 0, 32, 32), pivote = new Vector2(0.5f, 0.5f) } },
        };
    }

    static string RutaPersonaje(string nombre) =>
        nombre.StartsWith("clienta_") ? $"{CarpetaClientas}/{nombre}.png" : $"{CarpetaPersonajes}/{nombre}.png";

    // Las 5 columnas (0 quieto, 1-4 caminando) de una fila de la hoja del personaje
    static Sprite[] FilaPersonaje(Dictionary<string, Sprite> sprites, string personaje, string fila)
    {
        return Enumerable.Range(0, 5).Select(c =>
        {
            string n = $"{personaje}_{fila}_{c}";
            if (!sprites.TryGetValue(n, out var s)) throw new Exception($"Falta el sprite {n} en {RutaPersonaje(personaje)}.");
            return s;
        }).ToArray();
    }

    // Mismo orden de capa que los autos de la calle (se ordenan por Y desde el pie)
    static void CapaComoAutos(SpriteRenderer sr)
    {
        var auto = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == NombreAutos)?
                       .GetComponentInChildren<SpriteRenderer>();
        sr.sortingLayerID = auto != null ? auto.sortingLayerID : 0;
        sr.sortingOrder = auto != null ? auto.sortingOrder : 0;
        sr.spriteSortPoint = SpriteSortPoint.Pivot;
    }

    // Un Peaton_N por cada elemento de "peatones": SpriteRenderer, Rigidbody2D cinemático, BoxCollider2D en
    // los pies y el script Peaton con su recorrido (en coordenadas del mundo) y los sprites de su variante.
    // Antes se borran los de una corrida previa y cualquier Peaton suelto, para no duplicarlos.
    static int CrearPeatones(Dictionary<string, object> mapa, Vector3 origen)
    {
        var lista = Lista(mapa, "peatones").Cast<Dictionary<string, object>>().ToList();
        var viejos = UnityEngine.Object.FindObjectsByType<Peaton>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(p => p.gameObject)
            .Concat(SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name == NombrePeatones))
            .Distinct().ToList();
        foreach (var go in viejos.Where(g => !viejos.Any(o => o != g && g.transform.IsChildOf(o.transform))).ToList())
            Undo.DestroyObjectImmediate(go);

        var padre = new GameObject(NombrePeatones);
        Undo.RegisterCreatedObjectUndo(padre, "Construir mapa Residencial");
        padre.transform.position = origen;   // alineado con el origen del Tilemap, igual que los props

        var porVariante = new Dictionary<int, Dictionary<string, Sprite>>();
        foreach (var d in lista)
        {
            int variante = (int)Num(d["variante"]);
            string personaje = $"peaton_{variante}";
            if (!porVariante.TryGetValue(variante, out var sprites))
                porVariante[variante] = sprites = CargarSprites(RutaPersonaje(personaje));

            var puntos = Lista(d, "puntos").Cast<List<object>>()
                .Select(p => new Vector2(origen.x + (float)Num(p[0]), origen.y + (float)Num(p[1]))).ToArray();
            if (puntos.Length < 2) throw new Exception($"{d["nombre"]} tiene menos de dos puntos.");

            var go = new GameObject((string)d["nombre"]);
            go.transform.SetParent(padre.transform, false);
            go.transform.position = new Vector3(puntos[0].x, puntos[0].y, 0);

            var frente = FilaPersonaje(sprites, personaje, "frente");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frente[0];
            CapaComoAutos(sr);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            var pie = go.AddComponent<BoxCollider2D>();
            pie.size = TamPie;
            pie.offset = OffsetPie;

            var peaton = new SerializedObject(go.AddComponent<Peaton>());
            var arr = Campo(peaton, "puntos");
            arr.arraySize = puntos.Length;
            for (int i = 0; i < puntos.Length; i++) arr.GetArrayElementAtIndex(i).vector2Value = puntos[i];
            Campo(peaton, "velocidad").floatValue = (float)Num(d["velocidad"]);
            Campo(peaton, "pausaEnPunto").floatValue = (float)Num(d["pausa"]);
            foreach (var fila in FilasPersonaje)
            {
                var juego = FilaPersonaje(sprites, personaje, fila);
                var p = Campo(peaton, fila);   // los campos se llaman como las filas: frente, espalda, lado
                p.arraySize = juego.Length;
                for (int i = 0; i < juego.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = juego[i];
            }
            peaton.ApplyModifiedPropertiesWithoutUndo();
        }
        return lista.Count;
    }

    // La dueña, fija en la puerta de Brenn's (sin Peaton): solo su sprite de frente y un collider en los pies
    static void ColocarDuena(Dictionary<string, object> mapa, Vector3 origen)
    {
        foreach (var viejo in SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name == NombreDuena))
            Undo.DestroyObjectImmediate(viejo);
        if (!mapa.TryGetValue("duena", out var o) || !(o is Dictionary<string, object> d))
            throw new Exception("Al JSON le falta \"duena\".");
        var go = new GameObject(NombreDuena);
        Undo.RegisterCreatedObjectUndo(go, "Construir mapa Residencial");
        go.transform.position = Punto(d, origen);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = FilaPersonaje(CargarSprites(RutaPersonaje("duena")), "duena", "frente")[0];
        CapaComoAutos(sr);
        var pie = go.AddComponent<BoxCollider2D>();
        pie.size = TamPie;
        pie.offset = OffsetPie;
    }

    static string RevisarPeatones(Dictionary<string, object> mapa, List<string> errores)
    {
        var origen = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Props")?.transform.position ?? Vector3.zero;
        var lista = Lista(mapa, "peatones").Cast<Dictionary<string, object>>().ToList();
        var padre = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == NombrePeatones);
        var enEscena = UnityEngine.Object.FindObjectsByType<Peaton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (padre == null) errores.Add($"No hay objeto \"{NombrePeatones}\" en la escena.");
        if (enEscena.Length != lista.Count) errores.Add($"Hay {enEscena.Length} Peaton en la escena y el JSON trae {lista.Count}.");
        var entregas = Lista(mapa, "puntos_entrega").Cast<Dictionary<string, object>>()
            .Select(p => new Vector2((float)Num(p["x"]), (float)Num(p["y"]))).ToList();
        int bien = 0;
        foreach (var d in lista)
        {
            string nombre = (string)d["nombre"];
            var go = padre != null ? padre.transform.Find(nombre) : null;
            var p = go != null ? go.GetComponent<Peaton>() : null;
            if (p == null) { errores.Add($"{nombre}: no existe o no tiene Peaton."); continue; }
            int antes = errores.Count;
            string personaje = $"peaton_{(int)Num(d["variante"])}";
            var so = new SerializedObject(p);
            foreach (var fila in FilasPersonaje)
            {
                var arr = so.FindProperty(fila);
                if (arr.arraySize != 5) { errores.Add($"{nombre}: \"{fila}\" tiene {arr.arraySize} sprites, no 5."); continue; }
                for (int i = 0; i < 5; i++)
                {
                    var s = arr.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
                    if (s == null || s.name != $"{personaje}_{fila}_{i}")
                        errores.Add($"{nombre}: {fila}[{i}] es {(s == null ? "null" : s.name)}, debía ser {personaje}_{fila}_{i}.");
                }
            }
            var pts = so.FindProperty("puntos");
            var esperados = Lista(d, "puntos").Cast<List<object>>().ToList();
            if (pts.arraySize != esperados.Count) errores.Add($"{nombre}: {pts.arraySize} puntos, el JSON trae {esperados.Count}.");
            else
                for (int i = 0; i < esperados.Count; i++)
                {
                    var e = new Vector2(origen.x + (float)Num(esperados[i][0]), origen.y + (float)Num(esperados[i][1]));
                    if (Vector2.Distance(pts.GetArrayElementAtIndex(i).vector2Value, e) > 1e-3f)
                        errores.Add($"{nombre}: el punto {i} no coincide con el JSON.");
                    var local = e - (Vector2)origen;
                    if (entregas.Any(q => Vector2.Distance(q, local) < 0.75f))
                        errores.Add($"{nombre}: el punto {i} tapa un PuntoEntrega.");
                }
            float vel = so.FindProperty("velocidad").floatValue, pausa = so.FindProperty("pausaEnPunto").floatValue;
            if (vel < 1f || vel > 1.5f) errores.Add($"{nombre}: velocidad {vel} fuera de 1 a 1.5.");
            if (pausa < 1f || pausa > 3f) errores.Add($"{nombre}: pausaEnPunto {pausa} fuera de 1 a 3.");
            var rb = go.GetComponent<Rigidbody2D>();
            var caja = go.GetComponent<BoxCollider2D>();
            if (rb == null || rb.bodyType != RigidbodyType2D.Kinematic) errores.Add($"{nombre}: el Rigidbody2D no es cinemático.");
            if (caja == null || caja.size != TamPie || caja.offset != OffsetPie) errores.Add($"{nombre}: el collider de los pies no es 0.6 x 0.3 en (0, 0.15).");
            if (errores.Count == antes) bien++;
        }
        var duena = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == NombreDuena);
        string textoDuena;
        if (duena == null) { errores.Add("No está la dueña en la escena."); textoDuena = "falta la dueña"; }
        else
        {
            var sr = duena.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null || sr.sprite.name != "duena_frente_0") errores.Add("La dueña no tiene el sprite duena_frente_0.");
            if (duena.GetComponent<Peaton>() != null) errores.Add("La dueña no debe llevar Peaton.");
            textoDuena = $"dueña fija en ({duena.transform.position.x - origen.x:0.##}, {duena.transform.position.y - origen.y:0.##})";
        }
        int cruzan = lista.Count(d => (string)d["tipo"] == "cruce");
        return $"Peatones: {bien} de {lista.Count} bien armados ({lista.Count - cruzan} en banqueta, {cruzan} cruzando por las cebras); {textoDuena}.";
    }

    // ------------------------------------------------------------------ clientas y flecha guía

    static void BorrarViejos<T>(string nombreRaiz) where T : Component
    {
        var viejos = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(c => c.gameObject)
            .Concat(SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name == nombreRaiz))
            .Distinct().ToList();
        // solo se destruyen los de más arriba; sus hijos se van con ellos
        foreach (var go in viejos.Where(g => !viejos.Any(o => o != g && g.transform.IsChildOf(o.transform))).ToList())
            Undo.DestroyObjectImmediate(go);
    }

    static Vector2 PuntoMundo(object o, Vector3 origen)
    {
        var d = (Dictionary<string, object>)o;
        return new Vector2(origen.x + (float)Num(d["x"]), origen.y + (float)Num(d["y"]));
    }

    // Material del globo: el de la fuente por defecto de TMP con contorno oscuro. Es un asset propio, así
    // los globos no crean materiales sueltos dentro de la escena y reconstruir deja el mismo resultado.
    static (TMP_FontAsset fuente, Material material) MaterialGlobo()
    {
        var fuente = FuenteProyecto.Fuente();          // la fuente pixel del proyecto, la misma de la interfaz
        if (fuente == null) throw new Exception($"No encuentro la fuente pixel {FuenteProyecto.RutaFuente}.");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialGlobo);
        if (mat == null)
        {
            mat = new Material(fuente.material) { name = "GloboTexto" };
            AssetDatabase.CreateAsset(mat, RutaMaterialGlobo);
        }
        else
        {
            mat.shader = fuente.material.shader;
            mat.CopyPropertiesFromMaterial(fuente.material);
        }
        mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, ContornoLetraGlobo);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, ContornoGlobo);
        mat.SetFloat(ShaderUtilities.ID_FaceDilate, ContornoLetraGlobo / 2);   // el contorno va por fuera del trazo
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return (fuente, mat);
    }

    // Una clienta por casa (una por PuntoEntrega): sale de la puerta, espera en el camino de piedras y vuelve.
    // SpriteRenderer con la capa de los peatones, sin collider, el script Cliente y un hijo "Globo" con
    // TextMeshPro 3D, desactivado (Cliente lo enciende cuando habla).
    static int CrearClientes(Dictionary<string, object> mapa, Vector3 origen)
    {
        BorrarViejos<Cliente>(NombreClientes);
        var lista = Lista(mapa, "clientes").Cast<Dictionary<string, object>>().ToList();
        var puntos = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "PuntosEntrega");
        if (puntos == null) throw new Exception("No hay PuntosEntrega en la escena; se crean antes que las clientas.");
        var (fuente, material) = MaterialGlobo();

        var padre = new GameObject(NombreClientes);
        Undo.RegisterCreatedObjectUndo(padre, "Construir mapa Residencial");
        padre.transform.position = origen;

        var porPersonaje = new Dictionary<string, Dictionary<string, Sprite>>();
        foreach (var d in lista)
        {
            string personaje = PersonajeCliente(d);
            if (!porPersonaje.TryGetValue(personaje, out var sprites))
                porPersonaje[personaje] = sprites = CargarSprites(RutaPersonaje(personaje));
            var punto = puntos.transform.Find((string)d["punto"])?.GetComponent<DeliveryPoint>();
            if (punto == null) throw new Exception($"{d["nombre"]}: no encuentro {d["punto"]} con DeliveryPoint.");
            var puerta = PuntoMundo(d["puerta"], origen);
            var espera = PuntoMundo(d["espera"], origen);

            var go = new GameObject((string)d["nombre"]);
            go.transform.SetParent(padre.transform, false);
            go.transform.position = new Vector3(puerta.x, puerta.y, 0);   // Cliente la esconde ahí al empezar
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = FilaPersonaje(sprites, personaje, "frente")[0];
            CapaComoAutos(sr);                                          // la misma capa que los peatones

            var globoGo = new GameObject("Globo");
            globoGo.transform.SetParent(go.transform, false);
            var globo = globoGo.AddComponent<TextMeshPro>();
            globo.font = fuente;
            globo.fontSharedMaterial = material;
            globo.text = "¡Mi pedido!";
            globo.fontSize = TamLetraGlobo;
            globo.color = CremaGlobo;
            globo.alignment = TextAlignmentOptions.Center;
            globo.textWrappingMode = TextWrappingModes.NoWrap;
            globo.rectTransform.pivot = new Vector2(0.5f, 0f);          // la caja empieza arriba de la cabeza y crece hacia arriba
            globo.rectTransform.sizeDelta = new Vector2(4f, 1f);
            globo.rectTransform.localPosition = new Vector3(0f, AlturaGlobo, 0f);
            var rend = globo.GetComponent<MeshRenderer>();
            rend.sortingLayerID = sr.sortingLayerID;
            rend.sortingOrder = OrdenGlobo;
            globoGo.SetActive(false);

            var cliente = new SerializedObject(go.AddComponent<Cliente>());
            Campo(cliente, "punto").objectReferenceValue = punto;
            Campo(cliente, "puerta").vector2Value = puerta;
            Campo(cliente, "espera").vector2Value = espera;
            Campo(cliente, "velocidad").floatValue = VelocidadClienta;
            Campo(cliente, "globo").objectReferenceValue = globo;
            foreach (var fila in FilasPersonaje)
            {
                var juego = FilaPersonaje(sprites, personaje, fila);
                var p = Campo(cliente, fila);
                p.arraySize = juego.Length;
                for (int i = 0; i < juego.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = juego[i];
            }
            cliente.ApplyModifiedPropertiesWithoutUndo();
        }
        return lista.Count;
    }

    // Quién atiende: "personaje" si el JSON lo trae (en el Comercial también hay dueños, peaton_N), si no clienta_N
    static string PersonajeCliente(Dictionary<string, object> d) =>
        d.TryGetValue("personaje", out var p) && p is string s ? s : $"clienta_{(int)Num(d["variante"])}";

    // El Canvas del HUD: el que tiene TextoPedidos o PanelFinal (el mismo criterio que ConstruirInterfaz)
    static Canvas CanvasHUD()
    {
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(c => c.isRootCanvas).ToList();
        return canvases.FirstOrDefault(c => c.GetComponentsInChildren<RectTransform>(true)
                   .Any(r => r.name == "TextoPedidos" || r.name == "PanelFinal"))
               ?? canvases.FirstOrDefault(c => c.name == "Canvas");
    }

    static RectTransform HijoUI(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Construir mapa Residencial");
        go.layer = padre.gameObject.layer;
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
    }

    // La guía del pedido actual en el HUD: píldora arriba a la izquierda (debajo de PlacaPedidos) con la flecha
    // a la izquierda y la distancia a la derecha. GuiaPedido gira la flecha, escribe la distancia y apaga el
    // CanvasGroup cuando no hay pedido. La flecha del mundo es aparte (ConstruirMarcadorPedido.CrearFlecha).
    static void CrearGuiaPedido()
    {
        BorrarViejos<GuiaPedido>(NombreGuia);

        var canvas = CanvasHUD();
        if (canvas == null) throw new Exception("No encuentro el Canvas del HUD (con TextoPedidos o PanelFinal). Ejecuta Construir interfaz.");
        var pildora = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprits/UI/boton_pill_normal.png")
                      ?? throw new Exception("Falta el sprite Assets/Sprits/UI/boton_pill_normal.png.");
        var spriteFlecha = CargarSprites(RutaFlecha).TryGetValue(SpriteFlecha, out var s) ? s
                           : throw new Exception($"Falta el sprite {SpriteFlecha} en {RutaFlecha}.");

        var guia = HijoUI(NombreGuia, canvas.transform);
        guia.anchorMin = guia.anchorMax = guia.pivot = new Vector2(0, 1);
        guia.anchoredPosition = PosGuia;
        guia.sizeDelta = TamGuia;
        var fondo = guia.gameObject.AddComponent<UnityEngine.UI.Image>();
        fondo.sprite = pildora;
        fondo.type = UnityEngine.UI.Image.Type.Sliced;
        fondo.pixelsPerUnitMultiplier = canvas.referencePixelsPerUnit / (pildora.pixelsPerUnit * EscalaUI);
        fondo.raycastTarget = false;
        var grupo = guia.gameObject.AddComponent<CanvasGroup>();
        grupo.interactable = false;
        grupo.blocksRaycasts = false;

        var flecha = HijoUI("Flecha", guia);
        flecha.anchorMin = flecha.anchorMax = new Vector2(0, 0.5f);
        flecha.pivot = new Vector2(0.5f, 0.5f);                     // gira sobre su centro
        flecha.anchoredPosition = new Vector2(XFlechaUI, EscalaUI / 2f);   // centrada sobre la cara de la píldora, no el canto
        flecha.sizeDelta = new Vector2(LadoFlechaUI, LadoFlechaUI);
        var imgFlecha = flecha.gameObject.AddComponent<UnityEngine.UI.Image>();
        imgFlecha.sprite = spriteFlecha;
        imgFlecha.preserveAspect = true;
        imgFlecha.raycastTarget = false;

        var rtTexto = HijoUI("TextoDistancia", guia);
        rtTexto.anchorMin = Vector2.zero;
        rtTexto.anchorMax = Vector2.one;
        rtTexto.pivot = new Vector2(0.5f, 0.5f);
        // el producto del pedido, el mismo ícono de la burbuja del marcador
        var icono = HijoUI("IconoProducto", guia);
        icono.anchorMin = icono.anchorMax = new Vector2(0, 0.5f);
        icono.pivot = new Vector2(0.5f, 0.5f);
        icono.anchoredPosition = new Vector2(XIconoUI, EscalaUI / 2f);
        icono.sizeDelta = new Vector2(LadoIconoUI, LadoIconoUI);
        var catalogo = ConstruirMarcadorPedido.Catalogo();
        var imgIcono = icono.gameObject.AddComponent<UnityEngine.UI.Image>();
        imgIcono.sprite = catalogo != null ? catalogo.Icono(0) : null;   // GuiaPedido lo cambia al jugar
        imgIcono.preserveAspect = true;
        imgIcono.raycastTarget = false;

        rtTexto.offsetMin = new Vector2(XIconoUI + LadoIconoUI / 2 + 2 * EscalaUI, 3 * EscalaUI);
        rtTexto.offsetMax = new Vector2(-6 * EscalaUI, -1 * EscalaUI);
        var texto = rtTexto.gameObject.AddComponent<TextMeshProUGUI>();
        texto.text = "0 m";                                          // GuiaPedido lo reemplaza al jugar
        // como todo texto sobre una pastilla rosa: fuente pixel, #2A1F40, sin contorno, mínimo 36
        FuenteProyecto.EstiloPastilla(texto);

        var so = new SerializedObject(guia.gameObject.AddComponent<GuiaPedido>());
        Campo(so, "flecha").objectReferenceValue = flecha;
        Campo(so, "textoDistancia").objectReferenceValue = texto;
        Campo(so, "grupo").objectReferenceValue = grupo;
        Campo(so, "iconoProducto").objectReferenceValue = imgIcono;
        Campo(so, "catalogo").objectReferenceValue = catalogo;
        so.ApplyModifiedPropertiesWithoutUndo();

        // antes que PanelFinal, para que la pantalla final la tape
        var panel = canvas.transform.Find("PanelFinal");
        if (panel != null) guia.SetSiblingIndex(panel.GetSiblingIndex());
    }

    static string RevisarClientes(Dictionary<string, object> mapa, List<string> errores)
    {
        var origen = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Props")?.transform.position ?? Vector3.zero;
        var lista = Lista(mapa, "clientes").Cast<Dictionary<string, object>>().ToList();
        int casas = Lista(mapa, "casas").Count, puntosJson = Lista(mapa, "puntos_entrega").Count;
        if (lista.Count != casas || lista.Count != puntosJson)
            errores.Add($"El JSON trae {lista.Count} clientas para {casas} casas y {puntosJson} puntos de entrega.");
        var padre = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == NombreClientes);
        var enEscena = UnityEngine.Object.FindObjectsByType<Cliente>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (padre == null) errores.Add($"No hay objeto \"{NombreClientes}\" en la escena.");
        if (enEscena.Length != lista.Count) errores.Add($"Hay {enEscena.Length} Cliente en la escena y el JSON trae {lista.Count}.");
        var puntos = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "PuntosEntrega");
        var capaPeaton = UnityEngine.Object.FindFirstObjectByType<Peaton>(FindObjectsInactive.Include)?.GetComponent<SpriteRenderer>();
        int bien = 0;
        foreach (var d in lista)
        {
            string nombre = (string)d["nombre"];
            var go = padre != null ? padre.transform.Find(nombre) : null;
            var cl = go != null ? go.GetComponent<Cliente>() : null;
            if (cl == null) { errores.Add($"{nombre}: no existe o no tiene Cliente."); continue; }
            int antes = errores.Count;
            var so = new SerializedObject(cl);
            var esperado = puntos != null ? puntos.transform.Find((string)d["punto"])?.GetComponent<DeliveryPoint>() : null;
            if (esperado == null || so.FindProperty("punto").objectReferenceValue != esperado)
                errores.Add($"{nombre}: \"punto\" no es el DeliveryPoint de {d["punto"]}.");
            if (Vector2.Distance(so.FindProperty("puerta").vector2Value, PuntoMundo(d["puerta"], origen)) > 1e-3f)
                errores.Add($"{nombre}: la puerta no coincide con el JSON.");
            if (Vector2.Distance(so.FindProperty("espera").vector2Value, PuntoMundo(d["espera"], origen)) > 1e-3f)
                errores.Add($"{nombre}: el punto de espera no coincide con el JSON.");
            if (esperado != null && Mathf.Abs(Vector2.Distance(so.FindProperty("espera").vector2Value, esperado.transform.position) - 1.2f) > 0.01f)
                errores.Add($"{nombre}: espera no está a 1.2 tiles de su PuntoEntrega.");
            if (!Mathf.Approximately(so.FindProperty("velocidad").floatValue, VelocidadClienta))
                errores.Add($"{nombre}: velocidad {so.FindProperty("velocidad").floatValue}, no {VelocidadClienta}.");
            string personaje = PersonajeCliente(d);
            foreach (var fila in FilasPersonaje)
            {
                var arr = so.FindProperty(fila);
                if (arr.arraySize != 5) { errores.Add($"{nombre}: \"{fila}\" tiene {arr.arraySize} sprites, no 5."); continue; }
                for (int i = 0; i < 5; i++)
                {
                    var s = arr.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
                    if (s == null || s.name != $"{personaje}_{fila}_{i}")
                        errores.Add($"{nombre}: {fila}[{i}] es {(s == null ? "null" : s.name)}, debía ser {personaje}_{fila}_{i}.");
                }
            }
            var globo = so.FindProperty("globo").objectReferenceValue as TextMeshPro;
            if (globo == null) errores.Add($"{nombre}: el globo no es un TextMeshPro 3D.");
            else
            {
                if (globo.transform.parent != go.transform || globo.name != "Globo") errores.Add($"{nombre}: el globo no es su hijo \"Globo\".");
                if (globo.gameObject.activeSelf) errores.Add($"{nombre}: el globo debe empezar desactivado.");
                if (globo.GetComponent<MeshRenderer>().sortingOrder != OrdenGlobo) errores.Add($"{nombre}: el globo no tiene Sorting Order {OrdenGlobo}.");
                if ((Color32)globo.color is var c && (c.r != CremaGlobo.r || c.g != CremaGlobo.g || c.b != CremaGlobo.b))
                    errores.Add($"{nombre}: el globo no es crema #FFF1D0.");
                if (globo.alignment != TextAlignmentOptions.Center) errores.Add($"{nombre}: el globo no está centrado.");
                if (Mathf.Abs(globo.transform.localPosition.y - AlturaGlobo) > 1e-3f) errores.Add($"{nombre}: el globo no está a {AlturaGlobo} tiles.");
                if (globo.fontSharedMaterial == null || globo.fontSharedMaterial.GetFloat(ShaderUtilities.ID_OutlineWidth) <= 0f)
                    errores.Add($"{nombre}: el globo no tiene contorno.");
                else if ((Color32)globo.fontSharedMaterial.GetColor(ShaderUtilities.ID_OutlineColor) is var o && (o.r != ContornoGlobo.r || o.g != ContornoGlobo.g || o.b != ContornoGlobo.b))
                    errores.Add($"{nombre}: el contorno del globo no es #2A1F40.");
                if (globo.font == null || AssetDatabase.GetAssetPath(globo.font) != FuenteProyecto.RutaFuente)
                    errores.Add($"{nombre}: el globo no usa la fuente pixel ({FuenteProyecto.RutaFuente}).");
                if (Mathf.Abs(globo.fontSize - TamLetraGlobo) > 1e-3f) errores.Add($"{nombre}: el globo no tiene tamaño {TamLetraGlobo}.");
            }
            if (go.GetComponentsInChildren<Collider2D>(true).Length > 0) errores.Add($"{nombre}: no debe tener collider.");
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null || (capaPeaton != null && (sr.sortingLayerID != capaPeaton.sortingLayerID || sr.sortingOrder != capaPeaton.sortingOrder)))
                errores.Add($"{nombre}: el SpriteRenderer no tiene la capa de los peatones.");
            if (errores.Count == antes) bien++;
        }
        // dos casas vecinas (seguidas y en la misma fila de casas) nunca con la misma clienta
        for (int i = 1; i < lista.Count; i++)
        {
            var a = (Dictionary<string, object>)lista[i - 1]["puerta"];
            var b = (Dictionary<string, object>)lista[i]["puerta"];
            if (Math.Abs(Num(a["y"]) - Num(b["y"])) < 1e-3 && Math.Abs(Num(a["x"]) - Num(b["x"])) < 12 &&
                PersonajeCliente(lista[i - 1]) == PersonajeCliente(lista[i]))
                errores.Add($"{lista[i - 1]["nombre"]} y {lista[i]["nombre"]} son vecinas con la misma clienta.");
        }
        int variantes = lista.Select(PersonajeCliente).Distinct().Count();
        return $"Clientas: {bien} de {lista.Count} bien armadas, {variantes} variantes repartidas sin repetir entre vecinas.";
    }

    static string RevisarGuiaPedido(List<string> errores)
    {
        var guias = UnityEngine.Object.FindObjectsByType<GuiaPedido>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (guias.Length != 1) { errores.Add($"Hay {guias.Length} GuiaPedido en la escena, debe haber una."); return "Guía del pedido: falta"; }
        var g = guias[0];
        var rt = (RectTransform)g.transform;
        var canvas = CanvasHUD();
        if (g.name != NombreGuia) errores.Add($"La guía se llama \"{g.name}\", no \"{NombreGuia}\".");
        if (canvas == null || rt.parent != canvas.transform) errores.Add("GuiaPedido no es hijo directo del Canvas del HUD.");
        if (rt.anchorMin != new Vector2(0, 1) || rt.anchorMax != new Vector2(0, 1) || rt.pivot != new Vector2(0, 1))
            errores.Add("GuiaPedido no está anclada arriba a la izquierda.");
        var so = new SerializedObject(g);
        var flecha = so.FindProperty("flecha").objectReferenceValue as RectTransform;
        var texto = so.FindProperty("textoDistancia").objectReferenceValue as TextMeshProUGUI;
        var grupo = so.FindProperty("grupo").objectReferenceValue as CanvasGroup;
        if (flecha == null || flecha.parent != rt) errores.Add("GuiaPedido: \"flecha\" no es su hijo Flecha.");
        else if (!flecha.TryGetComponent(out UnityEngine.UI.Image img) || img.sprite == null || img.sprite.name != SpriteFlecha)
            errores.Add("GuiaPedido: la flecha no es una Image con el sprite flecha_guia.");
        if (texto == null || texto.transform.parent != rt) errores.Add("GuiaPedido: \"textoDistancia\" no es su hijo TextoDistancia.");
        if (grupo == null || grupo.gameObject != g.gameObject) errores.Add("GuiaPedido: \"grupo\" no es el CanvasGroup de la placa.");
        else if (grupo.blocksRaycasts) errores.Add("GuiaPedido: el CanvasGroup no debe bloquear clics.");
        var icono = so.FindProperty("iconoProducto").objectReferenceValue as UnityEngine.UI.Image;
        if (icono == null || icono.transform.parent != rt || icono.sprite == null) errores.Add("GuiaPedido: falta su hijo IconoProducto con un ícono.");
        if (so.FindProperty("catalogo").objectReferenceValue != ConstruirMarcadorPedido.Catalogo()) errores.Add("GuiaPedido: no usa el catálogo de productos.");
        if (texto != null && icono != null && texto.rectTransform.offsetMin.x < ((RectTransform)icono.transform).anchoredPosition.x + LadoIconoUI / 2)
            errores.Add("GuiaPedido: el texto de la distancia se encima con el ícono.");
        return $"Guía del pedido: en el Canvas en ({rt.anchoredPosition.x:0}, {rt.anchoredPosition.y:0}), {rt.sizeDelta.x:0}x{rt.sizeDelta.y:0}, " +
               $"flecha {(flecha != null ? "conectada" : "sin conectar")}, texto {(texto != null ? "conectado" : "sin conectar")}.";
    }

    // ------------------------------------------------------------------ verificación

    // Compara lo que Unity tiene importado contra los JSON y escribe en la consola cuántos sprites coinciden
    // y cuáles no (Debug.LogError si hay alguno distinto). También revisa la escala de los autos y que los
    // lagos y fuentes estén completos, y deja hoja_de_props.png con lo que Unity cortó.
    static string VerificarTodo(Dictionary<string, object> tileset, Dictionary<string, object> atlas,
                                Dictionary<string, object> mapa, List<DefSprite> defsTileset, List<DefSprite> defsAtlas,
                                List<HojaPersonaje> hojas)
    {
        var errores = new List<string>();
        var lineas = new List<string>();

        var (_, _, mapaT) = VerificarRecortes(RutaTileset, ArchivoTileset, defsTileset, null, errores, lineas);
        UnityEngine.Object.DestroyImmediate(mapaT);
        foreach (var h in hojas)
        {
            var (_, _, texH) = VerificarRecortes(h.ruta, h.json, h.defs, h.tam, errores, lineas);
            UnityEngine.Object.DestroyImmediate(texH);
        }
        var (bienA, malA, importada) = VerificarRecortes(RutaAtlas, ArchivoAtlas, defsAtlas,
            new Vector2Int((int)Num(atlas["ancho"]), (int)Num(atlas["alto"])), errores, lineas);
        try
        {
            lineas.Add(RevisarAutos(mapa, errores));
            lineas.Add(RevisarTrafico(mapa, errores));
            lineas.Add(RevisarCurvas(mapa, tileset, errores));
            lineas.Add(RevisarSemaforos(mapa, errores));
            if (mapa.ContainsKey("baches")) lineas.Add(RevisarBaches(mapa, errores));
            // el mapa no arma la interfaz: si el Canvas falta, se avisa (no se recrea a escondidas)
            var faltaUI = RevisarCanvas.Faltantes(SceneManager.GetActiveScene());
            if (faltaUI.Count > 0) errores.Add("Falta la interfaz: " + string.Join(", ", faltaUI));
            lineas.Add(faltaUI.Count == 0 ? "Canvas: HUD, GuiaPedido, PanelPausa y PanelFinal presentes." : "Canvas: FALTA.");
            lineas.Add(RevisarBloqueoMoto(mapa, errores));
            lineas.Add(RevisarLotesSolidos(mapa, errores));
            lineas.Add(RevisarPiezasCompletas(atlas, mapa, errores));
            lineas.Add(RevisarPeatones(mapa, errores));
            lineas.Add(RevisarClientes(mapa, errores));
            lineas.Add(ConstruirMarcadorPedido.RevisarTodos(errores));
            lineas.Add(ConstruirMarcadorPedido.RevisarFlechaYEntrega(errores));
            lineas.Add(RevisarGuiaPedido(errores));
            lineas.Add("Hoja de props: " + GuardarHoja(importada, malA));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(importada);
        }

        verificacionOk = errores.Count == 0;
        string texto = "VERIFICACIÓN\n" + string.Join("\n", lineas);
        if (verificacionOk)
            Debug.Log(texto + "\nVerificación sin errores.");
        else
            Debug.LogError(texto + $"\n{errores.Count} ERROR(ES):\n- " + string.Join("\n- ", errores));
        return texto + (verificacionOk ? "\nVerificación sin errores." : $"\nERRORES ({errores.Count}):\n- " + string.Join("\n- ", errores));
    }

    // Revisa cada sprite que Unity cargó de la textura contra su definición del JSON: nombre, rectángulo,
    // pivote (abajo-centro en el atlas), PPU, y que sus píxeles sean los del PNG del disco (si el import los
    // reescalara o comprimiera, o se cortara de otro lado, se nota aquí). Devuelve la textura importada
    // leída de la GPU para armar la hoja de props.
    static (int bien, HashSet<string> mal, Texture2D importada) VerificarRecortes(
        string ruta, string archivoJson, List<DefSprite> defs, Vector2Int? tamJson, List<string> errores, List<string> lineas)
    {
        var mal = new HashSet<string>();
        void Error(string nombre, string que)
        {
            mal.Add(nombre);
            errores.Add($"{Path.GetFileName(ruta)}: {nombre}: {que}");
        }

        var imp = Importador(ruta);
        if (imp.spriteImportMode != SpriteImportMode.Multiple) Error("(textura)", "no es Sprite Multiple");
        if (imp.filterMode != FilterMode.Point) Error("(textura)", $"Filter {imp.filterMode}, no Point");
        if (!Mathf.Approximately(imp.spritePixelsPerUnit, PPU)) Error("(textura)", $"PPU {imp.spritePixelsPerUnit}, no {PPU}");
        if (imp.textureCompression != TextureImporterCompression.Uncompressed) Error("(textura)", "tiene compresión");
        if (imp.mipmapEnabled) Error("(textura)", "tiene mipmaps");

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        var disco = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        disco.LoadImage(File.ReadAllBytes(ruta));
        if (tex.width != disco.width || tex.height != disco.height)
            Error("(textura)", $"Unity la importó de {tex.width}x{tex.height} y el PNG mide {disco.width}x{disco.height}");
        if (tamJson.HasValue && (disco.width != tamJson.Value.x || disco.height != tamJson.Value.y))
            Error("(textura)", $"el PNG mide {disco.width}x{disco.height} y {archivoJson} dice {tamJson.Value.x}x{tamJson.Value.y}");

        var importada = LeerDeGpu(tex);
        var pxDisco = disco.GetPixels32();
        var pxUnity = importada.GetPixels32();
        bool mismoTam = importada.width == disco.width && importada.height == disco.height;

        var sprites = AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>().ToList();
        foreach (var repetido in sprites.GroupBy(s => s.name).Where(g => g.Count() > 1))
            Error(repetido.Key, $"Unity tiene {repetido.Count()} sprites con ese nombre");
        var porNombre = sprites.GroupBy(s => s.name).ToDictionary(g => g.Key, g => g.First());
        foreach (var sobra in porNombre.Keys.Except(defs.Select(d => d.nombre)))
            Error(sobra, $"está en Unity pero no en {archivoJson}");

        foreach (var def in defs)
        {
            if (!porNombre.TryGetValue(def.nombre, out var s)) { Error(def.nombre, "no existe en Unity"); continue; }
            if (s.rect != def.rect) { Error(def.nombre, $"rectángulo {s.rect} en Unity, {def.rect} en {archivoJson}"); continue; }
            var pivotePx = new Vector2(def.pivote.x * def.rect.width, def.pivote.y * def.rect.height);
            if (Vector2.Distance(s.pivot, pivotePx) > 0.01f) Error(def.nombre, $"pivote {s.pivot} px, debía ser {pivotePx}");
            if (!Mathf.Approximately(s.pixelsPerUnit, PPU)) Error(def.nombre, $"PPU {s.pixelsPerUnit}");
            if (s.texture != tex) Error(def.nombre, "no sale de la textura");
            if (!mismoTam) continue;
            int distintos = 0;
            for (int y = (int)def.rect.y; y < (int)def.rect.yMax; y++)
                for (int x = (int)def.rect.x; x < (int)def.rect.xMax; x++)
                {
                    var a = pxDisco[y * disco.width + x];
                    var b = pxUnity[y * importada.width + x];
                    if (a.a == 0 && b.a == 0) continue;   // el color de lo transparente no importa
                    if (Math.Abs(a.r - b.r) > 3 || Math.Abs(a.g - b.g) > 3 || Math.Abs(a.b - b.b) > 3 || Math.Abs(a.a - b.a) > 3)
                        distintos++;
                }
            if (distintos > 0) Error(def.nombre, $"{distintos} píxeles distintos al PNG");
        }
        UnityEngine.Object.DestroyImmediate(disco);

        int bien = defs.Count(d => !mal.Contains(d.nombre));
        string linea = $"{Path.GetFileName(ruta)}: {bien} de {defs.Count} sprites coinciden con {archivoJson}" +
                       (mal.Count == 0 ? "" : $"; distintos: {string.Join(", ", mal.OrderBy(n => n))}");
        lineas.Add(linea);
        return (bien, mal, importada);
    }

    // Copia legible de una textura importada (no hace falta Read/Write): se dibuja en un RenderTexture sRGB
    static Texture2D LeerDeGpu(Texture2D tex)
    {
        var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previa = RenderTexture.active;
        try
        {
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            var copia = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            copia.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            copia.Apply();
            return copia;
        }
        finally
        {
            RenderTexture.active = previa;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    // Todos los autos (props estacionados y AutosCalle) deben medir lo mismo a lo largo: LargoAutoPx.
    static string RevisarAutos(Dictionary<string, object> mapa, List<string> errores)
    {
        float largo = (float)LargoAutoPx / PPU;
        var raices = SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name == "Props" || g.name == NombreAutos);
        var autos = raices.SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true))
                          .Where(sr => sr.sprite != null && sr.sprite.name.StartsWith("carro_")).ToList();
        int bien = 0;
        foreach (var sr in autos)
        {
            bool deLado = sr.sprite.name.StartsWith("carro_lateral_");
            var escala = sr.transform.lossyScale;
            float medido = deLado ? sr.sprite.rect.width / PPU * Mathf.Abs(escala.x) : sr.sprite.rect.height / PPU * Mathf.Abs(escala.y);
            if (Mathf.Abs(medido - largo) > 1e-3f)
                errores.Add($"Auto {sr.name} ({sr.sprite.name}) mide {medido:0.###} tiles de largo; la escala común es {largo:0.###}");
            else bien++;
        }
        int enCalle = autos.Count(a => a.GetComponent<TrafficCar>() != null);
        int esperados = RutasAutos(mapa).Sum(r => r.autos);
        if (enCalle != esperados) errores.Add($"Hay {enCalle} autos con TrafficCar y las rutas piden {esperados}.");
        return $"Autos: {bien} de {autos.Count} miden {largo:0.###} tiles ({LargoAutoPx} px) de largo; {enCalle} circulan con TrafficCar.";
    }

    // Cada lago y cada fuente es un solo objeto con su sprite completo; el atlas no trae pedazos sueltos.
    static string RevisarPiezasCompletas(Dictionary<string, object> atlas, Dictionary<string, object> mapa, List<string> errores)
    {
        bool EsPieza(string n) => PiezasCompletas.Any(p => n.StartsWith(p));
        var enJson = Lista(mapa, "props").Cast<Dictionary<string, object>>().Select(p => (string)p["nombre"]).Where(EsPieza).ToList();
        var enAtlas = Lista(atlas, "sprites").Cast<Dictionary<string, object>>().Select(s => (string)s["name"]).Where(EsPieza).ToList();
        foreach (var n in enAtlas.Except(enJson))
            errores.Add($"props_atlas.json trae \"{n}\" pero el mapa no lo usa (pieza suelta).");
        foreach (var g in enJson.GroupBy(n => n).Where(g => g.Count() > 1))
            errores.Add($"\"{g.Key}\" aparece {g.Count()} veces en el mapa; debe ser un solo objeto completo.");

        var props = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Props");
        var enEscena = props == null ? new List<SpriteRenderer>() :
            props.GetComponentsInChildren<SpriteRenderer>(true).Where(sr => sr.sprite != null && EsPieza(sr.sprite.name)).ToList();
        foreach (var sr in enEscena)
        {
            if (sr.gameObject.name != sr.sprite.name)
                errores.Add($"{sr.gameObject.name} usa el sprite {sr.sprite.name}.");
            var def = Lista(atlas, "sprites").Cast<Dictionary<string, object>>().FirstOrDefault(s => (string)s["name"] == sr.sprite.name);
            if (def != null && (sr.sprite.rect.width != (float)Num(def["w"]) || sr.sprite.rect.height != (float)Num(def["h"])))
                errores.Add($"{sr.sprite.name} en la escena no es el sprite completo ({sr.sprite.rect.size}).");
        }
        if (enEscena.Count != enJson.Count)
            errores.Add($"La escena tiene {enEscena.Count} lagos/fuentes y el mapa {enJson.Count}.");
        return $"Lagos y fuentes: {enEscena.Count} objeto(s) completo(s) en la escena ({string.Join(", ", enEscena.Select(s => s.sprite.name))}).";
    }

    // ------------------------------------------------------------------ hoja de props

    // Cada sprite que Unity cortó (su rectángulo de Unity, no el del JSON) sobre la textura importada, a 2x,
    // con su nombre debajo. El borde del recorte va en gris; si la verificación lo marcó, en rojo.
    static string GuardarHoja(Texture2D importada, HashSet<string> mal)
    {
        const int E = 2, Pad = 12, AnchoHoja = 2400, AltoLetra = 11 * E;
        var sprites = AssetDatabase.LoadAllAssetsAtPath(RutaAtlas).OfType<Sprite>().OrderBy(s => s.name).ToList();
        var fondoA = new Color32(0x3A, 0x3A, 0x40, 255);
        var fondoB = new Color32(0x44, 0x44, 0x4B, 255);

        // acomodo en filas (coordenadas desde arriba)
        var puestos = new List<(Sprite s, int x, int y, int w)>();
        int cx = Pad, cy = Pad, altoFila = 0;
        foreach (var s in sprites)
        {
            int w = Mathf.Max((int)s.rect.width * E, AnchoTexto(s.name, E)) + 2;
            int h = (int)s.rect.height * E + 2 + 4 + AltoLetra;
            if (cx + w + Pad > AnchoHoja) { cx = Pad; cy += altoFila + Pad; altoFila = 0; }
            puestos.Add((s, cx, cy, w));
            cx += w + Pad;
            altoFila = Mathf.Max(altoFila, h);
        }
        int W = AnchoHoja, H = cy + altoFila + Pad;
        var px = new Color32[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                px[y * W + x] = ((x / 8 + y / 8) & 1) == 0 ? fondoA : fondoB;

        void Poner(int x, int yArriba, Color32 c)
        {
            if (x < 0 || x >= W || yArriba < 0 || yArriba >= H) return;
            px[(H - 1 - yArriba) * W + x] = c;
        }

        var fuente = importada.GetPixels32();
        foreach (var (s, x0, y0, w) in puestos)
        {
            int sw = (int)s.rect.width, sh = (int)s.rect.height;
            int ox = x0 + 1 + (w - 2 - sw * E) / 2, oy = y0 + 1;
            var borde = mal.Contains(s.name) ? new Color32(255, 50, 50, 255) : new Color32(150, 150, 160, 255);
            for (int i = -1; i <= sw * E; i++) { Poner(ox + i, oy - 1, borde); Poner(ox + i, oy + sh * E, borde); }
            for (int j = -1; j <= sh * E; j++) { Poner(ox - 1, oy + j, borde); Poner(ox + sw * E, oy + j, borde); }
            for (int j = 0; j < sh; j++)
                for (int i = 0; i < sw; i++)
                {
                    // fila j desde arriba del sprite = fila (yMax - 1 - j) de la textura (que va desde abajo)
                    var c = fuente[((int)s.rect.yMax - 1 - j) * importada.width + (int)s.rect.x + i];
                    if (c.a == 0) continue;
                    for (int dy = 0; dy < E; dy++)
                        for (int dx = 0; dx < E; dx++)
                        {
                            int X = ox + i * E + dx, Y = oy + j * E + dy;
                            int k = (H - 1 - Y) * W + X;
                            var f = px[k];
                            px[k] = new Color32((byte)((c.r * c.a + f.r * (255 - c.a)) / 255),
                                                (byte)((c.g * c.a + f.g * (255 - c.a)) / 255),
                                                (byte)((c.b * c.a + f.b * (255 - c.a)) / 255), 255);
                        }
                }
            var colorTexto = mal.Contains(s.name) ? new Color32(255, 90, 90, 255) : new Color32(240, 240, 235, 255);
            Escribir(s.name, x0 + 1 + (w - 2 - AnchoTexto(s.name, E)) / 2, oy + sh * E + 4, E, colorTexto, Poner);
        }

        var hoja = new Texture2D(W, H, TextureFormat.RGBA32, false);
        hoja.SetPixels32(px);
        hoja.Apply();
        string ruta = Path.Combine(CarpetaDatos, ArchivoHoja).Replace('\\', '/');
        File.WriteAllBytes(ruta, hoja.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(hoja);
        return $"{sprites.Count} sprites en {ruta}";
    }

    // Letras de 6x11 px (la fuente bitmap de Pillow), una fila por byte en hex, bit 0 = columna izquierda
    const string Letras = "abcdefghijklmnopqrstuvwxyz0123456789_'.-";
    const string Glifos =
            "000000000e1b1e1b3f0000000003030f1b1b1b0f0000000000000e1b031b0e000000001c181e1b1b1b3e0000000000000e1b1f031e0000" +
            "00001c061f0606061f000000000000361b1b1b1e180f000003030f1b1b1b1b000000000c000f0c0c0c3f000000000c000f0c0c0c0c0c07" +
            "000003031b0f070f3b000000000f0c0c0c0c0c3f0000000000000f1f1515150000000000000d1b1b1b1b0000000000000e1b1b1b0e0000" +
            "000000000f1b1b1b0f030700000000361b1b1b1e183c000000003b2e06060f0000000000001e071e381f0000000006061f0606361c0000" +
            "000000001b1b1b1b3e0000000000001b1b0e0e0400000000000035151f1e0a000000000000371e0c1e3b0000000000003b1b1b0a0e0603" +
            "000000001f0d061b1f000000000e1b1b1b1b1b0e000000000c0f0c0c0c0c3f000000000e1b180c061b1f000000000e1b180e181b0e0000" +
            "0000181c1a1b3f1818000000001f030f1b18190f000000000e1b030f1b1b0e000000001f1b180c0c0606000000000e1b1b0e1b1b0e0000" +
            "00000e1b1b1e181b0e0000000000000000000000003f00000c0402000000000000000000000000000006000000000000001f0000000000";

    static int AnchoTexto(string texto, int escala) => texto.Length * 6 * escala;

    static void Escribir(string texto, int x, int yArriba, int escala, Color32 c, Action<int, int, Color32> poner)
    {
        foreach (char ch in texto.ToLowerInvariant())
        {
            int g = Letras.IndexOf(ch);
            if (g >= 0)
                for (int fila = 0; fila < 11; fila++)
                {
                    int bits = Convert.ToInt32(Glifos.Substring((g * 11 + fila) * 2, 2), 16);
                    for (int col = 0; col < 6; col++)
                        if ((bits >> col & 1) != 0)
                            for (int dy = 0; dy < escala; dy++)
                                for (int dx = 0; dx < escala; dx++)
                                    poner(x + col * escala + dx, yArriba + fila * escala + dy, c);
                }
            x += 6 * escala;
        }
    }

    [MenuItem("Delivery Brenn's/Verificar atlas Residencial (hoja de props)")]
    static void VerificarMenu()
    {
        try
        {
            var tileset = LeerJson(ArchivoTileset);
            var atlas = LeerJson(ArchivoAtlas);
            string texto = VerificarTodo(tileset, atlas, LeerJson(ArchivoMapa),
                                         DefsTileset(tileset, RutaTileset), DefsAtlas(atlas, RutaAtlas), HojasPersonajes());
            EditorUtility.DisplayDialog("Verificar atlas Residencial", texto, "OK");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Verificar atlas Residencial", e.Message, "OK");
        }
    }

    // ------------------------------------------------------------------ captura

    // Renderiza el mapa completo con una cámara ortográfica a 1 px de pantalla por px del arte
    // (128x64 tiles = 4096x2048) y lo guarda junto a los JSON.
    static string Capturar(Dictionary<string, object> mapa)
    {
        int cols = (int)Num(mapa["columnas"]), filas = (int)Num(mapa["filas"]);
        return CapturarArea(new Rect(0, 0, cols, filas), PPU, Path.Combine(CarpetaDatos, ArchivoCaptura));
    }

    // Lo mismo para un rectángulo del mapa (en tiles) a "ppu" píxeles por tile, a la ruta que se pida
    internal static string CapturarArea(Rect area, int ppu, string archivo)
    {
        var suelo = UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .FirstOrDefault(t => t.name == "Suelo");
        if (suelo == null) throw new Exception("No hay Tilemap \"Suelo\" en la escena; construye el mapa primero.");
        var origen = suelo.transform.position;
        int w = Mathf.RoundToInt(area.width * ppu), h = Mathf.RoundToInt(area.height * ppu);

        var go = new GameObject("CamaraCaptura") { hideFlags = HideFlags.HideAndDontSave };
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var previa = RenderTexture.active;
        try
        {
            var cam = go.AddComponent<Camera>();
            go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            cam.orthographic = true;
            cam.orthographicSize = area.height / 2f;
            cam.aspect = area.width / area.height;
            cam.transform.position = new Vector3(origen.x + area.center.x, origen.y + area.center.y, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.targetTexture = rt;
            var pedido = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, pedido))
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, pedido);
            else
                cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            string ruta = archivo.Replace('\\', '/');
            File.WriteAllBytes(ruta, tex.EncodeToPNG());
            Debug.Log($"Captura del mapa guardada en {ruta} ({w}x{h}).");
            return ruta;
        }
        finally
        {
            RenderTexture.active = previa;
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }
    }

    // Permite reconstruir y capturar sin pasar por el menú: si aparece Temp/ConstruirMapa.pedido,
    // se construye el mapa, se guarda la escena, se captura y el resultado queda en Temp/ConstruirMapa.resultado.
    [InitializeOnLoad]
    static class PedidoExterno
    {
        const string Pedido = "Temp/ConstruirMapa.pedido";
        const string Resultado = "Temp/ConstruirMapa.resultado";

        static PedidoExterno() => EditorApplication.update += Revisar;

        static void Revisar()
        {
            if (!File.Exists(Pedido) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;   // se construye al volver al modo edición
                return;
            }
            File.Delete(Pedido);
            string texto;
            try
            {
                texto = ConstruirTodo();
                texto = (verificacionOk ? "OK\n" : "OK CON ERRORES DE VERIFICACION\n") + texto;
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                texto += "\nCaptura: " + Capturar(LeerJson(ArchivoMapa));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                texto = "ERROR\n" + e;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            File.WriteAllText(Resultado, texto);
        }
    }

    // Medición del tráfico en Play sin tocar nada: si aparece Temp/MedirTrafico.pedido, entra a Play con el
    // tiempo x2, mide la separación mínima real entre autos del mismo carril (mismo sentido y misma línea) y
    // cuántos hay por carril; al final pone al repartidor en medio de la calle Olmo para revisar que los autos
    // frenen antes de tocarlo. Escribe Temp/MedirTrafico.resultado y sale de Play.
    [InitializeOnLoad]
    static class MedicionTrafico
    {
        const string Pedido = "Temp/MedirTrafico.pedido";
        const string Resultado = "Temp/MedirTrafico.resultado";
        const string Clave = "ConstruirMapa.MedirTrafico";
        const float Escala = 2f;
        const float SegundosSeparacion = 240f;   // de juego
        const float SegundosFrenado = 40f;

        static float inicio = -1f, minimo = float.MaxValue, minimoFrenado = float.MaxValue, cercaniaJugador = float.MaxValue;
        static string parMinimo = "";
        static int muestras, toques;
        static string peorPasto = "", peorCamellon = "";
        static Vector2 origenMapa;
        static Vector3 posJugador;
        static bool frenadoListo;

        static MedicionTrafico() => EditorApplication.update += Revisar;

        static void Revisar()
        {
            if (!EditorApplication.isPlaying)
            {
                if (SessionState.GetBool(Clave, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                    SessionState.EraseBool(Clave);   // se salió de Play antes de terminar
                if (!File.Exists(Pedido) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                    EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete(Pedido);
                SessionState.SetBool(Clave, true);
                EditorApplication.isPlaying = true;
                return;
            }
            if (!SessionState.GetBool(Clave, false)) return;
            if (inicio < 0f)
            {
                inicio = Time.time;
                minimo = minimoFrenado = cercaniaJugador = float.MaxValue;
                parMinimo = "";
                muestras = toques = pxPasto = pxCamellon = maxPasto = maxCamellon = 0;
                peorPasto = peorCamellon = "";
                var mapa = LeerJson(ArchivoMapa);
                PrepararSuelo(mapa, LeerJson(ArchivoTileset));
                origenMapa = UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None).First(m => m.name == "Suelo").transform.position;
                frenadoListo = false;
                Time.timeScale = Escala;
                Application.runInBackground = true;   // sin foco Unity deja de avanzar los cuadros
                return;
            }

            float t = Time.time - inicio;
            var autos = TrafficCar.Todos.Where(a => a != null && !a.Esperando).ToList();
            var jugador = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (t < SegundosSeparacion)
            {
                Medir(autos, ref minimo, ref parMinimo);
                MedirPasto(autos);
                muestras++;
            }
            else if (t < SegundosSeparacion + SegundosFrenado && jugador != null)
            {
                var rbJ = jugador.GetComponent<Rigidbody2D>();
                if (!frenadoListo)
                {
                    // de pie en el carril de la calle Olmo que va hacia la izquierda, a medio mapa
                    posJugador = jugador.transform.position;
                    var o = UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None).First(m => m.name == "Suelo").transform.position;
                    rbJ.position = new Vector2(o.x + 70f, o.y + 20.6f);
                    frenadoListo = true;
                }
                rbJ.linearVelocity = Vector2.zero;
                var pie = jugador.GetComponent<Collider2D>();
                foreach (var a in autos)
                {
                    float d = Physics2D.Distance(pie, a.GetComponent<Collider2D>()).distance;
                    cercaniaJugador = Mathf.Min(cercaniaJugador, d);
                    if (d <= 0.001f) toques++;
                }
                string nada = "";
                Medir(autos, ref minimoFrenado, ref nada);
            }
            else
            {
                if (jugador != null) jugador.GetComponent<Rigidbody2D>().position = posJugador;
                var porCarril = autos.Concat(TrafficCar.Todos.Where(a => a != null && a.Esperando))
                    .GroupBy(a => a.Carril).OrderBy(g => g.Key).Select(g => $"{g.Key}: {g.Count()}");
                string texto = $"OK\nMedido {SegundosSeparacion:0} s de juego ({muestras} muestras, tiempo x{Escala}).\n" +
                               $"Autos por carril: {string.Join(", ", porCarril)}\n" +
                               $"Separación mínima real entre autos del mismo carril: {minimo:0.00} tiles ({parMinimo})\n" +
                               $"Con el repartidor parado en Olmo ({SegundosFrenado:0} s): el auto más cercano quedó a {cercaniaJugador:0.00} tiles " +
                               $"de sus pies, {toques} muestras con contacto; separación mínima en la fila que se formó {minimoFrenado:0.00} tiles.\n" +
                               $"Carrocería sobre pasto: {pxPasto} muestras, máximo {maxPasto} px{(maxPasto > 0 ? $" ({peorPasto})" : "")}; " +
                               $"sobre el camellón: {pxCamellon} muestras, máximo {maxCamellon} px{(maxCamellon > 0 ? $" ({peorCamellon})" : "")}.";
                File.WriteAllText(Resultado, texto);
                Debug.Log(texto);
                Time.timeScale = 1f;
                SessionState.EraseBool(Clave);
                inicio = -1f;
                EditorApplication.isPlaying = false;
            }
        }

        // Píxeles de la carrocería (alfa 255 del sprite, sin la sombra) que caen sobre pasto o sobre el
        // camellón, comparando con los píxeles del suelo pintado. Fuera del mapa no hay suelo.
        static bool[] verdeMapa, camellonMapa;
        static int anchoPx, altoPx;
        static Color32[] atlasPx;
        static int atlasAncho;
        static int pxPasto, pxCamellon, maxPasto, maxCamellon;

        static void PrepararSuelo(Dictionary<string, object> mapa, Dictionary<string, object> tileset)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(RutaTileset));
            var px = tex.GetPixels32();
            int tw = tex.width, th = tex.height;
            UnityEngine.Object.DestroyImmediate(tex);
            var pos = Lista(tileset, "tiles").Cast<Dictionary<string, object>>()
                .ToDictionary(t => (string)t["name"], t => ((int)Num(t["x"]), (int)Num(t["y"])));
            // (i, j) con j = 0 abajo del tile
            Color32 Px(string tile, int i, int j) { var (x0, y0) = pos[tile]; return px[(th - 1 - (y0 + 31 - j)) * tw + x0 + i]; }
            var asfalto = new HashSet<Color32>();
            foreach (var n in new[] { "asfalto_a", "asfalto_b" })
                for (int i = 0; i < 32; i++) for (int j = 0; j < 32; j++) asfalto.Add(Px(n, i, j));
            bool Verde(Color32 c) => c.g > c.r + 8 && c.g > c.b + 8;

            var filas = Lista(mapa, "suelo").Cast<List<object>>().ToList();
            int alto = filas.Count, ancho = filas[0].Count;
            anchoPx = ancho * 32; altoPx = alto * 32;
            verdeMapa = new bool[anchoPx * altoPx];
            camellonMapa = new bool[anchoPx * altoPx];
            for (int r = 0; r < alto; r++)
                for (int c = 0; c < ancho; c++)
                {
                    string t = (string)filas[r][c];
                    bool esCamellon = t.StartsWith("camellon"), esCurva = t.StartsWith("curva_avenida");
                    bool conPasto = false;
                    if (esCamellon)
                        for (int i = 0; i < 32 && !conPasto; i++)
                            for (int j = 0; j < 32 && !conPasto; j++)
                                conPasto = Verde(Px(t, i, j));
                    for (int i = 0; i < 32; i++)
                        for (int j = 0; j < 32; j++)
                        {
                            var col = Px(t, i, j);
                            int mx = c * 32 + i, my = (alto - 1 - r) * 32 + j, k = my * anchoPx + mx;
                            verdeMapa[k] = Verde(col);
                            if (asfalto.Contains(col)) continue;
                            if (esCamellon && conPasto) camellonMapa[k] = true;
                            else if (esCurva)
                            {
                                // en la vuelta el camellón es el anillo de radio 9 a 10
                                float d = Vector2.Distance(new Vector2((mx + 0.5f) / 32f, (my + 0.5f) / 32f), CentroCurva);
                                if (d >= RadioCarrilInterior + 1.5f && d <= RadioCarrilExterior - 1.5f) camellonMapa[k] = true;
                            }
                        }
                }
            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            atlas.LoadImage(File.ReadAllBytes(RutaAtlas));
            atlasPx = atlas.GetPixels32();
            atlasAncho = atlas.width;
            UnityEngine.Object.DestroyImmediate(atlas);
        }

        static void MedirPasto(List<TrafficCar> autos)
        {
            foreach (var a in autos)
            {
                var sr = a.GetComponent<SpriteRenderer>();
                if (sr == null || sr.sprite == null) continue;
                var rect = sr.sprite.rect;
                int w = (int)rect.width, h = (int)rect.height, rx = (int)rect.x, ry = (int)rect.y;
                Vector2 piv = sr.sprite.pivot;
                Vector2 p = (Vector2)a.transform.position - origenMapa;
                int baseX = Mathf.RoundToInt(p.x * 32f - piv.x), baseY = Mathf.RoundToInt(p.y * 32f - piv.y);
                if (baseX < 0 || baseY < 0 || baseX + w > anchoPx || baseY + h > altoPx) continue;
                int nPasto = 0, nCamellon = 0;
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                    {
                        int si = sr.flipX ? w - 1 - i : i;
                        if (atlasPx[(ry + j) * atlasAncho + rx + si].a < 255) continue;
                        int k = (baseY + j) * anchoPx + baseX + i;
                        if (verdeMapa[k]) nPasto++;
                        else if (camellonMapa[k]) nCamellon++;
                    }
                if (nPasto > 0) { pxPasto++; if (nPasto > maxPasto) { maxPasto = nPasto; peorPasto = $"{a.name} en {p}"; } }
                if (nCamellon > 0) { pxCamellon++; if (nCamellon > maxCamellon) { maxCamellon = nCamellon; peorCamellon = $"{a.name} en {p}"; } }
            }
        }

        static void Medir(List<TrafficCar> autos, ref float min, ref string par)
        {
            for (int i = 0; i < autos.Count; i++)
                for (int j = i + 1; j < autos.Count; j++)
                {
                    var a = autos[i]; var b = autos[j];
                    if (Vector2.Dot(a.Dir, b.Dir) < 0.9f) continue;
                    Vector2 delta = b.Centro - a.Centro;
                    if (Mathf.Abs(a.Dir.x * delta.y - a.Dir.y * delta.x) > 0.6f) continue;   // otro carril
                    if (delta.magnitude < min) { min = delta.magnitude; par = $"{a.name} y {b.name}"; }
                }
        }
    }

    // Dónde puede quedar el centro de la cámara (ortográfica de la escena) con 16:9 y 4:3
    static string TextoCentroCamara(Rect mapa)
    {
        var cam = UnityEngine.Object.FindFirstObjectByType<CameraFollow>(FindObjectsInactive.Include)?.GetComponent<Camera>() ?? Camera.main;
        if (cam == null || !cam.orthographic) return ".";
        float alto = cam.orthographicSize;
        string Rango(float aspecto)
        {
            float ancho = alto * aspecto;
            string Eje(float min, float max, float medio) =>
                max - min <= 2 * medio ? $"{(min + max) / 2:0.##} (centrada)" : $"{min + medio:0.##}..{max - medio:0.##}";
            return $"x {Eje(mapa.xMin, mapa.xMax, ancho)}, y {Eje(mapa.yMin, mapa.yMax, alto)}";
        }
        return $"; centro de la cámara (tamaño {alto}) con 16:9: {Rango(16f / 9f)}; con 4:3: {Rango(4f / 3f)}.";
    }

    // El orden por Y solo funciona si el Renderer 2D ordena por eje personalizado (0, 1, 0).
    // Aquí solo se revisa y se avisa; no se cambia la configuración del proyecto.
    static void RevisarOrdenPorY()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Renderer2DData"))
        {
            var ruta = AssetDatabase.GUIDToAssetPath(guid);
            var obj = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(ruta));
            var modo = obj.FindProperty("m_TransparencySortMode");
            var eje = obj.FindProperty("m_TransparencySortAxis");
            if (modo == null || eje == null) continue;
            if (modo.intValue != (int)TransparencySortMode.CustomAxis || eje.vector3Value != Vector3.up)
                Debug.LogWarning($"Para que los props se ordenen por Y, abre {ruta} y pon " +
                                 "Transparency Sort Mode = Custom Axis y Transparency Sort Axis = (0, 1, 0).");
        }
    }

    // ------------------------------------------------------------------ JSON

    static Dictionary<string, object> LeerJson(string archivo)
    {
        string ruta = Path.Combine(CarpetaDatos, archivo);
        if (!File.Exists(ruta))
            throw new Exception($"No existe {ruta}. Ejecuta antes generate_unity.py y generate_residencial_grande.py en pixel_town.");
        return (Dictionary<string, object>)Json.Leer(File.ReadAllText(ruta));
    }

    internal static List<object> Lista(Dictionary<string, object> d, string clave)
    {
        if (!d.TryGetValue(clave, out var v) || !(v is List<object> lista))
            throw new Exception($"Al JSON le falta la lista \"{clave}\".");
        return lista;
    }

    internal static double Num(object o) => Convert.ToDouble(o, CultureInfo.InvariantCulture);

    // Lector de JSON mínimo: JsonUtility no sabe leer listas de listas como "suelo".
    static class Json
    {
        public static object Leer(string s)
        {
            int i = 0;
            var v = Valor(s, ref i);
            Espacios(s, ref i);
            if (i < s.Length) throw Error(s, i, "texto de más al final");
            return v;
        }

        static object Valor(string s, ref int i)
        {
            Espacios(s, ref i);
            if (i >= s.Length) throw Error(s, i, "fin inesperado");
            char c = s[i];
            if (c == '{')
            {
                i++;
                var d = new Dictionary<string, object>();
                Espacios(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Espacios(s, ref i);
                    string clave = Cadena(s, ref i);
                    Espacios(s, ref i);
                    Esperar(s, ref i, ':');
                    d[clave] = Valor(s, ref i);
                    Espacios(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    Esperar(s, ref i, '}');
                    return d;
                }
            }
            if (c == '[')
            {
                i++;
                var l = new List<object>();
                Espacios(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Valor(s, ref i));
                    Espacios(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    Esperar(s, ref i, ']');
                    return l;
                }
            }
            if (c == '"') return Cadena(s, ref i);
            if (Palabra(s, ref i, "true")) return true;
            if (Palabra(s, ref i, "false")) return false;
            if (Palabra(s, ref i, "null")) return null;

            int inicio = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == inicio) throw Error(s, i, $"carácter inesperado '{c}'");
            return double.Parse(s.Substring(inicio, i - inicio), CultureInfo.InvariantCulture);
        }

        static string Cadena(string s, ref int i)
        {
            Esperar(s, ref i, '"');
            var sb = new StringBuilder();
            while (true)
            {
                if (i >= s.Length) throw Error(s, i, "cadena sin cerrar");
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber));
                        i += 4;
                        break;
                    default: sb.Append(e); break;   // \" \\ \/
                }
            }
        }

        static bool Palabra(string s, ref int i, string palabra)
        {
            if (string.CompareOrdinal(s, i, palabra, 0, palabra.Length) != 0) return false;
            i += palabra.Length;
            return true;
        }

        static void Espacios(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static void Esperar(string s, ref int i, char c)
        {
            if (i >= s.Length || s[i] != c) throw Error(s, i, $"se esperaba '{c}'");
            i++;
        }

        static Exception Error(string s, int i, string que) => new FormatException($"JSON inválido en la posición {i}: {que}.");
    }
}
