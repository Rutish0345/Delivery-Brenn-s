using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Prueba en Play de los baches del nivel 2 (Nivel2_Comercial directo, motos de la tienda prestadas solo durante la
// prueba): a pie y con cada una de las 4 motos se pone al repartidor sobre un bache distinto y se revisa la
// sacudida (velocidad al 60 % por 0.8 s), el temblor de la cámara, el salpicón de 3 partículas, la penalización
// (5 x el factor de choques de la moto) y cuánto baja la satisfacción; que el cooldown no la repita al volver a
// entrar enseguida, y que un auto encima del bache no haga nada. Los autos se apartan (apagados) mientras tanto
// para que un choque no se mezcle con la medida. El avance guardado queda como estaba.
// Temp/PruebaBaches.pedido (dentro: carpeta para las capturas) → Temp/PruebaBaches.resultado
[InitializeOnLoad]
public static class PruebaBaches
{
    const string Pedido = "Temp/PruebaBaches.pedido";
    const string Resultado = "Temp/PruebaBaches.resultado";
    const string RutaNivel = "Assets/Scenes/Nivel2_Comercial.unity";
    const string P = "PruebaBaches.";
    static readonly string[] ClavesProgreso = { "dinero_total", "motos_compradas", "moto_equipada" };
    static readonly int[] Modos = { -1, 0, 1, 2, 3 };   // a pie y las 4 motos

    static bool enganchado;

    static PruebaBaches()
    {
        if (enganchado) return;
        enganchado = true;
        EditorApplication.update += Revisar;
        Application.logMessageReceived += Anotar;
    }

    static int Paso { get => SessionState.GetInt(P + "paso", 0); set => SessionState.SetInt(P + "paso", value); }
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
    static GameManager GM => GameManager.Instance;
    static PlayerController Jugador => Object.FindFirstObjectByType<PlayerController>();

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
            // Play arranca en el menú (IniciarDesdeMenu lo vuelve a poner al recargar); el paso 1 carga el nivel 2
            Log("Entrando a Play (menú) y cargando Nivel2_Comercial.");
            Indice = 0;
            Paso = 1;
            Esperar(2f);
            EditorApplication.isPlaying = true;
            return;
        }
        if (Esperando) return;
        try { Avanzar(); }
        catch (Exception e) { Fallo($"excepción en el paso {Paso}: {e}"); Paso = 90; EditorApplication.isPlaying = false; }
    }

    static float Penalizacion() =>
        (float)typeof(GameManager).GetField("penalizacion", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(GM);

    static Bache[] Baches() => Object.FindObjectsByType<Bache>(FindObjectsSortMode.None).OrderBy(b => b.name).ToArray();

    static void Poner(Vector2 p)
    {
        var pc = Jugador;
        pc.GetComponent<Rigidbody2D>().position = p;
        pc.transform.position = p;
    }

    static void Avanzar()
    {
        if (Paso < 90 && !EditorApplication.isPlaying)
        {
            // Play se cerró antes de tiempo (o nunca abrió): tras 20 s se aborta y se restaura el avance
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
                // siempre un nivel 2 recién cargado (nunca uno a medias o ya terminado)
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
                Comprobar(Baches().Length == 10, $"{Baches().Length} baches en el nivel");
                // empieza el nivel como lo hace "EMPEZAR NIVEL" (la tienda ya se probó en PruebaNivel2)
                var nivel = Object.FindFirstObjectByType<LevelManager>();
                if (tienda.Abierta)
                {
                    var ventana = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "TiendaMotos_Fondo");
                    ventana.Find("Ventana/BotonEmpezar").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                }
                // sin autos durante la medida (un choque también penaliza)
                foreach (var a in Object.FindObjectsByType<TrafficCar>(FindObjectsSortMode.None)) a.gameObject.SetActive(false);
                Paso = 2;
                Esperar(0.5f);
                break;
            }
            case 2:
            {
                // un modo: equipar, alejar del bache y esperar a que se refresque
                int modo = Modos[Indice];
                Progreso.Equipar(modo);
                var b = Baches()[Indice];
                Poner((Vector2)b.transform.position + new Vector2(0f, 3f));   // en el carril de al lado, lejos del trigger
                Log($"{Indice + 1}) {(modo < 0 ? "A pie" : Progreso.Motos[modo].nombre + $" (choques x{Progreso.Motos[modo].factorPenalizacion})")} sobre {b.name}");
                Paso = 3;
                Esperar(0.4f);
                break;
            }
            case 3:
            {
                var b = Baches()[Indice];
                SessionState.SetFloat(P + "pen", Penalizacion());
                SessionState.SetFloat(P + "sat", GM.SatisfaccionActual);
                SessionState.SetInt(P + "sac", b.Sacudidas);
                Poner(b.transform.position);
                Paso = 4;
                Esperar(0.15f);
                break;
            }
            case 4:
            {
                int modo = Modos[Indice];
                var b = Baches()[Indice];
                var cam = Object.FindFirstObjectByType<CameraFollow>();
                float dPen = Penalizacion() - SessionState.GetFloat(P + "pen", 0);
                float dSat = SessionState.GetFloat(P + "sat", 0) - GM.SatisfaccionActual;
                int sac = b.Sacudidas - SessionState.GetInt(P + "sac", 0);
                int activas = b.GetComponentsInChildren<SpriteRenderer>(true).Count(s => s.gameObject != b.gameObject && s.gameObject.activeInHierarchy);
                if (modo < 0)
                {
                    Comprobar(Jugador.EnMoto == false, "va a pie");
                    Comprobar(sac == 0 && Mathf.Approximately(Jugador.FactorVelocidad, 1f), "a pie no hay sacudida (velocidad x1)");
                    Comprobar(dPen == 0f && !cam.Temblando && activas == 0, $"sin penalización ({dPen}), sin temblor y sin salpicón");
                }
                else
                {
                    float esperado = 5f * Progreso.Motos[modo].factorPenalizacion;
                    Comprobar(Jugador.EnMoto, "va en moto");
                    Comprobar(sac == 1, "una sacudida al entrar");
                    Comprobar(Mathf.Approximately(Jugador.FactorVelocidad, 0.6f), $"velocidad x{Jugador.FactorVelocidad:0.00} ({Progreso.Motos[modo].velocidad * Jugador.FactorVelocidad:0.0} de {Progreso.Motos[modo].velocidad} tiles/s)");
                    Comprobar(cam.Temblando, "la cámara tiembla");
                    Comprobar(activas == 3, $"salpicón: {activas} partículas");
                    Comprobar(Mathf.Abs(dPen - esperado) < 1e-3f, $"penalización {dPen:0.0} (5 x {Progreso.Motos[modo].factorPenalizacion})");
                    Comprobar(dSat >= esperado - 0.5f, $"la satisfacción baja {dSat:0.0} puntos (barra de {SessionState.GetFloat(P + "sat", 0):0} a {GM.SatisfaccionActual:0})");
                    if (Indice == 1) Capturar("Bache_sacudida.png");
                }
                // salir y volver a entrar enseguida: el cooldown (1 s) no la repite
                Poner((Vector2)b.transform.position + new Vector2(0f, 3f));
                SessionState.SetInt(P + "sac", b.Sacudidas);
                Paso = 5;
                Esperar(0.15f);
                break;
            }
            case 5:
                Poner(Baches()[Indice].transform.position);
                Paso = 6;
                Esperar(0.2f);
                break;
            case 6:
            {
                var b = Baches()[Indice];
                if (Modos[Indice] >= 0)
                    Comprobar(b.Sacudidas == SessionState.GetInt(P + "sac", 0), "al volver a entrar antes de 1 s no se repite (cooldown)");
                Poner((Vector2)b.transform.position + new Vector2(0f, 3f));
                Paso = 7;
                Esperar(0.9f);
                break;
            }
            case 7:
                Comprobar(Mathf.Approximately(Jugador.FactorVelocidad, 1f), "a los 0.8 s vuelve a velocidad normal");
                Indice++;
                if (Indice < Modos.Length) { Paso = 2; break; }
                Paso = 8;
                break;
            case 8:
            {
                Log($"{Modos.Length + 1}) Un auto sobre un bache");
                var b = Baches()[Modos.Length];
                var auto = Object.FindObjectsByType<TrafficCar>(FindObjectsInactive.Include, FindObjectsSortMode.None).First();
                Progreso.Equipar(0);
                Poner(new Vector2(70f, 38.5f));                                    // el repartidor lejos, en moto
                SessionState.SetInt(P + "sac", b.Sacudidas);
                SessionState.SetFloat(P + "pen", Penalizacion());
                auto.gameObject.SetActive(true);
                auto.enabled = false;                                              // quieto encima del bache
                auto.GetComponent<Rigidbody2D>().position = (Vector2)b.transform.position - new Vector2(0f, 0.75f);
                auto.transform.position = (Vector2)b.transform.position - new Vector2(0f, 0.75f);
                Paso = 9;
                Esperar(0.4f);
                break;
            }
            case 9:
            {
                var b = Baches()[Modos.Length];
                Comprobar(b.Sacudidas == SessionState.GetInt(P + "sac", 0) && Penalizacion() == SessionState.GetFloat(P + "pen", 0),
                          "el auto no dispara el bache ni penaliza");
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
                int fallos = SessionState.GetInt(P + "fallos", 0);
                string consola = SessionState.GetString(P + "consola", "");
                File.WriteAllText(Resultado, (fallos == 0 ? "OK" : $"{fallos} FALLOS") + "\n" + SessionState.GetString(P + "log", "") +
                                  "\nCONSOLA (advertencias y errores en Play): " + (consola.Length == 0 ? "ninguno" : "\n" + consola));
                Paso = 0;
                break;
        }
    }

    static void Capturar(string archivo)
    {
        var ruta = Path.Combine(SessionState.GetString(P + "carpeta", "Temp"), archivo);
        var cam = Camera.main;
        var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.isRootCanvas && c.transform.Find("PanelFinal") != null);
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
        Log($"  captura 1920x1080: {ruta}");
    }
}
