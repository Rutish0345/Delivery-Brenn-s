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
using Object = UnityEngine.Object;

// Prueba en Play del botón "Menú" de la pausa, desde MenuPrincipal, en los 3 niveles (el 1 por Iniciar → Intro; el 2
// y el 3 se cargan desde el menú): pausa → Menú → No (vuelve) → Menú → Esc (= No) → Esc (cierra la pausa) → pausa →
// Menú → Sí: el reloj se detiene, timeScale 1, lluvia y motor callados, llega al menú con su música y sin sumar el
// dinero del nivel. Al final entra otra vez al nivel 1 y revisa reloj, HUD y música. Captura la pausa y la pregunta.
// El avance guardado queda como estaba. Temp/PruebaPausaMenu.pedido → Temp/PruebaPausaMenu.resultado
[InitializeOnLoad]
public static class PruebaPausaMenu
{
    const string Pedido = "Temp/PruebaPausaMenu.pedido";
    const string Resultado = "Temp/PruebaPausaMenu.resultado";
    const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";
    const string P = "PruebaPausaMenu.";
    const string Carpeta = @"C:\Users\kangu\AppData\Local\Temp\claude\C--Users-kangu-DeliveryBrenns\5e10bfe6-cf05-4391-9b55-19df2ab5b693\scratchpad\pausa_menu";
    static readonly string[] Niveles = { "SampleScene", "Nivel2_Comercial", "Nivel3_Lluvia" };

    static bool enganchado;

    static PruebaPausaMenu()
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
    static void Siguiente(float espera) { Paso++; Esperar(espera); }
    static string Escena => SceneManager.GetActiveScene().name;
    static GameManager GM => GameManager.Instance;
    static AudioManager AM => AudioManager.Instance;
    static MenuJuego Menu => Object.FindFirstObjectByType<MenuJuego>();
    static bool PausaAbierta => Buscar("Canvas/PanelPausa") != null && Buscar("Canvas/PanelPausa").gameObject.activeSelf;
    static bool Pregunta => Buscar("Canvas/PanelPausa/PausaConfirmar") != null && Buscar("Canvas/PanelPausa/PausaConfirmar").gameObject.activeSelf;
    static string N => $"[{Niveles[Nivel]}]";

    static void Revisar()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (Paso == 0)
        {
            if (!File.Exists(Pedido) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Pedido);
            Directory.CreateDirectory(Carpeta);
            SessionState.SetString(P + "log", "");
            SessionState.SetString(P + "consola", "");
            SessionState.SetInt(P + "fallos", 0);
            SessionState.SetFloat(P + "desde", 0);
            Nivel = 0;
            ProgresoPrueba.Preparar(motos: 0b1111);
            PlayerPrefs.Save();
            var inicio = EditorSceneManager.playModeStartScene;
            SessionState.SetString(P + "inicio", inicio != null ? AssetDatabase.GetAssetPath(inicio) : "");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaMenu);
            var errores = RevisarCanvas.RevisarEscenasDeJuego();
            foreach (var e in errores) Fallo(e);
            if (errores.Count == 0) Log("  ok: RevisarCanvas: PausaMenu y PausaConfirmar (Sí/No) en las 3 escenas");
            Log("Entrando a Play desde MenuPrincipal.");
            PruebaIntro.EntradaSinFoco();   // el Esc inyectado llega aunque la Game view no tenga el foco
            Paso = 1;
            Esperar(2f);
            EditorApplication.isPlaying = true;
            return;
        }
        if (Esperando) return;
        try { Avanzar(); }
        catch (Exception e) { Fallo($"excepción en el paso {Paso}: {e}"); Time.timeScale = 1f; Paso = 90; PruebaIntro.RestaurarEntrada(); EditorApplication.isPlaying = false; }
    }

    static void Avanzar()
    {
        if (Paso < 90 && !EditorApplication.isPlaying) { Fallo($"Play se cerró en el paso {Paso}"); Paso = 90; return; }
        switch (Paso)
        {
            case 1:
                Application.runInBackground = true;
                if (!EsperarA(() => Escena == "MenuPrincipal" && Time.timeSinceLevelLoad > 0.5f && Buscar("Canvas/BotonIniciar") != null, 15, "el menú")) return;
                SessionState.SetInt(P + "dinero", Progreso.Dinero);
                Log($"Menú (cartera ${Progreso.Dinero}) → Iniciar → Intro → Saltar");
                Progreso.OlvidarPersonaje();   // que vuelva a pasar por Seleccion e Intro
                Clic("Canvas/BotonIniciar");
                Siguiente(0.5f);
                break;
            case 2:
                if (!EsperarA(() => SeleccionPrueba.Pasar() && Escena == "Intro" && Time.timeSinceLevelLoad > 0.5f, 10, "la Intro")) return;
                Clic("Canvas/BotonSaltar");
                Paso = 10;
                Esperar(0.5f);
                break;

            // ---------------------------------------------------------------- en cada nivel
            case 10:
            {
                // con ELIGE TU MOTO el tiempo está congelado (timeScale 0): basta con que la escena esté cargada
                if (!EsperarA(() => Escena == Niveles[Nivel] && GM != null && (GM.EnEspera || Time.timeSinceLevelLoad > 0.5f), 20, Niveles[Nivel])) return;
                var tienda = Object.FindFirstObjectByType<TiendaMotos>();
                if (GM.EnEspera && tienda != null && tienda.Abierta)   // ELIGE TU MOTO
                {
                    Progreso.Equipar(1);
                    var ventana = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "TiendaMotos_Fondo");
                    Clic(RutaDe(ventana.Find("Ventana/BotonEmpezar")));
                    Esperar(0.5f);
                    return;
                }
                if (!EsperarA(() => !GM.EnEspera && GM.PuntoActual != null && Time.timeScale == 1f && Time.timeSinceLevelLoad > 1.2f, 15, "que empiece el nivel")) return;
                Log($"{N} nivel en marcha: se entrega un pedido (sin terminar el nivel) y se abre la pausa");
                GM.Entregar(GM.PuntoActual);
                SessionState.SetInt(P + "dineroNivel", GM.Dinero);
                Clic("Canvas/BotonPausa");
                Siguiente(0.3f);
                break;
            }
            case 11:
            {
                Comprobar(PausaAbierta && Time.timeScale == 0f && !Pregunta, $"{N} pausa abierta, sin la pregunta");
                var nombres = Enumerable.Range(0, Buscar("Canvas/PanelPausa").childCount).Select(i => Buscar("Canvas/PanelPausa").GetChild(i))
                              .Where(t => t.gameObject.activeSelf).Select(t => t.name).ToList();
                Comprobar(nombres.SequenceEqual(new[] { "PausaTitulo", "PausaSeguir", "PausaReiniciar", "PausaMenu", "PausaSonido" })
                          && Texto("Canvas/PanelPausa/PausaMenu/Texto") == "Menú",
                          $"{N} orden: {string.Join(", ", nombres)}");
                if (Nivel == 0) Capturar("Pausa_con_menu.png");
                Siguiente(0.3f);
                break;
            }
            case 12:
                Clic("Canvas/PanelPausa/PausaMenu");
                Comprobar(Pregunta && PausaAbierta && Time.timeScale == 0f, $"{N} \"Menú\" abre \"¿Salir al menú?\"");
                Siguiente(0.3f);
                break;
            case 13:
                Comprobar(Texto("Canvas/PanelPausa/PausaConfirmar/PausaConfirmarTitulo") == "¿Salir al menú?"
                          && Texto("Canvas/PanelPausa/PausaConfirmar/PausaConfirmarTexto") == "Perderás el avance de este nivel", $"{N} textos de la pregunta");
                if (Nivel == 0) Capturar("Confirmar_salir.png");
                Siguiente(0.3f);
                break;
            case 14:
                Clic("Canvas/PanelPausa/PausaConfirmar/PausaConfirmarNo");
                Comprobar(!Pregunta && PausaAbierta && Time.timeScale == 0f && Escena == Niveles[Nivel], $"{N} \"No\" vuelve a la pausa");
                Clic("Canvas/PanelPausa/PausaMenu");
                Comprobar(Pregunta, $"{N} \"Menú\" otra vez");
                Tecla(true);
                Siguiente(0.05f);
                break;
            case 15:
                if (!Cuadros()) return;
                Tecla(false);
                Siguiente(0.05f);
                break;
            case 16:
                if (!Cuadros()) return;
                Comprobar(!Pregunta && PausaAbierta && Time.timeScale == 0f, $"{N} Esc con la pregunta abierta = \"No\" (la pausa sigue)");
                Tecla(true);
                Siguiente(0.05f);
                break;
            case 17:
                if (!Cuadros()) return;
                Tecla(false);
                Siguiente(0.05f);
                break;
            case 18:
                if (!Cuadros()) return;
                Comprobar(!PausaAbierta && Time.timeScale == 1f, $"{N} Esc otra vez cierra la pausa");
                Clic("Canvas/BotonPausa");
                Siguiente(0.3f);
                break;
            case 19:
                Clic("Canvas/PanelPausa/PausaMenu");
                Siguiente(0.3f);
                break;
            case 20:
            {
                if (Nivel == 2) Comprobar(AM.FuenteLluvia.isPlaying, $"{N} antes de salir llueve");
                bool enMoto = Object.FindFirstObjectByType<PlayerController>().EnMoto;
                Clic("Canvas/PanelPausa/PausaConfirmar/PausaConfirmarSi");
                Comprobar(GM.EnEspera && Time.timeScale == 1f && !AM.FuenteLluvia.isPlaying && !AM.FuenteMotor.isPlaying,
                          $"{N} \"Sí\": reloj detenido, timeScale 1, sin lluvia ni motor (iba {(enMoto ? "en moto" : "a pie")})");
                Siguiente(0.3f);
                break;
            }
            case 21:
            {
                if (!EsperarA(() => Escena == "MenuPrincipal" && Time.timeSinceLevelLoad > 1.0f, 10, "el menú principal")) return;
                Comprobar(AM.MusicaActual == "musica_menu" && AM.FuenteMusica.isPlaying && Mathf.Abs(AM.FuenteMusica.volume - AudioManager.VolumenMusica * (AudioManager.MusicaActiva ? 1 : 0)) < 0.01f,
                          $"{N} → menú principal con su música ({AM.MusicaActual})");
                Comprobar(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(s => s.isPlaying && s.loop) == 1,
                          $"{N} solo suena el loop de la música (sin lluvia ni motor)");
                Comprobar(Progreso.Dinero == SessionState.GetInt(P + "dinero", 0) && Time.timeScale == 1f && GameManager.Instance == null,
                          $"{N} la cartera no cambia (${Progreso.Dinero}; el nivel llevaba ${SessionState.GetInt(P + "dineroNivel", 0)})");
                Nivel++;
                if (Nivel < Niveles.Length)
                {
                    Log($"Desde el menú se carga {Niveles[Nivel]}");
                    SceneManager.LoadScene(Niveles[Nivel]);
                    Paso = 10;
                }
                else
                {
                    Log("De nuevo: Iniciar → Intro → Saltar → nivel 1");
                    Progreso.OlvidarPersonaje();   // que vuelva a pasar por Seleccion e Intro
                Clic("Canvas/BotonIniciar");
                    Paso = 30;
                }
                Esperar(0.5f);
                break;
            }

            // ---------------------------------------------------------------- volver a jugar
            case 30:
                if (!EsperarA(() => SeleccionPrueba.Pasar() && Escena == "Intro" && Time.timeSinceLevelLoad > 0.5f, 10, "la Intro")) return;
                Clic("Canvas/BotonSaltar");
                Siguiente(0.5f);
                break;
            case 31:
            {
                if (!EsperarA(() => NivelPrueba.Pasar() && Escena == "SampleScene" && GM != null && GM.PuntoActual != null && Time.timeSinceLevelLoad > 1.5f, 15, "el nivel 1 otra vez")) return;
                var reloj = Object.FindFirstObjectByType<RelojPedido>();
                Comprobar(reloj.EstadoActual == RelojPedido.Estado.Verde && reloj.GetComponent<CanvasGroup>().alpha == 1f && GM.TiempoPedido > 0.5f,
                          $"reloj en verde y corriendo (\"{reloj.Texto}\", {GM.TiempoPedido:0.0} s)");
                Comprobar(Texto("Canvas/CajaHUD/PlacaPedidos/TextoPedidos") == "Pedidos 0/3" && Texto("Canvas/PlacaDinero/TextoDinero") == "$0" && !PausaAbierta && Time.timeScale == 1f,
                          $"HUD desde cero (\"{Texto("Canvas/CajaHUD/PlacaPedidos/TextoPedidos")}\", \"{Texto("Canvas/PlacaDinero/TextoDinero")}\")");
                Comprobar(AM.MusicaActual == "musica_nivel1_residencial" && AM.FuenteMusica.isPlaying && !AM.FuenteLluvia.isPlaying && !AM.FuenteMotor.isPlaying,
                          $"audio del nivel 1 ({AM.MusicaActual}, sin lluvia ni motor)");
                SessionState.SetFloat(P + "t", GM.TiempoPedido);
                Siguiente(1f);
                break;
            }
            case 32:
                Comprobar(GM.TiempoPedido > SessionState.GetFloat(P + "t", 0) + 0.5f, "el reloj sigue avanzando");
                Paso = 90;
                EditorApplication.isPlaying = false;
                Esperar(1f);
                break;

            case 90:
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                PruebaIntro.RestaurarEntrada();
                ProgresoPrueba.Limpiar();   // el progreso vive solo en la sesión: no hay nada que restaurar
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

    // ------------------------------------------------------------------ ayudantes

    // Esc de verdad, por el Input System (MenuJuego lo lee con wasPressedThisFrame). Se mantiene al menos 2 cuadros del
    // juego antes de soltarla: si bajar y soltar caen en el mismo cuadro, wasPressedThisFrame no la ve
    static void Tecla(bool abajo)
    {
        if (abajo) PruebaIntro.EnfocarGameView();
        InputSystem.QueueStateEvent(Keyboard.current, abajo ? new KeyboardState(Key.Escape) : new KeyboardState());
        SessionState.SetInt(P + "cuadro", Time.frameCount);
    }

    static bool Cuadros()
    {
        if (Time.frameCount >= SessionState.GetInt(P + "cuadro", 0) + 2) return true;
        Esperar(0.02f);
        return false;
    }

    static string Texto(string ruta) => Buscar(ruta)?.GetComponent<TMP_Text>()?.text ?? "";

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

    static string RutaDe(Transform t) => t.parent == null ? t.name : RutaDe(t.parent) + "/" + t.name;

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
    }

    static void Capturar(string archivo)
    {
        var cam = Camera.main;
        var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.isRootCanvas && c.name == "Canvas");
        var rt = new RenderTexture(1920, 1080, 24);
        var modo = canvas.renderMode;
        int orden = canvas.sortingOrder;
        try
        {
            cam.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.sortingOrder = short.MaxValue;
            canvas.planeDistance = cam.nearClipPlane + 1;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Carpeta, archivo), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        finally
        {
            RenderTexture.active = null;
            cam.targetTexture = null;
            canvas.renderMode = modo;
            canvas.sortingOrder = orden;
            rt.Release();
        }
        Log($"  captura: {archivo}");
    }
}
