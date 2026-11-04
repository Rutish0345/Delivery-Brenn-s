using UnityEngine;

// Sube y baja suavemente un elemento de la interfaz (el título del menú). Con tiempo sin escalar y en pasos de
// 'paso' unidades del Canvas, para que se mueva de píxel en píxel como el resto del arte (x4).
public class Flotar : MonoBehaviour
{
    [SerializeField] private float amplitud = 12f;   // unidades del Canvas hacia arriba y hacia abajo
    [SerializeField] private float periodo = 3f;     // segundos por vaivén completo
    [SerializeField] private float paso = 4f;        // un píxel del arte
    // opcional (el título del menú): mientras la presentación esté a la vista va pegado a su letrero, y al irse esta
    // vuelve a flotar poco a poco
    [SerializeField] private PresentacionViva letrero;

    private float amplitudActual = -1f;

    private RectTransform rt;
    private Vector2 origen;

    private void Awake()
    {
        rt = (RectTransform)transform;
        origen = rt.anchoredPosition;
    }

    private void OnDisable()
    {
        if (rt != null) rt.anchoredPosition = origen;
    }

    private void Update()
    {
        if (letrero != null && letrero.isActiveAndEnabled)
        {
            rt.anchoredPosition = origen + letrero.Desplazamiento;
            amplitudActual = 0f;
            return;
        }
        amplitudActual = amplitudActual < 0f ? amplitud : Mathf.MoveTowards(amplitudActual, amplitud, amplitud * Time.unscaledDeltaTime);
        float y = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / periodo) * amplitudActual;
        if (paso > 0f) y = Mathf.Round(y / paso) * paso;
        rt.anchoredPosition = origen + new Vector2(0f, y);
    }
}
