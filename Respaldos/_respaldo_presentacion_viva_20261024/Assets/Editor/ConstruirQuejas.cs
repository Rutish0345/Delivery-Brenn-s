using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Quejas y despido (GAME OVER) en las 3 escenas de juego:
//  - Canvas/CajaHUD/FilaQuejas, justo debajo de PlacaReloj en la caja del HUD (ConstruirCajaHUD): Queja1..N (queja_vacia / queja_llena de 18 px a x3, Point, de
//    pixel_town/generate_quejas_hud.py), tantos como LevelManager.quejasMaximas (nivel 1 = 2, niveles 2 y 3 = 3),
//    conectados al UIManager.
//  - Canvas/PanelDespido (apagado, con CanvasGroup y PantallaDespido): la pantalla "¡TE DESPIDIERON!" con el estilo
//    de la intro: fondo oscuro, la ilustración despido.png a pantalla completa (pixel_town/intro/generar_final.py),
//    el degradado de la intro, el titular en Press Start con el contorno grueso de la intro, el texto de apoyo en crema
//    y las pastillas rosas "Reintentar" y "Menú" (LevelManager.Reiniciar / IrAlMenu). Todos sus hijos empiezan con
//    "Despido", así ningún nombre se repite en el Canvas.
// Se puede correr las veces que sea (borra y rehace la fila y el panel). ConstruirMapa la llama al armar el HUD de
// cualquier nivel; el menú y el pedido la aplican a las 3 escenas sin reconstruirlas, y de paso rehacen su PanelFinal
// (ConstruirPanelFinal: aviso de NIVEL NO SUPERADO, "Continuar" en el nivel 3), así PanelDespido queda encima de todo.
// Temp/ConstruirQuejas.pedido → Temp/ConstruirQuejas.resultado
public static class ConstruirQuejas
{
    const string CarpetaUI = "Assets/Sprits/UI/";
    const string CarpetaIntro = "Assets/Sprits/Intro/";
    public const string NombreFila = "FilaQuejas";
    public const string NombrePanel = "PanelDespido";
    public const string BotonReintentar = "DespidoBotonReintentar";
    public const string BotonMenu = "DespidoBotonMenu";
    public const string TitularDespido = "¡TE DESPIDIERON!";
    public const string TextoDespido = "Demasiadas quejas de las clientas. El Salón Brenn's todavía te necesita, ¡inténtalo otra vez!";
    const string NombreUndo = "Quejas y despido";

    const int EscalaIcono = 3;
    public const float LadoIcono = 18 * EscalaIcono;
    const float SeparacionIconos = 10;
    const int EscalaPastilla = 4;
    static readonly Color32 FondoOscuro = new Color32(0x14, 0x0E, 0x22, 255);

    // igual que la intro: de abajo hacia arriba, botones, texto de apoyo y titular (en el tercio oscuro)
    const float YBotones = 40, AltoBoton = 18 * EscalaPastilla, AnchoBoton = 440, XBoton = 250;
    const float YTexto = 140, AltoTexto = 96, AnchoTexto = 1600;
    const float YTitular = 248, AltoTitular = 92, AnchoTitular = 1760;

    // nivel 1 (SampleScene) aguanta 2 quejas; los otros, 3
    public static int MaximoPara(string escena) => escena == "SampleScene" ? 2 : 3;

    static Canvas CanvasHUD() => ConstruirCajaHUD.CanvasHUD();

    static UIManager BuscarUIManager() =>
        Object.FindObjectsByType<UIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderByDescending(u => u.name == "UIManager").FirstOrDefault();

    // Crea la fila y el panel en la escena activa. Devuelve un renglón para el resumen.
    public static string Crear()
    {
        var canvas = CanvasHUD() ?? throw new Exception("No encuentro el Canvas con PlacaPedidos (ejecuta Construir interfaz).");
        var vacia = ConstruirMenuPrincipal.ImportarPixel(CarpetaUI + "queja_vacia.png", Vector4.zero);
        var llena = ConstruirMenuPrincipal.ImportarPixel(CarpetaUI + "queja_llena.png", Vector4.zero);
        if (vacia == null || llena == null) throw new Exception("Faltan queja_vacia/queja_llena (corre pixel_town/generate_quejas_hud.py).");
        var ilustracion = ConstruirMenuPrincipal.ImportarPixel(CarpetaIntro + "despido.png", Vector4.zero);
        var degradado = ConstruirMenuPrincipal.ImportarPixel(CarpetaIntro + "degradado_intro.png", Vector4.zero);
        if (ilustracion == null || degradado == null) throw new Exception("Faltan despido.png o degradado_intro.png (corre pixel_town/intro/generar_final.py).");
        var pildora = AssetDatabase.LoadAssetAtPath<Sprite>(CarpetaUI + "boton_pill_normal.png");
        var pildoraPresionada = AssetDatabase.LoadAssetAtPath<Sprite>(CarpetaUI + "boton_pill_presionado.png");
        if (pildora == null || pildoraPresionada == null) throw new Exception("Faltan las pastillas rosas en " + CarpetaUI);
        var ui = BuscarUIManager() ?? throw new Exception("No hay UIManager en la escena.");
        var nivel = ConstruirPanelFinal.BuscarOCrearLevelManager(SceneManager.GetActiveScene());

        string escena = SceneManager.GetActiveScene().name;
        int maximo = MaximoPara(escena);
        var soNivel = new SerializedObject(nivel);
        soNivel.FindProperty("quejasMaximas").intValue = maximo;
        soNivel.ApplyModifiedPropertiesWithoutUndo();

        var iconos = CrearFila(canvas, maximo, vacia);
        var so = new SerializedObject(ui);
        var lista = so.FindProperty("iconosQueja");
        lista.arraySize = iconos.Length;
        for (int i = 0; i < iconos.Length; i++) lista.GetArrayElementAtIndex(i).objectReferenceValue = iconos[i];
        so.FindProperty("quejaVacia").objectReferenceValue = vacia;
        so.FindProperty("quejaLlena").objectReferenceValue = llena;
        so.ApplyModifiedPropertiesWithoutUndo();

        var pantalla = CrearPanel(canvas, nivel, ilustracion, degradado, pildora, pildoraPresionada);
        soNivel.Update();
        soNivel.FindProperty("pantallaDespido").objectReferenceValue = pantalla;
        soNivel.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return $"{escena}: {maximo} quejas en {ConstruirCajaHUD.Nombre} (bajo el reloj) y PanelDespido.";
    }

    // ------------------------------------------------------------------ fila de quejas

    static Image[] CrearFila(Canvas canvas, int cantidad, Sprite vacia)
    {
        var vieja = ConstruirCajaHUD.Buscar(canvas.transform, NombreFila);
        if (vieja != null) Undo.DestroyObjectImmediate(vieja.gameObject);

        var fila = Hijo(NombreFila, canvas.transform);
        fila.anchorMin = fila.anchorMax = fila.pivot = new Vector2(0, 1);
        fila.sizeDelta = new Vector2(cantidad * LadoIcono + (cantidad - 1) * SeparacionIconos, LadoIcono);

        var iconos = new Image[cantidad];
        for (int i = 0; i < cantidad; i++)
        {
            var rt = Hijo("Queja" + (i + 1), fila);
            // pivote al centro, para que el rebote crezca desde el medio; posiciones enteras (píxeles nítidos)
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(i * (LadoIcono + SeparacionIconos) + LadoIcono / 2, -LadoIcono / 2);
            rt.sizeDelta = new Vector2(LadoIcono, LadoIcono);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = vacia;
            img.raycastTarget = false;
            iconos[i] = img;
        }

        // a la caja del HUD, bajo el reloj (la pausa, el panel final y el despido la tapan)
        ConstruirCajaHUD.Acomodar(canvas);
        return iconos;
    }

    // ------------------------------------------------------------------ pantalla de despido

    static PantallaDespido CrearPanel(Canvas canvas, LevelManager nivel, Sprite ilustracion, Sprite degradado, Sprite pildora, Sprite pildoraPresionada)
    {
        var viejo = canvas.transform.Find(NombrePanel);
        if (viejo != null) Undo.DestroyObjectImmediate(viejo.gameObject);

        var panel = Hijo(NombrePanel, canvas.transform);
        Estirar(panel);
        var grupo = panel.gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;
        grupo.interactable = false;
        grupo.blocksRaycasts = false;

        var fondo = Hijo("DespidoFondo", panel);
        Estirar(fondo);
        var imgFondo = fondo.gameObject.AddComponent<Image>();
        imgFondo.color = (Color)FondoOscuro;
        imgFondo.raycastTarget = true;   // tapa el nivel: no se puede clicar nada detrás

        var rtIlustracion = Hijo("DespidoIlustracion", panel);
        Estirar(rtIlustracion);
        var imgIlustracion = rtIlustracion.gameObject.AddComponent<Image>();
        imgIlustracion.sprite = ilustracion;
        imgIlustracion.raycastTarget = false;

        var rtVelo = Hijo("DespidoDegradado", panel);
        Estirar(rtVelo);
        var velo = rtVelo.gameObject.AddComponent<Image>();
        velo.sprite = degradado;
        velo.raycastTarget = false;

        var materialIntro = FuenteProyecto.MaterialIntro();
        var titular = DesdeAbajo("DespidoTitular", panel, YTitular, AnchoTitular, AltoTitular).gameObject.AddComponent<TextMeshProUGUI>();
        FuenteProyecto.AplicarTitulo(titular, materialIntro);   // contorno y sombra #2A1F40 gruesos, como la intro
        titular.text = TitularDespido;
        titular.color = Color.white;
        titular.enableAutoSizing = true;
        titular.fontSizeMin = 40;
        titular.fontSizeMax = titular.fontSize = 64;
        titular.alignment = TextAlignmentOptions.Center;
        titular.textWrappingMode = TextWrappingModes.NoWrap;
        titular.raycastTarget = false;

        var texto = DesdeAbajo("DespidoTexto", panel, YTexto, AnchoTexto, AltoTexto).gameObject.AddComponent<TextMeshProUGUI>();
        FuenteProyecto.AplicarTitulo(texto, materialIntro);
        texto.text = TextoDespido;
        texto.color = (Color)FuenteProyecto.Crema;
        texto.enableAutoSizing = true;
        texto.fontSizeMin = 22;
        texto.fontSizeMax = texto.fontSize = 28;
        texto.lineSpacing = 24;
        texto.alignment = TextAlignmentOptions.Top;
        texto.textWrappingMode = TextWrappingModes.Normal;
        texto.raycastTarget = false;

        float multiplicador = canvas.referencePixelsPerUnit / (pildora.pixelsPerUnit * EscalaPastilla);
        var reintentar = Pastilla(BotonReintentar, panel, "Reintentar", -XBoton, pildora, pildoraPresionada, multiplicador);
        var menu = Pastilla(BotonMenu, panel, "Menú", XBoton, pildora, pildoraPresionada, multiplicador);
        UnityEventTools.AddPersistentListener(reintentar.onClick, nivel.Reiniciar);
        UnityEventTools.AddPersistentListener(menu.onClick, nivel.IrAlMenu);

        var pantalla = panel.gameObject.AddComponent<PantallaDespido>();
        var so = new SerializedObject(pantalla);
        so.FindProperty("grupo").objectReferenceValue = grupo;
        so.FindProperty("titular").objectReferenceValue = titular.transform;
        so.FindProperty("botonReintentar").objectReferenceValue = reintentar;
        so.ApplyModifiedPropertiesWithoutUndo();

        panel.SetAsLastSibling();          // encima de todo (PantallaDespido lo vuelve a subir al mostrarse)
        panel.gameObject.SetActive(false); // lo enciende el LevelManager al despedir
        return pantalla;
    }

    static Button Pastilla(string nombre, Transform padre, string texto, float x, Sprite normal, Sprite presionada, float multiplicador)
    {
        var rt = DesdeAbajo(nombre, padre, YBotones, AnchoBoton, AltoBoton);
        rt.anchoredPosition = new Vector2(x, YBotones);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = normal;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = multiplicador;
        var boton = rt.gameObject.AddComponent<Button>();
        boton.targetGraphic = img;
        boton.transition = Selectable.Transition.SpriteSwap;
        var estados = boton.spriteState;
        estados.pressedSprite = presionada;
        boton.spriteState = estados;

        var rtTexto = Hijo(nombre + "Texto", rt);
        rtTexto.anchorMin = Vector2.zero;
        rtTexto.anchorMax = Vector2.one;
        rtTexto.pivot = new Vector2(0.5f, 0.5f);
        rtTexto.offsetMin = new Vector2(6 * EscalaPastilla, 3 * EscalaPastilla);
        rtTexto.offsetMax = new Vector2(-6 * EscalaPastilla, -1 * EscalaPastilla);
        var tmp = rtTexto.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;
        FuenteProyecto.EstiloBoton(tmp);   // Press Start #2A1F40, como las del panel final
        return boton;
    }

    // Un rectángulo centrado en x, con el borde de abajo a 'y' del borde de abajo de la pantalla
    static RectTransform DesdeAbajo(string nombre, Transform padre, float y, float ancho, float alto)
    {
        var rt = Hijo(nombre, padre);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = new Vector2(ancho, alto);
        return rt;
    }

    static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static RectTransform Hijo(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, NombreUndo);
        go.layer = padre.gameObject.layer;
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
    }

    // ------------------------------------------------------------------ verificación (escena abierta)

    public static List<string> Verificar()
    {
        var errores = new List<string>();
        var canvas = CanvasHUD();
        if (canvas == null) return new List<string> { "no hay Canvas con PlacaPedidos" };
        string escena = SceneManager.GetActiveScene().name;
        int maximo = MaximoPara(escena);
        var nivel = ConstruirPanelFinal.BuscarLevelManager();
        var ui = BuscarUIManager();
        if (nivel == null || ui == null) return new List<string> { "falta LevelManager o UIManager" };
        if (nivel.QuejasMaximas != maximo) errores.Add($"LevelManager: quejasMaximas = {nivel.QuejasMaximas}, no {maximo}.");

        // fila: bajo el reloj, N iconos vacíos de 54, sin encimarse y conectados al UIManager
        var fila = canvas.transform.Find(ConstruirCajaHUD.Nombre + "/" + NombreFila) as RectTransform;
        var reloj = fila != null ? fila.parent.Find("PlacaReloj") : null;
        if (fila == null) errores.Add($"Falta Canvas/{ConstruirCajaHUD.Nombre}/{NombreFila}.");
        else
        {
            if (reloj != null && fila.GetSiblingIndex() != reloj.GetSiblingIndex() + 1) errores.Add($"{NombreFila} no va justo después de PlacaReloj.");
            if (fila.childCount != maximo) errores.Add($"{NombreFila} tiene {fila.childCount} iconos, no {maximo}.");
            var so = new SerializedObject(ui);
            var lista = so.FindProperty("iconosQueja");
            if (lista.arraySize != fila.childCount) errores.Add($"UIManager: Iconos Queja tiene {lista.arraySize}, no {fila.childCount}.");
            for (int i = 0; i < fila.childCount; i++)
            {
                var hijo = (RectTransform)fila.GetChild(i);
                var img = hijo.GetComponent<Image>();
                if (hijo.name != "Queja" + (i + 1)) errores.Add($"{NombreFila}: el hijo {i} se llama {hijo.name}.");
                if (img == null || img.sprite == null || img.sprite.name != "queja_vacia") errores.Add($"{hijo.name}: no empieza con queja_vacia.");
                if (hijo.sizeDelta != Vector2.one * LadoIcono) errores.Add($"{hijo.name}: mide {hijo.sizeDelta}, no {LadoIcono} (x{EscalaIcono}).");
                if (i < lista.arraySize && lista.GetArrayElementAtIndex(i).objectReferenceValue != img) errores.Add($"UIManager: Iconos Queja[{i}] no es {hijo.name}.");
            }
            if ((so.FindProperty("quejaVacia").objectReferenceValue as Sprite)?.name != "queja_vacia"
                || (so.FindProperty("quejaLlena").objectReferenceValue as Sprite)?.name != "queja_llena")
                errores.Add("UIManager: faltan los sprites queja_vacia / queja_llena.");
            foreach (var n in new[] { "queja_vacia", "queja_llena" })
                if (AssetImporter.GetAtPath(CarpetaUI + n + ".png") is TextureImporter t && (t.filterMode != FilterMode.Point || t.textureCompression != TextureImporterCompression.Uncompressed))
                    errores.Add($"{n}: no está en Point sin compresión (se vería borroso).");
            var placa = reloj as RectTransform;
            if (placa != null && ConstruirCajaHUD.Rectangulo(fila).Overlaps(ConstruirCajaHUD.Rectangulo(placa))) errores.Add($"{NombreFila} se encima con PlacaReloj.");
        }

        // panel de despido
        var panel = canvas.transform.Find(NombrePanel);
        if (panel == null) { errores.Add($"Falta Canvas/{NombrePanel}."); return errores; }
        if (panel.gameObject.activeSelf) errores.Add($"{NombrePanel} quedó encendido.");
        if (panel.GetSiblingIndex() != canvas.transform.childCount - 1) errores.Add($"{NombrePanel} no es el último hijo del Canvas (lo taparía algo).");
        var pantalla = panel.GetComponent<PantallaDespido>();
        if (pantalla == null || panel.GetComponent<CanvasGroup>() == null) errores.Add($"{NombrePanel} sin PantallaDespido o sin CanvasGroup.");
        if (new SerializedObject(nivel).FindProperty("pantallaDespido").objectReferenceValue != pantalla) errores.Add("LevelManager: Pantalla Despido no es la del panel.");
        if (pantalla != null)
        {
            var sp = new SerializedObject(pantalla);
            if (sp.FindProperty("grupo").objectReferenceValue != panel.GetComponent<CanvasGroup>()) errores.Add("PantallaDespido: Grupo no es el CanvasGroup del panel.");
            if ((sp.FindProperty("botonReintentar").objectReferenceValue as Button)?.transform.parent != panel) errores.Add("PantallaDespido: Boton Reintentar no es el del panel.");
        }
        string[] hijos = { "DespidoFondo", "DespidoIlustracion", "DespidoDegradado", "DespidoTitular", "DespidoTexto", BotonReintentar, BotonMenu };
        var reales = Enumerable.Range(0, panel.childCount).Select(i => panel.GetChild(i).name).ToList();
        if (!reales.SequenceEqual(hijos)) errores.Add($"{NombrePanel}: hijos [{string.Join(", ", reales)}], se esperaba [{string.Join(", ", hijos)}].");
        // nombres únicos: nada del panel se llama igual que otra cosa del Canvas
        var deFuera = canvas.GetComponentsInChildren<Transform>(true).Where(t => !t.IsChildOf(panel)).Select(t => t.name).ToHashSet();
        foreach (var t in panel.GetComponentsInChildren<Transform>(true).Where(t => deFuera.Contains(t.name)))
            errores.Add($"{NombrePanel}/{t.name}: el nombre se repite fuera del panel.");
        if (panel.Find("DespidoIlustracion")?.GetComponent<Image>()?.sprite?.name != "despido") errores.Add("DespidoIlustracion: no es despido.png.");
        if (panel.Find("DespidoDegradado")?.GetComponent<Image>()?.sprite?.name != "degradado_intro") errores.Add("DespidoDegradado: no es el degradado de la intro.");
        var tit = panel.Find("DespidoTitular")?.GetComponent<TextMeshProUGUI>();
        if (tit == null || tit.text != TitularDespido || !FuenteProyecto.EsTitulo(tit.font) || tit.fontSharedMaterial == null
            || !tit.fontSharedMaterial.IsKeywordEnabled(ShaderUtilities.Keyword_Outline))
            errores.Add($"DespidoTitular: debe decir {TitularDespido} en Press Start con contorno grueso.");
        else
        {
            tit.enableAutoSizing = false;
            tit.fontSize = tit.fontSizeMin;
            if (tit.GetPreferredValues(tit.text).x > AnchoTitular + 0.5f) errores.Add("DespidoTitular: no cabe en una línea.");
            tit.enableAutoSizing = true;
            tit.fontSize = tit.fontSizeMax;
        }
        var tx = panel.Find("DespidoTexto")?.GetComponent<TextMeshProUGUI>();
        if (tx == null || tx.text != TextoDespido) errores.Add("DespidoTexto: no es el texto pedido.");
        else
        {
            tx.enableAutoSizing = false;
            tx.fontSize = tx.fontSizeMin;
            if (tx.GetPreferredValues(tx.text, AnchoTexto, 0).y > AltoTexto + 0.5f) errores.Add("DespidoTexto: no cabe en su caja.");
            tx.enableAutoSizing = true;
            tx.fontSize = tx.fontSizeMax;
        }
        foreach (var (nombre, metodo) in new[] { (BotonReintentar, "Reiniciar"), (BotonMenu, "IrAlMenu") })
        {
            var b = panel.Find(nombre)?.GetComponent<Button>();
            if (b == null || b.onClick.GetPersistentEventCount() != 1 || b.onClick.GetPersistentTarget(0) != nivel || b.onClick.GetPersistentMethodName(0) != metodo)
                errores.Add($"{nombre} no llama (solo) a LevelManager.{metodo}.");
            if ((b?.targetGraphic as Image)?.sprite?.name != "boton_pill_normal") errores.Add($"{nombre}: no es la pastilla rosa.");
            var t = b != null ? b.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            if (t == null || !FuenteProyecto.EsTitulo(t.font) || !FuenteProyecto.Cabe(t)) errores.Add($"{nombre}: el texto no es Press Start o no cabe.");
        }
        return errores;
    }

    // ------------------------------------------------------------------ menú y pedido (las 3 escenas)

    [MenuItem("Delivery Brenn's/Quejas, despido y panel final (3 niveles)")]
    static void Menu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        try { EditorUtility.DisplayDialog("Quejas y despido", Aplicar(out _), "OK"); }
        catch (Exception e) { Debug.LogException(e); EditorUtility.DisplayDialog("Quejas y despido", e.Message, "OK"); }
    }

    // Abre la escena, deja escenaSiguiente (si se pasa), rehace su PanelFinal y las quejas con el despido, verifica y
    // guarda (solo si todo salió bien). Devuelve el renglón del resumen; los que empiezan con ERROR son fallas.
    public static string AplicarEnEscena(string ruta, string siguiente = null)
    {
        var escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
        if (siguiente != null)
        {
            var so = new SerializedObject(ConstruirPanelFinal.BuscarOCrearLevelManager(escena));
            so.FindProperty("escenaSiguiente").stringValue = siguiente;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        bool panel = ConstruirPanelFinal.Construir();
        string informePanel = ConstruirPanelFinal.Verificar(out bool panelOk);
        string linea = Crear();
        var errores = Verificar();
        if (!panel || !panelOk) errores.Add("PanelFinal: " + informePanel);
        if (errores.Count > 0) return $"ERROR en {escena.name} (no se guardó):\n- " + string.Join("\n- ", errores);
        if (!EditorSceneManager.SaveScene(escena)) return $"ERROR: no pude guardar {ruta}.";
        string sig = new SerializedObject(ConstruirPanelFinal.BuscarLevelManager()).FindProperty("escenaSiguiente").stringValue;
        return linea + $" PanelFinal rehecho (siguiente \"{sig}\").";
    }

    static string Aplicar(out bool ok)
    {
        string volverA = SceneManager.GetActiveScene().path;
        var lineas = new List<string>();
        try
        {
            foreach (var ruta in RevisarCanvas.EscenasDeJuego) lineas.Add(AplicarEnEscena(ruta));
        }
        finally
        {
            if (!string.IsNullOrEmpty(volverA) && File.Exists(volverA) && SceneManager.GetActiveScene().path != volverA)
                EditorSceneManager.OpenScene(volverA, OpenSceneMode.Single);
        }
        var faltan = RevisarCanvas.RevisarEscenasDeJuego();
        ok = faltan.Count == 0 && !lineas.Any(l => l.StartsWith("ERROR"));
        return string.Join("\n", lineas) + (faltan.Count == 0 ? "\nVERIFICACIÓN: Canvas completo (con quejas y despido) en las 3 escenas." : "\nFALTAN:\n- " + string.Join("\n- ", faltan));
    }

    [InitializeOnLoad]
    static class PedidoExterno
    {
        const string Pedido = "Temp/ConstruirQuejas.pedido";
        const string Resultado = "Temp/ConstruirQuejas.resultado";

        static PedidoExterno() => EditorApplication.update += Revisar;

        static void Revisar()
        {
            if (!File.Exists(Pedido) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
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
                if (SceneManager.GetActiveScene().isDirty) throw new Exception("La escena abierta tiene cambios sin guardar; no la cambio.");
                texto = Aplicar(out bool ok);
                texto = (ok ? "OK\n" : "CON ERRORES\n") + texto;
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
}
