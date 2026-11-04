using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Solo lee: vuelca el árbol del Canvas, los botones con sus listeners, los EventSystem, las imágenes que
// bloquean clics a pantalla completa y el historial de Undo. Funciona en modo edición y en Play.
// Temp/DiagnosticoUI.pedido → Temp/DiagnosticoUI.resultado
public static class DiagnosticoUI
{
    [MenuItem("Delivery Brenn's/Pruebas/Diagnóstico de la UI")]
    static void DiagnosticoMenu() => Debug.Log(Informe());

    public static string Informe()
    {
        var sb = new StringBuilder();
        var escena = SceneManager.GetActiveScene();
        sb.AppendLine($"Escena {escena.name}  dirty={escena.isDirty}  playing={EditorApplication.isPlaying}  Time.timeScale={Time.timeScale}");
        sb.AppendLine("Undo (más reciente al final): " + string.Join(" | ", HistorialUndo().Skip(Math.Max(0, HistorialUndo().Count - 15))));

        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.isRootCanvas))
        {
            sb.AppendLine($"\n=== ÁRBOL de {canvas.name} (sortingOrder {canvas.sortingOrder}) ===");
            Arbol(canvas.transform, 0, sb);
        }

        sb.AppendLine("\n=== BOTONES ===");
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .OrderBy(b => Ruta(b.transform)))
        {
            var rt = (RectTransform)b.transform;
            var tmp = b.GetComponentInChildren<TMP_Text>(true);
            var oyentes = Enumerable.Range(0, b.onClick.GetPersistentEventCount())
                .Select(i => $"{(b.onClick.GetPersistentTarget(i) != null ? b.onClick.GetPersistentTarget(i).name + "(" + b.onClick.GetPersistentTarget(i).GetType().Name + ")" : "NULL")}.{b.onClick.GetPersistentMethodName(i)}");
            sb.AppendLine($"- {b.name}  padre={b.transform.parent?.name}  ruta={Ruta(b.transform)}");
            sb.AppendLine($"    pos={rt.anchoredPosition} tam={rt.sizeDelta} activo={b.gameObject.activeInHierarchy}(self {b.gameObject.activeSelf}) interactable={b.interactable}" +
                          $" texto=\"{tmp?.text}\" colorTexto={(tmp != null ? "#" + ColorUtility.ToHtmlStringRGB(tmp.color) : "-")}");
            sb.AppendLine($"    persistentes=[{string.Join(", ", oyentes)}]");
        }

        sb.AppendLine("\n=== TEXTOS DE PanelFinal / PanelPausa ===");
        foreach (var t in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .Where(t => Ruta(t.transform).Contains("PanelFinal") || Ruta(t.transform).Contains("PanelPausa")).OrderBy(t => Ruta(t.transform)))
        {
            var mat = t.fontSharedMaterial;
            sb.AppendLine($"- {Ruta(t.transform)}: \"{t.text.Replace("\n", "\\n")}\" color=#{ColorUtility.ToHtmlStringRGBA(t.color)} material={mat?.name}" +
                          $" contorno={(mat != null && mat.HasProperty(ShaderUtilities.ID_OutlineColor) ? "#" + ColorUtility.ToHtmlStringRGB(mat.GetColor(ShaderUtilities.ID_OutlineColor)) + " w=" + mat.GetFloat(ShaderUtilities.ID_OutlineWidth) : "-")}");
        }

        sb.AppendLine("\n=== EVENTSYSTEM ===");
        foreach (var es in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sb.AppendLine($"- {Ruta(es.transform)} activo={es.isActiveAndEnabled} módulos=[{string.Join(", ", es.GetComponents<BaseInputModule>().Select(m => m.GetType().Name + (m.enabled ? "" : "(off)")))}]");

        sb.AppendLine("\n=== IMÁGENES CON raycastTarget QUE CUBREN ≥ 50% DE LA PANTALLA (1920x1080) ===");
        foreach (var g in Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(g => g.raycastTarget))
        {
            var r = g.rectTransform;
            var c = g.canvas != null ? g.canvas.rootCanvas : null;
            if (c == null) continue;
            var esquinas = new Vector3[4];
            r.GetWorldCorners(esquinas);
            var crt = (RectTransform)c.transform;
            float area = Mathf.Abs((esquinas[2].x - esquinas[0].x) * (esquinas[2].y - esquinas[0].y)) /
                         Mathf.Max(1, crt.rect.width * crt.rect.height * crt.lossyScale.x * crt.lossyScale.y);
            if (area >= 0.5f)
                sb.AppendLine($"- {Ruta(g.transform)} activoEnJerarquía={g.gameObject.activeInHierarchy} cubre={area:P0} color={g.color}");
        }

        sb.AppendLine("\n=== OBJETOS DE LÓGICA ===");
        foreach (var t in new[] { typeof(MenuJuego), typeof(LevelManager), typeof(UIManager), typeof(TiendaMotos), typeof(GameManager) })
            foreach (var o in Object.FindObjectsByType(t, FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(o);
                var campos = new List<string>();
                var it = so.GetIterator();
                for (bool entrar = true; it.NextVisible(entrar); entrar = false)
                    if (it.name != "m_Script")
                        campos.Add($"{it.name}={Valor(it)}");
                sb.AppendLine($"- {t.Name} en \"{((Component)o).gameObject.name}\": {string.Join("; ", campos)}");
            }
        return sb.ToString();
    }

    static string Valor(SerializedProperty p)
    {
        switch (p.propertyType)
        {
            case SerializedPropertyType.ObjectReference: return p.objectReferenceValue != null ? p.objectReferenceValue.name : "null";
            case SerializedPropertyType.String: return "\"" + p.stringValue + "\"";
            case SerializedPropertyType.Integer: return p.intValue.ToString();
            case SerializedPropertyType.Float: return p.floatValue.ToString();
            case SerializedPropertyType.Boolean: return p.boolValue.ToString();
            case SerializedPropertyType.Enum: return p.enumDisplayNames.Length > p.enumValueIndex && p.enumValueIndex >= 0 ? p.enumDisplayNames[p.enumValueIndex] : p.enumValueIndex.ToString();
            default: return p.isArray ? $"[{p.arraySize}]" : p.propertyType.ToString();
        }
    }

    static void Arbol(Transform t, int nivel, StringBuilder sb)
    {
        var rt = t as RectTransform;
        var comps = t.GetComponents<Component>().Where(c => c != null && !(c is Transform) && !(c is CanvasRenderer)).Select(c => c.GetType().Name);
        var tmp = t.GetComponent<TMP_Text>();
        sb.AppendLine($"{new string(' ', nivel * 2)}{(t.gameObject.activeSelf ? "" : "[off] ")}{t.name}" +
                      (rt != null ? $"  pos={rt.anchoredPosition} tam={rt.sizeDelta}" : "") +
                      $"  [{string.Join(",", comps)}]" + (tmp != null ? $" \"{tmp.text.Replace("\n", "\\n")}\"" : ""));
        // las filas de props/tráfico no interesan: solo la UI
        for (int i = 0; i < t.childCount; i++) Arbol(t.GetChild(i), nivel + 1, sb);
    }

    static string Ruta(Transform t) => t.parent == null ? t.name : Ruta(t.parent) + "/" + t.name;

    // Undo.GetRecords es interno; si cambia de firma se devuelve vacío
    static List<string> HistorialUndo()
    {
        var hechos = new List<string>();
        try
        {
            var m = typeof(Undo).GetMethod("GetRecords", BindingFlags.NonPublic | BindingFlags.Static, null,
                                           new[] { typeof(List<string>), typeof(List<string>) }, null);
            m?.Invoke(null, new object[] { hechos, new List<string>() });
        }
        catch { }
        return hechos;
    }

    [InitializeOnLoad]
    static class PedidoExterno
    {
        const string Pedido = "Temp/DiagnosticoUI.pedido";
        const string Resultado = "Temp/DiagnosticoUI.resultado";

        static PedidoExterno() => EditorApplication.update += Revisar;

        // Temp/EjecutarMenu.pedido con la ruta de un menú dentro → lo ejecuta y deja Temp/EjecutarMenu.resultado
        const string PedidoMenu = "Temp/EjecutarMenu.pedido";
        const string ResultadoMenu = "Temp/EjecutarMenu.resultado";

        static void Revisar()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.Exists(PedidoMenu) && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                string ruta = File.ReadAllText(PedidoMenu).Trim();
                File.Delete(PedidoMenu);
                var consola = new List<string>();
                void Anotar(string msg, string pila, LogType tipo) => consola.Add($"[{tipo}] {msg}");
                Application.logMessageReceived += Anotar;
                bool ok;
                try { ok = EditorApplication.ExecuteMenuItem(ruta); }
                finally { Application.logMessageReceived -= Anotar; }
                File.WriteAllText(ResultadoMenu, $"{(ok ? "EJECUTADO" : "NO EXISTE")} {ruta}\n{string.Join("\n", consola)}");
            }
            if (!File.Exists(Pedido)) return;
            File.Delete(Pedido);
            string texto;
            try { texto = Informe(); }
            catch (Exception e) { texto = "ERROR\n" + e; }
            File.WriteAllText(Resultado, texto);
        }
    }
}
