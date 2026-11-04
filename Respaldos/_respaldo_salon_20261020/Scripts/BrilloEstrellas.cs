using UnityEngine;

// Hace brillar el marcador del punto de entrega: cambia entre cuadros de destello,
// flota un poquito y late de tamaño. Va en el objeto que tiene el SpriteRenderer del marcador.
[RequireComponent(typeof(SpriteRenderer))]
public class BrilloEstrellas : MonoBehaviour
{
    [SerializeField] private Sprite[] cuadros;
    [SerializeField] private float cuadrosPorSegundo = 6f;
    [SerializeField] private float alturaFlote = 0.08f;
    [SerializeField] private float velocidadFlote = 2f;
    [SerializeField] private float pulso = 0.08f;

    private SpriteRenderer sr;
    private Vector3 posicionBase;
    private Vector3 escalaBase;
    private float fase;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        posicionBase = transform.localPosition;
        escalaBase = transform.localScale;
        fase = Random.value * 10f; // para que no todos brillen sincronizados
    }

    private void OnDisable()
    {
        // al apagarse (cuando el pedido ya no está activo) vuelve a su sitio
        transform.localPosition = posicionBase;
        transform.localScale = escalaBase;
    }

    private void Update()
    {
        if (cuadros != null && cuadros.Length > 0)
        {
            int i = Mathf.FloorToInt(Time.time * cuadrosPorSegundo + fase) % cuadros.Length;
            sr.sprite = cuadros[i];
        }

        float t = Time.time * velocidadFlote + fase;
        transform.localPosition = posicionBase + Vector3.up * (Mathf.Sin(t) * alturaFlote);
        transform.localScale = escalaBase * (1f + Mathf.Sin(t * 1.7f) * pulso);
    }
}