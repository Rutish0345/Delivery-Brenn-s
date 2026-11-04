using UnityEngine;

// Marcador del punto de entrega: una burbuja con el producto del pedido flotando arriba y un aro en el suelo.
// Va en el primer hijo del DeliveryPoint (el que se prende y apaga con el pedido; su posición es Centro()).
//  - La burbuja sube y baja y queda encima de todo (techos, árboles); el aro pulsa en el suelo.
//  - Con el repartidor dentro del radio de entrega el aro se pone verde y la burbuja da un rebote: ya se
//    puede entregar con E.
//  - Mientras la clienta de esa casa enseña su globo ("¡Mi pedido!") la burbuja se esconde y queda el aro.
// Sirve igual en cualquier nivel: solo necesita el catálogo, los cuadros del aro y sus dos hijos.
public class MarcadorPedido : MonoBehaviour
{
    [SerializeField] private CatalogoProductos catalogo;
    [SerializeField] private SpriteRenderer burbuja;
    [SerializeField] private SpriteRenderer producto;
    [SerializeField] private SpriteRenderer aro;
    [SerializeField] private Sprite[] aroNormal;     // dorado, 3 cuadros del pulso
    [SerializeField] private Sprite[] aroListo;      // verde: dentro del radio de entrega

    [Header("Movimiento")]
    [SerializeField] private float alturaFlote = 0.15f;      // de lo más bajo a lo más alto
    [SerializeField] private float periodoFlote = 1.2f;      // segundos por subida y bajada
    [SerializeField] private float cuadrosAroPorSegundo = 4f;
    [SerializeField] private float escalaRebote = 1.15f;
    [SerializeField] private float duracionRebote = 0.25f;

    private const float Pixel = 1f / 32f;   // el flote avanza de pixel en pixel del arte

    private DeliveryPoint punto;
    private Cliente clienta;
    private Transform jugador;
    private Vector3 baseBurbuja;
    private float inicioRebote = -1f;
    private bool listo;

    public Producto ProductoMostrado { get; private set; }
    public bool Listo => listo;                                         // el repartidor ya puede entregar
    public bool BurbujaVisible => burbuja != null && burbuja.enabled;
    public float EscalaBurbuja => burbuja != null ? burbuja.transform.localScale.x : 1f;

    private void Awake()
    {
        punto = GetComponentInParent<DeliveryPoint>();
        if (burbuja != null) baseBurbuja = burbuja.transform.localPosition;
    }

    private void Start()
    {
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) jugador = pc.transform;
        foreach (var c in FindObjectsByType<Cliente>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.Punto == punto) { clienta = c; break; }
    }

    private void OnEnable()
    {
        listo = false;
        inicioRebote = -1f;
        ActualizarProducto();
        LateUpdate();
    }

    private void OnDisable()
    {
        if (burbuja == null) return;
        burbuja.transform.localPosition = baseBurbuja;
        burbuja.transform.localScale = Vector3.one;
    }

    private void ActualizarProducto()
    {
        var gm = GameManager.Instance;
        if (gm == null || catalogo == null || producto == null) return;
        ProductoMostrado = gm.ProductoActual;
        producto.sprite = catalogo.Icono(ProductoMostrado);
    }

    // en LateUpdate: el globo de la clienta se prende en una corrutina (después de los Update), así la
    // burbuja se esconde en el mismo cuadro y no se enciman ni un instante
    private void LateUpdate()
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.ProductoActual != ProductoMostrado) ActualizarProducto();

        bool dentro = jugador != null && punto != null &&
                      Vector2.Distance(jugador.position, punto.Centro()) <= punto.RadioEntrega;
        if (dentro && !listo) inicioRebote = Time.time;
        listo = dentro;

        // aro: ida y vuelta por los 3 cuadros (0 1 2 1), verde si ya se puede entregar
        var cuadros = listo ? aroListo : aroNormal;
        if (aro != null && cuadros != null && cuadros.Length > 0)
        {
            int paso = Mathf.FloorToInt(Time.time * cuadrosAroPorSegundo) % (2 * cuadros.Length - 2 > 0 ? 2 * cuadros.Length - 2 : 1);
            aro.sprite = cuadros[paso < cuadros.Length ? paso : 2 * cuadros.Length - 2 - paso];
        }

        if (burbuja == null) return;
        bool globo = clienta != null && clienta.GloboVisible;
        burbuja.enabled = !globo;
        if (producto != null) producto.enabled = !globo;

        float flote = (Mathf.Sin(Time.time * 2f * Mathf.PI / periodoFlote) + 1f) * 0.5f * alturaFlote;
        burbuja.transform.localPosition = baseBurbuja + Vector3.up * (Mathf.Round(flote / Pixel) * Pixel);

        float escala = 1f;
        if (inicioRebote >= 0f)
        {
            float t = (Time.time - inicioRebote) / duracionRebote;
            if (t >= 1f) inicioRebote = -1f;
            else escala = Mathf.Lerp(1f, escalaRebote, Mathf.Sin(t * Mathf.PI));
        }
        burbuja.transform.localScale = Vector3.one * escala;
    }
}
