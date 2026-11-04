using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Intro tipo videoclip: viñetas con texto que se escribe solo.
// Espacio / Enter / clic avanzan; Esc la salta.
public class IntroHistoria : MonoBehaviour
{
    [Serializable]
    public class Vineta
    {
        public Sprite imagen;
        [TextArea(2, 6)] public string texto; // {nombre} se cambia por el nombre escrito en el menú
    }

    [SerializeField] private Vineta[] vinetas;
    [SerializeField] private Image imagen;
    [SerializeField] private TextMeshProUGUI texto;
    [SerializeField] private CanvasGroup grupo;             // se desvanece entre viñetas
    [SerializeField] private GameObject indicadorSiguiente; // "Espacio para continuar" (opcional)
    [SerializeField] private string escenaJuego = "SampleScene";
    [SerializeField] private float caracteresPorSegundo = 45f;
    [SerializeField] private float duracionFade = 0.4f;

    private void Start()
    {
        Time.timeScale = 1f;
        if (grupo != null) grupo.alpha = 0f;
        StartCoroutine(Secuencia());
    }

    private IEnumerator Secuencia()
    {
        // ignora el clic del botón "Iniciar juego" que nos trajo hasta aquí
        yield return new WaitForSecondsRealtime(0.3f);

        foreach (Vineta v in vinetas)
        {
            imagen.sprite = v.imagen;
            texto.text = v.texto.Replace("{nombre}", Nombre());
            texto.ForceMeshUpdate();
            texto.maxVisibleCharacters = 0;
            if (indicadorSiguiente != null) indicadorSiguiente.SetActive(false);

            yield return Fade(0f, 1f);

            // máquina de escribir; si se pulsa avanzar, se muestra todo de golpe
            int total = texto.textInfo.characterCount;
            float visibles = 0f;
            while ((int)visibles < total)
            {
                if (PidenOmitir()) { Omitir(); yield break; }
                if (Avanzar()) break;
                visibles += caracteresPorSegundo * Time.unscaledDeltaTime;
                texto.maxVisibleCharacters = (int)visibles;
                yield return null;
            }
            texto.maxVisibleCharacters = total;
            yield return null; // para que la misma pulsación no cuente dos veces

            if (indicadorSiguiente != null) indicadorSiguiente.SetActive(true);
            while (!Avanzar())
            {
                if (PidenOmitir()) { Omitir(); yield break; }
                yield return null;
            }

            yield return Fade(1f, 0f);
        }

        Omitir();
    }

    private IEnumerator Fade(float desde, float hasta)
    {
        float t = 0f;
        while (t < duracionFade)
        {
            t += Time.unscaledDeltaTime;
            if (grupo != null) grupo.alpha = Mathf.Lerp(desde, hasta, t / duracionFade);
            yield return null;
        }
        if (grupo != null) grupo.alpha = hasta;
    }

    private bool Avanzar()
    {
        var k = Keyboard.current;
        var m = Mouse.current;
        return (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame))
            || (m != null && m.leftButton.wasPressedThisFrame);
    }

    private bool PidenOmitir()
    {
        var k = Keyboard.current;
        return k != null && k.escapeKey.wasPressedThisFrame;
    }

    private string Nombre()
    {
        string n = Progreso.NombreJugador;
        return string.IsNullOrWhiteSpace(n) ? "Repartidor" : n;
    }

    // También sirve para un botón "Saltar" (OnClick)
    public void Omitir()
    {
        StopAllCoroutines();
        Time.timeScale = 1f;
        SceneManager.LoadScene(escenaJuego);
    }
}