using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Reconstruye el HUD (medidor, placas de pedidos y dinero, botón de pausa) con el kit de UI en
// pixel art, todo dentro del Canvas que ya existe.
// Se puede ejecutar las veces que quieras: lo que crea este script se borra y se
// vuelve a hacer, y TextoPedidos / TextoDinero se reutilizan.
// No toca PanelFinal ni PanelPausa: esos los arman ConstruirPanelFinal y ConstruirPausa. Como
// BotonPausa se rehace, aquí mismo se vuelve a conectar con MenuJuego.AlternarPausa si ya existe.
// Deja fijo lo que comparte toda la interfaz: el Canvas Scaler (1920x1080, match 0.5) y la fuente pixel
// (FuenteProyecto). Los textos sobre las placas rosas van en #2A1F40, sin contorno y a 36 como mínimo.
// PlacaPedidos va dentro de la caja del HUD (Canvas/CajaHUD, ConstruirCajaHUD) con el reloj, las quejas y el turbo.
public static class ConstruirInterfaz
{
    const string NombreUndo = "Construir interfaz";
    const string CarpetaUI = "Assets/Sprits/UI/";

    // Cada píxel del arte ocupa 4 unidades del Canvas (referencia 1920x1080)
    const int Escala = 4;

    static readonly Color32 Verde = new Color32(0x7B, 0xD8, 0x8F, 255);

    static readonly string[] SpritesNecesarios =
    {
        "carita_1", "carita_3", "carita_5",
        "medidor_pista", "medidor_relleno",
        "boton_pill_normal", "boton_pill_presionado",
        "boton_redondo_normal", "boton_redondo_presionado",
        "icono_pausa",
        "estrella_llena", "estrella_vacia",
    };

    static readonly string[] CamposUIManager =
        { "barraRelleno", "caritasMedidor", "textoDinero", "estrellaLlena", "estrellaVacia" };

    static Dictionary<string, Sprite> sprites;
    static float multiplicadorSliced;   // para que los bordes 9-slice también salgan a x4
    static TMP_FontAsset fuente;

    [MenuItem("Delivery Brenn's/Construir interfaz")]
    static void Construir()
    {
        // ---- Primero se revisa todo; si falta algo no se toca la escena ----
        sprites = new Dictionary<string, Sprite>();
        bool faltaAlgo = false;
        foreach (var nombre in SpritesNecesarios)
        {
            var ruta = CarpetaUI + nombre + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
            if (sprite == null)
            {
                Debug.LogError($"Falta el sprite {ruta} (o no está importado como Sprite).");
                faltaAlgo = true;
            }
            sprites[nombre] = sprite;
        }
        if (faltaAlgo)
        {
            Debug.LogError("No se construyó la interfaz porque faltan sprites.");
            return;
        }

        var canvas = BuscarCanvas();
        if (canvas == null)
        {
            Debug.LogError("No encuentro el Canvas de la escena.");
            return;
        }

        var todos = canvas.GetComponentsInChildren<RectTransform>(true);
        var panel = canvas.transform.Find("PanelFinal");   // solo para dejar el HUD debajo de él

        var textoPedidos = todos.FirstOrDefault(r => r.name == "TextoPedidos")?.GetComponent<TextMeshProUGUI>();
        if (textoPedidos == null)
        {
            Debug.LogError("No encuentro TextoPedidos (con TextMeshProUGUI) dentro del Canvas.");
            return;
        }

        var uiManager = Object.FindObjectsByType<UIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderByDescending(u => u.name == "UIManager").FirstOrDefault();
        if (uiManager == null)
        {
            Debug.LogError("No encuentro el objeto UIManager (con el script UIManager) en la escena.");
            return;
        }

        var so = new SerializedObject(uiManager);
        foreach (var campo in CamposUIManager)
        {
            if (so.FindProperty(campo) != null) continue;
            Debug.LogError($"El UIManager no tiene el campo \"{campo}\". No se construyó la interfaz.");
            return;
        }

        // Image.pixelsPerUnit = PPU del sprite / referencia del Canvas * multiplicador,
        // así que este valor hace que 1 píxel del borde mida exactamente Escala unidades
        multiplicadorSliced = canvas.referencePixelsPerUnit / (sprites["boton_pill_normal"].pixelsPerUnit * Escala);
        fuente = FuenteProyecto.Fuente();
        if (fuente == null)
        {
            Debug.LogError("No se pudo crear la fuente pixel (Assets/Fonts/PixelifySans SDF.asset).");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(NombreUndo);
        int grupo = Undo.GetCurrentGroup();

        // ---- Canvas Scaler: referencia 1920x1080, mitad ancho / mitad alto ----
        if (canvas.TryGetComponent(out CanvasScaler escalador))
        {
            Undo.RecordObject(escalador, NombreUndo);
            escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escalador.referenceResolution = new Vector2(1920, 1080);
            escalador.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            escalador.matchWidthOrHeight = 0.5f;
        }
        else Debug.LogWarning("El Canvas no tiene CanvasScaler.");

        // ---- Limpieza de intentos anteriores ----
        var textoDinero = todos.FirstOrDefault(r => r.name == "TextoDinero")?.GetComponent<TextMeshProUGUI>();

        // Los textos se sacan de sus placas antes de borrarlas, para no perderlos
        SacarDePlaca(textoPedidos, canvas.transform);
        if (textoDinero != null) SacarDePlaca(textoDinero, canvas.transform);

        // Lo del HUD son hijos directos del Canvas; los paneles (y lo que tengan dentro) no se tocan
        string[] delHUD = { "BarraSatisfaccion", "MedidorSatisfaccion", "PlacaPedidos", "PlacaDinero", "BotonPausa" };
        var caja = canvas.transform.Find(ConstruirCajaHUD.Nombre);
        var viejos = canvas.transform.Cast<Transform>().Concat(caja != null ? caja.Cast<Transform>() : Enumerable.Empty<Transform>())
            .Where(t => delHUD.Contains(t.name)).ToList();
        foreach (var viejo in viejos)
        {
            if (viejo != null) Undo.DestroyObjectImmediate(viejo.gameObject);
        }

        // ---- HUD ----
        var medidor = CrearMedidor(canvas.transform, out var relleno, out var caritas);

        var placaPedidos = CrearPlaca("PlacaPedidos", canvas.transform, new Vector2(0, 1), new Vector2(30, -38), 360);
        MeterEnPlaca(textoPedidos, placaPedidos);
        Undo.RecordObject(textoPedidos, NombreUndo);
        textoPedidos.text = "Pedidos 0/0";   // el UIManager lo reemplaza al empezar

        var botonPausa = CrearBotonRedondo("BotonPausa", canvas.transform, "icono_pausa");
        var rtPausa = (RectTransform)botonPausa.transform;
        rtPausa.anchorMin = rtPausa.anchorMax = rtPausa.pivot = new Vector2(1, 1);
        rtPausa.anchoredPosition = new Vector2(-30, -30);
        var menuJuego = SceneManager.GetActiveScene().GetRootGameObjects()
            .Select(g => g.GetComponent<MenuJuego>()).FirstOrDefault(m => m != null);
        if (menuJuego != null) UnityEventTools.AddPersistentListener(botonPausa.onClick, menuJuego.AlternarPausa);
        else Debug.LogWarning("No hay MenuJuego: BotonPausa queda sin conectar hasta ejecutar \"Construir pausa\".");

        // la placa del dinero queda a la izquierda del botón, centrada con él en vertical
        float xDinero = -30 - rtPausa.sizeDelta.x - 16;
        var placaDinero = CrearPlaca("PlacaDinero", canvas.transform, new Vector2(1, 1), new Vector2(xDinero, -38), 240);
        if (textoDinero == null)
        {
            var rt = CrearHijo("TextoDinero", placaDinero);
            textoDinero = rt.gameObject.AddComponent<TextMeshProUGUI>();
            textoDinero.font = fuente;
        }
        MeterEnPlaca(textoDinero, placaDinero);
        Undo.RecordObject(textoDinero, NombreUndo);
        textoDinero.text = "$0";

        // El HUD va antes que PanelFinal en la jerarquía, para que la pantalla final lo tape
        if (panel != null)
        {
            foreach (var t in new Transform[] { medidor, placaPedidos, placaDinero, botonPausa.transform })
                t.SetSiblingIndex(panel.GetSiblingIndex());
        }

        // Pedidos (y lo que ya exista del reloj, las quejas y el turbo) a la caja del HUD, arriba a la izquierda
        ConstruirCajaHUD.QuitarGuiaVieja(canvas);
        ConstruirCajaHUD.Acomodar(canvas);

        // ---- Conexiones con el UIManager ----
        so.Update();
        so.FindProperty("barraRelleno").objectReferenceValue = relleno;
        var lista = so.FindProperty("caritasMedidor");
        lista.arraySize = caritas.Length;
        for (int i = 0; i < caritas.Length; i++)
            lista.GetArrayElementAtIndex(i).objectReferenceValue = caritas[i];
        so.FindProperty("textoDinero").objectReferenceValue = textoDinero;

        // las estrellas solo si están vacías; lo demás que ya esté lleno se respeta
        var llena = so.FindProperty("estrellaLlena");
        if (llena.objectReferenceValue == null) llena.objectReferenceValue = sprites["estrella_llena"];
        var vacia = so.FindProperty("estrellaVacia");
        if (vacia.objectReferenceValue == null) vacia.objectReferenceValue = sprites["estrella_vacia"];
        so.ApplyModifiedProperties();

        Undo.CollapseUndoOperations(grupo);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("Interfaz construida y conectada al UIManager. Guarda la escena para conservar los cambios.");
    }

    // Se prefiere el Canvas donde ya vive TextoPedidos o PanelFinal
    static Canvas BuscarCanvas()
    {
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(c => c.isRootCanvas).ToList();
        return canvases.FirstOrDefault(c => c.GetComponentsInChildren<RectTransform>(true)
                   .Any(r => r.name == "TextoPedidos" || r.name == "PanelFinal"))
               ?? canvases.FirstOrDefault(c => c.name == "Canvas")
               ?? canvases.FirstOrDefault();
    }

    // ------------------------------------------------------------------
    // Medidor de satisfacción: tres caritas arriba y la barra debajo
    // ------------------------------------------------------------------
    static RectTransform CrearMedidor(Transform canvas, out Image relleno, out Image[] caritas)
    {
        const int ancho = 100 * Escala;      // la barra mide 100 px de arte
        const int ladoCarita = 18 * Escala;
        const int separacion = 2 * Escala;
        const int altoBarra = 12 * Escala;

        var medidor = CrearHijo("MedidorSatisfaccion", canvas);
        medidor.anchorMin = medidor.anchorMax = new Vector2(0.5f, 1);
        medidor.pivot = new Vector2(0.5f, 1);
        medidor.anchoredPosition = new Vector2(0, -30);
        medidor.sizeDelta = new Vector2(ancho, ladoCarita + separacion + altoBarra);

        var fila = CrearHijo("Caritas", medidor);
        ArribaIzquierda(fila, Vector2.zero, new Vector2(ancho, ladoCarita));
        var layout = fila.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.spacing = 8;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // de peor a mejor, en el orden que espera el UIManager
        string[] nombres = { "carita_1", "carita_3", "carita_5" };
        caritas = new Image[nombres.Length];
        for (int i = 0; i < nombres.Length; i++)
        {
            var rt = CrearHijo("Carita" + (i + 1), fila);
            rt.sizeDelta = new Vector2(ladoCarita, ladoCarita);
            var img = CrearImagen(rt, nombres[i]);
            img.preserveAspect = true;
            caritas[i] = img;
        }

        var pista = CrearHijo("Pista", medidor);
        ArribaIzquierda(pista, new Vector2(0, -(ladoCarita + separacion)), new Vector2(ancho, altoBarra));
        CrearImagen(pista, "medidor_pista");

        // mismo tamaño y posición que la pista; el UIManager le cambia fillAmount y color
        var rtRelleno = CrearHijo("Relleno", pista);
        rtRelleno.anchorMin = Vector2.zero;
        rtRelleno.anchorMax = Vector2.one;
        rtRelleno.pivot = new Vector2(0.5f, 0.5f);
        rtRelleno.offsetMin = rtRelleno.offsetMax = Vector2.zero;
        relleno = CrearImagen(rtRelleno, "medidor_relleno");
        relleno.type = Image.Type.Filled;
        relleno.fillMethod = Image.FillMethod.Horizontal;
        relleno.fillOrigin = (int)Image.OriginHorizontal.Left;
        relleno.fillAmount = 1;
        relleno.color = Verde;

        return medidor;
    }

    // ------------------------------------------------------------------
    // Placas de píldora para los textos de arriba
    // ------------------------------------------------------------------
    static RectTransform CrearPlaca(string nombre, Transform canvas, Vector2 esquina, Vector2 posicion, float ancho)
    {
        var placa = CrearHijo(nombre, canvas);
        placa.anchorMin = placa.anchorMax = placa.pivot = esquina;
        placa.anchoredPosition = posicion;
        placa.sizeDelta = new Vector2(ancho, 18 * Escala);
        CrearImagenSliced(placa, "boton_pill_normal");
        return placa;
    }

    static void MeterEnPlaca(TextMeshProUGUI texto, RectTransform placa)
    {
        var rt = (RectTransform)texto.transform;
        if (rt.parent != placa) Undo.SetTransformParent(rt, placa, NombreUndo);
        Undo.RecordObject(rt, NombreUndo);
        RellenarSobrePildora(rt);

        Undo.RecordObject(texto, NombreUndo);
        FuenteProyecto.EstiloPastilla(texto);
    }

    // Si el texto quedó dentro de una placa de una ejecución anterior, se devuelve al Canvas
    static void SacarDePlaca(TextMeshProUGUI texto, Transform canvas)
    {
        var padre = texto.transform.parent;
        if (padre != null && padre.name.StartsWith("Placa"))
            Undo.SetTransformParent(texto.transform, canvas, NombreUndo);
    }

    // Ocupa la píldora dejando libres las tapas y el canto de abajo
    static void RellenarSobrePildora(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(6 * Escala, 3 * Escala);
        rt.offsetMax = new Vector2(-6 * Escala, -1 * Escala);
    }

    // ------------------------------------------------------------------
    // Botones
    // ------------------------------------------------------------------
    static Button CrearBotonRedondo(string nombre, Transform padre, string icono)
    {
        var rt = CrearHijo(nombre, padre);
        rt.sizeDelta = new Vector2(20 * Escala, 22 * Escala);
        var fondo = CrearImagen(rt, "boton_redondo_normal");
        fondo.raycastTarget = true;
        var boton = CrearBoton(rt, fondo, "boton_redondo_presionado");

        // el ícono de 12x12 va centrado sobre el círculo, a 4 px de arte del borde
        var rtIcono = CrearHijo("Icono", rt);
        ArribaIzquierda(rtIcono, new Vector2(4 * Escala, -4 * Escala), new Vector2(12 * Escala, 12 * Escala));
        CrearImagen(rtIcono, icono);
        return boton;
    }

    // Al pulsar se cambia al sprite hundido
    static Button CrearBoton(RectTransform rt, Image fondo, string spritePresionado)
    {
        var boton = rt.gameObject.AddComponent<Button>();
        boton.targetGraphic = fondo;
        boton.transition = Selectable.Transition.SpriteSwap;
        var estados = boton.spriteState;
        estados.pressedSprite = sprites[spritePresionado];
        boton.spriteState = estados;
        return boton;
    }

    // ------------------------------------------------------------------
    // Ayudantes
    // ------------------------------------------------------------------
    static RectTransform CrearHijo(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, NombreUndo);
        go.layer = padre.gameObject.layer;   // capa UI, igual que el Canvas
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
    }

    // Imagen decorativa: no bloquea clics (los botones lo vuelven a activar)
    static Image CrearImagen(RectTransform rt, string sprite)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprites[sprite];
        img.raycastTarget = false;
        return img;
    }

    static Image CrearImagenSliced(RectTransform rt, string sprite)
    {
        var img = CrearImagen(rt, sprite);
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = multiplicadorSliced;
        return img;
    }

    static void ArribaIzquierda(RectTransform rt, Vector2 posicion, Vector2 tam)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = posicion;
        rt.sizeDelta = tam;
    }

    // Ancla y pivote al centro, con la posición en Y y el tamaño indicados
    static void Centrar(RectTransform rt, float y, float ancho, float alto)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = new Vector2(ancho, alto);
    }
}
