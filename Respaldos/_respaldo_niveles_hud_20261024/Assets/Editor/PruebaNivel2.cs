using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Prueba en Play del flujo completo hasta el nivel 2, empezando en MenuPrincipal (como el build):
// Menú → Iniciar juego → Intro → Saltar → nivel 1 → se entregan sus pedidos → panel final con "Siguiente nivel" →
// ELIGE TU MOTO (EMPEZAR NIVEL) → Nivel2_Comercial: revisa LevelManager, luz de atardecer, que ningún auto se vea
// al empezar, el plan de SeleccionPedidos (5 distintos, 20 tiles entre uno y otro, norte y sur), separación de los
// autos durante 20 s de juego y que no aparezcan a la vista; captura un negocio con su punto y una calle con
// tráfico; entrega los 5 pedidos y revisa el panel final (Jugar de nuevo, Menú y Tienda; sin Siguiente).
// El avance guardado (dinero, motos compradas y equipada) queda como estaba al empezar.
// Temp/PruebaNivel2.pedido (dentro: carpeta para las capturas) → Temp/PruebaNivel2.resultado
[InitializeOnLoad]
public static class PruebaNivel2
{
    const string Pedido = "Temp/PruebaNivel2.pedido";
    const string Resultado = "Temp/PruebaNivel2.resultado";
    const string Cancelar = "Temp/PruebaNivel2.cancelar";
    const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";
    const string P = "PruebaNivel2.";
    static readonly string[] ClavesProgreso = { "dinero_total", "motos_compradas", "moto_equipada" };

    static bool enganchado;

    static PruebaNivel2()
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
            SessionState.SetInt(P + "sub3", 0);
            SessionState.SetInt(P + "sub5", 0);
            SessionState.SetInt(P + "sub6", 0);
            foreach (var k in ClavesProgreso)
                SessionState.SetString(P + "pp." + k, PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k).ToString() : "");
            var inicio = EditorSceneManager.playModeStartScene;
            SessionState.SetString(P + "inicio", inicio != null ? AssetDatabase.GetAssetPath(inicio) : "");
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
            File.Delete(Cancelar);
            Fallo($"cancelada en el paso {Paso}");
            Paso = 90;
            EditorApplication.isPlaying = false;
            return;
        }
        if (Paso == 41) Muestrear();   // cada cuadro del editor mientras corre el tráfico
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
                Application.runInBackground = true;
                if (!EsperarA(() => Escena == "MenuPrincipal" && Time.timeSinceLevelLoad > 0.3f && EventSystem.current != null
                                    && Buscar("Canvas/BotonIniciar") != null, 15, "el menú")) return;
                Log("1) Menú → Iniciar juego");
                Clic("Canvas/BotonIniciar");
                Siguiente(0.5f);
                break;
            case 2:
                if (!EsperarA(() => SeleccionPrueba.Pasar() && Escena == "Intro" && Time.timeSinceLevelLoad > 0.5f, 10, "la Intro")) return;
                Comprobar(Escena == "Intro", "Iniciar juego carga la Intro");
                Log("2) Intro → Saltar");
                Clic("Canvas/BotonSaltar");
                Siguiente(0.3f);
                break;
            case 3:
                if (!EsperarA(() => Escena == "SampleScene" && GM != null && GM.PuntoActual != null, 15, "el nivel 1")) return;
                // 3a) HUD del nivel 1, 3b) pausa (abrir y seguir); sub-pasos para no mover la numeración
                switch (SessionState.GetInt(P + "sub3", 0))
                {
                    case 0:
                    {
                        Comprobar(Escena == "SampleScene", "la Intro lleva al nivel 1");
                        Log("3a) Nivel 1: HUD");
                        string Texto(string r) => Buscar(r)?.GetComponent<TMP_Text>()?.text;
                        bool Visible(string r) { var t = Buscar(r); return t != null && t.gameObject.activeInHierarchy; }
                        Comprobar(Visible("Canvas/PlacaPedidos") && Texto("Canvas/PlacaPedidos/TextoPedidos") == $"Pedidos 0/{GM.PedidosMeta}",
                                  $"pedidos: \"{Texto("Canvas/PlacaPedidos/TextoPedidos")}\"");
                        var relleno = Buscar("Canvas/MedidorSatisfaccion/Pista/Relleno")?.GetComponent<UnityEngine.UI.Image>();
                        Comprobar(Visible("Canvas/MedidorSatisfaccion") && relleno != null && relleno.fillAmount > 0.9f, $"barra de satisfacción ({relleno?.fillAmount:0.00})");
                        var caritas = Buscar("Canvas/MedidorSatisfaccion/Caritas");
                        Comprobar(caritas != null && caritas.childCount == 3 && caritas.gameObject.activeInHierarchy, "3 caritas");
                        Comprobar(Visible("Canvas/PlacaDinero") && Texto("Canvas/PlacaDinero/TextoDinero") == "$0", $"dinero: \"{Texto("Canvas/PlacaDinero/TextoDinero")}\"");
                        Comprobar(Visible("Canvas/BotonPausa") && Alcanzable(Buscar("Canvas/BotonPausa")), "botón de pausa visible y recibe el clic");
                        Comprobar(Visible("Canvas/GuiaPedido"), "guía del pedido visible");
                        Capturar("HUD_nivel1.png");
                        Log("3b) Pausa");
                        Clic("Canvas/BotonPausa");
                        SessionState.SetInt(P + "sub3", 1);
                        Esperar(0.4f);
                        return;
                    }
                    case 1:
                        Comprobar(Buscar("Canvas/PanelPausa") != null && Buscar("Canvas/PanelPausa").gameObject.activeInHierarchy && Time.timeScale == 0f,
                                  $"el botón de pausa abre PanelPausa y detiene el juego (timeScale {Time.timeScale})");
                        Capturar("Pausa_nivel1.png");
                        Clic("Canvas/PanelPausa/PausaSeguir");
                        SessionState.SetInt(P + "sub3", 2);
                        Esperar(0.4f);
                        return;
                    default:
                        Comprobar(!Buscar("Canvas/PanelPausa").gameObject.activeInHierarchy && Time.timeScale == 1f, "Seguir cierra la pausa y el juego sigue");
                        SessionState.SetInt(P + "sub3", 0);
                        break;
                }
                Log($"3c) Nivel 1: se entregan sus {GM.PedidosMeta} pedidos");
                Siguiente(0.5f);
                break;
            case 4:
                if (GM.NivelTerminado) { Siguiente(2.5f); break; }
                GM.Entregar(GM.PuntoActual);
                Esperar(0.4f);
                break;
            case 5:
            {
                // tras la captura el Canvas vuelve a Overlay y se acomoda en el siguiente cuadro: el clic va después
                if (SessionState.GetInt(P + "sub5", 0) == 1)
                {
                    SessionState.SetInt(P + "sub5", 0);
                    Log("4) Clic en Siguiente nivel");
                    Clic(RutaDe(Panel().Find("BotonSiguiente")));
                    Siguiente(0.5f);
                    break;
                }
                if (!EsperarA(() => Panel() != null && Panel().gameObject.activeInHierarchy, 10, "el panel final del nivel 1")) return;
                var sig = Panel().Find("BotonSiguiente");
                Comprobar(sig != null && sig.gameObject.activeSelf, "nivel 1: \"Siguiente nivel\" visible (Nivel2_Comercial existe)");
                foreach (var b in new[] { "BotonJugarDeNuevo", "BotonMenu", "BotonSiguiente" })
                {
                    var t = Panel().Find(b);
                    Comprobar(t != null && t.gameObject.activeInHierarchy && Alcanzable(t), $"nivel 1: {b} visible y recibe el clic");
                }
                var tiendaN1 = Panel().Find("BotonTienda");
                Comprobar(tiendaN1 == null || !tiendaN1.gameObject.activeInHierarchy, "nivel 1: sin botón de tienda");
                var estrellas = Panel().Find("Estrellas");
                Comprobar(estrellas != null && estrellas.gameObject.activeInHierarchy && estrellas.childCount == 3, "nivel 1: 3 estrellas en el panel");
                var pedidosHUD = Buscar("Canvas/PlacaPedidos/TextoPedidos")?.GetComponent<TMP_Text>()?.text;
                var dineroHUD = Buscar("Canvas/PlacaDinero/TextoDinero")?.GetComponent<TMP_Text>()?.text;
                Comprobar(pedidosHUD == $"Pedidos {GM.PedidosMeta}/{GM.PedidosMeta}" && dineroHUD == "$" + GM.Dinero && GM.Dinero > 0,
                          $"el HUD se actualizó: \"{pedidosHUD}\", \"{dineroHUD}\"");
                Capturar("PanelFinal_nivel1.png");
                SessionState.SetInt(P + "sub5", 1);
                Esperar(0.3f);
                break;
            }
            case 6:
            {
                // como en el paso 5: el clic en EMPEZAR va un cuadro después de la captura
                if (SessionState.GetInt(P + "sub6", 0) == 1)
                {
                    SessionState.SetInt(P + "sub6", 0);
                    Clic(RutaDe(Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                     .First(t => t.name == "TiendaMotos_Fondo").Find("Ventana/BotonEmpezar")));
                    Siguiente(0.5f);
                    break;
                }
                if (!EsperarA(() => Escena == "Nivel2_Comercial" && Object.FindFirstObjectByType<TiendaMotos>() != null
                                    && Object.FindFirstObjectByType<TiendaMotos>().Abierta, 15, "el nivel 2 con ELIGE TU MOTO")) return;
                var tienda = Object.FindFirstObjectByType<TiendaMotos>();
                var ventana = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "TiendaMotos_Fondo");
                var titulo = ventana.Find("Ventana/Titulo")?.GetComponent<TMP_Text>();
                Log("5) Nivel 2: ELIGE TU MOTO");
                Comprobar(Escena == "Nivel2_Comercial", "Siguiente nivel carga Nivel2_Comercial");
                Comprobar(tienda.ModoActual == TiendaMotos.Modo.AntesDeNivel && titulo != null && titulo.text == "ELIGE TU MOTO", $"tienda en modo AntesDeNivel (\"{titulo?.text}\")");
                Comprobar(Time.timeScale == 0f && GM.EnEspera && GM.PuntoActual == null, "todo quieto y sin pedido mientras se elige");
                RevisarArranque();
                Capturar("EligeTuMoto_nivel2.png");
                SessionState.SetInt(P + "sub6", 1);
                Esperar(0.3f);
                break;
            }
            case 7:
            {
                if (!EsperarA(() => !GM.EnEspera && GM.PuntoActual != null && Time.timeScale == 1f, 5, "que empiece el nivel 2")) return;
                Log("6) Nivel 2 en marcha");
                var nivel = Object.FindFirstObjectByType<LevelManager>();
                var so = new SerializedObject(nivel);
                Comprobar(nivel.NombreNivel == "Comercial" && GM.PedidosMeta == 5 && Mathf.Approximately(so.FindProperty("velocidadReferencia").floatValue, 5f)
                          && nivel.MotosHabilitadas && so.FindProperty("escenaSiguiente").stringValue == "Nivel3_Lluvia", "LevelManager: Comercial, 5 pedidos, velocidad 5, motos, siguiente Nivel3_Lluvia");
                var luz = Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None).First(l => l.lightType == Light2D.LightType.Global);
                Comprobar(ColorUtility.ToHtmlStringRGB(luz.color) == "FFB880" && Mathf.Approximately(luz.intensity, 0.95f), $"atardecer #{ColorUtility.ToHtmlStringRGB(luz.color)} a {luz.intensity}");
                Comprobar(Object.FindFirstObjectByType<PlayerController>().MotosHabilitadas, "el repartidor puede ir en moto");
                RevisarPlan();
                SessionState.SetFloat(P + "sep", 999f);
                SessionState.SetInt(P + "aparecen", 0);
                SessionState.SetString(P + "vistos", "");
                SessionState.SetFloat(P + "hasta", (float)EditorApplication.timeSinceStartup + 20f);
                Log("7) 20 s de tráfico: separación por carril y que nadie aparezca a la vista");
                Paso = 41;
                break;
            }
            case 41:
                if (EditorApplication.timeSinceStartup < SessionState.GetFloat(P + "hasta", 0)) return;
                float sep = SessionState.GetFloat(P + "sep", 999f);
                Comprobar(sep >= 9.5f, $"separación mínima entre autos del mismo carril: {sep:0.0} tiles");
                Comprobar(SessionState.GetInt(P + "aparecen", 0) == 0, $"autos que aparecieron a la vista: {SessionState.GetInt(P + "aparecen", 0)}");
                Paso = 8;
                break;
            case 8:
            {
                Log("8) Captura de un negocio con su punto de entrega");
                var p = GM.PuntoActual;
                MoverA(p.Centro() + new Vector2(-3f, -0.2f));
                Siguiente(1.2f);
                break;
            }
            case 9:
                Capturar("Negocio_punto.png");
                Log("9) Captura de una calle con tráfico (cruce de Av. Comercio y Calle Roble)");
                MoverA(new Vector2(37.2f, 38.2f));
                Siguiente(2.5f);
                break;
            case 10:
                Capturar("Calle_trafico.png");
                Log("10) Se entregan los 5 pedidos");
                SessionState.SetString(P + "orden", "");
                Siguiente(0.3f);
                break;
            case 11:
                if (GM.NivelTerminado) { Log("  orden: " + SessionState.GetString(P + "orden", "")); Siguiente(2.5f); break; }
                SessionState.SetString(P + "orden", SessionState.GetString(P + "orden", "") + GM.PuntoActual.name.Replace("PuntoEntrega_", "#") + $" ({GM.TiempoIdeal:0.0} s) ");
                GM.Entregar(GM.PuntoActual);
                Esperar(0.5f);
                break;
            case 12:
            {
                if (!EsperarA(() => Panel() != null && Panel().gameObject.activeInHierarchy, 10, "el panel final del nivel 2")) return;
                var panel = Panel();
                Log("11) Panel final del nivel 2");
                foreach (var b in new[] { "BotonJugarDeNuevo", "BotonMenu", "BotonTienda" })
                {
                    var t = panel.Find(b);
                    Comprobar(t != null && t.gameObject.activeInHierarchy && Alcanzable(t), $"{b} visible y recibe el clic");
                }
                var sig = panel.Find("BotonSiguiente");
                Comprobar(sig != null && sig.gameObject.activeSelf && Alcanzable(sig), "\"Siguiente nivel\" visible (Nivel3_Lluvia existe)");
                Capturar("PanelFinal_nivel2.png");
                Paso = 90;
                EditorApplication.isPlaying = false;
                Esperar(1f);
                break;
            }
            case 90:
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                foreach (var k in ClavesProgreso)
                {
                    var v = SessionState.GetString(P + "pp." + k, null);
                    if (v == null) continue;
                    if (v == "") PlayerPrefs.DeleteKey(k); else PlayerPrefs.SetInt(k, int.Parse(v));
                }
                PlayerPrefs.Save();
                Log($"Avance restaurado: dinero {PlayerPrefs.GetInt("dinero_total", 0)}, motos {PlayerPrefs.GetInt("motos_compradas", 0)}, equipada {PlayerPrefs.GetInt("moto_equipada", -1)}");
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

    // ---- nivel 2 ----

    static Rect Vista()
    {
        var cam = Camera.main;
        float h = cam.orthographicSize, w = h * cam.aspect;
        return new Rect(cam.transform.position.x - w, cam.transform.position.y - h, 2 * w, 2 * h);
    }

    static Rect Caja(TrafficCar a)
    {
        var b = a.GetComponent<SpriteRenderer>().bounds;
        return new Rect(b.min.x, b.min.y, b.size.x, b.size.y);
    }

    static void RevisarArranque()
    {
        var autos = Object.FindObjectsByType<TrafficCar>(FindObjectsSortMode.None);
        var vista = Vista();
        var visibles = autos.Where(a => Caja(a).Overlaps(vista)).Select(a => a.name).ToList();
        Comprobar(visibles.Count == 0, $"ningún auto a la vista al empezar ({autos.Length} autos; vista x {vista.xMin:0.0}..{vista.xMax:0.0}, y {vista.yMin:0.0}..{vista.yMax:0.0})" +
                                       (visibles.Count > 0 ? ": " + string.Join(", ", visibles) : ""));
        var pc = Object.FindFirstObjectByType<PlayerController>();
        var duena = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Duena");
        Comprobar(duena != null && Mathf.Abs(duena.transform.position.x - pc.transform.position.x) < 0.5f,
                  $"el repartidor arranca frente al Salón Brenn's ({pc.transform.position.x:0.0}, {pc.transform.position.y:0.0})");
    }

    static void RevisarPlan()
    {
        var sel = Object.FindFirstObjectByType<SeleccionPedidos>();
        if (sel == null) { Fallo("no hay SeleccionPedidos"); return; }
        var plan = sel.Plan;
        float yAv = new SerializedObject(sel).FindProperty("yAvenida").floatValue;
        var spawn = (Vector2)Object.FindFirstObjectByType<PlayerController>().transform.position;
        Comprobar(plan.Count == 5 && plan.Distinct().Count() == 5, $"plan de {plan.Count} puntos distintos");
        var dist = new List<float> { Vector2.Distance(spawn, plan[0].Centro()) };
        for (int i = 1; i < plan.Count; i++) dist.Add(Vector2.Distance(plan[i - 1].Centro(), plan[i].Centro()));
        Comprobar(dist.All(d => d >= 20f), "cada pedido a 20 tiles o más del anterior: " + string.Join(", ", dist.Select(d => d.ToString("0.0"))));
        Comprobar(plan.Any(p => p.Centro().y > yAv) && plan.Any(p => p.Centro().y < yAv), "al menos uno al norte y uno al sur de la avenida");
        Comprobar(GM.PuntoActual == plan[0], "el primer pedido es el primero del plan");
        Log("  plan: " + string.Join(" → ", plan.Select(p => p.name.Replace("PuntoEntrega_", "#") + (p.Centro().y > yAv ? "N" : "S"))));
    }

    // separación entre autos del mismo carril y autos que "aparecen" a la vista (no estaban visibles en la muestra
    // anterior y ahora están dentro, a más de un largo del borde de la vista)
    static void Muestrear()
    {
        if (!EditorApplication.isPlaying) return;
        var autos = Object.FindObjectsByType<TrafficCar>(FindObjectsSortMode.None);
        var vista = Vista();
        float sep = SessionState.GetFloat(P + "sep", 999f);
        var datos = autos.Select(a => (a, c: (Vector2)a.transform.position, d: (Vector2)new SerializedObject(a).FindProperty("direccion").vector2Value.normalized)).ToList();
        foreach (var x in datos)
            foreach (var y in datos)
            {
                if (x.a == y.a || Vector2.Dot(x.d, y.d) < 0.9f) continue;
                var delta = y.c - x.c;
                if (Mathf.Abs(x.d.x * delta.y - x.d.y * delta.x) > 0.6f) continue;
                sep = Mathf.Min(sep, delta.magnitude);
            }
        SessionState.SetFloat(P + "sep", sep);
        var antes = new HashSet<string>(SessionState.GetString(P + "vistos", "").Split('|'));
        var ahora = autos.Where(a => Caja(a).Overlaps(vista)).ToList();
        var adentro = new Rect(vista.xMin + 3.5f, vista.yMin + 3.5f, vista.width - 7f, vista.height - 7f);
        foreach (var a in ahora)
            if (!antes.Contains(a.name) && Caja(a).Overlaps(adentro))
            {
                SessionState.SetInt(P + "aparecen", SessionState.GetInt(P + "aparecen", 0) + 1);
                Log($"  {a.name} apareció a la vista en {a.transform.position}");
            }
        SessionState.SetString(P + "vistos", string.Join("|", ahora.Select(a => a.name)));
    }

    static void MoverA(Vector2 destino)
    {
        var pc = Object.FindFirstObjectByType<PlayerController>();
        pc.GetComponent<Rigidbody2D>().position = destino;
        pc.transform.position = destino;
        var cam = Camera.main;
        var c = new Vector2(destino.x, destino.y + 1.25f);
        if (CameraFollow.LimitesDelMapa(out var lim))   // como CameraFollow: nunca se ve fuera del mapa
        {
            float h = cam.orthographicSize, w = h * cam.aspect;
            c.x = Mathf.Clamp(c.x, lim.xMin + w, lim.xMax - w);
            c.y = Mathf.Clamp(c.y, lim.yMin + h, lim.yMax - h);
        }
        cam.transform.position = new Vector3(c.x, c.y, cam.transform.position.z);
    }

    // ---- utilidades (las mismas que PruebaIntro) ----

    static bool EsperarA(Func<bool> condicion, float limite, string que)
    {
        float ahora = (float)EditorApplication.timeSinceStartup;
        if (SessionState.GetFloat(P + "desde", 0) == 0) SessionState.SetFloat(P + "desde", ahora);
        bool listo = false;
        try { listo = condicion(); } catch (NullReferenceException) { } catch (InvalidOperationException) { }
        if (!listo && ahora - SessionState.GetFloat(P + "desde", 0) < limite) { Esperar(0.1f); return false; }
        if (!listo) Fallo($"se acabó el tiempo esperando {que}");
        SessionState.SetFloat(P + "desde", 0);
        return true;
    }

    static Transform Panel() => Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "PanelFinal");

    static string RutaDe(Transform t) => t.parent == null ? t.name : RutaDe(t.parent) + "/" + t.name;

    static Transform Buscar(string ruta)
    {
        var partes = ruta.Split(new[] { '/' }, 2);
        var raiz = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == partes[0])?.transform;
        return raiz == null ? null : partes.Length == 1 ? raiz : raiz.Find(partes[1]);
    }

    static GameObject Arriba(Transform t, out PointerEventData datos)
    {
        datos = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, t.position) };
        var golpes = new List<RaycastResult>();
        EventSystem.current.RaycastAll(datos, golpes);
        return golpes.Count > 0 ? golpes[0].gameObject : null;
    }

    static bool Alcanzable(Transform t)
    {
        var arriba = Arriba(t, out _);
        return arriba != null && (arriba.transform == t || arriba.transform.IsChildOf(t));
    }

    static void Clic(string ruta)
    {
        var t = Buscar(ruta);
        if (t == null || !t.gameObject.activeInHierarchy) { Fallo($"{ruta} no existe o está apagado"); return; }
        var arriba = Arriba(t, out var datos);
        if (arriba == null || !(arriba.transform == t || arriba.transform.IsChildOf(t)))
        {
            Fallo($"clic en {ruta}: lo tapa {(arriba != null ? arriba.name : "nada (sin raycast)")} " +
                  $"(punto {datos.position}, pantalla {Screen.width}x{Screen.height}, EventSystem {EventSystem.current?.name})");
            return;
        }
        ExecuteEvents.ExecuteHierarchy(arriba, datos, ExecuteEvents.pointerClickHandler);
        Log($"  clic en {ruta}");
    }

    static void Capturar(string archivo)
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
            // CameraFollow limita con la proporción de la Game view; la captura es 16:9 y se limita con la suya
            if (CameraFollow.LimitesDelMapa(out var lim))
            {
                float h = cam.orthographicSize, w = h * 1920f / 1080f;
                cam.transform.position = new Vector3(Mathf.Clamp(posCamara.x, lim.xMin + w, lim.xMax - w),
                                                     Mathf.Clamp(posCamara.y, lim.yMin + h, lim.yMax - h), posCamara.z);
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
        Log($"  captura 1920x1080: {ruta}");
    }
}
