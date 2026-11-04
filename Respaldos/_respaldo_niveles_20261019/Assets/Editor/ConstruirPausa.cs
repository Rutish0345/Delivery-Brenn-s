using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Crea el objeto MenuJuego y el PanelPausa (con el mismo estilo que PanelFinal)
// y conecta los botones de pausa, seguir, reiniciar y jugar de nuevo.
// Se puede ejecutar varias veces: PanelPausa y MenuJuego se borran y se rehacen,
// y a los botones existentes se les limpian los listeners antes de conectarlos.
public static class ConstruirPausa
{
    const string NombreUndo = "Construir pausa";
    const string CarpetaUI = "Assets/Sprits/UI/";

    // Igual que en ConstruirInterfaz: cada píxel del arte ocupa 4 unidades del Canvas
    const int Escala = 4;

    static readonly Color32 Crema = new Color32(0xFF, 0xF1, 0xD0, 255);
    static readonly Color32 Oscuro = new Color32(0x2A, 0x1F, 0x40, 255);
    static readonly Color32 RosaTitulo = new Color32(0xC2, 0x41, 0x7A, 255);

    [MenuItem("Delivery Brenn's/Construir pausa")]
    static void Construir()
    {
        // ---- Primero se revisa todo; si falta algo no se toca la escena ----
        var marco = CargarSprite("marco_panel");
        var pildora = CargarSprite("boton_pill_normal");
        var pildoraPresionada = CargarSprite("boton_pill_presionado");
        if (marco == null || pildora == null || pildoraPresionada == null)
        {
            Debug.LogError("No se construyó la pausa porque faltan sprites.");
            return;
        }

        var canvas = BuscarCanvas();
        if (canvas == null)
        {
            Debug.LogError("No encuentro el Canvas de la escena.");
            return;
        }

        var enCanvas = canvas.GetComponentsInChildren<RectTransform>(true);
        var panelFinal = enCanvas.FirstOrDefault(r => r.name == "PanelFinal");
        if (panelFinal == null)
        {
            Debug.LogError("No encuentro PanelFinal dentro del Canvas.");
            return;
        }

        var botonPausa = BuscarBoton(enCanvas, "BotonPausa", "el Canvas");
        var botonJugar = BuscarBoton(panelFinal.GetComponentsInChildren<RectTransform>(true), "BotonJugarDeNuevo", "PanelFinal");
        if (botonPausa == null || botonJugar == null)
        {
            Debug.LogError("Ejecuta antes \"Delivery Brenn's > Construir interfaz\" para crear los botones.");
            return;
        }

        if (typeof(MenuJuego).GetField("panelPausa", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) == null)
        {
            Debug.LogError("MenuJuego no tiene el campo \"panelPausa\". No se construyó la pausa.");
            return;
        }

        // Si hay otro MenuJuego puesto en un objeto con otro nombre, no se borra (puede tener
        // más cosas), pero se avisa: con dos, Escape pausaría y despausaría en el mismo frame
        foreach (var otro in Object.FindObjectsByType<MenuJuego>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .Where(m => m.name != "MenuJuego"))
            Debug.LogWarning($"Hay otro MenuJuego en \"{otro.name}\". Quítalo para que Escape no se procese dos veces.", otro);

        // la fuente se toma del título del panel final, para que todo combine
        var tituloFinal = panelFinal.Find("Titulo");
        var fuente = tituloFinal != null && tituloFinal.TryGetComponent(out TextMeshProUGUI tmpFinal) ? tmpFinal.font : null;

        // Image.pixelsPerUnit = PPU del sprite / referencia del Canvas * multiplicador,
        // así que con este valor el borde 9-slice también sale a x4
        float multiplicador = canvas.referencePixelsPerUnit / (pildora.pixelsPerUnit * Escala);

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(NombreUndo);
        int grupo = Undo.GetCurrentGroup();

        // ---- Limpieza de ejecuciones anteriores ----
        foreach (var viejo in enCanvas.Where(r => r != null && r.name == "PanelPausa").ToList())
            if (viejo != null) Undo.DestroyObjectImmediate(viejo.gameObject);

        foreach (var viejo in canvas.gameObject.scene.GetRootGameObjects().Where(g => g.name == "MenuJuego").ToList())
            Undo.DestroyObjectImmediate(viejo);

        // ---- MenuJuego ----
        var goMenu = new GameObject("MenuJuego");
        Undo.RegisterCreatedObjectUndo(goMenu, NombreUndo);
        SceneManager.MoveGameObjectToScene(goMenu, canvas.gameObject.scene);
        var menu = goMenu.AddComponent<MenuJuego>();

        // ---- PanelPausa ----
        var panel = CrearHijo("PanelPausa", canvas.transform);
        Centrar(panel, 0, 700, 520);
        var fondo = panel.gameObject.AddComponent<Image>();
        fondo.sprite = marco;
        fondo.type = Image.Type.Sliced;
        fondo.fillCenter = true;
        fondo.pixelsPerUnitMultiplier = multiplicador;

        var rtTitulo = CrearHijo("Titulo", panel);
        Centrar(rtTitulo, 150, 600, 110);
        var titulo = CrearTexto(rtTitulo, "PAUSA", 72, RosaTitulo, Crema, fuente);
        titulo.textWrappingMode = TextWrappingModes.NoWrap;

        // uno debajo del otro, con 48 unidades de aire entre ellos
        const float anchoBoton = 400;
        const float altoBoton = 18 * Escala;
        var seguir = CrearBoton("BotonSeguir", panel, "SEGUIR", 10, anchoBoton, altoBoton,
                                pildora, pildoraPresionada, multiplicador, fuente);
        var reiniciar = CrearBoton("BotonReiniciar", panel, "REINICIAR", 10 - altoBoton - 48, anchoBoton, altoBoton,
                                   pildora, pildoraPresionada, multiplicador, fuente);

        // por encima de todo el HUD y también del PanelFinal
        panel.SetAsLastSibling();
        panel.gameObject.SetActive(false);

        // ---- Conexiones ----
        var soMenu = new SerializedObject(menu);
        soMenu.FindProperty("panelPausa").objectReferenceValue = panel.gameObject;
        soMenu.ApplyModifiedPropertiesWithoutUndo();   // el objeto es nuevo: deshacer su creación ya basta

        Conectar(botonPausa, menu.AlternarPausa);
        Conectar(seguir, menu.AlternarPausa);
        Conectar(reiniciar, menu.Reiniciar);
        Conectar(botonJugar, menu.Reiniciar);

        Undo.CollapseUndoOperations(grupo);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("Pausa lista: MenuJuego creado, PanelPausa armado y botones conectados. Guarda la escena.");
    }

    // Deja el onClick solo con la llamada indicada (se borran los listeners que tuviera)
    static void Conectar(Button boton, UnityAction accion)
    {
        Undo.RecordObject(boton, NombreUndo);
        for (int i = boton.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(boton.onClick, i);
        UnityEventTools.AddVoidPersistentListener(boton.onClick, accion);
        PrefabUtility.RecordPrefabInstancePropertyModifications(boton);
    }

    static Sprite CargarSprite(string nombre)
    {
        var ruta = CarpetaUI + nombre + ".png";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        if (sprite == null) Debug.LogError($"Falta el sprite {ruta} (o no está importado como Sprite).");
        return sprite;
    }

    static Button BuscarBoton(RectTransform[] donde, string nombre, string lugar)
    {
        var rt = donde.FirstOrDefault(r => r.name == nombre);
        if (rt == null)
        {
            Debug.LogError($"No encuentro {nombre} dentro de {lugar}.");
            return null;
        }
        if (!rt.TryGetComponent(out Button boton))
        {
            Debug.LogError($"{nombre} no tiene componente Button.");
            return null;
        }
        return boton;
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

    // Botón píldora con su texto crema; al pulsar cambia al sprite hundido como los demás
    static Button CrearBoton(string nombre, Transform padre, string texto, float y, float ancho, float alto,
                             Sprite normal, Sprite presionado, float multiplicador, TMP_FontAsset fuente)
    {
        var rt = CrearHijo(nombre, padre);
        Centrar(rt, y, ancho, alto);

        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = normal;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = multiplicador;

        var boton = rt.gameObject.AddComponent<Button>();
        boton.targetGraphic = img;
        boton.transition = Selectable.Transition.SpriteSwap;
        var estados = boton.spriteState;
        estados.pressedSprite = presionado;
        boton.spriteState = estados;

        // el texto deja libres las tapas y el canto de abajo de la píldora
        var rtTexto = CrearHijo("Texto", rt);
        rtTexto.anchorMin = Vector2.zero;
        rtTexto.anchorMax = Vector2.one;
        rtTexto.pivot = new Vector2(0.5f, 0.5f);
        rtTexto.offsetMin = new Vector2(6 * Escala, 3 * Escala);
        rtTexto.offsetMax = new Vector2(-6 * Escala, -1 * Escala);
        var tmp = CrearTexto(rtTexto, texto, 36, Crema, Oscuro, fuente);
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return boton;
    }

    static TextMeshProUGUI CrearTexto(RectTransform rt, string texto, float tam, Color color, Color contorno,
                                      TMP_FontAsset fuente)
    {
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (fuente != null) tmp.font = fuente;
        tmp.text = texto;
        tmp.fontSize = tam;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = contorno;
        return tmp;
    }

    static RectTransform CrearHijo(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, NombreUndo);
        go.layer = padre.gameObject.layer;   // capa UI, igual que el Canvas
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
    }

    // Ancla y pivote al centro, con la posición en Y y el tamaño indicados
    static void Centrar(RectTransform rt, float y, float ancho, float alto)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = new Vector2(ancho, alto);
    }
}
