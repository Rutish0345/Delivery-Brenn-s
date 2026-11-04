using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Hace que el botón Play siempre arranque desde el menú principal,
// sin importar qué escena tengas abierta en el editor.
[InitializeOnLoad]
public static class IniciarDesdeMenu
{
    private const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";
    private const string Clave = "DeliveryBrenns_PlayDesdeMenu";
    private const string Opcion = "Delivery Brenn's/Play siempre desde el menú";

    static IniciarDesdeMenu()
    {
        Aplicar(EditorPrefs.GetBool(Clave, true));
    }

    private static void Aplicar(bool activo)
    {
        if (activo)
        {
            var menu = AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaMenu);
            if (menu == null)
            {
                Debug.LogWarning("No encuentro " + RutaMenu);
                return;
            }
            EditorSceneManager.playModeStartScene = menu;
        }
        else
        {
            EditorSceneManager.playModeStartScene = null;
        }
    }

    [MenuItem(Opcion)]
    private static void Alternar()
    {
        bool nuevo = !EditorPrefs.GetBool(Clave, true);
        EditorPrefs.SetBool(Clave, nuevo);
        Aplicar(nuevo);
    }

    [MenuItem(Opcion, true)]
    private static bool Validar()
    {
        Menu.SetChecked(Opcion, EditorPrefs.GetBool(Clave, true));
        return true;
    }
}