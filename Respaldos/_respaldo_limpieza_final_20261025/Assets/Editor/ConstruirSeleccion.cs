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

// Crea Assets/Scenes/Seleccion.unity desde cero ("ELIGE A TU REPARTIDOR", entre MenuPrincipal e Intro): fondo pastel
// con piso de cuadros (seleccion_fondo.png de pixel_town/intro/generar_intro.py), el título en Press Start crema con
// contorno oscuro, y una sola pantalla con dos tarjetas lado a lado (520x700, separadas 80): #2A1F40 con borde rosa de
// 4 px en 9-slice (pixel_town/intro/generar_tarjetas_seleccion.py), el personaje grande en idle de frente a x6 (Point)
// sobre un círculo y un piso suaves, y solo el nombre abajo. Debajo, la pista "A / D o clic · Enter" y CONFIRMAR
// (BotonBrenn Principal). Todo lo maneja SeleccionPersonaje (TarjetaPersonaje recibe el clic de cada tarjeta).
// Al guardar pone el orden del build (OrdenEscenas).
// Idempotente: la escena se rehace entera en una escena aditiva; la escena abierta no se toca.
// Temp/ConstruirSeleccion.pedido → Temp/ConstruirSeleccion.resultado
public static class ConstruirSeleccion
{
    public const string RutaEscena = "Assets/Scenes/Seleccion.unity";
    const string CarpetaUI = "Assets/Sprits/UI/";
    const string RutaFondo = "Assets/Sprits/Intro/seleccion_fondo.png";
    const string HojaChico = "Assets/Sprits/Personajes/repartidor_sheet.png";
    const string HojaChica = "Assets/Sprits/Personajes/repartidora_sheet.png";
    const int Escala = SeleccionPersonaje.Escala;   // cada píxel del personaje = 6 unidades del Canvas (1920x1080)

    public const string Titulo = "ELIGE A TU REPARTIDOR";
    public const string Ayuda = "A / D o clic · Enter";
    public static readonly string[] Nombres = { "Repartidor", "Repartidora" };
    public static readonly string[] Tarjetas = { "TarjetaRepartidor", "TarjetaRepartidora" };
    // el idle de frente y el pasito (los cuadros de Walk_Frente)
    static readonly int[] Cuadros = { 0, 1, 2, 3 };

    static readonly Color32 Oscuro = FuenteProyecto.Oscuro;
    static readonly Color32 Crema = FuenteProyecto.Crema;

    // (sprite, 9-slice izquierda, abajo, derecha, arriba)
    static readonly (string nombre, Vector4 borde)[] Piezas =
    {
        ("seleccion_tarjeta", Vector4.one * 10), ("seleccion_tarjeta_elegida", Vector4.one * 10), ("seleccion_halo", Vector4.one * 16),
        ("seleccion_piso", Vector4.zero), ("seleccion_circulo", Vector4.zero),
    };

    // Tarjetas de 520x700 separadas 80, centradas (x = ±300) en y = -20; adentro, coordenadas de la tarjeta
    public const float AnchoTarjeta = 520, AltoTarjeta = 700, Separacion = 80, YTarjetas = -20, Halo = 12;
    public const float TamNombre = 36, TamAyuda = 22, YAyuda = -400, YConfirmar = -478, AltoConfirmar = 92, YTitulo = 418;
    const float YPies = -200, XPersonaje = 12, YCirculo = 70, YNombre = -290;

    [MenuItem("Delivery Brenn's/Construir selección de personaje")]
    static void ConstruirMenu()
    {
        try { Debug.Log(Construir(out _)); }
        catch (Exception e) { Debug.LogException(e); }
    }

    static string Construir(out bool ok)
    {
        ok = false;
        var sprites = Piezas.ToDictionary(p => p.nombre, p => ConstruirMenuPrincipal.ImportarPixel(CarpetaUI + p.nombre + ".png", p.borde));
        sprites["fondo"] = ConstruirMenuPrincipal.ImportarPixel(RutaFondo, Vector4.zero);
        foreach (var (hoja, prefijo) in new[] { (HojaChico, "repartidor_sheet_"), (HojaChica, "repartidora_sheet_") })
        {
            var todos = AssetDatabase.LoadAllAssetsAtPath(hoja).OfType<Sprite>().ToList();
            foreach (int c in Cuadros) sprites[prefijo + c] = todos.FirstOrDefault(x => x.name == prefijo + c);
        }
        var faltan = sprites.Where(s => s.Value == null).Select(s => s.Key).ToList();
        if (faltan.Count > 0) return "NO SE CONSTRUYÓ la selección: faltan " + string.Join(", ", faltan) +
                                     " (corre pixel_town/intro/generar_intro.py, generar_tarjetas_seleccion.py y generar_repartidora_sheet.py --unity).";
        if (EstiloBrenn.Cargar() == null) return "NO SE CONSTRUYÓ la selección: falta Resources/EstiloBrenn (corre ConstruirBotones).";

        if (SceneManager.GetSceneByPath(RutaEscena).isLoaded)
        {
            var abierta = SceneManager.GetSceneByPath(RutaEscena);
            if (SceneManager.loadedSceneCount == 1) return "NO SE CONSTRUYÓ: Seleccion es la única escena abierta. Abre otra y vuelve a ejecutarlo.";
            if (abierta.isDirty) return "NO SE CONSTRUYÓ: Seleccion está abierta con cambios sin guardar.";
            EditorSceneManager.CloseScene(abierta, true);
        }

        var activa = SceneManager.GetActiveScene();
        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        string informe;
        try
        {
            SceneManager.SetActiveScene(escena);
            Armar(sprites);
            informe = Verificar(escena, out ok);
            if (ok)
            {
                bool guardada = EditorSceneManager.SaveScene(escena, RutaEscena);
                string build = OrdenEscenas.Poner(out bool ordenOk);
                ok = guardada && ordenOk;
                informe = (guardada ? "Escena guardada en " + RutaEscena : "NO SE PUDO GUARDAR " + RutaEscena) + "\n" + build + "\n" + informe;
            }
            else informe = "SELECCIÓN CON ERRORES (no se guardó)\n" + informe;
        }
        finally
        {
            if (activa.IsValid() && activa.isLoaded) SceneManager.SetActiveScene(activa);
            EditorSceneManager.CloseScene(escena, true);
        }
        return informe;
    }

    static RectTransform Hijo(string nombre, Transform padre, float x, float y, float ancho, float alto)
    {
        var rt = ConstruirMenuPrincipal.CrearHijo(nombre, padre);
        ConstruirMenuPrincipal.Centrar(rt, x, y, ancho, alto);
        return rt;
    }

    // escala: unidades del Canvas por píxel del arte (1 para los marcos 9-slice, 6 para el personaje y su piso)
    static Image Imagen(RectTransform rt, Sprite sprite, float referenciaPPU, int escala, bool rebanada = true)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.type = rebanada ? Image.Type.Sliced : Image.Type.Simple;
        img.pixelsPerUnitMultiplier = referenciaPPU / (sprite.pixelsPerUnit * escala);
        img.raycastTarget = false;
        return img;
    }

    // Press Start, centrado, en una línea y sin Auto Size
    static TextMeshProUGUI Texto(RectTransform rt, string texto, float tam, Color color, Material material = null)
    {
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        FuenteProyecto.AplicarTitulo(tmp, material);
        tmp.text = texto;
        tmp.enableAutoSizing = false;
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
        var raiz = ConstruirMenuPrincipal.CrearBase("Canvas", out float ppu);
        var sel = new GameObject("SeleccionPersonaje").AddComponent<SeleccionPersonaje>();

        var fondo = ConstruirMenuPrincipal.CrearHijo("Fondo", raiz);
        ConstruirMenuPrincipal.Estirar(fondo);
        Imagen(fondo, s["fondo"], ppu, 4, false);

        // ---- Título: Press Start crema con contorno y sombra #2A1F40 gruesos (el material de los titulares de la intro) ----
        Texto(Hijo("Titulo", raiz, 0, YTitulo, 1800, 120), Titulo, 72, Crema, FuenteProyecto.MaterialIntro());

        // ---- Las dos tarjetas ----
        var contenedor = Hijo("Tarjetas", raiz, 0, YTarjetas, 2 * AnchoTarjeta + Separacion, AltoTarjeta);
        var so = new SerializedObject(sel);
        var lista = so.FindProperty("tarjetas");
        lista.arraySize = 2;
        for (int i = 0; i < 2; i++)
        {
            string prefijo = i == 0 ? "repartidor_sheet_" : "repartidora_sheet_";
            float x = (i == 0 ? -1 : 1) * (AnchoTarjeta + Separacion) / 2;
            var tarjeta = Hijo(Tarjetas[i], contenedor, x, 0, AnchoTarjeta, AltoTarjeta);

            var halo = Imagen(Hijo("Halo", tarjeta, 0, 0, AnchoTarjeta + 2 * Halo, AltoTarjeta + 2 * Halo), s["seleccion_halo"], ppu, 1);
            var marco = Imagen(Hijo("Marco", tarjeta, 0, 0, AnchoTarjeta, AltoTarjeta), s["seleccion_tarjeta"], ppu, 1);
            marco.raycastTarget = true;   // recibe el clic de la tarjeta (TarjetaPersonaje, en la raíz)
            var circulo = s["seleccion_circulo"];
            Imagen(Hijo("Circulo", tarjeta, XPersonaje, YCirculo, circulo.rect.width * Escala, circulo.rect.height * Escala), circulo, ppu, Escala, false);
            var piso = s["seleccion_piso"];
            Imagen(Hijo("Piso", tarjeta, XPersonaje, YPies, piso.rect.width * Escala, piso.rect.height * Escala), piso, ppu, Escala, false);

            // el personaje a x6, con los pies (la fila 77 del cuadro de 80) sobre el piso
            var idle = s[prefijo + "0"];
            var rtPersonaje = Hijo("Personaje", tarjeta, XPersonaje, 0, idle.rect.width * Escala, idle.rect.height * Escala);
            rtPersonaje.pivot = new Vector2(0.5f, 0f);
            rtPersonaje.anchoredPosition = new Vector2(XPersonaje, YPies - 3 * Escala);
            var personaje = rtPersonaje.gameObject.AddComponent<Image>();
            personaje.sprite = idle;
            personaje.raycastTarget = false;

            Texto(Hijo("Nombre", tarjeta, 0, YNombre, AnchoTarjeta - 40, 56), Nombres[i], TamNombre, Crema, FuenteProyecto.MaterialIntro());

            // el velo de la no elegida: la misma forma de la tarjeta en negro al 30% (queda al 70%)
            var velo = Imagen(Hijo("Velo", tarjeta, 0, 0, AnchoTarjeta, AltoTarjeta), s["seleccion_tarjeta"], ppu, 1);
            velo.color = new Color(0, 0, 0, 0);

            var clic = new SerializedObject(tarjeta.gameObject.AddComponent<TarjetaPersonaje>());
            clic.FindProperty("seleccion").objectReferenceValue = sel;
            clic.FindProperty("indice").intValue = i;
            clic.ApplyModifiedPropertiesWithoutUndo();

            var t = lista.GetArrayElementAtIndex(i);
            t.FindPropertyRelative("raiz").objectReferenceValue = tarjeta;
            t.FindPropertyRelative("marco").objectReferenceValue = marco;
            t.FindPropertyRelative("halo").objectReferenceValue = halo;
            t.FindPropertyRelative("velo").objectReferenceValue = velo;
            t.FindPropertyRelative("personaje").objectReferenceValue = personaje;
            var cuadros = t.FindPropertyRelative("cuadros");
            cuadros.arraySize = Cuadros.Length;
            for (int c = 0; c < Cuadros.Length; c++) cuadros.GetArrayElementAtIndex(c).objectReferenceValue = s[prefijo + Cuadros[c]];
        }

        // ---- Pista y CONFIRMAR (BotonBrenn Principal) ----
        Texto(Hijo("Ayuda", raiz, 0, YAyuda, 1400, 32), Ayuda, TamAyuda, Oscuro, FuenteProyecto.MaterialTituloPlano());
        var confirmar = Hijo("Confirmar", raiz, 0, YConfirmar, ConstruirMenuPrincipal.AnchoPrincipal, AltoConfirmar);
        var imgConfirmar = confirmar.gameObject.AddComponent<Image>();
        var botonConfirmar = confirmar.gameObject.AddComponent<Button>();
        botonConfirmar.targetGraphic = imgConfirmar;
        UnityEventTools.AddPersistentListener(botonConfirmar.onClick, sel.Confirmar);
        Texto(Hijo("Texto", confirmar, 0, 0, ConstruirMenuPrincipal.AnchoPrincipal - 24, 64), "CONFIRMAR", ConstruirMenuPrincipal.TamPrincipal, Crema);
        BotonBrenn.Vestir(botonConfirmar, BotonBrenn.Variante.Principal, EstiloBrenn.Cargar(),
                          new Vector2(ConstruirMenuPrincipal.AnchoPrincipal, AltoConfirmar), ConstruirMenuPrincipal.TamPrincipal);

        so.FindProperty("marcoNormal").objectReferenceValue = s["seleccion_tarjeta"];
        so.FindProperty("marcoElegido").objectReferenceValue = s["seleccion_tarjeta_elegida"];
        so.FindProperty("escenaIntro").stringValue = "Intro";
        so.FindProperty("escenaJuego").stringValue = ConstruirSeleccionNivel.Escena;   // sin intro, a la selección de nivel
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static string Verificar(Scene escena, out bool ok)
    {
        var errores = new List<string>();
        var raices = escena.GetRootGameObjects();
        var canvas = raices.Select(g => g.GetComponent<Canvas>()).FirstOrDefault(c => c != null);
        var sel = raices.Select(g => g.GetComponent<SeleccionPersonaje>()).FirstOrDefault(c => c != null);
        if (canvas == null || sel == null)
        {
            ok = false;
            return "VERIFICACIÓN: falta el Canvas o SeleccionPersonaje.";
        }
        errores.AddRange(ConstruirMenuPrincipal.VerificarBase(raices, canvas));
        errores.AddRange(ConstruirMenuPrincipal.VerificarTextos(canvas.transform, "Boton"));
        var raiz = canvas.transform;

        var titulo = raiz.Find("Titulo")?.GetComponent<TextMeshProUGUI>();
        if (titulo == null || titulo.text != Titulo || !FuenteProyecto.EsTitulo(titulo.font)) errores.Add($"El título no es \"{Titulo}\" en Press Start 2P.");
        else if (FuenteProyecto.Ancho(titulo, titulo.text) > titulo.rectTransform.rect.width) errores.Add("El título no cabe.");

        // nada de pestañas, ficha, franja ni descripciones
        foreach (var viejo in new[] { "Pestanas", "Ventana", "FranjaIgual", "Estadisticas" })
            if (raiz.Find(viejo) != null) errores.Add($"Sobra Canvas/{viejo}.");

        var so = new SerializedObject(sel);
        var lista = so.FindProperty("tarjetas");
        if (lista.arraySize != 2) errores.Add("SeleccionPersonaje necesita 2 tarjetas.");
        var rects = new List<Rect>();
        for (int i = 0; i < Math.Min(2, lista.arraySize); i++)
        {
            var t = lista.GetArrayElementAtIndex(i);
            var tarjeta = t.FindPropertyRelative("raiz").objectReferenceValue as RectTransform;
            if (tarjeta == null || tarjeta.name != Tarjetas[i]) { errores.Add($"Tarjeta {i}: no es {Tarjetas[i]}."); continue; }
            if (tarjeta.sizeDelta != new Vector2(AnchoTarjeta, AltoTarjeta)) errores.Add($"{tarjeta.name}: mide {tarjeta.sizeDelta}, no {AnchoTarjeta}x{AltoTarjeta}.");
            rects.Add(new Rect(tarjeta.anchoredPosition - tarjeta.sizeDelta / 2, tarjeta.sizeDelta));
            var clic = tarjeta.GetComponent<TarjetaPersonaje>();
            if (clic == null || clic.Indice != i) errores.Add($"{tarjeta.name}: falta TarjetaPersonaje con el índice {i}.");
            if (tarjeta.GetComponent<Button>() != null) errores.Add($"{tarjeta.name}: no debe ser un Button (las flechas las maneja SeleccionPersonaje).");
            var personaje = t.FindPropertyRelative("personaje").objectReferenceValue as Image;
            string esperado = (i == 0 ? "repartidor_sheet_" : "repartidora_sheet_") + "0";
            if (personaje == null || personaje.sprite == null || personaje.sprite.name != esperado) errores.Add($"{tarjeta.name}: el personaje no es {esperado}.");
            else if (personaje.rectTransform.sizeDelta != personaje.sprite.rect.size * Escala || personaje.sprite.texture.filterMode != FilterMode.Point)
                errores.Add($"{tarjeta.name}: el personaje no va a x{Escala} con Point.");
            var cuadros = t.FindPropertyRelative("cuadros");
            if (cuadros.arraySize != Cuadros.Length || personaje == null || cuadros.GetArrayElementAtIndex(0).objectReferenceValue != personaje.sprite)
                errores.Add($"{tarjeta.name}: faltan los cuadros del idle y del pasito.");
            foreach (var campo in new[] { "marco", "halo", "velo" })
                if (t.FindPropertyRelative(campo).objectReferenceValue == null) errores.Add($"{tarjeta.name}: {campo} sin asignar.");
            var textos = tarjeta.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (textos.Length != 1 || textos[0].text != Nombres[i] || textos[0].fontSize != TamNombre)
                errores.Add($"{tarjeta.name}: solo debe llevar el nombre \"{Nombres[i]}\" a {TamNombre}.");
            else if (FuenteProyecto.Ancho(textos[0], textos[0].text) > textos[0].rectTransform.rect.width) errores.Add($"{tarjeta.name}: el nombre no cabe.");
        }
        if (rects.Count == 2 && !Mathf.Approximately(rects[1].xMin - rects[0].xMax, Separacion)) errores.Add($"Las tarjetas no están separadas {Separacion}.");
        foreach (var campo in new[] { "marcoNormal", "marcoElegido" })
            if (so.FindProperty(campo).objectReferenceValue == null) errores.Add($"SeleccionPersonaje: {campo} sin asignar.");

        var ayuda = raiz.Find("Ayuda")?.GetComponent<TextMeshProUGUI>();
        if (ayuda == null || ayuda.text != Ayuda || ayuda.fontSize != TamAyuda) errores.Add($"Falta la pista \"{Ayuda}\" a {TamAyuda}.");
        var b = raiz.Find("Confirmar")?.GetComponent<Button>();
        if (b == null || b.onClick.GetPersistentEventCount() != 1 || b.onClick.GetPersistentTarget(0) != sel || b.onClick.GetPersistentMethodName(0) != "Confirmar")
            errores.Add("Confirmar no llama (solo) a SeleccionPersonaje.Confirmar.");
        else if (b.GetComponent<BotonBrenn>()?.VarianteDelBoton != BotonBrenn.Variante.Principal || ((RectTransform)b.transform).sizeDelta.y != AltoConfirmar)
            errores.Add($"CONFIRMAR no es BotonBrenn Principal de {AltoConfirmar} de alto.");
        // la tarjeta elegida (sube 12 y crece a 1.05) no toca el título ni la pista
        float arriba = YTarjetas + AltoTarjeta / 2 * 1.05f + 12, abajo = YTarjetas - AltoTarjeta / 2 * 1.05f + 12;
        if (arriba > YTitulo - 36 || abajo < YAyuda + 16) errores.Add($"La tarjeta elegida ({abajo:0}..{arriba:0}) se mete bajo el título o la pista.");

        ok = errores.Count == 0;
        return $"VERIFICACIÓN: Seleccion con título, 2 tarjetas (personaje x{Escala} y nombre), pista y CONFIRMAR." +
               (ok ? "\nSin errores." : "\nERRORES:\n- " + string.Join("\n- ", errores));
    }

    [InitializeOnLoad]
    static class PedidoExterno
    {
        const string Pedido = "Temp/ConstruirSeleccion.pedido";
        const string Resultado = "Temp/ConstruirSeleccion.resultado";

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
            try
            {
                texto = Construir(out bool ok);
                texto = (ok ? "OK\n" : "CON ERRORES\n") + texto;
            }
            catch (Exception e)
            {
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
