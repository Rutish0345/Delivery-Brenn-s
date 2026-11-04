using System.Collections;
using TMPro;
using UnityEngine;

public class Cliente : MonoBehaviour
{
    private enum Estado { Oculta, Saliendo, Esperando, Reaccionando }

    [Header("Casa")]
    [SerializeField] private DeliveryPoint punto;
    [SerializeField] private Vector2 puerta;   // de dónde sale
    [SerializeField] private Vector2 espera;   // dónde espera el pedido
    [SerializeField] private float velocidad = 1.6f;

    [Header("Sprites (0 = quieto, 1 a 4 = caminando)")]
    [SerializeField] private Sprite[] frente;
    [SerializeField] private Sprite[] espalda;
    [SerializeField] private Sprite[] lado;    // mira a la derecha; a la izquierda se voltea
    [SerializeField] private float tiempoPorFrame = 0.15f;

    [Header("Globo de texto")]
    [SerializeField] private TextMeshPro globo;

    public DeliveryPoint Punto => punto;
    public bool GloboVisible => globo != null && globo.gameObject.activeSelf;   // el marcador esconde su burbuja

    private SpriteRenderer sr;
    private Estado estado = Estado.Oculta;
    private Vector2 mirada = Vector2.down;
    private bool caminando;
    private float reloj;
    private int frameActual = 1;

    private void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        Ocultar();
        if (GameManager.Instance != null) GameManager.Instance.PedidoEntregado += AlEntregar;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.PedidoEntregado -= AlEntregar;
    }

    private void Update()
    {
        var gm = GameManager.Instance;
        if (estado == Estado.Oculta && punto != null && punto.Activo && gm != null && !gm.NivelTerminado)
            StartCoroutine(Salir());

        Animar();
    }

    private void AlEntregar(DeliveryPoint entregado, int propina)
    {
        if (entregado != punto || estado == Estado.Oculta) return;
        StopAllCoroutines();
        StartCoroutine(Recibir(propina));
    }

    private IEnumerator Salir()
    {
        estado = Estado.Saliendo;
        transform.position = puerta;
        sr.enabled = true;
        MostrarGlobo("");

        yield return StartCoroutine(MoverA(espera));

        estado = Estado.Esperando;
        mirada = Vector2.down;
        MostrarGlobo("¡Mi pedido!");
    }

    private IEnumerator Recibir(int propina)
    {
        estado = Estado.Reaccionando;
        caminando = false;
        mirada = Vector2.down;
        MostrarGlobo(propina > 0 ? "¡Gracias!\n+$" + propina : "¡Gracias!");

        // saltito de alegría
        Vector2 baseP = transform.position;
        float t = 0f;
        while (t < 1.2f)
        {
            t += Time.deltaTime;
            float salto = Mathf.Abs(Mathf.Sin(t * 10f)) * 0.25f * (1f - t / 1.2f);
            transform.position = baseP + Vector2.up * salto;
            yield return null;
        }
        transform.position = baseP;

        MostrarGlobo("");
        yield return StartCoroutine(MoverA(puerta));
        Ocultar();
    }

    private IEnumerator MoverA(Vector2 destino)
    {
        while (((Vector2)transform.position - destino).sqrMagnitude > 0.0025f)
        {
            Vector2 antes = transform.position;
            Vector2 nueva = Vector2.MoveTowards(antes, destino, velocidad * Time.deltaTime);
            Vector2 paso = nueva - antes;
            if (paso.sqrMagnitude > 0.000001f) mirada = paso.normalized;

            transform.position = nueva;
            caminando = true;
            yield return null;
        }
        caminando = false;
    }

    private void Ocultar()
    {
        estado = Estado.Oculta;
        caminando = false;
        if (sr != null) sr.enabled = false;
        MostrarGlobo("");
        transform.position = puerta;
    }

    private void MostrarGlobo(string texto)
    {
        if (globo == null) return;
        globo.text = texto;
        globo.gameObject.SetActive(texto != "");
    }

    private void Animar()
    {
        if (sr == null || !sr.enabled) return;

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
}