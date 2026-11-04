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

// Prueba en Play de la flecha guía (FlechaGuia: chevron dorado opaco que orbita; ya no hay chevron en la orilla), empezando en
// MenuPrincipal, en el nivel 1 (a pie, de día), el nivel 2 (en moto, al atardecer) y el nivel 3 (en moto, de noche con
// lluvia). Con el pedido activo pone al repartidor a 10, 7, 5, 4.5 y 3.5 tiles del destino (en la línea hacia donde
// empezó, con la física del repartidor en pausa) y mide: alfa 1 siempre, pulso de escala 1.0–1.1 lejos, se encoge
// entre 6 y 4, nada a menos de 4; radio de 1.2 al pecho, que apunte al centro del pedido, y que con el destino fuera
// de la cámara no aparezca ningún chevron en la orilla (solo el que orbita). También el orden de dibujo (debajo de la
// burbuja, el globo de la clienta y el texto de choque), que vaya Unlit y el sprite de 24 px. Luego busca un lugar
// donde la flecha quede sobre banqueta, sobre pasto y sobre calle, y captura cada uno de cerca.
// La cámara se fija en 16:9 durante la prueba (como las capturas). Capturas 1920x1080 (sin HUD) a 10, 5 y 3.5 tiles, una
// con el destino fuera de pantalla, un acercamiento x3 del repartidor y Flecha_<nivel>_sobre_<suelo> (x2.5).
// El avance guardado queda como estaba. Temp/PruebaFlecha.pedido (dentro: carpeta) → Temp/PruebaFlecha.resultado
[InitializeOnLoad]
public static class PruebaFlecha
{
    const string Pedido = "Temp/PruebaFlecha.pedido";
    const string Resultado = "Temp/PruebaFlecha.resultado";
    const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";
    const string RutaUnlit = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    const string P = "PruebaFlecha.";
    static readonly string[] Niveles = { "SampleScene", "Nivel2_Comercial", "Nivel3_Lluvia" };
    static readonly string[] Suelos = { "banqueta", "pasto", "calle" };
    static readonly float[] Distancias = { 10f, 7f, 5f, 4.5f, 3.5f };

    static bool enganchado;

    static PruebaFlecha()
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
    static FlechaGuia Flecha => Object.FindFirstObjectByType<FlechaGuia>();
    static string N => $"[{Niveles[Nivel]}]";
    static string Carpeta => SessionState.GetString(P + "carpeta", "Temp");

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
            var inicio = EditorSceneManager.playModeStartScene;
            SessionState.SetString(P + "inicio", inicio != null ? AssetDatabase.GetAssetPath(inicio) : "");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaMenu);
            Log("Entrando a Play desde MenuPrincipal.");
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
            case 1:
                Application.runInBackground = true;
                if (!EsperarA(() => Escena == "MenuPrincipal" && Time.timeSinceLevelLoad > 0.5f, 15, "el menú")) return;
                Log($"{N} desde el menú");
                SceneManager.LoadScene(Niveles[Nivel]);
                Paso = 2;
                Esperar(0.5f);
                break;
            case 2:
            {
                if (!EsperarA(() => Escena == Niveles[Nivel] && GM != null && (GM.EnEspera || Time.timeSinceLevelLoad > 0.5f), 20, Niveles[Nivel])) return;
                var tienda = Object.FindFirstObjectByType<TiendaMotos>();
                if (GM.EnEspera && tienda != null && tienda.Abierta)   // ELIGE TU MOTO
                {
                    Progreso.Equipar(1);
                    var ventana = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "TiendaMotos_Fondo");
                    ExecuteEvents.Execute(ventana.Find("Ventana/BotonEmpezar").gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
                    Esperar(0.5f);
                    return;
                }
                if (!EsperarA(() => !GM.EnEspera && GM.PuntoActual != null && Time.timeScale == 1f && Time.timeSinceLevelLoad > 1f, 15, "que empiece el nivel")) return;
                foreach (var raiz in new[] { "AutosCalle", "Peatones" })
                    SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == raiz)?.SetActive(false);
                var pc = Jugador;
                pc.SetMotosHabilitadas(Nivel > 0);
                Camera.main.aspect = 16f / 9f;
                var p0 = (Vector2)pc.transform.position;
                SessionState.SetString(P + "inicioJugador", $"{p0.x}|{p0.y}");
                Estatico();
                Paso = 3;
                Esperar(0.4f);
                break;
            }
            case 3:
                Estatico();
                Log($"{N} {(Jugador.EnMoto ? "en moto" : "a pie")}; pedido activo: {GM.PuntoActual.name} en {(Vector2)GM.PuntoActual.Centro()}");
                Comprobar(Jugador.EnMoto == (Nivel > 0), Nivel > 0 ? "va en moto" : "va a pie");
                Orden();
                Indice = 0;
                Paso = 4;
                break;

            case 4:   // poner al repartidor a la distancia del turno
                Poner(Lugar(Distancias[Indice]));
                SessionState.SetFloat(P + "amin", 9f);
                SessionState.SetFloat(P + "amax", -1f);
                SessionState.SetFloat(P + "emin", 9f);
                SessionState.SetFloat(P + "emax", -1f);
                SessionState.SetFloat(P + "t0", (float)EditorApplication.timeSinceStartup);
                Paso = 5;
                Esperar(0.25f);
                break;
            case 5:   // muestrear el alfa 1.3 s (más de un pulso) y medir
            {
                Estatico();
                var f = Flecha;
                float a = f.AlfaOrbita;
                SessionState.SetFloat(P + "amin", Mathf.Min(SessionState.GetFloat(P + "amin", 9f), a));
                SessionState.SetFloat(P + "amax", Mathf.Max(SessionState.GetFloat(P + "amax", -1f), a));
                float e = f.EscalaRelativa;
                SessionState.SetFloat(P + "emin", Mathf.Min(SessionState.GetFloat(P + "emin", 9f), e));
                SessionState.SetFloat(P + "emax", Mathf.Max(SessionState.GetFloat(P + "emax", -1f), e));
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(P + "t0", 0) < 1.55f) { Esperar(0.03f); return; }
                Medir(Distancias[Indice], SessionState.GetFloat(P + "amin", 0), SessionState.GetFloat(P + "amax", 0));
                float d = Distancias[Indice];
                if (d == 10f || d == 5f || d == 3.5f) Capturar($"Flecha_{Niveles[Nivel]}_{d:0.#}tiles.png", 1f);
                if (d == 10f) Capturar($"Flecha_{Niveles[Nivel]}_cerca_x3.png", 3f);
                Indice++;
                Paso = Indice < Distancias.Length ? 4 : 6;
                break;
            }
            case 6:   // destino fuera de la cámara: a 13 tiles en horizontal (la mitad del ancho de la vista es 8.9)
            {
                var destino = (Vector2)GM.PuntoActual.Centro();
                var mapa = Object.FindFirstObjectByType<CameraFollow>().Mapa;
                Poner(destino + Vector2.right * (destino.x < mapa.center.x ? 13f : -13f));
                Paso = 7;
                Esperar(0.4f);
                break;
            }
            case 7:
            {
                Estatico();
                var f = Flecha;
                var cam = Camera.main;
                var destino = (Vector2)GM.PuntoActual.Centro();
                Vector2 c = cam.transform.position, medio = new Vector2(cam.orthographicSize * cam.aspect, cam.orthographicSize);
                bool fuera = Mathf.Abs(destino.x - c.x) > medio.x || Mathf.Abs(destino.y - c.y) > medio.y;
                Comprobar(fuera, $"{N} el destino queda fuera de la cámara");
                SinOrilla($"{N} destino fuera de pantalla:");
                Comprobar(f.Visible, $"{N} destino fuera de pantalla: la que orbita al repartidor sigue visible");
                Capturar($"Flecha_{Niveles[Nivel]}_fuera_de_pantalla.png", 1f);
                SessionState.SetInt(P + "suelo", 0);
                Paso = 20;
                break;
            }

            case 20:   // la flecha sobre banqueta, pasto y calle
            {
                string suelo = Suelos[SessionState.GetInt(P + "suelo", 0)];
                if (SobreSuelo(suelo, out var lugar)) { Poner(lugar); Paso = 21; Esperar(0.4f); }
                else { Fallo($"{N} no encontré dónde poner la flecha sobre {suelo}"); Paso = 22; }
                break;
            }
            case 21:
            {
                Estatico();
                string suelo = Suelos[SessionState.GetInt(P + "suelo", 0)];
                var f = Flecha;
                var orbita = Hijo("flecha_guia");
                string tile = TileEn(orbita.transform.position);
                Comprobar(f.Visible && f.AlfaOrbita == 1f && Categoria(tile) == suelo,
                          $"{N} sobre {suelo} ({tile}): se ve, alfa {f.AlfaOrbita:0.00}, escala {f.EscalaRelativa:0.00}");
                Capturar($"Flecha_{Niveles[Nivel]}_sobre_{suelo}.png", 2.5f);
                Paso = 22;
                break;
            }
            case 22:
            {
                int k = SessionState.GetInt(P + "suelo", 0) + 1;
                SessionState.SetInt(P + "suelo", k);
                Paso = k < Suelos.Length ? 20 : 8;
                break;
            }
            case 8:
                Nivel++;
                cam().ResetAspect();
                if (Nivel < Niveles.Length)
                {
                    LiberarJugador();
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
                ProgresoPrueba.Limpiar();   // el progreso vive solo en la sesión: no hay nada que restaurar
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

    static Camera cam() => Camera.main;

    static SpriteRenderer Hijo(string nombre) => Flecha.transform.Find(nombre).GetComponent<SpriteRenderer>();

    // orden de dibujo y material
    static void Orden()
    {
        var orbita = Hijo("flecha_guia");
        Comprobar(Flecha.transform.childCount == 1 && Flecha.transform.Find("flecha_borde") == null, $"{N} FlechaPedido solo tiene flecha_guia (sin flecha_borde)");
        Comprobar(orbita.sprite != null && orbita.sprite.texture.name == "flecha_chevron" && orbita.sprite.rect.width == 24,
                  $"{N} usa el chevron dorado ({orbita.sprite?.texture.name}, {orbita.sprite?.rect.width}x{orbita.sprite?.rect.height} px)");
        var png = new Texture2D(2, 2);
        png.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(orbita.sprite)));   // la textura importada no es legible
        var px = png.GetPixels32();
        Object.DestroyImmediate(png);
        Comprobar(px.Any(c => c.r == 0xFF && c.g == 0xD1 && c.b == 0x66 && c.a == 255) && px.Any(c => c.r == 0x2A && c.g == 0x1F && c.b == 0x40 && c.a == 255)
                  && px.Any(c => c.r == 255 && c.g == 255 && c.b == 255 && c.a == 255) && px.All(c => c.a == 0 || c.a == 255),
                  $"{N} sprite: relleno #FFD166, borde #2A1F40 y borde blanco, sin semitransparencias");
        Comprobar(orbita.color == Color.white, $"{N} sin tinte ni alfa en el SpriteRenderer ({orbita.color})");
        var encima = new List<(string, int)>();
        foreach (var dp in Object.FindObjectsByType<DeliveryPoint>(FindObjectsSortMode.None).Take(1))
            foreach (var r in dp.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.name == "Burbuja" || r.name == "Producto")) encima.Add(("burbuja/" + r.name, r.sortingOrder));
        var globo = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(m => m.name.StartsWith("Globo"));
        if (globo != null) encima.Add(("globo de la clienta", globo.sortingOrder));
        encima.Add(("texto de choque", 100));
        var entrega = Object.FindFirstObjectByType<EntregaProducto>(FindObjectsInactive.Include)?.GetComponentInChildren<SpriteRenderer>(true);
        if (entrega != null) encima.Add(("entrega del producto", entrega.sortingOrder));
        Comprobar(encima.All(e => e.Item2 > orbita.sortingOrder),
                  $"{N} la flecha (Order in Layer {orbita.sortingOrder}) va debajo de: {string.Join(", ", encima.Select(e => $"{e.Item1} {e.Item2}"))}");
        var unlit = AssetDatabase.LoadAssetAtPath<Material>(RutaUnlit);
        var luz = Object.FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>(FindObjectsSortMode.None).FirstOrDefault(l => l.lightType == UnityEngine.Rendering.Universal.Light2D.LightType.Global);
        Comprobar(orbita.sharedMaterial == unlit,
                  $"{N} va Unlit ({orbita.sharedMaterial?.name}): la luz global {(luz != null ? $"#{ColorUtility.ToHtmlStringRGB(luz.color)} x{luz.intensity:0.00}" : "-")} no la cambia");
    }

    static void Medir(float d, float amin, float amax)
    {
        float emin = SessionState.GetFloat(P + "emin", 0), emax = SessionState.GetFloat(P + "emax", 0);
        var f = Flecha;
        var orbita = Hijo("flecha_guia");
        var pc = Jugador;
        var destino = (Vector2)GM.PuntoActual.Centro();
        float real = Vector2.Distance(pc.transform.position, destino);
        string t = $"{N} a {real:0.0} tiles:";
        if (d >= 6.5f)
        {
            Comprobar(f.Visible && amin == 1f && amax == 1f, $"{t} se ve, opaca (alfa {amin:0.00}–{amax:0.00})");
            Comprobar(emin >= 0.995f && emin <= 1.02f && emax >= 1.08f && emax <= 1.101f, $"{t} pulso de escala {emin:0.00}–{emax:0.00} (1.0–1.1)");
            Vector2 pecho = (Vector2)pc.transform.position + Vector2.up * 0.9f;
            Vector2 op = orbita.transform.position;
            Comprobar(Mathf.Abs(Vector2.Distance(op, pecho) - 1.2f) < 0.02f, $"{t} orbita a {Vector2.Distance(op, pecho):0.00} del pecho (1.2)");
            float err = Vector2.Angle(orbita.transform.right, destino - pecho);
            Comprobar(err < 1.5f, $"{t} apunta al centro del pedido (error {err:0.0}°)");
        }
        else if (d > 4f)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, 6f, real));
            Comprobar(f.Visible && amin == 1f && emax < 1.1f * k + 0.02f && emax > k - 0.02f && emax < 1f,
                      $"{t} se encoge sin transparentarse: escala máx {emax:0.00} (esperado {k:0.00}–{1.1f * k:0.00}), alfa {amin:0.00}");
        }
        else
            Comprobar(!f.Visible && amax <= 0f, $"{t} no se ve (manda la burbuja)");
        SinOrilla(t);
    }

    // ningún chevron (SpriteRenderer con flecha_chevron) pegado a la orilla de la cámara: solo queda el que orbita
    static void SinOrilla(string t)
    {
        var c = (Vector2)cam().transform.position;
        Vector2 medio = new Vector2(cam().orthographicSize * cam().aspect, cam().orthographicSize);
        var enOrilla = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
            .Where(r => r.enabled && r.sprite != null && r.sprite.texture != null && r.sprite.texture.name == "flecha_chevron")
            .Where(r => r != Hijo("flecha_guia"))
            .Count(r =>
            {
                Vector2 p = r.transform.position;
                return Mathf.Min(medio.x - Mathf.Abs(p.x - c.x), medio.y - Mathf.Abs(p.y - c.y)) < 1f;
            });
        Comprobar(enOrilla == 0, $"{t} sin chevron en la orilla de la pantalla ({enOrilla})");
    }

    // ---- suelo bajo la flecha
    static string TileEn(Vector2 p)
    {
        var tm = Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Suelo");
        return tm != null ? tm.GetTile(tm.WorldToCell(p))?.name ?? "" : "";
    }

    static string Categoria(string tile)
    {
        if (tile.StartsWith("banqueta") || tile.StartsWith("plaza") || tile.StartsWith("adoquin")) return "banqueta";
        if (tile.StartsWith("pasto") || tile.StartsWith("flores") || tile.StartsWith("camellon")) return "pasto";
        if (tile.StartsWith("asfalto") || tile.StartsWith("carril") || tile.StartsWith("cajon") || tile.StartsWith("estacionamiento")) return "calle";
        return "";
    }

    // un lugar a 7–30 tiles del destino, libre de colliders, donde la flecha (pecho + 1.2 hacia el destino) y los
    // tiles alrededor de ella son del suelo pedido
    static bool SobreSuelo(string suelo, out Vector2 lugar)
    {
        var destino = (Vector2)GM.PuntoActual.Centro();
        var mapa = Object.FindFirstObjectByType<CameraFollow>().Mapa;
        for (float r = 7f; r <= 30f; r += 0.5f)
            for (int a = 0; a < 96; a++)
            {
                float ang = a * Mathf.PI * 2f / 96f;
                var pie = destino + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                if (!mapa.Contains(pie)) continue;
                var pecho = pie + Vector2.up * 0.9f;
                var flecha = pecho + (destino - pecho).normalized * 1.2f;
                if (new[] { Vector2.zero, Vector2.right * 0.3f, Vector2.left * 0.3f, Vector2.up * 0.3f, Vector2.down * 0.3f }
                        .Any(o => Categoria(TileEn(flecha + o)) != suelo)) continue;
                bool choca = Physics2D.OverlapCircleAll(pie, 0.4f).Any(c => !c.isTrigger && c.GetComponentInParent<PlayerController>() == null);
                if (choca) continue;
                lugar = pie;
                return true;
            }
        lugar = default;
        return false;
    }

    // sobre la misma banqueta del destino, a d tiles en horizontal (hacia el centro del mapa, para no salirse)
    static Vector2 Lugar(float d)
    {
        var destino = (Vector2)GM.PuntoActual.Centro();
        var mapa = Object.FindFirstObjectByType<CameraFollow>().Mapa;
        return destino + Vector2.right * (destino.x < mapa.center.x ? d : -d);
    }

    // la física del repartidor en pausa: que no lo empuje nada mientras se mide
    static void Estatico()
    {
        var pc = Jugador;
        var rb = pc.GetComponent<Rigidbody2D>();
        rb.simulated = false;
        pc.DireccionForzada = Vector2.zero;
    }

    static void LiberarJugador()
    {
        var pc = Jugador;
        if (pc == null) return;
        pc.GetComponent<Rigidbody2D>().simulated = true;
        pc.DireccionForzada = null;
    }

    static void Poner(Vector2 pivote)
    {
        var pc = Jugador;
        Estatico();
        pc.transform.position = pivote;
        var cf = Object.FindFirstObjectByType<CameraFollow>();
        cf.transform.position = cf.Limitar(new Vector3(pivote.x, pivote.y + 1.25f, cf.transform.position.z));
    }

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

    // lo que ve la cámara del juego (16:9, sin HUD); con zoom > 1, un acercamiento centrado en el repartidor
    static void Capturar(string archivo, float zoom)
    {
        var ruta = Path.Combine(Carpeta, archivo);
        var c = cam();
        var rt = new RenderTexture(1920, 1080, 24);
        var pos = c.transform.position;
        float tam = c.orthographicSize;
        try
        {
            if (zoom > 1f)
            {
                var pc = Jugador.transform.position;
                c.transform.position = new Vector3(pc.x, pc.y + 1.1f, pos.z);
                c.orthographicSize = tam / zoom;
            }
            c.targetTexture = rt;
            c.Render();
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
            c.targetTexture = null;
            c.orthographicSize = tam;
            c.transform.position = pos;
            rt.Release();
        }
        Log($"  captura: {ruta}");
    }
}
