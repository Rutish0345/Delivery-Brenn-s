using UnityEngine;
using UnityEngine.InputSystem;

public class DeliveryPoint : MonoBehaviour
{
    [SerializeField] private float radioEntrega = 2.2f;

    private GameObject marcador;
    private Transform jugador;
    private bool activo;
    public bool Activo => activo;
    public float RadioEntrega => radioEntrega;

    private void Awake()
    {
        // el marcador (MarcadorPedido) es el primer hijo de cada punto
        if (transform.childCount > 0)
            marcador = transform.GetChild(0).gameObject;
    }

    private void Start()
    {
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) jugador = pc.transform;
    }

    public void SetActivo(bool valor)
    {
        activo = valor;
        if (marcador != null) marcador.SetActive(valor);
    }

   public Vector2 Centro()
    {
        return marcador != null ? (Vector2)marcador.transform.position : (Vector2)transform.position;
    }

    private void Update()
    {
        if (!activo || jugador == null) return;
        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;

        float distancia = Vector2.Distance(jugador.position, Centro());
        if (distancia <= radioEntrega)
        {
            GameManager.Instance.Entregar(this);
        }
        else
        {
            Debug.Log("Muy lejos para entregar. Punto: " + name
                + " | padre en " + (Vector2)transform.position
                + " | círculo en " + Centro()
                + " | jugador en " + (Vector2)jugador.position
                + " | distancia " + distancia.ToString("0.0"));
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(Centro(), radioEntrega);
    }
}