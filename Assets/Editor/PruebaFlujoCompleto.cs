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

// Prueba en Play del flujo completo, dos veces en la misma sesión, desde MenuPrincipal (como el build):
// A) Menú → "Iniciar juego" → Seleccion (CONFIRMAR) → Intro de 6 viñetas (botón Omitir) → SeleccionNivel → nivel 1.
// B) Se simula haber jugado (dinero, motos, estrellas de los 3 niveles y el final de 3 estrellas) y se fuerza el Final →
//    Creditos hasta el final → Espacio → menú: sin la presentación "El mirador" y con el progreso en 0.
// C) "Iniciar juego" otra vez: Seleccion e Intro vuelven a salir (la Intro se omite con Esc) y en SeleccionNivel solo
//    está abierto el nivel 1, con $0.
// D) GAME OVER en el nivel 1: "Reintentar" repite el nivel sin pasar por la Intro ni borrar el progreso; "Menú" vuelve
//    al menú (sin presentación) con el progreso en 0.
// Capturas 1920x1080 de cada paso (01_Menu ... 15_Menu_tras_game_over).
// Temp/PruebaFlujoCompleto.pedido (dentro: carpeta para las capturas) → Temp/PruebaFlujoCompleto.resultado
[InitializeOnLoad]
public static class PruebaFlujoCompleto
{
    const string Pedido = "Temp/PruebaFlujoCompleto.pedido";
    const string Resultado = "Temp/PruebaFlujoCompleto.resultado";
    const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";
    const string P = "PruebaFlujoCompleto.";

    static bool enganchado;

    static PruebaFlujoCompleto()
    {
        if (enganchado) return;
        enganchado = true;
        EditorApplication.update += Revisar;
        Application.logMessageReceived += Anotar;
    }

    static int Paso { get => SessionState.GetInt(P + "paso", 0); set => SessionState.SetInt(P + "paso", value); }
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
    static MenuPrincipal Menu => Object.FindFirstObjectByType<MenuPrincipal>();
    static IntroHistoria Intro => Object.FindFirstObjectByType<IntroHistoria>();
    static Creditos Cred => Object.FindFirstObjectByType<Creditos>();
    static GameManager GM => GameManager.Instance;

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
            SessionState.SetString(P + "clic", "");
            ProgresoPrueba.Limpiar();
            var inicio = EditorSceneManager.playModeStartScene;
            SessionState.SetString(P + "inicio", inicio != null ? AssetDatabase.GetAssetPath(inicio) : "");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaMenu);
            Log("Entrando a Play desde MenuPrincipal.");
            Paso = 1;
            Esperar(2f);
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
        // el clic que quedó pendiente de una captura: un cuadro después, con el Canvas ya de vuelta en su lugar
        string pendiente = SessionState.GetString(P + "clic", "");
        if (pendiente.Length > 0)
        {
            SessionState.SetString(P + "clic", "");
            Clic(pendiente);
            Esperar(0.5f);
            return;
        }
        switch (Paso)
        {
            // ---------------------------------------------------------------- A) primera vuelta
            case 1:
                Application.runInBackground = true;
                if (!EsperarA(() => Escena == "MenuPrincipal" && Menu != null && Time.timeSinceLevelLoad > 0.5f, 15, "el menú")) return;
                Log("A) Primera vuelta: Menú → Seleccion → Intro → SeleccionNivel → nivel 1");
                Comprobar(Menu.EnPresentacion, "al abrir el juego sale la presentación \"El mirador\"");
                if (!EsperarA(() => MenuPrueba.Listo(), 5, "el PanelMenu")) return;
                Siguiente(0.6f);
                break;
            case 2:
                Capturar("01_Menu.png");
                Buscar("Canvas/PanelMenu/CampoNombre").GetComponent<TMP_InputField>().text = "Flujo";
                Siguiente(0.1f);
                break;
            case 3:
                Clic("Canvas/PanelMenu/BotonIniciar");
                Siguiente(0.5f);
                break;
            case 4:
                if (!EsperarA(() => Escena == "Seleccion" && Time.timeSinceLevelLoad > 0.6f, 10, "la Seleccion")) return;
                Comprobar(true, "Iniciar juego → Seleccion");
                Capturar("02_Seleccion.png");
                Siguiente(0.2f);
                break;
            case 5:
                Clic("Canvas/Confirmar");
                Siguiente(0.5f);
                break;
            case 6:
                if (!EsperarA(() => Escena == "Intro" && Intro != null && Time.timeSinceLevelLoad > 1.5f, 10, "la Intro")) return;
                Comprobar(new SerializedObject(Intro).FindProperty("vinetas").arraySize == 6, "CONFIRMAR → Intro de 6 viñetas");
                Capturar("03_Intro.png");
                Siguiente(0.2f);
                break;
            case 7:
                Clic("Canvas/BotonSaltar");   // el botón Omitir
                Siguiente(0.5f);
                break;
            case 8:
                if (!EsperarA(() => Escena == "SeleccionNivel" && Time.timeSinceLevelLoad > 0.6f, 15, "SeleccionNivel")) return;
                Comprobar(true, "Omitir → SeleccionNivel");
                Capturar("04_SeleccionNivel.png");
                Siguiente(0.2f);
                break;
            case 9:
                Clic("Canvas/Jugar");
                Siguiente(0.5f);
                break;
            case 10:
                if (!EsperarA(() => Escena == "SampleScene" && GM != null && GM.PuntoActual != null && Time.timeSinceLevelLoad > 0.6f, 15, "el nivel 1")) return;
                Comprobar(true, "Jugar → nivel 1");
                Capturar("05_Nivel1.png");
                // se simula haber jugado los 3 niveles y llegado al final de 3 estrellas
                Progreso.Agregar(900);
                Progreso.DarMotos(0b0011);
                Progreso.Equipar(1);
                foreach (var n in Progreso.Niveles) Progreso.CompletarNivel(n.escena, 3, 90f);
                Progreso.GuardarNivel3(3, false);
                Comprobar(Progreso.Dinero == 900 && Progreso.NivelesCompletados == 3 && Progreso.HayResultadoNivel3, "progreso simulado: $900, 2 motos, 3 niveles con 3 estrellas");
                Log("B) Final → Creditos → menú");
                SceneManager.LoadScene("Final");
                Siguiente(0.5f);
                break;

            // ---------------------------------------------------------------- B) final y créditos
            case 11:
                if (!EsperarA(() => Escena == "Final" && Object.FindFirstObjectByType<FinalHistoria>()?.Activo != null && Time.timeSinceLevelLoad > 1.5f, 10, "el Final")) return;
                var activo = Object.FindFirstObjectByType<FinalHistoria>().Activo;
                Comprobar(activo.EscenaSiguiente == ConstruirCreditos.Escena, $"final {activo.name} → {activo.EscenaSiguiente}");
                Capturar("06_Final.png");
                SceneManager.LoadScene(activo.EscenaSiguiente);   // como al terminar sus viñetas
                Siguiente(0.5f);
                break;
            case 12:
                if (!EsperarA(() => Escena == ConstruirCreditos.Escena && Cred != null && Time.timeSinceLevelLoad > 1f, 10, "los créditos")) return;
                Cred.Avanzar(100000f);
                Siguiente(0.8f);
                break;
            case 13:
                Comprobar(Cred.Terminado && Buscar("Canvas/Indicador").gameObject.activeInHierarchy, "créditos hasta el final, con el aviso");
                Capturar("07_Creditos_final.png");
                Tecla(Key.Space);
                Siguiente(0f);
                break;
            case 14:
                if (!Cuadros()) return;
                Soltar();
                Siguiente(0.3f);
                break;
            case 15:
                if (!EsperarA(() => Escena == "MenuPrincipal" && Menu != null && Time.timeSinceLevelLoad > 0.3f, 10, "el menú después de los créditos")) return;
                Comprobar(!Menu.EnPresentacion && Menu.MenuVisible, "Espacio vuelve al menú, sin repetir la presentación");
                ProgresoEnCero("después de los créditos");
                Capturar("08_Menu_tras_creditos.png");
                ClicLuego("Canvas/PanelMenu/BotonIniciar");
                Siguiente(0.2f);
                break;

            // ---------------------------------------------------------------- C) segunda vuelta
            case 16:
                if (!EsperarA(() => Escena == "Seleccion" && Time.timeSinceLevelLoad > 0.6f, 10, "la Seleccion otra vez")) return;
                Log("C) Segunda vuelta");
                Comprobar(true, "Iniciar juego después de terminar el juego → Seleccion otra vez");
                Capturar("09_Seleccion_otra_vez.png");
                ClicLuego("Canvas/Confirmar");
                Siguiente(0.2f);
                break;
            case 17:
                if (!EsperarA(() => Escena == "Intro" && Intro != null && Time.timeSinceLevelLoad > 1.5f, 10, "la Intro otra vez")) return;
                Comprobar(true, "→ Intro otra vez");
                Capturar("10_Intro_otra_vez.png");
                Tecla(Key.Escape);
                Siguiente(0f);
                break;
            case 18:
                if (!Cuadros()) return;
                Soltar();
                Siguiente(0.5f);
                break;
            case 19:
                if (!EsperarA(() => Escena == "SeleccionNivel" && Time.timeSinceLevelLoad > 0.6f, 15, "SeleccionNivel (Esc en la Intro)")) return;
                Comprobar(true, "Esc omite la Intro → SeleccionNivel");
                Comprobar(Progreso.Dinero == 0 && Progreso.Desbloqueado(0) && !Progreso.Desbloqueado(1) && !Progreso.Desbloqueado(2),
                          $"SeleccionNivel con $0 y solo el nivel 1 abierto (dinero {Progreso.Dinero})");
                Capturar("11_SeleccionNivel_otra_vez.png");
                ClicLuego("Canvas/Jugar");
                Siguiente(0.2f);
                break;

            // ---------------------------------------------------------------- D) GAME OVER
            case 20:
                if (!EsperarA(() => Escena == "SampleScene" && GM != null && GM.PuntoActual != null && Time.timeSinceLevelLoad > 0.6f, 15, "el nivel 1")) return;
                Log("D) GAME OVER: Reintentar y Menú");
                Progreso.Agregar(300);   // lo ganado en un nivel anterior
                Despedir();
                Siguiente(0.8f);
                break;
            case 21:
                if (!EsperarA(() => Buscar("Canvas/" + ConstruirQuejas.NombrePanel)?.gameObject.activeInHierarchy == true, 5, "la pantalla de despido")) return;
                Capturar("12_GameOver.png");
                Siguiente(0.2f);
                break;
            case 22:
                Clic($"Canvas/{ConstruirQuejas.NombrePanel}/{ConstruirQuejas.BotonReintentar}");
                Siguiente(0.5f);
                break;
            case 23:
                if (!EsperarA(() => Escena == "SampleScene" && GM != null && !GM.FueDespedido && GM.PuntoActual != null && Time.timeSinceLevelLoad > 0.6f, 15, "el nivel 1 otra vez")) return;
                Comprobar(Progreso.Dinero == 300, $"Reintentar repite el nivel 1 sin Intro y sin borrar el progreso (${Progreso.Dinero})");
                Capturar("13_Reintentar.png");
                Despedir();
                Siguiente(0.8f);
                break;
            case 24:
                if (!EsperarA(() => Buscar("Canvas/" + ConstruirQuejas.NombrePanel)?.gameObject.activeInHierarchy == true, 5, "la pantalla de despido")) return;
                Capturar("14_GameOver_otra_vez.png");
                ClicLuego($"Canvas/{ConstruirQuejas.NombrePanel}/{ConstruirQuejas.BotonMenu}");
                Siguiente(0.2f);
                break;
            case 25:
                if (!EsperarA(() => Escena == "MenuPrincipal" && Menu != null && Time.timeSinceLevelLoad > 0.3f, 10, "el menú después del GAME OVER")) return;
                Comprobar(!Menu.EnPresentacion && Menu.MenuVisible, "Menú desde el GAME OVER: directo al PanelMenu, sin presentación");
                ProgresoEnCero("después del GAME OVER");
                Capturar("15_Menu_tras_game_over.png");
                Paso = 90;
                EditorApplication.isPlaying = false;
                Esperar(1f);
                break;

            case 90:
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                ProgresoPrueba.Limpiar();
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

    static void ClicLuego(string ruta) => SessionState.SetString(P + "clic", ruta);

    static void ProgresoEnCero(string cuando)
    {
        bool cero = Progreso.Dinero == 0 && Progreso.MotosCompradas == 0 && Progreso.MotoEquipada == -1 && Progreso.NivelesCompletados == 0
                    && Enumerable.Range(0, Progreso.Niveles.Length).All(i => Progreso.Estrellas(i) == 0) && !Progreso.HayResultadoNivel3
                    && !Progreso.Desbloqueado(1);
        Comprobar(cero, $"{cuando}: progreso en 0 (dinero {Progreso.Dinero}, motos {Progreso.MotosCompradas}, equipada {Progreso.MotoEquipada}, " +
                        $"niveles {Progreso.NivelesCompletados}, final {Progreso.EstrellasNivel3})");
    }

    // GAME OVER al momento (como la última queja): GameManager.Despedir avisa al LevelManager
    static void Despedir() =>
        typeof(GameManager).GetMethod("Despedir", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(GM, null);

    // ------------------------------------------------------------------ entrada y ayudantes

    static void Tecla(Key k)
    {
        PruebaIntro.EnfocarGameView();
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k));
        SessionState.SetInt(P + "cuadro", Time.frameCount);
    }
    static void Soltar() { if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); }
    static bool Cuadros()
    {
        if (Time.frameCount >= SessionState.GetInt(P + "cuadro", 0) + 2) return true;
        Esperar(0.02f);
        return false;
    }

    static bool EsperarA(Func<bool> condicion, float limite, string que)
    {
        float ahora = (float)EditorApplication.timeSinceStartup;
        if (SessionState.GetFloat(P + "desde", 0) == 0) SessionState.SetFloat(P + "desde", ahora);
        bool listo = false;
        try { listo = condicion(); } catch (NullReferenceException) { } catch (InvalidOperationException) { }
        if (!listo && ahora - SessionState.GetFloat(P + "desde", 0) < limite) { Esperar(0.05f); return false; }
        if (!listo) Fallo($"se acabó el tiempo esperando {que} (escena {Escena})");
        SessionState.SetFloat(P + "desde", 0);
        return true;
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

    static void Capturar(string archivo)
    {
        var ruta = Path.Combine(SessionState.GetString(P + "carpeta", "Temp"), archivo);
        var cam = Camera.main;
        var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.gameObject.scene == SceneManager.GetActiveScene())
            .OrderByDescending(c => c.name == "Canvas").FirstOrDefault();
        var rt = new RenderTexture(1920, 1080, 24);
        RenderMode modo = default;
        Camera camCanvas = null;
        int capa = 0, orden = 0;
        float distancia = 0;
        if (canvas != null)
        {
            modo = canvas.renderMode; camCanvas = canvas.worldCamera; capa = canvas.sortingLayerID; orden = canvas.sortingOrder; distancia = canvas.planeDistance;
        }
        try
        {
            cam.targetTexture = rt;
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.sortingLayerID = SortingLayer.layers.Last().id;
                canvas.sortingOrder = short.MaxValue;
                canvas.planeDistance = cam.nearClipPlane + 1;
            }
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
            if (canvas != null)
            {
                canvas.renderMode = modo;
                canvas.worldCamera = camCanvas;
                canvas.sortingLayerID = capa;
                canvas.sortingOrder = orden;
                canvas.planeDistance = distancia;
            }
            rt.Release();
        }
        Log($"  captura 1920x1080: {ruta}");
    }
}
