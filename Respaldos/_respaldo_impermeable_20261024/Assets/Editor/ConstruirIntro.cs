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
// Input System y el objeto IntroHistoria con todo asignado. Estilo de la referencia "menu 2": un CanvasGroup
// "Contenido" envuelve la viñeta a pantalla completa (480x270 a x4, de pixel_town/intro/generar_intro.py),
// el degradado oscuro encima (velo suave arriba, negro al 50 % en el tercio de abajo) y, en ese tercio,
// centrados: el TITULAR grande (blanco, Press Start 2P, contorno y sombra #2A1F40 gruesos), el texto de
// apoyo en crema más chico y la placa tipo START (el marco del proyecto en oscuro) con el indicador que
// parpadea. Arriba a la derecha, fuera del fundido, la pastilla "Saltar (Esc)" que llama a IntroHistoria.Omitir.
// El arte importante de cada viñeta está por encima de la fila 180 (de 270), donde empieza el tercio oscuro.
// Se puede ejecutar varias veces: la escena se rehace entera y se sobrescribe; al final se pone el orden
// del build (OrdenEscenas: MenuPrincipal 0, Seleccion 1, Intro 2, SeleccionNivel 3, SampleScene 4...). Son 6 viñetas; la 2 (el salón,
// "¡SE BUSCA REPARTIDOR/A!"), la imagen de la 4 y el texto de la 6 con la repartidora son de octubre de 2026.
public static class ConstruirIntro
{
    const string RutaEscena = ConstruirMenuPrincipal.RutaIntro;
    const string CarpetaVinetas = "Assets/Sprits/Intro/";
    const string CarpetaUI = "Assets/Sprits/UI/";
    const int Escala = 4;
    const int EscalaPlaca = 3;

    static readonly Color32 Blanco = new Color32(255, 255, 255, 255);
    static readonly Color32 Crema = FuenteProyecto.Crema;

    // {nombre} lo cambia IntroHistoria por el nombre escrito en el menú. archivo: la imagen en Sprits/Intro;
    // archivoRepartidora y textoRepartidora (si no están vacíos) se usan cuando juega la repartidora (Progreso.Genero)
    internal static readonly (string archivo, string titular, string texto, string indicador, string archivoRepartidora, string textoRepartidora)[] Vinetas =
    {
        ("vineta_1", "SALÓN BRENN'S", "Donde cada look empieza con el producto perfecto.", "ESPACIO PARA CONTINUAR", "", ""),
        ("vineta_salon", "¡SE BUSCA REPARTIDOR/A!", "La dueña del Salón Brenn's necesita a alguien rápido y confiable para llevar los pedidos. ¿Serás tú?",
         "ESPACIO PARA CONTINUAR", "", ""),
        ("vineta_2", "¡ALERTA DE BELLEZA!", "Las clientas pidieron sus productos a domicilio y no quieren esperar.", "ESPACIO PARA CONTINUAR", "", ""),
        ("vineta_3", "{nombre}, TE NECESITAMOS", "Tú llevas la belleza hasta la puerta de cada casa.", "ESPACIO PARA CONTINUAR", "vineta_3_repartidora", ""),
        ("vineta_4", "ASÍ SE JUEGA", "Muévete con W A S D, entrega con E y pausa con Esc. No te distraigas: la clienta se desespera.", "ESPACIO PARA CONTINUAR", "", ""),
        ("vineta_5", "¡GÁNATE EL PUESTO!", "Junta propinas para conseguir tu medio de transporte y conviértete en el mejor repartidor del Salón Brenn's. " +
                                          "¡El puesto de repartidor puede ser tuyo!", "PRESIONA ESPACIO PARA EMPEZAR", "",
         "Junta propinas para conseguir tu medio de transporte y conviértete en la mejor repartidora del Salón Brenn's. " +
         "¡El puesto de repartidora puede ser tuyo!"),
    };

    // el tercio de abajo (360 de alto), de abajo hacia arriba: placa, texto de apoyo y titular
    internal const float YPlaca = 40, AltoPlaca = 80;
    internal const float YTexto = 140, AltoTexto = 96, AnchoTexto = 1600;
    internal const float YTitular = 248, AltoTitular = 92, AnchoTitular = 1760;
    internal const float TamTitular = 64, TamTitularMin = 40, TamTexto = 28, TamTextoMin = 22, LineasTexto = 3, TamIndicador = FuenteProyecto.MinimoTitulo;
    const string NombrePrueba = "Repartidora Brenn";   // nombre largo (17) para ver que el titular cabe

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
        var vinetas = Vinetas.Select(v => ConstruirMenuPrincipal.ImportarPixel($"{CarpetaVinetas}{v.archivo}.png", Vector4.zero)).ToArray();
        var deRepartidora = Vinetas.Select(v => v.archivoRepartidora.Length == 0 ? null
                                               : ConstruirMenuPrincipal.ImportarPixel($"{CarpetaVinetas}{v.archivoRepartidora}.png", Vector4.zero)).ToArray();
        var degradado = ConstruirMenuPrincipal.ImportarPixel(CarpetaVinetas + "degradado_intro.png", Vector4.zero);
        var placa = ConstruirMenuPrincipal.ImportarPixel(CarpetaUI + "placa_start.png", new Vector4(7, 7, 7, 7));
        var pastel = ConstruirMenuPrincipal.ImportarPixel(CarpetaUI + "boton_pastel_normal.png", new Vector4(6, 6, 6, 4));
        var pastelPresionado = ConstruirMenuPrincipal.ImportarPixel(CarpetaUI + "boton_pastel_presionado.png", new Vector4(6, 6, 6, 4));
        for (int i = 0; i < vinetas.Length; i++)
        {
            if (vinetas[i] == null) Debug.LogError($"Falta {CarpetaVinetas}{Vinetas[i].archivo}.png (corre pixel_town/intro/generar_intro.py).");
            if (Vinetas[i].archivoRepartidora.Length > 0 && deRepartidora[i] == null)
                Debug.LogError($"Falta {CarpetaVinetas}{Vinetas[i].archivoRepartidora}.png (corre pixel_town/intro/generar_intro.py).");
        }
        if (degradado == null || placa == null) Debug.LogError("Faltan degradado_intro o placa_start (corre pixel_town/intro/generar_intro.py).");
        if (pastel == null || pastelPresionado == null) Debug.LogError("Faltan las pastillas pastel en " + CarpetaUI);
        var fuente = FuenteProyecto.FuenteTitulo();
        if (vinetas.Any(v => v == null) || Vinetas.Where((v, i) => v.archivoRepartidora.Length > 0 && deRepartidora[i] == null).Any() || degradado == null || placa == null || pastel == null || pastelPresionado == null || fuente == null)
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
            Armar(vinetas, deRepartidora, degradado, placa, pastel, pastelPresionado);
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

    // Un rectángulo centrado en x, con el borde de abajo a 'y' del borde de abajo de la pantalla
    internal static RectTransform DesdeAbajo(string nombre, Transform padre, float y, float ancho, float alto)
    {
        var rt = ConstruirMenuPrincipal.CrearHijo(nombre, padre);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = new Vector2(ancho, alto);
        return rt;
    }

    // Lo que comparte con la escena Final (ConstruirFinal): la vista de la historia y el botón de arriba a la derecha
    internal struct Vista
    {
        public CanvasGroup grupo;
        public Image imagen;
        public TextMeshProUGUI titular, texto, indicador;
        public GameObject placa;
    }

    internal static Vista ArmarVista(RectTransform raiz, float referenciaPPU, Sprite primera, Sprite degradado, Sprite placa,
                                     string titular0, string texto0, string indicador0)
    {
        var v = new Vista();
        var materialIntro = FuenteProyecto.MaterialIntro();

        // ---- Contenido: viñeta, degradado y textos; se desvanece entre viñetas ----
        var contenido = ConstruirMenuPrincipal.CrearHijo("Contenido", raiz);
        ConstruirMenuPrincipal.Estirar(contenido);
        v.grupo = contenido.gameObject.AddComponent<CanvasGroup>();
        v.grupo.alpha = 0;                   // IntroHistoria lo enciende con el primer fundido
        v.grupo.blocksRaycasts = false;      // los clics (avanzar) se leen directo del ratón

        var rtVineta = ConstruirMenuPrincipal.CrearHijo("Vineta", contenido);
        ConstruirMenuPrincipal.Estirar(rtVineta);
        v.imagen = rtVineta.gameObject.AddComponent<Image>();
        v.imagen.sprite = primera;
        v.imagen.raycastTarget = false;

        var rtVelo = ConstruirMenuPrincipal.CrearHijo("Degradado", contenido);
        ConstruirMenuPrincipal.Estirar(rtVelo);
        var velo = rtVelo.gameObject.AddComponent<Image>();
        velo.sprite = degradado;
        velo.raycastTarget = false;

        // titular: blanco, Press Start, contorno y sombra #2A1F40; se encoge (hasta 40) con nombres largos
        var titular = DesdeAbajo("Titular", contenido, YTitular, AnchoTitular, AltoTitular).gameObject.AddComponent<TextMeshProUGUI>();
        FuenteProyecto.AplicarTitulo(titular, materialIntro);
        titular.text = titular0;
        titular.color = Blanco;
        titular.enableAutoSizing = true;
        titular.fontSizeMin = TamTitularMin;
        titular.fontSizeMax = TamTitular;
        titular.fontSize = TamTitular;
        titular.alignment = TextAlignmentOptions.Center;
        titular.textWrappingMode = TextWrappingModes.NoWrap;
        titular.raycastTarget = false;
        v.titular = titular;

        // texto de apoyo: crema, más chico, hasta dos líneas
        var texto = DesdeAbajo("Texto", contenido, YTexto, AnchoTexto, AltoTexto).gameObject.AddComponent<TextMeshProUGUI>();
        FuenteProyecto.AplicarTitulo(texto, materialIntro);
        texto.text = texto0;
        texto.color = Crema;
        texto.enableAutoSizing = true;
        texto.fontSizeMin = TamTextoMin;   // solo se encoge si no cabe (el de la viñeta 5, hasta 3 líneas)
        texto.fontSizeMax = TamTexto;
        texto.fontSize = TamTexto;
        texto.lineSpacing = 24;
        texto.alignment = TextAlignmentOptions.Top;
        texto.textWrappingMode = TextWrappingModes.Normal;
        texto.raycastTarget = false;
        v.texto = texto;

        // placa tipo START: crece con su texto (ContentSizeFitter); el texto parpadea
        var rtPlaca = DesdeAbajo("IndicadorSiguiente", contenido, YPlaca, 600, AltoPlaca);
        var imgPlaca = rtPlaca.gameObject.AddComponent<Image>();
        imgPlaca.sprite = placa;
        imgPlaca.type = Image.Type.Sliced;
        imgPlaca.pixelsPerUnitMultiplier = referenciaPPU / (placa.pixelsPerUnit * EscalaPlaca);
        imgPlaca.raycastTarget = false;
        var fila = rtPlaca.gameObject.AddComponent<HorizontalLayoutGroup>();
        fila.padding = new RectOffset(48, 48, 28, 28);
        fila.childAlignment = TextAnchor.MiddleCenter;
        fila.childControlWidth = fila.childControlHeight = true;
        fila.childForceExpandWidth = fila.childForceExpandHeight = false;
        var ajuste = rtPlaca.gameObject.AddComponent<ContentSizeFitter>();
        ajuste.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        ajuste.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var rtIndicador = ConstruirMenuPrincipal.CrearHijo("Texto", rtPlaca);
        rtIndicador.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
        rtIndicador.gameObject.AddComponent<Parpadeo>();
        var indicador = rtIndicador.gameObject.AddComponent<TextMeshProUGUI>();
        FuenteProyecto.AplicarTitulo(indicador);
        indicador.text = indicador0;
        indicador.color = Crema;
        indicador.fontSize = TamIndicador;
        indicador.alignment = TextAlignmentOptions.Center;
        indicador.textWrappingMode = TextWrappingModes.NoWrap;
        indicador.raycastTarget = false;
        v.indicador = indicador;
        v.placa = rtPlaca.gameObject;
        return v;
    }

    // ---- "Saltar (Esc)" (o "Menú (Esc)" en el final), siempre visible arriba a la derecha ----
    internal static Button BotonSaltar(RectTransform raiz, float referenciaPPU, Sprite pastel, Sprite pastelPresionado, string etiqueta)
    {
        var saltar = ConstruirMenuPrincipal.CrearHijo("BotonSaltar", raiz);
        saltar.anchorMin = saltar.anchorMax = saltar.pivot = new Vector2(1, 1);
        saltar.anchoredPosition = new Vector2(-32, -32);
        saltar.sizeDelta = new Vector2(400, 84);
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
        tmpSaltar.text = etiqueta;
        FuenteProyecto.EstiloBoton(tmpSaltar, 36);
        return boton;
    }

    // Conecta la vista con un IntroHistoria (sin las viñetas)
    internal static void ConectarVista(SerializedObject so, Vista v)
    {
        so.FindProperty("imagen").objectReferenceValue = v.imagen;
        so.FindProperty("titular").objectReferenceValue = v.titular;
        so.FindProperty("texto").objectReferenceValue = v.texto;
        so.FindProperty("textoIndicador").objectReferenceValue = v.indicador;
        so.FindProperty("grupo").objectReferenceValue = v.grupo;
        so.FindProperty("indicadorSiguiente").objectReferenceValue = v.placa;
    }

    static void Armar(Sprite[] vinetas, Sprite[] deRepartidora, Sprite degradado, Sprite placa, Sprite pastel, Sprite pastelPresionado)
    {
        var raiz = ConstruirMenuPrincipal.CrearBase("Canvas", out float referenciaPPU);
        var intro = new GameObject("IntroHistoria").AddComponent<IntroHistoria>();
        var vista = ArmarVista(raiz, referenciaPPU, vinetas[0], degradado, placa, Vinetas[0].titular, Vinetas[0].texto, Vinetas[0].indicador);
        var boton = BotonSaltar(raiz, referenciaPPU, pastel, pastelPresionado, "Saltar (Esc)");
        UnityEventTools.AddPersistentListener(boton.onClick, intro.Omitir);

        // ---- IntroHistoria ----
        var so = new SerializedObject(intro);
        var lista = so.FindProperty("vinetas");
        lista.arraySize = Vinetas.Length;
        for (int i = 0; i < Vinetas.Length; i++)
        {
            var v = lista.GetArrayElementAtIndex(i);
            v.FindPropertyRelative("imagen").objectReferenceValue = vinetas[i];
            v.FindPropertyRelative("imagenRepartidora").objectReferenceValue = deRepartidora[i];
            v.FindPropertyRelative("titular").stringValue = Vinetas[i].titular;
            v.FindPropertyRelative("texto").stringValue = Vinetas[i].texto;
            v.FindPropertyRelative("textoRepartidora").stringValue = Vinetas[i].textoRepartidora;
            v.FindPropertyRelative("indicador").stringValue = Vinetas[i].indicador;
        }
        ConectarVista(so, vista);
        so.FindProperty("escenaSiguiente").stringValue = ConstruirSeleccionNivel.Escena;   // la intro termina en la selección de nivel
        so.FindProperty("vinetaRemate").intValue = Vinetas.Length;   // el remate, en la última
        so.ApplyModifiedPropertiesWithoutUndo();

        vista.placa.SetActive(false);   // IntroHistoria la enciende al terminar de escribir
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
        foreach (var t in canvas.GetComponentsInChildren<TMP_Text>(true).Where(t => !FuenteProyecto.EsTitulo(t.font)))
            errores.Add($"{t.transform.parent.name}/{t.name}: la intro va toda en Press Start 2P.");

        var so = new SerializedObject(intro);
        var grupo = so.FindProperty("grupo").objectReferenceValue as CanvasGroup;
        var imagen = so.FindProperty("imagen").objectReferenceValue as Image;
        var titular = so.FindProperty("titular").objectReferenceValue as TextMeshProUGUI;
        var texto = so.FindProperty("texto").objectReferenceValue as TextMeshProUGUI;
        var indicador = so.FindProperty("textoIndicador").objectReferenceValue as TextMeshProUGUI;
        var placa = so.FindProperty("indicadorSiguiente").objectReferenceValue as GameObject;
        if (grupo == null) errores.Add("IntroHistoria: Grupo sin asignar.");
        bool Dentro(Component c) => c != null && grupo != null && c.transform.IsChildOf(grupo.transform);
        if (!Dentro(imagen)) errores.Add("IntroHistoria: la Imagen no está dentro del CanvasGroup.");
        else if (imagen.rectTransform.anchorMin != Vector2.zero || imagen.rectTransform.anchorMax != Vector2.one) errores.Add("La viñeta no es de pantalla completa.");
        var velo = grupo != null ? grupo.transform.Find("Degradado")?.GetComponent<Image>() : null;
        if (velo == null || velo.sprite == null || velo.transform.GetSiblingIndex() != imagen?.transform.GetSiblingIndex() + 1)
            errores.Add("Falta el degradado justo encima de la viñeta.");
        if (!Dentro(titular)) errores.Add("IntroHistoria: el Titular no está dentro del CanvasGroup.");
        else
        {
            if ((Color32)titular.color is var c && (c.r != 255 || c.g != 255 || c.b != 255)) errores.Add("El titular no es blanco.");
            var mat = titular.fontSharedMaterial;
            if (mat == null || !mat.IsKeywordEnabled(ShaderUtilities.Keyword_Outline) || !mat.IsKeywordEnabled(ShaderUtilities.Keyword_Underlay)
                || (Color32)mat.GetColor(ShaderUtilities.ID_OutlineColor) is var o && (o.r != 0x2A || o.g != 0x1F || o.b != 0x40))
                errores.Add("El titular no lleva contorno y sombra #2A1F40.");
        }
        if (!Dentro(texto)) errores.Add("IntroHistoria: el Texto no está dentro del CanvasGroup.");
        else if ((Color32)texto.color is var c && (c.r != Crema.r || c.g != Crema.g || c.b != Crema.b)) errores.Add("El texto de apoyo no es #FFF1D0.");
        if (placa == null || !Dentro(placa.transform) || placa.GetComponent<Image>()?.sprite?.name != "placa_start" || placa.activeSelf)
            errores.Add("IntroHistoria: Indicador Siguiente no es la placa (apagada) dentro del CanvasGroup.");
        if (indicador == null || placa == null || !indicador.transform.IsChildOf(placa.transform) || indicador.GetComponent<Parpadeo>() == null)
            errores.Add("IntroHistoria: Texto Indicador no es el texto de la placa con Parpadeo.");
        if (canvas.transform.Find("Contenido/PanelTexto") != null) errores.Add("Todavía está el panel crema de abajo.");
        if (so.FindProperty("escenaSiguiente").stringValue != ConstruirSeleccionNivel.Escena) errores.Add($"IntroHistoria: Escena Siguiente no es {ConstruirSeleccionNivel.Escena}.");
        if (so.FindProperty("vinetaRemate").intValue != Vinetas.Length) errores.Add("IntroHistoria: el remate no va en la última viñeta.");

        var lista = so.FindProperty("vinetas");
        if (lista.arraySize != Vinetas.Length) errores.Add($"IntroHistoria tiene {lista.arraySize} viñetas, no {Vinetas.Length}.");
        for (int i = 0; i < Math.Min(lista.arraySize, Vinetas.Length); i++)
        {
            var v = lista.GetArrayElementAtIndex(i);
            var sprite = v.FindPropertyRelative("imagen").objectReferenceValue as Sprite;
            var imp = sprite != null ? AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite)) as TextureImporter : null;
            if (sprite == null || sprite.rect.size != new Vector2(480, 270) || sprite.name != Vinetas[i].archivo) errores.Add($"Viñeta {i + 1}: no tiene su imagen {Vinetas[i].archivo} de 480x270.");
            var deElla = v.FindPropertyRelative("imagenRepartidora").objectReferenceValue as Sprite;
            if ((deElla != null ? deElla.name : "") != Vinetas[i].archivoRepartidora || (deElla != null && deElla.rect.size != new Vector2(480, 270)))
                errores.Add($"Viñeta {i + 1}: la imagen de la repartidora no es \"{Vinetas[i].archivoRepartidora}\" de 480x270.");
            if (v.FindPropertyRelative("textoRepartidora").stringValue != Vinetas[i].textoRepartidora) errores.Add($"Viñeta {i + 1}: el texto de la repartidora no es el pedido.");
            else if (imp == null || imp.filterMode != FilterMode.Point || imp.textureCompression != TextureImporterCompression.Uncompressed)
                errores.Add($"Viñeta {i + 1}: la imagen no está en Point sin compresión.");
            if (v.FindPropertyRelative("titular").stringValue != Vinetas[i].titular) errores.Add($"Viñeta {i + 1}: el titular no es el pedido.");
            if (v.FindPropertyRelative("texto").stringValue != Vinetas[i].texto) errores.Add($"Viñeta {i + 1}: el texto no es el pedido.");
            if (v.FindPropertyRelative("indicador").stringValue != Vinetas[i].indicador) errores.Add($"Viñeta {i + 1}: el indicador no es el pedido.");
        }

        // cada titular (con un nombre largo en la 3) cabe en una línea a 40 y cada texto en su caja a 24
        string ConNombre(string s) => s.Replace("{nombre}", NombrePrueba);
        if (titular != null && texto != null)
        {
            // (con el autoajuste encendido GetPreferredValues mide al máximo: se apaga para medir)
            titular.enableAutoSizing = texto.enableAutoSizing = false;
            foreach (var (t, x) in Vinetas.Select(v => (v.titular, v.texto)).Concat(Vinetas.Where(v => v.textoRepartidora.Length > 0).Select(v => (v.titular, v.textoRepartidora))))
            {
                titular.fontSize = titular.fontSizeMin;
                float ancho = titular.GetPreferredValues(ConNombre(t)).x;
                if (ancho > AnchoTitular + 0.5f) errores.Add($"El titular no cabe ({ancho:0} de ancho en {AnchoTitular}): \"{ConNombre(t)}\".");
                texto.fontSize = texto.fontSizeMin;
                float alto = texto.GetPreferredValues(x, AnchoTexto, 0).y;
                if (alto > AltoTexto + 0.5f) errores.Add($"El texto no cabe ({alto:0} de alto en {AltoTexto}): \"{x}\".");
                texto.text = x;
                texto.ForceMeshUpdate();
                if (texto.textInfo.lineCount > LineasTexto) errores.Add($"El texto pasa de {LineasTexto} líneas a {texto.fontSize}: \"{x}\".");
            }
            texto.text = Vinetas[0].texto;
            titular.enableAutoSizing = texto.enableAutoSizing = true;
            titular.fontSize = titular.fontSizeMax;
            texto.fontSize = texto.fontSizeMax;
        }

        var saltar = canvas.transform.Find("BotonSaltar")?.GetComponent<Button>();
        if (saltar == null || saltar.onClick.GetPersistentEventCount() != 1 || saltar.onClick.GetPersistentTarget(0) != intro
            || saltar.onClick.GetPersistentMethodName(0) != "Omitir")
            errores.Add("BotonSaltar no llama (solo) a IntroHistoria.Omitir.");
        else if (grupo != null && saltar.transform.IsChildOf(grupo.transform)) errores.Add("BotonSaltar no debería desvanecerse con el contenido.");

        // el sonido de la historia: la pista que pide IntroHistoria y sus efectos tienen que estar en el prefab de audio
        string pista = so.FindProperty("musica").stringValue;
        if (!ConstruirSonido.Musicas.Contains(pista)) errores.Add($"IntroHistoria: la música \"{pista}\" no está en el AudioManager.");
        errores.AddRange(ConstruirSonido.VerificarPrefab());

        ok = errores.Count == 0;
        return $"VERIFICACIÓN: Intro con {lista.arraySize} viñetas, degradado, titular, texto de apoyo, placa tipo START y \"Saltar (Esc)\", todo en Press Start 2P." +
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
