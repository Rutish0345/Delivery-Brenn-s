using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// El marcador del pedido, la flecha guía y la entrega del producto para cualquier nivel (lo usan ConstruirMapa y
// los constructores de los niveles 2 y 3). El marcador:
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

    // Sprite pixel art: único, PPU 32, Point, sin mipmaps ni compresión y con el pivote dado
    static void ImportarSprite(string ruta, Vector2 pivote)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(ruta) ?? throw new Exception($"No encuentro {ruta}.");
        var ajustes = new TextureImporterSettings();
        imp.ReadTextureSettings(ajustes);
        bool cambia = imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode != SpriteImportMode.Single ||
                      imp.spritePixelsPerUnit != PPU || imp.filterMode != FilterMode.Point || imp.mipmapEnabled ||
                      imp.textureCompression != TextureImporterCompression.Uncompressed ||
                      ajustes.spriteAlignment != (int)SpriteAlignment.Custom || imp.spritePivot != pivote;
        if (!cambia) return;
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

    // Importa los PNG del marcador y deja el catálogo de productos al día. Devuelve el catálogo.
    public static CatalogoProductos PrepararArte()
    {
        var pngs = AssetDatabase.FindAssets("t:Texture2D", new[] { Carpeta }).Select(AssetDatabase.GUIDToAssetPath).ToList();
        if (pngs.Count == 0) throw new Exception($"No hay arte en {Carpeta}: corre pixel_town/generate_marcador_pedido.py.");
        foreach (var ruta in pngs)
            ImportarSprite(ruta, ruta.EndsWith("burbuja_pedido.png") ? new Vector2(0.5f, 0.5f / 44f) : new Vector2(0.5f, 0.5f));

        var catalogo = AssetDatabase.LoadAssetAtPath<CatalogoProductos>(RutaCatalogo);
        if (catalogo == null)
        {
            catalogo = ScriptableObject.CreateInstance<CatalogoProductos>();
            AssetDatabase.CreateAsset(catalogo, RutaCatalogo);
        }
        var so = new SerializedObject(catalogo);
        var iconos = so.FindProperty("iconos");
        iconos.arraySize = Productos.Length;
        for (int i = 0; i < Productos.Length; i++)
            iconos.GetArrayElementAtIndex(i).objectReferenceValue = SpriteObligatorio("producto_" + Productos[i]);
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

    // ------------------------------------------------------------------ flecha guía y entrega del producto
    public const string NombreFlecha = "FlechaPedido", NombreEntrega = "EntregaProducto";
    public const int OrdenFlecha = 90, OrdenEntrega = 95;
    // chevron dorado opaco con doble borde de pixel_town/generate_flecha.py; flecha_guia.png es la del HUD y no se toca
    const string RutaFlecha = "Assets/Sprits/UI/flecha_chevron.png";
    const string RutaUnlitFlecha = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    const float EscalaFlecha = 1f, EscalaBorde = 0.6f;
    const string NombreProductoMochilaViejo = "ProductoMochila";   // el producto que asomaba (quitado el 2026-10-21)

    static void BorrarRaices(Func<GameObject, bool> cual)
    {
        var escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var go in escena.GetRootGameObjects().Where(cual).ToList()) Undo.DestroyObjectImmediate(go);
    }

    // FlechaPedido (FlechaGuia) con los hijos flecha_guia (orbita al repartidor y apunta al pedido activo) y flecha_borde
    // (en la orilla de la cámara cuando el pedido no se ve). Unlit en los 3 niveles: no la apagan la noche, la lluvia
    // ni el atardecer; color blanco con alfa 1 (el dorado del sprite tal cual).
    public static void CrearFlecha()
    {
        BorrarRaices(g => g.name == NombreFlecha || g.name == "FlechaGuia" || g.GetComponentInChildren<FlechaGuia>(true) != null);
        ImportarSprite(RutaFlecha, new Vector2(0.5f, 0.5f));
        var sprite = AssetDatabase.LoadAllAssetsAtPath(RutaFlecha).OfType<Sprite>().FirstOrDefault()
                     ?? throw new Exception($"Falta el sprite de {RutaFlecha}: corre pixel_town/generate_flecha.py.");
        var unlit = AssetDatabase.LoadAssetAtPath<Material>(RutaUnlitFlecha) ?? throw new Exception($"No encuentro {RutaUnlitFlecha}.");
        var raiz = new GameObject(NombreFlecha);
        Undo.RegisterCreatedObjectUndo(raiz, "Crear flecha guía");
        SpriteRenderer Parte(string nombre, float escala)
        {
            var hijo = new GameObject(nombre);
            hijo.transform.SetParent(raiz.transform, false);
            hijo.transform.localScale = Vector3.one * escala;
            var sr = hijo.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = unlit;
            sr.color = Color.white;
            sr.sortingOrder = OrdenFlecha;  // sobre suelo y props; debajo de la entrega (95), la burbuja y el texto de choque (100) y el globo (200)
            sr.enabled = false;             // FlechaGuia la prende cuando hay pedido y estás lejos (o no se ve)
            return sr;
        }
        var orbita = Parte("flecha_guia", EscalaFlecha);
        var borde = Parte("flecha_borde", EscalaBorde);
        var so = new SerializedObject(raiz.AddComponent<FlechaGuia>());
        so.FindProperty("orbita").objectReferenceValue = orbita;
        so.FindProperty("borde").objectReferenceValue = borde;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // EntregaProducto con el hijo Producto (el ícono que vuela de la mochila a la clienta)
    public static void CrearEntrega()
    {
        var catalogo = Catalogo() ?? throw new Exception("Falta el catálogo de productos: llama antes a PrepararArte().");
        BorrarRaices(g => g.name == NombreEntrega || g.GetComponentInChildren<EntregaProducto>(true) != null);
        var raiz = new GameObject(NombreEntrega);
        Undo.RegisterCreatedObjectUndo(raiz, "Crear entrega del producto");
        var producto = Hijo("Producto", raiz.transform, Vector3.zero, catalogo.Icono(0), OrdenEntrega);
        producto.enabled = false;
        var so = new SerializedObject(raiz.AddComponent<EntregaProducto>());
        so.FindProperty("catalogo").objectReferenceValue = catalogo;
        so.FindProperty("producto").objectReferenceValue = producto;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // La mochila queda limpia: fuera el hijo ProductoMochila y el SortingGroup que se le puso para él
    public static void LimpiarMochila(PlayerController jugador)
    {
        foreach (Transform viejo in jugador.transform.Cast<Transform>().Where(t => t.name == NombreProductoMochilaViejo).ToList())
            Undo.DestroyObjectImmediate(viejo.gameObject);
        if (jugador.TryGetComponent(out UnityEngine.Rendering.SortingGroup grupo)) Undo.DestroyObjectImmediate(grupo);
    }

    public static string RevisarFlechaYEntrega(List<string> errores)
    {
        int antes = errores.Count;
        var flechas = Object.FindObjectsByType<FlechaGuia>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (flechas.Length != 1) errores.Add($"Hay {flechas.Length} FlechaGuia en la escena, debe haber una.");
        else
        {
            var f = flechas[0];
            var so = new SerializedObject(f);
            if (f.name != NombreFlecha || f.transform.parent != null || !f.gameObject.activeSelf || !f.enabled)
                errores.Add($"La flecha debe ser el objeto raíz {NombreFlecha}, activo y con FlechaGuia prendido.");
            foreach (var (campo, nombre, escala) in new[] { ("orbita", "flecha_guia", EscalaFlecha), ("borde", "flecha_borde", EscalaBorde) })
            {
                var sr = so.FindProperty(campo).objectReferenceValue as SpriteRenderer;
                if (sr == null || sr.name != nombre || sr.transform.parent != f.transform || sr.sprite == null || sr.sprite.texture == null ||
                    AssetDatabase.GetAssetPath(sr.sprite) != RutaFlecha || sr.sortingLayerID != 0 || sr.sortingOrder != OrdenFlecha ||
                    Mathf.Abs(sr.transform.localScale.x - escala) > 1e-4f)
                    errores.Add($"La flecha: falta {nombre} con el chevron en Default / {OrdenFlecha} y escala {escala}.");
                else if (sr.sharedMaterial == null || sr.sharedMaterial.name != "Sprite-Unlit-Default" || sr.color != Color.white)
                    errores.Add($"La flecha: {nombre} debe ir Unlit y opaca ({sr.sharedMaterial?.name}, color {sr.color}).");
                else if (sr.sprite.rect.width != 24) errores.Add($"La flecha: {nombre} no es el chevron dorado de 24 px (corre pixel_town/generate_flecha.py).");
            }
        }
        var entregas = Object.FindObjectsByType<EntregaProducto>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (entregas.Length != 1) errores.Add($"Hay {entregas.Length} EntregaProducto en la escena, debe haber uno.");
        else
        {
            var so = new SerializedObject(entregas[0]);
            var sr = so.FindProperty("producto").objectReferenceValue as SpriteRenderer;
            if (so.FindProperty("catalogo").objectReferenceValue != Catalogo()) errores.Add("EntregaProducto no usa el catálogo de productos.");
            if (sr == null || sr.transform.parent != entregas[0].transform || sr.sortingOrder != OrdenEntrega)
                errores.Add($"EntregaProducto no tiene su hijo Producto en Order in Layer {OrdenEntrega}.");
        }
        var jugador = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (jugador != null && (jugador.transform.Find(NombreProductoMochilaViejo) != null || jugador.GetComponent<UnityEngine.Rendering.SortingGroup>() != null))
            errores.Add("El repartidor todavía tiene el producto de la mochila (o su SortingGroup).");
        return errores.Count == antes
            ? $"Flecha guía ({NombreFlecha}: chevron dorado opaco y Unlit que orbita y el de la orilla al {EscalaBorde:P0}, Order in Layer {OrdenFlecha}) y entrega del producto ({NombreEntrega}, {OrdenEntrega}); mochila limpia."
            : "Flecha guía / entrega del producto: con errores";
    }

    public static string RevisarTodos(List<string> errores)
    {
        var puntos = Object.FindObjectsByType<DeliveryPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int bien = puntos.Count(p => Revisar(p, errores));
        return $"Marcadores: {bien} de {puntos.Length} puntos con la burbuja del producto (Order in Layer {OrdenBurbuja}) " +
               $"y el aro (Order in Layer {OrdenAro}).";
    }
}
