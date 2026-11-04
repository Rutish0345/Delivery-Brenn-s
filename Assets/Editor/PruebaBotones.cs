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

// Prueba en Play del estilo único de botones (BotonBrenn), desde MenuPrincipal a 1920x1080: en cada pantalla, todos los
// botones visibles con BotonBrenn (nada del estilo viejo), el texto dentro del botón, el clic les llega (nada encima en su
// centro) y el teclado los elige (la primera flecha elige uno; la segunda mueve). Pantallas y capturas: Menu,
// Menu_hover_instrucciones (la tijera y el dorado), Seleccion, SeleccionNivel, Pausa, NivelCompletado, GameOver, Final y
// Motos (ELIGE TU MOTO, la ventana que se arma al jugar). Los clics de verdad de cada botón los siguen probando las
// demás Prueba* (PruebaNiveles, PruebaDerrota, PruebaPausaMenu, PruebaAudio, PruebaPresentacion).
// Temp/PruebaBotones.pedido (dentro: carpeta para las capturas) → Temp/PruebaBotones.resultado
[InitializeOnLoad]
public static class PruebaBotones
{
    const string Pedido = "Temp/PruebaBotones.pedido";
    const string Resultado = "Temp/PruebaBotones.resultado";
    const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";
    const string P = "PruebaBotones.";

    // pantalla: escena a cargar (null: la que está), lo que hay que hacer para que aparezca y cuándo está lista
    static readonly (string nombre, string escena, Action preparar, Func<bool> lista)[] Pantallas =
    {
        ("Menu", null, () => Object.FindFirstObjectByType<MenuPrincipal>()?.Continuar(), () => Object.FindFirstObjectByType<MenuPrincipal>()?.MenuVisible == true),
        ("Seleccion", "Seleccion", null, () => Time.timeSinceLevelLoad > 0.8f),
        ("SeleccionNivel", "SeleccionNivel", null, () => Time.timeSinceLevelLoad > 0.8f),
        ("Pausa", "SampleScene", () => Object.FindFirstObjectByType<MenuJuego>().AlternarPausa(),
         () => Buscar("Canvas/PanelPausa")?.gameObject.activeInHierarchy == true),
        ("NivelCompletado", null, () => { Object.FindFirstObjectByType<MenuJuego>().AlternarPausa(); NivelCompletado(3, 100f); },
         () => Buscar("Canvas/PanelFinal")?.gameObject.activeInHierarchy == true),
        ("GameOver", "SampleScene", () => Object.FindFirstObjectByType<PantallaDespido>(FindObjectsInactive.Include).Mostrar(),
         () => Object.FindFirstObjectByType<PantallaDespido>()?.Visible == true),
        ("Final", "Final", null, () => Time.timeSinceLevelLoad > 1.2f),
        ("Motos", "Nivel2_Comercial", null, () => Object.FindFirstObjectByType<TiendaMotos>()?.Abierta == true),
    };

    static bool enganchado;

    static PruebaBotones()
    {
        if (enganchado) return;
        enganchado = true;
        EditorApplication.update += Revisar;
        Application.logMessageReceived += Anotar;
    }

    static int Paso { get => SessionState.GetInt(P + "paso", 0); set => SessionState.SetInt(P + "paso", value); }
    static int Pantalla { get => SessionState.GetInt(P + "pantalla", 0); set => SessionState.SetInt(P + "pantalla", value); }
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
            ProgresoPrueba.Preparar(motos: 0b0011, dinero: 2000, todosLosNiveles: true);   // la tienda con motos compradas y por comprar
            var inicio = EditorSceneManager.playModeStartScene;
            SessionState.SetString(P + "inicio", inicio != null ? AssetDatabase.GetAssetPath(inicio) : "");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaMenu);
            Log("Entrando a Play desde MenuPrincipal.");
            Pantalla = 0;
            Ir(1, 2f);
            EditorApplication.isPlaying = true;
            return;
        }
        if (Esperando) return;
        try { Avanzar(); }
        catch (Exception e) { Fallo($"excepción en el paso {Paso} ({Pantallas[Mathf.Min(Pantalla, Pantallas.Length - 1)].nombre}): {e}"); Soltar(); Paso = 90; EditorApplication.isPlaying = false; }
    }

    static void Avanzar()
    {
        if (Paso < 90 && !EditorApplication.isPlaying) { Fallo($"Play se cerró en el paso {Paso}"); Paso = 90; return; }
        var p = Pantallas[Mathf.Min(Pantalla, Pantallas.Length - 1)];
        switch (Paso)
        {
            case 1:   // cargar la escena de la pantalla (o seguir en la que está)
                Application.runInBackground = true;
                if (Pantalla == 0 && !EsperarA(() => Escena == "MenuPrincipal" && Time.timeSinceLevelLoad > 0.5f, 15, "el menú")) return;
                Log($"{Pantalla + 1}) {p.nombre}");
                if (p.escena != null && (Escena != p.escena || p.nombre == "GameOver"))
                {
                    Progreso.MarcarPersonajeElegido();
                    Time.timeScale = 1f;
                    SceneManager.LoadScene(p.escena);
                }
                Ir(2, 0.6f);
                break;
            case 2:   // que la escena arranque y abrir lo que toca
                if (!EsperarA(() => p.escena == null || Escena == p.escena && (p.escena != "SampleScene" || GameManager.Instance != null && Time.timeSinceLevelLoad > 0.8f), 15, p.escena ?? Escena)) return;
                p.preparar?.Invoke();
                Ir(3, 0.1f);
                break;
            case 3:
                if (!EsperarA(p.lista, 15, p.nombre)) return;
                Ir(4, 0.7f);   // los fundidos
                break;
            case 4:   // los botones de la pantalla
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                RevisarBotones(p.nombre);
                Capturar(p.nombre + ".png");
                Ir(5, 0.1f);
                break;
            }
            case 5:   // teclado: la primera flecha elige un botón
                Tecla(Key.DownArrow);
                Ir(6, 0.12f);
                break;
            case 6:
                Soltar();
                Ir(7, 0.12f);
                break;
            case 7:
            {
                var elegido = EventSystem.current?.currentSelectedGameObject;
                var bb = elegido != null ? elegido.GetComponent<BotonBrenn>() : null;
                Comprobar(bb != null && bb.Elegido, $"teclado: la primera flecha elige un botón ({(elegido != null ? elegido.name : "nada")})");
                SessionState.SetString(P + "elegido", elegido != null ? elegido.name : "");
                if (p.nombre == "Menu") { Ir(20, 0.1f); break; }   // y el hover con el ratón
                Ir(8, 0.1f);
                break;
            }
            case 8:
            {
                bool dorado = EventSystem.current?.currentSelectedGameObject?.GetComponent<BotonBrenn>() is BotonBrenn b && b.Texto != null
                              && ColorUtility.ToHtmlStringRGB(b.Texto.color) == "FFD166";
                bool sinTexto = EventSystem.current?.currentSelectedGameObject?.GetComponent<BotonBrenn>()?.Texto == null;
                Comprobar(dorado || sinTexto, "el elegido con el teclado se ve dorado");
                Siguiente();
                break;
            }
            case 20:   // menú: hover con el ratón sobre "Instrucciones"
                EventSystem.current.SetSelectedGameObject(null);
                Raton(EnPantalla("Canvas/PanelMenu/BotonInstrucciones"), false);
                Ir(21, 0.2f);
                break;
            case 21:
            {
                var b = Buscar("Canvas/PanelMenu/BotonInstrucciones").GetComponent<BotonBrenn>();
                if (!EsperarA(() => b.Elegido && b.TijeraVisible, 3, "la tijera de \"Instrucciones\"")) return;
                var tij = (RectTransform)b.transform.Find("Tijera");
                Comprobar(b.Borde.sprite.name == "boton_normal_hover" && ColorUtility.ToHtmlStringRGB(b.Texto.color) == "FFD166"
                          && Mathf.Approximately(tij.rect.width, BotonBrenn.LadoTijera),
                          $"hover en \"Instrucciones\": borde y texto dorados y la tijera de {BotonBrenn.LadoTijera} parpadeando");
                Capturar("Menu_hover_instrucciones.png");
                Ir(22, 0.1f);
                break;
            }
            case 22:
                Raton(new Vector2(120, 120), false);
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

    // como al terminar el nivel: el evento del GameManager abre el panel final
    static void NivelCompletado(int estrellas, float satisfaccion)
    {
        var gm = GameManager.Instance;
        var campo = typeof(GameManager).GetField("NivelCompletado", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static
                                                                     | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        (campo.GetValue(campo.IsStatic ? null : gm) as Delegate)?.DynamicInvoke(estrellas, satisfaccion);
    }

    static void Siguiente()
    {
        Pantalla++;
        if (Pantalla < Pantallas.Length) { Ir(1, 0.1f); return; }
        Soltar();
        Time.timeScale = 1f;
        EditorApplication.isPlaying = false;
        Ir(90, 1f);
    }

    // todos los botones visibles: con BotonBrenn, sin sprites viejos, el texto adentro y el clic les llega
    static void RevisarBotones(string pantalla)
    {
        var es = EventSystem.current;
        var botones = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Where(b => b.isActiveAndEnabled).ToList();
        int visibles = 0;
        var problemas = new List<string>();
        foreach (var b in botones)
        {
            var bb = b.GetComponent<BotonBrenn>();
            var img = b.GetComponent<Image>();
            if (bb == null) { problemas.Add($"{b.name} sin BotonBrenn (estilo viejo)"); continue; }
            if (bb.Borde == null || !AssetDatabase.GetAssetPath(bb.Borde.sprite).StartsWith(ConstruirMenuPrincipal.CarpetaLetreros))
                problemas.Add($"{b.name}: el borde no es del estilo nuevo ({bb.Borde?.sprite?.name})");
            if (img != null && img.sprite != null && !AssetDatabase.GetAssetPath(img.sprite).StartsWith(ConstruirMenuPrincipal.CarpetaLetreros) && bb.VarianteDelBoton != BotonBrenn.Variante.Tarjeta)
                problemas.Add($"{b.name}: sprite viejo {img.sprite.name}");
            if (bb.Texto != null)
            {
                bb.Texto.ForceMeshUpdate();
                if (!FuenteProyecto.Cabe(bb.Texto) || bb.Texto.isTextOverflowing) problemas.Add($"{b.name}: \"{bb.Texto.text}\" no cabe");
            }
            if (!b.IsInteractable()) continue;
            var golpes = new List<RaycastResult>();
            var centro = RectTransformUtility.WorldToScreenPoint(null, b.transform.position);
            es.RaycastAll(new PointerEventData(es) { position = centro }, golpes);
            bool llega = golpes.Count > 0 && golpes[0].gameObject.transform.IsChildOf(b.transform);
            bool tapadoPorPanel = golpes.Count > 0 && !llega && golpes[0].gameObject.GetComponentInParent<Button>() == null;
            if (llega) visibles++;
            else if (!tapadoPorPanel && golpes.Count > 0) problemas.Add($"{b.name}: lo tapa {golpes[0].gameObject.name}");
        }
        Comprobar(problemas.Count == 0 && visibles > 0,
                  $"{pantalla}: {visibles} botones visibles reciben el clic, {botones.Count} con BotonBrenn y el texto adentro" +
                  (problemas.Count > 0 ? " — " + string.Join("; ", problemas) : ""));
        var nombres = botones.Where(b => b.GetComponent<BotonBrenn>() != null)
                             .Select(b => $"{b.name} {b.GetComponent<BotonBrenn>().VarianteDelBoton} {((RectTransform)b.transform).rect.width:0}x{((RectTransform)b.transform).rect.height:0}");
        Log("    " + string.Join(", ", nombres));
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

    static Vector2 EnPantalla(string ruta) => RectTransformUtility.WorldToScreenPoint(null, Buscar(ruta).position);

    // ------------------------------------------------------------------ ayudantes

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

    // como PruebaPresentacion: el Canvas pasa un momento a ScreenSpaceCamera para leerlo a 1920x1080
    static void Capturar(string archivo)
    {
        var ruta = Path.Combine(SessionState.GetString(P + "carpeta", "Temp"), archivo);
        var cam = Camera.main;
        var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.gameObject.scene == SceneManager.GetActiveScene())
            .OrderByDescending(c => c.name == "Canvas").First();
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
