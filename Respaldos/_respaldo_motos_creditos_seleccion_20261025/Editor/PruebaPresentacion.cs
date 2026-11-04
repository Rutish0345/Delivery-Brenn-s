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
// A) Presentación "El mirador": a los 0.5 s (entrando desde negro, neón apagado), a los 2 s (neón encendido tras 2
//    parpadeos) y a los 6 s (ella señala; ventanas, sol, luciérnagas y destello vivos; menos de 60 objetos animados y
//    60 FPS); el título y "HAZ CLIC PARA CONTINUAR" sin nada encima y los personajes nítidos (x4, Point). Un clic del
//    ratón justo encima de "Iniciar juego" quita la presentación (fundido, clic sonando, la música del menú sigue) sin
//    disparar el botón: el PanelMenu sin caja (degradado a la derecha y la columna de letreros en x = 1500). Hover en
//    "Instrucciones" (borde y texto dorados, la tijera parpadeando, pulsado baja 3 px), teclado (la primera flecha elige
//    "Iniciar juego", flechas y Enter abren y cierran las instrucciones) y el icono de música apagado con su raya roja.
// B) Volver al menú desde la pantalla de niveles y desde un nivel (Pausa → Menú → Sí): directo al PanelMenu.
// C) Final de 2 estrellas: las 4 viñetas con Espacio → Creditos: textos con tildes, colores, música de historia, entra desde abajo sin pausa a ~170 px/s, x4 con Espacio
//    y x3 con Espacio sostenido; Esc salta al menú (directo al PanelMenu).
// D) Final de 3 estrellas → Creditos hasta el final: el último bloque detenido al 45% con 120 px o más de aire, sin tocar la
//    franja ni el aviso "PRESIONA ESPACIO PARA VOLVER AL MENÚ" (se comparan los rectángulos), y Espacio vuelve al menú.
// E) El final amable no lleva a los créditos. F) "Salir" detiene el Play. G) Otro Play: la presentación otra vez.
// Capturas: Presentacion_0.5s, Presentacion_2s, Presentacion_6s, Menu, Menu_hover_instrucciones, Instrucciones,
// Menu_musica_apagada, Creditos_inicio, Creditos_mitad, Creditos_final.
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
    static PresentacionMirador Mirador => Object.FindFirstObjectByType<PresentacionMirador>();
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
            case 1:   // a los 0.5 s: entrando desde negro, el letrero apagado y ella esperando
            {
                Application.runInBackground = true;
                if (!EsperarA(() => Escena == "MenuPrincipal" && Menu != null && Mirador != null && Mirador.Tiempo >= 0.5f, 15, "el menú")) return;
                Log("A) Presentación: El mirador");
                var m = Menu;
                var mir = Mirador;
                var pres = Buscar("Canvas/Presentacion");
                var panel = Buscar("Canvas/PanelMenu").GetComponent<CanvasGroup>();
                Comprobar(m.EnPresentacion && pres.gameObject.activeInHierarchy && !MenuPrincipal.PresentacionVista,
                          $"al abrir se ve la presentación (foco {Application.isFocused})");
                Comprobar(panel.alpha == 0f && !panel.interactable && !panel.blocksRaycasts, "PanelMenu invisible y sin clics (nombre, botones y sonido ocultos)");
                var texto = Buscar("Canvas/Presentacion/TextoContinuar").GetComponent<TMP_Text>();
                Comprobar(texto.text == ConstruirMenuPrincipal.TextoContinuar && texto.GetComponent<Parpadeo>() != null
                          && pres.GetChild(pres.childCount - 1) == texto.transform && Buscar("Canvas/Presentacion/Franja") != null,
                          $"\"{texto.text}\" parpadeando sobre la franja oscura (último hijo: nada lo tapa)");
                Comprobar(Buscar("Canvas/TituloGrupo/Titulo")?.GetComponent<TMP_Text>().text == "Delivery Brenn's"
                          && Buscar("Canvas/TituloGrupo/Subtitulo")?.GetComponent<TMP_Text>().text == ConstruirMenuPrincipal.TextoSubtitulo
                          && Buscar("Canvas/TituloGrupo").GetSiblingIndex() > Buscar("Canvas/Fondo").GetSiblingIndex(),
                          "el título y \"ENTREGAS CON ESTILO\" delante del mirador");
                Comprobar(mir.AlfaNegro > 0.2f && mir.AlfaNegro < 0.9f, $"fundido desde negro en curso (alfa {mir.AlfaNegro:0.00} a los {mir.Tiempo:0.0} s)");
                Comprobar(!mir.LetreroEncendido && !Buscar("Canvas/Fondo/CiudadCerca/HaloNeon").GetComponent<Image>().enabled, "el neón todavía apagado");
                Comprobar(!mir.Senalando, "la repartidora espera con los brazos abajo");
                Comprobar(BotonBajo(Pantalla("Canvas/PanelMenu/BotonIniciar")) == null, "sobre \"Iniciar juego\" no hay ningún botón que reciba el clic");
                Comprobar(AM != null && AM.MusicaActual == "musica_menu", $"suena la música del menú ({AM?.MusicaActual})");
                foreach (var n in new[] { "Canvas/Fondo/Repartidor", "Canvas/Fondo/Repartidora" })
                {
                    var img = Buscar(n).GetComponent<Image>();
                    var k = img.rectTransform.sizeDelta / img.sprite.rect.size;
                    var p = img.rectTransform.anchoredPosition;
                    Comprobar(k == new Vector2(4, 4) && img.sprite.texture.filterMode == FilterMode.Point && p.x % 4 == 0 && p.y % 4 == 0,
                              $"{img.name} nítido: {img.sprite.rect.width}x{img.sprite.rect.height} a x4 ({img.rectTransform.sizeDelta.x}x{img.rectTransform.sizeDelta.y}), Point, en la rejilla");
                }
                Guardar("cuadros", Time.frameCount);
                Guardar("tCuadros", (float)EditorApplication.timeSinceStartup);   // el reloj del editor (el del juego no vale aquí)
                Guardar("solY", Buscar("Canvas/Fondo/Sol").GetComponent<RectTransform>().anchoredPosition.y);
                Guardar("lucX", Buscar("Canvas/Fondo/Luciernaga0").GetComponent<RectTransform>().anchoredPosition.x
                                + Buscar("Canvas/Fondo/Luciernaga1").GetComponent<RectTransform>().anchoredPosition.y);
                Capturar("Presentacion_0.5s.png");
                Ir(81, 0f);
                break;
            }
            case 81:   // a los 2 s: el neón ya se encendió (2 parpadeos) y el fundido terminó
            {
                if (!EsperarA(() => Mirador.Tiempo >= 2f, 10, "los 2 s")) return;
                var mir = Mirador;
                Comprobar(mir.AlfaNegro == 0f, "el fundido desde negro terminó (1.2 s)");
                Comprobar(mir.LetreroEncendido && mir.Encendidos == 3 && Buscar("Canvas/Fondo/CiudadCerca/HaloNeon").GetComponent<Image>().enabled,
                          $"el neón \"SALÓN BRENN'S\" encendido con su halo, después de 2 parpadeos ({mir.Encendidos} encendidos)");
                Capturar("Presentacion_2s.png");
                Ir(82, 0f);
                break;
            }
            case 82:   // a los 6 s: ella señala el letrero; ventanas, luciérnagas, sol y destello vivos
            {
                if (!EsperarA(() => Mirador.Tiempo >= 6f, 10, "los 6 s")) return;
                var mir = Mirador;
                Comprobar(mir.Senalando, "la repartidora levantó el brazo y señala el letrero");
                Comprobar(mir.CambiosDeVentana >= 3, $"ventanas que se encienden y apagan ({mir.CambiosDeVentana})");
                Comprobar(mir.Destellos >= 1, $"el destello recorrió el título ({mir.Destellos})");
                float solY = Buscar("Canvas/Fondo/Sol").GetComponent<RectTransform>().anchoredPosition.y;
                Comprobar(solY < Dato("solY"), $"el sol baja ({Dato("solY")} → {solY})");
                float luc = Buscar("Canvas/Fondo/Luciernaga0").GetComponent<RectTransform>().anchoredPosition.x
                            + Buscar("Canvas/Fondo/Luciernaga1").GetComponent<RectTransform>().anchoredPosition.y;
                Comprobar(!Mathf.Approximately(luc, Dato("lucX")), "las luciérnagas flotan");
                Comprobar(mir.ObjetosAnimados < ConstruirMenuPrincipal.MaxObjetosAnimados,
                          $"{mir.ObjetosAnimados} objetos animados (menos de {ConstruirMenuPrincipal.MaxObjetosAnimados})");
                float fps = (Time.frameCount - Dato("cuadros")) / ((float)EditorApplication.timeSinceStartup - Dato("tCuadros"));
                Comprobar(fps >= 55f, $"{fps:0} FPS de los 0.5 a los 6 s (60 o más)");
                Capturar("Presentacion_6s.png");
                Ir(2, 0.1f);
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
                Raton(Lejos, false);   // el ratón se va: la captura del menú sin nada elegido
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
                foreach (var b in new[] { "BotonIniciar", "BotonInstrucciones", "BotonSalir", "CampoNombre", "MenuSonido" })
                    Comprobar(Buscar("Canvas/PanelMenu/" + b)?.gameObject.activeInHierarchy == true, $"PanelMenu/{b} visible");
                var degradado = Buscar("Canvas/PanelMenu/Degradado").GetComponent<Image>();
                Comprobar(degradado.sprite.name == "degradado_menu" && Mathf.Approximately(degradado.rectTransform.anchorMin.x * 1920, ConstruirMenuPrincipal.DegradadoDesde)
                          && Buscar("Canvas/PanelMenu/Caja") == null && Buscar("Canvas/PanelMenu/Velo") == null,
                          $"sin caja ni velo: el degradado de x = {ConstruirMenuPrincipal.DegradadoDesde} al borde");
                var zona = new Rect(ConstruirMenuPrincipal.DegradadoDesde - 960, -540, 1920 - ConstruirMenuPrincipal.DegradadoDesde, 1080);
                Comprobar(new[] { "Canvas/Fondo/Repartidor", "Canvas/Fondo/Repartidora", "Canvas/Fondo/CiudadCerca/LetreroNeon", "Canvas/Fondo/CiudadCerca/HaloNeon" }
                              .All(n => !zona.Overlaps(ConstruirMenuPrincipal.EnCanvas(Buscar(n)))),
                          "el degradado no tapa a los personajes ni al letrero");
                foreach (var b in new[] { "EtiquetaNombre", "CampoNombre", "BotonIniciar", "BotonInstrucciones", "BotonSalir" })
                {
                    var r = ConstruirMenuPrincipal.EnCanvas(Buscar("Canvas/PanelMenu/" + b));
                    float anchoEsperado = b == "EtiquetaNombre" ? ConstruirMenuPrincipal.AnchoPlaca : b == "BotonIniciar" ? ConstruirMenuPrincipal.AnchoPrincipal
                                       : b == "CampoNombre" ? ConstruirMenuPrincipal.AnchoPlaca - 2 * ConstruirMenuPrincipal.MargenPlaca : ConstruirMenuPrincipal.AnchoBoton;
                    Comprobar(Mathf.Approximately(r.center.x, ConstruirMenuPrincipal.ColumnaX) && Mathf.Approximately(r.width, anchoEsperado) && r.xMin >= zona.xMin,
                              $"{b}: {r.width}x{r.height} centrado en x = {r.center.x + 960} (sobre el degradado)");
                }
                var sonido = ConstruirMenuPrincipal.EnCanvas(Buscar("Canvas/PanelMenu/MenuSonido"));
                Comprobar(sonido.xMax == 960 - ConstruirMenuPrincipal.MargenIconos && sonido.yMax == 540 - ConstruirMenuPrincipal.MargenIconos
                          && sonido.height == ConstruirMenuPrincipal.LadoIcono,
                          $"Música y Efectos: iconos de {ConstruirMenuPrincipal.LadoIcono} arriba a la derecha ({sonido})");
                Comprobar(EventSystem.current.currentSelectedGameObject == null, "sin nada elegido al aparecer el menú");
                Comprobar(Mirador.isActiveAndEnabled && Mirador.LetreroEncendido, "el mirador sigue vivo detrás del menú");
                Capturar("Menu.png");
                Ir(59, 0.1f);   // después de capturar, un cuadro para que el Canvas vuelva a su lugar
                break;
            }
            case 59:
                Raton(Pantalla("Canvas/PanelMenu/BotonInstrucciones"), false);   // hover
                Ir(60, 0.2f);
                break;
            // ---------------------------------------------------------------- A2) letreros: hover, teclado y sonido
            case 60:
            {
                if (!EsperarA(() => Letrero("BotonInstrucciones").Elegido && Letrero("BotonInstrucciones").TijeraVisible, 3, "la tijera de \"Instrucciones\"")) return;
                var boton = Buscar("Canvas/PanelMenu/BotonInstrucciones");
                Comprobar(boton.GetComponent<Image>().sprite.name == "boton_normal_hover"
                          && ColorUtility.ToHtmlStringRGB(boton.Find("Texto").GetComponent<TMP_Text>().color) == "FFD166",
                          "hover en \"Instrucciones\": borde y texto dorados #FFD166");
                var rt = ConstruirMenuPrincipal.EnCanvas(boton);
                var tij = (RectTransform)boton.Find("Tijera");
                Vector2 centroTijera = (Vector2)tij.position - (Vector2)boton.position;   // en pantalla, desde el centro del botón
                Comprobar(centroTijera.x < -rt.width / 2 * boton.lossyScale.x, "la tijera a la izquierda del botón");
                Capturar("Menu_hover_boton.png");
                Guardar("yBoton", ((RectTransform)boton).anchoredPosition.y);
                Ir(58, 0.1f);
                break;
            }
            case 58:
                Raton(Pantalla("Canvas/PanelMenu/BotonInstrucciones"), true);
                Ir(61, 0.1f);
                break;
            case 61:
                Comprobar(Mathf.Approximately(((RectTransform)Buscar("Canvas/PanelMenu/BotonInstrucciones")).anchoredPosition.y, Dato("yBoton") - 3),
                          $"pulsado: \"Instrucciones\" baja 3 px ({Dato("yBoton")} → {((RectTransform)Buscar("Canvas/PanelMenu/BotonInstrucciones").transform).anchoredPosition.y}, pulsado {Letrero("BotonInstrucciones").Pulsado}, {Elegido})");
                Raton(Lejos, true);   // se sale apretado: soltar fuera no dispara el botón
                Siguiente(0.1f);
                break;
            case 62:
                Raton(Lejos, false);
                Siguiente(0.2f);
                break;
            case 63:
                Comprobar(Escena == "MenuPrincipal" && Mathf.Approximately(((RectTransform)Buscar("Canvas/PanelMenu/BotonInstrucciones")).anchoredPosition.y, Dato("yBoton"))
                          && !Letrero("BotonInstrucciones").Elegido && !Letrero("BotonInstrucciones").TijeraVisible,
                          $"al soltar fuera vuelve a su sitio, sin tijera y sin disparar ({Escena}, {Elegido})");
                Tecla(Key.DownArrow);
                Siguiente(0.1f);
                break;
            case 64:
                Soltar();
                Ir(71, 0.15f);
                break;
            case 71:
                Comprobar(EventSystem.current.currentSelectedGameObject == Buscar("Canvas/PanelMenu/BotonIniciar").gameObject,
                          $"teclado: la primera flecha elige \"Iniciar juego\" ({Elegido})");
                Tecla(Key.DownArrow);
                Siguiente(0.1f);
                break;
            case 72:
                Soltar();
                Siguiente(0.15f);
                break;
            case 73:
                Comprobar(EventSystem.current.currentSelectedGameObject == Buscar("Canvas/PanelMenu/BotonInstrucciones").gameObject
                          && Letrero("BotonInstrucciones").Elegido && Buscar("Canvas/PanelMenu/BotonInstrucciones").GetComponent<Image>().sprite.name == "boton_normal_hover"
                          && Buscar("Canvas/PanelMenu/BotonIniciar").GetComponent<Image>().sprite.name == "boton_principal",
                          $"teclado: flecha abajo pasa a \"Instrucciones\" (dorado) y \"Iniciar juego\" vuelve a rosa ({Elegido})");
                Tecla(Key.Enter);
                Siguiente(0.1f);
                break;
            case 74:
                Soltar();
                Siguiente(0.4f);
                break;
            case 75:
            {
                var panelInst = Buscar("Canvas/PanelMenu/PanelInstrucciones");
                var cerrar = Buscar("Canvas/PanelMenu/PanelInstrucciones/Marco/BotonCerrar");
                Comprobar(panelInst.gameObject.activeSelf && EventSystem.current.currentSelectedGameObject == cerrar.gameObject,
                          "Enter abre las instrucciones y el foco pasa a \"Cerrar\"");
                Comprobar(Buscar("Canvas/PanelMenu/PanelInstrucciones/Marco").GetComponent<Image>().sprite.name == "panel",
                          "instrucciones en el panel oscuro con borde rosa");
                Capturar("Instrucciones.png");
                Tecla(Key.Enter);   // el teclado no depende de dónde quedó el Canvas
                Siguiente(0.1f);
                break;
            }
            case 76:
                Soltar();
                Siguiente(0.3f);
                break;
            case 77:
                Comprobar(!Buscar("Canvas/PanelMenu/PanelInstrucciones").gameObject.activeSelf
                          && EventSystem.current.currentSelectedGameObject == Buscar("Canvas/PanelMenu/BotonInstrucciones").gameObject,
                          "Enter en \"Cerrar\" cierra y el foco vuelve a \"Instrucciones\"");
                EventSystem.current.SetSelectedGameObject(null);
                Clic("Canvas/PanelMenu/MenuSonido/BotonMusica");
                Siguiente(0.2f);
                break;
            case 78:
            {
                var musica = Buscar("Canvas/PanelMenu/MenuSonido/BotonMusica").GetComponent<BotonSonido>();
                var efectos = Buscar("Canvas/PanelMenu/MenuSonido/BotonEfectos").GetComponent<BotonSonido>();
                Comprobar(!AudioManager.MusicaActiva && AM.FuenteMusica.volume == 0f && PlayerPrefs.GetInt("musica_activa") == 0 && musica.RayaVisible && !efectos.RayaVisible,
                          "música apagada: volumen 0, guardada en PlayerPrefs y la raya roja solo en la nota");
                Capturar("Menu_musica_apagada.png");
                Ir(57, 0.1f);
                break;
            }
            case 57:
                Clic("Canvas/PanelMenu/MenuSonido/BotonMusica");
                Ir(79, 0.2f);
                break;
            case 79:
                Comprobar(AudioManager.MusicaActiva && AM.FuenteMusica.volume > 0.3f && !Buscar("Canvas/PanelMenu/MenuSonido/BotonMusica").GetComponent<BotonSonido>().RayaVisible,
                          "música encendida otra vez, sin raya");
                EventSystem.current.SetSelectedGameObject(null);
                Log("B) Volver al menú desde la pantalla de niveles y desde un nivel");
                Clic("Canvas/PanelMenu/BotonIniciar");
                Ir(5, 0.5f);
                break;
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
                {
                    // ya entró desde abajo, sin pausa: lo que lleva adentro de la ventana es lo de ~170 px/s desde el primer cuadro
                    var primera = (RectTransform)Buscar("Canvas/Ventana/Contenido").GetChild(0);
                    float adentro = Cred.EnCanvas(primera).yMax - Cred.Ventana.yMin;
                    float t = Time.timeSinceLevelLoad;
                    var bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    Comprobar(adentro > 0 && adentro >= ConstruirCreditos.Velocidad * (t - 0.5f) - 10f,
                              $"el texto entra desde abajo sin pausa: {adentro:0} px adentro a los {t:0.0} s (desde Start {Cred.Transcurrido:0.00} s; " +
                              $"desde {typeof(Creditos).GetField("desde", bf).GetValue(Cred)}, hasta {typeof(Creditos).GetField("hasta", bf).GetValue(Cred)}, " +
                              $"contenido {((RectTransform)Buscar("Canvas/Ventana/Contenido")).anchoredPosition.y:0}, ventana {EstiloHUD.Texto(Cred.Ventana)}, primera {EstiloHUD.Texto(Cred.EnCanvas(primera))})");
                    Comprobar(Buscar("Canvas/Pista").gameObject.activeInHierarchy && !Buscar("Canvas/Indicador").gameObject.activeInHierarchy,
                              $"mientras sube, la franja solo dice \"{ConstruirCreditos.Pista}\"");
                    Capturar("Creditos_inicio.png");
                }
                Guardar("y", Cred.Avance);
                Guardar("t", Time.unscaledTime);
                Siguiente(1.5f);
                break;
            case 17:   // velocidad normal
            {
                float v = Velocidad();
                Comprobar(Mathf.Abs(v - ConstruirCreditos.Velocidad) < ConstruirCreditos.Velocidad * 0.15f, $"sube a {v:0} px/s ({ConstruirCreditos.Velocidad})");
                Capturar("Creditos_mitad.png");
                Tecla(Key.Space);   // sostenido
                Guardar("y", Cred.Avance);
                Guardar("t", Time.unscaledTime);
                Siguiente(0.5f);
                break;
            }
            case 18:
            {
                float v = Velocidad();
                Soltar();
                float rapida = ConstruirCreditos.Velocidad * ConstruirCreditos.Aceleracion;
                Comprobar(Mathf.Abs(v - rapida) < rapida * 0.15f, $"con Espacio sostenido, x{ConstruirCreditos.Aceleracion}: {v:0} px/s ({rapida})");
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
                {
                    // el último bloque detenido no toca la franja ni el aviso: se comparan los rectángulos
                    var bloque = Cred.UltimoBloque();
                    var franja = Cred.Franja;
                    var aviso = Cred.EnCanvas((RectTransform)ind);
                    var pantalla = ((RectTransform)Cred.transform).rect;
                    float aire = bloque.yMin - franja.yMax;
                    float centro = (bloque.center.y - pantalla.yMin) / pantalla.height;
                    Comprobar(!bloque.Overlaps(aviso) && !bloque.Overlaps(franja) && aire >= ConstruirCreditos.AireMinimo,
                              $"el último bloque {EstiloHUD.Texto(bloque)} no toca la franja {EstiloHUD.Texto(franja)} ni el aviso {EstiloHUD.Texto(aviso)}: {aire:0} px de aire (≥ {ConstruirCreditos.AireMinimo})");
                    Comprobar(Mathf.Abs(centro - ConstruirCreditos.AlturaFinal) < 0.01f, $"se detiene con su centro al {centro:P0} de la altura (45%)");
                    Comprobar(franja.Contains(aviso.center) && aviso.yMin >= franja.yMin - 0.5f && aviso.yMax <= franja.yMax + 0.5f && !Buscar("Canvas/Pista").gameObject.activeInHierarchy,
                              "el aviso va dentro de la franja fija (y la pista se apaga)");
                    Comprobar(Cred.Ventana.yMin >= franja.yMax, "la ventana del texto (con su máscara) termina antes de la franja");
                }
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
        // el ratón real del editor también mueve el hover de la UI: apagado mientras dura la prueba (case 90 lo prende)
        foreach (var real in InputSystem.devices.OfType<Mouse>().Where(m => m.name != "RatonPrueba" && m.enabled).ToList())
            InputSystem.DisableDevice(real);
        var r = InputSystem.GetDevice<Mouse>("RatonPrueba") ?? InputSystem.AddDevice<Mouse>("RatonPrueba");
        r.MakeCurrent();
        var estado = new MouseState { position = donde };
        InputSystem.QueueStateEvent(r, abajo ? estado.WithButton(MouseButton.Left) : estado);
    }

    static readonly Vector2 Lejos = new Vector2(120, 120);   // abajo a la izquierda: ningún botón
    static string Elegido => EventSystem.current?.currentSelectedGameObject?.name ?? "nada elegido";
    static BotonBrenn Letrero(string boton) => Buscar("Canvas/PanelMenu/" + boton).GetComponent<BotonBrenn>();

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
