using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Prueba en Play del audio, desde MenuPrincipal (como el build): menú (música del menú, controles Música/Efectos),
// Seleccion (la misma música sin cortarse), Intro (musica_historia), nivel 1 (cambia la música con fundido; el loop da la vuelta sin detenerse;
// pausa abre/cierra; entrega, propina y pedido nuevo suenan una vez; tick en los últimos 5 s; cancelación; silenciar
// música y efectos), nivel 2 en moto (música del nivel; el motor sube de tono con la velocidad y calla al detenerse;
// choque y claxon, bache real una vez), nivel 3 (música nocturna, lluvia en loop y trueno; al final el jingle de victoria: la fanfarria
// va en la escena Final, ver PruebaDerrota).
// Mide también el tiempo por cuadro. El avance y los ajustes de sonido quedan como estaban.
// Temp/PruebaAudio.pedido → Temp/PruebaAudio.resultado
[InitializeOnLoad]
public static class PruebaAudio
{
    const string Pedido = "Temp/PruebaAudio.pedido";
    const string Resultado = "Temp/PruebaAudio.resultado";
    const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";
    const string P = "PruebaAudio.";
    static readonly string[] ClavesInt = { "musica_activa", "efectos_activos" };
    static readonly string[] ClavesFloat = { "volumen_musica", "volumen_efectos" };

    static bool enganchado;

    static PruebaAudio()
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
    static AudioManager AM => AudioManager.Instance;
    static float F(string k) => SessionState.GetFloat(P + k, 0);
    static void F(string k, float v) => SessionState.SetFloat(P + k, v);
    static int Veces(string n) => AM.VecesSonado(n);

    static void Revisar()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (Paso == 0)
        {
            if (!File.Exists(Pedido) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Pedido);
            SessionState.SetString(P + "log", "");
            SessionState.SetString(P + "consola", "");
            SessionState.SetInt(P + "fallos", 0);
            SessionState.SetBool(P + "sel", false);
            SessionState.SetFloat(P + "desde", 0);
            foreach (var k in ClavesInt) SessionState.SetString(P + "pp." + k, PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k).ToString() : "");
            foreach (var k in ClavesFloat) SessionState.SetString(P + "pf." + k, PlayerPrefs.HasKey(k) ? PlayerPrefs.GetFloat(k).ToString("R") : "");
            ProgresoPrueba.Preparar(motos: 0b1111);
            PlayerPrefs.SetInt("musica_activa", 1);
            PlayerPrefs.SetInt("efectos_activos", 1);
            PlayerPrefs.DeleteKey("volumen_musica");
            PlayerPrefs.DeleteKey("volumen_efectos");
            PlayerPrefs.Save();
            var inicio = EditorSceneManager.playModeStartScene;
            SessionState.SetString(P + "inicio", inicio != null ? AssetDatabase.GetAssetPath(inicio) : "");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaMenu);
            var errores = RevisarCanvas.RevisarEscenasDeJuego();
            foreach (var e in errores) Fallo(e);
            if (errores.Count == 0) Log("  ok: prefab de audio completo y controles de sonido en las 3 pausas");
            Log("Entrando a Play desde MenuPrincipal.");
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
        switch (Paso)
        {
            // ---------------------------------------------------------------- menú e intro
            case 1:
            {
                Application.runInBackground = true;
                if (!EsperarA(() => Escena == "MenuPrincipal" && Time.timeSinceLevelLoad > 1f && Buscar("Canvas/BotonIniciar") != null, 15, "el menú")) return;
                Log("1) Menú");
                Comprobar(AM != null && Object.FindObjectsByType<AudioManager>(FindObjectsSortMode.None).Length == 1, "un solo AudioManager, creado solo desde el prefab");
                Comprobar(AM.MusicaActual == "musica_menu" && AM.FuenteMusica.isPlaying && AM.FuenteMusica.loop, $"música del menú en loop ({AM.MusicaActual})");
                Comprobar(Mathf.Abs(AM.FuenteMusica.volume - 0.35f) < 0.01f, $"volumen de música por defecto 0.35 ({AM.FuenteMusica.volume:0.00})");
                var clip = AM.FuenteMusica.clip;
                Comprobar(clip.loadType == AudioClipLoadType.Streaming && clip.samples == Mathf.RoundToInt(48f * 44100),
                          $"música en Streaming y sin relleno al final ({clip.samples} muestras = {clip.length:0.000} s)");
                Comprobar(Buscar("Canvas/MenuSonido/BotonMusica") != null && Buscar("Canvas/MenuSonido/BotonEfectos") != null, "controles Música y Efectos en el menú");
                Clic("Canvas/MenuSonido/BotonMusica");
                Comprobar(!AudioManager.MusicaActiva && AM.FuenteMusica.volume == 0f && Texto("Canvas/MenuSonido/BotonMusica/Texto") == "Música: no" && PlayerPrefs.GetInt("musica_activa") == 0,
                          $"Música: no → volumen 0 y guardado (\"{Texto("Canvas/MenuSonido/BotonMusica/Texto")}\")");
                Clic("Canvas/MenuSonido/BotonMusica");
                Comprobar(AudioManager.MusicaActiva && AM.FuenteMusica.volume > 0.3f && Texto("Canvas/MenuSonido/BotonMusica/Texto") == "Música: sí", "Música: sí → vuelve");
                F("tMenu", AM.FuenteMusica.time);
                Capturar("Menu_sonido.png");
                Siguiente(0.3f);
                break;
            }
            case 2:
                Progreso.OlvidarPersonaje();   // que vuelva a pasar por Seleccion e Intro
                Clic("Canvas/BotonIniciar");
                Siguiente(0.5f);
                break;
            case 3:
                if (Escena != "Intro" && !SessionState.GetBool(P + "sel", false))
                {
                    if (!EsperarA(() => Escena == "Seleccion" && Time.timeSinceLevelLoad > 0.2f, 10, "la selección")) return;
                    Log("2) Selección e Intro");
                    Comprobar(AM.MusicaActual == "musica_menu" && AM.FuenteMusica.isPlaying && AM.FuenteMusica.time > F("tMenu"),
                              $"en la selección la música del menú sigue sin reiniciarse ({F("tMenu"):0.0} → {AM.FuenteMusica.time:0.0} s)");
                    SessionState.SetBool(P + "sel", true);
                }
                if (!EsperarA(() => SeleccionPrueba.Pasar() && Escena == "Intro" && Time.timeSinceLevelLoad > 0.5f, 10, "la Intro")) return;
                SessionState.SetBool(P + "sel", false);
                Comprobar(AM.MusicaActual == "musica_historia" && AM.FuenteMusica.isPlaying, $"la Intro pone musica_historia ({AM.MusicaActual})");
                Clic("Canvas/BotonSaltar");
                Siguiente(0.5f);
                break;

            // ---------------------------------------------------------------- nivel 1
            case 4:
                if (!EsperarA(() => NivelPrueba.Pasar() && Escena == "SampleScene" && GM != null && GM.PuntoActual != null && Time.timeSinceLevelLoad > 1.2f, 15, "el nivel 1")) return;
                Log("3) Nivel 1");
                Comprobar(AM.MusicaActual == "musica_nivel1_residencial" && AM.FuenteMusica.isPlaying && Mathf.Abs(AM.FuenteMusica.volume - 0.35f) < 0.01f,
                          $"música del nivel 1 tras el fundido ({AM.MusicaActual}, vol {AM.FuenteMusica.volume:0.00})");
                Comprobar(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(s => s.isPlaying && s.clip != null && s.clip.name.StartsWith("musica_")) == 1,
                          "la música del menú ya no suena (una sola pista)");
                Comprobar(!AM.FuenteMotor.isPlaying, "a pie: sin motor");
                Comprobar(Veces("pedido_nuevo") == 1, $"ding-dong del primer pedido una vez ({Veces("pedido_nuevo")})");
                // el loop da la vuelta: se adelanta a 0.3 s del final
                var pista = AM.FuenteMusica;
                pista.timeSamples = pista.clip.samples - (int)(0.3f * 44100);
                Siguiente(0.8f);
                break;
            case 5:
            {
                var m = AM.FuenteMusica;
                Comprobar(m.isPlaying && m.timeSamples < 1f * 44100 && AM.MusicaActual == "musica_nivel1_residencial", $"el loop da la vuelta sin detenerse (ahora en {m.time:0.00} s)");
                Log("4) Pausa");
                Clic("Canvas/BotonPausa");
                Comprobar(Veces("pausa_abrir") == 1 && Time.timeScale == 0f, "abrir pausa suena");
                Siguiente(0.3f);
                break;
            }
            case 6:
                Comprobar(Buscar("Canvas/PanelPausa/PausaSonido/BotonEfectos") != null, "controles de sonido en la pausa");
                Comprobar(!AM.FuenteMotor.isPlaying && AM.FuenteMusica.isPlaying, "en pausa la música sigue (y no hay motor)");
                Clic("Canvas/PanelPausa/PausaSonido/BotonEfectos");
                Comprobar(!AudioManager.EfectosActivos && Texto("Canvas/PanelPausa/PausaSonido/BotonEfectos/Texto") == "Efectos: no", "Efectos: no");
                AudioManager.Sonar("entrega");
                Comprobar(AM.EfectosSonando == 0, "con efectos apagados no suena nada");
                Clic("Canvas/PanelPausa/PausaSonido/BotonMusica");
                Capturar("Pausa_sonido_apagado.png");
                Siguiente(0.3f);
                break;
            case 7:
                Clic("Canvas/PanelPausa/PausaSonido/BotonMusica");
                Clic("Canvas/PanelPausa/PausaSonido/BotonEfectos");
                Comprobar(AudioManager.EfectosActivos && AudioManager.MusicaActiva && PlayerPrefs.GetInt("efectos_activos") == 1, "Efectos: sí y Música: sí otra vez");
                Clic("Canvas/PanelPausa/PausaSeguir");
                Comprobar(Veces("pausa_cerrar") == 1 && Time.timeScale == 1f, "cerrar pausa suena");
                Log("5) Entrega con propina");
                AM.ReiniciarContador();
                GM.Entregar(GM.PuntoActual);
                Siguiente(1.6f);
                break;
            case 8:
                Comprobar(Veces("entrega") == 1 && Veces("propina") == 1 && Veces("pedido_nuevo") == 1,
                          $"entrega {Veces("entrega")}, propina {Veces("propina")}, ding-dong del siguiente {Veces("pedido_nuevo")} (una vez cada uno)");
                Log("6) Tick de los últimos 5 s y cancelación (tiempo x8 hasta los 6 s)");
                Time.timeScale = 8f;
                Siguiente(0.1f);
                break;
            case 9:
                if (!EsperarA(() => GM.TiempoMaximo - GM.TiempoPedido <= 6.5f, 60, "los últimos segundos")) return;
                Time.timeScale = 1f;
                AM.ReiniciarContador();
                Siguiente(0.1f);
                break;
            case 10:
                if (!EsperarA(() => GM.Cancelados == 1, 12, "la cancelación")) return;
                Comprobar(Veces("tick") >= 4 && Veces("tick") <= 5, $"tick en los últimos segundos: {Veces("tick")} (uno por segundo)");
                Comprobar(Veces("pedido_cancelado") == 1, "pedido cancelado: zumbido una vez");
                Siguiente(0.5f);
                break;
            case 11:
                GM.Entregar(GM.PuntoActual);
                Siguiente(2.5f);
                break;
            case 12:
            {
                if (!EsperarA(() => GM.NivelTerminado && Buscar("Canvas/PanelFinal").gameObject.activeInHierarchy, 10, "el panel final")) return;
                Comprobar(AM.MusicaActual == "" && Veces("jingle_victoria") == 1, $"fin del nivel 1 (2 estrellas): la música se apaga y suena el jingle de victoria ({Veces("jingle_victoria")})");
                Clic("Canvas/PanelFinal/BotonSiguiente");
                Siguiente(0.5f);
                break;
            }

            // ---------------------------------------------------------------- nivel 2 en moto
            case 13:
            {
                if (!EsperarA(() => Escena == "Nivel2_Comercial" && Object.FindFirstObjectByType<TiendaMotos>() != null && Object.FindFirstObjectByType<TiendaMotos>().Abierta, 15, "ELIGE TU MOTO")) return;
                Log("7) Nivel 2: ELIGE TU MOTO");
                Comprobar(AM.MusicaActual == "musica_nivel2_comercial" && AM.FuenteMusica.isPlaying, $"música del nivel 2 ({AM.MusicaActual})");
                AM.ReiniciarContador();
                var ventana = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "TiendaMotos_Fondo");
                var tienda = Object.FindFirstObjectByType<TiendaMotos>();
                typeof(TiendaMotos).GetMethod("Clic", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(tienda, new object[] { 1 });
                Comprobar(Veces("equipar_moto") == 1 && Progreso.MotoEquipada == 1, "equipar la Clásica suena");
                Clic(RutaDe(ventana.Find("Ventana/BotonEmpezar")));
                Siguiente(0.6f);
                break;
            }
            case 14:
            {
                if (!EsperarA(() => !GM.EnEspera && GM.PuntoActual != null && Time.timeScale == 1f, 10, "que empiece el nivel 2")) return;
                var pc = Object.FindFirstObjectByType<PlayerController>();
                Comprobar(pc.EnMoto && AM.FuenteMotor.isPlaying && AM.FuenteMotor.clip.name == "motor", "en moto: el motor suena (loop)");
                Log("8) Motor: acelera y se detiene");
                F("tonoQuieto", AM.FuenteMotor.pitch);
                F("tonoMax", 0);
                F("velMax", 0);
                pc.DireccionForzada = Vector2.right;
                F("t0", Time.time);
                Siguiente(0.05f);
                break;
            }
            case 15:
            {
                var pc = Object.FindFirstObjectByType<PlayerController>();
                F("tonoMax", Mathf.Max(F("tonoMax"), AM.FuenteMotor.pitch));
                F("velMax", Mathf.Max(F("velMax"), pc.GetComponent<Rigidbody2D>().linearVelocity.magnitude));
                if (Time.time - F("t0") < 1.2f) { Esperar(0.05f); return; }
                pc.DireccionForzada = Vector2.zero;
                Siguiente(1.0f);
                break;
            }
            case 16:
            {
                float esperado = Mathf.Lerp(0.8f, 1.6f, F("velMax") / 6.5f);
                Comprobar(F("tonoQuieto") < 0.85f && F("tonoMax") > 1.3f && Mathf.Abs(F("tonoMax") - esperado) < 0.08f,
                          $"el tono sigue la velocidad: quieto {F("tonoQuieto"):0.00}, a {F("velMax"):0.0} u/s → {F("tonoMax"):0.00} (esperado {esperado:0.00})");
                Comprobar(AM.FuenteMotor.pitch < 0.9f && AM.FuenteMotor.isPlaying, $"al detenerse vuelve abajo ({AM.FuenteMotor.pitch:0.00}) sin apagarse");
                Log("9) Choque fuerte con un auto (claxon) y bache");
                AM.ReiniciarContador();
                var pc = Object.FindFirstObjectByType<PlayerController>();
                var imp = pc.GetComponent<ImpactoMoto>();
                var auto = Object.FindObjectsByType<TrafficCar>(FindObjectsSortMode.None).OrderBy(a => Vector2.Distance(a.transform.position, pc.transform.position)).First();
                var golpear = typeof(ImpactoMoto).GetMethod("Golpear", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                golpear.Invoke(imp, new object[] { ImpactoMoto.Intensidad.Fuerte, 8f, (Vector2)pc.transform.position + Vector2.right * 0.5f, Vector2.left, auto, null });
                golpear.Invoke(imp, new object[] { ImpactoMoto.Intensidad.Fuerte, 8f, (Vector2)pc.transform.position + Vector2.right * 0.5f, Vector2.left, auto, null });
                Siguiente(0.6f);
                break;
            }
            case 17:
                Comprobar(Veces("choque_fuerte") == 1 && Veces("claxon") == 1, $"choque fuerte {Veces("choque_fuerte")} y claxon {Veces("claxon")}: una vez cada uno (el segundo golpe cae en la invulnerabilidad)");
                Siguiente(2.0f);   // que pase el parpadeo
                break;
            case 18:
            {
                var pc = Object.FindFirstObjectByType<PlayerController>();
                var bache = Object.FindObjectsByType<Bache>(FindObjectsSortMode.None).OrderBy(b => Vector2.Distance(b.transform.position, pc.transform.position)).First();
                AM.ReiniciarContador();
                pc.GetComponent<Rigidbody2D>().position = bache.transform.position;
                pc.transform.position = bache.transform.position;
                Siguiente(1.2f);
                break;
            }
            case 19:
                Comprobar(Veces("bache") == 1 && Veces("choque_leve") == 0 && Veces("choque_fuerte") == 0, $"bache real: suena una vez ({Veces("bache")})");
                Log("10) Rendimiento: 3 s de juego en moto");
                F("cuadros", 0); F("suma", 0); F("max", 0); F("t0", Time.realtimeSinceStartup);
                Object.FindFirstObjectByType<PlayerController>().DireccionForzada = Vector2.left;
                Siguiente(0f);
                break;
            case 20:
            {
                float dt = Time.unscaledDeltaTime;
                F("cuadros", F("cuadros") + 1); F("suma", F("suma") + dt); F("max", Mathf.Max(F("max"), dt));
                if (Time.realtimeSinceStartup - F("t0") < 3f) { Esperar(0f); return; }
                Object.FindFirstObjectByType<PlayerController>().DireccionForzada = null;
                int fuentes = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length;
                Log($"  medido: {F("cuadros"):0} muestras, dt medio {1000 * F("suma") / F("cuadros"):0.0} ms, máximo {1000 * F("max"):0.0} ms; {fuentes} AudioSource en total");
                Comprobar(fuentes <= 20, $"pool acotado ({fuentes} AudioSource)");
                for (int i = 0; i < 5 && !GM.NivelTerminado; i++) GM.Entregar(GM.PuntoActual);
                Siguiente(2.5f);
                break;
            }
            case 21:
                if (!EsperarA(() => GM.NivelTerminado && Buscar("Canvas/PanelFinal").gameObject.activeInHierarchy, 10, "el panel final del nivel 2")) return;
                Comprobar(!AM.FuenteMotor.isPlaying || AM.FuenteMotor.volume < 0.05f, "al terminar el nivel el motor se apaga");
                Clic("Canvas/PanelFinal/BotonSiguiente");
                Siguiente(0.5f);
                break;

            // ---------------------------------------------------------------- nivel 3
            case 22:
            {
                if (!EsperarA(() => Escena == "Nivel3_Lluvia" && Object.FindFirstObjectByType<TiendaMotos>() != null && Object.FindFirstObjectByType<TiendaMotos>().Abierta, 15, "el nivel 3")) return;
                Log("11) Nivel 3");
                Comprobar(AM.MusicaActual == "musica_nivel3_lluvia" && AM.FuenteMusica.isPlaying, $"música nocturna ({AM.MusicaActual})");
                Comprobar(AM.FuenteLluvia.isPlaying && AM.FuenteLluvia.loop && AM.FuenteLluvia.clip.name == "lluvia", "lluvia continua en loop");
                var ventana = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "TiendaMotos_Fondo");
                Clic(RutaDe(ventana.Find("Ventana/BotonEmpezar")));
                Siguiente(0.6f);
                break;
            }
            case 23:
                if (!EsperarA(() => !GM.EnEspera && GM.PuntoActual != null && Time.timeScale == 1f, 10, "que empiece el nivel 3")) return;
                AM.ReiniciarContador();
                AM.TruenoYa();
                Siguiente(0.5f);
                break;
            case 24:
                Comprobar(Veces("trueno") == 1, "trueno lejano (al azar cada 15 a 40 s; aquí forzado)");
                Log($"  se entregan los {GM.PedidosMeta} pedidos del nivel 3");
                for (int i = 0; i < 30 && !GM.NivelTerminado; i++) GM.Entregar(GM.PuntoActual);
                Siguiente(1.5f);
                break;
            case 25:
                if (!EsperarA(() => GM.NivelTerminado, 10, "el final del nivel 3")) return;
                Comprobar(AM.MusicaActual == "" && Veces("jingle_victoria") == 1,
                          $"final del nivel 3: jingle de victoria, la música se apaga (la fanfarria va en Final; {Veces("jingle_victoria")})");
                Paso = 90;
                EditorApplication.isPlaying = false;
                Esperar(1f);
                break;

            case 90:
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                foreach (var k in ClavesInt)
                {
                    var v = SessionState.GetString(P + "pp." + k, null);
                    if (v == null) continue;
                    if (v == "") PlayerPrefs.DeleteKey(k); else PlayerPrefs.SetInt(k, int.Parse(v));
                }
                foreach (var k in ClavesFloat)
                {
                    var v = SessionState.GetString(P + "pf." + k, null);
                    if (v == null) continue;
                    if (v == "") PlayerPrefs.DeleteKey(k); else PlayerPrefs.SetFloat(k, float.Parse(v));
                }
                PlayerPrefs.Save();
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
        var carpeta = @"C:\Users\kangu\AppData\Local\Temp\claude\C--Users-kangu-DeliveryBrenns\5e10bfe6-cf05-4391-9b55-19df2ab5b693\scratchpad\audio";
        Directory.CreateDirectory(carpeta);
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
            File.WriteAllBytes(Path.Combine(carpeta, archivo), tex.EncodeToPNG());
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
