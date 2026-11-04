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

// Crea la escena Assets/Scenes/Final.unity desde cero (como ConstruirIntro, con la misma vista: viñeta a pantalla
// completa, degradado, titular grande, texto de apoyo y la placa del indicador). Tiene tres finales armados con
// IntroHistoria bajo el objeto FinalHistoria, que enciende el que toca según el resultado del nivel 3:
//  - FinalEstrella (3 estrellas), 4 viñetas: LA TORMENTA PASÓ, LA DUEÑA TIENE NOTICIAS, ¡REPARTIDOR/A ESTRELLA! (gafete
//    dorado, confeti, remate y fanfarria) y GRACIAS POR JUGAR (jingle de victoria; el personaje con su moto equipada).
//  - FinalOficial (2 estrellas): igual, con ¡TE QUEDASTE CON EL PUESTO! (gafete rosa, sin confeti ni fanfarria).
//  - FinalAmable (1 estrella), 2 viñetas: ESTA VEZ NO FUE (jingle de derrota suave) y ¡EL PUESTO SIGUE DISPONIBLE!;
//    Espacio reintenta el nivel 3 (Nivel3_Lluvia) y Esc va al menú.
// En los tres, la última viñeta con Espacio va a escenaSiguiente y Esc (o "Menú (Esc)") al menú. Textos con género
// (titularRepartidora / textoRepartidora) y {nombre}. El arte sale de pixel_town/intro/generar_final.py.
// Al final: Build Profiles (OrdenEscenas: ... Nivel3_Lluvia 5, Final 6) y, en Nivel3_Lluvia, escenaSiguiente = Final
// con su panel final ("Continuar") y las quejas rehechos (ConstruirQuejas.AplicarEnEscena).
// Se puede ejecutar varias veces. Temp/ConstruirFinal.pedido → Temp/ConstruirFinal.resultado
public static class ConstruirFinal
{
    public const string RutaEscena = "Assets/Scenes/Final.unity";
    public const string NombreEscena = "Final";
    const string RutaNivel3 = "Assets/Scenes/Nivel3_Lluvia.unity";
    const string CarpetaVinetas = "Assets/Sprits/Intro/";
    const string CarpetaUI = "Assets/Sprits/UI/";
    const string Menu = "MenuPrincipal";
    const string Continuar = "ESPACIO PARA CONTINUAR";
    static readonly string[] Motos = { "scooter_naranja", "clasica_azul", "chopper_verde", "deportiva_roja" };   // Progreso.Motos

    internal class Vineta
    {
        public string archivo, archivoRepartidora = "", titular, titularRepartidora = "", texto = "", textoRepartidora = "";
        public string indicador = Continuar, musica = "", efecto = "";
        public bool bucle = true, porMoto;
        public float volumen = 1f;
    }

    internal class Final
    {
        public string objeto, siguiente;
        public int remate;                 // la viñeta del stinger (historia_remate), 0 = ninguna
        public string sonidoSalida = "";
        public Vineta[] vinetas;
    }

    static Vineta Tormenta() => new Vineta
    {
        archivo = "final_1", titular = "LA TORMENTA PASÓ",
        texto = "Después de la lluvia, el sol vuelve a brillar sobre el Salón Brenn's.",
    };

    static Vineta Noticias() => new Vineta
    {
        archivo = "final_2", titular = "LA DUEÑA TIENE NOTICIAS",
        texto = "Todas las clientas llamaron para felicitarte, {nombre}.",
    };

    static Vineta Gracias(bool jingle) => new Vineta
    {
        archivo = "final_4", archivoRepartidora = "final_4_repartidora", porMoto = true,
        titular = "GRACIAS POR JUGAR", indicador = "PRESIONA ESPACIO PARA VOLVER AL MENÚ",
        musica = jingle ? "musica_historia" : "", efecto = jingle ? "jingle_victoria" : "",   // después de la fanfarria
    };

    internal static readonly Final[] Finales =
    {
        new Final
        {
            objeto = "FinalEstrella", siguiente = Menu, remate = 3, vinetas = new[]
            {
                Tormenta(), Noticias(),
                new Vineta
                {
                    archivo = "final_3_estrella", archivoRepartidora = "final_3_estrella_repartidora",
                    titular = "¡REPARTIDOR ESTRELLA!", titularRepartidora = "¡REPARTIDORA ESTRELLA!",
                    texto = "Desde hoy eres el repartidor estrella del Salón Brenn's.",
                    textoRepartidora = "Desde hoy eres la repartidora estrella del Salón Brenn's.",
                    musica = "fanfarria_final", bucle = false,
                },
                Gracias(true),
            },
        },
        new Final
        {
            objeto = "FinalOficial", siguiente = Menu, remate = 3, vinetas = new[]
            {
                Tormenta(), Noticias(),
                new Vineta
                {
                    archivo = "final_3_oficial", archivoRepartidora = "final_3_oficial_repartidora",
                    titular = "¡TE QUEDASTE CON EL PUESTO!",
                    texto = "Desde hoy eres el repartidor oficial del Salón Brenn's.",
                    textoRepartidora = "Desde hoy eres la repartidora oficial del Salón Brenn's.",
                },
                Gracias(false),
            },
        },
        new Final
        {
            objeto = "FinalAmable", siguiente = "Nivel3_Lluvia", remate = 0, sonidoSalida = "moto_arranque", vinetas = new[]
            {
                new Vineta
                {
                    archivo = "final_amable_1", titular = "ESTA VEZ NO FUE",
                    texto = "Terminaste los pedidos, pero las clientas esperaban más rapidez.",
                    efecto = "jingle_derrota", volumen = 0.5f,
                },
                new Vineta
                {
                    archivo = "final_amable_2", archivoRepartidora = "final_amable_2_repartidora",
                    titular = "¡EL PUESTO SIGUE DISPONIBLE!", texto = "Inténtalo otra vez, {nombre}.",
                    indicador = "PRESIONA ESPACIO PARA REINTENTAR",
                },
            },
        },
    };

    const string NombrePrueba = "Maximiliano Ruiz";   // nombre largo para ver que todo cabe

    [MenuItem("Delivery Brenn's/Construir final de la historia")]
    static void ConstruirMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        try { Debug.Log(Construir(out _)); }
        catch (Exception e) { Debug.LogException(e); }
    }

    static Sprite Cargar(string archivo, List<string> faltan)
    {
        if (string.IsNullOrEmpty(archivo)) return null;
        var s = ConstruirMenuPrincipal.ImportarPixel($"{CarpetaVinetas}{archivo}.png", Vector4.zero);
        if (s == null) faltan.Add(archivo);
        return s;
    }

    static string Construir(out bool ok)
    {
        ok = false;
        var faltan = new List<string>();
        foreach (var f in Finales)
            foreach (var v in f.vinetas)
            {
                Cargar(v.archivo, faltan);
                Cargar(v.archivoRepartidora, faltan);
                if (v.porMoto)
                    foreach (var m in Motos) { Cargar($"{v.archivo}_{m}", faltan); Cargar($"{v.archivo}_{m}_repartidora", faltan); }
            }
        var degradado = ConstruirMenuPrincipal.ImportarPixel(CarpetaVinetas + "degradado_intro.png", Vector4.zero);
        var placa = ConstruirMenuPrincipal.ImportarPixel(CarpetaUI + "placa_start.png", new Vector4(7, 7, 7, 7));
        var pastel = ConstruirMenuPrincipal.ImportarPixel(CarpetaUI + "boton_pastel_normal.png", new Vector4(6, 6, 6, 4));
        var pastelPresionado = ConstruirMenuPrincipal.ImportarPixel(CarpetaUI + "boton_pastel_presionado.png", new Vector4(6, 6, 6, 4));
        if (degradado == null || placa == null || pastel == null || pastelPresionado == null) faltan.Add("degradado_intro / placa_start / pastillas pastel");
        if (FuenteProyecto.FuenteTitulo() == null) faltan.Add("la fuente Press Start");
        if (faltan.Count > 0) return "NO SE CONSTRUYÓ el final; faltan (corre pixel_town/intro/generar_final.py): " + string.Join(", ", faltan);

        var activa = SceneManager.GetActiveScene();
        if (activa.path == RutaEscena) return "NO SE CONSTRUYÓ: Final es la escena abierta. Abre otra y vuelve a ejecutarlo.";
        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        string informe;
        try
        {
            SceneManager.SetActiveScene(escena);
            Armar(degradado, placa, pastel, pastelPresionado);
            informe = Verificar(escena, out ok);
            if (ok)
            {
                bool guardada = EditorSceneManager.SaveScene(escena, RutaEscena);
                string build = "Build sin cambiar.";
                ok = guardada && ConstruirMenuPrincipal.PonerEscenasDelBuild(out build);
                informe = (guardada ? "Escena guardada en " + RutaEscena : "NO SE PUDO GUARDAR " + RutaEscena) + "\n" + build + "\n" + informe;
            }
            else informe = "FINAL CON ERRORES (no se guardó)\n" + informe;
        }
        finally
        {
            if (activa.IsValid() && activa.isLoaded) SceneManager.SetActiveScene(activa);
            EditorSceneManager.CloseScene(escena, true);
        }
        if (!ok) return informe;

        // el nivel 3: "Continuar" del panel final lleva aquí
        string volverA = SceneManager.GetActiveScene().path;
        try
        {
            string nivel3 = ConstruirQuejas.AplicarEnEscena(RutaNivel3, NombreEscena);
            ok = !nivel3.StartsWith("ERROR");
            informe += "\n" + nivel3;
        }
        finally
        {
            if (!string.IsNullOrEmpty(volverA) && File.Exists(volverA) && SceneManager.GetActiveScene().path != volverA)
                EditorSceneManager.OpenScene(volverA, OpenSceneMode.Single);
        }
        return informe;
    }

    static void Armar(Sprite degradado, Sprite placa, Sprite pastel, Sprite pastelPresionado)
    {
        var raiz = ConstruirMenuPrincipal.CrearBase("Canvas", out float referenciaPPU);
        var selector = new GameObject("FinalHistoria").AddComponent<FinalHistoria>();
        var primera = Finales[0].vinetas[0];
        var vista = ConstruirIntro.ArmarVista(raiz, referenciaPPU, Cargar(primera.archivo, new List<string>()), degradado, placa,
                                              primera.titular, primera.texto, primera.indicador);
        var boton = ConstruirIntro.BotonSaltar(raiz, referenciaPPU, pastel, pastelPresionado, "Menú (Esc)");
        UnityEventTools.AddPersistentListener(boton.onClick, selector.Saltar);

        var soSelector = new SerializedObject(selector);
        string[] campos = { "finalEstrella", "finalOficial", "finalAmable" };
        for (int k = 0; k < Finales.Length; k++)
        {
            var f = Finales[k];
            var go = new GameObject(f.objeto);
            go.transform.SetParent(selector.transform, false);
            var historia = go.AddComponent<IntroHistoria>();
            var so = new SerializedObject(historia);
            var lista = so.FindProperty("vinetas");
            lista.arraySize = f.vinetas.Length;
            for (int i = 0; i < f.vinetas.Length; i++)
            {
                var v = f.vinetas[i];
                var p = lista.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("imagen").objectReferenceValue = Cargar(v.archivo, new List<string>());
                p.FindPropertyRelative("imagenRepartidora").objectReferenceValue = Cargar(v.archivoRepartidora, new List<string>());
                foreach (var (campo, suf) in new[] { ("imagenPorMoto", ""), ("imagenPorMotoRepartidora", "_repartidora") })
                {
                    var arr = p.FindPropertyRelative(campo);
                    arr.arraySize = v.porMoto ? Motos.Length : 0;
                    for (int m = 0; m < arr.arraySize; m++)
                        arr.GetArrayElementAtIndex(m).objectReferenceValue = Cargar($"{v.archivo}_{Motos[m]}{suf}", new List<string>());
                }
                p.FindPropertyRelative("titular").stringValue = v.titular;
                p.FindPropertyRelative("titularRepartidora").stringValue = v.titularRepartidora;
                p.FindPropertyRelative("texto").stringValue = v.texto;
                p.FindPropertyRelative("textoRepartidora").stringValue = v.textoRepartidora;
                p.FindPropertyRelative("indicador").stringValue = v.indicador;
                p.FindPropertyRelative("musica").stringValue = v.musica;
                p.FindPropertyRelative("musicaEnBucle").boolValue = v.bucle;
                p.FindPropertyRelative("efecto").stringValue = v.efecto;
                p.FindPropertyRelative("volumenEfecto").floatValue = v.volumen;
            }
            ConstruirIntro.ConectarVista(so, vista);
            so.FindProperty("escenaSiguiente").stringValue = f.siguiente;
            so.FindProperty("escenaAlOmitir").stringValue = Menu;
            so.FindProperty("vinetaRemate").intValue = f.remate;
            so.FindProperty("sonidoSalida").stringValue = f.sonidoSalida;
            so.FindProperty("musica").stringValue = "musica_historia";
            so.ApplyModifiedPropertiesWithoutUndo();
            soSelector.FindProperty(campos[k]).objectReferenceValue = historia;
            go.SetActive(false);   // FinalHistoria enciende el que toca
        }
        soSelector.ApplyModifiedPropertiesWithoutUndo();
        vista.placa.SetActive(false);   // IntroHistoria la enciende al terminar de escribir
    }

    static string Verificar(Scene escena, out bool ok)
    {
        var errores = new List<string>();
        var raices = escena.GetRootGameObjects();
        var canvas = raices.Select(g => g.GetComponent<Canvas>()).FirstOrDefault(c => c != null);
        var selector = raices.Select(g => g.GetComponent<FinalHistoria>()).FirstOrDefault(m => m != null);
        if (canvas == null || selector == null)
        {
            ok = false;
            return "VERIFICACIÓN: falta el Canvas o FinalHistoria.";
        }
        errores.AddRange(ConstruirMenuPrincipal.VerificarBase(raices, canvas));
        errores.AddRange(ConstruirMenuPrincipal.VerificarTextos(canvas.transform, "Boton"));
        foreach (var t in canvas.GetComponentsInChildren<TMP_Text>(true).Where(t => !FuenteProyecto.EsTitulo(t.font)))
            errores.Add($"{t.transform.parent.name}/{t.name}: el final va todo en Press Start 2P.");

        var soSel = new SerializedObject(selector);
        string[] campos = { "finalEstrella", "finalOficial", "finalAmable" };
        TextMeshProUGUI titular = null, texto = null;
        for (int k = 0; k < Finales.Length; k++)
        {
            var f = Finales[k];
            var historia = soSel.FindProperty(campos[k]).objectReferenceValue as IntroHistoria;
            if (historia == null || historia.name != f.objeto || historia.gameObject.activeSelf || historia.transform.parent != selector.transform)
            {
                errores.Add($"FinalHistoria: {campos[k]} no es el {f.objeto} apagado bajo FinalHistoria.");
                continue;
            }
            var so = new SerializedObject(historia);
            titular = so.FindProperty("titular").objectReferenceValue as TextMeshProUGUI;
            texto = so.FindProperty("texto").objectReferenceValue as TextMeshProUGUI;
            var grupo = so.FindProperty("grupo").objectReferenceValue as CanvasGroup;
            if (grupo == null || titular == null || texto == null || so.FindProperty("imagen").objectReferenceValue == null
                || !(so.FindProperty("indicadorSiguiente").objectReferenceValue is GameObject placa) || placa.activeSelf)
                errores.Add($"{f.objeto}: la vista (grupo, imagen, titular, texto, placa apagada) no está completa.");
            if (so.FindProperty("escenaSiguiente").stringValue != f.siguiente) errores.Add($"{f.objeto}: Escena Siguiente no es {f.siguiente}.");
            if (so.FindProperty("escenaAlOmitir").stringValue != Menu) errores.Add($"{f.objeto}: Esc no va al menú.");
            if (so.FindProperty("vinetaRemate").intValue != f.remate) errores.Add($"{f.objeto}: el remate no va en la viñeta {f.remate}.");
            if (!Application.CanStreamedLevelBeLoaded(f.siguiente) && !EditorBuildSettings.scenes.Any(e => Path.GetFileNameWithoutExtension(e.path) == f.siguiente))
                errores.Add($"{f.objeto}: {f.siguiente} no está en el build.");
            var lista = so.FindProperty("vinetas");
            if (lista.arraySize != f.vinetas.Length) { errores.Add($"{f.objeto}: {lista.arraySize} viñetas, no {f.vinetas.Length}."); continue; }
            for (int i = 0; i < f.vinetas.Length; i++)
            {
                var v = f.vinetas[i];
                var p = lista.GetArrayElementAtIndex(i);
                var img = p.FindPropertyRelative("imagen").objectReferenceValue as Sprite;
                var deElla = p.FindPropertyRelative("imagenRepartidora").objectReferenceValue as Sprite;
                if (img == null || img.name != v.archivo || img.rect.size != new Vector2(480, 270)) errores.Add($"{f.objeto} {i + 1}: no tiene {v.archivo} de 480x270.");
                if ((deElla != null ? deElla.name : "") != v.archivoRepartidora) errores.Add($"{f.objeto} {i + 1}: la imagen de la repartidora no es \"{v.archivoRepartidora}\".");
                foreach (var (campo, suf) in new[] { ("imagenPorMoto", ""), ("imagenPorMotoRepartidora", "_repartidora") })
                {
                    var arr = p.FindPropertyRelative(campo);
                    if (arr.arraySize != (v.porMoto ? Motos.Length : 0)) errores.Add($"{f.objeto} {i + 1}: {campo} con {arr.arraySize} imágenes.");
                    for (int m = 0; m < arr.arraySize; m++)
                        if ((arr.GetArrayElementAtIndex(m).objectReferenceValue as Sprite)?.name != $"{v.archivo}_{Motos[m]}{suf}")
                            errores.Add($"{f.objeto} {i + 1}: {campo}[{m}] no es {v.archivo}_{Motos[m]}{suf}.");
                }
                if (p.FindPropertyRelative("titular").stringValue != v.titular || p.FindPropertyRelative("titularRepartidora").stringValue != v.titularRepartidora
                    || p.FindPropertyRelative("texto").stringValue != v.texto || p.FindPropertyRelative("textoRepartidora").stringValue != v.textoRepartidora
                    || p.FindPropertyRelative("indicador").stringValue != v.indicador)
                    errores.Add($"{f.objeto} {i + 1}: los textos no son los pedidos.");
                foreach (var pista in new[] { v.musica }.Where(x => x.Length > 0))
                    if (!ConstruirSonido.Musicas.Contains(pista)) errores.Add($"{f.objeto} {i + 1}: la música \"{pista}\" no está en el AudioManager.");
                foreach (var e in new[] { v.efecto }.Where(x => x.Length > 0))
                    if (!ConstruirSonido.Efectos.Contains(e)) errores.Add($"{f.objeto} {i + 1}: el efecto \"{e}\" no está en el AudioManager.");
            }
        }

        // cada titular (con un nombre largo) cabe en una línea a 40 y cada texto en su caja a 22, máximo 3 líneas
        if (titular != null && texto != null)
        {
            titular.enableAutoSizing = texto.enableAutoSizing = false;
            foreach (var v in Finales.SelectMany(f => f.vinetas))
                foreach (var (t, x) in new[] { (v.titular, v.texto), (v.titularRepartidora, v.textoRepartidora) }.Where(p => p.Item1.Length + p.Item2.Length > 0))
                {
                    string tt = (t.Length > 0 ? t : v.titular).Replace("{nombre}", NombrePrueba);
                    string xx = (x.Length > 0 ? x : v.texto).Replace("{nombre}", NombrePrueba);
                    titular.fontSize = titular.fontSizeMin;
                    if (titular.GetPreferredValues(tt).x > ConstruirIntro.AnchoTitular + 0.5f) errores.Add($"El titular no cabe: \"{tt}\".");
                    texto.fontSize = texto.fontSizeMin;
                    if (texto.GetPreferredValues(xx, ConstruirIntro.AnchoTexto, 0).y > ConstruirIntro.AltoTexto + 0.5f) errores.Add($"El texto no cabe: \"{xx}\".");
                }
            titular.enableAutoSizing = texto.enableAutoSizing = true;
            titular.fontSize = titular.fontSizeMax;
            texto.fontSize = texto.fontSizeMax;
        }

        var saltar = canvas.transform.Find("BotonSaltar")?.GetComponent<Button>();
        if (saltar == null || saltar.onClick.GetPersistentEventCount() != 1 || saltar.onClick.GetPersistentTarget(0) != selector
            || saltar.onClick.GetPersistentMethodName(0) != "Saltar")
            errores.Add("BotonSaltar no llama (solo) a FinalHistoria.Saltar.");
        errores.AddRange(ConstruirSonido.VerificarPrefab());

        ok = errores.Count == 0;
        return $"VERIFICACIÓN: Final con {string.Join(", ", Finales.Select(f => $"{f.objeto} ({f.vinetas.Length} viñetas → {f.siguiente})"))}." +
               (ok ? "\nSin errores." : "\nERRORES:\n- " + string.Join("\n- ", errores));
    }

    [InitializeOnLoad]
    static class PedidoExterno
    {
        const string Pedido = "Temp/ConstruirFinal.pedido";
        const string Resultado = "Temp/ConstruirFinal.resultado";

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
