using UnityEngine;

// Flecha del mundo que orbita al repartidor (a pie o en moto) y apunta al centro del pedido activo.
// Se esconde dentro del radio de entrega (ahí manda el aro verde del marcador) y vuelve con el siguiente
// pedido. La crea ConstruirMapa (objeto FlechaPedido con el hijo flecha_guia, Order in Layer 90).
public class FlechaGuia : MonoBehaviour
{
    [SerializeField] private float radio = 1.4f;             // distancia fija al pecho del repartidor
    [SerializeField] private float alturaPecho = 0.9f;       // el pivote del repartidor está en los pies
    [SerializeField] private float rebote = 0.12f;
    [SerializeField] private float velocidadRebote = 4f;

    private Transform jugador;
    private SpriteRenderer sr;

    public bool Visible => sr != null && sr.enabled;

    private void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) jugador = pc.transform;
    }

    private void LateUpdate()
    {
        var gm = GameManager.Instance;
        var punto = gm != null ? gm.PuntoActual : null;
        if (jugador == null || punto == null || gm.NivelTerminado || gm.EnEspera)
        {
            Mostrar(false);
            return;
        }

        // la misma distancia con la que DeliveryPoint deja entregar (de los pies al centro del punto)
        if (Vector2.Distance(jugador.position, punto.Centro()) <= punto.RadioEntrega)
        {
            Mostrar(false);
            return;
        }

        Vector2 origen = (Vector2)jugador.position + Vector2.up * alturaPecho;
        Vector2 hacia = punto.Centro() - origen;
        if (hacia.sqrMagnitude < 0.0001f)
        {
            Mostrar(false);
            return;
        }

        Mostrar(true);
        Vector2 dir = hacia.normalized;
        float extra = Mathf.Sin(Time.time * velocidadRebote) * rebote;
        transform.position = origen + dir * (radio + extra);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    private void Mostrar(bool valor)
    {
        if (sr != null && sr.enabled != valor) sr.enabled = valor;
    }
}
