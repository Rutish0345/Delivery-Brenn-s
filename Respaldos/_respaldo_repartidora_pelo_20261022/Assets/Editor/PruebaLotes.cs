using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Prueba en Play de los lotes cortos (22/10/2026) en el nivel 1 y el nivel 3, empezando en MenuPrincipal:
// 1) A pie, con la física de verdad, una rejilla de 0.2 tiles sobre TODO el mapa: en cada celda se prueba la caja de los pies
//    del repartidor contra los colliders sólidos (sin BloqueoMoto, que a pie está apagado). En el pasto y las banquetas
//    fuera de los patios, lo que estorba tiene que verse (un prop con sprite, o una caja de LotesSolidos debajo de un seto
//    o una cerca dibujados): si no, es un collider fantasma (fallo). Todo el pasto abierto tiene que alcanzarse desde
//    las banquetas, ningún patio privado (ni su camino de piedras) se alcanza a pie, y los 28 puntos de entrega sí.
//    Ninguna caja de LotesSolidos sale de su lote.
// 2) A pie, caminando de verdad (PlayerController.DireccionForzada) por el camino más corto de la rejilla, detrás de las
//    tres filas de casas, desde las banquetas norte, oeste y este.
// 3) Nivel 3, en moto: los 28 puntos alcanzables desde el inicio con BloqueoMoto, recorridos de banqueta y un intento
//    de meterse al pasto del parque del que tiene que salir sin quedarse atrapada.
// Capturas del mapa entero (2560x1280, sin HUD), con y sin la rejilla encima: verde = se camina, naranja = un prop que
// se ve, rojo = collider fantasma, morado = libre pero encerrado, azul = patio privado.
// Autos y peatones se apagan durante las medidas. El avance guardado queda como estaba.
// Temp/PruebaLotes.pedido (dentro: carpeta para las capturas) → Temp/PruebaLotes.resultado
[InitializeOnLoad]
public static class PruebaLotes
{
    const string Pedido = "Temp/PruebaLotes.pedido";
    const string Resultado = "Temp/PruebaLotes.resultado";
    const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";
    const string P = "PruebaLotes.";
    static readonly string[] ClavesProgreso = { "dinero_total", "motos_compradas", "moto_equipada" };
    static readonly string[] Niveles = { "SampleScene", "Nivel3_Lluvia" };
    static ConstruirMapa.Perfil PerfilNivel => Nivel == 0 ? ConstruirMapa.Perfil.Residencial : ConstruirMapa.Perfil.Lluvia;

    // rejilla: el mapa entero (128 x 64 tiles)
    static readonly Rect Zona = new Rect(0f, 0f, 128f, 64f);
    const float Celda = 0.2f;

    // detrás de las filas de casas (seto de atrás: copa hasta y = 61.84 en la fila A, 33.84 en la B y 11.84 en la C)
    static readonly (string nombre, Vector2 desde, Vector2 hasta)[] Caminatas =
    {
        ("A: banqueta este → oeste, detrás de las 9 casas", new Vector2(101.4f, 62.8f), new Vector2(13.2f, 62.6f)),
        ("B oeste: norte → fondo, x 4", new Vector2(4f, 40.6f), new Vector2(4f, 34.3f)),
        ("B oeste: norte → fondo, x 15", new Vector2(15f, 40.6f), new Vector2(15f, 34.3f)),
        ("B oeste: norte → fondo, x 25", new Vector2(25f, 40.6f), new Vector2(25f, 34.3f)),
        ("B oeste: banqueta este → oeste, junto a los setos", new Vector2(30.6f, 36.5f), new Vector2(1.2f, 34.3f)),
        ("B centro: norte → fondo, x 48", new Vector2(48f, 40.6f), new Vector2(48f, 34.3f)),
        ("B centro: norte → fondo, x 56.5", new Vector2(56.5f, 40.6f), new Vector2(56.5f, 34.3f)),
        ("B centro: norte → fondo, x 64.5", new Vector2(64.5f, 40.6f), new Vector2(64.5f, 34.3f)),
        ("B centro: norte → fondo, x 76.5", new Vector2(76.5f, 40.6f), new Vector2(76.5f, 34.3f)),
        ("B centro: norte → cancha, x 88.5", new Vector2(88.5f, 40.6f), new Vector2(88.5f, 35.5f)),
        ("B centro: norte → fondo, x 96", new Vector2(96f, 40.6f), new Vector2(96f, 34.3f)),
        ("B centro: oeste → este (todo el parque)", new Vector2(37.4f, 36.5f), new Vector2(97.5f, 36.8f)),
        ("B centro: este → oeste (junto a los setos)", new Vector2(99.6f, 35f), new Vector2(47.6f, 34.3f)),
        ("B este: banqueta oeste → este, junto a los setos", new Vector2(111.3f, 36.5f), new Vector2(127.3f, 34.3f)),
        ("B este: norte → fondo, x 120", new Vector2(120f, 40.6f), new Vector2(120f, 34.3f)),
        ("C centro: norte → fondo, x 45", new Vector2(45f, 14.6f), new Vector2(45f, 12.3f)),
        ("C centro: norte → fondo, x 60", new Vector2(60f, 14.6f), new Vector2(60f, 12.3f)),
        ("C centro: norte → fondo, x 75", new Vector2(75f, 14.6f), new Vector2(75f, 12.3f)),
        ("C centro: norte → fondo, x 90", new Vector2(90f, 14.6f), new Vector2(90f, 12.3f)),
        ("C centro: banqueta oeste → este, junto a los setos", new Vector2(38.6f, 12.8f), new Vector2(98.6f, 12.4f)),
        ("C este: norte → fondo, x 120", new Vector2(120f, 14.6f), new Vector2(120f, 12.3f)),
    };
    static readonly (string nombre, Vector2 desde, Vector2 hasta)[] Recorridos =
    {
        ("banqueta norte, oeste → este", new Vector2(38.5f, 41f), new Vector2(98.5f, 41f)),
        ("banqueta norte, este → oeste", new Vector2(98.5f, 41f), new Vector2(38.5f, 41f)),
        ("banqueta oeste, norte → sur", new Vector2(37.3f, 41f), new Vector2(37.3f, 31.5f)),
        ("banqueta este, norte → sur", new Vector2(99.6f, 40.5f), new Vector2(99.6f, 31.5f)),
    };

    static bool enganchado;

    static PruebaLotes()
    {
        if (enganchado) return;
        enganchado = true;
        EditorApplication.update += Revisar;
        Application.logMessageReceived += Anotar;
    }

    static int Paso { get => SessionState.GetInt(P + "paso", 0); set => SessionState.SetInt(P + "paso", value); }
    static int Nivel { get => SessionState.GetInt(P + "nivel", 0); set => SessionState.SetInt(P + "nivel", value); }
    static int Indice { get => SessionState.GetInt(P + "i", 0); set => SessionState.SetInt(P + "i", value); }
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
    static string Escena => SceneManager.GetActiveScene().name;
    static GameManager GM => GameManager.Instance;
    static PlayerController Jugador => Object.FindFirstObjectByType<PlayerController>();
    static string N => $"[{Niveles[Nivel]}]";

    static void Revisar()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (Paso == 0)
        {
            if (!File.Exists(Pedido) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var carpeta = File.ReadAllText(Pedido).Trim();
            File.Delete(Pedido);
            Directory.CreateDirectory(carpeta);
            SessionState.SetString(P + "carpeta", carpeta);
            SessionState.SetString(P + "log", "");
            SessionState.SetString(P + "consola", "");
            SessionState.SetInt(P + "fallos", 0);
            SessionState.SetFloat(P + "desde", 0);
            Nivel = 0;
            foreach (var k in ClavesProgreso)
                SessionState.SetString(P + "pp." + k, PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k).ToString() : "");
            var inicio = EditorSceneManager.playModeStartScene;
            SessionState.SetString(P + "inicio", inicio != null ? AssetDatabase.GetAssetPath(inicio) : "");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaMenu);
            Log("Entrando a Play desde MenuPrincipal.");
            Paso = 1;
            Esperar(2f);
            EditorApplication.isPlaying = true;
            return;
        }
        if (Paso >= 30 && Paso < 90 && EditorApplication.isPlaying) { Caminar(); return; }
        if (Esperando) return;
        try { Avanzar(); }
        catch (Exception e) { Fallo($"excepción en el paso {Paso}: {e}"); Paso = 90; EditorApplication.isPlaying = false; }
    }

    static void Avanzar()
    {
        if (Paso < 90 && !EditorApplication.isPlaying) { Fallo($"Play se cerró en el paso {Paso}"); Paso = 90; return; }
        switch (Paso)
        {
            case 1:
                Application.runInBackground = true;
                if (!EsperarA(() => Escena == "MenuPrincipal" && Time.timeSinceLevelLoad > 0.5f, 15, "el menú")) return;
                Paso = 10;
                break;

            case 10:
                Log($"{N} desde el menú");
                SceneManager.LoadScene(Niveles[Nivel]);
                Paso = 11;
                Esperar(0.5f);
                break;
            case 11:
            {
                if (!EsperarA(() => Escena == Niveles[Nivel] && GM != null && (GM.EnEspera || Time.timeSinceLevelLoad > 0.5f), 20, Niveles[Nivel])) return;
                var tienda = Object.FindFirstObjectByType<TiendaMotos>();
                if (GM.EnEspera && tienda != null && tienda.Abierta)   // ELIGE TU MOTO
                {
                    Progreso.Equipar(1);
                    var ventana = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "TiendaMotos_Fondo");
                    var boton = ventana.Find("Ventana/BotonEmpezar");
                    ExecuteEvents.Execute(boton.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
                    Esperar(0.5f);
                    return;
                }
                if (!EsperarA(() => !GM.EnEspera && GM.PuntoActual != null && Time.timeScale == 1f && Time.timeSinceLevelLoad > 1f, 15, "que empiece el nivel")) return;
                // a pie y sin tráfico ni peatones que se crucen en las medidas
                foreach (var raiz in new[] { "AutosCalle", "Peatones" })
                    SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == raiz)?.SetActive(false);
                Jugador.SetMotosHabilitadas(false);
                Paso = 12;
                Esperar(0.3f);
                break;
            }
            case 12:
                Analizar();
                Capturar($"Mapa_{Niveles[Nivel]}.png", false);
                Capturar($"Mapa_{Niveles[Nivel]}_rejilla.png", true);
                Log($"{N} a pie, caminando de verdad (DireccionForzada, por el camino más corto de la rejilla):");
                Indice = 0;
                SessionState.SetString(P + "modo", "pie");
                IniciarCamino(Caminatas[0].desde, Caminatas[0].hasta);
                Paso = 30;
                break;

            case 20:   // después de las caminatas a pie
                if (Nivel == 1)
                {
                    Log($"{N} en moto ({Progreso.Motos[Math.Max(0, Progreso.MotoEquipada)].nombre}), con BloqueoMoto:");
                    Jugador.SetMotosHabilitadas(true);
                    Comprobar(Jugador.EnMoto, "va en moto");
                    SessionState.SetString(P + "modo", "moto");
                    Indice = 0;
                    Paso = 21;
                    Esperar(0.3f);
                    break;
                }
                Paso = 25;
                break;
            case 21:
                CalcularRejilla(true);
                PuntosEnMoto();
                IniciarCamino(Recorridos[0].desde, Recorridos[0].hasta);
                Paso = 30;
                break;
            case 22:   // la moto se mete al pasto del parque (no puede) y luego sale
                Poner(new Vector2(60f, 40.5f));
                Jugador.DireccionForzada = Vector2.down;
                SessionState.SetFloat(P + "y0", 40.5f);
                Paso = 23;
                Esperar(1.2f);
                break;
            case 23:
            {
                var pos = (Vector2)Jugador.transform.position;
                Comprobar(pos.y > 39.5f, $"la moto no entra al pasto del parque (se detuvo en y = {pos.y:0.00})");
                IniciarCamino(pos, new Vector2(66f, 41f));
                SessionState.SetString(P + "modo", "salida");
                Paso = 30;
                break;
            }
            case 25:
                Nivel++;
                if (Nivel < Niveles.Length)
                {
                    Jugador.DireccionForzada = null;
                    SceneManager.LoadScene("MenuPrincipal");
                    Paso = 1;
                    Esperar(0.5f);
                    break;
                }
                Paso = 90;
                EditorApplication.isPlaying = false;
                Esperar(1f);
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

    // ------------------------------------------------------------------ rejilla y física

    enum Estado : byte { Libre, Visible, Fantasma }
    static Estado[,] rejilla;
    static bool[,] alcanzable;
    static List<Vector2Int> camino;
    static List<string> suelo;
    static List<Rect> patios, cocheras;

    static int Nx => Mathf.CeilToInt(Zona.width / Celda);
    static int Ny => Mathf.CeilToInt(Zona.height / Celda);
    static Vector2 Centro(int i, int j) => new Vector2(Zona.x + (i + 0.5f) * Celda, Zona.y + (j + 0.5f) * Celda);
    static Vector2Int CeldaDe(Vector2 p) => new Vector2Int(Mathf.Clamp((int)((p.x - Zona.x) / Celda), 0, Nx - 1),
                                                           Mathf.Clamp((int)((p.y - Zona.y) / Celda), 0, Ny - 1));

    static float F(object o) => Convert.ToSingle(o, System.Globalization.CultureInfo.InvariantCulture);
    static Rect Caja(object o) { var d = (Dictionary<string, object>)o; return new Rect(F(d["x"]), F(d["y"]), F(d["ancho"]), F(d["alto"])); }

    // suelo, patios y cocheras del JSON del nivel (suelo[0] es la fila de arriba)
    static void LeerMapa()
    {
        var mapa = ConstruirMapa.LeerJsonDe(PerfilNivel, PerfilNivel.archivoMapa);
        suelo = ((List<object>)mapa["suelo"]).Select(f => string.Join("|", ((List<object>)f).Select(t => t.ToString()))).ToList();
        patios = ((List<object>)mapa["patios"]).Select(p => Caja(((Dictionary<string, object>)p)["patio"])).ToList();
        cocheras = ((List<object>)mapa["patios"]).Select(p => Caja(((Dictionary<string, object>)p)["cochera"])).ToList();
    }

    static string Tile(Vector2 p)
    {
        int fila = suelo.Count - 1 - Mathf.FloorToInt(p.y), col = Mathf.FloorToInt(p.x);
        if (fila < 0 || fila >= suelo.Count) return "";
        var t = suelo[fila].Split('|');
        return col >= 0 && col < t.Length ? t[col] : "";
    }

    static bool EnPatio(Vector2 p, float margen = 0f) => patios.Any(r => Expandir(r, margen).Contains(p));
    static bool EnCochera(Vector2 p, float margen = 0f) => cocheras.Any(r => Expandir(r, margen).Contains(p));
    static Rect Expandir(Rect r, float m) => new Rect(r.x - m, r.y - m, r.width + 2 * m, r.height + 2 * m);
    static bool Pasto(string t) => t.StartsWith("pasto") || t.StartsWith("flores");
    static bool Banqueta(string t) => t.StartsWith("banqueta");
    // dentro de un patio, sin contar su cochera (de banqueta) ni la orilla del lote, donde van el seto y la cerca
    static bool Privado(Vector2 p) => EnPatio(p, -0.3f) && !EnCochera(p, 0.1f);
    // donde un sólido tiene que verse: pasto y banquetas fuera de los patios y las cocheras
    static bool Revisable(Vector2 p) { var t = Tile(p); return (Pasto(t) || Banqueta(t)) && !EnPatio(p, 0.15f) && !EnCochera(p, 0.15f); }

    // los pies en una posición (el pivote del repartidor): su BoxCollider2D tal como está (a pie o en moto)
    static (Vector2 centro, Vector2 tam) Pies(Vector2 pivote, bool moto)
    {
        var caja = Jugador.GetComponent<BoxCollider2D>();
        var esc = (Vector2)Jugador.transform.lossyScale;
        var tam = Vector2.Scale(caja.size, esc);
        if (moto) tam = new Vector2(Mathf.Max(tam.x, tam.y), Mathf.Max(tam.x, tam.y));   // de lado o de frente
        return (pivote + Vector2.Scale(caja.offset, esc), tam * 1.1f);   // con holgura: el camino no roza los troncos
    }

    static readonly Dictionary<string, int> fantasmas = new Dictionary<string, int>();

    static void CalcularRejilla(bool moto)
    {
        rejilla = new Estado[Nx, Ny];
        fantasmas.Clear();
        var dibujos = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
            .Where(r => r.enabled && r.gameObject.activeInHierarchy && r.sprite != null && (r.name.StartsWith("seto_lote") || r.name.StartsWith("cerca"))).Select(r => r.bounds).ToList();
        var cuerpo = Jugador.GetComponent<Rigidbody2D>();
        for (int i = 0; i < Nx; i++)
            for (int j = 0; j < Ny; j++)
            {
                var (c, tam) = Pies(Centro(i, j), moto);
                foreach (var col in Physics2D.OverlapBoxAll(c, tam, 0f))
                {
                    if (col.isTrigger || !col.enabled || (cuerpo != null && col.attachedRigidbody == cuerpo)) continue;
                    var e = Clasificar(col, dibujos);
                    if (e == Estado.Fantasma && (moto || !Revisable(Centro(i, j)))) e = Estado.Visible;   // fuera del pasto y las banquetas abiertas no cuenta
                    if (e > rejilla[i, j]) rejilla[i, j] = e;
                    if (e == Estado.Fantasma)
                    {
                        string nombre = Ruta(col.transform);
                        fantasmas[nombre] = fantasmas.TryGetValue(nombre, out int n) ? n + 1 : 1;
                    }
                }
            }
        // alcanzable: a pie desde cualquier banqueta; en moto desde donde está el repartidor
        alcanzable = new bool[Nx, Ny];
        var cola = new Queue<Vector2Int>();
        if (moto)
        {
            var a = CeldaDe(Jugador.transform.position);
            if (rejilla[a.x, a.y] != Estado.Libre) a = MasCercanaLibre(a);
            alcanzable[a.x, a.y] = true;
            cola.Enqueue(a);
        }
        else
            for (int i = 0; i < Nx; i++)
                for (int j = 0; j < Ny; j++)
                    if (rejilla[i, j] == Estado.Libre && Banqueta(Tile(Centro(i, j))) && !EnCochera(Centro(i, j))) { alcanzable[i, j] = true; cola.Enqueue(new Vector2Int(i, j)); }
        while (cola.Count > 0)
        {
            var a = cola.Dequeue();
            foreach (var d in Vecinos)
            {
                var b = a + d;
                if (b.x < 0 || b.y < 0 || b.x >= Nx || b.y >= Ny || alcanzable[b.x, b.y] || rejilla[b.x, b.y] != Estado.Libre) continue;
                alcanzable[b.x, b.y] = true;
                cola.Enqueue(b);
            }
        }
    }

    static readonly Vector2Int[] Vecinos = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

    // un collider que estorba se ve si es (o cuelga de) un objeto con sprite; los de LotesSolidos (sin sprite) valen
    // solo debajo de un seto o una cerca dibujados
    static Estado Clasificar(Collider2D col, List<Bounds> dibujos)
    {
        if (col.transform.name == "BordeMapa") return Estado.Visible;   // el muro de afuera del mapa: es la orilla, no estorba adentro
        for (var t = col.transform; t != null; t = t.parent)
        {
            var r = t.GetComponent<SpriteRenderer>();
            if (r != null && r.enabled && r.sprite != null) return Estado.Visible;
        }
        var b = col.bounds;
        bool tapado = dibujos.Any(d => d.min.x < b.max.x - 0.02f && b.min.x + 0.02f < d.max.x && d.min.y < b.max.y - 0.02f && b.min.y + 0.02f < d.max.y);
        return tapado ? Estado.Visible : Estado.Fantasma;
    }

    static void Analizar()
    {
        LeerMapa();
        CalcularRejilla(false);
        int abiertas = 0, encerradas = 0, visibles = 0, privadasAlcanzables = 0;
        var ejemplo = new List<string>();
        var ejemploPatio = new List<string>();
        for (int i = 0; i < Nx; i++)
            for (int j = 0; j < Ny; j++)
            {
                var p = Centro(i, j);
                if (Privado(p))
                {
                    if (alcanzable[i, j]) { privadasAlcanzables++; if (ejemploPatio.Count < 5) ejemploPatio.Add($"({p.x:0.0}, {p.y:0.0})"); }
                    continue;
                }
                if (!Revisable(p) || !Pasto(Tile(p))) continue;
                if (rejilla[i, j] == Estado.Visible) { visibles++; continue; }
                if (rejilla[i, j] != Estado.Libre) continue;
                abiertas++;
                if (!alcanzable[i, j]) { encerradas++; if (ejemplo.Count < 8) ejemplo.Add($"({p.x:0.0}, {p.y:0.0})"); }
            }
        Log($"{N} rejilla a pie de todo el mapa ({Nx}x{Ny} celdas de {Celda}; pasto abierto: {abiertas} libres, {visibles} con un prop que se ve)");
        Comprobar(fantasmas.Count == 0, fantasmas.Count == 0 ? "sin colliders fantasma en el pasto ni en las banquetas (fuera de los patios)"
                                                             : "colliders fantasma: " + string.Join(", ", fantasmas.Select(f => $"{f.Key} ({f.Value} celdas)")));
        Comprobar(encerradas == 0, $"todo el pasto abierto se alcanza a pie desde las banquetas ({encerradas} celdas encerradas{(ejemplo.Count > 0 ? ": " + string.Join(" ", ejemplo) : "")})");
        Comprobar(privadasAlcanzables == 0, $"los 28 patios privados siguen cerrados a pie ({privadasAlcanzables} celdas de patio alcanzables{(ejemploPatio.Count > 0 ? ": " + string.Join(" ", ejemploPatio) : "")})");
        var lotes = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "LotesSolidos");
        var cajas = lotes == null ? new BoxCollider2D[0] : lotes.GetComponentsInChildren<BoxCollider2D>();
        var fuera = cajas.Where(c => !patios.Any(r => Expandir(r, 0.3f).Contains(c.bounds.min) && Expandir(r, 0.3f).Contains(c.bounds.max))).Select(c => c.name).ToList();
        Comprobar(cajas.Length > 0 && fuera.Count == 0, $"las {cajas.Length} cajas de LotesSolidos quedan dentro de su lote{(fuera.Count > 0 ? "; fuera: " + string.Join(", ", fuera) : "")}");
        var filas = patios.GroupBy(r => Mathf.Round(r.y)).Select(g => $"seto de atrás en y = {string.Join("/", g.Select(r => r.yMax.ToString("0.###")).Distinct())}");
        Comprobar(patios.Select(r => Math.Round(r.height, 3)).Distinct().Count() == 1 && patios.GroupBy(r => Mathf.Round(r.y)).All(g => g.Select(r => Math.Round(r.yMax, 3)).Distinct().Count() == 1),
                  $"los 28 lotes miden lo mismo de fondo ({patios[0].height:0.###} tiles) y el seto de atrás va en línea recta por fila ({string.Join(" | ", filas)})");
        Puntos("a pie", 0.7f);
    }

    static void Puntos(string como, float radio)
    {
        var puntos = SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == "PuntosEntrega").GetComponentsInChildren<Transform>(true)
            .Where(t => t.name.StartsWith("PuntoEntrega_")).ToList();
        var malos = new List<string>();
        foreach (var t in puntos)
        {
            var c = CeldaDe(t.position);
            int r = Mathf.CeilToInt(radio / Celda);
            bool ok = false;
            for (int dx = -r; dx <= r && !ok; dx++)
                for (int dy = -r; dy <= r && !ok; dy++)
                {
                    int i = c.x + dx, j = c.y + dy;
                    ok = i >= 0 && j >= 0 && i < Nx && j < Ny && alcanzable[i, j] && Vector2.Distance(Centro(i, j), t.position) <= radio;
                }
            if (!ok) malos.Add(t.name);
        }
        Comprobar(puntos.Count == 28 && malos.Count == 0, $"los {puntos.Count} puntos de entrega se alcanzan {como}{(malos.Count > 0 ? "; no: " + string.Join(", ", malos) : "")}");
    }

    static void PuntosEnMoto() => Puntos("en moto desde donde empieza (con BloqueoMoto)", 0.9f);

    // camino más corto en la rejilla (BFS con 8 vecinos, sin cortar esquinas)
    static List<Vector2Int> Buscar(Vector2Int a, Vector2Int b)
    {
        var previo = new Dictionary<Vector2Int, Vector2Int> { [a] = a };
        var cola = new Queue<Vector2Int>();
        cola.Enqueue(a);
        while (cola.Count > 0)
        {
            var x = cola.Dequeue();
            if (x == b) break;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var y = new Vector2Int(x.x + dx, x.y + dy);
                    if (y.x < 0 || y.y < 0 || y.x >= Nx || y.y >= Ny || previo.ContainsKey(y) || rejilla[y.x, y.y] != Estado.Libre) continue;
                    if (dx != 0 && dy != 0 && (rejilla[x.x + dx, x.y] != Estado.Libre || rejilla[x.x, x.y + dy] != Estado.Libre)) continue;
                    previo[y] = x;
                    cola.Enqueue(y);
                }
        }
        if (!previo.ContainsKey(b)) return null;
        var res = new List<Vector2Int>();
        for (var c = b; c != a; c = previo[c]) res.Add(c);
        res.Reverse();
        return res;
    }

    static void IniciarCamino(Vector2 desde, Vector2 hasta)
    {
        // las caminatas (y la salida de la moto) usan la rejilla vigente; si el destino cae en una celda ocupada, la libre más cercana
        var a = CeldaDe(desde);
        var b = CeldaDe(hasta);
        if (rejilla[b.x, b.y] != Estado.Libre) b = MasCercanaLibre(b);
        if (rejilla[a.x, a.y] != Estado.Libre) a = MasCercanaLibre(a);
        camino = Buscar(a, b);
        SessionState.SetString(P + "camino", "");
        Poner(Centro(a.x, a.y));
        SessionState.SetFloat(P + "t0", (float)EditorApplication.timeSinceStartup);
        SessionState.SetFloat(P + "tquieto", (float)EditorApplication.timeSinceStartup);
        SessionState.SetString(P + "ult", $"{desde.x}|{desde.y}");
        SessionState.SetInt(P + "wp", 0);
        SessionState.SetFloat(P + "largo", camino == null ? 0f : camino.Count * Celda);
    }

    static Vector2Int MasCercanaLibre(Vector2Int c)
    {
        for (int r = 1; r < 20; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    var x = new Vector2Int(c.x + dx, c.y + dy);
                    if (x.x >= 0 && x.y >= 0 && x.x < Nx && x.y < Ny && rejilla[x.x, x.y] == Estado.Libre) return x;
                }
        return c;
    }

    // se llama en cada update del editor mientras dura un camino: lleva al repartidor punto por punto
    static void Caminar()
    {
        string modo = SessionState.GetString(P + "modo", "pie");
        var lista = modo == "pie" ? Caminatas : Recorridos;
        string nombre = modo == "salida" ? "salir del pasto hacia la banqueta" : lista[Indice].nombre;
        var pc = Jugador;
        if (camino == null || camino.Count == 0)
        {
            Fallo($"{N} {nombre}: no hay camino en la rejilla");
            Terminar(modo, lista);
            return;
        }
        int wp = SessionState.GetInt(P + "wp", 0);
        var pos = (Vector2)pc.transform.position;
        var meta = Centro(camino[camino.Count - 1].x, camino[camino.Count - 1].y);
        // el punto siguiente: el más lejano de los próximos 6 al que ya se está cerca
        while (wp < camino.Count - 1 && Vector2.Distance(pos, Centro(camino[wp].x, camino[wp].y)) < 0.25f) wp++;
        SessionState.SetInt(P + "wp", wp);
        var objetivo = Centro(camino[wp].x, camino[wp].y);
        float ahora = (float)EditorApplication.timeSinceStartup;
        float vel = pc.EnMoto ? 4f : 3.5f;
        float limite = SessionState.GetFloat(P + "largo", 0) / vel * 2.5f + 3f;
        if (Vector2.Distance(pos, meta) < 0.3f)
        {
            float t = ahora - SessionState.GetFloat(P + "t0", 0);
            Comprobar(true, $"{N} {nombre}: llega ({SessionState.GetFloat(P + "largo", 0):0.0} tiles en {t:0.0} s)");
            Terminar(modo, lista);
            return;
        }
        var ult = SessionState.GetString(P + "ult", "0|0").Split('|');
        var antes = new Vector2(float.Parse(ult[0]), float.Parse(ult[1]));
        if (Vector2.Distance(antes, pos) > 0.05f)
        {
            SessionState.SetString(P + "ult", $"{pos.x}|{pos.y}");
            SessionState.SetFloat(P + "tquieto", ahora);
        }
        if (ahora - SessionState.GetFloat(P + "tquieto", ahora) > 1.5f || ahora - SessionState.GetFloat(P + "t0", ahora) > limite)
        {
            var (c, tam) = Pies(pos, pc.EnMoto);
            var tocan = Physics2D.OverlapBoxAll(c, tam * 1.1f, 0f).Where(x => !x.isTrigger && x.enabled && x.attachedRigidbody != pc.GetComponent<Rigidbody2D>()).Select(x => Ruta(x.transform)).ToList();
            Fallo($"{N} {nombre}: se atoró en ({pos.x:0.00}, {pos.y:0.00}) yendo a ({objetivo.x:0.0}, {objetivo.y:0.0}); toca: {(tocan.Count == 0 ? "nada" : string.Join(", ", tocan))}");
            Terminar(modo, lista);
            return;
        }
        pc.DireccionForzada = (objetivo - pos).normalized;
    }

    static void Terminar(string modo, (string nombre, Vector2 desde, Vector2 hasta)[] lista)
    {
        Jugador.DireccionForzada = Vector2.zero;
        if (modo == "salida") { Paso = 25; Esperar(0.2f); return; }
        Indice++;
        if (Indice < lista.Length)
        {
            IniciarCamino(lista[Indice].desde, lista[Indice].hasta);
            return;
        }
        Paso = modo == "pie" ? 20 : 22;
        Esperar(0.2f);
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

    static string Ruta(Transform t) => t.parent == null ? t.name : Ruta(t.parent) + "/" + t.name;

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

    // Vista del parque entero (sin HUD: el Canvas va en Screen Space Overlay y la cámara no lo dibuja); con rejilla, las
    // celdas encima: verde se camina, naranja prop que se ve, rojo fantasma, morado libre pero encerrado
    static void Capturar(string archivo, bool conRejilla)
    {
        var ruta = Path.Combine(SessionState.GetString(P + "carpeta", "Temp"), archivo);
        var cam = Camera.main;
        const int W = 2560, Hh = 1280;
        var rt = new RenderTexture(W, Hh, 24);
        var pos = cam.transform.position;
        float tam = cam.orthographicSize;
        var centro = new Vector3(Zona.center.x, Zona.center.y, pos.z);
        const float alto = 32f;
        try
        {
            cam.targetTexture = rt;
            cam.orthographicSize = alto;
            cam.transform.position = centro;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, Hh, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, Hh), 0, 0);
            if (conRejilla)
            {
                float ppu = Hh / (2f * alto);
                for (int i = 0; i < Nx; i++)
                    for (int j = 0; j < Ny; j++)
                    {
                        var e = rejilla[i, j];
                        var p = Centro(i, j);
                        bool privado = Privado(p);
                        Color c = e == Estado.Fantasma ? Color.red : e == Estado.Visible ? new Color(1f, 0.55f, 0f)
                                : privado && !alcanzable[i, j] ? new Color(0.2f, 0.4f, 1f)
                                : alcanzable[i, j] ? new Color(0.2f, 0.85f, 0.3f) : new Color(0.7f, 0.1f, 0.9f);
                        int x0 = Mathf.RoundToInt(W / 2 + (p.x - Celda / 2 - centro.x) * ppu), y0 = Mathf.RoundToInt(Hh / 2 + (p.y - Celda / 2 - centro.y) * ppu);
                        int lado = Mathf.Max(1, Mathf.RoundToInt(Celda * ppu) - 1);
                        for (int x = x0; x < x0 + lado; x++)
                            for (int y = y0; y < y0 + lado; y++)
                                if (x >= 0 && y >= 0 && x < W && y < Hh)
                                    tex.SetPixel(x, y, Color.Lerp(tex.GetPixel(x, y), c, e == Estado.Libre && alcanzable[i, j] ? 0.25f : 0.6f));
                    }
            }
            tex.Apply();
            File.WriteAllBytes(ruta, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        finally
        {
            RenderTexture.active = null;
            cam.targetTexture = null;
            cam.orthographicSize = tam;
            cam.transform.position = pos;
            rt.Release();
        }
        Log($"  captura: {ruta}");
    }
}
