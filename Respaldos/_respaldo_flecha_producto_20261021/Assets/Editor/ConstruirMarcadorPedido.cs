using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// El marcador del pedido para cualquier nivel (lo usan ConstruirMapa y los constructores de los niveles 2 y 3):
//   PuntoEntrega_N
//    └ MarcadorPedido      (primer hijo, en el centro del punto: DeliveryPoint lo prende con el pedido)
//       ├ Aro              aro del suelo, Order in Layer -5 (encima del suelo, debajo de la gente)
//       └ Burbuja          globo crema, Order in Layer 100 (encima de techos y árboles), punta a AlturaBurbuja
//          └ Producto      ícono del pedido, Order in Layer 101, centrado en el cuerpo de la burbuja
// Arte: pixel_town/generate_marcador_pedido.py → Assets/Sprits/Comun/Marcador (Point, sin compresión, PPU 32).
public static class ConstruirMarcadorPedido
{
    public const string Carpeta = "Assets/Sprits/Comun/Marcador";
    public const string RutaCatalogo = Carpeta + "/Productos.asset";
    public const string NombreMarcador = "MarcadorPedido";
    public const int OrdenAro = -5, OrdenBurbuja = 100, OrdenProducto = 101;
    const int PPU = 32;
    const float AlturaBurbuja = 0.5f;              // la punta de la colita, sobre el centro del punto
    const float CentroCuerpo = 26f / PPU;          // el ícono de 16x16 en el cuerpo de la burbuja (26 px sobre la punta)
    static readonly string[] Productos = { "esmalte", "labial", "tijeras", "crema", "tinte", "accesorio" };   // orden del enum

    // Importa los PNG del marcador y deja el catálogo de productos al día. Devuelve el catálogo.
    public static CatalogoProductos PrepararArte()
    {
        var pngs = AssetDatabase.FindAssets("t:Texture2D", new[] { Carpeta }).Select(AssetDatabase.GUIDToAssetPath).ToList();
        if (pngs.Count == 0) throw new Exception($"No hay arte en {Carpeta}: corre pixel_town/generate_marcador_pedido.py.");
        foreach (var ruta in pngs)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
            var nombre = System.IO.Path.GetFileName(ruta);
            var pivote = nombre == "burbuja_pedido.png" ? new Vector2(0.5f, 0.5f / 44f)
                       : nombre.StartsWith("asoma_") ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);
            var ajustes = new TextureImporterSettings();
            imp.ReadTextureSettings(ajustes);
            bool cambia = imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode != SpriteImportMode.Single ||
                          imp.spritePixelsPerUnit != PPU || imp.filterMode != FilterMode.Point || imp.mipmapEnabled ||
                          imp.textureCompression != TextureImporterCompression.Uncompressed ||
                          ajustes.spriteAlignment != (int)SpriteAlignment.Custom || imp.spritePivot != pivote;
            if (!cambia) continue;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = PPU;
            imp.filterMode = FilterMode.Point;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.ReadTextureSettings(ajustes);
            ajustes.spriteAlignment = (int)SpriteAlignment.Custom;
            ajustes.spritePivot = pivote;
            ajustes.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(ajustes);
            imp.SaveAndReimport();
        }

        var catalogo = AssetDatabase.LoadAssetAtPath<CatalogoProductos>(RutaCatalogo);
        if (catalogo == null)
        {
            catalogo = ScriptableObject.CreateInstance<CatalogoProductos>();
            AssetDatabase.CreateAsset(catalogo, RutaCatalogo);
        }
        var so = new SerializedObject(catalogo);
        var iconos = so.FindProperty("iconos");
        iconos.arraySize = Productos.Length;
        var asomando = so.FindProperty("asomando");
        asomando.arraySize = Productos.Length;
        for (int i = 0; i < Productos.Length; i++)
        {
            iconos.GetArrayElementAtIndex(i).objectReferenceValue = SpriteObligatorio("producto_" + Productos[i]);
            asomando.GetArrayElementAtIndex(i).objectReferenceValue = SpriteObligatorio("asoma_" + Productos[i]);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        return catalogo;
    }

    public static CatalogoProductos Catalogo() => AssetDatabase.LoadAssetAtPath<CatalogoProductos>(RutaCatalogo);

    static Sprite SpriteObligatorio(string nombre) =>
        AssetDatabase.LoadAssetAtPath<Sprite>($"{Carpeta}/{nombre}.png")
        ?? throw new Exception($"Falta {Carpeta}/{nombre}.png (o no está importado como Sprite).");

    static Sprite[] Aro(string color) => Enumerable.Range(0, 3).Select(i => SpriteObligatorio($"aro_{color}_{i}")).ToArray();

    // Crea el marcador como primer hijo del punto (se llama con el punto recién creado o para reemplazar uno viejo)
    public static GameObject Crear(Transform punto)
    {
        var catalogo = Catalogo() ?? throw new Exception("Falta el catálogo de productos: llama antes a PrepararArte().");
        var marcador = new GameObject(NombreMarcador);
        Undo.RegisterCreatedObjectUndo(marcador, "Crear marcador del pedido");
        marcador.transform.SetParent(punto, false);
        marcador.transform.SetSiblingIndex(0);

        var aro = Hijo("Aro", marcador.transform, Vector3.zero, SpriteObligatorio("aro_dorado_0"), OrdenAro);
        var burbuja = Hijo("Burbuja", marcador.transform, new Vector3(0, AlturaBurbuja, 0), SpriteObligatorio("burbuja_pedido"), OrdenBurbuja);
        var producto = Hijo("Producto", burbuja.transform, new Vector3(0, CentroCuerpo, 0), catalogo.Icono(0), OrdenProducto);

        var so = new SerializedObject(marcador.AddComponent<MarcadorPedido>());
        so.FindProperty("catalogo").objectReferenceValue = catalogo;
        so.FindProperty("burbuja").objectReferenceValue = burbuja;
        so.FindProperty("producto").objectReferenceValue = producto;
        so.FindProperty("aro").objectReferenceValue = aro;
        Lista(so.FindProperty("aroNormal"), Aro("dorado"));
        Lista(so.FindProperty("aroListo"), Aro("verde"));
        so.ApplyModifiedPropertiesWithoutUndo();
        marcador.SetActive(false);   // DeliveryPoint lo prende cuando su pedido está activo
        return marcador;
    }

    static SpriteRenderer Hijo(string nombre, Transform padre, Vector3 pos, Sprite sprite, int orden)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = orden;
        return sr;
    }

    static void Lista(SerializedProperty prop, Sprite[] sprites)
    {
        prop.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }

    // El marcador de un punto: único hijo, en el centro, con sus dos partes en su orden de dibujo, sin
    // BrilloEstrellas, y el radio de entrega intacto. Devuelve true si todo está bien.
    public static bool Revisar(DeliveryPoint p, List<string> errores)
    {
        int antes = errores.Count;
        if (p.transform.childCount != 1) { errores.Add($"{p.name}: tiene {p.transform.childCount} hijos, debe tener solo el marcador."); return false; }
        var m = p.transform.GetChild(0);
        var marcador = m.GetComponent<MarcadorPedido>();
        if (m.name != NombreMarcador) errores.Add($"{p.name}: el marcador se llama {m.name}, no {NombreMarcador}.");
        if (m.localPosition != Vector3.zero || m.localScale != Vector3.one) errores.Add($"{p.name}: el marcador no está en el centro del punto con escala 1.");
        if (marcador == null) { errores.Add($"{p.name}: el marcador no tiene MarcadorPedido."); return false; }
        if (m.GetComponentsInChildren<Component>(true).Any(c => c == null || c.GetType().Name == "BrilloEstrellas"))
            errores.Add($"{p.name}: el marcador todavía tiene BrilloEstrellas (o un script perdido).");
        var so = new SerializedObject(marcador);
        var burbuja = so.FindProperty("burbuja").objectReferenceValue as SpriteRenderer;
        var producto = so.FindProperty("producto").objectReferenceValue as SpriteRenderer;
        var aro = so.FindProperty("aro").objectReferenceValue as SpriteRenderer;
        if (so.FindProperty("catalogo").objectReferenceValue != Catalogo()) errores.Add($"{p.name}: el marcador no usa {RutaCatalogo}.");
        void Parte(SpriteRenderer sr, string nombre, Transform padre, int orden)
        {
            if (sr == null || sr.name != nombre || sr.transform.parent != padre) errores.Add($"{p.name}: falta {nombre} bajo {padre?.name}.");
            else if (sr.sprite == null || sr.sortingLayerID != 0 || sr.sortingOrder != orden || sr.transform.localScale != Vector3.one)
                errores.Add($"{p.name}/{nombre}: sin sprite o no está en Default / {orden} con escala 1.");
        }
        Parte(aro, "Aro", m, OrdenAro);
        Parte(burbuja, "Burbuja", m, OrdenBurbuja);
        Parte(producto, "Producto", burbuja != null ? burbuja.transform : null, OrdenProducto);
        foreach (var lista in new[] { "aroNormal", "aroListo" })
        {
            var arr = so.FindProperty(lista);
            if (arr.arraySize != 3 || Enumerable.Range(0, 3).Any(i => arr.GetArrayElementAtIndex(i).objectReferenceValue == null))
                errores.Add($"{p.name}: {lista} no tiene sus 3 cuadros.");
        }
        if (Mathf.Abs(new SerializedObject(p).FindProperty("radioEntrega").floatValue - 2.2f) > 1e-4f) errores.Add($"{p.name}: el radio de entrega cambió.");
        return errores.Count == antes;
    }

    // ------------------------------------------------------------------ producto en la mochila
    public const string NombreProductoMochila = "ProductoMochila";
    const string HojaRepartidor = "Assets/Sprits/Personajes/repartidor_sheet.png";
    const int AnchoCelda = 56, AltoCelda = 80;

    // Los 5 cuadros de una fila de la hoja del repartidor (0 frente, 1 espalda, 2 lado), por su rect
    static Sprite[] FilaRepartidor(int fila)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(HojaRepartidor).OfType<Sprite>()
            .Where(s => Mathf.RoundToInt(s.rect.y) == (2 - fila) * AltoCelda)
            .OrderBy(s => s.rect.x).ToArray();
        if (sprites.Length != 5) throw new Exception($"La fila {fila} de {HojaRepartidor} tiene {sprites.Length} sprites, no 5.");
        return sprites;
    }

    // El repartidor con SortingGroup (se ordena por sus pies, igual que su sprite) y el hijo ProductoMochila
    public static void CrearProductoMochila(PlayerController jugador)
    {
        var catalogo = Catalogo() ?? throw new Exception("Falta el catálogo de productos: llama antes a PrepararArte().");
        var cuerpo = jugador.GetComponent<SpriteRenderer>();
        // (sin ??: GetComponent devuelve un "null" de Unity que ?? no reconoce)
        if (!jugador.TryGetComponent(out UnityEngine.Rendering.SortingGroup grupo))
            grupo = Undo.AddComponent<UnityEngine.Rendering.SortingGroup>(jugador.gameObject);
        grupo.sortingLayerID = cuerpo.sortingLayerID;
        grupo.sortingOrder = cuerpo.sortingOrder;
        foreach (Transform viejo in jugador.transform.Cast<Transform>().Where(t => t.name == NombreProductoMochila).ToList())
            Undo.DestroyObjectImmediate(viejo.gameObject);

        var go = new GameObject(NombreProductoMochila);
        Undo.RegisterCreatedObjectUndo(go, "Crear producto de la mochila");
        go.transform.SetParent(jugador.transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = catalogo.Asomando(0);
        sr.sortingLayerID = cuerpo.sortingLayerID;
        sr.sortingOrder = cuerpo.sortingOrder - 1;
        sr.enabled = false;
        var so = new SerializedObject(go.AddComponent<ProductoMochila>());
        so.FindProperty("catalogo").objectReferenceValue = catalogo;
        so.FindProperty("cuerpo").objectReferenceValue = cuerpo;
        Lista(so.FindProperty("frente"), FilaRepartidor(0));
        Lista(so.FindProperty("espalda"), FilaRepartidor(1));
        Lista(so.FindProperty("lado"), FilaRepartidor(2));
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static string RevisarProductoMochila(List<string> errores)
    {
        var jugadores = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (jugadores.Length != 1) { errores.Add($"Hay {jugadores.Length} PlayerController en la escena."); return "Mochila: sin revisar"; }
        var j = jugadores[0];
        int antes = errores.Count;
        if (j.GetComponent<UnityEngine.Rendering.SortingGroup>() == null) errores.Add("El repartidor no tiene SortingGroup.");
        var hijos = j.GetComponentsInChildren<ProductoMochila>(true);
        if (hijos.Length != 1 || hijos[0].transform.parent != j.transform || hijos[0].name != NombreProductoMochila)
            errores.Add($"El repartidor debe tener un solo hijo {NombreProductoMochila} con ProductoMochila (hay {hijos.Length}).");
        else
        {
            var so = new SerializedObject(hijos[0]);
            if (so.FindProperty("catalogo").objectReferenceValue != Catalogo()) errores.Add("ProductoMochila no usa el catálogo de productos.");
            if (so.FindProperty("cuerpo").objectReferenceValue != j.GetComponent<SpriteRenderer>()) errores.Add("ProductoMochila no apunta al sprite del repartidor.");
            foreach (var (campo, fila) in new[] { ("frente", 0), ("espalda", 1), ("lado", 2) })
            {
                var arr = so.FindProperty(campo);
                var esperados = FilaRepartidor(fila);
                if (arr.arraySize != 5 || Enumerable.Range(0, 5).Any(i => arr.GetArrayElementAtIndex(i).objectReferenceValue != esperados[i]))
                    errores.Add($"ProductoMochila: \"{campo}\" no tiene los 5 cuadros de su fila en orden.");
            }
        }
        return errores.Count == antes ? "Mochila: el repartidor tiene SortingGroup y ProductoMochila con sus 15 cuadros." : "Mochila: con errores";
    }

    public static string RevisarTodos(List<string> errores)
    {
        var puntos = Object.FindObjectsByType<DeliveryPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int bien = puntos.Count(p => Revisar(p, errores));
        return $"Marcadores: {bien} de {puntos.Length} puntos con la burbuja del producto (Order in Layer {OrdenBurbuja}) " +
               $"y el aro (Order in Layer {OrdenAro}).";
    }
}
