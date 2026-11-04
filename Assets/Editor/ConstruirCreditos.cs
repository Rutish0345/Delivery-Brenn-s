using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Crea Assets/Scenes/Creditos.unity desde cero: los créditos que salen después de "GRACIAS POR JUGAR" en los finales
// de 2 y 3 estrellas (ConstruirFinal pone escenaSiguiente = Creditos en esos dos). Cielo nocturno de pixel art
// (pixel_town/creditos/generar_creditos.py: cielo con luna y pueblo en silueta, estrellas que titilan y nubes lentas),
// el título "Delivery Brenn's" fijo arriba (blanco con las sombras rosa y morada del menú) y el texto subiendo en una
// ventana bajo el título: encabezados dorados #FFD166 y nombres crema #FFF1D0, en Press Start 2P con el contorno
// #2A1F40 de la intro, con espaciado compacto (24 px entre líneas, 56 entre bloques) para que los tres bloques quepan a
// la vez en el área libre entre el título y la franja: el texto entra desde abajo y se detiene centrado ahí. Al final,
// "PRESIONA ESPACIO PARA VOLVER AL MENÚ" parpadeando en la franja. Lo maneja Creditos.cs.
// Idempotente: la escena se rehace entera en una escena aditiva (la abierta no se toca) y queda en Build Profiles
// después de Final (OrdenEscenas). Temp/ConstruirCreditos.pedido → Temp/ConstruirCreditos.resultado
public static class ConstruirCreditos
{
    public const string Escena = "Creditos";
    public const string RutaEscena = "Assets/Scenes/Creditos.unity";
    const string Carpeta = "Assets/Sprits/Creditos/";
    const int Escala = 4;

    public const string Titulo = "Delivery Brenn's";
    public const string Indicador = "PRESIONA ESPACIO PARA VOLVER AL MENÚ";
    public const string Pista = "ESPACIO: ACELERAR    ESC: SALTAR";
        public static readonly (string encabezado, string[] nombres)[] Bloques =
    {
        ("DESARROLLADO POR", new[] { "Ruth Barrientos Angeles", "Benilde Méndez Hernández", "Karla Sofía Morales Hernández" }),
        ("ASESOR", new[] { "Mtro. Juvencio Mendoza Castelán" }),
        ("EMPRESA INSPIRADA", new[] { "Academia, Distribuidora y Salón Brenn's" }),
        ("UNIVERSIDAD TECNOLÓGICA DE LA HUASTECA HIDALGUENSE", new[] { "Ingeniería en Desarrollo y Gestión de Software" }),
    };

    static readonly Color32 Dorado = new Color32(0xFF, 0xD1, 0x66, 255);
    static readonly Color32 Crema = FuenteProyecto.Crema;
    static readonly Color32 Rosa = new Color32(0xFF, 0x6F, 0xA5, 255);
    static readonly Color32 Morado = new Color32(0x8C, 0x52, 0xFF, 255);

    const float TamEncabezado = 36, TamNombre = 30, AltoEncabezado = 44, AltoNombre = 38;
       public const float AireLinea = 16, AireBloque = 40;
    const float AnchoTexto = 1760;
    // la ventana del texto va de debajo del título (su caja termina en 370; la ventana, en 362) hasta 8 px encima de la
    // franja de abajo (80 de alto: su orilla en -460)
    const float YTitulo = 430, ArribaVentana = 362, AireFranja = 8;
    public const float AltoFranja = 80, BordeFranja = 2;
    // la ventana se ancla abajo a la franja (88 sobre la orilla de abajo) y arriba bajo el título (300 sobre el centro):
    // con cualquier alto de pantalla el texto nunca llega a la franja
    const float AbajoVentana = AltoFranja + AireFranja;
    const int CantidadEstrellas = 40;
    public const float Velocidad = 170f, Aceleracion = 4f;
    static readonly string[] Piezas = { "creditos_cielo", "estrella_grande", "estrella_chica", "nube_noche_1", "nube_noche_2" };

    // lo que no puede faltar (también lo exige RevisarCanvas en disco)
    public static readonly string[] Requeridas =
    {
        "Cielo", "Estrellas", "Nubes", "TituloGrupo/Titulo", "Ventana/Contenido", "Franja", "Franja/BordeRosa", "Indicador", "Pista", "Fundido",
    };

        [MenuItem("Delivery Brenn's/Construir créditos")]
    static void ConstruirMenu()
    {
        try
        {
            string informe = Construir(out bool ok);
            Debug.Log(informe);
            // la ventana dice si se guardó o por qué no
            EditorUtility.DisplayDialog("Construir créditos", ok ? "Listo: escena Creditos guardada." : informe, "OK");
        }
        catch (Exception e) { Debug.LogException(e); }
    }

    static string Construir(out bool ok)
    {
        ok = false;
        var s = Piezas.ToDictionary(n => n, n => ConstruirMenuPrincipal.ImportarPixel(Carpeta + n + ".png", Vector4.zero));
        var faltan = s.Where(p => p.Value == null).Select(p => p.Key).ToList();
        if (faltan.Count > 0) return "NO SE CONSTRUYERON los créditos: faltan " + string.Join(", ", faltan) + " (corre pixel_town/creditos/generar_creditos.py).";

        if (SceneManager.GetSceneByPath(RutaEscena).isLoaded)
        {
            var abierta = SceneManager.GetSceneByPath(RutaEscena);
            if (SceneManager.loadedSceneCount == 1) return "NO SE CONSTRUYÓ: Creditos es la única escena abierta. Abre otra y vuelve a ejecutarlo.";
            if (abierta.isDirty) return "NO SE CONSTRUYÓ: Creditos está abierta con cambios sin guardar.";
            EditorSceneManager.CloseScene(abierta, true);
        }

        var activa = SceneManager.GetActiveScene();
        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        string informe;
        try
        {
            SceneManager.SetActiveScene(escena);
            Armar(s);
            informe = Verificar(escena, out ok);
            if (ok)
            {
                bool guardada = EditorSceneManager.SaveScene(escena, RutaEscena);
                string build = OrdenEscenas.Poner(out bool ordenOk);
                ok = guardada && ordenOk;
                informe = (guardada ? "Escena guardada en " + RutaEscena : "NO SE PUDO GUARDAR " + RutaEscena) + "\n" + build + "\n" + informe;
            }
            else informe = "CRÉDITOS CON ERRORES (no se guardó)\n" + informe;
        }
        finally
        {
            if (activa.IsValid() && activa.isLoaded) SceneManager.SetActiveScene(activa);
            EditorSceneManager.CloseScene(escena, true);
        }
        return informe;
    }

    // ------------------------------------------------------------------ armado

    static RectTransform Hijo(string nombre, Transform padre, float x, float y, float ancho, float alto)
    {
        var rt = ConstruirMenuPrincipal.CrearHijo(nombre, padre);
        ConstruirMenuPrincipal.Centrar(rt, x, y, ancho, alto);
        return rt;
    }

    static Image Imagen(RectTransform rt, Sprite sprite, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // Press Start con el contorno #2A1F40 de la intro; el color de cara es el del vértice
    static TextMeshProUGUI Texto(RectTransform rt, string texto, float tam, Color color, Material material = null)
    {
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        FuenteProyecto.AplicarTitulo(tmp, material ?? FuenteProyecto.MaterialIntro());
        tmp.text = texto;
        tmp.fontSize = tam;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
    }

    static void Armar(Dictionary<string, Sprite> s)
    {
        var raiz = ConstruirMenuPrincipal.CrearBase("Canvas", out _);
        var creditos = raiz.gameObject.AddComponent<Creditos>();

        var cielo = ConstruirMenuPrincipal.CrearHijo("Cielo", raiz);
        ConstruirMenuPrincipal.Estirar(cielo);
        Imagen(cielo, s["creditos_cielo"], Color.white);

        // estrellas en la parte alta del cielo (sobre el pueblo), a x4, siempre en las mismas posiciones
        var contEstrellas = ConstruirMenuPrincipal.CrearHijo("Estrellas", raiz);
        ConstruirMenuPrincipal.Estirar(contEstrellas);
        var rnd = new System.Random(24);
        var estrellas = new List<Image>();
        for (int i = 0; i < CantidadEstrellas; i++)
        {
            bool grande = rnd.NextDouble() < 0.35;
            var sprite = s[grande ? "estrella_grande" : "estrella_chica"];
            float lado = sprite.rect.width * Escala;
            float x = Mathf.Round((float)(rnd.NextDouble() * 1840 - 920) / Escala) * Escala;
            float ye = Mathf.Round((float)(rnd.NextDouble() * 640 - 120) / Escala) * Escala;   // de -120 a 520
            estrellas.Add(Imagen(Hijo("Estrella" + (i + 1), contEstrellas, x, ye, lado, lado), sprite, Color.white));
        }

        var contNubes = ConstruirMenuPrincipal.CrearHijo("Nubes", raiz);
        ConstruirMenuPrincipal.Estirar(contNubes);
        var nubes = new List<RectTransform>();
        foreach (var (pieza, xn, yn) in new[] { ("nube_noche_1", -700f, 300f), ("nube_noche_2", -80f, 180f), ("nube_noche_1", 520f, 380f), ("nube_noche_2", 860f, 40f) })
        {
            var sprite = s[pieza];
            var rt = Hijo("Nube" + (nubes.Count + 1), contNubes, xn, yn, sprite.rect.width * Escala, sprite.rect.height * Escala);
            Imagen(rt, sprite, Color.white);
            nubes.Add(rt);
        }

        // título fijo arriba, como el del menú (blanco con sombras rosa y morada desplazadas)
        var tituloGrupo = Hijo("TituloGrupo", raiz, 0, YTitulo, 1600, 120);
        foreach (var (nombre, color, dx, dy) in new[] { ("TituloSombraMorada", (Color)Morado, 9f, -12f), ("TituloSombraRosa", (Color)Rosa, 4.5f, -6f), ("Titulo", Color.white, 0f, 0f) })
        {
            var tmp = Texto(Hijo(nombre, tituloGrupo, dx, dy, 1600, 120), Titulo, 72, color, FuenteProyecto.MaterialTituloPlano());
            tmp.alignment = TextAlignmentOptions.Center;
        }

        // la ventana bajo el título y el texto que sube dentro
        var ventana = ConstruirMenuPrincipal.CrearHijo("Ventana", raiz);
        ventana.anchorMin = new Vector2(0.5f, 0);
        ventana.anchorMax = new Vector2(0.5f, 0.5f);
        ventana.pivot = new Vector2(0.5f, 0.5f);
        ventana.offsetMin = new Vector2(-920, AbajoVentana);
        ventana.offsetMax = new Vector2(920, ArribaVentana);
        ventana.gameObject.AddComponent<RectMask2D>();
        var contenido = ConstruirMenuPrincipal.CrearHijo("Contenido", ventana);
        contenido.anchorMin = contenido.anchorMax = contenido.pivot = new Vector2(0.5f, 1f);
        contenido.anchoredPosition = Vector2.zero;
        float y = 0;
        int n = 0;
        for (int b = 0; b < Bloques.Length; b++)
        {
            if (b > 0) y -= AireBloque;
            var (encabezado, nombres) = Bloques[b];
            // si el encabezado es muy largo (la universidad), se achica solo para que quepa en una línea
            float tamEnc = Mathf.Min(TamEncabezado, Mathf.Floor(AnchoTexto * 0.95f / encabezado.Length));
            Texto(Hijo($"Linea{++n:00}_Encabezado", contenido, 0, y - AltoEncabezado / 2, AnchoTexto, AltoEncabezado), encabezado, tamEnc, Dorado);            y -= AltoEncabezado;
            for (int i = 0; i < nombres.Length; i++)
            {
                y -= AireLinea;
                Texto(Hijo($"Linea{++n:00}_Nombre", contenido, 0, y - AltoNombre / 2, AnchoTexto, AltoNombre), nombres[i], TamNombre, Crema);
                y -= AltoNombre;
            }
        }
        contenido.sizeDelta = new Vector2(AnchoTexto, -y);

        // la franja fija de abajo, fuera de la ventana: el aviso y la pista van encima de ella (hermanos, después)
        var franja = ConstruirMenuPrincipal.CrearHijo("Franja", raiz);
        franja.anchorMin = Vector2.zero;
        franja.anchorMax = new Vector2(1, 0);
        franja.pivot = new Vector2(0.5f, 0);
        franja.anchoredPosition = Vector2.zero;
        franja.sizeDelta = new Vector2(0, AltoFranja);
        Imagen(franja, null, new Color32(0x2A, 0x1F, 0x40, 230));
        var bordeFranja = ConstruirMenuPrincipal.CrearHijo("BordeRosa", franja);
        bordeFranja.anchorMin = new Vector2(0, 1);
        bordeFranja.anchorMax = Vector2.one;
        bordeFranja.pivot = new Vector2(0.5f, 1);
        bordeFranja.anchoredPosition = Vector2.zero;
        bordeFranja.sizeDelta = new Vector2(0, BordeFranja);
        Imagen(bordeFranja, null, Rosa);

        var pista = EnFranja("Pista", raiz, 40);
        Texto(pista, Pista, 18, Crema).alpha = 0.7f;
        var indicador = EnFranja("Indicador", raiz, 64);
        indicador.gameObject.AddComponent<CanvasGroup>();
        Texto(indicador, Indicador, 32, Crema);
        var parpadeo = new SerializedObject(indicador.gameObject.AddComponent<Parpadeo>());
        parpadeo.FindProperty("periodo").floatValue = ConstruirMenuPrincipal.PeriodoParpadeo;
        parpadeo.FindProperty("alphaMinima").floatValue = 0.2f;
        parpadeo.ApplyModifiedPropertiesWithoutUndo();

        var fundido = ConstruirMenuPrincipal.CrearHijo("Fundido", raiz);
        ConstruirMenuPrincipal.Estirar(fundido);
        fundido.gameObject.AddComponent<Image>().color = Color.black;
        fundido.GetComponent<Image>().raycastTarget = false;
        var grupoFundido = fundido.gameObject.AddComponent<CanvasGroup>();
        grupoFundido.alpha = 1f;               // abre en negro y se aclara (Creditos.Start)
        grupoFundido.blocksRaycasts = false;

        var so = new SerializedObject(creditos);
        so.FindProperty("contenido").objectReferenceValue = contenido;
        so.FindProperty("ventana").objectReferenceValue = ventana;
        so.FindProperty("indicador").objectReferenceValue = indicador.gameObject;
        so.FindProperty("pista").objectReferenceValue = pista.gameObject;
        so.FindProperty("franja").objectReferenceValue = franja;
        so.FindProperty("fundido").objectReferenceValue = grupoFundido;
        var lista = so.FindProperty("estrellas");
        lista.arraySize = estrellas.Count;
        for (int i = 0; i < estrellas.Count; i++) lista.GetArrayElementAtIndex(i).objectReferenceValue = estrellas[i];
        var listaNubes = so.FindProperty("nubes");
        listaNubes.arraySize = nubes.Count;
        for (int i = 0; i < nubes.Count; i++) listaNubes.GetArrayElementAtIndex(i).objectReferenceValue = nubes[i];
        so.FindProperty("velocidad").floatValue = Velocidad;
        so.FindProperty("aceleracion").floatValue = Aceleracion;
        so.FindProperty("musica").stringValue = "musica_historia";
        so.FindProperty("escenaMenu").stringValue = "MenuPrincipal";
        so.ApplyModifiedPropertiesWithoutUndo();

        indicador.gameObject.SetActive(false);   // al final: TextMeshPro no arma su material en un objeto inactivo
    }

    // un texto centrado en la franja de abajo (anclado abajo, como la franja)
    static RectTransform EnFranja(string nombre, Transform raiz, float alto)
    {
        var rt = ConstruirMenuPrincipal.CrearHijo(nombre, raiz);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, (AltoFranja - BordeFranja) / 2);
        rt.sizeDelta = new Vector2(1600, alto);
        return rt;
    }

    // ------------------------------------------------------------------ verificación

    static string Verificar(Scene escena, out bool ok)
    {
        var errores = new List<string>();
        var raices = escena.GetRootGameObjects();
        var canvas = raices.Select(g => g.GetComponent<Canvas>()).FirstOrDefault(c => c != null);
        var creditos = canvas != null ? canvas.GetComponent<Creditos>() : null;
        if (canvas == null || creditos == null)
        {
            ok = false;
            return "VERIFICACIÓN: falta el Canvas con Creditos.";
        }
        errores.AddRange(ConstruirMenuPrincipal.VerificarBase(raices, canvas));
        errores.AddRange(ConstruirMenuPrincipal.VerificarTextos(canvas.transform, "Boton"));
        var raiz = canvas.transform;
        foreach (var r in Requeridas)
            if (raiz.Find(r) == null) errores.Add($"Falta Canvas/{r}.");

        // los textos, tal cual (con sus tildes) y con todas sus letras en la fuente
        var contenido = raiz.Find("Ventana/Contenido");
        var lineas = contenido != null ? contenido.Cast<Transform>().Select(t => t.GetComponent<TextMeshProUGUI>()).ToList() : new List<TextMeshProUGUI>();
        var esperadas = Bloques.SelectMany(b => new[] { (b.encabezado, true) }.Concat(b.nombres.Select(n => (n, false)))).ToList();
        if (lineas.Count != esperadas.Count) errores.Add($"Contenido tiene {lineas.Count} líneas, no {esperadas.Count}.");
        for (int i = 0; i < Math.Min(lineas.Count, esperadas.Count); i++)
        {
            var tmp = lineas[i];
            var (texto, encabezado) = esperadas[i];
            if (tmp == null || tmp.text != texto) { errores.Add($"Línea {i + 1}: \"{tmp?.text}\", no \"{texto}\"."); continue; }
            if (!FuenteProyecto.EsTitulo(tmp.font) || tmp.fontSharedMaterial != FuenteProyecto.MaterialIntro()) errores.Add($"\"{texto}\": no es Press Start con el contorno #2A1F40.");
            if (!tmp.font.HasCharacters(texto, out List<char> sinLetra)) errores.Add($"\"{texto}\": la fuente no tiene {string.Concat(sinLetra)}.");
            var c = (Color32)tmp.color;
            var debe = encabezado ? Dorado : Crema;
            if (c.r != debe.r || c.g != debe.g || c.b != debe.b) errores.Add($"\"{texto}\": el color no es {(encabezado ? "#FFD166" : "#FFF1D0")}.");
            if (tmp.alignment != TextAlignmentOptions.Center) errores.Add($"\"{texto}\": no está centrado.");
            if (FuenteProyecto.Ancho(tmp, texto) > tmp.rectTransform.rect.width) errores.Add($"\"{texto}\" no cabe a lo ancho.");
            if (i > 0 && lineas[i - 1] != null)
            {
                var a = lineas[i - 1].rectTransform;
                if (a.anchoredPosition.y - a.sizeDelta.y / 2 < tmp.rectTransform.anchoredPosition.y + tmp.rectTransform.sizeDelta.y / 2)
                    errores.Add($"\"{texto}\" se encima con la línea de arriba.");
            }
        }
        var titulo = raiz.Find("TituloGrupo/Titulo")?.GetComponent<TextMeshProUGUI>();
        if (titulo == null || titulo.text != Titulo || !FuenteProyecto.EsTitulo(titulo.font)) errores.Add($"El título no es \"{Titulo}\" en Press Start 2P.");
        else if (FuenteProyecto.Ancho(titulo, Titulo) > titulo.rectTransform.rect.width) errores.Add("El título no cabe.");
        var ventana = raiz.Find("Ventana") as RectTransform;
        var rtTitulo = raiz.Find("TituloGrupo") as RectTransform;
        if (ventana != null && rtTitulo != null && ventana.offsetMax.y > rtTitulo.anchoredPosition.y - rtTitulo.sizeDelta.y / 2)
            errores.Add("La ventana del texto se mete bajo el título.");
        if (ventana != null && ventana.GetComponent<RectMask2D>() == null) errores.Add("Ventana: falta RectMask2D (el texto se vería sobre el título).");

        var indicador = raiz.Find("Indicador");
        var tmpIndicador = indicador?.GetComponent<TextMeshProUGUI>();
        if (tmpIndicador == null || tmpIndicador.text != Indicador || !tmpIndicador.font.HasCharacters(Indicador)) errores.Add($"Indicador: no dice \"{Indicador}\".");
        if (indicador != null && indicador.gameObject.activeSelf) errores.Add("Indicador: tiene que empezar apagado.");
        if (indicador?.GetComponent<Parpadeo>() == null) errores.Add("Indicador: falta Parpadeo.");
        var pista = raiz.Find("Pista")?.GetComponent<TextMeshProUGUI>();
        if (pista == null || pista.text != Pista || !pista.font.HasCharacters(Pista) || !pista.gameObject.activeSelf) errores.Add($"Pista: \"{Pista}\" prendida desde el principio.");

        // la franja de abajo, fuera de la ventana que se mueve, con el aviso y la pista adentro
        var franja = raiz.Find("Franja") as RectTransform;
        if (franja == null) errores.Add("Falta la franja de abajo.");
        else
        {
            var img = franja.GetComponent<Image>();
            if (franja.sizeDelta.y != AltoFranja || franja.anchorMin != Vector2.zero || franja.anchorMax != new Vector2(1, 0) || img == null
                || ColorUtility.ToHtmlStringRGBA(img.color) != "2A1F40E6")
                errores.Add($"Franja: {AltoFranja} px abajo, a todo lo ancho, #2A1F40 al 90%.");
            var borde = franja.Find("BordeRosa") as RectTransform;
            if (borde == null || borde.sizeDelta.y != BordeFranja || borde.anchorMin.y != 1 || ColorUtility.ToHtmlStringRGB(borde.GetComponent<Image>().color) != "FF6FA5")
                errores.Add("Franja: falta el borde rosa de 2 px arriba.");
            if (franja.IsChildOf(raiz.Find("Ventana"))) errores.Add("Franja: no puede ir dentro de la ventana que se mueve.");
            foreach (var t in new[] { raiz.Find("Indicador") as RectTransform, raiz.Find("Pista") as RectTransform })
                if (t != null && (t.anchoredPosition.y - t.sizeDelta.y / 2 < 0 || t.anchoredPosition.y + t.sizeDelta.y / 2 > AltoFranja - BordeFranja || t.GetSiblingIndex() < franja.GetSiblingIndex()))
                    errores.Add($"{t.name}: tiene que ir dentro de la franja y encima de ella.");
        }
        if (ventana != null && (ventana.anchorMin.y != 0 || ventana.offsetMin.y < AltoFranja))
            errores.Add("La ventana del texto llega a la franja (el texto se vería encima del aviso).");
        // todo el contenido cabe a la vez en la ventana (a 1080p: de -452 a 362), con aire arriba y abajo
        var cont = contenido as RectTransform;
        if (cont != null && ventana != null && cont.sizeDelta.y > 1080 - AltoFranja - AireFranja - (540 - ArribaVentana) - 2 * AireBloque)
            errores.Add($"El contenido ({cont.sizeDelta.y} px) no cabe entero entre el título y la franja.");

        var so = new SerializedObject(creditos);
        foreach (var campo in new[] { "contenido", "ventana", "indicador", "fundido", "pista", "franja" })
            if (so.FindProperty(campo).objectReferenceValue == null) errores.Add($"Creditos: {campo} sin asignar.");
        if (so.FindProperty("estrellas").arraySize != CantidadEstrellas) errores.Add($"Creditos: no tiene sus {CantidadEstrellas} estrellas.");
        if (so.FindProperty("nubes").arraySize == 0) errores.Add("Creditos: sin nubes.");
        if (!Mathf.Approximately(so.FindProperty("velocidad").floatValue, Velocidad) || !Mathf.Approximately(so.FindProperty("aceleracion").floatValue, Aceleracion))
            errores.Add($"Creditos: velocidad {Velocidad} y aceleración x{Aceleracion}.");
        if (so.FindProperty("musica").stringValue != "musica_historia" || so.FindProperty("escenaMenu").stringValue != "MenuPrincipal")
            errores.Add("Creditos: música musica_historia y vuelta a MenuPrincipal.");
        foreach (var n in Piezas)
            if (AssetImporter.GetAtPath(Carpeta + n + ".png") is TextureImporter imp && (imp.filterMode != FilterMode.Point || imp.textureCompression != TextureImporterCompression.Uncompressed))
                errores.Add($"{n}: no está en Point sin compresión.");

        ok = errores.Count == 0;
        return $"VERIFICACIÓN: Creditos con cielo, {so.FindProperty("estrellas").arraySize} estrellas, {so.FindProperty("nubes").arraySize} nubes, título, " +
               $"{lineas.Count} líneas e indicador." + (ok ? "\nSin errores." : "\nERRORES:\n- " + string.Join("\n- ", errores));
    }

    [InitializeOnLoad]
    static class PedidoExterno
    {
        const string Pedido = "Temp/ConstruirCreditos.pedido";
        const string Resultado = "Temp/ConstruirCreditos.resultado";

        static PedidoExterno() => EditorApplication.update += Revisar;

        static void Revisar()
        {
            if (!File.Exists(Pedido) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Pedido);
            var consola = new List<string>();
            void Anotar(string msg, string pila, LogType tipo)
            {
                if (tipo != LogType.Log) consola.Add($"[{tipo}] {msg}" + (tipo == LogType.Exception ? "\n" + pila : ""));
            }
            Application.logMessageReceived += Anotar;
            string texto;
            try { texto = Construir(out bool ok) is var r && ok ? "OK\n" + r : "CON ERRORES\n" + r; }
            catch (Exception e) { Debug.LogException(e); texto = "ERROR\n" + e; }
            finally { Application.logMessageReceived -= Anotar; }
            texto += "\n\nCONSOLA (advertencias y errores): " + (consola.Count == 0 ? "ninguno" : "\n" + string.Join("\n", consola));
            File.WriteAllText(Resultado, texto);
        }
    }
}
