using UnityEngine;

// Escena Final (la arma ConstruirFinal): tres finales armados con IntroHistoria, apagados en la escena; al cargar se
// enciende el que toca según el resultado del nivel 3 (Progreso.EstrellasNivel3): 3 estrellas = repartidor/a estrella,
// 2 = te quedaste con el puesto, 1 (o 0) = el final amable. Sin resultado (abrir Final directo) se toma 3.
// El botón de saltar de la escena llama a Saltar (el Esc lo atiende el IntroHistoria encendido).
public class FinalHistoria : MonoBehaviour
{
    [SerializeField] private IntroHistoria finalEstrella;   // 3 estrellas
    [SerializeField] private IntroHistoria finalOficial;    // 2 estrellas
    [SerializeField] private IntroHistoria finalAmable;     // 1 estrella

    public IntroHistoria Activo { get; private set; }

    public static int EstrellasDelFinal => Progreso.HayResultadoNivel3 ? Progreso.EstrellasNivel3 : 3;

    private void Awake()
    {
        int estrellas = EstrellasDelFinal;
        Activo = estrellas >= 3 ? finalEstrella : estrellas == 2 ? finalOficial : finalAmable;
        foreach (var f in new[] { finalEstrella, finalOficial, finalAmable })
            if (f != null) f.gameObject.SetActive(f == Activo);
    }

    public void Saltar()
    {
        if (Activo != null) Activo.Omitir();
    }
}
