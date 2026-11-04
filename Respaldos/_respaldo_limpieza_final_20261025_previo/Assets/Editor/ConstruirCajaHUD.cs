using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Caja del HUD arriba a la izquierda (Canvas/CajaHUD) en las 3 escenas de juego: junta en una sola columna
// PlacaPedidos (ConstruirInterfaz), PlacaReloj (ConstruirReloj), FilaQuejas (ConstruirQuejas) y PlacaTurbo
// (ConstruirTurbos). Fondo #2A1F40 al 90% con borde rosa #FF6FA5 de 3 px (hud_caja.png de
// pixel_town/generate_menu_letreros.py, 9-slice a x1, el estilo de BotonBrenn), VerticalLayoutGroup con
// relleno 12 y separación 8 y ContentSizeFitter: la caja crece y encoge sola. La placa del turbo lleva un LayoutElement
// que TurboHUD saca del acomodo (ignoreLayout) mientras no hay turbo, así la caja encoge cuando se acaba.
// Las placas se crean en el Canvas y Acomodar las mete en la caja (en ese orden); los demás constructores la llaman al
// terminar. Crear() además quita la vieja píldora de guía (Canvas/GuiaPedido: flecha, ícono del producto y metros).
// Al final de Acomodar, EstiloHUD.Vestir le pone el estilo nuevo a todo el HUD (placas, satisfacción, dinero y pausa),
// así ningún constructor viejo deja el estilo viejo.
// Temp/ConstruirCajaHUD.pedido → Temp/ConstruirCajaHUD.resultado (aplica a las 3 escenas y verifica)
public static class ConstruirCajaHUD
{
    public const string Nombre = "CajaHUD";
    public const string NombreGuiaVieja = "GuiaPedido";
    public static readonly string[] Orden = { "PlacaPedidos", "PlacaReloj", "FilaQuejas", "PlacaTurbo" };
    const string RutaMarco = "Assets/Sprits/MenuLetreros/hud_caja.png";
    static readonly Vector4 BordeMarco = new Vector4(3, 3, 3, 3);
    const int EscalaMarco = 1;                                   // a x1, como los botones BotonBrenn
    public const int Relleno = 12, Separacion = 8;
    public static readonly Vector2 Posicion = new Vector2(24, -24);
    const string NombreUndo = "Caja del HUD";

    static Sprite Marco() =>
        ConstruirMenuPrincipal.ImportarPixel(RutaMarco, BordeMarco)
        ?? throw new Exception($"Falta {RutaMarco} (corre pixel_town/generate_menu_letreros.py).");

    // El Canvas del HUD: el que tiene PlacaPedidos, suelta o ya dentro de la caja
    public static bool EsCanvasHUD(Canvas c) => c != null && Buscar(c.transform, "PlacaPedidos") != null;

    public static Canvas CanvasHUD() =>
        SceneManager.GetActiveScene().GetRootGameObjects().Select(g => g.GetComponent<Canvas>()).FirstOrDefault(EsCanvasHUD);

    // Una placa del HUD izquierdo, esté en la caja o todavía suelta en el Canvas
    public static Transform Buscar(Transform canvas, string nombre) => canvas.Find(Nombre + "/" + nombre) ?? canvas.Find(nombre);

    // Crea la caja si falta y mete dentro, en orden, las placas que ya existan. Devuelve la caja.
    public static RectTransform Acomodar(Canvas canvas)
    {
        var caja = canvas.transform.Find(Nombre) as RectTransform;
        if (caja == null)
        {
            var go = new GameObject(Nombre, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, NombreUndo);
            go.layer = canvas.gameObject.layer;
            go.transform.SetParent(canvas.transform, false);
            caja = (RectTransform)go.transform;
        }
        Undo.RecordObject(caja, NombreUndo);
        caja.anchorMin = caja.anchorMax = caja.pivot = new Vector2(0, 1);
        caja.anchoredPosition = Posicion;
        caja.localScale = Vector3.one;

        var marco = Marco();
        if (!caja.TryGetComponent(out Image fondo)) fondo = Undo.AddComponent<Image>(caja.gameObject);
        fondo.sprite = marco;
        fondo.type = Image.Type.Sliced;
        fondo.fillCenter = true;
        fondo.pixelsPerUnitMultiplier = canvas.referencePixelsPerUnit / (marco.pixelsPerUnit * EscalaMarco);
        fondo.color = Color.white;
        fondo.raycastTarget = false;   // no tapa clics del juego

        if (!caja.TryGetComponent(out VerticalLayoutGroup layout)) layout = Undo.AddComponent<VerticalLayoutGroup>(caja.gameObject);
        layout.padding = new RectOffset(Relleno, Relleno, Relleno, Relleno);
        layout.spacing = Separacion;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = layout.childControlHeight = false;   // cada placa conserva su tamaño
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        layout.childScaleWidth = layout.childScaleHeight = false;        // el pulso del reloj y el rebote de las quejas no la mueven
        if (!caja.TryGetComponent(out ContentSizeFitter ajuste)) ajuste = Undo.AddComponent<ContentSizeFitter>(caja.gameObject);
        ajuste.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        ajuste.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        int indice = 0;
        foreach (var n in Orden)
        {
            var placa = Buscar(canvas.transform, n) as RectTransform;
            if (placa == null) continue;
            if (placa.parent != caja) Undo.SetTransformParent(placa, caja, NombreUndo);
            Undo.RecordObject(placa, NombreUndo);
            placa.anchorMin = placa.anchorMax = placa.pivot = new Vector2(0, 1);
            placa.localScale = Vector3.one;
            placa.SetSiblingIndex(indice++);
        }
        // la placa del turbo entra y sale del acomodo (TurboHUD.Acomodar): empieza fuera, la caja sin hueco
        var turbo = caja.Find("PlacaTurbo");
        if (turbo != null)
        {
            if (!turbo.TryGetComponent(out LayoutElement enCaja)) enCaja = Undo.AddComponent<LayoutElement>(turbo.gameObject);
            enCaja.ignoreLayout = true;
            var hud = turbo.GetComponentInParent<TurboHUD>(true) ?? Object.FindObjectsByType<TurboHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                          .FirstOrDefault(h => h.gameObject.scene == canvas.gameObject.scene);
            if (hud != null)
            {
                var so = new SerializedObject(hud);
                so.FindProperty("enCaja").objectReferenceValue = enCaja;
                so.ApplyModifiedProperties();
            }
        }
        // lo que no sea una placa del HUD izquierdo no va en la caja
        foreach (var ajeno in caja.Cast<Transform>().Where(t => !Orden.Contains(t.name)).ToList())
            Undo.SetTransformParent(ajeno, canvas.transform, NombreUndo);

        // con el HUD, debajo de los paneles (la pausa, el panel final y el despido la tapan)
        var panel = canvas.transform.Find("PanelPausa") ?? canvas.transform.Find("PanelFinal");
        if (panel != null && caja.GetSiblingIndex() > panel.GetSiblingIndex()) caja.SetSiblingIndex(panel.GetSiblingIndex());

        LayoutRebuilder.ForceRebuildLayoutImmediate(caja);
        EstiloHUD.Vestir(canvas);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        return caja;
    }

    // Quita la vieja píldora de guía (flecha, ícono y metros); solo por nombre, ya no se usa el componente
    public static bool QuitarGuiaVieja(Canvas canvas)
    {
        var guia = canvas.transform.Find(NombreGuiaVieja);
        if (guia == null) return false;
        Undo.DestroyObjectImmediate(guia.gameObject);
        return true;
    }

    // En la escena activa: caja con las 4 placas y sin la guía vieja. Devuelve un renglón para el resumen.
    public static string Crear()
    {
        var canvas = CanvasHUD() ?? throw new Exception("No encuentro el Canvas con PlacaPedidos (ejecuta Construir interfaz).");
        bool quitada = QuitarGuiaVieja(canvas);
        var caja = Acomodar(canvas);
        return $"{SceneManager.GetActiveScene().name}: {Nombre} en {Posicion} con [{string.Join(", ", caja.Cast<Transform>().Select(t => t.name))}], " +
               $"{caja.sizeDelta.x:0}x{caja.sizeDelta.y:0}{(quitada ? "; GuiaPedido quitada" : "")}.";
    }

    // ------------------------------------------------------------------ verificación (escena abierta)

    // Rectángulo en una pantalla de 1920x1080 (origen abajo a la izquierda), sin depender del tamaño de la Game view:
    // anclas de un solo punto, hijos directos del Canvas o de la caja
    public static readonly Vector2 Pantalla = new Vector2(1920, 1080);

    public static Rect Rectangulo(RectTransform rt)
    {
        Rect padre = rt.parent is RectTransform p && p.GetComponent<Canvas>() == null ? Rectangulo(p) : new Rect(Vector2.zero, Pantalla);
        Vector2 ancla = padre.min + Vector2.Scale(rt.anchorMin, padre.size);
        Vector2 min = ancla + rt.anchoredPosition - Vector2.Scale(rt.pivot, rt.sizeDelta);
        return new Rect(min, rt.sizeDelta);
    }

    public static List<string> Verificar()
    {
        var errores = new List<string>();
        var canvas = CanvasHUD();
        if (canvas == null) return new List<string> { "no hay Canvas con PlacaPedidos" };
        string escena = SceneManager.GetActiveScene().name;
        if (canvas.transform.Find(NombreGuiaVieja) != null || canvas.GetComponentsInChildren<Transform>(true).Any(t => t.name == NombreGuiaVieja))
            errores.Add($"{escena}: todavía hay un {NombreGuiaVieja} (la píldora de flecha y metros) en el Canvas.");
        var caja = canvas.transform.Find(Nombre) as RectTransform;
        if (caja == null) { errores.Add($"{escena}: falta Canvas/{Nombre}."); return errores; }
        if (caja.anchorMin != new Vector2(0, 1) || caja.anchorMax != new Vector2(0, 1) || caja.pivot != new Vector2(0, 1) || caja.anchoredPosition != Posicion)
            errores.Add($"{Nombre}: debe ir anclada arriba a la izquierda en {Posicion}.");
        var fondo = caja.GetComponent<Image>();
        if (fondo == null || fondo.sprite == null || AssetDatabase.GetAssetPath(fondo.sprite) != RutaMarco || fondo.type != Image.Type.Sliced || fondo.raycastTarget)
            errores.Add($"{Nombre}: el fondo debe ser hud_caja en 9-slice y sin bloquear clics.");
        else if (AssetImporter.GetAtPath(RutaMarco) is TextureImporter t && (t.filterMode != FilterMode.Point || t.textureCompression != TextureImporterCompression.Uncompressed))
            errores.Add("hud_caja: no está en Point sin compresión.");
        var layout = caja.GetComponent<VerticalLayoutGroup>();
        if (layout == null || layout.padding.left != Relleno || layout.padding.top != Relleno || layout.padding.right != Relleno || layout.padding.bottom != Relleno
            || layout.spacing != Separacion || layout.childControlWidth || layout.childControlHeight)
            errores.Add($"{Nombre}: falta el VerticalLayoutGroup con relleno {Relleno} y separación {Separacion} (sin controlar el tamaño de las placas).");
        var ajuste = caja.GetComponent<ContentSizeFitter>();
        if (ajuste == null || ajuste.horizontalFit != ContentSizeFitter.FitMode.PreferredSize || ajuste.verticalFit != ContentSizeFitter.FitMode.PreferredSize)
            errores.Add($"{Nombre}: falta el ContentSizeFitter (tamaño preferido) para crecer y encoger.");
        var hijos = caja.Cast<Transform>().Select(h => h.name).ToList();
        if (!hijos.SequenceEqual(Orden)) errores.Add($"{Nombre}: hijos [{string.Join(", ", hijos)}], se esperaba [{string.Join(", ", Orden)}].");
        foreach (var n in Orden)
            if (canvas.transform.Find(n) != null) errores.Add($"{n} está suelta en el Canvas (debe ir dentro de {Nombre}).");
        var turbo = caja.Find("PlacaTurbo");
        var elemento = turbo != null ? turbo.GetComponent<LayoutElement>() : null;
        if (turbo != null && (elemento == null || !elemento.ignoreLayout)) errores.Add("PlacaTurbo: necesita un LayoutElement que empiece fuera del acomodo (ignoreLayout).");
        if (fondo == null || layout == null || ajuste == null || turbo == null || elemento == null) return errores;

        // con y sin turbo: la caja crece y encoge, nada se encima ni se sale de la pantalla (1920x1080)
        var ocupados = new[] { "MedidorSatisfaccion", "PlacaDinero", "BotonPausa" }
            .Select(n => canvas.transform.Find(n) as RectTransform).Where(r => r != null).ToList();
        var medidas = new List<float>();
        foreach (bool conTurbo in new[] { false, true })
        {
            elemento.ignoreLayout = !conTurbo;
            LayoutRebuilder.ForceRebuildLayoutImmediate(caja);
            var rc = Rectangulo(caja);
            medidas.Add(caja.sizeDelta.y);
            var pantalla = new Rect(Vector2.zero, Pantalla);
            if (rc.xMin < pantalla.xMin || rc.yMax > pantalla.yMax || rc.xMax > pantalla.xMax || rc.yMin < pantalla.yMin)
                errores.Add($"{Nombre} ({(conTurbo ? "con" : "sin")} turbo) se sale de la pantalla: {rc}.");
            foreach (var o in ocupados)
                if (rc.Overlaps(Rectangulo(o))) errores.Add($"{Nombre} ({(conTurbo ? "con" : "sin")} turbo) se encima con {o.name}.");
            var visibles = caja.Cast<RectTransform>().Where(h => h.gameObject.activeSelf && (conTurbo || h != turbo)).ToList();
            for (int i = 0; i < visibles.Count; i++)
            {
                var ri = Rectangulo(visibles[i]);
                var dentro = Rect.MinMaxRect(rc.xMin + Relleno - 0.5f, rc.yMin + Relleno - 0.5f, rc.xMax - Relleno + 0.5f, rc.yMax - Relleno + 0.5f);
                if (ri.xMin < dentro.xMin || ri.xMax > dentro.xMax || ri.yMin < dentro.yMin || ri.yMax > dentro.yMax)
                    errores.Add($"{visibles[i].name} se sale del relleno de {Nombre}.");
                for (int j = i + 1; j < visibles.Count; j++)
                {
                    var rj = Rectangulo(visibles[j]);
                    if (ri.Overlaps(rj)) errores.Add($"{visibles[i].name} se encima con {visibles[j].name}.");
                    else if (j == i + 1 && Mathf.Abs(ri.yMin - rj.yMax - Separacion) > 0.5f) errores.Add($"{visibles[i].name} y {visibles[j].name} no están a {Separacion}.");
                }
            }
        }
        elemento.ignoreLayout = true;
        LayoutRebuilder.ForceRebuildLayoutImmediate(caja);
        if (medidas[1] <= medidas[0]) errores.Add($"{Nombre}: no crece con la placa del turbo ({medidas[0]} → {medidas[1]}).");
        return errores;
    }

    // ------------------------------------------------------------------ menú y pedido (las 3 escenas)

    static string Aplicar(out bool ok)
    {
        string volverA = SceneManager.GetActiveScene().path;
        var lineas = new List<string>();
        try
        {
            foreach (var ruta in RevisarCanvas.EscenasDeJuego)
            {
                var escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
                string linea = Crear();
                var errores = Verificar();
                if (errores.Count > 0) { lineas.Add($"ERROR en {escena.name} (no se guardó):\n- " + string.Join("\n- ", errores)); continue; }
                lineas.Add(EditorSceneManager.SaveScene(escena) ? linea : $"ERROR: no pude guardar {ruta}.");
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(volverA) && File.Exists(volverA) && SceneManager.GetActiveScene().path != volverA)
                EditorSceneManager.OpenScene(volverA, OpenSceneMode.Single);
        }
        var faltan = RevisarCanvas.RevisarEscenasDeJuego();
        ok = faltan.Count == 0 && !lineas.Any(l => l.StartsWith("ERROR"));
        return string.Join("\n", lineas) + (faltan.Count == 0 ? "\nVERIFICACIÓN: Canvas completo (con CajaHUD) en las 3 escenas." : "\nFALTAN:\n- " + string.Join("\n- ", faltan));
    }

    [MenuItem("Delivery Brenn's/Caja del HUD (3 niveles)")]
    static void Menu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        try { EditorUtility.DisplayDialog("Caja del HUD", Aplicar(out _), "OK"); }
        catch (Exception e) { Debug.LogException(e); EditorUtility.DisplayDialog("Caja del HUD", e.Message, "OK"); }
    }

    [InitializeOnLoad]
    static class PedidoExterno
    {
        const string Pedido = "Temp/ConstruirCajaHUD.pedido";
        const string Resultado = "Temp/ConstruirCajaHUD.resultado";

        static PedidoExterno() => EditorApplication.update += Revisar;

        static void Revisar()
        {
            if (!File.Exists(Pedido) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
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
                if (SceneManager.GetActiveScene().isDirty) throw new Exception("La escena abierta tiene cambios sin guardar; no la cambio.");
                texto = Aplicar(out bool ok);
                texto = (ok ? "OK\n" : "CON ERRORES\n") + texto;
            }
            catch (Exception e) { Debug.LogException(e); texto = "ERROR\n" + e; }
            finally { Application.logMessageReceived -= Anotar; }
            texto += "\n\nCONSOLA (advertencias y errores): " + (consola.Count == 0 ? "ninguno" : "\n" + string.Join("\n", consola));
            File.WriteAllText(Resultado, texto);
        }
    }
}
