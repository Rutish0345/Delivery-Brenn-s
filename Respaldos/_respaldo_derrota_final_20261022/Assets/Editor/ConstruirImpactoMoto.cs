using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Pone ImpactoMoto (choques de la moto) en el repartidor con su fuente y materiales: Brenn Start (Press Start 2P),
// ChoqueTexto.mat (contorno #2A1F40) y DestelloBlanco.mat (silueta blanca, GUI/Text Shader). ConstruirMapa lo
// llama al colocar al repartidor, así reconstruir un nivel no lo pierde; el menú y el pedido lo aplican a los
// niveles 2 y 3 sin reconstruirlos. En el nivel 1 las motos están apagadas y ImpactoMoto no hace nada.
// Temp/ConstruirImpactoMoto.pedido → Temp/ConstruirImpactoMoto.resultado
public static class ConstruirImpactoMoto
{
    const string RutaMaterialTexto = "Assets/Fonts/ChoqueTexto.mat";
    const string CarpetaChoque = "Assets/Sprits/Comun/Choque";
    const string RutaMaterialDestello = CarpetaChoque + "/DestelloBlanco.mat";
    const float ContornoTexto = 0.3f;
    static readonly string[] Escenas = { "Assets/Scenes/Nivel2_Comercial.unity", "Assets/Scenes/Nivel3_Lluvia.unity" };

    // Agrega (o deja) ImpactoMoto en el repartidor y le asigna fuente y materiales
    public static void Configurar(PlayerController jugador)
    {
        if (jugador == null) return;
        var impacto = jugador.GetComponent<ImpactoMoto>();
        if (impacto == null) impacto = Undo.AddComponent<ImpactoMoto>(jugador.gameObject);

        var fuente = FuenteProyecto.FuenteTitulo();
        if (fuente == null) throw new Exception($"No encuentro la fuente {FuenteProyecto.RutaFuenteTitulo}.");
        var so = new SerializedObject(impacto);
        so.FindProperty("fuente").objectReferenceValue = fuente;
        so.FindProperty("materialContorno").objectReferenceValue = MaterialTexto(fuente);
        so.FindProperty("materialPlano").objectReferenceValue = fuente.material;
        so.FindProperty("materialDestello").objectReferenceValue = MaterialDestello();
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(impacto);
    }

    // Press Start con contorno #2A1F40 por fuera del trazo (el color de la cara lo pone cada texto)
    static Material MaterialTexto(TMP_FontAsset fuente)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialTexto);
        if (mat == null)
        {
            mat = new Material(fuente.material) { name = "ChoqueTexto" };
            AssetDatabase.CreateAsset(mat, RutaMaterialTexto);
        }
        else
        {
            mat.shader = fuente.material.shader;
            mat.CopyPropertiesFromMaterial(fuente.material);
        }
        mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, ContornoTexto);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, FuenteProyecto.Oscuro);
        mat.SetFloat(ShaderUtilities.ID_FaceDilate, ContornoTexto / 2);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    // Pinta la silueta del sprite de un solo color (el alfa de la textura por el color del SpriteRenderer)
    static Material MaterialDestello()
    {
        var shader = Shader.Find("GUI/Text Shader");
        if (shader == null) throw new Exception("No encuentro el shader GUI/Text Shader.");
        if (!AssetDatabase.IsValidFolder(CarpetaChoque)) AssetDatabase.CreateFolder("Assets/Sprits/Comun", "Choque");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialDestello);
        if (mat == null)
        {
            mat = new Material(shader) { name = "DestelloBlanco" };
            AssetDatabase.CreateAsset(mat, RutaMaterialDestello);
        }
        mat.shader = shader;
        mat.color = Color.white;
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    public static string Revisar(List<string> errores)
    {
        var jugador = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        var impacto = jugador != null ? jugador.GetComponent<ImpactoMoto>() : null;
        string escena = SceneManager.GetActiveScene().name;
        if (impacto == null) { errores.Add($"{escena}: el repartidor no tiene ImpactoMoto."); return $"{escena}: sin ImpactoMoto."; }
        var so = new SerializedObject(impacto);
        foreach (var campo in new[] { "fuente", "materialContorno", "materialPlano", "materialDestello" })
            if (so.FindProperty(campo).objectReferenceValue == null) errores.Add($"{escena}: ImpactoMoto sin {campo}.");
        if (!FuenteProyecto.EsTitulo(so.FindProperty("fuente").objectReferenceValue as TMP_FontAsset))
            errores.Add($"{escena}: el texto del choque no usa Press Start 2P.");
        return $"{escena}: ImpactoMoto en {jugador.name} (autos {so.FindProperty("penalizacionAuto").floatValue}, " +
               $"peatones {so.FindProperty("penalizacionPeaton").floatValue}, mínimo {so.FindProperty("velocidadMinima").floatValue} tiles/s, " +
               $"cooldown {so.FindProperty("cooldown").floatValue} s).";
    }

    [MenuItem("Delivery Brenn's/Choques de la moto (niveles 2 y 3)")]
    static void Menu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        try { EditorUtility.DisplayDialog("Choques de la moto", Aplicar(out _), "OK"); }
        catch (Exception e) { Debug.LogException(e); EditorUtility.DisplayDialog("Choques de la moto", e.Message, "OK"); }
    }

    // Abre cada nivel, configura al repartidor, guarda y vuelve a la escena que estaba abierta
    static string Aplicar(out bool ok)
    {
        string volverA = SceneManager.GetActiveScene().path;
        var lineas = new List<string>();
        var errores = new List<string>();
        try
        {
            foreach (var ruta in Escenas)
            {
                var escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
                var jugador = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
                if (jugador == null) { errores.Add($"{ruta}: no hay PlayerController."); continue; }
                Configurar(jugador);
                EditorSceneManager.MarkSceneDirty(escena);
                if (!EditorSceneManager.SaveScene(escena)) errores.Add($"No pude guardar {ruta}.");
                lineas.Add(Revisar(errores));
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(volverA) && File.Exists(volverA) && SceneManager.GetActiveScene().path != volverA)
                EditorSceneManager.OpenScene(volverA, OpenSceneMode.Single);
        }
        ok = errores.Count == 0;
        return string.Join("\n", lineas) + (ok ? "\nVERIFICACIÓN: sin errores." : "\nERRORES:\n- " + string.Join("\n- ", errores));
    }

    [InitializeOnLoad]
    static class PedidoExterno
    {
        const string Pedido = "Temp/ConstruirImpactoMoto.pedido";
        const string Resultado = "Temp/ConstruirImpactoMoto.resultado";

        static PedidoExterno() => EditorApplication.update += Revisar;

        static void Revisar()
        {
            if (!File.Exists(Pedido) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;   // nunca interrumpe una prueba en Play
            File.Delete(Pedido);
            var consola = new List<string>();
            void Anotar(string msg, string pila, LogType tipo)
            {
                if (tipo != LogType.Log) consola.Add($"[{tipo}] {msg}" + (tipo == LogType.Exception ? "\n" + pila : ""));
            }
            Application.logMessageReceived += Anotar;
            string texto;
            try
            {
                texto = Aplicar(out bool ok);
                texto = (ok ? "OK\n" : "OK CON ERRORES DE VERIFICACION\n") + texto;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                texto = "ERROR\n" + e;
            }
            finally { Application.logMessageReceived -= Anotar; }
            texto += "\n\nCONSOLA (advertencias y errores): " + (consola.Count == 0 ? "ninguno" : "\n" + string.Join("\n", consola));
            File.WriteAllText(Resultado, texto);
        }
    }
}
