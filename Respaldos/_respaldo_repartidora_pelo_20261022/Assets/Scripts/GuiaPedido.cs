using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiaPedido : MonoBehaviour
{
    [SerializeField] private RectTransform flecha;
    [SerializeField] private TextMeshProUGUI textoDistancia;
    [SerializeField] private CanvasGroup grupo;              // para ocultar todo el panel
    [SerializeField] private Image iconoProducto;            // el mismo ícono de la burbuja del marcador
    [SerializeField] private CatalogoProductos catalogo;
    [SerializeField] private float distanciaLlegada = 3f;
    [SerializeField] private float rebote = 8f;              // vaivén de la flecha en píxeles
    [SerializeField] private float velocidadRebote = 4f;

    private Transform jugador;
    private Vector2 posicionBase;

    private void Start()
    {
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) jugador = pc.transform;
        if (flecha != null) posicionBase = flecha.anchoredPosition;
    }

    private void Update()
    {
        var gm = GameManager.Instance;
        bool hayPedido = jugador != null && gm != null && !gm.NivelTerminado && gm.PuntoActual != null;

        if (grupo != null) grupo.alpha = hayPedido ? 1f : 0f;
        if (!hayPedido || flecha == null) return;

        if (iconoProducto != null && catalogo != null)
        {
            var icono = catalogo.Icono(gm.ProductoActual);
            if (iconoProducto.sprite != icono) iconoProducto.sprite = icono;
        }

        Vector2 origen = (Vector2)jugador.position + Vector2.up * 0.9f;
        Vector2 hacia = gm.PuntoActual.Centro() - origen;
        float distancia = hacia.magnitude;
        if (distancia < 0.01f) return;

        Vector2 dir = hacia / distancia;
        float angulo = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        flecha.localRotation = Quaternion.Euler(0f, 0f, angulo);

        // la flecha se mueve un poco en la dirección a la que apunta
        float vaiven = Mathf.Sin(Time.time * velocidadRebote) * rebote;
        flecha.anchoredPosition = posicionBase + dir * vaiven;

        if (textoDistancia != null)
            textoDistancia.text = distancia < distanciaLlegada ? "¡Aquí!" : Mathf.RoundToInt(distancia) + " m";
    }
}