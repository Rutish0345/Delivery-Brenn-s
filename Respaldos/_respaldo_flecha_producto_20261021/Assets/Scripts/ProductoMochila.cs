using UnityEngine;

// El producto del pedido asomando por la mochila del repartidor (hijo del repartidor, que lleva un
// SortingGroup). Se ve mientras hay pedido y se esconde un momento al entregar; con el siguiente pedido
// reaparece con su producto. Solo a pie: si el cuerpo no muestra un cuadro de la hoja del repartidor
// (va en moto) no se dibuja.
//  - espalda: delante de la mochila (más cerca de la cámara que la cabeza)
//  - frente y lado: detrás del cuerpo, asomando por el hombro o la espalda
// Sigue el rebote de la caminata: baja 1 px en los cuadros de paso (columnas 1 y 3 de la hoja).
[RequireComponent(typeof(SpriteRenderer))]
public class ProductoMochila : MonoBehaviour
{
    [SerializeField] private CatalogoProductos catalogo;
    [SerializeField] private SpriteRenderer cuerpo;
    [Tooltip("Los 5 cuadros a pie de cada vista: 0 quieto, 1-4 caminando")]
    [SerializeField] private Sprite[] frente;
    [SerializeField] private Sprite[] espalda;
    [SerializeField] private Sprite[] lado;
    [Tooltip("Pixeles desde el pivote (los pies) a la base del producto, mirando a la derecha")]
    [SerializeField] private Vector2 enFrente = new Vector2(12, 49);   // al lado de la cabeza: los productos angostos no quedan tapados
    [SerializeField] private Vector2 enEspalda = new Vector2(6, 49);   // por la esquina de la mochila, no sobre la nuca
    [SerializeField] private Vector2 enLado = new Vector2(-14, 49);
    [SerializeField] private float pausaAlEntregar = 0.6f;

    private const float Pixel = 1f / 32f;
    private SpriteRenderer sr;
    private float ocultoHasta;

    public bool Visible => sr != null && sr.enabled;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        sr.enabled = false;
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PedidoEntregado += AlEntregar;
            GameManager.Instance.PedidoNuevo += AlNuevoPedido;
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PedidoEntregado -= AlEntregar;
            GameManager.Instance.PedidoNuevo -= AlNuevoPedido;
        }
    }

    private void AlEntregar(DeliveryPoint punto, int propina) => ocultoHasta = Time.time + pausaAlEntregar;

    private void AlNuevoPedido(DeliveryPoint punto, Producto producto)
    {
        if (catalogo != null) sr.sprite = catalogo.Asomando(producto);
    }

    private void LateUpdate()
    {
        var gm = GameManager.Instance;
        int col;
        int vista = Vista(out col);
        bool ver = gm != null && gm.PuntoActual != null && !gm.NivelTerminado && !gm.EnEspera &&
                   Time.time >= ocultoHasta && vista >= 0 && catalogo != null;
        sr.enabled = ver;
        if (!ver) return;

        sr.sprite = catalogo.Asomando(gm.ProductoActual);
        Vector2 pos = vista == 0 ? enFrente : vista == 1 ? enEspalda : enLado;
        int bob = col == 1 || col == 3 ? 1 : 0;
        float signo = cuerpo.flipX ? -1f : 1f;   // mirando a la izquierda, del otro lado
        transform.localPosition = new Vector3(pos.x * signo * Pixel, (pos.y - bob) * Pixel, 0f);
        sr.flipX = cuerpo.flipX;
        sr.sortingOrder = vista == 1 ? cuerpo.sortingOrder + 1 : cuerpo.sortingOrder - 1;
    }

    // 0 frente, 1 espalda, 2 lado; -1 si el cuerpo no muestra un cuadro a pie
    private int Vista(out int col)
    {
        col = -1;
        if (cuerpo == null || cuerpo.sprite == null) return -1;
        var s = cuerpo.sprite;
        if ((col = System.Array.IndexOf(frente, s)) >= 0) return 0;
        if ((col = System.Array.IndexOf(espalda, s)) >= 0) return 1;
        if ((col = System.Array.IndexOf(lado, s)) >= 0) return 2;
        return -1;
    }
}
