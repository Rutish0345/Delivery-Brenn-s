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
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Prueba en Play de la selección de nivel y del avance solo de la sesión, desde MenuPrincipal (como el build):
// A) Sesión nueva: $0, sin motos, solo el nivel 1 abierto. Menú → Seleccion → Intro (Saltar) → SeleccionNivel con la
//    música del menú: Residencial NUEVO, Comercial y Lluvia BLOQUEADO con candado; D elige el 2, pero Jugar no hace nada.
// B) Nivel 1: la caja del HUD (sin GuiaPedido ni chevron); dos cancelaciones → despido: no suma estrellas ni abre nada.
// C) Reintentar, 3 entregas perfectas → 3 estrellas → "Niveles": Residencial COMPLETADO (3 estrellas, Mejor: 100%),
//    Comercial NUEVO, Lluvia BLOQUEADO, "Niveles 1/3".
// D) Reiniciar progreso: "No" no cambia nada; "Sí" deja $0, sin motos ni estrellas y solo el nivel 1.
// E) "Menú" → Iniciar juego: con personaje ya elegido va directo a SeleccionNivel.
// F) Con todo abierto y las motos: nivel 2 y nivel 3 desde la pantalla (ELIGE TU MOTO) con la caja del HUD.
// G) Salir de Play y volver a entrar: $0, sin motos ni estrellas, solo el nivel 1 (y hay que elegir personaje otra vez).
// Capturas: SeleccionNivel_nuevo, HUD_caja_nivel1, SeleccionNivel_completado, HUD_caja_nivel2, HUD_caja_nivel3.
// Temp/PruebaNiveles.pedido (dentro: carpeta para las capturas) → Temp/PruebaNiveles.resultado
[InitializeOnLoad]
public static class PruebaNiveles
{
    const string Pedido = "Temp/PruebaNiveles.pedido";
    const string Resultado = "Temp/PruebaNiveles.resultado";
    const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";
    const string P = "PruebaNiveles.";
    const BindingFlags Privado = BindingFlags.Instance | BindingFlags.NonPublic;

    static bool enganchado;

    static PruebaNiveles()
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
    static SeleccionNivel Sel => Object.FindFirstObjectByType<SeleccionNivel>();
    static AudioManager AM => AudioManager.Instance;

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
            ProgresoPrueba.Limpiar();   // nada preparado: la sesión empieza como en el build
            var inicio = EditorSceneManager.playModeStartScene;
            SessionState.SetString(P + "inicio", inicio != null ? AssetDatabase.GetAssetPath(inicio) : "");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaMenu);
            var sinUI = RevisarCanvas.RevisarEscenasDeJuego();
            foreach (var falta in sinUI) Fallo(falta);
            if (sinUI.Count == 0) Log("  ok: Canvas completo (CajaHUD, BotonNiveles) en las 3 escenas y SeleccionNivel completa");
            var orden = EditorBuildSettings.scenes.Where(e => e.enabled).Select(e => Path.GetFileNameWithoutExtension(e.path)).ToArray();
            string esperado = "MenuPrincipal, Seleccion, Intro, SeleccionNivel, SampleScene, Nivel2_Comercial, Nivel3_Lluvia, Final, Creditos";
            Comprobar(string.Join(", ", orden) == esperado, $"Build Profiles: {string.Join(", ", orden)}");
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
        if (Paso < 40 && !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) { Fallo($"Play se cerró en el paso {Paso}"); Paso = 90; return; }
        switch (Paso)
        {
            // ---------------------------------------------------------------- A) sesión nueva
            case 1:
                Application.runInBackground = true;
                if (!EsperarA(() => Escena == "MenuPrincipal" && Time.timeSinceLevelLoad > 0.3f && MenuPrueba.Listo(), 15, "el menú")) return;
                Log("A) Sesión nueva");
                ComprobarSesionNueva();
                Clic("Canvas/PanelMenu/BotonIniciar");
                Siguiente(0.5f);
                break;
            case 2:
                if (!EsperarA(() => SeleccionPrueba.Pasar() && Escena == "Intro" && Time.timeSinceLevelLoad > 0.5f, 10, "Seleccion → Intro")) return;
                Comprobar(Progreso.PersonajeElegido, "al confirmar el personaje queda elegido para la sesión");
                Clic("Canvas/BotonSaltar");
                Siguiente(0.5f);
                break;
            case 3:
                if (!EsperarA(() => Escena == "SeleccionNivel" && Sel != null && Time.timeSinceLevelLoad > 0.6f, 15, "SeleccionNivel después de la Intro")) return;
                Comprobar(true, "la Intro lleva a SeleccionNivel");
                Comprobar(AM != null && AM.MusicaActual == "musica_menu", $"suena la música del menú ({AM?.MusicaActual})");
                ComprobarPantalla(new[] { SeleccionNivel.Nuevo, SeleccionNivel.Bloqueado, SeleccionNivel.Bloqueado }, "$0", "Motos 0/4", "Niveles 0/3");
                Comprobar(Sel.Actual == 0 && Jugar().interactable, "elegido el nivel 1 y Jugar habilitado");
                Capturar("SeleccionNivel_nuevo.png");
                Sel.Elegir(1, true);   // como la tecla D
                Comprobar(Sel.Actual == 1 && !Jugar().interactable, "D elige Comercial (bloqueado): Jugar deshabilitado");
                Sel.Jugar();
                Siguiente(0.4f);
                break;
            case 4:
                Comprobar(Escena == "SeleccionNivel", "Jugar con un nivel bloqueado no carga nada");
                Sel.Elegir(0, true);   // A
                Clic("Canvas/Jugar");
                Siguiente(0.5f);
                break;

            // ---------------------------------------------------------------- B) despido en el nivel 1
            case 5:
            {
                if (!EsperarA(() => Escena == "SampleScene" && GM != null && GM.PuntoActual != null && Time.timeSinceLevelLoad > 0.8f, 15, "el nivel 1")) return;
                Log("B) Nivel 1: la caja del HUD");
                var caja = Buscar("Canvas/CajaHUD") as RectTransform;
                Comprobar(caja != null && caja.gameObject.activeInHierarchy, "CajaHUD visible");
                Comprobar(Buscar("Canvas/GuiaPedido") == null, "sin la píldora de guía (GuiaPedido)");
                var flecha = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == ConstruirMarcadorPedido.NombreFlecha);
                Comprobar(flecha != null && flecha.transform.childCount == 1 && flecha.transform.Find("flecha_borde") == null, "FlechaPedido solo con la flecha que orbita (sin chevron de la orilla)");
                var hijos = Enumerable.Range(0, caja.childCount).Select(i => caja.GetChild(i).name).ToArray();
                Comprobar(hijos.SequenceEqual(new[] { "PlacaPedidos", "PlacaReloj", "FilaQuejas", "PlacaTurbo" }), $"en la caja: {string.Join(", ", hijos)}");
                Comprobar(Texto("Canvas/CajaHUD/PlacaPedidos/TextoPedidos") == "Pedidos 0/3", $"\"{Texto("Canvas/CajaHUD/PlacaPedidos/TextoPedidos")}\"");
                Capturar("HUD_caja_nivel1.png");
                Log("  dos cancelaciones → despido");
                Cancelar();
                Siguiente(0.05f);
                break;
            }
            case 6:
                if (!EsperarA(() => GM.Quejas == 1, 5, "la primera queja")) return;
                Cancelar();
                Siguiente(0.05f);
                break;
            case 7:
                if (!EsperarA(() => GM.FueDespedido, 5, "el despido")) return;
                Comprobar(Progreso.Estrellas(0) == 0 && !Progreso.Completado(0) && !Progreso.Desbloqueado(1) && Progreso.Dinero == 0,
                          $"despedido: sin estrellas ({Progreso.Estrellas(0)}), sin completar, Comercial cerrado, ${Progreso.Dinero}");
                Siguiente(1.0f);
                break;
            case 8:
                Clic("Canvas/PanelDespido/" + ConstruirQuejas.BotonReintentar);
                Siguiente(0.5f);
                break;

            // ---------------------------------------------------------------- C) nivel 1 con 3 estrellas
            case 9:
                if (!EsperarA(() => Escena == "SampleScene" && GM != null && GM.PuntoActual != null && !GM.FueDespedido && GM.Resueltos == 0 && Time.timeSinceLevelLoad > 0.4f, 10, "el nivel 1 otra vez")) return;
                Log("C) Nivel 1: 3 entregas perfectas");
                EntregarCon(100f);
                Siguiente(0.3f);
                break;
            case 10:
                EntregarCon(100f);
                Siguiente(0.3f);
                break;
            case 11:
                EntregarCon(100f);
                Siguiente(2.5f);   // las estrellas se animan
                break;
            case 12:
                if (!EsperarA(() => Buscar("Canvas/PanelFinal") != null && Buscar("Canvas/PanelFinal").gameObject.activeInHierarchy, 10, "el panel final")) return;
                Comprobar(Progreso.Estrellas(0) == 3 && Mathf.Approximately(Progreso.MejorSatisfaccion(0), 100f) && Progreso.Desbloqueado(1) && !Progreso.Desbloqueado(2),
                          $"3 estrellas guardadas ({Progreso.Estrellas(0)}, mejor {Progreso.MejorSatisfaccion(0):0}%): se abre Comercial, Lluvia sigue cerrado");
                Comprobar(Progreso.Dinero > 0, $"el dinero del nivel pasa a la sesión (${Progreso.Dinero})");
                SessionState.SetInt(P + "dinero", Progreso.Dinero);
                var niveles = Buscar("Canvas/PanelFinal/BotonNiveles");
                Comprobar(niveles != null && niveles.gameObject.activeInHierarchy && Texto("Canvas/PanelFinal/BotonNiveles/Texto") == "Niveles",
                          $"el panel final tiene \"Niveles\" (\"{Texto("Canvas/PanelFinal/BotonNiveles/Texto")}\")");
                Comprobar(Buscar("Canvas/PanelFinal/BotonSiguiente")?.gameObject.activeInHierarchy == true, "y sigue \"Siguiente nivel\"");
                Clic("Canvas/PanelFinal/BotonNiveles");
                Siguiente(0.5f);
                break;
            case 13:
                if (!EsperarA(() => Escena == "SeleccionNivel" && Sel != null && Time.timeSinceLevelLoad > 0.6f, 10, "SeleccionNivel desde el panel final")) return;
                Comprobar(AM.MusicaActual == "musica_menu", $"vuelve la música del menú ({AM.MusicaActual})");
                ComprobarPantalla(new[] { SeleccionNivel.Completado, SeleccionNivel.Nuevo, SeleccionNivel.Bloqueado },
                                  "$" + SessionState.GetInt(P + "dinero", 0), "Motos 0/4", "Niveles 1/3");
                Comprobar(EstrellasLlenas(0) == 3 && Texto("Canvas/Tarjetas/Tarjeta1/Mejor") == "Mejor: 100%", $"Residencial: {EstrellasLlenas(0)} estrellas, \"{Texto("Canvas/Tarjetas/Tarjeta1/Mejor")}\"");
                Comprobar(Barra() > 0.32f && Barra() < 0.35f, $"barra de niveles a 1/3 ({Barra():0.00})");
                Sel.Elegir(1, true);
                Capturar("SeleccionNivel_completado.png");
                Siguiente(0.2f);
                break;

            // ---------------------------------------------------------------- D) reiniciar progreso
            case 14:
                Log("D) Reiniciar progreso");
                Clic("Canvas/BotonReiniciar");
                Siguiente(0.3f);
                break;
            case 15:
                Comprobar(Sel.ConfirmacionAbierta && Texto("Canvas/ConfirmarReinicio/Panel/Pregunta") == ConstruirSeleccionNivel.PreguntaReinicio, "pide confirmación (Sí / No)");
                Clic("Canvas/ConfirmarReinicio/Panel/BotonNo");
                Siguiente(0.3f);
                break;
            case 16:
                Comprobar(!Sel.ConfirmacionAbierta && Progreso.Estrellas(0) == 3 && Progreso.Dinero == SessionState.GetInt(P + "dinero", 0), "\"No\": cierra y no cambia nada");
                Clic("Canvas/BotonReiniciar");
                Siguiente(0.3f);
                break;
            case 17:
                Clic("Canvas/ConfirmarReinicio/Panel/BotonSi");
                Siguiente(0.3f);
                break;
            case 18:
                Comprobar(!Sel.ConfirmacionAbierta && Progreso.Dinero == 0 && Progreso.MotosCompradas == 0 && Progreso.Estrellas(0) == 0 && !Progreso.Desbloqueado(1),
                          "\"Sí\": $0, sin motos ni estrellas, solo el nivel 1");
                ComprobarPantalla(new[] { SeleccionNivel.Nuevo, SeleccionNivel.Bloqueado, SeleccionNivel.Bloqueado }, "$0", "Motos 0/4", "Niveles 0/3");
                Comprobar(Sel.Actual == 0, "vuelve a elegir Residencial");
                Clic("Canvas/BotonMenu");
                Siguiente(0.5f);
                break;

            // ---------------------------------------------------------------- E) el menú ya no pasa por la intro
            case 19:
                if (!EsperarA(() => Escena == "MenuPrincipal" && Time.timeSinceLevelLoad > 0.3f && MenuPrueba.Listo(), 10, "el menú")) return;
                Log("E) Menú → Iniciar juego (personaje ya elegido): otra vez Seleccion e Intro");
                SeleccionPrueba.Usada = IntroPrueba.Usada = false;
                Clic("Canvas/PanelMenu/BotonIniciar");
                Siguiente(0.3f);
                break;
            case 20:
                if (!EsperarA(() => SeleccionPrueba.Pasar() && IntroPrueba.Pasar() && Escena == "SeleccionNivel" && Time.timeSinceLevelLoad > 0.3f, 20, "Seleccion → Intro → SeleccionNivel")) return;
                Comprobar(SeleccionPrueba.Usada && IntroPrueba.Usada, $"pasa por Seleccion e Intro y llega a SeleccionNivel ({Escena})");
                Log("F) Niveles 2 y 3 con todo abierto");
                Progreso.DesbloquearTodos();
                Progreso.DarMotos(0b1111);
                Progreso.Equipar(0);
                Siguiente(0.2f);
                break;
            case 21:
                Comprobar(Estado(1) == SeleccionNivel.Nuevo && Estado(2) == SeleccionNivel.Nuevo && Texto("Canvas/PlacaMotos/Texto") == "Motos 4/4",
                          "con todo abierto: Comercial y Lluvia NUEVO, Motos 4/4");
                Sel.Elegir(1, true);
                Clic("Canvas/Jugar");
                Siguiente(0.5f);
                break;
            case 22:
            case 25:
            {
                string escena = Paso == 22 ? "Nivel2_Comercial" : "Nivel3_Lluvia";
                if (!EsperarA(() => Escena == escena && Object.FindFirstObjectByType<TiendaMotos>() != null && Object.FindFirstObjectByType<TiendaMotos>().Abierta, 15, $"ELIGE TU MOTO en {escena}")) return;
                Paso += 100;   // la ventana de la tienda se arma y anima con el tiempo en 0: un momento y "Empezar"
                Esperar(0.8f);
                break;
            }
            case 122:
            case 125:
                Paso -= 100;
                Clic(RutaDe(Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                .First(t => t.name == "TiendaMotos_Fondo").Find("Ventana/BotonEmpezar")));
                Siguiente(0.5f);
                break;
            case 23:
            case 26:
            {
                int n = Paso == 23 ? 2 : 3;
                if (!EsperarA(() => GM != null && !GM.EnEspera && GM.PuntoActual != null && Time.timeScale == 1f, 10, $"que empiece el nivel {n}")) return;
                Siguiente(1.2f);   // que se acomode la cámara
                break;
            }
            case 24:
            case 27:
            {
                int n = Paso == 24 ? 2 : 3;
                var caja = Buscar("Canvas/CajaHUD");
                Comprobar(caja != null && caja.gameObject.activeInHierarchy && Buscar("Canvas/GuiaPedido") == null,
                          $"nivel {n}: CajaHUD visible y sin GuiaPedido");
                Comprobar(Texto("Canvas/CajaHUD/PlacaPedidos/TextoPedidos") == $"Pedidos 0/{Progreso.Niveles[n - 1].pedidos}", $"nivel {n}: \"{Texto("Canvas/CajaHUD/PlacaPedidos/TextoPedidos")}\"");
                Capturar($"HUD_caja_nivel{n}.png");
                if (n == 2)
                {
                    SceneManager.LoadScene("SeleccionNivel");
                    Paso = 28;
                    Esperar(0.8f);
                }
                else
                {
                    Log("G) Salir de Play y volver a entrar");
                    Progreso.CompletarNivel("Nivel3_Lluvia", 3, 90f);
                    Progreso.Agregar(500);
                    Paso = 40;
                    EditorApplication.isPlaying = false;
                    Esperar(1f);
                }
                break;
            }
            case 28:   // de vuelta en la pantalla: Lluvia
                if (!EsperarA(() => Escena == "SeleccionNivel" && Sel != null && Time.timeSinceLevelLoad > 0.4f, 10, "SeleccionNivel")) return;
                Sel.Elegir(2, true);
                Clic("Canvas/Jugar");
                Paso = 25;
                Esperar(0.5f);
                break;

            // ---------------------------------------------------------------- G) otro Play
            case 40:
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Paso = 41;
                Esperar(1f);
                EditorApplication.isPlaying = true;
                break;
            case 41:
                if (!EsperarA(() => EditorApplication.isPlaying && Escena == "MenuPrincipal" && Time.timeSinceLevelLoad > 0.3f, 20, "el menú en el segundo Play")) return;
                ComprobarSesionNueva();
                Comprobar(!Progreso.PersonajeElegido, "hay que elegir personaje otra vez");
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

    // ------------------------------------------------------------------ comprobaciones

    static void ComprobarSesionNueva()
    {
        Comprobar(Progreso.Dinero == 0 && Progreso.MotosCompradas == 0 && Progreso.MotoEquipada == -1, $"${Progreso.Dinero}, {Progreso.MotosCompradas} motos, equipada {Progreso.MotoEquipada}");
        Comprobar(Enumerable.Range(0, 3).All(i => Progreso.Estrellas(i) == 0 && !Progreso.Completado(i)), "sin estrellas ni niveles completados");
        Comprobar(Progreso.Desbloqueado(0) && !Progreso.Desbloqueado(1) && !Progreso.Desbloqueado(2), "solo el nivel 1 abierto");
        Comprobar(!PlayerPrefs.HasKey("dinero_total") && !PlayerPrefs.HasKey("motos_compradas") && !PlayerPrefs.HasKey("moto_equipada"),
                  "sin las claves viejas del avance en PlayerPrefs");
    }

    static void ComprobarPantalla(string[] estados, string dinero, string motos, string niveles)
    {
        for (int i = 0; i < 3; i++)
        {
            bool bloqueado = estados[i] == SeleccionNivel.Bloqueado;
            var candado = Buscar($"Canvas/Tarjetas/Tarjeta{i + 1}/Miniatura/Candado");
            Comprobar(Estado(i) == estados[i] && candado != null && candado.gameObject.activeSelf == bloqueado,
                      $"{Progreso.Niveles[i].nombre}: {Estado(i)}{(candado != null && candado.gameObject.activeSelf ? " con candado" : "")}");
        }
        Comprobar(Texto("Canvas/PlacaDinero/Texto") == dinero && Texto("Canvas/PlacaMotos/Texto") == motos && Texto("Canvas/PlacaNiveles/Texto") == niveles,
                  $"arriba: \"{Texto("Canvas/PlacaDinero/Texto")}\", \"{Texto("Canvas/PlacaMotos/Texto")}\", \"{Texto("Canvas/PlacaNiveles/Texto")}\"");
        for (int i = 0; i < 3; i++)
            Comprobar(Texto($"Canvas/Tarjetas/Tarjeta{i + 1}/Pedidos") == $"{Progreso.Niveles[i].pedidos} pedidos", $"tarjeta {i + 1}: {Texto($"Canvas/Tarjetas/Tarjeta{i + 1}/Pedidos")}");
    }

    static string Estado(int i) => Texto($"Canvas/Tarjetas/Tarjeta{i + 1}/PlacaEstado/Texto");
    static Button Jugar() => Buscar("Canvas/Jugar").GetComponent<Button>();
    static float Barra() => Buscar("Canvas/PlacaNiveles/Barra/Relleno").GetComponent<Image>().fillAmount;
    static int EstrellasLlenas(int i) =>
        Buscar($"Canvas/Tarjetas/Tarjeta{i + 1}/Estrellas").GetComponentsInChildren<Image>().Count(e => e.sprite != null && e.sprite.name == "estrella_llena");

    static void Campo(string nombre, float valor) => typeof(GameManager).GetField(nombre, Privado).SetValue(GM, valor);

    static void EntregarCon(float s)
    {
        float t = s >= 100f ? 0f : GM.TiempoIdeal + (1f - s / 100f) * (GM.TiempoMaximo - GM.TiempoIdeal);
        Campo("tiempoPedido", t);
        Campo("penalizacion", 0f);
        GM.Entregar(GM.PuntoActual);
    }

    // el reloj llega al máximo: el GameManager cancela en su Update (como en el juego)
    static void Cancelar() => Campo("tiempoPedido", GM.TiempoMaximo);

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
            Fallo($"clic en {ruta}: lo tapa {(arriba != null ? arriba.name : "nada (sin raycast)")} (punto {datos.position}, pantalla {Screen.width}x{Screen.height})");
            return;
        }
        ExecuteEvents.ExecuteHierarchy(arriba, datos, ExecuteEvents.pointerClickHandler);
        Log($"  clic en {ruta}");
    }

    // 1920x1080 con la cámara de la escena; el Canvas de arriba (el del HUD en los niveles) se dibuja encima
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
