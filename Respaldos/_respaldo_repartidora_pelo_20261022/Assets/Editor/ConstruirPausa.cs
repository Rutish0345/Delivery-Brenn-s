using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Arma el PanelPausa (con el mismo estilo que PanelFinal: marco_panel, píldoras rosas, fuente pixel y
// textos #2A1F40; el título con contorno crema suave y los botones sin contorno, a 36 como mínimo) y conecta BotonPausa y Seguir con MenuJuego.AlternarPausa, y Reiniciar con
// LevelManager.Reiniciar (que ya devuelve Time.timeScale a 1). "Menú" abre PausaConfirmar ("¿Salir al menú?", Sí/No):
// Sí → MenuJuego.ConfirmarMenu (detiene reloj y audio y llama a LevelManager.IrAlMenu, como el panel final),
// No → MenuJuego.CancelarMenu.
// Se puede ejecutar varias veces y en cualquier orden con ConstruirPanelFinal: solo trabaja con
// Canvas/BotonPausa y dentro de Canvas/PanelPausa, cuyos hijos llevan nombres propios (PausaTitulo,
// PausaSeguir, PausaReiniciar, PausaMenu, PausaConfirmar/PausaConfirmarSi...) para no confundirse con los del PanelFinal. MenuJuego y PanelPausa se
// reutilizan si ya existen (se vacía el panel y se rehace por dentro).
public static class ConstruirPausa
{
    const string NombreUndo = "Construir pausa";
    const string CarpetaUI = "Assets/Sprits/UI/";

    // Igual que en ConstruirInterfaz: cada píxel del arte ocupa 4 unidades del Canvas
    const int Escala = 4;

    static readonly Color32 Oscuro = new Color32(0x2A, 0x1F, 0x40, 255);

    // Seguir, Reiniciar y Menú en columna, y abajo la fila de sonido ("Música: sí/no" y "Efectos: sí/no", de
    // ConstruirSonido). PausaConfirmar ("¿Salir al menú?") va al final, encima de todo, y empieza apagado.
    const float AnchoPanel = 820, AltoPanel = 700;
    const float AnchoBoton = 400, AltoBoton = 18 * Escala;
    const float Aire = 40;                           // entre botones
    const float YTitulo = 240, YPrimerBoton = 110;
    const float AnchoBotonSonido = 360, SeparacionSonido = 20;
    const float AnchoSiNo = 240;
    static readonly string[] Hijos = { "PausaTitulo", "PausaSeguir", "PausaReiniciar", "PausaMenu", "PausaSonido", "PausaConfirmar" };
    static readonly string[] HijosConfirmar = { "PausaConfirmarTitulo", "PausaConfirmarTexto", "PausaConfirmarSi", "PausaConfirmarNo" };

    // Para ConstruirSonido: rehace la pausa de la escena abierta y la verifica
    internal static bool ConstruirYVerificar(out string informe)
    {
        bool hecho = Construir();
        informe = Verificar(out bool ok);
        return hecho && ok;
    }

    [MenuItem("Delivery Brenn's/Construir pausa")]
    static void ConstruirMenu()
    {
        try
        {
            if (Construir()) Debug.Log(Verificar(out _));
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    static bool Construir()
    {
        // ---- Primero se revisa todo; si falta algo no se toca la escena ----
        var marco = CargarSprite("marco_panel");
        var pildora = CargarSprite("boton_pill_normal");
        var pildoraPresionada = CargarSprite("boton_pill_presionado");
        if (marco == null || pildora == null || pildoraPresionada == null)
        {
            Debug.LogError("No se construyó la pausa porque faltan sprites.");
            return false;
        }

        var canvas = BuscarCanvas();
        if (canvas == null)
        {
            Debug.LogError("No encuentro el Canvas de la escena.");
            return false;
        }

        // el botón del HUD es hijo directo del Canvas (lo crea ConstruirInterfaz)
        var botonPausa = canvas.transform.Find("BotonPausa")?.GetComponent<Button>();
        if (botonPausa == null)
        {
            Debug.LogError("No encuentro Canvas/BotonPausa con Button. Ejecuta antes \"Delivery Brenn's > Construir interfaz\".");
            return false;
        }

        foreach (var campo in new[] { "panelPausa", "confirmacion" })
            if (typeof(MenuJuego).GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) == null)
            {
                Debug.LogError($"MenuJuego no tiene el campo \"{campo}\". No se construyó la pausa.");
                return false;
            }

        // Si hay otro MenuJuego puesto en un objeto con otro nombre, no se borra (puede tener
        // más cosas), pero se avisa: con dos, Escape pausaría y despausaría en el mismo frame
        foreach (var otro in Object.FindObjectsByType<MenuJuego>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .Where(m => m.name != "MenuJuego"))
            Debug.LogWarning($"Hay otro MenuJuego en \"{otro.name}\". Quítalo para que Escape no se procese dos veces.", otro);

        // la fuente pixel de toda la interfaz
        var fuente = FuenteProyecto.Fuente();
        if (fuente == null)
        {
            Debug.LogError("No se pudo crear la fuente pixel (Assets/Fonts/PixelifySans SDF.asset).");
            return false;
        }

        // Image.pixelsPerUnit = PPU del sprite / referencia del Canvas * multiplicador,
        // así que con este valor el borde 9-slice también sale a x4
        float multiplicador = canvas.referencePixelsPerUnit / (pildora.pixelsPerUnit * Escala);

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(NombreUndo);
        int grupo = Undo.GetCurrentGroup();
        var escena = canvas.gameObject.scene;

        try
        {
            // ---- MenuJuego (se reutiliza) ----
            var menu = escena.GetRootGameObjects().Where(g => g.name == "MenuJuego")
                .Select(g => g.GetComponent<MenuJuego>()).FirstOrDefault(m => m != null);
            if (menu == null)
            {
                var goMenu = new GameObject("MenuJuego");
                Undo.RegisterCreatedObjectUndo(goMenu, NombreUndo);
                SceneManager.MoveGameObjectToScene(goMenu, escena);
                menu = goMenu.AddComponent<MenuJuego>();
            }
            var nivel = ConstruirPanelFinal.BuscarOCrearLevelManager(escena);

            // ---- PanelPausa (se reutiliza y se vacía) ----
            var panel = canvas.transform.Find("PanelPausa") as RectTransform;
            if (panel == null) panel = CrearHijo("PanelPausa", canvas.transform);
            Undo.RecordObject(panel.gameObject, NombreUndo);
            panel.gameObject.SetActive(true);   // TextMeshPro no inicializa su material en un objeto inactivo
            for (int i = panel.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(panel.GetChild(i).gameObject);

            Undo.RecordObject(panel, NombreUndo);
            Centrar(panel, 0, 0, AnchoPanel, AltoPanel);
            if (!panel.TryGetComponent(out Image fondo)) fondo = Undo.AddComponent<Image>(panel.gameObject);
            Undo.RecordObject(fondo, NombreUndo);
            fondo.sprite = marco;
            fondo.type = Image.Type.Sliced;
            fondo.fillCenter = true;
            fondo.pixelsPerUnitMultiplier = multiplicador;
            fondo.color = Color.white;
            fondo.raycastTarget = true;   // que no se pueda clicar el juego detrás

            var rtTitulo = CrearHijo("PausaTitulo", panel);
            Centrar(rtTitulo, 0, YTitulo, 600, 110);
            CrearTexto(rtTitulo, "Pausa", 72, true);

            // uno debajo del otro, con 48 unidades de aire entre ellos
            const float paso = AltoBoton + Aire;
            var seguir = CrearBoton("PausaSeguir", panel, "Seguir", YPrimerBoton, pildora, pildoraPresionada, multiplicador);
            var reiniciar = CrearBoton("PausaReiniciar", panel, "Reiniciar", YPrimerBoton - paso, pildora, pildoraPresionada, multiplicador);
            var irMenu = CrearBoton("PausaMenu", panel, "Menú", YPrimerBoton - 2 * paso, pildora, pildoraPresionada, multiplicador);
            // abajo, la fila de sonido: dos píldoras lado a lado (con un poco más de aire)
            ConstruirSonido.Crear(panel, "PausaSonido", new Vector2(0, YPrimerBoton - 3 * paso - 10),
                                  new Vector2(AnchoBotonSonido, AltoBoton), SeparacionSonido, pildora, pildoraPresionada,
                                  multiplicador, new Vector2(6 * Escala, 3 * Escala), new Vector2(-6 * Escala, -1 * Escala), 40);

            // "¿Salir al menú?": tapa todo el panel con el mismo marco crema
            var confirmar = CrearHijo("PausaConfirmar", panel);
            confirmar.anchorMin = Vector2.zero;
            confirmar.anchorMax = Vector2.one;
            confirmar.offsetMin = confirmar.offsetMax = Vector2.zero;
            var fondoConfirmar = confirmar.gameObject.AddComponent<Image>();
            fondoConfirmar.sprite = marco;
            fondoConfirmar.type = Image.Type.Sliced;
            fondoConfirmar.pixelsPerUnitMultiplier = multiplicador;
            fondoConfirmar.raycastTarget = true;   // los botones de la pausa no se pueden pulsar detrás
            var rtPregunta = CrearHijo("PausaConfirmarTitulo", confirmar);
            Centrar(rtPregunta, 0, 130, 700, 100);
            FuenteProyecto.EstiloBoton(CrearTexto(rtPregunta, "¿Salir al menú?", 56, false), 56);
            var rtAviso = CrearHijo("PausaConfirmarTexto", confirmar);
            Centrar(rtAviso, 0, 30, 700, 70);
            var aviso = CrearTexto(rtAviso, "Perderás el avance de este nivel", 40, false);
            FuenteProyecto.EstiloPastilla(aviso, 40);   // texto de cuerpo, en la fuente pixel de lectura
            var si = CrearBoton("PausaConfirmarSi", confirmar, "Sí", -120, pildora, pildoraPresionada, multiplicador, -(AnchoSiNo / 2 + 20), AnchoSiNo);
            var no = CrearBoton("PausaConfirmarNo", confirmar, "No", -120, pildora, pildoraPresionada, multiplicador, AnchoSiNo / 2 + 20, AnchoSiNo);

            // por encima de todo el HUD y también del PanelFinal
            panel.SetAsLastSibling();
            confirmar.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);

            // ---- Conexiones ----
            var soMenu = new SerializedObject(menu);
            soMenu.FindProperty("panelPausa").objectReferenceValue = panel.gameObject;
            soMenu.FindProperty("confirmacion").objectReferenceValue = confirmar.gameObject;
            soMenu.ApplyModifiedProperties();

            Conectar(botonPausa, menu.AlternarPausa);
            Conectar(seguir, menu.AlternarPausa);
            Conectar(reiniciar, nivel.Reiniciar);
            Conectar(irMenu, menu.PedirMenu);
            Conectar(si, menu.ConfirmarMenu);
            Conectar(no, menu.CancelarMenu);
            Undo.CollapseUndoOperations(grupo);
        }
        catch
        {
            Undo.RevertAllDownToGroup(grupo);
            throw;
        }
        EditorSceneManager.MarkSceneDirty(escena);
        Debug.Log("Pausa lista: BotonPausa y Seguir → MenuJuego.AlternarPausa, Reiniciar → LevelManager.Reiniciar, Menú → PedirMenu (Sí/No). Guarda la escena.");
        return true;
    }

    // Deja el onClick solo con la llamada indicada (se borran los listeners que tuviera)
    static void Conectar(Button boton, UnityAction accion)
    {
        Undo.RecordObject(boton, NombreUndo);
        for (int i = boton.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(boton.onClick, i);
        UnityEventTools.AddPersistentListener(boton.onClick, accion);
        PrefabUtility.RecordPrefabInstancePropertyModifications(boton);
    }

    // Revisa lo que quedó en la escena; devuelve el informe y si todo está bien
    static string Verificar(out bool ok)
    {
        var errores = new List<string>();
        var canvas = BuscarCanvas();
        var panel = canvas != null ? canvas.transform.Find("PanelPausa") as RectTransform : null;
        var botonPausa = canvas != null ? canvas.transform.Find("BotonPausa")?.GetComponent<Button>() : null;
        var menus = Object.FindObjectsByType<MenuJuego>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var nivel = ConstruirPanelFinal.BuscarLevelManager();
        if (panel == null || botonPausa == null || menus.Length != 1 || nivel == null)
        {
            ok = false;
            return $"VERIFICACIÓN: PanelPausa {(panel != null ? "ok" : "FALTA")}, BotonPausa {(botonPausa != null ? "ok" : "FALTA")}, " +
                   $"{menus.Length} MenuJuego, LevelManager {(nivel != null ? "ok" : "FALTA")}.";
        }
        var menu = menus[0];

        var reales = Enumerable.Range(0, panel.childCount).Select(i => panel.GetChild(i).name).ToList();
        if (!reales.SequenceEqual(Hijos)) errores.Add($"PanelPausa tiene [{string.Join(", ", reales)}], se esperaba [{string.Join(", ", Hijos)}].");
        if (panel.gameObject.activeSelf) errores.Add("PanelPausa quedó activo.");
        if (panel.GetSiblingIndex() != canvas.transform.childCount - 1) errores.Add("PanelPausa no es el último hijo del Canvas.");
        if (new SerializedObject(menu).FindProperty("panelPausa").objectReferenceValue != panel.gameObject)
            errores.Add("MenuJuego: Panel Pausa no es PanelPausa.");
        var confirmar = panel.Find("PausaConfirmar");
        if (confirmar == null) errores.Add("Falta PanelPausa/PausaConfirmar.");
        else
        {
            var hijosConfirmar = Enumerable.Range(0, confirmar.childCount).Select(i => confirmar.GetChild(i).name).ToList();
            if (!hijosConfirmar.SequenceEqual(HijosConfirmar)) errores.Add($"PausaConfirmar tiene [{string.Join(", ", hijosConfirmar)}].");
            if (confirmar.gameObject.activeSelf) errores.Add("PausaConfirmar quedó activo.");
            if (new SerializedObject(menu).FindProperty("confirmacion").objectReferenceValue != confirmar.gameObject)
                errores.Add("MenuJuego: Confirmacion no es PausaConfirmar.");
        }
        // todo dentro del marco crema (con 24 unidades de borde) y sin encimarse
        var limite = new Rect(-AnchoPanel / 2 + 24, -AltoPanel / 2 + 24, AnchoPanel - 48, AltoPanel - 48);
        foreach (RectTransform hijo in panel)
        {
            if (hijo.name == "PausaConfirmar") continue;
            var r = new Rect(hijo.anchoredPosition - hijo.sizeDelta / 2, hijo.sizeDelta);
            if (r.xMin < limite.xMin || r.xMax > limite.xMax || r.yMin < limite.yMin || r.yMax > limite.yMax)
                errores.Add($"{hijo.name} se sale del marco ({r}).");
        }
        var filas = new[] { "PausaTitulo", "PausaSeguir", "PausaReiniciar", "PausaMenu", "PausaSonido" }.Select(n => panel.Find(n) as RectTransform).ToList();
        for (int i = 1; i < filas.Count; i++)
            if (filas[i] != null && filas[i - 1] != null
                && filas[i].anchoredPosition.y + filas[i].sizeDelta.y / 2 > filas[i - 1].anchoredPosition.y - filas[i - 1].sizeDelta.y / 2)
                errores.Add($"{filas[i].name} se encima con {filas[i - 1].name}.");

        foreach (var tmp in panel.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            var t = (Color32)tmp.color;
            var mat = tmp.fontSharedMaterial;
            bool esTitulo = tmp.name == "PausaTitulo";
            string quien = $"{tmp.transform.parent.name}/{tmp.name}";
            if (tmp.name == "PausaConfirmarTexto")   // texto de cuerpo: fuente pixel de lectura, que quepa
            {
                if (!FuenteProyecto.EsDelProyecto(tmp.font) || !FuenteProyecto.Cabe(tmp)) errores.Add($"{quien}: fuente o tamaño.");
                continue;
            }
            if (!FuenteProyecto.EsTitulo(tmp.font)) errores.Add($"{quien}: no usa Press Start 2P.");
            if (t.r != Oscuro.r || t.g != Oscuro.g || t.b != Oscuro.b || t.a != 255)
                errores.Add($"{quien}: el texto no es #2A1F40.");
            if (mat == null || mat.IsKeywordEnabled(ShaderUtilities.Keyword_Outline) != esTitulo)
                errores.Add($"{quien}: {(esTitulo ? "el título debe llevar el contorno crema" : "el botón no debe llevar contorno")}.");
            if (!esTitulo && (tmp.fontSizeMin < FuenteProyecto.MinimoDe(tmp) || !FuenteProyecto.Cabe(tmp)))
                errores.Add($"{quien}: el texto es menor que {FuenteProyecto.MinimoDe(tmp)} o no cabe en la pastilla.");
            if (tmp.text == tmp.text.ToUpperInvariant() || (tmp.fontStyle & FontStyles.UpperCase) != 0)
                errores.Add($"{quien}: \"{tmp.text}\" está en mayúsculas.");
        }

        var conexiones = new (Button boton, string nombre, Object destino, string metodo)[]
        {
            (botonPausa, "BotonPausa", menu, "AlternarPausa"),
            (panel.Find("PausaSeguir")?.GetComponent<Button>(), "PausaSeguir", menu, "AlternarPausa"),
            (panel.Find("PausaReiniciar")?.GetComponent<Button>(), "PausaReiniciar", nivel, "Reiniciar"),
            (panel.Find("PausaMenu")?.GetComponent<Button>(), "PausaMenu", menu, "PedirMenu"),
            (confirmar?.Find("PausaConfirmarSi")?.GetComponent<Button>(), "PausaConfirmarSi", menu, "ConfirmarMenu"),
            (confirmar?.Find("PausaConfirmarNo")?.GetComponent<Button>(), "PausaConfirmarNo", menu, "CancelarMenu"),
        };
        foreach (var (boton, nombre, destino, metodo) in conexiones)
        {
            if (boton == null || boton.onClick.GetPersistentEventCount() != 1 || boton.onClick.GetPersistentTarget(0) != destino
                || boton.onClick.GetPersistentMethodName(0) != metodo)
                errores.Add($"{nombre} no llama (solo) a {destino.GetType().Name}.{metodo}.");
            if (nombre != "BotonPausa" && boton != null && boton.GetComponent<Image>()?.sprite?.name != "boton_pill_normal")
                errores.Add($"{nombre}: no es la píldora rosa.");
        }

        errores.AddRange(ConstruirSonido.Verificar(panel.Find("PausaSonido"), "PanelPausa/PausaSonido"));
        ok = errores.Count == 0;
        return $"VERIFICACIÓN: PanelPausa {panel.sizeDelta.x:0}x{panel.sizeDelta.y:0}, hijos [{string.Join(", ", reales)}], " +
               $"activo = {panel.gameObject.activeSelf}." + (ok ? "\nSin errores." : "\nERRORES:\n- " + string.Join("\n- ", errores));
    }

    static Sprite CargarSprite(string nombre)
    {
        var ruta = CarpetaUI + nombre + ".png";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        if (sprite == null) Debug.LogError($"Falta el sprite {ruta} (o no está importado como Sprite).");
        return sprite;
    }

    // Se prefiere el Canvas donde ya vive TextoPedidos o PanelFinal
    static Canvas BuscarCanvas()
    {
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(c => c.isRootCanvas).ToList();
        return canvases.FirstOrDefault(c => c.GetComponentsInChildren<RectTransform>(true)
                   .Any(r => r.name == "TextoPedidos" || r.name == "PanelFinal"))
               ?? canvases.FirstOrDefault(c => c.name == "Canvas")
               ?? canvases.FirstOrDefault();
    }

    // Botón píldora rosa con su texto morado oscuro; al pulsar cambia al sprite hundido (igual que en el panel final)
    static Button CrearBoton(string nombre, Transform padre, string texto, float y,
                             Sprite normal, Sprite presionado, float multiplicador, float x = 0, float ancho = AnchoBoton)
    {
        var rt = CrearHijo(nombre, padre);
        Centrar(rt, x, y, ancho, AltoBoton);

        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = normal;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = multiplicador;
        img.color = Color.white;

        var boton = rt.gameObject.AddComponent<Button>();
        boton.targetGraphic = img;
        boton.transition = Selectable.Transition.SpriteSwap;
        var estados = boton.spriteState;
        estados.pressedSprite = presionado;
        boton.spriteState = estados;

        // el texto deja libres las tapas y el canto de abajo de la píldora
        var rtTexto = CrearHijo("Texto", rt);
        rtTexto.anchorMin = Vector2.zero;
        rtTexto.anchorMax = Vector2.one;
        rtTexto.pivot = new Vector2(0.5f, 0.5f);
        rtTexto.offsetMin = new Vector2(6 * Escala, 3 * Escala);
        rtTexto.offsetMax = new Vector2(-6 * Escala, -1 * Escala);
        FuenteProyecto.EstiloBoton(CrearTexto(rtTexto, texto, 40, false));
        return boton;
    }

    // Texto morado oscuro con la fuente pixel (el título con contorno crema), centrado, sin bloquear clics
    static TextMeshProUGUI CrearTexto(RectTransform rt, string texto, float tam, bool titulo)
    {
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        FuenteProyecto.Aplicar(tmp, titulo);
        tmp.text = texto;
        tmp.fontSize = titulo ? FuenteProyecto.Equivalente(tam) : tam;   // los títulos van en Press Start
        tmp.color = Oscuro;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    // Si aparece Temp/ConstruirPausa.pedido se construye la pausa, se verifica, se guarda la escena si
    // todo salió bien y el informe (con lo que salió en la consola) queda en Temp/ConstruirPausa.resultado
    [InitializeOnLoad]
    static class PedidoExterno
    {
        const string Pedido = "Temp/ConstruirPausa.pedido";
        const string Resultado = "Temp/ConstruirPausa.resultado";

        static PedidoExterno() => EditorApplication.update += Revisar;

        static void Revisar()
        {
            if (!File.Exists(Pedido) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;   // se construye al volver al modo edición
                return;
            }
            bool guardar = File.ReadAllText(Pedido).Trim() != "sin guardar";
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
                bool construido = Construir();
                string informe = Verificar(out bool ok);
                Debug.Log(informe);
                if (construido && ok && guardar)
                {
                    bool guardada = EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                    texto = (guardada ? "OK, escena guardada\n" : "OK PERO NO SE PUDO GUARDAR LA ESCENA\n") + informe;
                }
                else if (construido && ok) texto = "OK (sin guardar)\n" + informe;
                else texto = (construido ? "CONSTRUIDO CON ERRORES DE VERIFICACION (escena sin guardar)\n" : "NO SE CONSTRUYO\n") + informe;
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

    static RectTransform CrearHijo(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, NombreUndo);
        go.layer = padre.gameObject.layer;   // capa UI, igual que el Canvas
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
    }

    // Ancla y pivote al centro, con la posición y el tamaño indicados
    static void Centrar(RectTransform rt, float x, float y, float ancho, float alto)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(ancho, alto);
    }
}
