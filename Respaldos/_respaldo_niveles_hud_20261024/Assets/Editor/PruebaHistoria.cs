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

// Prueba en Play del sonido de la intro (la historia), empezando en MenuPrincipal:
// A) Iniciar juego: la música pasa de la del menú a musica_historia (a su volumen propio, 0.3 por defecto) y recorre las
//    6 viñetas con Espacio y clic del ratón (entrada real del Input System): "pop" con cada titular, tic de máquina cada
//    2 letras (que se calla si el texto se completa de golpe), clic al avanzar, música al 70 % en los fades y de vuelta,
//    remate en la viñeta 6 y, al terminar, arranque de moto → nivel 1 con su música.
// B) Pausa → "Menú" → "Sí": en el menú vuelve la música del menú a su volumen normal.
// C) Iniciar juego y Esc a mitad de la escritura: el tic se calla, arranque de moto → nivel 1 con su música.
// D) Música y Efectos en "no": en la intro y al saltarla no suena nada (se piden los efectos, pero ninguna fuente suena).
// El avance guardado y los ajustes de sonido quedan como estaban.
// Temp/PruebaHistoria.pedido → Temp/PruebaHistoria.resultado
[InitializeOnLoad]
public static class PruebaHistoria
{
    const string Pedido = "Temp/PruebaHistoria.pedido";
    const string Resultado = "Temp/PruebaHistoria.resultado";
    const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";
    const string P = "PruebaHistoria.";
    static readonly string[] ClavesGuardadas = { "dinero_total", "motos_compradas", "moto_equipada", "musica_activa", "efectos_activos" };

    static bool enganchado;

    static PruebaHistoria()
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
    static int Dato(string k) => SessionState.GetInt(P + "d." + k, 0);
    static void Guardar(string k, int v) => SessionState.SetInt(P + "d." + k, v);
    static string Escena => SceneManager.GetActiveScene().name;
    static AudioManager AM => AudioManager.Instance;
    static int Veces(string n) => AM.VecesSonado(n);
    static int Tics => Veces("maquina_tic_1") + Veces("maquina_tic_2") + Veces("maquina_tic_3");

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
            SessionState.SetFloat(P + "desde", 0);
            foreach (var k in ClavesGuardadas)
                SessionState.SetString(P + "pp." + k, PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k).ToString() : "");
            var inicio = EditorSceneManager.playModeStartScene;
            SessionState.SetString(P + "inicio", inicio != null ? AssetDatabase.GetAssetPath(inicio) : "");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaMenu);
            var errores = ConstruirSonido.VerificarPrefab().ToList();
            foreach (var e in errores) Fallo(e);
            if (errores.Count == 0) Log("  ok: prefab AudioManager con musica_historia y los 7 efectos de la intro");
            Log("Entrando a Play desde MenuPrincipal.");
            PruebaIntro.EntradaSinFoco();   // Espacio, clic y Esc llegan aunque la Game view no tenga el foco
            Paso = 1;
            Esperar(2f);
            EditorApplication.isPlaying = true;
            return;
        }
        if (Esperando) return;
        try { Avanzar(); }
        catch (Exception e) { Fallo($"excepción en el paso {Paso}: {e}"); Paso = 90; EditorApplication.isPlaying = false; }
    }

    static void Avanzar()
    {
        if (Paso < 90 && !EditorApplication.isPlaying) { Fallo($"Play se cerró en el paso {Paso}"); Paso = 90; return; }
        switch (Paso)
        {
            // ---------------------------------------------------------------- A) la historia completa
            case 1:
                Application.runInBackground = true;
                if (!EsperarMenu()) return;
                AudioManager.MusicaActiva = true;
                AudioManager.EfectosActivos = true;
                Log($"A) Menú → Iniciar juego (volumen de música {AudioManager.VolumenMusica:0.00})");
                Comprobar(Object.FindAnyObjectByType<AudioListener>() != null, "el menú tiene AudioListener");
                Comprobar(AM.MusicaActual == "musica_menu" && AM.FuenteMusica.isPlaying && AM.FuenteMusica.volume > 0.01f,
                          $"en el menú suena musica_menu (vol {AM.FuenteMusica.volume:0.00})");
                AM.ReiniciarContador();
                Clic("Canvas/BotonIniciar");
                Siguiente(0.3f);
                break;
            case 2:
                if (!EsperarIntro()) return;
                Comprobar(Object.FindAnyObjectByType<AudioListener>() != null, "la Intro tiene AudioListener");
                Comprobar(AM.MusicaActual == "musica_historia" && AM.FuenteMusica.isPlaying && AM.FuenteMusica.clip.name == "musica_historia",
                          $"la música cambia a musica_historia ({AM.MusicaActual})");
                Siguiente(0.5f);
                break;
            case 3:
                if (!EsperarA(() => Texto().maxVisibleCharacters >= 8, 6, "que empiece a escribirse la viñeta 1")) return;
                Log("  viñeta 1 (se deja escribir sola)");
                Comprobar(Veces("historia_pop") == 1, $"\"pop\" con el titular de la viñeta 1 ({Veces("historia_pop")})");
                Comprobar(!TextoCompleto() && Tics > 0, $"tic mientras se escribe ({Tics} tics, {Texto().maxVisibleCharacters}/{Texto().textInfo.characterCount} letras)");
                Siguiente(0.1f);
                break;
            case 4:
            {
                if (!EsperarListo(1)) return;
                int esperados = TicsEsperados();
                Comprobar(Tics >= esperados * 0.4f && Tics <= esperados, $"tics de la viñeta 1: {Tics} (cada 2 letras sin espacios serían {esperados}; uno por cuadro como mucho)");
                float vol = AudioManager.VolumenMusica * AM.VolumenPropio("musica_historia");
                Comprobar(Mathf.Abs(AM.FuenteMusica.volume - vol) < 0.01f && AM.Atenuacion == 1f,
                          $"la música de historia suena a {AM.FuenteMusica.volume:0.00} (esperado {vol:0.00}; 0.30 con el volumen por defecto)");
                Tecla(Key.Space);
                Siguiente(0.05f);
                break;
            }
            case 5:
                if (!Cuadros()) return;
                Soltar();
                Comprobar(Veces("historia_clic") == 1, "Espacio pasa de viñeta con el \"clic\"");
                Siguiente(0.25f);
                break;
            case 6:
                Comprobar(AM.Atenuacion <= 0.75f && AM.Atenuacion >= 0.69f, $"durante el fade la música baja ({AM.Atenuacion:0.00} de 0.70)");
                Paso = 60;
                break;
            case 60:
                if (!EsperarA(() => Imagen().sprite.name == Orden[1] && Texto().maxVisibleCharacters > 4, 5, "la viñeta 2")) return;
                Paso = 7;
                Esperar(0.05f);
                break;
            case 7:
                Log("  viñeta 2: Espacio a mitad del texto");
                Comprobar(Veces("historia_pop") == 2, "\"pop\" con el titular de la viñeta 2");
                Comprobar(!TextoCompleto(), "viñeta 2 a mitad de escribirse");
                Tecla(Key.Space);
                Siguiente(0.05f);
                break;
            case 8:
                if (!Cuadros()) return;
                Soltar();
                Comprobar(TextoCompleto() && AM.Sonando("maquina_tic") == 0, $"texto completo de golpe: el tic se calla ({AM.Sonando("maquina_tic")} sonando)");
                Guardar("tics", Tics);
                Siguiente(0.4f);
                break;
            case 9:
                Comprobar(Tics == Dato("tics") && AM.Sonando("maquina_tic") == 0, "no hay más tics con el texto completo");
                Comprobar(Veces("historia_clic") == 1, "completar el texto no hace \"clic\" (no cambia de viñeta)");
                Comprobar(AM.Atenuacion == 1f, $"la música vuelve a su volumen tras el fade ({AM.Atenuacion:0.00})");
                ClicRaton();
                SessionState.SetInt(P + "cuadro", Time.frameCount);
                Siguiente(0.05f);
                break;
            case 10:
                if (!Cuadros()) return;
                SoltarRaton();
                Comprobar(Veces("historia_clic") == 2, "clic del ratón pasa de viñeta con el \"clic\"");
                Siguiente(0.1f);
                break;
            case 11:
                if (!EsperarListo(3)) return;
                Comprobar(Veces("historia_pop") == 3, "\"pop\" con el titular de la viñeta 3");
                Tecla(Key.Space);
                Siguiente(0.05f);
                break;
            case 12:
                if (!Cuadros()) return;
                Soltar();
                Siguiente(0.1f);
                break;
            case 13:
                if (!EsperarListo(4)) return;
                Comprobar(Veces("historia_remate") == 0, "sin remate antes de la viñeta 6");
                ClicRaton();
                SessionState.SetInt(P + "cuadro", Time.frameCount);
                Siguiente(0.05f);
                break;
            case 14:
                if (!Cuadros()) return;
                SoltarRaton();
                Siguiente(0.1f);
                break;
            case 15:   // la 5 (ASÍ SE JUEGA): Espacio pasa a la última
                if (!EsperarListo(5)) return;
                Comprobar(Veces("historia_remate") == 0, "sin remate en la viñeta 5");
                Tecla(Key.Space);
                Paso = 62;
                Esperar(0.05f);
                break;
            case 62:
                if (!Cuadros()) return;
                Soltar();
                Paso = 63;
                Esperar(0.1f);
                break;
            case 63:
                if (!EsperarA(() => Imagen().sprite.name == Orden[5], 5, "la viñeta 6")) return;
                Comprobar(Veces("historia_remate") == 1 && AM.Sonando("historia_remate") == 1, "el remate (stinger) suena al entrar la viñeta 6");
                Comprobar(Veces("historia_pop") == 6, "\"pop\" con el titular de la viñeta 6");
                Paso = 64;
                Esperar(0.1f);
                break;
            case 64:
                if (!EsperarListo(6)) return;
                Guardar("ini", (int)(Time.realtimeSinceStartup * 1000));
                Tecla(Key.Space);
                Paso = 16;
                Esperar(0.05f);
                break;
            case 16:
                if (!Cuadros()) return;
                Soltar();
                Comprobar(Veces("historia_clic") == 6, $"6 \"clic\" (Espacio, ratón, Espacio, ratón, Espacio, Espacio): {Veces("historia_clic")}");
                Paso = 61;
                break;
            case 61:
                // tras el fade de 0.4 s de la última viñeta: arranque de moto y la música baja sin cortarse
                if (!EsperarA(() => Veces("moto_arranque") == 1 && AM.Atenuacion < 1f, 3, "el arranque de moto y que baje la música")) return;
                Comprobar(AM.Sonando("moto_arranque") == 1, "al terminar suena el arranque de moto");
                Comprobar(AM.MusicaActual == "musica_historia" && AM.FuenteMusica.isPlaying && AM.Atenuacion < 1f,
                          $"la música de historia sigue y baja en el fundido de salida ({AM.Atenuacion:0.00})");
                Comprobar(AM.Sonando("maquina_tic") == 0, "ningún tic sonando al salir");
                Paso = 17;
                Esperar(0.05f);
                break;
            case 17:
                if (!EsperarNivel()) return;
                Comprobar(AM.MusicaActual == "musica_nivel1_residencial", $"fundido hacia la música del nivel 1 ({AM.MusicaActual})");
                Siguiente(1.2f);
                break;
            case 18:
                Comprobar(Mathf.Abs(AM.FuenteMusica.volume - AudioManager.VolumenMusica) < 0.01f && AM.Atenuacion == 1f,
                          $"nivel 1 con su música a volumen normal ({AM.FuenteMusica.volume:0.00})");
                Comprobar(AM.Sonando("maquina_tic") == 0, "el tic no se queda sonando en el nivel");

                // ------------------------------------------------------------ B) pausa → Menú → Sí
                Log("B) Pausa → \"Menú\" → \"Sí\"");
                Clic("Canvas/BotonPausa");
                Siguiente(0.3f);
                break;
            case 19:
                Clic("Canvas/PanelPausa/PausaMenu");
                Siguiente(0.3f);
                break;
            case 20:
                Clic("Canvas/PanelPausa/PausaConfirmar/PausaConfirmarSi");
                Siguiente(0.3f);
                break;
            case 21:
                if (!EsperarMenu()) return;
                Siguiente(1.2f);
                break;
            case 22:
                Comprobar(AM.MusicaActual == "musica_menu" && AM.FuenteMusica.isPlaying
                          && Mathf.Abs(AM.FuenteMusica.volume - AudioManager.VolumenMusica) < 0.01f && AM.Atenuacion == 1f,
                          $"de vuelta en el menú: musica_menu a volumen normal ({AM.FuenteMusica.volume:0.00})");

                // ------------------------------------------------------------ C) Esc a mitad de la escritura
                Log("C) Iniciar juego y Esc a mitad de la viñeta 1");
                AM.ReiniciarContador();
                Clic("Canvas/BotonIniciar");
                Siguiente(0.3f);
                break;
            case 23:
                if (!EsperarIntro()) return;
                Comprobar(AM.MusicaActual == "musica_historia", "otra vez la música de historia");
                Siguiente(0.1f);
                break;
            case 24:
                if (!EsperarA(() => Veces("historia_pop") >= 1 && Tics > 0 && Texto().maxVisibleCharacters >= 12 && !TextoCompleto(), 6, "que empiece a escribirse")) return;
                Comprobar(!TextoCompleto() && Tics > 0, $"escribiendo con tic ({Tics})");
                Tecla(Key.Escape);
                Siguiente(0.05f);
                break;
            case 25:
                if (!Cuadros()) return;
                Soltar();
                Comprobar(AM.Sonando("maquina_tic") == 0, "Esc: el tic se calla");
                Comprobar(Veces("moto_arranque") == 1 && AM.Sonando("moto_arranque") == 1, "Esc: suena el arranque de moto");
                Comprobar(Veces("historia_clic") == 0, "Esc no hace \"clic\"");
                Guardar("tics", Tics);
                Siguiente(0.1f);
                break;
            case 26:
                if (!EsperarNivel()) return;
                Comprobar(AM.MusicaActual == "musica_nivel1_residencial" && Tics == Dato("tics"), $"Esc lleva al nivel 1 con su música ({AM.MusicaActual}) y sin más tics");

                // ------------------------------------------------------------ D) Música y Efectos en "no"
                Log("D) Música y Efectos en \"no\"");
                SceneManager.LoadScene("MenuPrincipal");
                Siguiente(0.3f);
                break;
            case 27:
                if (!EsperarMenu()) return;
                AudioManager.MusicaActiva = false;
                AudioManager.EfectosActivos = false;
                AM.ReiniciarContador();
                Clic("Canvas/BotonIniciar");
                Siguiente(0.3f);
                break;
            case 28:
                if (!EsperarIntro()) return;
                Siguiente(0.1f);
                break;
            case 29:
                if (!EsperarA(() => Veces("historia_pop") >= 1 && Tics > 0 && Texto().maxVisibleCharacters >= 12 && !TextoCompleto(), 6, "que empiece a escribirse")) return;
                Comprobar(AM.MusicaActual == "musica_historia" && Silencio(), $"intro: la pista cambia pero no suena nada ({Fuentes()})");
                Comprobar(Veces("historia_pop") == 1 && Tics > 0, $"(los efectos se piden: pop {Veces("historia_pop")}, tics {Tics}, y no se oyen)");
                Tecla(Key.Space);
                Siguiente(0.05f);
                break;
            case 30:
                if (!Cuadros()) return;
                Soltar();
                Comprobar(Silencio(), "Espacio: nada suena");
                Siguiente(0.2f);
                break;
            case 31:
                Tecla(Key.Escape);
                Siguiente(0.05f);
                break;
            case 32:
                if (!Cuadros()) return;
                Soltar();
                Comprobar(Veces("moto_arranque") == 1 && Silencio(), $"Esc: el arranque se pide pero no suena ({Fuentes()})");
                Siguiente(0.1f);
                break;
            case 33:
                if (!EsperarNivel()) return;
                Siguiente(1f);
                break;
            case 34:
                Comprobar(AM.MusicaActual == "musica_nivel1_residencial" && Silencio(), $"nivel 1 en silencio ({Fuentes()})");
                Paso = 90;
                EditorApplication.isPlaying = false;
                Esperar(1f);
                break;

            case 90:
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (InputSystem.GetDevice<Mouse>("RatonPrueba") is Mouse r) InputSystem.RemoveDevice(r);
                PruebaIntro.RestaurarEntrada();
                foreach (var k in ClavesGuardadas)
                {
                    var v = SessionState.GetString(P + "pp." + k, null);
                    if (v == null) continue;
                    if (v == "") PlayerPrefs.DeleteKey(k); else PlayerPrefs.SetInt(k, int.Parse(v));
                }
                PlayerPrefs.Save();
                Log($"Restaurado: dinero {PlayerPrefs.GetInt("dinero_total", 0)}, motos {PlayerPrefs.GetInt("motos_compradas", 0)}, " +
                    $"Música {(AudioManager.MusicaActiva ? "sí" : "no")}, Efectos {(AudioManager.EfectosActivos ? "sí" : "no")}");
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

    // ninguna fuente del AudioManager suena con volumen (la música puede seguir corriendo a volumen 0)
    static bool Silencio() => AM.GetComponentsInChildren<AudioSource>(true).All(s => !s.isPlaying || s.volume <= 0f);
    static string Fuentes() => string.Join(", ", AM.GetComponentsInChildren<AudioSource>(true).Where(s => s.isPlaying)
                                                   .Select(s => $"{s.name}:{(s.clip != null ? s.clip.name : "-")}@{s.volume:0.00}"));

    // cada 2 caracteres visibles, sin contar los que caen en espacio (lo que IntroHistoria pide como mucho)
    static int TicsEsperados()
    {
        var info = Texto().textInfo;
        int n = 0;
        for (int i = 1; i < info.characterCount; i += 2)
            if (!char.IsWhiteSpace(info.characterInfo[i].character)) n++;
        return n;
    }

    static IntroHistoria Intro() => Object.FindFirstObjectByType<IntroHistoria>();
    static T Campo<T>(string nombre) where T : Object => new SerializedObject(Intro()).FindProperty(nombre).objectReferenceValue as T;
    static TextMeshProUGUI Texto() => Campo<TextMeshProUGUI>("texto");
    static Image Imagen() => Campo<Image>("imagen");
    static GameObject Indicador() => Campo<GameObject>("indicadorSiguiente");
    static bool TextoCompleto() => Texto().maxVisibleCharacters >= Texto().textInfo.characterCount && Texto().textInfo.characterCount > 0;

    static bool EsperarMenu() => EsperarA(() => Escena == "MenuPrincipal" && Time.timeSinceLevelLoad > 0.5f && EventSystem.current != null
                                                && Buscar("Canvas/BotonIniciar") != null, 15, "el menú");
    static bool EsperarIntro() => EsperarA(() => SeleccionPrueba.Pasar() && Escena == "Intro" && Intro() != null, 10, "la Intro");
    // las imágenes de las 6 viñetas en orden (la 2 es la del salón, ConstruirIntro.Vinetas)
    static readonly string[] Orden = { "vineta_1", "vineta_salon", "vineta_2", "vineta_3", "vineta_4", "vineta_5" };
    // la viñeta 4 tiene su versión de la repartidora (Progreso.Genero)
    static bool EsDe(int vineta) => Imagen().sprite.name == Orden[vineta - 1] || Imagen().sprite.name == Orden[vineta - 1] + "_repartidora";
    static bool EsperarListo(int vineta) => EsperarA(() => EsDe(vineta) && TextoCompleto() && Indicador().activeInHierarchy,
                                                     12, $"que se escriba la viñeta {vineta}");
    static bool EsperarNivel() => EsperarA(() => Escena == "SampleScene" && GameManager.Instance != null && Time.timeSinceLevelLoad > 0.2f, 15, "el nivel 1");

    static bool EsperarA(Func<bool> condicion, float limite, string que)
    {
        float ahora = (float)EditorApplication.timeSinceStartup;
        if (SessionState.GetFloat(P + "desde", 0) == 0) SessionState.SetFloat(P + "desde", ahora);
        bool listo = false;
        try { listo = condicion(); } catch (NullReferenceException) { } catch (InvalidOperationException) { }
        if (!listo && ahora - SessionState.GetFloat(P + "desde", 0) < limite) { Esperar(0.03f); return false; }
        if (!listo) Fallo($"se acabó el tiempo esperando {que}");
        SessionState.SetFloat(P + "desde", 0);
        return true;
    }

    // ---- entrada real por el Input System; cada pulsación dura al menos 2 cuadros (si bajar y soltar caen en el mismo
    // cuadro, wasPressedThisFrame no la ve)
    static void Tecla(Key k)
    {
        PruebaIntro.EnfocarGameView();
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k));
        SessionState.SetInt(P + "cuadro", Time.frameCount);
    }
    static void Soltar() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
    static bool Cuadros()
    {
        if (Time.frameCount >= SessionState.GetInt(P + "cuadro", 0) + 2) return true;
        Esperar(0.02f);
        return false;
    }
    // ratón virtual que se vuelve Mouse.current (el del editor pisaría el clic); el puntero abajo a la izquierda, sin botones
    static Mouse Raton()
    {
        var r = InputSystem.GetDevice<Mouse>("RatonPrueba") ?? InputSystem.AddDevice<Mouse>("RatonPrueba");
        r.MakeCurrent();
        return r;
    }
    static void ClicRaton() => InputSystem.QueueStateEvent(Raton(), new MouseState { position = new Vector2(20, 20) }.WithButton(MouseButton.Left));
    static void SoltarRaton() => InputSystem.QueueStateEvent(Raton(), new MouseState { position = new Vector2(20, 20) });

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
}
