using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Crea la escena Assets/Scenes/Intro.unity desde cero: Canvas 1920x1080 (match 0.5), EventSystem con el
// Input System y el objeto IntroHistoria con todo asignado. Un CanvasGroup "Contenido" envuelve la viñeta
// a pantalla completa (480x270 a x4, de pixel_town/intro/generar_intro.py) y el panel crema de abajo con
// el texto grande y el "Espacio para continuar" que parpadea. Arriba a la derecha, fuera del fundido,
// la pastilla "Saltar (Esc)" que llama a IntroHistoria.Omitir.
// El arte importante de cada viñeta está por encima de la fila 215 (de 270), donde empieza el panel.
// Se puede ejecutar varias veces: la escena se rehace entera y se sobrescribe; al final se pone el orden
// del build MenuPrincipal (0), Intro (1), SampleScene (2).
public static class ConstruirIntro
{
    const string RutaEscena = ConstruirMenuPrincipal.RutaIntro;
    const string CarpetaVinetas = "Assets/Sprits/Intro/";
    const string CarpetaUI = "Assets/Sprits/UI/";
    const int Escala = 4;

    static readonly Color32 Oscuro = new Color32(0x2A, 0x1F, 0x40, 255);

    // {nombre} lo cambia IntroHistoria por el nombre escrito en el menú
    static readonly string[] Textos =
    {
        "En el barrio todos conocen el Salón Brenn's: peinados, uñas, maquillaje y mucho cariño.",
        "Hoy hay pedidos por todos lados: tintes, esmaltes, cremas y kits de belleza. Cada clienta espera con ansias.",
        "Hola, {nombre}. Hoy te toca llevar los pedidos del Salón Brenn's.",
        "Muévete con W A S D o las flechas. Entrega con E frente a la puerta. Si tardas o chocas, " +
        "la clienta se desespera. ¡Satisfacción alta, más propina!",
        "Junta tus ganancias, compra una moto y llega más rápido a los siguientes niveles. ¡A repartir!",
    };

    // panel de abajo: de 4 unidades sobre el borde hasta la fila 209 de la viñeta (lo que tapa por debajo
    // de la 215 es solo suelo); así caben tres líneas a 36
    const float AnchoPanel = 1760, AltoPanel = 240, YPanel = -540 + 4 + AltoPanel / 2;
    const float MargenMarco = 7 * Escala;
    const string NombreTextoPrueba = "Hola, Repartidora Brenn. Hoy te toca llevar los pedidos del Salón Brenn's.";   // nombre largo (16)

    [MenuItem("Delivery Brenn's/Construir intro")]
    static void ConstruirMenu()
    {
        try
        {
            Debug.Log(Construir(out _));
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    static string Construir(out bool ok)
    {
        ok = false;
        // ---- Primero se revisa todo; si falta algo no se crea nada ----
        var vinetas = Enumerable.Range(1, Textos.Length)
            .Select(i => ConstruirMenuPrincipal.ImportarPixel($"{CarpetaVinetas}vineta_{i}.png", Vector4.zero)).ToArray();
        var marco = AssetDatabase.LoadAssetAtPath<Sprite>(CarpetaUI + "marco_panel.png");
        var pastel = ConstruirMenuPrincipal.ImportarPixel(CarpetaUI + "boton_pastel_normal.png", new Vector4(6, 6, 6, 4));
        var pastelPresionado = ConstruirMenuPrincipal.ImportarPixel(CarpetaUI + "boton_pastel_presionado.png", new Vector4(6, 6, 6, 4));
        for (int i = 0; i < vinetas.Length; i++)
            if (vinetas[i] == null) Debug.LogError($"Falta {CarpetaVinetas}vineta_{i + 1}.png (corre pixel_town/intro/generar_intro.py).");
        if (marco == null || pastel == null || pastelPresionado == null) Debug.LogError("Faltan marco_panel o las pastillas pastel en " + CarpetaUI);
        var fuente = FuenteProyecto.Fuente();
        if (vinetas.Any(v => v == null) || marco == null || pastel == null || pastelPresionado == null || fuente == null)
            return "NO SE CONSTRUYÓ la intro (ver errores).";

        if (SceneManager.GetSceneByPath(RutaEscena).isLoaded)
        {
            var abierta = SceneManager.GetSceneByPath(RutaEscena);
            if (SceneManager.loadedSceneCount == 1)
                return "NO SE CONSTRUYÓ: Intro es la única escena abierta. Abre SampleScene y vuelve a ejecutarlo.";
            if (abierta.isDirty) return "NO SE CONSTRUYÓ: Intro está abierta con cambios sin guardar.";
            EditorSceneManager.CloseScene(abierta, true);
        }

        var activa = SceneManager.GetActiveScene();
        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        string informe;
        try
        {
            SceneManager.SetActiveScene(escena);
            Armar(vinetas, marco, pastel, pastelPresionado);
            informe = Verificar(escena, out ok);
            if (ok)
            {
                bool guardada = EditorSceneManager.SaveScene(escena, RutaEscena);
                string build = "Build sin cambiar.";
                ok = guardada && ConstruirMenuPrincipal.PonerEscenasDelBuild(out build);
                informe = (guardada ? "Escena guardada en " + RutaEscena : "NO SE PUDO GUARDAR " + RutaEscena) + "\n" + build + "\n" + informe;
            }
            else informe = "INTRO CON ERRORES (no se guardó)\n" + informe;
        }
        finally
        {
            if (activa.IsValid() && activa.isLoaded) SceneManager.SetActiveScene(activa);
            EditorSceneManager.CloseScene(escena, true);
        }
        return informe;
    }

    static void Armar(Sprite[] vinetas, Sprite marco, Sprite pastel, Sprite pastelPresionado)
    {
        var raiz = ConstruirMenuPrincipal.CrearBase("Canvas", out float referenciaPPU);
        var intro = new GameObject("IntroHistoria").AddComponent<IntroHistoria>();

        // ---- Contenido: viñeta + panel, se desvanece entre viñetas ----
        var contenido = ConstruirMenuPrincipal.CrearHijo("Contenido", raiz);
        ConstruirMenuPrincipal.Estirar(contenido);
        var grupo = contenido.gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0;                   // IntroHistoria lo enciende con el primer fundido
        grupo.blocksRaycasts = false;      // los clics (avanzar) se leen directo del ratón

        var rtVineta = ConstruirMenuPrincipal.CrearHijo("Vineta", contenido);
        ConstruirMenuPrincipal.Estirar(rtVineta);
        var imagen = rtVineta.gameObject.AddComponent<Image>();
        imagen.sprite = vinetas[0];
        imagen.raycastTarget = false;

        var panel = ConstruirMenuPrincipal.CrearHijo("PanelTexto", contenido);
        ConstruirMenuPrincipal.Centrar(panel, 0, YPanel, AnchoPanel, AltoPanel);
        var fondo = panel.gameObject.AddComponent<Image>();
        fondo.sprite = marco;
        fondo.type = Image.Type.Sliced;
        fondo.pixelsPerUnitMultiplier = referenciaPPU / (marco.pixelsPerUnit * Escala);
        fondo.raycastTarget = false;

        var rtTexto = ConstruirMenuPrincipal.CrearHijo("Texto", panel);
        rtTexto.anchorMin = Vector2.zero;
        rtTexto.anchorMax = Vector2.one;
        rtTexto.offsetMin = new Vector2(MargenMarco + 32, MargenMarco + 20);
        rtTexto.offsetMax = new Vector2(-(MargenMarco + 32), -(MargenMarco + 8));
        var texto = rtTexto.gameObject.AddComponent<TextMeshProUGUI>();
        FuenteProyecto.Aplicar(texto);
        texto.text = Textos[0];
        texto.color = Oscuro;
        texto.fontSize = 44;
        texto.enableAutoSizing = true;     // el texto más largo baja hasta 36 para caber en tres líneas
        texto.fontSizeMin = 36;
        texto.fontSizeMax = 44;
        texto.lineSpacing = -8;
        texto.alignment = TextAlignmentOptions.TopLeft;
        texto.textWrappingMode = TextWrappingModes.Normal;
        texto.raycastTarget = false;

        // "Espacio para continuar »", abajo a la derecha dentro del marco
        var indicador = ConstruirMenuPrincipal.CrearHijo("IndicadorSiguiente", panel);
        indicador.anchorMin = indicador.anchorMax = indicador.pivot = new Vector2(1, 0);
        indicador.anchoredPosition = new Vector2(-(MargenMarco + 24), MargenMarco + 2);   // dentro de la parte crema
        indicador.sizeDelta = new Vector2(520, 40);
        indicador.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
        indicador.gameObject.AddComponent<Parpadeo>();
        var tmpIndicador = indicador.gameObject.AddComponent<TextMeshProUGUI>();
        FuenteProyecto.Aplicar(tmpIndicador);
        tmpIndicador.text = "Espacio para continuar »";
        tmpIndicador.color = Oscuro;
        tmpIndicador.fontSize = 30;
        tmpIndicador.alignment = TextAlignmentOptions.BottomRight;
        tmpIndicador.textWrappingMode = TextWrappingModes.NoWrap;
        tmpIndicador.raycastTarget = false;

        // ---- "Saltar (Esc)", siempre visible ----
        var saltar = ConstruirMenuPrincipal.CrearHijo("BotonSaltar", raiz);
        saltar.anchorMin = saltar.anchorMax = saltar.pivot = new Vector2(1, 1);
        saltar.anchoredPosition = new Vector2(-32, -32);
        saltar.sizeDelta = new Vector2(330, 84);
        var img = saltar.gameObject.AddComponent<Image>();
        img.sprite = pastel;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = referenciaPPU / (pastel.pixelsPerUnit * Escala);
        var boton = saltar.gameObject.AddComponent<Button>();
        boton.targetGraphic = img;
        boton.transition = Selectable.Transition.SpriteSwap;
        var estados = boton.spriteState;
        estados.pressedSprite = pastelPresionado;
        boton.spriteState = estados;
        var rtSaltar = ConstruirMenuPrincipal.CrearHijo("Texto", saltar);
        rtSaltar.anchorMin = Vector2.zero;
        rtSaltar.anchorMax = Vector2.one;
        rtSaltar.offsetMin = new Vector2(6 * Escala, 4 * Escala);
        rtSaltar.offsetMax = new Vector2(-6 * Escala, -2 * Escala);
        var tmpSaltar = rtSaltar.gameObject.AddComponent<TextMeshProUGUI>();
        tmpSaltar.text = "Saltar (Esc)";
        FuenteProyecto.EstiloPastilla(tmpSaltar, 36);
        UnityEventTools.AddPersistentListener(boton.onClick, intro.Omitir);

        // ---- IntroHistoria ----
        var so = new SerializedObject(intro);
        var lista = so.FindProperty("vinetas");
        lista.arraySize = Textos.Length;
        for (int i = 0; i < Textos.Length; i++)
        {
            var v = lista.GetArrayElementAtIndex(i);
            v.FindPropertyRelative("imagen").objectReferenceValue = vinetas[i];
            v.FindPropertyRelative("texto").stringValue = Textos[i];
        }
        so.FindProperty("imagen").objectReferenceValue = imagen;
        so.FindProperty("texto").objectReferenceValue = texto;
        so.FindProperty("grupo").objectReferenceValue = grupo;
        so.FindProperty("indicadorSiguiente").objectReferenceValue = indicador.gameObject;
        so.FindProperty("escenaJuego").stringValue = Path.GetFileNameWithoutExtension("Assets/Scenes/SampleScene.unity");
        so.ApplyModifiedPropertiesWithoutUndo();

        indicador.gameObject.SetActive(false);   // IntroHistoria lo enciende al terminar de escribir
    }

    static string Verificar(Scene escena, out bool ok)
    {
        var errores = new List<string>();
        var raices = escena.GetRootGameObjects();
        var canvas = raices.Select(g => g.GetComponent<Canvas>()).FirstOrDefault(c => c != null);
        var intro = raices.Select(g => g.GetComponent<IntroHistoria>()).FirstOrDefault(m => m != null);
        if (canvas == null || intro == null)
        {
            ok = false;
            return "VERIFICACIÓN: falta el Canvas o el objeto IntroHistoria.";
        }
        errores.AddRange(ConstruirMenuPrincipal.VerificarBase(raices, canvas));
        errores.AddRange(ConstruirMenuPrincipal.VerificarTextos(canvas.transform, "Boton"));

        var so = new SerializedObject(intro);
        var grupo = so.FindProperty("grupo").objectReferenceValue as CanvasGroup;
        var imagen = so.FindProperty("imagen").objectReferenceValue as Image;
        var texto = so.FindProperty("texto").objectReferenceValue as TextMeshProUGUI;
        var indicador = so.FindProperty("indicadorSiguiente").objectReferenceValue as GameObject;
        if (grupo == null) errores.Add("IntroHistoria: Grupo sin asignar.");
        if (imagen == null || grupo == null || !imagen.transform.IsChildOf(grupo.transform)) errores.Add("IntroHistoria: la Imagen no está dentro del CanvasGroup.");
        else if (imagen.rectTransform.anchorMin != Vector2.zero || imagen.rectTransform.anchorMax != Vector2.one) errores.Add("La viñeta no es de pantalla completa.");
        if (texto == null || grupo == null || !texto.transform.IsChildOf(grupo.transform)) errores.Add("IntroHistoria: el Texto no está dentro del CanvasGroup.");
        if (indicador == null || indicador.GetComponent<Parpadeo>() == null || indicador.GetComponent<TMP_Text>()?.text.StartsWith("Espacio para continuar") != true)
            errores.Add("IntroHistoria: el indicador no es \"Espacio para continuar\" con Parpadeo.");
        if (so.FindProperty("escenaJuego").stringValue != "SampleScene") errores.Add("IntroHistoria: Escena Juego no es SampleScene.");

        var lista = so.FindProperty("vinetas");
        if (lista.arraySize != Textos.Length) errores.Add($"IntroHistoria tiene {lista.arraySize} viñetas, no {Textos.Length}.");
        for (int i = 0; i < Math.Min(lista.arraySize, Textos.Length); i++)
        {
            var v = lista.GetArrayElementAtIndex(i);
            var sprite = v.FindPropertyRelative("imagen").objectReferenceValue as Sprite;
            var imp = sprite != null ? AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite)) as TextureImporter : null;
            if (sprite == null || sprite.rect.size != new Vector2(480, 270)) errores.Add($"Viñeta {i + 1}: no tiene su imagen de 480x270.");
            else if (imp == null || imp.filterMode != FilterMode.Point || imp.textureCompression != TextureImporterCompression.Uncompressed)
                errores.Add($"Viñeta {i + 1}: la imagen no está en Point sin compresión.");
            if (v.FindPropertyRelative("texto").stringValue != Textos[i]) errores.Add($"Viñeta {i + 1}: el texto no es el pedido.");
        }

        // cada texto (con un nombre largo en la 3) cabe en el panel a 36 como mínimo
        if (texto != null)
        {
            var guardado = texto.text;
            float alto = texto.rectTransform.rect.height, ancho = texto.rectTransform.rect.width;
            foreach (var t in Textos.Select(t => t.Contains("{nombre}") ? NombreTextoPrueba : t))
            {
                texto.fontSize = texto.fontSizeMin;
                if (texto.GetPreferredValues(t, ancho, 0).y > alto + 0.5f) errores.Add($"No cabe en el panel ({texto.GetPreferredValues(t, ancho, 0).y:0} de alto en {alto:0}): \"{t}\".");
            }
            texto.fontSize = texto.fontSizeMax;
            texto.text = guardado;
        }

        var saltar = canvas.transform.Find("BotonSaltar")?.GetComponent<Button>();
        if (saltar == null || saltar.onClick.GetPersistentEventCount() != 1 || saltar.onClick.GetPersistentTarget(0) != intro
            || saltar.onClick.GetPersistentMethodName(0) != "Omitir")
            errores.Add("BotonSaltar no llama (solo) a IntroHistoria.Omitir.");
        else if (saltar.transform.IsChildOf(grupo.transform)) errores.Add("BotonSaltar no debería desvanecerse con el contenido.");

        ok = errores.Count == 0;
        return $"VERIFICACIÓN: Intro con {lista.arraySize} viñetas, panel de texto, indicador y \"Saltar (Esc)\"." +
               (ok ? "\nSin errores." : "\nERRORES:\n- " + string.Join("\n- ", errores));
    }

    // Si aparece Temp/ConstruirIntro.pedido se construye y el informe queda en Temp/ConstruirIntro.resultado
    [InitializeOnLoad]
    static class PedidoExterno
    {
        const string Pedido = "Temp/ConstruirIntro.pedido";
        const string Resultado = "Temp/ConstruirIntro.resultado";

        static PedidoExterno() => EditorApplication.update += Revisar;

        static void Revisar()
        {
            if (!File.Exists(Pedido) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;
                return;
            }
            File.Delete(Pedido);
            var consola = new List<string>();
            void Anotar(string msg, string pila, LogType tipo)
            {
                if (tipo != LogType.Log) consola.Add($"[{tipo}] {msg}" + (tipo == LogType.Exception ? "\n" + pila : ""));
            }
            Application.logMessageReceived += Anotar;
            string texto;
            try
            {
                texto = Construir(out bool ok);
                texto = (ok ? "OK\n" : "CON ERRORES\n") + texto;
                Debug.Log(texto);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                texto = "ERROR\n" + e;
            }
            finally
            {
                Application.logMessageReceived -= Anotar;
            }
            texto += "\n\nCONSOLA (advertencias y errores): " + (consola.Count == 0 ? "ninguno" : "\n" + string.Join("\n", consola));
            File.WriteAllText(Resultado, texto);
        }
    }
}
