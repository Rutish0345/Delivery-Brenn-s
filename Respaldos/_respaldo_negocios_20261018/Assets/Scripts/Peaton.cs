using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Peaton : MonoBehaviour
{
    [Header("Recorrido (coordenadas del mundo)")]
    [SerializeField] private Vector2[] puntos;
    [SerializeField] private bool circular = false;   // false = va y viene entre los puntos
    [SerializeField] private float velocidad = 1.2f;
    [SerializeField] private float pausaEnPunto = 1.5f;

    [Header("Sprites (0 = quieto, 1 a 4 = caminando)")]
    [SerializeField] private Sprite[] frente;
    [SerializeField] private Sprite[] espalda;
    [SerializeField] private Sprite[] lado;           // mira a la derecha; a la izquierda se voltea
    [SerializeField] private float tiempoPorFrame = 0.15f;

    [Header("Choque con el repartidor")]
    [SerializeField] private float penalizacion = 5f;
    [SerializeField] private float tiempoEntreGolpes = 3f;

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private int objetivo = 1;
    private int sentido = 1;
    private float pausa;
    private float proximoGolpe;
    private float reloj;
    private int frameActual = 1;
    private bool caminando;
    private Vector2 mirada = Vector2.down;
    private bool tramoIniciado;     // ya empezó a cruzar: no se detiene a media calle si cambia el semáforo

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        sr = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        if (puntos == null || puntos.Length < 2)
        {
            enabled = false;
            return;
        }
        rb.position = puntos[0];
        pausa = Random.Range(0f, pausaEnPunto);   // para que no arranquen todos al mismo tiempo
    }

    private void FixedUpdate()
    {
        caminando = false;

        if (pausa > 0f)
        {
            pausa -= Time.fixedDeltaTime;
            return;
        }

        Vector2 destino = puntos[objetivo];
        Vector2 hacia = destino - rb.position;
        if (hacia.sqrMagnitude > 0.0001f) mirada = hacia.normalized;

        if (JugadorAdelante()) return;
        if (!tramoIniciado)
        {
            if (!Semaforo.PermitePeaton(rb.position, destino, velocidad)) return;   // espera en la orilla
            tramoIniciado = true;
        }

        Vector2 nueva = Vector2.MoveTowards(rb.position, destino, velocidad * Time.fixedDeltaTime);
        rb.MovePosition(nueva);
        caminando = true;

        if ((destino - nueva).sqrMagnitude < 0.0025f)
        {
            pausa = pausaEnPunto;
            SiguientePunto();
        }
    }

    private void SiguientePunto()
    {
        tramoIniciado = false;
        if (circular)
        {
            objetivo = (objetivo + 1) % puntos.Length;
            return;
        }
        if (objetivo + sentido >= puntos.Length || objetivo + sentido < 0) sentido = -sentido;
        objetivo += sentido;
    }

    // si el repartidor está justo enfrente, espera en lugar de empujarlo
    private bool JugadorAdelante()
    {
        Vector2 centro = rb.position + Vector2.up * 0.15f + mirada * 0.6f;
        foreach (var c in Physics2D.OverlapCircleAll(centro, 0.35f))
        {
            if (c.GetComponentInParent<PlayerController>() != null) return true;
        }
        return false;
    }

    private void Update()
    {
        if (sr == null) return;

        Sprite[] juego;
        bool volteado = false;

        if (Mathf.Abs(mirada.y) > Mathf.Abs(mirada.x))
            juego = mirada.y > 0 ? espalda : frente;
        else
        {
            juego = lado;
            volteado = mirada.x < 0;
        }

        if (juego == null || juego.Length == 0) return;
        sr.flipX = volteado;

        if (!caminando || juego.Length == 1)
        {
            sr.sprite = juego[0];
            reloj = 0f;
            frameActual = 1;
            return;
        }

        reloj += Time.deltaTime;
        if (reloj >= tiempoPorFrame)
        {
            reloj = 0f;
            frameActual++;
            if (frameActual >= juego.Length) frameActual = 1;
        }
        sr.sprite = juego[frameActual];
    }

    private void OnCollisionStay2D(Collision2D col)
    {
        if (col.collider.GetComponentInParent<PlayerController>() == null) return;
        if (Time.time < proximoGolpe) return;

        proximoGolpe = Time.time + tiempoEntreGolpes;
        pausa = 1f;
        if (GameManager.Instance != null) GameManager.Instance.Penalizar(penalizacion);
    }
}