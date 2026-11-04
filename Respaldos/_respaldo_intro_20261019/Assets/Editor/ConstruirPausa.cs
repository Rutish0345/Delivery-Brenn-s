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

// Arma el PanelPausa (con el mismo estilo que PanelFinal: marco_panel, píldoras rosas y textos #2A1F40
// con contorno crema) y conecta BotonPausa y Seguir con MenuJuego.AlternarPausa, y Reiniciar con
// LevelManager.Reiniciar (que ya devuelve Time.timeScale a 1).
// Se puede ejecutar varias veces y en cualquier orden con ConstruirPanelFinal: solo trabaja con
// Canvas/BotonPausa y dentro de Canvas/PanelPausa, cuyos hijos llevan nombres propios (PausaTitulo,
// PausaSeguir, PausaReiniciar) para no confundirse con los del PanelFinal. MenuJuego y PanelPausa se
// reutilizan si ya existen (se vacía el panel y se rehace por dentro).
public static class ConstruirPausa
{
    const string NombreUndo = "Construir pausa";
    const string CarpetaUI = "Assets/Sprits/UI/";

    // Igual que en ConstruirInterfaz: cada píxel del arte ocupa 4 unidades del Canvas
    const int Escala = 4;

    static readonly Color32 Crema = new Color32(0xFF, 0xF1, 0xD0, 255);
    static readonly Color32 Oscuro = new Color32(0x2A, 0x1F, 0x40, 255);

    const float AnchoPanel = 700, AltoPanel = 520;
    const float AnchoBoton = 400, AltoBoton = 18 * Escala;
    static readonly string[] Hijos = { "PausaTitulo", "PausaSeguir", "PausaReiniciar" };

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

        if (typeof(MenuJuego).GetField("panelPausa", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) == null)
        {
            Debug.LogError("MenuJuego no tiene el campo \"panelPausa\". No se construyó la pausa.");
            return false;
        }

        // Si hay otro MenuJuego puesto en un objeto con otro nombre, no se borra (puede tener
        // más cosas), pero se avisa: con dos, Escape pausaría y despausaría en el mismo frame
        foreach (var otro in Object.FindObjectsByType<MenuJuego>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .Where(m => m.name != "MenuJuego"))
            Debug.LogWarning($"Hay otro MenuJuego en \"{otro.name}\". Quítalo para que Escape no se procese dos veces.", otro);

        // la fuente del HUD, para que todo combine
        var textoPedidos = canvas.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.name == "TextoPedidos");
        var fuente = textoPedidos != null ? textoPedidos.font : TMP_Settings.defaultFontAsset;
        if (fuente == null)
        {
            Debug.LogError("No encuentro la fuente de TextoPedidos ni la fuente por defecto de TextMeshPro.");
            return false;
        }
        var contorno = ConstruirPanelFinal.MaterialContorno(fuente);   // el mismo contorno crema del panel final

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
            Centrar(rtTitulo, 0, 150, 600, 110);
            CrearTexto(rtTitulo, "Pausa", 72, fuente, contorno);

            // uno debajo del otro, con 48 unidades de aire entre ellos
            var seguir = CrearBoton("PausaSeguir", panel, "Seguir", 10, pildora, pildoraPresionada, multiplicador, fuente, contorno);
            var reiniciar = CrearBoton("PausaReiniciar", panel, "Reiniciar", 10 - AltoBoton - 48,
                                       pildora, pildoraPresionada, multiplicador, fuente, contorno);

            // por encima de todo el HUD y también del PanelFinal
            panel.SetAsLastSibling();
            panel.gameObject.SetActive(false);

            // ---- Conexiones ----
            var soMenu = new SerializedObject(menu);
            soMenu.FindProperty("panelPausa").objectReferenceValue = panel.gameObject;
            soMenu.ApplyModifiedProperties();

            Conectar(botonPausa, menu.AlternarPausa);
            Conectar(seguir, menu.AlternarPausa);
            Conectar(reiniciar, nivel.Reiniciar);
            Undo.CollapseUndoOperations(grupo);
        }
        catch
        {
            Undo.RevertAllDownToGroup(grupo);
            throw;
        }
        EditorSceneManager.MarkSceneDirty(escena);
        Debug.Log("Pausa lista: BotonPausa y Seguir → MenuJuego.AlternarPausa, Reiniciar → LevelManager.Reiniciar. Guarda la escena.");
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

        foreach (var tmp in panel.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            var t = (Color32)tmp.color;
            var mat = tmp.fontSharedMaterial;
            if (t.r != Oscuro.r || t.g != Oscuro.g || t.b != Oscuro.b || t.a != 255)
                errores.Add($"{tmp.transform.parent.name}/{tmp.name}: el texto no es #2A1F40.");
            if (mat == null || !mat.IsKeywordEnabled(ShaderUtilities.Keyword_Outline) || (Color32)mat.GetColor(ShaderUtilities.ID_OutlineColor) is var c
                && (c.r != Crema.r || c.g != Crema.g || c.b != Crema.b))
                errores.Add($"{tmp.name}: el texto no tiene el contorno crema.");
            if (tmp.text == tmp.text.ToUpperInvariant() || (tmp.fontStyle & FontStyles.UpperCase) != 0)
                errores.Add($"{tmp.transform.parent.name}/{tmp.name}: \"{tmp.text}\" está en mayúsculas.");
        }

        var conexiones = new (Button boton, string nombre, Object destino, string metodo)[]
        {
            (botonPausa, "BotonPausa", menu, "AlternarPausa"),
            (panel.Find("PausaSeguir")?.GetComponent<Button>(), "PausaSeguir", menu, "AlternarPausa"),
            (panel.Find("PausaReiniciar")?.GetComponent<Button>(), "PausaReiniciar", nivel, "Reiniciar"),
        };
        foreach (var (boton, nombre, destino, metodo) in conexiones)
        {
            if (boton == null || boton.onClick.GetPersistentEventCount() != 1 || boton.onClick.GetPersistentTarget(0) != destino
                || boton.onClick.GetPersistentMethodName(0) != metodo)
                errores.Add($"{nombre} no llama (solo) a {destino.GetType().Name}.{metodo}.");
            if (nombre != "BotonPausa" && boton != null && boton.GetComponent<Image>()?.sprite?.name != "boton_pill_normal")
                errores.Add($"{nombre}: no es la píldora rosa.");
        }

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
                             Sprite normal, Sprite presionado, float multiplicador, TMP_FontAsset fuente, Material contorno)
    {
        var rt = CrearHijo(nombre, padre);
        Centrar(rt, 0, y, AnchoBoton, AltoBoton);

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
        var tmp = CrearTexto(rtTexto, texto, 36, fuente, contorno);
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18;
        tmp.fontSizeMax = 36;
        return boton;
    }

    // Texto morado oscuro con el material de contorno crema, centrado, sin bloquear clics
    static TextMeshProUGUI CrearTexto(RectTransform rt, string texto, float tam, TMP_FontAsset fuente, Material contorno)
    {
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = fuente;
        tmp.fontSharedMaterial = contorno;
        tmp.text = texto;
        tmp.fontSize = tam;
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
