using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Un estilo único para todos los botones del juego (BotonBrenn, arte de pixel_town/generate_menu_letreros.py):
//  - Estilo(): el asset Assets/Resources/EstiloBrenn.asset con los 9-slices, la tijera, el panel y la fuente.
//  - Los prefabs Assets/Prefabs/BotonBrenn.prefab y sus variantes BotonBrenn_Normal, _Principal, _Peligro e _Icono.
//  - Recorre las escenas del build (OrdenEscenas) y viste TODOS los botones conservando nombre, onClick, referencias y
//    posición: dentro del juego 360x72 con texto de 28 (más ancho si el texto no cabe; los de un mismo panel, parejos),
//    "Jugar"/"Confirmar" Principal de 520x108 con 38, "Reiniciar progreso" Peligro, el botón de pausa del HUD un icono de
//    64 y las tarjetas de nivel con su marco. Los paneles con el marco crema (pausa, nivel completado, confirmaciones)
//    pasan al #2A1F40 con borde rosa de 3 px y sus textos oscuros a crema. El menú principal lo arma ConstruirMenuPrincipal.
//  - También ajusta lo que pinta botones al jugar: LevelManager (el botón resaltado) y TiendaMotos (su ventana).
//    CONFIRMAR de la selección de repartidor queda de 92 de alto (ConstruirSeleccion); sus tarjetas no son botones.
// Idempotente: correrlo otra vez deja todo igual. Si una escena se reconstruye con su constructor viejo (ConstruirPausa,
// ConstruirSeleccion...), hay que volver a correrlo. Deja la lista de botones cambiados por escena.
// Temp/ConstruirBotones.pedido → Temp/ConstruirBotones.resultado
public static class ConstruirBotones
{
    const string RutaEstilo = "Assets/Resources/EstiloBrenn.asset";
    const string CarpetaPrefabs = "Assets/Prefabs/";
    public const float AnchoJuego = 360, AltoJuego = 72, TamJuego = 28;
    public const float LadoChico = 64;
    const float MargenTexto = 12;   // por lado (más los 12 del área del texto): que no quede apretado
    static readonly Vector2 TamPrincipal = new Vector2(ConstruirMenuPrincipal.AnchoPrincipal, ConstruirMenuPrincipal.AltoPrincipal);

    // sprites viejos que ya no deben quedar en ningún botón ni panel
    static readonly string[] Viejos = { "boton_pastel", "boton_pill", "boton_redondo", "placa_start", "seleccion_pestana", "marco_panel" };
    // textos que un botón muestra al jugar (también tienen que caber)
    static readonly Dictionary<string, string[]> TextosAlJugar = new Dictionary<string, string[]>
    {
        { "BotonSiguiente", new[] { "Siguiente nivel", "Continuar" } },
        { "BotonMusica", new[] { BotonSonido.Etiqueta(BotonSonido.Canal.Musica, true), BotonSonido.Etiqueta(BotonSonido.Canal.Musica, false) } },
        { "BotonEfectos", new[] { BotonSonido.Etiqueta(BotonSonido.Canal.Efectos, true), BotonSonido.Etiqueta(BotonSonido.Canal.Efectos, false) } },
    };

    [MenuItem("Delivery Brenn's/Aplicar estilo de botones (todas las escenas)")]
    static void Menu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Debug.Log(Aplicar(out _));
    }

    // ------------------------------------------------------------------ estilo y prefabs

    internal static EstiloBrenn Estilo()
    {
        var e = AssetDatabase.LoadAssetAtPath<EstiloBrenn>(RutaEstilo);
        if (e == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RutaEstilo));
            e = ScriptableObject.CreateInstance<EstiloBrenn>();
            AssetDatabase.CreateAsset(e, RutaEstilo);
        }
        Sprite L(string n) => ConstruirMenuPrincipal.Letrero(n);
        e.normal = L("boton_normal"); e.normalElegido = L("boton_normal_hover");
        e.principal = L("boton_principal"); e.principalElegido = L("boton_principal_hover");
        e.peligro = L("boton_peligro"); e.peligroElegido = L("boton_peligro_hover");
        e.icono = L("marco_icono"); e.iconoElegido = L("marco_icono_hover");
        e.tarjeta = L("marco_tarjeta"); e.tarjetaElegida = L("marco_tarjeta_hover");
        e.tijera = L("tijera");
        e.panel = L("panel");
        e.raya = L("raya_apagado");
        e.fuenteTitulos = FuenteProyecto.FuenteTitulo();
        EditorUtility.SetDirty(e);
        AssetDatabase.SaveAssetIfDirty(e);
        return e;
    }

    static List<string> CrearPrefabs(EstiloBrenn e)
    {
        var hechos = new List<string>();
        Directory.CreateDirectory(CarpetaPrefabs);
        // la base: Normal de 480x92 con su texto
        var raiz = new GameObject("BotonBrenn", typeof(RectTransform), typeof(Image), typeof(Button));
        try
        {
            var texto = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
            texto.transform.SetParent(raiz.transform, false);
            var tmp = texto.GetComponent<TextMeshProUGUI>();
            tmp.text = "Botón";
            BotonBrenn.Vestir(raiz.GetComponent<Button>(), BotonBrenn.Variante.Normal, e,
                              new Vector2(ConstruirMenuPrincipal.AnchoBoton, ConstruirMenuPrincipal.AltoBoton), ConstruirMenuPrincipal.TamTexto);
            var basePrefab = PrefabUtility.SaveAsPrefabAsset(raiz, CarpetaPrefabs + "BotonBrenn.prefab");
            hechos.Add("BotonBrenn");
            foreach (var (v, tam, tamTexto) in new[]
                     {
                         (BotonBrenn.Variante.Normal, new Vector2(ConstruirMenuPrincipal.AnchoBoton, ConstruirMenuPrincipal.AltoBoton), ConstruirMenuPrincipal.TamTexto),
                         (BotonBrenn.Variante.Principal, TamPrincipal, ConstruirMenuPrincipal.TamPrincipal),
                         (BotonBrenn.Variante.Peligro, new Vector2(ConstruirMenuPrincipal.AnchoBoton, ConstruirMenuPrincipal.AltoBoton), ConstruirMenuPrincipal.TamTexto),
                         (BotonBrenn.Variante.Icono, Vector2.one * ConstruirMenuPrincipal.LadoIcono, 0f),
                     })
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
                try
                {
                    inst.name = "BotonBrenn_" + v;
                    if (v == BotonBrenn.Variante.Icono)
                    {
                        var t = inst.transform.Find("Texto");
                        if (t != null) Object.DestroyImmediate(t.gameObject);
                    }
                    BotonBrenn.Vestir(inst.GetComponent<Button>(), v, e, tam, tamTexto);
                    PrefabUtility.SaveAsPrefabAsset(inst, CarpetaPrefabs + inst.name + ".prefab");   // variante del base
                    hechos.Add(inst.name);
                }
                finally { Object.DestroyImmediate(inst); }
            }
        }
        finally { Object.DestroyImmediate(raiz); }
        return hechos;
    }

    // ------------------------------------------------------------------ todas las escenas

    internal static string Aplicar(out bool ok)
    {
        var lineas = new List<string>();
        var errores = new List<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                ok = false;
                return $"NO SE APLICÓ: {SceneManager.GetSceneAt(i).name} tiene cambios sin guardar (guárdala o descártala y vuelve a correrlo).";
            }
        var estilo = Estilo();
        if (estilo.normal == null || estilo.tijera == null || estilo.panel == null || estilo.fuenteTitulos == null)
        {
            ok = false;
            return "NO SE APLICÓ: falta el arte de pixel_town/generate_menu_letreros.py o la fuente Press Start.";
        }
        lineas.Add("Prefabs: " + string.Join(", ", CrearPrefabs(estilo)));

        string volverA = SceneManager.GetActiveScene().path;
        try
        {
            foreach (var ruta in OrdenEscenas.Rutas.Where(File.Exists))
            {
                var escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
                var cambios = new List<string>();
                var err = new List<string>();
                if (escena.name == "MenuPrincipal") err.AddRange(VerificarEscena(escena, cambios, soloVerificar: true));
                else
                {
                    Vestir(escena, estilo, cambios);
                    err.AddRange(VerificarEscena(escena, null, soloVerificar: false));
                }
                lineas.Add($"{escena.name}: " + (cambios.Count == 0 ? "sin botones" : string.Join("; ", cambios)));
                errores.AddRange(err.Select(x => $"{escena.name}: {x}"));
                if (escena.name != "MenuPrincipal" && err.Count == 0 && !EditorSceneManager.SaveScene(escena))
                    errores.Add($"{escena.name}: no se pudo guardar.");
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(volverA) && File.Exists(volverA) && SceneManager.GetActiveScene().path != volverA)
                EditorSceneManager.OpenScene(volverA, OpenSceneMode.Single);
        }
        ok = errores.Count == 0;
        return "BOTONES CAMBIADOS POR ESCENA\n- " + string.Join("\n- ", lineas) +
               (ok ? "\nVERIFICACIÓN: sin errores." : "\nERRORES (esas escenas no se guardaron):\n- " + string.Join("\n- ", errores));
    }

    // la regla de cada botón: variante, tamaño base (null: el que tiene) y tamaño de texto
    static (BotonBrenn.Variante v, Vector2? tam, float texto, bool parejo) Regla(string escena, Button b)
    {
        string n = b.name;
        if (escena == "SeleccionNivel" && n.StartsWith("Tarjeta")) return (BotonBrenn.Variante.Tarjeta, null, 0, false);
        if (n == "BotonPausa") return (BotonBrenn.Variante.Icono, Vector2.one * EstiloHUD.LadoPausa, 0, false);   // EstiloHUD lo termina
        if (escena == "Seleccion" && n == "Confirmar")
            return (BotonBrenn.Variante.Principal, new Vector2(ConstruirMenuPrincipal.AnchoPrincipal, ConstruirSeleccion.AltoConfirmar), ConstruirMenuPrincipal.TamPrincipal, false);
        if (n == "Jugar" || n == "Confirmar") return (BotonBrenn.Variante.Principal, TamPrincipal, ConstruirMenuPrincipal.TamPrincipal, false);
        if (n == "BotonReiniciar") return (BotonBrenn.Variante.Peligro, new Vector2(AnchoJuego, AltoJuego), TamJuego, false);
        if (n.StartsWith("Pestana")) return (BotonBrenn.Variante.Normal, null, TamJuego, true);
        return (BotonBrenn.Variante.Normal, new Vector2(AnchoJuego, AltoJuego), TamJuego, true);
    }

    static void Vestir(Scene escena, EstiloBrenn e, List<string> cambios)
    {
        var raices = escena.GetRootGameObjects();
        foreach (var canvas in raices.SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).Where(c => c.isRootCanvas))
        {
            var escalador = canvas.GetComponent<CanvasScaler>();
            if (escalador != null && (escalador.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize || escalador.referenceResolution != new Vector2(1920, 1080)))
            {
                escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                escalador.referenceResolution = new Vector2(1920, 1080);
                escalador.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                escalador.matchWidthOrHeight = 0.5f;
                cambios.Add($"{canvas.name}: Canvas Scaler a 1920x1080");
            }
        }

        // los paneles crema → oscuros con borde rosa, textos oscuros a crema
        foreach (var img in raices.SelectMany(g => g.GetComponentsInChildren<Image>(true)).Where(i => i.sprite != null && i.sprite.name == "marco_panel").ToList())
        {
            img.sprite = e.panel;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = Referencia(img) / e.panel.pixelsPerUnit;
            img.color = Color.white;
            cambios.Add($"panel {Camino(img.transform)}");
        }
        foreach (var panel in raices.SelectMany(g => g.GetComponentsInChildren<Image>(true)).Where(i => i.sprite == e.panel))
            foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(true).Where(t => t.GetComponentInParent<Button>(true) == null))
            {
                var c = (Color32)tmp.color;
                if (tmp.name.Contains("Titulo") || Igual(c, e.oscuro)) tmp.color = new Color(e.crema.r, e.crema.g, e.crema.b, tmp.color.a);
                else if (Igual(c, new Color32(0x8C, 0x52, 0xFF, 255))) tmp.color = e.rosa;   // el morado no se lee sobre el oscuro
            }

        // los botones
        var botones = raices.SelectMany(g => g.GetComponentsInChildren<Button>(true)).ToList();
        var anchos = new Dictionary<Button, float>();
        foreach (var b in botones)
        {
            var (v, tam, tamTexto, _) = Regla(escena.name, b);
            var bb = BotonBrenn.Vestir(b, v, e, tam, tamTexto);
            if (bb.Texto != null) anchos[b] = AnchoNecesario(bb.Texto, b.name);
            var sonido = b.GetComponent<BotonSonido>();
            if (sonido != null)
            {
                var so = new SerializedObject(sonido);
                so.FindProperty("fondo").objectReferenceValue = bb.Borde;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        // los de un mismo panel, del mismo ancho (el que pida el texto más largo, mínimo 360)
        foreach (var grupo in botones.Where(b => Regla(escena.name, b).parejo).GroupBy(b => b.transform.parent))
        {
            float ancho = grupo.Max(b => Mathf.Max(((RectTransform)b.transform).sizeDelta.x, anchos.TryGetValue(b, out var a) ? a : 0));
            if (grupo.Any(b => b.name.StartsWith("Pestana"))) ancho = grupo.Max(b => ((RectTransform)b.transform).sizeDelta.x);
            foreach (var b in grupo)
            {
                var rt = (RectTransform)b.transform;
                rt.sizeDelta = new Vector2(Mathf.Max(ancho, anchos.TryGetValue(b, out var a) ? a : 0), rt.sizeDelta.y);
            }
        }
        foreach (var b in botones.Where(b => Regla(escena.name, b).v == BotonBrenn.Variante.Peligro || Regla(escena.name, b).v == BotonBrenn.Variante.Principal))
        {
            var rt = (RectTransform)b.transform;
            if (anchos.TryGetValue(b, out var a) && a > rt.sizeDelta.x) rt.sizeDelta = new Vector2(a, rt.sizeDelta.y);
        }
        // filas de botones hermanos que ahora se enciman: se reparten con 24 de separación alrededor del mismo centro
        foreach (var fila in botones.Where(b => b.GetComponentInParent<LayoutGroup>(true) == null && Regla(escena.name, b).v != BotonBrenn.Variante.Tarjeta)
                                    .GroupBy(b => (b.transform.parent, Mathf.Round(((RectTransform)b.transform).anchoredPosition.y))))
        {
            var orden = fila.Select(b => (RectTransform)b.transform).OrderBy(r => r.anchoredPosition.x).ToList();
            bool enciman = orden.Zip(orden.Skip(1), (a, b) => b.anchoredPosition.x - a.anchoredPosition.x < (a.sizeDelta.x + b.sizeDelta.x) / 2 + 24).Any(x => x);
            if (orden.Count < 2 || !enciman) continue;
            float total = orden.Sum(r => r.sizeDelta.x) + 24 * (orden.Count - 1);
            float x = orden.Average(r => r.anchoredPosition.x) - total / 2;
            foreach (var r in orden)
            {
                r.anchoredPosition = new Vector2(x + r.sizeDelta.x / 2, r.anchoredPosition.y);
                x += r.sizeDelta.x + 24;
            }
            cambios.Add("repartidos sin encimarse: " + string.Join(" y ", orden.Select(r => r.name)));
        }
        // lo que al crecer se salió del Canvas, de vuelta adentro
        foreach (var b in botones)
        {
            var canvas = b.GetComponentInParent<Canvas>(true)?.rootCanvas;
            if (canvas == null || b.GetComponentInParent<LayoutGroup>(true) != null) continue;
            var rt = (RectTransform)b.transform;
            var r = EnCanvas(rt, canvas);
            var limite = ((RectTransform)canvas.transform).rect;
            float dx = Mathf.Max(0, limite.xMin - r.xMin) - Mathf.Max(0, r.xMax - limite.xMax);
            float dy = Mathf.Max(0, limite.yMin - r.yMin) - Mathf.Max(0, r.yMax - limite.yMax);
            if (dx == 0 && dy == 0) continue;
            float escala = canvas.transform.lossyScale.x / rt.parent.lossyScale.x;
            rt.anchoredPosition += new Vector2(Mathf.Ceil(Mathf.Abs(dx)) * Mathf.Sign(dx), Mathf.Ceil(Mathf.Abs(dy)) * Mathf.Sign(dy)) * escala;
            cambios.Add($"{b.name} recorrido {dx:0},{dy:0} para no salirse");
        }
        // la tijera dentro del Canvas: si a la izquierda se saldría, va a la derecha
        foreach (var b in botones)
        {
            var t = b.transform.Find("Tijera") as RectTransform;
            if (t == null) continue;
            var canvas = b.GetComponentInParent<Canvas>(true)?.rootCanvas;
            if (canvas == null) continue;
            var r = EnCanvas(t, canvas);
            var limite = ((RectTransform)canvas.transform).rect;
            if (r.xMin < limite.xMin)
            {
                t.anchorMin = t.anchorMax = new Vector2(1f, 0.5f);
                t.pivot = new Vector2(0f, 0.5f);
                t.anchoredPosition = new Vector2(BotonBrenn.SeparacionTijera, 0f);
            }
        }
        foreach (var b in botones)
        {
            var rt = (RectTransform)b.transform;
            cambios.Add($"{b.name} ({b.GetComponent<BotonBrenn>().VarianteDelBoton} {rt.sizeDelta.x:0}x{rt.sizeDelta.y:0})");
        }

        // lo que pinta botones al jugar
        foreach (var lm in raices.SelectMany(g => g.GetComponentsInChildren<LevelManager>(true)))
        {
            var so = new SerializedObject(lm);
            so.FindProperty("pastillaResaltada").objectReferenceValue = e.principal;
            so.FindProperty("pastillaResaltadaPresionada").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (var tienda in raices.SelectMany(g => g.GetComponentsInChildren<TiendaMotos>(true)))
        {
            var so = new SerializedObject(tienda);
            float referencia = Referencia(tienda);
            so.FindProperty("marcoPanel").objectReferenceValue = e.panel;
            so.FindProperty("multiplicadorMarco").floatValue = referencia / e.panel.pixelsPerUnit;
            so.FindProperty("pildora").objectReferenceValue = e.normal;
            so.FindProperty("multiplicadorPildora").floatValue = referencia / e.normal.pixelsPerUnit;
            so.ApplyModifiedPropertiesWithoutUndo();
            cambios.Add("TiendaMotos: ventana y botones (Comprar/En uso, Ir a pie, Cerrar, EMPEZAR NIVEL) al jugar");
        }
        // el HUD de los niveles (placas, satisfacción, dinero y el botón de pausa)
        foreach (var canvas in raices.Select(g => g.GetComponent<Canvas>()).Where(EstiloHUD.EsHUD))
            cambios.AddRange(EstiloHUD.Vestir(canvas));
        EditorSceneManager.MarkSceneDirty(escena);
    }

    // el ancho que pide el texto (y los que muestra al jugar) con su margen, en múltiplos de 8
    static float AnchoNecesario(TMP_Text tmp, string boton)
    {
        var textos = new List<string> { tmp.text };
        if (TextosAlJugar.TryGetValue(boton, out var otros)) textos.AddRange(otros);
        float ancho = textos.Max(t => FuenteProyecto.Ancho(tmp, t)) + 2 * (MargenTexto + 12);
        return Mathf.Ceil(ancho / 8f) * 8f;
    }

    // ------------------------------------------------------------------ verificación

    static IEnumerable<string> VerificarEscena(Scene escena, List<string> lista, bool soloVerificar)
    {
        var raices = escena.GetRootGameObjects();
        var botones = raices.SelectMany(g => g.GetComponentsInChildren<Button>(true)).ToList();
        foreach (var b in botones)
        {
            var bb = b.GetComponent<BotonBrenn>();
            lista?.Add($"{b.name} ({(bb != null ? bb.VarianteDelBoton.ToString() : "SIN ESTILO")})");
            if (bb == null) { yield return $"{Camino(b.transform)}: sin BotonBrenn (estilo viejo)."; continue; }
            if (b.transition != Selectable.Transition.None) yield return $"{Camino(b.transform)}: la transición tiene que ser None.";
            var img = bb.Borde;
            if (img == null || img.sprite == null || !AssetDatabase.GetAssetPath(img.sprite).StartsWith(ConstruirMenuPrincipal.CarpetaLetreros) || img.type != Image.Type.Sliced)
                yield return $"{Camino(b.transform)}: el borde no es un 9-slice de MenuLetreros.";
            var propia = b.GetComponent<Image>();
            if (propia != null && propia.sprite != null && Viejos.Any(v => propia.sprite.name.StartsWith(v)))
                yield return $"{Camino(b.transform)}: todavía tiene el sprite viejo {propia.sprite.name}.";
            if (bb.Texto != null)
            {
                bb.Texto.ForceMeshUpdate();
                var textos = new List<string> { bb.Texto.text };
                if (TextosAlJugar.TryGetValue(b.name, out var otros)) textos.AddRange(otros);
                float disponible = bb.Texto.rectTransform.rect.width;
                foreach (var t in textos)
                    if (disponible > 0 && FuenteProyecto.Ancho(bb.Texto, t) > disponible + 0.5f) yield return $"{Camino(b.transform)}: \"{t}\" no cabe.";
                if (!FuenteProyecto.EsTitulo(bb.Texto.font)) yield return $"{Camino(b.transform)}: el texto no va en Press Start 2P.";
            }
            var canvas = b.GetComponentInParent<Canvas>(true)?.rootCanvas;
            if (canvas != null && b.GetComponentInParent<LayoutGroup>(true) == null)
            {
                var limite = ((RectTransform)canvas.transform).rect;
                var r = EnCanvas((RectTransform)b.transform, canvas);
                if (r.xMin < limite.xMin - 0.5f || r.xMax > limite.xMax + 0.5f || r.yMin < limite.yMin - 0.5f || r.yMax > limite.yMax + 0.5f)
                    yield return $"{Camino(b.transform)}: se sale del Canvas ({r} en {limite}).";
                var t = b.transform.Find("Tijera") as RectTransform;
                if (t != null)
                {
                    var rt = EnCanvas(t, canvas);
                    if (rt.xMin < limite.xMin - 0.5f || rt.xMax > limite.xMax + 0.5f) yield return $"{Camino(b.transform)}: la tijera se sale del Canvas.";
                }
            }
        }
        // botones hermanos sin encimarse
        foreach (var grupo in botones.Where(b => b.GetComponentInParent<LayoutGroup>(true) == null).GroupBy(b => b.transform.parent))
        {
            var lista2 = grupo.ToList();
            for (int i = 0; i < lista2.Count; i++)
                for (int j = i + 1; j < lista2.Count; j++)
                {
                    var c = lista2[i].GetComponentInParent<Canvas>(true)?.rootCanvas;
                    if (c == null || !lista2[i].gameObject.activeSelf || !lista2[j].gameObject.activeSelf) continue;
                    if (EnCanvas((RectTransform)lista2[i].transform, c).Overlaps(EnCanvas((RectTransform)lista2[j].transform, c)))
                        yield return $"{lista2[i].name} y {lista2[j].name} se enciman.";
                }
        }
        foreach (var canvas in raices.Select(g => g.GetComponent<Canvas>()).Where(EstiloHUD.EsHUD))
            foreach (var p in EstiloHUD.Verificar(canvas)) yield return "HUD: " + p;
        if (soloVerificar) yield break;
        foreach (var img in raices.SelectMany(g => g.GetComponentsInChildren<Image>(true)))
            if (img.sprite != null && img.sprite.name == "marco_panel") yield return $"{Camino(img.transform)}: todavía con el marco crema.";
    }

    // ------------------------------------------------------------------ ayudantes

    static bool Igual(Color32 a, Color b)
    {
        var c = (Color32)b;
        return a.r == c.r && a.g == c.g && a.b == c.b;
    }

    static float Referencia(Component c)
    {
        var canvas = c.GetComponentInParent<Canvas>(true);
        return canvas != null ? canvas.rootCanvas.referencePixelsPerUnit : 100f;
    }

    // el rectángulo en coordenadas locales del Canvas raíz (como lo ve el Canvas, sin importar las anclas)
    static Rect EnCanvas(RectTransform rt, Canvas canvas)
    {
        var esquinas = new Vector3[4];
        rt.GetWorldCorners(esquinas);
        var c = canvas.transform;
        Vector2 a = c.InverseTransformPoint(esquinas[0]), b = c.InverseTransformPoint(esquinas[2]);
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }

    static string Camino(Transform t) => t.parent == null ? t.name : Camino(t.parent) + "/" + t.name;

    // ------------------------------------------------------------------ pedido externo

    [InitializeOnLoad]
    static class PedidoExterno
    {
        const string Pedido = "Temp/ConstruirBotones.pedido";
        const string Resultado = "Temp/ConstruirBotones.resultado";

        static PedidoExterno() => EditorApplication.update += Revisar;

        static void Revisar()
        {
            if (!File.Exists(Pedido) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Pedido);
            var consola = new List<string>();
            void Anotar(string msg, string pila, LogType tipo)
            {
                if (tipo != LogType.Log) consola.Add($"[{tipo}] {msg}" + (tipo == LogType.Exception ? "\n" + pila : ""));
            }
            Application.logMessageReceived += Anotar;
            string texto;
            try
            {
                texto = Aplicar(out bool ok);
                texto = (ok ? "OK\n" : "CON ERRORES\n") + texto;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                texto = "ERROR\n" + e;
            }
            finally { Application.logMessageReceived -= Anotar; }
            texto += "\n\nCONSOLA (advertencias y errores): " + (consola.Count == 0 ? "ninguno" : "\n" + string.Join("\n", consola));
            File.WriteAllText(Resultado, texto);
        }
    }
}
