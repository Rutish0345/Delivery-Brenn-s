using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Las dos fuentes de la interfaz, como TMP Font Assets (atlas estático de 2048 con ASCII, Latin-1 y los
// signos que usa el juego). Las crean y las usan todos los constructores; los demás font assets del proyecto
// (LiberationSans) no se tocan.
//  - Brenn Start: titulares, botones y toda la intro (estilo de la referencia "menu 2"). Es Press Start 2P
//    con Á É Í Ó Ú Ñ Ü a tamaño completo (la original las dibuja del alto de una minúscula); la arma
//    pixel_town/fuentes/hacer_brenn_start.py y por la licencia OFL lleva otro nombre.
//  - Pixelify Sans SemiBold: párrafos largos (instrucciones, descripciones de la tienda), el HUD, el campo
//    del nombre y el globo de las clientas, donde Press Start 2P queda demasiado ancha.
// Materiales: el de cada fuente (plano, sin contorno), TituloPixel.mat (Press Start con contorno crema suave,
// títulos sobre fondos claros) y TituloIntro.mat (Press Start con contorno y sombra #2A1F40 gruesos).
public static class FuenteProyecto
{
    public const string RutaTTF = "Assets/Fonts/static/PixelifySans-SemiBold.ttf";
    public const string RutaFuente = "Assets/Fonts/PixelifySans SDF.asset";
    public const string RutaMaterialTitulo = "Assets/Fonts/TituloPixel.mat";
    public const string RutaTTFTitulo = "Assets/Fonts/Brenn_Start/BrennStart-Regular.ttf";
    public const string RutaFuenteTitulo = "Assets/Fonts/BrennStart SDF.asset";
    public const string RutaMaterialIntro = "Assets/Fonts/TituloIntro.mat";

    public static readonly Color32 Oscuro = new Color32(0x2A, 0x1F, 0x40, 255);
    public static readonly Color32 Crema = new Color32(0xFF, 0xF1, 0xD0, 255);
    public const float ContornoTitulo = 0.12f;
    public const float ContornoIntro = 0.4f;

    // Las mayúsculas de Press Start 2P miden 0.875 em (y cada letra 1 em de ancho); las de Pixelify, 0.645 em.
    // A 26, Press Start se ve del alto de Pixelify a 36: ese es su mínimo, y un tamaño de Pixelify equivale
    // a 0.74 veces en Press Start
    public const float Minimo = 36;
    public const float MinimoTitulo = 26;
    public const float Equivalencia = 0.74f;

    // ASCII imprimible + Latin-1 (á é í ó ú ñ ü ¿ ¡ ·, mayúsculas con acento) + algunos signos tipográficos
    public static readonly string Caracteres =
        new string(Enumerable.Range(0x20, 0x7F - 0x20).Select(c => (char)c).ToArray()) +
        new string(Enumerable.Range(0xA0, 0x100 - 0xA0).Where(c => c != 0xA6).Select(c => (char)c).ToArray()) +   // sin ¦: Pixelify no lo trae
        "–—‘’“”…•€";

    // Press Start 2P: lo mismo más ¦ (trae todo Latin-1, incluidos á é í ó ú ñ ü Á É Í Ó Ú Ñ ¿ ¡ $ % · |)
    public static readonly string CaracteresTitulo = Caracteres + "¦";

    [MenuItem("Delivery Brenn's/Crear fuente pixel")]
    static void CrearMenu() => Debug.Log($"Fuentes listas: {AssetDatabase.GetAssetPath(Fuente())}, {AssetDatabase.GetAssetPath(FuenteTitulo())}");

    // Pixelify Sans (párrafos). Si no existe (o le faltan caracteres) la crea o completa
    public static TMP_FontAsset Fuente() => Cargar(RutaFuente, RutaTTF, "PixelifySans SDF", Caracteres, 90, 9);

    // Press Start 2P (titulares, botones, intro). Muestreo de 96 = 12 px por pixel de la fuente (8x8)
    public static TMP_FontAsset FuenteTitulo() => Cargar(RutaFuenteTitulo, RutaTTFTitulo, "BrennStart SDF", CaracteresTitulo, 96, 10);

    public static bool EsTitulo(TMP_FontAsset f) => f != null && AssetDatabase.GetAssetPath(f) == RutaFuenteTitulo;
    public static bool EsDelProyecto(TMP_FontAsset f) => f != null && (EsTitulo(f) || AssetDatabase.GetAssetPath(f) == RutaFuente);

    static TMP_FontAsset Cargar(string ruta, string rutaTTF, string nombre, string caracteres, int muestreo, int relleno)
    {
        var fuente = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ruta);
        if (fuente != null && Faltantes(fuente, rutaTTF, caracteres).Length == 0) return fuente;
        if (fuente == null) fuente = Crear(ruta, rutaTTF, nombre, muestreo, relleno);
        if (fuente == null) return null;

        // dinámico mientras se agregan los caracteres; luego estático para que no cambie al jugar
        fuente.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        fuente.TryAddCharacters(caracteres, out string faltan);
        fuente.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(fuente);
        EditorUtility.SetDirty(fuente.atlasTexture);
        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(faltan)) Debug.LogWarning($"{nombre} no tiene: {faltan}");
        return fuente;
    }

    static TMP_FontAsset Crear(string ruta, string rutaTTF, string nombre, int muestreo, int relleno)
    {
        var ttf = AssetDatabase.LoadAssetAtPath<Font>(rutaTTF);
        if (ttf == null)
        {
            Debug.LogError($"No encuentro {rutaTTF}.");
            return null;
        }
        var fuente = TMP_FontAsset.CreateFontAsset(ttf, muestreo, relleno, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
        fuente.name = nombre;
        AssetDatabase.CreateAsset(fuente, ruta);
        fuente.atlasTexture.name = nombre + " Atlas";
        AssetDatabase.AddObjectToAsset(fuente.atlasTexture, fuente);
        fuente.material.name = nombre + " Material";
        AssetDatabase.AddObjectToAsset(fuente.material, fuente);
        AssetDatabase.SaveAssets();
        return fuente;
    }

    // Caracteres del juego que la fuente todavía no tiene (los que el TTF no trae no cuentan)
    public static string Faltantes(TMP_FontAsset fuente) =>
        EsTitulo(fuente) ? Faltantes(fuente, RutaTTFTitulo, CaracteresTitulo) : Faltantes(fuente, RutaTTF, Caracteres);

    static string Faltantes(TMP_FontAsset fuente, string rutaTTF, string caracteres)
    {
        var ttf = AssetDatabase.LoadAssetAtPath<Font>(rutaTTF);
        return new string(caracteres.Where(c => !fuente.HasCharacter(c) && (ttf == null || ttf.HasCharacter(c)) && !char.IsControl(c)
                                                && c != ' ' && c != ' ' && c != '­').ToArray());
    }

    // Material de cada fuente, sin contorno
    public static Material MaterialPlano() => Fuente().material;
    public static Material MaterialTituloPlano() => FuenteTitulo().material;

    // Press Start con contorno crema suave, para títulos. La cara se dilata lo mismo que media línea de
    // contorno, así el contorno queda por fuera de la letra y no se come el trazo
    public static Material MaterialTitulo()
    {
        var mat = MaterialDe(RutaMaterialTitulo, "TituloPixel");
        mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, ContornoTitulo);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, Crema);
        mat.SetFloat(ShaderUtilities.ID_FaceDilate, ContornoTitulo / 2);
        return Guardar(mat);
    }

    // Press Start con contorno #2A1F40 grueso y sombra del mismo color abajo a la derecha (titulares de la
    // intro, sobre la ilustración)
    public static Material MaterialIntro()
    {
        var mat = MaterialDe(RutaMaterialIntro, "TituloIntro");
        mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, ContornoIntro);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, Oscuro);
        mat.SetFloat(ShaderUtilities.ID_FaceDilate, ContornoIntro / 2);
        mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, Oscuro);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 1f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -1f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, ContornoIntro * 2);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
        return Guardar(mat);
    }

    static Material MaterialDe(string ruta, string nombre)
    {
        var fuente = FuenteTitulo();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat == null)
        {
            mat = new Material(fuente.material) { name = nombre };
            AssetDatabase.CreateAsset(mat, ruta);
        }
        else
        {
            mat.shader = fuente.material.shader;
            mat.CopyPropertiesFromMaterial(fuente.material);   // también el atlas de Press Start
            mat.shaderKeywords = new string[0];
        }
        return mat;
    }

    static Material Guardar(Material mat)
    {
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    // Deja un texto con la fuente del proyecto: titulo = Press Start con contorno crema; si no, Pixelify plano
    public static void Aplicar(TMP_Text tmp, bool titulo = false)
    {
        tmp.font = titulo ? FuenteTitulo() : Fuente();
        tmp.fontSharedMaterial = titulo ? MaterialTitulo() : MaterialPlano();
    }

    // Press Start, plano o con el material que se pase (MaterialTitulo, MaterialIntro)
    public static void AplicarTitulo(TMP_Text tmp, Material material = null)
    {
        tmp.font = FuenteTitulo();
        tmp.fontSharedMaterial = material != null ? material : MaterialTituloPlano();
    }

    // Tamaño en Press Start que se ve como 'tam' en Pixelify (par y nunca menos de MinimoTitulo)
    public static float Equivalente(float tam) => Mathf.Max(MinimoTitulo, Mathf.Round(tam * Equivalencia / 2f) * 2f);

    // El mínimo de un texto según su fuente
    public static float MinimoDe(TMP_Text tmp) => EsTitulo(tmp.font) ? MinimoTitulo : Minimo;

    // Texto de pastilla: #2A1F40, sin contorno, mínimo 36 (en referencia 1920x1080)
    public static void EstiloPastilla(TMP_Text tmp, float maximo = 40)
    {
        Aplicar(tmp);
        tmp.color = Oscuro;
        tmp.fontStyle = FontStyles.Normal;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = Minimo;
        tmp.fontSizeMax = Mathf.Max(Minimo, maximo);
        tmp.fontSize = tmp.fontSizeMax;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
    }

    // Texto de botón: igual que la pastilla pero en Press Start (mínimo 26, que se ve como 36 en Pixelify);
    // 'maximo' es el tamaño que tendría en Pixelify
    public static void EstiloBoton(TMP_Text tmp, float maximo = 40)
    {
        EstiloPastilla(tmp, maximo);
        AplicarTitulo(tmp);
        tmp.fontSizeMin = MinimoTitulo;
        tmp.fontSizeMax = Equivalente(maximo);
        tmp.fontSize = tmp.fontSizeMax;
    }

    // ¿Cabe el texto de una pastilla a su tamaño mínimo? (para las verificaciones)
    public static bool Cabe(TMP_Text tmp)
    {
        var rt = tmp.rectTransform;
        float ancho = rt.rect.width;
        if (ancho <= 0) return true;   // todavía sin layout
        return Ancho(tmp, tmp.text) <= ancho + 0.5f;
    }

    // Ancho de un texto a su tamaño mínimo (con autoajuste, GetPreferredValues mide al máximo: se apaga)
    public static float Ancho(TMP_Text tmp, string texto)
    {
        var (tam, auto) = (tmp.fontSize, tmp.enableAutoSizing);
        if (auto) { tmp.enableAutoSizing = false; tmp.fontSize = tmp.fontSizeMin; }
        float ancho = tmp.GetPreferredValues(texto).x;
        (tmp.fontSize, tmp.enableAutoSizing) = (tam, auto);
        return ancho;
    }
}
