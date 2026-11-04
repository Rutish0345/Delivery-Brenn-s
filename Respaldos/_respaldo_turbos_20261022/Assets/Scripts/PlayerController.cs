using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float velocidad = 3.5f;

    [Header("Motos (nivel 2 en adelante)")]
    [SerializeField] private bool motosHabilitadas = true;
    [SerializeField] private MotoVisual[] motos;
    [SerializeField] private float tiempoPorFrameMoto = 0.1f;

    [Header("Repartidora (Progreso.Genero = 1; los llena ConstruirRepartidora)")]
    [SerializeField] private RuntimeAnimatorController controladorRepartidora;   // AnimatorOverrideController: mismos estados
    [SerializeField] private MotoVisual[] motosRepartidora;                       // mismas motos, con ella encima

    [Header("Colisión con moto")]
    [SerializeField] private Vector2 cajaMotoLado = new Vector2(1.7f, 0.4f);
    [SerializeField] private Vector2 cajaMotoVertical = new Vector2(0.7f, 0.5f);

    private Rigidbody2D rb;
    private Animator anim;
    private RuntimeAnimatorController controladorRepartidor;   // el que trae la escena
    private SpriteRenderer sr;
    private BoxCollider2D caja;
    private Vector2 cajaAPie, offsetAPie;

    private Vector2 direccion;
    private string orientacion = "Frente";
    private string estadoActual = "";

    private MotoVisual motoActual;
    private float velocidadActual;
    private float factorSacudida = 1f;   // un bache frena la moto un momento (Bache.cs)
    private float relojSacudida;
    private float relojControl;          // un choque (ImpactoMoto): sin control, solo el rebote y luego quieto
    private float relojRebote;
    private Vector2 rebote;
    private float relojFrame;
    private int frame;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        controladorRepartidor = anim.runtimeAnimatorController;
        sr = GetComponent<SpriteRenderer>();
        caja = GetComponent<BoxCollider2D>();
        if (caja != null) { cajaAPie = caja.size; offsetAPie = caja.offset; }

        velocidadActual = velocidad;
    }

    private void OnEnable() { Progreso.Cambio += RefrescarModo; }
    private void OnDisable() { Progreso.Cambio -= RefrescarModo; }
    private void Start() { RefrescarModo(); }

    // Lo puede llamar el LevelManager: nivel 1 sin motos, niveles 2 y 3 con motos
    public bool MotosHabilitadas => motosHabilitadas;
    public bool EnMoto => motoActual != null;
    public string Orientacion => orientacion;              // "Frente", "Espalda" o "Lado"
    public bool MiraIzquierda => sr != null && sr.flipX;   // de lado, la hoja mira a la derecha
    public float FactorVelocidad => relojSacudida > 0f ? factorSacudida : 1f;
    public bool SinControl => relojControl > 0f;
    // Para las pruebas: si tiene valor, se usa en lugar del teclado
    public Vector2? DireccionForzada { get; set; }

    // La velocidad queda multiplicada por "factor" durante "duracion" segundos
    public void Sacudir(float factor, float duracion)
    {
        factorSacudida = factor;
        relojSacudida = Mathf.Max(relojSacudida, duracion);
    }

    // Choque: "duracion" segundos sin control; los primeros "tiempoEmpuje" se mueve a "empuje" (tiles/s) con la
    // física, así el rebote se detiene contra una pared en vez de atravesarla
    public void PerderControl(float duracion, Vector2 empuje, float tiempoEmpuje)
    {
        relojControl = Mathf.Max(relojControl, duracion);
        relojRebote = Mathf.Min(tiempoEmpuje, duracion);
        rebote = empuje;
    }

    public void SetMotosHabilitadas(bool valor)
    {
        motosHabilitadas = valor;
        RefrescarModo();
    }

    // Decide si el personaje va a pie o en moto, y con qué velocidad
    private void RefrescarModo()
    {
        motoActual = null;
        velocidadActual = velocidad;

        // repartidor o repartidora: otro juego de sprites con las mismas animaciones, colliders y velocidades
        bool chica = Progreso.EsRepartidora;
        var controlador = chica && controladorRepartidora != null ? controladorRepartidora : controladorRepartidor;
        if (anim.runtimeAnimatorController != controlador) anim.runtimeAnimatorController = controlador;
        var juego = chica && motosRepartidora != null && motosRepartidora.Length > 0 ? motosRepartidora : motos;

        int idx = motosHabilitadas ? Progreso.MotoEquipada : -1;
        if (idx >= 0 && juego != null)
        {
            string id = Progreso.Motos[idx].id;
            foreach (var m in juego)
            {
                if (m != null && m.id == id) { motoActual = m; break; }
            }
            if (motoActual != null) velocidadActual = Progreso.Motos[idx].velocidad;
        }

        // Pasto, jardines y casas solo estorban en moto
        BloqueoMoto.Activar(motoActual != null);

        // En moto animamos por código (cambiando sprites); a pie, con el Animator
        anim.enabled = motoActual == null;
        estadoActual = "";
        frame = 0;
        relojFrame = 0f;

        AjustarCaja();
    }

    // La moto es más larga que el personaje a pie: el collider se adapta a la orientación
    private void AjustarCaja()
    {
        if (caja == null) return;

        if (motoActual == null)
        {
            caja.size = cajaAPie;
            caja.offset = offsetAPie;
            return;
        }

        caja.size = orientacion == "Lado" ? cajaMotoLado : cajaMotoVertical;
        caja.offset = new Vector2(0f, offsetAPie.y);
    }

    private void Update()
    {
        if (relojSacudida > 0f) relojSacudida -= Time.deltaTime;
        // motor: el tono sigue a la velocidad real; a pie, silencio
        if (AudioManager.Instance != null)
            AudioManager.Instance.Motor(EnMoto ? rb.linearVelocity.magnitude : -1f, Progreso.MotoEquipada);

        var teclado = Keyboard.current;
        if (teclado == null && !DireccionForzada.HasValue) return;

        float x = 0f, y = 0f;
        if (DireccionForzada.HasValue) { x = DireccionForzada.Value.x; y = DireccionForzada.Value.y; }
        else
        {
            if (teclado.dKey.isPressed || teclado.rightArrowKey.isPressed) x += 1f;
            if (teclado.aKey.isPressed || teclado.leftArrowKey.isPressed) x -= 1f;
            if (teclado.wKey.isPressed || teclado.upArrowKey.isPressed) y += 1f;
            if (teclado.sKey.isPressed || teclado.downArrowKey.isPressed) y -= 1f;
        }

        // tras un choque la moto no responde (ni gira) hasta recuperar el control
        direccion = relojControl > 0f ? Vector2.zero : new Vector2(x, y).normalized;
        ActualizarAnimacion();
    }

    private void FixedUpdate()
    {
        if (relojControl > 0f)
        {
            relojControl -= Time.fixedDeltaTime;
            rb.linearVelocity = relojRebote > 0f ? rebote : Vector2.zero;
            relojRebote -= Time.fixedDeltaTime;
            return;
        }
        rb.linearVelocity = direccion * velocidadActual * FactorVelocidad;
    }

    private void ActualizarAnimacion()
    {
        bool camina = direccion.sqrMagnitude > 0.01f;

        if (camina)
        {
            string anterior = orientacion;

            if (Mathf.Abs(direccion.x) > Mathf.Abs(direccion.y))
            {
                orientacion = "Lado";
                sr.flipX = direccion.x < 0f; // los sprites de lado miran a la derecha
            }
            else
            {
                orientacion = direccion.y > 0f ? "Espalda" : "Frente";
            }

            // El collider de la moto cambia entre "de lado" y "de frente/espalda"
            if (motoActual != null && (orientacion == "Lado") != (anterior == "Lado"))
                AjustarCaja();
        }

        if (motoActual != null)
        {
            AnimarMoto(camina);
            return;
        }

        string nuevoEstado = (camina ? "Walk_" : "Idle_") + orientacion;
        if (nuevoEstado != estadoActual)
        {
            estadoActual = nuevoEstado;
            anim.Play(nuevoEstado);
        }
    }

    private void AnimarMoto(bool camina)
    {
        Sprite[] hoja = orientacion == "Lado" ? motoActual.lado
                      : orientacion == "Espalda" ? motoActual.espalda
                      : motoActual.frente;
        if (hoja == null || hoja.Length == 0) return;

        if (camina)
        {
            relojFrame += Time.deltaTime;
            if (relojFrame >= tiempoPorFrameMoto)
            {
                relojFrame = 0f;
                frame = (frame + 1) % hoja.Length;
            }
        }
        else
        {
            frame = 0;
            relojFrame = 0f;
        }

        sr.sprite = hoja[Mathf.Min(frame, hoja.Length - 1)];
    }
}