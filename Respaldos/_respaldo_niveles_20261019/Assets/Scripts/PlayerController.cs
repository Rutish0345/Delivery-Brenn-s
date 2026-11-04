using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float velocidad = 3.5f;

    [Header("Motos (nivel 2 en adelante)")]
    [SerializeField] private bool motosHabilitadas = true;
    [SerializeField] private MotoVisual[] motos;
    [SerializeField] private float tiempoPorFrameMoto = 0.1f;

    [Header("Colisión con moto")]
    [SerializeField] private Vector2 cajaMotoLado = new Vector2(1.7f, 0.4f);
    [SerializeField] private Vector2 cajaMotoVertical = new Vector2(0.7f, 0.5f);

    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer sr;
    private BoxCollider2D caja;
    private Vector2 cajaAPie, offsetAPie;

    private Vector2 direccion;
    private string orientacion = "Frente";
    private string estadoActual = "";

    private MotoVisual motoActual;
    private float velocidadActual;
    private float relojFrame;
    private int frame;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        caja = GetComponent<BoxCollider2D>();
        if (caja != null) { cajaAPie = caja.size; offsetAPie = caja.offset; }

        velocidadActual = velocidad;
    }

    private void OnEnable() { Progreso.Cambio += RefrescarModo; }
    private void OnDisable() { Progreso.Cambio -= RefrescarModo; }
    private void Start() { RefrescarModo(); }

    // Lo puede llamar el LevelManager: nivel 1 sin motos, niveles 2 y 3 con motos
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

        int idx = motosHabilitadas ? Progreso.MotoEquipada : -1;
        if (idx >= 0 && motos != null)
        {
            string id = Progreso.Motos[idx].id;
            foreach (var m in motos)
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
        var teclado = Keyboard.current;
        if (teclado == null) return;

        float x = 0f, y = 0f;
        if (teclado.dKey.isPressed || teclado.rightArrowKey.isPressed) x += 1f;
        if (teclado.aKey.isPressed || teclado.leftArrowKey.isPressed) x -= 1f;
        if (teclado.wKey.isPressed || teclado.upArrowKey.isPressed) y += 1f;
        if (teclado.sKey.isPressed || teclado.downArrowKey.isPressed) y -= 1f;

        direccion = new Vector2(x, y).normalized;
        ActualizarAnimacion();
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = direccion * velocidadActual;
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