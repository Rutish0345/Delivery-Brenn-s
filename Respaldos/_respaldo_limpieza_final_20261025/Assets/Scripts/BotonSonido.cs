using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Botón "Música: sí/no" o "Efectos: sí/no" (en PanelPausa y en el menú principal; lo arma ConstruirSonido).
// El onClick llama a Alternar; el estado se guarda en PlayerPrefs (AudioManager) y vale entre escenas y sesiones.
// En el menú principal es un icono (nota musical o bocina) sin texto: apagado, muestra la raya roja encima.
public class BotonSonido : MonoBehaviour
{
    public enum Canal { Musica, Efectos }

    [SerializeField] private Canal canal;
    [SerializeField] private TMP_Text texto;
    [SerializeField] private Image fondo;
    [SerializeField] private GameObject raya;    // los iconos del menú: la raya diagonal de apagado

    private static readonly Color Apagado = new Color32(0xC9, 0xB8, 0xD6, 0xFF);   // la pastilla se ve "desactivada"

    public Canal CanalDelBoton => canal;
    public bool RayaVisible => raya != null && raya.activeSelf;
    public bool Activo => canal == Canal.Musica ? AudioManager.MusicaActiva : AudioManager.EfectosActivos;

    private void OnEnable()
    {
        AudioManager.Cambio += Refrescar;
        Refrescar();
    }

    private void OnDisable() => AudioManager.Cambio -= Refrescar;

    public void Alternar()
    {
        if (canal == Canal.Musica) AudioManager.MusicaActiva = !AudioManager.MusicaActiva;
        else AudioManager.EfectosActivos = !AudioManager.EfectosActivos;
    }

    public static string Etiqueta(Canal canal, bool activo) => (canal == Canal.Musica ? "Música" : "Efectos") + (activo ? ": sí" : ": no");

    private void Refrescar()
    {
        if (texto != null) texto.text = Etiqueta(canal, Activo);
        if (raya != null) raya.SetActive(!Activo);
        else if (fondo != null) fondo.color = Activo ? Color.white : Apagado;
    }
}
