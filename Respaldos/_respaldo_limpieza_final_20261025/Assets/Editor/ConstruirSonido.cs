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
using Object = UnityEngine.Object;

// Audio del juego en el editor:
//  - Importa los WAV de Assets/Audio (los genera pixel_town/generate_audio.py): Musica en Streaming, SFX en
//    Decompress On Load, mono.
//  - Arma Assets/Resources/AudioManager.prefab con todos los clips y sus fuentes (MusicaA/B, Motor y Lluvia con la
//    casilla Loop). AudioManager se crea solo desde ese prefab al dar Play, así que ninguna escena depende de él.
//  - Controles "Música: sí/no" y "Efectos: sí/no" (BotonSonido): Crear() lo usan ConstruirPausa (PanelPausa/PausaSonido)
//    y ConstruirMenuPrincipal (Canvas/MenuSonido); Aplicar() los pone en las escenas que ya existen sin reconstruirlas.
// Menú "Delivery Brenn's > Audio" o Temp/ConstruirSonido.pedido → Temp/ConstruirSonido.resultado
public static class ConstruirSonido
{
    public const string CarpetaMusica = "Assets/Audio/Musica";
    public const string CarpetaSFX = "Assets/Audio/SFX";
    public const string RutaPrefab = "Assets/Resources/AudioManager.prefab";
    const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";
    const string RutaIntro = "Assets/Scenes/Intro.unity";
    const string Undo_ = "Controles de sonido";

    public static readonly string[] Musicas =
    {
        "musica_menu", "musica_historia", "musica_nivel1_residencial", "musica_nivel2_comercial", "musica_nivel3_lluvia",
        "fanfarria_final",
    };
    // volumen propio de algunas pistas, relativo al de la música (0.35 por defecto): la historia queda en 0.3
    public static readonly Dictionary<string, float> VolumenPropio = new Dictionary<string, float>
    {
        { "musica_historia", 0.3f / AudioManager.VolumenMusicaPorDefecto },
    };
    public static readonly string[] Efectos =
    {
        "ui_clic", "ui_hover", "pausa_abrir", "pausa_cerrar", "compra_moto", "equipar_moto", "pedido_nuevo", "entrega",
        "propina", "pedido_cancelado", "tick", "choque_fuerte", "choque_medio", "choque_leve", "bache", "claxon", "motor",
        "motor_grave", "lluvia", "trueno", "jingle_victoria", "jingle_derrota",
        // la intro (la historia)
        "historia_pop", "maquina_tic_1", "maquina_tic_2", "maquina_tic_3", "historia_clic", "historia_remate", "moto_arranque",
        // quejas del HUD (cada icono que se llena)
        "queja",
        // turbo Café exprés: al recogerlo, al acabarse el efecto y al reaparecer la taza
        "turbo_recoger", "turbo_fin", "turbo_aparece",
    };

    // ------------------------------------------------------------------ controles de sonido

    // Dos botones lado a lado, "BotonMusica" y "BotonEfectos", dentro de un contenedor 'nombre'
    public static RectTransform Crear(Transform padre, string nombre, Vector2 centro, Vector2 tamBoton, float separacion,
                                      Sprite normal, Sprite presionado, float multiplicador, Vector2 margenMin, Vector2 margenMax,
                                      float tamTexto)
    {
        var viejo = padre.Find(nombre);
        if (viejo != null) Undo.DestroyObjectImmediate(viejo.gameObject);

        var cont = Hijo(nombre, padre);
        cont.anchorMin = cont.anchorMax = cont.pivot = new Vector2(0.5f, 0.5f);
        cont.anchoredPosition = centro;
        cont.sizeDelta = new Vector2(tamBoton.x * 2 + separacion, tamBoton.y);

        foreach (var (canal, x) in new[] { (BotonSonido.Canal.Musica, -(tamBoton.x + separacion) / 2), (BotonSonido.Canal.Efectos, (tamBoton.x + separacion) / 2) })
        {
            var rt = Hijo(canal == BotonSonido.Canal.Musica ? "BotonMusica" : "BotonEfectos", cont);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0);
            rt.sizeDelta = tamBoton;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = normal;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = multiplicador;
            var boton = rt.gameObject.AddComponent<Button>();
            boton.targetGraphic = img;
            boton.transition = Selectable.Transition.SpriteSwap;
            var estados = boton.spriteState;
            estados.pressedSprite = presionado;
            boton.spriteState = estados;

            var rtTexto = Hijo("Texto", rt);
            rtTexto.anchorMin = Vector2.zero;
            rtTexto.anchorMax = Vector2.one;
            rtTexto.pivot = new Vector2(0.5f, 0.5f);
            rtTexto.offsetMin = margenMin;
            rtTexto.offsetMax = margenMax;
            var tmp = rtTexto.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = BotonSonido.Etiqueta(canal, true);
            FuenteProyecto.EstiloBoton(tmp, tamTexto);

            var bs = rt.gameObject.AddComponent<BotonSonido>();
            var so = new SerializedObject(bs);
            so.FindProperty("canal").enumValueIndex = (int)canal;
            so.FindProperty("texto").objectReferenceValue = tmp;
            so.FindProperty("fondo").objectReferenceValue = img;
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(boton.onClick, bs.Alternar);
        }
        return cont;
    }

    // Errores de un contenedor de controles (lo usan las verificaciones de pausa y menú)
    public static IEnumerable<string> Verificar(Transform cont, string quien)
    {
        if (cont == null) { yield return $"{quien}: faltan los controles de sonido."; yield break; }
        foreach (var canal in new[] { BotonSonido.Canal.Musica, BotonSonido.Canal.Efectos })
        {
            string n = canal == BotonSonido.Canal.Musica ? "BotonMusica" : "BotonEfectos";
            var b = cont.Find(n);
            var bs = b != null ? b.GetComponent<BotonSonido>() : null;
            var boton = b != null ? b.GetComponent<Button>() : null;
            if (bs == null || boton == null) { yield return $"{quien}/{n}: falta el botón con BotonSonido."; continue; }
            var so = new SerializedObject(bs);
            if (so.FindProperty("canal").enumValueIndex != (int)canal) yield return $"{quien}/{n}: canal equivocado.";
            var tmp = so.FindProperty("texto").objectReferenceValue as TMP_Text;
            bool icono = so.FindProperty("raya").objectReferenceValue != null;   // el menú: icono con raya, sin texto
            if (tmp == null && !icono || so.FindProperty("fondo").objectReferenceValue == null) yield return $"{quien}/{n}: referencias vacías.";
            if (boton.onClick.GetPersistentEventCount() != 1 || boton.onClick.GetPersistentTarget(0) != bs
                || boton.onClick.GetPersistentMethodName(0) != "Alternar")
                yield return $"{quien}/{n}: el clic no llama (solo) a BotonSonido.Alternar.";
            if (tmp != null)
            {
                float ancho = tmp.rectTransform.rect.width;
                foreach (bool activo in new[] { true, false })
                    if (ancho > 0 && FuenteProyecto.Ancho(tmp, BotonSonido.Etiqueta(canal, activo)) > ancho + 0.5f)
                        yield return $"{quien}/{n}: \"{BotonSonido.Etiqueta(canal, activo)}\" no cabe.";
            }
        }
    }

    static RectTransform Hijo(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, Undo_);
        go.layer = padre.gameObject.layer;
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
    }

    // ------------------------------------------------------------------ importación y prefab

    static string Importar(List<string> lineas)
    {
        int cambiados = 0;
        foreach (var (carpeta, musica) in new[] { (CarpetaMusica, true), (CarpetaSFX, false) })
        {
            foreach (var n in musica ? Musicas : Efectos)
            {
                string ruta = $"{carpeta}/{n}.wav";
                if (!File.Exists(ruta)) throw new Exception($"Falta {ruta} (corre pixel_town/generate_audio.py).");
                var imp = AssetImporter.GetAtPath(ruta) as AudioImporter;
                if (imp == null) { AssetDatabase.ImportAsset(ruta); imp = (AudioImporter)AssetImporter.GetAtPath(ruta); }
                var ajuste = imp.defaultSampleSettings;
                var nuevo = ajuste;
                // música: Streaming (Vorbis); efectos: Decompress On Load en PCM (cortos, y los loops sin costura)
                nuevo.loadType = musica ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                nuevo.compressionFormat = musica ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
                nuevo.quality = musica ? 0.7f : 1f;
                nuevo.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                nuevo.preloadAudioData = !musica;
                bool igual = ajuste.loadType == nuevo.loadType && ajuste.compressionFormat == nuevo.compressionFormat
                             && Mathf.Approximately(ajuste.quality, nuevo.quality) && ajuste.sampleRateSetting == nuevo.sampleRateSetting
                             && ajuste.preloadAudioData == nuevo.preloadAudioData && imp.forceToMono && imp.loadInBackground == musica;
                if (igual) continue;
                imp.defaultSampleSettings = nuevo;
                imp.forceToMono = true;
                imp.loadInBackground = musica;
                imp.SaveAndReimport();
                cambiados++;
            }
        }
        lineas.Add($"Importación: {Musicas.Length} músicas (Streaming, Vorbis) y {Efectos.Length} efectos (Decompress On Load, PCM); {cambiados} reimportados.");
        return "";
    }

    static void ArmarPrefab(List<string> lineas)
    {
        Directory.CreateDirectory("Assets/Resources");
        var go = new GameObject("AudioManager");
        try
        {
            var am = go.AddComponent<AudioManager>();
            var fuentes = new Dictionary<string, AudioSource>();
            foreach (var (n, loop) in new[] { ("MusicaA", true), ("MusicaB", true), ("Motor", true), ("Lluvia", true), ("Trueno", false) })
                fuentes[n] = am.Fuente(n, loop);
            fuentes["Motor"].volume = 0f;

            var so = new SerializedObject(am);
            Llenar(so.FindProperty("musicas"), CarpetaMusica, Musicas);
            Llenar(so.FindProperty("efectos"), CarpetaSFX, Efectos);
            so.FindProperty("musicaA").objectReferenceValue = fuentes["MusicaA"];
            so.FindProperty("musicaB").objectReferenceValue = fuentes["MusicaB"];
            so.FindProperty("motor").objectReferenceValue = fuentes["Motor"];
            so.FindProperty("lluvia").objectReferenceValue = fuentes["Lluvia"];
            so.FindProperty("trueno").objectReferenceValue = fuentes["Trueno"];
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(go, RutaPrefab, out bool ok);
            if (!ok) throw new Exception("No pude guardar " + RutaPrefab);
        }
        finally { Object.DestroyImmediate(go); }
        lineas.Add($"Prefab {RutaPrefab}: {Musicas.Length} músicas, {Efectos.Length} efectos, fuentes MusicaA/B, Motor y Lluvia con Loop.");
    }

    static void Llenar(SerializedProperty lista, string carpeta, string[] nombres)
    {
        lista.arraySize = nombres.Length;
        for (int i = 0; i < nombres.Length; i++)
        {
            var e = lista.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("nombre").stringValue = nombres[i];
            e.FindPropertyRelative("volumen").floatValue = VolumenPropio.TryGetValue(nombres[i], out float v) ? v : 1f;
            e.FindPropertyRelative("clip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{carpeta}/{nombres[i]}.wav")
                ?? throw new Exception($"No cargo {carpeta}/{nombres[i]}.wav como AudioClip.");
        }
    }

    // Lo que no puede faltar del prefab (también lo revisa RevisarCanvas)
    public static List<string> VerificarPrefab()
    {
        var errores = new List<string>();
        var am = AssetDatabase.LoadAssetAtPath<AudioManager>(RutaPrefab);
        if (am == null) { errores.Add($"Falta {RutaPrefab} (Delivery Brenn's > Audio)."); return errores; }
        var so = new SerializedObject(am);
        foreach (var (prop, nombres) in new[] { ("musicas", Musicas), ("efectos", Efectos) })
        {
            var l = so.FindProperty(prop);
            var hay = Enumerable.Range(0, l.arraySize).Select(l.GetArrayElementAtIndex)
                .Where(e => e.FindPropertyRelative("clip").objectReferenceValue != null)
                .Select(e => e.FindPropertyRelative("nombre").stringValue).ToHashSet();
            foreach (var n in nombres.Where(n => !hay.Contains(n))) errores.Add($"AudioManager: falta el clip \"{n}\".");
        }
        foreach (var (f, loop) in new[] { ("musicaA", true), ("musicaB", true), ("motor", true), ("lluvia", true), ("trueno", false) })
        {
            var s = so.FindProperty(f).objectReferenceValue as AudioSource;
            if (s == null) errores.Add($"AudioManager: fuente {f} vacía.");
            else if (s.loop != loop) errores.Add($"AudioManager: {f} con Loop = {s.loop}.");
        }
        return errores;
    }

    // ------------------------------------------------------------------ aplicar a las escenas que ya existen

    static string Aplicar(out bool ok)
    {
        var lineas = new List<string>();
        Importar(lineas);
        ArmarPrefab(lineas);
        string volverA = SceneManager.GetActiveScene().path;
        try
        {
            // la pausa se rehace entera con su constructor (que ahora incluye PausaSonido)
            foreach (var ruta in RevisarCanvas.EscenasDeJuego)
            {
                var escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
                bool hecho = ConstruirPausa.ConstruirYVerificar(out string informe);
                lineas.Add($"{escena.name}: pausa {(hecho ? "ok" : "CON ERRORES")}" + (hecho ? "" : "\n" + informe));
                if (hecho && !EditorSceneManager.SaveScene(escena)) lineas.Add($"ERROR: no pude guardar {ruta}.");
            }
            // el menú: solo se agrega la fila de controles (el resto queda como está)
            var menu = EditorSceneManager.OpenScene(RutaMenu, OpenSceneMode.Single);
            var canvas = menu.GetRootGameObjects().Select(g => g.GetComponent<Canvas>()).First(c => c != null);
            if (ConstruirMenuPrincipal.AsegurarOyente(menu)) lineas.Add("MenuPrincipal: se agregó el AudioListener a la cámara (no tenía).");
            var padreMenu = canvas.transform.Find("PanelMenu") ?? canvas.transform;   // los controles van en el PanelMenu
            ConstruirMenuPrincipal.CrearControlesSonido(padreMenu);
            var errMenu = Verificar(padreMenu.Find("MenuSonido"), "MenuSonido").ToList();
            errMenu.AddRange(ConstruirMenuPrincipal.VerificarTextos(padreMenu.Find("MenuSonido"), "Boton"));
            lineas.Add("MenuPrincipal: " + (errMenu.Count == 0 ? "controles de sonido ok" : "ERRORES: " + string.Join("; ", errMenu)));
            if (errMenu.Count == 0 && !EditorSceneManager.SaveScene(menu)) lineas.Add("ERROR: no pude guardar el menú.");
            // la intro: también le faltaba el AudioListener
            var intro = EditorSceneManager.OpenScene(RutaIntro, OpenSceneMode.Single);
            if (ConstruirMenuPrincipal.AsegurarOyente(intro))
            {
                lineas.Add("Intro: se agregó el AudioListener a la cámara (no tenía).");
                if (!EditorSceneManager.SaveScene(intro)) lineas.Add("ERROR: no pude guardar la Intro.");
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(volverA) && File.Exists(volverA) && SceneManager.GetActiveScene().path != volverA)
                EditorSceneManager.OpenScene(volverA, OpenSceneMode.Single);
        }
        var errores = RevisarCanvas.RevisarEscenasDeJuego();
        errores.AddRange(VerificarPrefab());
        ok = errores.Count == 0 && !lineas.Any(l => l.Contains("ERROR"));
        return string.Join("\n", lineas) + (errores.Count == 0 ? "\nVERIFICACIÓN: prefab completo y controles de sonido en las 3 pausas." : "\nERRORES:\n- " + string.Join("\n- ", errores));
    }

    [MenuItem("Delivery Brenn's/Audio (importar, prefab y controles)")]
    static void Menu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        try { EditorUtility.DisplayDialog("Audio", Aplicar(out _), "OK"); }
        catch (Exception e) { Debug.LogException(e); EditorUtility.DisplayDialog("Audio", e.Message, "OK"); }
    }

    [InitializeOnLoad]
    static class PedidoExterno
    {
        const string Pedido = "Temp/ConstruirSonido.pedido";
        const string Resultado = "Temp/ConstruirSonido.resultado";

        static PedidoExterno() => EditorApplication.update += Revisar;

        static void Revisar()
        {
            if (!File.Exists(Pedido) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
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
                if (SceneManager.GetActiveScene().isDirty) throw new Exception("La escena abierta tiene cambios sin guardar; no la cambio.");
                texto = Aplicar(out bool ok);
                texto = (ok ? "OK\n" : "OK CON ERRORES\n") + texto;
            }
            catch (Exception e) { Debug.LogException(e); texto = "ERROR\n" + e; }
            finally { Application.logMessageReceived -= Anotar; }
            texto += "\n\nCONSOLA (advertencias y errores): " + (consola.Count == 0 ? "ninguno" : "\n" + string.Join("\n", consola));
            File.WriteAllText(Resultado, texto);
        }
    }
}
