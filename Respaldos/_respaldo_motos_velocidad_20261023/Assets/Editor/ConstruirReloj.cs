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

// Placa del reloj por pedido en el HUD: Canvas/PlacaReloj (píldora verde/amarilla/roja, IconoReloj y TextoReloj en
// Press Start #2A1F40) con RelojPedido, justo debajo de PlacaPedidos; la guía del pedido baja dos renglones (el
// reloj y la fila de quejas). El arte lo
// hace pixel_town/generate_reloj_hud.py. Se puede correr las veces que sea (borra y rehace la placa).
// ConstruirMapa la llama al armar el HUD de cualquier nivel; el menú y el pedido la aplican a las 3 escenas de juego
// sin reconstruirlas. Temp/ConstruirReloj.pedido → Temp/ConstruirReloj.resultado
public static class ConstruirReloj
{
    const string Carpeta = "Assets/Sprits/UI/";
    const string Nombre = "PlacaReloj";
    const int Escala = 4;                                   // 1 píxel de arte = 4 unidades del Canvas
    const float AltoPlaca = 18 * Escala;
    const float Separacion = 12;
    public static readonly Vector2 PosPlaca = new Vector2(30, -38 - AltoPlaca - Separacion);   // bajo PlacaPedidos
    // bajo el reloj va la fila de quejas (ConstruirQuejas: iconos de 18 px a x3) y debajo la guía del pedido
    public const float AltoQuejas = 18 * 3;
    public static readonly Vector2 PosQuejas = new Vector2(30, PosPlaca.y - AltoPlaca - Separacion);
    // bajo las quejas, la placa del turbo (ConstruirTurbos; solo se ve con el Café exprés activo) y luego la guía
    public const float AltoTurbo = 18 * 3;
    public static readonly Vector2 PosTurbo = new Vector2(30, PosQuejas.y - AltoQuejas - Separacion);
    public static readonly Vector2 PosGuia = new Vector2(30, PosTurbo.y - AltoTurbo - Separacion);
    const float AnchoPlaca = 260;
    const float LadoIcono = 12 * Escala;

    static readonly string[] Placas = { "placa_reloj_verde", "placa_reloj_amarilla", "placa_reloj_roja" };

    // Sprites con la importación de la píldora (16 px por unidad, 9-slice, sin filtro ni compresión)
    static Sprite[] Sprites(out Sprite icono)
    {
        var pildora = (TextureImporter)AssetImporter.GetAtPath(Carpeta + "boton_pill_normal.png");
        var ajustes = new TextureImporterSettings();
        pildora.ReadTextureSettings(ajustes);
        foreach (var n in Placas.Append("icono_reloj"))
        {
            string ruta = Carpeta + n + ".png";
            if (!File.Exists(ruta)) throw new Exception($"Falta {ruta} (corre pixel_town/generate_reloj_hud.py).");
            var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
            if (imp == null) { AssetDatabase.ImportAsset(ruta); imp = (TextureImporter)AssetImporter.GetAtPath(ruta); }
            var antes = new TextureImporterSettings();
            imp.ReadTextureSettings(antes);
            var nuevo = new TextureImporterSettings();
            ajustes.CopyTo(nuevo);
            if (n == "icono_reloj") nuevo.spriteBorder = Vector4.zero;
            bool cambia = imp.textureType != TextureImporterType.Sprite || imp.textureCompression != TextureImporterCompression.Uncompressed ||
                          antes.spriteBorder != nuevo.spriteBorder || antes.spritePixelsPerUnit != nuevo.spritePixelsPerUnit ||
                          antes.filterMode != nuevo.filterMode || antes.spriteMode != nuevo.spriteMode;
            if (!cambia) continue;
            imp.SetTextureSettings(nuevo);
            imp.textureType = TextureImporterType.Sprite;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
        icono = AssetDatabase.LoadAssetAtPath<Sprite>(Carpeta + "icono_reloj.png");
        return Placas.Select(n => AssetDatabase.LoadAssetAtPath<Sprite>(Carpeta + n + ".png")).ToArray();
    }

    static Canvas CanvasHUD() =>
        SceneManager.GetActiveScene().GetRootGameObjects().Select(g => g.GetComponent<Canvas>())
            .FirstOrDefault(c => c != null && c.transform.Find("PlacaPedidos") != null);

    // Crea la placa en la escena activa (y baja la guía). Devuelve un renglón para el resumen.
    public static string Crear()
    {
        var canvas = CanvasHUD();
        if (canvas == null) throw new Exception("No encuentro el Canvas con PlacaPedidos (ejecuta Construir interfaz).");
        var placas = Sprites(out var icono);
        if (placas.Any(p => p == null) || icono == null) throw new Exception("No pude cargar los sprites del reloj.");
        var fuente = FuenteProyecto.FuenteTitulo();

        var vieja = canvas.transform.Find(Nombre);
        if (vieja != null) Undo.DestroyObjectImmediate(vieja.gameObject);

        var placa = Hijo(Nombre, canvas.transform);
        placa.anchorMin = placa.anchorMax = placa.pivot = new Vector2(0, 1);
        placa.anchoredPosition = PosPlaca;
        placa.sizeDelta = new Vector2(AnchoPlaca, AltoPlaca);
        var fondo = placa.gameObject.AddComponent<Image>();
        fondo.sprite = placas[0];
        fondo.type = Image.Type.Sliced;
        fondo.pixelsPerUnitMultiplier = canvas.referencePixelsPerUnit / (placas[0].pixelsPerUnit * Escala);
        fondo.raycastTarget = false;
        var grupo = placa.gameObject.AddComponent<CanvasGroup>();
        grupo.interactable = false;
        grupo.blocksRaycasts = false;
        grupo.alpha = 0f;   // RelojPedido la muestra cuando hay pedido

        // ícono a la izquierda, pasando la tapa de la píldora (6 px de arte), centrado en vertical sobre el canto
        var rtIcono = Hijo("IconoReloj", placa);
        rtIcono.anchorMin = rtIcono.anchorMax = rtIcono.pivot = new Vector2(0, 0.5f);
        rtIcono.anchoredPosition = new Vector2(6 * Escala, Escala);
        rtIcono.sizeDelta = new Vector2(LadoIcono, LadoIcono);
        var imgIcono = rtIcono.gameObject.AddComponent<Image>();
        imgIcono.sprite = icono;
        imgIcono.raycastTarget = false;

        var rtTexto = Hijo("TextoReloj", placa);
        rtTexto.anchorMin = Vector2.zero;
        rtTexto.anchorMax = Vector2.one;
        rtTexto.pivot = new Vector2(0.5f, 0.5f);
        rtTexto.offsetMin = new Vector2(6 * Escala + LadoIcono + 2 * Escala, 3 * Escala);
        rtTexto.offsetMax = new Vector2(-6 * Escala, -1 * Escala);
        var texto = rtTexto.gameObject.AddComponent<TextMeshProUGUI>();
        texto.text = "0:00";
        FuenteProyecto.EstiloBoton(texto);   // Press Start #2A1F40, como el texto de las pastillas
        if (fuente != null) texto.font = fuente;

        var reloj = placa.gameObject.AddComponent<RelojPedido>();
        var so = new SerializedObject(reloj);
        so.FindProperty("grupo").objectReferenceValue = grupo;
        so.FindProperty("placa").objectReferenceValue = fondo;
        so.FindProperty("texto").objectReferenceValue = texto;
        so.FindProperty("placaVerde").objectReferenceValue = placas[0];
        so.FindProperty("placaAmarilla").objectReferenceValue = placas[1];
        so.FindProperty("placaRoja").objectReferenceValue = placas[2];
        so.ApplyModifiedPropertiesWithoutUndo();

        // con el HUD (debajo de los paneles en la jerarquía, para que la pausa y el panel final lo tapen)
        var pedidos = canvas.transform.Find("PlacaPedidos");
        placa.SetSiblingIndex(pedidos.GetSiblingIndex() + 1);

        var guia = canvas.transform.Find("GuiaPedido") as RectTransform;
        if (guia != null)
        {
            Undo.RecordObject(guia, "Construir reloj");
            guia.anchoredPosition = PosGuia;
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return $"{SceneManager.GetActiveScene().name}: PlacaReloj en {PosPlaca}, GuiaPedido en {PosGuia}.";
    }

    static RectTransform Hijo(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Construir reloj");
        go.layer = padre.gameObject.layer;
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
    }

    [MenuItem("Delivery Brenn's/Reloj del pedido (3 niveles)")]
    static void Menu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        try { EditorUtility.DisplayDialog("Reloj del pedido", Aplicar(out _), "OK"); }
        catch (Exception e) { Debug.LogException(e); EditorUtility.DisplayDialog("Reloj del pedido", e.Message, "OK"); }
    }

    static string Aplicar(out bool ok)
    {
        string volverA = SceneManager.GetActiveScene().path;
        var lineas = new List<string>();
        try
        {
            foreach (var ruta in RevisarCanvas.EscenasDeJuego)
            {
                var escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
                lineas.Add(Crear());
                if (!EditorSceneManager.SaveScene(escena)) lineas.Add($"ERROR: no pude guardar {ruta}.");
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(volverA) && File.Exists(volverA) && SceneManager.GetActiveScene().path != volverA)
                EditorSceneManager.OpenScene(volverA, OpenSceneMode.Single);
        }
        var errores = RevisarCanvas.RevisarEscenasDeJuego();
        ok = errores.Count == 0 && !lineas.Any(l => l.StartsWith("ERROR"));
        return string.Join("\n", lineas) + (errores.Count == 0 ? "\nVERIFICACIÓN: Canvas completo (con reloj) en las 3 escenas." : "\nERRORES:\n- " + string.Join("\n- ", errores));
    }

    [InitializeOnLoad]
    static class PedidoExterno
    {
        const string Pedido = "Temp/ConstruirReloj.pedido";
        const string Resultado = "Temp/ConstruirReloj.resultado";

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
                texto = (ok ? "OK\n" : "OK CON ERRORES\n") + texto;
            }
            catch (Exception e) { Debug.LogException(e); texto = "ERROR\n" + e; }
            finally { Application.logMessageReceived -= Anotar; }
            texto += "\n\nCONSOLA (advertencias y errores): " + (consola.Count == 0 ? "ninguno" : "\n" + string.Join("\n", consola));
            File.WriteAllText(Resultado, texto);
        }
    }
}
