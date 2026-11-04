using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Rehace solo la flecha guía del mundo (FlechaPedido, ConstruirMarcadorPedido.CrearFlecha) en los 3 niveles, sin
// reconstruir el mapa; en el nivel 3 la deja Unlit como ConstruirMapaLluvia (de noche no se apaga con la luz global).
// No toca el marcador, el HUD ni nada más. Vuelve a abrir la escena que estaba abierta. No corre con cambios sin guardar.
// Temp/ConstruirFlecha.pedido → Temp/ConstruirFlecha.resultado
[InitializeOnLoad]
static class ConstruirFlecha
{
    const string Pedido = "Temp/ConstruirFlecha.pedido";
    const string Resultado = "Temp/ConstruirFlecha.resultado";
    const string RutaUnlit = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    static readonly string[] Escenas = { "Assets/Scenes/SampleScene.unity", "Assets/Scenes/Nivel2_Comercial.unity", "Assets/Scenes/Nivel3_Lluvia.unity" };

    static ConstruirFlecha() => EditorApplication.update += Revisar;

    static void Revisar()
    {
        if (!File.Exists(Pedido) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Pedido);
        var lineas = new List<string>();
        bool ok = true;
        var abierta = SceneManager.GetActiveScene().path;
        try
        {
            if (SceneManager.GetActiveScene().isDirty) throw new Exception($"{abierta} tiene cambios sin guardar; no la cambio.");
            foreach (var ruta in Escenas)
            {
                var escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
                ConstruirMarcadorPedido.CrearFlecha();
                var raiz = escena.GetRootGameObjects().First(g => g.name == ConstruirMarcadorPedido.NombreFlecha);
                if (ruta.Contains("Nivel3"))
                {
                    var unlit = AssetDatabase.LoadAssetAtPath<Material>(RutaUnlit) ?? throw new Exception($"No encuentro {RutaUnlit}.");
                    foreach (var sr in raiz.GetComponentsInChildren<SpriteRenderer>(true)) sr.sharedMaterial = unlit;
                }
                var errores = new List<string>();
                string resumen = ConstruirMarcadorPedido.RevisarFlechaYEntrega(errores);
                string material = string.Join(", ", raiz.GetComponentsInChildren<SpriteRenderer>(true).Select(s => $"{s.name}: {s.sharedMaterial?.name}"));
                EditorSceneManager.MarkSceneDirty(escena);
                bool guardada = EditorSceneManager.SaveScene(escena);
                ok &= errores.Count == 0 && guardada;
                lineas.Add($"{escena.name}: {resumen} [{material}] guardada {guardada}" + (errores.Count > 0 ? "\n  " + string.Join("\n  ", errores) : ""));
            }
        }
        catch (Exception e)
        {
            ok = false;
            lineas.Add("ERROR " + e);
        }
        finally
        {
            if (abierta.Length > 0 && SceneManager.GetActiveScene().path != abierta && !SceneManager.GetActiveScene().isDirty)
                EditorSceneManager.OpenScene(abierta, OpenSceneMode.Single);
        }
        File.WriteAllText(Resultado, (ok ? "OK" : "CON ERRORES") + "\n" + string.Join("\n", lineas));
    }
}
