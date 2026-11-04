using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Prueba en Play de los turbos "Café exprés", desde MenuPrincipal (como el build). Los autos y peatones se apagan
// durante las medidas (salvo el peatón / auto del choque). Velocidad = desplazamiento real en ~1 s por un carril libre
// (sin colliders, turbos ni baches en el camino).
//  Nivel 1, a pie: 4 tazas; base 3.5; recoger → 5.6 por 6 s (ding, se apaga la taza, destello dorado en el sprite,
//  la cámara se aleja +0.4 en 0.3 s, líneas de velocidad, HUD con la barra, estela densa, pasos más rápidos);
//  la pausa congela el turbo y el reloj de reaparición; se acaba solo a los 6 s (whoosh, la cámara regresa); otro turbo
//  con el efecto activo reinicia la duración sin acumular; un peatón lo cancela; la taza vuelve a los 25 s (destello y
//  pop); reiniciar el nivel lo borra todo.
//  Nivel 2, Scooter: base 5.0; turbo +2.0 → 7.0 por 5 s; TiempoIdeal / TiempoMaximo no cambian; un bache le quita la
//  mitad de lo que queda; un auto lo cancela (sin whoosh).
//  Nivel 3 (noche, lluvia): ELIGE TU MOTO con las barras de velocidad y resistencia; Deportiva base 6.5, turbo → 8.5
//  (tope 9); tazas Unlit. Luego las 4 motos una por una: velocidad medida sin turbo (5.0 / 5.5 / 6.0 / 6.5, en el orden
//  de precio) y con turbo (+2.0: 7.0 / 7.5 / 8.0 / 8.5).
// Capturas: Turbo_mapa_nivel1, Turbo_recoger_a_pie, Turbo_a_pie (HUD, líneas y estela), Turbo_reaparece,
// Turbo_recoger_moto, Turbo_moto, Tienda_elige_tu_moto, Turbo_noche, Turbo_mapa_nivel3.
// El avance guardado queda como estaba. Temp/PruebaTurbos.pedido (dentro: carpeta) → Temp/PruebaTurbos.resultado
[InitializeOnLoad]
public static class PruebaTurbos
{
    const string Pedido = "Temp/PruebaTurbos.pedido";
    const string Resultado = "Temp/PruebaTurbos.resultado";
    const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";
    const string P = "PruebaTurbos.";
    static readonly string[] ClavesProgreso = { "genero_jugador" };
    const BindingFlags Privado = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly Vector2[] Cuatro = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };

    static bool enganchado;
    static PlayerController enganchadoA;

    static PruebaTurbos()
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
    static GameManager GM => GameManager.Instance;
    static PlayerController Jugador => Object.FindFirstObjectByType<PlayerController>();
    static TurboHUD HUD => Object.FindFirstObjectByType<TurboHUD>();
    static float AltoCaja => ((RectTransform)HUD.transform.parent).rect.height;
    static CameraFollow Camara => Object.FindFirstObjectByType<CameraFollow>();
    static EstelaTurbo Estela => Object.FindFirstObjectByType<EstelaTurbo>();
    static AudioManager AM => AudioManager.Instance;
    static int Veces(string n) => AM != null ? AM.VecesSonado(n) : -1;
    static Turbo[] Turbos => Object.FindObjectsByType<Turbo>(FindObjectsSortMode.None).OrderBy(t => t.name).ToArray();
    static Turbo T(int k) => Turbos[k];
    static float F(string k) => SessionState.GetFloat(P + k, 0);
    static void F(string k, float v) => SessionState.SetFloat(P + k, v);
    static int I(string k) => SessionState.GetInt(P + k, 0);
    static void I(string k, int v) => SessionState.SetInt(P + k, v);

    // eventos del turbo del repartidor actual: cuándo empezó / terminó y si fue solo
    static void Enganchar()
    {
        var pc = Jugador;
        if (pc == null || pc == enganchadoA) return;
        enganchadoA = pc;
        pc.TurboEmpezo += () => { F("tini", Time.time); I("inicios", I("inicios") + 1); };
        pc.TurboTermino += natural => { F("tfin", Time.time); I("natural", natural ? 1 : 0); I("fines", I("fines") + 1); };
    }

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
            I("fallos", 0);
            F("desde", 0);
            I("m", 0);
            foreach (var k in ClavesProgreso)
                SessionState.SetString(P + "pp." + k, PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k).ToString() : "");
            ProgresoPrueba.Preparar(motos: 0b1111);   // las 4 motos, solo durante la prueba
            PlayerPrefs.Save();
            var inicio = EditorSceneManager.playModeStartScene;
            SessionState.SetString(P + "inicio", inicio != null ? AssetDatabase.GetAssetPath(inicio) : "");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaMenu);
            var sinUI = RevisarCanvas.RevisarEscenasDeJuego();
            foreach (var falta in sinUI) Fallo(falta);
            if (sinUI.Count == 0) Log("  ok: Canvas completo (con PlacaTurbo) en las 3 escenas y el prefab de audio con los 3 sonidos");
            Log("Entrando a Play desde MenuPrincipal.");
            enganchadoA = null;
            Paso = 1;
            Esperar(2f);
            EditorApplication.isPlaying = true;
            return;
        }
        if (Esperando) return;
        try { Avanzar(); }
        catch (Exception e) { Fallo($"excepción en el paso {Paso}: {e}"); Time.timeScale = 1f; Paso = 90; EditorApplication.isPlaying = false; }
    }

    static void Avanzar()
    {
        if (Paso < 90 && !EditorApplication.isPlaying) { Fallo($"Play se cerró en el paso {Paso}"); Paso = 90; return; }
        if (EditorApplication.isPlaying) Enganchar();
        switch (Paso)
        {
            case 1:
                Application.runInBackground = true;
                if (!EsperarA(() => Escena == "MenuPrincipal" && Time.timeSinceLevelLoad > 0.3f && MenuPrueba.Listo(), 15, "el menú")) return;
                Clic("Canvas/PanelMenu/BotonIniciar");
                Siguiente(0.5f);
                break;
            case 2:
                if (!EsperarA(() => SeleccionPrueba.Pasar() && Escena == "Intro" && Time.timeSinceLevelLoad > 0.5f, 10, "la Intro")) return;
                Clic("Canvas/BotonSaltar");
                Siguiente(0.5f);
                break;

            // ================================================================ nivel 1, a pie
            case 3:
            {
                if (!EsperarA(() => NivelPrueba.Pasar() && Escena == "SampleScene" && GM != null && GM.PuntoActual != null && Time.timeSinceLevelLoad > 0.5f, 15, "el nivel 1")) return;
                Log("1) Nivel 1 (a pie)");
                Enganchar();
                var ts = Turbos;
                Comprobar(ts.Length == 4 && ts.All(t => t.Disponible && t.GetComponent<Collider2D>().enabled), $"{ts.Length} Café exprés listos en el mapa");
                Comprobar(HUD != null && !HUD.Visible && HUD.GetComponent<CanvasGroup>().alpha == 0f, "sin turbo, la placa del turbo no se ve");
                Comprobar(HUD != null && !HUD.EnLaCaja && HUD.transform.parent.name == "CajaHUD", "sin turbo, la placa no ocupa lugar en la caja del HUD");
                F("cajaSin", AltoCaja);
                Comprobar(!Jugador.EnMoto && Mathf.Approximately(Jugador.VelocidadMaxima, 3.5f), $"a pie, velocidad {Jugador.VelocidadMaxima}");
                Apagar<TrafficCar>(null);
                Apagar<Peaton>(null);
                Capturar("Turbo_mapa_nivel1.png", T(0).transform.position);
                Siguiente(0.1f);
                break;
            }
            case 4:
                if (!Medir(3.5f, "a pie sin turbo")) return;
                Siguiente(0.1f);
                break;
            case 5:
                AM.ReiniciarContador();
                Poner(T(0).transform.position);
                Siguiente(0.05f);
                break;
            case 6:
            {
                if (!EsperarA(() => T(0).Recogidos == 1, 5, "recoger el Turbo_1")) return;
                var pc = Jugador;
                var t = T(0);
                F("recogido1", Time.time);
                Comprobar(pc.TurboActivo && Mathf.Approximately(pc.TurboDuracion, 6f) && Mathf.Abs(pc.VelocidadMaxima - 5.6f) < 0.001f,
                          $"a pie: x1.6 por {pc.TurboDuracion} s → {pc.VelocidadMaxima:0.00} tiles/s");
                Comprobar(Estela.Destellando, "destello dorado sobre el sprite al recogerlo");
                Capturar("Turbo_recoger_a_pie.png", Jugador.transform.position);
                Comprobar(!t.Disponible && !t.GetComponent<Collider2D>().enabled && !t.transform.Find("Taza").GetComponent<SpriteRenderer>().enabled
                          && !t.transform.Find("Brillo").GetComponent<SpriteRenderer>().enabled, "la taza se apaga (sin trigger)");
                Comprobar(Mathf.Abs(t.FaltaParaVolver - 25f) < 0.2f, $"vuelve en {t.FaltaParaVolver:0.0} s");
                Comprobar(Veces("turbo_recoger") == 1, $"suena turbo_recoger ({Veces("turbo_recoger")})");
                Siguiente(0.25f);
                break;
            }
            case 7:
                Comprobar(HUD.Visible && HUD.Fraccion < 1f && HUD.Fraccion > 0.85f, $"la placa del turbo aparece, barra al {HUD.Fraccion:P0}");
                Comprobar(HUD.EnLaCaja && Mathf.Abs(AltoCaja - F("cajaSin") - 54f - 8f) < 0.5f, $"la caja del HUD crece con la placa ({F("cajaSin"):0} → {AltoCaja:0})");
                Comprobar(Mathf.Abs(Camara.ZoomActual - 0.4f) < 0.01f, $"la cámara se alejó +{Camara.ZoomActual:0.00} (orthographicSize {Camera.main.orthographicSize:0.00})");
                Comprobar(HUD.AlfaLineas > 0.5f, $"líneas de velocidad en la orilla (alfa {HUD.AlfaLineas:0.00})");
                Comprobar(Mathf.Abs(Jugador.GetComponent<Animator>().speed - 1.6f) < 0.01f, $"los pasos van x{Jugador.GetComponent<Animator>().speed:0.0}");
                Paso++;
                break;
            case 8:
                if (!Medir(5.6f, "a pie con turbo", () =>
                {
                    Comprobar(Estela.Activas > 0, $"estela detrás del personaje ({Estela.Activas} partículas)");
                    Capturar("Turbo_a_pie.png", Jugador.transform.position);
                })) return;
                Siguiente(0.1f);
                break;
            case 9:   // pausa
                F("r", Jugador.TurboRestante);
                F("falta", T(0).FaltaParaVolver);
                F("frac", HUD.Fraccion);
                Clic("Canvas/BotonPausa");
                Siguiente(1.5f);
                break;
            case 10:
                Comprobar(Time.timeScale == 0f && Mathf.Abs(Jugador.TurboRestante - F("r")) < 0.001f && Mathf.Abs(T(0).FaltaParaVolver - F("falta")) < 0.001f,
                          $"en pausa no corren el turbo ({F("r"):0.00} → {Jugador.TurboRestante:0.00} s) ni la reaparición ({F("falta"):0.0} → {T(0).FaltaParaVolver:0.0} s)");
                Comprobar(Mathf.Abs(HUD.Fraccion - F("frac")) < 0.001f, "la barra se queda quieta");
                Clic("Canvas/PanelPausa/PausaSeguir");
                Siguiente(0.2f);
                break;
            case 11:
            {
                if (!EsperarA(() => !Jugador.TurboActivo, 10, "que se acabe el turbo")) return;
                float dura = F("tfin") - F("tini");
                Comprobar(I("natural") == 1 && Mathf.Abs(dura - 6f) < 0.1f, $"se acaba solo a los {dura:0.00} s de juego (sin contar la pausa)");
                Comprobar(Veces("turbo_fin") == 1, $"suena turbo_fin ({Veces("turbo_fin")})");
                Comprobar(Mathf.Approximately(Jugador.VelocidadMaxima, 3.5f), $"vuelve a {Jugador.VelocidadMaxima} tiles/s");
                Siguiente(0.4f);
                break;
            }
            case 12:
                Comprobar(!HUD.Visible && HUD.GetComponent<CanvasGroup>().alpha == 0f && HUD.AlfaLineas == 0f, "la placa y las líneas se esconden");
                Comprobar(!HUD.EnLaCaja && Mathf.Abs(AltoCaja - F("cajaSin")) < 0.5f, $"la caja del HUD vuelve a encoger ({AltoCaja:0})");
                Comprobar(Mathf.Abs(Camara.ZoomActual) < 0.01f, $"la cámara regresa ({Camara.ZoomActual:+0.00;-0.00;0})");
                Log("2) Otro turbo con el efecto activo");
                Poner(T(1).transform.position);
                Siguiente(0.05f);
                break;
            case 13:
                if (!EsperarA(() => T(1).Recogidos == 1, 5, "recoger el Turbo_2")) return;
                Siguiente(2f);
                break;
            case 14:
                F("r", Jugador.TurboRestante);
                Poner(T(2).transform.position);
                Siguiente(0.05f);
                break;
            case 15:
            {
                if (!EsperarA(() => T(2).Recogidos == 1, 5, "recoger el Turbo_3")) return;
                var pc = Jugador;
                Comprobar(pc.TurboRestante > 5.85f && Mathf.Abs(pc.VelocidadMaxima - 5.6f) < 0.001f,
                          $"la duración se reinicia ({F("r"):0.00} → {pc.TurboRestante:0.00} s) y no se acumula ({pc.VelocidadMaxima:0.00}, no {3.5f * 1.6f * 1.6f:0.00})");
                Paso++;
                break;
            }
            case 16:   // choque con peatón a pie
            {
                Log("3) Choque con un peatón (a pie)");
                Peaton peaton = null;
                Vector2 inicio = default, dir = default;
                foreach (var p in Object.FindObjectsByType<Peaton>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                             .OrderBy(p => Vector2.Distance(p.transform.position, Jugador.transform.position)))
                {
                    p.gameObject.SetActive(true);
                    if (Acercar(p.GetComponent<Collider2D>(), out inicio, out dir)) { peaton = p; break; }
                    p.gameObject.SetActive(false);
                }
                if (peaton == null) { Fallo("no encontré un peatón con el camino libre"); Paso = 18; break; }
                peaton.enabled = false;
                typeof(Peaton).GetField("pausa", Privado).SetValue(peaton, 0f);
                I("fines", 0);
                F("sat", GM.SatisfaccionActual);
                Poner(inicio);
                Jugador.DireccionForzada = dir;
                Siguiente(0.02f);
                break;
            }
            case 17:
                if (!EsperarA(() => I("fines") > 0, 4, "el choque con el peatón")) return;
                Jugador.DireccionForzada = null;
                Comprobar(!Jugador.TurboActivo && I("natural") == 0, $"el choque cancela el turbo (quedaban {Jugador.TurboRestante:0.00} s)");
                Comprobar(GM.SatisfaccionActual < F("sat"), $"y cobra el choque como siempre (satisfacción {F("sat"):0} → {GM.SatisfaccionActual:0})");
                Comprobar(Mathf.Approximately(Jugador.VelocidadMaxima, 3.5f), "sin turbo, a 3.5");
                Apagar<Peaton>(null);
                Paso++;
                break;
            case 18:   // reaparición
            {
                Log("4) El Turbo_1 vuelve a los 25 s");
                var t = T(0);
                Poner(LibreCerca(t.transform.position));
                F("aparece", Veces("turbo_aparece"));
                Time.timeScale = 5f;
                Siguiente(0.1f);
                break;
            }
            case 19:
            {
                if (!EsperarA(() => T(0).Disponible, 30, "que vuelva el Turbo_1")) return;
                Time.timeScale = 1f;
                var t = T(0);
                float pasado = Time.time - F("recogido1");
                Comprobar(Mathf.Abs(pasado - 25f) < 0.5f, $"vuelve a los {pasado:0.0} s de juego");
                Comprobar(t.GetComponent<Collider2D>().enabled && t.transform.Find("Taza").GetComponent<SpriteRenderer>().enabled && t.Destellando,
                          "con trigger, taza y destello");
                Comprobar(Veces("turbo_aparece") > F("aparece"), "suena turbo_aparece (está en pantalla)");
                Capturar("Turbo_reaparece.png", t.transform.position);
                Siguiente(0.1f);
                break;
            }
            case 20:   // reiniciar el nivel con turbo activo
                Log("5) Reiniciar el nivel con el turbo activo");
                Poner(T(0).transform.position);
                Siguiente(0.05f);
                break;
            case 21:
                if (!EsperarA(() => Jugador.TurboActivo, 5, "el turbo antes de reiniciar")) return;
                I("pc", Jugador.GetInstanceID());
                Object.FindFirstObjectByType<LevelManager>().Reiniciar();
                Siguiente(0.5f);
                break;
            case 22:
            {
                if (!EsperarA(() => NivelPrueba.Pasar() && Escena == "SampleScene" && Jugador != null && Jugador.GetInstanceID() != I("pc") && Time.timeSinceLevelLoad > 0.5f
                                    && GM.PuntoActual != null, 15, "el nivel reiniciado")) return;
                Comprobar(!Jugador.TurboActivo && Mathf.Approximately(Jugador.VelocidadMaxima, 3.5f) && Turbos.All(t => t.Disponible) && !HUD.Visible,
                          "al reiniciar: sin turbo, las 4 tazas listas y la placa escondida");
                Comprobar(Time.timeScale == 1f, "el tiempo corre normal");
                Paso++;
                break;
            }
            case 23: case 24: case 25:
                GM.Entregar(GM.PuntoActual);
                Siguiente(Paso == 25 ? 2.5f : 0.4f);
                break;
            case 26:
                if (!EsperarA(() => Buscar("Canvas/PanelFinal") != null && Buscar("Canvas/PanelFinal").gameObject.activeInHierarchy, 10, "el panel final del nivel 1")) return;
                Clic("Canvas/PanelFinal/BotonSiguiente");
                Siguiente(0.5f);
                break;

            // ================================================================ nivel 2, Scooter
            case 27:
                if (!EsperarA(() => Escena == "Nivel2_Comercial" && Tienda() != null && Tienda().Abierta && Time.unscaledTime > 0f, 15, "ELIGE TU MOTO (nivel 2)")) return;
                Progreso.Equipar(0);   // Scooter
                Siguiente(0.5f);
                break;
            case 28:
                Clic(RutaDe(Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                .First(t => t.name == "TiendaMotos_Fondo").Find("Ventana/BotonEmpezar")));
                Siguiente(0.5f);
                break;
            case 29:
            {
                if (!EsperarA(() => !GM.EnEspera && GM.PuntoActual != null && Time.timeScale == 1f, 10, "que empiece el nivel 2")) return;
                Log("6) Nivel 2 (Scooter)");
                Enganchar();
                Comprobar(Turbos.Length == 6 && Jugador.EnMoto && Mathf.Approximately(Jugador.VelocidadMaxima, 5f), $"{Turbos.Length} tazas; en moto a {Jugador.VelocidadMaxima}");
                F("ideal", GM.TiempoIdeal);
                F("maximo", GM.TiempoMaximo);
                Apagar<TrafficCar>(null);
                Apagar<Peaton>(null);
                Paso++;
                break;
            }
            case 30:
                if (!Medir(5f, "Scooter sin turbo")) return;
                AM.ReiniciarContador();
                Poner(T(0).transform.position);
                Siguiente(0.05f);
                break;
            case 31:
            {
                if (!EsperarA(() => T(0).Recogidos == 1, 5, "recoger un turbo en moto")) return;
                var pc = Jugador;
                Comprobar(pc.TurboActivo && Mathf.Approximately(pc.TurboDuracion, 5f) && Mathf.Abs(pc.VelocidadMaxima - 7f) < 0.001f,
                          $"en moto: +2.0 por {pc.TurboDuracion} s → {pc.VelocidadMaxima:0.00} tiles/s");
                Comprobar(Estela.Destellando, "destello dorado sobre la moto al recogerlo");
                Capturar("Turbo_recoger_moto.png", Jugador.transform.position);
                Comprobar(GM.TiempoIdeal == F("ideal") && GM.TiempoMaximo == F("maximo"),
                          $"TiempoIdeal {GM.TiempoIdeal:0.0} s y TiempoMaximo {GM.TiempoMaximo:0.0} s no cambian con el turbo");
                Paso++;
                break;
            }
            case 32:
                if (!Medir(7f, "Scooter con turbo", () =>
                {
                    Comprobar(Estela.Activas > 0 && HUD.Visible && HUD.AlfaLineas > 0.5f && Mathf.Abs(Camara.ZoomActual - 0.4f) < 0.01f,
                              $"estela ({Estela.Activas}), placa, líneas y cámara alejada en moto");
                    Capturar("Turbo_moto.png", Jugador.transform.position);
                })) return;
                Paso++;
                break;
            case 33:
            {
                if (!EsperarA(() => !Jugador.TurboActivo, 8, "que se acabe el turbo en moto")) return;
                float dura = F("tfin") - F("tini");
                Comprobar(I("natural") == 1 && Mathf.Abs(dura - 5f) < 0.1f, $"en moto dura {dura:0.00} s");
                Log("7) Bache con turbo");
                Poner(T(1).transform.position);
                Siguiente(0.05f);
                break;
            }
            case 34:
                if (!EsperarA(() => T(1).Recogidos == 1, 5, "recoger el segundo turbo en moto")) return;
                Siguiente(0.6f);
                break;
            case 35:
            {
                var b = Object.FindObjectsByType<Bache>(FindObjectsSortMode.None).OrderBy(x => Vector2.Distance(x.transform.position, Jugador.transform.position)).First();
                I("bache", b.GetInstanceID());
                I("sac", b.Sacudidas);
                F("r", Jugador.TurboRestante);
                F("t0", Time.time);
                Poner(b.transform.position);
                Siguiente(0.02f);
                break;
            }
            case 36:
            {
                var b = Object.FindObjectsByType<Bache>(FindObjectsSortMode.None).First(x => x.GetInstanceID() == I("bache"));
                if (!EsperarA(() => b.Sacudidas > I("sac"), 3, "el bache")) return;
                float esperado = (F("r") - (Time.time - F("t0"))) / 2f;
                var pc = Jugador;
                Comprobar(pc.TurboActivo && Mathf.Abs(pc.TurboRestante - esperado) < 0.12f,
                          $"el bache deja la mitad: quedaban {F("r") - (Time.time - F("t0")):0.00} s → {pc.TurboRestante:0.00} s");
                Siguiente(1.2f);   // que pase el frenón del bache
                break;
            }
            case 37:   // choque con auto en moto
            {
                Log("8) Choque con un auto (en moto)");
                if (!Jugador.TurboActivo) { Poner(T(2).transform.position); Esperar(0.3f); return; }
                TrafficCar auto = null;
                Vector2 inicio = default, dir = default;
                foreach (var a in Object.FindObjectsByType<TrafficCar>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                             .OrderBy(a => Vector2.Distance(a.transform.position, Jugador.transform.position)))
                {
                    a.gameObject.SetActive(true);
                    if (Acercar(a.GetComponent<Collider2D>(), out inicio, out dir)) { auto = a; break; }
                    a.gameObject.SetActive(false);
                }
                if (auto == null) { Fallo("no encontré un auto con el camino libre"); Paso = 39; break; }
                auto.enabled = false;   // quieto (los choques le siguen llegando)
                I("fines", 0);
                I("finAntes", Veces("turbo_fin"));
                Poner(inicio);
                Jugador.DireccionForzada = dir;
                Siguiente(0.02f);
                break;
            }
            case 38:
                if (!EsperarA(() => I("fines") > 0, 4, "el choque con el auto")) return;
                Jugador.DireccionForzada = null;
                Comprobar(!Jugador.TurboActivo && I("natural") == 0, "el choque con el auto cancela el turbo");
                Comprobar(Veces("turbo_fin") == I("finAntes"), "cancelado no suena el whoosh (solo cuando se acaba solo)");
                Apagar<TrafficCar>(null);
                Siguiente(1.5f);
                break;
            case 39: case 40: case 41: case 42: case 43:
                GM.Entregar(GM.PuntoActual);
                Siguiente(Paso == 43 ? 2.5f : 0.4f);
                break;
            case 44:
                if (!EsperarA(() => Buscar("Canvas/PanelFinal") != null && Buscar("Canvas/PanelFinal").gameObject.activeInHierarchy, 10, "el panel final del nivel 2")) return;
                Clic("Canvas/PanelFinal/BotonSiguiente");
                Siguiente(0.5f);
                break;

            // ================================================================ nivel 3, Deportiva (tope)
            case 45:
                if (!EsperarA(() => Escena == "Nivel3_Lluvia" && Tienda() != null && Tienda().Abierta && Time.unscaledTime > 0f, 15, "ELIGE TU MOTO (nivel 3)")) return;
                Progreso.Equipar(3);   // Deportiva, 6.5
                Siguiente(0.5f);
                break;
            case 46:
            {
                var ventana = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "TiendaMotos_Fondo").Find("Ventana");
                var motos = Progreso.Motos;
                Comprobar(Enumerable.Range(1, motos.Length - 1).All(i => motos[i].precio > motos[i - 1].precio && motos[i].velocidad > motos[i - 1].velocidad),
                          "Progreso.Motos por precio y la velocidad sube con él: " + string.Join(", ", motos.Select(m => $"{m.nombre} ${m.precio} = {m.velocidad:0.0}")));
                var lecturas = new List<string>();
                bool bien = true;
                int antes = 0;
                for (int i = 0; i < motos.Length; i++)
                {
                    var tarjeta = ventana.Find("Fila/Tarjeta_" + motos[i].id);
                    int vel = Llenos(tarjeta, "Velocidad"), res = Llenos(tarjeta, "Resistencia");
                    bien &= tarjeta != null && vel == TiendaMotos.SegmentosVelocidad(motos[i]) && res == TiendaMotos.SegmentosResistencia(motos[i]) && vel > antes
                            && !tarjeta.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(t => t.isTextOverflowing);
                    antes = vel;
                    lecturas.Add($"{motos[i].nombre} {vel}/{res}");
                }
                Comprobar(bien, "ELIGE TU MOTO: barras velocidad/resistencia (de 5) " + string.Join(", ", lecturas) + "; la velocidad crece y ningún texto se desborda");
                Capturar("Tienda_elige_tu_moto.png", Jugador.transform.position);
                Paso = 56;   // el clic en otro cuadro: la captura cambia el modo del Canvas
                Esperar(0.3f);
                break;
            }
            case 56:
                Clic(RutaDe(Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                .First(t => t.name == "TiendaMotos_Fondo").Find("Ventana/BotonEmpezar")));
                Paso = 47;
                Esperar(0.5f);
                break;
            case 47:
            {
                if (!EsperarA(() => !GM.EnEspera && GM.PuntoActual != null && Time.timeScale == 1f, 10, "que empiece el nivel 3")) return;
                Log("9) Nivel 3 (noche y lluvia, Deportiva)");
                Enganchar();
                var ts = Turbos;
                Comprobar(ts.Length == 6 && ts.All(t => t.transform.Find("Taza").GetComponent<SpriteRenderer>().sharedMaterial.name.StartsWith("Sprite-Unlit")),
                          $"{ts.Length} tazas Unlit (se ven de noche)");
                Comprobar(Mathf.Approximately(Jugador.VelocidadMaxima, 6.5f), $"Deportiva a {Jugador.VelocidadMaxima}");
                Apagar<TrafficCar>(null);
                Apagar<Peaton>(null);
                Capturar("Turbo_mapa_nivel3.png", ts[3].transform.position);
                Paso++;
                break;
            }
            case 48:
                if (!Medir(6.5f, "Deportiva sin turbo")) return;
                Poner(T(0).transform.position);
                Siguiente(0.05f);
                break;
            case 49:
                if (!EsperarA(() => T(0).Recogidos == 1, 5, "recoger un turbo con la Deportiva")) return;
                Comprobar(Mathf.Abs(Jugador.VelocidadMaxima - 8.5f) < 0.001f, $"Deportiva: 6.5 + 2.0 = {Jugador.VelocidadMaxima:0.00} tiles/s (tope 9)");
                Paso++;
                break;
            case 50:
                if (!Medir(8.5f, "Deportiva con turbo", () => Capturar("Turbo_noche.png", Jugador.transform.position))) return;
                Jugador.CancelarTurbo();
                Log("10) Las 4 motos");
                I("moto", 0);
                Paso = 51;
                break;
            case 51:
                Progreso.Equipar(I("moto"));
                Siguiente(0.3f);
                break;
            case 52:
            {
                var d = Progreso.Motos[I("moto")];
                Comprobar(Jugador.EnMoto && Mathf.Approximately(Jugador.VelocidadMaxima, d.velocidad), $"{d.nombre} equipada: velocidad {Jugador.VelocidadMaxima:0.0} (Progreso {d.velocidad:0.0})");
                Paso++;
                break;
            }
            case 53:
            {
                var d = Progreso.Motos[I("moto")];
                if (!Medir(d.velocidad, $"{d.nombre} sin turbo")) return;
                F("v" + I("moto"), F("medida"));
                Jugador.ActivarTurbo();
                Comprobar(Mathf.Abs(Jugador.VelocidadMaxima - (d.velocidad + 2f)) < 0.001f && Mathf.Approximately(Jugador.TurboDuracion, 5f),
                          $"{d.nombre} turbo: {d.velocidad:0.0} + 2.0 = {Jugador.VelocidadMaxima:0.0} por {Jugador.TurboDuracion} s");
                Paso++;
                break;
            }
            case 54:
            {
                int i = I("moto");
                var d = Progreso.Motos[i];
                if (!Medir(d.velocidad + 2f, $"{d.nombre} con turbo")) return;
                F("vt" + i, F("medida"));
                Jugador.CancelarTurbo();
                I("moto", i + 1);
                Paso = i + 1 < Progreso.Motos.Length ? 51 : 55;
                break;
            }
            case 55:
            {
                int n = Progreso.Motos.Length;
                var v = Enumerable.Range(0, n).Select(i => F("v" + i)).ToArray();
                var vt = Enumerable.Range(0, n).Select(i => F("vt" + i)).ToArray();
                Comprobar(Enumerable.Range(1, n - 1).All(i => v[i] > v[i - 1] && vt[i] > vt[i - 1]),
                          $"el orden se respeta: sin turbo {string.Join(" < ", v.Select(x => x.ToString("0.00")))}; con turbo {string.Join(" < ", vt.Select(x => x.ToString("0.00")))}");
                Paso = 90;
                EditorApplication.isPlaying = false;
                Esperar(1f);
                break;
            }

            case 90:
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Time.timeScale = 1f;
                foreach (var k in ClavesProgreso)
                {
                    var v = SessionState.GetString(P + "pp." + k, null);
                    if (v == null) continue;
                    if (v == "") PlayerPrefs.DeleteKey(k); else PlayerPrefs.SetInt(k, int.Parse(v));
                }
                PlayerPrefs.Save();
                ProgresoPrueba.Limpiar();   // el progreso vive solo en la sesión: no hay nada que restaurar
                var escenaInicio = SessionState.GetString(P + "inicio", "");
                EditorSceneManager.playModeStartScene = escenaInicio.Length > 0 ? AssetDatabase.LoadAssetAtPath<SceneAsset>(escenaInicio) : null;
                int fallos = I("fallos");
                string consola = SessionState.GetString(P + "consola", "");
                File.WriteAllText(Resultado, (fallos == 0 ? "OK" : $"{fallos} FALLOS") + "\n" + SessionState.GetString(P + "log", "") +
                                  "\nCONSOLA (advertencias y errores en Play): " + (consola.Length == 0 ? "ninguno" : "\n" + consola));
                Paso = 0;
                break;
        }
    }

    // ------------------------------------------------------------------ medir la velocidad (desplazamiento real)

    // Busca un carril libre, pone al repartidor y lo hace avanzar; a los 0.25 s apunta la posición y ~1 s después
    // compara lo recorrido entre el tiempo de juego con 'esperada'. 'durante' corre a media medida (capturas).
    static bool Medir(float esperada, string que, Action durante = null)
    {
        var pc = Jugador;
        var rb = pc.GetComponent<Rigidbody2D>();
        switch (I("m"))
        {
            case 0:
                if (!Carril(esperada * 1.4f + 1.5f, out var inicio, out var dir)) { Fallo($"{que}: no encontré un carril libre"); return true; }
                Poner(inicio);
                pc.DireccionForzada = dir;
                I("m", 1);
                Esperar(0.25f);
                return false;
            case 1:
                F("mx", rb.position.x); F("my", rb.position.y); F("mt", Time.time);
                I("m", 2);
                Esperar(0.5f);
                return false;
            case 2:
                durante?.Invoke();
                I("m", 3);
                Esperar(0.5f);
                return false;
            default:
                float dt = Time.time - F("mt");
                float d = Vector2.Distance(rb.position, new Vector2(F("mx"), F("my")));
                pc.DireccionForzada = null;
                I("m", 0);
                float v = d / dt;
                F("medida", v);
                Comprobar(Mathf.Abs(v - esperada) / esperada < 0.03f, $"{que}: {d:0.00} tiles en {dt:0.00} s = {v:0.00} tiles/s (se espera {esperada:0.00})");
                return true;
        }
    }

    // cuadritos llenos (relleno con color distinto del vacío) de una barra de la tarjeta
    static int Llenos(Transform tarjeta, string barra)
    {
        var fila = tarjeta != null ? tarjeta.Find("Barras/" + barra) : null;
        if (fila == null) return -1;
        var vacio = new Color32(0xE6, 0xC4, 0x8C, 255);
        int n = 0;
        for (int k = 0; k < TiendaMotos.Segmentos; k++)
        {
            var r = fila.Find($"Seg_{k}/Relleno");
            if (r == null) return -1;
            Color32 c = r.GetComponent<UnityEngine.UI.Image>().color;
            if (c.r != vacio.r || c.g != vacio.g || c.b != vacio.b) n++;
        }
        return n;
    }

    static Collider2D CuerpoJugador(PlayerController pc) =>
        pc.GetComponents<Collider2D>().FirstOrDefault(c => c.enabled && !c.isTrigger) ?? pc.GetComponentsInChildren<Collider2D>().First(c => c.enabled && !c.isTrigger);

    static ContactFilter2D Solidos()
    {
        var f = new ContactFilter2D { useTriggers = false };
        f.SetLayerMask(Physics2D.GetLayerCollisionMask(Jugador.gameObject.layer));
        return f;
    }

    // ¿cabe el cuerpo del repartidor en 'p' y puede avanzar 'largo' hacia 'dir' sin tocar nada? (ignora 'salvo')
    static bool Libre(Vector2 p, Vector2 dir, float largo, Collider2D salvo, out RaycastHit2D primero)
    {
        var pc = Jugador;
        var cuerpo = CuerpoJugador(pc);
        Vector2 off = (Vector2)cuerpo.bounds.center - (Vector2)pc.transform.position;
        Vector2 tam = (Vector2)cuerpo.bounds.size + new Vector2(0.06f, 0.06f);
        var filtro = Solidos();
        var res = new Collider2D[8];
        int n = Physics2D.OverlapBox(p + off, tam, 0f, filtro, res);
        primero = default;
        for (int i = 0; i < n; i++) if (res[i].attachedRigidbody != pc.GetComponent<Rigidbody2D>()) return false;
        var hits = new RaycastHit2D[8];
        int h = Physics2D.BoxCast(p + off, tam, 0f, dir, filtro, hits, largo);
        foreach (var hit in hits.Take(h).OrderBy(x => x.distance))
        {
            if (hit.rigidbody == pc.GetComponent<Rigidbody2D>()) continue;
            primero = hit;
            return hit.collider == salvo;
        }
        return salvo == null;
    }

    static bool Carril(float largo, out Vector2 inicio, out Vector2 dir)
    {
        Vector2 c = Jugador.transform.position;
        var peligros = Turbos.Select(t => (Vector2)t.transform.position)
            .Concat(Object.FindObjectsByType<Bache>(FindObjectsSortMode.None).Select(b => (Vector2)b.GetComponent<Collider2D>().bounds.center)).ToList();
        for (int r = 0; r <= 30; r++)
            for (int x = -r; x <= r; x++)
                for (int y = -r; y <= r; y++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != r) continue;
                    var p = new Vector2(Mathf.Round(c.x) + x + 0.5f, Mathf.Round(c.y) + y + 0.5f);
                    foreach (var d in Cuatro)
                    {
                        if (peligros.Any(q => DistanciaSegmento(q, p, p + d * largo) < 1.6f)) continue;
                        if (!Libre(p, d, largo, null, out _)) continue;
                        inicio = p;
                        dir = d;
                        return true;
                    }
                }
        inicio = dir = default;
        return false;
    }

    static float DistanciaSegmento(Vector2 q, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(q, a + t * ab);
    }

    // Un punto de arranque a ~2 tiles del objetivo, de frente y con el camino libre hasta él
    static bool Acercar(Collider2D objetivo, out Vector2 inicio, out Vector2 dir)
    {
        Physics2D.SyncTransforms();
        var b = objetivo.bounds;
        var cuerpo = CuerpoJugador(Jugador).bounds;
        Vector2 off = (Vector2)cuerpo.center - (Vector2)Jugador.transform.position;
        foreach (var d in Cuatro)
        {
            float ext = Mathf.Abs(d.x) * (b.extents.x + cuerpo.extents.x) + Mathf.Abs(d.y) * (b.extents.y + cuerpo.extents.y);
            var p = (Vector2)b.center - d * (ext + 2f) - off;
            if (Libre(p, d, 3f, objetivo, out _)) { inicio = p; dir = d; return true; }
        }
        inicio = dir = default;
        return false;
    }

    // un lugar libre a 2-3 tiles (para ver la taza en pantalla sin tocarla)
    static Vector2 LibreCerca(Vector2 p)
    {
        foreach (var d in Cuatro)
            foreach (float r in new[] { 2.5f, 3f, 2f })
                if (Libre(p + d * r, -d, 0.01f, null, out _)) return p + d * r;
        return p + Vector2.down * 2.5f;
    }

    // ------------------------------------------------------------------ ayudantes (como PruebaReloj)

    static TiendaMotos Tienda() => Object.FindFirstObjectByType<TiendaMotos>();

    static void Apagar<C>(GameObject salvo) where C : Component
    {
        foreach (var c in Object.FindObjectsByType<C>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            c.gameObject.SetActive(c.gameObject == salvo);
    }

    static void Poner(Vector2 p)
    {
        var pc = Jugador;
        pc.GetComponent<Rigidbody2D>().position = p;
        pc.transform.position = p;
        var cam = Object.FindFirstObjectByType<CameraFollow>();
        cam.transform.position = cam.Limitar(new Vector3(p.x, p.y + 1.25f, cam.transform.position.z));
    }

    static bool EsperarA(Func<bool> condicion, float limite, string que)
    {
        float ahora = (float)EditorApplication.timeSinceStartup;
        if (F("desde") == 0) F("desde", ahora);
        bool listo = false;
        try { listo = condicion(); } catch (NullReferenceException) { } catch (InvalidOperationException) { } catch (IndexOutOfRangeException) { }
        if (!listo && ahora - F("desde") < limite) { Esperar(0.05f); return false; }
        if (!listo) Fallo($"se acabó el tiempo esperando {que}");
        F("desde", 0);
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
        Log($"  clic en {ruta}");
    }

    static void Capturar(string archivo, Vector2? foco = null)
    {
        var ruta = Path.Combine(SessionState.GetString(P + "carpeta", "Temp"), archivo);
        var cam = Camera.main;
        var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.isRootCanvas && c.transform.Find("PanelFinal") != null);
        var rt = new RenderTexture(1920, 1080, 24);
        var modo = canvas.renderMode;
        int capa = canvas.sortingLayerID, orden = canvas.sortingOrder;
        var posCamara = cam.transform.position;
        try
        {
            cam.targetTexture = rt;
            if (foco.HasValue) cam.transform.position = new Vector3(foco.Value.x, foco.Value.y, posCamara.z);
            var centro = cam.transform.position;
            if (CameraFollow.LimitesDelMapa(out var lim))
            {
                float h = cam.orthographicSize, w = h * 1920f / 1080f;
                cam.transform.position = new Vector3(Mathf.Clamp(centro.x, lim.xMin + w, lim.xMax - w),
                                                     Mathf.Clamp(centro.y, lim.yMin + h, lim.yMax - h), posCamara.z);
            }
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
            cam.transform.position = posCamara;
            canvas.renderMode = modo;
            canvas.sortingLayerID = capa;
            canvas.sortingOrder = orden;
            rt.Release();
        }
        Log($"  captura: {ruta}");
    }
}
