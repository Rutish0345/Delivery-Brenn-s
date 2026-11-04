using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Repara el Input System del editor cuando su estado guardado quedó apuntando a unos ajustes que ya no
// existen (la copia sin foco de una prueba en Play que Unity descargó): entonces cada actualización lanza
// TypeInitializationException. Borra el InputSystemObject oculto y recarga los scripts; al recargar, el
// Input System arranca de cero con los ajustes del proyecto.
// Temp/RepararEntrada.pedido → Temp/RepararEntrada.resultado
// Temp/LimpiarConsola.pedido limpia la ventana Console y deja en Temp/LimpiarConsola.resultado lo que queda.
[InitializeOnLoad]
public static class RepararEntrada
{
    const string Pedido = "Temp/RepararEntrada.pedido";
    const string Resultado = "Temp/RepararEntrada.resultado";

    static RepararEntrada() => EditorApplication.update += Revisar;

    static void Revisar()
    {
        if (File.Exists("Temp/LimpiarConsola.pedido") && !EditorApplication.isCompiling)
        {
            File.Delete("Temp/LimpiarConsola.pedido");
            var entradas = Type.GetType("UnityEditor.LogEntries, UnityEditor");
            entradas.GetMethod("Clear").Invoke(null, null);
            int n = (int)entradas.GetMethod("GetCount").Invoke(null, null);
            File.WriteAllText("Temp/LimpiarConsola.resultado", $"OK\n{n} entradas en la consola después de limpiar.");
        }
        if (!File.Exists(Pedido) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Pedido);
        // por nombre: la clase es interna del paquete y el tipo InputSystem no se puede ni tocar mientras falla
        var tipo = Type.GetType("UnityEngine.InputSystem.InputSystemObject, Unity.InputSystem");
        if (tipo == null)
        {
            File.WriteAllText(Resultado, "ERROR\nNo encuentro UnityEngine.InputSystem.InputSystemObject.");
            return;
        }
        var viejos = Resources.FindObjectsOfTypeAll(tipo);
        foreach (var o in viejos) Object.DestroyImmediate(o);
        File.WriteAllText(Resultado, $"OK\nBorrados {viejos.Length} InputSystemObject; recargando scripts.");
        EditorUtility.RequestScriptReload();
    }
}
