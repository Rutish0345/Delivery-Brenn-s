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

// Prueba en Play de la presentación del menú y de los créditos, desde MenuPrincipal (como el build), con entrada real
// del Input System:
// A) Presentación: solo la ilustración, el título y "HAZ CLIC PARA CONTINUAR" (PanelMenu invisible y sin clics). Un clic
//    del ratón justo encima de "Iniciar juego" quita la presentación (fundido, clic sonando, la música del menú sigue)
//    sin disparar el botón.
// B) Volver al menú desde la pantalla de niveles y desde un nivel (Pausa → Menú → Sí): directo al PanelMenu.
// C) Final de 2 estrellas: las 4 viñetas con Espacio → Creditos: textos con tildes, colores, música de historia, ~60 px/s
//    y x3 con Espacio sostenido; Esc salta al menú (directo al PanelMenu).
// D) Final de 3 estrellas → Creditos hasta el final: "PRESIONA ESPACIO PARA VOLVER AL MENÚ" y Espacio vuelve al menú.
// E) El final amable no lleva a los créditos. F) "Salir" detiene el Play. G) Otro Play: la presentación otra vez.
// Capturas: Presentacion, Menu, Creditos, Creditos_final.
// Temp/PruebaPresentacion.pedido (dentro: carpeta para las capturas) → Temp/PruebaPresentacion.resultado
[InitializeOnLoad]
public static class PruebaPresentacion
{
    const string Pedido = "Temp/PruebaPresentacion.pedido";
    const string Resultado = "Temp/PruebaPresentacion.resultado";
    const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";
    const string P = "PruebaPresentacion.";

    static bool enganchado;

    static PruebaPresentacion()
    {
        if (enganchado) return;
        enganchado = true;
        EditorApplication.update += Revisar;
        Application.logMessageReceived += Anotar;
    }

    static int Paso { get => SessionState.GetInt(P + "paso", 0); set => SessionState.SetInt(P + "paso", value); }
    static int Vineta { get => SessionState.GetInt(P + "vineta", 1); set => SessionState.SetInt(P + "vineta", value); }
    static int Meta { get => SessionState.GetInt(P + "meta", 2); set => SessionState.SetInt(P + "meta", value); }
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
    static void Ir(int paso, float espera) { Paso = paso; Esperar(espera); }
    static string Escena => SceneManager.GetActiveScene().name;
    static AudioManager AM => AudioManager.Instance;
    static MenuPrincipal Menu => Object.FindFirstObjectByType<MenuPrincipal>();
    static Creditos Cred => Object.FindFirstObjectByType<Creditos>();
    static void Guardar(string k, float v) => SessionState.SetFloat(P + k, v);
    static float Dato(string k) => SessionState.GetFloat(P + k, 0);

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
            SessionState.SetString(P + "musica", AudioManager.MusicaActiva + "|" + AudioManager.EfectosActivos);
            var inicio = EditorSceneManager.playModeStartScene;
            SessionState.SetString(P + "inicio", inicio != null ? AssetDatabase.GetAssetPath(inicio) : "");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaMenu);
            var faltan = RevisarCanvas.RevisarEscenasDeJuego();
            foreach (var f in faltan) Fallo(f);
            if (faltan.Count == 0) Log("  ok: RevisarCanvas: menú (presentación y PanelMenu), créditos, niveles y escenas de juego completos");
            var orden = EditorBuildSettings.scenes.Where(e => e.enabled).Select(e => Path.GetFileNameWithoutExtension(e.path)).ToList();
            Comprobar(orden.IndexOf("Creditos") == orden.IndexOf("Final") + 1 && orden.IndexOf("Final") > 0, $"Build Profiles: {string.Join(", ", orden)}");
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
        if (Paso < 30 && !EditorApplication.isPlaying) { Fallo($"Play se cerró en el paso {Paso}"); Paso = 90; return; }
        switch (Paso)
        {
            // ---------------------------------------------------------------- A) presentación
            case 1:
            {
                Application.runInBackground = true;
                if (!EsperarA(() => Escena == "MenuPrincipal" && Menu != null && Time.timeSinceLevelLoad > 0.6f, 15, "el menú")) return;
                Log("A) Presentación");
                var m = Menu;
                var pres = Buscar("Canvas/Presentacion");
                var panel = Buscar("Canvas/PanelMenu").GetComponent<CanvasGroup>();
                Comprobar(m.EnPresentacion && pres.gameObject.activeInHierarchy && !MenuPrincipal.PresentacionVista, "al abrir se ve la presentación");
                Comprobar(panel.alpha == 0f && !panel.interactable && !panel.blocksRaycasts, "PanelMenu invisible y sin clics (nombre, botones y sonido ocultos)");
                var texto = Buscar("Canvas/Presentacion/TextoContinuar").GetComponent<TMP_Text>();
                Comprobar(texto.text == ConstruirMenuPrincipal.TextoContinuar && texto.GetComponent<Parpadeo>() != null, $"\"{texto.text}\" parpadeando");
                Comprobar(Buscar("Canvas/TituloGrupo/Titulo")?.GetComponent<TMP_Text>().text == "Delivery Brenn's" && Buscar("Canvas/TituloGrupo").GetComponent<Flotar>().enabled, "el título flotando");
                Comprobar(BotonBajo(Pantalla("Canvas/PanelMenu/BotonIniciar")) == null, "sobre \"Iniciar juego\" no hay ningún botón que reciba el clic");
                Comprobar(AM != null && AM.MusicaActual == "musica_menu", $"suena la música del menú ({AM?.MusicaActual})");
                Capturar("Presentacion.png");
                Siguiente(0.1f);
                break;
            }
            case 2:   // clic real del ratón encima de "Iniciar juego"
                AM.ReiniciarContador();
                Guardar("tMusica", AM.FuenteMusica.time);
                PruebaIntro.EnfocarGameView();
                Raton(Pantalla("Canvas/PanelMenu/BotonIniciar"), true);
                Siguiente(0.7f);   // el fundido es de 0.4 s: se suelta ya con el menú visible
                break;
            case 3:
                Raton(Pantalla("Canvas/PanelMenu/BotonIniciar"), false);
                Siguiente(0.4f);
                break;
            case 4:
            {
                var m = Menu;
                Comprobar(Escena == "MenuPrincipal", $"el clic no disparó \"Iniciar juego\" (sigue en {Escena})");
                Comprobar(m.MenuVisible && !Buscar("Canvas/Presentacion").gameObject.activeSelf && MenuPrincipal.PresentacionVista, "el fundido deja el PanelMenu visible y con clics");
                Comprobar(AM.VecesSonado("ui_clic") >= 1, $"suena el clic ({AM.VecesSonado("ui_clic")})");
                Comprobar(AM.MusicaActual == "musica_menu" && AM.FuenteMusica.isPlaying && AM.FuenteMusica.time > Dato("tMusica"),
                          $"la música del menú sigue sin cortarse ({Dato("tMusica"):0.0} → {AM.FuenteMusica.time:0.0} s)");
                foreach (var b in new[] { "BotonIniciar", "BotonInstrucciones", "BotonSalir", "CampoNombre" })
                    Comprobar(Buscar("Canvas/PanelMenu/" + b)?.gameObject.activeInHierarchy == true, $"PanelMenu/{b} visible");
                Capturar("Menu.png");
                Log("B) Volver al menú desde la pantalla de niveles y desde un nivel");
                Clic("Canvas/PanelMenu/BotonIniciar");
                Siguiente(0.5f);
                break;
            }
            case 5:
                if (!EsperarA(() => SeleccionPrueba.Pasar() && Escena == "Intro" && Time.timeSinceLevelLoad > 0.5f, 10, "Seleccion → Intro")) return;
                Clic("Canvas/BotonSaltar");
                Siguiente(0.5f);
                break;
            case 6:
                if (!EsperarA(() => Escena == "SeleccionNivel" && Time.timeSinceLevelLoad > 0.5f, 15, "SeleccionNivel")) return;
                Clic("Canvas/BotonMenu");
                Siguiente(0.3f);
                break;
            case 7:
                if (!EsperarA(() => Escena == "MenuPrincipal" && Menu != null && Time.timeSinceLevelLoad > 0.05f, 10, "el menú")) return;
                Comprobar(!Menu.EnPresentacion && Menu.MenuVisible && !Buscar("Canvas/Presentacion").gameObject.activeSelf,
                          "desde la pantalla de niveles: directo al PanelMenu, sin presentación");
                Clic("Canvas/PanelMenu/BotonIniciar");
                Siguiente(0.5f);
                break;
            case 8:
                if (!EsperarA(() => Escena == "SeleccionNivel" && Time.timeSinceLevelLoad > 0.5f, 10, "SeleccionNivel")) return;
                Clic("Canvas/Jugar");
                Siguiente(0.5f);
                break;
            case 9:
                if (!EsperarA(() => Escena == "SampleScene" && GameManager.Instance != null && GameManager.Instance.PuntoActual != null && Time.timeSinceLevelLoad > 0.6f, 15, "el nivel 1")) return;
                Clic("Canvas/BotonPausa");
                Siguiente(0.4f);
                break;
            case 10:
                Clic("Canvas/PanelPausa/PausaMenu");
                Siguiente(0.3f);
                break;
            case 11:
                Clic("Canvas/PanelPausa/PausaConfirmar/PausaConfirmarSi");
                Siguiente(0.4f);
                break;
            case 12:
                if (!EsperarA(() => Escena == "MenuPrincipal" && Menu != null && Time.timeSinceLevelLoad > 0.05f, 10, "el menú")) return;
                Comprobar(!Menu.EnPresentacion && Menu.MenuVisible, "desde el nivel (Pausa → Menú → Sí): directo al PanelMenu");
                Log("C) Final de 2 estrellas → créditos");
                Progreso.GuardarNivel3(2, false);
                Meta = 2;
                Vineta = 1;
                SceneManager.LoadScene("Final");
                Siguiente(0.5f);
                break;

            // ---------------------------------------------------------------- las viñetas del final (C y D)
            case 13:
            {
                if (!EsperarA(() => Escena == "Final" && Object.FindFirstObjectByType<FinalHistoria>()?.Activo != null, 10, "la escena Final")) return;
                var activo = Object.FindFirstObjectByType<FinalHistoria>().Activo;
                Comprobar(activo.name == (Meta == 3 ? "FinalEstrella" : "FinalOficial") && activo.EscenaSiguiente == ConstruirCreditos.Escena,
                          $"{activo.name}: al terminar va a {activo.EscenaSiguiente}");
                Siguiente(0.1f);
                break;
            }
            case 14:
            {
                var activo = Object.FindFirstObjectByType<FinalHistoria>().Activo;
                if (!EsperarA(() => activo.VinetaActual == Vineta && Buscar("Canvas/Contenido/IndicadorSiguiente").gameObject.activeSelf
                                    && Buscar("Canvas/Contenido").GetComponent<CanvasGroup>().alpha >= 1f, 20, $"la viñeta {Vineta}")) return;
                Tecla(Key.Space);
                Siguiente(0f);
                break;
            }
            case 15:
            {
                if (!Cuadros()) return;
                Soltar();
                var activo = Object.FindFirstObjectByType<FinalHistoria>().Activo;
                if (activo.Saliendo || Vineta >= activo.CantidadVinetas) { Ir(16, 0.2f); break; }
                Vineta++;
                Ir(14, 0.1f);
                break;
            }
            case 16:
                if (!EsperarA(() => Escena == ConstruirCreditos.Escena && Cred != null && Time.timeSinceLevelLoad > 1.2f, 15, "los créditos")) return;
                Comprobar(true, $"Espacio en GRACIAS POR JUGAR ({Meta} estrellas) lleva a los créditos");
                if (Meta == 3) { Ir(22, 0.1f); break; }
                RevisarCreditos();
                Guardar("y", Cred.Avance);
                Guardar("t", Time.unscaledTime);
                Siguiente(1.5f);
                break;
            case 17:   // velocidad normal
            {
                float v = Velocidad();
                Comprobar(Mathf.Abs(v - 60f) < 9f, $"sube a {v:0} px/s (60)");
                Capturar("Creditos.png");
                Tecla(Key.Space);   // sostenido
                Guardar("y", Cred.Avance);
                Guardar("t", Time.unscaledTime);
                Siguiente(1.2f);
                break;
            }
            case 18:
            {
                float v = Velocidad();
                Soltar();
                Comprobar(Mathf.Abs(v - 180f) < 27f, $"con Espacio sostenido, x3: {v:0} px/s");
                Comprobar(!Cred.Terminado, "todavía no termina");
                Siguiente(0.2f);
                break;
            }
            case 19:
                Tecla(Key.Escape);
                Siguiente(0f);
                break;
            case 20:
                if (!Cuadros()) return;
                Soltar();
                Siguiente(0.3f);
                break;
            case 21:
                if (!EsperarA(() => Escena == "MenuPrincipal" && Menu != null && Time.timeSinceLevelLoad > 0.05f, 10, "el menú después de Esc")) return;
                Comprobar(!Menu.EnPresentacion && Menu.MenuVisible, "Esc salta los créditos: al menú, directo al PanelMenu");
                Log("D) Final de 3 estrellas → créditos hasta el final");
                Progreso.GuardarNivel3(3, false);
                Meta = 3;
                Vineta = 1;
                SceneManager.LoadScene("Final");
                Ir(13, 0.5f);
                break;
            case 22:
            {
                Comprobar(AM.MusicaActual == "musica_historia", $"música de historia ({AM.MusicaActual})");
                Cred.Avanzar(100000f);   // hasta el final de una vez
                Siguiente(0.8f);
                break;
            }
            case 23:
            {
                var ind = Buscar("Canvas/Indicador");
                Comprobar(Cred.Terminado && ind.gameObject.activeInHierarchy && ind.GetComponent<TMP_Text>().text == ConstruirCreditos.Indicador
                          && ind.GetComponent<Parpadeo>() != null, $"al terminar: \"{ind.GetComponent<TMP_Text>().text}\" parpadeando");
                Comprobar(Escena == ConstruirCreditos.Escena, "se queda esperando (no vuelve solo)");
                Capturar("Creditos_final.png");
                Tecla(Key.Space);
                Siguiente(0f);
                break;
            }
            case 24:
                if (!Cuadros()) return;
                Soltar();
                Siguiente(0.3f);
                break;
            case 25:
                if (!EsperarA(() => Escena == "MenuPrincipal" && Menu != null && Time.timeSinceLevelLoad > 0.05f, 10, "el menú después de los créditos")) return;
                Comprobar(!Menu.EnPresentacion && Menu.MenuVisible && AM.MusicaActual == "musica_menu", "Espacio vuelve al menú, directo al PanelMenu, con su música");
                Log("E) El final amable no lleva a los créditos");
                Progreso.GuardarNivel3(1, false);
                SceneManager.LoadScene("Final");
                Siguiente(0.5f);
                break;
            case 26:
            {
                if (!EsperarA(() => Escena == "Final" && Object.FindFirstObjectByType<FinalHistoria>()?.Activo != null, 10, "la escena Final")) return;
                var activo = Object.FindFirstObjectByType<FinalHistoria>().Activo;
                Comprobar(activo.name == "FinalAmable" && activo.EscenaSiguiente == "Nivel3_Lluvia", $"{activo.name} → {activo.EscenaSiguiente}");
                SceneManager.LoadScene("MenuPrincipal");
                Siguiente(0.5f);
                break;
            }
            case 27:
                if (!EsperarA(() => Escena == "MenuPrincipal" && Menu != null && Menu.MenuVisible, 10, "el menú")) return;
                Log("F) Salir");
                Clic("Canvas/PanelMenu/BotonSalir");
                Ir(30, 0.5f);
                break;

            // ---------------------------------------------------------------- G) otro Play
            case 30:
                if (!EsperarA(() => !EditorApplication.isPlayingOrWillChangePlaymode, 10, "que \"Salir\" detenga el Play")) return;
                Comprobar(!EditorApplication.isPlaying, "\"Salir\" detiene el Play");
                Log("G) Otro Play");
                Paso = 31;
                Esperar(1f);
                EditorApplication.isPlaying = true;
                break;
            case 31:
                if (!EsperarA(() => EditorApplication.isPlaying && Escena == "MenuPrincipal" && Menu != null && Time.timeSinceLevelLoad > 0.4f, 20, "el menú en el segundo Play")) return;
                Comprobar(Menu.EnPresentacion && !Menu.MenuVisible && !MenuPrincipal.PresentacionVista, "la presentación vuelve en cada Play");
                Paso = 90;
                EditorApplication.isPlaying = false;
                Esperar(1f);
                break;

            case 90:
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                ProgresoPrueba.Limpiar();
                var raton = InputSystem.GetDevice<Mouse>("RatonPrueba");
                if (raton != null) InputSystem.RemoveDevice(raton);
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

    // ------------------------------------------------------------------ créditos

    static void RevisarCreditos()
    {
        var lineas = Buscar("Canvas/Ventana/Contenido").Cast<Transform>().Select(t => t.GetComponent<TMP_Text>()).ToList();
        var esperadas = ConstruirCreditos.Bloques.SelectMany(b => new[] { b.encabezado }.Concat(b.nombres)).ToList();
        Comprobar(lineas.Select(l => l.text).SequenceEqual(esperadas), "los créditos dicen: " + string.Join(" / ", lineas.Select(l => l.text)));
        foreach (var conTilde in new[] { "Benilde Méndez Hernández", "Karla Sofía Morales Hernández", "Mtro. Juvencio Mendoza Castelán", "Academia, Distribuidora y Salón Brenn's" })
        {
            var l = lineas.FirstOrDefault(x => x.text == conTilde);
            l?.ForceMeshUpdate();
            bool letras = l != null && l.font.HasCharacters(conTilde) && l.textInfo.characterInfo.Take(l.textInfo.characterCount).All(c => c.character == ' ' || c.textElement != null);
            Comprobar(letras, $"\"{conTilde}\" con todas sus letras (tildes) en Press Start 2P");
        }
        var enc = lineas.Where(l => ConstruirCreditos.Bloques.Any(b => b.encabezado == l.text)).ToList();
        Comprobar(enc.All(l => ColorUtility.ToHtmlStringRGB(l.color) == "FFD166") && lineas.Except(enc).All(l => ColorUtility.ToHtmlStringRGB(l.color) == "FFF1D0"),
                  "encabezados dorados #FFD166 y nombres crema #FFF1D0");
        Comprobar(lineas.All(l => l.alignment == TextAlignmentOptions.Center && FuenteProyecto.EsTitulo(l.font)), "centrados, en Press Start 2P");
        Comprobar(AM.MusicaActual == "musica_historia" && AM.FuenteMusica.loop, $"música de historia en bucle ({AM.MusicaActual}, loop {AM.FuenteMusica.loop})");
        Comprobar(!Buscar("Canvas/Indicador").gameObject.activeSelf, "el indicador espera al final");
        var estrellas = Buscar("Canvas/Estrellas").GetComponentsInChildren<Image>();
        Comprobar(estrellas.Select(e => e.color.a).Distinct().Count() > 1, "las estrellas titilan (alfas distintas)");
    }

    // unidades del Canvas por segundo desde lo guardado (Avance 0..1 sobre el recorrido)
    static float Velocidad()
    {
        var c = Cred;
        var cont = (RectTransform)Buscar("Canvas/Ventana/Contenido");
        float recorrido = (float)typeof(Creditos).GetField("hasta", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(c)
                        - (float)typeof(Creditos).GetField("desde", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(c);
        return (c.Avance - Dato("y")) * recorrido / (Time.unscaledTime - Dato("t"));
    }

    // ------------------------------------------------------------------ entrada real

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
    // ratón virtual que se vuelve Mouse.current (el del editor pisaría el clic)
    static void Raton(Vector2 donde, bool abajo)
    {
        var r = InputSystem.GetDevice<Mouse>("RatonPrueba") ?? InputSystem.AddDevice<Mouse>("RatonPrueba");
        r.MakeCurrent();
        var estado = new MouseState { position = donde };
        InputSystem.QueueStateEvent(r, abajo ? estado.WithButton(MouseButton.Left) : estado);
    }

    static Vector2 Pantalla(string ruta) => RectTransformUtility.WorldToScreenPoint(null, Buscar(ruta).position);

    static GameObject BotonBajo(Vector2 p)
    {
        var golpes = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = p }, golpes);
        return golpes.Select(g => g.gameObject.GetComponentInParent<Selectable>()).FirstOrDefault(s => s != null)?.gameObject;
    }

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
