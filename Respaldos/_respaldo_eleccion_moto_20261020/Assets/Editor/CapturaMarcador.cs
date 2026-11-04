using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Capturas en Play de SampleScene (1920x1080, con el HUD):
// 1) Salon.png: la fachada completa del Salón Brenn's; Salon_spawn.png: la cámara de siempre en el spawn
// 2) Marcador_0..2.png: el repartidor junto al pedido activo con la clienta esperando ("¡Mi pedido!") y
//    las estrellas del marcador en tres momentos del titileo
// 3) Gracias.png: justo después de entregar ("¡Gracias! +$N")
// Revisa que el marcador del pedido activo esté prendido, cambie de cuadro y los demás estén apagados.
// Temp/CapturaMarcador.pedido (dentro: carpeta para las capturas) → Temp/CapturaMarcador.resultado
[InitializeOnLoad]
public static class CapturaMarcador
{
    const string Pedido = "Temp/CapturaMarcador.pedido";
    const string Resultado = "Temp/CapturaMarcador.resultado";
    const string ClavePaso = "CapturaMarcador.paso", ClaveLog = "CapturaMarcador.log", ClaveConsola = "CapturaMarcador.consola",
                 ClaveCarpeta = "CapturaMarcador.carpeta", ClaveEspera = "CapturaMarcador.espera", ClaveInicio = "CapturaMarcador.inicio";

    static CapturaMarcador()
    {
        EditorApplication.update += Revisar;
        Application.logMessageReceived += Anotar;
    }

    static int Paso { get => SessionState.GetInt(ClavePaso, 0); set => SessionState.SetInt(ClavePaso, value); }
    static void Log(string s) => SessionState.SetString(ClaveLog, SessionState.GetString(ClaveLog, "") + s + "\n");
    static void Esperar(float segundos) => SessionState.SetFloat(ClaveEspera, (float)EditorApplication.timeSinceStartup + segundos);
    static bool Esperando => EditorApplication.timeSinceStartup < SessionState.GetFloat(ClaveEspera, 0);

    static void Anotar(string msg, string pila, LogType tipo)
    {
        if (Paso == 0 || tipo == LogType.Log) return;
        SessionState.SetString(ClaveConsola, SessionState.GetString(ClaveConsola, "") + $"[{tipo}] {msg}\n");
    }

    static void Revisar()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (Paso == 0)
        {
            if (!File.Exists(Pedido) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetString(ClaveCarpeta, File.ReadAllText(Pedido).Trim());
            File.Delete(Pedido);
            SessionState.SetString(ClaveLog, "");
            SessionState.SetString(ClaveConsola, "");
            // Play empieza en la escena abierta (SampleScene), no en el menú
            var inicio = EditorSceneManager.playModeStartScene;
            SessionState.SetString(ClaveInicio, inicio != null ? AssetDatabase.GetAssetPath(inicio) : "");
            EditorSceneManager.playModeStartScene = null;
            Paso = 1;
            Esperar(2f);
            EditorApplication.isPlaying = true;
            return;
        }
        if (Esperando) return;
        try { Avanzar(); }
        catch (System.Exception e) { Log("FALLO: " + e); Paso = 90; EditorApplication.isPlaying = false; }
    }

    static string cuadros = "";

    static void Avanzar()
    {
        if (Paso < 90 && !EditorApplication.isPlaying) { Esperar(0.5f); return; }
        var gm = GameManager.Instance;
        switch (Paso)
        {
            case 1:
                Application.runInBackground = true;   // solo en esta sesión de Play
                if (gm == null || gm.PuntoActual == null) { Esperar(0.3f); return; }
                Paso = 2; Esperar(1.2f);
                break;
            case 2:
                // el salón completo: la cámara sobre el centro de la fachada (7 tiles de alto) solo para la foto
                var salon = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).First(r => r.name == "empresa_brenns");
                Capturar("Salon.png", salon.bounds.center);
                Capturar("Salon_spawn.png");
                // el repartidor a un lado del pedido activo, en la banqueta
                var punto = gm.PuntoActual;
                var jugador = Object.FindFirstObjectByType<PlayerController>();
                var destino = (Vector2)punto.transform.position + new Vector2(-1.6f, -0.4f);
                var rb = jugador.GetComponent<Rigidbody2D>();
                if (rb != null) rb.position = destino;
                jugador.transform.position = destino;
                Log($"Pedido activo: {punto.name}; repartidor movido a {destino}.");
                var marca = punto.transform.GetChild(0);
                var otros = Object.FindObjectsByType<DeliveryPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(p => p != punto && p.transform.GetChild(0).gameObject.activeSelf).Select(p => p.name).ToList();
                Log(marca.gameObject.activeSelf ? "ok: el marcador del pedido activo está prendido" : "FALLO: el marcador del pedido activo está apagado");
                Log(otros.Count == 0 ? "ok: los marcadores de los otros 27 puntos están apagados" : "FALLO: marcadores prendidos en " + string.Join(", ", otros));
                Paso = 3; Esperar(4.5f);   // la clienta sale y llega a su lugar de espera
                break;
            case 3: case 4: case 5:
                var m = gm.PuntoActual.transform.GetChild(0).GetComponent<SpriteRenderer>();
                cuadros += m.sprite.name.Replace("estrellas_entrega_", "") + " ";
                Capturar($"Marcador_{Paso - 3}.png");
                Paso++; Esperar(0.37f);
                break;
            case 6:
                var cl = Object.FindObjectsByType<Cliente>(FindObjectsSortMode.None)
                    .Select(c => new SerializedObject(c).FindProperty("globo").objectReferenceValue as TMPro.TextMeshPro)
                    .FirstOrDefault(g => g != null && g.gameObject.activeInHierarchy);
                Log($"Cuadros del marcador en las tres capturas: {cuadros}");
                Log(cl != null ? $"Globo: \"{cl.text}\" con {cl.font.name}, tamaño {cl.fontSize}" : "FALLO: ningún globo visible");
                var anterior = gm.PuntoActual;
                gm.Entregar(anterior);
                Log($"Entregado {anterior.name}; marcador apagado: {!anterior.transform.GetChild(0).gameObject.activeSelf}; nuevo pedido: {gm.PuntoActual?.name}.");
                Paso = 7; Esperar(0.35f);
                break;
            case 7:
                Capturar("Gracias.png");
                Paso = 90;
                EditorApplication.isPlaying = false;
                Esperar(1f);
                break;
            case 90:
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var inicio = SessionState.GetString(ClaveInicio, "");
                EditorSceneManager.playModeStartScene = inicio.Length > 0 ? AssetDatabase.LoadAssetAtPath<SceneAsset>(inicio) : null;
                string consola = SessionState.GetString(ClaveConsola, "");
                File.WriteAllText(Resultado, SessionState.GetString(ClaveLog, "") +
                                  "\nCONSOLA (advertencias y errores en Play): " + (consola.Length == 0 ? "ninguno" : "\n" + consola));
                Paso = 0;
                break;
        }
    }

    // Render a 1920x1080 con la cámara de la escena, con el Canvas en Screen Space - Camera solo durante el render
    static void Capturar(string archivo, Vector3? centro = null)
    {
        var ruta = Path.Combine(SessionState.GetString(ClaveCarpeta, "Temp"), archivo);
        var cam = Camera.main;
        var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.isRootCanvas);
        var rt = new RenderTexture(1920, 1080, 24);
        var modo = canvas.renderMode;
        int orden = canvas.sortingOrder;
        var posCam = cam.transform.position;
        try
        {
            if (centro.HasValue) cam.transform.position = new Vector3(centro.Value.x, centro.Value.y, posCam.z);
            canvas.sortingOrder = 1000;   // en Screen Space - Camera el HUD se ordenaría por Y con el mapa
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
            Object.DestroyImmediate(tex);
        }
        finally
        {
            RenderTexture.active = null;
            cam.targetTexture = null;
            canvas.renderMode = modo;
            canvas.sortingOrder = orden;
            cam.transform.position = posCam;
            rt.Release();
        }
        Log($"captura 1920x1080: {ruta}");
    }
}
