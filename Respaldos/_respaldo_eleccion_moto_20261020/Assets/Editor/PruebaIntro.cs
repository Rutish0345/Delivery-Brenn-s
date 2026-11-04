using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Prueba en Play del arranque del juego, empezando en MenuPrincipal (como el build):
// 1) captura del menú, escribe un nombre y clic en "Iniciar juego" → Intro
// 2) recorre las 5 viñetas: Espacio a mitad de la escritura (muestra todo), Espacio para pasar, clic
//    del ratón para pasar; revisa titulares, indicadores y que {nombre} se cambió, y captura las 5 → SampleScene
// 3) vuelve al menú, Iniciar juego y Esc en la primera viñeta → SampleScene
// 4) vuelve al menú, Iniciar juego y clic en "Saltar (Esc)" → SampleScene
// Teclado y ratón se simulan con eventos del Input System; los clics de botones pasan por el EventSystem.
// Temp/PruebaIntro.pedido (dentro: carpeta para las capturas) → Temp/PruebaIntro.resultado
[InitializeOnLoad]
public static class PruebaIntro
{
    const string Pedido = "Temp/PruebaIntro.pedido";
    const string Resultado = "Temp/PruebaIntro.resultado";
    const string Cancelar = "Temp/PruebaIntro.cancelar";
    const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";
    const string Nombre = "Rut";
    const string ClavePaso = "PruebaIntro.paso", ClaveLog = "PruebaIntro.log", ClaveConsola = "PruebaIntro.consola",
                 ClaveCarpeta = "PruebaIntro.carpeta", ClaveEspera = "PruebaIntro.espera", ClaveFallos = "PruebaIntro.fallos",
                 ClaveDesde = "PruebaIntro.desde", ClaveInicio = "PruebaIntro.inicio";

    static bool enganchado;

    static PruebaIntro()
    {
        if (enganchado) return;
        enganchado = true;
        EditorApplication.update += Revisar;
        Application.logMessageReceived += Anotar;
    }

    static int Paso { get => SessionState.GetInt(ClavePaso, 0); set => SessionState.SetInt(ClavePaso, value); }

    static void Log(string s) => SessionState.SetString(ClaveLog, SessionState.GetString(ClaveLog, "") + s + "\n");
    static void Fallo(string s) { Log("  FALLO: " + s); SessionState.SetInt(ClaveFallos, SessionState.GetInt(ClaveFallos, 0) + 1); }
    static void Comprobar(bool ok, string que) { if (ok) Log("  ok: " + que); else Fallo(que); }

    static void Anotar(string msg, string pila, LogType tipo)
    {
        if (Paso == 0 || tipo == LogType.Log) return;
        SessionState.SetString(ClaveConsola, SessionState.GetString(ClaveConsola, "") + $"[{tipo}] {msg}\n{(tipo == LogType.Exception ? pila : "")}");
    }

    static void Esperar(float segundos) => SessionState.SetFloat(ClaveEspera, (float)EditorApplication.timeSinceStartup + segundos);
    static bool Esperando => EditorApplication.timeSinceStartup < SessionState.GetFloat(ClaveEspera, 0);

    static void Revisar()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (Paso == 0)
        {
            if (!File.Exists(Pedido) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetString(ClaveCarpeta, File.ReadAllText(Pedido).Trim());
            File.Delete(Pedido);
            SessionState.SetString(ClaveLog, "");
            SessionState.SetString(ClaveConsola, "");
            SessionState.SetInt(ClaveFallos, 0);
            // Play empieza en MenuPrincipal sin cambiar la escena abierta (se deja como estaba al terminar)
            var inicio = EditorSceneManager.playModeStartScene;
            SessionState.SetString(ClaveInicio, inicio != null ? AssetDatabase.GetAssetPath(inicio) : "");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaMenu);
            Log("Entrando a Play desde MenuPrincipal.");
            Paso = 1;
            Esperar(2f);
            EditorApplication.isPlaying = true;
            return;
        }
        if (File.Exists(Cancelar))
        {
            // la prueba se quedó a medias (p. ej. se salió de Play por fuera): se cierra como en el paso 90
            File.Delete(Cancelar);
            Fallo($"cancelada en el paso {Paso}");
            Paso = 90;
            EditorApplication.isPlaying = false;
            return;
        }
        if (Esperando) return;
        try { Avanzar(); }
        catch (Exception e) { Fallo($"excepción en el paso {Paso}: {e}"); Paso = 90; EditorApplication.isPlaying = false; }
    }

    static void Avanzar()
    {
        if (Paso < 90 && !EditorApplication.isPlaying) { Esperar(0.5f); return; }
        switch (Paso)
        {
            case 1:
                // solo en esta sesión de Play (el ajuste del proyecto no cambia): que el juego siga
                // corriendo aunque la ventana de Unity no tenga el foco
                Application.runInBackground = true;
                if (!EsperarMenu()) return;
                EnfocarGameView();
                EntradaSinFoco();
                Log("1) Menú principal");
                Comprobar(Escena == "MenuPrincipal", $"Play empieza en {Escena}");
                Comprobar(Buscar("Canvas/BotonIniciar") != null && Buscar("Canvas/BotonInstrucciones") != null
                          && Buscar("Canvas/BotonSalir") != null, "botones Iniciar juego, Instrucciones y Salir");
                Comprobar(Buscar("Canvas/BotonHistoria") == null, "sin botón Historia");
                Capturar("Menu.png");
                var campo = Buscar("Canvas/CampoNombre").GetComponent<TMP_InputField>();
                campo.text = Nombre;
                Log($"  nombre escrito: {Nombre}");
                Clic("Canvas/BotonIniciar");
                Siguiente(0.5f);
                break;
            case 2:
                if (!EsperarIntro()) return;
                Log("2) Intro: viñeta 1 (se deja escribir sola)");
                Comprobar(Progreso.NombreJugador == Nombre, $"Progreso.NombreJugador = \"{Progreso.NombreJugador}\"");
                Siguiente(0.1f);
                break;
            case 3:
                if (!EsperarListo("Donde")) return;
                Comprobar(Imagen().sprite != null && Imagen().sprite.name == "vineta_1", "imagen vineta_1");
                Comprobar(TextoCompleto(), "aparece la placa con el texto completo");
                Comprobar(Titular().text == "SALÓN BRENN'S", $"titular \"{Titular().text}\"");
                Comprobar(TextoIndicador().text == "ESPACIO PARA CONTINUAR", $"indicador \"{TextoIndicador().text}\"");
                Siguiente(0.3f);
                break;
            case 4:
                Capturar("Vineta1.png");
                Tecla(Key.Space);
                Log("  Espacio → viñeta 2");
                Siguiente(0.15f);
                break;
            case 5:
                Soltar();
                if (!EsperarA(() => Texto().text.StartsWith("Las clientas"), 5, "la viñeta 2")) return;
                Siguiente(0.55f);   // durante el fundido no se lee la entrada
                break;
            case 6:
                Comprobar(!TextoCompleto(), $"viñeta 2 a mitad de escribirse ({Texto().maxVisibleCharacters}/{Texto().textInfo.characterCount})");
                Tecla(Key.Space);
                Log("  Espacio a mitad del texto");
                Siguiente(0.15f);
                break;
            case 7:
                Soltar();
                Comprobar(TextoCompleto() && Texto().text.StartsWith("Las clientas"), "Espacio muestra todo el texto sin pasar de viñeta");
                Siguiente(0.3f);
                break;
            case 8:
                Capturar("Vineta2.png");
                Tecla(Key.Space);
                Log("  Espacio → viñeta 3");
                Siguiente(0.15f);
                break;
            case 9:
                Soltar();
                if (!EsperarListo("Tú llevas")) return;
                Comprobar(Titular().text == $"{Nombre}, TE NECESITAMOS", $"{{nombre}} cambiado en el titular: \"{Titular().text}\"");
                Siguiente(0.3f);
                break;
            case 10:
                Capturar("Vineta3.png");
                ClicRaton();
                Log("  clic del ratón → viñeta 4");
                Siguiente(0.15f);
                break;
            case 11:
                SoltarRaton();
                if (!EsperarListo("Muévete")) return;
                QuitarRaton();
                Comprobar(Imagen().sprite.name == "vineta_4", "imagen vineta_4");
                Siguiente(0.3f);
                break;
            case 12:
                Capturar("Vineta4.png");
                Tecla(Key.Space);
                Log("  Espacio → viñeta 5");
                Siguiente(0.15f);
                break;
            case 13:
                Soltar();
                if (!EsperarListo("Junta")) return;
                Comprobar(TextoIndicador().text == "PRESIONA ESPACIO PARA EMPEZAR", $"indicador de la última \"{TextoIndicador().text}\"");
                // el texto largo cabe en su caja (que termina arriba de la placa) en 3 líneas como máximo
                Comprobar(Texto().textInfo.lineCount <= 3 && Texto().textBounds.size.y <= Texto().rectTransform.rect.height + 0.5f,
                          $"texto de la viñeta 5 en {Texto().textInfo.lineCount} líneas a {Texto().fontSize:0.#}, " +
                          $"{Texto().textBounds.size.y:0} de alto en una caja de {Texto().rectTransform.rect.height:0}");
                Siguiente(0.3f);
                break;
            case 14:
                Capturar("Vineta5.png");
                Tecla(Key.Space);
                Log("  Espacio en la última viñeta");
                Siguiente(0.15f);
                break;
            case 15:
                Soltar();
                if (!EsperarJuego("al terminar la intro")) return;
                Log("3) De vuelta al menú para saltar con Esc");
                SceneManager.LoadScene("MenuPrincipal");
                Siguiente(0.5f);
                break;
            case 16:
                if (!EsperarMenu()) return;
                Clic("Canvas/BotonIniciar");
                Siguiente(0.5f);
                break;
            case 17:
                if (!EsperarIntro()) return;
                Siguiente(0.6f);
                break;
            case 18:
                Tecla(Key.Escape);
                Log("  Esc en la viñeta 1");
                Siguiente(0.15f);
                break;
            case 19:
                Soltar();
                if (!EsperarJuego("con Esc")) return;
                Log("4) De vuelta al menú para saltar con el botón");
                SceneManager.LoadScene("MenuPrincipal");
                Siguiente(0.5f);
                break;
            case 20:
                if (!EsperarMenu()) return;
                Clic("Canvas/BotonIniciar");
                Siguiente(0.5f);
                break;
            case 21:
                if (!EsperarIntro()) return;
                Siguiente(0.6f);
                break;
            case 22:
                Clic("Canvas/BotonSaltar");
                Siguiente(0.3f);
                break;
            case 23:
                if (!EsperarJuego("con \"Saltar (Esc)\"")) return;
                Paso = 90;
                EditorApplication.isPlaying = false;
                Esperar(1f);
                break;
            case 90:
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                QuitarRaton();
                RestaurarEntrada();
                var inicio = SessionState.GetString(ClaveInicio, "");
                EditorSceneManager.playModeStartScene = inicio.Length > 0 ? AssetDatabase.LoadAssetAtPath<SceneAsset>(inicio) : null;
                int fallos = SessionState.GetInt(ClaveFallos, 0);
                string consola = SessionState.GetString(ClaveConsola, "");
                File.WriteAllText(Resultado, (fallos == 0 ? "OK" : $"{fallos} FALLOS") + "\n" + SessionState.GetString(ClaveLog, "") +
                                  "\nCONSOLA (advertencias y errores en Play): " + (consola.Length == 0 ? "ninguno" : "\n" + consola));
                Paso = 0;
                break;
        }
    }

    static string Escena => SceneManager.GetActiveScene().name;

    static void Siguiente(float espera) { Paso++; Esperar(espera); }

    static bool EsperarA(Func<bool> condicion, float limite, string que)
    {
        float ahora = (float)EditorApplication.timeSinceStartup;
        if (SessionState.GetFloat(ClaveDesde, 0) == 0) SessionState.SetFloat(ClaveDesde, ahora);
        bool listo = false;
        try { listo = condicion(); } catch (NullReferenceException) { }   // la escena todavía está cargando
        if (!listo && ahora - SessionState.GetFloat(ClaveDesde, 0) < limite) { Esperar(0.1f); return false; }
        if (!listo) Fallo($"se acabó el tiempo esperando {que}");
        SessionState.SetFloat(ClaveDesde, 0);
        return true;
    }

    static bool EsperarIntro()
    {
        if (!EsperarA(() => Escena == "Intro" && Object.FindFirstObjectByType<IntroHistoria>() != null, 10, "que cargue la Intro")) return false;
        Comprobar(Escena == "Intro", $"Iniciar juego carga {Escena}");
        return true;
    }

    // el menú ya dibujó un par de fotogramas (antes el raycast no da en nada)
    static bool EsperarMenu() => EsperarA(() => Escena == "MenuPrincipal" && Time.timeSinceLevelLoad > 0.3f
                                                && EventSystem.current != null && Buscar("Canvas/BotonIniciar") != null, 15, "a que arranque el menú");

    // la viñeta terminó de escribirse: se enciende "Espacio para continuar"
    static bool EsperarListo(string inicio)
    {
        if (!EsperarA(() => Texto().text.StartsWith(inicio) && Indicador().activeInHierarchy, 10, $"la viñeta que empieza con \"{inicio}\"")) return false;
        Log($"  viñeta \"{inicio}…\" completa, indicador visible");
        return true;
    }

    static bool EsperarJuego(string como)
    {
        if (!EsperarA(() => Escena == "SampleScene" && GameManager.Instance != null && GameManager.Instance.PuntoActual != null, 15,
                      "que cargue SampleScene")) return false;
        Comprobar(Escena == "SampleScene" && Time.timeScale == 1f, $"la intro termina {como} y carga {Escena} (timeScale {Time.timeScale})");
        return true;
    }

    static IntroHistoria Intro() => Object.FindFirstObjectByType<IntroHistoria>();
    static T Campo<T>(string nombre) where T : Object => new SerializedObject(Intro()).FindProperty(nombre).objectReferenceValue as T;
    static TextMeshProUGUI Texto() => Campo<TextMeshProUGUI>("texto");
    static TextMeshProUGUI Titular() => Campo<TextMeshProUGUI>("titular");
    static TextMeshProUGUI TextoIndicador() => Campo<TextMeshProUGUI>("textoIndicador");
    static Image Imagen() => Campo<Image>("imagen");
    static GameObject Indicador() => Campo<GameObject>("indicadorSiguiente");
    static bool TextoCompleto() => Texto().maxVisibleCharacters >= Texto().textInfo.characterCount && Texto().textInfo.characterCount > 0;

    // ---- Entrada simulada ----
    static void Tecla(Key k) => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k));
    static void Soltar() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
    // el puntero en una esquina donde no hay botones (abajo a la izquierda, sobre la viñeta). Va por un ratón
    // virtual que se vuelve Mouse.current: el ratón real del editor manda su propio estado en cada cuadro y
    // pisaba el clic simulado antes de que el juego lo leyera
    static Mouse Raton()
    {
        var r = InputSystem.GetDevice<Mouse>("RatonPrueba") ?? InputSystem.AddDevice<Mouse>("RatonPrueba");
        r.MakeCurrent();
        return r;
    }
    static void ClicRaton() => InputSystem.QueueStateEvent(Raton(), new MouseState { position = new Vector2(20, 20) }.WithButton(MouseButton.Left));
    static void SoltarRaton()
    {
        InputSystem.QueueStateEvent(Raton(), new MouseState { position = new Vector2(20, 20) });
    }
    static void QuitarRaton() { if (InputSystem.GetDevice<Mouse>("RatonPrueba") is Mouse r) InputSystem.RemoveDevice(r); }

    // Mientras dura Play, una copia de los ajustes del Input System que no depende del foco (la ventana
    // de Unity o la Game view pueden no tenerlo). La copia no es un asset: no se guarda nada y al salir
    // de Play se vuelven a poner los del proyecto
    static InputSettings ajustesProyecto;

    // Vuelve a poner los ajustes del proyecto. Si la copia se perdió (recarga del dominio a media prueba),
    // se toman del asset configurado del proyecto; si no, el Input System avisa en cada cambio de Play.
    static void RestaurarEntrada()
    {
        if (ajustesProyecto == null)
            EditorBuildSettings.TryGetConfigObject("com.unity.input.settings", out ajustesProyecto);
        if (ajustesProyecto != null && InputSystem.settings != ajustesProyecto) InputSystem.settings = ajustesProyecto;
        ajustesProyecto = null;
    }

    static void EntradaSinFoco()
    {
        ajustesProyecto = InputSystem.settings;
        var ajustes = Object.Instantiate(InputSystem.settings);
        ajustes.hideFlags = HideFlags.HideAndDontSave;   // que no la descargue el cambio de escena (Unload unused assets)
        ajustes.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        ajustes.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings = ajustes;
    }

    // el Input System solo escucha al teclado si la Game view tiene el foco
    static void EnfocarGameView()
    {
        var tipo = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if (tipo != null) EditorWindow.FocusWindowIfItsOpen(tipo);
    }

    static Transform Buscar(string ruta)
    {
        var partes = ruta.Split(new[] { '/' }, 2);
        var raiz = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == partes[0])?.transform;
        return raiz == null ? null : partes.Length == 1 ? raiz : raiz.Find(partes[1]);
    }

    static void Clic(string ruta)
    {
        var t = Buscar(ruta);
        if (t == null || !t.gameObject.activeInHierarchy) { Fallo($"{ruta} no existe o está apagado"); return; }
        var datos = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, t.position) };
        var golpes = new List<RaycastResult>();
        EventSystem.current.RaycastAll(datos, golpes);
        var arriba = golpes.Count > 0 ? golpes[0].gameObject : null;
        if (arriba == null || !(arriba.transform == t || arriba.transform.IsChildOf(t)))
        {
            Fallo($"clic en {ruta}: lo tapa {(arriba != null ? arriba.name : "nada (sin raycast)")}");
            return;
        }
        ExecuteEvents.ExecuteHierarchy(arriba, datos, ExecuteEvents.pointerClickHandler);
        Log($"  clic en {ruta}");
    }

    // Render a 1920x1080 con la cámara de la escena, con el Canvas en Screen Space - Camera solo durante el render
    static void Capturar(string archivo)
    {
        var ruta = Path.Combine(SessionState.GetString(ClaveCarpeta, "Temp"), archivo);
        var cam = Camera.main;
        var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.isRootCanvas);
        var rt = new RenderTexture(1920, 1080, 24);
        var modo = canvas.renderMode;
        try
        {
            cam.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = cam.nearClipPlane + 1;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            File.WriteAllBytes(ruta, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        finally
        {
            RenderTexture.active = null;
            cam.targetTexture = null;
            canvas.renderMode = modo;
            rt.Release();
        }
        Log($"  captura 1920x1080: {ruta}");
    }
}
