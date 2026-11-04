using UnityEngine;

// Turbo "Café exprés" (los 3 niveles; los coloca ConstruirTurbos desde el JSON del mapa, sobre banquetas). Trigger:
// al tocarlo el repartidor, a pie o en moto, se recoge (PlayerController.ActivarTurbo: a pie x1.6 por 6 s, en moto
// +2.0 tiles/s por 5 s, tope 9; y el "ding" turbo_recoger), se apaga (taza, brillo y trigger) y a los reaparecerEn
// segundos vuelve con un destello y un "pop" (si se ve en pantalla). La taza flota con sus 4 cuadros y el halo late
// suave; en el nivel 3 (noche y lluvia) el halo es más fuerte y todo va Unlit para que se vea. El reloj usa tiempo
// escalado: con la pausa no corre.
[RequireComponent(typeof(CircleCollider2D))]
public class Turbo : MonoBehaviour
{
    [SerializeField] private SpriteRenderer taza;
    [SerializeField] private SpriteRenderer brillo;
    [SerializeField] private Sprite[] cuadros;                 // cafe_expres_0..3
    [SerializeField] private float tiempoPorCuadro = 0.16f;
    [SerializeField] private float reaparecerEn = 25f;
    [SerializeField] private float alfaBrillo = 0.35f;         // nivel 3: más
    [SerializeField] private float latidoBrillo = 0.25f;       // cuánto late el halo (fracción de alfaBrillo)
    [SerializeField] private float duracionDestello = 0.45f;   // al reaparecer

    private CircleCollider2D zona;
    private float reloj, relojCuadro, destello;
    private int cuadro;
    private Vector3 escalaBrillo;

    public bool Disponible { get; private set; } = true;
    public float FaltaParaVolver => Disponible ? 0f : Mathf.Max(0f, reloj);
    public float ReaparecerEn => reaparecerEn;
    public int Recogidos { get; private set; }               // para las pruebas
    public bool Destellando => destello > 0f;

    private void Awake()
    {
        zona = GetComponent<CircleCollider2D>();
        zona.isTrigger = true;
        if (brillo != null) escalaBrillo = brillo.transform.localScale;
        // que no floten todos al mismo tiempo
        cuadro = Mathf.Abs(Mathf.RoundToInt(transform.position.x * 3f + transform.position.y)) % Mathf.Max(1, cuadros?.Length ?? 1);
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (!Disponible) return;
        var jugador = otro.GetComponentInParent<PlayerController>();
        if (jugador == null) return;   // autos, peatones y clientas no lo recogen
        var gm = GameManager.Instance;
        if (gm != null && (gm.NivelTerminado || gm.EnEspera)) return;
        Recoger(jugador);
    }

    private void Recoger(PlayerController jugador)
    {
        jugador.ActivarTurbo();
        AudioManager.Sonar("turbo_recoger");
        Recogidos++;
        Disponible = false;
        reloj = reaparecerEn;
        Mostrar(false);
    }

    private void Mostrar(bool si)
    {
        zona.enabled = si;
        if (taza != null) taza.enabled = si;
        if (brillo != null) brillo.enabled = si;
    }

    private void Update()
    {
        if (!Disponible)
        {
            reloj -= Time.deltaTime;
            if (reloj > 0f) return;
            Disponible = true;
            Mostrar(true);
            destello = duracionDestello;
            if (SeVe()) AudioManager.Sonar("turbo_aparece");
        }

        if (cuadros != null && cuadros.Length > 0 && taza != null)
        {
            relojCuadro += Time.deltaTime;
            if (relojCuadro >= tiempoPorCuadro)
            {
                relojCuadro -= tiempoPorCuadro;
                cuadro = (cuadro + 1) % cuadros.Length;
            }
            taza.sprite = cuadros[cuadro];
        }

        if (brillo != null)
        {
            float a = alfaBrillo * (1f + latidoBrillo * Mathf.Sin(Time.time * 3f));
            float escala = 1f;
            if (destello > 0f)
            {
                destello -= Time.deltaTime;
                float k = Mathf.Clamp01(destello / duracionDestello);   // 1 -> 0
                a = Mathf.Lerp(a, 1f, k);
                escala = 1f + 1.2f * k;
            }
            var c = brillo.color;
            c.a = a;
            brillo.color = c;
            brillo.transform.localScale = escalaBrillo * escala;
        }
    }

    private bool SeVe()
    {
        var cam = Camera.main;
        if (cam == null) return false;
        var v = cam.WorldToViewportPoint(transform.position);
        return v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
    }
}
