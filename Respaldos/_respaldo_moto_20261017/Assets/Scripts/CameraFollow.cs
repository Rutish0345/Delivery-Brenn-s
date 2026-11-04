using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform objetivo;
    [SerializeField] private float suavizado = 8f;
    // sube el punto de enfoque del pivote (pies) al centro del cuerpo
    [SerializeField] private Vector3 desfase = new Vector3(0f, 1.25f, 0f);

    private void Start()
    {
        if (objetivo == null)
        {
            PlayerController jugador = FindFirstObjectByType<PlayerController>();
            if (jugador != null) objetivo = jugador.transform;
        }
    }

    private void LateUpdate()
    {
        if (objetivo == null) return;

        Vector3 destino = new Vector3(
            objetivo.position.x + desfase.x,
            objetivo.position.y + desfase.y,
            transform.position.z);

        transform.position = Vector3.Lerp(transform.position, destino, suavizado * Time.deltaTime);
    }
}