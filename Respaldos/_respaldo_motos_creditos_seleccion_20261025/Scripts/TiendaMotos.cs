using TMPro;
using UnityEngine;
using UnityEngine.UI;

// La ventana para comprar y equipar motos, armada al jugar. Dos modos:
//  - Tienda: se abre desde "Tienda de motos" del panel final (solo si mostrarBotonEnFinal) y se cierra con Cerrar.
//  - AntesDeNivel: la abre el LevelManager al arrancar un nivel con motos: "ELIGE TU MOTO" y, en vez de
//    Cerrar, "EMPEZAR NIVEL", que cierra la ventana y avisa al LevelManager para que arranque el nivel.
// Con el estilo único (EstiloBrenn en Resources, lo deja ConstruirBotones): ventana #2A1F40 con borde rosa, tarjetas
// oscuras con textos crema y rosa, y botones BotonBrenn (abajo 424x72 con texto de 28; en las tarjetas, 64 de alto con
// texto de 14, para que "Comprar $1500" quepa).
// Cada tarjeta lleva dos barras de 5 cuadritos (estilo pixel: cuadro oscuro con relleno) con la velocidad y la
// resistencia a choques relativas de la moto (SegmentosVelocidad / SegmentosResistencia).
public class TiendaMotos : MonoBehaviour
{
    public enum Modo { Tienda, AntesDeNivel }

    [SerializeField] private Sprite[] iconos;      // mismo orden que Progreso.Motos
    [SerializeField] private TMP_FontAsset fuente;         // opcional: descripciones
    [SerializeField] private TMP_FontAsset fuenteTitulos;  // opcional: título, nombres y botones (Press Start 2P)
    [SerializeField] private bool mostrarBotonEnFinal = true;  // "Tienda de motos" en el panel final (no en el nivel 1, a pie)

    [Header("Estilo (se copia del panel final con el menú Configurar motos)")]
    [SerializeField] private Sprite marcoPanel;
    [SerializeField] private float multiplicadorMarco = 1f;
    [SerializeField] private Sprite pildora;
    [SerializeField] private float multiplicadorPildora = 1f;

    private static readonly Color Oscuro = new Color32(0x2A, 0x1F, 0x40, 255);
    private static readonly Color Morado = new Color32(0x8C, 0x52, 0xFF, 255);
    private static readonly Color Crema = new Color32(0xFF, 0xF1, 0xD0, 255);
    private static readonly Color Tarjeta = new Color32(0xFF, 0xE3, 0xB0, 255);
    private static readonly Color Rosa = new Color32(0xFF, 0x6F, 0xA5, 255);
    private static readonly Color Vacio = new Color32(0xE6, 0xC4, 0x8C, 255);

    public const int Segmentos = 5;
    // 5.0 → 2, 5.5 → 3, 6.0 → 4, 6.5 → 5 cuadritos
    public static int SegmentosVelocidad(Progreso.DatosMoto d) => Mathf.Clamp(Mathf.RoundToInt((d.velocidad - 4f) / 0.5f), 1, Segmentos);
    // choques x0.5 → 5, x1.0 → 3, x1.5 → 1 cuadritos
    public static int SegmentosResistencia(Progreso.DatosMoto d) => Mathf.Clamp(Mathf.RoundToInt(5f - (d.factorPenalizacion - 0.5f) * 4f), 1, Segmentos);

    private static readonly Color TarjetaOscura = new Color32(0x3A, 0x2C, 0x58, 255);
    private EstiloBrenn estilo;   // null: el estilo de antes (pastillas y marco crema)
    private Color TextoOscuro => estilo != null ? estilo.crema : Oscuro;     // el texto que iba oscuro sobre crema
    private Color TextoMorado => estilo != null ? estilo.rosa : Morado;

    private GameObject ventana;
    private TextMeshProUGUI textoDinero;
    private Button[] botones;
    private TextMeshProUGUI[] etiquetas;
    private Button botonAPie;
    private TextMeshProUGUI titulo;
    private Button botonCerrar;
    private Button botonEmpezar;
    private System.Action alEmpezar;

    public bool Abierta => ventana != null && ventana.activeSelf;
    public Modo ModoActual { get; private set; } = Modo.Tienda;

    private void Start()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // el LevelManager puede pedir la ventana en su Start antes que este Start (orden de ejecución)
        if (ventana == null) ConstruirVentana(canvas.transform);
        ConstruirBotonAbrir(canvas.transform);
        if (ModoActual == Modo.Tienda && alEmpezar == null) ventana.SetActive(false);
    }

    private bool Construida()
    {
        if (ventana != null) return true;
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return false;
        ConstruirVentana(canvas.transform);
        ventana.SetActive(false);
        return true;
    }

    private void OnEnable() { Progreso.Cambio += Refrescar; }
    private void OnDisable() { Progreso.Cambio -= Refrescar; }

    // Desde el panel final: comprar y equipar, y Cerrar
    public void Abrir() { Mostrar(Modo.Tienda, null); }

    // Antes de un nivel con motos: elegir y "EMPEZAR NIVEL" (cierra la ventana y llama a alEmpezar)
    public void AbrirAntesDeNivel(System.Action alEmpezar) { Mostrar(Modo.AntesDeNivel, alEmpezar); }

    public void Cerrar() { if (ventana != null) ventana.SetActive(false); }

    private void Mostrar(Modo modo, System.Action alEmpezar)
    {
        if (!Construida()) return;
        ModoActual = modo;
        this.alEmpezar = alEmpezar;
        bool antes = modo == Modo.AntesDeNivel;
        titulo.text = antes ? "ELIGE TU MOTO" : "Tienda de motos";
        botonCerrar.gameObject.SetActive(!antes);
        botonEmpezar.gameObject.SetActive(antes);
        ventana.SetActive(true);
        ventana.transform.SetAsLastSibling(); // que quede encima del panel final
        Refrescar();
    }

    private void EmpezarNivel()
    {
        Cerrar();
        var accion = alEmpezar;
        alEmpezar = null;
        ModoActual = Modo.Tienda;
        accion?.Invoke();
    }

    private void ConstruirBotonAbrir(Transform canvas)
    {
        Transform panelFinal = canvas.Find("PanelFinal");
        if (panelFinal == null) return;

        // ConstruirPanelFinal ya lo deja en su fila, conectado a Abrir (y no lo pone si el nivel es a pie)
        Transform existente = panelFinal.Find("BotonTienda");
        if (existente != null) { existente.gameObject.SetActive(mostrarBotonEnFinal); return; }
        if (!mostrarBotonEnFinal) return;

        Button b = Boton("BotonTienda", panelFinal, new Vector2(380, 70), "Tienda de motos", out TextMeshProUGUI etiqueta);
        etiqueta.fontSize = 32;
        ((RectTransform)b.transform).anchoredPosition = new Vector2(0, -290);
        b.onClick.AddListener(Abrir);
    }

    private void ConstruirVentana(Transform canvas)
    {
        estilo = EstiloBrenn.Cargar();
        // Fondo oscuro que cubre toda la pantalla y bloquea clics
        Image fondo = Panel("TiendaMotos_Fondo", canvas, Vector2.zero, new Color(0, 0, 0, 0.6f));
        Estirar(fondo.rectTransform);
        ventana = fondo.gameObject;

        // Marco igual al del panel final: crema con borde rosa
        Image marco = Panel("Ventana", ventana.transform, new Vector2(1100, 680), Crema);
        if (marcoPanel != null)
        {
            marco.sprite = marcoPanel;
            marco.type = Image.Type.Sliced;
            marco.pixelsPerUnitMultiplier = multiplicadorMarco;
            marco.color = Color.white;
        }

        titulo = Titular(Texto("Titulo", marco.transform, "Tienda de motos", 54, TextoOscuro, new Vector2(0, 280), new Vector2(900, 70)));
        textoDinero = Texto("Dinero", marco.transform, "", 34, TextoMorado, new Vector2(0, 226), new Vector2(900, 50));

        // Fila con las 4 tarjetas
        RectTransform fila = Rect("Fila", marco.transform, new Vector2(1000, 420));
        fila.anchoredPosition = new Vector2(0, -3);
        var h = fila.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 16;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;

        int n = Progreso.Motos.Length;
        botones = new Button[n];
        etiquetas = new TextMeshProUGUI[n];

        for (int i = 0; i < n; i++)
        {
            var d = Progreso.Motos[i];
            Image tarjeta = Panel("Tarjeta_" + d.id, fila, Vector2.zero, estilo != null ? TarjetaOscura : Tarjeta);
            var v = tarjeta.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(12, 12, 12, 12);
            v.spacing = 8;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            // Ícono
            Image icono = Panel("Icono", tarjeta.transform, Vector2.zero, Color.white);
            icono.preserveAspect = true;
            if (iconos != null && i < iconos.Length && iconos[i] != null) icono.sprite = iconos[i];
            else icono.color = new Color(1, 1, 1, 0.1f);
            Alto(icono, 120);

            Alto(Titular(Texto("Nombre", tarjeta.transform, d.nombre, 34, TextoMorado, Vector2.zero, Vector2.zero)), 40);

            RectTransform barras = Rect("Barras", tarjeta.transform, Vector2.zero);
            Alto(barras, 64);
            Barra(barras, "Velocidad", 16, SegmentosVelocidad(d), Morado);
            Barra(barras, "Resistencia", -16, SegmentosResistencia(d), Rosa);

            Alto(Texto("Datos", tarjeta.transform, d.descripcion, 22, TextoOscuro, Vector2.zero, Vector2.zero), 56);

            Button b = Boton("Boton", tarjeta.transform, Vector2.zero, "", out TextMeshProUGUI et);
            Alto(b, estilo != null ? 64 : 58);
            int idx = i; // copia para el listener
            b.onClick.AddListener(() => Clic(idx));
            botones[i] = b;
            etiquetas[i] = et;
        }

        // Botones inferiores
        botonAPie = Boton("BotonAPie", marco.transform, TamAbajo, "Ir a pie", out _);
        ((RectTransform)botonAPie.transform).anchoredPosition = new Vector2(-XAbajo, -268);
        botonAPie.onClick.AddListener(() => { Progreso.Equipar(-1); AudioManager.Sonar("equipar_moto"); Refrescar(); });

        botonCerrar = Boton("BotonCerrar", marco.transform, TamAbajo, "Cerrar", out _);
        ((RectTransform)botonCerrar.transform).anchoredPosition = new Vector2(XAbajo, -268);
        botonCerrar.onClick.AddListener(Cerrar);

        // en el modo AntesDeNivel va en el lugar de Cerrar
        botonEmpezar = Boton("BotonEmpezar", marco.transform, TamAbajo, "EMPEZAR NIVEL", out _);
        ((RectTransform)botonEmpezar.transform).anchoredPosition = new Vector2(XAbajo, -268);
        botonEmpezar.onClick.AddListener(EmpezarNivel);
        botonEmpezar.gameObject.SetActive(false);
    }

    private Vector2 TamAbajo => estilo != null ? new Vector2(424, 72) : new Vector2(320, 62);
    private float XAbajo => estilo != null ? 226 : 190;

    private void Clic(int i)
    {
        bool compra = false;
        if (!Progreso.Tiene(i))
        {
            if (!Progreso.Comprar(i)) return;
            compra = true;
        }
        Progreso.Equipar(i);
        AudioManager.Sonar(compra ? "compra_moto" : "equipar_moto");   // cha-ching al comprar
        Refrescar();
    }

    private void Refrescar()
    {
        if (ventana == null || textoDinero == null) return;

        textoDinero.text = "Tu dinero: $" + Progreso.Dinero;

        for (int i = 0; i < botones.Length; i++)
        {
            var d = Progreso.Motos[i];
            if (!Progreso.Tiene(i))
            {
                etiquetas[i].text = "Comprar $" + d.precio;
                botones[i].interactable = Progreso.Dinero >= d.precio;
            }
            else if (Progreso.MotoEquipada == i)
            {
                etiquetas[i].text = "En uso";
                botones[i].interactable = false;
            }
            else
            {
                etiquetas[i].text = "Equipar";
                botones[i].interactable = true;
            }
        }

        botonAPie.interactable = Progreso.MotoEquipada >= 0;
    }

    // Una fila: la etiqueta a la izquierda y 5 cuadritos a la derecha ("llenos" con el color; el resto vacíos)
    private void Barra(RectTransform padre, string nombre, float y, int llenos, Color color)
    {
        RectTransform fila = Rect(nombre, padre, new Vector2(0, 26));
        fila.anchorMin = new Vector2(0, 0.5f);
        fila.anchorMax = new Vector2(1, 0.5f);
        fila.anchoredPosition = new Vector2(0, y);

        const float ancho = 16, alto = 16, hueco = 3, borde = 3;
        // la etiqueta ocupa lo que dejan los cuadritos, en una sola línea (se encoge si no cabe)
        var etiqueta = Texto("Etiqueta", fila, nombre, 19, TextoOscuro, Vector2.zero, Vector2.zero);
        etiqueta.alignment = TextAlignmentOptions.MidlineLeft;
        etiqueta.textWrappingMode = TextWrappingModes.NoWrap;
        etiqueta.enableAutoSizing = true;
        etiqueta.fontSizeMax = 19;
        etiqueta.fontSizeMin = 12;
        Estirar(etiqueta.rectTransform);
        etiqueta.rectTransform.offsetMax = new Vector2(-(Segmentos * (ancho + hueco) + 4), 0);

        for (int k = 0; k < Segmentos; k++)
        {
            Image cuadro = Panel("Seg_" + k, fila, new Vector2(ancho, alto), Oscuro);
            cuadro.raycastTarget = false;
            var rt = cuadro.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(1, 0.5f);
            rt.anchoredPosition = new Vector2(-(Segmentos - 1 - k) * (ancho + hueco), 0);
            Image relleno = Panel("Relleno", rt, Vector2.zero, k < llenos ? color : Vacio);
            relleno.raycastTarget = false;
            Estirar(relleno.rectTransform);
            relleno.rectTransform.offsetMin = new Vector2(borde, borde);
            relleno.rectTransform.offsetMax = new Vector2(-borde, -borde);
        }
    }

    // ---------- Ayudas para construir la interfaz por código ----------

    private RectTransform Rect(string nombre, Transform padre, Vector2 tam)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(padre, false);
        rt.sizeDelta = tam;
        return rt;
    }

    private Image Panel(string nombre, Transform padre, Vector2 tam, Color color)
    {
        var rt = Rect(nombre, padre, tam);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private TextMeshProUGUI Texto(string nombre, Transform padre, string txt, float tam,
                                  Color color, Vector2 pos, Vector2 size)
    {
        var rt = Rect(nombre, padre, size);
        rt.anchoredPosition = pos;
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = txt;
        t.fontSize = tam;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        if (fuente != null) t.font = fuente;
        return t;
    }

    // Pasa un texto a Press Start (más ancha: 0.7 de su tamaño en Pixelify, y se encoge si no cabe)
    private TextMeshProUGUI Titular(TextMeshProUGUI t)
    {
        if (fuenteTitulos == null) return t;
        t.font = fuenteTitulos;
        t.fontSizeMax = Mathf.Round(t.fontSize * 0.7f);
        t.fontSizeMin = 12;
        t.fontSize = t.fontSizeMax;
        t.enableAutoSizing = true;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    private Button Boton(string nombre, Transform padre, Vector2 tam, string txt, out TextMeshProUGUI etiqueta)
    {
        Image img = Panel(nombre, padre, tam, Rosa);
        if (pildora != null)
        {
            // misma píldora de "Jugar de nuevo" y "Menú"
            img.sprite = pildora;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = multiplicadorPildora;
            img.color = Color.white;
        }

        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var colores = b.colors;
        colores.disabledColor = new Color(0.72f, 0.68f, 0.78f, 1f); // apagado, pero sin volverse transparente
        b.colors = colores;

        etiqueta = Titular(Texto("Texto", img.transform, txt, 28, Oscuro, Vector2.zero, Vector2.zero));
        Estirar(etiqueta.rectTransform);
        if (estilo != null)
        {
            // en las tarjetas (tam cero: los acomoda el layout) texto de 16; los de abajo, de 28
            BotonBrenn.Vestir(b, BotonBrenn.Variante.Normal, estilo, null, tam == Vector2.zero ? 14f : 28f);
            if (tam == Vector2.zero && b.transform.Find("Tijera") is Transform tij) tij.gameObject.SetActive(false);   // no cabe entre tarjetas
        }
        return b;
    }

    private void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void Alto(Component c, float alto)
    {
        var le = c.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = alto;
    }
}