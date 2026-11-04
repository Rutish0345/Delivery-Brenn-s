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

// Prueba en Play de la flecha guía fina (FlechaGuia: chevron que orbita y el de la orilla), empezando en MenuPrincipal,
// en el nivel 1 (a pie) y el nivel 3 (en moto, de noche). Con el pedido activo pone al repartidor a 10, 7, 5, 4.5 y 3.5
// tiles del destino (en la línea hacia donde empezó, con la física del repartidor en pausa) y mide: alfa (pulso de 0.45
// a 0.7 lejos, desvanecida entre 6 y 4, nada a menos de 4), radio de 1.2 al pecho, que apunte al centro del pedido, y la
// de la orilla: solo con el destino fuera de la cámara, a la orilla y apuntando hacia él. También el orden de dibujo
// (debajo de la burbuja, el globo de la clienta y el texto de choque) y, en el nivel 3, que vaya Unlit.
// La cámara se fija en 16:9 durante la prueba (como las capturas). Capturas 1920x1080 (sin HUD) a 10, 5 y 3.5 tiles, una
// con el destino fuera de pantalla y un acercamiento x3 del repartidor. El avance guardado queda como estaba.
// Temp/PruebaFlecha.pedido (dentro: carpeta para las capturas) → Temp/PruebaFlecha.resultado
[InitializeOnLoad]
public static class PruebaFlecha
{
    const string Pedido = "Temp/PruebaFlecha.pedido";
    const string Resultado = "Temp/PruebaFlecha.resultado";
    const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";
    const string RutaUnlit = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    const string P = "PruebaFlecha.";
    static readonly string[] ClavesProgreso = { "dinero_total", "motos_compradas", "moto_equipada" };
    static readonly string[] Niveles = { "SampleScene", "Nivel3_Lluvia" };
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
                pc.SetMotosHabilitadas(Nivel == 1);
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
                Comprobar(Jugador.EnMoto == (Nivel == 1), Nivel == 1 ? "va en moto" : "va a pie");
                Orden();
                Indice = 0;
                Paso = 4;
                break;

            case 4:   // poner al repartidor a la distancia del turno
                Poner(Lugar(Distancias[Indice]));
                SessionState.SetFloat(P + "amin", 9f);
                SessionState.SetFloat(P + "amax", -1f);
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
                var borde = Hijo("flecha_borde");
                var destino = (Vector2)GM.PuntoActual.Centro();
                Vector2 c = cam.transform.position, medio = new Vector2(cam.orthographicSize * cam.aspect, cam.orthographicSize);
                bool fuera = Mathf.Abs(destino.x - c.x) > medio.x || Mathf.Abs(destino.y - c.y) > medio.y;
                Comprobar(fuera, $"{N} el destino queda fuera de la cámara");
                Comprobar(f.VisibleBorde, $"{N} con el destino fuera de pantalla se ve el chevron de la orilla (alfa {(borde.enabled ? borde.color.a : 0):0.00})");
                if (borde.enabled)
                {
                    Vector2 bp = borde.transform.position;
                    float aOrilla = Mathf.Min(medio.x - Mathf.Abs(bp.x - c.x), medio.y - Mathf.Abs(bp.y - c.y));
                    Comprobar(aOrilla > 0.2f && aOrilla < 0.7f, $"{N} pegado a la orilla de la cámara ({aOrilla:0.00} de la orilla)");
                    float err = Vector2.Angle(borde.transform.right, destino - c);
                    Comprobar(err < 2f, $"{N} el de la orilla apunta al destino (error {err:0.0}°)");
                    Comprobar(Mathf.Abs(borde.transform.lossyScale.x - 0.6f) < 0.01f, $"{N} el de la orilla al 60% ({borde.transform.lossyScale.x:0.00})");
                }
                Capturar($"Flecha_{Niveles[Nivel]}_fuera_de_pantalla.png", 1f);
                Paso = 8;
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

    static Camera cam() => Camera.main;

    static SpriteRenderer Hijo(string nombre) => Flecha.transform.Find(nombre).GetComponent<SpriteRenderer>();

    // orden de dibujo y material
    static void Orden()
    {
        var orbita = Hijo("flecha_guia");
        var borde = Hijo("flecha_borde");
        Comprobar(orbita.sprite != null && orbita.sprite.texture.name == "flecha_chevron" && orbita.sprite.rect.width == 16 && borde.sprite == orbita.sprite,
                  $"{N} los dos usan el chevron fino ({orbita.sprite?.texture.name}, {orbita.sprite?.rect.width}x{orbita.sprite?.rect.height} px, escala {orbita.transform.lossyScale.x:0.##})");
        var encima = new List<(string, int)>();
        foreach (var dp in Object.FindObjectsByType<DeliveryPoint>(FindObjectsSortMode.None).Take(1))
            foreach (var r in dp.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.name == "Burbuja" || r.name == "Producto")) encima.Add(("burbuja/" + r.name, r.sortingOrder));
        var globo = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(m => m.name.StartsWith("Globo"));
        if (globo != null) encima.Add(("globo de la clienta", globo.sortingOrder));
        encima.Add(("texto de choque", 100));
        var entrega = Object.FindFirstObjectByType<EntregaProducto>(FindObjectsInactive.Include)?.GetComponentInChildren<SpriteRenderer>(true);
        if (entrega != null) encima.Add(("entrega del producto", entrega.sortingOrder));
        Comprobar(encima.All(e => e.Item2 > orbita.sortingOrder) && borde.sortingOrder == orbita.sortingOrder,
                  $"{N} la flecha (Order in Layer {orbita.sortingOrder}) va debajo de: {string.Join(", ", encima.Select(e => $"{e.Item1} {e.Item2}"))}");
        if (Nivel == 1)
        {
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(RutaUnlit);
            Comprobar(orbita.sharedMaterial == unlit && borde.sharedMaterial == unlit, $"{N} de noche va Unlit ({orbita.sharedMaterial?.name}, {borde.sharedMaterial?.name}): no la apaga la luz global");
        }
    }

    static void Medir(float d, float amin, float amax)
    {
        var f = Flecha;
        var orbita = Hijo("flecha_guia");
        var pc = Jugador;
        var destino = (Vector2)GM.PuntoActual.Centro();
        float real = Vector2.Distance(pc.transform.position, destino);
        string t = $"{N} a {real:0.0} tiles:";
        if (d >= 6.5f)
        {
            Comprobar(f.Visible && amin >= 0.44f && amin <= 0.5f && amax >= 0.65f && amax <= 0.71f, $"{t} se ve, alfa del pulso {amin:0.00}–{amax:0.00} (0.45–0.7)");
            Vector2 pecho = (Vector2)pc.transform.position + Vector2.up * 0.9f;
            Vector2 op = orbita.transform.position;
            Comprobar(Mathf.Abs(Vector2.Distance(op, pecho) - 1.2f) < 0.02f, $"{t} orbita a {Vector2.Distance(op, pecho):0.00} del pecho (1.2)");
            float err = Vector2.Angle(orbita.transform.right, destino - pecho);
            Comprobar(err < 1.5f, $"{t} apunta al centro del pedido (error {err:0.0}°)");
        }
        else if (d > 4f)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, 6f, real));
            Comprobar(f.Visible && amax < 0.7f * k + 0.02f && amax > 0.45f * k - 0.02f && amax < 0.6f,
                      $"{t} se desvanece: alfa máx {amax:0.00} (esperado {0.45f * k:0.00}–{0.7f * k:0.00})");
        }
        else
            Comprobar(!f.Visible && amax <= 0f, $"{t} no se ve (manda la burbuja)");
        // con el destino a la vista, nada en la orilla
        var c = (Vector2)cam().transform.position;
        Vector2 medio = new Vector2(cam().orthographicSize * cam().aspect, cam().orthographicSize);
        bool dentro = Mathf.Abs(destino.x - c.x) <= medio.x && Mathf.Abs(destino.y - c.y) <= medio.y;
        if (dentro) Comprobar(!f.VisibleBorde, $"{t} destino a la vista: sin chevron en la orilla");
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
