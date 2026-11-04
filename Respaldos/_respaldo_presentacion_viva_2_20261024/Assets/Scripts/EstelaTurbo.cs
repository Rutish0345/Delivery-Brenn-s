using UnityEngine;

// Estela del turbo (hijo del repartidor; la arma ConstruirTurbos). Mientras PlayerController.TurboActivo y se mueve,
// suelta cada "cada" segundos una partícula detrás del personaje, a la altura de los pies, que recorre los 5 cuadros
// estela_turbo_0..4 (estrella dorada, chispa rosa, estrella chica, chispa chica, destello) mientras se encoge, sube un
// poco y se desvanece. Al recoger el turbo: destello dorado breve sobre el sprite del personaje (una copia del sprite
// teñida de dorado que se apaga) y un estallido de partículas alrededor. Las partículas viven en el mundo (no siguen al
// personaje): un pool fijo de SpriteRenderers bajo un objeto propio en la raíz de la escena. Tiempo escalado: con la
// pausa se congelan.
public class EstelaTurbo : MonoBehaviour
{
    [SerializeField] private PlayerController jugador;
    [SerializeField] private Sprite[] cuadros;            // estela_turbo_0..4
    [SerializeField] private Material material;           // nivel 3: Unlit (si no, el de sprites por defecto)
    [SerializeField] private float cada = 0.025f;
    [SerializeField] private float vida = 0.5f;
    [SerializeField] private float atras = 0.35f;         // tiles detrás del personaje
    [SerializeField] private float alturaPies = 0.25f;
    [SerializeField] private float escalaInicial = 1.5f;  // la partícula nace grande y se encoge
    [SerializeField] private int tamPool = 48;
    [SerializeField] private int estallido = 14;          // partículas al recoger el turbo
    [SerializeField] private float duracionDestello = 0.35f;
    [SerializeField] private Color colorDestello = new Color(1f, 0.84f, 0.35f, 0.9f);

    private SpriteRenderer[] pool;
    private float[] edad;
    private Vector2[] deriva;
    private int siguiente;
    private float reloj, relojDestello;
    private Rigidbody2D rb;
    private SpriteRenderer srJugador, destello;

    public int Activas { get; private set; }   // para las pruebas y las capturas
    public bool Destellando => relojDestello > 0f;

    private void Awake()
    {
        if (jugador == null) jugador = GetComponentInParent<PlayerController>();
        rb = jugador.GetComponent<Rigidbody2D>();
        srJugador = jugador.GetComponent<SpriteRenderer>();
        var raiz = new GameObject("EstelaTurbo_Particulas").transform;
        pool = new SpriteRenderer[tamPool];
        edad = new float[tamPool];
        deriva = new Vector2[tamPool];
        for (int i = 0; i < tamPool; i++)
        {
            var go = new GameObject("Particula");
            go.transform.SetParent(raiz, false);
            var sr = go.AddComponent<SpriteRenderer>();
            if (material != null) sr.sharedMaterial = material;
            if (srJugador != null) sr.sortingLayerID = srJugador.sortingLayerID;
            sr.enabled = false;
            pool[i] = sr;
            edad[i] = -1f;
        }

        // copia del sprite del personaje encima de él, teñida de dorado, para el destello al recoger
        var gd = new GameObject("DestelloTurbo");
        gd.transform.SetParent(jugador.transform, false);
        destello = gd.AddComponent<SpriteRenderer>();
        if (material != null) destello.sharedMaterial = material;
        destello.enabled = false;
        jugador.TurboEmpezo += AlRecoger;
    }

    private void OnDestroy()
    {
        if (jugador != null) jugador.TurboEmpezo -= AlRecoger;
        if (pool != null && pool.Length > 0 && pool[0] != null) Destroy(pool[0].transform.parent.gameObject);
    }

    private void AlRecoger()
    {
        relojDestello = duracionDestello;
        for (int k = 0; k < estallido; k++)
        {
            float ang = k * Mathf.PI * 2f / estallido;
            var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            Soltar((Vector2)jugador.transform.position + Vector2.up * 0.6f + dir * 0.3f, dir * 2.2f, k % 2);
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        Vector2 vel = rb != null ? rb.linearVelocity : Vector2.zero;
        if (jugador.TurboActivo && vel.sqrMagnitude > 0.25f && dt > 0f)
        {
            reloj -= dt;
            while (reloj <= 0f)
            {
                reloj += cada;
                Vector2 n = vel.normalized;
                Vector2 lado = Vector2.Perpendicular(n) * Random.Range(-0.22f, 0.22f);
                Vector2 p = (Vector2)jugador.transform.position - n * atras + lado + Vector2.up * alturaPies;
                Soltar(p, new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(0.3f, 0.7f)) - n * 0.6f, Random.Range(0, 2));
            }
        }
        else reloj = 0f;

        int activas = 0;
        for (int i = 0; i < pool.Length; i++)
        {
            if (edad[i] < 0f) continue;
            edad[i] += dt;
            if (edad[i] >= vida)
            {
                edad[i] = -1f;
                pool[i].enabled = false;
                continue;
            }
            activas++;
            float t = edad[i] / vida;
            pool[i].sprite = cuadros[Mathf.Min(cuadros.Length - 1, (int)(t * cuadros.Length))];
            pool[i].transform.position += (Vector3)(deriva[i] * dt);
            deriva[i] *= 1f - 2.5f * dt;   // frena
            pool[i].transform.localScale = Vector3.one * Mathf.Lerp(escalaInicial, 0.7f, t);
            var c = Color.white;
            c.a = 1f - t * t * t;
            pool[i].color = c;
        }
        Activas = activas;
    }

    private void LateUpdate()
    {
        if (destello == null) return;
        if (relojDestello > 0f && srJugador != null)
        {
            relojDestello -= Time.deltaTime;
            destello.enabled = true;
            destello.sprite = srJugador.sprite;
            destello.flipX = srJugador.flipX;
            destello.sortingLayerID = srJugador.sortingLayerID;
            destello.sortingOrder = srJugador.sortingOrder + 1;
            var c = colorDestello;
            c.a *= Mathf.Clamp01(relojDestello / duracionDestello);
            destello.color = c;
        }
        else if (destello.enabled) destello.enabled = false;
    }

    // el primer cuadro: 0 la estrella dorada, 1 la chispa rosa (y desde ahí recorre el resto)
    private void Soltar(Vector2 donde, Vector2 velocidad, int primero)
    {
        if (cuadros == null || cuadros.Length == 0) return;
        int i = siguiente;
        var sr = pool[i];
        siguiente = (siguiente + 1) % pool.Length;
        sr.transform.position = donde;
        sr.transform.localScale = Vector3.one * escalaInicial;
        sr.sortingOrder = srJugador != null ? srJugador.sortingOrder : 0;
        sr.sprite = cuadros[Mathf.Min(primero, cuadros.Length - 1)];
        sr.color = Color.white;
        sr.enabled = true;
        edad[i] = primero * vida / cuadros.Length;   // la rosa empieza en su cuadro
        deriva[i] = velocidad;
    }
}
