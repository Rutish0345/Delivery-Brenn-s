using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Arma el mapa Residencial dentro de Unity a partir de lo que exporta pixel_town/generate_residencial_grande.py
// (128x64 tiles, mismo tileset_suelo y props_atlas que generate_unity.py; los props nuevos del mapa grande
// van agregados al final del atlas y los que ya existían conservan su lugar y su ID).
// Se puede ejecutar las veces que haga falta: corta de nuevo las texturas, actualiza los tiles,
// repinta el suelo, reemplaza los props, el borde del mapa y los puntos de entrega, y mueve al repartidor al spawn.
public static class ConstruirMapa
{
    // De aquí salen los JSON (los genera el script de Python)
    const string CarpetaDatos = "C:/Users/kangu/pixel_town/unity/residencial";

    // Las mismas imágenes, ya importadas en el proyecto
    const string RutaTileset = "Assets/Tiles/tileset_suelo.png";
    const string RutaAtlas = "Assets/Sprits/Escenarios/Residencial/props_atlas.png";
    const string CarpetaTiles = "Assets/Tiles/Residencial";
    const string ArchivoMapa = "mapa_residencial_grande.json";

    // Círculo del paquete 2D Sprite (el mismo que usaban los PuntoEntrega_1 hechos a mano)
    const string GuidCirculo = "a86470a33a6bf42c4b3595704624658b";
    static readonly Color RosaEntrega = new Color32(0xFF, 0x6F, 0xA5, 0xFF);
    const int OrdenCirculo = 100;
    const float GrosorBorde = 2f;

    const int PPU = 32;
    const int OrdenSuelo = -10;
    static readonly Vector2 PivoteProps = new Vector2(0.5f, 0f);   // abajo-centro, igual que el JSON

    // Captura de lo que pinta Unity, para compararla con mapa_referencia_grande.png
    const string ArchivoCaptura = "captura_unity.png";

    // Franja de colisión de cada tipo de prop, medida en píxeles sobre el atlas:
    // tramos horizontales (desde el borde izquierdo del sprite, ambos inclusive) y el alto desde abajo.
    // Se usa el primer prefijo que coincida, por eso el portón va antes que "barda".
    static readonly (string prefijo, (int x0, int x1)[] tramos, int alto)[] Colisiones =
    {
        ("casa_",             new[] { (4, 155) },          16),
        ("arbol_grande",      new[] { (24, 31) },           8),
        ("arbol_mediano",     new[] { (18, 25) },           8),
        ("pino",              new[] { (17, 21) },           6),
        ("farol",             new[] { (2, 13) },            7),
        ("barda_porton",      new[] { (1, 8), (55, 62) },   8),   // solo los postes: el hueco se puede cruzar
        ("barda_poste_final", new[] { (1, 8) },             8),
        ("barda",             new[] { (0, 31) },            8),
        ("buzon_",            new[] { (7, 10) },            6),
        // props agregados para el mapa grande
        ("empresa_brenns",    new[] { (4, 315) },          22),
        ("carro_lateral_",    new[] { (2, 93) },           16),
        ("carro_frontal_",    new[] { (4, 47) },           24),
        ("cerca_baja_porton", new[] { (1, 8), (87, 94) },   6),   // solo los postes: el portón se cruza
        ("cerca_baja_poste",  new[] { (0, 7) },             6),
        ("cerca_baja",        new[] { (0, 31) },            5),
        ("lago_parque",       new[] { (6, 242) },         104),   // todo el lago, no se puede entrar
        ("banca_parque",      new[] { (1, 44) },            7),
        ("hidrante",          new[] { (2, 15) },            6),
        ("bote_basura",       new[] { (2, 15) },            6),
        ("cajas_apiladas",    new[] { (0, 43) },           10),
        ("caja_carga",        new[] { (0, 19) },            8),
    };

    struct DefSprite
    {
        public string nombre;
        public Rect rect;
        public Vector2 pivote;
    }

    [MenuItem("Delivery Brenn's/Construir mapa Residencial")]
    static void Construir()
    {
        try
        {
            ConstruirTodo();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Construir mapa Residencial", e.Message, "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("Delivery Brenn's/Capturar mapa Residencial (PNG)")]
    static void CapturarMenu()
    {
        try
        {
            EditorUtility.RevealInFinder(Capturar(LeerJson(ArchivoMapa)));
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Capturar mapa Residencial", e.Message, "OK");
        }
    }

    static string ConstruirTodo()
    {
        var tileset = LeerJson("tileset_suelo.json");
        var atlas = LeerJson("props_atlas.json");
        var mapa = LeerJson(ArchivoMapa);

        EditorUtility.DisplayProgressBar("Mapa Residencial", "Cortando texturas…", 0.1f);
        CortarTextura(RutaTileset, DefsTileset(tileset, RutaTileset));
        CortarTextura(RutaAtlas, DefsAtlas(atlas, RutaAtlas));

        EditorUtility.DisplayProgressBar("Mapa Residencial", "Creando tiles…", 0.4f);
        var tiles = CrearTiles(CargarSprites(RutaTileset));

        EditorUtility.DisplayProgressBar("Mapa Residencial", "Pintando el suelo…", 0.6f);
        var suelo = PintarSuelo(mapa, tiles);

        EditorUtility.DisplayProgressBar("Mapa Residencial", "Colocando props…", 0.8f);
        var origen = suelo.transform.position;
        int colocados = ColocarProps(mapa, CargarSprites(RutaAtlas), origen);

        EditorUtility.DisplayProgressBar("Mapa Residencial", "Borde, repartidor y puntos de entrega…", 0.9f);
        CrearBorde(mapa, origen);
        ColocarRepartidor(mapa, origen);
        int puntos = CrearPuntosEntrega(mapa, origen);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        int dinamicos = Lista(mapa, "dinamicos").Count;
        string resumen = $"Mapa Residencial listo ({Num(mapa["columnas"])}x{Num(mapa["filas"])} tiles): " +
                         $"{Lista(atlas, "sprites").Count} sprites en props_atlas, {colocados} props, " +
                         $"{Lista(mapa, "casas").Count} casas y {puntos} puntos de entrega. " +
                         $"{dinamicos} dinámicos (autos y baches) quedan en el JSON para colocarlos por código.";
        Debug.Log(resumen);
        RevisarOrdenPorY();
        return resumen;
    }

    // ------------------------------------------------------------------ texturas

    static List<DefSprite> DefsTileset(Dictionary<string, object> json, string ruta)
    {
        int alto = AltoTextura(ruta);
        return Lista(json, "tiles").Cast<Dictionary<string, object>>()
            .Select(t => new DefSprite
            {
                nombre = (string)t["name"],
                rect = RectDesdeArriba(t, alto),
                pivote = new Vector2(0.5f, 0.5f),   // el Tilemap centra el sprite en la celda
            }).ToList();
    }

    static List<DefSprite> DefsAtlas(Dictionary<string, object> json, string ruta)
    {
        int alto = AltoTextura(ruta);
        if ((int)Num(json["alto"]) != alto)
            throw new Exception($"{ruta} mide {alto} px de alto pero props_atlas.json dice {Num(json["alto"])}. " +
                                "Copia el props_atlas.png actual del generador.");
        var piv = Lista(json, "pivote");
        if (new Vector2((float)Num(piv[0]), (float)Num(piv[1])) != PivoteProps)
            throw new Exception($"props_atlas.json trae pivote ({Num(piv[0])}, {Num(piv[1])}); los props usan abajo-centro (0.5, 0).");
        return Lista(json, "sprites").Cast<Dictionary<string, object>>()
            .Select(s => new DefSprite { nombre = (string)s["name"], rect = RectDesdeArriba(s, alto), pivote = PivoteProps })
            .ToList();
    }

    // Los JSON miden desde arriba-izquierda; Unity mide los rectángulos desde abajo-izquierda.
    static Rect RectDesdeArriba(Dictionary<string, object> d, int altoTextura)
    {
        float x = (float)Num(d["x"]), y = (float)Num(d["y"]), w = (float)Num(d["w"]), h = (float)Num(d["h"]);
        return new Rect(x, altoTextura - y - h, w, h);
    }

    static int AltoTextura(string ruta)
    {
        var imp = Importador(ruta);
        imp.GetSourceTextureWidthAndHeight(out _, out int alto);
        return alto;
    }

    static TextureImporter Importador(string ruta)
    {
        var imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (imp == null) throw new Exception($"No encuentro la textura {ruta} en el proyecto.");
        return imp;
    }

    static void CortarTextura(string ruta, List<DefSprite> defs)
    {
        // 1) Ajustes de importación para pixel art
        var imp = Importador(ruta);
        var ajustes = new TextureImporterSettings();
        imp.ReadTextureSettings(ajustes);
        ajustes.textureType = TextureImporterType.Sprite;
        ajustes.spriteMode = (int)SpriteImportMode.Multiple;
        ajustes.spritePixelsPerUnit = PPU;
        ajustes.filterMode = FilterMode.Point;
        ajustes.mipmapEnabled = false;
        ajustes.alphaIsTransparency = true;
        ajustes.spriteMeshType = SpriteMeshType.FullRect;   // sin recortes de malla que dejen huecos
        imp.SetTextureSettings(ajustes);
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        foreach (var plataforma in new[] { "Standalone", "Android", "WebGL" })
        {
            var ps = imp.GetPlatformTextureSettings(plataforma);
            if (!ps.overridden) continue;
            ps.textureCompression = TextureImporterCompression.Uncompressed;
            ps.format = TextureImporterFormat.RGBA32;
            imp.SetPlatformTextureSettings(ps);
        }
        imp.SaveAndReimport();

        // 2) Nombres, rectángulos y pivotes: se reemplazan TODOS los recortes por los del JSON (los que
        //    sobren del atlas viejo se borran). Se reaprovecha el ID del sprite anterior con el mismo
        //    nombre para que no se rompan las referencias que ya existían; el rectángulo solo sirve para
        //    reconocer un sprite renombrado, nunca uno cuyo nombre sigue en el JSON.
        var fabrica = new SpriteDataProviderFactories();
        fabrica.Init();
        var datos = fabrica.GetSpriteEditorDataProviderFromObject(imp);
        datos.InitSpriteEditorDataProvider();
        var previos = datos.GetSpriteRects().ToList();
        var nombres = new HashSet<string>(defs.Select(d => d.nombre));
        var usados = new HashSet<GUID>();
        var nuevos = new List<SpriteRect>();
        foreach (var def in defs)
        {
            var sr = previos.FirstOrDefault(p => !usados.Contains(p.spriteID) && p.name == def.nombre)
                  ?? previos.FirstOrDefault(p => !usados.Contains(p.spriteID) && !nombres.Contains(p.name) && p.rect == def.rect)
                  ?? new SpriteRect { spriteID = GUID.Generate() };
            usados.Add(sr.spriteID);
            sr.name = def.nombre;
            sr.rect = def.rect;
            sr.alignment = SpriteAlignment.Custom;
            sr.pivot = def.pivote;
            sr.border = Vector4.zero;
            nuevos.Add(sr);
        }
        datos.SetSpriteRects(nuevos.ToArray());
        datos.GetDataProvider<ISpriteNameFileIdDataProvider>()
             .SetNameFileIdPairs(nuevos.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
        datos.Apply();
        imp.SaveAndReimport();

        // 3) Se comprueba lo que quedó guardado en el .meta: si no coincide con el JSON el mapa saldría mal cortado
        imp = Importador(ruta);
        if (imp.spriteImportMode != SpriteImportMode.Multiple || imp.filterMode != FilterMode.Point ||
            !Mathf.Approximately(imp.spritePixelsPerUnit, PPU))
            throw new Exception($"{ruta} no quedó como Sprite Multiple, PPU {PPU}, Filter Point.");
        var revisar = fabrica.GetSpriteEditorDataProviderFromObject(imp);
        revisar.InitSpriteEditorDataProvider();
        var guardados = revisar.GetSpriteRects().ToDictionary(s => s.name);
        var mal = defs.Where(d => !guardados.TryGetValue(d.nombre, out var g) || g.rect != d.rect ||
                                  Vector2.Distance(g.pivot, d.pivote) > 1e-4f).Select(d => d.nombre).ToList();
        if (mal.Count > 0 || guardados.Count != defs.Count)
            throw new Exception($"Los recortes de {ruta} no coinciden con el JSON ({guardados.Count} en el .meta, " +
                                $"{defs.Count} en el JSON). Mal: {string.Join(", ", mal)}");
        int cargados = AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>().Count();
        if (cargados != defs.Count)
            throw new Exception($"{ruta}: Unity cargó {cargados} sprites y el JSON tiene {defs.Count}.");
        Debug.Log($"{ruta}: {defs.Count} sprites recortados con los rectángulos del JSON.");
    }

    static Dictionary<string, Sprite> CargarSprites(string ruta)
    {
        return AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>().ToDictionary(s => s.name);
    }

    // ------------------------------------------------------------------ tiles y suelo

    static Dictionary<string, TileBase> CrearTiles(Dictionary<string, Sprite> sprites)
    {
        if (!AssetDatabase.IsValidFolder(CarpetaTiles))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(CarpetaTiles).Replace('\\', '/'), Path.GetFileName(CarpetaTiles));

        var tiles = new Dictionary<string, TileBase>();
        foreach (var par in sprites)
        {
            string ruta = $"{CarpetaTiles}/{par.Key}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(ruta);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, ruta);
            }
            tile.sprite = par.Value;
            tile.colliderType = Tile.ColliderType.None;   // el suelo no choca; los props llevan su collider
            EditorUtility.SetDirty(tile);
            tiles[par.Key] = tile;
        }
        AssetDatabase.SaveAssets();
        return tiles;
    }

    static Tilemap PintarSuelo(Dictionary<string, object> mapa, Dictionary<string, TileBase> tiles)
    {
        // Reutiliza el Grid y el Tilemap "Suelo" de la escena si ya existen
        var grid = UnityEngine.Object.FindObjectsByType<Grid>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                       .OrderByDescending(g => g.name == "Grid").FirstOrDefault();
        if (grid == null)
        {
            grid = new GameObject("Grid", typeof(Grid)).GetComponent<Grid>();
            Undo.RegisterCreatedObjectUndo(grid.gameObject, "Construir mapa Residencial");
        }
        Undo.RecordObject(grid, "Construir mapa Residencial");
        grid.cellSize = new Vector3(1, 1, 0);   // 1 unidad = 32 px con PPU 32

        var suelo = grid.GetComponentsInChildren<Tilemap>(true).FirstOrDefault(t => t.name == "Suelo");
        if (suelo == null)
        {
            var go = new GameObject("Suelo", typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(grid.transform, false);
            Undo.RegisterCreatedObjectUndo(go, "Construir mapa Residencial");
            suelo = go.GetComponent<Tilemap>();
        }
        var render = suelo.GetComponent<TilemapRenderer>();
        Undo.RecordObject(render, "Construir mapa Residencial");
        render.sortingOrder = OrdenSuelo;

        // suelo[0] es la fila de arriba del mapa; en el Tilemap la fila 0 es la de abajo
        var filas = Lista(mapa, "suelo");
        int alto = filas.Count;
        var posiciones = new List<Vector3Int>();
        var celdas = new List<TileBase>();
        var faltantes = new HashSet<string>();
        for (int r = 0; r < alto; r++)
        {
            var fila = (List<object>)filas[r];
            for (int c = 0; c < fila.Count; c++)
            {
                if (!(fila[c] is string nombre)) continue;   // null = cielo, sin tile
                if (!tiles.TryGetValue(nombre, out var tile)) { faltantes.Add(nombre); continue; }
                posiciones.Add(new Vector3Int(c, alto - 1 - r, 0));
                celdas.Add(tile);
            }
        }
        if (faltantes.Count > 0)
            throw new Exception("El mapa usa tiles que no están en tileset_suelo: " + string.Join(", ", faltantes));

        int columnas = (int)Num(mapa["columnas"]);
        if (alto != (int)Num(mapa["filas"]) || filas.Cast<List<object>>().Any(f => f.Count != columnas))
            throw new Exception($"\"suelo\" no mide {columnas}x{Num(mapa["filas"])} tiles.");

        Undo.RegisterCompleteObjectUndo(suelo, "Construir mapa Residencial");
        suelo.ClearAllTiles();
        suelo.SetTiles(posiciones.ToArray(), celdas.ToArray());
        suelo.CompressBounds();

        // Cada celda debe tener su tile y su sprite: calles, banquetas, bordillos y pasto
        int vacias = 0;
        for (int i = 0; i < posiciones.Count; i++)
            if (suelo.GetTile(posiciones[i]) != celdas[i] || suelo.GetSprite(posiciones[i]) == null) vacias++;
        if (vacias > 0 || suelo.GetUsedTilesCount() == 0)
            throw new Exception($"El Tilemap Suelo quedó con {vacias} celdas sin pintar.");
        var conteo = celdas.GroupBy(t => t.name).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}");
        Debug.Log($"Suelo pintado: {posiciones.Count} celdas ({columnas}x{alto}). " + string.Join(", ", conteo));

        // Ningún otro Tilemap debe tapar el suelo
        foreach (var otro in UnityEngine.Object.FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (otro != render && otro.GetComponent<Tilemap>().GetUsedTilesCount() > 0)
                Debug.LogWarning($"El Tilemap \"{otro.name}\" también tiene tiles y puede tapar al Suelo.");
        return suelo;
    }

    static string TileEn(Dictionary<string, object> mapa, float x, float y)
    {
        var filas = Lista(mapa, "suelo");
        int r = filas.Count - 1 - Mathf.FloorToInt(y), c = Mathf.FloorToInt(x);
        if (r < 0 || r >= filas.Count) return null;
        var fila = (List<object>)filas[r];
        return c < 0 || c >= fila.Count ? null : fila[c] as string;
    }

    // ------------------------------------------------------------------ props

    static int ColocarProps(Dictionary<string, object> mapa, Dictionary<string, Sprite> sprites, Vector3 origen)
    {
        var props = Lista(mapa, "props").Cast<Dictionary<string, object>>().ToList();
        var faltantes = props.Select(p => (string)p["nombre"]).Where(n => !sprites.ContainsKey(n)).Distinct().ToList();
        if (faltantes.Count > 0)
            throw new Exception("El mapa usa props que no están en props_atlas: " + string.Join(", ", faltantes));

        // Se borra el "Props" anterior para no duplicar al reconstruir
        foreach (var viejo in SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name == "Props"))
            Undo.DestroyObjectImmediate(viejo);

        var padre = new GameObject("Props");
        Undo.RegisterCreatedObjectUndo(padre, "Construir mapa Residencial");
        padre.transform.position = origen;   // alineado con el origen del Tilemap

        foreach (var p in props)
        {
            string nombre = (string)p["nombre"];
            var sprite = sprites[nombre];
            var go = new GameObject(nombre);
            go.transform.SetParent(padre.transform, false);
            // (x, y) es el pivote abajo-centro, en tiles = unidades del mundo
            go.transform.localPosition = new Vector3((float)Num(p["x"]), (float)Num(p["y"]), 0);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;   // el orden por Y se toma del pie del sprite
            sr.flipX = p.TryGetValue("voltear", out var v) && v is bool b && b;   // autos del carril de abajo

            AgregarColision(go, nombre, sprite.rect.width);
        }
        return props.Count;
    }

    static void AgregarColision(GameObject go, string nombre, float anchoSprite)
    {
        foreach (var regla in Colisiones)
        {
            if (!nombre.StartsWith(regla.prefijo)) continue;
            foreach (var (x0, x1) in regla.tramos)
            {
                float ancho = x1 - x0 + 1;
                var caja = go.AddComponent<BoxCollider2D>();
                caja.size = new Vector2(ancho, regla.alto) / PPU;
                // el pivote está en el centro de la base, así que el borde izquierdo queda en -ancho/2
                caja.offset = new Vector2(x0 + ancho / 2f - anchoSprite / 2f, regla.alto / 2f) / PPU;
            }
            return;
        }
    }

    // ------------------------------------------------------------------ borde, spawn y entregas

    // Cuatro colliders invisibles justo afuera del mapa para que el repartidor no se salga.
    static void CrearBorde(Dictionary<string, object> mapa, Vector3 origen)
    {
        foreach (var viejo in SceneManager.GetActiveScene().GetRootGameObjects().Where(o => o.name == "BordeMapa"))
            Undo.DestroyObjectImmediate(viejo);

        float ancho = (float)Num(mapa["columnas"]), alto = (float)Num(mapa["filas"]), g = GrosorBorde;
        var borde = new GameObject("BordeMapa");
        Undo.RegisterCreatedObjectUndo(borde, "Construir mapa Residencial");
        borde.transform.position = origen;
        borde.isStatic = true;
        foreach (var (centro, tam) in new[]
                 {
                     (new Vector2(-g / 2, alto / 2), new Vector2(g, alto + 2 * g)),          // izquierda
                     (new Vector2(ancho + g / 2, alto / 2), new Vector2(g, alto + 2 * g)),   // derecha
                     (new Vector2(ancho / 2, -g / 2), new Vector2(ancho + 2 * g, g)),        // abajo
                     (new Vector2(ancho / 2, alto + g / 2), new Vector2(ancho + 2 * g, g)),  // arriba
                 })
        {
            var caja = borde.AddComponent<BoxCollider2D>();
            caja.offset = centro;
            caja.size = tam;
        }
    }

    static Vector3 Punto(Dictionary<string, object> d, Vector3 origen, float z = 0)
    {
        return new Vector3(origen.x + (float)Num(d["x"]), origen.y + (float)Num(d["y"]), z);
    }

    static void ColocarRepartidor(Dictionary<string, object> mapa, Vector3 origen)
    {
        if (!mapa.TryGetValue("spawn", out var s) || !(s is Dictionary<string, object> spawn))
            throw new Exception("Al JSON le falta \"spawn\".");
        // El repartidor arranca en la banqueta frente a la puerta de Brenn's, nunca en el pasto
        string bajo = TileEn(mapa, (float)Num(spawn["x"]), (float)Num(spawn["y"]));
        if (bajo == null || !bajo.StartsWith("banqueta"))
            throw new Exception($"El spawn ({Num(spawn["x"])}, {Num(spawn["y"])}) cae en \"{bajo}\", no en la banqueta.");
        if (mapa.TryGetValue("empresa", out var e) && e is Dictionary<string, object> empresa &&
            Math.Abs(Num(((Dictionary<string, object>)empresa["puerta"])["x"]) - Num(spawn["x"])) > 0.5)
            throw new Exception("El spawn no está frente a la puerta de Brenn's.");
        var jugador = UnityEngine.Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (jugador == null)
        {
            Debug.LogWarning("No hay ningún PlayerController en la escena; no se movió al repartidor.");
            return;
        }
        var pos = Punto(spawn, origen, jugador.transform.position.z);
        Undo.RecordObject(jugador.transform, "Construir mapa Residencial");
        jugador.transform.position = pos;

        // La cámara arranca encima del repartidor para que no cruce todo el mapa al empezar
        var camara = UnityEngine.Object.FindFirstObjectByType<CameraFollow>(FindObjectsInactive.Include);
        if (camara != null)
        {
            Undo.RecordObject(camara.transform, "Construir mapa Residencial");
            camara.transform.position = new Vector3(pos.x, pos.y + 1.25f, camara.transform.position.z);
        }
    }

    // Borra los puntos de entrega anteriores (los PuntoEntrega_1 hechos a mano y los de una corrida previa)
    // y crea uno por cada puerta, en la banqueta frente al portón.
    static int CrearPuntosEntrega(Dictionary<string, object> mapa, Vector3 origen)
    {
        var viejos = UnityEngine.Object.FindObjectsByType<DeliveryPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(p => p.gameObject)
            .Concat(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .Where(t => t.name.StartsWith("PuntoEntrega_") || (t.parent == null && t.name == "PuntosEntrega"))
                        .Select(t => t.gameObject))
            .Distinct().ToList();
        // solo se destruyen los de más arriba; sus hijos se van con ellos
        foreach (var go in viejos.Where(g => !viejos.Any(o => o != g && g.transform.IsChildOf(o.transform))).ToList())
            Undo.DestroyObjectImmediate(go);

        var circulo = SpriteCirculo();
        var padre = new GameObject("PuntosEntrega");
        Undo.RegisterCreatedObjectUndo(padre, "Construir mapa Residencial");

        int n = 0;
        foreach (var datos in Lista(mapa, "puntos_entrega").Cast<Dictionary<string, object>>())
        {
            n++;
            var punto = new GameObject((string)datos["nombre"]);
            punto.transform.SetParent(padre.transform, false);
            punto.transform.position = Punto(datos, origen);

            // DeliveryPoint toma el primer hijo como marcador: "Circle" debe ser el único
            var marca = new GameObject("Circle");
            marca.transform.SetParent(punto.transform, false);
            marca.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            var sr = marca.AddComponent<SpriteRenderer>();
            sr.sprite = circulo;
            sr.color = RosaEntrega;
            sr.sortingOrder = OrdenCirculo;

            punto.AddComponent<DeliveryPoint>();

            string bajo = TileEn(mapa, (float)Num(datos["x"]), (float)Num(datos["y"]));
            if (bajo == null || !bajo.StartsWith("banqueta"))
                Debug.LogWarning($"{punto.name} cae en \"{bajo}\", no en la banqueta.", punto);
        }
        int casas = Lista(mapa, "casas").Count;
        if (n != casas)
            throw new Exception($"Hay {casas} casas pero {n} puntos de entrega.");
        return n;
    }

    // ------------------------------------------------------------------ captura

    // Renderiza el mapa completo con una cámara ortográfica a 1 px de pantalla por px del arte
    // (128x64 tiles = 4096x2048) y lo guarda junto a los JSON.
    static string Capturar(Dictionary<string, object> mapa)
    {
        var suelo = UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .FirstOrDefault(t => t.name == "Suelo");
        if (suelo == null) throw new Exception("No hay Tilemap \"Suelo\" en la escena; construye el mapa primero.");
        var origen = suelo.transform.position;
        int cols = (int)Num(mapa["columnas"]), filas = (int)Num(mapa["filas"]);
        int w = cols * PPU, h = filas * PPU;

        var go = new GameObject("CamaraCaptura") { hideFlags = HideFlags.HideAndDontSave };
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var previa = RenderTexture.active;
        try
        {
            var cam = go.AddComponent<Camera>();
            go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            cam.orthographic = true;
            cam.orthographicSize = filas / 2f;
            cam.aspect = (float)cols / filas;
            cam.transform.position = new Vector3(origen.x + cols / 2f, origen.y + filas / 2f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.targetTexture = rt;
            var pedido = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, pedido))
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, pedido);
            else
                cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            string ruta = Path.Combine(CarpetaDatos, ArchivoCaptura).Replace('\\', '/');
            File.WriteAllBytes(ruta, tex.EncodeToPNG());
            Debug.Log($"Captura del mapa guardada en {ruta} ({w}x{h}).");
            return ruta;
        }
        finally
        {
            RenderTexture.active = previa;
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }
    }

    // Permite reconstruir y capturar sin pasar por el menú: si aparece Temp/ConstruirMapa.pedido,
    // se construye el mapa, se guarda la escena, se captura y el resultado queda en Temp/ConstruirMapa.resultado.
    [InitializeOnLoad]
    static class PedidoExterno
    {
        const string Pedido = "Temp/ConstruirMapa.pedido";
        const string Resultado = "Temp/ConstruirMapa.resultado";

        static PedidoExterno() => EditorApplication.update += Revisar;

        static void Revisar()
        {
            if (!File.Exists(Pedido) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;   // se construye al volver al modo edición
                return;
            }
            File.Delete(Pedido);
            string texto;
            try
            {
                texto = "OK\n" + ConstruirTodo();
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                texto += "\nCaptura: " + Capturar(LeerJson(ArchivoMapa));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                texto = "ERROR\n" + e;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            File.WriteAllText(Resultado, texto);
        }
    }

    static Sprite SpriteCirculo()
    {
        string ruta = AssetDatabase.GUIDToAssetPath(GuidCirculo);
        var sprite = string.IsNullOrEmpty(ruta) ? null : AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>().FirstOrDefault();
        if (sprite == null) sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        if (sprite == null) throw new Exception("No encuentro un sprite circular para los puntos de entrega.");
        return sprite;
    }

    // El orden por Y solo funciona si el Renderer 2D ordena por eje personalizado (0, 1, 0).
    // Aquí solo se revisa y se avisa; no se cambia la configuración del proyecto.
    static void RevisarOrdenPorY()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Renderer2DData"))
        {
            var ruta = AssetDatabase.GUIDToAssetPath(guid);
            var obj = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(ruta));
            var modo = obj.FindProperty("m_TransparencySortMode");
            var eje = obj.FindProperty("m_TransparencySortAxis");
            if (modo == null || eje == null) continue;
            if (modo.intValue != (int)TransparencySortMode.CustomAxis || eje.vector3Value != Vector3.up)
                Debug.LogWarning($"Para que los props se ordenen por Y, abre {ruta} y pon " +
                                 "Transparency Sort Mode = Custom Axis y Transparency Sort Axis = (0, 1, 0).");
        }
    }

    // ------------------------------------------------------------------ JSON

    static Dictionary<string, object> LeerJson(string archivo)
    {
        string ruta = Path.Combine(CarpetaDatos, archivo);
        if (!File.Exists(ruta))
            throw new Exception($"No existe {ruta}. Ejecuta antes generate_unity.py y generate_residencial_grande.py en pixel_town.");
        return (Dictionary<string, object>)Json.Leer(File.ReadAllText(ruta));
    }

    static List<object> Lista(Dictionary<string, object> d, string clave)
    {
        if (!d.TryGetValue(clave, out var v) || !(v is List<object> lista))
            throw new Exception($"Al JSON le falta la lista \"{clave}\".");
        return lista;
    }

    static double Num(object o) => Convert.ToDouble(o, CultureInfo.InvariantCulture);

    // Lector de JSON mínimo: JsonUtility no sabe leer listas de listas como "suelo".
    static class Json
    {
        public static object Leer(string s)
        {
            int i = 0;
            var v = Valor(s, ref i);
            Espacios(s, ref i);
            if (i < s.Length) throw Error(s, i, "texto de más al final");
            return v;
        }

        static object Valor(string s, ref int i)
        {
            Espacios(s, ref i);
            if (i >= s.Length) throw Error(s, i, "fin inesperado");
            char c = s[i];
            if (c == '{')
            {
                i++;
                var d = new Dictionary<string, object>();
                Espacios(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Espacios(s, ref i);
                    string clave = Cadena(s, ref i);
                    Espacios(s, ref i);
                    Esperar(s, ref i, ':');
                    d[clave] = Valor(s, ref i);
                    Espacios(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    Esperar(s, ref i, '}');
                    return d;
                }
            }
            if (c == '[')
            {
                i++;
                var l = new List<object>();
                Espacios(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Valor(s, ref i));
                    Espacios(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    Esperar(s, ref i, ']');
                    return l;
                }
            }
            if (c == '"') return Cadena(s, ref i);
            if (Palabra(s, ref i, "true")) return true;
            if (Palabra(s, ref i, "false")) return false;
            if (Palabra(s, ref i, "null")) return null;

            int inicio = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == inicio) throw Error(s, i, $"carácter inesperado '{c}'");
            return double.Parse(s.Substring(inicio, i - inicio), CultureInfo.InvariantCulture);
        }

        static string Cadena(string s, ref int i)
        {
            Esperar(s, ref i, '"');
            var sb = new StringBuilder();
            while (true)
            {
                if (i >= s.Length) throw Error(s, i, "cadena sin cerrar");
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber));
                        i += 4;
                        break;
                    default: sb.Append(e); break;   // \" \\ \/
                }
            }
        }

        static bool Palabra(string s, ref int i, string palabra)
        {
            if (string.CompareOrdinal(s, i, palabra, 0, palabra.Length) != 0) return false;
            i += palabra.Length;
            return true;
        }

        static void Espacios(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static void Esperar(string s, ref int i, char c)
        {
            if (i >= s.Length || s[i] != c) throw Error(s, i, $"se esperaba '{c}'");
            i++;
        }

        static Exception Error(string s, int i, string que) => new FormatException($"JSON inválido en la posición {i}: {que}.");
    }
}
