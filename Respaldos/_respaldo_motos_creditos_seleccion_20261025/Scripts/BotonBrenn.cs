using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// El estilo único de los botones del juego (arte de pixel_town/generate_menu_letreros.py; los viste ConstruirBotones en
// todas las escenas, ConstruirMenuPrincipal en el menú y TiendaMotos al armar su ventana, con Vestir y EstiloBrenn).
// Variantes: Normal (#2A1F40 al 85% con borde rosa de 2 px y texto crema), Principal (borde rosa de 3 px con halo y
// texto dorado), Peligro (borde rojo #FF5D73: "Salir", "Reiniciar progreso"), Icono (cuadrado, sin texto ni tijera) y
// Tarjeta (las tarjetas de nivel: solo un marco encima).
// Un solo estado "elegido" para el ratón y el teclado: pasar el ratón encima lo elige en el EventSystem y salir lo suelta;
// las flechas lo eligen igual. Elegido: borde y texto dorados #FFD166 y la tijera del letrero parpadeando 2 veces por
// segundo a su izquierda. Pulsado (clic o Enter): baja 3 px. Marcado (la pestaña activa, el botón resaltado del panel
// final): se ve como Principal. El clic suena con el AudioManager (el ratón ya lo detecta solo; Enter lo pide aquí).
public class BotonBrenn : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
                          ISubmitHandler
{
    public enum Variante { Normal, Principal, Peligro, Icono, Tarjeta }

    public const float Bajada = 3f;
    const float PeriodoTijera = 0.5f;   // 2 parpadeos por segundo
    public const float LadoTijera = 56f, SeparacionTijera = 30f;
    static readonly Color Apagado = new Color(0.55f, 0.5f, 0.62f, 1f);

    [SerializeField] private Variante variante;
    [SerializeField] private Image borde;
    [SerializeField] private Sprite spriteNormal;
    [SerializeField] private Sprite spriteElegido;
    [SerializeField] private Sprite spriteMarcado;
    [SerializeField] private Sprite spriteMarcadoElegido;
    [SerializeField] private TMP_Text texto;
    [SerializeField] private Color colorTexto = new Color32(0xFF, 0xF1, 0xD0, 0xFF);
    [SerializeField] private Color colorElegido = new Color32(0xFF, 0xD1, 0x66, 0xFF);
    [SerializeField] private Image tijera;
    [SerializeField] private Graphic[] glifos;   // dibujos de un icono (las barras de pausa) que se pintan como el texto

    private RectTransform rt;
    private bool pulsado, marcado;
    private float desde;
    private int pintado = -1;   // nada pintado todavía: el primer Update pinta siempre

    public Variante VarianteDelBoton => variante;
    public bool Elegido => EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
    public bool Pulsado => pulsado;
    public bool Marcado => marcado;
    public bool TijeraVisible => tijera != null && tijera.enabled;
    public Image Borde => borde;
    public TMP_Text Texto => texto;

    private void Awake() => rt = (RectTransform)transform;

    private void OnDisable()
    {
        Soltar();
        pintado = -1;
        if (tijera != null) tijera.enabled = false;
    }

    private void Update()
    {
        bool activo = Interactivo;
        bool elegido = Elegido && activo;
        Pintar((elegido ? 1 : 0) | (marcado ? 2 : 0) | (activo ? 0 : 4));
        if (tijera != null) tijera.enabled = elegido && Mathf.Repeat(Time.unscaledTime - desde, PeriodoTijera) < PeriodoTijera / 2f;
    }

    // Teclado: con nada elegido, la primera flecha (o Tab) elige el botón visible que toca: el marcado o Principal, si no
    // el de texto más arriba (y más a la izquierda), y al final iconos y tarjetas; los tapados por un panel no cuentan. Después del EventSystem, para que esa
    // misma flecha no mueva ya desde él.
    static int cuadroTeclado = -1;
    static readonly System.Collections.Generic.List<RaycastResult> golpes = new System.Collections.Generic.List<RaycastResult>();

    private void LateUpdate()
    {
        var es = EventSystem.current;
        var k = UnityEngine.InputSystem.Keyboard.current;
        if (es == null || k == null || cuadroTeclado == Time.frameCount) return;
        if (es.currentSelectedGameObject != null && es.currentSelectedGameObject.activeInHierarchy) return;
        if (!(k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame
              || k.rightArrowKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame)) return;
        cuadroTeclado = Time.frameCount;
        BotonBrenn mejor = null;
        foreach (var b in FindObjectsByType<BotonBrenn>(FindObjectsSortMode.None))
        {
            if (!b.isActiveAndEnabled || !b.Interactivo || !b.Visible(es)) continue;
            if (mejor == null || b.Antes(mejor)) mejor = b;
        }
        if (mejor != null) es.SetSelectedGameObject(mejor.gameObject);
    }

    // sin nada encima en su centro (un panel abierto tapa lo de atrás)
    private bool Visible(EventSystem es)
    {
        golpes.Clear();
        es.RaycastAll(new PointerEventData(es) { position = RectTransformUtility.WorldToScreenPoint(null, transform.position) }, golpes);
        return golpes.Count > 0 && golpes[0].gameObject.transform.IsChildOf(transform);
    }

    private bool Antes(BotonBrenn otro)
    {
        // los iconos (el de pausa del HUD) y las tarjetas, al final: primero los botones con texto del panel abierto
        int Peso(BotonBrenn b) => b.marcado ? 0 : b.variante == Variante.Principal ? 1 : b.variante == Variante.Icono || b.variante == Variante.Tarjeta ? 3 : 2;
        if (Peso(this) != Peso(otro)) return Peso(this) < Peso(otro);
        Vector2 a = transform.position, b2 = otro.transform.position;
        return Mathf.Abs(a.y - b2.y) > 1f ? a.y > b2.y : a.x < b2.x;
    }

    private bool Interactivo
    {
        get
        {
            var s = GetComponent<Selectable>();
            return s == null || s.IsInteractable();
        }
    }

    // La pestaña activa o el botón que hay que ver primero: aspecto de Principal
    public void Marcar(bool m)
    {
        marcado = m;
        pintado = -1;
    }

    private void Pintar(int estado)
    {
        if (estado == pintado) return;
        bool elegido = (estado & 1) != 0, marca = (estado & 2) != 0, apagado = (estado & 4) != 0;
        if (elegido && (pintado < 0 || (pintado & 1) == 0)) desde = Time.unscaledTime;
        pintado = estado;
        if (borde != null)
        {
            var s = marca && spriteMarcado != null ? (elegido ? spriteMarcadoElegido : spriteMarcado) : (elegido ? spriteElegido : spriteNormal);
            if (s != null) borde.sprite = s;
            borde.color = apagado ? Apagado : Color.white;
        }
        if (texto != null)
        {
            var c = elegido || marca ? colorElegido : colorTexto;
            if (apagado) c.a = 0.55f;
            texto.color = c;
        }
        if (glifos != null)
            foreach (var g in glifos)
            {
                if (g == null) continue;
                var c = elegido || marca ? colorElegido : colorTexto;
                if (apagado) c.a = 0.55f;
                g.color = c;
            }
    }

    public Graphic[] Glifos => glifos;

    // Los dibujos del icono que cambian de color con el estado (crema / dorado), como el texto (lo usa EstiloHUD)
    public void PonerGlifos(params Graphic[] g)
    {
        glifos = g;
        pintado = -1;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (Interactivo && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (Elegido && !pulsado) EventSystem.current.SetSelectedGameObject(null);
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || !Interactivo) return;
        Hundir();
    }

    public void OnPointerUp(PointerEventData e)
    {
        Soltar();
        // soltado fuera del botón: deja de estar elegido (como al salir sin pulsar)
        var encima = e.pointerCurrentRaycast.gameObject;
        if (Elegido && (encima == null || !encima.transform.IsChildOf(transform))) EventSystem.current.SetSelectedGameObject(null);
    }

    // Enter con el teclado: el mismo hundido, un momento, y el clic
    public void OnSubmit(BaseEventData e)
    {
        if (!isActiveAndEnabled || !Interactivo) return;
        AudioManager.Sonar("ui_clic");
        StartCoroutine(HundirUnMomento());
    }

    private IEnumerator HundirUnMomento()
    {
        Hundir();
        yield return new WaitForSecondsRealtime(0.1f);
        Soltar();
    }

    // relativo a donde esté (los paneles acomodan sus botones al abrir: no se guarda una posición fija)
    private void Hundir()
    {
        if (pulsado || rt == null) return;
        pulsado = true;
        rt.anchoredPosition += Vector2.down * Bajada;
    }

    private void Soltar()
    {
        if (!pulsado || rt == null) return;
        pulsado = false;
        rt.anchoredPosition += Vector2.up * Bajada;
    }

    // ------------------------------------------------------------------ vestir un botón (editor y juego)

    // Le pone el estilo a un botón existente sin tocar su nombre, onClick ni referencias: el 9-slice de la variante a x1,
    // la tijera (Normal, Principal, Peligro), el texto en Press Start (crema; dorado en Principal) y este componente.
    // tam: el tamaño nuevo (null: el que tiene); tamTexto: 0 deja el tamaño del texto. Idempotente.
    public static BotonBrenn Vestir(Button boton, Variante v, EstiloBrenn e, Vector2? tam = null, float tamTexto = 0f)
    {
        var rtBoton = (RectTransform)boton.transform;
        if (tam.HasValue) rtBoton.sizeDelta = tam.Value;
        var canvas = boton.GetComponentInParent<Canvas>();
        float referencia = canvas != null ? canvas.rootCanvas.referencePixelsPerUnit : 100f;
        var (normal, elegido) = e.Sprites(v);

        Image img;
        if (v == Variante.Tarjeta)
        {
            // la tarjeta conserva su dibujo; el marco va encima (sin tapar clics)
            var marco = boton.transform.Find("Borde");
            if (marco == null)
            {
                marco = new GameObject("Borde", typeof(RectTransform)).transform;
                marco.SetParent(boton.transform, false);
            }
            marco.SetAsLastSibling();
            var rtMarco = (RectTransform)marco;
            rtMarco.anchorMin = Vector2.zero;
            rtMarco.anchorMax = Vector2.one;
            rtMarco.offsetMin = rtMarco.offsetMax = Vector2.zero;
            img = Obtener<Image>(marco.gameObject);
            img.raycastTarget = false;
        }
        else
        {
            img = boton.targetGraphic is Image propia && propia != null && propia.gameObject == boton.gameObject ? propia : Obtener<Image>(boton.gameObject);
            boton.targetGraphic = img;
        }
        img.sprite = normal;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = referencia / normal.pixelsPerUnit;
        img.color = Color.white;
        img.preserveAspect = false;
        boton.transition = Selectable.Transition.None;   // los estados los pinta BotonBrenn
        boton.spriteState = new SpriteState();

        // la tijera: 56 px, 30 px a la izquierda, centrada en el alto
        var t = boton.transform.Find("Tijera");
        Image tij = null;
        if (v == Variante.Normal || v == Variante.Principal || v == Variante.Peligro)
        {
            if (t == null)
            {
                t = new GameObject("Tijera", typeof(RectTransform)).transform;
                t.SetParent(boton.transform, false);
            }
            var rtTij = (RectTransform)t;
            rtTij.anchorMin = rtTij.anchorMax = new Vector2(0f, 0.5f);
            rtTij.pivot = new Vector2(1f, 0.5f);
            rtTij.sizeDelta = Vector2.one * LadoTijera;
            rtTij.anchoredPosition = new Vector2(-SeparacionTijera, 0f);
            rtTij.localScale = Vector3.one;
            tij = Obtener<Image>(t.gameObject);
            tij.sprite = e.tijera;
            tij.raycastTarget = false;
            tij.enabled = false;
        }
        else if (t != null) Destruir(t.gameObject);

        // el texto (el primero que no sea de un icono)
        TMP_Text tmp = null;
        if (v != Variante.Icono && v != Variante.Tarjeta)
        {
            foreach (var x in boton.GetComponentsInChildren<TMP_Text>(true))
                if (x.transform.parent == boton.transform || x.transform.parent.parent == boton.transform) { tmp = x; break; }
        }
        Color colorTexto = v == Variante.Principal ? e.dorado : e.crema;
        if (tmp != null)
        {
            if (e.fuenteTitulos != null && tmp.font != e.fuenteTitulos) tmp.font = e.fuenteTitulos;
            if (e.fuenteTitulos != null) tmp.fontSharedMaterial = e.fuenteTitulos.material;   // sin el contorno de la pastilla
            if (tamTexto > 0f)
            {
                tmp.enableAutoSizing = false;
                tmp.fontSize = tmp.fontSizeMin = tmp.fontSizeMax = tamTexto;
            }
            tmp.fontStyle = FontStyles.Normal;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.color = colorTexto;
            if (tmp.transform.parent == boton.transform && tmp.transform != boton.transform)
            {
                var rtTexto = tmp.rectTransform;
                rtTexto.anchorMin = Vector2.zero;
                rtTexto.anchorMax = Vector2.one;
                rtTexto.pivot = new Vector2(0.5f, 0.5f);
                rtTexto.offsetMin = new Vector2(12, 4);
                rtTexto.offsetMax = new Vector2(-12, -4);
            }
        }

        var bb = Obtener<BotonBrenn>(boton.gameObject);
        var (marcado, marcadoElegido) = e.Sprites(v == Variante.Tarjeta ? Variante.Tarjeta : v == Variante.Icono ? Variante.Icono : Variante.Principal);
        bb.variante = v;
        bb.borde = img;
        bb.spriteNormal = normal;
        bb.spriteElegido = elegido;
        bb.spriteMarcado = marcado;
        bb.spriteMarcadoElegido = marcadoElegido;
        bb.texto = tmp;
        bb.colorTexto = colorTexto;
        bb.colorElegido = e.dorado;
        bb.tijera = tij;
        bb.pintado = -1;
        return bb;
    }

    static T Obtener<T>(GameObject g) where T : Component => g.TryGetComponent(out T c) ? c : g.AddComponent<T>();

    static void Destruir(GameObject g)
    {
        if (Application.isPlaying) Destroy(g);
        else DestroyImmediate(g);
    }
}
