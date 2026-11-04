using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Repartidora (Progreso.Genero = 1), idempotente:
//  - Assets/Animations/Repartidora: las 6 animaciones del repartidor con los cuadros de repartidora_sheet.png (mismo
//    índice de cuadro, mismos tiempos) y RepartidoraOverride.overrideController sobre RepartidorController (mismos
//    estados, así PlayerController.Play("Walk_Lado") sirve para los dos).
//  - En los 3 niveles llena PlayerController.controladorRepartidora y motosRepartidora (de Motos/<id>/<id>_chica.png,
//    generar_motos.py --chica). No toca colliders, velocidades ni la economía.
// No corre si la escena abierta tiene cambios sin guardar; al terminar la vuelve a abrir.
// Temp/ConstruirRepartidora.pedido → Temp/ConstruirRepartidora.resultado
[InitializeOnLoad]
static class ConstruirRepartidora
{
    const string Pedido = "Temp/ConstruirRepartidora.pedido";
    const string Resultado = "Temp/ConstruirRepartidora.resultado";
    const string HojaChico = "Assets/Sprits/Personajes/repartidor_sheet.png";
    const string HojaChica = "Assets/Sprits/Personajes/repartidora_sheet.png";
    const string Controlador = "Assets/Animations/RepartidorController.controller";
    const string Carpeta = "Assets/Animations/Repartidora";
    public const string RutaOverride = Carpeta + "/RepartidoraOverride.overrideController";
    const string CarpetaMotos = "Assets/Sprits/Motos/";
    static readonly string[] Escenas = { "Assets/Scenes/SampleScene.unity", "Assets/Scenes/Nivel2_Comercial.unity", "Assets/Scenes/Nivel3_Lluvia.unity" };

    static ConstruirRepartidora() => EditorApplication.update += Revisar;

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
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);   // las hojas recién generadas en pixel_town
            var over = CrearAnimaciones(lineas);
            var motos = MotosChica(lineas, out bool motosCompletas);
            foreach (var ruta in Escenas)
            {
                var escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
                var pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>() ?? throw new Exception($"{escena.name}: no hay PlayerController.");
                var so = new SerializedObject(pc);
                so.FindProperty("controladorRepartidora").objectReferenceValue = over;
                var prop = so.FindProperty("motosRepartidora");
                prop.arraySize = motos.Count;
                for (int i = 0; i < motos.Count; i++)
                {
                    var el = prop.GetArrayElementAtIndex(i);
                    el.FindPropertyRelative("id").stringValue = motos[i].id;
                    foreach (var (campo, arr) in new[] { ("frente", motos[i].frente), ("espalda", motos[i].espalda), ("lado", motos[i].lado) })
                    {
                        var a = el.FindPropertyRelative(campo);
                        a.arraySize = arr.Length;
                        for (int j = 0; j < arr.Length; j++) a.GetArrayElementAtIndex(j).objectReferenceValue = arr[j];
                    }
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(escena);
                bool guardada = EditorSceneManager.SaveScene(escena);
                ok &= guardada;
                lineas.Add($"{escena.name}: PlayerController con la repartidora ({over.name}, {motos.Count} motos) guardada {guardada}");
            }
            if (!motosCompletas) lineas.Add("AVISO: faltan hojas de motos con la repartidora (generar_motos.py --chica).");
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

    // Cada clip del controlador del repartidor → <clip>_Chica con repartidor_sheet_N cambiado por repartidora_sheet_N
    static AnimatorOverrideController CrearAnimaciones(List<string> lineas)
    {
        AssetDatabase.ImportAsset(HojaChica, ImportAssetOptions.ForceSynchronousImport);
        var chica = AssetDatabase.LoadAllAssetsAtPath(HojaChica).OfType<Sprite>().ToDictionary(s => s.name);
        if (chica.Count != 15) throw new Exception($"{HojaChica} tiene {chica.Count} cuadros, no 15 (corre generar_repartidora_sheet.py --unity).");
        var baseCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Controlador) ?? throw new Exception($"No encuentro {Controlador}.");
        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Animations", "Repartidora");

        var pares = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        foreach (var clip in baseCtrl.animationClips.Distinct())
        {
            string ruta = $"{Carpeta}/{clip.name}_Chica.anim";
            var nuevo = new AnimationClip();
            EditorUtility.CopySerialized(clip, nuevo);
            nuevo.name = clip.name + "_Chica";
            int cambiados = 0;
            foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var claves = AnimationUtility.GetObjectReferenceCurve(clip, b);
                for (int i = 0; i < claves.Length; i++)
                {
                    var s = claves[i].value as Sprite;
                    if (s == null || AssetDatabase.GetAssetPath(s) != HojaChico) continue;
                    string nombre = s.name.Replace("repartidor_sheet_", "repartidora_sheet_");
                    claves[i].value = chica.TryGetValue(nombre, out var c) ? c : throw new Exception($"Falta {nombre}.");
                    cambiados++;
                }
                AnimationUtility.SetObjectReferenceCurve(nuevo, b, claves);
            }
            if (cambiados == 0) throw new Exception($"{clip.name} no usa cuadros de {HojaChico}.");
            var existente = AssetDatabase.LoadAssetAtPath<AnimationClip>(ruta);
            if (existente != null) { EditorUtility.CopySerialized(nuevo, existente); EditorUtility.SetDirty(existente); nuevo = existente; }
            else AssetDatabase.CreateAsset(nuevo, ruta);
            pares.Add(new KeyValuePair<AnimationClip, AnimationClip>(clip, nuevo));
            lineas.Add($"{nuevo.name}: {cambiados} cuadros");
        }

        var over = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(RutaOverride);
        if (over == null)
        {
            over = new AnimatorOverrideController(baseCtrl) { name = "RepartidoraOverride" };
            AssetDatabase.CreateAsset(over, RutaOverride);
        }
        over.runtimeAnimatorController = baseCtrl;
        over.ApplyOverrides(pares);
        EditorUtility.SetDirty(over);
        AssetDatabase.SaveAssets();
        return over;
    }

    // Motos/<id>/<id>_chica.png con los cuadros <id>_chica_<vista>_<n> (generar_motos.py --chica)
    static List<MotoVisual> MotosChica(List<string> lineas, out bool completas)
    {
        var lista = new List<MotoVisual>();
        completas = true;
        foreach (var d in Progreso.Motos)
        {
            string ruta = $"{CarpetaMotos}{d.id}/{d.id}_chica.png";
            var hoja = AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>().ToDictionary(s => s.name);
            if (hoja.Count == 0) { completas = false; continue; }
            Sprite[] Vista(string v) => Enumerable.Range(0, 3).Select(j => hoja.TryGetValue($"{d.id}_chica_{v}_{j}", out var s) ? s
                                         : throw new Exception($"Falta {d.id}_chica_{v}_{j} en {ruta}.")).ToArray();
            lista.Add(new MotoVisual { id = d.id, frente = Vista("frente"), espalda = Vista("espalda"), lado = Vista("lado") });
        }
        lineas.Add($"motos con la repartidora: {lista.Count} de {Progreso.Motos.Length}");
        return lista;
    }
}
