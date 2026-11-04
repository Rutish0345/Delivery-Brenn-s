using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Atuendo de LLUVIA (impermeable dorado, capucha hacia atrás, botas moradas; la repartidora con su falda), solo en
// Nivel3_Lluvia. Idempotente:
//  - Assets/Animations/Lluvia: las 6 animaciones del repartidor con los cuadros de repartidor_sheet_lluvia.png
//    (<clip>_Lluvia) y de repartidora_sheet_lluvia.png (<clip>_ChicaLluvia), y dos AnimatorOverrideController sobre
//    RepartidorController (mismos estados): RepartidorLluviaOverride y RepartidoraLluviaOverride.
//  - En Nivel3_Lluvia: PlayerController.atuendoLluvia = true, controladorLluvia, controladorRepartidoraLluvia,
//    motosLluvia (Motos/<id>/<id>_lluvia.png) y motosRepartidoraLluvia (<id>_chica_lluvia.png). Los otros niveles quedan
//    con atuendoLluvia apagado; la selección, la intro y el final no usan PlayerController.
// El arte sale de pixel_town/personajes: generar_impermeable.py --unity, generar_motos.py --lluvia y --chica --lluvia.
// No toca colliders, velocidades ni los juegos normales (ConstruirRepartidora), y ConstruirRepartidora no toca estos.
// No corre si la escena abierta tiene cambios sin guardar (con "descartar" en el pedido, si es Nivel3_Lluvia, la vuelve
// a abrir del disco); si falla, descarta lo que alcanzó a cambiar; al terminar vuelve a abrir la escena que estaba.
// Temp/ConstruirImpermeable.pedido → Temp/ConstruirImpermeable.resultado
[InitializeOnLoad]
static class ConstruirImpermeable
{
    const string Pedido = "Temp/ConstruirImpermeable.pedido";
    const string Resultado = "Temp/ConstruirImpermeable.resultado";
    public const string HojaChico = "Assets/Sprits/Personajes/repartidor_sheet_lluvia.png";
    public const string HojaChica = "Assets/Sprits/Personajes/repartidora_sheet_lluvia.png";
    const string Carpeta = "Assets/Animations/Lluvia";
    public const string RutaOverrideChico = Carpeta + "/RepartidorLluviaOverride.overrideController";
    public const string RutaOverrideChica = Carpeta + "/RepartidoraLluviaOverride.overrideController";
    public const string Escena = "Assets/Scenes/Nivel3_Lluvia.unity";
    static readonly string[] OtrosNiveles = { "Assets/Scenes/SampleScene.unity", "Assets/Scenes/Nivel2_Comercial.unity" };
    const string Generador = "pixel_town/personajes/generar_impermeable.py --unity";

    static ConstruirImpermeable() => EditorApplication.update += Revisar;

    static void Revisar()
    {
        if (!File.Exists(Pedido) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        bool descartar = File.ReadAllText(Pedido).Contains("descartar");
        File.Delete(Pedido);
        var lineas = new List<string>();
        bool ok = true;
        var abierta = SceneManager.GetActiveScene().path;
        try
        {
            // "descartar": Nivel3_Lluvia abierta con cambios de una corrida fallida se vuelve a abrir del disco
            if (descartar && SceneManager.GetActiveScene().path == Escena && SceneManager.GetActiveScene().isDirty)
            {
                EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
                lineas.Add("Nivel3_Lluvia tenía cambios sin guardar: se descartaron (se abrió del disco).");
            }
            if (SceneManager.GetActiveScene().isDirty) throw new Exception($"{abierta} tiene cambios sin guardar; no la cambio.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);   // las hojas recién generadas en pixel_town
            var chico = ConstruirRepartidora.Clonar(HojaChico, Carpeta, "_Lluvia", RutaOverrideChico, Generador, lineas);
            var chica = ConstruirRepartidora.Clonar(HojaChica, Carpeta, "_ChicaLluvia", RutaOverrideChica, Generador, lineas);
            var motos = ConstruirRepartidora.MotosDe("_lluvia", out bool completasChico);
            var motosChica = ConstruirRepartidora.MotosDe("_chica_lluvia", out bool completasChica);
            if (!completasChico || !completasChica || motos.Count != Progreso.Motos.Length || motosChica.Count != Progreso.Motos.Length)
                throw new Exception($"faltan hojas de motos de lluvia ({motos.Count} y {motosChica.Count} de {Progreso.Motos.Length}; " +
                                    "corre generar_motos.py --lluvia y --chica --lluvia).");

            var escena = EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
            var pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>() ?? throw new Exception($"{escena.name}: no hay PlayerController.");
            var so = new SerializedObject(pc);
            so.FindProperty("atuendoLluvia").boolValue = true;
            so.FindProperty("controladorLluvia").objectReferenceValue = chico;
            so.FindProperty("controladorRepartidoraLluvia").objectReferenceValue = chica;
            ConstruirRepartidora.PonerMotos(so.FindProperty("motosLluvia"), motos);
            ConstruirRepartidora.PonerMotos(so.FindProperty("motosRepartidoraLluvia"), motosChica);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(escena);
            var errores = Verificar(pc);
            if (errores.Count > 0) throw new Exception("VERIFICACIÓN:\n- " + string.Join("\n- ", errores));
            bool guardada = EditorSceneManager.SaveScene(escena);
            ok &= guardada;
            lineas.Add($"{escena.name}: atuendo de lluvia ({chico.name}, {chica.name}, {motos.Count} + {motosChica.Count} motos) guardada {guardada}");
            foreach (var otro in VerificarOtros()) { ok = false; lineas.Add("ERROR " + otro); }
            lineas.Add("Verificación: hojas de 15 cuadros (Point, sin compresión, PPU y pivotes del repartidor), 6 animaciones por " +
                       "personaje con los cuadros de lluvia, 4 + 4 motos de 3 vistas; los otros niveles sin el atuendo.");
        }
        catch (Exception e)
        {
            ok = false;
            lineas.Add("ERROR " + e.Message);
            // lo que este constructor alcanzó a cambiar en Nivel3_Lluvia no se queda a medias: se vuelve a abrir del disco
            var activa = SceneManager.GetActiveScene();
            if (activa.path == Escena && activa.isDirty && abierta != Escena)
            {
                EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
                lineas.Add("Nivel3_Lluvia sin guardar: cambios descartados.");
            }
        }
        finally
        {
            if (abierta.Length > 0 && SceneManager.GetActiveScene().path != abierta && !SceneManager.GetActiveScene().isDirty)
                EditorSceneManager.OpenScene(abierta, OpenSceneMode.Single);
        }
        File.WriteAllText(Resultado, (ok ? "OK" : "CON ERRORES") + "\n" + string.Join("\n", lineas));
    }

    // El PlayerController de Nivel3_Lluvia con el atuendo completo y las texturas importadas como las normales
    static List<string> Verificar(PlayerController pc)
    {
        var errores = new List<string>();
        var so = new SerializedObject(pc);
        if (!so.FindProperty("atuendoLluvia").boolValue) errores.Add("atuendoLluvia apagado.");
        foreach (var (campo, hoja) in new[] { ("controladorLluvia", HojaChico), ("controladorRepartidoraLluvia", HojaChica) })
        {
            var over = so.FindProperty(campo).objectReferenceValue as AnimatorOverrideController;
            if (over == null) { errores.Add($"{campo} vacío."); continue; }
            var pares = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            over.GetOverrides(pares);
            if (pares.Count == 0 || pares.Any(p => p.Value == null)) errores.Add($"{over.name}: faltan animaciones.");
            foreach (var p in pares.Where(p => p.Value != null))
            {
                var sprites = AnimationUtility.GetObjectReferenceCurveBindings(p.Value)
                    .SelectMany(b => AnimationUtility.GetObjectReferenceCurve(p.Value, b)).Select(k => k.value as Sprite).ToList();
                if (sprites.Count == 0 || sprites.Any(s => s == null || AssetDatabase.GetAssetPath(s) != hoja))
                    errores.Add($"{p.Value.name}: no usa solo cuadros de {Path.GetFileName(hoja)}.");
            }
        }
        foreach (var campo in new[] { "motosLluvia", "motosRepartidoraLluvia" })
        {
            var prop = so.FindProperty(campo);
            if (prop.arraySize != Progreso.Motos.Length) errores.Add($"{campo}: {prop.arraySize} motos, no {Progreso.Motos.Length}.");
            for (int i = 0; i < prop.arraySize; i++)
                foreach (var vista in new[] { "frente", "espalda", "lado" })
                {
                    var arr = prop.GetArrayElementAtIndex(i).FindPropertyRelative(vista);
                    if (arr.arraySize != 3 || Enumerable.Range(0, 3).Any(j => !(arr.GetArrayElementAtIndex(j).objectReferenceValue is Sprite s)
                                                                              || !s.name.Contains("_lluvia_")))
                        errores.Add($"{campo}[{i}].{vista}: no son 3 cuadros de lluvia.");
                }
        }
        // mismas opciones de importación que la hoja del repartidor (PPU, filtro, compresión, pivotes)
        var normal = (TextureImporter)AssetImporter.GetAtPath("Assets/Sprits/Personajes/repartidor_sheet.png");
        foreach (var ruta in new[] { HojaChico, HojaChica })
        {
            var imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (imp == null) { errores.Add($"No existe {ruta}."); continue; }
            if (imp.filterMode != FilterMode.Point || imp.textureCompression != TextureImporterCompression.Uncompressed
                || imp.spritePixelsPerUnit != normal.spritePixelsPerUnit)
                errores.Add($"{Path.GetFileName(ruta)}: no es Point, sin compresión y PPU {normal.spritePixelsPerUnit}.");
            // el mismo corte que la hoja normal (rectángulos de 54x80 dentro de las celdas de 56) y el mismo pivote
            string prefijo = Path.GetFileNameWithoutExtension(ruta) + "_";
            var normales = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprits/Personajes/repartidor_sheet.png").OfType<Sprite>()
                .ToDictionary(s => s.name.Replace("repartidor_sheet_", prefijo));
            var cuadros = AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>().ToList();
            if (cuadros.Count != 15 || cuadros.Any(c => !normales.TryGetValue(c.name, out var n) || c.rect != n.rect || c.pivot != n.pivot))
                errores.Add($"{Path.GetFileName(ruta)}: no tiene los 15 cuadros con el corte y los pivotes de repartidor_sheet.png.");
        }
        return errores;
    }

    // Los otros niveles no llevan el atuendo de lluvia (se lee el YAML, sin abrir las escenas)
    static IEnumerable<string> VerificarOtros()
    {
        foreach (var ruta in OtrosNiveles)
            if (File.Exists(ruta) && File.ReadAllText(ruta).Contains("atuendoLluvia: 1"))
                yield return $"{Path.GetFileNameWithoutExtension(ruta)} tiene el atuendo de lluvia encendido.";
    }
}
