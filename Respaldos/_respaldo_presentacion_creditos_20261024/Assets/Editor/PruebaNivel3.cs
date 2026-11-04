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

// Prueba en Play del flujo completo hasta el final de la historia, empezando en MenuPrincipal (como el build):
// Menú → Intro → nivel 1 → Siguiente nivel → ELIGE TU MOTO → nivel 2 → Siguiente nivel → ELIGE TU MOTO → nivel 3
// (Lluvia): LevelManager, noche, luces y lluvia encendidas, que la lluvia se apague con el LevelManager, el plan de
// SeleccionPedidos (8 distintos, sin pares injustos), rendimiento con lluvia y luces, un bache con charco en moto;
// capturas (noche con lluvia, cruce con faroles, charco/bache y panel final); entrega los 8 pedidos y revisa el
// panel final neutro ("¡NIVEL COMPLETADO!" con "Continuar" a la escena Final y el resultado en Progreso) y que "Menú"
// vuelva al menú.
// El avance guardado (dinero, motos, moto equipada y nombre) queda como estaba al empezar.
// Temp/PruebaNivel3.pedido (dentro: carpeta para las capturas) → Temp/PruebaNivel3.resultado
[InitializeOnLoad]
public static class PruebaNivel3
{
    const string Pedido = "Temp/PruebaNivel3.pedido";
    const string Resultado = "Temp/PruebaNivel3.resultado";
    const string Cancelar = "Temp/PruebaNivel3.cancelar";
    const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";
    const string P = "PruebaNivel3.";
    const string ClaveNombre = "nombre_jugador";

    static bool enganchado;

    static PruebaNivel3()
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
            SessionState.SetBool(P + "empezar", false);
            SessionState.SetString(P + "pp.nombre", PlayerPrefs.HasKey(ClaveNombre) ? "=" + PlayerPrefs.GetString(ClaveNombre) : "");
            ProgresoPrueba.Preparar(motos: 0b1111);   // las 4 motos, solo durante la prueba
            PlayerPrefs.Save();
            var inicio = EditorSceneManager.playModeStartScene;
            SessionState.SetString(P + "inicio", inicio != null ? AssetDatabase.GetAssetPath(inicio) : "");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaMenu);
            Log("Entrando a Play desde MenuPrincipal.");
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
        if (Paso == 41) Muestrear();   // cada cuadro del editor mientras se mide el rendimiento
        if (Esperando) return;
        if (Paso > 1 && Paso < 90 && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Fallo($"Play se cerró en el paso {Paso}; prueba abortada.");
            Paso = 90;
        }
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
                if (!EsperarA(() => NivelPrueba.Pasar() && Escena == "SampleScene" && GM != null && GM.PuntoActual != null, 15, "el nivel 1")) return;
                Comprobar(Escena == "SampleScene" && NivelPrueba.Usada, "la Intro lleva a SeleccionNivel y Jugar al nivel 1");
                Log($"3) Nivel 1: se entregan sus {GM.PedidosMeta} pedidos");
                Siguiente(0.5f);
                break;
            case 4:
            case 8:
            case 20:
                if (GM.NivelTerminado) { Siguiente(2.5f); break; }
                if (Paso == 20)
                    SessionState.SetString(P + "orden", SessionState.GetString(P + "orden", "") +
                                           GM.PuntoActual.name.Replace("PuntoEntrega_", "#") + $" ({GM.TiempoIdeal:0.0} s) ");
                GM.Entregar(GM.PuntoActual);
                Esperar(0.4f);
                break;
            case 5:
            case 9:
            {
                string n = Paso == 5 ? "1" : "2";
                if (!EsperarA(() => Panel() != null && Panel().gameObject.activeInHierarchy, 10, $"el panel final del nivel {n}")) return;
                var sig = Panel().Find("BotonSiguiente");
                Comprobar(sig != null && sig.gameObject.activeSelf && Alcanzable(sig), $"nivel {n}: \"Siguiente nivel\" visible");
                Log($"{(Paso == 5 ? 4 : 7)}) Nivel {n}: clic en Siguiente nivel");
                Clic(RutaDe(sig));
                Siguiente(0.5f);
                break;
            }
            case 6:
            case 10:
            {
                string escena = Paso == 6 ? "Nivel2_Comercial" : "Nivel3_Lluvia";
                if (!EsperarA(() => Escena == escena && Object.FindFirstObjectByType<TiendaMotos>() != null
                                    && Object.FindFirstObjectByType<TiendaMotos>().Abierta, 15, $"{escena} con ELIGE TU MOTO")) return;
                var tienda = Object.FindFirstObjectByType<TiendaMotos>();
                var ventana = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "TiendaMotos_Fondo");
                if (SessionState.GetBool(P + "empezar", false))
                {
                    // un cuadro después de la captura: el Canvas ya volvió a Overlay y el raycast da en el botón
                    SessionState.SetBool(P + "empezar", false);
                    Clic(RutaDe(ventana.Find("Ventana/BotonEmpezar")));
                    Siguiente(0.5f);
                    break;
                }
                var titulo = ventana.Find("Ventana/Titulo")?.GetComponent<TMP_Text>();
                Log($"{(Paso == 6 ? 5 : 8)}) {escena}: ELIGE TU MOTO");
                Comprobar(Escena == escena, $"Siguiente nivel carga {escena}");
                Comprobar(tienda.ModoActual == TiendaMotos.Modo.AntesDeNivel && titulo != null && titulo.text == "ELIGE TU MOTO", $"tienda en modo AntesDeNivel (\"{titulo?.text}\")");
                Comprobar(Time.timeScale == 0f && GM.EnEspera && GM.PuntoActual == null, "todo quieto y sin pedido mientras se elige");
                int moto = Paso == 6 ? 0 : 1;            // nivel 3 con la Clásica (5.5, la velocidad de referencia)
                Progreso.Equipar(moto);
                Log($"  moto equipada: {Progreso.Motos[moto].nombre}");
                if (Paso == 10) Capturar("EligeTuMoto_nivel3.png");
                SessionState.SetBool(P + "empezar", true);
                Esperar(0.2f);
                break;
            }
            case 7:
                if (!EsperarA(() => !GM.EnEspera && GM.PuntoActual != null && Time.timeScale == 1f, 5, "que empiece el nivel 2")) return;
                Log("6) Nivel 2: se entregan sus pedidos");
                Comprobar(new SerializedObject(Object.FindFirstObjectByType<LevelManager>()).FindProperty("escenaSiguiente").stringValue == "Nivel3_Lluvia",
                          "nivel 2: escenaSiguiente = Nivel3_Lluvia");
                Siguiente(0.3f);
                break;
            case 11:
            {
                if (!EsperarA(() => !GM.EnEspera && GM.PuntoActual != null && Time.timeScale == 1f, 5, "que empiece el nivel 3")) return;
                Log("9) Nivel 3 en marcha");
                var nivel = Object.FindFirstObjectByType<LevelManager>();
                var so = new SerializedObject(nivel);
                Comprobar(nivel.NombreNivel == "Lluvia" && GM.PedidosMeta == 8 && Mathf.Approximately(so.FindProperty("velocidadReferencia").floatValue, 5.5f)
                          && nivel.MotosHabilitadas && so.FindProperty("elegirMotoAntes").boolValue && so.FindProperty("escenaSiguiente").stringValue == "Final"
                          && so.FindProperty("escenaMenu").stringValue == "MenuPrincipal" && nivel.CierreHistoria && nivel.QuejasMaximas == 3,
                          "LevelManager: Lluvia, 8 pedidos, velocidad 5.5, motos (elegir antes), Continuar a Final, menú, cierre de la historia, 3 quejas");
                var luz = Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None).First(l => l.lightType == Light2D.LightType.Global);
                Comprobar(ColorUtility.ToHtmlStringRGB(luz.color) == "6F6AB8" && Mathf.Approximately(luz.intensity, 0.55f), $"noche #{ColorUtility.ToHtmlStringRGB(luz.color)} a {luz.intensity}");
                var noche = Enumerable.Range(0, so.FindProperty("soloDeNoche").arraySize).Select(i => so.FindProperty("soloDeNoche").GetArrayElementAtIndex(i).objectReferenceValue as GameObject).ToList();
                Comprobar(noche.Count > 0 && noche.All(g => g != null && g.activeInHierarchy), $"{noche.Count} objetos de soloDeNoche encendidos");
                int luces = Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None).Count(l => l.lightType != Light2D.LightType.Global && l.isActiveAndEnabled);
                Log($"  {luces} Light2D encendidas (faroles, ventanas, faros, linterna)");
                var lluvia = so.FindProperty("lluvia").objectReferenceValue as GameObject;
                Comprobar(lluvia != null && lluvia.activeInHierarchy && nivel.Llueve, "llueve (capa en pantalla y salpicaduras)");
                Comprobar(Object.FindFirstObjectByType<PlayerController>().EnMoto, "el repartidor va en moto");
                RevisarPlan();
                Siguiente(0.3f);
                break;
            }
            case 12:
            {
                Log("10) La lluvia se apaga y se enciende con el LevelManager");
                var nivel = Object.FindFirstObjectByType<LevelManager>();
                var lluvia = new SerializedObject(nivel).FindProperty("lluvia").objectReferenceValue as GameObject;
                nivel.SetLluvia(false);
                bool apagada = !lluvia.activeSelf && !nivel.Llueve;
                nivel.SetLluvia(true);
                Comprobar(apagada && lluvia.activeSelf, "SetLluvia(false) la apaga y SetLluvia(true) la vuelve a encender");
                Log("11) Rendimiento: 6 s de juego con lluvia y luces junto a Brenn's y la avenida");
                MoverA(new Vector2(14f, 47.5f));
                SessionState.SetInt(P + "cuadros0", Time.frameCount);
                SessionState.SetFloat(P + "t0", Time.realtimeSinceStartup);
                SessionState.SetFloat(P + "peorCuadro", 0f);
                SessionState.SetInt(P + "maxSalp", 0);
                SessionState.SetFloat(P + "hasta", (float)EditorApplication.timeSinceStartup + 6f);
                Paso = 41;
                break;
            }
            case 41:
            {
                if (EditorApplication.timeSinceStartup < SessionState.GetFloat(P + "hasta", 0)) return;
                int cuadros = Time.frameCount - SessionState.GetInt(P + "cuadros0", 0);
                float segundos = Time.realtimeSinceStartup - SessionState.GetFloat(P + "t0", 0);
                float fps = cuadros / Mathf.Max(0.01f, segundos);
                Comprobar(fps >= 55f, $"rendimiento: {fps:0} FPS de media en {segundos:0.0} s ({cuadros} cuadros), peor cuadro {SessionState.GetFloat(P + "peorCuadro", 0) * 1000f:0} ms (en el editor)");
                int maxSalp = SessionState.GetInt(P + "maxSalp", 0);
                Comprobar(maxSalp <= 20 && maxSalp > 0, $"salpicaduras a la vez: {maxSalp} como máximo (límite 20)");
                Capturar("Noche_lluvia.png");
                Paso = 13;
                break;
            }
            case 13:
            {
                // el cruce (un semáforo) con más faroles a la vista
                var faroles = SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == "LucesFaroles").GetComponentsInChildren<Light2D>();
                var cruces = Object.FindObjectsByType<Semaforo>(FindObjectsSortMode.None).Select(x => (Vector2)x.transform.position).ToList();
                int Cerca(Vector2 c) => faroles.Count(f => Mathf.Abs(f.transform.position.x - c.x) < 8f && Mathf.Abs(f.transform.position.y - c.y) < 4.5f);
                // el centro: un farol con un semáforo a menos de 10 tiles y el mayor número de faroles en la vista
                var mejor = faroles.Select(f => (Vector2)f.transform.position).Where(f => cruces.Any(c => Vector2.Distance(c, f) < 10f))
                    .OrderByDescending(Cerca).First();
                Log($"12) Cruce con faroles, vista centrada en ({mejor.x:0.0}, {mejor.y:0.0}): {Cerca(mejor)} faroles a la vista");
                MoverA(mejor + new Vector2(1.5f, -2.25f));
                Siguiente(1.5f);
                break;
            }
            case 14:
                Capturar("Cruce_faroles.png");
                Paso = 15;
                break;
            case 15:
            {
                // un bache con charco y el charco decorativo más cercano
                var baches = Object.FindObjectsByType<Bache>(FindObjectsSortMode.None);
                var charcos = SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == "Charcos").GetComponentsInChildren<SpriteRenderer>();
                var par = baches.SelectMany(b => charcos.Select(c => (b, c, d: Vector2.Distance(b.transform.position, c.transform.position)))).OrderBy(x => x.d).First();
                Log($"13) {par.b.name} ({par.b.GetComponent<SpriteRenderer>().sprite.name}) y {par.c.name} ({par.c.sprite.name}) a {par.d:0.0} tiles");
                SessionState.SetString(P + "bache", par.b.name);
                // sin autos mientras se mide (un choque también penaliza) y para que no tapen el bache en la captura
                foreach (var a in Object.FindObjectsByType<TrafficCar>(FindObjectsSortMode.None)) a.gameObject.SetActive(false);
                MoverA((Vector2)par.b.transform.position + new Vector2(-4f, 2.5f));
                Siguiente(1.2f);
                break;
            }
            case 16:
            {
                Capturar("Bache_charco.png");
                var b = Object.FindObjectsByType<Bache>(FindObjectsSortMode.None).First(x => x.name == SessionState.GetString(P + "bache", ""));
                SessionState.SetFloat(P + "pen", Penalizacion());
                SessionState.SetInt(P + "sac", b.Sacudidas);
                var pc = Object.FindFirstObjectByType<PlayerController>();
                pc.GetComponent<Rigidbody2D>().position = b.transform.position;
                pc.transform.position = b.transform.position;
                Siguiente(0.12f);
                break;
            }
            case 17:
            {
                var b = Object.FindObjectsByType<Bache>(FindObjectsSortMode.None).First(x => x.name == SessionState.GetString(P + "bache", ""));
                var pc = Object.FindFirstObjectByType<PlayerController>();
                float dPen = Penalizacion() - SessionState.GetFloat(P + "pen", 0);
                float esperado = 5f * Progreso.FactorPenalizacion;
                Comprobar(b.Sacudidas - SessionState.GetInt(P + "sac", 0) == 1 && Mathf.Approximately(pc.FactorVelocidad, 0.6f)
                          && Object.FindFirstObjectByType<CameraFollow>().Temblando,
                          "el bache con charco sacude la moto (velocidad x0.6 y temblor de cámara)");
                Comprobar(Mathf.Abs(dPen - esperado) < 1e-3f, $"penalización {dPen:0.0} (5 x {Progreso.FactorPenalizacion})");
                Comprobar(b.GetComponentsInChildren<SpriteRenderer>().Count(s => s.gameObject != b.gameObject && s.sprite.name == "particula_gota") == 3, "salpicón de 3 gotas");
                Capturar("Bache_salpicon.png");
                foreach (var a in Object.FindObjectsByType<TrafficCar>(FindObjectsInactive.Include, FindObjectsSortMode.None)) a.gameObject.SetActive(true);
                Log("14) Se entregan los 8 pedidos");
                SessionState.SetString(P + "orden", "");
                Paso = 20;
                Esperar(1.2f);
                break;
            }
            case 21:
            {
                if (!EsperarA(() => Panel() != null && Panel().gameObject.activeInHierarchy, 10, "el panel final del nivel 3")) return;
                Log("  orden: " + SessionState.GetString(P + "orden", ""));
                var panel = Panel();
                Log("15) Panel final del nivel 3: neutro, con \"Continuar\" al final de la historia");
                foreach (var b in new[] { "BotonJugarDeNuevo", "BotonMenu", "BotonTienda", "BotonSiguiente" })
                {
                    var t = panel.Find(b);
                    Comprobar(t != null && t.gameObject.activeInHierarchy && Alcanzable(t), $"{b} visible y recibe el clic");
                }
                var sig = panel.Find("BotonSiguiente").GetComponentInChildren<TMP_Text>();
                Comprobar(sig.text == "Continuar", $"BotonSiguiente dice \"{sig.text}\"");
                var titulo = panel.Find("Titulo").GetComponent<TMP_Text>();
                float promedio = GM.SatisfaccionesPorPedido.Average();
                int estrellas = promedio >= 80f ? 3 : promedio >= 50f ? 2 : 1;
                Comprobar(titulo.text == "¡NIVEL COMPLETADO!" && !panel.Find("TextoAviso").gameObject.activeSelf && panel.Find("TextoCierre") == null,
                          $"{estrellas} estrellas ({promedio:0}%): \"{titulo.text}\", sin aviso ni \"¡TE QUEDASTE CON EL PUESTO!\"");
                Comprobar(Progreso.EstrellasNivel3 == estrellas && !Progreso.DespedidoNivel3, $"Progreso guarda el resultado del nivel 3 ({Progreso.EstrellasNivel3} estrellas)");
                Comprobar(FuenteProyecto.EsTitulo(titulo.font) && Visible(titulo), "titular en Press Start 2P y cabe en el panel");
                Siguiente(2f);   // que terminen de salir las estrellas
                break;
            }
            case 22:
            {
                Capturar("PanelFinal_nivel3.png");
                Siguiente(0.2f);
                break;
            }
            case 23:
                Log("16) Clic en Menú");
                Clic(RutaDe(Panel().Find("BotonMenu")));
                Siguiente(0.5f);
                break;
            case 24:
                if (!EsperarA(() => Escena == "MenuPrincipal", 10, "el menú principal")) return;
                Comprobar(Escena == "MenuPrincipal", "\"Menú\" vuelve a MenuPrincipal: flujo completo");
                Paso = 90;
                EditorApplication.isPlaying = false;
                Esperar(1f);
                break;
            case 90:
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var nom = SessionState.GetString(P + "pp.nombre", "");
                if (nom == "") PlayerPrefs.DeleteKey(ClaveNombre); else PlayerPrefs.SetString(ClaveNombre, nom.Substring(1));
                PlayerPrefs.Save();
                ProgresoPrueba.Limpiar();   // el progreso vive solo en la sesión: no hay nada que restaurar
                Log($"Restaurado: nombre \"{PlayerPrefs.GetString(ClaveNombre, "")}\"");
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

    // ---- nivel 3 ----

    static float Penalizacion() =>
        (float)typeof(GameManager).GetField("penalizacion", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(GM);

    static bool Visible(TMP_Text t)
    {
        t.ForceMeshUpdate();
        return !t.isTextOverflowing && t.textBounds.size.x <= t.rectTransform.rect.width + 1f && t.textBounds.size.y <= t.rectTransform.rect.height + 1f;
    }

    static void RevisarPlan()
    {
        var sel = Object.FindFirstObjectByType<SeleccionPedidos>();
        if (sel == null) { Fallo("no hay SeleccionPedidos"); return; }
        var plan = sel.Plan;
        Comprobar(plan.Count == 8 && plan.Distinct().Count() == 8, $"plan de {plan.Count} puntos distintos");
        var spawn = (Vector2)Object.FindFirstObjectByType<PlayerController>().transform.position;
        var dist = new List<float> { Vector2.Distance(spawn, plan[0].Centro()) };
        for (int i = 1; i < plan.Count; i++) dist.Add(Vector2.Distance(plan[i - 1].Centro(), plan[i].Centro()));
        Comprobar(dist.All(d => d >= sel.DistanciaMinima), $"cada pedido a {sel.DistanciaMinima} tiles o más del anterior: " + string.Join(", ", dist.Select(d => d.ToString("0.0"))));
        bool justo = sel.Justo("spawn", plan[0]);
        for (int i = 1; i < plan.Count; i++) justo &= sel.Justo(plan[i - 1].name, plan[i]);
        Comprobar(justo, "ningún par seguido está entre los injustos (con la moto más lenta se llega en el tiempo ideal)");
        Comprobar(GM.PuntoActual == plan[0], "el primer pedido es el primero del plan");
        Log("  plan: " + string.Join(" → ", plan.Select(p => p.name.Replace("PuntoEntrega_", "#"))));
    }

    // rendimiento: el cuadro más lento y cuántas salpicaduras hay a la vez
    static void Muestrear()
    {
        if (!EditorApplication.isPlaying) return;
        SessionState.SetFloat(P + "peorCuadro", Mathf.Max(SessionState.GetFloat(P + "peorCuadro", 0), Time.unscaledDeltaTime));
        var s = Object.FindFirstObjectByType<SalpicadurasLluvia>();
        if (s != null) SessionState.SetInt(P + "maxSalp", Mathf.Max(SessionState.GetInt(P + "maxSalp", 0), s.Activas));
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
        var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.isRootCanvas && c.transform.Find("PanelFinal") != null);
        // la capa de lluvia (otro Canvas en pantalla) también sale en la captura, debajo del HUD
        var lluvia = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).FirstOrDefault(c => c.isRootCanvas && c.GetComponent<LluviaPantalla>() != null);
        int ordenLluvia = lluvia != null ? lluvia.sortingOrder : 0;
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
            if (lluvia != null)
            {
                lluvia.renderMode = RenderMode.ScreenSpaceCamera;
                lluvia.worldCamera = cam;
                lluvia.sortingLayerID = canvas.sortingLayerID;
                lluvia.sortingOrder = short.MaxValue - 1;
                lluvia.planeDistance = cam.nearClipPlane + 1;
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
            cam.transform.position = posCamara;
            canvas.renderMode = modo;
            if (lluvia != null) { lluvia.renderMode = RenderMode.ScreenSpaceOverlay; lluvia.sortingOrder = ordenLluvia; }
            canvas.sortingLayerID = capa;
            canvas.sortingOrder = orden;
            rt.Release();
        }
        Log($"  captura 1920x1080: {ruta}");
    }
}
