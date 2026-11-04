using UnityEngine;

public class FlechaGuia : MonoBehaviour
{
    [SerializeField] private float radio = 3.2f;             // qué tan lejos del repartidor flota
    [SerializeField] private float distanciaOcultar = 4.5f;  // si ya estás cerca, desaparece
    [SerializeField] private float rebote = 0.2f;
    [SerializeField] private float velocidadRebote = 4f;

    private Transform jugador;
    private SpriteRenderer sr;

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
        if (jugador == null || gm == null || gm.NivelTerminado || gm.PuntoActual == null)
        {
            Mostrar(false);
            return;
        }

        // el pivote del repartidor está en los pies, se sube a la altura del pecho
        Vector2 origen = (Vector2)jugador.position + Vector2.up * 0.9f;
        Vector2 hacia = gm.PuntoActual.Centro() - origen;
        float distancia = hacia.magnitude;

        if (distancia < distanciaOcultar)
        {
            Mostrar(false);
            return;
        }

        Mostrar(true);
        Vector2 dir = hacia / distancia;
        float extra = Mathf.Sin(Time.time * velocidadRebote) * rebote;

        transform.position = origen + dir * (radio + extra);
        float angulo = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angulo);
    }

    private void Mostrar(bool valor)
    {
        if (sr != null && sr.enabled != valor) sr.enabled = valor;
    }
}