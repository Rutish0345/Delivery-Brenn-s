using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Pone objetos de la escena activa en una posición mundial dada y la guarda (para devolver ajustes hechos a mano que un
// rebuild pisó). Temp/MoverObjetos.pedido (una línea por objeto: ruta|x|y) → Temp/MoverObjetos.resultado. No corre en Play.
[InitializeOnLoad]
static class MoverObjetos
{
    const string Pedido = "Temp/MoverObjetos.pedido";
    const string Resultado = "Temp/MoverObjetos.resultado";

    static MoverObjetos() => EditorApplication.update += Revisar;

    static void Revisar()
    {
        if (!File.Exists(Pedido) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var lineas = File.ReadAllLines(Pedido).Where(l => l.Trim().Length > 0).ToList();
        File.Delete(Pedido);
        var escena = SceneManager.GetActiveScene();
        var log = new List<string>();
        int movidos = 0;
        foreach (var l in lineas)
        {
            var p = l.Split('|');
            var partes = p[0].Split('/');
            var t = escena.GetRootGameObjects().FirstOrDefault(g => g.name == partes[0])?.transform;
            for (int i = 1; t != null && i < partes.Length; i++) t = t.Find(partes[i]);
            if (t == null) { log.Add("NO ESTÁ: " + p[0]); continue; }
            var antes = t.position;
            t.position = new Vector3(float.Parse(p[1], CultureInfo.InvariantCulture), float.Parse(p[2], CultureInfo.InvariantCulture), antes.z);
            log.Add($"{p[0]}: ({antes.x:0.###}, {antes.y:0.###}) → ({t.position.x:0.###}, {t.position.y:0.###})");
            movidos++;
        }
        EditorSceneManager.MarkSceneDirty(escena);
        bool guardada = EditorSceneManager.SaveScene(escena);
        File.WriteAllText(Resultado, $"{(movidos == lineas.Count && guardada ? "OK" : "ERROR")}\n{escena.path}: {movidos} de {lineas.Count} movidos, guardada {guardada}\n" + string.Join("\n", log));
    }
}
