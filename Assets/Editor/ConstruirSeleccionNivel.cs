using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Crea Assets/Scenes/SeleccionNivel.unity desde cero ("ELIGE EL NIVEL", después de Intro o directo desde el menú), con
// el estilo de "Elige a tu repartidor" (ConstruirSeleccion) y del menú: el fondo pastel con piso de cuadros, el título en
// Press Start con el contorno grueso de la intro, las ventanas rosas de la selección, las pastillas rosas y la placa START.
//  - Arriba: placas del dinero, las motos compradas (x/4) y "Niveles n/3" con su barrita.
//  - Tres tarjetas (Residencial, Comercial, Lluvia): miniatura (pixel_town/generate_miniaturas_niveles.py, 240x135 a x2
//    con Point), NIVEL n, nombre, pedidos (3, 5 y 8), 3 estrellas, mejor satisfacción y la placa de estado (NUEVO /
//    COMPLETADO / BLOQUEADO; bloqueado con velo y candado). Clic en la tarjeta = SeleccionNivel.Elegir(i).
//  - Abajo: "Menú", "Jugar" (placa START) y "Reiniciar progreso", con su recuadro Sí / No (ConfirmarReinicio).
// Lo maneja SeleccionNivel. De paso deja la intro, la selección de personaje y el menú apuntando a esta escena, y el
// orden del build (OrdenEscenas). Idempotente: la escena se rehace entera en una escena aditiva; la abierta no se toca.
// Temp/ConstruirSeleccionNivel.pedido → Temp/ConstruirSeleccionNivel.resultado
public static class ConstruirSeleccionNivel
{
    public const string Escena = "SeleccionNivel";
    public const string RutaEscena = "Assets/Scenes/SeleccionNivel.unity";
    const string CarpetaUI = "Assets/Sprits/UI/";
    const string CarpetaNiveles = "Assets/Sprits/UI/Niveles/";
    const string RutaFondo = "Assets/Sprits/Intro/seleccion_fondo.png";
    const int Escala = 4;   // cada píxel del arte = 4 unidades del Canvas (1920x1080)

    public const string Titulo = "ELIGE EL NIVEL";
    public const string Ayuda = "A / D o clic para elegir  ·  Enter para jugar  ·  Esc para el menú";
    public const string PreguntaReinicio = "¿Reiniciar progreso?";
    public const string TextoReinicio = "Dinero $0, sin motos y sin estrellas.";
    static readonly string[] NumeroNivel = { "NIVEL 1", "NIVEL 2", "NIVEL 3" };

    static readonly Color32 Oscuro = FuenteProyecto.Oscuro;
    static readonly Color32 Crema = FuenteProyecto.Crema;
    static readonly Color32 Dorado = new Color32(0xFF, 0xD1, 0x66, 255);
    static readonly Color32 Velo = new Color32(0x14, 0x0E, 0x22, 170);

    static readonly (string nombre, Vector4 borde)[] Piezas =
    {
        ("seleccion_ventana", new Vector4(6, 6, 6, 6)), ("seleccion_barra", new Vector4(4, 4, 4, 4)), ("placa_start", new Vector4(7, 7, 7, 7)),
        ("marco_panel", new Vector4(10, 10, 10, 10)), ("estrella_llena", Vector4.zero), ("estrella_vacia", Vector4.zero),
        ("medidor_pista", Vector4.zero), ("medidor_relleno", Vector4.zero),
    };

    // Medidas (coordenadas del Canvas, centro de la pantalla = 0,0)
    const float YTitulo = 452, YBarra = 350, AltoPlaca = 18 * Escala;
    const float AnchoTarjeta = 540, AltoTarjeta = 600, YTarjetas = -40, SeparacionTarjetas = 580, BordeMarco = 14;
    const float AnchoMini = 480, AltoMini = 270;
    const float YAyuda = -385, YBotones = -470;

    [MenuItem("Delivery Brenn's/Construir selección de nivel")]
    static void ConstruirMenu()
    {
        try { Debug.Log(Construir(out _)); }
        catch (Exception e) { Debug.LogException(e); }
    }

    static Dictionary<string, Sprite> Sprites(out List<string> faltan)
    {
        var s = Piezas.ToDictionary(p => p.nombre, p => ConstruirMenuPrincipal.ImportarPixel(CarpetaUI + p.nombre + ".png", p.borde));
        var pildora = (TextureImporter)AssetImporter.GetAtPath(CarpetaUI + "boton_pill_normal.png");
        s["pildora"] = ConstruirMenuPrincipal.ImportarPixel(CarpetaUI + "boton_pill_normal.png", pildora.spriteBorder);
        s["pildora_presionada"] = ConstruirMenuPrincipal.ImportarPixel(CarpetaUI + "boton_pill_presionado.png", pildora.spriteBorder);
        s["fondo"] = ConstruirMenuPrincipal.ImportarPixel(RutaFondo, Vector4.zero);
        for (int i = 1; i <= 3; i++) s["mini" + i] = ConstruirMenuPrincipal.ImportarPixel($"{CarpetaNiveles}miniatura_nivel{i}.png", Vector4.zero);
        s["candado"] = ConstruirMenuPrincipal.ImportarPixel(CarpetaNiveles + "candado.png", Vector4.zero);
        s["placa_estado"] = ConstruirMenuPrincipal.ImportarPixel(CarpetaNiveles + "placa_estado.png", new Vector4(4, 4, 4, 4));
        faltan = s.Where(p => p.Value == null).Select(p => p.Key).ToList();
        return s;
    }

    static string Construir(out bool ok)
    {
        ok = false;
        var sprites = Sprites(out var faltan);
        if (faltan.Count > 0) return "NO SE CONSTRUYÓ la selección de nivel: faltan " + string.Join(", ", faltan) +
                                     " (corre pixel_town/generate_miniaturas_niveles.py y pixel_town/intro/generar_intro.py).";

        if (SceneManager.GetSceneByPath(RutaEscena).isLoaded)
        {
            var abierta = SceneManager.GetSceneByPath(RutaEscena);
            if (SceneManager.loadedSceneCount == 1) return "NO SE CONSTRUYÓ: SeleccionNivel es la única escena abierta. Abre otra y vuelve a ejecutarlo.";
            if (abierta.isDirty) return "NO SE CONSTRUYÓ: SeleccionNivel está abierta con cambios sin guardar.";
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
            else informe = "SELECCIÓN DE NIVEL CON ERRORES (no se guardó)\n" + informe;
        }
        finally
        {
            if (activa.IsValid() && activa.isLoaded) SceneManager.SetActiveScene(activa);
            EditorSceneManager.CloseScene(escena, true);
        }
        if (ok) informe += "\n" + ApuntarEscenas(out bool apuntadas);
        else return informe;
        return informe;
    }

    // ------------------------------------------------------------------ armado

    static RectTransform Hijo(string nombre, Transform padre, float x, float y, float ancho, float alto)
    {
        var rt = ConstruirMenuPrincipal.CrearHijo(nombre, padre);
        ConstruirMenuPrincipal.Centrar(rt, x, y, ancho, alto);
        return rt;
    }

    static Image Imagen(RectTransform rt, Sprite sprite, float referenciaPPU, bool rebanada = true, int escala = Escala)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.type = rebanada ? Image.Type.Sliced : Image.Type.Simple;
        img.pixelsPerUnitMultiplier = referenciaPPU / (sprite.pixelsPerUnit * escala);
        img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI Texto(RectTransform rt, string texto, float tam, Color color, bool titulo, TextAlignmentOptions alineacion, float minimo = 0)
    {
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (titulo) FuenteProyecto.AplicarTitulo(tmp);
        else FuenteProyecto.Aplicar(tmp);
        tmp.text = texto;
        tmp.fontSize = tam;
        tmp.color = color;
        tmp.alignment = alineacion;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
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

    // Placa rosa de información (no es botón): texto de pastilla #2A1F40
    static TextMeshProUGUI Placa(string nombre, Transform padre, float x, float ancho, Dictionary<string, Sprite> s, float ppu)
    {
        var rt = Hijo(nombre, padre, x, YBarra, ancho, AltoPlaca);
        Imagen(rt, s["pildora"], ppu);
        var rtTexto = ConstruirMenuPrincipal.CrearHijo("Texto", rt);
        Relleno(rtTexto);
        var tmp = rtTexto.gameObject.AddComponent<TextMeshProUGUI>();
        FuenteProyecto.EstiloPastilla(tmp);
        return tmp;
    }

    // Ocupa la píldora dejando libres las tapas y el canto de abajo (como ConstruirInterfaz)
    static void Relleno(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(6 * Escala, 3 * Escala);
        rt.offsetMax = new Vector2(-6 * Escala, -1 * Escala);
    }

    // Pastilla rosa (Boton...): Press Start #2A1F40, sin contorno
    static Button Pastilla(string nombre, Transform padre, string texto, float x, float y, float ancho, Dictionary<string, Sprite> s, float ppu)
    {
        var rt = Hijo(nombre, padre, x, y, ancho, AltoPlaca);
        var img = Imagen(rt, s["pildora"], ppu);
        img.raycastTarget = true;
        var boton = rt.gameObject.AddComponent<Button>();
        boton.targetGraphic = img;
        boton.transition = Selectable.Transition.SpriteSwap;
        var estados = boton.spriteState;
        estados.pressedSprite = s["pildora_presionada"];
        boton.spriteState = estados;
        var rtTexto = ConstruirMenuPrincipal.CrearHijo("Texto", rt);
        Relleno(rtTexto);
        var tmp = rtTexto.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;
        FuenteProyecto.EstiloBoton(tmp);
        return boton;
    }

    static void Armar(Dictionary<string, Sprite> s)
    {
        var raiz = ConstruirMenuPrincipal.CrearBase("Canvas", out float ppu);
        var sel = new GameObject("SeleccionNivel").AddComponent<SeleccionNivel>();

        var fondo = ConstruirMenuPrincipal.CrearHijo("Fondo", raiz);
        ConstruirMenuPrincipal.Estirar(fondo);
        Imagen(fondo, s["fondo"], ppu, false);

        // ---- Título: Press Start crema con el contorno y la sombra #2A1F40 gruesos de la intro ----
        var titulo = Texto(Hijo("Titulo", raiz, 0, YTitulo, 1800, 96), Titulo, 64, Crema, true, TextAlignmentOptions.Center, 44);
        FuenteProyecto.AplicarTitulo(titulo, FuenteProyecto.MaterialIntro());

        // ---- Arriba: dinero, motos y niveles ----
        var dinero = Placa("PlacaDinero", raiz, -560, 300, s, ppu);
        dinero.text = "$0";
        var motos = Placa("PlacaMotos", raiz, -170, 360, s, ppu);
        motos.text = "Motos 0/4";
        var niveles = Placa("PlacaNiveles", raiz, 350, 580, s, ppu);
        niveles.text = "Niveles 0/3";
        var rtNiveles = (RectTransform)niveles.transform;
        rtNiveles.offsetMax = new Vector2(-(6 * Escala + 200 + 2 * Escala), rtNiveles.offsetMax.y);   // la barra va a la derecha
        var barra = Hijo("Barra", rtNiveles.parent, 580 / 2f - 6 * Escala - 100, Escala, 200, 24);
        Imagen(barra, s["medidor_pista"], ppu, false, 2);
        var rtRelleno = ConstruirMenuPrincipal.CrearHijo("Relleno", barra);
        ConstruirMenuPrincipal.Estirar(rtRelleno);
        var relleno = Imagen(rtRelleno, s["medidor_relleno"], ppu, false, 2);
        relleno.type = Image.Type.Filled;
        relleno.fillMethod = Image.FillMethod.Horizontal;
        relleno.fillOrigin = (int)Image.OriginHorizontal.Left;
        relleno.fillAmount = 0f;
        relleno.color = Dorado;

        // ---- Tarjetas ----
        var tarjetas = Hijo("Tarjetas", raiz, 0, YTarjetas, 3 * SeparacionTarjetas, AltoTarjeta);
        var datos = new SeleccionNivel.Tarjeta[3];
        for (int i = 0; i < 3; i++)
            datos[i] = Tarjeta(i, tarjetas, sel, s, ppu);

        // ---- Ayuda y botones ----
        Texto(Hijo("Ayuda", raiz, 0, YAyuda, 1600, 40), Ayuda, 32, Oscuro, false, TextAlignmentOptions.Center, 28);
        var menu = Pastilla("BotonMenu", raiz, "Menú", -600, YBotones, 360, s, ppu);
        UnityEventTools.AddPersistentListener(menu.onClick, sel.Menu);
        var reiniciar = Pastilla("BotonReiniciar", raiz, "Reiniciar progreso", 620, YBotones, 620, s, ppu);
        UnityEventTools.AddPersistentListener(reiniciar.onClick, sel.PedirReinicio);

        // Jugar: la placa START de la intro (como CONFIRMAR en la selección de personaje)
        var jugar = Hijo("Jugar", raiz, 0, YBotones, 560, 104);
        var imgJugar = Imagen(jugar, s["placa_start"], ppu * Escala / 3f);   // la placa va a x3, como en la intro
        imgJugar.raycastTarget = true;
        var botonJugar = jugar.gameObject.AddComponent<Button>();
        botonJugar.targetGraphic = imgJugar;
        var colores = botonJugar.colors;
        colores.highlightedColor = new Color(1f, 0.92f, 0.96f);
        colores.pressedColor = new Color(0.8f, 0.75f, 0.85f);
        colores.disabledColor = new Color(0.55f, 0.52f, 0.6f, 1f);
        botonJugar.colors = colores;
        UnityEventTools.AddPersistentListener(botonJugar.onClick, sel.Jugar);
        Texto(Hijo("Texto", jugar, 0, 0, 480, 64), "JUGAR", 36, Crema, true, TextAlignmentOptions.Center, FuenteProyecto.MinimoTitulo);

        var confirmacion = Confirmacion(raiz, sel, s, ppu);

        // ---- SeleccionNivel ----
        var so = new SerializedObject(sel);
        var lista = so.FindProperty("tarjetas");
        lista.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            var t = lista.GetArrayElementAtIndex(i);
            var d = datos[i];
            t.FindPropertyRelative("raiz").objectReferenceValue = d.raiz;
            t.FindPropertyRelative("marcoElegida").objectReferenceValue = d.marcoElegida;
            t.FindPropertyRelative("miniatura").objectReferenceValue = d.miniatura;
            t.FindPropertyRelative("velo").objectReferenceValue = d.velo;
            t.FindPropertyRelative("candado").objectReferenceValue = d.candado;
            var est = t.FindPropertyRelative("estrellas");
            est.arraySize = 3;
            for (int e = 0; e < 3; e++) est.GetArrayElementAtIndex(e).objectReferenceValue = d.estrellas[e];
            t.FindPropertyRelative("mejor").objectReferenceValue = d.mejor;
            t.FindPropertyRelative("estado").objectReferenceValue = d.estado;
            t.FindPropertyRelative("placaEstado").objectReferenceValue = d.placaEstado;
        }
        so.FindProperty("estrellaLlena").objectReferenceValue = s["estrella_llena"];
        so.FindProperty("estrellaVacia").objectReferenceValue = s["estrella_vacia"];
        so.FindProperty("textoDinero").objectReferenceValue = dinero;
        so.FindProperty("textoMotos").objectReferenceValue = motos;
        so.FindProperty("textoNiveles").objectReferenceValue = niveles;
        so.FindProperty("barraNiveles").objectReferenceValue = relleno;
        so.FindProperty("botonJugar").objectReferenceValue = botonJugar;
        so.FindProperty("confirmacion").objectReferenceValue = confirmacion;
        so.FindProperty("escenaMenu").stringValue = "MenuPrincipal";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Una tarjeta: marco dorado (solo la elegida), ventana rosa, miniatura con velo y candado, NIVEL n, nombre, pedidos,
    // estrellas, mejor satisfacción y la placa de estado. Toda la tarjeta es el botón.
    static SeleccionNivel.Tarjeta Tarjeta(int i, Transform padre, SeleccionNivel sel, Dictionary<string, Sprite> s, float ppu)
    {
        var nivel = Progreso.Niveles[i];
        var d = new SeleccionNivel.Tarjeta();
        var rt = Hijo("Tarjeta" + (i + 1), padre, (i - 1) * SeparacionTarjetas, 0, AnchoTarjeta, AltoTarjeta);
        d.raiz = rt;

        var marco = Hijo("MarcoElegida", rt, 0, 0, AnchoTarjeta + 2 * BordeMarco, AltoTarjeta + 2 * BordeMarco);
        d.marcoElegida = Imagen(marco, s["seleccion_ventana"], ppu);
        d.marcoElegida.color = Dorado;
        d.marcoElegida.enabled = i == 0;

        var fondo = Hijo("Fondo", rt, 0, 0, AnchoTarjeta, AltoTarjeta);
        var imgFondo = Imagen(fondo, s["seleccion_ventana"], ppu);
        imgFondo.raycastTarget = true;
        var boton = rt.gameObject.AddComponent<Button>();
        boton.targetGraphic = imgFondo;
        boton.transition = Selectable.Transition.ColorTint;
        var colores = boton.colors;
        colores.highlightedColor = new Color(1f, 0.95f, 0.97f);
        colores.pressedColor = new Color(0.9f, 0.85f, 0.92f);
        boton.colors = colores;
        UnityEventTools.AddIntPersistentListener(boton.onClick, new UnityAction<int>(sel.Elegir), i);

        // filas, de arriba abajo (alto 600): miniatura 272..2, NIVEL n, nombre, pedidos, estrellas | mejor, estado
        float arriba = AltoTarjeta / 2 - 28;
        var mini = Hijo("Miniatura", rt, 0, arriba - AltoMini / 2, AnchoMini, AltoMini);
        d.miniatura = Imagen(mini, s["mini" + (i + 1)], ppu, false, 2);
        var velo = ConstruirMenuPrincipal.CrearHijo("Velo", mini);
        ConstruirMenuPrincipal.Estirar(velo);
        var imgVelo = velo.gameObject.AddComponent<Image>();
        imgVelo.color = Velo;
        imgVelo.raycastTarget = false;
        d.velo = velo.gameObject;
        var candado = Hijo("Candado", mini, 0, 0, 12 * 7, 14 * 7);
        Imagen(candado, s["candado"], ppu, false, 7);
        d.candado = candado.gameObject;
        velo.gameObject.SetActive(i > 0);
        candado.gameObject.SetActive(i > 0);

        float ancho = AnchoTarjeta - 64;
        Texto(Hijo("Numero", rt, 0, -20, ancho, 36), NumeroNivel[i], FuenteProyecto.MinimoTitulo, Oscuro, true, TextAlignmentOptions.Center);
        Texto(Hijo("Nombre", rt, 0, -64, ancho, 48), nivel.nombre, 44, Oscuro, false, TextAlignmentOptions.Center, FuenteProyecto.Minimo);
        Texto(Hijo("Pedidos", rt, 0, -110, ancho, 40), $"{nivel.pedidos} pedidos", FuenteProyecto.Minimo, Oscuro, false, TextAlignmentOptions.Center);

        // estrella de 15 px a x3
        var estrellas = Hijo("Estrellas", rt, -140, -164, 3 * 45 + 2 * 12, 56);
        d.estrellas = new Image[3];
        for (int e = 0; e < 3; e++)
        {
            var re = Hijo("Estrella" + (e + 1), estrellas, (e - 1) * 57, 0, 45, 45);
            d.estrellas[e] = Imagen(re, s["estrella_vacia"], ppu, false);
        }
        d.mejor = Texto(Hijo("Mejor", rt, 118, -164, 240, 48), "Mejor: --", FuenteProyecto.Minimo, Oscuro, false, TextAlignmentOptions.Center);

        var placa = Hijo("PlacaEstado", rt, 0, -236, 320, 56);
        d.placaEstado = Imagen(placa, s["placa_estado"], ppu);
        var rtEstado = Hijo("Texto", placa, 0, 2, 320 - 24, 40);
        d.estado = Texto(rtEstado, i == 0 ? SeleccionNivel.Nuevo : SeleccionNivel.Bloqueado, FuenteProyecto.MinimoTitulo, Oscuro, true, TextAlignmentOptions.Center);
        return d;
    }

    // Recuadro "¿Reiniciar progreso?" con Sí / No, sobre un velo que tapa los clics (empieza apagado)
    static GameObject Confirmacion(RectTransform raiz, SeleccionNivel sel, Dictionary<string, Sprite> s, float ppu)
    {
        var overlay = ConstruirMenuPrincipal.CrearHijo("ConfirmarReinicio", raiz);
        ConstruirMenuPrincipal.Estirar(overlay);
        var velo = overlay.gameObject.AddComponent<Image>();
        velo.color = new Color(0, 0, 0, 0.6f);
        velo.raycastTarget = true;
        var panel = Hijo("Panel", overlay, 0, 0, 940, 440);
        Imagen(panel, s["marco_panel"], ppu).raycastTarget = true;
        Texto(Hijo("Pregunta", panel, 0, 110, 820, 64), PreguntaReinicio, 40, Oscuro, true, TextAlignmentOptions.Center, FuenteProyecto.MinimoTitulo);
        Texto(Hijo("Detalle", panel, 0, 30, 820, 48), TextoReinicio, 40, Oscuro, false, TextAlignmentOptions.Center, 36);
        var si = Pastilla("BotonSi", panel, "Sí", -200, -100, 320, s, ppu);
        var no = Pastilla("BotonNo", panel, "No", 200, -100, 320, s, ppu);
        UnityEventTools.AddPersistentListener(si.onClick, sel.ConfirmarReinicio);
        UnityEventTools.AddPersistentListener(no.onClick, sel.CancelarReinicio);
        overlay.gameObject.SetActive(false);
        return overlay.gameObject;
    }

    // ------------------------------------------------------------------ verificación

    static Rect Caja(RectTransform rt)
    {
        // anclas al centro: posición relativa al padre, sumando los padres hasta el Canvas
        Vector2 c = rt.anchoredPosition;
        for (var p = rt.parent as RectTransform; p != null && p.GetComponent<Canvas>() == null; p = p.parent as RectTransform) c += p.anchoredPosition;
        return new Rect(c - rt.sizeDelta / 2, rt.sizeDelta);
    }

    static string Verificar(Scene escena, out bool ok)
    {
        var errores = new List<string>();
        var raices = escena.GetRootGameObjects();
        var canvas = raices.Select(g => g.GetComponent<Canvas>()).FirstOrDefault(c => c != null);
        var sel = raices.Select(g => g.GetComponent<SeleccionNivel>()).FirstOrDefault(c => c != null);
        if (canvas == null || sel == null)
        {
            ok = false;
            return "VERIFICACIÓN: falta el Canvas o SeleccionNivel.";
        }
        errores.AddRange(ConstruirMenuPrincipal.VerificarBase(raices, canvas));
        errores.AddRange(ConstruirMenuPrincipal.VerificarTextos(canvas.transform, "Boton"));
        var raiz = canvas.transform;
        foreach (var ruta in Requeridas)
            if (raiz.Find(ruta) == null) errores.Add($"Falta Canvas/{ruta}.");

        var titulo = raiz.Find("Titulo")?.GetComponent<TextMeshProUGUI>();
        if (titulo == null || titulo.text != Titulo || !FuenteProyecto.EsTitulo(titulo.font)) errores.Add($"El título no es \"{Titulo}\" en Press Start 2P.");
        else if (FuenteProyecto.Ancho(titulo, titulo.text) > titulo.rectTransform.rect.width) errores.Add("El título no cabe.");

        var conexiones = new List<(string ruta, string metodo, int arg)>
        {
            ("Jugar", "Jugar", -1), ("BotonMenu", "Menu", -1), ("BotonReiniciar", "PedirReinicio", -1),
            ("ConfirmarReinicio/Panel/BotonSi", "ConfirmarReinicio", -1), ("ConfirmarReinicio/Panel/BotonNo", "CancelarReinicio", -1),
        };
        for (int i = 0; i < 3; i++) conexiones.Add(($"Tarjetas/Tarjeta{i + 1}", "Elegir", i));
        foreach (var (ruta, metodo, arg) in conexiones)
        {
            var b = raiz.Find(ruta)?.GetComponent<Button>();
            if (b == null || b.onClick.GetPersistentEventCount() != 1 || b.onClick.GetPersistentTarget(0) != sel || b.onClick.GetPersistentMethodName(0) != metodo)
                errores.Add($"{ruta} no llama (solo) a SeleccionNivel.{metodo}.");
        }
        if (raiz.Find("Jugar")?.GetComponent<Image>()?.sprite?.name != "placa_start") errores.Add("Jugar no usa la placa START.");
        if (raiz.Find("ConfirmarReinicio")?.gameObject.activeSelf != false) errores.Add("ConfirmarReinicio debe empezar apagado.");
        if (raiz.Find("ConfirmarReinicio")?.GetSiblingIndex() != raiz.childCount - 1) errores.Add("ConfirmarReinicio debe quedar encima de todo.");

        var so = new SerializedObject(sel);
        var lista = so.FindProperty("tarjetas");
        if (lista.arraySize != 3) errores.Add("SeleccionNivel necesita 3 tarjetas.");
        for (int i = 0; i < lista.arraySize; i++)
        {
            var t = lista.GetArrayElementAtIndex(i);
            foreach (var campo in new[] { "raiz", "marcoElegida", "miniatura", "velo", "candado", "mejor", "estado", "placaEstado" })
                if (t.FindPropertyRelative(campo).objectReferenceValue == null) errores.Add($"Tarjeta {i + 1}: {campo} sin asignar.");
            var est = t.FindPropertyRelative("estrellas");
            if (est.arraySize != 3 || Enumerable.Range(0, est.arraySize).Any(e => est.GetArrayElementAtIndex(e).objectReferenceValue == null))
                errores.Add($"Tarjeta {i + 1}: faltan sus 3 estrellas.");
            var mini = t.FindPropertyRelative("miniatura").objectReferenceValue as Image;
            if (mini != null && (mini.sprite == null || mini.sprite.name != $"miniatura_nivel{i + 1}")) errores.Add($"Tarjeta {i + 1}: la miniatura no es miniatura_nivel{i + 1}.");
            var nombre = raiz.Find($"Tarjetas/Tarjeta{i + 1}/Nombre")?.GetComponent<TextMeshProUGUI>();
            if (nombre == null || nombre.text != Progreso.Niveles[i].nombre) errores.Add($"Tarjeta {i + 1}: el nombre no es {Progreso.Niveles[i].nombre}.");
            var pedidos = raiz.Find($"Tarjetas/Tarjeta{i + 1}/Pedidos")?.GetComponent<TextMeshProUGUI>();
            if (pedidos == null || pedidos.text != $"{Progreso.Niveles[i].pedidos} pedidos") errores.Add($"Tarjeta {i + 1}: no dice {Progreso.Niveles[i].pedidos} pedidos.");
            var mejorTxt = t.FindPropertyRelative("mejor").objectReferenceValue as TextMeshProUGUI;
            if (mejorTxt != null && FuenteProyecto.Ancho(mejorTxt, "Mejor: 100%") > mejorTxt.rectTransform.rect.width + 0.5f) errores.Add($"Tarjeta {i + 1}: \"Mejor: 100%\" no cabe.");
            // los tres estados caben en la placa a su tamaño mínimo
            var estado = t.FindPropertyRelative("estado").objectReferenceValue as TextMeshProUGUI;
            if (estado != null)
                foreach (var e in new[] { SeleccionNivel.Nuevo, SeleccionNivel.Completado, SeleccionNivel.Bloqueado })
                    if (FuenteProyecto.Ancho(estado, e) > estado.rectTransform.rect.width + 0.5f) errores.Add($"Tarjeta {i + 1}: \"{e}\" no cabe en su placa.");
        }
        foreach (var campo in new[] { "estrellaLlena", "estrellaVacia", "textoDinero", "textoMotos", "textoNiveles", "barraNiveles", "botonJugar", "confirmacion" })
            if (so.FindProperty(campo).objectReferenceValue == null) errores.Add($"SeleccionNivel: {campo} sin asignar.");
        foreach (var n in new[] { "miniatura_nivel1", "miniatura_nivel2", "miniatura_nivel3", "candado", "placa_estado" })
            if (AssetImporter.GetAtPath(CarpetaNiveles + n + ".png") is TextureImporter imp && (imp.filterMode != FilterMode.Point || imp.textureCompression != TextureImporterCompression.Uncompressed))
                errores.Add($"{n}: no está en Point sin compresión.");

        // nada se encima ni se sale de 1920x1080 (las tarjetas, con la elegida levantada, y su marco)
        var pantalla = new Rect(-960, -540, 1920, 1080);
        var cajas = new List<(string, Rect)>();
        foreach (var n in new[] { "Titulo", "PlacaDinero", "PlacaMotos", "PlacaNiveles", "Ayuda", "BotonMenu", "Jugar", "BotonReiniciar" })
            if (raiz.Find(n) is RectTransform r) cajas.Add((n, Caja(r)));
        for (int i = 0; i < 3; i++)
            if (raiz.Find($"Tarjetas/Tarjeta{i + 1}/MarcoElegida") is RectTransform m)
            {
                var c = Caja(m);
                cajas.Add(($"Tarjeta{i + 1}", new Rect(c.x, c.y, c.width, c.height + 12)));   // sube 12 al elegirla
            }
        for (int i = 0; i < cajas.Count; i++)
        {
            if (!pantalla.Contains(cajas[i].Item2.min) || !pantalla.Contains(cajas[i].Item2.max)) errores.Add($"{cajas[i].Item1} se sale de la pantalla.");
            for (int j = i + 1; j < cajas.Count; j++)
                if (cajas[i].Item2.Overlaps(cajas[j].Item2)) errores.Add($"{cajas[i].Item1} se encima con {cajas[j].Item1}.");
        }
        // dentro de cada tarjeta, nada se sale de la ventana ni se encima
        for (int i = 0; i < 3; i++)
        {
            var tarjeta = raiz.Find($"Tarjetas/Tarjeta{i + 1}") as RectTransform;
            if (tarjeta == null) continue;
            var interior = new Rect(-AnchoTarjeta / 2 + 24, -AltoTarjeta / 2 + 24, AnchoTarjeta - 48, AltoTarjeta - 48);
            var hijos = tarjeta.Cast<RectTransform>().Where(h => h.name != "MarcoElegida" && h.name != "Fondo")
                .Select(h => (h.name, caja: new Rect(h.anchoredPosition - h.sizeDelta / 2, h.sizeDelta))).ToList();
            for (int a = 0; a < hijos.Count; a++)
            {
                if (!interior.Contains(hijos[a].caja.min) || !interior.Contains(hijos[a].caja.max)) errores.Add($"Tarjeta{i + 1}/{hijos[a].name} se sale de la ventana.");
                for (int b = a + 1; b < hijos.Count; b++)
                    if (hijos[a].caja.Overlaps(hijos[b].caja)) errores.Add($"Tarjeta{i + 1}: {hijos[a].name} se encima con {hijos[b].name}.");
            }
        }

        ok = errores.Count == 0;
        return "VERIFICACIÓN: SeleccionNivel con título, dinero, motos, barra de niveles, 3 tarjetas (miniatura, estrellas, mejor, estado), " +
               "Menú / Jugar / Reiniciar progreso y el recuadro Sí / No." + (ok ? "\nSin errores." : "\nERRORES:\n- " + string.Join("\n- ", errores));
    }

    // Lo que no puede faltar (también lo exige RevisarCanvas en disco)
    public static readonly string[] Requeridas =
    {
        "Fondo", "Titulo", "PlacaDinero", "PlacaMotos", "PlacaNiveles", "PlacaNiveles/Barra/Relleno",
        "Tarjetas/Tarjeta1", "Tarjetas/Tarjeta2", "Tarjetas/Tarjeta3",
        "Tarjetas/Tarjeta1/Miniatura/Candado", "Tarjetas/Tarjeta2/Miniatura/Candado", "Tarjetas/Tarjeta3/Miniatura/Candado",
        "Tarjetas/Tarjeta1/Estrellas", "Tarjetas/Tarjeta1/PlacaEstado", "Tarjetas/Tarjeta1/Mejor",
        "BotonMenu", "Jugar", "BotonReiniciar",
        "ConfirmarReinicio/Panel/BotonSi", "ConfirmarReinicio/Panel/BotonNo",
    };

    // ------------------------------------------------------------------ las escenas que llevan aquí

    // Intro (al terminar), Seleccion (sin intro) y MenuPrincipal (sin Seleccion ni Intro) cargan SeleccionNivel
    static string ApuntarEscenas(out bool ok)
    {
        var cambios = new List<string>();
        ok = true;
        foreach (var (ruta, tipo, campo) in new[]
                 {
                     ("Assets/Scenes/Intro.unity", typeof(IntroHistoria), "escenaSiguiente"),
                     ("Assets/Scenes/Seleccion.unity", typeof(SeleccionPersonaje), "escenaJuego"),
                     ("Assets/Scenes/MenuPrincipal.unity", typeof(MenuPrincipal), "escenaJuego"),
                 })
        {
            if (!File.Exists(ruta)) { cambios.Add($"{Path.GetFileNameWithoutExtension(ruta)}: no existe"); ok = false; continue; }
            var abierta = SceneManager.GetSceneByPath(ruta);
            bool yaAbierta = abierta.isLoaded;
            if (yaAbierta && abierta.isDirty) { cambios.Add($"{abierta.name}: abierta con cambios sin guardar, no la toqué"); ok = false; continue; }
            var escena = yaAbierta ? abierta : EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);
            try
            {
                var comp = escena.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren(tipo, true)).FirstOrDefault();
                if (comp == null) { cambios.Add($"{escena.name}: sin {tipo.Name}"); ok = false; continue; }
                var so = new SerializedObject(comp);
                var p = so.FindProperty(campo);
                if (p.stringValue == Escena) { cambios.Add($"{escena.name}: ya va a {Escena}"); continue; }
                p.stringValue = Escena;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(escena);
                bool guardada = EditorSceneManager.SaveScene(escena);
                ok &= guardada;
                cambios.Add($"{escena.name}: {tipo.Name}.{campo} = {Escena}" + (guardada ? "" : " (NO SE GUARDÓ)"));
            }
            finally
            {
                if (!yaAbierta) EditorSceneManager.CloseScene(escena, true);
            }
        }
        return "Destinos: " + string.Join("; ", cambios);
    }

    [InitializeOnLoad]
    static class PedidoExterno
    {
        const string Pedido = "Temp/ConstruirSeleccionNivel.pedido";
        const string Resultado = "Temp/ConstruirSeleccionNivel.resultado";

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
