using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// La fuente de toda la interfaz: Pixelify Sans SemiBold como TMP Font Asset (atlas estático de 2048 con
// ASCII, Latin-1 y los signos que usa el juego). La crean y la usan todos los constructores; los demás
// font assets del proyecto (LiberationSans) no se tocan.
// Dos materiales: el de la fuente (plano, sin contorno: el de botones, pastillas y textos sobre crema) y
// TituloPixel.mat (contorno crema suave, solo para títulos).
public static class FuenteProyecto
{
    public const string RutaTTF = "Assets/Fonts/static/PixelifySans-SemiBold.ttf";
    public const string RutaFuente = "Assets/Fonts/PixelifySans SDF.asset";
    public const string RutaMaterialTitulo = "Assets/Fonts/TituloPixel.mat";

    public static readonly Color32 Oscuro = new Color32(0x2A, 0x1F, 0x40, 255);
    public static readonly Color32 Crema = new Color32(0xFF, 0xF1, 0xD0, 255);
    public const float ContornoTitulo = 0.12f;

    // ASCII imprimible + Latin-1 (á é í ó ú ñ ü ¿ ¡ ·, mayúsculas con acento) + algunos signos tipográficos
    public static readonly string Caracteres =
        new string(Enumerable.Range(0x20, 0x7F - 0x20).Select(c => (char)c).ToArray()) +
        new string(Enumerable.Range(0xA0, 0x100 - 0xA0).Where(c => c != 0xA6).Select(c => (char)c).ToArray()) +   // sin ¦: Pixelify no lo trae
        "–—‘’“”…•€";

    [MenuItem("Delivery Brenn's/Crear fuente pixel")]
    static void CrearMenu() => Debug.Log($"Fuente lista: {AssetDatabase.GetAssetPath(Fuente())}");

    // Carga el font asset; si no existe (o le faltan caracteres) lo crea o completa
    public static TMP_FontAsset Fuente()
    {
        var fuente = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RutaFuente);
        if (fuente != null && Faltantes(fuente).Length == 0) return fuente;
        if (fuente == null) fuente = Crear();
        if (fuente == null) return null;

        // dinámico mientras se agregan los caracteres; luego estático para que no cambie al jugar
        fuente.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        fuente.TryAddCharacters(Caracteres, out string faltan);
        fuente.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(fuente);
        EditorUtility.SetDirty(fuente.atlasTexture);
        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(faltan)) Debug.LogWarning($"Pixelify Sans no tiene: {faltan}");
        return fuente;
    }

    static TMP_FontAsset Crear()
    {
        var ttf = AssetDatabase.LoadAssetAtPath<Font>(RutaTTF);
        if (ttf == null)
        {
            Debug.LogError($"No encuentro {RutaTTF}.");
            return null;
        }
        var fuente = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
        fuente.name = "PixelifySans SDF";
        AssetDatabase.CreateAsset(fuente, RutaFuente);
        fuente.atlasTexture.name = "PixelifySans SDF Atlas";
        AssetDatabase.AddObjectToAsset(fuente.atlasTexture, fuente);
        fuente.material.name = "PixelifySans SDF Material";
        AssetDatabase.AddObjectToAsset(fuente.material, fuente);
        AssetDatabase.SaveAssets();
        return fuente;
    }

    // Caracteres del juego que la fuente todavía no tiene (los que el TTF no trae no cuentan)
    public static string Faltantes(TMP_FontAsset fuente)
    {
        var ttf = AssetDatabase.LoadAssetAtPath<Font>(RutaTTF);
        return new string(Caracteres.Where(c => !fuente.HasCharacter(c) && (ttf == null || ttf.HasCharacter(c)) && !char.IsControl(c)
                                                && c != ' ' && c != '­').ToArray());
    }

    // Material de la fuente, sin contorno
    public static Material MaterialPlano() => Fuente().material;

    // Contorno crema suave para títulos. La cara se dilata lo mismo que media línea de contorno, así el
    // contorno queda por fuera de la letra y no se come el trazo (que se veía claro y borroso)
    public static Material MaterialTitulo()
    {
        var fuente = Fuente();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialTitulo);
        if (mat == null)
        {
            mat = new Material(fuente.material) { name = "TituloPixel" };
            AssetDatabase.CreateAsset(mat, RutaMaterialTitulo);
        }
        else
        {
            mat.shader = fuente.material.shader;
            mat.CopyPropertiesFromMaterial(fuente.material);
        }
        mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, ContornoTitulo);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, Crema);
        mat.SetFloat(ShaderUtilities.ID_FaceDilate, ContornoTitulo / 2);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    // Deja un texto con la fuente pixel; titulo = contorno crema, si no, plano
    public static void Aplicar(TMP_Text tmp, bool titulo = false)
    {
        tmp.font = Fuente();
        tmp.fontSharedMaterial = titulo ? MaterialTitulo() : MaterialPlano();
    }

    // Texto de pastilla: #2A1F40, sin contorno, mínimo 36 (en referencia 1920x1080)
    public static void EstiloPastilla(TMP_Text tmp, float maximo = 40)
    {
        Aplicar(tmp);
        tmp.color = Oscuro;
        tmp.fontStyle = FontStyles.Normal;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 36;
        tmp.fontSizeMax = Mathf.Max(36, maximo);
        tmp.fontSize = tmp.fontSizeMax;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
    }

    // ¿Cabe el texto de una pastilla a su tamaño mínimo? (para las verificaciones)
    public static bool Cabe(TMP_Text tmp)
    {
        var rt = tmp.rectTransform;
        float ancho = rt.rect.width;
        if (ancho <= 0) return true;   // todavía sin layout
        var tam = tmp.fontSize;
        tmp.fontSize = tmp.enableAutoSizing ? tmp.fontSizeMin : tmp.fontSize;
        bool cabe = tmp.GetPreferredValues(tmp.text).x <= ancho + 0.5f;
        tmp.fontSize = tam;
        return cabe;
    }
}
