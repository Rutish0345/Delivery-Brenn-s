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

// Crea la escena Assets/Scenes/MenuPrincipal.unity desde cero, inspirada en "referencia menu 1" pero con
// la paleta del juego: fondo rosa pastel con nubes y arbustos de flores a los lados (menu_fondo.png, de
// pixel_town/intro/generar_intro.py), título grande blanco con sombra rosa y morada, el repartidor idle de
// frente, el campo "Tu nombre" y pastillas pastel tipo piedra: "Iniciar juego" (dorada, la principal),
// "Instrucciones" y "Salir". La historia ya no es un panel: es la escena Intro.
// Todo con la fuente pixel (FuenteProyecto); el texto de las pastillas en #2A1F40, sin contorno, a 36
// como mínimo. Después deja el orden de escenas del build: MenuPrincipal (0), Intro (1), SampleScene (2).
// Se puede ejecutar varias veces: la escena se rehace entera y se sobrescribe. La escena que esté
// abierta no se toca (el menú se arma en una escena aditiva que se cierra al guardar).
public static class ConstruirMenuPrincipal
{
    const string RutaEscena = "Assets/Scenes/MenuPrincipal.unity";
    internal const string RutaIntro = "Assets/Scenes/Intro.unity";
    const string RutaJuego = "Assets/Scenes/SampleScene.unity";
    const string CarpetaUI = "Assets/Sprits/UI/";
    const string RutaFondo = "Assets/Sprits/Intro/menu_fondo.png";
    const string RutaIdleFrente = "Assets/Animations/Idle_Frente.anim";

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

    // los 9-slice de las pastillas pastel (24x18): tapas de 6 px, brillo arriba y canto abajo
    static readonly Vector4 BordePastel = new Vector4(6, 6, 6, 4);   // izquierda, abajo, derecha, arriba
    static readonly string[] SpritesNecesarios = { "marco_panel" };

    // botón → método de MenuPrincipal, para conectar y para verificar
    static readonly (string boton, string metodo)[] Conexiones =
    {
        ("BotonIniciar", "Jugar"), ("BotonInstrucciones", "AbrirInstrucciones"), ("BotonSalir", "Salir"),
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
        sprites["fondo"] = ImportarPixel(RutaFondo, Vector4.zero);
        foreach (var n in new[] { "boton_pastel_normal", "boton_pastel_presionado", "boton_pastel_dorado_normal", "boton_pastel_dorado_presionado" })
            sprites[n] = ImportarPixel(CarpetaUI + n + ".png", BordePastel);
        foreach (var faltante in sprites.Where(s => s.Value == null))
            Debug.LogError($"Falta el sprite \"{faltante.Key}\" (corre pixel_town/intro/generar_intro.py).");
        var repartidor = SpriteIdleFrente();
        if (repartidor == null) Debug.LogError($"No saco el sprite del repartidor de {RutaIdleFrente}.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaJuego) == null) Debug.LogError($"No existe {RutaJuego}.");
        var fuente = FuenteProyecto.Fuente();
        if (fuente == null) Debug.LogError("No se pudo crear la fuente pixel.");
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

        var activa = SceneManager.GetActiveScene();
        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        string informe;
        try
        {
            SceneManager.SetActiveScene(escena);
            Armar(sprites, repartidor);
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

    static void Armar(Dictionary<string, Sprite> sprites, Sprite repartidor)
    {
        var raiz = CrearBase("Canvas", out float referenciaPPU);
        float multiplicador = referenciaPPU / (sprites["boton_pastel_normal"].pixelsPerUnit * Escala);
        float multiplicadorMarco = referenciaPPU / (sprites["marco_panel"].pixelsPerUnit * Escala);

        var menu = new GameObject("MenuPrincipal").AddComponent<MenuPrincipal>();

        // ---- Fondo: 480x270 a x4 ----
        var fondo = CrearHijo("Fondo", raiz);
        Estirar(fondo);
        var imgFondo = fondo.gameObject.AddComponent<Image>();
        imgFondo.sprite = sprites["fondo"];
        imgFondo.raycastTarget = false;

        // ---- Título blanco en Press Start con sombra rosa y morada (copias desplazadas, a lo pixel art) ----
        foreach (var (nombre, color, dx, dy) in new[] { ("TituloSombraMorada", Morado, 12, -16), ("TituloSombraRosa", Rosa, 6, -8), ("Titulo", Blanco, 0, 0) })
        {
            var rt = CrearHijo(nombre, raiz);
            Centrar(rt, dx, 330 + dy, 1600, 200);
            var tmp = CrearTexto(rt, "Delivery Brenn's", 156, color, false);
            FuenteProyecto.AplicarTitulo(tmp);
            tmp.fontSize = TamTitulo;
        }

        // ---- Repartidor idle de frente, a x6, de pie sobre el encaje del suelo ----
        var rtRepartidor = CrearHijo("Repartidor", raiz);
        rtRepartidor.anchorMin = rtRepartidor.anchorMax = new Vector2(0.5f, 0.5f);
        rtRepartidor.pivot = new Vector2(0.5f, 0);
        rtRepartidor.anchoredPosition = new Vector2(-560, -380);
        rtRepartidor.sizeDelta = repartidor.rect.size * 6;
        var imgRepartidor = rtRepartidor.gameObject.AddComponent<Image>();
        imgRepartidor.sprite = repartidor;
        imgRepartidor.preserveAspect = true;
        imgRepartidor.raycastTarget = false;

        // ---- Nombre y botones, en columna al centro ----
        var rtEtiqueta = CrearHijo("EtiquetaNombre", raiz);
        Centrar(rtEtiqueta, 0, 150, 600, 60);
        CrearTexto(rtEtiqueta, "Tu nombre", 44, Oscuro, true);

        var campo = CrearCampoNombre(raiz, sprites["marco_panel"], referenciaPPU);

        var iniciar = CrearBoton("BotonIniciar", raiz, "Iniciar juego", 0, -70, 600, 120, 56, sprites, true, multiplicador);
        var instrucciones = CrearBoton("BotonInstrucciones", raiz, "Instrucciones", 0, -200, 460, 96, 44, sprites, false, multiplicador);
        var salir = CrearBoton("BotonSalir", raiz, "Salir", 0, -316, 460, 96, 44, sprites, false, multiplicador);

        // ---- Panel de instrucciones (encima de todo, empieza cerrado) ----
        var panelInstrucciones = CrearPanelTexto("PanelInstrucciones", raiz, "Instrucciones", TextoInstrucciones,
                                                 sprites, multiplicador, multiplicadorMarco, out var cerrar);

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
        so.FindProperty("escenaJuego").stringValue = Path.GetFileNameWithoutExtension(RutaJuego);
        so.ApplyModifiedPropertiesWithoutUndo();

        // se apaga al final: TextMeshPro no inicializa su material en un objeto inactivo
        panelInstrucciones.SetActive(false);
    }

    // Cámara, EventSystem con el Input System y Canvas 1920x1080 (match 0.5). Lo usa también ConstruirIntro
    internal static RectTransform CrearBase(string nombreCanvas, out float referenciaPPU)
    {
        var camara = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
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

    // MenuPrincipal (0), Intro (1, si ya existe), SampleScene (2); las demás escenas se quedan detrás
    internal static bool PonerEscenasDelBuild(out string informe)
    {
        var primeras = new List<string> { RutaEscena };
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaIntro) != null) primeras.Add(RutaIntro);
        primeras.Add(RutaJuego);
        var escenas = primeras.Where(r => AssetDatabase.LoadAssetAtPath<SceneAsset>(r) != null)
            .Select(r => new EditorBuildSettingsScene(r, true)).ToList();
        escenas.AddRange(EditorBuildSettings.scenes.Where(e => !primeras.Contains(e.path)));
        EditorBuildSettings.scenes = escenas.ToArray();

        var reales = EditorBuildSettings.scenes;
        bool ok = primeras.Select((r, i) => reales.Length > i && reales[i].path == r && reales[i].enabled && !reales[i].guid.Empty()).All(b => b)
                  && primeras.Contains(RutaIntro);
        informe = "Build: " + string.Join(", ", reales.Select((e, i) => $"{Path.GetFileNameWithoutExtension(e.path)} ({i})")) +
                  (ok ? "" : primeras.Contains(RutaIntro) ? "  ← ORDEN INCORRECTO" : "  (falta Intro: ejecuta Construir intro)");
        return ok || !primeras.Contains(RutaIntro);   // sin Intro todavía no es un error del menú
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

    // Overlay oscuro a pantalla completa que bloquea clics, con el marco crema, título, texto y "Cerrar"
    static GameObject CrearPanelTexto(string nombre, RectTransform raiz, string titulo, string texto,
                                      Dictionary<string, Sprite> sprites, float multiplicador, float multiplicadorMarco,
                                      out Button cerrar)
    {
        var overlay = CrearHijo(nombre, raiz);
        Estirar(overlay);
        var velo = overlay.gameObject.AddComponent<Image>();
        velo.color = new Color(0, 0, 0, 0.6f);

        var marco = CrearHijo("Marco", overlay);
        Centrar(marco, 0, 0, 1100, 640);
        var img = marco.gameObject.AddComponent<Image>();
        img.sprite = sprites["marco_panel"];
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = multiplicadorMarco;

        var rtTitulo = CrearHijo("Titulo", marco);
        Centrar(rtTitulo, 0, 228, 900, 90);
        CrearTexto(rtTitulo, titulo, 64, Oscuro, true);

        var rtTexto = CrearHijo("Texto", marco);
        Centrar(rtTexto, 0, 10, 960, 300);
        var tmp = CrearTexto(rtTexto, texto, 38, Oscuro, false);
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.lineSpacing = 12;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 28;
        tmp.fontSizeMax = 38;

        cerrar = CrearBoton("BotonCerrar", marco, "Cerrar", 0, -220, 360, 88, 40, sprites, false, multiplicador);
        return overlay.gameObject;
    }

    // Campo con el marco crema a x3 y el texto a la izquierda, en la fuente pixel
    static TMP_InputField CrearCampoNombre(RectTransform padre, Sprite marco, float referenciaPPU)
    {
        var rt = CrearHijo("CampoNombre", padre);
        Centrar(rt, 0, 70, 600, 96);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = marco;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = referenciaPPU / (marco.pixelsPerUnit * 3);

        var area = CrearHijo("Text Area", rt);
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(36, 16);
        area.offsetMax = new Vector2(-36, -16);
        area.gameObject.AddComponent<RectMask2D>();

        var rtPlaceholder = CrearHijo("Placeholder", area);
        Estirar(rtPlaceholder);
        var placeholder = CrearTexto(rtPlaceholder, "Escribe tu nombre", 40, new Color32(Oscuro.r, Oscuro.g, Oscuro.b, 140), false);
        placeholder.alignment = TextAlignmentOptions.Left;

        var rtTexto = CrearHijo("Text", area);
        Estirar(rtTexto);
        var texto = CrearTexto(rtTexto, "", 40, Oscuro, false);
        texto.alignment = TextAlignmentOptions.Left;

        var campo = rt.gameObject.AddComponent<TMP_InputField>();
        campo.targetGraphic = img;
        campo.textViewport = area;
        campo.textComponent = texto;
        campo.placeholder = placeholder;
        campo.fontAsset = FuenteProyecto.Fuente();
        campo.pointSize = 40;
        campo.characterLimit = 16;
        campo.lineType = TMP_InputField.LineType.SingleLine;
        campo.customCaretColor = true;
        campo.caretColor = Oscuro;
        campo.caretWidth = 3;
        campo.selectionColor = new Color32(Rosa.r, Rosa.g, Rosa.b, 120);
        return campo;
    }

    // Pastilla pastel tipo piedra (dorada la principal, crema las demás) con su texto #2A1F40 sin contorno
    static Button CrearBoton(string nombre, Transform padre, string texto, float x, float y, float ancho, float alto, float tam,
                             Dictionary<string, Sprite> sprites, bool principal, float multiplicador)
    {
        var rt = CrearHijo(nombre, padre);
        Centrar(rt, x, y, ancho, alto);

        string clave = principal ? "boton_pastel_dorado" : "boton_pastel";
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprites[clave + "_normal"];
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = multiplicador;
        img.color = Color.white;

        var boton = rt.gameObject.AddComponent<Button>();
        boton.targetGraphic = img;
        boton.transition = Selectable.Transition.SpriteSwap;
        var estados = boton.spriteState;
        estados.pressedSprite = sprites[clave + "_presionado"];
        boton.spriteState = estados;

        // el texto deja libres las tapas, el brillo de arriba y el canto de abajo
        var rtTexto = CrearHijo("Texto", rt);
        rtTexto.anchorMin = Vector2.zero;
        rtTexto.anchorMax = Vector2.one;
        rtTexto.pivot = new Vector2(0.5f, 0.5f);
        rtTexto.offsetMin = new Vector2(6 * Escala, 4 * Escala);
        rtTexto.offsetMax = new Vector2(-6 * Escala, -2 * Escala);
        FuenteProyecto.EstiloBoton(CrearTexto(rtTexto, texto, tam, Oscuro, false), tam);
        return boton;
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
        foreach (var (ruta, metodo) in Conexiones)
        {
            var boton = raiz.Find(ruta)?.GetComponent<Button>();
            if (boton == null) { errores.Add($"No existe el botón {ruta}."); continue; }
            if (boton.onClick.GetPersistentEventCount() != 1 || boton.onClick.GetPersistentTarget(0) != menu
                || boton.onClick.GetPersistentMethodName(0) != metodo)
                errores.Add($"{ruta} no llama (solo) a MenuPrincipal.{metodo}.");
            var img = boton.GetComponent<Image>();
            if (img == null || img.sprite == null || !img.sprite.name.StartsWith("boton_pastel")) errores.Add($"{ruta}: no es una pastilla pastel.");
        }
        if (raiz.GetComponentsInChildren<Transform>(true).Any(t => t.name.Contains("Historia")))
            errores.Add("Todavía hay un botón o panel de Historia.");
        errores.AddRange(VerificarTextos(raiz, "Boton"));

        var so = new SerializedObject(menu);
        var campo = so.FindProperty("campoNombre").objectReferenceValue as TMP_InputField;
        if (campo == null || campo.transform.parent != raiz) errores.Add("MenuPrincipal: Campo Nombre no es el CampoNombre del menú.");
        else if (!(campo.placeholder is TMP_Text ph) || ph.text != "Escribe tu nombre") errores.Add("CampoNombre: el placeholder no dice \"Escribe tu nombre\".");
        else if (campo.fontAsset != FuenteProyecto.Fuente()) errores.Add("CampoNombre: no usa la fuente pixel.");
        if (so.FindProperty("panelHistoria").objectReferenceValue != null) errores.Add("MenuPrincipal: Panel Historia debería quedar vacío.");
        var instrucciones = so.FindProperty("panelInstrucciones").objectReferenceValue as GameObject;
        if (instrucciones == null || instrucciones.name != "PanelInstrucciones") errores.Add("MenuPrincipal: Panel Instrucciones sin asignar.");
        else if (instrucciones.activeSelf) errores.Add("PanelInstrucciones quedó activo.");
        if (so.FindProperty("escenaIntro").stringValue != "Intro") errores.Add("MenuPrincipal: Escena Intro no es \"Intro\".");
        if (so.FindProperty("escenaJuego").stringValue != Path.GetFileNameWithoutExtension(RutaJuego)) errores.Add("MenuPrincipal: Escena Juego no es SampleScene.");
        if (raiz.Find("Repartidor")?.GetComponent<Image>()?.sprite == null) errores.Add("Falta el repartidor.");
        if (raiz.Find("Titulo")?.GetComponent<TextMeshProUGUI>()?.text != "Delivery Brenn's") errores.Add("Falta el título.");
        foreach (var n in new[] { "Titulo", "TituloSombraRosa", "TituloSombraMorada" })
            if (!FuenteProyecto.EsTitulo(raiz.Find(n)?.GetComponent<TextMeshProUGUI>()?.font)) errores.Add($"{n}: no usa Press Start 2P.");
        if (raiz.Find("Fondo")?.GetComponent<Image>()?.sprite?.name != "menu_fondo") errores.Add("Falta el fondo menu_fondo.");

        ok = errores.Count == 0;
        return $"VERIFICACIÓN: MenuPrincipal con {Conexiones.Length} botones conectados, campo de nombre y panel de instrucciones." +
               (ok ? "\nSin errores." : "\nERRORES:\n- " + string.Join("\n- ", errores));
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
    }

    // Todos los textos con una fuente del proyecto; los de las pastillas (hijos de "Boton...") en Press Start,
    // #2A1F40, sin contorno, a su mínimo (24, que se ve como 36 en Pixelify) y cabiendo en la pastilla
    // (también para la Intro)
    internal static IEnumerable<string> VerificarTextos(Transform raiz, string prefijoBoton)
    {
        foreach (var tmp in raiz.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            string quien = $"{tmp.transform.parent.name}/{tmp.name}";
            if (!FuenteProyecto.EsDelProyecto(tmp.font) || tmp.fontSharedMaterial == null) { yield return $"{quien}: no usa la fuente pixel."; continue; }
            if (!tmp.transform.parent.name.StartsWith(prefijoBoton)) continue;
            if (!FuenteProyecto.EsTitulo(tmp.font)) yield return $"{quien}: el botón no usa Press Start 2P.";
            var c = (Color32)tmp.color;
            if (c.r != Oscuro.r || c.g != Oscuro.g || c.b != Oscuro.b || c.a != 255) yield return $"{quien}: el texto no es #2A1F40.";
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
