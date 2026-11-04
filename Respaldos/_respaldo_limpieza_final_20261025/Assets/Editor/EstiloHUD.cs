using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// El HUD de los niveles (SampleScene, Nivel2_Comercial, Nivel3_Lluvia) con el estilo de BotonBrenn: fondo #2A1F40 al 90% con
// borde rosa (los 9-slices hud_* de MenuLetreros, a x1) y textos en Press Start.
//  - CajaHUD: hud_caja (borde 3). PlacaPedidos sin píldora, "Pedidos 0/0" crema de 26. PlacaReloj: hud_reloj_verde /
//    _amarilla / _roja (el borde dice el estado), el tiempo crema de 30. FilaQuejas: hud_queja_vacia (círculo con borde
//    crema; la queja recibida sigue siendo la carita roja). PlacaTurbo: hud_marco (borde 2).
//  - MedidorSatisfaccion: un Marco hud_marco (borde 2) detrás, 12 de aire alrededor; las caritas no se tocan.
//  - BotonPausa: 72x72, BotonBrenn Icono, solo dos barras crema de 10x32 separadas 12 (doradas en hover), a 24 del borde
//    derecho y del de arriba.
//  - PlacaDinero: hud_caja 280x72, la moneda a la izquierda y "$0" dorado de 30, 16 a la izquierda del botón de pausa.
//  - Todo a 24 o más del borde de la pantalla.
// Idempotente. Lo llaman ConstruirBotones (en cada escena de juego) y ConstruirCajaHUD.Acomodar (que llaman al terminar
// ConstruirInterfaz, ConstruirReloj, ConstruirQuejas y ConstruirTurbos), así un constructor viejo no regresa el estilo viejo.
public static class EstiloHUD
{
    public const float Margen = 24, LadoPausa = 72, AnchoBarra = 10, AltoBarra = 32, SeparacionBarras = 12;
    public const float SeparacionDinero = 16, AnchoDinero = 280, AltoDinero = 72, LadoMoneda = 36, MargenDinero = 16;
    public const float AireMedidor = 12, TamPedidos = 26, TamDinero = 30, TamReloj = 30;
    public const string TextoDineroMaximo = "$99999", TextoPedidosMaximo = "Pedidos 10/10", TextoRelojMaximo = "9:59";
    // lo que no puede salirse de la pantalla ni encimarse
    public static readonly string[] Piezas = { "CajaHUD", "MedidorSatisfaccion/Marco", "PlacaDinero", "BotonPausa" };

    static Sprite S(string n) => ConstruirMenuPrincipal.Letrero(n);

    public static bool EsHUD(Canvas c) => c != null && c.isRootCanvas && ConstruirCajaHUD.EsCanvasHUD(c);

    public static List<string> Vestir(Canvas canvas)
    {
        var cambios = new List<string>();
        if (!EsHUD(canvas)) return cambios;
        var e = ConstruirBotones.Estilo();
        var raiz = canvas.transform;
        float referencia = canvas.rootCanvas.referencePixelsPerUnit;

        // ---- la caja de la izquierda
        var caja = raiz.Find(ConstruirCajaHUD.Nombre);
        if (caja != null && caja.TryGetComponent(out Image fondoCaja)) Rebanada(fondoCaja, S("hud_caja"), referencia);
        var pedidos = ConstruirCajaHUD.Buscar(raiz, "PlacaPedidos") as RectTransform;
        if (pedidos != null)
        {
            if (pedidos.TryGetComponent(out Image pildora)) pildora.enabled = false;   // el título va directo sobre la caja
            pedidos.sizeDelta = new Vector2(pedidos.sizeDelta.x, 40);
            var t = pedidos.Find("TextoPedidos")?.GetComponent<TextMeshProUGUI>();
            if (t != null)
            {
                Texto(t, e, TamPedidos, e.crema);
                t.alignment = TextAlignmentOptions.Left;
                Estirar(t.rectTransform, 4, 0, 4, 0);
            }
            cambios.Add("PlacaPedidos: título crema de 26 sobre la caja oscura");
        }
        var reloj = ConstruirCajaHUD.Buscar(raiz, "PlacaReloj");
        if (reloj != null)
        {
            if (reloj.TryGetComponent(out Image placa)) Rebanada(placa, S("hud_reloj_verde"), referencia);
            if (reloj.TryGetComponent(out RelojPedido rp))
            {
                var so = new SerializedObject(rp);
                so.FindProperty("placaVerde").objectReferenceValue = S("hud_reloj_verde");
                so.FindProperty("placaAmarilla").objectReferenceValue = S("hud_reloj_amarilla");
                so.FindProperty("placaRoja").objectReferenceValue = S("hud_reloj_roja");
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var t = reloj.Find("TextoReloj")?.GetComponent<TextMeshProUGUI>();
            if (t != null) Texto(t, e, TamReloj, e.crema);
            cambios.Add("PlacaReloj: oscura con el borde verde / dorado / rojo según el tiempo");
        }
        var quejas = ConstruirCajaHUD.Buscar(raiz, "FilaQuejas");
        var vacia = S("hud_queja_vacia");
        if (quejas != null)
            foreach (Transform q in quejas)
                if (q.TryGetComponent(out Image img) && (img.sprite == null || img.sprite.name.EndsWith("queja_vacia"))) img.sprite = vacia;
        foreach (var ui in canvas.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UIManager>(true)))
        {
            var so = new SerializedObject(ui);
            so.FindProperty("quejaVacia").objectReferenceValue = vacia;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var turbo = ConstruirCajaHUD.Buscar(raiz, "PlacaTurbo");
        if (turbo != null && turbo.TryGetComponent(out Image placaTurbo)) Rebanada(placaTurbo, S("hud_marco"), referencia);
        if (caja is RectTransform rc) LayoutRebuilder.ForceRebuildLayoutImmediate(rc);

        // ---- satisfacción: el marco detrás de la barra y las caritas
        var medidor = raiz.Find("MedidorSatisfaccion") as RectTransform;
        if (medidor != null)
        {
            medidor.anchoredPosition = new Vector2(medidor.anchoredPosition.x, -(Margen + AireMedidor));
            var marco = Hijo(medidor, "Marco");
            marco.SetAsFirstSibling();
            Estirar(marco, -AireMedidor, -AireMedidor, -AireMedidor, -AireMedidor);
            Rebanada(Obtener<Image>(marco.gameObject), S("hud_marco"), referencia);
            cambios.Add("MedidorSatisfaccion: marco oscuro con borde rosa de 2 px");
        }

        // ---- el botón de pausa: arriba a la derecha, solo las dos barras
        var pausa = raiz.Find("BotonPausa") as RectTransform;
        if (pausa != null && pausa.TryGetComponent(out Button boton))
        {
            pausa.anchorMin = pausa.anchorMax = pausa.pivot = Vector2.one;
            pausa.anchoredPosition = new Vector2(-Margen, -Margen);
            pausa.localScale = Vector3.one;
            var bb = BotonBrenn.Vestir(boton, BotonBrenn.Variante.Icono, e, Vector2.one * LadoPausa);
            foreach (var ajeno in pausa.Cast<Transform>().Where(h => h.name != "Barra1" && h.name != "Barra2").ToList())
                Object.DestroyImmediate(ajeno.gameObject);
            var barras = new Graphic[2];
            for (int i = 0; i < 2; i++)
            {
                var b = Hijo(pausa, "Barra" + (i + 1));
                b.anchorMin = b.anchorMax = b.pivot = new Vector2(0.5f, 0.5f);
                b.sizeDelta = new Vector2(AnchoBarra, AltoBarra);
                b.anchoredPosition = new Vector2((i == 0 ? -1 : 1) * (SeparacionBarras + AnchoBarra) / 2, 0);
                var img = Obtener<Image>(b.gameObject);
                img.sprite = null;
                img.color = e.crema;
                img.raycastTarget = false;
                barras[i] = img;
            }
            bb.PonerGlifos(barras);
            EditorUtility.SetDirty(bb);
            cambios.Add("BotonPausa: 72x72 a 24 de las orillas, dos barras crema de 10x32 (doradas en hover)");
        }

        // ---- el dinero, a la izquierda del botón de pausa
        var dinero = raiz.Find("PlacaDinero") as RectTransform;
        if (dinero != null)
        {
            dinero.anchorMin = dinero.anchorMax = dinero.pivot = Vector2.one;
            dinero.anchoredPosition = new Vector2(-(Margen + LadoPausa + SeparacionDinero), -Margen);
            dinero.sizeDelta = new Vector2(AnchoDinero, AltoDinero);
            Rebanada(Obtener<Image>(dinero.gameObject), S("hud_caja"), referencia);
            var moneda = Hijo(dinero, "Moneda");
            moneda.anchorMin = moneda.anchorMax = moneda.pivot = new Vector2(0, 0.5f);
            moneda.anchoredPosition = new Vector2(MargenDinero, 0);
            moneda.sizeDelta = Vector2.one * LadoMoneda;
            var imgMoneda = Obtener<Image>(moneda.gameObject);
            imgMoneda.sprite = S("moneda");
            imgMoneda.type = Image.Type.Simple;
            imgMoneda.color = Color.white;
            imgMoneda.raycastTarget = false;
            var t = dinero.Find("TextoDinero")?.GetComponent<TextMeshProUGUI>();
            if (t != null)
            {
                t.transform.SetAsLastSibling();
                Texto(t, e, TamDinero, e.dorado);
                Estirar(t.rectTransform, MargenDinero + LadoMoneda + 8, 0, MargenDinero, 0);
            }
            cambios.Add("PlacaDinero: oscura 280x72 con la moneda y el texto dorado de 30, 16 a la izquierda de la pausa");
        }

        // con el HUD, debajo de los paneles; la pausa al final del HUD (nada la tapa)
        var panel = raiz.Find("PanelPausa") ?? raiz.Find("PanelFinal");
        if (pausa != null && panel != null)
        {
            int indice = panel.GetSiblingIndex();
            foreach (Transform h in raiz)
                if (h.name.StartsWith("Panel") && h.GetSiblingIndex() < indice) indice = h.GetSiblingIndex();
            pausa.SetSiblingIndex(pausa.GetSiblingIndex() < indice ? indice - 1 : indice);
        }
        EditorUtility.SetDirty(canvas.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        return cambios;
    }

    public static IEnumerable<string> Verificar(Canvas canvas)
    {
        if (!EsHUD(canvas)) yield break;
        var raiz = canvas.transform;
        Canvas.ForceUpdateCanvases();
        if (raiz.Find(ConstruirCajaHUD.Nombre) is RectTransform caja) LayoutRebuilder.ForceRebuildLayoutImmediate(caja);
        foreach (var p in Problemas(canvas)) yield return p;

        // estilo
        foreach (var (ruta, sprite) in new[] { ("CajaHUD", "hud_caja"), ("CajaHUD/PlacaReloj", "hud_reloj_verde"), ("CajaHUD/PlacaTurbo", "hud_marco"),
                                                ("MedidorSatisfaccion/Marco", "hud_marco"), ("PlacaDinero", "hud_caja"), ("PlacaDinero/Moneda", "moneda") })
        {
            var img = raiz.Find(ruta)?.GetComponent<Image>();
            if (img == null) { yield return $"Falta {ruta}."; continue; }
            if (img.sprite == null || img.sprite.name != sprite) yield return $"{ruta}: no es {sprite} ({img.sprite?.name}).";
            else if (sprite.StartsWith("hud_") && (img.type != Image.Type.Sliced || Mathf.Abs(img.pixelsPerUnitMultiplier - canvas.referencePixelsPerUnit / img.sprite.pixelsPerUnit) > 0.001f))
                yield return $"{ruta}: el 9-slice no está a x1.";
        }
        foreach (var (ruta, tam, color, maximo) in new[] { ("CajaHUD/PlacaPedidos/TextoPedidos", TamPedidos, "FFF1D0", TextoPedidosMaximo),
                                                           ("CajaHUD/PlacaReloj/TextoReloj", TamReloj, "FFF1D0", TextoRelojMaximo),
                                                           ("PlacaDinero/TextoDinero", TamDinero, "FFD166", TextoDineroMaximo) })
        {
            var t = raiz.Find(ruta)?.GetComponent<TMP_Text>();
            if (t == null) { yield return $"Falta {ruta}."; continue; }
            if (!Mathf.Approximately(t.fontSize, tam) || t.enableAutoSizing || ColorUtility.ToHtmlStringRGB(t.color) != color || !FuenteProyecto.EsTitulo(t.font))
                yield return $"{ruta}: tiene que ir en Press Start de {tam} y #{color}.";
            if (FuenteProyecto.Ancho(t, maximo) > t.rectTransform.rect.width + 0.5f) yield return $"{ruta}: \"{maximo}\" no cabe.";
        }
        var pedidos = raiz.Find("CajaHUD/PlacaPedidos")?.GetComponent<Image>();
        if (pedidos != null && pedidos.enabled) yield return "PlacaPedidos: todavía con la píldora rosa.";
        var quejas = raiz.Find("CajaHUD/FilaQuejas");
        if (quejas != null)
            foreach (Transform q in quejas)
                if (q.GetComponent<Image>()?.sprite?.name != "hud_queja_vacia") yield return $"{q.name}: no empieza con hud_queja_vacia.";
        var reloj = raiz.Find("CajaHUD/PlacaReloj")?.GetComponent<RelojPedido>();
        if (reloj != null)
        {
            var so = new SerializedObject(reloj);
            if ((so.FindProperty("placaRoja").objectReferenceValue as Sprite)?.name != "hud_reloj_roja") yield return "RelojPedido: sin las placas hud_reloj_*.";
        }

        // el botón de pausa
        var pausa = raiz.Find("BotonPausa") as RectTransform;
        var bb = pausa != null ? pausa.GetComponent<BotonBrenn>() : null;
        if (bb == null || bb.VarianteDelBoton != BotonBrenn.Variante.Icono) { yield return "BotonPausa: sin BotonBrenn Icono."; yield break; }
        if (pausa.rect.size != Vector2.one * LadoPausa) yield return $"BotonPausa: mide {pausa.rect.size}, no {LadoPausa}x{LadoPausa}.";
        var hijos = pausa.Cast<Transform>().Select(h => h.name).ToList();
        if (!hijos.OrderBy(n => n).SequenceEqual(new[] { "Barra1", "Barra2" })) yield return $"BotonPausa: tiene que tener solo Barra1 y Barra2 ({string.Join(", ", hijos)}).";
        else
        {
            var b1 = (RectTransform)pausa.Find("Barra1");
            var b2 = (RectTransform)pausa.Find("Barra2");
            if (b1.rect.size != new Vector2(AnchoBarra, AltoBarra) || b2.rect.size != new Vector2(AnchoBarra, AltoBarra)) yield return "Las barras de pausa no miden 10x32.";
            float hueco = (b2.anchoredPosition.x - AnchoBarra / 2) - (b1.anchoredPosition.x + AnchoBarra / 2);
            if (!Mathf.Approximately(hueco, SeparacionBarras) || !Mathf.Approximately(b1.anchoredPosition.x + b2.anchoredPosition.x, 0) || b1.anchoredPosition.y != 0 || b2.anchoredPosition.y != 0)
                yield return $"Las barras de pausa no están centradas con {SeparacionBarras} de separación ({hueco}).";
            if (bb.Glifos == null || bb.Glifos.Length != 2) yield return "BotonPausa: las barras no cambian de color con el hover.";
            foreach (var g in new[] { b1, b2 })
                if (ColorUtility.ToHtmlStringRGB(g.GetComponent<Image>().color) != "FFF1D0") yield return $"{g.name}: no es crema #FFF1D0.";
        }
        var limite = ((RectTransform)raiz).rect;
        var rp = EnCanvas(pausa, canvas);
        if (Mathf.Abs(limite.xMax - rp.xMax - Margen) > 0.5f || Mathf.Abs(limite.yMax - rp.yMax - Margen) > 0.5f)
            yield return $"BotonPausa: tiene que quedar a {Margen} del borde derecho y del de arriba ({limite.xMax - rp.xMax:0}, {limite.yMax - rp.yMax:0}).";
        var dinero = raiz.Find("PlacaDinero") as RectTransform;
        if (dinero != null && Mathf.Abs(rp.xMin - EnCanvas(dinero, canvas).xMax - SeparacionDinero) > 0.5f)
            yield return $"PlacaDinero: tiene que quedar a {SeparacionDinero} a la izquierda de la pausa.";
    }

    // Lo que se puede revisar también al jugar: las piezas dentro de la pantalla con su margen y sin encimarse
    public static List<string> Problemas(Canvas canvas)
    {
        var errores = new List<string>();
        var raiz = canvas.transform;
        var limite = ((RectTransform)raiz).rect;
        var rects = new List<(string, Rect)>();
        foreach (var n in Piezas)
        {
            var rt = raiz.Find(n) as RectTransform;
            if (rt == null) { errores.Add($"Falta Canvas/{n}."); continue; }
            if (!rt.gameObject.activeInHierarchy) continue;
            var r = EnCanvas(rt, canvas);
            rects.Add((n, r));
            if (r.xMin < limite.xMin + Margen - 0.5f || r.xMax > limite.xMax - Margen + 0.5f || r.yMin < limite.yMin + Margen - 0.5f || r.yMax > limite.yMax - Margen + 0.5f)
                errores.Add($"{n}: a menos de {Margen} del borde o fuera de la pantalla ({Texto(r)} en {Texto(limite)}).");
        }
        // el turbo, si está visible, dentro de su caja
        var turbo = raiz.Find("CajaHUD/PlacaTurbo") as RectTransform;
        if (turbo != null && turbo.gameObject.activeInHierarchy && turbo.GetComponent<LayoutElement>() is LayoutElement le && !le.ignoreLayout)
        {
            var r = EnCanvas(turbo, canvas);
            if (r.yMin < limite.yMin + Margen - 0.5f) errores.Add("PlacaTurbo: se sale por abajo.");
        }
        for (int i = 0; i < rects.Count; i++)
            for (int j = i + 1; j < rects.Count; j++)
                if (rects[i].Item2.Overlaps(rects[j].Item2)) errores.Add($"{rects[i].Item1} y {rects[j].Item1} se enciman.");
        return errores;
    }

    public static string Texto(Rect r) => $"({r.xMin:0},{r.yMin:0})-({r.xMax:0},{r.yMax:0})";

    public static Rect EnCanvas(RectTransform rt, Canvas canvas)
    {
        var esquinas = new Vector3[4];
        rt.GetWorldCorners(esquinas);
        var c = canvas.transform;
        Vector2 a = c.InverseTransformPoint(esquinas[0]), b = c.InverseTransformPoint(esquinas[2]);
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }

    // ------------------------------------------------------------------ ayudantes

    static void Rebanada(Image img, Sprite s, float referencia)
    {
        img.enabled = true;
        img.sprite = s;
        img.type = Image.Type.Sliced;
        img.fillCenter = true;
        img.pixelsPerUnitMultiplier = referencia / s.pixelsPerUnit;
        img.color = Color.white;
        img.raycastTarget = false;
    }

    static void Texto(TMP_Text t, EstiloBrenn e, float tam, Color color)
    {
        if (e.fuenteTitulos != null)
        {
            t.font = e.fuenteTitulos;
            t.fontSharedMaterial = e.fuenteTitulos.material;
        }
        t.enableAutoSizing = false;
        t.fontSize = t.fontSizeMin = t.fontSizeMax = tam;
        t.fontStyle = FontStyles.Normal;
        t.color = color;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
    }

    static void Estirar(RectTransform rt, float izquierda, float abajo, float derecha, float arriba)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(izquierda, abajo);
        rt.offsetMax = new Vector2(-derecha, -arriba);
    }

    static RectTransform Hijo(Transform padre, string nombre)
    {
        var t = padre.Find(nombre);
        if (t == null)
        {
            t = new GameObject(nombre, typeof(RectTransform)).transform;
            t.gameObject.layer = padre.gameObject.layer;
            t.SetParent(padre, false);
        }
        t.localScale = Vector3.one;
        return (RectTransform)t;
    }

    static T Obtener<T>(GameObject g) where T : Component => g.TryGetComponent(out T c) ? c : g.AddComponent<T>();
}
