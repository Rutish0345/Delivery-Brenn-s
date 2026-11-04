using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Arma la pantalla final del nivel dentro del PanelFinal que ya está en el Canvas, con el mismo kit
// que el HUD (marco_panel en 9-slice y píldoras rosas, todo a x4) y la fuente pixel (FuenteProyecto).
// Los textos van en morado oscuro #2A1F40: el título con un contorno suave crema #FFF1D0 y el resto
// sin contorno; los botones a 36 como mínimo. TiendaMotos.fuente también queda con la fuente pixel.
// Se puede ejecutar varias veces: borra los hijos del panel y los vuelve a crear, y después conecta
// el panel, el resumen y las estrellas con el UIManager, los tres botones con el LevelManager (si no
// hay uno en la escena se crea con los valores del nivel 1) y "Tienda de motos" con TiendaMotos.
// Solo trabaja dentro de Canvas/PanelFinal: no toca el HUD ni el PanelPausa (ConstruirPausa), y los
// nombres de sus hijos no se repiten en la pausa. PanelFinal y BotonJugarDeNuevo no se renombran
// porque "Configurar motos" copia de ellos el marco y la píldora.
// Filas, de arriba abajo: título, estrellas, resumen (2 líneas), Jugar de nuevo | Menú, Siguiente nivel
// (apagado hasta que el LevelManager lo enciende) y Tienda de motos.
public static class ConstruirPanelFinal
{
    const string NombreUndo = "Construir panel final";
    const string CarpetaUI = "Assets/Sprits/UI/";

    // Igual que en ConstruirInterfaz: cada píxel del arte ocupa 4 unidades del Canvas
    const int Escala = 4;

    static readonly Color32 Oscuro = new Color32(0x2A, 0x1F, 0x40, 255);
    static readonly Color32 ColorTexto = Oscuro;                               // título, resumen y botones
    static readonly Color32 RosaBoton = new Color32(0xFF, 0x6F, 0xA5, 255);    // el color de boton_pill_normal

    const float AnchoPanel = 1040, AltoPanel = 780;   // ancho para "Jugar de nuevo" en Press Start
    const float MargenMarco = 7 * Escala;         // el crema de marco_panel empieza a 7 px del borde
    const float AltoBoton = 18 * Escala;
    const float YFila1 = -153, YFila2 = -237, YFila3 = -321;   // 12 unidades de aire entre filas
    const float AnchoBoton = 440, XBoton = 250;   // a ±250 no se mete en el borde del marco (50 unidades)
    const float AnchoBotonSolo = 480;             // los que van solos en su fila (Siguiente nivel, Tienda de motos)
    const float LadoEstrella = 15 * 8;            // la estrella de 15 px a x8
    static readonly string[] SpritesNecesarios =
        { "marco_panel", "boton_pill_normal", "boton_pill_presionado", "estrella_vacia", "estrella_llena" };

    [MenuItem("Delivery Brenn's/Construir panel final")]
    static void ConstruirMenu()
    {
        try
        {
            if (Construir()) Debug.Log(Verificar(out _));
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    static bool Construir()
    {
        // ---- Primero se revisa todo; si falta algo no se toca la escena ----
        var sprites = SpritesNecesarios.ToDictionary(n => n, n => AssetDatabase.LoadAssetAtPath<Sprite>(CarpetaUI + n + ".png"));
        foreach (var faltante in sprites.Where(s => s.Value == null))
            Debug.LogError($"Falta el sprite {CarpetaUI}{faltante.Key}.png (o no está importado como Sprite).");
        if (sprites.Values.Any(s => s == null))
        {
            Debug.LogError("No se construyó el panel final porque faltan sprites.");
            return false;
        }

        var canvas = BuscarCanvas();
        if (canvas == null)
        {
            Debug.LogError("No encuentro el Canvas de la escena.");
            return false;
        }

        // PanelFinal puede estar desactivado, por eso se busca incluyendo inactivos
        var enCanvas = canvas.GetComponentsInChildren<RectTransform>(true);
        var panel = canvas.transform.Find("PanelFinal") as RectTransform;
        if (panel == null)
        {
            Debug.LogError("No encuentro PanelFinal como hijo directo del Canvas.");
            return false;
        }

        var uiManager = Object.FindObjectsByType<UIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderByDescending(u => u.name == "UIManager").FirstOrDefault();
        if (uiManager == null)
        {
            Debug.LogError("No encuentro el objeto UIManager (con el script UIManager) en la escena.");
            return false;
        }
        var so = new SerializedObject(uiManager);
        foreach (var campo in new[] { "panelFinal", "textoResumen", "estrellas" })
        {
            if (so.FindProperty(campo) != null) continue;
            Debug.LogError($"El UIManager no tiene el campo \"{campo}\". No se construyó el panel final.");
            return false;
        }

        if (typeof(LevelManager).GetField("botonSiguiente", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) == null)
        {
            Debug.LogError("El LevelManager no tiene el campo \"botonSiguiente\". No se construyó el panel final.");
            return false;
        }

        // la fuente pixel de toda la interfaz
        var fuente = FuenteProyecto.Fuente();

        // Image.pixelsPerUnit = PPU del sprite / referencia del Canvas * multiplicador,
        // así que con este valor el borde 9-slice también sale a x4
        float multiplicador = canvas.referencePixelsPerUnit / (sprites["boton_pill_normal"].pixelsPerUnit * Escala);

        if (fuente == null)
        {
            Debug.LogError("No se pudo crear la fuente pixel (Assets/Fonts/PixelifySans SDF.asset).");
            return false;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(NombreUndo);
        int grupo = Undo.GetCurrentGroup();

        // TextMeshPro no inicializa su material en un objeto inactivo: el panel se enciende mientras se
        // arma. Si algo falla se deshace todo lo de esta ejecución y el panel vuelve a como estaba.
        bool estabaActivo = panel.gameObject.activeSelf;
        Undo.RecordObject(panel.gameObject, NombreUndo);
        panel.gameObject.SetActive(true);
        if (!panel.gameObject.activeInHierarchy)
            Debug.LogWarning("PanelFinal está dentro de un objeto desactivado; se arma igual (el contorno no depende de eso).");
        try
        {
            var levelManager = BuscarOCrearLevelManager(canvas.gameObject.scene);
            Armar(panel, sprites, multiplicador, levelManager, BuscarTienda(), so, new SerializedObject(levelManager));
            panel.gameObject.SetActive(false);                 // el panel solo se enseña al terminar el nivel
            Undo.CollapseUndoOperations(grupo);
        }
        catch
        {
            Undo.RevertAllDownToGroup(grupo);
            if (panel != null) panel.gameObject.SetActive(estabaActivo);
            throw;
        }
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("Panel final listo y conectado al UIManager (Panel Final, Texto Resumen y 3 estrellas); " +
                  "los botones llaman a LevelManager.Reiniciar / IrAlMenu / SiguienteNivel. Guarda la escena para conservar los cambios.");
        return true;
    }

    static void Armar(RectTransform panel, Dictionary<string, Sprite> sprites, float multiplicador,
                      LevelManager nivel, TiendaMotos tienda,
                      SerializedObject so, SerializedObject soNivel)
    {
        // ---- Fuera lo que hubiera dentro del panel, así no se duplica nada ----
        for (int i = panel.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(panel.GetChild(i).gameObject);

        // ---- Fondo: marco_panel en 9-slice, 900x700 al centro ----
        Undo.RecordObject(panel, NombreUndo);
        Centrar(panel, 0, 0, AnchoPanel, AltoPanel);
        foreach (var borde in panel.GetComponents<Outline>())     // el borde amarillo de antes ya no hace falta
            Undo.DestroyObjectImmediate(borde);
        if (!panel.TryGetComponent(out Image fondo)) fondo = Undo.AddComponent<Image>(panel.gameObject);
        Undo.RecordObject(fondo, NombreUndo);
        fondo.sprite = sprites["marco_panel"];
        fondo.type = Image.Type.Sliced;
        fondo.fillCenter = true;
        fondo.pixelsPerUnitMultiplier = multiplicador;
        fondo.color = Color.white;
        fondo.raycastTarget = true;                               // que no se pueda clicar el juego detrás

        // ---- Hijos ----
        var rtTitulo = CrearHijo("Titulo", panel);
        Centrar(rtTitulo, 0, 250, 800, 120);
        CrearTexto(rtTitulo, "¡Nivel completado!", 72, true);

        var estrellas = CrearEstrellas(panel, sprites["estrella_vacia"]);

        var rtResumen = CrearHijo("TextoResumen", panel);
        Centrar(rtResumen, 0, -60, 800, 90);
        // el UIManager escribe dos líneas (la segunda al 70%); si no caben, el texto se encoge
        var resumen = CrearTexto(rtResumen, "Satisfacción 0%  |  $0\n<size=70%>Pedidos: 0%</size>", 40, false);
        resumen.enableAutoSizing = true;
        resumen.fontSizeMin = 28;
        resumen.fontSizeMax = 40;

        // fila 1: Jugar de nuevo | Menú; fila 2: Siguiente nivel (empieza apagado: el LevelManager lo
        // enciende al terminar si existe la escena siguiente); fila 3: Tienda de motos
        var jugar = CrearBoton("BotonJugarDeNuevo", panel, "Jugar de nuevo", -XBoton, YFila1, sprites, multiplicador);
        var menuBoton = CrearBoton("BotonMenu", panel, "Menú", XBoton, YFila1, sprites, multiplicador);
        var siguiente = CrearBoton("BotonSiguiente", panel, "Siguiente nivel", 0, YFila2, sprites, multiplicador);
        Conectar(jugar, nivel.Reiniciar);
        Conectar(menuBoton, nivel.IrAlMenu);
        Conectar(siguiente, nivel.SiguienteNivel);
        siguiente.gameObject.SetActive(false);

        // TiendaMotos reutiliza este botón al jugar en vez de crear el suyo
        if (tienda != null)
        {
            var botonTienda = CrearBoton("BotonTienda", panel, "Tienda de motos", 0, YFila3, sprites, multiplicador);
            Conectar(botonTienda, tienda.Abrir);
            // la tienda arma su ventana al jugar con esta fuente
            var soTienda = new SerializedObject(tienda);
            soTienda.FindProperty("fuente").objectReferenceValue = FuenteProyecto.Fuente();             // descripciones
            soTienda.FindProperty("fuenteTitulos").objectReferenceValue = FuenteProyecto.FuenteTitulo();  // título, nombres y botones
            soTienda.ApplyModifiedProperties();
        }
        else Debug.LogWarning("No hay TiendaMotos en la escena: el panel queda sin \"Tienda de motos\".");

        // ---- Conexión con el LevelManager ----
        soNivel.Update();
        soNivel.FindProperty("botonSiguiente").objectReferenceValue = siguiente.gameObject;
        soNivel.ApplyModifiedProperties();

        // ---- Conexiones con el UIManager ----
        so.Update();
        so.FindProperty("panelFinal").objectReferenceValue = panel.gameObject;
        so.FindProperty("textoResumen").objectReferenceValue = resumen;
        var lista = so.FindProperty("estrellas");
        lista.arraySize = estrellas.Length;
        for (int i = 0; i < estrellas.Length; i++)
            lista.GetArrayElementAtIndex(i).objectReferenceValue = estrellas[i];
        // los sprites de las estrellas solo si están vacíos
        var llena = so.FindProperty("estrellaLlena");
        if (llena != null && llena.objectReferenceValue == null) llena.objectReferenceValue = sprites["estrella_llena"];
        var vacia = so.FindProperty("estrellaVacia");
        if (vacia != null && vacia.objectReferenceValue == null) vacia.objectReferenceValue = sprites["estrella_vacia"];
        so.ApplyModifiedProperties();
    }

    // Los botones se rehacen en cada ejecución, pero por si acaso se quita cualquier listener que
    // tuvieran (como el viejo a MenuJuego.Reiniciar) antes de poner el del LevelManager
    static void Conectar(Button boton, UnityEngine.Events.UnityAction accion)
    {
        for (int i = boton.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(boton.onClick, i);
        UnityEventTools.AddPersistentListener(boton.onClick, accion);
    }

    // Si la escena no tiene LevelManager se crea uno con los valores del nivel 1 (Residencial). Si ya
    // existe un objeto "LevelManager" sin el componente, se le pone; uno que ya existe no se toca
    internal static LevelManager BuscarOCrearLevelManager(Scene escena) => BuscarLevelManager() ?? CrearLevelManager(escena);

    static TiendaMotos BuscarTienda() =>
        Object.FindObjectsByType<TiendaMotos>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();

    static LevelManager CrearLevelManager(Scene escena)
    {
        var go = escena.GetRootGameObjects().FirstOrDefault(g => g.name == "LevelManager");
        if (go == null)
        {
            go = new GameObject("LevelManager");
            Undo.RegisterCreatedObjectUndo(go, NombreUndo);
            SceneManager.MoveGameObjectToScene(go, escena);
        }
        var nivel = Undo.AddComponent<LevelManager>(go);
        var so = new SerializedObject(nivel);
        so.FindProperty("nombreNivel").stringValue = "Residencial";
        so.FindProperty("pedidos").intValue = 3;
        so.FindProperty("velocidadReferencia").floatValue = 3.5f;
        so.FindProperty("motosHabilitadas").boolValue = false;
        so.FindProperty("ambiente").enumValueIndex = (int)Ambiente.Dia;
        so.FindProperty("escenaMenu").stringValue = "MenuPrincipal";
        so.FindProperty("escenaSiguiente").stringValue = "";
        so.ApplyModifiedProperties();
        Debug.Log("No había LevelManager: se creó con Residencial, 3 pedidos, velocidad 3.5, sin motos, de día y menú \"MenuPrincipal\".");
        return nivel;
    }

    internal static LevelManager BuscarLevelManager() =>
        Object.FindObjectsByType<LevelManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderByDescending(l => l.name == "LevelManager").FirstOrDefault();

    // Se prefiere el Canvas donde ya vive TextoPedidos o PanelFinal
    static Canvas BuscarCanvas()
    {
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(c => c.isRootCanvas).ToList();
        return canvases.FirstOrDefault(c => c.GetComponentsInChildren<RectTransform>(true)
                   .Any(r => r.name == "TextoPedidos" || r.name == "PanelFinal"))
               ?? canvases.FirstOrDefault(c => c.name == "Canvas")
               ?? canvases.FirstOrDefault();
    }

    // Contenedor con layout horizontal y las tres estrellas, todas empezando vacías
    static Image[] CrearEstrellas(Transform padre, Sprite vacia)
    {
        var contenedor = CrearHijo("Estrellas", padre);
        Centrar(contenedor, 0, 90, 600, LadoEstrella);

        var layout = contenedor.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 40;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var estrellas = new Image[3];
        for (int i = 0; i < estrellas.Length; i++)
        {
            var rt = CrearHijo("Estrella" + (i + 1), contenedor);
            rt.sizeDelta = new Vector2(LadoEstrella, LadoEstrella);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = vacia;
            img.preserveAspect = true;
            img.raycastTarget = false;
            estrellas[i] = img;
        }
        return estrellas;
    }

    // Botón píldora rosa con su texto morado oscuro; al pulsar cambia al sprite hundido como los demás
    static Button CrearBoton(string nombre, Transform padre, string texto, float x, float y,
                             Dictionary<string, Sprite> sprites, float multiplicador)
    {
        var rt = CrearHijo(nombre, padre);
        Centrar(rt, x, y, x == 0 ? AnchoBotonSolo : AnchoBoton, AltoBoton);

        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprites["boton_pill_normal"];
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = multiplicador;
        img.color = Color.white;   // sin teñir: el rosa #FF6FA5 es el del sprite de la píldora

        var boton = rt.gameObject.AddComponent<Button>();
        boton.targetGraphic = img;
        boton.transition = Selectable.Transition.SpriteSwap;
        var estados = boton.spriteState;
        estados.pressedSprite = sprites["boton_pill_presionado"];
        boton.spriteState = estados;

        // el texto deja libres las tapas y el canto de abajo de la píldora
        var rtTexto = CrearHijo("Texto", rt);
        rtTexto.anchorMin = Vector2.zero;
        rtTexto.anchorMax = Vector2.one;
        rtTexto.pivot = new Vector2(0.5f, 0.5f);
        rtTexto.offsetMin = new Vector2(6 * Escala, 3 * Escala);
        rtTexto.offsetMax = new Vector2(-6 * Escala, -1 * Escala);
        FuenteProyecto.EstiloBoton(CrearTexto(rtTexto, texto, 40, false));   // Press Start #2A1F40, sin contorno, mínimo 26
        return boton;
    }

    // Texto morado oscuro con la fuente pixel, centrado, sin bloquear clics. El título lleva el material con
    // contorno crema (asignado como material compartido, sin tocar outlineWidth)
    static TextMeshProUGUI CrearTexto(RectTransform rt, string texto, float tam, bool titulo)
    {
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        FuenteProyecto.Aplicar(tmp, titulo);
        tmp.text = texto;
        tmp.fontSize = titulo ? FuenteProyecto.Equivalente(tam) : tam;   // los títulos van en Press Start
        tmp.color = ColorTexto;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    // Revisa lo que quedó en la escena; devuelve el informe y si todo está bien
    static string Verificar(out bool ok)
    {
        var errores = new List<string>();
        var canvas = BuscarCanvas();
        var panel = canvas != null ? canvas.transform.Find("PanelFinal") as RectTransform : null;
        var uiManager = Object.FindObjectsByType<UIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderByDescending(u => u.name == "UIManager").FirstOrDefault();
        if (panel == null || uiManager == null)
        {
            ok = false;
            return "VERIFICACIÓN: no encuentro PanelFinal o UIManager.";
        }
        var tienda = BuscarTienda();
        var hijos = new List<string> { "Titulo", "Estrellas", "TextoResumen", "BotonJugarDeNuevo", "BotonMenu", "BotonSiguiente" };
        if (tienda != null) hijos.Add("BotonTienda");
        var botones = hijos.Where(h => h.StartsWith("Boton")).ToArray();
        var reales = Enumerable.Range(0, panel.childCount).Select(i => panel.GetChild(i).name).ToList();
        foreach (var h in hijos.Where(h => !reales.Contains(h))) errores.Add($"PanelFinal no tiene el hijo {h}.");
        foreach (var h in reales.Where(h => !hijos.Contains(h))) errores.Add($"PanelFinal tiene un hijo de más: {h}.");
        var est = panel.Find("Estrellas");
        int nEst = est != null ? est.childCount : 0;
        if (nEst != 3) errores.Add($"Estrellas tiene {nEst} hijos, no 3.");
        foreach (var tmp in panel.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            var mat = tmp.fontSharedMaterial;
            bool esTitulo = tmp.name == "Titulo", enBoton = tmp.transform.parent.name.StartsWith("Boton");
            if (esTitulo || enBoton ? !FuenteProyecto.EsTitulo(tmp.font) : !FuenteProyecto.EsDelProyecto(tmp.font))
                errores.Add($"{tmp.transform.parent.name}/{tmp.name}: no usa {(esTitulo || enBoton ? "Press Start 2P" : "la fuente pixel")}.");
            if (mat == null || mat.IsKeywordEnabled(ShaderUtilities.Keyword_Outline) != esTitulo)
                errores.Add($"{tmp.transform.parent.name}/{tmp.name}: {(esTitulo ? "el título debe llevar el contorno crema" : "no debe llevar contorno")}.");
            else if (esTitulo && (Color32)mat.GetColor(ShaderUtilities.ID_OutlineColor) is var c && (c.r != 0xFF || c.g != 0xF1 || c.b != 0xD0))
                errores.Add($"{tmp.name}: el contorno no es #FFF1D0.");
            if (enBoton && (tmp.fontSizeMin < FuenteProyecto.MinimoDe(tmp) || !FuenteProyecto.Cabe(tmp)))
                errores.Add($"{tmp.transform.parent.name}: el texto es menor que {FuenteProyecto.MinimoDe(tmp)} o no cabe en la pastilla.");
            var t = (Color32)tmp.color;
            if (t.r != ColorTexto.r || t.g != ColorTexto.g || t.b != ColorTexto.b || t.a != 255)
                errores.Add($"{tmp.transform.parent.name}/{tmp.name}: el texto no es #2A1F40.");
            if ((tmp.fontStyle & FontStyles.UpperCase) != 0 || (tmp.text.Any(char.IsLetter) && tmp.text == tmp.text.ToUpperInvariant()))
                errores.Add($"{tmp.transform.parent.name}/{tmp.name}: \"{tmp.text}\" está en mayúsculas.");
        }

        // nada se encima y todo queda dentro de la parte crema del marco
        var cajas = Enumerable.Range(0, panel.childCount).Select(i => (RectTransform)panel.GetChild(i))
            .Select(h => (h.name, caja: new Rect(h.anchoredPosition - h.sizeDelta / 2, h.sizeDelta))).ToList();
        var interior = new Rect(-panel.sizeDelta / 2 + Vector2.one * MargenMarco, panel.sizeDelta - Vector2.one * 2 * MargenMarco);
        for (int i = 0; i < cajas.Count; i++)
        {
            if (!interior.Contains(cajas[i].caja.min) || !interior.Contains(cajas[i].caja.max))
                errores.Add($"{cajas[i].name} se sale del marco.");
            for (int j = i + 1; j < cajas.Count; j++)
                if (cajas[i].caja.Overlaps(cajas[j].caja)) errores.Add($"{cajas[i].name} se encima con {cajas[j].name}.");
        }

        foreach (var nombre in botones)
        {
            var img = panel.Find(nombre)?.GetComponent<Image>();
            if (img == null || img.sprite == null || img.sprite.name != "boton_pill_normal" || img.color != Color.white)
                errores.Add($"{nombre}: el fondo no es la píldora rosa sin teñir.");
            else
            {
                var tex = img.sprite.texture;
                var r = img.sprite.textureRect;
                var centro = tex.isReadable ? (Color32)tex.GetPixel((int)r.center.x, (int)r.center.y) : RosaBoton;
                if (centro.r != RosaBoton.r || centro.g != RosaBoton.g || centro.b != RosaBoton.b)
                    errores.Add($"{nombre}: la píldora no es #FF6FA5 en el centro.");
            }
        }
        // cada botón con un único listener persistente al LevelManager de la escena
        var nivel = BuscarLevelManager();
        if (nivel == null) errores.Add("No hay LevelManager en la escena.");
        var conexiones = new List<(string boton, Object destino, string metodo)>
            { ("BotonJugarDeNuevo", nivel, "Reiniciar"), ("BotonMenu", nivel, "IrAlMenu"), ("BotonSiguiente", nivel, "SiguienteNivel") };
        if (tienda != null) conexiones.Add(("BotonTienda", tienda, "Abrir"));
        foreach (var (nombre, destino, metodo) in conexiones)
        {
            var boton = panel.Find(nombre)?.GetComponent<Button>();
            if (boton == null || destino == null || boton.onClick.GetPersistentEventCount() != 1
                || boton.onClick.GetPersistentMethodName(0) != metodo || boton.onClick.GetPersistentTarget(0) != destino
                || boton.onClick.GetPersistentListenerState(0) == UnityEngine.Events.UnityEventCallState.Off)
                errores.Add($"{nombre} no llama (solo) a {destino?.GetType().Name}.{metodo}.");
        }
        var botonSiguiente = panel.Find("BotonSiguiente");
        if (botonSiguiente != null && botonSiguiente.gameObject.activeSelf) errores.Add("BotonSiguiente quedó activo.");
        if (nivel != null && (botonSiguiente == null
            || new SerializedObject(nivel).FindProperty("botonSiguiente")?.objectReferenceValue != botonSiguiente.gameObject))
            errores.Add("LevelManager: Boton Siguiente no es el BotonSiguiente del panel.");

        var so = new SerializedObject(uiManager);
        if (so.FindProperty("panelFinal").objectReferenceValue != panel.gameObject) errores.Add("UIManager: Panel Final no es PanelFinal.");
        var resumen = so.FindProperty("textoResumen").objectReferenceValue as TextMeshProUGUI;
        if (resumen == null || resumen.transform.parent != panel || resumen.name != "TextoResumen")
            errores.Add("UIManager: Texto Resumen no es el TextoResumen del panel.");
        var lista = so.FindProperty("estrellas");
        if (lista.arraySize != 3) errores.Add($"UIManager: Estrellas tiene {lista.arraySize} elementos, no 3.");
        for (int i = 0; i < lista.arraySize; i++)
        {
            var img = lista.GetArrayElementAtIndex(i).objectReferenceValue as Image;
            if (img == null || img.transform.parent != est) errores.Add($"UIManager: Estrellas[{i}] no es una estrella del panel.");
        }
        if (panel.gameObject.activeSelf) errores.Add("PanelFinal quedó activo.");
        if (tienda != null && new SerializedObject(tienda).FindProperty("fuente").objectReferenceValue != FuenteProyecto.Fuente())
            errores.Add("TiendaMotos: Fuente no es la fuente pixel.");
        if (tienda != null && new SerializedObject(tienda).FindProperty("fuenteTitulos").objectReferenceValue != FuenteProyecto.FuenteTitulo())
            errores.Add("TiendaMotos: Fuente Titulos no es Press Start 2P.");

        ok = errores.Count == 0;
        return $"VERIFICACIÓN: PanelFinal {panel.sizeDelta.x:0}x{panel.sizeDelta.y:0}, hijos [{string.Join(", ", reales)}], " +
               $"Estrellas con {nEst} hijos, activo = {panel.gameObject.activeSelf}; UIManager con " +
               $"{(so.FindProperty("panelFinal").objectReferenceValue != null ? "Panel Final" : "SIN Panel Final")}, " +
               $"{(resumen != null ? "Texto Resumen" : "SIN Texto Resumen")} y {lista.arraySize} estrellas." +
               (ok ? "\nSin errores." : "\nERRORES:\n- " + string.Join("\n- ", errores));
    }

    // Igual que el mapa: si aparece Temp/ConstruirPanelFinal.pedido se construye el panel, se verifica,
    // se guarda la escena si todo salió bien y el informe (con lo que salió en la consola) queda en
    // Temp/ConstruirPanelFinal.resultado
    [InitializeOnLoad]
    static class PedidoExterno
    {
        const string Pedido = "Temp/ConstruirPanelFinal.pedido";
        const string Resultado = "Temp/ConstruirPanelFinal.resultado";

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
                bool construido = Construir();
                string informe = Verificar(out bool ok);
                Debug.Log(informe);
                if (construido && ok)
                {
                    bool guardada = EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                    texto = (guardada ? "OK, escena guardada\n" : "OK PERO NO SE PUDO GUARDAR LA ESCENA\n") + informe;
                }
                else texto = (construido ? "CONSTRUIDO CON ERRORES DE VERIFICACION (escena sin guardar)\n" : "NO SE CONSTRUYO\n") + informe;
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

    static RectTransform CrearHijo(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, NombreUndo);
        go.layer = padre.gameObject.layer;   // capa UI, igual que el panel
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
    }

    // Ancla y pivote al centro, con la posición y el tamaño indicados
    static void Centrar(RectTransform rt, float x, float y, float ancho, float alto)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(ancho, alto);
    }
}
