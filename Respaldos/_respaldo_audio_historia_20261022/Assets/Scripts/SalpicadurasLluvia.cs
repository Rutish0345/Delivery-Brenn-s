using UnityEngine;
using UnityEngine.Tilemaps;

// Salpicaduras de la lluvia en el suelo (nivel 3): una reserva fija de "maximo" SpriteRenderer (20 como máximo,
// nunca se crean más). Cada una espera un momento, aparece en un lugar al azar dentro de la cámara sobre calle o
// banqueta mojada (tiles *_mojado del Tilemap "Suelo") y anima su onda en 3 cuadros. Se apaga con el objeto "lluvia".
public class SalpicadurasLluvia : MonoBehaviour
{
    [SerializeField] private Sprite[] cuadros;          // salpicadura_1..3
    [SerializeField] private SpriteRenderer[] reserva;  // hijos que arma el constructor (apagados)
    [SerializeField] private Tilemap suelo;
    [SerializeField] private float duracionCuadro = 0.08f;
    [SerializeField] private Vector2 esperaEntre = new Vector2(0.05f, 0.9f);
    [SerializeField] private int intentosLugar = 6;

    private float[] reloj;
    private Camera cam;

    public int Activas { get; private set; }   // para las pruebas

    private void OnEnable()
    {
        cam = Camera.main;
        reloj = new float[reserva != null ? reserva.Length : 0];
        for (int i = 0; i < reloj.Length; i++)
        {
            reloj[i] = -Random.Range(esperaEntre.x, esperaEntre.y);   // negativo: esperando su turno
            if (reserva[i] != null) reserva[i].gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (cam == null || cuadros == null || cuadros.Length == 0) return;
        float dt = Time.deltaTime;
        int activas = 0;
        for (int i = 0; i < reloj.Length; i++)
        {
            var sr = reserva[i];
            if (sr == null) continue;
            if (reloj[i] < 0f)
            {
                reloj[i] += dt;
                if (reloj[i] >= 0f && !Aparecer(sr)) reloj[i] = -esperaEntre.x;   // sin lugar: lo intenta enseguida
                continue;
            }
            reloj[i] += dt;
            int k = (int)(reloj[i] / duracionCuadro);
            if (k >= cuadros.Length)
            {
                sr.gameObject.SetActive(false);
                reloj[i] = -Random.Range(esperaEntre.x, esperaEntre.y);
                continue;
            }
            if (sr.sprite != cuadros[k]) sr.sprite = cuadros[k];
            activas++;
        }
        Activas = activas;
    }

    private bool Aparecer(SpriteRenderer sr)
    {
        float alto = cam.orthographicSize, ancho = alto * cam.aspect;
        Vector2 c = cam.transform.position;
        for (int n = 0; n < intentosLugar; n++)
        {
            var p = new Vector2(c.x + Random.Range(-ancho, ancho), c.y + Random.Range(-alto, alto));
            if (suelo != null)
            {
                var tile = suelo.GetTile(suelo.WorldToCell(p));
                if (tile == null || !tile.name.EndsWith("_mojado")) continue;
            }
            sr.transform.position = new Vector3(p.x, p.y, 0f);
            sr.sprite = cuadros[0];
            sr.gameObject.SetActive(true);
            return true;
        }
        return false;
    }
}
