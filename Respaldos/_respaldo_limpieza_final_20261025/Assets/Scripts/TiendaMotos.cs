using TMPro;
using UnityEngine;
using UnityEngine.UI;

// La ventana para comprar y equipar motos, armada al jugar. Dos modos:
//  - Tienda: se abre desde "Tienda de motos" del panel final (solo si mostrarBotonEnFinal) y se cierra con Cerrar.
//  - AntesDeNivel: la abre el LevelManager al arrancar un nivel con motos: "ELIGE TU MOTO" y, en vez de
//    Cerrar, "EMPEZAR NIVEL", que cierra la ventana y avisa al LevelManager para que arranque el nivel.
// Pensada para leerse a 1920x1080: panel #2A1F40 al 96% (EstiloBrenn.panel, el mapa no se ve detrás de las letras) de
// 1500x860 con 4 tarjetas de 330x520 separadas 24. Todo en Press Start 2P sin Auto Size: título 56, "Tu dinero" 30
// dorado, nombre 32 rosa, etiquetas VEL / RES 22 con barras de 5 cuadros de 22, descripción 20 en máximo 2 líneas y el
// botón de la tarjeta 26. Textos crema #FFF1D0 con contorno oscuro. Estados de la tarjeta: sin comprar "COMPRAR" y el
// precio (apagado y el precio en rojo #FF5D73 si no alcanza), comprada "ELEGIR" en dorado, elegida con borde dorado de
// 4 px y la insignia "EN USO". Abajo "IR A PIE" y "EMPEZAR NIVEL" (o "CERRAR") BotonBrenn de 92 con texto de 34,
// separados 40. Las medidas son públicas para la prueba (PruebaMotos), que mide que ningún texto se salga.
public class TiendaMotos : MonoBehaviour
{
    public enum Modo { Tienda, AntesDeNivel }

    [SerializeField] private Sprite[] iconos;      // mismo orden que Progreso.Motos
    [SerializeField] private TMP_FontAsset fuente;         // opcional: si no hay Press Start
    [SerializeField] private TMP_FontAsset fuenteTitulos;  // Press Start 2P: todos los textos de la ventana
    [SerializeField] private bool mostrarBotonEnFinal = true;  // "Tienda de motos" en el panel final (no en el nivel 1, a pie)

    [Header("Estilo (se copia del panel final con el menú Configurar motos)")]
    [SerializeField] private Sprite marcoPanel;
    [SerializeField] private float multiplicadorMarco = 1f;
    [SerializeField] private Sprite pildora;
    [SerializeField] private float multiplicadorPildora = 1f;

    private static readonly Color Oscuro = new Color32(0x2A, 0x1F, 0x40, 255);
    private static readonly Color Crema = new Color32(0xFF, 0xF1, 0xD0, 255);
    private static readonly Color Rosa = new Color32(0xFF, 0x6F, 0xA5, 255);
    private static readonly Color Dorado = new Color32(0xFF, 0xD1, 0x66, 255);
    private static readonly Color Morado = new Color32(0x8C, 0x52, 0xFF, 255);
    private static readonly Color Vacio = new Color32(0xE6, 0xC4, 0x8C, 255);
    public const string RojoPrecio = "#FF5D73";

    // ---- medidas (a 1920x1080) ----
    public const float AnchoVentana = 1500, AltoVentana = 860, AlfaVentana = 0.96f;
    public const float AnchoTarjeta = 330, AltoTarjeta = 520, SeparacionTarjetas = 24, BordeElegida = 4;
    public const float TamTitulo = 56, TamDinero = 30, TamNombre = 32, TamEtiqueta = 22, TamDescripcion = 20, TamBotonTarjeta = 26;
    public const float LadoCuadro = 22, HuecoCuadro = 4, BordeCuadro = 4;
    public const float AltoAbajo = 92, AnchoAbajo = 480, TamAbajo = 34, SeparacionAbajo = 40;
    public const float TamMinimo = 20;
    public const int LineasDescripcion = 2;
    public const float Interlineado = 30;   // en centésimas de em: los textos de 2 renglones no se pegan
    const float YTitulo = 362, YDinero = 296, YFila = 6, YAbajo = -330;
    // dentro de la tarjeta (centro en 0; arriba en 260)
    const float Margen = 14, YIcono = 150, AltoIcono = 170, YNombre = 30, YVel = -22, YRes = -54, YDescripcion = -112, AltoDescripcion = 56;
    const float AltoBotonTarjeta = 92, AnchoInsignia = 170, AltoInsignia = 40;

    public const int Segmentos = 5;
    // 5.0 → 2, 5.5 → 3, 6.0 → 4, 6.5 → 5 cuadritos
    public static int SegmentosVelocidad(Progreso.DatosMoto d) => Mathf.Clamp(Mathf.RoundToInt((d.velocidad - 4f) / 0.5f), 1, Segmentos);
    // choques x0.5 → 5, x1.0 → 3, x1.5 → 1 cuadritos
    public static int SegmentosResistencia(Progreso.DatosMoto d) => Mathf.Clamp(Mathf.RoundToInt(5f - (d.factorPenalizacion - 0.5f) * 4f), 1, Segmentos);

    private EstiloBrenn estilo;

    private GameObject ventana;
    private TextMeshProUGUI textoDinero;
    private Button[] botones;
    private TextMeshProUGUI[] etiquetas;
    private GameObject[] bordesElegida, insignias;
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
        titulo.text = antes ? "ELIGE TU MOTO" : "TIENDA DE MOTOS";
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

        Button b = Boton("BotonTienda", panelFinal, new Vector2(380, 70), "Tienda de motos", 28, out _);
        ((RectTransform)b.transform).anchoredPosition = new Vector2(0, -290);
        b.onClick.AddListener(Abrir);
    }

    private void ConstruirVentana(Transform canvas)
    {
        estilo = EstiloBrenn.Cargar();
        if (fuenteTitulos == null && estilo != null) fuenteTitulos = estilo.fuenteTitulos;

        // Fondo oscuro que cubre toda la pantalla y bloquea clics
        Image fondo = Panel("TiendaMotos_Fondo", canvas, Vector2.zero, new Color(0, 0, 0, 0.6f));
        Estirar(fondo.rectTransform);
        ventana = fondo.gameObject;

        // El panel: #2A1F40 con borde rosa, al 96% (el mapa casi no se ve detrás)
        Image marco = Panel("Ventana", ventana.transform, new Vector2(AnchoVentana, AltoVentana), Oscuro);
        Sprite spritePanel = estilo != null ? estilo.panel : marcoPanel;
        if (spritePanel != null)
        {
            marco.sprite = spritePanel;
            marco.type = Image.Type.Sliced;
            marco.pixelsPerUnitMultiplier = Referencia(canvas) / spritePanel.pixelsPerUnit;
            marco.color = new Color(1, 1, 1, AlfaVentana);
        }
        else marco.color = new Color(Oscuro.r, Oscuro.g, Oscuro.b, AlfaVentana);

        titulo = Texto("Titulo", marco.transform, "ELIGE TU MOTO", TamTitulo, Crema, new Vector2(0, YTitulo), new Vector2(AnchoVentana - 100, 70));
        textoDinero = Texto("Dinero", marco.transform, "", TamDinero, Dorado, new Vector2(0, YDinero), new Vector2(AnchoVentana - 100, 44));

        // Fila con las 4 tarjetas, a mano (sin layout): las medidas son exactas
        int n = Progreso.Motos.Length;
        float anchoFila = n * AnchoTarjeta + (n - 1) * SeparacionTarjetas;
        RectTransform fila = Rect("Fila", marco.transform, new Vector2(anchoFila, AltoTarjeta));
        fila.anchoredPosition = new Vector2(0, YFila);

        botones = new Button[n];
        etiquetas = new TextMeshProUGUI[n];
        bordesElegida = new GameObject[n];
        insignias = new GameObject[n];
        float interior = AnchoTarjeta - 2 * Margen;

        for (int i = 0; i < n; i++)
        {
            var d = Progreso.Motos[i];
            Image tarjeta = Panel("Tarjeta_" + d.id, fila, new Vector2(AnchoTarjeta, AltoTarjeta), Oscuro);
            tarjeta.rectTransform.anchoredPosition = new Vector2(-anchoFila / 2 + AnchoTarjeta / 2 + i * (AnchoTarjeta + SeparacionTarjetas), 0);
            if (spritePanel != null)
            {
                tarjeta.sprite = spritePanel;   // oscura con su borde rosa
                tarjeta.type = Image.Type.Sliced;
                tarjeta.pixelsPerUnitMultiplier = marco.pixelsPerUnitMultiplier;
                tarjeta.color = Color.white;
            }

            // Ícono
            Image icono = Panel("Icono", tarjeta.transform, new Vector2(interior, AltoIcono), Color.white);
            icono.rectTransform.anchoredPosition = new Vector2(0, YIcono);
            icono.preserveAspect = true;
            icono.raycastTarget = false;
            if (iconos != null && i < iconos.Length && iconos[i] != null) icono.sprite = iconos[i];
            else icono.color = new Color(1, 1, 1, 0.1f);

            Texto("Nombre", tarjeta.transform, d.nombre, TamNombre, Rosa, new Vector2(0, YNombre), new Vector2(interior, 44));

            RectTransform barras = Rect("Barras", tarjeta.transform, new Vector2(interior, 64));
            barras.anchoredPosition = new Vector2(0, (YVel + YRes) / 2);
            Barra(barras, "Velocidad", "VEL", YVel - (YVel + YRes) / 2, SegmentosVelocidad(d), Morado);
            Barra(barras, "Resistencia", "RES", YRes - (YVel + YRes) / 2, SegmentosResistencia(d), Rosa);

            var datos = Texto("Datos", tarjeta.transform, d.descripcion, TamDescripcion, Crema, new Vector2(0, YDescripcion), new Vector2(interior, AltoDescripcion));
            datos.textWrappingMode = TextWrappingModes.Normal;
            datos.lineSpacing = Interlineado;

            Button b = Boton("Boton", tarjeta.transform, new Vector2(interior, AltoBotonTarjeta), "", TamBotonTarjeta, out TextMeshProUGUI et);
            ((RectTransform)b.transform).anchoredPosition = new Vector2(0, -AltoTarjeta / 2 + Margen + AltoBotonTarjeta / 2);
            if (b.transform.Find("Tijera") is Transform tij) tij.gameObject.SetActive(false);   // no cabe entre tarjetas
            int idx = i; // copia para el listener
            b.onClick.AddListener(() => Clic(idx));
            botones[i] = b;
            etiquetas[i] = et;

            // La elegida: borde dorado de 4 px encima del rosa y la insignia "EN USO" arriba
            bordesElegida[i] = Marco("BordeElegida", tarjeta.transform, BordeElegida, Dorado);
            Image insignia = Panel("Insignia", tarjeta.transform, new Vector2(AnchoInsignia, AltoInsignia), Dorado);
            insignia.raycastTarget = false;
            insignia.rectTransform.anchoredPosition = new Vector2(0, AltoTarjeta / 2 - BordeElegida - AltoInsignia / 2);
            var textoInsignia = Texto("Texto", insignia.transform, "EN USO", TamEtiqueta, Oscuro, Vector2.zero, Vector2.zero, false);
            Estirar(textoInsignia.rectTransform);
            insignias[i] = insignia.gameObject;
        }

        // Botones inferiores, separados 40
        float xAbajo = (AnchoAbajo + SeparacionAbajo) / 2;
        botonAPie = Boton("BotonAPie", marco.transform, new Vector2(AnchoAbajo, AltoAbajo), "IR A PIE", TamAbajo, out _);
        ((RectTransform)botonAPie.transform).anchoredPosition = new Vector2(-xAbajo, YAbajo);
        botonAPie.onClick.AddListener(() => { Progreso.Equipar(-1); AudioManager.Sonar("equipar_moto"); Refrescar(); });

        botonCerrar = Boton("BotonCerrar", marco.transform, new Vector2(AnchoAbajo, AltoAbajo), "CERRAR", TamAbajo, out _);
        ((RectTransform)botonCerrar.transform).anchoredPosition = new Vector2(xAbajo, YAbajo);
        botonCerrar.onClick.AddListener(Cerrar);

        // en el modo AntesDeNivel va en el lugar de Cerrar
        botonEmpezar = Boton("BotonEmpezar", marco.transform, new Vector2(AnchoAbajo, AltoAbajo), "EMPEZAR NIVEL", TamAbajo, out _, BotonBrenn.Variante.Principal);
        ((RectTransform)botonEmpezar.transform).anchoredPosition = new Vector2(xAbajo, YAbajo);
        botonEmpezar.onClick.AddListener(EmpezarNivel);
        botonEmpezar.gameObject.SetActive(false);
    }

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
            bool tiene = Progreso.Tiene(i), elegida = tiene && Progreso.MotoEquipada == i;
            var brenn = botones[i].GetComponent<BotonBrenn>();
            if (!tiene)
            {
                bool alcanza = Progreso.Dinero >= d.precio;
                // en dos renglones: a 26 px "COMPRAR $350" no cabe en una tarjeta de 330
                etiquetas[i].text = "COMPRAR\n" + (alcanza ? "$" + d.precio : $"<color={RojoPrecio}>${d.precio}</color>");
                botones[i].interactable = alcanza;
            }
            else
            {
                etiquetas[i].text = elegida ? "ELEGIDA" : "ELEGIR";
                botones[i].interactable = true;
            }
            if (brenn != null) brenn.Marcar(tiene && !elegida);   // "ELEGIR" en dorado
            bordesElegida[i].SetActive(elegida);
            insignias[i].SetActive(elegida);
        }

        botonAPie.interactable = Progreso.MotoEquipada >= 0;
    }

    // Una fila: la etiqueta (VEL / RES) a la izquierda y 5 cuadros de 22 a la derecha ("llenos" con el color; el resto vacíos)
    private void Barra(RectTransform padre, string nombre, string etiquetaTexto, float y, int llenos, Color color)
    {
        float anchoCuadros = Segmentos * LadoCuadro + (Segmentos - 1) * HuecoCuadro;
        float anchoEtiqueta = etiquetaTexto.Length * TamEtiqueta + 8;
        float ancho = anchoEtiqueta + 16 + anchoCuadros;
        RectTransform fila = Rect(nombre, padre, new Vector2(ancho, LadoCuadro + 6));
        fila.anchoredPosition = new Vector2(0, y);

        var etiqueta = Texto("Etiqueta", fila, etiquetaTexto, TamEtiqueta, Crema, Vector2.zero, new Vector2(anchoEtiqueta, LadoCuadro + 6));
        etiqueta.alignment = TextAlignmentOptions.MidlineLeft;
        var rtE = etiqueta.rectTransform;
        rtE.anchorMin = rtE.anchorMax = rtE.pivot = new Vector2(0, 0.5f);
        rtE.anchoredPosition = Vector2.zero;

        for (int k = 0; k < Segmentos; k++)
        {
            Image cuadro = Panel("Seg_" + k, fila, new Vector2(LadoCuadro, LadoCuadro), Color.black);
            cuadro.raycastTarget = false;
            var rt = cuadro.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(1, 0.5f);
            rt.anchoredPosition = new Vector2(-(Segmentos - 1 - k) * (LadoCuadro + HuecoCuadro), 0);
            Image relleno = Panel("Relleno", rt, Vector2.zero, k < llenos ? color : Vacio);
            relleno.raycastTarget = false;
            Estirar(relleno.rectTransform);
            relleno.rectTransform.offsetMin = new Vector2(BordeCuadro, BordeCuadro);
            relleno.rectTransform.offsetMax = new Vector2(-BordeCuadro, -BordeCuadro);
        }
    }

    // Un marco de 'grueso' px por dentro del padre (4 tiras), sin recibir clics
    private GameObject Marco(string nombre, Transform padre, float grueso, Color color)
    {
        var rt = Rect(nombre, padre, Vector2.zero);
        Estirar(rt);
        foreach (var (min, max, tam) in new[] {
                     (new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, grueso)), (new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, grueso)),
                     (new Vector2(0, 0), new Vector2(0, 1), new Vector2(grueso, 0)), (new Vector2(1, 0), new Vector2(1, 1), new Vector2(grueso, 0)) })
        {
            Image tira = Panel("Tira", rt, tam, color);
            tira.raycastTarget = false;
            tira.rectTransform.anchorMin = min;
            tira.rectTransform.anchorMax = max;
            tira.rectTransform.pivot = new Vector2(min.x == max.x ? min.x : 0.5f, min.y == max.y ? min.y : 0.5f);
            tira.rectTransform.anchoredPosition = Vector2.zero;
        }
        return rt.gameObject;
    }

    // ---------- Ayudas para construir la interfaz por código ----------

    private static float Referencia(Component c)
    {
        var canvas = c.GetComponentInParent<Canvas>();
        return canvas != null ? canvas.rootCanvas.referencePixelsPerUnit : 100f;
    }

    private RectTransform Rect(string nombre, Transform padre, Vector2 tam)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.layer = padre.gameObject.layer;
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

    // Press Start, centrado, en una línea, sin Auto Size y (contorno) con el borde oscuro que lo despega del fondo
    private TextMeshProUGUI Texto(string nombre, Transform padre, string txt, float tam, Color color, Vector2 pos, Vector2 size, bool contorno = true)
    {
        var rt = Rect(nombre, padre, size);
        rt.anchoredPosition = pos;
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (fuenteTitulos != null) t.font = fuenteTitulos;
        else if (fuente != null) t.font = fuente;
        t.text = txt;
        t.enableAutoSizing = false;
        t.fontSize = tam;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        if (contorno)
        {
            t.outlineWidth = 0.2f;
            t.outlineColor = Oscuro;
        }
        return t;
    }

    private Button Boton(string nombre, Transform padre, Vector2 tam, string txt, float tamTexto, out TextMeshProUGUI etiqueta,
                         BotonBrenn.Variante variante = BotonBrenn.Variante.Normal)
    {
        Image img = Panel(nombre, padre, tam, Rosa);
        if (pildora != null)
        {
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

        etiqueta = Texto("Texto", img.transform, txt, tamTexto, Crema, Vector2.zero, Vector2.zero, false);
        Estirar(etiqueta.rectTransform);
        etiqueta.richText = true;
        if (estilo != null) BotonBrenn.Vestir(b, variante, estilo, null, tamTexto);
        etiqueta.lineSpacing = Interlineado;   // "COMPRAR" y el precio, sin pegarse
        return b;
    }

    private void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
