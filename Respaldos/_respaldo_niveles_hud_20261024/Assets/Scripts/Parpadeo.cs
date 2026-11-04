using UnityEngine;

// Hace parpadear un objeto de la interfaz (como "Espacio para continuar") subiendo y bajando la alpha
// de su CanvasGroup. Usa tiempo sin escalar, así funciona aunque el juego esté en pausa.
[RequireComponent(typeof(CanvasGroup))]
public class Parpadeo : MonoBehaviour
{
    [SerializeField] private float periodo = 1.1f;            // segundos por parpadeo completo
    [SerializeField, Range(0f, 1f)] private float alphaMinima = 0.35f;   // que se lea aun en lo más tenue

    private CanvasGroup grupo;
    private float inicio;

    private void Awake()
    {
        grupo = GetComponent<CanvasGroup>();
    }

    // cada vez que aparece empieza visible
    private void OnEnable()
    {
        inicio = Time.unscaledTime;
        if (grupo != null) grupo.alpha = 1f;
    }

    private void Update()
    {
        float t = (Time.unscaledTime - inicio) / periodo;
        float onda = 0.5f + 0.5f * Mathf.Cos(t * Mathf.PI * 2f);   // 1 → 0 → 1
        grupo.alpha = Mathf.Lerp(alphaMinima, 1f, onda);
    }
}
