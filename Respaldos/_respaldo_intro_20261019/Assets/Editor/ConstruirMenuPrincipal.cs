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

// Crea la escena Assets/Scenes/MenuPrincipal.unity desde cero (título, repartidor, campo de nombre,
// cuatro botones y los paneles de Historia e Instrucciones) con el mismo kit que el panel final:
// marco_panel en 9-slice, píldoras rosas y textos #2A1F40 con contorno crema. Después deja el orden de
// escenas del build en MenuPrincipal (0), SampleScene (1).
// Se puede ejecutar varias veces: la escena se rehace entera y se sobrescribe. La escena que esté
// abierta no se toca (el menú se arma en una escena aditiva que se cierra al guardar).
public static class ConstruirMenuPrincipal
{
    const string RutaEscena = "Assets/Scenes/MenuPrincipal.unity";
    const string RutaJuego = "Assets/Scenes/SampleScene.unity";
    const string CarpetaUI = "Assets/Sprits/UI/";
    const string RutaIdleFrente = "Assets/Animations/Idle_Frente.anim";
    const string RutaMaterialPanel = CarpetaUI + "TextoPanelFinal.mat";     // contorno crema (lo comparte con el panel final)
    const string RutaMaterialTitulo = CarpetaUI + "TextoTituloMenu.mat";    // contorno morado oscuro, para el título

    // Igual que en el resto de la interfaz: cada píxel del arte ocupa 4 unidades del Canvas
    const int Escala = 4;

    static readonly Color32 Crema = new Color32(0xFF, 0xF1, 0xD0, 255);
    static readonly Color32 Oscuro = new Color32(0x2A, 0x1F, 0x40, 255);
    static readonly Color32 Morado = new Color32(0x45, 0x33, 0x66, 255);
    static readonly Color32 MoradoMedio = new Color32(0x36, 0x28, 0x52, 255);
    static readonly Color32 Rosa = new Color32(0xFF, 0x6F, 0xA5, 255);
    static readonly Color32 RosaOscuro = new Color32(0xC2, 0x41, 0x7A, 255);
    static readonly Color32 Vino = new Color32(0xB0, 0x3A, 0x74, 255);
    static readonly Color32 Dorado = new Color32(0xFF, 0xD1, 0x66, 255);

    const string TextoHistoria =
        "Eres repartidor de Delivery Brenn's. Entrega los pedidos de las clientas del barrio antes de que se " +
        "desesperen, junta propinas y, con tu dinero, compra una moto para llegar más rápido en los siguientes niveles.";
    const string TextoInstrucciones =
        "<b>Moverte:</b> W A S D o las flechas.\n" +
        "<b>Entregar:</b> tecla E cuando estés cerca de la puerta de la clienta.\n" +
        "<b>Pausa:</b> Esc o el botón de pausa.\n" +
        "La barra de satisfacción baja si tardas o chocas.\n" +
        "Más satisfacción, más estrellas y más propina.";

    static readonly string[] SpritesNecesarios = { "marco_panel", "boton_pill_normal", "boton_pill_presionado" };

    // botón → método de MenuPrincipal, para conectar y para verificar
    static readonly (string boton, string metodo)[] Conexiones =
    {
        ("BotonJugar", "Jugar"), ("BotonHistoria", "AbrirHistoria"), ("BotonInstrucciones", "AbrirInstrucciones"),
        ("BotonSalir", "Salir"), ("PanelHistoria/Marco/BotonCerrar", "CerrarPaneles"),
        ("PanelInstrucciones/Marco/BotonCerrar", "CerrarPaneles"),
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
        // ---- Primero se revisa todo; si falta algo no se crea nada ----
        var sprites = SpritesNecesarios.ToDictionary(n => n, n => AssetDatabase.LoadAssetAtPath<Sprite>(CarpetaUI + n + ".png"));
        foreach (var faltante in sprites.Where(s => s.Value == null))
            Debug.LogError($"Falta el sprite {CarpetaUI}{faltante.Key}.png (o no está importado como Sprite).");
        var repartidor = SpriteIdleFrente();
        if (repartidor == null) Debug.LogError($"No saco el sprite del repartidor de {RutaIdleFrente}.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaJuego) == null) Debug.LogError($"No existe {RutaJuego}.");
        var fuente = Fuente();
        if (fuente == null) Debug.LogError("No encuentro la fuente del HUD ni la fuente por defecto de TextMeshPro.");
        if (sprites.Values.Any(s => s == null) || repartidor == null || fuente == null
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

        var contornoPanel = MaterialContorno(fuente, RutaMaterialPanel, "TextoPanelFinal", Crema, 0.12f);
        var contornoTitulo = MaterialContorno(fuente, RutaMaterialTitulo, "TextoTituloMenu", Oscuro, 0.3f);

        var activa = SceneManager.GetActiveScene();
        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        string informe;
        try
        {
            SceneManager.SetActiveScene(escena);
            Armar(sprites, repartidor, fuente, contornoPanel, contornoTitulo);
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

    static void Armar(Dictionary<string, Sprite> sprites, Sprite repartidor, TMP_FontAsset fuente,
                      Material contornoPanel, Material contornoTitulo)
    {
        // ---- Cámara (solo para que el fondo no quede negro detrás del Canvas) ----
        var camara = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        camara.tag = "MainCamera";
        camara.orthographic = true;
        camara.clearFlags = CameraClearFlags.SolidColor;
        camara.backgroundColor = Oscuro;
        camara.transform.position = new Vector3(0, 0, -10);

        // ---- EventSystem con el Input System ----
        var eventos = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventos.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

        // ---- Canvas 1920x1080 ----
        var goCanvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        goCanvas.layer = LayerMask.NameToLayer("UI");
        var canvas = goCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var escalador = goCanvas.GetComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920, 1080);
        escalador.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        escalador.matchWidthOrHeight = 0.5f;
        var raiz = (RectTransform)goCanvas.transform;

        // Image.pixelsPerUnit = PPU del sprite / referencia del Canvas * multiplicador → borde 9-slice a x4
        float multiplicador = escalador.referencePixelsPerUnit / (sprites["boton_pill_normal"].pixelsPerUnit * Escala);

        var menu = new GameObject("MenuPrincipal").AddComponent<MenuPrincipal>();

        // ---- Fondo con la paleta: cielo morado en franjas, estrellitas doradas y suelo rosa ----
        var fondo = CrearHijo("Fondo", raiz);
        Estirar(fondo);
        Rect(fondo, "Cielo", Morado, 0, 0, 1920, 1080);
        Rect(fondo, "FranjaAlta", MoradoMedio, 0, 540 - 130, 1920, 260);
        Rect(fondo, "FranjaTope", Oscuro, 0, 540 - 50, 1920, 100);
        var estrellitas = new (float x, float y, int lado)[]
        {
            (-860, 420, 12), (-700, 300, 8), (-520, 460, 8), (-330, 340, 12), (330, 330, 8), (520, 450, 12),
            (700, 280, 8), (860, 410, 12), (-880, 160, 8), (880, 140, 8), (-760, 40, 12), (760, 20, 12),
        };
        for (int i = 0; i < estrellitas.Length; i++)
        {
            var (x, y, lado) = estrellitas[i];
            Rect(fondo, "Estrellita" + (i + 1), i % 3 == 0 ? Crema : Dorado, x, y, lado, lado);
        }
        Rect(fondo, "Suelo", RosaOscuro, 0, -540 + 100, 1920, 200);
        Rect(fondo, "SueloBajo", Vino, 0, -540 + 30, 1920, 60);
        Rect(fondo, "LineaDorada", Dorado, 0, -340 - 6, 1920, 12);

        // ---- Título: dorado con contorno oscuro y una sombra rosa desplazada, a lo pixel art ----
        var rtSombra = CrearHijo("TituloSombra", raiz);
        Centrar(rtSombra, 10, 380 - 10, 1500, 180);
        var sombra = CrearTexto(rtSombra, "Delivery Brenn's", 140, RosaOscuro, fuente, contornoTitulo);
        sombra.fontStyle = FontStyles.Bold;
        var rtTitulo = CrearHijo("Titulo", raiz);
        Centrar(rtTitulo, 0, 380, 1500, 180);
        var titulo = CrearTexto(rtTitulo, "Delivery Brenn's", 140, Dorado, fuente, contornoTitulo);
        titulo.fontStyle = FontStyles.Bold;

        // ---- Repartidor idle de frente, de pie sobre el suelo ----
        var rtRepartidor = CrearHijo("Repartidor", raiz);
        rtRepartidor.anchorMin = rtRepartidor.anchorMax = new Vector2(0.5f, 0.5f);
        rtRepartidor.pivot = new Vector2(0.5f, 0);
        rtRepartidor.anchoredPosition = new Vector2(-640, -360);
        rtRepartidor.sizeDelta = repartidor.rect.size * 8;
        var imgRepartidor = rtRepartidor.gameObject.AddComponent<Image>();
        imgRepartidor.sprite = repartidor;
        imgRepartidor.preserveAspect = true;
        imgRepartidor.raycastTarget = false;

        // ---- Panel central: nombre y botones ----
        var panel = CrearHijo("PanelMenu", raiz);
        Centrar(panel, 0, -90, 720, 700);
        Marco(panel, sprites["marco_panel"], multiplicador);

        var rtEtiqueta = CrearHijo("EtiquetaNombre", panel);
        Centrar(rtEtiqueta, 0, 250, 600, 50);
        CrearTexto(rtEtiqueta, "Tu nombre", 36, Oscuro, fuente, contornoPanel);

        var campo = CrearCampoNombre(panel, sprites["marco_panel"], escalador.referencePixelsPerUnit, fuente, contornoPanel);

        var jugar = CrearBoton("BotonJugar", panel, "Jugar", 0, 60, 440, sprites, multiplicador, fuente, contornoPanel);
        var historia = CrearBoton("BotonHistoria", panel, "Historia", 0, -40, 440, sprites, multiplicador, fuente, contornoPanel);
        var instrucciones = CrearBoton("BotonInstrucciones", panel, "Instrucciones", 0, -140, 440, sprites, multiplicador, fuente, contornoPanel);
        var salir = CrearBoton("BotonSalir", panel, "Salir", 0, -240, 440, sprites, multiplicador, fuente, contornoPanel);

        // ---- Paneles de texto (encima de todo, empiezan cerrados) ----
        var panelHistoria = CrearPanelTexto("PanelHistoria", raiz, "Historia", TextoHistoria, TextAlignmentOptions.Center,
                                            sprites, multiplicador, fuente, contornoPanel, out var cerrarHistoria);
        var panelInstrucciones = CrearPanelTexto("PanelInstrucciones", raiz, "Instrucciones", TextoInstrucciones, TextAlignmentOptions.Left,
                                                 sprites, multiplicador, fuente, contornoPanel, out var cerrarInstrucciones);

        // ---- Conexiones ----
        UnityEventTools.AddPersistentListener(jugar.onClick, menu.Jugar);
        UnityEventTools.AddPersistentListener(historia.onClick, menu.AbrirHistoria);
        UnityEventTools.AddPersistentListener(instrucciones.onClick, menu.AbrirInstrucciones);
        UnityEventTools.AddPersistentListener(salir.onClick, menu.Salir);
        UnityEventTools.AddPersistentListener(cerrarHistoria.onClick, menu.CerrarPaneles);
        UnityEventTools.AddPersistentListener(cerrarInstrucciones.onClick, menu.CerrarPaneles);

        var so = new SerializedObject(menu);
        so.FindProperty("campoNombre").objectReferenceValue = campo;
        so.FindProperty("panelHistoria").objectReferenceValue = panelHistoria;
        so.FindProperty("panelInstrucciones").objectReferenceValue = panelInstrucciones;
        so.FindProperty("escenaJuego").stringValue = Path.GetFileNameWithoutExtension(RutaJuego);
        so.ApplyModifiedPropertiesWithoutUndo();

        // se apagan al final: TextMeshPro no inicializa su material en un objeto inactivo
        panelHistoria.SetActive(false);
        panelInstrucciones.SetActive(false);
    }

    // MenuPrincipal (0), SampleScene (1); las demás escenas que hubiera se quedan detrás
    static bool PonerEscenasDelBuild(out string informe)
    {
        var primeras = new[] { RutaEscena, RutaJuego };
        var escenas = primeras.Select(r => new EditorBuildSettingsScene(r, true)).ToList();
        escenas.AddRange(EditorBuildSettings.scenes.Where(e => !primeras.Contains(e.path)));
        EditorBuildSettings.scenes = escenas.ToArray();

        var reales = EditorBuildSettings.scenes;
        bool ok = reales.Length >= 2 && reales[0].path == RutaEscena && reales[1].path == RutaJuego
                  && reales[0].enabled && reales[1].enabled && reales.All(e => !e.guid.Empty());
        informe = "Build: " + string.Join(", ", reales.Select((e, i) => $"{Path.GetFileNameWithoutExtension(e.path)} ({i})")) +
                  (ok ? "" : "  ← ORDEN INCORRECTO");
        return ok;
    }

    // Overlay oscuro a pantalla completa que bloquea clics, con el marco crema, título, texto y "Cerrar"
    static GameObject CrearPanelTexto(string nombre, RectTransform raiz, string titulo, string texto, TextAlignmentOptions alineacion,
                                      Dictionary<string, Sprite> sprites, float multiplicador, TMP_FontAsset fuente,
                                      Material contorno, out Button cerrar)
    {
        var overlay = CrearHijo(nombre, raiz);
        Estirar(overlay);
        var velo = overlay.gameObject.AddComponent<Image>();
        velo.color = new Color(0, 0, 0, 0.6f);

        var marco = CrearHijo("Marco", overlay);
        Centrar(marco, 0, 0, 1100, 620);
        Marco(marco, sprites["marco_panel"], multiplicador);

        var rtTitulo = CrearHijo("Titulo", marco);
        Centrar(rtTitulo, 0, 220, 900, 90);
        CrearTexto(rtTitulo, titulo, 64, Oscuro, fuente, contorno);

        var rtTexto = CrearHijo("Texto", marco);
        Centrar(rtTexto, 0, 10, 960, 300);
        var tmp = CrearTexto(rtTexto, texto, 38, Oscuro, fuente, contorno);
        tmp.alignment = alineacion;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.lineSpacing = 12;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 24;
        tmp.fontSizeMax = 38;

        cerrar = CrearBoton("BotonCerrar", marco, "Cerrar", 0, -210, 320, sprites, multiplicador, fuente, contorno);
        return overlay.gameObject;
    }

    // Campo con el marco a x3 (borde más fino que el del panel) y el texto a la izquierda
    static TMP_InputField CrearCampoNombre(RectTransform padre, Sprite marco, float referenciaPPU, TMP_FontAsset fuente, Material contorno)
    {
        var rt = CrearHijo("CampoNombre", padre);
        Centrar(rt, 0, 170, 560, 96);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = marco;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = referenciaPPU / (marco.pixelsPerUnit * 3);

        var area = CrearHijo("Text Area", rt);
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(36, 18);
        area.offsetMax = new Vector2(-36, -18);
        area.gameObject.AddComponent<RectMask2D>();

        var rtPlaceholder = CrearHijo("Placeholder", area);
        Estirar(rtPlaceholder);
        var placeholder = CrearTexto(rtPlaceholder, "Escribe tu nombre", 36, new Color32(Oscuro.r, Oscuro.g, Oscuro.b, 128), fuente, contorno);
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.alignment = TextAlignmentOptions.Left;

        var rtTexto = CrearHijo("Text", area);
        Estirar(rtTexto);
        var texto = CrearTexto(rtTexto, "", 36, Oscuro, fuente, contorno);
        texto.alignment = TextAlignmentOptions.Left;

        var campo = rt.gameObject.AddComponent<TMP_InputField>();
        campo.targetGraphic = img;
        campo.textViewport = area;
        campo.textComponent = texto;
        campo.placeholder = placeholder;
        campo.fontAsset = fuente;
        campo.pointSize = 36;
        campo.characterLimit = 16;
        campo.lineType = TMP_InputField.LineType.SingleLine;
        campo.customCaretColor = true;
        campo.caretColor = Oscuro;
        campo.selectionColor = new Color32(Rosa.r, Rosa.g, Rosa.b, 120);
        return campo;
    }

    // Botón píldora rosa con su texto morado oscuro; al pulsar cambia al sprite hundido (igual que en el panel final)
    static Button CrearBoton(string nombre, Transform padre, string texto, float x, float y, float ancho,
                             Dictionary<string, Sprite> sprites, float multiplicador, TMP_FontAsset fuente, Material contorno)
    {
        var rt = CrearHijo(nombre, padre);
        Centrar(rt, x, y, ancho, 18 * Escala);

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
        var tmp = CrearTexto(rtTexto, texto, 36, Oscuro, fuente, contorno);
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18;
        tmp.fontSizeMax = 36;
        return boton;
    }

    static TextMeshProUGUI CrearTexto(RectTransform rt, string texto, float tam, Color color, TMP_FontAsset fuente, Material material)
    {
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = fuente;
        tmp.fontSharedMaterial = material;
        tmp.text = texto;
        tmp.fontSize = tam;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    static void Marco(RectTransform rt, Sprite marco, float multiplicador)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = marco;
        img.type = Image.Type.Sliced;
        img.fillCenter = true;
        img.pixelsPerUnitMultiplier = multiplicador;
        img.color = Color.white;
    }

    static void Rect(RectTransform padre, string nombre, Color color, float x, float y, float ancho, float alto)
    {
        var rt = CrearHijo(nombre, padre);
        Centrar(rt, x, y, ancho, alto);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    // El primer fotograma del clip Idle_Frente del jugador
    static Sprite SpriteIdleFrente()
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

    // La fuente de TextoPedidos si SampleScene está abierta; si no, la de TextMeshPro (que es la misma)
    static TMP_FontAsset Fuente()
    {
        var pedidos = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name == "TextoPedidos");
        return pedidos != null && pedidos.font != null ? pedidos.font : TMP_Settings.defaultFontAsset;
    }

    // Material propio con contorno (la fuente con OUTLINE_ON), asignado como fontSharedMaterial
    static Material MaterialContorno(TMP_FontAsset fuente, string ruta, string nombre, Color color, float ancho)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat == null)
        {
            mat = new Material(fuente.material) { name = nombre };
            AssetDatabase.CreateAsset(mat, ruta);
        }
        else
        {
            mat.shader = fuente.material.shader;
            mat.CopyPropertiesFromMaterial(fuente.material);
        }
        mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, ancho);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, color);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    // Revisa lo que quedó en la escena del menú; devuelve el informe y si todo está bien
    static string Verificar(Scene escena, out bool ok)
    {
        var errores = new List<string>();
        var raices = escena.GetRootGameObjects();
        var canvas = raices.Select(g => g.GetComponent<Canvas>()).FirstOrDefault(c => c != null);
        var menu = raices.Select(g => g.GetComponent<MenuPrincipal>()).FirstOrDefault(m => m != null);
        var eventos = raices.Select(g => g.GetComponent<EventSystem>()).FirstOrDefault(e => e != null);
        if (canvas == null || menu == null)
        {
            ok = false;
            return "VERIFICACIÓN: falta el Canvas o el objeto MenuPrincipal.";
        }
        var escalador = canvas.GetComponent<CanvasScaler>();
        if (escalador == null || escalador.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize || escalador.referenceResolution != new Vector2(1920, 1080))
            errores.Add("El Canvas no es Scale With Screen Size 1920x1080.");
        if (eventos == null || eventos.GetComponent<InputSystemUIInputModule>() == null || eventos.GetComponent<StandaloneInputModule>() != null)
            errores.Add("El EventSystem no usa InputSystemUIInputModule.");

        var raiz = canvas.transform;
        var panelMenu = raiz.Find("PanelMenu");
        foreach (var (ruta, metodo) in Conexiones)
        {
            var boton = (ruta.Contains("/") ? raiz.Find(ruta) : panelMenu?.Find(ruta))?.GetComponent<Button>();
            if (boton == null) { errores.Add($"No existe el botón {ruta}."); continue; }
            if (boton.onClick.GetPersistentEventCount() != 1 || boton.onClick.GetPersistentTarget(0) != menu
                || boton.onClick.GetPersistentMethodName(0) != metodo)
                errores.Add($"{ruta} no llama (solo) a MenuPrincipal.{metodo}.");
            var img = boton.GetComponent<Image>();
            if (img == null || img.sprite == null || img.sprite.name != "boton_pill_normal") errores.Add($"{ruta}: no es la píldora rosa.");
        }

        var so = new SerializedObject(menu);
        var campo = so.FindProperty("campoNombre").objectReferenceValue as TMP_InputField;
        if (campo == null || campo.transform.parent != panelMenu) errores.Add("MenuPrincipal: Campo Nombre no es el CampoNombre del menú.");
        else if (!(campo.placeholder is TMP_Text ph) || ph.text != "Escribe tu nombre") errores.Add("CampoNombre: el placeholder no dice \"Escribe tu nombre\".");
        var historia = so.FindProperty("panelHistoria").objectReferenceValue as GameObject;
        var instrucciones = so.FindProperty("panelInstrucciones").objectReferenceValue as GameObject;
        if (historia == null || historia.name != "PanelHistoria") errores.Add("MenuPrincipal: Panel Historia sin asignar.");
        if (instrucciones == null || instrucciones.name != "PanelInstrucciones") errores.Add("MenuPrincipal: Panel Instrucciones sin asignar.");
        if (historia != null && historia.activeSelf) errores.Add("PanelHistoria quedó activo.");
        if (instrucciones != null && instrucciones.activeSelf) errores.Add("PanelInstrucciones quedó activo.");
        var escenaJuego = so.FindProperty("escenaJuego").stringValue;
        if (escenaJuego != Path.GetFileNameWithoutExtension(RutaJuego)) errores.Add($"MenuPrincipal: Escena Juego es \"{escenaJuego}\".");
        if (raiz.Find("Repartidor")?.GetComponent<Image>()?.sprite == null) errores.Add("Falta el repartidor.");
        if (raiz.Find("Titulo")?.GetComponent<TextMeshProUGUI>()?.text != "Delivery Brenn's") errores.Add("Falta el título.");
        foreach (var tmp in raiz.GetComponentsInChildren<TextMeshProUGUI>(true))
            if (tmp.font == null || tmp.fontSharedMaterial == null || !tmp.fontSharedMaterial.IsKeywordEnabled(ShaderUtilities.Keyword_Outline))
                errores.Add($"{tmp.transform.parent.name}/{tmp.name}: sin fuente o sin material con contorno.");

        ok = errores.Count == 0;
        return $"VERIFICACIÓN: MenuPrincipal con {Conexiones.Length} botones conectados, campo de nombre y 2 paneles; " +
               $"EventSystem {(eventos != null ? "con Input System" : "FALTA")}." +
               (ok ? "\nSin errores." : "\nERRORES:\n- " + string.Join("\n- ", errores));
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

    static RectTransform CrearHijo(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.layer = padre.gameObject.layer;   // capa UI, igual que el Canvas
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

    static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
