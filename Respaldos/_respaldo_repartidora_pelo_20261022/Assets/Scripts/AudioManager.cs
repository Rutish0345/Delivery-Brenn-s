using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Música y efectos de todo el juego (singleton, DontDestroyOnLoad). Vive en el prefab Resources/AudioManager (lo arma
// "Delivery Brenn's > Audio" con los clips de Assets/Audio, que genera pixel_town/generate_audio.py) y se crea solo
// antes de cargar la primera escena, así funciona aunque se dé Play desde cualquier escena y ningún constructor de
// escenas lo puede perder.
//  - Música: PonerMusica(nombre) con fundido de 0.8 s entre pistas (la de cada nivel la decide LevelManager).
//  - Efectos: Reproducir(nombre) con un pool de AudioSource; el mismo efecto pedido dos veces en el mismo instante
//    suena una sola vez.
//  - Escucha al GameManager de cada escena (pedido nuevo, entrega, propina, cancelación) y hace sonar los botones
//    (clic y hover) de cualquier escena sin conectar nada en ellos.
//  - Motor de la moto en loop (Motor, desde PlayerController) y lluvia con truenos lejanos (Lluvia, desde LevelManager).
//  - Volúmenes y encendido/apagado de música y efectos en PlayerPrefs (se mantienen entre escenas y sesiones).
[DefaultExecutionOrder(-500)]
public class AudioManager : MonoBehaviour
{
    [Serializable]
    public struct Entrada
    {
        public string nombre;
        public AudioClip clip;
        [Tooltip("Música: volumen propio de la pista, relativo al de la música (1 = igual; 0 se toma como 1)")]
        public float volumen;
    }

    [SerializeField] private Entrada[] musicas = new Entrada[0];
    [SerializeField] private Entrada[] efectos = new Entrada[0];
    [SerializeField] private float duracionFundido = 0.8f;
    [SerializeField] private int fuentesDeEfectos = 12;
    [Header("Fuentes (hijas del prefab; las de loop con la casilla Loop marcada)")]
    [SerializeField] private AudioSource musicaA;
    [SerializeField] private AudioSource musicaB;
    [SerializeField] private AudioSource motor;
    [SerializeField] private AudioSource lluvia;
    [SerializeField] private AudioSource trueno;
    [Header("Motor")]
    [SerializeField] private float velocidadMaximaMoto = 6.5f;   // la Deportiva: con ella el tono llega a 1.6
    [SerializeField] private float volumenMotorQuieto = 0.10f;
    [SerializeField] private float volumenMotorAndando = 0.20f;
    [Header("Lluvia")]
    [SerializeField] private float volumenLluvia = 0.35f;
    [SerializeField] private Vector2 esperaTrueno = new Vector2(15f, 40f);

    public const string PrefabEnResources = "AudioManager";
    const string kVolMusica = "volumen_musica", kVolEfectos = "volumen_efectos";
    const string kMusicaActiva = "musica_activa", kEfectosActivos = "efectos_activos";
    public const float VolumenMusicaPorDefecto = 0.35f;

    public static AudioManager Instance { get; private set; }
    public static event Action Cambio;   // cambió algún volumen o interruptor (BotonSonido se refresca)

    // ---- ajustes guardados (se pueden leer sin que exista el AudioManager) ----

    public static bool MusicaActiva
    {
        get => PlayerPrefs.GetInt(kMusicaActiva, 1) == 1;
        set { PlayerPrefs.SetInt(kMusicaActiva, value ? 1 : 0); Guardar(); }
    }
    public static bool EfectosActivos
    {
        get => PlayerPrefs.GetInt(kEfectosActivos, 1) == 1;
        set { PlayerPrefs.SetInt(kEfectosActivos, value ? 1 : 0); Guardar(); }
    }
    public static float VolumenMusica
    {
        get => PlayerPrefs.GetFloat(kVolMusica, VolumenMusicaPorDefecto);
        set { PlayerPrefs.SetFloat(kVolMusica, Mathf.Clamp01(value)); Guardar(); }
    }
    public static float VolumenEfectos
    {
        get => PlayerPrefs.GetFloat(kVolEfectos, 1f);
        set { PlayerPrefs.SetFloat(kVolEfectos, Mathf.Clamp01(value)); Guardar(); }
    }
    static void Guardar()
    {
        PlayerPrefs.Save();
        if (Instance != null) Instance.AplicarVolumenes();
        Cambio?.Invoke();
    }
    static float FactorMusica => MusicaActiva ? VolumenMusica : 0f;
    static float FactorEfectos => EfectosActivos ? VolumenEfectos : 0f;

    // ---- para las pruebas ----
    public string MusicaActual => musicaActual;
    public AudioSource FuenteMusica => musicaA;
    public AudioSource FuenteMotor => motor;
    public AudioSource FuenteLluvia => lluvia;
    public int VecesSonado(string nombre) => contador.TryGetValue(nombre, out int n) ? n : 0;
    public void ReiniciarContador() => contador.Clear();
    public int EfectosSonando { get { int n = 0; foreach (var s in pool) if (s.isPlaying) n++; return n; } }
    public void TruenoYa() => proximoTrueno = Time.time;
    public bool TieneClip(string nombre) => clips.ContainsKey(nombre) || pistas.ContainsKey(nombre);

    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private readonly Dictionary<string, AudioClip> pistas = new Dictionary<string, AudioClip>();
    private readonly Dictionary<string, float> ultimaVez = new Dictionary<string, float>();
    private readonly Dictionary<string, int> contador = new Dictionary<string, int>();
    private readonly List<AudioSource> pool = new List<AudioSource>();
    private string musicaActual = "";
    private float volumenPista = 1f;            // 0..1 durante el fundido de la pista que suena
    private readonly Dictionary<string, float> volumenDePista = new Dictionary<string, float>();
    private float propiaActual = 1f;            // volumen propio de la pista que suena (la historia va más baja)
    private float atenuacion = 1f, atenuacionObjetivo = 1f;   // AtenuarMusica (fades de la intro)
    private Coroutine fundido;
    private int siguienteFuente;
    private GameManager gm;
    private static bool saliendo;

    private int cuadroMotor = -1;
    private float fraccionMotor;
    private bool enMoto;
    private string clipMotor;
    private bool llueve;
    private float proximoTrueno;

    private GameObject hover, presionado;
    private Vector2 ultimoPuntero = new Vector2(-1, -1);
    private bool clicPendiente;
    private int cuadroSonidoExplicito = -1;

    // ------------------------------------------------------------------ creación

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Limpiar() { Instance = null; Cambio = null; saliendo = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CrearSolo()
    {
        if (Instance != null || FindFirstObjectByType<AudioManager>() != null) return;
        var prefab = Resources.Load<AudioManager>(PrefabEnResources);
        if (prefab == null)
        {
            Debug.LogWarning("No hay Resources/AudioManager.prefab (Delivery Brenn's > Audio): el juego va sin sonido.");
            return;
        }
        var am = Instantiate(prefab);
        am.name = "AudioManager";
    }

    // Atajo que no falla si no hay AudioManager (o si el juego se está cerrando)
    public static void Sonar(string nombre, float volumen = 1f, float retraso = 0f)
    {
        if (Instance != null && !saliendo) Instance.Reproducir(nombre, volumen, 1f, retraso);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Application.quitting += () => saliendo = true;

        LlenarBiblioteca();

        if (musicaA == null) musicaA = Fuente("MusicaA", true);
        if (musicaB == null) musicaB = Fuente("MusicaB", true);
        if (motor == null) motor = Fuente("Motor", true);
        if (lluvia == null) lluvia = Fuente("Lluvia", true);
        if (trueno == null) trueno = Fuente("Trueno", false);
        motor.volume = 0f;
        for (int i = 0; i < fuentesDeEfectos; i++) pool.Add(Fuente("Efecto" + i, false));
        AplicarVolumenes();
    }

    // Nombre → clip. También se llama si los diccionarios llegan vacíos: al recompilar en pleno Play, Unity recarga
    // los scripts y solo conserva lo serializado
    private void LlenarBiblioteca()
    {
        foreach (var e in musicas)
        {
            if (e.clip == null || string.IsNullOrEmpty(e.nombre)) continue;
            pistas[e.nombre] = e.clip;
            volumenDePista[e.nombre] = e.volumen > 0f ? e.volumen : 1f;
        }
        foreach (var e in efectos) if (e.clip != null && !string.IsNullOrEmpty(e.nombre)) clips[e.nombre] = e.clip;
    }

    // Las suscripciones van aquí (y no en Awake) para sobrevivir a esa recarga: Unity llama OnDisable antes y OnEnable
    // después, y todo lo que no es serializado (estáticos, diccionarios, pool, eventos) hay que rehacerlo
    private void OnEnable()
    {
        if (Instance == null) Instance = this;
        if (Instance != this) return;   // el duplicado que Awake ya mandó destruir
        if (clips.Count == 0) LlenarBiblioteca();
        if (pool.Count == 0)
            foreach (Transform hijo in transform)
                if (hijo.name.StartsWith("Efecto") && hijo.TryGetComponent(out AudioSource s)) pool.Add(s);
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
        if (gm == null) Enganchar();
    }

    private void OnDisable()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= AlCargarEscena;
        Soltar();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // También la usa el constructor del prefab
    public AudioSource Fuente(string nombre, bool loop)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = 0f;
        return s;
    }

    // ------------------------------------------------------------------ escenas y GameManager

    private void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        if (modo != LoadSceneMode.Single) return;
        // red de seguridad: sin AudioListener no se oye nada (los constructores ya lo ponen en la cámara)
        if (FindAnyObjectByType<AudioListener>() == null && Camera.main != null)
        {
            Camera.main.gameObject.AddComponent<AudioListener>();
            Debug.LogWarning($"AudioManager: {escena.name} no tenía AudioListener; se agregó a la cámara. Vuelve a construir la escena.");
        }
        // lo de la escena anterior se apaga; LevelManager vuelve a poner la lluvia si toca
        Lluvia(false);
        enMoto = false;
        motor.Stop();
        hover = presionado = null;
        Enganchar();
    }

    private void Enganchar()
    {
        Soltar();
        gm = GameManager.Instance;
        if (gm == null) return;
        gm.PedidoNuevo += AlPedidoNuevo;
        gm.PedidoEntregado += AlEntregar;
        gm.PedidoCancelado += AlCancelar;
    }

    private void Soltar()
    {
        if (gm == null) return;
        gm.PedidoNuevo -= AlPedidoNuevo;
        gm.PedidoEntregado -= AlEntregar;
        gm.PedidoCancelado -= AlCancelar;
        gm = null;
    }

    private float ultimaEntrega = -10f;

    // tras una entrega el ding-dong del siguiente espera a que termine la campanita
    private void AlPedidoNuevo(DeliveryPoint punto, Producto producto)
    {
        float desde = Time.unscaledTime - ultimaEntrega;
        Reproducir("pedido_nuevo", 1f, 1f, desde < 0.9f ? 0.9f - desde : 0f);
    }

    private void AlEntregar(DeliveryPoint punto, int propina)
    {
        ultimaEntrega = Time.unscaledTime;
        Reproducir("entrega");
        if (propina > 0) Reproducir("propina", 1f, 1f, 0.35f);
    }

    private void AlCancelar(DeliveryPoint punto) => Reproducir("pedido_cancelado");

    // Final del nivel (lo llama LevelManager): la fanfarria en el cierre de la historia con puesto; si no, la música
    // se apaga y suena el jingle de victoria (2 o 3 estrellas) o de derrota (1 estrella)
    public void TerminarNivel(bool victoria, bool fanfarria)
    {
        enMoto = false;
        if (fanfarria)
        {
            PonerMusica("fanfarria_final", false);
            return;
        }
        PararMusica();
        Reproducir(victoria ? "jingle_victoria" : "jingle_derrota", 1f, 1f, 0.15f);
    }

    // Al salir de un nivel a medias (pausa → Menú → Sí): se callan la lluvia, el motor y los efectos en curso
    public void SalirDelNivel()
    {
        Lluvia(false);
        enMoto = false;
        motor.Stop();
        motor.volume = 0f;
        clipMotor = null;
        StopAllCoroutines();   // efectos diferidos (propina, ding-dong) que ya no tocan
        fundido = null;
        musicaB.Stop();
        musicaA.volume = VolumenMusicaA();
        foreach (var s in pool) s.Stop();
    }

    // ------------------------------------------------------------------ música

    private float VolumenMusicaA() => volumenPista * propiaActual * atenuacion * FactorMusica;

    // Baja (o restaura con 1) la música que suena, en ~0.2 s y sin cortarla: la intro la baja a 0.7 en los fades
    public void AtenuarMusica(float factor) => atenuacionObjetivo = Mathf.Clamp01(factor);
    public float Atenuacion => atenuacion;
    public float VolumenPropio(string pista) => volumenDePista.TryGetValue(pista, out float v) ? v : 1f;

    // Corta los efectos que están sonando cuyo clip empieza con 'prefijo' (p. ej. el tic de la máquina de escribir)
    public void CallarEfectos(string prefijo)
    {
        foreach (var f in pool)
            if (f.isPlaying && f.clip != null && f.clip.name.StartsWith(prefijo)) f.Stop();
    }

    public int Sonando(string prefijo)
    {
        int n = 0;
        foreach (var f in pool) if (f.isPlaying && f.clip != null && f.clip.name.StartsWith(prefijo)) n++;
        return n;
    }

    public void PonerMusica(string nombre) => PonerMusica(nombre, true);

    public void PonerMusica(string nombre, bool loop)
    {
        if (nombre == musicaActual && musicaA.isPlaying) return;   // la misma pista sigue sin cortarse
        if (!pistas.TryGetValue(nombre, out var clip))
        {
            Debug.LogWarning($"AudioManager: no hay música \"{nombre}\".");
            return;
        }
        musicaActual = nombre;
        propiaActual = volumenDePista.TryGetValue(nombre, out float v) ? v : 1f;
        atenuacion = atenuacionObjetivo = 1f;   // la pista nueva entra sin atenuar (la vieja sale desde donde estaba)
        // la que sonaba pasa a B y se desvanece; la nueva entra en A
        (musicaA, musicaB) = (musicaB, musicaA);
        musicaA.clip = clip;
        musicaA.loop = loop;
        musicaA.time = 0f;
        musicaA.Play();
        Fundir(true);
    }

    public void PararMusica()
    {
        musicaActual = "";
        (musicaA, musicaB) = (musicaB, musicaA);
        musicaA.Stop();
        musicaA.clip = null;
        Fundir(false);
    }

    private void Fundir(bool entra)
    {
        if (fundido != null) StopCoroutine(fundido);
        fundido = StartCoroutine(Fundido(entra));
    }

    private IEnumerator Fundido(bool entra)
    {
        float saleDesde = musicaB.isPlaying ? musicaB.volume : 0f;
        for (float t = 0f; t < duracionFundido; t += Time.unscaledDeltaTime)
        {
            float k = t / duracionFundido;
            volumenPista = entra ? k : 0f;
            musicaA.volume = VolumenMusicaA();
            musicaB.volume = saleDesde * (1f - k);
            yield return null;
        }
        volumenPista = entra ? 1f : 0f;
        musicaA.volume = VolumenMusicaA();
        musicaB.Stop();
        musicaB.clip = null;
        fundido = null;
    }

    // ------------------------------------------------------------------ efectos

    public void Reproducir(string nombre) => Reproducir(nombre, 1f, 1f, 0f);

    public void Reproducir(string nombre, float volumen, float tono, float retraso)
    {
        if (!clips.TryGetValue(nombre, out var clip))
        {
            Debug.LogWarning($"AudioManager: no hay efecto \"{nombre}\".");
            return;
        }
        if (retraso > 0f) { StartCoroutine(Diferido(nombre, volumen, tono, retraso)); return; }
        // el mismo efecto dos veces en el mismo instante (dos avisos del mismo golpe) suena una sola vez
        float ahora = Time.unscaledTime;
        if (ultimaVez.TryGetValue(nombre, out float antes) && ahora - antes < 0.05f) return;
        ultimaVez[nombre] = ahora;
        contador[nombre] = VecesSonado(nombre) + 1;
        if (!nombre.StartsWith("ui_")) cuadroSonidoExplicito = Time.frameCount;
        if (FactorEfectos <= 0f || pool.Count == 0) return;

        AudioSource s = null;
        for (int i = 0; i < pool.Count && s == null; i++)
        {
            var f = pool[(siguienteFuente + i) % pool.Count];
            if (!f.isPlaying) { s = f; siguienteFuente = (siguienteFuente + i + 1) % pool.Count; }
        }
        if (s == null) { s = pool[siguienteFuente]; siguienteFuente = (siguienteFuente + 1) % pool.Count; }   // la más vieja
        s.clip = clip;
        s.pitch = tono;
        s.volume = volumen * FactorEfectos;
        s.Play();
    }

    private IEnumerator Diferido(string nombre, float volumen, float tono, float retraso)
    {
        yield return new WaitForSecondsRealtime(retraso);
        Reproducir(nombre, volumen, tono, 0f);
    }

    // ------------------------------------------------------------------ motor y lluvia

    // PlayerController lo llama cada cuadro: velocidad < 0 = a pie (silencio)
    public void Motor(float velocidad, int moto)
    {
        cuadroMotor = Time.frameCount;
        enMoto = velocidad >= 0f;
        fraccionMotor = Mathf.Clamp01(velocidad / velocidadMaximaMoto);
        string clip = moto == 2 ? "motor_grave" : "motor";   // la Chopper suena más grave y pesada
        if (enMoto && clip != clipMotor && clips.TryGetValue(clip, out var c))
        {
            clipMotor = clip;
            motor.clip = c;
            motor.Play();
        }
    }

    public void Lluvia(bool activa)
    {
        llueve = activa;
        if (!activa) { lluvia.Stop(); return; }
        if (!clips.TryGetValue("lluvia", out var c)) return;
        if (lluvia.clip != c || !lluvia.isPlaying) { lluvia.clip = c; lluvia.Play(); }
        proximoTrueno = Time.time + UnityEngine.Random.Range(esperaTrueno.x, esperaTrueno.y);
    }

    private void AplicarVolumenes()
    {
        if (musicaA == null) return;
        musicaA.volume = VolumenMusicaA();
        if (fundido == null) musicaB.volume = 0f;
        lluvia.volume = volumenLluvia * FactorEfectos;
        trueno.volume = FactorEfectos;
        if (FactorEfectos <= 0f) foreach (var s in pool) s.Stop();
    }

    // ------------------------------------------------------------------ cada cuadro

    private void Update()
    {
        // atenuación de la música (AtenuarMusica): se mueve suave, con el tiempo real (también en pausa)
        if (atenuacion != atenuacionObjetivo)
        {
            atenuacion = Mathf.MoveTowards(atenuacion, atenuacionObjetivo, 1.5f * Time.unscaledDeltaTime);
            if (fundido == null) musicaA.volume = VolumenMusicaA();
        }

        // motor: suena solo en moto, con el juego corriendo y el nivel sin terminar; el tono sigue a la velocidad (0.8 a 1.6)
        bool suena = enMoto && cuadroMotor >= Time.frameCount - 1 && Time.timeScale > 0f && FactorEfectos > 0f
                     && (gm == null || !gm.NivelTerminado);
        if (suena)
        {
            if (!motor.isPlaying && motor.clip != null) motor.Play();
            float tono = Mathf.Lerp(0.8f, 1.6f, fraccionMotor);
            motor.pitch = Mathf.MoveTowards(motor.pitch, tono, 3f * Time.unscaledDeltaTime);
            float vol = Mathf.Lerp(volumenMotorQuieto, volumenMotorAndando, fraccionMotor) * FactorEfectos;
            motor.volume = Mathf.MoveTowards(motor.volume, vol, Time.unscaledDeltaTime);
        }
        else if (motor.isPlaying)
        {
            motor.volume = Mathf.MoveTowards(motor.volume, 0f, 4f * Time.unscaledDeltaTime);   // sin clic al apagar
            if (motor.volume <= 0f) { motor.Stop(); clipMotor = null; }
        }

        // truenos lejanos al azar mientras llueve (con el tiempo del juego: en pausa no hay)
        if (llueve && Time.time >= proximoTrueno && clips.TryGetValue("trueno", out var t))
        {
            proximoTrueno = Time.time + UnityEngine.Random.Range(esperaTrueno.x, esperaTrueno.y);
            if (FactorEfectos > 0f)
            {
                trueno.pitch = UnityEngine.Random.Range(0.85f, 1.1f);
                trueno.PlayOneShot(t, UnityEngine.Random.Range(0.5f, 0.8f));
                contador["trueno"] = VecesSonado("trueno") + 1;
            }
        }

        RevisarBotones();
    }

    // Clic y hover de cualquier botón de la interfaz, sin conectar nada en los botones
    private void RevisarBotones()
    {
        var raton = Mouse.current;
        var es = EventSystem.current;
        if (raton == null || es == null) return;
        Vector2 p = raton.position.ReadValue();
        bool pulsa = raton.leftButton.wasPressedThisFrame, suelta = raton.leftButton.wasReleasedThisFrame;
        if (p != ultimoPuntero || pulsa || suelta)
        {
            ultimoPuntero = p;
            var nuevo = BotonBajo(es, p);
            if (nuevo != null && nuevo != hover) Reproducir("ui_hover");
            hover = nuevo;
        }
        if (pulsa) presionado = hover;
        if (suelta)
        {
            clicPendiente = hover != null && hover == presionado;
            presionado = null;
        }
    }

    private readonly List<RaycastResult> golpes = new List<RaycastResult>();

    private GameObject BotonBajo(EventSystem es, Vector2 p)
    {
        golpes.Clear();
        es.RaycastAll(new PointerEventData(es) { position = p }, golpes);
        if (golpes.Count == 0) return null;
        var b = golpes[0].gameObject.GetComponentInParent<Button>();
        return b != null && b.IsInteractable() ? b.gameObject : null;
    }

    private void LateUpdate()
    {
        // el clic suena después de que el botón hizo lo suyo: si eso ya sonó (pausa, compra...), no se encima
        if (!clicPendiente) return;
        clicPendiente = false;
        if (cuadroSonidoExplicito != Time.frameCount) Reproducir("ui_clic");
    }
}
