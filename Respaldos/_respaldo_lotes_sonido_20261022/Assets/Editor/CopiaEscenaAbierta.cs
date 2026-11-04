using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// Diagnóstico: guarda una COPIA de la escena abierta (con sus cambios sin guardar) en Temp/EscenaAbierta.unity, sin
// guardar ni descartar la original, para compararla con la del disco.
// Temp/CopiaEscenaAbierta.pedido → Temp/CopiaEscenaAbierta.resultado
[InitializeOnLoad]
static class CopiaEscenaAbierta
{
    const string Pedido = "Temp/CopiaEscenaAbierta.pedido";
    const string Resultado = "Temp/CopiaEscenaAbierta.resultado";

    static CopiaEscenaAbierta() => EditorApplication.update += Revisar;

    static void Revisar()
    {
        if (!File.Exists(Pedido) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Pedido);
        var escena = SceneManager.GetActiveScene();
        bool ok = EditorSceneManager.SaveScene(escena, "Temp/EscenaAbierta.unity", true);
        File.WriteAllText(Resultado, $"{(ok ? "OK" : "ERROR")}\n{escena.path}\nsucia: {escena.isDirty}");
    }
}
