using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Prueba en Play del reloj por pedido y la cancelación, desde MenuPrincipal (como el build):
// Menú → Iniciar → Saltar → nivel 1: el reloj en verde (m:ss, cuenta regresiva hasta TiempoMaximo, bajo "Pedidos");
// la pausa lo detiene; el primer pedido se deja correr (tiempo x5) por amarillo, rojo y el parpadeo hasta que la
// clienta cancela ("¡Cancelado!", sin pago, satisfacción 0, Pedidos 1/3, el marcador se apaga y sale el siguiente;
// entregar el cancelado ya no hace nada); los otros dos se entregan a tiempo; panel final con
// "Pedidos: 0 · 100 · 100%", "Cancelados: 1" y 2 estrellas. Siguiente nivel → ELIGE TU MOTO (reloj escondido) →
// nivel 2 en moto: el primer pedido se cancela igual (tiempo x8). Capturas: Reloj_verde, Reloj_amarillo, Reloj_rojo,
// Cancelado, PanelFinal_cancelado, Cancelado_nivel2. El avance guardado queda como estaba al empezar.
// Temp/PruebaReloj.pedido (dentro: carpeta para las capturas) → Temp/PruebaReloj.resultado
[InitializeOnLoad]
public static class PruebaReloj
{
    const string Pedido = "Temp/PruebaReloj.pedido";
    const string Resultado = "Temp/PruebaReloj.resultado";
    const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";
    const string P = "PruebaReloj.";
    static readonly string[] ClavesProgreso = { "dinero_total", "motos_compradas", "moto_equipada" };
    const BindingFlags Privado = BindingFlags.Instance | BindingFlags.NonPublic;

    static bool enganchado;

    static PruebaReloj()
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
    static RelojPedido Reloj => Object.FindFirstObjectByType<RelojPedido>();
    static float Restante => GM.TiempoMaximo - GM.TiempoPedido;

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
            SessionState.SetFloat(P + "desde", 0);
            foreach (var k in ClavesProgreso)
                SessionState.SetString(P + "pp." + k, PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k).ToString() : "");
            PlayerPrefs.SetInt("motos_compradas", 0b1111);   // las 4 motos, solo durante la prueba
            PlayerPrefs.Save();
            var inicio = EditorSceneManager.playModeStartScene;
            SessionState.SetString(P + "inicio", inicio != null ? AssetDatabase.GetAssetPath(inicio) : "");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaMenu);
            var sinUI = RevisarCanvas.RevisarEscenasDeJuego();
            foreach (var falta in sinUI) Fallo(falta);
            if (sinUI.Count == 0) Log("  ok: Canvas completo (con PlacaReloj) en las 3 escenas de juego");
            Log("Entrando a Play desde MenuPrincipal.");
            Paso = 1;
            Esperar(2f);
            EditorApplication.isPlaying = true;
            return;
        }
        if (Esperando) return;
        try { Avanzar(); }
        catch (Exception e) { Fallo($"excepción en el paso {Paso}: {e}"); Time.timeScale = 1f; Paso = 90; EditorApplication.isPlaying = false; }
    }

    static void Avanzar()
    {
        if (Paso < 90 && !EditorApplication.isPlaying) { Fallo($"Play se cerró en el paso {Paso}"); Paso = 90; return; }
        switch (Paso)
        {
            case 1:
                Application.runInBackground = true;
                if (!EsperarA(() => Escena == "MenuPrincipal" && Time.timeSinceLevelLoad > 0.3f && Buscar("Canvas/BotonIniciar") != null, 15, "el menú")) return;
                Clic("Canvas/BotonIniciar");
                Siguiente(0.5f);
                break;
            case 2:
                if (!EsperarA(() => Escena == "Intro" && Time.timeSinceLevelLoad > 0.5f, 10, "la Intro")) return;
                Clic("Canvas/BotonSaltar");
                Siguiente(0.5f);
                break;

            case 3:   // nivel 1: reloj en verde
            {
                if (!EsperarA(() => Escena == "SampleScene" && GM != null && GM.PuntoActual != null && Time.timeSinceLevelLoad > 0.5f, 15, "el nivel 1")) return;
                Log($"1) Nivel 1: reloj del primer pedido (ideal {GM.TiempoIdeal:0.0} s, máximo {GM.TiempoMaximo:0.0} s)");
                var r = Reloj;
                Comprobar(r != null && r.EstadoActual == RelojPedido.Estado.Verde && Alfa(r) == 1f, $"reloj visible y en verde ({r?.EstadoActual})");
                int esperado = Mathf.CeilToInt(Restante);
                Comprobar(Regex.IsMatch(r.Texto, @"^\d+:\d\d$") && Math.Abs(Segundos(r.Texto) - esperado) <= 1,
                          $"cuenta regresiva m:ss: \"{r.Texto}\" (faltan {Restante:0.0} s)");
                var pedidos = (RectTransform)Buscar("Canvas/PlacaPedidos");
                var placa = (RectTransform)r.transform;
                var guia = (RectTransform)Buscar("Canvas/GuiaPedido");
                Comprobar(Abajo(pedidos) >= Arriba(placa) && Abajo(placa) >= Arriba(guia) && Mathf.Abs(pedidos.position.x - placa.position.x) < 1f,
                          "la placa va justo debajo de \"Pedidos\" y la guía debajo de ella");
                var texto = placa.Find("TextoReloj").GetComponent<TMP_Text>();
                Comprobar(FuenteProyecto.EsTitulo(texto.font) && ColorUtility.ToHtmlStringRGB(texto.color) == "2A1F40", "texto en Press Start 2P, #2A1F40");
                Capturar("Reloj_verde.png");
                Siguiente(0.3f);
                break;
            }
            case 4:
                Log("2) Pausa");
                SessionState.SetFloat(P + "t", GM.TiempoPedido);
                SessionState.SetString(P + "texto", Reloj.Texto);
                Clic("Canvas/BotonPausa");
                Siguiente(1.5f);
                break;
            case 5:
                Comprobar(Time.timeScale == 0f && Mathf.Abs(GM.TiempoPedido - SessionState.GetFloat(P + "t", 0)) < 0.05f && Reloj.Texto == SessionState.GetString(P + "texto", ""),
                          $"en pausa el reloj no corre ({SessionState.GetFloat(P + "t", 0):0.00} → {GM.TiempoPedido:0.00} s, \"{Reloj.Texto}\")");
                Clic("Canvas/PanelPausa/PausaSeguir");
                Siguiente(0.3f);
                break;
            case 6:
                Comprobar(Time.timeScale == 1f && GM.TiempoPedido > SessionState.GetFloat(P + "t", 0), "al seguir, el reloj vuelve a correr");
                SessionState.SetInt(P + "dinero", GM.Dinero);
                SessionState.SetString(P + "punto", GM.PuntoActual.name);
                Log("3) Se deja correr el primer pedido (tiempo x5)");
                Time.timeScale = 5f;
                Siguiente(0.1f);
                break;
            case 7:
                if (!EsperarA(() => GM.TiempoPedido >= GM.TiempoIdeal + 0.5f, 60, "que pase el tiempo ideal")) return;
                Comprobar(Reloj.EstadoActual == RelojPedido.Estado.Amarillo, $"pasado el ideal: {Reloj.EstadoActual} (\"{Reloj.Texto}\")");
                Capturar("Reloj_amarillo.png");
                Siguiente(0.1f);
                break;
            case 8:
                if (!EsperarA(() => Restante <= GM.TiempoMaximo * 0.25f - 0.5f, 60, "el último 25 %")) return;
                Comprobar(Reloj.EstadoActual == RelojPedido.Estado.Rojo, $"último 25 %: {Reloj.EstadoActual} (\"{Reloj.Texto}\")");
                Capturar("Reloj_rojo.png");
                Siguiente(0.1f);
                break;
            case 9:
            {
                if (!EsperarA(() => Restante <= 4f, 60, "los últimos 5 s")) return;
                var r = Reloj;
                Comprobar(r.Parpadeando && r.EstadoActual == RelojPedido.Estado.Rojo, $"últimos 5 s: rojo y parpadeando (\"{r.Texto}\")");
                // a velocidad normal y con la clienta a la vista para el globo (fuera del radio de entrega)
                Time.timeScale = 1f;
                var punto = GM.PuntoActual;
                Poner(punto.Centro() + new Vector2(0f, -(punto.RadioEntrega + 1.5f)));
                Siguiente(0.05f);
                break;
            }
            case 10:   // la cancelación
            {
                if (!EsperarA(() => GM.Cancelados == 1, 10, "la cancelación")) return;
                Log("4) Cancelado");
                var viejo = Object.FindObjectsByType<DeliveryPoint>(FindObjectsSortMode.None).First(p => p.name == SessionState.GetString(P + "punto", ""));
                var clienta = Object.FindObjectsByType<Cliente>(FindObjectsSortMode.None).FirstOrDefault(c => c.Punto == viejo);
                var globo = clienta != null ? (TextMeshPro)typeof(Cliente).GetField("globo", Privado).GetValue(clienta) : null;
                Comprobar(globo != null && globo.gameObject.activeInHierarchy && globo.text == "¡Cancelado!", $"globo de la clienta: \"{globo?.text}\"");
                Comprobar(GM.Entregados == 0 && GM.Resueltos == 1 && GM.Dinero == SessionState.GetInt(P + "dinero", 0), $"sin pago ni propina (${GM.Dinero}), resueltos {GM.Resueltos}");
                Comprobar(GM.SatisfaccionesPorPedido.Count == 1 && GM.SatisfaccionesPorPedido[0] == 0f, "cuenta con satisfacción 0");
                Comprobar(Texto("Canvas/PlacaPedidos/TextoPedidos") == "Pedidos 1/3", $"HUD: \"{Texto("Canvas/PlacaPedidos/TextoPedidos")}\"");
                Comprobar(!viejo.Activo && !viejo.transform.GetChild(0).gameObject.activeSelf, "el marcador y la burbuja de ese pedido se apagan");
                Comprobar(GM.PuntoActual != null && GM.PuntoActual != viejo, $"siguiente pedido asignado ({GM.PuntoActual?.name}); la flecha apunta a él");
                Comprobar(GM.TiempoPedido < 1f && Reloj.EstadoActual == RelojPedido.Estado.Verde, $"el reloj se reinicia en verde (\"{Reloj.Texto}\")");
                GM.Entregar(viejo);   // ya no se puede entregar el cancelado
                Comprobar(GM.Entregados == 0 && GM.Resueltos == 1 && GM.Dinero == SessionState.GetInt(P + "dinero", 0), "entregar el pedido cancelado no hace nada");
                Capturar("Cancelado.png", clienta.transform.position);
                Siguiente(1.8f);
                break;
            }
            case 11:
            {
                var viejo = Object.FindObjectsByType<DeliveryPoint>(FindObjectsSortMode.None).First(p => p.name == SessionState.GetString(P + "punto", ""));
                var clienta = Object.FindObjectsByType<Cliente>(FindObjectsSortMode.None).FirstOrDefault(c => c.Punto == viejo);
                Comprobar(clienta != null && !clienta.GloboVisible, "el globo se va a los 1.5 s");
                Log("5) Los otros dos se entregan a tiempo");
                GM.Entregar(GM.PuntoActual);
                Comprobar(GM.Entregados == 1 && GM.Dinero > 0 && Mathf.Approximately(GM.SatisfaccionesPorPedido.Last(), 100f),
                          $"entregado a tiempo: ${GM.Dinero}, satisfacción {GM.SatisfaccionesPorPedido.Last():0}");
                Comprobar(Texto("Canvas/PlacaPedidos/TextoPedidos") == "Pedidos 2/3", $"HUD: \"{Texto("Canvas/PlacaPedidos/TextoPedidos")}\"");
                Siguiente(0.5f);
                break;
            }
            case 12:
                GM.Entregar(GM.PuntoActual);
                Siguiente(2.5f);   // las estrellas se animan
                break;
            case 13:
            {
                if (!EsperarA(() => Buscar("Canvas/PanelFinal") != null && Buscar("Canvas/PanelFinal").gameObject.activeInHierarchy, 10, "el panel final")) return;
                Log("6) Panel final con un cancelado");
                string resumen = Texto("Canvas/PanelFinal/TextoResumen");
                Comprobar(resumen.Contains("Pedidos: 0 · 100 · 100%") && resumen.Contains("Cancelados: 1"), $"resumen: \"{resumen.Replace("\n", " / ")}\"");
                float promedio = GM.SatisfaccionesPorPedido.Average();
                var ui = Object.FindFirstObjectByType<UIManager>();
                var estrellas = (Image[])typeof(UIManager).GetField("estrellas", Privado).GetValue(ui);
                var llena = (Sprite)typeof(UIManager).GetField("estrellaLlena", Privado).GetValue(ui);
                int llenas = estrellas.Count(e => e != null && e.sprite == llena);
                Comprobar(Mathf.Abs(promedio - 66.67f) < 0.1f && llenas == 2, $"promedio {promedio:0.0} con el cancelado en 0 → {llenas} estrellas");
                Comprobar(Reloj.EstadoActual == RelojPedido.Estado.Oculto, "sin pedido activo el reloj se esconde");
                Capturar("PanelFinal_cancelado.png");
                Siguiente(0.3f);
                break;
            }
            case 14:
                Clic("Canvas/PanelFinal/BotonSiguiente");
                Siguiente(0.5f);
                break;
            case 15:
            {
                if (!EsperarA(() => Escena == "Nivel2_Comercial" && Object.FindFirstObjectByType<TiendaMotos>() != null
                                    && Object.FindFirstObjectByType<TiendaMotos>().Abierta && Time.unscaledTime > 0f, 15, "ELIGE TU MOTO")) return;
                Log("7) Nivel 2: ELIGE TU MOTO");
                Comprobar(Reloj != null && Reloj.EstadoActual == RelojPedido.Estado.Oculto && GM.TiempoPedido == 0f, "mientras se elige moto el reloj no se ve ni corre");
                Progreso.Equipar(0);   // Scooter
                Siguiente(0.5f);
                break;
            }
            case 16:
                Clic(RutaDe(Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                .First(t => t.name == "TiendaMotos_Fondo").Find("Ventana/BotonEmpezar")));
                Siguiente(0.5f);
                break;
            case 17:
            {
                if (!EsperarA(() => !GM.EnEspera && GM.PuntoActual != null && Time.timeScale == 1f, 10, "que empiece el nivel 2")) return;
                var pc = Object.FindFirstObjectByType<PlayerController>();
                Comprobar(pc.EnMoto && Reloj.EstadoActual == RelojPedido.Estado.Verde, $"en moto; reloj en verde (\"{Reloj.Texto}\", máximo {GM.TiempoMaximo:0.0} s)");
                Comprobar(Texto("Canvas/PlacaPedidos/TextoPedidos") == "Pedidos 0/5", $"HUD: \"{Texto("Canvas/PlacaPedidos/TextoPedidos")}\"");
                SessionState.SetString(P + "punto", GM.PuntoActual.name);
                Log("8) Nivel 2: se deja correr el primer pedido (tiempo x8)");
                Time.timeScale = 8f;
                Siguiente(0.1f);
                break;
            }
            case 18:
                if (!EsperarA(() => Restante <= 3f, 60, "el final del tiempo en el nivel 2")) return;
                Time.timeScale = 1f;
                Poner(GM.PuntoActual.Centro() + new Vector2(0f, -(GM.PuntoActual.RadioEntrega + 1.5f)));
                Siguiente(0.05f);
                break;
            case 19:
            {
                if (!EsperarA(() => GM.Cancelados == 1, 10, "la cancelación en el nivel 2")) return;
                var viejo = Object.FindObjectsByType<DeliveryPoint>(FindObjectsSortMode.None).First(p => p.name == SessionState.GetString(P + "punto", ""));
                var clienta = Object.FindObjectsByType<Cliente>(FindObjectsSortMode.None).FirstOrDefault(c => c.Punto == viejo);
                var globo = clienta != null ? (TextMeshPro)typeof(Cliente).GetField("globo", Privado).GetValue(clienta) : null;
                Comprobar(globo != null && globo.gameObject.activeInHierarchy && globo.text == "¡Cancelado!", $"nivel 2: globo \"{globo?.text}\"");
                Comprobar(GM.Entregados == 0 && GM.Resueltos == 1 && GM.Dinero == 0 && GM.SatisfaccionesPorPedido[0] == 0f, "nivel 2: sin pago, satisfacción 0");
                Comprobar(Texto("Canvas/PlacaPedidos/TextoPedidos") == "Pedidos 1/5" && GM.PuntoActual != viejo && !viejo.Activo,
                          $"nivel 2: \"{Texto("Canvas/PlacaPedidos/TextoPedidos")}\", siguiente pedido {GM.PuntoActual?.name}");
                Comprobar(Object.FindFirstObjectByType<PlayerController>().EnMoto, "nivel 2: sigue en moto");
                Capturar("Cancelado_nivel2.png", clienta.transform.position);
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

    // ------------------------------------------------------------------ ayudantes

    static float Alfa(RelojPedido r) => r.GetComponent<CanvasGroup>().alpha;
    static int Segundos(string mss) { var p = mss.Split(':'); return int.Parse(p[0]) * 60 + int.Parse(p[1]); }
    static float Arriba(RectTransform rt) { var c = new Vector3[4]; rt.GetWorldCorners(c); return c[1].y; }
    static float Abajo(RectTransform rt) { var c = new Vector3[4]; rt.GetWorldCorners(c); return c[0].y; }
    static string Texto(string ruta) => Buscar(ruta)?.GetComponent<TMP_Text>()?.text ?? "";

    static void Poner(Vector2 p)
    {
        var pc = Object.FindFirstObjectByType<PlayerController>();
        pc.GetComponent<Rigidbody2D>().position = p;
        pc.transform.position = p;
        var cam = Object.FindFirstObjectByType<CameraFollow>();
        cam.transform.position = cam.Limitar(new Vector3(p.x, p.y + 1.25f, cam.transform.position.z));
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
            Fallo($"clic en {ruta}: lo tapa {(arriba != null ? arriba.name : "nada (sin raycast)")} (punto {datos.position}, pantalla {Screen.width}x{Screen.height})");
            return;
        }
        ExecuteEvents.ExecuteHierarchy(arriba, datos, ExecuteEvents.pointerClickHandler);
        Log($"  clic en {ruta}");
    }

    static void Capturar(string archivo, Vector2? foco = null)   // foco: centrar la cámara ahí (la clienta)
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
            if (foco.HasValue) cam.transform.position = new Vector3(foco.Value.x, foco.Value.y, posCamara.z);
            var centro = cam.transform.position;
            if (CameraFollow.LimitesDelMapa(out var lim))
            {
                float h = cam.orthographicSize, w = h * 1920f / 1080f;
                cam.transform.position = new Vector3(Mathf.Clamp(centro.x, lim.xMin + w, lim.xMax - w),
                                                     Mathf.Clamp(centro.y, lim.yMin + h, lim.yMax - h), posCamara.z);
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
