using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Prueba en Play de los choques de la moto (ImpactoMoto) en el nivel 2, desde el menú: con cada moto (Scooter,
// Clásica, Chopper, Deportiva) la moto arranca a unos tiles de un obstáculo y avanza sola hacia él
// (PlayerController.DireccionForzada): un auto (por detrás, quieto), un peatón (quieto), una pared y un bache.
// Revisa la penalización (8 / 5 / 0 / 5 x el factor de la moto, una sola vez aunque siga empujando), que la
// satisfacción baje eso, la pérdida de control, el texto, el freno del auto con su globo, que el peatón se pare,
// que la moto no quede dentro del auto, y con la Scooter que un roce lento no cuente. Captura el choque con auto.
// Los demás autos y peatones se apagan durante cada medida. El avance guardado queda como estaba.
// Temp/PruebaChoques.pedido (dentro: carpeta para las capturas) → Temp/PruebaChoques.resultado
[InitializeOnLoad]
public static class PruebaChoques
{
    const string Pedido = "Temp/PruebaChoques.pedido";
    const string Resultado = "Temp/PruebaChoques.resultado";
    const string P = "PruebaChoques.";
    static readonly string[] ClavesProgreso = { "dinero_total", "motos_compradas", "moto_equipada" };
    static readonly string[] Obstaculos = { "auto", "peatón", "pared", "roce lento", "bache" };
    const float Distancia = 2.5f;   // tiles entre la moto y el obstáculo al arrancar

    static bool enganchado;

    static PruebaChoques()
    {
        if (enganchado) return;
        enganchado = true;
        EditorApplication.update += Revisar;
        Application.logMessageReceived += Anotar;
    }

    static int Paso { get => SessionState.GetInt(P + "paso", 0); set => SessionState.SetInt(P + "paso", value); }
    static int Moto { get => SessionState.GetInt(P + "moto", 0); set => SessionState.SetInt(P + "moto", value); }
    static int Sub { get => SessionState.GetInt(P + "sub", 0); set => SessionState.SetInt(P + "sub", value); }
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
    static GameManager GM => GameManager.Instance;
    static PlayerController Jugador => Object.FindFirstObjectByType<PlayerController>();
    static ImpactoMoto Impacto => Jugador != null ? Jugador.GetComponent<ImpactoMoto>() : null;

    const BindingFlags Privado = BindingFlags.Instance | BindingFlags.NonPublic;
    static float Penalizacion() => (float)typeof(GameManager).GetField("penalizacion", Privado).GetValue(GM);
    static void Campo(object o, string nombre, object valor) => o.GetType().GetField(nombre, Privado).SetValue(o, valor);
    static T Campo<T>(object o, string nombre) => (T)o.GetType().GetField(nombre, Privado).GetValue(o);

    // el objetivo de la medida actual, por nombre de objeto (sobrevive en SessionState entre pasos)
    static GameObject Objetivo => Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .Where(t => t.GetInstanceID() == SessionState.GetInt(P + "objetivo", 0)).Select(t => t.gameObject).FirstOrDefault();

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
            SessionState.SetInt(P + "cargado", 0);
            foreach (var k in ClavesProgreso)
                SessionState.SetString(P + "pp." + k, PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k).ToString() : "");
            PlayerPrefs.SetInt("motos_compradas", 0b1111);   // las 4 motos, solo durante la prueba
            PlayerPrefs.Save();
            Log("Entrando a Play (menú) y cargando Nivel2_Comercial.");
            Moto = 0;
            Sub = 0;
            Paso = 1;
            Esperar(2f);
            EditorApplication.isPlaying = true;
            return;
        }
        if (Esperando) return;
        try { Avanzar(); }
        catch (Exception e) { Fallo($"excepción en el paso {Paso}: {e}"); Paso = 90; EditorApplication.isPlaying = false; }
    }

    static void Poner(Vector2 pivote)
    {
        var pc = Jugador;
        pc.GetComponent<Rigidbody2D>().position = pivote;
        pc.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        pc.transform.position = pivote;
        var cam = Object.FindFirstObjectByType<CameraFollow>();
        cam.transform.position = cam.Limitar(new Vector3(pivote.x, pivote.y + 1.25f, cam.transform.position.z));
    }

    // ¿El camino de la moto (caja "ancho" de alto) hasta el obstáculo está libre de otros colliders sólidos?
    static bool Libre(Vector2 desde, Vector2 hasta, float ancho, Collider2D obstaculo)
    {
        Vector2 d = hasta - desde;
        bool horizontal = Mathf.Abs(d.x) > Mathf.Abs(d.y);
        var tam = horizontal ? new Vector2(Mathf.Abs(d.x) + 1.8f, ancho) : new Vector2(ancho, Mathf.Abs(d.y) + 0.8f);
        foreach (var c in Physics2D.OverlapBoxAll((desde + hasta) / 2f, tam, 0f))
        {
            if (c == obstaculo || c.isTrigger || !c.enabled) continue;
            if (c.GetComponentInParent<PlayerController>() != null) continue;
            return false;
        }
        return CameraFollow.LimitesDelMapa(out var mapa) && mapa.Contains(desde);
    }

    // Busca de qué lado llegar al obstáculo: devuelve el pivote de salida y la dirección (unitaria, en un eje)
    static bool Acercamiento(Collider2D obstaculo, Vector2[] direcciones, out Vector2 pivote, out Vector2 dir)
    {
        var b = obstaculo.bounds;
        foreach (var d in direcciones)
        {
            bool horizontal = Mathf.Abs(d.x) > 0.5f;
            float mitadMoto = horizontal ? 0.85f : 0.25f;
            float mitadObs = horizontal ? b.extents.x : b.extents.y;
            Vector2 borde = (Vector2)b.center - d * mitadObs;
            Vector2 centroMoto = (Vector2)b.center - d * (mitadObs + Distancia + mitadMoto);
            if (!Libre(centroMoto, borde - d * 0.05f, horizontal ? 0.6f : 0.9f, obstaculo)) continue;
            pivote = centroMoto - new Vector2(0f, 0.15f);   // la caja de la moto va 0.15 arriba del pivote
            dir = d;
            return true;
        }
        pivote = dir = Vector2.zero;
        return false;
    }

    static readonly Vector2[] Cuatro = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };

    static void Apagar<T>(GameObject salvo) where T : Component
    {
        foreach (var c in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            c.gameObject.SetActive(c.gameObject == salvo);
    }

    static void Avanzar()
    {
        if (Paso < 90 && !EditorApplication.isPlaying)
        {
            if (SessionState.GetFloat(P + "fuera", 0) == 0) SessionState.SetFloat(P + "fuera", (float)EditorApplication.timeSinceStartup);
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(P + "fuera", 0) > 20f)
            { Fallo($"Play se cerró en el paso {Paso}; prueba abortada."); Paso = 90; }
            else { Esperar(0.5f); return; }
        }
        else SessionState.SetFloat(P + "fuera", 0);

        switch (Paso)
        {
            case 1:
            {
                Application.runInBackground = true;
                if (SessionState.GetInt(P + "cargado", 0) == 0)
                {
                    if (Time.frameCount <= 3) { Esperar(0.5f); return; }
                    SessionState.SetInt(P + "cargado", 1);
                    SceneManager.LoadScene("Nivel2_Comercial");
                    Esperar(1.5f);
                    return;
                }
                if (SceneManager.GetActiveScene().name != "Nivel2_Comercial") { Esperar(0.5f); return; }
                var tienda = Object.FindFirstObjectByType<TiendaMotos>();
                if (GM == null || tienda == null || Time.frameCount < 5) { Esperar(0.3f); return; }
                if (tienda.Abierta)
                {
                    var ventana = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "TiendaMotos_Fondo");
                    ventana.Find("Ventana/BotonEmpezar").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                }
                Comprobar(Impacto != null, "el repartidor tiene ImpactoMoto");
                if (Impacto == null) { Paso = 90; EditorApplication.isPlaying = false; return; }
                SessionState.SetString(P + "spawn", JsonUtility.ToJson(Jugador.transform.position));
                Paso = 2;
                Esperar(0.5f);
                break;
            }

            case 2:   // preparar una medida: moto, objetivo y salida
            {
                Progreso.Equipar(Moto);
                var jugador = Jugador;
                jugador.DireccionForzada = null;
                Campo(GM, "penalizacion", 0f);      // cada medida con la barra llena (que no tope en 0)
                Campo(GM, "tiempoPedido", 0f);
                string nombre = Progreso.Motos[Moto].nombre;
                if (Sub == 0) Log($"\n== {nombre} (choques x{Progreso.Motos[Moto].factorPenalizacion}) ==");
                Log($"{nombre} contra {Obstaculos[Sub]}");

                Vector2 spawn = JsonUtility.FromJson<Vector3>(SessionState.GetString(P + "spawn", "{}"));
                Collider2D objetivo = null;
                Vector2 pivote = Vector2.zero, dir = Vector2.zero;
                bool hallado = false;
                if (Sub == 0)
                {
                    Apagar<Peaton>(null);
                    foreach (var a in Object.FindObjectsByType<TrafficCar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        a.gameObject.SetActive(true);
                        a.enabled = true;
                    }
                    foreach (var a in Object.FindObjectsByType<TrafficCar>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                 .Where(a => a.gameObject.activeInHierarchy && !a.Esperando && !a.EnCurva)
                                 .OrderBy(a => Vector2.Distance(a.Centro, spawn)))
                    {
                        Vector2 d = a.Dir;
                        if (Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)) < 0.99f) continue;
                        d = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? new Vector2(Mathf.Sign(d.x), 0f) : new Vector2(0f, Mathf.Sign(d.y));
                        objetivo = a.GetComponent<Collider2D>();
                        if (Acercamiento(objetivo, new[] { d }, out pivote, out dir)) { hallado = true; break; }   // por detrás
                    }
                    if (hallado)
                    {
                        objetivo.GetComponent<TrafficCar>().enabled = false;   // quieto (los mensajes de choque le siguen llegando)
                        Apagar<TrafficCar>(objetivo.gameObject);
                    }
                }
                else if (Sub == 1)
                {
                    Apagar<TrafficCar>(null);
                    foreach (var p in Object.FindObjectsByType<Peaton>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                 .OrderBy(p => Vector2.Distance(p.transform.position, spawn)))
                    {
                        objetivo = p.GetComponent<Collider2D>();
                        p.gameObject.SetActive(true);
                        if (objetivo != null && Acercamiento(objetivo, Cuatro, out pivote, out dir)) { hallado = true; break; }
                    }
                    if (hallado)
                    {
                        objetivo.GetComponent<Peaton>().enabled = false;
                        Campo(objetivo.GetComponent<Peaton>(), "pausa", 0f);
                        Apagar<Peaton>(objetivo.gameObject);
                    }
                }
                else if (Sub == 2 || Sub == 3)
                {
                    Apagar<TrafficCar>(null);
                    Apagar<Peaton>(null);
                    int saltar = Sub == 3 ? 1 : 0;   // el roce lento contra otra pared
                    foreach (var c in Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None)
                                 .Where(c => c.enabled && !c.isTrigger && c.attachedRigidbody == null && c.name != "BordeMapa"
                                             && c.bounds.size.x < 8f && c.bounds.size.y < 8f)
                                 .OrderBy(c => Vector2.Distance(c.bounds.center, spawn)))
                    {
                        if (!Acercamiento(c, Cuatro, out pivote, out dir)) continue;
                        if (saltar-- > 0) continue;
                        objetivo = c;
                        hallado = true;
                        break;
                    }
                    if (hallado && Sub == 3) Jugador.Sacudir(0.25f, 3f);   // x0.25: 1.25-1.6 tiles/s, menos de 1.5 salvo la deportiva
                }
                else
                {
                    var b = Object.FindObjectsByType<Bache>(FindObjectsSortMode.None).OrderBy(x => x.name).ElementAt(Moto);
                    objetivo = b.GetComponent<Collider2D>();
                    pivote = (Vector2)b.transform.position + new Vector2(0f, 3f);
                    hallado = true;
                    SessionState.SetInt(P + "sac", b.Sacudidas);
                }
                if (!hallado) { Fallo($"no encontré un {Obstaculos[Sub]} con el camino libre"); Siguiente(); break; }

                SessionState.SetInt(P + "objetivo", objetivo.transform.GetInstanceID());
                SessionState.SetString(P + "dir", JsonUtility.ToJson(dir));
                SessionState.SetString(P + "ini", JsonUtility.ToJson(objetivo.transform.position));
                Poner(pivote);
                Log($"  {objetivo.name} en ({objetivo.bounds.center.x:0.0}, {objetivo.bounds.center.y:0.0}); la moto sale de ({pivote.x:0.0}, {pivote.y:0.0}) hacia ({dir.x:0}, {dir.y:0})");
                Paso = 3;
                Esperar(0.3f);
                break;
            }

            case 3:   // arrancar
            {
                var im = Impacto;
                SessionState.SetFloat(P + "pen", Penalizacion());
                SessionState.SetFloat(P + "sat", GM.SatisfaccionActual);
                SessionState.SetInt(P + "golpes", im.Golpes);
                SessionState.SetFloat(P + "inicio", (float)EditorApplication.timeSinceStartup);
                if (Sub == 4) Poner(Objetivo.transform.position);
                else Jugador.DireccionForzada = JsonUtility.FromJson<Vector2>(SessionState.GetString(P + "dir", "{}"));
                Paso = 4;
                Esperar(0.02f);
                break;
            }

            case 4:   // esperar el golpe
            {
                var im = Impacto;
                bool golpe = im.Golpes > SessionState.GetInt(P + "golpes", 0);
                float pasado = (float)EditorApplication.timeSinceStartup - SessionState.GetFloat(P + "inicio", 0);
                if (Sub == 3)
                {
                    if (pasado < 2f && !golpe) { Esperar(0.05f); return; }
                    Comprobar(!golpe, $"roce lento (x{Jugador.FactorVelocidad:0.00}): sin golpe, sin penalización ({Penalizacion() - SessionState.GetFloat(P + "pen", 0):0.0})");
                    Jugador.DireccionForzada = null;
                    Campo(Jugador, "relojSacudida", 0f);
                    Siguiente();
                    return;
                }
                if (!golpe)
                {
                    if (pasado > 3f) { Fallo($"la moto no chocó con el {Obstaculos[Sub]} en 3 s"); Jugador.DireccionForzada = null; Siguiente(); }
                    else Esperar(0.02f);
                    return;
                }
                SessionState.SetFloat(P + "golpe", (float)EditorApplication.timeSinceStartup);
                SessionState.SetInt(P + "sincontrol", Jugador.SinControl ? 1 : 0);
                Paso = 5;
                Esperar(0.15f);   // el texto ya creció
                break;
            }

            case 5:   // justo después del golpe
            {
                var im = Impacto;
                var jugador = Jugador;
                float factor = Progreso.Motos[Moto].factorPenalizacion;
                float baseP = Sub == 0 ? 8f : Sub == 1 || Sub == 4 ? 5f : 0f;
                float esperado = baseP * factor;
                float dPen = Penalizacion() - SessionState.GetFloat(P + "pen", 0);
                float dSat = SessionState.GetFloat(P + "sat", 0) - GM.SatisfaccionActual;
                var intensidad = Sub == 0 ? ImpactoMoto.Intensidad.Fuerte : Sub == 1 ? ImpactoMoto.Intensidad.Media
                               : Sub == 2 ? ImpactoMoto.Intensidad.Leve : ImpactoMoto.Intensidad.Bache;
                Comprobar(im.UltimoGolpe == intensidad, $"golpe {im.UltimoGolpe}");
                Comprobar(Mathf.Abs(dPen - esperado) < 1e-3f, $"penalización {dPen:0.##} ({baseP} x {factor})");
                if (esperado > 0f)
                    Comprobar(dSat >= esperado - 0.5f, $"la satisfacción baja {dSat:0.0} (barra de {SessionState.GetFloat(P + "sat", 0):0} a {GM.SatisfaccionActual:0})");
                var textos = Object.FindObjectsByType<TMPro.TextMeshPro>(FindObjectsSortMode.None).Where(t => t.transform.parent != null && t.transform.parent.name == "TextoChoque").ToList();
                if (Sub == 2)
                    Comprobar(textos.Count == 0, "pared: sin texto");
                else
                {
                    string titulo = Sub == 0 ? "¡CHOQUE!" : Sub == 1 ? "¡AY!" : "¡BACHE!";
                    var t1 = textos.FirstOrDefault(t => t.name == "Titulo");
                    var t2 = textos.FirstOrDefault(t => t.name == "Penalizacion");
                    Comprobar(t1 != null && t1.text == titulo && t1.GetComponent<MeshRenderer>().sortingOrder == 100 && FuenteEsPressStart(t1),
                              $"texto \"{t1?.text}\" en Press Start, Order 100");
                    Comprobar(t2 != null && t2.text == "-" + esperado.ToString("0.#"), $"debajo \"{t2?.text}\" en rojo");
                }
                if (Sub == 0 || Sub == 1)
                {
                    Comprobar(SessionState.GetInt(P + "sincontrol", 0) == 1, $"pierde el control ({(Sub == 0 ? "0.35" : "0.2")} s)");
                    Comprobar(Object.FindFirstObjectByType<CameraFollow>().Temblando, "la cámara tiembla");
                    Comprobar(im.Invulnerable, "parpadeo / sin recibir otro golpe");
                    int chispas = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Count(s => s.name == "ChispaChoque");
                    Comprobar(Sub == 0 ? chispas >= 5 : chispas == 0, $"{chispas} chispas");
                }
                if (Sub == 0)
                {
                    var auto = Objetivo.GetComponent<TrafficCar>();
                    Comprobar(Campo<float>(auto, "relojFrenado") > 0.5f, "el auto frena 1 s");
                    Comprobar(GameObject.Find("GloboPiii") != null, "globo \"¡PIII!\" sobre el auto");
                    auto.enabled = true;   // que intente avanzar: el freno lo detiene
                    SessionState.SetString(P + "ini", JsonUtility.ToJson(auto.transform.position));
                    if (Moto == 0 || Moto == 3) Capturar($"Choque_auto_{Progreso.Motos[Moto].nombre}.png");
                }
                if (Sub == 1)
                {
                    Comprobar(Campo<float>(Objetivo.GetComponent<Peaton>(), "pausa") > 0.5f, "el peatón se detiene 1 s");
                    if (Moto == 1) Capturar("Choque_peaton.png");
                }
                if (Sub == 4)
                {
                    var b = Objetivo.GetComponent<Bache>();
                    Comprobar(b.Sacudidas == SessionState.GetInt(P + "sac", 0) + 1 && Mathf.Approximately(jugador.FactorVelocidad, 0.6f),
                              "el bache sacude como antes (velocidad x0.6)");
                }
                SessionState.SetFloat(P + "pen", Penalizacion());
                Paso = 6;
                Esperar(Sub == 0 ? 0.45f : 1.05f);   // sigue empujando contra el obstáculo
                break;
            }

            case 6:
            {
                var im = Impacto;
                var jugador = Jugador;
                if (Sub == 0)
                {
                    var auto = Objetivo.GetComponent<TrafficCar>();
                    Vector3 ini = JsonUtility.FromJson<Vector3>(SessionState.GetString(P + "ini", "{}"));
                    Comprobar(Vector2.Distance(auto.transform.position, ini) < 0.01f, "el auto sigue frenado a los 0.6 s");
                    auto.enabled = false;
                    Paso = 7;
                    Esperar(0.8f);   // el golpe fuerte dura 0.35 s + 1 s de parpadeo
                    break;
                }
                Terminar(im, jugador);
                break;
            }

            case 7:
                Terminar(Impacto, Jugador);
                break;

            case 90:
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                foreach (var k in ClavesProgreso)
                {
                    var v = SessionState.GetString(P + "pp." + k, null);
                    if (v == null) continue;
                    if (v == "") PlayerPrefs.DeleteKey(k); else PlayerPrefs.SetInt(k, int.Parse(v));
                }
                PlayerPrefs.Save();
                Log($"\nAvance restaurado: dinero {PlayerPrefs.GetInt("dinero_total", 0)}, motos {PlayerPrefs.GetInt("motos_compradas", 0)}, equipada {PlayerPrefs.GetInt("moto_equipada", -1)}");
                int fallos = SessionState.GetInt(P + "fallos", 0);
                string consola = SessionState.GetString(P + "consola", "");
                File.WriteAllText(Resultado, (fallos == 0 ? "OK" : $"{fallos} FALLOS") + "\n" + SessionState.GetString(P + "log", "") +
                                  "\nCONSOLA (advertencias y errores en Play): " + (consola.Length == 0 ? "ninguno" : "\n" + consola));
                Paso = 0;
                break;
        }
    }

    // ~1.2 s después del golpe, empujando todo el tiempo contra el obstáculo
    static void Terminar(ImpactoMoto im, PlayerController jugador)
    {
        int golpes = im.Golpes - SessionState.GetInt(P + "golpes", 0);
        float extra = Penalizacion() - SessionState.GetFloat(P + "pen", 0);
        Comprobar(golpes == 1 && Mathf.Abs(extra) < 1e-3f, $"un solo golpe ({golpes}) y nada más de penalización (+{extra:0.##}) mientras sigue empujando");
        if (Sub == 0)
        {
            var mio = jugador.GetComponent<Collider2D>();
            var suyo = Objetivo.GetComponent<Collider2D>();
            var d = Physics2D.Distance(mio, suyo);
            Comprobar(!d.isOverlapped || d.distance > -0.05f, $"la moto no queda dentro del auto (separación {d.distance:0.00})");
        }
        Comprobar(!jugador.SinControl && jugador.GetComponent<SpriteRenderer>().enabled, "recupera el control y el sprite normal");
        jugador.DireccionForzada = null;
        Siguiente();
    }

    static bool FuenteEsPressStart(TMPro.TextMeshPro t) => t.font != null && t.font.name.StartsWith("BrennStart");

    static void Siguiente()
    {
        Sub++;
        if (Sub == 3 && Moto != 0) Sub = 4;   // el roce lento solo con la Scooter
        if (Sub >= Obstaculos.Length) { Sub = 0; Moto++; }
        if (Moto >= Progreso.Motos.Length) { Paso = 90; EditorApplication.isPlaying = false; Esperar(1f); return; }
        Paso = 2;
        Esperar(1.3f);   // que pase el parpadeo del golpe anterior
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
