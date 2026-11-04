using UnityEngine;
using UnityEngine.SceneManagement;

// Al dar Play desde cualquier escena, se manda primero al menú principal.
// Solo corre una vez, al arrancar; los cambios de escena normales no se tocan.
public static class ArranqueDesdeMenu
{
    private const string EscenaMenu = "MenuPrincipal";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Revisar()
    {
#if UNITY_EDITOR
        // Usa el mismo interruptor del menú "Delivery Brenn's > Play siempre desde el menú"
        if (!UnityEditor.EditorPrefs.GetBool("DeliveryBrenns_PlayDesdeMenu", true)) return;
#endif
        if (SceneManager.GetActiveScene().name == EscenaMenu) return;

        Time.timeScale = 1f;
        SceneManager.LoadScene(EscenaMenu);
    }
}