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

// Arma el mapa Residencial dentro de Unity a partir de lo que exporta pixel_town/generate_residencial_grande.py
// (128x64 tiles, mismo tileset_suelo y props_atlas que generate_unity.py; los props nuevos del mapa grande
// van agregados al final del atlas).
// Se puede ejecutar las veces que haga falta: vuelve a cortar las texturas SIEMPRE desde los JSON (nunca
// reutiliza los recortes del .meta), actualiza los tiles, repinta el suelo, reemplaza los props, los autos
// de la calle, el borde del mapa y los puntos de entrega, y mueve al repartidor al spawn. Al final verifica
// que cada sprite importado coincida con su rectángulo del JSON y deja hoja_de_props.png para revisar a ojo.
public static class ConstruirMapa
{
    // De aquí salen los JSON (los genera el script de Python)
    const string CarpetaDatos = "C:/Users/kangu/pixel_town/unity/residencial";

    // Las mismas imágenes, ya importadas en el proyecto
    const string RutaTileset = "Assets/Tiles/tileset_suelo.png";
    const string RutaAtlas = "Assets/Sprits/Escenarios/Residencial/props_atlas.png";
    const string CarpetaTiles = "Assets/Tiles/Residencial";
    const string ArchivoMapa = "mapa_residencial_grande.json";

    // Círculo del paquete 2D Sprite (el mismo que usaban los PuntoEntrega_1 hechos a mano)
    const string GuidCirculo = "a86470a33a6bf42c4b3595704624658b";
    static readonly Color RosaEntrega = new Color32(0xFF, 0x6F, 0xA5, 0xFF);
    const int OrdenCirculo = 100;
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

    struct HojaPersonaje
    {
        public string ruta;
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
        var tileset = LeerJson("tileset_suelo.json");
        var atlas = LeerJson("props_atlas.json");
        var mapa = LeerJson(ArchivoMapa);

        EditorUtility.DisplayProgressBar("Mapa Residencial", "Cortando texturas…", 0.1f);
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

        EditorUtility.DisplayProgressBar("Mapa Residencial", "Creando tiles…", 0.4f);
        var tiles = CrearTiles(CargarSprites(RutaTileset));

        EditorUtility.DisplayProgressBar("Mapa Residencial", "Pintando el suelo…", 0.6f);
        var suelo = PintarSuelo(mapa, tiles);

        EditorUtility.DisplayProgressBar("Mapa Residencial", "Colocando props…", 0.8f);
        var origen = suelo.transform.position;
        var spritesAtlas = CargarSprites(RutaAtlas);
        int colocados = ColocarProps(mapa, spritesAtlas, origen);
        int autos = CrearAutosCalle(mapa, spritesAtlas, origen);

        EditorUtility.DisplayProgressBar("Mapa Residencial", "Borde, repartidor y puntos de entrega…", 0.9f);
        CrearBorde(mapa, origen);
        ColocarRepartidor(mapa, origen);
        int puntos = CrearPuntosEntrega(mapa, origen);
        int peatones = CrearPeatones(mapa, origen);
        ColocarDuena(mapa, origen);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        int dinamicos = Lista(mapa, "dinamicos").Count;
        string resumen = $"Mapa Residencial listo ({Num(mapa["columnas"])}x{Num(mapa["filas"])} tiles): " +
                         $"{Lista(atlas, "sprites").Count} sprites en props_atlas, {colocados} props, " +
                         $"{Lista(mapa, "casas").Count} casas, {puntos} puntos de entrega, {autos} autos en la calle, " +
                         $"{peatones} peatones y la dueña en la puerta de Brenn's. " +
                         $"{dinamicos} dinámicos (baches) quedan en el JSON para colocarlos por código.";
        Debug.Log(resumen);
        RevisarOrdenPorY();

        EditorUtility.DisplayProgressBar("Mapa Residencial", "Verificando recortes y armando hoja_de_props…", 0.95f);
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
            .Select(s => new DefSprite { nombre = (string)s["name"], rect = RectDesdeArriba(s, alto), pivote = PivoteProps })
            .ToList();
    }

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
            // (x, y) es el pivote abajo-centro, en tiles = unidades del mundo
            go.transform.localPosition = new Vector3((float)Num(p["x"]), (float)Num(p["y"]), 0);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;   // el orden por Y se toma del pie del sprite
            sr.flipX = p.TryGetValue("voltear", out var v) && v is bool b && b;   // autos del carril de abajo

            AgregarColision(go, nombre, sprite.rect.width);
        }
        return props.Count;
    }

    static void AgregarColision(GameObject go, string nombre, float anchoSprite)
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
                caja.offset = new Vector2(x0 + ancho / 2f - anchoSprite / 2f, regla.alto / 2f) / PPU;
            }
            return;
        }
    }

    // ------------------------------------------------------------------ autos de la calle

    // Un auto por cada elemento de "autos_calle": sprite según hacia dónde va, Rigidbody2D, BoxCollider2D del
    // tamaño del cuerpo y TrafficCar con su dirección, velocidad y los extremos del carril (en coordenadas del
    // mundo). Antes se borran los de una corrida previa y cualquier TrafficCar suelto, para no duplicarlos.
    static int CrearAutosCalle(Dictionary<string, object> mapa, Dictionary<string, Sprite> sprites, Vector3 origen)
    {
        var autos = Lista(mapa, "autos_calle").Cast<Dictionary<string, object>>().ToList();
        var faltantes = autos.Select(a => (string)a["sprite"]).Where(n => !sprites.ContainsKey(n)).Distinct().ToList();
        if (faltantes.Count > 0)
            throw new Exception("Los autos de la calle usan sprites que no están en props_atlas: " + string.Join(", ", faltantes));

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

        var porCarril = new Dictionary<string, int>();
        foreach (var a in autos)
        {
            string carril = (string)a["carril"];
            porCarril[carril] = porCarril.TryGetValue(carril, out int k) ? k + 1 : 1;
            var go = new GameObject($"Auto_{carril}_{porCarril[carril]}");
            go.transform.SetParent(padre.transform, false);
            float x = (float)Num(a["x"]), y = (float)Num(a["y"]);
            go.transform.localPosition = new Vector3(x, y, 0);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprites[(string)a["sprite"]];
            sr.spriteSortPoint = SpriteSortPoint.Pivot;   // se ordena por Y junto con los props
            sr.flipX = a.TryGetValue("voltear", out var v) && v is bool b && b;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            var cuerpo = (Dictionary<string, object>)a["cuerpo"];
            var caja = go.AddComponent<BoxCollider2D>();
            caja.isTrigger = false;
            caja.size = new Vector2((float)Num(cuerpo["ancho"]), (float)Num(cuerpo["alto"]));
            caja.offset = new Vector2((float)Num(cuerpo["x"]), (float)Num(cuerpo["y"]));

            var vec = Lista(a, "vector");
            var direccion = new Vector2((float)Num(vec[0]), (float)Num(vec[1]));
            bool horizontal = Mathf.Abs(direccion.x) > Mathf.Abs(direccion.y);
            float corrimiento = horizontal ? origen.x : origen.y;   // los límites del JSON son relativos al mapa
            var auto = new SerializedObject(go.AddComponent<TrafficCar>());
            Campo(auto, "direccion").vector2Value = direccion;
            Campo(auto, "velocidad").floatValue = (float)Num(a["velocidad"]);
            Campo(auto, "limiteMin").floatValue = (float)Num(a["limite_min"]) + corrimiento;
            Campo(auto, "limiteMax").floatValue = (float)Num(a["limite_max"]) + corrimiento;
            if (a.TryGetValue("altura_detector", out var alto) && alto != null)   // al subir, el frente queda arriba
                Campo(auto, "alturaCentro").floatValue = (float)Num(alto);
            auto.ApplyModifiedPropertiesWithoutUndo();

            string bajo = TileEn(mapa, x + caja.offset.x, y + caja.offset.y);
            if (bajo != null && (bajo.StartsWith("banqueta") || bajo.StartsWith("pasto") ||
                                 bajo.StartsWith("flores") || bajo.StartsWith("plaza")))
                throw new Exception($"{go.name} arranca sobre \"{bajo}\", fuera de la calle.");
        }
        return autos.Count;
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

        var circulo = SpriteCirculo();
        var padre = new GameObject("PuntosEntrega");
        Undo.RegisterCreatedObjectUndo(padre, "Construir mapa Residencial");

        int n = 0;
        foreach (var datos in Lista(mapa, "puntos_entrega").Cast<Dictionary<string, object>>())
        {
            n++;
            var punto = new GameObject((string)datos["nombre"]);
            punto.transform.SetParent(padre.transform, false);
            punto.transform.position = Punto(datos, origen);

            // DeliveryPoint toma el primer hijo como marcador: "Circle" debe ser el único
            var marca = new GameObject("Circle");
            marca.transform.SetParent(punto.transform, false);
            marca.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            var sr = marca.AddComponent<SpriteRenderer>();
            sr.sprite = circulo;
            sr.color = RosaEntrega;
            sr.sortingOrder = OrdenCirculo;

            punto.AddComponent<DeliveryPoint>();

            string bajo = TileEn(mapa, (float)Num(datos["x"]), (float)Num(datos["y"]));
            if (bajo == null || !bajo.StartsWith("banqueta"))
                Debug.LogWarning($"{punto.name} cae en \"{bajo}\", no en la banqueta.", punto);
        }
        int casas = Lista(mapa, "casas").Count;
        if (n != casas)
            throw new Exception($"Hay {casas} casas pero {n} puntos de entrega.");
        return n;
    }

    // ------------------------------------------------------------------ peatones y dueña

    // Hojas de personajes según personajes.json (nombre, rectángulo y pivote abajo-centro de cada celda)
    static List<HojaPersonaje> HojasPersonajes()
    {
        string archivo = Path.Combine(CarpetaPersonajes, ArchivoPersonajes);
        if (!File.Exists(archivo))
            throw new Exception($"No existe {archivo}. Ejecuta pixel_town/personajes/generar_personajes.py.");
        var json = (Dictionary<string, object>)Json.Leer(File.ReadAllText(archivo));
        var hojas = new List<HojaPersonaje>();
        foreach (var h in Lista(json, "hojas").Cast<Dictionary<string, object>>())
        {
            string ruta = $"{CarpetaPersonajes}/{h["imagen"]}";
            AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
            var imp = Importador(ruta);
            imp.GetSourceTextureWidthAndHeight(out int ancho, out int alto);
            var tam = new Vector2Int((int)Num(h["ancho"]), (int)Num(h["alto"]));
            if (ancho != tam.x || alto != tam.y)
                throw new Exception($"{ruta} mide {ancho}x{alto} px pero {ArchivoPersonajes} dice {tam.x}x{tam.y}.");
            hojas.Add(new HojaPersonaje
            {
                ruta = ruta,
                tam = tam,
                defs = Lista(h, "sprites").Cast<Dictionary<string, object>>()
                    .Select(s => new DefSprite { nombre = (string)s["name"], rect = RectDesdeArriba(s, alto), pivote = PivoteProps })
                    .ToList(),
            });
        }
        return hojas;
    }

    static string RutaPersonaje(string nombre) => $"{CarpetaPersonajes}/{nombre}.png";

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

        var (_, _, mapaT) = VerificarRecortes(RutaTileset, "tileset_suelo.json", defsTileset, null, errores, lineas);
        UnityEngine.Object.DestroyImmediate(mapaT);
        foreach (var h in hojas)
        {
            var (_, _, texH) = VerificarRecortes(h.ruta, ArchivoPersonajes, h.defs, h.tam, errores, lineas);
            UnityEngine.Object.DestroyImmediate(texH);
        }
        var (bienA, malA, importada) = VerificarRecortes(RutaAtlas, "props_atlas.json", defsAtlas,
            new Vector2Int((int)Num(atlas["ancho"]), (int)Num(atlas["alto"])), errores, lineas);
        try
        {
            lineas.Add(RevisarAutos(mapa, errores));
            lineas.Add(RevisarPiezasCompletas(atlas, mapa, errores));
            lineas.Add(RevisarPeatones(mapa, errores));
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
        int esperados = Lista(mapa, "autos_calle").Count;
        if (enCalle != esperados) errores.Add($"Hay {enCalle} autos con TrafficCar y el JSON trae {esperados}.");
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
            var tileset = LeerJson("tileset_suelo.json");
            var atlas = LeerJson("props_atlas.json");
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
        var suelo = UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .FirstOrDefault(t => t.name == "Suelo");
        if (suelo == null) throw new Exception("No hay Tilemap \"Suelo\" en la escena; construye el mapa primero.");
        var origen = suelo.transform.position;
        int cols = (int)Num(mapa["columnas"]), filas = (int)Num(mapa["filas"]);
        int w = cols * PPU, h = filas * PPU;

        var go = new GameObject("CamaraCaptura") { hideFlags = HideFlags.HideAndDontSave };
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var previa = RenderTexture.active;
        try
        {
            var cam = go.AddComponent<Camera>();
            go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            cam.orthographic = true;
            cam.orthographicSize = filas / 2f;
            cam.aspect = (float)cols / filas;
            cam.transform.position = new Vector3(origen.x + cols / 2f, origen.y + filas / 2f, -10f);
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
            string ruta = Path.Combine(CarpetaDatos, ArchivoCaptura).Replace('\\', '/');
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

    static Sprite SpriteCirculo()
    {
        string ruta = AssetDatabase.GUIDToAssetPath(GuidCirculo);
        var sprite = string.IsNullOrEmpty(ruta) ? null : AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>().FirstOrDefault();
        if (sprite == null) sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        if (sprite == null) throw new Exception("No encuentro un sprite circular para los puntos de entrega.");
        return sprite;
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

    static List<object> Lista(Dictionary<string, object> d, string clave)
    {
        if (!d.TryGetValue(clave, out var v) || !(v is List<object> lista))
            throw new Exception($"Al JSON le falta la lista \"{clave}\".");
        return lista;
    }

    static double Num(object o) => Convert.ToDouble(o, CultureInfo.InvariantCulture);

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
