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

// Prueba en Play del arranque del juego, empezando en MenuPrincipal (como el build):
// 1) captura del menú, escribe un nombre y clic en "Iniciar juego" → Intro
// 2) recorre las 5 viñetas: Espacio a mitad de la escritura (muestra todo), Espacio para pasar, clic
//    del ratón para pasar; revisa titulares, indicadores y que {nombre} se cambió, y captura las 5 → SampleScene
// 3) vuelve al menú, Iniciar juego y Esc en la primera viñeta → SampleScene
// 4) vuelve al menú, Iniciar juego y clic en "Saltar (Esc)" → SampleScene
// 5) nivel 1 a pie: sin "Tienda de motos" ni "Siguiente nivel" en el panel final (captura PanelFinal.png)
// 6) TiendaMotos en modo AntesDeNivel, forzado: pausa sin pedido, "EMPEZAR NIVEL" arranca el nivel en moto
// Con "carpeta|marcador" en el pedido: menú → Iniciar → Saltar → nivel 1 y solo la prueba del marcador del pedido
// (los 28 puntos se prenden, producto por pedido sin repetir, aro verde en el radio, burbuja escondida con el
// globo de la clienta, nada tras entregar; capturas MarcadorLejos, MarcadorCerca y MarcadorGlobo)
// Con "carpeta|entrega": menú → Iniciar → Saltar → nivel 1 y 3 pedidos seguidos (2 a pie, el 3.º en moto): la flecha
// guía orbita y apunta al pedido y se esconde en el radio; la mochila limpia en las 4 direcciones; al entregar el
// ícono del producto vuela hasta la clienta y después sale su "¡Gracias!" (en moto, desde la caja trasera).
// El avance guardado (dinero, motos compradas y equipada) queda como estaba al empezar.
// Teclado y ratón se simulan con eventos del Input System; los clics de botones pasan por el EventSystem.
// Temp/PruebaIntro.pedido (dentro: carpeta para las capturas) → Temp/PruebaIntro.resultado
[InitializeOnLoad]
public static class PruebaIntro
{
    const string Pedido = "Temp/PruebaIntro.pedido";
    const string Resultado = "Temp/PruebaIntro.resultado";
    const string Cancelar = "Temp/PruebaIntro.cancelar";
    const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";
    const string Nombre = "Rut";
    const string ClavePaso = "PruebaIntro.paso", ClaveLog = "PruebaIntro.log", ClaveConsola = "PruebaIntro.consola",
                 ClaveCarpeta = "PruebaIntro.carpeta", ClaveEspera = "PruebaIntro.espera", ClaveFallos = "PruebaIntro.fallos",
                 ClaveDesde = "PruebaIntro.desde", ClaveInicio = "PruebaIntro.inicio", ClaveModo = "PruebaIntro.modo",
                 ClaveDato = "PruebaIntro.dato", ClaveIndice = "PruebaIntro.indice", ClaveMoto = "PruebaIntro.moto";

    static bool enganchado;

    static PruebaIntro()
    {
        if (enganchado) return;
        enganchado = true;
        EditorApplication.update += Revisar;
        Application.logMessageReceived += Anotar;
    }

    static int Paso { get => SessionState.GetInt(ClavePaso, 0); set => SessionState.SetInt(ClavePaso, value); }

    static void Log(string s) => SessionState.SetString(ClaveLog, SessionState.GetString(ClaveLog, "") + s + "\n");
    static void Fallo(string s) { Log("  FALLO: " + s); SessionState.SetInt(ClaveFallos, SessionState.GetInt(ClaveFallos, 0) + 1); }
    static void Comprobar(bool ok, string que) { if (ok) Log("  ok: " + que); else Fallo(que); }

    static void Anotar(string msg, string pila, LogType tipo)
    {
        if (Paso == 0 || tipo == LogType.Log) return;
        SessionState.SetString(ClaveConsola, SessionState.GetString(ClaveConsola, "") + $"[{tipo}] {msg}\n{(tipo == LogType.Exception ? pila : "")}");
    }

    static void Esperar(float segundos) => SessionState.SetFloat(ClaveEspera, (float)EditorApplication.timeSinceStartup + segundos);
    static bool Esperando => EditorApplication.timeSinceStartup < SessionState.GetFloat(ClaveEspera, 0);

    static void Revisar()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (Paso == 0)
        {
            if (!File.Exists(Pedido) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var partes = File.ReadAllText(Pedido).Trim().Split('|');
            SessionState.SetString(ClaveCarpeta, partes[0]);
            SessionState.SetString(ClaveModo, partes.Length > 1 ? partes[1] : "");
            File.Delete(Pedido);
            SessionState.SetString(ClaveLog, "");
            SessionState.SetString(ClaveConsola, "");
            SessionState.SetInt(ClaveFallos, 0);
            GuardarProgreso();
            // Play empieza en MenuPrincipal sin cambiar la escena abierta (se deja como estaba al terminar)
            var inicio = EditorSceneManager.playModeStartScene;
            SessionState.SetString(ClaveInicio, inicio != null ? AssetDatabase.GetAssetPath(inicio) : "");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaMenu);
            Log("Entrando a Play desde MenuPrincipal.");
            // las 3 escenas de juego con su Canvas (HUD, GuiaPedido, PanelPausa, PanelFinal), revisadas en disco
            var sinUI = RevisarCanvas.RevisarEscenasDeJuego();
            foreach (var falta in sinUI) Fallo(falta);
            if (sinUI.Count == 0) Log("  ok: Canvas completo en SampleScene, Nivel2_Comercial y Nivel3_Lluvia");
            Paso = 1;
            Esperar(2f);
            EditorApplication.isPlaying = true;
            return;
        }
        if (File.Exists(Cancelar))
        {
            // la prueba se quedó a medias (p. ej. se salió de Play por fuera): se cierra como en el paso 90
            File.Delete(Cancelar);
            Fallo($"cancelada en el paso {Paso}");
            Paso = 90;
            EditorApplication.isPlaying = false;
            return;
        }
        if (Esperando) return;
        try { Avanzar(); }
        catch (Exception e) { Fallo($"excepción en el paso {Paso}: {e}"); Paso = 90; EditorApplication.isPlaying = false; }
    }

    static void Avanzar()
    {
        if (Paso < 90 && !EditorApplication.isPlaying) { Esperar(0.5f); return; }
        switch (Paso)
        {
            case 1:
                // solo en esta sesión de Play (el ajuste del proyecto no cambia): que el juego siga
                // corriendo aunque la ventana de Unity no tenga el foco
                Application.runInBackground = true;
                if (!EsperarMenu()) return;
                EnfocarGameView();
                EntradaSinFoco();
                Log("1) Menú principal");
                Comprobar(Escena == "MenuPrincipal", $"Play empieza en {Escena}");
                Comprobar(Buscar("Canvas/BotonIniciar") != null && Buscar("Canvas/BotonInstrucciones") != null
                          && Buscar("Canvas/BotonSalir") != null, "botones Iniciar juego, Instrucciones y Salir");
                Comprobar(Buscar("Canvas/BotonHistoria") == null, "sin botón Historia");
                Capturar("Menu.png");
                var campo = Buscar("Canvas/CampoNombre").GetComponent<TMP_InputField>();
                campo.text = Nombre;
                Log($"  nombre escrito: {Nombre}");
                Clic("Canvas/BotonIniciar");
                if (Modo != "") { Paso = 21; Esperar(0.5f); break; }   // directo a "Saltar (Esc)"
                Siguiente(0.5f);
                break;
            case 2:
                if (!EsperarIntro()) return;
                Log("2) Intro: viñeta 1 (se deja escribir sola)");
                Comprobar(Progreso.NombreJugador == Nombre, $"Progreso.NombreJugador = \"{Progreso.NombreJugador}\"");
                Siguiente(0.1f);
                break;
            case 3:
                if (!EsperarListo("Donde")) return;
                Comprobar(Imagen().sprite != null && Imagen().sprite.name == "vineta_1", "imagen vineta_1");
                Comprobar(TextoCompleto(), "aparece la placa con el texto completo");
                Comprobar(Titular().text == "SALÓN BRENN'S", $"titular \"{Titular().text}\"");
                Comprobar(TextoIndicador().text == "ESPACIO PARA CONTINUAR", $"indicador \"{TextoIndicador().text}\"");
                Siguiente(0.3f);
                break;
            case 4:
                Capturar("Vineta1.png");
                Tecla(Key.Space);
                Log("  Espacio → viñeta 2");
                Siguiente(0.15f);
                break;
            case 5:
                Soltar();
                if (!EsperarA(() => Texto().text.StartsWith("Las clientas"), 5, "la viñeta 2")) return;
                Siguiente(0.55f);   // durante el fundido no se lee la entrada
                break;
            case 6:
                Comprobar(!TextoCompleto(), $"viñeta 2 a mitad de escribirse ({Texto().maxVisibleCharacters}/{Texto().textInfo.characterCount})");
                Tecla(Key.Space);
                Log("  Espacio a mitad del texto");
                Siguiente(0.15f);
                break;
            case 7:
                Soltar();
                Comprobar(TextoCompleto() && Texto().text.StartsWith("Las clientas"), "Espacio muestra todo el texto sin pasar de viñeta");
                Siguiente(0.3f);
                break;
            case 8:
                Capturar("Vineta2.png");
                Tecla(Key.Space);
                Log("  Espacio → viñeta 3");
                Siguiente(0.15f);
                break;
            case 9:
                Soltar();
                if (!EsperarListo("Tú llevas")) return;
                Comprobar(Titular().text == $"{Nombre}, TE NECESITAMOS", $"{{nombre}} cambiado en el titular: \"{Titular().text}\"");
                Siguiente(0.3f);
                break;
            case 10:
                Capturar("Vineta3.png");
                ClicRaton();
                Log("  clic del ratón → viñeta 4");
                Siguiente(0.15f);
                break;
            case 11:
                SoltarRaton();
                if (!EsperarListo("Muévete")) return;
                QuitarRaton();
                Comprobar(Imagen().sprite.name == "vineta_4", "imagen vineta_4");
                Siguiente(0.3f);
                break;
            case 12:
                Capturar("Vineta4.png");
                Tecla(Key.Space);
                Log("  Espacio → viñeta 5");
                Siguiente(0.15f);
                break;
            case 13:
                Soltar();
                if (!EsperarListo("Junta")) return;
                Comprobar(TextoIndicador().text == "PRESIONA ESPACIO PARA EMPEZAR", $"indicador de la última \"{TextoIndicador().text}\"");
                // el texto largo cabe en su caja (que termina arriba de la placa) en 3 líneas como máximo
                Comprobar(Texto().textInfo.lineCount <= 3 && Texto().textBounds.size.y <= Texto().rectTransform.rect.height + 0.5f,
                          $"texto de la viñeta 5 en {Texto().textInfo.lineCount} líneas a {Texto().fontSize:0.#}, " +
                          $"{Texto().textBounds.size.y:0} de alto en una caja de {Texto().rectTransform.rect.height:0}");
                Siguiente(0.3f);
                break;
            case 14:
                Capturar("Vineta5.png");
                Tecla(Key.Space);
                Log("  Espacio en la última viñeta");
                Siguiente(0.15f);
                break;
            case 15:
                Soltar();
                if (!EsperarJuego("al terminar la intro")) return;
                Log("3) De vuelta al menú para saltar con Esc");
                SceneManager.LoadScene("MenuPrincipal");
                Siguiente(0.5f);
                break;
            case 16:
                if (!EsperarMenu()) return;
                Clic("Canvas/BotonIniciar");
                Siguiente(0.5f);
                break;
            case 17:
                if (!EsperarIntro()) return;
                Siguiente(0.6f);
                break;
            case 18:
                Tecla(Key.Escape);
                Log("  Esc en la viñeta 1");
                Siguiente(0.15f);
                break;
            case 19:
                Soltar();
                if (!EsperarJuego("con Esc")) return;
                Log("4) De vuelta al menú para saltar con el botón");
                SceneManager.LoadScene("MenuPrincipal");
                Siguiente(0.5f);
                break;
            case 20:
                if (!EsperarMenu()) return;
                Clic("Canvas/BotonIniciar");
                Siguiente(0.5f);
                break;
            case 21:
                if (!EsperarIntro()) return;
                Siguiente(0.6f);
                break;
            case 22:
                Clic("Canvas/BotonSaltar");
                Siguiente(0.3f);
                break;
            case 23:
                if (!EsperarJuego("con \"Saltar (Esc)\"")) return;
                if (Modo == "marcador") { Paso = 30; Esperar(0.5f); break; }
                if (Modo == "entrega") { Paso = 50; Esperar(0.5f); break; }
                Log("5) Nivel 1 (a pie): sin tienda en el panel final");
                var tiendaNivel = Object.FindFirstObjectByType<TiendaMotos>();
                var panelFinal = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "PanelFinal");
                Comprobar(panelFinal != null && panelFinal.Find("BotonTienda") == null, "PanelFinal sin BotonTienda");
                Comprobar(tiendaNivel != null && !new SerializedObject(tiendaNivel).FindProperty("mostrarBotonEnFinal").boolValue, "TiendaMotos.mostrarBotonEnFinal = false");
                var siguiente = panelFinal != null ? panelFinal.Find("BotonSiguiente") : null;
                Comprobar(siguiente != null && !siguiente.gameObject.activeSelf, "Siguiente nivel apagado (la escena siguiente no existe)");
                Comprobar(!Object.FindFirstObjectByType<PlayerController>().MotosHabilitadas, "el repartidor va a pie");
                // vista del panel final como queda al terminar (se enciende solo para la captura)
                if (panelFinal != null)
                {
                    panelFinal.gameObject.SetActive(true);
                    Capturar("PanelFinal.png");
                    panelFinal.gameObject.SetActive(false);
                }
                // el modo "ELIGE TU MOTO" (para el nivel 2): se abre a mano en el nivel 1 solo para probarlo
                Log("6) TiendaMotos en modo AntesDeNivel (forzado en el nivel 1 para probarlo)");
                typeof(LevelManager).GetMethod("AbrirEleccionDeMoto", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(Object.FindFirstObjectByType<LevelManager>(), new object[] { GameManager.Instance, Object.FindFirstObjectByType<PlayerController>() });
                Siguiente(0.5f);
                break;
            case 24:
            {
                var tienda = Object.FindFirstObjectByType<TiendaMotos>();
                var ventana = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "TiendaMotos_Fondo");
                var titulo = ventana != null ? ventana.Find("Ventana/Titulo")?.GetComponent<TMP_Text>() : null;
                Comprobar(tienda.Abierta && tienda.ModoActual == TiendaMotos.Modo.AntesDeNivel, "ventana abierta en modo AntesDeNivel");
                Comprobar(titulo != null && titulo.text == "ELIGE TU MOTO", $"título \"{titulo?.text}\"");
                Comprobar(ventana.Find("Ventana/BotonEmpezar").gameObject.activeSelf && !ventana.Find("Ventana/BotonCerrar").gameObject.activeSelf,
                          "\"EMPEZAR NIVEL\" en lugar de Cerrar");
                Comprobar(ventana.Find("Ventana/Fila").childCount == 4 && ventana.Find("Ventana/BotonAPie") != null, "4 tarjetas e \"Ir a pie\"");
                Comprobar(ventana.Find("Ventana/Dinero").GetComponent<TMP_Text>().text == "Tu dinero: $" + Progreso.Dinero, "muestra el dinero actual");
                Comprobar(Time.timeScale == 0f && GameManager.Instance.EnEspera && GameManager.Instance.PuntoActual == null,
                          $"en pausa sin pedido (timeScale {Time.timeScale}, pedido {(GameManager.Instance.PuntoActual != null ? "activo" : "ninguno")})");
                Capturar("EligeTuMoto.png");
                Clic(RutaDe(ventana.Find("Ventana/BotonEmpezar")));
                Siguiente(0.5f);
                break;
            }
            case 25:
            {
                var tienda = Object.FindFirstObjectByType<TiendaMotos>();
                Comprobar(!tienda.Abierta, "EMPEZAR NIVEL cierra la ventana");
                Comprobar(Time.timeScale == 1f && !GameManager.Instance.EnEspera && GameManager.Instance.PuntoActual != null,
                          $"el nivel arranca (timeScale {Time.timeScale}, con pedido: {GameManager.Instance.PuntoActual != null})");
                Comprobar(Object.FindFirstObjectByType<PlayerController>().MotosHabilitadas, "SetMotosHabilitadas(true)");
                Paso = 90;
                EditorApplication.isPlaying = false;
                Esperar(1f);
                break;
            }
            case >= 30 and < 50:
                PasoMarcador();
                break;
            case >= 50 and < 90:
                PasoEntrega();
                break;
            case 90:
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                QuitarRaton();
                RestaurarEntrada();
                RestaurarProgreso();
                var inicio = SessionState.GetString(ClaveInicio, "");
                EditorSceneManager.playModeStartScene = inicio.Length > 0 ? AssetDatabase.LoadAssetAtPath<SceneAsset>(inicio) : null;
                int fallos = SessionState.GetInt(ClaveFallos, 0);
                string consola = SessionState.GetString(ClaveConsola, "");
                File.WriteAllText(Resultado, (fallos == 0 ? "OK" : $"{fallos} FALLOS") + "\n" + SessionState.GetString(ClaveLog, "") +
                                  "\nCONSOLA (advertencias y errores en Play): " + (consola.Length == 0 ? "ninguno" : "\n" + consola));
                Paso = 0;
                break;
        }
    }

    static string Escena => SceneManager.GetActiveScene().name;
    static string Modo => SessionState.GetString(ClaveModo, "");

    // ---- Marcador del pedido (nivel 1) ----
    static GameManager GM => GameManager.Instance;
    static DeliveryPoint[] Puntos() => Object.FindObjectsByType<DeliveryPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    static MarcadorPedido Marcador(DeliveryPoint p) => p.transform.GetChild(0).GetComponent<MarcadorPedido>();
    static List<DeliveryPoint> ConMarcador() => Puntos().Where(p => p.transform.GetChild(0).gameObject.activeInHierarchy).ToList();
    static SpriteRenderer Parte(MarcadorPedido m, string campo) => new SerializedObject(m).FindProperty(campo).objectReferenceValue as SpriteRenderer;
    static void NuevoPedido() => typeof(GameManager).GetMethod("ElegirNuevoPunto", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(GM, null);
    static Sprite IconoHud()
    {
        var g = Object.FindFirstObjectByType<GuiaPedido>();
        return (new SerializedObject(g).FindProperty("iconoProducto").objectReferenceValue as Image)?.sprite;
    }
    static Sprite IconoDe(Producto p) => (new SerializedObject(Marcador(GM.PuntoActual)).FindProperty("catalogo").objectReferenceValue as CatalogoProductos).Icono(p);

    // ---- Flecha guía, mochila limpia y entrega del producto (nivel 1: 2 pedidos a pie y el 3.º en moto) ----
    static readonly (Key tecla, string nombre)[] Direcciones =
        { (Key.S, "Frente"), (Key.W, "Espalda"), (Key.D, "Derecha"), (Key.A, "Izquierda") };
    static int Indice { get => SessionState.GetInt(ClaveIndice, 0); set => SessionState.SetInt(ClaveIndice, value); }
    static int Entrega { get => SessionState.GetInt(ClaveMoto, 0); set => SessionState.SetInt(ClaveMoto, value); }
    static PlayerController Jugador() => Object.FindFirstObjectByType<PlayerController>();
    static FlechaGuia Flecha() => Object.FindFirstObjectByType<FlechaGuia>();
    static EntregaProducto Animacion() => EntregaProducto.Instancia;
    static Cliente ClientaDe(DeliveryPoint p) => Object.FindObjectsByType<Cliente>(FindObjectsSortMode.None).FirstOrDefault(c => c.Punto == p);
    static TMP_Text GloboDe(Cliente c) => new SerializedObject(c).FindProperty("globo").objectReferenceValue as TMP_Text;
    static bool DiceGracias(Cliente c) => GloboDe(c) is TMP_Text g && g.gameObject.activeSelf && g.text.StartsWith("¡Gracias");
    static void AbrirEleccion() =>
        typeof(LevelManager).GetMethod("AbrirEleccionDeMoto", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(Object.FindFirstObjectByType<LevelManager>(), new object[] { GM, Jugador() });

    static void PasoEntrega()
    {
        switch (Paso)
        {
            case 50:
            {
                Log("8) Flecha guía, mochila limpia y entrega del producto (3 pedidos)");
                var j = Jugador();
                Comprobar(j.GetComponentsInChildren<SpriteRenderer>(true).Length == 1 && j.GetComponent<UnityEngine.Rendering.SortingGroup>() == null,
                          "el repartidor no tiene sprites hijos (nada asomando de la mochila)");
                Comprobar(Flecha() != null && Animacion() != null, "FlechaPedido y EntregaProducto en la escena");
                Entrega = 0;
                Siguiente(0.2f);
                break;
            }
            case 51:
                MoverJunto(new Vector2(-5f, 0f));
                Siguiente(0.3f);
                break;
            case 52:
            {
                // la flecha: a ~1.4 del pecho y apuntando al centro del pedido
                var f = Flecha();
                Vector2 pecho = (Vector2)Jugador().transform.position + Vector2.up * 0.9f;
                Vector2 hacia = GM.PuntoActual.Centro() - pecho;
                float dist = Vector2.Distance(f.transform.position, pecho);
                float error = Vector2.Angle(f.transform.right, hacia);
                string como = Jugador().EnMoto ? "en moto" : "a pie";
                Comprobar(f.Visible, $"pedido {Entrega + 1} ({como}): la flecha se ve lejos del punto");
                Comprobar(Mathf.Abs(dist - 1.4f) <= 0.13f && error < 2f, $"  orbita a {dist:0.00} del pecho y apunta con {error:0.0}° de error");
                Comprobar(f.GetComponentInChildren<SpriteRenderer>().sortingOrder == 90, "  Order in Layer 90");
                Capturar($"Flecha_{Entrega + 1}.png", true);
                if (Entrega == 0) { Indice = 0; Siguiente(0.1f); }
                else { Paso = 55; Esperar(0.1f); }
                break;
            }
            case 53:
                Tecla(Direcciones[Indice].tecla);
                Siguiente(0.35f);
                break;
            case 54:
            {
                var nombre = Direcciones[Indice].nombre;
                Comprobar(Jugador().GetComponentsInChildren<SpriteRenderer>(true).Length == 1, $"mochila limpia caminando: {nombre}");
                Capturar($"Mochila_{nombre}.png", true);
                Soltar();
                Indice++;
                if (Indice < Direcciones.Length) { Paso = 53; Esperar(0.25f); }
                else { MoverJunto(new Vector2(-5f, 0f)); Siguiente(0.3f); }
                break;
            }
            case 55:
                if (Entrega == 2)
                {
                    // en moto: de lado (mirando a la derecha) para ver que sale de la caja trasera
                    Tecla(Key.D);
                    Siguiente(0.2f);
                    break;
                }
                Siguiente(0f);
                break;
            case 56:
                Soltar();
                MoverJunto(new Vector2(0f, -0.7f));
                Siguiente(0.25f);
                break;
            case 57:
            {
                var clienta = ClientaDe(GM.PuntoActual);
                SessionState.SetString(ClaveDato, $"{GM.ProductoActual}|{GM.PuntoActual.name}|{GM.Entregados}");
                if (!EsperarA(() => clienta.GloboVisible, 8, "a que la clienta salga")) return;
                Comprobar(!Flecha().Visible && Marcador(GM.PuntoActual).Listo, "  dentro del radio de entrega: la flecha se esconde (aro verde)");
                Tecla(Key.E);
                Siguiente(0.22f);   // a mitad del vuelo (0.5 s)
                break;
            }
            case 58:
            {
                Soltar();
                var dato = SessionState.GetString(ClaveDato, "").Split('|');
                var punto = Puntos().First(p => p.name == dato[1]);
                var clienta = ClientaDe(punto);
                var a = Animacion();
                var sr = new SerializedObject(a).FindProperty("producto").objectReferenceValue as SpriteRenderer;
                var catalogo = new SerializedObject(a).FindProperty("catalogo").objectReferenceValue as CatalogoProductos;
                Comprobar(GM.Entregados == int.Parse(dato[2]) + 1, $"  entregado en {punto.name}; pago registrado");
                Comprobar(a.Volando && sr.enabled && sr.sortingOrder == 95 && a.UltimoProducto.ToString() == dato[0] && sr.sprite == catalogo.Icono(a.UltimoProducto),
                          $"  vuela el ícono de {dato[0]} (Order in Layer 95)");
                Comprobar(!DiceGracias(clienta), "  la clienta todavía no dice \"¡Gracias!\" mientras vuela");
                var j = Jugador();
                Vector3 d = a.Salida - j.transform.position;
                if (j.EnMoto)
                    Comprobar(j.Orientacion == "Lado" && d.x < -0.6f && d.y > 0.9f && d.y < 1.3f, $"  en moto sale de la caja trasera ({d.x:0.00}, {d.y:0.00} desde el pivote)");
                else
                    Comprobar(d.y > 1f && d.y < 1.5f, $"  a pie sale de la mochila ({d.x:0.00}, {d.y:0.00} desde el pivote)");
                Comprobar(GM.NivelTerminado || GM.PuntoActual != punto, "  el siguiente pedido ya está asignado (no espera a la animación)");
                Capturar($"Entrega_{Entrega + 1}.png", true);
                Siguiente(0.05f);
                break;
            }
            case 59:
            {
                var dato = SessionState.GetString(ClaveDato, "").Split('|');
                var clienta = ClientaDe(Puntos().First(p => p.name == dato[1]));
                if (!EsperarA(() => !Animacion().Volando, 2, "a que llegue el producto")) return;
                // unos cuadros del juego para que la clienta reaccione (la prueba no va al paso del juego)
                if (!SessionState.GetBool("PruebaIntro.llego", false)) { SessionState.SetBool("PruebaIntro.llego", true); Esperar(0.1f); return; }
                SessionState.EraseBool("PruebaIntro.llego");
                Comprobar(DiceGracias(clienta), $"  al llegar: \"{GloboDe(clienta).text.Replace("\n", " ")}\"");
                Entrega++;
                if (Entrega == 2)
                {
                    Log("  el 3.er pedido en moto (clasica_azul elegida en ELIGE TU MOTO)");
                    PlayerPrefs.SetInt("motos_compradas", 15);   // solo durante la prueba (se restaura al final)
                    AbrirEleccion();
                    Siguiente(0.5f);
                    break;
                }
                if (Entrega < 3) { Paso = 51; Esperar(0.3f); break; }
                Comprobar(GM.NivelTerminado && !Flecha().Visible, "nivel terminado: la flecha se esconde");
                Paso = 90;
                EditorApplication.isPlaying = false;
                Esperar(1f);
                break;
            }
            case 60:
                Clic("Canvas/TiendaMotos_Fondo/Ventana/Fila/Tarjeta_clasica_azul/Boton");
                Siguiente(0.2f);
                break;
            case 61:
                Clic("Canvas/TiendaMotos_Fondo/Ventana/BotonEmpezar");
                Siguiente(0.5f);
                break;
            case 62:
                Comprobar(Jugador().EnMoto, "  en moto");
                Paso = 51; Esperar(0.1f);
                break;
        }
    }

    // El avance guardado (dinero, motos y la equipada) se guarda al empezar y se deja igual al terminar:
    // la prueba entrega pedidos y equipa motos, pero no cambia la partida
    static readonly string[] ClavesProgreso = { "dinero_total", "motos_compradas", "moto_equipada" };
    static void GuardarProgreso()
    {
        foreach (var k in ClavesProgreso)
            SessionState.SetString("PruebaIntro.pp." + k, PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k).ToString() : "");
    }
    static void RestaurarProgreso()
    {
        foreach (var k in ClavesProgreso)
        {
            var v = SessionState.GetString("PruebaIntro.pp." + k, null);
            if (v == null) continue;
            if (v == "") PlayerPrefs.DeleteKey(k); else PlayerPrefs.SetInt(k, int.Parse(v));
        }
        PlayerPrefs.Save();
        Log($"Avance restaurado: dinero {PlayerPrefs.GetInt("dinero_total", 0)}, motos {PlayerPrefs.GetInt("motos_compradas", 0)}, equipada {PlayerPrefs.GetInt("moto_equipada", -1)}");
    }

    // el repartidor (y la cámara, para no esperar el seguimiento) a una distancia del punto activo
    static void MoverJunto(Vector2 desplazamiento)
    {
        var pc = Object.FindFirstObjectByType<PlayerController>();
        Vector2 destino = GM.PuntoActual.Centro() + desplazamiento;
        pc.GetComponent<Rigidbody2D>().position = destino;
        pc.transform.position = destino;
        var cam = Camera.main.transform;
        cam.position = new Vector3(destino.x, destino.y, cam.position.z);
    }

    static void PasoMarcador()
    {
        switch (Paso)
        {
            case 30:
            {
                Log("7) Marcador del pedido en el nivel 1");
                var puntos = Puntos();
                Comprobar(puntos.Length == 28, $"{puntos.Length} puntos de entrega");
                Comprobar(Object.FindObjectsByType<FlechaGuia>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1, "una sola flecha guía en el mundo");
                Comprobar(Puntos().All(p => p.GetComponentsInChildren<Component>(true).All(c => c != null && c.GetType().Name != "BrilloEstrellas")), "sin BrilloEstrellas en los puntos");
                var original = GM.PuntoActual;
                int bien = 0;
                foreach (var p in puntos)
                {
                    foreach (var q in puntos) q.SetActivo(q == p);
                    var activos = ConMarcador();
                    var m = Marcador(p);
                    if (activos.Count == 1 && activos[0] == p && m != null && m.BurbujaVisible && Parte(m, "aro").enabled) bien++;
                    else Fallo($"{p.name}: al activarlo hay {activos.Count} marcadores prendidos");
                }
                foreach (var q in puntos) q.SetActivo(q == original);
                Comprobar(bien == puntos.Length, $"los {bien} de {puntos.Length} puntos prenden su marcador y solo el suyo");
                // 40 pedidos seguidos: nunca el mismo producto dos veces y salen los 6
                var vistos = new List<Producto>();
                for (int i = 0; i < 40; i++)
                {
                    NuevoPedido();
                    vistos.Add(GM.ProductoActual);
                    if (Marcador(GM.PuntoActual).ProductoMostrado != GM.ProductoActual) { Fallo("el marcador no muestra el producto del pedido"); break; }
                }
                Comprobar(vistos.Zip(vistos.Skip(1), (a, b) => a != b).All(x => x), "40 pedidos sin repetir el producto del anterior");
                Comprobar(vistos.Distinct().Count() == CatalogoProductos.Cantidad, $"salen los {vistos.Distinct().Count()} productos");
                // un pedido nuevo de verdad (su clienta sale ahora) y el repartidor lejos
                NuevoPedido();
                SessionState.SetString(ClaveDato, GM.ProductoActual + "|" + GM.PuntoActual.name);
                MoverJunto(new Vector2(-4.5f, 0f));
                Siguiente(0.35f);
                break;
            }
            case 31:
            {
                var m = Marcador(GM.PuntoActual);
                var aro = Parte(m, "aro");
                Log($"  pedido en {GM.PuntoActual.name}: {GM.ProductoActual}");
                Comprobar(ConMarcador().Count == 1, "solo el punto activo tiene marcador");
                Comprobar(m.BurbujaVisible && Parte(m, "producto").sprite == IconoDe(GM.ProductoActual), "lejos: burbuja con el ícono del producto");
                Comprobar(!m.Listo && aro.sprite.name.StartsWith("aro_dorado"), $"lejos: aro dorado ({aro.sprite.name})");
                Comprobar(IconoHud() == IconoDe(GM.ProductoActual), "el HUD muestra el mismo ícono");
                Capturar("MarcadorLejos.png");
                MoverJunto(new Vector2(0f, -0.7f));
                Siguiente(0.1f);
                break;
            }
            case 32:
            {
                var m = Marcador(GM.PuntoActual);
                Comprobar(m.Listo && Parte(m, "aro").sprite.name.StartsWith("aro_verde"), $"en el radio: aro verde ({Parte(m, "aro").sprite.name})");
                Comprobar(m.EscalaBurbuja > 1.03f, $"la burbuja rebota al entrar (escala {m.EscalaBurbuja:0.00})");
                Siguiente(0.3f);
                break;
            }
            case 33:
                Capturar("MarcadorCerca.png");
                Siguiente(0.1f);
                break;
            case 34:
            {
                var clienta = Object.FindObjectsByType<Cliente>(FindObjectsSortMode.None).FirstOrDefault(c => c.Punto == GM.PuntoActual);
                if (!EsperarA(() => clienta.GloboVisible, 8, "el globo de la clienta")) return;
                // un momento para que corran unos cuadros del juego (la prueba no va al paso del juego)
                if (!SessionState.GetBool("PruebaIntro.globo", false)) { SessionState.SetBool("PruebaIntro.globo", true); Esperar(0.1f); return; }
                SessionState.EraseBool("PruebaIntro.globo");
                var m = Marcador(GM.PuntoActual);
                Comprobar(clienta.GloboVisible && !m.BurbujaVisible && Parte(m, "aro").enabled, "con el globo de la clienta: sin burbuja, con aro");
                Capturar("MarcadorGlobo.png");
                Tecla(Key.E);
                Siguiente(0.15f);
                break;
            }
            case 35:
            {
                Soltar();
                var dato = SessionState.GetString(ClaveDato, "").Split('|');
                var anterior = Puntos().First(p => p.name == dato[1]);
                Comprobar(GM.Entregados >= 1 && !anterior.transform.GetChild(0).gameObject.activeSelf, $"tras entregar en {anterior.name} su marcador se apaga");
                Comprobar(GM.NivelTerminado || (GM.PuntoActual != anterior && GM.ProductoActual.ToString() != dato[0]),
                          $"el pedido siguiente cambia de ícono ({dato[0]} -> {GM.ProductoActual})");
                Comprobar(GM.NivelTerminado ? ConMarcador().Count == 0 : ConMarcador().Count == 1 && ConMarcador()[0] == GM.PuntoActual, "solo queda el marcador del pedido nuevo");
                Siguiente(0.2f);
                break;
            }
            case 36:
                // se entregan los que faltan hasta terminar el nivel
                Soltar();
                if (!GM.NivelTerminado)
                {
                    MoverJunto(new Vector2(0f, -0.7f));
                    Paso = 37; Esperar(0.15f);
                    break;
                }
                Comprobar(ConMarcador().Count == 0, "nivel terminado: no queda ningún marcador");
                Paso = 90;
                EditorApplication.isPlaying = false;
                Esperar(1f);
                break;
            case 37:
                Tecla(Key.E);
                Paso = 36; Esperar(0.15f);
                break;
        }
    }

    static void Siguiente(float espera) { Paso++; Esperar(espera); }

    static bool EsperarA(Func<bool> condicion, float limite, string que)
    {
        float ahora = (float)EditorApplication.timeSinceStartup;
        if (SessionState.GetFloat(ClaveDesde, 0) == 0) SessionState.SetFloat(ClaveDesde, ahora);
        bool listo = false;
        try { listo = condicion(); } catch (NullReferenceException) { }   // la escena todavía está cargando
        if (!listo && ahora - SessionState.GetFloat(ClaveDesde, 0) < limite) { Esperar(0.1f); return false; }
        if (!listo) Fallo($"se acabó el tiempo esperando {que}");
        SessionState.SetFloat(ClaveDesde, 0);
        return true;
    }

    static bool EsperarIntro()
    {
        if (!EsperarA(() => Escena == "Intro" && Object.FindFirstObjectByType<IntroHistoria>() != null, 10, "que cargue la Intro")) return false;
        Comprobar(Escena == "Intro", $"Iniciar juego carga {Escena}");
        return true;
    }

    // el menú ya dibujó un par de fotogramas (antes el raycast no da en nada)
    static bool EsperarMenu() => EsperarA(() => Escena == "MenuPrincipal" && Time.timeSinceLevelLoad > 0.3f
                                                && EventSystem.current != null && Buscar("Canvas/BotonIniciar") != null, 15, "a que arranque el menú");

    // la viñeta terminó de escribirse: se enciende "Espacio para continuar"
    static bool EsperarListo(string inicio)
    {
        if (!EsperarA(() => Texto().text.StartsWith(inicio) && Indicador().activeInHierarchy, 10, $"la viñeta que empieza con \"{inicio}\"")) return false;
        Log($"  viñeta \"{inicio}…\" completa, indicador visible");
        return true;
    }

    static bool EsperarJuego(string como)
    {
        if (!EsperarA(() => Escena == "SampleScene" && GameManager.Instance != null && GameManager.Instance.PuntoActual != null, 15,
                      "que cargue SampleScene")) return false;
        Comprobar(Escena == "SampleScene" && Time.timeScale == 1f, $"la intro termina {como} y carga {Escena} (timeScale {Time.timeScale})");
        return true;
    }

    static IntroHistoria Intro() => Object.FindFirstObjectByType<IntroHistoria>();
    static T Campo<T>(string nombre) where T : Object => new SerializedObject(Intro()).FindProperty(nombre).objectReferenceValue as T;
    static TextMeshProUGUI Texto() => Campo<TextMeshProUGUI>("texto");
    static TextMeshProUGUI Titular() => Campo<TextMeshProUGUI>("titular");
    static TextMeshProUGUI TextoIndicador() => Campo<TextMeshProUGUI>("textoIndicador");
    static Image Imagen() => Campo<Image>("imagen");
    static GameObject Indicador() => Campo<GameObject>("indicadorSiguiente");
    static bool TextoCompleto() => Texto().maxVisibleCharacters >= Texto().textInfo.characterCount && Texto().textInfo.characterCount > 0;

    // ---- Entrada simulada ----
    static void Tecla(Key k) => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k));
    static void Soltar() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
    // el puntero en una esquina donde no hay botones (abajo a la izquierda, sobre la viñeta). Va por un ratón
    // virtual que se vuelve Mouse.current: el ratón real del editor manda su propio estado en cada cuadro y
    // pisaba el clic simulado antes de que el juego lo leyera
    static Mouse Raton()
    {
        var r = InputSystem.GetDevice<Mouse>("RatonPrueba") ?? InputSystem.AddDevice<Mouse>("RatonPrueba");
        r.MakeCurrent();
        return r;
    }
    static void ClicRaton() => InputSystem.QueueStateEvent(Raton(), new MouseState { position = new Vector2(20, 20) }.WithButton(MouseButton.Left));
    static void SoltarRaton()
    {
        InputSystem.QueueStateEvent(Raton(), new MouseState { position = new Vector2(20, 20) });
    }
    static void QuitarRaton() { if (InputSystem.GetDevice<Mouse>("RatonPrueba") is Mouse r) InputSystem.RemoveDevice(r); }

    // Mientras dura Play, una copia de los ajustes del Input System que no depende del foco (la ventana
    // de Unity o la Game view pueden no tenerlo). La copia no es un asset: no se guarda nada y al salir
    // de Play se vuelven a poner los del proyecto
    static InputSettings ajustesProyecto;

    // Vuelve a poner los ajustes del proyecto. Si la copia se perdió (recarga del dominio a media prueba),
    // se toman del asset configurado del proyecto; si no, el Input System avisa en cada cambio de Play.
    static void RestaurarEntrada()
    {
        if (ajustesProyecto == null)
            EditorBuildSettings.TryGetConfigObject("com.unity.input.settings", out ajustesProyecto);
        if (ajustesProyecto != null && InputSystem.settings != ajustesProyecto) InputSystem.settings = ajustesProyecto;
        ajustesProyecto = null;
    }

    static void EntradaSinFoco()
    {
        ajustesProyecto = InputSystem.settings;
        var ajustes = Object.Instantiate(InputSystem.settings);
        ajustes.hideFlags = HideFlags.HideAndDontSave;   // que no la descargue el cambio de escena (Unload unused assets)
        ajustes.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        ajustes.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings = ajustes;
    }

    // el Input System solo escucha al teclado si la Game view tiene el foco
    static void EnfocarGameView()
    {
        var tipo = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if (tipo != null) EditorWindow.FocusWindowIfItsOpen(tipo);
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

    // Render a 1920x1080 con la cámara de la escena, con el Canvas en Screen Space - Camera solo durante el render
    // con jugador: además guarda <archivo>_recorte.png, 400x440 alrededor del repartidor
    static void Capturar(string archivo, bool jugador = false)
    {
        var ruta = Path.Combine(SessionState.GetString(ClaveCarpeta, "Temp"), archivo);
        var cam = Camera.main;
        var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.isRootCanvas);
        var rt = new RenderTexture(1920, 1080, 24);
        var modo = canvas.renderMode;
        try
        {
            cam.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = cam.nearClipPlane + 1;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            File.WriteAllBytes(ruta, tex.EncodeToPNG());
            if (jugador)
            {
                var pc = Object.FindFirstObjectByType<PlayerController>();
                Vector3 p = cam.WorldToScreenPoint(pc.transform.position + Vector3.up * 1.1f);
                int w = 400, h = 440;
                int x0 = Mathf.Clamp(Mathf.RoundToInt(p.x) - w / 2, 0, 1920 - w), y0 = Mathf.Clamp(Mathf.RoundToInt(p.y) - h / 2, 0, 1080 - h);
                var recorte = new Texture2D(w, h, TextureFormat.RGB24, false);
                recorte.SetPixels(tex.GetPixels(x0, y0, w, h));
                recorte.Apply();
                File.WriteAllBytes(Path.ChangeExtension(ruta, null) + "_recorte.png", recorte.EncodeToPNG());
                Object.DestroyImmediate(recorte);
            }
            Object.DestroyImmediate(tex);
        }
        finally
        {
            RenderTexture.active = null;
            cam.targetTexture = null;
            canvas.renderMode = modo;
            rt.Release();
        }
        Log($"  captura 1920x1080: {ruta}");
    }
}
