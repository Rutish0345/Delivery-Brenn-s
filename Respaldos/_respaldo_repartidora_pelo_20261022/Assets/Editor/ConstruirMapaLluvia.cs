using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Nivel 3 (Lluvia, de noche, con motos): arma Assets/Scenes/Nivel3_Lluvia.unity con el constructor del nivel 1
// (ConstruirMapa con el perfil Lluvia: el mapa Residencial con la variante "lluvia" de generate_lluvia_grande.py).
// Se puede correr las veces que haga falta:
//   - si la escena no existe, la copia de Nivel2_Comercial (cámara, Grid, repartidor, GameManager, Canvas con el HUD,
//     PanelFinal, PanelPausa, GuiaPedido, UIManager, TiendaMotos, MenuJuego, EventSystem y LevelManager);
//   - reconstruye el mapa (suelo mojado, Props, AutosCalle, PuntosEntrega, Peatones, Clientes, Baches con charco…);
//   - la noche: Light2D cálida en cada farol, luz tenue en las ventanas, faros en cono en los autos y una linterna
//     suave en el repartidor (todo en soloDeNoche del LevelManager); marcadores, flecha y entrega sin luz (Unlit)
//     para que se lean de noche;
//   - charcos decorativos (sin collider) y la lluvia: capa en pantalla (Canvas propio bajo el HUD) y salpicaduras
//     en el suelo (20 como máximo), en un objeto "Lluvia" que enciende y apaga el LevelManager;
//   - LevelManager del nivel 3 con el cierre de la historia, SeleccionPedidos con los pares injustos del generador
//     y el panel final; guarda, deja el build MenuPrincipal, Intro, SampleScene, Nivel2_Comercial, Nivel3_Lluvia, y
//     en Nivel2_Comercial escenaSiguiente = Nivel3_Lluvia (con su panel final otra vez, igual que en SampleScene).
// No toca SampleScene (más que volver a correr su panel final) ni el mapa del nivel 2. No toca una escena con
// cambios sin guardar. Con "solo_mapa" dentro del pedido arma solo Nivel3_Lluvia: no toca el build, ni el nivel 2
// ni SampleScene.
public static class ConstruirMapaLluvia
{
    const string Escena = "Assets/Scenes/Nivel3_Lluvia.unity";
    const string EscenaBase = "Assets/Scenes/Nivel2_Comercial.unity";
    const string EscenaNivel1 = "Assets/Scenes/SampleScene.unity";
    const string NombreEscena = "Nivel3_Lluvia";
    static readonly string[] OrdenBuild =
        { "Assets/Scenes/MenuPrincipal.unity", "Assets/Scenes/Intro.unity", EscenaNivel1, EscenaBase, Escena };

    static readonly string[] RaicesConocidas =
    {
        "AutosCalle", "EventSystem", "MenuJuego", "repartidor", "BordeMapa", "Main Camera", "Global Light 2D", "Duena",
        "Canvas", "FlechaPedido", "BloqueoMoto", "LotesSolidos", "GameManager", "Semaforos", "Props", "PuntosEntrega",
        "EntregaProducto", "TiendaMotos", "Peatones", "Grid", "LevelManager", "UIManager", "Clientes", "Baches",
        NombreFaroles, NombreVentanas, NombreCharcos, NombreLluvia,
    };

    const string NombreFaroles = "LucesFaroles", NombreVentanas = "LucesVentanas", NombreCharcos = "Charcos",
                 NombreLluvia = "Lluvia", NombreLinterna = "Linterna", NombreFaros = "Faros";
    const string RutaUnlit = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    const string CarpetaLluvia = "Assets/Sprits/Efectos/Lluvia";

    static readonly Color Noche = new Color32(0x6F, 0x6A, 0xB8, 0xFF);
    const float IntensidadNoche = 0.55f;
    static readonly Color Farol = new Color32(0xFF, 0xD1, 0x66, 0xFF);
    static readonly Color Ventana = new Color32(0xFF, 0xD9, 0x9A, 0xFF);
    static readonly Color Faro = new Color32(0xFF, 0xF4, 0xC8, 0xFF);
    static readonly Color Linterna = new Color32(0xFF, 0xF1, 0xD0, 0xFF);

    // Luces: (radio exterior, interior, intensidad, caída)
    const float RadioFarol = 4f, AlturaLuzFarol = 1.2f;     // el charco de luz al pie del farol (la cabeza queda dentro)
    const float RadioVentana = 1.15f;
    const float RadioFaro = 3.2f, AnguloFaroInterior = 40f, AnguloFaroExterior = 75f;
    const float RadioLinterna = 3.2f;
    const int MaxSalpicaduras = 20;
    const int OrdenCharco = -7;         // bajo el bache (-6) y el aro (-5), sobre el suelo (-10)
    const int OrdenSalpicadura = -4;

    [MenuItem("Delivery Brenn's/Construir nivel 3 (Lluvia)")]
    static void ConstruirMenu()
    {
        try
        {
            string texto = Construir(out bool ok);
            EditorUtility.DisplayDialog("Construir nivel 3 (Lluvia)",
                (ok ? "Verificación sin errores.\n\n" : "LA VERIFICACIÓN ENCONTRÓ ERRORES (mira la consola).\n\n") + Recortar(texto), "OK");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Construir nivel 3 (Lluvia)", e.Message, "OK");
        }
    }

    static string Recortar(string t) => t.Length > 3000 ? t.Substring(0, 3000) + "…" : t;

    static string Construir(out bool ok) => Construir(out ok, true);

    static string Construir(out bool ok, bool conectar)
    {
        var activa = SceneManager.GetActiveScene();
        if (activa.isDirty && activa.path != Escena)
            throw new Exception($"La escena {activa.name} tiene cambios sin guardar; guárdala o descártalos antes de construir el nivel 3.");

        if (!File.Exists(Escena))
        {
            if (!AssetDatabase.CopyAsset(EscenaBase, Escena)) throw new Exception($"No pude copiar {EscenaBase} a {Escena}.");
            Debug.Log($"{Escena} creada como copia de {EscenaBase}.");
        }
        var escena = EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
        var lineas = new List<string>();
        int sobrantes = 0;
        foreach (var raiz in escena.GetRootGameObjects().Where(g => !RaicesConocidas.Contains(g.name)).ToList())
        {
            Object.DestroyImmediate(raiz);
            sobrantes++;
        }

        PrepararTexturasLluvia();
        lineas.Add(ConstruirMapa.ConstruirConPerfil(ConstruirMapa.Perfil.Lluvia, out bool mapaOk));
        if (sobrantes > 0) lineas.Add($"Quitados {sobrantes} objetos sueltos de la raíz.");

        var mapa = ConstruirMapa.LeerJsonDe(ConstruirMapa.Perfil.Lluvia, ConstruirMapa.Perfil.Lluvia.archivoMapa);
        var sprites = ConstruirMapa.SpritesDe(ConstruirMapa.Perfil.Lluvia.rutaAtlas);
        var origen = escena.GetRootGameObjects().First(g => g.name == "Props").transform.position;
        var deNoche = new List<GameObject>();
        lineas.Add(CrearLuces(mapa, origen, deNoche));
        lineas.Add(MarcadoresSinLuz());
        lineas.Add(CrearCharcos(mapa, sprites, origen));
        var lluvia = CrearLluvia(sprites);
        lineas.Add(ConfigurarNivel(lluvia, deNoche));
        lineas.Add(ConfigurarSeleccion(mapa, origen));

        bool panel = ConstruirPanelFinal.Construir();
        string informePanel = ConstruirPanelFinal.Verificar(out bool panelOk);
        lineas.Add("Panel final del nivel 3: " + informePanel);

        var errores = new List<string>();
        lineas.Add(Verificar(mapa, errores));
        foreach (var e in errores) Debug.LogError("Nivel 3: " + e);

        EditorSceneManager.MarkSceneDirty(escena);
        if (!EditorSceneManager.SaveScene(escena)) throw new Exception($"No pude guardar {Escena}.");

        bool n2Ok = true, n1Ok = true;
        if (conectar)
        {
            lineas.Add(ActualizarBuild());
            lineas.Add(ConectarEscena(EscenaBase, NombreEscena, out n2Ok));
            lineas.Add(ConectarEscena(EscenaNivel1, null, out n1Ok));
            EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
        }
        else lineas.Add("Solo el mapa: sin tocar el build, el nivel 2 ni SampleScene.");

        ok = mapaOk && panel && panelOk && errores.Count == 0 && n2Ok && n1Ok;
        string texto = string.Join("\n", lineas);
        if (ok) Debug.Log("Nivel 3 listo.\n" + texto);
        else Debug.LogError("Nivel 3 con errores.\n" + texto);
        return texto;
    }

    // ------------------------------------------------------------------ luces de noche

    static Light2D Luz(string nombre, Transform padre, Vector3 pos, Color color, float radio, float interior, float intensidad, float caida)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.position = pos;
        var l = go.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        l.color = color;
        l.intensity = intensidad;
        l.pointLightOuterRadius = radio;
        l.pointLightInnerRadius = interior;
        l.pointLightInnerAngle = 360f;
        l.pointLightOuterAngle = 360f;
        l.falloffIntensity = caida;
        l.shadowsEnabled = false;
        l.targetSortingLayers = SortingLayer.layers.Select(s => s.id).ToArray();
        return l;
    }

    static GameObject NuevaRaiz(string nombre, Vector3 origen)
    {
        foreach (var vieja in SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name == nombre).ToList())
            Object.DestroyImmediate(vieja);
        var raiz = new GameObject(nombre);
        raiz.transform.position = origen;
        return raiz;
    }

    static string CrearLuces(Dictionary<string, object> mapa, Vector3 origen, List<GameObject> deNoche)
    {
        var raices = SceneManager.GetActiveScene().GetRootGameObjects();
        var props = raices.First(g => g.name == "Props");

        // faroles de las calles y los cruces: los props con el sprite "farol"
        var faroles = NuevaRaiz(NombreFaroles, origen);
        int nFaroles = 0;
        foreach (var sr in props.GetComponentsInChildren<SpriteRenderer>(true).Where(s => s.sprite != null && s.sprite.name == "farol"))
        {
            Luz($"Luz_{sr.name}_{++nFaroles}", faroles.transform, sr.transform.position + Vector3.up * AlturaLuzFarol,
                Farol, RadioFarol, 0.6f, 1.0f, 0.55f);
        }
        deNoche.Add(faroles);

        // ventanas de las casas (del JSON) y las dos vitrinas de Brenn's
        var ventanas = NuevaRaiz(NombreVentanas, origen);
        int nVentanas = 0;
        foreach (var v in ConstruirMapa.Lista(mapa, "ventanas").Cast<Dictionary<string, object>>())
            Luz($"Ventana_{++nVentanas}", ventanas.transform,
                origen + new Vector3((float)ConstruirMapa.Num(v["x"]), (float)ConstruirMapa.Num(v["y"])),
                Ventana, RadioVentana, 0.15f, 0.6f, 0.7f);
        var empresa = props.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(s => s.sprite != null && s.sprite.name == "empresa_brenns");
        if (empresa != null)
            foreach (float dx in new[] { -2.6f, 2.6f })
                Luz($"Ventana_{++nVentanas}", ventanas.transform, empresa.transform.position + new Vector3(dx, 1.4f),
                    Ventana, 1.6f, 0.2f, 0.7f, 0.7f);
        deNoche.Add(ventanas);

        // faros de los autos: cono corto hacia adelante
        int nFaros = 0;
        foreach (var auto in Object.FindObjectsByType<TrafficCar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var viejo = auto.transform.Find(NombreFaros);
            if (viejo != null) Object.DestroyImmediate(viejo.gameObject);
            var l = Luz(NombreFaros, auto.transform, auto.transform.position, Faro, RadioFaro, 0.4f, 0.9f, 0.6f);
            l.pointLightInnerAngle = AnguloFaroInterior;
            l.pointLightOuterAngle = AnguloFaroExterior;
            var faros = l.gameObject.AddComponent<FarosAuto>();
            var so = new SerializedObject(faros);
            so.FindProperty("auto").objectReferenceValue = auto;
            so.ApplyModifiedPropertiesWithoutUndo();
            deNoche.Add(l.gameObject);
            nFaros++;
        }

        // linterna suave del repartidor (sin borde duro: caída alta y poca intensidad)
        var jugador = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (jugador == null) throw new Exception("No hay repartidor en la escena del nivel 3.");
        var vieja = jugador.transform.Find(NombreLinterna);
        if (vieja != null) Object.DestroyImmediate(vieja.gameObject);
        var linterna = Luz(NombreLinterna, jugador.transform, jugador.transform.position + Vector3.up * 0.4f, Linterna,
                           RadioLinterna, 0.3f, 0.55f, 0.95f);
        deNoche.Add(linterna.gameObject);

        return $"Noche: {nFaroles} faroles con Light2D #{ColorUtility.ToHtmlStringRGB(Farol)} (radio {RadioFarol}), " +
               $"{nVentanas} ventanas (radio {RadioVentana}), faros en {nFaros} autos (cono {AnguloFaroExterior}°, {RadioFaro} tiles) " +
               $"y la linterna del repartidor (radio {RadioLinterna}, caída suave).";
    }

    // De noche la luz global (0.55) oscurece todo lo Lit: el marcador, la flecha y la entrega van Unlit
    static string MarcadoresSinLuz()
    {
        var unlit = AssetDatabase.LoadAssetAtPath<Material>(RutaUnlit) ?? throw new Exception($"No encuentro {RutaUnlit}.");
        int n = 0;
        foreach (var nombre in new[] { "PuntosEntrega", "FlechaPedido", "EntregaProducto" })
        {
            var raiz = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == nombre);
            if (raiz == null) throw new Exception($"No hay {nombre} en la escena del nivel 3.");
            foreach (var sr in raiz.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sharedMaterial = unlit;
                n++;
            }
        }
        return $"Marcadores, flecha y entrega sin luz (Sprite-Unlit-Default): {n} sprites; el HUD es un Canvas en pantalla (no le afecta la luz).";
    }

    // ------------------------------------------------------------------ charcos y lluvia

    static string CrearCharcos(Dictionary<string, object> mapa, Dictionary<string, Sprite> sprites, Vector3 origen)
    {
        var raiz = NuevaRaiz(NombreCharcos, origen);
        var suelo = Object.FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "Suelo");
        int n = 0;
        foreach (var c in ConstruirMapa.Lista(mapa, "charcos").Cast<Dictionary<string, object>>())
        {
            string nombre = (string)c["sprite"];
            if (!sprites.TryGetValue(nombre, out var sp)) throw new Exception($"Falta el sprite {nombre} en props_lluvia.");
            var go = new GameObject((string)c["nombre"]);
            go.transform.SetParent(raiz.transform, false);
            go.transform.localPosition = new Vector3((float)ConstruirMapa.Num(c["x"]), (float)ConstruirMapa.Num(c["y"]));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.sortingLayerID = suelo.sortingLayerID;
            sr.sortingOrder = OrdenCharco;
            n++;
        }
        return $"Charcos decorativos: {n} (sin collider ni efecto; claros, lisos y semitransparentes; los baches, oscuros y con borde roto).";
    }

    static void PrepararTexturasLluvia()
    {
        for (int k = 1; k <= 4; k++)
        {
            string ruta = $"{CarpetaLluvia}/lluvia_{k}.png";
            AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
            var imp = AssetImporter.GetAtPath(ruta) as TextureImporter ?? throw new Exception($"Falta {ruta} (corre generate_lluvia_grande.py).");
            imp.textureType = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.filterMode = FilterMode.Point;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
    }

    static GameObject CrearLluvia(Dictionary<string, Sprite> sprites)
    {
        var raiz = NuevaRaiz(NombreLluvia, Vector3.zero);
        var hud = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(c => c.isRootCanvas && c.transform.Find("PanelFinal") != null);

        // capa en pantalla: un Canvas propio, debajo del HUD
        var goCanvas = new GameObject("CapaLluvia", typeof(RectTransform));
        goCanvas.transform.SetParent(raiz.transform, false);
        var canvas = goCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = hud.sortingOrder - 1;
        var escalador = goCanvas.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920, 1080);
        escalador.matchWidthOrHeight = 1f;
        var cuadros = Enumerable.Range(1, 4).Select(k => AssetDatabase.LoadAssetAtPath<Texture2D>($"{CarpetaLluvia}/lluvia_{k}.png")).ToArray();
        if (cuadros.Any(c => c == null)) throw new Exception($"Faltan las texturas de lluvia en {CarpetaLluvia}.");
        var capas = new List<(RawImage img, float escala, Vector2 vel, float alfa)>();
        foreach (var (nombre, escala, vel, alfa) in new[]
                 { ("Lejos", 2.25f, new Vector2(-95f, -270f), 0.22f), ("Cerca", 3.375f, new Vector2(-150f, -430f), 0.38f) })
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.transform.SetParent(goCanvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<RawImage>();
            img.texture = cuadros[0];
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, alfa);
            capas.Add((img, escala, vel, alfa));
        }
        var pantalla = goCanvas.AddComponent<LluviaPantalla>();
        var so = new SerializedObject(pantalla);
        var arr = so.FindProperty("cuadros");
        arr.arraySize = cuadros.Length;
        for (int i = 0; i < cuadros.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = cuadros[i];
        var pc = so.FindProperty("capas");
        pc.arraySize = capas.Count;
        for (int i = 0; i < capas.Count; i++)
        {
            var e = pc.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("imagen").objectReferenceValue = capas[i].img;
            e.FindPropertyRelative("escala").floatValue = capas[i].escala;
            e.FindPropertyRelative("velocidad").vector2Value = capas[i].vel;
            e.FindPropertyRelative("opacidad").floatValue = capas[i].alfa;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        // salpicaduras: reserva fija de MaxSalpicaduras, apagadas
        var suelo = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "Suelo");
        var capa = suelo.GetComponent<TilemapRenderer>().sortingLayerID;
        var marcos = Enumerable.Range(1, 3).Select(k => sprites.TryGetValue($"salpicadura_{k}", out var s) ? s
                         : throw new Exception($"Falta salpicadura_{k} en props_lluvia.")).ToArray();
        var goSalp = new GameObject("Salpicaduras");
        goSalp.transform.SetParent(raiz.transform, false);
        var reserva = new SpriteRenderer[MaxSalpicaduras];
        for (int i = 0; i < reserva.Length; i++)
        {
            var g = new GameObject($"Salpicadura_{i + 1}");
            g.transform.SetParent(goSalp.transform, false);
            reserva[i] = g.AddComponent<SpriteRenderer>();
            reserva[i].sprite = marcos[0];
            reserva[i].sortingLayerID = capa;
            reserva[i].sortingOrder = OrdenSalpicadura;
            g.SetActive(false);
        }
        var salp = goSalp.AddComponent<SalpicadurasLluvia>();
        var ss = new SerializedObject(salp);
        var sc = ss.FindProperty("cuadros");
        sc.arraySize = marcos.Length;
        for (int i = 0; i < marcos.Length; i++) sc.GetArrayElementAtIndex(i).objectReferenceValue = marcos[i];
        var sr = ss.FindProperty("reserva");
        sr.arraySize = reserva.Length;
        for (int i = 0; i < reserva.Length; i++) sr.GetArrayElementAtIndex(i).objectReferenceValue = reserva[i];
        ss.FindProperty("suelo").objectReferenceValue = suelo;
        ss.ApplyModifiedPropertiesWithoutUndo();

        raiz.SetActive(false);   // la enciende el LevelManager (NocheConLluvia y lluviaActiva)
        return raiz;
    }

    // ------------------------------------------------------------------ nivel, pedidos, build

    static string ConfigurarNivel(GameObject lluvia, List<GameObject> deNoche)
    {
        var nivel = ConstruirPanelFinal.BuscarOCrearLevelManager(SceneManager.GetActiveScene());
        var so = new SerializedObject(nivel);
        so.FindProperty("nombreNivel").stringValue = "Lluvia";
        so.FindProperty("pedidos").intValue = 8;
        so.FindProperty("velocidadReferencia").floatValue = 5.5f;
        so.FindProperty("motosHabilitadas").boolValue = true;
        so.FindProperty("elegirMotoAntes").boolValue = true;
        so.FindProperty("ambiente").enumValueIndex = (int)Ambiente.NocheConLluvia;
        so.FindProperty("lluvia").objectReferenceValue = lluvia;
        so.FindProperty("lluviaActiva").boolValue = true;
        var arr = so.FindProperty("soloDeNoche");
        arr.arraySize = deNoche.Count;
        for (int i = 0; i < deNoche.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = deNoche[i];
        so.FindProperty("escenaSiguiente").stringValue = "";
        so.FindProperty("escenaMenu").stringValue = "MenuPrincipal";
        so.FindProperty("cierreHistoria").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        var luz = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(l => l.lightType == Light2D.LightType.Global) ?? throw new Exception("No hay Global Light 2D en el nivel 3.");
        luz.color = Noche;
        luz.intensity = IntensidadNoche;
        EditorUtility.SetDirty(luz);
        return $"LevelManager: Lluvia, 8 pedidos, velocidad de referencia 5.5, motos sí (elegir antes), NocheConLluvia " +
               $"#{ColorUtility.ToHtmlStringRGB(luz.color)} a {luz.intensity}, lluvia = {lluvia.name}, {deNoche.Count} objetos en soloDeNoche, " +
               "sin siguiente nivel (cierre de la historia), menú MenuPrincipal.";
    }

    static string ConfigurarSeleccion(Dictionary<string, object> mapa, Vector3 origen)
    {
        var d = (Dictionary<string, object>)mapa["seleccion"];
        var puntos = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "PuntosEntrega")
                     ?? throw new Exception("No hay PuntosEntrega en el nivel 3.");
        var sel = puntos.GetComponent<SeleccionPedidos>() ?? puntos.AddComponent<SeleccionPedidos>();
        var pares = ConstruirMapa.Lista(d, "pares_injustos").Cast<List<object>>().Select(p => $"{p[0]}>{p[1]}").ToArray();
        var so = new SerializedObject(sel);
        so.FindProperty("cantidad").intValue = (int)ConstruirMapa.Num(d["cantidad"]);
        so.FindProperty("distanciaMinima").floatValue = (float)ConstruirMapa.Num(d["distancia_minima"]);
        so.FindProperty("yAvenida").floatValue = (float)ConstruirMapa.Num(d["y_avenida"]) + origen.y;
        var arr = so.FindProperty("paresInjustos");
        arr.arraySize = pares.Length;
        for (int i = 0; i < pares.Length; i++) arr.GetArrayElementAtIndex(i).stringValue = pares[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return $"SeleccionPedidos: {so.FindProperty("cantidad").intValue} pedidos, {so.FindProperty("distanciaMinima").floatValue} tiles o más " +
               $"entre uno y otro, norte y sur de y = {d["y_avenida"]}, y {pares.Length} pares que no se encadenan (ni la moto más lenta " +
               "llega en el tiempo ideal con velocidad 5.5).";
    }

    static string ActualizarBuild()
    {
        foreach (var ruta in OrdenBuild)
            if (!File.Exists(ruta)) throw new Exception($"Falta {ruta} para el build.");
        return OrdenEscenas.Poner(out _);   // MenuPrincipal, Seleccion, Intro, niveles 1-3; cualquier otra queda después
    }

    // Abre la escena, deja escenaSiguiente (si se pasa) y vuelve a correr su panel final
    static string ConectarEscena(string ruta, string siguiente, out bool ok)
    {
        var escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
        var nivel = ConstruirPanelFinal.BuscarLevelManager() ?? throw new Exception($"{escena.name} no tiene LevelManager.");
        var so = new SerializedObject(nivel);
        string antes = so.FindProperty("escenaSiguiente").stringValue;
        if (siguiente != null)
        {
            so.FindProperty("escenaSiguiente").stringValue = siguiente;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        bool panel = ConstruirPanelFinal.Construir();
        string informe = ConstruirPanelFinal.Verificar(out bool panelOk);
        EditorSceneManager.MarkSceneDirty(escena);
        bool guardada = EditorSceneManager.SaveScene(escena);
        ok = panel && panelOk && guardada;
        return $"{escena.name}: escenaSiguiente \"{antes}\" -> \"{so.FindProperty("escenaSiguiente").stringValue}\"; panel final: {informe}" +
               (guardada ? "" : " (NO SE GUARDÓ)");
    }

    // ------------------------------------------------------------------ verificación

    static string Verificar(Dictionary<string, object> mapa, List<string> errores)
    {
        var raices = SceneManager.GetActiveScene().GetRootGameObjects();
        var nivel = ConstruirPanelFinal.BuscarLevelManager();
        var so = new SerializedObject(nivel);
        var deNoche = Enumerable.Range(0, so.FindProperty("soloDeNoche").arraySize)
            .Select(i => so.FindProperty("soloDeNoche").GetArrayElementAtIndex(i).objectReferenceValue as GameObject).ToList();
        if (deNoche.Any(g => g == null)) errores.Add("soloDeNoche tiene huecos.");
        var luces = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(l => l.lightType != Light2D.LightType.Global).ToList();
        var fuera = luces.Where(l => !deNoche.Any(g => g != null && l.transform.IsChildOf(g.transform))).ToList();
        if (fuera.Count > 0) errores.Add($"{fuera.Count} Light2D fuera de soloDeNoche (p. ej. {fuera[0].name}).");
        if (luces.Any(l => l.shadowsEnabled)) errores.Add("Hay Light2D con sombras (pesan).");
        var faroles = raices.First(g => g.name == NombreFaroles).GetComponentsInChildren<Light2D>(true);
        if (faroles.Any(l => (Color32)l.color is var c && (c.r != 0xFF || c.g != 0xD1 || c.b != 0x66))) errores.Add("Un farol no es #FFD166.");
        var autos = Object.FindObjectsByType<TrafficCar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int sinFaros = autos.Count(a => a.GetComponentInChildren<FarosAuto>(true) == null);
        if (sinFaros > 0) errores.Add($"{sinFaros} autos sin faros.");

        // baches y charcos: los charcos no tienen collider ni Bache y no pisan un bache
        var baches = Object.FindObjectsByType<Bache>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (baches.Length != 10) errores.Add($"{baches.Length} baches, no 10.");
        if (baches.Any(b => !b.GetComponent<SpriteRenderer>().sprite.name.StartsWith("bache_charco_"))) errores.Add("Un bache no usa la versión de charco.");
        var charcos = raices.First(g => g.name == NombreCharcos).GetComponentsInChildren<SpriteRenderer>(true);
        if (charcos.Any(c => c.GetComponent<Collider2D>() != null || c.GetComponent<Bache>() != null)) errores.Add("Un charco decorativo tiene collider o Bache.");
        float cerca = charcos.Length == 0 ? 99f : charcos.Min(c => baches.Min(b => Vector2.Distance(c.transform.position, b.transform.position)));
        if (cerca < 3f) errores.Add($"Un charco decorativo queda a {cerca:0.0} tiles de un bache (se confunden).");

        // lluvia: apagada al guardar, con su LevelManager, 20 salpicaduras como máximo
        var lluvia = raices.First(g => g.name == NombreLluvia);
        if (so.FindProperty("lluvia").objectReferenceValue != lluvia) errores.Add("LevelManager: Lluvia no es el objeto Lluvia.");
        int reserva = lluvia.GetComponentsInChildren<SpriteRenderer>(true).Length;
        if (reserva > MaxSalpicaduras) errores.Add($"{reserva} salpicaduras (máximo {MaxSalpicaduras}).");
        var pantalla = lluvia.GetComponentInChildren<LluviaPantalla>(true);
        var hud = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(c => c.isRootCanvas && c.transform.Find("PanelFinal") != null);
        if (pantalla == null || pantalla.GetComponent<Canvas>().sortingOrder >= hud.sortingOrder) errores.Add("La capa de lluvia no queda debajo del HUD.");
        foreach (var t in Enumerable.Range(1, 4).Select(k => AssetImporter.GetAtPath($"{CarpetaLluvia}/lluvia_{k}.png") as TextureImporter))
            if (t == null || t.filterMode != FilterMode.Point || t.wrapMode != TextureWrapMode.Repeat || t.textureCompression != TextureImporterCompression.Uncompressed)
                errores.Add($"{t?.assetPath}: debe ser Point, Repeat y sin compresión.");

        // marcadores que se leen de noche
        var unlit = AssetDatabase.LoadAssetAtPath<Material>(RutaUnlit);
        int lit = raices.Where(g => g.name == "PuntosEntrega" || g.name == "FlechaPedido" || g.name == "EntregaProducto")
            .SelectMany(g => g.GetComponentsInChildren<SpriteRenderer>(true)).Count(s => s.sharedMaterial != unlit);
        if (lit > 0) errores.Add($"{lit} sprites de marcador/flecha/entrega siguen con luz.");

        var sel = raices.First(g => g.name == "PuntosEntrega").GetComponent<SeleccionPedidos>();
        if (sel == null) errores.Add("PuntosEntrega sin SeleccionPedidos.");
        if (!so.FindProperty("cierreHistoria").boolValue) errores.Add("LevelManager: falta el cierre de la historia.");

        return $"VERIFICACIÓN nivel 3: {luces.Count} Light2D ({faroles.Length} faroles, {autos.Length} autos con faros, ventanas y linterna), " +
               $"todas en soloDeNoche y sin sombras; {baches.Length} baches con charco; {charcos.Length} charcos decorativos a " +
               $"{cerca:0.0} tiles o más de un bache; lluvia con {reserva} salpicaduras y la capa bajo el HUD (orden " +
               $"{pantalla?.GetComponent<Canvas>().sortingOrder} < {hud.sortingOrder}); marcadores Unlit." +
               (errores.Count == 0 ? " Sin errores." : $" {errores.Count} ERRORES.");
    }

    // Si aparece Temp/ConstruirMapaLluvia.pedido: construye todo y deja Temp/ConstruirMapaLluvia.resultado
    [InitializeOnLoad]
    static class PedidoExterno
    {
        const string Pedido = "Temp/ConstruirMapaLluvia.pedido";
        const string Resultado = "Temp/ConstruirMapaLluvia.resultado";

        static PedidoExterno() => EditorApplication.update += Revisar;

        static void Revisar()
        {
            if (!File.Exists(Pedido) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;   // nunca interrumpe Play
            bool conectar = File.ReadAllText(Pedido).Trim() != "solo_mapa";
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
                texto = Construir(out bool ok, conectar);
                texto = (ok ? "OK\n" : "OK CON ERRORES DE VERIFICACION\n") + texto;
                string carpeta = ConstruirMapa.Perfil.Lluvia.carpetaDatos;
                texto += "\nCapturas: " + string.Join(", ",
                    ConstruirMapa.CapturarArea(new Rect(0, 0, 128, 64), 32, Path.Combine(carpeta, "captura_unity.png")),
                    ConstruirMapa.CapturarArea(new Rect(26, 12, 22, 12), 32, Path.Combine(carpeta, "captura_cruce.png")));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                texto = "ERROR\n" + e;
            }
            finally
            {
                Application.logMessageReceived -= Anotar;
                EditorUtility.ClearProgressBar();
            }
            texto += "\n\nCONSOLA (advertencias y errores): " + (consola.Count == 0 ? "ninguno" : "\n" + string.Join("\n", consola));
            File.WriteAllText(Resultado, texto);
        }
    }
}
