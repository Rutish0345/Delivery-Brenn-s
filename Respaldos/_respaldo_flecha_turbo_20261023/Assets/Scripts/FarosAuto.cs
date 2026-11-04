using UnityEngine;

// Faros delanteros de un auto (nivel 3, de noche): una Light2D en cono corto, hija del TrafficCar, que se pone frente
// al cofre y apunta hacia donde avanza el auto (también en las curvas). La Light2D apunta hacia su +Y local.
public class FarosAuto : MonoBehaviour
{
    [SerializeField] private TrafficCar auto;
    [SerializeField] private float adelante = 1.3f;   // del centro del auto al frente (mide 3 tiles de largo)

    private void LateUpdate()
    {
        if (auto == null) return;
        Vector2 d = auto.Dir;
        if (d.sqrMagnitude < 1e-4f) return;
        d.Normalize();
        transform.position = (Vector3)(auto.Centro + d * adelante);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f);
    }
}
