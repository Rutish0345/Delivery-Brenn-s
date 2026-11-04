using System.IO;
using UnityEditor;
using UnityEngine;

public static class CrearEstrellas
{
    // 13x13, cada # es un pixel de la estrella
    private static readonly string[] molde =
    {
        "......#......",
        "......#......",
        ".....###.....",
        ".....###.....",
        "#############",
        ".###########.",
        "..#########..",
        "...#######...",
        "...#######...",
        "..####.####..",
        "..###...###..",
        ".###.....###.",
        ".##.......##."
    };

    [MenuItem("Delivery Brenn's/Crear sprites de estrella")]
    public static void Crear()
    {
        string carpeta = "Assets/Sprits/UI";
        Directory.CreateDirectory(carpeta);

        string llena = carpeta + "/estrella_llena.png";
        string vacia = carpeta + "/estrella_vacia.png";

        Generar(llena, Color("#FFD166"), Color("#9A5B00"));
        Generar(vacia, Color("#4A3A6B"), Color("#251B3D"));

        AssetDatabase.Refresh();
        Configurar(llena);
        Configurar(vacia);
        Debug.Log("Estrellas creadas en " + carpeta);
    }

    private static void Generar(string ruta, Color relleno, Color borde)
    {
        const int tam = 15; // 13 de la estrella + 1 de margen por lado
        bool[,] hay = new bool[tam, tam];

        for (int fila = 0; fila < molde.Length; fila++)
            for (int col = 0; col < molde[fila].Length; col++)
                hay[col + 1, fila + 1] = molde[fila][col] == '#';

        Texture2D tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        for (int x = 0; x < tam; x++)
        {
            for (int y = 0; y < tam; y++)
            {
                Color c = Color32Transparente();
                if (hay[x, y]) c = relleno;
                else if (Vecino(hay, x, y, tam)) c = borde;
                tex.SetPixel(x, tam - 1 - y, c);
            }
        }
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    private static bool Vecino(bool[,] hay, int x, int y, int tam)
    {
        if (x > 0 && hay[x - 1, y]) return true;
        if (x < tam - 1 && hay[x + 1, y]) return true;
        if (y > 0 && hay[x, y - 1]) return true;
        if (y < tam - 1 && hay[x, y + 1]) return true;
        return false;
    }

    private static Color Color32Transparente() { return new Color(0, 0, 0, 0); }

    private static Color Color(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    private static void Configurar(string ruta)
    {
        TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 16;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
    }
}