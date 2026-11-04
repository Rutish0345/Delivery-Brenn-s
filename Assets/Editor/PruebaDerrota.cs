using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

// Prueba en Play de las quejas, el despido (GAME OVER), el nivel no superado y los tres finales, desde MenuPrincipal
// (como el build), dos rondas: con el repartidor y con la repartidora (Progreso.Genero).
//  Nivel 1 (2 quejas): HUD con 2 iconos; una entrega y una cancelación (icono lleno, sonido "queja"); Pausa →
//    Reiniciar deja el contador en 0; dos cancelaciones → despido: juego, reloj y pedidos detenidos, pantalla de
//    despido, música más baja con el jingle de derrota, el dinero del nivel no pasa a Progreso; Reintentar recarga con
//    timeScale 1 y el contador en 0. Luego 3 entregas al 40 % (1 estrella): NIVEL NO SUPERADO, sin "Siguiente nivel",
//    "Jugar de nuevo" dorado y seleccionado; Jugar de nuevo y 3 entregas al 100 % (3 estrellas) → Siguiente nivel.
//  Nivel 2 (3 quejas): tres cancelaciones → despido → "Menú" vuelve al menú sin sumar dinero; se vuelve directo al
//    nivel 2 y 5 entregas al 65 % (2 estrellas) → Siguiente nivel.
//  Nivel 3 (3 quejas): tres cancelaciones → despido (la lluvia se apaga; Progreso marca el despido) → Reintentar.
//    Después se termina tres veces, con 3, 2 y 1 estrellas (entregas al 100, 65 y 40 %): panel "¡NIVEL COMPLETADO!"
//    con "Continuar" → escena Final con el final que toca, viñeta por viñeta con Espacio (titular, imagen según
//    género y moto, música y efectos) hasta el menú (3 y 2 estrellas) o el reintento del nivel 3 (1 estrella).
//    Al final de la segunda ronda, Esc en el final amable va al menú.
// Capturas (carpeta del pedido): HUD_quejas, GameOver de cada nivel, NoSuperado / Superado, el panel del nivel 3 y
// cada viñeta de cada final, con el sufijo _repartidor / _repartidora. El avance guardado queda como estaba.
// Temp/PruebaDerrota.pedido (dentro: carpeta para las capturas) → Temp/PruebaDerrota.resultado
[InitializeOnLoad]
public static class PruebaDerrota
{
    const string Pedido = "Temp/PruebaDerrota.pedido";
    const string Resultado = "Temp/PruebaDerrota.resultado";
    const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";
    const string P = "PruebaDerrota.";
    static readonly string[] ClavesProgreso = { "genero_jugador" };
    const BindingFlags Privado = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly string[] MotoDeRonda = { "scooter_naranja", "deportiva_roja" };
    static readonly int[] IndiceMoto = { 0, 3 };

    static bool enganchado;

    static PruebaDerrota()
    {
        if (enganchado) return;
        enganchado = true;
        EditorApplication.update += Revisar;
        Application.logMessageReceived += Anotar;
    }

    static int Paso { get => SessionState.GetInt(P + "paso", 0); set => SessionState.SetInt(P + "paso", value); }
    static int Ronda { get => SessionState.GetInt(P + "ronda", 0); set => SessionState.SetInt(P + "ronda", value); }
    static int Meta { get => SessionState.GetInt(P + "meta", 3); set => SessionState.SetInt(P + "meta", value); }        // estrellas del nivel 3
    static int Vineta { get => SessionState.GetInt(P + "vineta", 1); set => SessionState.SetInt(P + "vineta", value); }
    static string G => Ronda == 0 ? "_repartidor" : "_repartidora";
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
    static GameManager GM => GameManager.Instance;
    static AudioManager AM => AudioManager.Instance;
    static LevelManager Nivel => Object.FindFirstObjectByType<LevelManager>();
    static int Veces(string efecto) => AM != null ? AM.VecesSonado(efecto) : -1;
    static void Guardar(string k, int v) => SessionState.SetInt(P + k, v);
    static int Leer(string k) => SessionState.GetInt(P + k, 0);

    static void Revisar()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (Paso == 0)
        {
            if (!File.Exists(Pedido) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetString(P + "carpeta", File.ReadAllText(Pedido).Trim());
            File.Delete(Pedido);
            Directory.CreateDirectory(SessionState.GetString(P + "carpeta", "Temp"));
            SessionState.SetString(P + "log", "");
            SessionState.SetString(P + "consola", "");
            SessionState.SetInt(P + "fallos", 0);
            SessionState.SetFloat(P + "desde", 0);
            foreach (var k in ClavesProgreso)
                SessionState.SetString(P + "pp." + k, PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k).ToString() : "");
            ProgresoPrueba.Preparar(motos: 0b1111);   // las 4 motos, solo durante la prueba
            PlayerPrefs.SetInt("genero_jugador", 0);
            PlayerPrefs.Save();
            Ronda = 0;
            var inicio = EditorSceneManager.playModeStartScene;
            SessionState.SetString(P + "inicio", inicio != null ? AssetDatabase.GetAssetPath(inicio) : "");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaMenu);
            var sinUI = RevisarCanvas.RevisarEscenasDeJuego();
            foreach (var falta in sinUI) Fallo(falta);
            if (sinUI.Count == 0) Log("  ok: RevisarCanvas: Canvas completo (quejas, PanelDespido, TextoAviso) en las 3 escenas de juego");
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
            // ================================================================ del menú al nivel 1
            case 1:
                Application.runInBackground = true;
                PruebaIntro.EntradaSinFoco();
                if (!EsperarA(() => Escena == "MenuPrincipal" && Time.timeSinceLevelLoad > 0.3f && MenuPrueba.Listo(), 15, "el menú")) return;
                Progreso.Genero = Ronda;
                Log($"\n===== RONDA {Ronda + 1}: {(Ronda == 0 ? "REPARTIDOR" : "REPARTIDORA")} =====");
                AM.ReiniciarContador();
                Progreso.OlvidarPersonaje();   // cada ronda pasa por Seleccion e Intro
                Clic("Canvas/PanelMenu/BotonIniciar");
                Siguiente(0.5f);
                break;
            case 2:
                if (!EsperarA(() => SeleccionPrueba.Pasar() && Escena == "Intro" && Time.timeSinceLevelLoad > 0.5f, 10, "la Intro")) return;
                Comprobar(Progreso.Genero == Ronda, $"Seleccion confirmó {(Ronda == 0 ? "al repartidor" : "a la repartidora")}");
                Clic("Canvas/BotonSaltar");
                Siguiente(0.5f);
                break;

            // ================================================================ nivel 1: quejas, pausa/reiniciar, despido
            case 3:
            {
                if (!EsperarA(() => NivelPrueba.Pasar() && Escena == "SampleScene" && GM != null && GM.PuntoActual != null && Time.timeSinceLevelLoad > 0.5f, 15, "el nivel 1")) return;
                Log("A) Nivel 1: fila de quejas");
                var iconos = Iconos();
                Comprobar(Nivel.QuejasMaximas == 2 && GM.QuejasMaximas == 2 && iconos.Count(i => i.gameObject.activeInHierarchy) == 2,
                          $"2 quejas como máximo y 2 iconos ({iconos.Length} en la fila)");
                Comprobar(iconos.All(i => i.sprite.name.EndsWith("queja_vacia")), "los iconos empiezan vacíos (grises)");
                var reloj = (RectTransform)Buscar("Canvas/CajaHUD/PlacaReloj");
                var fila = (RectTransform)Buscar("Canvas/CajaHUD/FilaQuejas");
                Comprobar(fila.parent == reloj.parent && fila.parent.name == "CajaHUD" && Abajo(reloj) >= Arriba(fila) && Mathf.Abs(Izquierda(reloj) - Izquierda(fila)) < 1f
                          && Buscar("Canvas/GuiaPedido") == null,
                          "en la caja del HUD, la fila va justo debajo del reloj (alineada); no hay píldora de guía");
                Guardar("dinero", Progreso.Dinero);
                Guardar("motos", Progreso.MotosCompradas);
                EntregarCon(100f);
                Comprobar(GM.Dinero > 0 && GM.Quejas == 0, $"una entrega a tiempo: ${GM.Dinero} en el nivel, sin queja");
                Cancelar();
                Siguiente(0.05f);
                break;
            }
            case 4:
            {
                if (!EsperarA(() => GM.Quejas == 1, 5, "la primera queja")) return;
                var iconos = Iconos();
                Comprobar(GM.Cancelados == 1 && iconos[0].sprite.name == "queja_llena" && iconos[1].sprite.name.EndsWith("queja_vacia"),
                          "cancelado: el primer icono se llena con la carita enojada, el segundo sigue gris");
                Comprobar(Veces("queja") == 1, $"suena \"queja\" una vez ({Veces("queja")})");
                Comprobar(!GM.FueDespedido && Time.timeScale == 1f, "con 1 de 2 el nivel sigue");
                Comprobar(iconos[0].transform.localScale.x > 1.01f, $"el icono rebota (escala {iconos[0].transform.localScale.x:0.00})");
                Siguiente(0.5f);
                break;
            }
            case 5:
                Capturar($"HUD_quejas_nivel1{G}.png");
                Log("  Pausa → Reiniciar");
                Siguiente(0.2f);
                break;
            case 6:
                Clic("Canvas/BotonPausa");
                Siguiente(0.4f);
                break;
            case 7:
                Clic("Canvas/PanelPausa/PausaReiniciar");
                Siguiente(0.5f);
                break;
            case 8:
                if (!EsperarA(() => NivelPrueba.Pasar() && Escena == "SampleScene" && GM != null && GM.PuntoActual != null && GM.Resueltos == 0 && Time.timeSinceLevelLoad > 0.4f, 10, "el nivel 1 reiniciado")) return;
                Comprobar(GM.Quejas == 0 && Iconos().All(i => i.sprite.name.EndsWith("queja_vacia")) && Time.timeScale == 1f, "Pausa → Reiniciar: contador en 0 y los iconos vacíos");
                Log("B) Nivel 1: dos cancelaciones → despido");
                Guardar("dinero", Progreso.Dinero);
                EntregarCon(100f);
                Guardar("dineroNivel", GM.Dinero);
                Cancelar();
                Siguiente(0.05f);
                break;
            case 9:
                if (!EsperarA(() => GM.Quejas == 1, 5, "la primera queja")) return;
                Cancelar();
                Siguiente(0.05f);
                break;
            case 10:
                if (!EsperarA(() => GM.FueDespedido, 5, "el despido")) return;
                ComprobarDespido(2);
                Siguiente(0.9f);   // el fundido de la pantalla
                break;
            case 11:
                ComprobarPantallaDespido();
                Capturar($"GameOver_nivel1{G}.png");
                Siguiente(0.2f);
                break;
            case 12:
                Clic("Canvas/PanelDespido/" + ConstruirQuejas.BotonReintentar);
                Siguiente(0.5f);
                break;
            case 13:
                if (!EsperarA(() => NivelPrueba.Pasar() && Escena == "SampleScene" && GM != null && GM.PuntoActual != null && !GM.FueDespedido && Time.timeSinceLevelLoad > 0.4f, 10, "el nivel 1 otra vez")) return;
                Comprobar(Time.timeScale == 1f && GM.Quejas == 0 && GM.Resueltos == 0 && !Buscar("Canvas/PanelDespido").gameObject.activeSelf
                          && Iconos().All(i => i.sprite.name.EndsWith("queja_vacia")), "Reintentar: el nivel 1 recarga con timeScale 1, contador en 0 y sin la pantalla");
                Comprobar(Progreso.Dinero == Leer("dinero"), $"Progreso sigue con ${Progreso.Dinero} (no se sumaron los ${Leer("dineroNivel")} del nivel)");
                Siguiente(0.1f);
                break;
            case 14:
                if (!EsperarA(() => AM.Atenuacion > 0.99f, 3, "que la música vuelva a su volumen")) return;
                Comprobar(AM.Atenuacion > 0.99f && AM.MusicaActual == "musica_nivel1_residencial", $"la música del nivel vuelve a su volumen ({AM.Atenuacion:0.00})");
                Log("C) Nivel 1 con 1 estrella (3 entregas al 40 %)");
                Guardar("dinero", Progreso.Dinero);
                Siguiente(0.1f);
                break;
            case 15:
                if (GM.Resueltos < GM.PedidosMeta) { EntregarCon(40f); Esperar(0.15f); return; }
                Comprobar(GM.Quejas == 0, "entregar al 40 % no es queja (el límite es 30 %)");
                Siguiente(2.5f);
                break;
            case 16:
            {
                if (!EsperarA(() => Buscar("Canvas/PanelFinal").gameObject.activeInHierarchy, 10, "el panel final")) return;
                var panel = Buscar("Canvas/PanelFinal");
                Comprobar(Estrellas() == 1, $"{Estrellas()} estrella (promedio {GM.SatisfaccionesPorPedido.Average():0})");
                Comprobar(TextoDe(panel.Find("Titulo")) == "NIVEL NO SUPERADO", $"titular \"{TextoDe(panel.Find("Titulo"))}\"");
                Comprobar(panel.Find("TextoAviso").gameObject.activeSelf && TextoDe(panel.Find("TextoAviso")) == "Necesitas al menos 2 estrellas para avanzar.",
                          $"aviso \"{TextoDe(panel.Find("TextoAviso"))}\"");
                Comprobar(!panel.Find("BotonSiguiente").gameObject.activeSelf, "sin \"Siguiente nivel\"");
                var jugar = panel.Find("BotonJugarDeNuevo");
                Comprobar(jugar.GetComponent<BotonBrenn>().Marcado && EventSystem.current.currentSelectedGameObject == jugar.gameObject,
                          "\"Jugar de nuevo\" resaltado (marcado como Principal, seleccionado)");
                Comprobar(Progreso.Dinero == Leer("dinero") + GM.Dinero, $"terminar el nivel (sin despido) sí suma el dinero: ${Progreso.Dinero}");
                Comprobar(Visible(panel.Find("Titulo")) && Visible(panel.Find("TextoAviso")), "titular y aviso caben en el panel");
                Capturar($"NoSuperado_nivel1{G}.png");
                Siguiente(0.2f);
                break;
            }
            case 17:
                Clic("Canvas/PanelFinal/BotonJugarDeNuevo");
                Siguiente(0.5f);
                break;
            case 18:
                if (!EsperarA(() => NivelPrueba.Pasar() && Escena == "SampleScene" && GM != null && GM.PuntoActual != null && GM.Resueltos == 0 && Time.timeSinceLevelLoad > 0.4f, 10, "el nivel 1 otra vez")) return;
                Log("D) Nivel 1 con 3 estrellas");
                Siguiente(0.1f);
                break;
            case 19:
                if (GM.Resueltos < GM.PedidosMeta) { EntregarCon(100f); Esperar(0.15f); return; }
                Siguiente(2.5f);
                break;
            case 20:
            {
                if (!EsperarA(() => Buscar("Canvas/PanelFinal").gameObject.activeInHierarchy, 10, "el panel final")) return;
                var panel = Buscar("Canvas/PanelFinal");
                Comprobar(Estrellas() == 3 && TextoDe(panel.Find("Titulo")) == "¡Nivel completado!" && !panel.Find("TextoAviso").gameObject.activeSelf,
                          $"{Estrellas()} estrellas: \"{TextoDe(panel.Find("Titulo"))}\", sin aviso (como antes)");
                Comprobar(panel.Find("BotonSiguiente").gameObject.activeSelf && !panel.Find("BotonJugarDeNuevo").GetComponent<BotonBrenn>().Marcado,
                          "\"Siguiente nivel\" visible y \"Jugar de nuevo\" normal (sin marcar)");
                Capturar($"Superado_nivel1{G}.png");
                Siguiente(0.2f);
                break;
            }
            case 21:
                Clic("Canvas/PanelFinal/BotonSiguiente");
                Siguiente(0.5f);
                break;

            // ================================================================ nivel 2: despido → Menú; 2 estrellas
            case 22:
                if (!EsperarEligeMoto("Nivel2_Comercial")) return;
                Log("E) Nivel 2: ELIGE TU MOTO → " + Progreso.Motos[IndiceMoto[Ronda]].nombre);
                Progreso.Equipar(IndiceMoto[Ronda]);
                Comprobar(Iconos().Count(i => i.gameObject.activeInHierarchy) == 3 && Nivel.QuejasMaximas == 3, "nivel 2: 3 iconos de queja");
                Siguiente(0.3f);
                break;
            case 23:
                ClicEmpezar();
                Siguiente(0.5f);
                break;
            case 24:
                if (!EsperarA(() => !GM.EnEspera && GM.PuntoActual != null && Time.timeScale == 1f, 10, "que empiece el nivel 2")) return;
                Guardar("dinero", Progreso.Dinero);
                Guardar("equipada", Progreso.MotoEquipada);
                EntregarCon(100f);
                Guardar("dineroNivel", GM.Dinero);
                Cancelar();
                Siguiente(0.05f);
                break;
            case 25:
                if (!EsperarA(() => GM.Quejas == 1, 5, "la primera queja")) return;
                Cancelar();
                Siguiente(0.05f);
                break;
            case 26:
                if (!EsperarA(() => GM.Quejas == 2, 5, "la segunda queja")) return;
                Comprobar(Iconos().Count(i => i.sprite.name == "queja_llena") == 2 && !GM.FueDespedido, "nivel 2: 2 de 3, el nivel sigue");
                Siguiente(0.5f);
                break;
            case 27:
                Capturar($"HUD_quejas_nivel2{G}.png");
                Cancelar();
                Siguiente(0.05f);
                break;
            case 28:
                if (!EsperarA(() => GM.FueDespedido, 5, "el despido en el nivel 2")) return;
                ComprobarDespido(3);
                Siguiente(0.9f);
                break;
            case 29:
                ComprobarPantallaDespido();
                Capturar($"GameOver_nivel2{G}.png");
                Siguiente(0.2f);
                break;
            case 30:
                Clic("Canvas/PanelDespido/" + ConstruirQuejas.BotonMenu);
                Siguiente(0.5f);
                break;
            case 31:
                if (!EsperarA(() => Escena == "MenuPrincipal" && Time.timeSinceLevelLoad > 0.3f, 10, "el menú")) return;
                Comprobar(Time.timeScale == 1f && Progreso.Dinero == Leer("dinero") && Progreso.MotoEquipada == Leer("equipada")
                          && Progreso.MotosCompradas == Leer("motos"),
                          $"\"Menú\" vuelve al menú; Progreso sin el dinero del nivel (${Progreso.Dinero}) y con las motos como estaban");
                Log("  (de vuelta al nivel 2 directo)");
                SceneManager.LoadScene("Nivel2_Comercial");
                Siguiente(0.5f);
                break;
            case 32:
                if (!EsperarEligeMoto("Nivel2_Comercial")) return;
                Siguiente(0.3f);
                break;
            case 33:
                ClicEmpezar();
                Siguiente(0.5f);
                break;
            case 34:
                if (!EsperarA(() => !GM.EnEspera && GM.PuntoActual != null && Time.timeScale == 1f, 10, "que empiece el nivel 2")) return;
                Comprobar(GM.Quejas == 0 && Iconos().All(i => i.sprite.name.EndsWith("queja_vacia")), "nivel 2 de nuevo: contador en 0");
                Log("F) Nivel 2 con 2 estrellas (5 entregas al 65 %)");
                Siguiente(0.1f);
                break;
            case 35:
                if (GM.Resueltos < GM.PedidosMeta) { EntregarCon(65f); Esperar(0.15f); return; }
                Siguiente(2.5f);
                break;
            case 36:
            {
                if (!EsperarA(() => Buscar("Canvas/PanelFinal").gameObject.activeInHierarchy, 10, "el panel final")) return;
                var panel = Buscar("Canvas/PanelFinal");
                Comprobar(Estrellas() == 2 && TextoDe(panel.Find("Titulo")) == "¡Nivel completado!" && !panel.Find("TextoAviso").gameObject.activeSelf
                          && panel.Find("BotonSiguiente").gameObject.activeSelf, $"{Estrellas()} estrellas: se supera, con \"Siguiente nivel\"");
                Capturar($"Superado_nivel2{G}.png");
                Siguiente(0.2f);
                break;
            }
            case 37:
                Clic("Canvas/PanelFinal/BotonSiguiente");
                Siguiente(0.5f);
                break;

            // ================================================================ nivel 3: despido y los tres finales
            case 38:
                if (!EsperarEligeMoto("Nivel3_Lluvia")) return;
                Log("G) Nivel 3: tres cancelaciones → despido");
                Siguiente(0.3f);
                break;
            case 39:
                ClicEmpezar();
                Siguiente(0.5f);
                break;
            case 40:
                if (!EsperarA(() => !GM.EnEspera && GM.PuntoActual != null && Time.timeScale == 1f, 10, "que empiece el nivel 3")) return;
                Comprobar(Lluvia().activeSelf && AM.FuenteLluvia.isPlaying, "llueve");
                Guardar("dinero", Progreso.Dinero);
                EntregarCon(100f);
                Guardar("dineroNivel", GM.Dinero);
                Cancelar();
                Siguiente(0.05f);
                break;
            case 41:
                if (!EsperarA(() => GM.Quejas == 1, 5, "la primera queja")) return;
                Cancelar();
                Siguiente(0.05f);
                break;
            case 42:
                if (!EsperarA(() => GM.Quejas == 2, 5, "la segunda queja")) return;
                Cancelar();
                Siguiente(0.05f);
                break;
            case 43:
                if (!EsperarA(() => GM.FueDespedido, 5, "el despido en el nivel 3")) return;
                ComprobarDespido(3);
                Comprobar(!Lluvia().activeSelf && !AM.FuenteLluvia.isPlaying, "la lluvia se detiene (capa y sonido)");
                Comprobar(Progreso.DespedidoNivel3 && Progreso.EstrellasNivel3 == 0, "Progreso: despedido en el nivel 3");
                Siguiente(0.9f);
                break;
            case 44:
                ComprobarPantallaDespido();
                Capturar($"GameOver_nivel3{G}.png");
                Siguiente(0.2f);
                break;
            case 45:
                Clic("Canvas/PanelDespido/" + ConstruirQuejas.BotonReintentar);
                Meta = 3;
                Siguiente(0.5f);
                break;

            // ---- jugar el nivel 3 hasta el final con Meta estrellas (se repite con 3, 2 y 1)
            case 46:
                if (!EsperarEligeMoto("Nivel3_Lluvia")) return;
                Siguiente(0.3f);
                break;
            case 47:
                ClicEmpezar();
                Siguiente(0.5f);
                break;
            case 48:
                if (!EsperarA(() => !GM.EnEspera && GM.PuntoActual != null && Time.timeScale == 1f, 10, "que empiece el nivel 3")) return;
                Comprobar(GM.Quejas == 0 && !GM.FueDespedido && Lluvia().activeSelf, "nivel 3 de nuevo: contador en 0 y lloviendo");
                Log($"H) Nivel 3 con {Meta} estrella(s) (8 entregas al {Satisfaccion(Meta):0} %)");
                Siguiente(0.1f);
                break;
            case 49:
                if (GM.Resueltos < GM.PedidosMeta) { EntregarCon(Satisfaccion(Meta)); Esperar(0.12f); return; }
                Siguiente(2.5f);
                break;
            case 50:
            {
                if (!EsperarA(() => Buscar("Canvas/PanelFinal").gameObject.activeInHierarchy, 10, "el panel final del nivel 3")) return;
                var panel = Buscar("Canvas/PanelFinal");
                var continuar = panel.Find("BotonSiguiente");
                Comprobar(Estrellas() == Meta && TextoDe(panel.Find("Titulo")) == "¡NIVEL COMPLETADO!" && !panel.Find("TextoAviso").gameObject.activeSelf,
                          $"{Estrellas()} estrella(s): \"{TextoDe(panel.Find("Titulo"))}\", neutro (sin aviso ni titular del puesto)");
                Comprobar(continuar.gameObject.activeSelf && TextoDe(continuar) == "Continuar" && panel.Find("BotonJugarDeNuevo").gameObject.activeSelf
                          && panel.Find("BotonMenu").gameObject.activeSelf, "\"Continuar\", \"Jugar de nuevo\" y \"Menú\"");
                Comprobar(Progreso.EstrellasNivel3 == Meta && !Progreso.DespedidoNivel3, $"Progreso: nivel 3 con {Progreso.EstrellasNivel3} estrella(s)");
                Capturar($"PanelFinal_nivel3_{Meta}estrellas{G}.png");
                Siguiente(0.2f);
                break;
            }
            case 51:
                AM.ReiniciarContador();
                Clic("Canvas/PanelFinal/BotonSiguiente");
                Vineta = 1;
                Siguiente(0.5f);
                break;
            case 52:
            {
                if (!EsperarA(() => Escena == "Final" && Object.FindFirstObjectByType<FinalHistoria>()?.Activo != null, 10, "la escena Final")) return;
                var activo = Object.FindFirstObjectByType<FinalHistoria>().Activo;
                string esperado = Meta >= 3 ? "FinalEstrella" : Meta == 2 ? "FinalOficial" : "FinalAmable";
                Comprobar(activo.name == esperado && activo.gameObject.activeInHierarchy
                          && Object.FindObjectsByType<IntroHistoria>(FindObjectsSortMode.None).Length == 1,
                          $"\"Continuar\" lleva a Final con {activo.name} (solo ese encendido)");
                Comprobar(AM.MusicaActual == "musica_historia", $"música de la historia ({AM.MusicaActual})");
                Siguiente(0.1f);
                break;
            }
            case 53:   // cada viñeta: espera a que termine de escribirse, revisa y captura
            {
                var activo = Object.FindFirstObjectByType<FinalHistoria>().Activo;
                if (!EsperarA(() => activo.VinetaActual == Vineta && Buscar("Canvas/Contenido/IndicadorSiguiente").gameObject.activeSelf
                                    && Buscar("Canvas/Contenido").GetComponent<CanvasGroup>().alpha >= 1f, 15, $"la viñeta {Vineta}")) return;
                RevisarVineta(activo);
                Capturar($"Final_{Meta}estrellas_v{Vineta}{G}.png");
                Siguiente(0.2f);
                break;
            }
            case 54:
                if (Ronda == 1 && Meta == 1 && Vineta == 1 && SessionState.GetBool(P + "esc", false))
                {
                    Tecla(Key.Escape);   // Esc en el final amable: al menú
                    Siguiente(0f);
                    break;
                }
                Tecla(Key.Space);
                Siguiente(0f);
                break;
            case 55:
            {
                if (!Cuadros()) return;
                Soltar();
                var activo = Object.FindFirstObjectByType<FinalHistoria>().Activo;
                if (activo.Saliendo || Vineta >= activo.CantidadVinetas || SessionState.GetBool(P + "esc", false))
                {
                    Ir(56, 0.1f);
                    break;
                }
                Vineta++;
                Ir(53, 0.1f);
                break;
            }
            case 56:
            {
                bool esc = SessionState.GetBool(P + "esc", false);
                string destino = esc ? "MenuPrincipal" : Meta == 1 ? "Nivel3_Lluvia" : ConstruirCreditos.Escena;
                if (!EsperarA(() => Escena == destino && Time.unscaledTime > 0f, 10, destino)) return;   // el nivel 3 abre la tienda con timeScale 0
                if (esc)
                {
                    Comprobar(true, "Esc en el final amable va al menú");
                    Ir(80, 0.1f);
                    break;
                }
                Comprobar(Vineta == ConstruirFinal.Finales[3 - Math.Max(1, Meta)].vinetas.Length,
                          Meta == 1 ? "Espacio en la última viñeta reintenta el nivel 3 (PRESIONA ESPACIO PARA REINTENTAR)"
                                    : "Espacio en GRACIAS POR JUGAR lleva a los créditos");
                if (Meta > 1)
                {
                    Meta--;
                    Log("  (otra vez al nivel 3 directo)");
                    SceneManager.LoadScene("Nivel3_Lluvia");
                    Ir(46, 0.5f);
                }
                else Ir(Ronda == 1 ? 57 : 80, 0.5f);
                break;
            }
            case 57:   // segunda ronda: el final amable otra vez y Esc
                if (!EsperarEligeMoto("Nivel3_Lluvia")) return;
                Log("I) Esc en el final amable");
                Progreso.GuardarNivel3(1, false);
                SessionState.SetBool(P + "esc", true);
                Vineta = 1;
                SceneManager.LoadScene("Final");
                Ir(52, 0.5f);
                break;

            // ================================================================ fin de la ronda
            case 80:
                if (Ronda == 0)
                {
                    Ronda = 1;
                    Time.timeScale = 1f;
                    SceneManager.LoadScene("MenuPrincipal");
                    Ir(1, 0.5f);
                    break;
                }
                Paso = 90;
                EditorApplication.isPlaying = false;
                Esperar(1f);
                break;

            case 90:
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                PruebaIntro.RestaurarEntrada();
                foreach (var k in ClavesProgreso)
                {
                    var v = SessionState.GetString(P + "pp." + k, null);
                    if (v == null) continue;
                    if (v == "") PlayerPrefs.DeleteKey(k); else PlayerPrefs.SetInt(k, int.Parse(v));
                }
                PlayerPrefs.Save();
                SessionState.SetBool(P + "esc", false);
                ProgresoPrueba.Limpiar();   // el progreso vive solo en la sesión: no hay nada que restaurar
                Log($"Restaurado: género {PlayerPrefs.GetInt("genero_jugador", 0)}");
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

    // ------------------------------------------------------------------ comprobaciones

    static void ComprobarDespido(int maximo)
    {
        var iconos = Iconos();
        Comprobar(GM.Quejas == maximo && iconos.Count(i => i.gameObject.activeInHierarchy && i.sprite.name == "queja_llena") == maximo,
                  $"{maximo} de {maximo} quejas: todos los iconos llenos");
        Comprobar(Time.timeScale == 0f && GM.NivelTerminado && GM.PuntoActual != null && !GM.PuntoActual.Activo,
                  "GAME OVER inmediato: juego detenido (timeScale 0), sin pedido activo");
        var reloj = Object.FindFirstObjectByType<RelojPedido>();
        Comprobar(reloj.EstadoActual == RelojPedido.Estado.Oculto, "el reloj se detiene y se esconde");
        var panel = Buscar("Canvas/PanelDespido");
        Comprobar(panel.gameObject.activeInHierarchy && panel.GetSiblingIndex() == panel.parent.childCount - 1, "sale la pantalla de despido, encima de todo");
        Comprobar(Progreso.Dinero == Leer("dinero"), $"el dinero del nivel (${Leer("dineroNivel")}) no pasa a Progreso (${Progreso.Dinero})");
        Comprobar(!Buscar("Canvas/PanelFinal").gameObject.activeSelf, "no sale el panel final");
    }

    static void ComprobarPantallaDespido()
    {
        var panel = Buscar("Canvas/PanelDespido");
        var pantalla = panel.GetComponent<PantallaDespido>();
        Comprobar(pantalla.Visible, "la pantalla terminó de aparecer (alpha 1, botones activos)");
        Comprobar(TextoDe(panel.Find("DespidoTitular")) == "¡TE DESPIDIERON!" && TextoDe(panel.Find("DespidoTexto")) == ConstruirQuejas.TextoDespido,
                  "titular \"¡TE DESPIDIERON!\" y el texto de apoyo");
        Comprobar(Visible(panel.Find("DespidoTitular")) && Visible(panel.Find("DespidoTexto")), "titular y texto caben");
        Comprobar(Veces("jingle_derrota") >= 1 && AM.Atenuacion < 0.4f, $"jingle de derrota y la música más baja ({AM.Atenuacion:0.00})");
        Comprobar(!Object.FindFirstObjectByType<MenuJuego>().Confirmando && !Buscar("Canvas/PanelPausa").gameObject.activeSelf, "sin pausa encima");
    }

    static void RevisarVineta(IntroHistoria activo)
    {
        var f = ConstruirFinal.Finales.First(x => x.objeto == activo.name);
        var v = f.vinetas[Vineta - 1];
        bool ella = Ronda == 1;
        string titular = (ella && v.titularRepartidora.Length > 0 ? v.titularRepartidora : v.titular).Replace("{nombre}", Texto.Nombre());
        string texto = (ella && v.textoRepartidora.Length > 0 ? v.textoRepartidora : v.texto).Replace("{nombre}", Texto.Nombre());
        string imagen = v.porMoto ? $"{v.archivo}_{MotoDeRonda[Ronda]}{(ella ? "_repartidora" : "")}"
                                  : ella && v.archivoRepartidora.Length > 0 ? v.archivoRepartidora : v.archivo;
        string real = Buscar("Canvas/Contenido/Vineta").GetComponent<Image>().sprite.name;
        Log($"  {activo.name} {Vineta}/{f.vinetas.Length}: \"{TextoDe(Buscar("Canvas/Contenido/Titular"))}\" ({real})");
        Comprobar(TextoDe(Buscar("Canvas/Contenido/Titular")) == titular, $"titular \"{titular}\"");
        Comprobar(TextoDe(Buscar("Canvas/Contenido/Texto")) == texto, texto.Length > 0 ? $"texto \"{texto}\"" : "sin texto de apoyo");
        Comprobar(real == imagen, $"imagen {imagen}");
        Comprobar(TextoDe(Buscar("Canvas/Contenido/IndicadorSiguiente/Texto")) == v.indicador, $"indicador \"{v.indicador}\"");
        if (Vineta == f.remate) Comprobar(Veces("historia_remate") >= 1, "el remate (stinger) en la viñeta 3");
        if (v.musica.Length > 0) Comprobar(AM.MusicaActual == v.musica, $"música {v.musica} ({AM.MusicaActual})");
        if (v.efecto.Length > 0) Comprobar(Veces(v.efecto) >= 1, $"suena {v.efecto}");
    }

    // ------------------------------------------------------------------ ayudantes

    static float Satisfaccion(int estrellas) => estrellas >= 3 ? 100f : estrellas == 2 ? 65f : 40f;

    static void Campo(string nombre, object valor) => typeof(GameManager).GetField(nombre, Privado).SetValue(GM, valor);

    // entrega el pedido actual con la satisfacción dada (el tiempo del pedido se pone donde da ese porcentaje)
    static void EntregarCon(float s)
    {
        float t = s >= 100f ? 0f : GM.TiempoIdeal + (1f - s / 100f) * (GM.TiempoMaximo - GM.TiempoIdeal);
        Campo("tiempoPedido", t);
        Campo("penalizacion", 0f);
        GM.Entregar(GM.PuntoActual);
    }

    // el reloj llega al máximo: el GameManager cancela en su Update (como en el juego)
    static void Cancelar() => Campo("tiempoPedido", GM.TiempoMaximo);

    static Image[] Iconos()
    {
        var ui = Object.FindFirstObjectByType<UIManager>();
        return (Image[])typeof(UIManager).GetField("iconosQueja", Privado).GetValue(ui);
    }

    static int Estrellas()
    {
        var ui = Object.FindFirstObjectByType<UIManager>();
        var estrellas = (Image[])typeof(UIManager).GetField("estrellas", Privado).GetValue(ui);
        var llena = (Sprite)typeof(UIManager).GetField("estrellaLlena", Privado).GetValue(ui);
        return estrellas.Count(e => e != null && e.sprite == llena);
    }

    static GameObject Lluvia() => (GameObject)typeof(LevelManager).GetField("lluvia", Privado).GetValue(Nivel);

    static bool EsperarEligeMoto(string escena) =>
        EsperarA(() => Escena == escena && Object.FindFirstObjectByType<TiendaMotos>() != null && Object.FindFirstObjectByType<TiendaMotos>().Abierta
                       && Time.timeSinceLevelLoad >= 0f, 15, $"ELIGE TU MOTO en {escena}");

    static void ClicEmpezar() =>
        Clic(RutaDe(Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .First(t => t.name == "TiendaMotos_Fondo").Find("Ventana/BotonEmpezar")));

    static float Arriba(RectTransform rt) { var c = new Vector3[4]; rt.GetWorldCorners(c); return c[1].y; }
    static float Abajo(RectTransform rt) { var c = new Vector3[4]; rt.GetWorldCorners(c); return c[0].y; }
    static float Izquierda(RectTransform rt) { var c = new Vector3[4]; rt.GetWorldCorners(c); return c[0].x; }
    static string TextoDe(Transform t) => t != null ? t.GetComponentInChildren<TMP_Text>(true)?.text ?? "" : "";

    // el texto cabe en su caja (sin desbordar)
    static bool Visible(Transform t)
    {
        var tmp = t.GetComponent<TMP_Text>();
        if (tmp == null) return false;
        tmp.ForceMeshUpdate();
        var b = tmp.textBounds;   // lo que se dibuja (con el texto ya cortado en líneas)
        var r = tmp.rectTransform.rect;
        return !tmp.isTextOverflowing && b.size.x <= r.width + 1f && b.size.y <= r.height + 1f;
    }

    static bool EsperarA(Func<bool> condicion, float limite, string que)
    {
        float ahora = (float)EditorApplication.timeSinceStartup;
        if (SessionState.GetFloat(P + "desde", 0) == 0) SessionState.SetFloat(P + "desde", ahora);
        bool listo = false;
        try { listo = condicion(); } catch (NullReferenceException) { } catch (InvalidOperationException) { } catch (MissingReferenceException) { }
        if (!listo && ahora - SessionState.GetFloat(P + "desde", 0) < limite) { Esperar(0.05f); return false; }
        if (!listo) Fallo($"se acabó el tiempo esperando {que} (escena {Escena}, paso {Paso})");
        SessionState.SetFloat(P + "desde", 0);
        return true;
    }

    // entrada real por el Input System; cada pulsación dura al menos 2 cuadros
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
            Fallo($"clic en {ruta}: lo tapa {(arriba != null ? arriba.name : "nada (sin raycast)")} (punto {datos.position}, pantalla {Screen.width}x{Screen.height})");
            return;
        }
        ExecuteEvents.ExecuteHierarchy(arriba, datos, ExecuteEvents.pointerClickHandler);
        Log($"  clic en {ruta}");
    }

    // Captura a 1920x1080 con el Canvas (el del HUD, o el de la escena) dibujado por la cámara
    static void Capturar(string archivo)
    {
        var ruta = Path.Combine(SessionState.GetString(P + "carpeta", "Temp"), archivo);
        var cam = Camera.main;
        var raices = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas).ToList();
        var canvas = raices.FirstOrDefault(c => c.transform.Find("PanelFinal") != null) ?? raices.First(c => c.name == "Canvas");
        var rt = new RenderTexture(1920, 1080, 24);
        var modo = canvas.renderMode;
        int capa = canvas.sortingLayerID, orden = canvas.sortingOrder;
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
            canvas.sortingLayerID = capa;
            canvas.sortingOrder = orden;
            rt.Release();
        }
        Log($"  captura: {archivo}");
    }
}
