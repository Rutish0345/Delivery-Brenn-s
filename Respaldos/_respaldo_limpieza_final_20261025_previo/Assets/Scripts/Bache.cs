using UnityEngine;

// Bache de la calle (niveles 2 y 3): trigger en el suelo. Si el repartidor entra EN MOTO, la moto se sacude:
// velocidad al 60 % por 0.8 s, un temblor corto de la cámara, GameManager.Penalizar(5) (que ya se multiplica por el
// factor de choques de la moto) y un salpicón de 3 partículas (polvo, o gotas si es un charco). A pie, los autos y
// los peatones lo ignoran. Después de sacudir espera "cooldown" segundos para no disparar dos veces seguidas.
// Si el repartidor tiene ImpactoMoto, la penalización pasa por ImpactoMoto.Golpe (intensidad Bache: tambaleo,
// tinte y texto con lo que se restó); si acaba de chocar y está parpadeando, el bache no hace nada.
[RequireComponent(typeof(BoxCollider2D))]
public class Bache : MonoBehaviour
{
    [SerializeField] private float factorVelocidad = 0.6f;
    [SerializeField] private float duracionFrenado = 0.8f;
    [SerializeField] private float cooldown = 1f;
    [SerializeField] private float penalizacion = 5f;
    [SerializeField] private float amplitudTemblor = 0.12f;
    [SerializeField] private float duracionTemblor = 0.25f;

    [Header("Salpicón (hijos apagados que arma el constructor del mapa)")]
    [SerializeField] private SpriteRenderer[] particulas;
    [SerializeField] private float duracionSalpicon = 0.45f;
    [SerializeField] private float alturaSalpicon = 0.6f;

    private float listoEn;
    private float relojSalpicon;
    private Vector2[] direcciones;
    private CameraFollow camara;

    public int Sacudidas { get; private set; }   // para las pruebas

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        camara = FindFirstObjectByType<CameraFollow>();
        direcciones = new Vector2[particulas != null ? particulas.Length : 0];
        foreach (var p in particulas ?? new SpriteRenderer[0]) if (p != null) p.gameObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (Time.time < listoEn) return;
        var jugador = otro.GetComponentInParent<PlayerController>();
        if (jugador == null || !jugador.EnMoto) return;   // a pie (y autos, peatones) no pasa nada

        var impacto = jugador.GetComponent<ImpactoMoto>();
        if (impacto != null && impacto.isActiveAndEnabled)
        {
            if (!impacto.Golpe(ImpactoMoto.Intensidad.Bache, penalizacion, transform.position)) return;   // ya penaliza aquí
        }
        else
        {
            var gm = GameManager.Instance;
            if (gm != null && !gm.NivelTerminado && !gm.EnEspera) gm.Penalizar(penalizacion);
        }

        listoEn = Time.time + cooldown;
        Sacudidas++;
        jugador.Sacudir(factorVelocidad, duracionFrenado);
        jugador.AcortarTurbo(0.5f);   // el turbo pierde la mitad de lo que le quedaba
        if (camara != null) camara.Temblar(amplitudTemblor, duracionTemblor);
        Salpicar();
    }

    private void Salpicar()
    {
        if (particulas == null || particulas.Length == 0) return;
        relojSalpicon = duracionSalpicon;
        for (int i = 0; i < particulas.Length; i++)
        {
            if (particulas[i] == null) continue;
            // en abanico hacia arriba: izquierda, centro y derecha, con un poco de azar
            float x = (i - (particulas.Length - 1) / 2f) * 0.45f + Random.Range(-0.1f, 0.1f);
            direcciones[i] = new Vector2(x, 1f);
            particulas[i].transform.localPosition = Vector3.zero;
            particulas[i].color = Color.white;
            particulas[i].gameObject.SetActive(true);
        }
    }

    private void Update()
    {
        if (relojSalpicon <= 0f) return;
        relojSalpicon -= Time.deltaTime;
        float t = 1f - Mathf.Clamp01(relojSalpicon / duracionSalpicon);   // 0 → 1
        for (int i = 0; i < particulas.Length; i++)
        {
            var p = particulas[i];
            if (p == null) continue;
            // sube y cae (parábola) mientras se desvanece
            float arco = 4f * t * (1f - t) * alturaSalpicon;
            p.transform.localPosition = new Vector3(direcciones[i].x * t, arco, 0f);
            p.color = new Color(1f, 1f, 1f, 1f - t * t);
            if (relojSalpicon <= 0f) p.gameObject.SetActive(false);
        }
    }
}
