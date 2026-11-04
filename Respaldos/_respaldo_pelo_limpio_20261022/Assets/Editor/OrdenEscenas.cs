using System.IO;
using System.Linq;
using UnityEditor;

// El orden de Build Profiles, en un solo lugar (lo usan ConstruirMenuPrincipal, ConstruirIntro, ConstruirSeleccion,
// ConstruirMapaComercial y ConstruirMapaLluvia): MenuPrincipal 0, Seleccion 1, Intro 2, SampleScene 3,
// Nivel2_Comercial 4, Nivel3_Lluvia 5. Las escenas cargan por nombre, pero así el índice también queda fijo.
public static class OrdenEscenas
{
    public static readonly string[] Rutas =
    {
        "Assets/Scenes/MenuPrincipal.unity", "Assets/Scenes/Seleccion.unity", "Assets/Scenes/Intro.unity",
        "Assets/Scenes/SampleScene.unity", "Assets/Scenes/Nivel2_Comercial.unity", "Assets/Scenes/Nivel3_Lluvia.unity",
    };

    // Pone las que existen en ese orden (cualquier otra queda detrás, como estaba). ok: están todas y en su lugar
    public static string Poner(out bool ok)
    {
        var existentes = Rutas.Where(File.Exists).ToList();
        var lista = existentes.Select(r => new EditorBuildSettingsScene(r, true)).ToList();
        lista.AddRange(EditorBuildSettings.scenes.Where(s => !existentes.Contains(s.path)));
        EditorBuildSettings.scenes = lista.ToArray();

        var reales = EditorBuildSettings.scenes;
        ok = existentes.Count == Rutas.Length &&
             Rutas.Select((r, i) => reales.Length > i && reales[i].path == r && reales[i].enabled && !reales[i].guid.Empty()).All(b => b);
        return "Build: " + string.Join(", ", reales.Select((e, i) => $"{i} {Path.GetFileNameWithoutExtension(e.path)}")) +
               (ok ? "" : "  (faltan escenas o el orden no quedó: " + string.Join(", ", Rutas.Except(existentes).Select(Path.GetFileNameWithoutExtension)) + ")");
    }
}
