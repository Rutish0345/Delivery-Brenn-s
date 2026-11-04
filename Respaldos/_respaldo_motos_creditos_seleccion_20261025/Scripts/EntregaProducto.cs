using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Animación de la entrega: al entregar (GameManager.PedidoEntregado) el ícono del producto sale de la mochila
// del repartidor (o de la caja trasera si va en moto), vuela en arco hasta la clienta de ese punto, rebota
// y desaparece; en ese momento la clienta dice "¡Gracias! +$N" (Cliente espera a RecibirProducto).
// No toca el control del jugador ni el flujo del GameManager: el pago y el siguiente pedido ya ocurrieron.
// La crea ConstruirMapa (objeto EntregaProducto con el hijo Producto, Order in Layer 95).
public class EntregaProducto : MonoBehaviour
{
    public static EntregaProducto Instancia { get; private set; }

    [SerializeField] private CatalogoProductos catalogo;
    [SerializeField] private SpriteRenderer producto;
    [SerializeField] private float escala = 1.5f;             // el ícono de 16x16 a x1.5
    [SerializeField] private float duracionVuelo = 0.5f;
    [SerializeField] private float alturaArco = 1.2f;
    [SerializeField] private float escalaRebote = 1.2f;
    [SerializeField] private float duracionRebote = 0.18f;
    [SerializeField] private float alturaManos = 1.0f;        // dónde recibe la clienta (desde sus pies)

    [Header("De dónde sale (desde los pies del repartidor)")]
    [SerializeField] private Vector2 mochilaEspalda = new Vector2(0f, 1.25f);
    [SerializeField] private Vector2 mochilaLado = new Vector2(-0.45f, 1.25f);   // mirando a la derecha
    [SerializeField] private Vector2 cajaMotoLado = new Vector2(-0.78f, 1.1f);   // la caja de la parte trasera
    [SerializeField] private Vector2 cajaMotoFrente = new Vector2(0f, 1.3f);

    private readonly Dictionary<DeliveryPoint, Producto> productoDe = new Dictionary<DeliveryPoint, Producto>();
    private PlayerController jugador;
    private Cliente[] clientas;
    private Coroutine vuelo;
    private Cliente destinoActual;

    public bool Volando => vuelo != null;
    public Producto UltimoProducto { get; private set; }
    public Vector3 Salida { get; private set; }               // el último punto de salida (para la prueba)

    private void Awake()
    {
        Instancia = this;
        if (producto != null) producto.enabled = false;
    }

    private void Start()
    {
        jugador = FindFirstObjectByType<PlayerController>();
        clientas = FindObjectsByType<Cliente>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var gm = GameManager.Instance;
        if (gm == null) return;
        gm.PedidoNuevo += AlNuevoPedido;
        gm.PedidoEntregado += AlEntregar;
        // el primer pedido pudo asignarse antes de este Start
        if (gm.PuntoActual != null) productoDe[gm.PuntoActual] = gm.ProductoActual;
    }

    private void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
        if (GameManager.Instance == null) return;
        GameManager.Instance.PedidoNuevo -= AlNuevoPedido;
        GameManager.Instance.PedidoEntregado -= AlEntregar;
    }

    // el GameManager elige el pedido siguiente antes de avisar la entrega: el producto entregado se guarda aquí
    private void AlNuevoPedido(DeliveryPoint punto, Producto p) => productoDe[punto] = p;

    private void AlEntregar(DeliveryPoint punto, int propina)
    {
        if (!productoDe.TryGetValue(punto, out var p)) return;
        Cliente clienta = null;
        foreach (var c in clientas)
            if (c != null && c.Punto == punto) { clienta = c; break; }

        // si todavía vuela el anterior, llega de golpe
        if (vuelo != null)
        {
            StopCoroutine(vuelo);
            Terminar();
        }
        UltimoProducto = p;
        vuelo = StartCoroutine(Volar(p, clienta, punto));
    }

    private Vector3 PuntoDeSalida()
    {
        if (jugador == null) return transform.position;
        Vector2 d;
        bool lado = jugador.Orientacion == "Lado";
        if (jugador.EnMoto) d = lado ? cajaMotoLado : cajaMotoFrente;
        else d = lado ? mochilaLado : mochilaEspalda;
        if (lado && jugador.MiraIzquierda) d.x = -d.x;
        return jugador.transform.position + (Vector3)d;
    }

    private IEnumerator Volar(Producto p, Cliente clienta, DeliveryPoint punto)
    {
        destinoActual = clienta;
        producto.sprite = catalogo != null ? catalogo.Icono(p) : null;
        Vector3 desde = PuntoDeSalida();
        Salida = desde;
        producto.transform.position = desde;
        producto.transform.localScale = Vector3.one * escala;
        producto.enabled = true;

        for (float t = 0f; t < duracionVuelo; t += Time.deltaTime)
        {
            // la clienta puede seguir caminando: se apunta a donde está ahora
            Vector3 hasta = Destino(clienta, punto);
            float k = Mathf.SmoothStep(0f, 1f, t / duracionVuelo);
            producto.transform.position = Vector3.Lerp(desde, hasta, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * alturaArco);
            yield return null;
        }
        producto.transform.position = Destino(clienta, punto);

        for (float t = 0f; t < duracionRebote; t += Time.deltaTime)
        {
            producto.transform.localScale = Vector3.one * (escala * Mathf.Lerp(1f, escalaRebote, Mathf.Sin(t / duracionRebote * Mathf.PI)));
            yield return null;
        }
        Terminar();
    }

    private Vector3 Destino(Cliente clienta, DeliveryPoint punto) =>
        clienta != null ? clienta.transform.position + Vector3.up * alturaManos : (Vector3)punto.Centro() + Vector3.up * alturaManos;

    private void Terminar()
    {
        producto.enabled = false;
        producto.transform.localScale = Vector3.one * escala;
        vuelo = null;
        if (destinoActual != null) destinoActual.RecibirProducto();
        destinoActual = null;
    }
}
