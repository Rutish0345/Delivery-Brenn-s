using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Prueba de la UI en Play: pausa → Seguir → pausa → Reiniciar; termina el nivel (Pedidos = 1 solo
// durante la prueba) → captura del PanelFinal con "Siguiente nivel" encendido → Jugar de nuevo →
// captura de la pausa → termina otra vez → Menú. Los clics pasan por el EventSystem (raycast en el
// centro del botón), así que también detecta si algo tapa el botón. Pedidos = 1 se guarda en la escena
// mientras dura la prueba (al reiniciar en Play, Unity carga la escena del disco); al salir de Play
// vuelve a Pedidos = 3 y se guarda otra vez.
// Temp/PruebaUI.pedido (dentro: carpeta para las capturas) → Temp/PruebaUI.resultado
[InitializeOnLoad]
public static class PruebaPlayUI
{
    const string Pedido = "Temp/PruebaUI.pedido";
    const string Resultado = "Temp/PruebaUI.resultado";
    const string ClavePaso = "PruebaUI.paso", ClaveLog = "PruebaUI.log", ClaveConsola = "PruebaUI.consola",
                 ClaveCarpeta = "PruebaUI.carpeta", ClaveEspera = "PruebaUI.espera", ClaveId = "PruebaUI.id",
                 ClaveFallos = "PruebaUI.fallos", ClaveDesde = "PruebaUI.desde";

    static bool enganchado;

    static PruebaPlayUI()
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

    // espera en tiempo real (la pausa pone timeScale en 0)
    static void Esperar(float segundos) => SessionState.SetFloat(ClaveEspera, (float)EditorApplication.timeSinceStartup + segundos);
    static bool Esperando => EditorApplication.timeSinceStartup < SessionState.GetFloat(ClaveEspera, 0);

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
            SessionState.SetInt(ClaveFallos, 0);
            if (!PonerPedidos(1)) { Paso = 0; File.WriteAllText(Resultado, "NO HAY LevelManager"); return; }
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Log("Pedidos = 1 en el LevelManager (temporal, guardado). Entrando a Play.");
            Paso = 1;
            Esperar(2.5f);
            EditorApplication.isPlaying = true;
            return;
        }
        if (Esperando) return;
        try { Avanzar(); }
        catch (Exception e) { Fallo($"excepción en el paso {Paso}: {e}"); Paso = 90; EditorApplication.isPlaying = false; }
    }

    static void Avanzar()
    {
        if (Paso < 90 && !EditorApplication.isPlaying) { Esperar(0.5f); return; }   // todavía entrando
        switch (Paso)
        {
            case 1:
                // que la escena ya haya corrido sus Start y el Canvas tenga tamaño (si no, el raycast no da en nada)
                if (!EsperarA(() => GameManager.Instance != null && GameManager.Instance.PuntoActual != null
                                    && Screen.width > 0 && Time.frameCount > 5, 15, "a que arranque la escena")) return;
                Log("1) Al iniciar");
                Comprobar(Mathf.Approximately(Time.timeScale, 1f), $"Time.timeScale = {Time.timeScale}");
                Comprobar(!Panel("PanelPausa").activeSelf && !Panel("PanelFinal").activeSelf, "PanelPausa y PanelFinal apagados");
                Comprobar(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "un solo EventSystem");
                Log("2) Clic en BotonPausa");
                Clic("Canvas/BotonPausa");
                Siguiente(0.5f);
                break;
            case 2:
                Comprobar(Panel("PanelPausa").activeInHierarchy, "PanelPausa se abrió");
                Comprobar(Time.timeScale == 0f, $"juego pausado (timeScale {Time.timeScale})");
                Log("3) Clic en Seguir");
                Clic("Canvas/PanelPausa/PausaSeguir");
                Siguiente(0.5f);
                break;
            case 3:
                Comprobar(!Panel("PanelPausa").activeInHierarchy, "PanelPausa se cerró");
                Comprobar(Time.timeScale == 1f, $"juego sigue (timeScale {Time.timeScale})");
                Log("4) Pausa y clic en Reiniciar");
                Clic("Canvas/BotonPausa");
                GuardarIdNivel();
                Siguiente(0.5f);
                break;
            case 4:
                Clic("Canvas/PanelPausa/PausaReiniciar");
                Siguiente(2f);
                break;
            case 5:
                if (!EsperarRecarga()) return;
                ComprobarRecarga("Reiniciar desde la pausa");
                Log("5) Terminar el nivel (1 pedido)");
                var gm = GameManager.Instance;
                Comprobar(gm.PedidosMeta == 1, $"PedidosMeta = {gm.PedidosMeta}");
                gm.Entregar(gm.PuntoActual);
                Siguiente(2.5f);   // las estrellas se animan
                break;
            case 6:
                if (!EsperarA(() => Panel("PanelFinal").activeInHierarchy, 10, "que aparezca el PanelFinal")) return;
                var final = Panel("PanelFinal");
                Comprobar(final.activeInHierarchy, "PanelFinal visible");
                var sig = final.transform.Find("BotonSiguiente").gameObject;
                Comprobar(!sig.activeSelf, "BotonSiguiente apagado (no hay escena siguiente)");
                Comprobar(final.GetComponentsInChildren<Button>(true).Count(b => b.name == "BotonTienda") == 1, "un solo BotonTienda");
                foreach (var b in new[] { "BotonJugarDeNuevo", "BotonMenu", "BotonTienda" })
                    Comprobar(BotonAlcanzable("Canvas/PanelFinal/" + b), $"{b} recibe el clic (nada lo tapa)");
                sig.SetActive(true);   // solo para la captura
                Log("6) Captura del PanelFinal con Siguiente nivel encendido");
                Capturar("PanelFinal.png");
                Siguiente(1.5f);
                break;
            case 7:
                Comprobar(BotonAlcanzable("Canvas/PanelFinal/BotonSiguiente"), "BotonSiguiente recibe el clic");
                Log("7) Clic en Jugar de nuevo");
                GuardarIdNivel();
                Clic("Canvas/PanelFinal/BotonJugarDeNuevo");
                Siguiente(2f);
                break;
            case 8:
                if (!EsperarRecarga()) return;
                ComprobarRecarga("Jugar de nuevo");
                Comprobar(!Panel("PanelFinal").activeSelf, "PanelFinal apagado tras reiniciar");
                Log("8) Captura de la pausa");
                Clic("Canvas/BotonPausa");
                Siguiente(0.5f);
                break;
            case 9:
                Capturar("PanelPausa.png");
                Siguiente(1.5f);
                break;
            case 10:
                Clic("Canvas/PanelPausa/PausaSeguir");
                Comprobar(Time.timeScale == 1f, "Seguir quita la pausa");
                Log("9) Terminar otra vez y clic en Menú");
                GameManager.Instance.Entregar(GameManager.Instance.PuntoActual);
                Siguiente(2.5f);
                break;
            case 11:
                if (!EsperarA(() => Panel("PanelFinal").activeInHierarchy, 10, "que aparezca el PanelFinal")) return;
                Clic("Canvas/PanelFinal/BotonMenu");
                Siguiente(2.5f);
                break;
            case 12:
                if (!EsperarA(() => SceneManager.GetActiveScene().name == "MenuPrincipal", 10, "que cargue MenuPrincipal")) return;
                Comprobar(SceneManager.GetActiveScene().name == "MenuPrincipal", $"escena activa: {SceneManager.GetActiveScene().name}");
                Comprobar(Time.timeScale == 1f, $"timeScale en el menú = {Time.timeScale}");
                Paso = 90;
                EditorApplication.isPlaying = false;
                Esperar(1f);
                break;
            case 90:
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                PonerPedidos(3);
                bool guardada = EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                Log($"Fuera de Play: Pedidos = 3 otra vez y escena {(guardada ? "guardada" : "SIN GUARDAR")}.");
                int fallos = SessionState.GetInt(ClaveFallos, 0);
                string consola = SessionState.GetString(ClaveConsola, "");
                File.WriteAllText(Resultado, (fallos == 0 ? "OK" : $"{fallos} FALLOS") + "\n" + SessionState.GetString(ClaveLog, "") +
                                  "\nCONSOLA (advertencias y errores en Play): " + (consola.Length == 0 ? "ninguno" : "\n" + consola));
                Paso = 0;
                break;
        }
    }

    static void Siguiente(float espera) { Paso++; Esperar(espera); }

    // true cuando se cumple la condición (o se acabó el tiempo, que cuenta como fallo); mientras, false
    static bool EsperarA(Func<bool> condicion, float limite, string que)
    {
        float ahora = (float)EditorApplication.timeSinceStartup;
        if (SessionState.GetFloat(ClaveDesde, 0) == 0) SessionState.SetFloat(ClaveDesde, ahora);
        bool listo = condicion();
        if (!listo && ahora - SessionState.GetFloat(ClaveDesde, 0) < limite) { Esperar(0.2f); return false; }
        if (!listo) Fallo($"se acabó el tiempo esperando {que}");
        SessionState.SetFloat(ClaveDesde, 0);
        return true;
    }

    // la escena nueva ya corrió sus Start (el GameManager eligió punto) y pasó al menos un frame más
    static bool EsperarRecarga() => EsperarA(() =>
    {
        var nivel = Object.FindFirstObjectByType<LevelManager>();
        return nivel != null && nivel.GetInstanceID() != SessionState.GetInt(ClaveId, 0)
               && GameManager.Instance != null && GameManager.Instance.PuntoActual != null;
    }, 15, "a que se recargue la escena");

    static GameObject Panel(string nombre) =>
        Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.isRootCanvas)
            .Select(c => c.transform.Find(nombre)).First(t => t != null).gameObject;

    static Transform Buscar(string ruta)
    {
        var partes = ruta.Split(new[] { '/' }, 2);
        var raiz = SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == partes[0]).transform;
        return partes.Length == 1 ? raiz : raiz.Find(partes[1]);
    }

    // Raycast del EventSystem en el centro del botón; devuelve el objeto de arriba
    static GameObject Arriba(RectTransform rt, out PointerEventData datos)
    {
        datos = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rt.position) };
        var golpes = new List<RaycastResult>();
        EventSystem.current.RaycastAll(datos, golpes);
        return golpes.Count > 0 ? golpes[0].gameObject : null;
    }

    static bool BotonAlcanzable(string ruta)
    {
        var rt = (RectTransform)Buscar(ruta);
        var arriba = Arriba(rt, out _);
        return arriba != null && (arriba.transform == rt || arriba.transform.IsChildOf(rt));
    }

    static void Clic(string ruta)
    {
        var t = Buscar(ruta);
        if (t == null || !t.gameObject.activeInHierarchy) { Fallo($"{ruta} no existe o está apagado"); return; }
        var arriba = Arriba((RectTransform)t, out var datos);
        if (arriba == null || !(arriba.transform == t || arriba.transform.IsChildOf(t)))
        {
            Fallo($"clic en {ruta}: lo tapa {(arriba != null ? arriba.name : "nada (sin raycast)")}");
            return;
        }
        ExecuteEvents.ExecuteHierarchy(arriba, datos, ExecuteEvents.pointerClickHandler);
        Log($"  clic en {ruta}");
    }

    static void GuardarIdNivel() => SessionState.SetInt(ClaveId, Object.FindFirstObjectByType<LevelManager>().GetInstanceID());

    static void ComprobarRecarga(string que)
    {
        var nivel = Object.FindFirstObjectByType<LevelManager>();
        Comprobar(nivel != null && nivel.GetInstanceID() != SessionState.GetInt(ClaveId, 0), $"{que}: la escena se recargó");
        Comprobar(Time.timeScale == 1f, $"{que}: timeScale = {Time.timeScale}");
        Comprobar(!Panel("PanelPausa").activeSelf, $"{que}: PanelPausa apagado");
    }

    static void Capturar(string archivo)
    {
        var carpeta = SessionState.GetString(ClaveCarpeta, "Temp");
        var ruta = Path.Combine(carpeta, archivo);
        // La Game view suele estar acoplada y pequeña: se renderiza a 1920x1080 con la cámara principal,
        // pasando el Canvas a Screen Space - Camera solo durante el render (estamos en Play: nada queda)
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
            canvas.sortingLayerID = SortingLayer.layers.Last().id;   // encima de todos los sprites, como en Overlay
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

    static bool PonerPedidos(int n)
    {
        var nivel = Object.FindFirstObjectByType<LevelManager>(FindObjectsInactive.Include);
        if (nivel == null) return false;
        var so = new SerializedObject(nivel);
        so.FindProperty("pedidos").intValue = n;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }
}
