using UnityEngine;

// Estela del turbo (hijo del repartidor; la arma ConstruirTurbos): mientras PlayerController.TurboActivo y se mueve,
// suelta cada "cada" segundos una partícula (estela_turbo_0..2: estrellita dorada, chispa rosa, punto que se apaga)
// detrás del personaje, a la altura de los pies. Las partículas viven en el mundo (no siguen al personaje): un pool
// fijo de SpriteRenderers bajo un objeto propio en la raíz de la escena. Con la pausa se congelan (tiempo escalado).
public class EstelaTurbo : MonoBehaviour
{
    [SerializeField] private PlayerController jugador;
    [SerializeField] private Sprite[] cuadros;            // estela_turbo_0..2
    [SerializeField] private Material material;           // nivel 3: Unlit (si no, el de sprites por defecto)
    [SerializeField] private float cada = 0.05f;
    [SerializeField] private float vida = 0.36f;
    [SerializeField] private float atras = 0.35f;         // tiles detrás del personaje
    [SerializeField] private float alturaPies = 0.2f;
    [SerializeField] private int tamPool = 16;

    private SpriteRenderer[] pool;
    private float[] edad;
    private Vector2[] deriva;
    private int siguiente;
    private float reloj;
    private Rigidbody2D rb;
    private SpriteRenderer srJugador;

    public int Activas { get; private set; }   // para las pruebas y las capturas

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
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        Vector2 vel = rb != null ? rb.linearVelocity : Vector2.zero;
        if (jugador.TurboActivo && vel.sqrMagnitude > 0.25f && dt > 0f)
        {
            reloj -= dt;
            if (reloj <= 0f)
            {
                reloj += cada;
                Soltar(vel);
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
            var c = Color.white;
            c.a = 1f - t * t;
            pool[i].color = c;
        }
        Activas = activas;
    }

    private void Soltar(Vector2 vel)
    {
        if (cuadros == null || cuadros.Length == 0) return;
        var sr = pool[siguiente];
        int i = siguiente;
        siguiente = (siguiente + 1) % pool.Length;
        Vector2 atrasDe = -vel.normalized * atras;
        Vector2 lado = Vector2.Perpendicular(vel.normalized) * Random.Range(-0.15f, 0.15f);
        sr.transform.position = (Vector3)((Vector2)jugador.transform.position + atrasDe + lado + Vector2.up * alturaPies);
        sr.sortingOrder = srJugador != null ? srJugador.sortingOrder : 0;
        sr.sprite = cuadros[0];
        sr.color = Color.white;
        sr.enabled = true;
        edad[i] = 0f;
        deriva[i] = new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.25f, 0.55f));   // sube un poco
    }

    private void OnDestroy()
    {
        if (pool != null && pool.Length > 0 && pool[0] != null) Destroy(pool[0].transform.parent.gameObject);
    }
}
