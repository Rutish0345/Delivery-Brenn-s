using UnityEngine;
using UnityEngine.UI;

// Capa de lluvia en pantalla (nivel 3): RawImage a pantalla completa en un Canvas propio, debajo del HUD. Cada capa
// repite una textura de gotas diagonales (128x128, tileable), cambia entre sus 4 cuadros y se desliza en diagonal;
// la capa de atrás va más chica, lenta y tenue. Solo mueve uvRect: no crea objetos ni partículas (barato a 60 FPS).
// La enciende y apaga el LevelManager (campo "lluvia") junto con las salpicaduras.
public class LluviaPantalla : MonoBehaviour
{
    [System.Serializable]
    public class Capa
    {
        public RawImage imagen;
        [Tooltip("Pixeles de pantalla (en 1080 de alto) por pixel de la textura")]
        public float escala = 3.375f;
        [Tooltip("Pixeles de la textura por segundo, hacia abajo-izquierda")]
        public Vector2 velocidad = new Vector2(-150f, -430f);
        [Range(0f, 1f)] public float opacidad = 0.4f;
        [HideInInspector] public Vector2 desfase;
    }

    [SerializeField] private Texture2D[] cuadros;
    [SerializeField] private float cuadrosPorSegundo = 12f;
    [SerializeField] private Capa[] capas;

    private void OnEnable()
    {
        foreach (var c in capas)
        {
            if (c.imagen == null) continue;
            c.imagen.raycastTarget = false;
            c.imagen.color = new Color(1f, 1f, 1f, c.opacidad);
        }
    }

    private void Update()
    {
        if (cuadros == null || cuadros.Length == 0) return;
        // sigue lloviendo en la pausa y en "ELIGE TU MOTO" (timeScale 0)
        float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        for (int i = 0; i < capas.Length; i++)
        {
            var c = capas[i];
            if (c.imagen == null) continue;
            var tex = cuadros[(int)(t * cuadrosPorSegundo + i) % cuadros.Length];
            if (c.imagen.texture != tex) c.imagen.texture = tex;
            float alto = Screen.height > 0 ? 1080f / Screen.height : 1f;   // misma escala con cualquier resolución
            var tam = new Vector2(Screen.width * alto / (tex.width * c.escala), 1080f / (tex.height * c.escala));
            c.desfase -= new Vector2(c.velocidad.x / tex.width, c.velocidad.y / tex.height) * dt;
            c.desfase = new Vector2(Mathf.Repeat(c.desfase.x, 1f), Mathf.Repeat(c.desfase.y, 1f));
            c.imagen.uvRect = new Rect(c.desfase, tam);
        }
    }
}
