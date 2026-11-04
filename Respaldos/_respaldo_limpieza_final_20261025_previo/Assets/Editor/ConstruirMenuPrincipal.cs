using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Crea la escena Assets/Scenes/MenuPrincipal.unity desde cero con la paleta del juego: título grande blanco con
// sombra rosa, el campo "Tu nombre" y pastillas pastel tipo piedra: "Iniciar juego" (dorada, la principal),
// "Instrucciones" y "Salir". La historia ya no es un panel: es la escena Intro.
// Todo con la fuente pixel (FuenteProyecto); el texto de las pastillas en #2A1F40, sin contorno, a 36
// como mínimo. Después deja el orden de escenas del build: MenuPrincipal (0), Intro (1), SampleScene (2).
// El fondo es "EL MIRADOR" (PresentacionMirador; arte y acomodo de pixel_town/generate_presentacion_mirador.py en
// Assets/Sprits/Mirador; reemplaza a la presentación viva del letrero colgante, cuyos assets se quedan sin usarse): dos
// jóvenes de espaldas en una colina al atardecer, él con la mano de visera y ella señalando el neón "SALÓN BRENN'S" que
// se enciende entre las luces de la ciudad, con luciérnagas; el título arriba en el cielo con sus adornos y su destello.
// Encima, dos partes: Presentacion (la franja oscura con "HAZ CLIC PARA CONTINUAR" parpadeando; la primera vez de cada
// sesión) y PanelMenu (sin caja: un degradado oscuro a la derecha y una columna de botones tipo letrero, con el nombre,
// las instrucciones y los iconos de Música y Efectos arriba a la derecha; arte de pixel_town/generate_menu_letreros.py en
// Assets/Sprits/MenuLetreros, 9-slices a x1 con bordes de 2 y 3 px), que aparece con un fundido (MenuPrincipal). El fondo y el título se ven siempre; "Negro" (el fundido
// de entrada de 1.2 s) va hasta adelante.
// Se puede ejecutar varias veces: la escena se rehace entera y se sobrescribe. La escena que esté
// abierta no se toca (el menú se arma en una escena aditiva que se cierra al guardar).
public static class ConstruirMenuPrincipal
{
    const string RutaEscena = "Assets/Scenes/MenuPrincipal.unity";
    internal const string RutaIntro = "Assets/Scenes/Intro.unity";
    const string RutaJuego = "Assets/Scenes/SampleScene.unity";
    const string CarpetaUI = "Assets/Sprits/UI/";
    const string RutaIdleFrente = "Assets/Animations/Idle_Frente.anim";
    internal const string CarpetaMirador = "Assets/Sprits/Mirador/";
    const string RutaAcomodo = CarpetaMirador + "mirador.json";
    public const int MaxObjetosAnimados = 60;
    internal const string CarpetaLetreros = "Assets/Sprits/MenuLetreros/";
    // la columna del menú (a 1920x1080; en el Canvas el origen va al centro), centrada en x = 1500: la placa "TU NOMBRE"
    // de 520x150 con el campo de 76 de alto adentro, "Iniciar juego" de 520x108 y los demás de 480x92, separados 24;
    // el degradado va de x = 1150 al borde
    public const float ColumnaX = 1500 - 960, Separacion = 24;
    public const float AnchoBoton = 480, AltoBoton = 92, AnchoPrincipal = 520, AltoPrincipal = 108;
    public const float AnchoPlaca = 520, AltoPlaca = 150, AltoCampo = 76, MargenPlaca = 16;
    public const float TamTexto = 34, TamPrincipal = 38, TamPlaca = 30, TamCampo = 34;
    public const float DegradadoDesde = 1150;
    public const float AlfaDegradado = 0.55f;
    const float ArribaColumna = (AltoPlaca + AltoPrincipal + 2 * AltoBoton + 3 * Separacion) / 2;   // la columna centrada en el alto
    public const float LadoIcono = 84, SeparacionIconos = 20, MargenIconos = 24, MargenGlifo = 6;   // glifos de 24 a x3 = 72
    // los 9-slices de los letreros (px por lado) y sus sprites
    static readonly Dictionary<string, int> BordesLetreros = new Dictionary<string, int>
    {
        { "boton_normal", 2 }, { "boton_normal_hover", 2 }, { "boton_principal", 6 }, { "boton_principal_hover", 6 },
        { "boton_peligro", 2 }, { "boton_peligro_hover", 2 }, { "campo", 2 }, { "placa", 2 }, { "panel", 3 },
        { "marco_icono", 2 }, { "marco_icono_hover", 2 }, { "marco_tarjeta", 3 }, { "marco_tarjeta_hover", 3 },
        { "icono_musica", 0 }, { "icono_efectos", 0 }, { "icono_pausa", 0 }, { "raya_apagado", 0 }, { "tijera", 0 },
        { "degradado_menu", 0 },
        { "hud_caja", 3 }, { "hud_marco", 2 }, { "hud_reloj_verde", 3 }, { "hud_reloj_amarilla", 3 }, { "hud_reloj_roja", 3 },
        { "hud_queja_vacia", 0 }, { "moneda", 0 },
    };
    internal static Sprite Letrero(string n) => ImportarPixel(CarpetaLetreros + n + ".png", Vector4.one * BordesLetreros[n]);
    static readonly Color32 Dorado = new Color32(0xFF, 0xD1, 0x66, 255);

    // mirador.json (generate_presentacion_mirador.py): cada pieza con su padre y su esquina de arriba a la izquierda
    // respecto al centro del padre (unidades del Canvas, y hacia arriba); todo a x4
    [Serializable] class Pieza { public string nombre; public string sprite; public float x; public float y; public string padre; }
    [Serializable] class Caja { public float x; public float y; public float w; public float h; }
    [Serializable] class Sol { public float bajada; public float duracion; }
    [Serializable] class Recorrido { public float desde; public float hasta; }
    [Serializable]
    class Acomodo
    {
        public Pieza[] piezas; public Caja luciernagas; public Sol sol; public Recorrido destello; public Caja caja;
        public float titulo; public float subtitulo; public float continuar; public int borde_caja;
    }

    // Igual que en el resto de la interfaz: cada píxel del arte ocupa 4 unidades del Canvas
    const int Escala = 4;

    static readonly Color32 Oscuro = new Color32(0x2A, 0x1F, 0x40, 255);
    static readonly Color32 Rosa = new Color32(0xFF, 0x6F, 0xA5, 255);
    static readonly Color32 Morado = new Color32(0x8C, 0x52, 0xFF, 255);
    static readonly Color32 Blanco = new Color32(0xFF, 0xFF, 0xFF, 255);

    const string TextoInstrucciones =
        "<b>Moverte:</b> W A S D o las flechas.\n" +
        "<b>Entregar:</b> tecla E cuando estés cerca de la puerta de la clienta.\n" +
        "<b>Pausa:</b> Esc o el botón de pausa.\n" +
        "La barra de satisfacción baja si tardas o chocas.\n" +
        "Más satisfacción, más estrellas y más propina.";

    // botón → método de MenuPrincipal, para conectar y para verificar
    static readonly (string boton, string metodo)[] Conexiones =
    {
        ("PanelMenu/BotonIniciar", "Jugar"), ("PanelMenu/BotonInstrucciones", "AbrirInstrucciones"), ("PanelMenu/BotonSalir", "Salir"),
        ("PanelMenu/PanelInstrucciones/Marco/BotonCerrar", "CerrarPaneles"),
    };

    public const string TextoContinuar = "HAZ CLIC PARA CONTINUAR";
    public const float PeriodoParpadeo = 1.2f;
    // lo que no puede faltar (también lo exige RevisarCanvas en disco)
    public static readonly string[] Requeridas =
    {
        "Fondo", "Fondo/Repartidor", "Fondo/Repartidora", "Fondo/CiudadCerca/LetreroNeon", "Fondo/Colina",
        "TituloGrupo/Titulo", "TituloGrupo/Subtitulo", "Presentacion", "Presentacion/Franja", "Presentacion/TextoContinuar",
        "PanelMenu", "PanelMenu/Degradado", "Negro", "PanelMenu/EtiquetaNombre", "PanelMenu/CampoNombre", "PanelMenu/BotonIniciar", "PanelMenu/BotonInstrucciones",
        "PanelMenu/BotonSalir", "PanelMenu/MenuSonido", "PanelMenu/MenuSonido/BotonMusica", "PanelMenu/MenuSonido/BotonEfectos",
        "PanelMenu/PanelInstrucciones",
    };

    [MenuItem("Delivery Brenn's/Construir menú principal")]
    static void ConstruirMenu()
    {
        try
        {
            Debug.Log(Construir(out _));
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // Arma, verifica y (si todo está bien) guarda la escena y el orden del build. Devuelve el informe
    static string Construir(out bool ok)
    {
        ok = false;
        AssetDatabase.Refresh();   // el arte recién generado en pixel_town
        // ---- Primero se revisa todo; si falta algo no se crea nada ----
        var sprites = BordesLetreros.Keys.ToDictionary(n => n, Letrero);
        foreach (var faltante in sprites.Where(s => s.Value == null))
            Debug.LogError($"Falta el sprite {CarpetaLetreros}{faltante.Key}.png (corre pixel_town/generate_menu_letreros.py).");
        var acomodo = File.Exists(RutaAcomodo) ? JsonUtility.FromJson<Acomodo>(File.ReadAllText(RutaAcomodo)) : null;
        if (acomodo == null || acomodo.piezas == null) Debug.LogError($"Falta {RutaAcomodo} (corre pixel_town/generate_presentacion_mirador.py).");
        else
        {
            foreach (var n in NombresMirador(acomodo).Where(n => Mir(n) == null))
                Debug.LogError($"Falta el sprite {CarpetaMirador}{n}.png (corre pixel_town/generate_presentacion_mirador.py).");
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaJuego) == null) Debug.LogError($"No existe {RutaJuego}.");
        var fuente = FuenteProyecto.Fuente();
        if (fuente == null) Debug.LogError("No se pudo crear la fuente pixel.");
        if (sprites.Values.Any(s => s == null) || fuente == null || acomodo?.piezas == null
            || NombresMirador(acomodo).Any(n => Mir(n) == null)
            || AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaJuego) == null)
            return "NO SE CONSTRUYÓ el menú principal (ver errores).";

        if (SceneManager.GetSceneByPath(RutaEscena).isLoaded)
        {
            var abierta = SceneManager.GetSceneByPath(RutaEscena);
            if (SceneManager.loadedSceneCount == 1)
                return "NO SE CONSTRUYÓ: MenuPrincipal es la única escena abierta. Abre SampleScene y vuelve a ejecutarlo.";
            if (abierta.isDirty) return "NO SE CONSTRUYÓ: MenuPrincipal está abierta con cambios sin guardar.";
            EditorSceneManager.CloseScene(abierta, true);
        }

        var activa = SceneManager.GetActiveScene();
        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        string informe;
        try
        {
            SceneManager.SetActiveScene(escena);
            Armar(sprites, acomodo);
            informe = Verificar(escena, out ok);
            if (ok)
            {
                bool guardada = EditorSceneManager.SaveScene(escena, RutaEscena);
                string build = "Build sin cambiar.";
                ok = guardada && PonerEscenasDelBuild(out build);
                informe = (guardada ? "Escena guardada en " + RutaEscena : "NO SE PUDO GUARDAR " + RutaEscena) + "\n" + build + "\n" + informe;
            }
            else informe = "MENÚ CON ERRORES (no se guardó)\n" + informe;
        }
        finally
        {
            if (activa.IsValid() && activa.isLoaded) SceneManager.SetActiveScene(activa);
            EditorSceneManager.CloseScene(escena, true);
        }
        return informe;
    }

    static void Armar(Dictionary<string, Sprite> sprites, Acomodo a)
    {
        var raiz = CrearBase("Canvas", out float referenciaPPU);

        var menu = new GameObject("MenuPrincipal").AddComponent<MenuPrincipal>();

        // ---- Fondo: El mirador (lo anima PresentacionMirador; se ve siempre, también detrás del PanelMenu) ----
        var fondo = CrearHijo("Fondo", raiz);
        Estirar(fondo);
        var mirador = fondo.gameObject.AddComponent<PresentacionMirador>();

        // ---- Título en el cielo: Press Start blanco con sombra rosa; adornos y destello (piezas del JSON) ----
        var tituloGrupo = CrearHijo("TituloGrupo", raiz);
        Centrar(tituloGrupo, 0, a.titulo, 1600, 200);
        foreach (var (nombre, color, dx, dy) in new[] { ("TituloSombraRosa", Rosa, 6, -8), ("Titulo", Blanco, 0, 0) })
        {
            var rt = CrearHijo(nombre, tituloGrupo);
            Centrar(rt, dx, dy, 1600, 200);
            var tmp = CrearTexto(rt, "Delivery Brenn's", 156, color, false);
            FuenteProyecto.AplicarTitulo(tmp);
            tmp.fontSize = TamTitulo;
        }
        var rtSub = CrearHijo("Subtitulo", tituloGrupo);
        Centrar(rtSub, 0, a.subtitulo - a.titulo, 900, 40);
        var sub = CrearTexto(rtSub, TextoSubtitulo, 24, FuenteProyecto.Crema, false);
        FuenteProyecto.AplicarTitulo(sub, FuenteProyecto.MaterialIntro());
        sub.fontSize = 24;

        // ---- Presentacion: la franja oscura con "HAZ CLIC PARA CONTINUAR" (la primera vez de cada sesión) ----
        var presentacion = CrearHijo("Presentacion", raiz);
        Estirar(presentacion);
        var grupoPresentacion = presentacion.gameObject.AddComponent<CanvasGroup>();
        grupoPresentacion.blocksRaycasts = false;

        // las piezas del mirador, de atrás hacia adelante, cada una en su padre
        var piezas = new Dictionary<string, Image>();
        foreach (var p in a.piezas)
        {
            var padre = raiz.Find(p.padre);
            piezas[p.nombre] = PonerPieza(p.nombre, padre, Mir(p.sprite), p.x, p.y, Escala);
        }
        piezas["HaloNeon"].enabled = false;
        piezas["Destello"].enabled = false;
        ConectarMirador(new SerializedObject(mirador), piezas, a);

        // "HAZ CLIC PARA CONTINUAR" en Press Start crema con contorno #2A1F40, parpadeo lento (el último hijo de la
        // presentación, encima de la franja)
        var rtContinuar = CrearHijo("TextoContinuar", presentacion);
        Centrar(rtContinuar, 0, a.continuar, 1400, 80);
        rtContinuar.gameObject.AddComponent<CanvasGroup>();
        var continuar = CrearTexto(rtContinuar, TextoContinuar, 40, FuenteProyecto.Crema, false);
        FuenteProyecto.AplicarTitulo(continuar, FuenteProyecto.MaterialIntro());
        continuar.fontSize = 40;
        var parpadeo = new SerializedObject(rtContinuar.gameObject.AddComponent<Parpadeo>());
        parpadeo.FindProperty("periodo").floatValue = PeriodoParpadeo;
        parpadeo.FindProperty("alphaMinima").floatValue = 0.2f;
        parpadeo.ApplyModifiedPropertiesWithoutUndo();

        // ---- PanelMenu: sin caja; el degradado a la derecha y la columna de letreros (empieza invisible: MenuPrincipal
        // decide) ----
        var panelMenu = CrearHijo("PanelMenu", raiz);
        Estirar(panelMenu);
        var grupoMenu = panelMenu.gameObject.AddComponent<CanvasGroup>();
        grupoMenu.alpha = 0f;
        grupoMenu.interactable = grupoMenu.blocksRaycasts = false;
        var degradado = CrearHijo("Degradado", panelMenu);
        degradado.anchorMin = new Vector2(DegradadoDesde / 1920f, 0f);
        degradado.anchorMax = Vector2.one;
        degradado.offsetMin = degradado.offsetMax = Vector2.zero;
        var imgDegradado = degradado.gameObject.AddComponent<Image>();
        imgDegradado.sprite = sprites["degradado_menu"];
        imgDegradado.raycastTarget = false;

        // de arriba abajo: la placa "TU NOMBRE" (con el campo adentro), "Iniciar juego", "Instrucciones" y "Salir"
        var estilo = ConstruirBotones.Estilo();
        float y = ArribaColumna;
        float Siguiente(float alto) { float c = y - alto / 2; y -= alto + Separacion; return c; }
        var placa = CrearHijo("EtiquetaNombre", panelMenu);
        float yPlaca = Siguiente(AltoPlaca);
        Centrar(placa, ColumnaX, yPlaca, AnchoPlaca, AltoPlaca);
        Rebanado(placa.gameObject.AddComponent<Image>(), sprites["placa"], referenciaPPU).raycastTarget = false;
        var rtPlaca = CrearHijo("Texto", placa);
        rtPlaca.anchorMin = new Vector2(0f, 1f);
        rtPlaca.anchorMax = Vector2.one;
        rtPlaca.pivot = new Vector2(0.5f, 1f);
        rtPlaca.sizeDelta = new Vector2(0f, AltoPlaca - AltoCampo - 2 * MargenPlaca + 8);
        rtPlaca.anchoredPosition = new Vector2(0f, -MargenPlaca / 2);
        var tmpPlaca = CrearTexto(rtPlaca, "TU NOMBRE", TamPlaca, Rosa, false);
        FuenteProyecto.AplicarTitulo(tmpPlaca);
        tmpPlaca.fontSize = TamPlaca;

        // el campo, abajo dentro de la placa
        var campo = CrearCampoNombre(panelMenu, sprites["campo"], referenciaPPU, ColumnaX, yPlaca - AltoPlaca / 2 + MargenPlaca + AltoCampo / 2);
        var iniciar = CrearLetrero("BotonIniciar", panelMenu, "Iniciar juego", ColumnaX, Siguiente(AltoPrincipal), BotonBrenn.Variante.Principal, estilo);
        var instrucciones = CrearLetrero("BotonInstrucciones", panelMenu, "Instrucciones", ColumnaX, Siguiente(AltoBoton), BotonBrenn.Variante.Normal, estilo);
        var salir = CrearLetrero("BotonSalir", panelMenu, "Salir", ColumnaX, Siguiente(AltoBoton), BotonBrenn.Variante.Peligro, estilo);

        // ---- Música y Efectos: iconos arriba a la derecha ----
        CrearControlesSonido(panelMenu);

        // ---- Panel de instrucciones (encima de todo, empieza cerrado) ----
        var panelInstrucciones = CrearPanelTexto("PanelInstrucciones", panelMenu, "Instrucciones", TextoInstrucciones,
                                                 sprites, referenciaPPU, out var cerrar);
        Navegacion(campo, iniciar, instrucciones, salir);

        // ---- Negro: el fundido de entrada (hasta adelante, sin tapar clics) ----
        var negro = CrearHijo("Negro", raiz);
        Estirar(negro);
        var imgNegro = negro.gameObject.AddComponent<Image>();
        imgNegro.color = Color.black;
        imgNegro.raycastTarget = false;
        var soMirador = new SerializedObject(mirador);
        soMirador.FindProperty("negro").objectReferenceValue = imgNegro;
        soMirador.ApplyModifiedPropertiesWithoutUndo();

        // ---- Conexiones ----
        UnityEventTools.AddPersistentListener(iniciar.onClick, menu.Jugar);
        UnityEventTools.AddPersistentListener(instrucciones.onClick, menu.AbrirInstrucciones);
        UnityEventTools.AddPersistentListener(salir.onClick, menu.Salir);
        UnityEventTools.AddPersistentListener(cerrar.onClick, menu.CerrarPaneles);

        var so = new SerializedObject(menu);
        so.FindProperty("campoNombre").objectReferenceValue = campo;
        so.FindProperty("panelHistoria").objectReferenceValue = null;   // la historia ahora es la escena Intro
        so.FindProperty("panelInstrucciones").objectReferenceValue = panelInstrucciones;
        so.FindProperty("escenaIntro").stringValue = Path.GetFileNameWithoutExtension(RutaIntro);
        so.FindProperty("escenaJuego").stringValue = ConstruirSeleccionNivel.Escena;   // sin Seleccion ni Intro
        so.FindProperty("presentacion").objectReferenceValue = grupoPresentacion;
        so.FindProperty("panelMenu").objectReferenceValue = grupoMenu;
        so.FindProperty("primerBoton").objectReferenceValue = iniciar.gameObject;
        so.FindProperty("botonInstrucciones").objectReferenceValue = instrucciones.gameObject;
        so.FindProperty("esperaPresentacion").floatValue = 1.2f;   // el fundido de entrada: el clic de dar el foco no cuenta
        so.ApplyModifiedPropertiesWithoutUndo();

        // se apaga al final: TextMeshPro no inicializa su material en un objeto inactivo
        panelInstrucciones.SetActive(false);
    }

    public const string TextoSubtitulo = "ENTREGAS CON ESTILO";

    static Sprite Mir(string nombre) => ImportarPixel(CarpetaMirador + nombre + ".png", Vector4.zero);

    // Los cuadros que cambia PresentacionMirador (además de los sprites de las piezas del JSON)
    static readonly string[] CuadrosMirador =
    {
        "estrella_0", "estrella_1", "ventana_encendida", "ventana_apagada", "letrero_apagado", "letrero_encendido", "halo",
        "repartidor_visera_0", "repartidor_visera_1", "repartidor_visera_2", "repartidora_espera_0", "repartidora_espera_1",
        "repartidora_espera_2", "repartidora_medio", "repartidora_senala_0", "repartidora_senala_1", "repartidora_senala_2",
        "luciernaga_oro_0", "luciernaga_oro_1", "luciernaga_verde_0", "luciernaga_verde_1", "destello",
    };

    static IEnumerable<string> NombresMirador(Acomodo a) => CuadrosMirador.Concat(a.piezas.Select(p => p.sprite)).Distinct();

    static Object[] Cuadros(params string[] nombres) => nombres.Select(n => (Object)Mir(n)).ToArray();

    // Conecta PresentacionMirador con las piezas (por nombre) y sus cuadros
    static void ConectarMirador(SerializedObject so, Dictionary<string, Image> piezas, Acomodo a)
    {
        Object[] Con(string prefijo, bool rect) => piezas.Where(kv => kv.Key.StartsWith(prefijo))
            .OrderBy(kv => int.Parse(kv.Key.Substring(prefijo.Length))).Select(kv => rect ? (Object)kv.Value.rectTransform : kv.Value).ToArray();
        void Flotantes(string campo, params float[] v)
        {
            var prop = so.FindProperty(campo);
            prop.arraySize = v.Length;
            for (int i = 0; i < v.Length; i++) prop.GetArrayElementAtIndex(i).floatValue = v[i];
        }
        PonerArreglo(so, "estrellas", Con("Estrella", false));
        PonerArreglo(so, "cuadrosEstrella", Cuadros("estrella_0", "estrella_1"));
        so.FindProperty("sol").objectReferenceValue = piezas["Sol"].rectTransform;
        so.FindProperty("bajadaSol").floatValue = a.sol.bajada;
        so.FindProperty("duracionSol").floatValue = a.sol.duracion;
        var nubes = Con("Nube", true);
        PonerArreglo(so, "nubes", nubes);
        Flotantes("velocidadNubes", new[] { 6f, 10f, 8f, 7f }.Take(nubes.Length).ToArray());
        PonerArreglo(so, "capas", new Object[] { piezas["CiudadLejos"].rectTransform, piezas["CiudadCerca"].rectTransform });
        Flotantes("amplitudCapas", 4f, 8f);
        PonerArreglo(so, "ventanas", Con("Ventana", false));
        so.FindProperty("ventanaEncendida").objectReferenceValue = Mir("ventana_encendida");
        so.FindProperty("ventanaApagada").objectReferenceValue = Mir("ventana_apagada");
        so.FindProperty("letrero").objectReferenceValue = piezas["LetreroNeon"];
        so.FindProperty("letreroApagado").objectReferenceValue = Mir("letrero_apagado");
        so.FindProperty("letreroEncendido").objectReferenceValue = Mir("letrero_encendido");
        so.FindProperty("halo").objectReferenceValue = piezas["HaloNeon"];
        so.FindProperty("repartidor").objectReferenceValue = piezas["Repartidor"];
        PonerArreglo(so, "cuadrosVisera", Cuadros("repartidor_visera_0", "repartidor_visera_1", "repartidor_visera_2"));
        so.FindProperty("repartidora").objectReferenceValue = piezas["Repartidora"];
        PonerArreglo(so, "cuadrosEspera", Cuadros("repartidora_espera_0", "repartidora_espera_1", "repartidora_espera_2"));
        so.FindProperty("cuadroMedio").objectReferenceValue = Mir("repartidora_medio");
        PonerArreglo(so, "cuadrosSenala", Cuadros("repartidora_senala_0", "repartidora_senala_1", "repartidora_senala_2"));
        PonerArreglo(so, "luciernagas", Con("Luciernaga", false));
        PonerArreglo(so, "cuadrosLuciernaga", Cuadros("luciernaga_oro_0", "luciernaga_oro_1", "luciernaga_verde_0", "luciernaga_verde_1"));
        var z = a.luciernagas;
        so.FindProperty("zonaLuciernagas").rectValue = new Rect(z.x, z.y - z.h, z.w, z.h);
        so.FindProperty("destello").objectReferenceValue = piezas["Destello"];
        so.FindProperty("destelloDesde").floatValue = a.destello.desde;
        so.FindProperty("destelloHasta").floatValue = a.destello.hasta;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Imagen de pixel art con su esquina de arriba a la izquierda en (x, y) respecto al centro del padre, a escala entera
    static Image PonerPieza(string nombre, Transform padre, Sprite sprite, float x, float y, int escala, Vector2? pivote = null)
    {
        var rt = CrearHijo(nombre, padre);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = pivote ?? new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = sprite.rect.size * escala;
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    static void PonerArreglo(SerializedObject so, string campo, IList<Object> valores)
    {
        var prop = so.FindProperty(campo);
        prop.arraySize = valores.Count;
        for (int i = 0; i < valores.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = valores[i];
    }

    // Música y Efectos del menú: dos iconos de 48x48 (nota y bocina, el arte a x2) arriba a la derecha, separados 16, con
    // el marco rosa (dorado al elegirlos) y la raya roja encima cuando están apagados. La lógica y el guardado son los de
    // BotonSonido (AudioManager, PlayerPrefs). También lo usa ConstruirSonido.Aplicar (idempotente: rehace MenuSonido)
    internal static void CrearControlesSonido(Transform raiz)
    {
        var viejo = raiz.Find("MenuSonido");
        if (viejo != null) UnityEngine.Object.DestroyImmediate(viejo.gameObject);
        var cont = CrearHijo("MenuSonido", raiz);
        float ancho = LadoIcono * 2 + SeparacionIconos;
        Centrar(cont, 960 - MargenIconos - ancho / 2, 540 - MargenIconos - LadoIcono / 2, ancho, LadoIcono);
        var botones = new List<Button>();
        foreach (var (nombre, canal, icono) in new[] { ("BotonMusica", BotonSonido.Canal.Musica, "icono_musica"),
                                                      ("BotonEfectos", BotonSonido.Canal.Efectos, "icono_efectos") })
        {
            var rt = CrearHijo(nombre, cont);
            Centrar(rt, (botones.Count == 0 ? -1 : 1) * (LadoIcono + SeparacionIconos) / 2, 0, LadoIcono, LadoIcono);
            var boton = rt.gameObject.AddComponent<Button>();
            var marco = BotonBrenn.Vestir(boton, BotonBrenn.Variante.Icono, ConstruirBotones.Estilo()).Borde;
            var rtIcono = CrearHijo("Icono", rt);
            Estirar(rtIcono);
            rtIcono.offsetMin = Vector2.one * MargenGlifo;
            rtIcono.offsetMax = -Vector2.one * MargenGlifo;
            var imgIcono = rtIcono.gameObject.AddComponent<Image>();
            imgIcono.sprite = Letrero(icono);
            imgIcono.raycastTarget = false;
            var rtRaya = CrearHijo("Raya", rt);
            Estirar(rtRaya);
            rtRaya.offsetMin = Vector2.one * MargenGlifo;
            rtRaya.offsetMax = -Vector2.one * MargenGlifo;
            var imgRaya = rtRaya.gameObject.AddComponent<Image>();
            imgRaya.sprite = Letrero("raya_apagado");
            imgRaya.raycastTarget = false;

            var bs = rt.gameObject.AddComponent<BotonSonido>();
            var so = new SerializedObject(bs);
            so.FindProperty("canal").enumValueIndex = (int)canal;
            so.FindProperty("fondo").objectReferenceValue = marco;
            so.FindProperty("raya").objectReferenceValue = rtRaya.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(boton.onClick, bs.Alternar);
            botones.Add(boton);
        }
        botones[0].navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = botones[1] };
        botones[1].navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = botones[0] };
        var instrucciones = raiz.Find("PanelInstrucciones");
        if (instrucciones != null) cont.SetSiblingIndex(instrucciones.GetSiblingIndex());   // el panel queda encima
    }

    // Cámara (con el AudioListener: sin él no se oye la música), EventSystem con el Input System y Canvas 1920x1080
    // (match 0.5). Lo usa también ConstruirIntro
    internal static RectTransform CrearBase(string nombreCanvas, out float referenciaPPU)
    {
        var camara = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camara.tag = "MainCamera";
        camara.orthographic = true;
        camara.clearFlags = CameraClearFlags.SolidColor;
        camara.backgroundColor = Oscuro;
        camara.transform.position = new Vector3(0, 0, -10);
        camara.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();   // URP lo pide

        var eventos = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventos.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

        var goCanvas = new GameObject(nombreCanvas, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        goCanvas.layer = LayerMask.NameToLayer("UI");
        goCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var escalador = goCanvas.GetComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920, 1080);
        escalador.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        escalador.matchWidthOrHeight = 0.5f;
        referenciaPPU = escalador.referencePixelsPerUnit;
        return (RectTransform)goCanvas.transform;
    }

    // El orden de OrdenEscenas (MenuPrincipal 0, Seleccion 1, Intro 2, SeleccionNivel 3, SampleScene 4, Nivel2 5, Nivel3 6, Final 7); las demás
    // escenas se quedan detrás. Que falte una escena todavía no construida no es un error del menú
    internal static bool PonerEscenasDelBuild(out string informe)
    {
        informe = OrdenEscenas.Poner(out bool ok);
        var reales = EditorBuildSettings.scenes.Select(e => e.path).ToList();
        var existentes = OrdenEscenas.Rutas.Where(File.Exists).ToList();
        return ok || existentes.Select((r, i) => reales.Count > i && reales[i] == r).All(b => b);
    }

    // Deja un PNG de pixel art importado como Sprite: Point, sin compresión ni mipmaps, PPU 16 y con el
    // 9-slice indicado. Lo usa también ConstruirIntro
    internal static Sprite ImportarPixel(string ruta, Vector4 borde, float ppu = 16)
    {
        if (!(AssetImporter.GetAtPath(ruta) is TextureImporter imp)) return null;
        bool cambia = imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode != SpriteImportMode.Single
                      || imp.filterMode != FilterMode.Point || imp.textureCompression != TextureImporterCompression.Uncompressed
                      || imp.mipmapEnabled || imp.spriteBorder != borde || !Mathf.Approximately(imp.spritePixelsPerUnit, ppu)
                      || imp.npotScale != TextureImporterNPOTScale.None;
        if (cambia)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.spritePixelsPerUnit = ppu;
            imp.spriteBorder = borde;
            imp.maxTextureSize = 2048;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
    }

    // Image 9-slice a x1 (cada píxel del sprite, un píxel de la pantalla a 1920x1080: bordes de 2 y 3 px nítidos)
    static Image Rebanado(Image img, Sprite sprite, float referenciaPPU)
    {
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = referenciaPPU / sprite.pixelsPerUnit;
        return img;
    }

    // Overlay oscuro a pantalla completa que bloquea clics, con el panel oscuro de borde rosa, título rosa, texto crema y
    // "Cerrar" (un letrero como los del menú)
    static GameObject CrearPanelTexto(string nombre, RectTransform raiz, string titulo, string texto,
                                      Dictionary<string, Sprite> sprites, float referenciaPPU, out Button cerrar)
    {
        var overlay = CrearHijo(nombre, raiz);
        Estirar(overlay);
        var velo = overlay.gameObject.AddComponent<Image>();
        velo.color = new Color(0, 0, 0, 0.6f);

        var marco = CrearHijo("Marco", overlay);
        Centrar(marco, 0, 0, 1100, 640);
        Rebanado(marco.gameObject.AddComponent<Image>(), sprites["panel"], referenciaPPU);

        var rtTitulo = CrearHijo("Titulo", marco);
        Centrar(rtTitulo, 0, 228, 900, 90);
        var tmpTitulo = CrearTexto(rtTitulo, titulo, 48, Rosa, false);
        FuenteProyecto.AplicarTitulo(tmpTitulo);
        tmpTitulo.fontSize = 40;

        var rtTexto = CrearHijo("Texto", marco);
        Centrar(rtTexto, 0, 10, 960, 300);
        var tmp = CrearTexto(rtTexto, texto, 38, FuenteProyecto.Crema, false);
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.lineSpacing = 12;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 28;
        tmp.fontSizeMax = 38;

        cerrar = CrearLetrero("BotonCerrar", marco, "Cerrar", 0, -230, BotonBrenn.Variante.Normal, ConstruirBotones.Estilo());
        return overlay.gameObject;
    }

    // Campo del nombre: #2A1F40 con borde morado de 2 px y el texto crema a la izquierda, en la fuente pixel
    static TMP_InputField CrearCampoNombre(RectTransform padre, Sprite marco, float referenciaPPU, float x, float y)
    {
        var rt = CrearHijo("CampoNombre", padre);
        Centrar(rt, x, y, AnchoPlaca - 2 * MargenPlaca, AltoCampo);
        var img = Rebanado(rt.gameObject.AddComponent<Image>(), marco, referenciaPPU);

        var area = CrearHijo("Text Area", rt);
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(18, 8);
        area.offsetMax = new Vector2(-18, -8);
        area.gameObject.AddComponent<RectMask2D>();

        var crema = FuenteProyecto.Crema;
        var rtPlaceholder = CrearHijo("Placeholder", area);
        Estirar(rtPlaceholder);
        var placeholder = CrearTexto(rtPlaceholder, "Escribe tu nombre", TamCampo, new Color32(crema.r, crema.g, crema.b, 120), false);
        placeholder.alignment = TextAlignmentOptions.Left;

        var rtTexto = CrearHijo("Text", area);
        Estirar(rtTexto);
        var texto = CrearTexto(rtTexto, "", TamCampo, crema, false);
        texto.alignment = TextAlignmentOptions.Left;

        var campo = rt.gameObject.AddComponent<TMP_InputField>();
        campo.targetGraphic = img;
        campo.transition = Selectable.Transition.None;
        campo.textViewport = area;
        campo.textComponent = texto;
        campo.placeholder = placeholder;
        campo.fontAsset = FuenteProyecto.Fuente();
        campo.pointSize = TamCampo;
        campo.characterLimit = 16;
        campo.lineType = TMP_InputField.LineType.SingleLine;
        campo.customCaretColor = true;
        campo.caretColor = crema;
        campo.caretWidth = 3;
        campo.selectionColor = new Color32(Rosa.r, Rosa.g, Rosa.b, 120);
        return campo;
    }

    // Botón del menú con el estilo único (BotonBrenn): Principal de 520x108 con texto de 38, los demás de 480x92 con
    // texto de 34; elegido (ratón o teclado), borde y texto dorados y la tijera parpadeando a la izquierda; pulsado,
    // baja 3 px
    static Button CrearLetrero(string nombre, Transform padre, string texto, float x, float y, BotonBrenn.Variante v, EstiloBrenn estilo)
    {
        bool principal = v == BotonBrenn.Variante.Principal;
        var rt = CrearHijo(nombre, padre);
        Centrar(rt, x, y, principal ? AnchoPrincipal : AnchoBoton, principal ? AltoPrincipal : AltoBoton);
        rt.gameObject.AddComponent<Image>();
        var boton = rt.gameObject.AddComponent<Button>();
        var rtTexto = CrearHijo("Texto", rt);
        CrearTexto(rtTexto, texto, TamTexto, FuenteProyecto.Crema, true);
        BotonBrenn.Vestir(boton, v, estilo, null, principal ? TamPrincipal : TamTexto);
        return boton;
    }

    // Flechas arriba y abajo por la columna (campo, Iniciar, Instrucciones, Salir); desde el campo, arriba van los iconos
    static void Navegacion(params Selectable[] columna)
    {
        for (int i = 0; i < columna.Length; i++)
            columna[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = i > 0 ? columna[i - 1] : null,
                selectOnDown = i < columna.Length - 1 ? columna[i + 1] : null,
            };
    }

    const float TamTitulo = 88;   // Press Start: 16 letras de 88 caben en los 1600 del título

    static TextMeshProUGUI CrearTexto(RectTransform rt, string texto, float tam, Color color, bool titulo)
    {
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        FuenteProyecto.Aplicar(tmp, titulo);
        tmp.text = texto;
        tmp.fontSize = titulo ? FuenteProyecto.Equivalente(tam) : tam;   // los títulos van en Press Start
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    // El primer fotograma del clip Idle_Frente del jugador
    internal static Sprite SpriteIdleFrente()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(RutaIdleFrente);
        if (clip == null) return null;
        foreach (var enlace in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            var claves = AnimationUtility.GetObjectReferenceCurve(clip, enlace);
            if (claves.Length > 0 && claves[0].value is Sprite s) return s;
        }
        return null;
    }

    // Revisa lo que quedó en la escena del menú; devuelve el informe y si todo está bien
    static string Verificar(Scene escena, out bool ok)
    {
        var errores = new List<string>();
        var raices = escena.GetRootGameObjects();
        var canvas = raices.Select(g => g.GetComponent<Canvas>()).FirstOrDefault(c => c != null);
        var menu = raices.Select(g => g.GetComponent<MenuPrincipal>()).FirstOrDefault(m => m != null);
        if (canvas == null || menu == null)
        {
            ok = false;
            return "VERIFICACIÓN: falta el Canvas o el objeto MenuPrincipal.";
        }
        errores.AddRange(VerificarBase(raices, canvas));

        var raiz = canvas.transform;
        foreach (var ruta in Requeridas)
            if (raiz.Find(ruta) == null) errores.Add($"Falta Canvas/{ruta}.");
        foreach (var (ruta, metodo) in Conexiones)
        {
            var boton = raiz.Find(ruta)?.GetComponent<Button>();
            if (boton == null) { errores.Add($"No existe el botón {ruta}."); continue; }
            if (boton.onClick.GetPersistentEventCount() != 1 || boton.onClick.GetPersistentTarget(0) != menu
                || boton.onClick.GetPersistentMethodName(0) != metodo)
                errores.Add($"{ruta} no llama (solo) a MenuPrincipal.{metodo}.");
            var img = boton.GetComponent<Image>();
            if (img == null || img.sprite == null || !img.sprite.name.StartsWith("boton_") || img.type != Image.Type.Sliced)
                errores.Add($"{ruta}: no es un letrero (9-slice de MenuLetreros).");
            var letrero = boton.GetComponent<BotonBrenn>();
            var tijera = boton.transform.Find("Tijera")?.GetComponent<Image>();
            if (letrero == null || tijera == null || tijera.enabled || tijera.sprite == null || tijera.sprite.name != "tijera")
                errores.Add($"{ruta}: falta BotonBrenn con la tijera (apagada hasta que lo eligen).");
            else
            {
                var sl = new SerializedObject(letrero);
                foreach (var campoLetrero in new[] { "borde", "spriteNormal", "spriteElegido", "texto", "tijera" })
                    if (sl.FindProperty(campoLetrero).objectReferenceValue == null) errores.Add($"{ruta}: BotonBrenn.{campoLetrero} vacío.");
            }
            if (boton.transition != Selectable.Transition.None) errores.Add($"{ruta}: la transición la pinta BotonBrenn (None).");
        }
        if (raiz.GetComponentsInChildren<Transform>(true).Any(t => t.name.Contains("Historia")))
            errores.Add("Todavía hay un botón o panel de Historia.");
        errores.AddRange(VerificarTextos(raiz, "Boton", FuenteProyecto.Crema, Dorado));
        errores.AddRange(VerificarColumna(raiz));

        var so = new SerializedObject(menu);
        var campo = so.FindProperty("campoNombre").objectReferenceValue as TMP_InputField;
        if (campo == null || campo.transform.parent != raiz.Find("PanelMenu")) errores.Add("MenuPrincipal: Campo Nombre no es el CampoNombre del menú.");
        else if (!(campo.placeholder is TMP_Text ph) || ph.text != "Escribe tu nombre") errores.Add("CampoNombre: el placeholder no dice \"Escribe tu nombre\".");
        else if (campo.fontAsset != FuenteProyecto.Fuente()) errores.Add("CampoNombre: no usa la fuente pixel.");
        if (so.FindProperty("panelHistoria").objectReferenceValue != null) errores.Add("MenuPrincipal: Panel Historia debería quedar vacío.");
        var instrucciones = so.FindProperty("panelInstrucciones").objectReferenceValue as GameObject;
        if (instrucciones == null || instrucciones.name != "PanelInstrucciones") errores.Add("MenuPrincipal: Panel Instrucciones sin asignar.");
        else if (instrucciones.activeSelf) errores.Add("PanelInstrucciones quedó activo.");
        if (so.FindProperty("primerBoton").objectReferenceValue != raiz.Find("PanelMenu/BotonIniciar")?.gameObject
            || so.FindProperty("botonInstrucciones").objectReferenceValue != raiz.Find("PanelMenu/BotonInstrucciones")?.gameObject)
            errores.Add("MenuPrincipal: Primer Boton (Iniciar) o Boton Instrucciones sin asignar (teclado).");
        if (so.FindProperty("escenaIntro").stringValue != "Intro") errores.Add("MenuPrincipal: Escena Intro no es \"Intro\".");
        if (so.FindProperty("escenaJuego").stringValue != ConstruirSeleccionNivel.Escena) errores.Add($"MenuPrincipal: Escena Juego no es {ConstruirSeleccionNivel.Escena}.");
        foreach (var n in new[] { "Fondo/Repartidor", "Fondo/Repartidora" })
            if (raiz.Find(n)?.GetComponent<Image>()?.sprite == null) errores.Add($"Falta {n}.");
        if (raiz.Find("TituloGrupo/Titulo")?.GetComponent<TextMeshProUGUI>()?.text != "Delivery Brenn's") errores.Add("Falta el título.");
        foreach (var n in new[] { "Titulo", "TituloSombraRosa" })
            if (!FuenteProyecto.EsTitulo(raiz.Find("TituloGrupo/" + n)?.GetComponent<TextMeshProUGUI>()?.font)) errores.Add($"{n}: no usa Press Start 2P.");

        // presentación y PanelMenu
        var grupoPresentacion = so.FindProperty("presentacion").objectReferenceValue as CanvasGroup;
        var grupoMenu = so.FindProperty("panelMenu").objectReferenceValue as CanvasGroup;
        if (grupoPresentacion == null || grupoPresentacion.name != "Presentacion") errores.Add("MenuPrincipal: Presentacion sin asignar.");
        else if (!grupoPresentacion.gameObject.activeSelf || grupoPresentacion.alpha < 1f || grupoPresentacion.blocksRaycasts)
            errores.Add("Presentacion: tiene que empezar encendida, visible y sin tapar clics.");
        if (grupoMenu == null || grupoMenu.name != "PanelMenu") errores.Add("MenuPrincipal: Panel Menu sin asignar.");
        else if (grupoMenu.alpha > 0f || grupoMenu.interactable || grupoMenu.blocksRaycasts) errores.Add("PanelMenu: tiene que empezar invisible y sin clics.");
        var continuar = raiz.Find("Presentacion/TextoContinuar");
        var tmpContinuar = continuar?.GetComponent<TextMeshProUGUI>();
        if (tmpContinuar == null || tmpContinuar.text != TextoContinuar || !FuenteProyecto.EsTitulo(tmpContinuar.font)
            || tmpContinuar.fontSharedMaterial != FuenteProyecto.MaterialIntro() || ColorUtility.ToHtmlStringRGB(tmpContinuar.color) != "FFF1D0")
            errores.Add($"TextoContinuar: \"{TextoContinuar}\" en Press Start crema con el contorno #2A1F40.");
        else if (FuenteProyecto.Ancho(tmpContinuar, TextoContinuar) > tmpContinuar.rectTransform.rect.width) errores.Add("TextoContinuar no cabe.");
        var parpadeo = continuar?.GetComponent<Parpadeo>();
        if (parpadeo == null || !Mathf.Approximately(new SerializedObject(parpadeo).FindProperty("periodo").floatValue, PeriodoParpadeo))
            errores.Add($"TextoContinuar: falta Parpadeo de {PeriodoParpadeo} s.");
        // todo lo que se toca está dentro del PanelMenu
        foreach (var b in raiz.GetComponentsInChildren<Selectable>(true))
            if (!b.transform.IsChildOf(raiz.Find("PanelMenu"))) errores.Add($"{b.name} está fuera del PanelMenu.");
        errores.AddRange(VerificarMirador(raiz));
        errores.AddRange(ConstruirSonido.Verificar(raiz.Find("PanelMenu/MenuSonido"), "MenuSonido"));

        ok = errores.Count == 0;
        return $"VERIFICACIÓN: MenuPrincipal con El mirador, la presentación y el PanelMenu ({Conexiones.Length} botones conectados, campo de nombre y panel de instrucciones)." +
               (ok ? "\nSin errores." : "\nERRORES:\n- " + string.Join("\n- ", errores));
    }

    // El mirador: todo conectado, menos de MaxObjetosAnimados objetos animados, el orden (fondo, título, presentación,
    // PanelMenu, negro), "HAZ CLIC" al final de la presentación, el degradado del menú sin tapar a los personajes ni el
    // letrero, y las piezas nítidas (escala entera, Point y en la rejilla de x4)
    static IEnumerable<string> VerificarMirador(Transform raiz)
    {
        var fondo = raiz.Find("Fondo");
        var mirador = fondo?.GetComponent<PresentacionMirador>();
        if (mirador == null) { yield return "Fondo: falta PresentacionMirador."; yield break; }
        var so = new SerializedObject(mirador);
        foreach (var campo in new[] { "negro", "sol", "ventanaEncendida", "ventanaApagada", "letrero", "letreroApagado", "letreroEncendido",
                                      "halo", "repartidor", "repartidora", "cuadroMedio", "destello" })
            if (so.FindProperty(campo).objectReferenceValue == null) yield return $"PresentacionMirador: {campo} vacío.";
        foreach (var (campo, minimo) in new[] { ("estrellas", 3), ("cuadrosEstrella", 2), ("nubes", 2), ("capas", 2), ("ventanas", 6),
                                                ("cuadrosVisera", 3), ("cuadrosEspera", 3), ("cuadrosSenala", 3), ("luciernagas", 14),
                                                ("cuadrosLuciernaga", 4) })
        {
            var prop = so.FindProperty(campo);
            if (prop.arraySize < minimo) yield return $"PresentacionMirador: {campo} tiene {prop.arraySize} (mínimo {minimo}).";
            else if (Enumerable.Range(0, prop.arraySize).Any(i => prop.GetArrayElementAtIndex(i).objectReferenceValue == null))
                yield return $"PresentacionMirador: {campo} tiene huecos.";
        }
        int luciernagas = so.FindProperty("luciernagas").arraySize;
        if (luciernagas < 14 || luciernagas > 20) yield return $"Tienen que ser de 14 a 20 luciérnagas ({luciernagas}).";
        if (mirador.ObjetosAnimados >= MaxObjetosAnimados) yield return $"El mirador: {mirador.ObjetosAnimados} objetos animados (máximo {MaxObjetosAnimados - 1}).";
        // orden de adelante hacia atrás
        string[] orden = { "Fondo", "TituloGrupo", "Presentacion", "PanelMenu", "Negro" };
        var indices = orden.Select(n => raiz.Find(n)?.GetSiblingIndex() ?? -1).ToArray();
        if (indices.Any(i => i < 0) || !indices.Zip(indices.Skip(1), (x, y) => x < y).All(b => b) || indices[^1] != raiz.childCount - 1)
            yield return "Orden: Fondo, TituloGrupo, Presentacion, PanelMenu y Negro hasta adelante.";
        var pres = raiz.Find("Presentacion");
        if (pres != null && pres.GetChild(pres.childCount - 1).name != "TextoContinuar") yield return "TextoContinuar tiene que ser el último hijo de Presentacion.";
        var negro = raiz.Find("Negro")?.GetComponent<Image>();
        if (negro != null && negro.raycastTarget) yield return "Negro no debe tapar clics.";
        // el degradado: el primero del PanelMenu, sin clics, de x = 1150 al borde, sin tapar a los personajes ni al letrero
        var degradado = raiz.Find("PanelMenu/Degradado")?.GetComponent<Image>();
        if (degradado == null || degradado.raycastTarget || degradado.transform.GetSiblingIndex() != 0 || degradado.sprite == null
            || degradado.sprite.name != "degradado_menu" || !Mathf.Approximately(degradado.rectTransform.anchorMin.x * 1920f, DegradadoDesde))
            yield return $"PanelMenu/Degradado: el primero del PanelMenu, sin clics, de x = {DegradadoDesde} al borde.";
        else
        {
            var tex = degradado.sprite.texture;
            float alfa = tex.isReadable ? tex.GetPixel(tex.width - 1, 0).a : AlfaDegradado;
            if (Mathf.Abs(alfa - AlfaDegradado) > 0.01f) yield return $"El degradado no llega a #2A1F40 al {AlfaDegradado:P0} ({alfa:P0}).";
            var zona = new Rect(DegradadoDesde - 960, -540, 1920 - DegradadoDesde, 1080);
            foreach (var n in Personajes)
                if (raiz.Find(n) != null && zona.Overlaps(EnCanvas(raiz.Find(n)))) yield return $"El degradado tapa {n}.";
        }
        foreach (var n in new[] { "TituloGrupo/Subtitulo" })
            if (!FuenteProyecto.EsTitulo(raiz.Find(n)?.GetComponent<TextMeshProUGUI>()?.font)) yield return $"{n}: no usa Press Start 2P.";
        // pixel art nítido: escala entera, Point y en la rejilla del arte
        foreach (var img in raiz.GetComponentsInChildren<Image>(true).Where(i => i.sprite != null && !i.transform.IsChildOf(raiz.Find("PanelMenu"))))
        {
            var k = img.rectTransform.sizeDelta / img.sprite.rect.size;
            if (Mathf.Abs(k.x - Mathf.Round(k.x)) > 0.001f || Mathf.Abs(k.x - k.y) > 0.001f) yield return $"{img.name}: escala no entera ({k.x:0.##}).";
            if (img.sprite.texture.filterMode != FilterMode.Point) yield return $"{img.name}: sin Point (se vería borroso).";
            var p = img.rectTransform.anchoredPosition;
            if (Mathf.Abs(p.x % Escala) > 0.01f || Mathf.Abs(p.y % Escala) > 0.01f) yield return $"{img.name}: fuera de la rejilla de x{Escala} ({p}).";
        }
    }

    static readonly string[] Personajes =
        { "Fondo/Repartidor", "Fondo/Repartidora", "Fondo/CiudadCerca/LetreroNeon", "Fondo/CiudadCerca/HaloNeon" };

    // La columna: placa, campo y botones de 360 de ancho centrados en x = 1500, 64 de alto (80 "Iniciar juego"),
    // separados 18 y en ese orden; nada de la columna (con la tijera) ni los iconos tapa a los personajes o al letrero;
    // los iconos de 48x48 arriba a la derecha, separados 16
    static IEnumerable<string> VerificarColumna(Transform raiz)
    {
        var columna = new (string n, float ancho, float alto)[] { ("EtiquetaNombre", AnchoPlaca, AltoPlaca), ("BotonIniciar", AnchoPrincipal, AltoPrincipal),
                                                                  ("BotonInstrucciones", AnchoBoton, AltoBoton), ("BotonSalir", AnchoBoton, AltoBoton) };
        Rect? anterior = null;
        foreach (var (n, ancho, alto) in columna)
        {
            var t = raiz.Find("PanelMenu/" + n);
            if (t == null) continue;
            var r = EnCanvas(t);
            if (Mathf.Abs(r.center.x - ColumnaX) > 0.01f || Mathf.Abs(r.width - ancho) > 0.01f || Mathf.Abs(r.height - alto) > 0.01f)
                yield return $"{n}: tiene que medir {ancho}x{alto} centrado en x = {ColumnaX + 960} ({r.width}x{r.height} en {r.center.x + 960}).";
            if (r.yMax > 540 || r.yMin < -540) yield return $"{n}: se sale del Canvas.";
            if (anterior.HasValue && Mathf.Abs(anterior.Value.yMin - r.yMax - Separacion) > 0.01f)
                yield return $"{n}: separado {anterior.Value.yMin - r.yMax} del de arriba (tienen que ser {Separacion}).";
            anterior = r;
            var tijera = t.Find("Tijera");
            var zonas = new List<Rect> { r };
            if (tijera != null)
            {
                var rtij = EnCanvasPorEsquinas((RectTransform)tijera);
                zonas.Add(rtij);
                if (rtij.width != BotonBrenn.LadoTijera || Mathf.Abs(r.xMin - rtij.xMax - BotonBrenn.SeparacionTijera) > 0.01f || rtij.xMin < -960)
                    yield return $"{n}: la tijera tiene que medir {BotonBrenn.LadoTijera} a {BotonBrenn.SeparacionTijera} px a la izquierda, dentro del Canvas.";
            }
            foreach (var p in Personajes)
                if (raiz.Find(p) != null && zonas.Any(z => z.Overlaps(EnCanvas(raiz.Find(p))))) yield return $"{n} tapa {p}.";
        }
        var placa = raiz.Find("PanelMenu/EtiquetaNombre/Texto")?.GetComponent<TextMeshProUGUI>();
        if (placa == null || placa.text != "TU NOMBRE" || !FuenteProyecto.EsTitulo(placa.font) || ColorUtility.ToHtmlStringRGB(placa.color) != "FF6FA5"
            || placa.fontSize != TamPlaca)
            yield return $"EtiquetaNombre: \"TU NOMBRE\" en Press Start rosa #FF6FA5 de {TamPlaca}.";
        else if (!FuenteProyecto.Cabe(placa)) yield return "EtiquetaNombre: \"TU NOMBRE\" no cabe.";
        var rPlaca = raiz.Find("PanelMenu/EtiquetaNombre") != null ? EnCanvas(raiz.Find("PanelMenu/EtiquetaNombre")) : default;
        var rCampo = raiz.Find("PanelMenu/CampoNombre") != null ? EnCanvas(raiz.Find("PanelMenu/CampoNombre")) : default;
        if (rCampo.height != AltoCampo || rCampo.xMin < rPlaca.xMin || rCampo.xMax > rPlaca.xMax || rCampo.yMin < rPlaca.yMin || rCampo.yMax > rPlaca.yMax)
            yield return $"CampoNombre: {AltoCampo} de alto, dentro de la placa \"TU NOMBRE\".";
        foreach (var b in new[] { "BotonIniciar", "BotonInstrucciones", "BotonSalir" })
        {
            var tmp = raiz.Find("PanelMenu/" + b + "/Texto")?.GetComponent<TextMeshProUGUI>();
            float esperado = b == "BotonIniciar" ? TamPrincipal : TamTexto;
            if (tmp == null || tmp.fontSize != esperado || !FuenteProyecto.Cabe(tmp)) yield return $"{b}: texto de {esperado} que quepa entero.";
        }
        var sonido = raiz.Find("PanelMenu/MenuSonido");
        if (sonido == null) yield break;
        var rs = EnCanvas(sonido);
        if (Mathf.Abs(rs.xMax - (960 - MargenIconos)) > 0.01f || Mathf.Abs(rs.yMax - (540 - MargenIconos)) > 0.01f)
            yield return "MenuSonido: arriba a la derecha.";
        foreach (Transform b in sonido)
        {
            var rb = EnCanvas(b);
            if (rb.width != LadoIcono || rb.height != LadoIcono) yield return $"MenuSonido/{b.name}: tiene que medir {LadoIcono}x{LadoIcono}.";
            if (b.GetComponent<Image>()?.type != Image.Type.Sliced) yield return $"MenuSonido/{b.name}: el marco va en 9-slice.";
            if (b.Find("Icono")?.GetComponent<Image>()?.sprite == null || b.Find("Raya")?.GetComponent<Image>()?.sprite?.name != "raya_apagado")
                yield return $"MenuSonido/{b.name}: falta el icono o la raya de apagado.";
            if (b.GetComponent<BotonBrenn>()?.VarianteDelBoton != BotonBrenn.Variante.Icono) yield return $"MenuSonido/{b.name}: falta BotonBrenn Icono (marco dorado al elegirlo).";
        }
        if (sonido.childCount == 2 && Mathf.Abs(EnCanvas(sonido.GetChild(1)).xMin - EnCanvas(sonido.GetChild(0)).xMax - SeparacionIconos) > 0.01f)
            yield return $"MenuSonido: los iconos van separados {SeparacionIconos}.";
    }

    // Para hijos con anclas en un borde del padre (la tijera)
    static Rect EnCanvasPorEsquinas(RectTransform rt)
    {
        var padre = EnCanvas(rt.parent);
        var ancla = new Vector2(Mathf.Lerp(padre.xMin, padre.xMax, rt.anchorMin.x), Mathf.Lerp(padre.yMin, padre.yMax, rt.anchorMin.y));
        return new Rect(ancla + rt.anchoredPosition - Vector2.Scale(rt.pivot, rt.sizeDelta), rt.sizeDelta);
    }

    // El rectángulo de un objeto en el Canvas (origen al centro), para objetos anclados al centro de su padre o estirados
    internal static Rect EnCanvas(Transform t)
    {
        var rt = (RectTransform)t;
        if (rt.GetComponent<Canvas>() != null) return new Rect(-960, -540, 1920, 1080);
        var padre = EnCanvas(rt.parent);
        if (rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one) return padre;
        var esquina = padre.center + rt.anchoredPosition - Vector2.Scale(rt.pivot, rt.sizeDelta);
        return new Rect(esquina, rt.sizeDelta);
    }

    // Canvas Scaler 1920x1080 / 0.5 y un EventSystem con el Input System (también para la Intro)
    internal static IEnumerable<string> VerificarBase(GameObject[] raices, Canvas canvas)
    {
        var escalador = canvas.GetComponent<CanvasScaler>();
        if (escalador == null || escalador.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize
            || escalador.referenceResolution != new Vector2(1920, 1080) || !Mathf.Approximately(escalador.matchWidthOrHeight, 0.5f))
            yield return "El Canvas no es Scale With Screen Size 1920x1080 con match 0.5.";
        var eventos = raices.Select(g => g.GetComponent<EventSystem>()).Where(e => e != null).ToList();
        if (eventos.Count != 1 || eventos[0].GetComponent<InputSystemUIInputModule>() == null || eventos[0].GetComponent<StandaloneInputModule>() != null)
            yield return "Tiene que haber un EventSystem con InputSystemUIInputModule.";
        int oyentes = raices.Sum(g => g.GetComponentsInChildren<AudioListener>(true).Length);
        if (oyentes != 1) yield return $"Tiene que haber un AudioListener (en la cámara) y hay {oyentes}: sin él no se oye nada.";
    }

    // Para escenas ya construidas: le pone el AudioListener a la cámara si falta (true si cambió algo)
    internal static bool AsegurarOyente(Scene escena)
    {
        var raices = escena.GetRootGameObjects();
        if (raices.Any(g => g.GetComponentInChildren<AudioListener>(true) != null)) return false;
        var camara = raices.Select(g => g.GetComponentInChildren<Camera>(true)).First(c => c != null);
        Undo.AddComponent<AudioListener>(camara.gameObject);
        EditorSceneManager.MarkSceneDirty(escena);
        return true;
    }

    // Todos los textos con una fuente del proyecto; los de las pastillas (hijos de "Boton...") en Press Start,
    // #2A1F40, sin contorno, a su mínimo (24, que se ve como 36 en Pixelify) y cabiendo en la pastilla
    // (también para la Intro)
    internal static IEnumerable<string> VerificarTextos(Transform raiz, string prefijoBoton, params Color32[] colores)
    {
        if (colores.Length == 0) colores = new[] { Oscuro };
        foreach (var tmp in raiz.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            string quien = $"{tmp.transform.parent.name}/{tmp.name}";
            if (!FuenteProyecto.EsDelProyecto(tmp.font) || tmp.fontSharedMaterial == null) { yield return $"{quien}: no usa la fuente pixel."; continue; }
            if (!tmp.transform.parent.name.StartsWith(prefijoBoton)) continue;
            if (!FuenteProyecto.EsTitulo(tmp.font)) yield return $"{quien}: el botón no usa Press Start 2P.";
            var c = (Color32)tmp.color;
            if (!colores.Any(k => c.r == k.r && c.g == k.g && c.b == k.b && c.a == 255))
                yield return $"{quien}: el texto no es " + string.Join(" ni ", colores.Select(k => "#" + ColorUtility.ToHtmlStringRGB(k))) + ".";
            if (tmp.fontSharedMaterial.IsKeywordEnabled(ShaderUtilities.Keyword_Outline)) yield return $"{quien}: la pastilla no debe llevar contorno.";
            if (tmp.fontSizeMin < FuenteProyecto.MinimoDe(tmp) || !FuenteProyecto.Cabe(tmp))
                yield return $"{quien}: el texto es menor que {FuenteProyecto.MinimoDe(tmp)} o no cabe.";
        }
    }

    // Si aparece Temp/ConstruirMenuPrincipal.pedido se construye y el informe (con lo que salió en la
    // consola) queda en Temp/ConstruirMenuPrincipal.resultado
    [InitializeOnLoad]
    static class PedidoExterno
    {
        const string Pedido = "Temp/ConstruirMenuPrincipal.pedido";
        const string Resultado = "Temp/ConstruirMenuPrincipal.resultado";

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
            var consola = new List<string>();
            void Anotar(string msg, string pila, LogType tipo)
            {
                if (tipo != LogType.Log) consola.Add($"[{tipo}] {msg}" + (tipo == LogType.Exception ? "\n" + pila : ""));
            }
            Application.logMessageReceived += Anotar;
            string texto;
            try
            {
                texto = Construir(out bool ok);
                texto = (ok ? "OK\n" : "CON ERRORES\n") + texto;
                Debug.Log(texto);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                texto = "ERROR\n" + e;
            }
            finally
            {
                Application.logMessageReceived -= Anotar;
            }
            texto += "\n\nCONSOLA (advertencias y errores): " + (consola.Count == 0 ? "ninguno" : "\n" + string.Join("\n", consola));
            File.WriteAllText(Resultado, texto);
        }
    }

    internal static RectTransform CrearHijo(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.layer = padre.gameObject.layer;   // capa UI, igual que el Canvas
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
    }

    // Ancla y pivote al centro, con la posición y el tamaño indicados
    internal static void Centrar(RectTransform rt, float x, float y, float ancho, float alto)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(ancho, alto);
    }

    internal static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
