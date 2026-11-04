using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TiendaMotos : MonoBehaviour
{
    [SerializeField] private Sprite[] iconos;      // mismo orden que Progreso.Motos
    [SerializeField] private TMP_FontAsset fuente; // opcional

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

    private GameObject ventana;
    private TextMeshProUGUI textoDinero;
    private Button[] botones;
    private TextMeshProUGUI[] etiquetas;
    private Button botonAPie;

    private void Start()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        ConstruirVentana(canvas.transform);
        ConstruirBotonAbrir(canvas.transform);
        ventana.SetActive(false);
    }

    private void OnEnable() { Progreso.Cambio += Refrescar; }
    private void OnDisable() { Progreso.Cambio -= Refrescar; }

    public void Abrir()
    {
        ventana.SetActive(true);
        ventana.transform.SetAsLastSibling(); // que quede encima del panel final
        Refrescar();
    }

    public void Cerrar() { ventana.SetActive(false); }

    private void ConstruirBotonAbrir(Transform canvas)
    {
        Transform panelFinal = canvas.Find("PanelFinal");
        if (panelFinal == null) return;

        // ConstruirPanelFinal ya lo deja en su fila, conectado a Abrir
        if (panelFinal.Find("BotonTienda") != null) return;

        Button b = Boton("BotonTienda", panelFinal, new Vector2(380, 70), "Tienda de motos", out TextMeshProUGUI etiqueta);
        etiqueta.fontSize = 32;
        ((RectTransform)b.transform).anchoredPosition = new Vector2(0, -290);
        b.onClick.AddListener(Abrir);
    }

    private void ConstruirVentana(Transform canvas)
    {
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

        Texto("Titulo", marco.transform, "Tienda de motos", 54, Oscuro, new Vector2(0, 280), new Vector2(900, 70));
        textoDinero = Texto("Dinero", marco.transform, "", 34, Morado, new Vector2(0, 226), new Vector2(900, 50));

        // Fila con las 4 tarjetas
        RectTransform fila = Rect("Fila", marco.transform, new Vector2(1000, 370));
        fila.anchoredPosition = new Vector2(0, 22);
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
            Image tarjeta = Panel("Tarjeta_" + d.id, fila, Vector2.zero, Tarjeta);
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
            Alto(icono, 140);

            Alto(Texto("Nombre", tarjeta.transform, d.nombre, 34, Morado, Vector2.zero, Vector2.zero), 44);
            Alto(Texto("Datos", tarjeta.transform,
                "Velocidad " + d.velocidad.ToString("0.0") + "\n" + d.descripcion, 22, Oscuro, Vector2.zero, Vector2.zero), 100);

            Button b = Boton("Boton", tarjeta.transform, Vector2.zero, "", out TextMeshProUGUI et);
            Alto(b, 58);
            int idx = i; // copia para el listener
            b.onClick.AddListener(() => Clic(idx));
            botones[i] = b;
            etiquetas[i] = et;
        }

        // Botones inferiores
        botonAPie = Boton("BotonAPie", marco.transform, new Vector2(320, 62), "Ir a pie", out _);
        ((RectTransform)botonAPie.transform).anchoredPosition = new Vector2(-190, -268);
        botonAPie.onClick.AddListener(() => { Progreso.Equipar(-1); Refrescar(); });

        Button cerrar = Boton("BotonCerrar", marco.transform, new Vector2(320, 62), "Cerrar", out _);
        ((RectTransform)cerrar.transform).anchoredPosition = new Vector2(190, -268);
        cerrar.onClick.AddListener(Cerrar);
    }

    private void Clic(int i)
    {
        if (!Progreso.Tiene(i))
        {
            if (!Progreso.Comprar(i)) return;
        }
        Progreso.Equipar(i);
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

        etiqueta = Texto("Texto", img.transform, txt, 28, Oscuro, Vector2.zero, Vector2.zero);
        Estirar(etiqueta.rectTransform);
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