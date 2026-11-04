using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class TrafficCar : MonoBehaviour
{
    [Header("Recorrido")]
    [SerializeField] private Vector2 direccion = Vector2.right;
    [SerializeField] private float velocidad = 2.5f;
    [SerializeField] private float limiteMin;   // inicio del carril (x si es horizontal, y si es vertical)
    [SerializeField] private float limiteMax;   // final del carril

    [Header("Frenado")]
    [SerializeField] private float distanciaFrenado = 1.8f;
    [SerializeField] private float anchoDeteccion = 0.6f;
    [SerializeField] private float alturaCentro = 0.5f;  // el pivote está abajo, el cuerpo queda más arriba

    [Header("Choque con el repartidor")]
    [SerializeField] private float penalizacion = 15f;
    [SerializeField] private float tiempoEntreGolpes = 2f;

    private Rigidbody2D rb;
    private Vector2 dir;
    private bool horizontal;
    private float proximoGolpe;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        dir = direccion.normalized;
        horizontal = Mathf.Abs(dir.x) > Mathf.Abs(dir.y);
    }

    private void FixedUpdate()
    {
        if (HayAlgoAdelante()) return;

        Vector2 nueva = rb.position + dir * velocidad * Time.fixedDeltaTime;
        bool reaparece = false;

        // al llegar al final del carril, reaparece al inicio
        if (horizontal)
        {
            if (dir.x > 0 && nueva.x > limiteMax) { nueva.x = limiteMin; reaparece = true; }
            else if (dir.x < 0 && nueva.x < limiteMin) { nueva.x = limiteMax; reaparece = true; }
        }
        else
        {
            if (dir.y > 0 && nueva.y > limiteMax) { nueva.y = limiteMin; reaparece = true; }
            else if (dir.y < 0 && nueva.y < limiteMin) { nueva.y = limiteMax; reaparece = true; }
        }

        if (reaparece) rb.position = nueva;
        else rb.MovePosition(nueva);
    }

    // frena si hay otro auto o el repartidor justo enfrente
    private bool HayAlgoAdelante()
    {
        Vector2 centro = rb.position + Vector2.up * alturaCentro + dir * (distanciaFrenado * 0.5f + 0.4f);
        Vector2 tamano = horizontal
            ? new Vector2(distanciaFrenado, anchoDeteccion)
            : new Vector2(anchoDeteccion, distanciaFrenado);

        Collider2D[] golpes = Physics2D.OverlapBoxAll(centro, tamano, 0f);
        foreach (var c in golpes)
        {
            if (c.attachedRigidbody == rb) continue;
            if (c.GetComponentInParent<PlayerController>() != null) return true;
            if (c.GetComponentInParent<Peaton>() != null) return true;

            var otro = c.GetComponentInParent<TrafficCar>();
            if (otro != null && otro != this) return true;
        }
        return false;
    }

    private void OnCollisionStay2D(Collision2D col)
    {
        if (col.collider.GetComponentInParent<PlayerController>() == null) return;
        if (Time.time < proximoGolpe) return;

        proximoGolpe = Time.time + tiempoEntreGolpes;
        if (GameManager.Instance != null) GameManager.Instance.Penalizar(penalizacion);
    }
}