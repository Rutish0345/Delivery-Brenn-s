#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ConfigurarMotos
{
    private const string Carpeta = "Assets/Sprits/Motos/";

    [MenuItem("Delivery Brenn's/Configurar motos")]
    public static void Configurar()
    {
        var jugador = Object.FindFirstObjectByType<PlayerController>();
        if (jugador == null)
        {
            EditorUtility.DisplayDialog("Configurar motos", "No encontré el PlayerController en la escena.", "OK");
            return;
        }

        var datos = Progreso.Motos;
        var so = new SerializedObject(jugador);
        var prop = so.FindProperty("motos");
        prop.arraySize = datos.Length;

        var iconos = new Sprite[datos.Length];
        var faltan = new List<string>();

        for (int i = 0; i < datos.Length; i++)
        {
            string id = datos[i].id;
            string ruta = Carpeta + id + "/" + id + ".png";

            var hoja = new Dictionary<string, Sprite>();
            foreach (var s in AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>())
                hoja[s.name] = s;

            var el = prop.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("id").stringValue = id;
            Llenar(el.FindPropertyRelative("frente"), hoja, id + "_frente_", faltan);
            Llenar(el.FindPropertyRelative("espalda"), hoja, id + "_espalda_", faltan);
            Llenar(el.FindPropertyRelative("lado"), hoja, id + "_lado_", faltan);

            iconos[i] = AssetDatabase.LoadAssetAtPath<Sprite>(Carpeta + id + "/" + id + "_icono.png");
            if (iconos[i] == null) faltan.Add(id + "_icono (Texture Type debe ser Sprite)");
        }
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(jugador);

        // Objeto TiendaMotos (se crea si no existe)
        var tienda = Object.FindFirstObjectByType<TiendaMotos>();
        if (tienda == null)
        {
            var go = new GameObject("TiendaMotos");
            tienda = go.AddComponent<TiendaMotos>();
        }

        var so2 = new SerializedObject(tienda);
        var pi = so2.FindProperty("iconos");
        pi.arraySize = iconos.Length;
        for (int i = 0; i < iconos.Length; i++)
            pi.GetArrayElementAtIndex(i).objectReferenceValue = iconos[i];

        // Misma fuente que el resto de la interfaz
        var texto = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>()
            .FirstOrDefault(t => t.gameObject.scene.IsValid() && t.name == "TextoResumen");
        if (texto != null) so2.FindProperty("fuente").objectReferenceValue = texto.font;

        // Mismo marco y misma píldora que el panel final
        var panel = ImagenDeEscena("PanelFinal");
        var boton = ImagenDeEscena("BotonJugarDeNuevo");

        if (panel != null && panel.sprite != null)
        {
            so2.FindProperty("marcoPanel").objectReferenceValue = panel.sprite;
            so2.FindProperty("multiplicadorMarco").floatValue = panel.pixelsPerUnitMultiplier;
        }
        else faltan.Add("marco del PanelFinal (el objeto PanelFinal no tiene sprite)");

        if (boton != null && boton.sprite != null)
        {
            so2.FindProperty("pildora").objectReferenceValue = boton.sprite;
            so2.FindProperty("multiplicadorPildora").floatValue = boton.pixelsPerUnitMultiplier;
        }
        else faltan.Add("píldora de BotonJugarDeNuevo");

        so2.ApplyModifiedProperties();
        EditorUtility.SetDirty(tienda);
        EditorSceneManager.MarkSceneDirty(jugador.gameObject.scene);

        string msg = faltan.Count == 0
            ? "Listo: motos y tienda configuradas con el estilo del panel final.\nGuarda la escena con Ctrl+S."
            : "Configuré lo que encontré, pero faltan:\n- " + string.Join("\n- ", faltan);
        EditorUtility.DisplayDialog("Configurar motos", msg, "OK");
    }

    private static Image ImagenDeEscena(string nombre)
    {
        return Resources.FindObjectsOfTypeAll<Image>()
            .FirstOrDefault(i => i.gameObject.scene.IsValid() && i.name == nombre);
    }

    private static void Llenar(SerializedProperty arr, Dictionary<string, Sprite> hoja,
                               string prefijo, List<string> faltan)
    {
        arr.arraySize = 3;
        for (int j = 0; j < 3; j++)
        {
            if (hoja.TryGetValue(prefijo + j, out Sprite s)) arr.GetArrayElementAtIndex(j).objectReferenceValue = s;
            else faltan.Add(prefijo + j);
        }
    }

    // "Dar $1000", "Reiniciar progreso" y "Desbloquear todos los niveles" están en Editor/ProgresoPrueba.cs
}
#endif