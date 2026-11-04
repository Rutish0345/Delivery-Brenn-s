using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Prueba en Play del panel "ELIGE TU MOTO" a 1920x1080, desde MenuPrincipal (como el build) → Nivel2_Comercial:
// 1) sin comprar (con dinero para todas), 2) con dinero insuficiente (solo alcanza la Scooter: las demás apagadas y su
//    precio en rojo #FF5D73), 3) con motos compradas (Scooter comprada → "ELEGIR" dorado; Clásica comprada con un clic y
//    elegida → borde dorado de 4 px e insignia "EN USO"). En cada estado mide todos los textos de la ventana: ninguno se
//    desborda (TMP preferredWidth/preferredHeight contra su RectTransform), ninguno mide menos de 20 px ni usa Auto
//    Size, los de cada tarjeta quedan dentro de su tarjeta y no se enciman entre ellos, y la descripción va en 2 líneas
//    como máximo. También las medidas del panel (1500x860, tarjetas 330x520 separadas 24, botones de abajo de 92
//    separados 40) y el fondo del panel al 96%. Al final "EMPEZAR NIVEL" arranca el nivel con la moto elegida.
// Capturas: Motos_sin_comprar, Motos_dinero_insuficiente, Motos_comprada.
// Temp/PruebaMotos.pedido (dentro: carpeta para las capturas) → Temp/PruebaMotos.resultado
[InitializeOnLoad]
public static class PruebaMotos
{
    const string Pedido = "Temp/PruebaMotos.pedido";
    const string Resultado = "Temp/PruebaMotos.resultado";
    const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";
    const string P = "PruebaMotos.";

    static bool enganchado;

    static PruebaMotos()
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
    static TiendaMotos Tienda => Object.FindFirstObjectByType<TiendaMotos>();
    static Transform Ventana => Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                      .FirstOrDefault(t => t.name == "TiendaMotos_Fondo")?.Find("Ventana");

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
            ProgresoPrueba.Limpiar();
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
                if (!EsperarA(() => Escena == "MenuPrincipal" && MenuPrueba.Listo(), 15, "el menú")) return;
                Progreso.Reiniciar();
                Progreso.Agregar(1000);   // alcanza para todas
                SceneManager.LoadScene("Nivel2_Comercial");
                Siguiente(0.5f);
                break;
            case 2:
            {
                if (!EsperarA(() => Escena == "Nivel2_Comercial" && Tienda != null && Tienda.Abierta && Ventana != null, 20, "ELIGE TU MOTO en el nivel 2")) return;
                Log("1) Sin comprar, con $1000");
                var v = Ventana;
                Comprobar(v.Find("Titulo").GetComponent<TMP_Text>().text == "ELIGE TU MOTO", "título ELIGE TU MOTO");
                Medidas(v);
                Comprobar(Botones().All(b => b.interactable && Texto(b) == "COMPRAR\n$" + Precio(b)), "las 4 dicen COMPRAR y su precio, y se pueden comprar: " + Lista());
                Comprobar(Insignias().All(i => !i.activeSelf) && Bordes().All(b => !b.activeSelf), "ninguna elegida: sin insignia ni borde dorado");
                Medir(v, "sin comprar");
                Capturar("Motos_sin_comprar.png");
                Siguiente(0.2f);
                break;
            }
            case 3:
                Progreso.Reiniciar();
                Progreso.Agregar(150);   // solo la Scooter ($120)
                Siguiente(0.2f);
                break;
            case 4:
            {
                Log("2) Dinero insuficiente: $150");
                var v = Ventana;
                var b = Botones();
                Comprobar(v.Find("Dinero").GetComponent<TMP_Text>().text == "Tu dinero: $150", "Tu dinero: $150");
                Comprobar(b[0].interactable && b.Skip(1).All(x => !x.interactable), "solo la Scooter se puede comprar; las demás apagadas");
                Comprobar(b.Skip(1).All(x => Texto(x).Contains($"<color={TiendaMotos.RojoPrecio}>")) && !Texto(b[0]).Contains("<color"),
                          $"el precio que no alcanza va en rojo {TiendaMotos.RojoPrecio}: " + Lista());
                Comprobar(b.Skip(1).All(x => x.GetComponent<BotonBrenn>().Borde.color != Color.white), "los que no alcanzan, en gris");
                Medir(v, "dinero insuficiente");
                Capturar("Motos_dinero_insuficiente.png");
                Siguiente(0.2f);
                break;
            }
            case 5:
                Progreso.Agregar(250);       // $400
                Progreso.DarMotos(0b0001);   // la Scooter ya comprada (sin elegir)
                Progreso.Equipar(-1);
                Siguiente(0.2f);
                break;
            case 6:
                Clic(RutaDe(Ventana.Find("Fila/Tarjeta_clasica_azul/Boton")));   // compra la Clásica y la elige
                Siguiente(0.3f);
                break;
            case 7:
            {
                Log("3) Motos compradas: Scooter (sin elegir) y Clásica (elegida con un clic)");
                var v = Ventana;
                var b = Botones();
                Comprobar(Progreso.Tiene(1) && Progreso.MotoEquipada == 1 && Progreso.Dinero == 220, $"el clic compró y eligió la Clásica (dinero {Progreso.Dinero})");
                Comprobar(Texto(b[0]) == "ELEGIR" && b[0].GetComponent<BotonBrenn>().Marcado, "Scooter comprada: ELEGIR en dorado");
                var tarjeta = v.Find("Fila/Tarjeta_clasica_azul");
                var borde = tarjeta.Find("BordeElegida");
                bool cuatro = borde.Cast<Transform>().All(t => { var r = ((RectTransform)t).rect; return Mathf.Approximately(Mathf.Min(r.width, r.height), TiendaMotos.BordeElegida); })
                              && borde.GetComponentsInChildren<Image>().All(i => ColorUtility.ToHtmlStringRGB(i.color) == "FFD166");
                Comprobar(borde.gameObject.activeSelf && cuatro && tarjeta.Find("Insignia").gameObject.activeSelf
                          && tarjeta.Find("Insignia/Texto").GetComponent<TMP_Text>().text == "EN USO",
                          "Clásica elegida: borde dorado #FFD166 de 4 px y la insignia EN USO");
                Comprobar(Insignias().Count(i => i.activeSelf) == 1, "una sola insignia EN USO");
                Comprobar(Texto(b[2]).Contains($"<color={TiendaMotos.RojoPrecio}>") && !b[2].interactable, $"Chopper ($260) sigue sin alcanzar con $220 (en rojo): {Texto(b[2])}");
                Comprobar(v.Find("BotonAPie").GetComponent<Button>().interactable, "IR A PIE se puede usar (hay moto elegida)");
                Medir(v, "con motos compradas");
                Capturar("Motos_comprada.png");
                Siguiente(0.2f);
                break;
            }
            case 8:
                Clic(RutaDe(Ventana.Find("BotonEmpezar")));
                Siguiente(0.5f);
                break;
            case 9:
                if (!EsperarA(() => Tienda != null && !Tienda.Abierta && Time.timeScale == 1f, 10, "que empiece el nivel")) return;
                Comprobar(Object.FindFirstObjectByType<PlayerController>() != null && Progreso.MotoEquipada == 1, "EMPEZAR NIVEL arranca el nivel con la Clásica");
                Paso = 90;
                EditorApplication.isPlaying = false;
                Esperar(1f);
                break;

            case 90:
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                ProgresoPrueba.Limpiar();
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

    // ------------------------------------------------------------------ medidas

    static List<Button> Botones() => Progreso.Motos.Select(m => Ventana.Find($"Fila/Tarjeta_{m.id}/Boton").GetComponent<Button>()).ToList();
    static List<GameObject> Insignias() => Progreso.Motos.Select(m => Ventana.Find($"Fila/Tarjeta_{m.id}/Insignia").gameObject).ToList();
    static List<GameObject> Bordes() => Progreso.Motos.Select(m => Ventana.Find($"Fila/Tarjeta_{m.id}/BordeElegida").gameObject).ToList();
    static string Texto(Button b) => b.GetComponentInChildren<TMP_Text>().text;
    static int Precio(Button b) => Progreso.Motos[Botones().IndexOf(b)].precio;
    static string Lista() => string.Join(", ", Botones().Select(b => Texto(b).Replace("\n", " ")));

    // El panel y sus piezas, con las medidas pedidas
    static void Medidas(Transform v)
    {
        var rt = (RectTransform)v;
        var img = v.GetComponent<Image>();
        Comprobar(rt.rect.size == new Vector2(TiendaMotos.AnchoVentana, TiendaMotos.AltoVentana) && Mathf.Approximately(rt.anchoredPosition.magnitude, 0f),
                  $"panel {rt.rect.width}x{rt.rect.height} centrado");
        Comprobar(Mathf.Approximately(img.color.a, TiendaMotos.AlfaVentana) && img.sprite != null && img.sprite.name == "panel",
                  $"fondo del panel #2A1F40 (EstiloBrenn.panel) al {img.color.a:P0}");
        var tarjetas = Progreso.Motos.Select(m => (RectTransform)v.Find("Fila/Tarjeta_" + m.id)).ToList();
        bool tam = tarjetas.All(t => t.rect.size == new Vector2(TiendaMotos.AnchoTarjeta, TiendaMotos.AltoTarjeta));
        bool separadas = Enumerable.Range(1, tarjetas.Count - 1).All(i =>
            Mathf.Approximately(tarjetas[i].anchoredPosition.x - tarjetas[i - 1].anchoredPosition.x, TiendaMotos.AnchoTarjeta + TiendaMotos.SeparacionTarjetas));
        Comprobar(tam && separadas, $"4 tarjetas de {TiendaMotos.AnchoTarjeta}x{TiendaMotos.AltoTarjeta} separadas {TiendaMotos.SeparacionTarjetas}");
        var aPie = (RectTransform)v.Find("BotonAPie");
        var empezar = (RectTransform)v.Find("BotonEmpezar");
        float hueco = (empezar.anchoredPosition.x - empezar.rect.width / 2) - (aPie.anchoredPosition.x + aPie.rect.width / 2);
        Comprobar(aPie.rect.height == TiendaMotos.AltoAbajo && empezar.rect.height == TiendaMotos.AltoAbajo && Mathf.Approximately(hueco, TiendaMotos.SeparacionAbajo)
                  && aPie.GetComponent<BotonBrenn>() != null && empezar.GetComponent<BotonBrenn>() != null
                  && aPie.GetComponentInChildren<TMP_Text>().fontSize == TiendaMotos.TamAbajo && empezar.GetComponentInChildren<TMP_Text>().fontSize == TiendaMotos.TamAbajo,
                  $"IR A PIE y EMPEZAR NIVEL: BotonBrenn de {TiendaMotos.AltoAbajo} con texto de {TiendaMotos.TamAbajo}, separados {hueco}");
        Comprobar(v.GetComponentsInChildren<TMP_Text>().All(t => FuenteProyecto.EsTitulo(t.font)), "todos los textos en Press Start 2P");
        var tamanos = new Dictionary<string, float>
        {
            ["Titulo"] = TiendaMotos.TamTitulo, ["Dinero"] = TiendaMotos.TamDinero,
            [$"Fila/Tarjeta_{Progreso.Motos[0].id}/Nombre"] = TiendaMotos.TamNombre,
            [$"Fila/Tarjeta_{Progreso.Motos[0].id}/Barras/Velocidad/Etiqueta"] = TiendaMotos.TamEtiqueta,
            [$"Fila/Tarjeta_{Progreso.Motos[0].id}/Datos"] = TiendaMotos.TamDescripcion,
            [$"Fila/Tarjeta_{Progreso.Motos[0].id}/Boton/Texto"] = TiendaMotos.TamBotonTarjeta,
        };
        Comprobar(tamanos.All(p => v.Find(p.Key)?.GetComponent<TMP_Text>()?.fontSize == p.Value),
                  "tamaños: " + string.Join(", ", tamanos.Select(p => $"{p.Key.Split('/').Last()} {v.Find(p.Key)?.GetComponent<TMP_Text>()?.fontSize}")));
        var dinero = v.Find("Dinero").GetComponent<TMP_Text>();
        var nombre = v.Find($"Fila/Tarjeta_{Progreso.Motos[0].id}/Nombre").GetComponent<TMP_Text>();
        Comprobar(ColorUtility.ToHtmlStringRGB(dinero.color) == "FFD166" && ColorUtility.ToHtmlStringRGB(nombre.color) == "FF6FA5",
                  "dinero dorado y nombres rosas");
        Comprobar(Progreso.Motos.All(m => v.Find($"Fila/Tarjeta_{m.id}/Barras/Velocidad/Etiqueta").GetComponent<TMP_Text>().text == "VEL"
                                          && v.Find($"Fila/Tarjeta_{m.id}/Barras/Resistencia/Etiqueta").GetComponent<TMP_Text>().text == "RES"
                                          && ((RectTransform)v.Find($"Fila/Tarjeta_{m.id}/Barras/Velocidad/Seg_0")).rect.width == TiendaMotos.LadoCuadro),
                  $"etiquetas VEL y RES con cuadros de {TiendaMotos.LadoCuadro}");
        Comprobar(Progreso.Motos.All(m => m.descripcion.Split('\n').All(l => l.Length <= 28) && !m.descripcion.Contains('\n')),
                  "descripciones de una frase corta: " + string.Join(" / ", Progreso.Motos.Select(m => m.descripcion)));
    }

    // Que ningún texto se desborde ni sea chico, y que los de cada tarjeta queden dentro de ella sin encimarse
    static void Medir(Transform v, string estado)
    {
        Canvas.ForceUpdateCanvases();
        var malos = new List<string>();
        int n = 0;
        float menor = float.MaxValue;
        foreach (var t in v.GetComponentsInChildren<TMP_Text>())
        {
            t.ForceMeshUpdate();
            n++;
            var r = t.rectTransform.rect;
            // lo que se parte en renglones se mide con su ancho (preferredWidth a secas lo mide en una sola línea)
            Vector2 pref = t.textWrappingMode == TextWrappingModes.NoWrap ? new Vector2(t.preferredWidth, t.preferredHeight) : t.GetPreferredValues(t.text, r.width, 0);
            float pw = pref.x, ph = pref.y;
            menor = Mathf.Min(menor, t.fontSize);
            string quien = $"{t.transform.parent.name}/{t.name} \"{t.text.Replace("\n", "\\n")}\"";
            if (pw > r.width + 0.5f || ph > r.height + 0.5f) malos.Add($"{quien}: {pw:0}x{ph:0} en {r.width:0}x{r.height:0}");
            if (t.isTextOverflowing) malos.Add($"{quien}: se desborda");
            if (t.fontSize < TiendaMotos.TamMinimo || t.enableAutoSizing) malos.Add($"{quien}: {t.fontSize} px{(t.enableAutoSizing ? " con Auto Size" : "")}");
            if (t.name == "Datos" && t.textInfo.lineCount > TiendaMotos.LineasDescripcion) malos.Add($"{quien}: {t.textInfo.lineCount} líneas");
        }
        foreach (var m in Progreso.Motos)
        {
            var tarjeta = (RectTransform)v.Find("Fila/Tarjeta_" + m.id);
            var caja = EnPantalla(tarjeta);
            var textos = tarjeta.GetComponentsInChildren<TMP_Text>().Where(t => t.transform.parent.name != "Insignia").ToList();
            var cajas = textos.Select(t => (t, r: Tinta(t))).ToList();
            foreach (var (t, r) in cajas)
                if (!caja.Contains(r.min) || !caja.Contains(r.max)) malos.Add($"{m.nombre}/{t.name}: sale de la tarjeta");
            for (int i = 0; i < cajas.Count; i++)
                for (int j = i + 1; j < cajas.Count; j++)
                    if (cajas[i].r.Overlaps(cajas[j].r)) malos.Add($"{m.nombre}: {cajas[i].t.name} se encima con {cajas[j].t.name}");
        }
        Comprobar(malos.Count == 0, $"{estado}: {n} textos, ninguno se desborda ni se encima, el menor de {menor} px (≥ {TiendaMotos.TamMinimo}) y todos dentro de su tarjeta"
                                    + (malos.Count > 0 ? ":\n      " + string.Join("\n      ", malos) : ""));
    }

    // el rectángulo en pantalla de lo que el texto ocupa (su caja preferida, centrada como su alineación)
    static Rect Tinta(TMP_Text t)
    {
        var b = t.textBounds;
        Vector2 a = t.rectTransform.TransformPoint(b.min), c = t.rectTransform.TransformPoint(b.max);
        return Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y), Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y));
    }

    static Rect EnPantalla(RectTransform rt)
    {
        var e = new Vector3[4];
        rt.GetWorldCorners(e);
        return Rect.MinMaxRect(e[0].x, e[0].y, e[2].x, e[2].y);
    }

    // ------------------------------------------------------------------ ayudantes

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
        var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.gameObject.scene == SceneManager.GetActiveScene())
            .OrderByDescending(c => c.name == "Canvas").First();
        var rt = new RenderTexture(1920, 1080, 24);
        var modo = canvas.renderMode;
        var camCanvas = canvas.worldCamera;
        int capa = canvas.sortingLayerID, orden = canvas.sortingOrder;
        float distancia = canvas.planeDistance;
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
            canvas.worldCamera = camCanvas;
            canvas.sortingLayerID = capa;
            canvas.sortingOrder = orden;
            canvas.planeDistance = distancia;
            rt.Release();
        }
        Log($"  captura 1920x1080: {ruta}");
    }
}
