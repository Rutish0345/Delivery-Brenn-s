using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Turbos "Café exprés" en las 3 escenas de juego:
//  - Turbos (raíz en el origen del mapa): Turbo_1..N donde dice "turbos" del JSON del mapa (pixel_town/turbos.py, que
//    corren los 3 generadores): CircleCollider2D trigger, Turbo, la taza animada (cafe_expres_0..3) y el halo
//    (brillo_turbo) debajo. Nivel 3: Unlit y halo más fuerte, para que se vean de noche con lluvia.
//  - En el repartidor: los valores del turbo (a pie x1.6 por 6 s, en moto x1.4 por 5 s, tope 8 tiles/s; se escriben
//    aquí porque la escena guarda los de la versión anterior) y el hijo EstelaTurbo: las partículas doradas y rosas
//    (estela_turbo_0..4) y el destello dorado al recoger.
//  - Canvas/PlacaTurbo, debajo de la fila de quejas (ConstruirReloj.PosTurbo): píldora lavanda con el rayo y la barra
//    dorada que se vacía (TurboHUD); solo se ve con el turbo activo. La guía del pedido baja a ConstruirReloj.PosGuia.
//  - Canvas/LineasVelocidad (primer hijo del Canvas: debajo de todo el HUD): líneas de velocidad en la orilla de la
//    pantalla (lineas_velocidad_0..2), solo con el turbo.
// Arte: pixel_town/generate_turbos.py. Se puede correr las veces que sea (borra y rehace). ConstruirMapa la llama al
// armar cualquier nivel; el menú y el pedido la aplican a las 3 escenas sin reconstruirlas, y verifican: 4 / 6 / 6
// turbos, a 4 tiles o más de puntos de entrega, cebras, baches y semáforos, fuera de patios y de colliders, y que se
// llega a cada uno a pie (hueco 0.6) y en moto (hueco 1.2, con BloqueoMoto).
// Temp/ConstruirTurbos.pedido → Temp/ConstruirTurbos.resultado
public static class ConstruirTurbos
{
    const string CarpetaMapa = "Assets/Sprits/Comun/Turbo/";
    const string CarpetaUI = "Assets/Sprits/UI/";
    const string RutaUnlit = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    public const string NombreRaiz = "Turbos";
    public const string NombrePlaca = "PlacaTurbo";
    public const string NombreEstela = "EstelaTurbo";
    public const string NombreLineas = "LineasVelocidad";
    public const float TurboAPie = 1.6f, DuracionAPie = 6f, TurboMoto = 1.4f, DuracionMoto = 5f, TopeVelocidad = 8f;
    const string NombreUndo = "Turbos";

    const int PpuMapa = 32;                 // 16 px = media celda
    const float RadioTrigger = 0.45f;
    const int OrdenBrillo = -7;             // sobre el suelo (-10), bajo los baches (-6)
    const float AlfaBrillo = 0.35f, AlfaBrilloNoche = 0.6f;
    public const float DistanciaMinima = 4f;

    const int Escala = 3;                   // HUD: 1 píxel de arte = 3 unidades del Canvas (como las quejas)
    const float AnchoPlaca = 240;
    const float LadoIcono = 12 * Escala;
    const float AltoBarra = 6 * Escala;

    public static int CantidadPara(string escena) => escena == "SampleScene" ? 4 : 6;
    static bool EsNoche(string escena) => escena == "Nivel3_Lluvia";

    static ConstruirMapa.Perfil PerfilDe(string escena) =>
        escena == "Nivel2_Comercial" ? ConstruirMapa.Perfil.Comercial :
        escena == "Nivel3_Lluvia" ? ConstruirMapa.Perfil.Lluvia : ConstruirMapa.Perfil.Residencial;

    static Dictionary<string, object> MapaDe(string escena)
    {
        var p = PerfilDe(escena);
        return ConstruirMapa.LeerJsonDe(p, p.archivoMapa);
    }

    static Vector3 OrigenDeEscena() =>
        SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Props")?.transform.position
        ?? throw new Exception("No hay raíz Props (el origen del mapa).");

    static Sprite Mapa(string n) =>
        ConstruirMenuPrincipal.ImportarPixel(CarpetaMapa + n + ".png", Vector4.zero, PpuMapa)
        ?? throw new Exception($"Falta {CarpetaMapa}{n}.png (corre pixel_town/generate_turbos.py).");

    static Sprite UI(string n, Vector4 borde) =>
        ConstruirMenuPrincipal.ImportarPixel(CarpetaUI + n + ".png", borde)
        ?? throw new Exception($"Falta {CarpetaUI}{n}.png (corre pixel_town/generate_turbos.py).");

    static Canvas CanvasHUD() =>
        SceneManager.GetActiveScene().GetRootGameObjects().Select(g => g.GetComponent<Canvas>())
            .FirstOrDefault(c => c != null && c.transform.Find("PlacaPedidos") != null);

    // ------------------------------------------------------------------ construcción

    // Tazas, estela y placa en la escena activa. Devuelve un renglón para el resumen.
    public static string Crear(Dictionary<string, object> mapa, Vector3 origen)
    {
        string escena = SceneManager.GetActiveScene().name;
        bool noche = EsNoche(escena);
        var unlit = noche ? AssetDatabase.LoadAssetAtPath<Material>(RutaUnlit) ?? throw new Exception("Falta " + RutaUnlit) : null;
        AssetDatabase.Refresh();
        var cuadros = Enumerable.Range(0, 4).Select(k => Mapa("cafe_expres_" + k)).ToArray();
        var brillo = Mapa("brillo_turbo");
        var estela = Enumerable.Range(0, 5).Select(k => Mapa("estela_turbo_" + k)).ToArray();

        var jugador = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault()
                      ?? throw new Exception("No hay repartidor (PlayerController) en la escena.");
        var srJugador = jugador.GetComponent<SpriteRenderer>();
        var suelo = Object.FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Suelo");
        int capaSuelo = suelo != null ? suelo.sortingLayerID : 0;

        // tazas
        foreach (var viejo in SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name == NombreRaiz))
            Undo.DestroyObjectImmediate(viejo);
        var raiz = new GameObject(NombreRaiz);
        Undo.RegisterCreatedObjectUndo(raiz, NombreUndo);
        raiz.transform.position = origen;
        var lista = mapa.ContainsKey("turbos") ? ConstruirMapa.Lista(mapa, "turbos").Cast<Dictionary<string, object>>().ToList()
                                               : new List<Dictionary<string, object>>();
        if (lista.Count == 0) throw new Exception("El JSON del mapa no trae \"turbos\" (corre python turbos.py --aplicar en pixel_town).");
        foreach (var t in lista)
        {
            var go = new GameObject((string)t["nombre"]);
            go.transform.SetParent(raiz.transform, false);
            go.transform.localPosition = new Vector3((float)ConstruirMapa.Num(t["x"]), (float)ConstruirMapa.Num(t["y"]), 0);
            var zona = go.AddComponent<CircleCollider2D>();
            zona.isTrigger = true;
            zona.radius = RadioTrigger;

            var halo = CrearSprite("Brillo", go.transform, brillo, capaSuelo, OrdenBrillo, unlit);
            halo.color = new Color(1, 1, 1, noche ? AlfaBrilloNoche : AlfaBrillo);
            // la taza se ordena como el repartidor y los props (misma capa y orden; el resto lo hace la altura)
            var taza = CrearSprite("Taza", go.transform, cuadros[0], srJugador != null ? srJugador.sortingLayerID : capaSuelo,
                              srJugador != null ? srJugador.sortingOrder : 0, unlit);

            var so = new SerializedObject(go.AddComponent<Turbo>());
            so.FindProperty("taza").objectReferenceValue = taza;
            so.FindProperty("brillo").objectReferenceValue = halo;
            var arr = so.FindProperty("cuadros");
            arr.arraySize = cuadros.Length;
            for (int i = 0; i < cuadros.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = cuadros[i];
            so.FindProperty("alfaBrillo").floatValue = noche ? AlfaBrilloNoche : AlfaBrillo;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // valores del turbo en el repartidor
        var sp = new SerializedObject(jugador);
        sp.FindProperty("turboAPie").floatValue = TurboAPie;
        sp.FindProperty("duracionAPie").floatValue = DuracionAPie;
        sp.FindProperty("turboMoto").floatValue = TurboMoto;
        sp.FindProperty("duracionMoto").floatValue = DuracionMoto;
        sp.FindProperty("topeVelocidad").floatValue = TopeVelocidad;
        sp.ApplyModifiedPropertiesWithoutUndo();

        // estela del repartidor
        var vieja = jugador.transform.Find(NombreEstela);
        if (vieja != null) Undo.DestroyObjectImmediate(vieja.gameObject);
        var goEstela = new GameObject(NombreEstela);
        Undo.RegisterCreatedObjectUndo(goEstela, NombreUndo);
        goEstela.transform.SetParent(jugador.transform, false);
        var se = new SerializedObject(goEstela.AddComponent<EstelaTurbo>());
        se.FindProperty("jugador").objectReferenceValue = jugador;
        var ce = se.FindProperty("cuadros");
        ce.arraySize = estela.Length;
        for (int i = 0; i < estela.Length; i++) ce.GetArrayElementAtIndex(i).objectReferenceValue = estela[i];
        se.FindProperty("material").objectReferenceValue = unlit;
        se.ApplyModifiedPropertiesWithoutUndo();

        CrearPlaca();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return $"Turbos: {lista.Count} Café exprés{(noche ? " (Unlit, halo de noche)" : "")}; repartidor a pie x{TurboAPie} por {DuracionAPie} s, " +
               $"en moto x{TurboMoto} por {DuracionMoto} s, tope {TopeVelocidad}; {NombreEstela}, {NombrePlaca} en {ConstruirReloj.PosTurbo} y {NombreLineas}.";
    }

    static SpriteRenderer CrearSprite(string nombre, Transform padre, Sprite sprite, int capa, int orden, Material material)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerID = capa;
        sr.sortingOrder = orden;
        if (material != null) sr.sharedMaterial = material;
        return sr;
    }

    // Placa del HUD: píldora lavanda (importación de la píldora rosa), rayo a la izquierda y barra a la derecha
    static void CrearPlaca()
    {
        var canvas = CanvasHUD() ?? throw new Exception("No encuentro el Canvas con PlacaPedidos (ejecuta Construir interfaz).");
        var pildora = (TextureImporter)AssetImporter.GetAtPath(CarpetaUI + "boton_pill_normal.png");
        var placa = UI("placa_turbo", pildora.spriteBorder);
        var rayo = UI("icono_rayo", Vector4.zero);
        var fondoBarra = UI("barra_turbo_fondo", Vector4.one);
        var rellenoBarra = UI("barra_turbo_relleno", Vector4.zero);
        var cuadrosLineas = Enumerable.Range(0, 3).Select(k => UI("lineas_velocidad_" + k, Vector4.zero)).ToArray();

        // líneas de velocidad: pantalla completa, primer hijo del Canvas (todo el HUD encima)
        var viejas = canvas.transform.Find(NombreLineas);
        if (viejas != null) Undo.DestroyObjectImmediate(viejas.gameObject);
        var rtLineas = Hijo(NombreLineas, canvas.transform);
        rtLineas.anchorMin = Vector2.zero;
        rtLineas.anchorMax = Vector2.one;
        rtLineas.offsetMin = rtLineas.offsetMax = Vector2.zero;
        var imgLineas = rtLineas.gameObject.AddComponent<Image>();
        imgLineas.sprite = cuadrosLineas[0];
        imgLineas.color = new Color(1, 1, 1, 0);
        imgLineas.raycastTarget = false;
        imgLineas.enabled = false;   // TurboHUD la prende con el turbo
        rtLineas.SetAsFirstSibling();

        var vieja = canvas.transform.Find(NombrePlaca);
        if (vieja != null) Undo.DestroyObjectImmediate(vieja.gameObject);
        var rt = Hijo(NombrePlaca, canvas.transform);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = ConstruirReloj.PosTurbo;
        rt.sizeDelta = new Vector2(AnchoPlaca, ConstruirReloj.AltoTurbo);
        float mult = canvas.referencePixelsPerUnit / (placa.pixelsPerUnit * Escala);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = placa;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = mult;
        img.raycastTarget = false;
        var grupo = rt.gameObject.AddComponent<CanvasGroup>();
        grupo.interactable = false;
        grupo.blocksRaycasts = false;
        grupo.alpha = 0f;   // TurboHUD la muestra con el turbo

        // rayo pasando la tapa de la píldora (6 px de arte), centrado sobre el canto (1 px más arriba)
        var icono = Hijo("IconoTurbo", rt);
        icono.anchorMin = icono.anchorMax = new Vector2(0, 0.5f);
        icono.pivot = new Vector2(0.5f, 0.5f);
        icono.anchoredPosition = new Vector2(6 * Escala + LadoIcono / 2, Escala);
        icono.sizeDelta = new Vector2(LadoIcono, LadoIcono);
        var imgIcono = icono.gameObject.AddComponent<Image>();
        imgIcono.sprite = rayo;
        imgIcono.raycastTarget = false;

        float x0 = 6 * Escala + LadoIcono + 2 * Escala;
        var barra = Hijo("BarraTurbo", rt);
        barra.anchorMin = barra.anchorMax = barra.pivot = new Vector2(0, 0.5f);
        barra.anchoredPosition = new Vector2(x0, Escala);
        barra.sizeDelta = new Vector2(AnchoPlaca - x0 - 6 * Escala, AltoBarra);
        var imgBarra = barra.gameObject.AddComponent<Image>();
        imgBarra.sprite = fondoBarra;
        imgBarra.type = Image.Type.Sliced;
        imgBarra.pixelsPerUnitMultiplier = canvas.referencePixelsPerUnit / (fondoBarra.pixelsPerUnit * Escala);
        imgBarra.raycastTarget = false;

        var relleno = Hijo("BarraTurboRelleno", barra);
        relleno.anchorMin = Vector2.zero;
        relleno.anchorMax = Vector2.one;
        relleno.offsetMin = new Vector2(Escala, Escala);
        relleno.offsetMax = new Vector2(-Escala, -Escala);
        var imgRelleno = relleno.gameObject.AddComponent<Image>();
        imgRelleno.sprite = rellenoBarra;
        imgRelleno.type = Image.Type.Filled;
        imgRelleno.fillMethod = Image.FillMethod.Horizontal;
        imgRelleno.fillOrigin = (int)Image.OriginHorizontal.Left;
        imgRelleno.fillAmount = 1f;
        imgRelleno.raycastTarget = false;

        var so = new SerializedObject(rt.gameObject.AddComponent<TurboHUD>());
        so.FindProperty("grupo").objectReferenceValue = grupo;
        so.FindProperty("relleno").objectReferenceValue = imgRelleno;
        so.FindProperty("icono").objectReferenceValue = icono;
        so.FindProperty("lineas").objectReferenceValue = imgLineas;
        var cl = so.FindProperty("cuadrosLineas");
        cl.arraySize = cuadrosLineas.Length;
        for (int i = 0; i < cuadrosLineas.Length; i++) cl.GetArrayElementAtIndex(i).objectReferenceValue = cuadrosLineas[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        // con el HUD, justo después de las quejas (la pausa y los paneles lo tapan)
        var antes = canvas.transform.Find(ConstruirQuejas.NombreFila) ?? canvas.transform.Find("PlacaReloj") ?? canvas.transform.Find("PlacaPedidos");
        rt.SetSiblingIndex(antes.GetSiblingIndex() + 1);

        var guia = canvas.transform.Find("GuiaPedido") as RectTransform;
        if (guia != null)
        {
            Undo.RecordObject(guia, NombreUndo);
            guia.anchoredPosition = ConstruirReloj.PosGuia;
        }
    }

    static RectTransform Hijo(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, NombreUndo);
        go.layer = padre.gameObject.layer;
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
    }

    // ------------------------------------------------------------------ verificación (escena abierta)

    public static List<string> Verificar(Dictionary<string, object> mapa, Vector3 origen, out string informe)
    {
        var errores = new List<string>();
        string escena = SceneManager.GetActiveScene().name;
        informe = "";
        var raiz = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == NombreRaiz);
        if (raiz == null) { errores.Add($"No hay raíz {NombreRaiz}."); return errores; }
        var turbos = raiz.GetComponentsInChildren<Turbo>(true);
        int esperados = CantidadPara(escena);
        if (turbos.Length != esperados) errores.Add($"{turbos.Length} turbos, se esperaban {esperados}.");
        if (!mapa.ContainsKey("turbos") || ConstruirMapa.Lista(mapa, "turbos").Count != turbos.Length) errores.Add("No hay un turbo por cada uno del JSON.");
        bool noche = EsNoche(escena);
        foreach (var t in turbos)
        {
            var so = new SerializedObject(t);
            var zona = t.GetComponent<CircleCollider2D>();
            if (zona == null || !zona.isTrigger || !zona.enabled) errores.Add($"{t.name}: sin CircleCollider2D trigger.");
            var taza = so.FindProperty("taza").objectReferenceValue as SpriteRenderer;
            var halo = so.FindProperty("brillo").objectReferenceValue as SpriteRenderer;
            if (taza == null || halo == null || so.FindProperty("cuadros").arraySize != 4) errores.Add($"{t.name}: le falta la taza, el halo o sus 4 cuadros.");
            if (noche && (taza?.sharedMaterial?.name != "Sprite-Unlit-Default" || halo?.sharedMaterial?.name != "Sprite-Unlit-Default"))
                errores.Add($"{t.name}: en el nivel 3 debe ir Unlit (se perdería de noche).");
        }

        // distancias: puntos de entrega, baches, semáforos y cebras / líneas de alto
        Physics2D.SyncTransforms();
        var cosas = new List<(string, Vector2)>();
        cosas.AddRange(Object.FindObjectsByType<DeliveryPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(d => (d.name, (Vector2)d.transform.position)));
        cosas.AddRange(Object.FindObjectsByType<Bache>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                             .Select(b => (b.name, ConstruirMapa.CajaMundo(b.GetComponent<BoxCollider2D>()).center)));
        cosas.AddRange(Object.FindObjectsByType<Semaforo>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(s => (s.name, (Vector2)s.transform.position)));
        var filas = ConstruirMapa.Lista(mapa, "suelo").Cast<List<object>>().ToList();
        for (int r = 0; r < filas.Count; r++)
            for (int c = 0; c < filas[r].Count; c++)
            {
                var n = (string)filas[r][c];
                if (n.StartsWith("cebra") || n.StartsWith("linea_alto"))
                    cosas.Add(($"cebra ({c}, {filas.Count - 1 - r})", (Vector2)origen + new Vector2(c + 0.5f, filas.Count - 1 - r + 0.5f)));
            }
        float peor = float.MaxValue;
        foreach (var t in turbos)
        {
            Vector2 p = t.transform.position;
            var (cerca, d) = cosas.Select(o => (o.Item1, Vector2.Distance(o.Item2, p))).OrderBy(o => o.Item2).FirstOrDefault();
            peor = Mathf.Min(peor, d);
            if (d < DistanciaMinima) errores.Add($"{t.name}: a {d:0.0} tiles de {cerca} (mínimo {DistanciaMinima}).");
        }

        // patios privados
        foreach (var p in (mapa.ContainsKey("patios") ? ConstruirMapa.Lista(mapa, "patios") : new List<object>()).Cast<Dictionary<string, object>>())
        {
            var patio = ConstruirMapa.CajaJson(p["patio"]);
            foreach (var t in turbos.Where(t => patio.Contains((Vector2)(t.transform.position - origen))))
                errores.Add($"{t.name}: dentro del patio de la casa {ConstruirMapa.Num(p["casa"])}.");
        }

        // colliders y alcance: a pie (colliders fijos) y en moto (más BloqueoMoto), desde el spawn
        int F = ConstruirMapa.CeldasRecorrido;
        var obst = ConstruirMapa.ObstaculosFijos(mapa, origen, null, out int W, out int H, out int fijos);
        foreach (var t in turbos)
        {
            Vector2 p = (Vector2)(t.transform.position - origen);
            for (int x = Mathf.FloorToInt((p.x - 0.3f) * F); x <= Mathf.FloorToInt((p.x + 0.3f) * F); x++)
                for (int y = Mathf.FloorToInt((p.y - 0.3f) * F); y <= Mathf.FloorToInt((p.y + 0.3f) * F); y++)
                    if (obst[(y + 1) * W + x + 1]) { errores.Add($"{t.name}: encima de un collider en ({(x + 0.5f) / F:0.0}, {(y + 0.5f) / F:0.0})."); goto siguiente; }
            siguiente:;
        }
        var moto = (bool[])obst.Clone();
        int cajasMoto = 0;
        foreach (var b in Object.FindObjectsByType<BloqueoMoto>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (var c in b.GetComponentsInChildren<Collider2D>(true))
            {
                if (!(c is BoxCollider2D caja)) { errores.Add($"BloqueoMoto/{c.name} no es BoxCollider2D (no lo sé medir)."); continue; }
                var r = ConstruirMapa.CajaMundo(caja);
                r.position -= (Vector2)origen;
                cajasMoto++;
                for (int x = Mathf.Max(0, Mathf.FloorToInt(r.xMin * F)); x < Mathf.Min(W - 2, Mathf.CeilToInt(r.xMax * F)); x++)
                    for (int y = Mathf.Max(0, Mathf.FloorToInt(r.yMin * F)); y < Mathf.Min(H - 2, Mathf.CeilToInt(r.yMax * F)); y++)
                        moto[(y + 1) * W + x + 1] = true;
            }
        var spawn = (Dictionary<string, object>)mapa["spawn"];
        var salida = new Vector2((float)ConstruirMapa.Num(spawn["x"]), (float)ConstruirMapa.Num(spawn["y"]));
        var aPie = Alcanzables(obst, W, H, F, salida, ConstruirMapa.HuecoAPie);
        var enMoto = Alcanzables(moto, W, H, F, salida, ConstruirMapa.HuecoMoto);
        foreach (var t in turbos)
        {
            Vector2 p = (Vector2)(t.transform.position - origen);
            if (!Llega(aPie, W, F, p, RadioTrigger)) errores.Add($"{t.name}: no se llega a pie.");
            if (!Llega(enMoto, W, F, p, RadioTrigger + 0.3f)) errores.Add($"{t.name}: no se llega en moto.");
        }

        // placa del HUD
        errores.AddRange(VerificarPlaca());
        informe = $"Verificados: {turbos.Length} turbos, el más cercano a {peor:0.0} tiles de un punto, bache, semáforo o cebra; " +
                  $"alcanzables a pie (hueco {ConstruirMapa.HuecoAPie}) y en moto (hueco {ConstruirMapa.HuecoMoto}, {cajasMoto} cajas de BloqueoMoto).";
        return errores;
    }

    static bool[] Alcanzables(bool[] obst, int W, int H, int F, Vector2 salida, float hueco)
    {
        var d2 = ConstruirMapa.DistanciaCuadrada(obst, W, H);
        var libre = new bool[W * H];
        for (int i = 0; i < W * H; i++) libre[i] = !obst[i] && 2f * (Mathf.Sqrt(d2[i]) - 0.5f) / F >= hueco - 1e-4f;
        var visto = new bool[W * H];
        int inicio = (Mathf.FloorToInt(salida.y * F) + 1) * W + Mathf.FloorToInt(salida.x * F) + 1;
        if (!libre[inicio]) return visto;
        var cola = new Queue<int>();
        cola.Enqueue(inicio);
        visto[inicio] = true;
        while (cola.Count > 0)
        {
            int i = cola.Dequeue();
            foreach (int j in new[] { i - 1, i + 1, i - W, i + W })
                if (libre[j] && !visto[j]) { visto[j] = true; cola.Enqueue(j); }
        }
        return visto;
    }

    // ¿alguna celda alcanzable a menos de 'radio' del centro del turbo?
    static bool Llega(bool[] alcanzable, int W, int F, Vector2 p, float radio)
    {
        for (int x = Mathf.FloorToInt((p.x - radio) * F); x <= Mathf.CeilToInt((p.x + radio) * F); x++)
            for (int y = Mathf.FloorToInt((p.y - radio) * F); y <= Mathf.CeilToInt((p.y + radio) * F); y++)
            {
                int i = (y + 1) * W + x + 1;
                if (i < 0 || i >= alcanzable.Length || !alcanzable[i]) continue;
                if (Vector2.Distance(new Vector2((x + 0.5f) / F, (y + 0.5f) / F), p) <= radio) return true;
            }
        return false;
    }

    public static List<string> VerificarPlaca()
    {
        var errores = new List<string>();
        var canvas = CanvasHUD();
        if (canvas == null) return new List<string> { "no hay Canvas con PlacaPedidos" };
        var placa = canvas.transform.Find(NombrePlaca) as RectTransform;
        if (placa == null) return new List<string> { $"Falta Canvas/{NombrePlaca}." };
        if (placa.anchoredPosition != ConstruirReloj.PosTurbo) errores.Add($"{NombrePlaca} no está en {ConstruirReloj.PosTurbo}.");
        var fila = canvas.transform.Find(ConstruirQuejas.NombreFila) as RectTransform;
        if (fila != null)
        {
            if (placa.GetSiblingIndex() != fila.GetSiblingIndex() + 1) errores.Add($"{NombrePlaca} no va justo después de {ConstruirQuejas.NombreFila}.");
            if (placa.anchoredPosition.y > fila.anchoredPosition.y - fila.sizeDelta.y) errores.Add($"{NombrePlaca} se encima con las quejas.");
        }
        var guia = canvas.transform.Find("GuiaPedido") as RectTransform;
        if (guia != null && guia.anchoredPosition.y > placa.anchoredPosition.y - placa.sizeDelta.y) errores.Add("GuiaPedido se encima con la placa del turbo.");
        var hud = placa.GetComponent<TurboHUD>();
        var grupo = placa.GetComponent<CanvasGroup>();
        if (hud == null || grupo == null) errores.Add($"{NombrePlaca}: sin TurboHUD o sin CanvasGroup.");
        else
        {
            if (grupo.alpha != 0f) errores.Add($"{NombrePlaca}: debe empezar oculta (alpha 0).");
            var so = new SerializedObject(hud);
            var relleno = so.FindProperty("relleno").objectReferenceValue as Image;
            if (so.FindProperty("grupo").objectReferenceValue != grupo) errores.Add("TurboHUD: Grupo no es el de la placa.");
            if (relleno == null || relleno.type != Image.Type.Filled || relleno.fillMethod != Image.FillMethod.Horizontal)
                errores.Add("TurboHUD: Relleno debe ser una Image Filled horizontal.");
            if (so.FindProperty("icono").objectReferenceValue == null) errores.Add("TurboHUD: falta el icono.");
            var lineas = so.FindProperty("lineas").objectReferenceValue as Image;
            if (lineas == null || lineas.transform.parent != canvas.transform || lineas.transform.GetSiblingIndex() != 0 || lineas.raycastTarget
                || so.FindProperty("cuadrosLineas").arraySize != 3)
                errores.Add($"TurboHUD: {NombreLineas} debe ser el primer hijo del Canvas, sin raycast y con sus 3 cuadros.");
        }
        if (placa.GetComponent<Image>()?.sprite?.name != "placa_turbo") errores.Add($"{NombrePlaca}: no es la píldora placa_turbo.");
        foreach (var n in new[] { "IconoTurbo", "BarraTurbo", "BarraTurbo/BarraTurboRelleno" })
            if (placa.Find(n) == null) errores.Add($"Falta {NombrePlaca}/{n}.");
        foreach (var ruta in new[] { CarpetaUI + "placa_turbo.png", CarpetaUI + "icono_rayo.png", CarpetaMapa + "cafe_expres_0.png", CarpetaMapa + "estela_turbo_0.png" })
            if (AssetImporter.GetAtPath(ruta) is TextureImporter t && (t.filterMode != FilterMode.Point || t.textureCompression != TextureImporterCompression.Uncompressed))
                errores.Add($"{ruta}: no está en Point sin compresión.");
        if (AssetImporter.GetAtPath(CarpetaMapa + "cafe_expres_0.png") is TextureImporter ti && !Mathf.Approximately(ti.spritePixelsPerUnit, PpuMapa))
            errores.Add($"cafe_expres: PPU {ti.spritePixelsPerUnit}, no {PpuMapa}.");
        var jugador = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        var estela = jugador != null ? jugador.transform.Find(NombreEstela)?.GetComponent<EstelaTurbo>() : null;
        if (estela == null) errores.Add($"El repartidor no tiene {NombreEstela}.");
        else if (new SerializedObject(estela).FindProperty("cuadros").arraySize != 5) errores.Add($"{NombreEstela}: le faltan sus 5 cuadros.");
        if (jugador != null)
        {
            var sp = new SerializedObject(jugador);
            if (!Mathf.Approximately(sp.FindProperty("turboAPie").floatValue, TurboAPie) || !Mathf.Approximately(sp.FindProperty("duracionAPie").floatValue, DuracionAPie)
                || !Mathf.Approximately(sp.FindProperty("turboMoto").floatValue, TurboMoto) || !Mathf.Approximately(sp.FindProperty("duracionMoto").floatValue, DuracionMoto)
                || !Mathf.Approximately(sp.FindProperty("topeVelocidad").floatValue, TopeVelocidad))
                errores.Add($"El repartidor no tiene los valores del turbo (x{TurboAPie} / {DuracionAPie} s, x{TurboMoto} / {DuracionMoto} s, tope {TopeVelocidad}).");
        }
        return errores;
    }

    // ------------------------------------------------------------------ menú y pedido (las 3 escenas)

    [MenuItem("Delivery Brenn's/Turbos Café exprés (3 niveles)")]
    static void Menu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        try { EditorUtility.DisplayDialog("Turbos", Aplicar(out _), "OK"); }
        catch (Exception e) { Debug.LogException(e); EditorUtility.DisplayDialog("Turbos", e.Message, "OK"); }
    }

    // Abre la escena, rehace los turbos, verifica y guarda (solo si todo salió bien)
    public static string AplicarEnEscena(string ruta)
    {
        var escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
        var mapa = MapaDe(escena.name);
        var origen = OrigenDeEscena();
        string linea = Crear(mapa, origen);
        var errores = Verificar(mapa, origen, out string informe);
        if (errores.Count > 0) return $"ERROR en {escena.name} (no se guardó):\n- " + string.Join("\n- ", errores);
        if (!EditorSceneManager.SaveScene(escena)) return $"ERROR: no pude guardar {ruta}.";
        var detalle = Object.FindObjectsByType<Turbo>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(t => t.name)
                            .Select(t => $"{t.name} ({t.transform.position.x - origen.x:0.0}, {t.transform.position.y - origen.y:0.0})");
        return $"{escena.name}: {linea} {informe} [{string.Join(", ", detalle)}]";
    }

    static string Aplicar(out bool ok)
    {
        string volverA = SceneManager.GetActiveScene().path;
        var lineas = new List<string>();
        try
        {
            foreach (var ruta in RevisarCanvas.EscenasDeJuego) lineas.Add(AplicarEnEscena(ruta));
        }
        finally
        {
            if (!string.IsNullOrEmpty(volverA) && File.Exists(volverA) && SceneManager.GetActiveScene().path != volverA)
                EditorSceneManager.OpenScene(volverA, OpenSceneMode.Single);
        }
        var faltan = RevisarCanvas.RevisarEscenasDeJuego();
        ok = faltan.Count == 0 && !lineas.Any(l => l.StartsWith("ERROR"));
        return string.Join("\n", lineas) + (faltan.Count == 0 ? "\nVERIFICACIÓN: Canvas completo (con la placa del turbo) en las 3 escenas." : "\nFALTAN:\n- " + string.Join("\n- ", faltan));
    }

    [InitializeOnLoad]
    static class PedidoExterno
    {
        const string Pedido = "Temp/ConstruirTurbos.pedido";
        const string Resultado = "Temp/ConstruirTurbos.resultado";

        static PedidoExterno() => EditorApplication.update += Revisar;

        static void Revisar()
        {
            if (!File.Exists(Pedido) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
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
                if (SceneManager.GetActiveScene().isDirty) throw new Exception("La escena abierta tiene cambios sin guardar; no la cambio.");
                texto = Aplicar(out bool ok);
                texto = (ok ? "OK\n" : "CON ERRORES\n") + texto;
            }
            catch (Exception e) { Debug.LogException(e); texto = "ERROR\n" + e; }
            finally { Application.logMessageReceived -= Anotar; }
            texto += "\n\nCONSOLA (advertencias y errores): " + (consola.Count == 0 ? "ninguno" : "\n" + string.Join("\n", consola));
            File.WriteAllText(Resultado, texto);
        }
    }
}
