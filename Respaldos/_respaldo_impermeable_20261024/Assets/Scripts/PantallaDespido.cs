using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Pantalla de GAME OVER por quejas (Canvas/PanelDespido, la arma ConstruirQuejas): encima del nivel, con el estilo de
// la intro (ilustración 16:9, degradado oscuro, titular "¡TE DESPIDIERON!" con contorno grueso, texto de apoyo y las
// pastillas "Reintentar" y "Menú", que llaman a LevelManager.Reiniciar / IrAlMenu). El LevelManager la muestra al
// despedir; el juego ya está en timeScale 0, así que todo corre en tiempo real.
public class PantallaDespido : MonoBehaviour
{
    [SerializeField] private CanvasGroup grupo;
    [SerializeField] private Transform titular;
    [SerializeField] private Button botonReintentar;
    [SerializeField] private float duracionFade = 0.5f;

    public bool Visible => gameObject.activeInHierarchy && grupo != null && grupo.alpha >= 1f && grupo.interactable;

    public void Mostrar()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();   // encima del HUD, la guía y cualquier panel
        StopAllCoroutines();
        StartCoroutine(Aparecer());
    }

    private IEnumerator Aparecer()
    {
        grupo.alpha = 0f;
        grupo.interactable = false;
        grupo.blocksRaycasts = true;   // desde ya no se puede tocar el nivel detrás
        for (float t = 0f; t < duracionFade; t += Time.unscaledDeltaTime)
        {
            grupo.alpha = t / duracionFade;
            if (titular != null) titular.localScale = Vector3.one * Mathf.Lerp(1.35f, 1f, 1f - (1f - t / duracionFade) * (1f - t / duracionFade));
            yield return null;
        }
        grupo.alpha = 1f;
        if (titular != null) titular.localScale = Vector3.one;
        grupo.interactable = true;
        if (botonReintentar != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(botonReintentar.gameObject);
    }
}
