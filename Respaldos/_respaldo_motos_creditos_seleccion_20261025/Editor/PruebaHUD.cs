using System;
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

// Prueba en Play del HUD de los niveles con el estilo de BotonBrenn (EstiloHUD), desde MenuPrincipal a 1920x1080:
//  - en SampleScene, Nivel2_Comercial y Nivel3_Lluvia (con la tienda cerrada con EMPEZAR NIVEL): ningún rectángulo del
//    HUD (caja, marco de satisfacción, dinero, pausa) se sale de la pantalla, queda a menos de 24 del borde ni se encima;
//    con el turbo visible tampoco; los textos caben ("$99999", "Pedidos 10/10") y llevan su color.
//  - el botón de pausa (nivel 1): 72x72 a 24 de las orillas, solo dos barras crema 10x32; con el ratón encima, doradas;
//    el clic, Esc y P pausan y siguen, con los sonidos pausa_abrir / pausa_cerrar.
// Los rectángulos se miden con el Canvas a 1920x1080 (el mismo cambio a ScreenSpaceCamera de la captura).
// Capturas: HUD_Nivel1, HUD_Nivel2, HUD_Nivel3, Pausa_reposo, Pausa_hover, Pausa_abierta.
// Temp/PruebaHUD.pedido (dentro: carpeta para las capturas) → Temp/PruebaHUD.resultado
[InitializeOnLoad]
public static class PruebaHUD
{
    const string Pedido = "Temp/PruebaHUD.pedido";
    const string Resultado = "Temp/PruebaHUD.resultado";
    const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";
    const string P = "PruebaHUD.";
    static readonly string[] Niveles = { "SampleScene", "Nivel2_Comercial", "Nivel3_Lluvia" };

    static bool enganchado;

    static PruebaHUD()
    {
        if (enganchado) return;
        enganchado = true;
        EditorApplication.update += Revisar;
        Application.logMessageReceived += Anotar;
    }

    static int Paso { get => SessionState.GetInt(P + "paso", 0); set => SessionState.SetInt(P + "paso", value); }
    static int Nivel { get => SessionState.GetInt(P + "nivel", 0); set => SessionState.SetInt(P + "nivel", value); }
    static void Log(string s) => SessionState.SetString(P + "log", SessionState.GetString(P + "log", "") + s + "\n");
    static void Fallo(string s) { Log("  FALLO: " + s); SessionState.SetInt(P + "fallos", SessionState.GetInt(P + "fallos", 0) + 1); }
    static void Comprobar(bool ok, string que) { if (ok) Log("  ok: " + que); else Fallo(que); }
    static void Anotar(string msg, string pila, LogType tipo)
    {
        if (Paso == 0 || tipo == LogType.Log) return;
        SessionState.SetString(P + "consola", SessionState.GetString(P + "consola", "") + $"[{tipo}] {msg}\n{(tipo == LogType.Exception ? pila : "")}");
    }
    static void Esperar(float s) => SessionState.SetFloat(P + "espera", (float)EditorApplication.timeSinceStartup + s);
    static bool Esperando => EditorApplication.timeSinceStartup < SessionState.GetFloat(P + "espera", 0);
    static void Ir(int paso, float espera) { Paso = paso; Esperar(espera); }
    static string Escena => SceneManager.GetActiveScene().name;
    static AudioManager AM => AudioManager.Instance;
    static MenuJuego Menu => Object.FindFirstObjectByType<MenuJuego>();
    static bool PausaAbierta => Buscar("Canvas/PanelPausa")?.gameObject.activeInHierarchy == true;
    static Canvas CanvasHUD => Buscar("Canvas")?.GetComponent<Canvas>();
    static BotonBrenn Pausa => Buscar("Canvas/BotonPausa")?.GetComponent<BotonBrenn>();

    static void Revisar()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (Paso == 0)
        {
            if (!File.Exists(Pedido) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetString(P + "carpeta", File.ReadAllText(Pedido).Trim());
            File.Delete(Pedido);
            SessionState.SetString(P + "log", "");
            SessionState.SetString(P + "consola", "");
            SessionState.SetInt(P + "fallos", 0);
            SessionState.SetFloat(P + "desde", 0);
            ProgresoPrueba.Limpiar();
            ProgresoPrueba.Preparar(motos: 0b0011, dinero: 2000, todosLosNiveles: true);
            var inicio = EditorSceneManager.playModeStartScene;
            SessionState.SetString(P + "inicio", inicio != null ? AssetDatabase.GetAssetPath(inicio) : "");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaMenu);
            Log("Entrando a Play desde MenuPrincipal.");
            Nivel = 0;
            Ir(1, 2f);
            EditorApplication.isPlaying = true;
            return;
        }
        if (Esperando) return;
        try { Avanzar(); }
        catch (Exception e) { Fallo($"excepción en el paso {Paso}: {e}"); Soltar(); Paso = 90; EditorApplication.isPlaying = false; }
    }

    static void Avanzar()
    {
        if (Paso < 90 && !EditorApplication.isPlaying) { Fallo($"Play se cerró en el paso {Paso}"); Paso = 90; return; }
        string nivel = Niveles[Mathf.Min(Nivel, Niveles.Length - 1)];
        switch (Paso)
        {
            case 1:
                Application.runInBackground = true;
                if (Nivel == 0 && !EsperarA(() => Escena == "MenuPrincipal" && Time.timeSinceLevelLoad > 0.5f, 15, "el menú")) return;
                Log($"{Nivel + 1}) {nivel}");
                Progreso.MarcarPersonajeElegido();
                Time.timeScale = 1f;
                SceneManager.LoadScene(nivel);
                Ir(2, 0.6f);
                break;
            case 2:   // que arranque; con motos, EMPEZAR NIVEL cierra la tienda
            {
                if (!EsperarA(() => Escena == nivel && GameManager.Instance != null, 15, nivel)) return;   // con la tienda abierta el tiempo está en 0
                var tienda = Object.FindFirstObjectByType<TiendaMotos>();
                if (tienda != null && tienda.Abierta)
                {
                    var empezar = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == "BotonEmpezar" && t.gameObject.activeInHierarchy);
                    empezar?.GetComponent<Button>().onClick.Invoke();
                }
                Ir(3, 1.2f);
                break;
            }
            case 3:   // los rectángulos y los textos
            {
                Comprobar(Object.FindFirstObjectByType<TiendaMotos>()?.Abierta != true && !PausaAbierta, $"{nivel}: jugando, sin ventanas encima");
                Capturar($"HUD_Nivel{Nivel + 1}.png", () => RevisarHUD(nivel));   // medido a 1920x1080, como se captura
                if (Nivel == 0) { Ir(10, 0.1f); break; }
                Siguiente();
                break;
            }

            // ---- el botón de pausa (nivel 1)
            case 10:
            {
                var b = Pausa;
                var rt = (RectTransform)b.transform;
                var barras = b.Glifos.Cast<Image>().ToList();
                Comprobar(barras.Count == 2 && barras.All(i => ColorUtility.ToHtmlStringRGB(i.color) == "FFF1D0") && !b.Elegido,
                          $"en reposo: las dos barras crema ({string.Join(", ", barras.Select(i => ColorUtility.ToHtmlStringRGB(i.color)))})");
                Comprobar(rt.rect.size == Vector2.one * EstiloHUD.LadoPausa && rt.childCount == 2, $"72x72 y solo las dos barras ({rt.rect.size}, {rt.childCount} hijos)");
                Capturar("Pausa_reposo.png");
                Ir(11, 0.1f);
                break;
            }
            case 11:
                EventSystem.current.SetSelectedGameObject(null);
                Raton(RectTransformUtility.WorldToScreenPoint(null, Pausa.transform.position), false);
                Ir(12, 0.2f);
                break;
            case 12:
            {
                var b = Pausa;
                if (!EsperarA(() => b.Elegido, 3, "el hover del botón de pausa")) return;
                var barras = b.Glifos.Cast<Image>().ToList();
                Comprobar(barras.All(i => ColorUtility.ToHtmlStringRGB(i.color) == "FFD166") && b.Borde.sprite.name == "marco_icono_hover",
                          "hover: barras y borde dorados");
                // nada encima del botón: el rayo de su centro le llega a él
                var golpes = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, b.transform.position) }, golpes);
                Comprobar(golpes.Count > 0 && golpes[0].gameObject.transform.IsChildOf(b.transform), $"nada tapa el botón ({(golpes.Count > 0 ? golpes[0].gameObject.name : "nada")})");
                Capturar("Pausa_hover.png");
                Guardar("abrir", AM.VecesSonado("pausa_abrir"));
                Ir(13, 0.1f);
                break;
            }
            case 13:   // clic de verdad
                Raton(RectTransformUtility.WorldToScreenPoint(null, Pausa.transform.position), true);
                Ir(14, 0.1f);
                break;
            case 14:
                Raton(RectTransformUtility.WorldToScreenPoint(null, Pausa.transform.position), false);
                Ir(15, 0.3f);
                break;
            case 15:
                Comprobar(PausaAbierta && Time.timeScale == 0f && AM.VecesSonado("pausa_abrir") > Dato("abrir"), "el clic pausa (con pausa_abrir)");
                Raton(new Vector2(960, 200), false);
                Ir(16, 0.3f);
                break;
            case 16:
                Capturar("Pausa_abierta.png");
                Guardar("cerrar", AM.VecesSonado("pausa_cerrar"));
                Tecla(Key.Escape);
                Ir(17, 0.15f);
                break;
            case 17:
                Soltar();
                Ir(18, 0.2f);
                break;
            case 18:
                Comprobar(!PausaAbierta && Time.timeScale == 1f && AM.VecesSonado("pausa_cerrar") > Dato("cerrar"), "Esc sigue el juego (con pausa_cerrar)");
                Tecla(Key.P);
                Ir(19, 0.15f);
                break;
            case 19:
                Soltar();
                Ir(20, 0.2f);
                break;
            case 20:
                Comprobar(PausaAbierta && Time.timeScale == 0f, "P pausa");
                Tecla(Key.P);
                Ir(21, 0.15f);
                break;
            case 21:
                Soltar();
                Ir(22, 0.2f);
                break;
            case 22:
                Comprobar(!PausaAbierta && Time.timeScale == 1f, "P sigue el juego");
                Siguiente();
                break;

            case 90:
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                ProgresoPrueba.Limpiar();
                var raton = InputSystem.GetDevice<Mouse>("RatonPrueba");
                if (raton != null) InputSystem.RemoveDevice(raton);
                foreach (var real in InputSystem.devices.OfType<Mouse>().Where(m => !m.enabled).ToList()) InputSystem.EnableDevice(real);
                var inicio = SessionState.GetString(P + "inicio", "");
                EditorSceneManager.playModeStartScene = inicio.Length > 0 ? AssetDatabase.LoadAssetAtPath<SceneAsset>(inicio) : null;
                int fallos = SessionState.GetInt(P + "fallos", 0);
                string consola = SessionState.GetString(P + "consola", "");
                File.WriteAllText(Resultado, (fallos == 0 ? "OK" : $"{fallos} FALLOS") + "\n" + SessionState.GetString(P + "log", "") +
                                  "\nCONSOLA (advertencias y errores en Play): " + (consola.Length == 0 ? "ninguno" : "\n" + consola));
                Paso = 0;
                break;
        }
    }

    static void Siguiente()
    {
        Nivel++;
        if (Nivel < Niveles.Length) { Ir(1, 0.1f); return; }
        Soltar();
        Time.timeScale = 1f;
        EditorApplication.isPlaying = false;
        Ir(90, 1f);
    }

    static void RevisarHUD(string nivel)
    {
        var canvas = CanvasHUD;
        Canvas.ForceUpdateCanvases();
        var problemas = EstiloHUD.Problemas(canvas);
        var pantalla = ((RectTransform)canvas.transform).rect;
        Comprobar(problemas.Count == 0 && Mathf.Approximately(pantalla.width, 1920) && Mathf.Approximately(pantalla.height, 1080),
                  $"{nivel}: el HUD dentro de la pantalla ({pantalla.width:0}x{pantalla.height:0}) a 24 o más del borde y sin encimarse — " +
                  string.Join(", ", EstiloHUD.Piezas.Select(n => Buscar("Canvas/" + n) is RectTransform r ? $"{n} {EstiloHUD.Texto(EstiloHUD.EnCanvas(r, canvas))}" : n + " FALTA")) +
                  (problemas.Count > 0 ? " — " + string.Join("; ", problemas) : ""));

        // con el turbo en la caja: la caja crece y sigue sin salirse ni encimarse
        var turbo = Buscar("Canvas/CajaHUD/PlacaTurbo")?.GetComponent<LayoutElement>();
        if (turbo != null)
        {
            bool antes = turbo.ignoreLayout;
            var grupo = turbo.GetComponent<CanvasGroup>();
            float alfa = grupo != null ? grupo.alpha : 1f;
            turbo.ignoreLayout = false;
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)Buscar("Canvas/CajaHUD"));
            var conTurbo = EstiloHUD.Problemas(canvas);
            Comprobar(conTurbo.Count == 0, $"{nivel}: con el turbo visible tampoco" + (conTurbo.Count > 0 ? " — " + string.Join("; ", conTurbo) : ""));
            turbo.ignoreLayout = antes;
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)Buscar("Canvas/CajaHUD"));
            if (grupo != null) grupo.alpha = alfa;
        }

        foreach (var (ruta, maximo, color) in new[] { ("Canvas/PlacaDinero/TextoDinero", EstiloHUD.TextoDineroMaximo, "FFD166"),
                                                       ("Canvas/CajaHUD/PlacaPedidos/TextoPedidos", EstiloHUD.TextoPedidosMaximo, "FFF1D0"),
                                                       ("Canvas/CajaHUD/PlacaReloj/TextoReloj", EstiloHUD.TextoRelojMaximo, "FFF1D0") })
        {
            var t = Buscar(ruta)?.GetComponent<TMP_Text>();
            Comprobar(t != null && FuenteProyecto.Ancho(t, maximo) <= t.rectTransform.rect.width + 0.5f && ColorUtility.ToHtmlStringRGB(t.color) == color,
                      $"{ruta.Split('/').Last()}: \"{t?.text}\" #{color}, y \"{maximo}\" cabe");
        }
    }

    // ------------------------------------------------------------------ entrada real

    static void Tecla(Key k)
    {
        PruebaIntro.EnfocarGameView();
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k));
    }
    static void Soltar() { if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); }

    // ratón virtual que se vuelve Mouse.current; el real del editor se apaga mientras dura la prueba (case 90 lo prende)
    static void Raton(Vector2 donde, bool abajo)
    {
        foreach (var real in InputSystem.devices.OfType<Mouse>().Where(m => m.name != "RatonPrueba" && m.enabled).ToList())
            InputSystem.DisableDevice(real);
        var r = InputSystem.GetDevice<Mouse>("RatonPrueba") ?? InputSystem.AddDevice<Mouse>("RatonPrueba");
        r.MakeCurrent();
        var estado = new MouseState { position = donde };
        InputSystem.QueueStateEvent(r, abajo ? estado.WithButton(MouseButton.Left) : estado);
    }

    // ------------------------------------------------------------------ ayudantes

    static void Guardar(string k, float v) => SessionState.SetFloat(P + "dato." + k, v);
    static float Dato(string k) => SessionState.GetFloat(P + "dato." + k, 0);

    static bool EsperarA(Func<bool> condicion, float limite, string que)
    {
        float ahora = (float)EditorApplication.timeSinceStartup;
        if (SessionState.GetFloat(P + "desde", 0) == 0) SessionState.SetFloat(P + "desde", ahora);
        bool listo = false;
        try { listo = condicion(); } catch (NullReferenceException) { } catch (InvalidOperationException) { }
        if (!listo && ahora - SessionState.GetFloat(P + "desde", 0) < limite) { Esperar(0.05f); return false; }
        if (!listo) Fallo($"se acabó el tiempo esperando {que}");
        SessionState.SetFloat(P + "desde", 0);
        return true;
    }

    static Transform Buscar(string ruta)
    {
        var partes = ruta.Split(new[] { '/' }, 2);
        var raiz = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == partes[0])?.transform;
        return raiz == null ? null : partes.Length == 1 ? raiz : raiz.Find(partes[1]);
    }

    // como PruebaBotones: el Canvas pasa un momento a ScreenSpaceCamera para leerlo a 1920x1080
    static void Capturar(string archivo, Action antes = null)
    {
        var ruta = Path.Combine(SessionState.GetString(P + "carpeta", "Temp"), archivo);
        var cam = Camera.main;
        var canvas = CanvasHUD;
        var rt = new RenderTexture(1920, 1080, 24);
        var modo = canvas.renderMode;
        var camCanvas = canvas.worldCamera;
        int capa = canvas.sortingLayerID, orden = canvas.sortingOrder;
        float distancia = canvas.planeDistance;
        try
        {
            cam.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.sortingLayerID = SortingLayer.layers.Last().id;
            canvas.sortingOrder = short.MaxValue;
            canvas.planeDistance = cam.nearClipPlane + 1;
            Canvas.ForceUpdateCanvases();
            if (antes != null)
            {
                antes();
                Canvas.ForceUpdateCanvases();
            }
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
            canvas.worldCamera = camCanvas;
            canvas.sortingLayerID = capa;
            canvas.sortingOrder = orden;
            canvas.planeDistance = distancia;
            rt.Release();
        }
        Log($"  captura 1920x1080: {ruta}");
    }
}
