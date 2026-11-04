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

// Crea Assets/Scenes/Seleccion.unity desde cero ("ELIGE A TU REPARTIDOR", entre MenuPrincipal e Intro), según la
// referencia "referencia seleccion personaje" con la paleta del juego: fondo pastel con piso de cuadros
// (seleccion_fondo.png de pixel_town/intro/generar_intro.py), el título en Press Start con contorno oscuro grueso,
// las pestañas REPARTIDOR / REPARTIDORA sobre la ventana rosa (barra de título con su X), la cita, el retrato grande
// con su idle a la izquierda, la ficha a la derecha (Nombre, Puesto, Ítem favorito, Dato), las mismas estadísticas
// para los dos, la franja "Solo cambia el estilo..." y la placa CONFIRMAR (la de START de la intro).
// Todo lo maneja SeleccionPersonaje. Al guardar pone el orden del build (OrdenEscenas).
// Idempotente: la escena se rehace entera en una escena aditiva; la escena abierta no se toca.
// Temp/ConstruirSeleccion.pedido → Temp/ConstruirSeleccion.resultado
public static class ConstruirSeleccion
{
    public const string RutaEscena = "Assets/Scenes/Seleccion.unity";
    const string CarpetaUI = "Assets/Sprits/UI/";
    const string RutaFondo = "Assets/Sprits/Intro/seleccion_fondo.png";
    const string HojaChico = "Assets/Sprits/Personajes/repartidor_sheet.png";
    const string HojaChica = "Assets/Sprits/Personajes/repartidora_sheet.png";
    const int Escala = 4;   // cada píxel del arte = 4 unidades del Canvas (1920x1080)

    public const string Titulo = "ELIGE A TU REPARTIDOR";
    public const string Igual = "Solo cambia el estilo, ¡las dos entregan igual de rápido!";
    public const string Ayuda = "A / D o clic para cambiar  ·  Enter para confirmar";
    public static readonly (string pestana, string cita, string puesto, string dato)[] Fichas =
    {
        ("REPARTIDOR", "{nombre}, LISTO PARA REPARTIR", "Repartidor del Salón Brenn's", "conoce cada atajo del barrio... y a cada perro también."),
        ("REPARTIDORA", "{nombre}, LISTA PARA REPARTIR", "Repartidora del Salón Brenn's", "jamás se le ha caído un labial. Ni uno."),
    };
    public static readonly (string nombre, int puntos)[] Estadisticas = { ("Velocidad", 4), ("Cuidado", 4) };

    static readonly Color32 Oscuro = FuenteProyecto.Oscuro;
    static readonly Color32 Crema = FuenteProyecto.Crema;
    static readonly Color32 RosaOscuro = new Color32(0xC2, 0x41, 0x7A, 255);

    // (sprite, 9-slice izquierda, abajo, derecha, arriba)
    static readonly (string nombre, Vector4 borde)[] Piezas =
    {
        ("seleccion_ventana", new Vector4(6, 6, 6, 6)), ("seleccion_barra", new Vector4(4, 4, 4, 4)), ("seleccion_x", Vector4.zero),
        ("seleccion_retrato", new Vector4(4, 4, 4, 4)), ("seleccion_pestana_activa", new Vector4(6, 2, 6, 5)),
        ("seleccion_pestana_inactiva", new Vector4(6, 2, 6, 5)), ("seleccion_franja", new Vector4(4, 4, 4, 4)),
        ("seleccion_punto_lleno", Vector4.zero), ("seleccion_punto_vacio", Vector4.zero), ("placa_start", new Vector4(7, 7, 7, 7)),
    };

    // Ventana: 1440x600 centrada en y = -40; todo lo de adentro en coordenadas de la ventana
    const float AnchoVentana = 1440, AltoVentana = 600, YVentana = -40;

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
        sprites["chico"] = AssetDatabase.LoadAllAssetsAtPath(HojaChico).OfType<Sprite>().FirstOrDefault(s => s.name == "repartidor_sheet_0");
        sprites["chica"] = AssetDatabase.LoadAllAssetsAtPath(HojaChica).OfType<Sprite>().FirstOrDefault(s => s.name == "repartidora_sheet_0");
        var faltan = sprites.Where(s => s.Value == null).Select(s => s.Key).ToList();
        if (faltan.Count > 0) return "NO SE CONSTRUYÓ la selección: faltan " + string.Join(", ", faltan) +
                                     " (corre pixel_town/intro/generar_intro.py y generar_repartidora_sheet.py --unity).";

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

    static Image Imagen(RectTransform rt, Sprite sprite, float referenciaPPU, bool rebanada = true)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.type = rebanada ? Image.Type.Sliced : Image.Type.Simple;
        img.pixelsPerUnitMultiplier = referenciaPPU / (sprite.pixelsPerUnit * Escala);
        img.raycastTarget = false;
        return img;
    }

    // Pixelify (párrafo) o Press Start (titulo), alineado como se pida
    static TextMeshProUGUI Texto(RectTransform rt, string texto, float tam, Color color, bool titulo, TextAlignmentOptions alineacion,
                                 float minimo = 0, bool envolver = false)
    {
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (titulo) FuenteProyecto.AplicarTitulo(tmp);
        else FuenteProyecto.Aplicar(tmp);
        tmp.text = texto;
        tmp.fontSize = tam;
        tmp.color = color;
        tmp.alignment = alineacion;
        tmp.textWrappingMode = envolver ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        if (minimo > 0)
        {
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = minimo;
            tmp.fontSizeMax = tam;
        }
        tmp.raycastTarget = false;
        return tmp;
    }

    static void Armar(Dictionary<string, Sprite> s)
    {
        var raiz = ConstruirMenuPrincipal.CrearBase("Canvas", out float ppu);
        var sel = new GameObject("SeleccionPersonaje").AddComponent<SeleccionPersonaje>();

        var fondo = ConstruirMenuPrincipal.CrearHijo("Fondo", raiz);
        ConstruirMenuPrincipal.Estirar(fondo);
        Imagen(fondo, s["fondo"], ppu, false);

        // ---- Título: Press Start crema con contorno y sombra #2A1F40 gruesos (el material de los titulares de la intro) ----
        var titulo = Texto(Hijo("Titulo", raiz, 0, 418, 1800, 120), Titulo, 72, Crema, true, TextAlignmentOptions.Center, 48);
        FuenteProyecto.AplicarTitulo(titulo, FuenteProyecto.MaterialIntro());

        // ---- Pestañas (detrás de la ventana: su borde de abajo queda tapado por el de la ventana) ----
        var pestanas = Hijo("Pestanas", raiz, 0, YVentana + AltoVentana / 2 + 40, 1000, 96);
        var imgPestanas = new Image[2];
        var textosPestana = new TextMeshProUGUI[2];
        for (int i = 0; i < 2; i++)
        {
            var rt = Hijo(i == 0 ? "PestanaRepartidor" : "PestanaRepartidora", pestanas, i == 0 ? -250 : 250, 0, 460, 96);
            imgPestanas[i] = Imagen(rt, s[i == 0 ? "seleccion_pestana_activa" : "seleccion_pestana_inactiva"], ppu);
            imgPestanas[i].raycastTarget = true;
            var boton = rt.gameObject.AddComponent<Button>();
            boton.targetGraphic = imgPestanas[i];
            boton.transition = Selectable.Transition.None;   // SeleccionPersonaje cambia el sprite de la activa
            UnityEventTools.AddPersistentListener(boton.onClick, i == 0 ? sel.ElegirRepartidor : sel.ElegirRepartidora);
            var rtTexto = Hijo("Texto", rt, 0, -4, 420, 70);
            textosPestana[i] = Texto(rtTexto, Fichas[i].pestana, 28, i == 0 ? Oscuro : Crema, true, TextAlignmentOptions.Center, FuenteProyecto.MinimoTitulo);
        }

        // ---- Ventana ----
        var ventana = Hijo("Ventana", raiz, 0, YVentana, AnchoVentana, AltoVentana);
        Imagen(ventana, s["seleccion_ventana"], ppu);
        float arriba = AltoVentana / 2, ancho = AnchoVentana - 64;

        var barra = Hijo("Barra", ventana, 0, arriba - 52, ancho, 56);
        Imagen(barra, s["seleccion_barra"], ppu);
        Texto(Hijo("Texto", barra, -40, 0, ancho - 160, 48), "FICHA DE REPARTO · SALÓN BRENN'S", 20, Crema, true, TextAlignmentOptions.Left);
        Imagen(Hijo("X", barra, ancho / 2 - 32, 0, 48, 48), s["seleccion_x"], ppu, false);

        var cita = Texto(Hijo("Cita", ventana, 0, arriba - 128, ancho, 64), Fichas[0].cita, 30, Oscuro, true, TextAlignmentOptions.Center, 20);
        var linea = Hijo("Linea", ventana, 0, arriba - 172, ancho, 4).gameObject.AddComponent<Image>();
        linea.color = RosaOscuro;
        linea.raycastTarget = false;

        // retrato: caja rosa y el personaje a x4, de pie sobre el fondo de la caja
        var caja = Hijo("CajaRetrato", ventana, -ancho / 2 + 220, -38, 400, 340);
        Imagen(caja, s["seleccion_retrato"], ppu);
        var rtRetrato = ConstruirMenuPrincipal.CrearHijo("Retrato", caja);
        rtRetrato.anchorMin = rtRetrato.anchorMax = rtRetrato.pivot = new Vector2(0.5f, 0);
        rtRetrato.anchoredPosition = new Vector2(0, 12);   // la sombra de los pies, justo arriba del filete de la caja
        rtRetrato.sizeDelta = s["chico"].rect.size * Escala;
        var retrato = rtRetrato.gameObject.AddComponent<Image>();
        retrato.sprite = s["chico"];
        retrato.preserveAspect = true;
        retrato.raycastTarget = false;

        // ficha a la derecha del retrato
        float x0 = -ancho / 2 + 460, anchoFicha = ancho / 2 - x0 - 24, cx = x0 + anchoFicha / 2;
        TextMeshProUGUI Dato(string nombre, float y, float alto, bool envolver = false) =>
            Texto(Hijo(nombre, ventana, cx, y, anchoFicha, alto), "", 36, Oscuro, false, TextAlignmentOptions.Left, envolver ? 30 : 28, envolver);
        var nombre = Dato("Nombre", 92, 50);
        var puesto = Dato("Puesto", 38, 50);
        var item = Dato("Item", -16, 50);
        var dato = Dato("Dato", -92, 96, true);
        dato.alignment = TextAlignmentOptions.TopLeft;

        // las mismas estadísticas para los dos
        var stats = Hijo("Estadisticas", ventana, cx, -170, anchoFicha, 40);
        for (int i = 0; i < Estadisticas.Length; i++)
        {
            float xs = -anchoFicha / 2 + i * 440;
            var etiqueta = Hijo(Estadisticas[i].nombre, stats, xs + 100, 0, 200, 40);
            Texto(etiqueta, Estadisticas[i].nombre, 34, Oscuro, false, TextAlignmentOptions.Left);
            for (int p = 0; p < 5; p++)
                Imagen(Hijo($"Punto{p + 1}", stats, xs + 220 + p * 32, 0, 24, 32),
                       s[p < Estadisticas[i].puntos ? "seleccion_punto_lleno" : "seleccion_punto_vacio"], ppu, false);
        }

        var franja = Hijo("FranjaIgual", ventana, 0, -arriba + 60, ancho, 64);
        Imagen(franja, s["seleccion_franja"], ppu);
        Texto(Hijo("Texto", franja, 0, 0, ancho - 48, 56), Igual, 34, Oscuro, false, TextAlignmentOptions.Center, 28);

        // ---- Ayuda y CONFIRMAR (la placa START de la intro: fondo oscuro, texto crema) ----
        Texto(Hijo("Ayuda", raiz, 0, -388, 1400, 50), Ayuda, 36, Oscuro, false, TextAlignmentOptions.Center);
        var confirmar = Hijo("Confirmar", raiz, 0, -472, 560, 104);
        var imgConfirmar = Imagen(confirmar, s["placa_start"], ppu * Escala / 3f);   // la placa va a x3, como en la intro
        imgConfirmar.raycastTarget = true;
        var botonConfirmar = confirmar.gameObject.AddComponent<Button>();
        botonConfirmar.targetGraphic = imgConfirmar;
        var colores = botonConfirmar.colors;
        colores.highlightedColor = new Color(1f, 0.92f, 0.96f);
        colores.pressedColor = new Color(0.8f, 0.75f, 0.85f);
        botonConfirmar.colors = colores;
        UnityEventTools.AddPersistentListener(botonConfirmar.onClick, sel.Confirmar);
        Texto(Hijo("Texto", confirmar, 0, 0, 480, 64), "CONFIRMAR", 36, Crema, true, TextAlignmentOptions.Center, FuenteProyecto.MinimoTitulo);

        // ---- SeleccionPersonaje ----
        var so = new SerializedObject(sel);
        var lista = so.FindProperty("fichas");
        lista.arraySize = 2;
        for (int i = 0; i < 2; i++)
        {
            var f = lista.GetArrayElementAtIndex(i);
            f.FindPropertyRelative("retrato").objectReferenceValue = s[i == 0 ? "chico" : "chica"];
            f.FindPropertyRelative("cita").stringValue = Fichas[i].cita;
            f.FindPropertyRelative("puesto").stringValue = Fichas[i].puesto;
            f.FindPropertyRelative("dato").stringValue = Fichas[i].dato;
        }
        void Arreglo(string campo, UnityEngine.Object[] valores)
        {
            var p = so.FindProperty(campo);
            p.arraySize = valores.Length;
            for (int i = 0; i < valores.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = valores[i];
        }
        Arreglo("pestanas", imgPestanas);
        Arreglo("textosPestana", textosPestana);
        so.FindProperty("pestanaActiva").objectReferenceValue = s["seleccion_pestana_activa"];
        so.FindProperty("pestanaInactiva").objectReferenceValue = s["seleccion_pestana_inactiva"];
        so.FindProperty("retrato").objectReferenceValue = retrato;
        so.FindProperty("cita").objectReferenceValue = cita;
        so.FindProperty("nombre").objectReferenceValue = nombre;
        so.FindProperty("puesto").objectReferenceValue = puesto;
        so.FindProperty("item").objectReferenceValue = item;
        so.FindProperty("dato").objectReferenceValue = dato;
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
        else if (!titulo.fontSharedMaterial.IsKeywordEnabled(ShaderUtilities.Keyword_Outline) ||
                 (Color32)titulo.fontSharedMaterial.GetColor(ShaderUtilities.ID_OutlineColor) is var o && (o.r != Oscuro.r || o.g != Oscuro.g || o.b != Oscuro.b))
            errores.Add("El título no lleva el contorno oscuro.");
        else if (FuenteProyecto.Ancho(titulo, titulo.text) > titulo.rectTransform.rect.width) errores.Add("El título no cabe.");

        foreach (var (ruta, metodo) in new[] { ("Pestanas/PestanaRepartidor", "ElegirRepartidor"), ("Pestanas/PestanaRepartidora", "ElegirRepartidora"), ("Confirmar", "Confirmar") })
        {
            var b = raiz.Find(ruta)?.GetComponent<Button>();
            if (b == null || b.onClick.GetPersistentEventCount() != 1 || b.onClick.GetPersistentTarget(0) != sel || b.onClick.GetPersistentMethodName(0) != metodo)
                errores.Add($"{ruta} no llama (solo) a SeleccionPersonaje.{metodo}.");
        }
        if (raiz.Find("Confirmar")?.GetComponent<Image>()?.sprite?.name != "placa_start") errores.Add("CONFIRMAR no usa la placa START.");
        if (raiz.Find("Ventana")?.GetSiblingIndex() < raiz.Find("Pestanas")?.GetSiblingIndex()) errores.Add("Las pestañas deben ir detrás de la ventana.");

        var so = new SerializedObject(sel);
        var fichas = so.FindProperty("fichas");
        if (fichas.arraySize != 2) errores.Add("SeleccionPersonaje necesita 2 fichas.");
        else
            for (int i = 0; i < 2; i++)
            {
                var f = fichas.GetArrayElementAtIndex(i);
                string esperado = i == 0 ? "repartidor_sheet_0" : "repartidora_sheet_0";
                if ((f.FindPropertyRelative("retrato").objectReferenceValue as Sprite)?.name != esperado) errores.Add($"Ficha {i}: el retrato no es {esperado}.");
                if (f.FindPropertyRelative("puesto").stringValue != Fichas[i].puesto) errores.Add($"Ficha {i}: el puesto no es \"{Fichas[i].puesto}\".");
                if (string.IsNullOrEmpty(f.FindPropertyRelative("dato").stringValue)) errores.Add($"Ficha {i}: falta la línea divertida.");
            }
        foreach (var campo in new[] { "retrato", "cita", "nombre", "puesto", "item", "dato", "pestanaActiva", "pestanaInactiva" })
            if (so.FindProperty(campo).objectReferenceValue == null) errores.Add($"SeleccionPersonaje: {campo} sin asignar.");
        if (so.FindProperty("itemFavorito").stringValue != "mochila de reparto") errores.Add("El ítem favorito no es \"mochila de reparto\".");
        if (raiz.Find("Ventana/FranjaIgual/Texto")?.GetComponent<TextMeshProUGUI>()?.text != Igual) errores.Add($"Falta el texto \"{Igual}\".");
        if (raiz.Find("Ventana/Estadisticas") == null) errores.Add("Faltan las estadísticas.");

        // los textos que cambian caben con un nombre largo (16 letras) y la ficha más larga
        const string NombreLargo = "Repartidora Bren";
        var cita = so.FindProperty("cita").objectReferenceValue as TextMeshProUGUI;
        var nombre = so.FindProperty("nombre").objectReferenceValue as TextMeshProUGUI;
        var puesto = so.FindProperty("puesto").objectReferenceValue as TextMeshProUGUI;
        foreach (var (tmp, texto) in Fichas.SelectMany(f => new[] { (cita, "“" + f.cita.Replace("{nombre}", NombreLargo.ToUpperInvariant()) + "”"),
                                                                     (nombre, "<b>Nombre:</b> " + NombreLargo), (puesto, "<b>Puesto:</b> " + f.puesto) }))
            if (tmp != null && FuenteProyecto.Ancho(tmp, texto) > tmp.rectTransform.rect.width + 0.5f) errores.Add($"No cabe: \"{texto}\".");

        ok = errores.Count == 0;
        return $"VERIFICACIÓN: Seleccion con título, 2 pestañas, ventana con retrato, ficha, estadísticas iguales, franja y CONFIRMAR." +
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
