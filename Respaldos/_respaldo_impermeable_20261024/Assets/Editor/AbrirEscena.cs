using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// Abre una escena para que la use un constructor que trabaja sobre la escena activa (ConstruirMapa).
// Temp/AbrirEscena.pedido (dentro: ruta de la escena) → Temp/AbrirEscena.resultado. No cambia de escena si la abierta
// tiene cambios sin guardar ni durante Play.
[InitializeOnLoad]
static class AbrirEscena
{
    const string Pedido = "Temp/AbrirEscena.pedido";
    const string Resultado = "Temp/AbrirEscena.resultado";

    static AbrirEscena() => EditorApplication.update += Revisar;

    static void Revisar()
    {
        if (!File.Exists(Pedido) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string ruta = File.ReadAllText(Pedido).Trim();
        File.Delete(Pedido);
        var activa = SceneManager.GetActiveScene();
        if (activa.isDirty)
        {
            File.WriteAllText(Resultado, $"ERROR\n{activa.path} tiene cambios sin guardar; no la cambio.");
            return;
        }
        var escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
        File.WriteAllText(Resultado, $"OK\n{escena.path}");
    }
}
