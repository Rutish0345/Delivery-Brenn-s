using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Nivel 2 (Comercial, atardecer, con motos): arma Assets/Scenes/Nivel2_Comercial.unity con el mismo constructor del
// nivel 1 (ConstruirMapa con el perfil Comercial, que lee pixel_town/unity/comercial de generate_comercial_grande.py).
// Se puede correr las veces que haga falta:
//   - si la escena no existe, la copia de SampleScene (cámara, Grid, repartidor, GameManager, Canvas con el HUD,
//     PanelFinal, PanelPausa, CajaHUD, UIManager, TiendaMotos, MenuJuego, EventSystem y LevelManager);
//   - reconstruye el mapa (suelo, Props, AutosCalle, BordeMapa, PuntosEntrega, Peatones, Clientes, Semaforos,
//     LotesSolidos y BloqueoMoto) y quita lo que sobró del nivel 1 en la raíz;
//   - pone el LevelManager del nivel 2, la luz de atardecer y SeleccionPedidos en PuntosEntrega;
//   - vuelve a armar el panel final, guarda, y en SampleScene deja escenaSiguiente = Nivel2_Comercial (con su panel
//     final otra vez); el build queda MenuPrincipal, Intro, SampleScene, Nivel2_Comercial.
// No toca una escena con cambios sin guardar.
public static class ConstruirMapaComercial
{
    const string Escena = "Assets/Scenes/Nivel2_Comercial.unity";
    const string EscenaNivel1 = "Assets/Scenes/SampleScene.unity";
    const string NombreEscena = "Nivel2_Comercial";
    const string EscenaNivel3 = "Assets/Scenes/Nivel3_Lluvia.unity";   // ConstruirMapaLluvia
    static string[] OrdenBuild => File.Exists(EscenaNivel3)
        ? new[] { "Assets/Scenes/MenuPrincipal.unity", "Assets/Scenes/Intro.unity", EscenaNivel1, Escena, EscenaNivel3 }
        : new[] { "Assets/Scenes/MenuPrincipal.unity", "Assets/Scenes/Intro.unity", EscenaNivel1, Escena };

    // Lo que la escena copia de SampleScene trae en la raíz; lo demás (props sueltos del nivel 1) se borra
    static readonly string[] RaicesConocidas =
    {
        "AutosCalle", "EventSystem", "MenuJuego", "repartidor", "BordeMapa", "Main Camera", "Global Light 2D", "Duena",
        "Canvas", "FlechaPedido", "BloqueoMoto", "LotesSolidos", "GameManager", "Semaforos", "Props", "PuntosEntrega",
        "EntregaProducto", "TiendaMotos", "Peatones", "Grid", "LevelManager", "UIManager", "Clientes", "Baches",
    };

    static readonly Color Atardecer = new Color32(0xFF, 0xB8, 0x80, 0xFF);
    const float IntensidadAtardecer = 0.95f;

    [MenuItem("Delivery Brenn's/Construir mapa Comercial")]
    static void ConstruirMenu()
    {
        try
        {
            string texto = Construir(out bool ok);
            EditorUtility.DisplayDialog("Construir mapa Comercial",
                (ok ? "Verificación sin errores.\n\n" : "LA VERIFICACIÓN ENCONTRÓ ERRORES (mira la consola).\n\n") + Recortar(texto), "OK");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Construir mapa Comercial", e.Message, "OK");
        }
    }

    static string Recortar(string t) => t.Length > 3000 ? t.Substring(0, 3000) + "…" : t;

    static string Construir(out bool ok)
    {
        var activa = SceneManager.GetActiveScene();
        // el propio nivel 2 se puede descartar: todo lo que tiene sale de este constructor
        if (activa.isDirty && activa.path != Escena)
            throw new Exception($"La escena {activa.name} tiene cambios sin guardar; guárdala o descártalos antes de construir el nivel 2.");
        string volverA = activa.path == Escena ? "" : activa.path;

        if (!File.Exists(Escena))
        {
            if (!AssetDatabase.CopyAsset(EscenaNivel1, Escena)) throw new Exception($"No pude copiar {EscenaNivel1} a {Escena}.");
            Debug.Log($"{Escena} creada como copia de {EscenaNivel1}.");
        }
        var escena = EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);

        var lineas = new List<string>();
        int sobrantes = 0;
        foreach (var raiz in escena.GetRootGameObjects().Where(g => !RaicesConocidas.Contains(g.name)).ToList())
        {
            Debug.Log($"Nivel 2: quito {raiz.name} de la raíz (venía del nivel 1).");
            UnityEngine.Object.DestroyImmediate(raiz);
            sobrantes++;
        }

        string mapa = ConstruirMapa.ConstruirConPerfil(ConstruirMapa.Perfil.Comercial, out bool mapaOk);
        lineas.Add(mapa);
        if (sobrantes > 0) lineas.Add($"Quitados {sobrantes} objetos sueltos del nivel 1 en la raíz.");

        lineas.Add(ConfigurarNivel(escena));
        lineas.Add(ConfigurarSeleccion());

        bool panel = ConstruirPanelFinal.Construir();
        string informePanel = ConstruirPanelFinal.Verificar(out bool panelOk);
        lineas.Add("Panel final del nivel 2: " + informePanel);
        EditorSceneManager.MarkSceneDirty(escena);
        if (!EditorSceneManager.SaveScene(escena)) throw new Exception($"No pude guardar {Escena}.");

        lineas.Add(ActualizarBuild());
        lineas.Add(ConectarNivel1(out bool nivel1Ok));

        if (!string.IsNullOrEmpty(volverA) && volverA != EscenaNivel1 && File.Exists(volverA))
            EditorSceneManager.OpenScene(volverA, OpenSceneMode.Single);
        else
            EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);

        ok = mapaOk && panel && panelOk && nivel1Ok;
        string texto = string.Join("\n", lineas);
        if (ok) Debug.Log("Nivel 2 listo.\n" + texto);
        else Debug.LogError("Nivel 2 con errores.\n" + texto);
        return texto;
    }

    static string ConfigurarNivel(Scene escena)
    {
        var nivel = ConstruirPanelFinal.BuscarOCrearLevelManager(escena);
        var so = new SerializedObject(nivel);
        so.FindProperty("nombreNivel").stringValue = "Comercial";
        so.FindProperty("pedidos").intValue = 5;
        so.FindProperty("velocidadReferencia").floatValue = 5.0f;
        so.FindProperty("motosHabilitadas").boolValue = true;
        so.FindProperty("elegirMotoAntes").boolValue = true;
        so.FindProperty("ambiente").enumValueIndex = (int)Ambiente.Atardecer;
        so.FindProperty("escenaSiguiente").stringValue = File.Exists(EscenaNivel3) ? "Nivel3_Lluvia" : "";
        so.FindProperty("escenaMenu").stringValue = "MenuPrincipal";
        so.ApplyModifiedPropertiesWithoutUndo();

        // la luz global ya con el atardecer en el editor (LevelManager la vuelve a poner al empezar)
        var luz = UnityEngine.Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(l => l.lightType == Light2D.LightType.Global);
        if (luz == null) throw new Exception("No hay Global Light 2D en la escena del nivel 2.");
        luz.color = Atardecer;
        luz.intensity = IntensidadAtardecer;
        EditorUtility.SetDirty(luz);
        return $"LevelManager: Comercial, 5 pedidos, velocidad de referencia 5.0, motos sí (elegir antes), atardecer " +
               $"#{ColorUtility.ToHtmlStringRGB(luz.color)} a {luz.intensity}, siguiente nivel \"{so.FindProperty("escenaSiguiente").stringValue}\", menú MenuPrincipal. " +
               "Faroles sin Light2D (no hace falta para el atardecer y así no pesa).";
    }

    static string ConfigurarSeleccion()
    {
        var datos = LeerSeleccion(out float yAvenida, out int cantidad, out float distancia);
        var puntos = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "PuntosEntrega")
                     ?? throw new Exception("No hay PuntosEntrega en el nivel 2.");
        var origen = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Props")?.transform.position ?? Vector3.zero;
        var sel = puntos.GetComponent<SeleccionPedidos>() ?? puntos.AddComponent<SeleccionPedidos>();
        var so = new SerializedObject(sel);
        so.FindProperty("cantidad").intValue = cantidad;
        so.FindProperty("distanciaMinima").floatValue = distancia;
        so.FindProperty("yAvenida").floatValue = yAvenida + origen.y;
        so.ApplyModifiedPropertiesWithoutUndo();

        var lista = puntos.GetComponentsInChildren<DeliveryPoint>(true);
        int norte = lista.Count(p => p.transform.position.y > yAvenida + origen.y);
        return $"SeleccionPedidos: {cantidad} de {lista.Length} puntos ({norte} al norte, {lista.Length - norte} al sur de la avenida), " +
               $"cada uno a {distancia} tiles o más del anterior. {datos}";
    }

    static string LeerSeleccion(out float yAvenida, out int cantidad, out float distancia)
    {
        string json = File.ReadAllText(Path.Combine(ConstruirMapa.Perfil.Comercial.carpetaDatos, ConstruirMapa.Perfil.Comercial.archivoMapa));
        string Valor(string clave)
        {
            int i = json.IndexOf("\"seleccion\"", StringComparison.Ordinal);
            int k = json.IndexOf($"\"{clave}\":", i, StringComparison.Ordinal) + clave.Length + 3;
            int fin = json.IndexOfAny(new[] { ',', '}' }, k);
            return json.Substring(k, fin - k).Trim();
        }
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        cantidad = int.Parse(Valor("cantidad"), inv);
        distancia = float.Parse(Valor("distancia_minima"), inv);
        yAvenida = float.Parse(Valor("y_avenida"), inv);
        return $"(línea norte/sur en y = {yAvenida})";
    }

    static string ActualizarBuild()
    {
        foreach (var ruta in OrdenBuild)
            if (!File.Exists(ruta)) throw new Exception($"Falta {ruta} para el build.");
        return OrdenEscenas.Poner(out _);   // MenuPrincipal, Seleccion, Intro, niveles 1-3; cualquier otra queda después
    }

    static string ConectarNivel1(out bool ok)
    {
        var escena = EditorSceneManager.OpenScene(EscenaNivel1, OpenSceneMode.Single);
        var nivel = ConstruirPanelFinal.BuscarLevelManager() ?? throw new Exception("SampleScene no tiene LevelManager.");
        var so = new SerializedObject(nivel);
        string antes = so.FindProperty("escenaSiguiente").stringValue;
        so.FindProperty("escenaSiguiente").stringValue = NombreEscena;
        so.ApplyModifiedPropertiesWithoutUndo();
        bool panel = ConstruirPanelFinal.Construir();
        string informe = ConstruirPanelFinal.Verificar(out bool panelOk);
        EditorSceneManager.MarkSceneDirty(escena);
        bool guardada = EditorSceneManager.SaveScene(escena);
        ok = panel && panelOk && guardada;
        return $"SampleScene: escenaSiguiente \"{antes}\" -> \"{NombreEscena}\"; panel final: {informe}" + (guardada ? "" : " (NO SE GUARDÓ)");
    }

    // Capturas para revisar: el mapa completo, una calle con tráfico y un negocio con su punto de entrega
    static string Capturas()
    {
        string carpeta = ConstruirMapa.Perfil.Comercial.carpetaDatos;
        var rutas = new List<string>
        {
            ConstruirMapa.CapturarArea(new Rect(0, 0, 128, 64), 32, Path.Combine(carpeta, "captura_unity.png")),
            ConstruirMapa.CapturarArea(new Rect(28, 20, 32, 18), 32, Path.Combine(carpeta, "captura_calle.png")),
            ConstruirMapa.CapturarArea(new Rect(0, 34, 20, 12), 32, Path.Combine(carpeta, "captura_negocio.png")),
            ConstruirMapa.CapturarArea(new Rect(48, 2, 36, 22), 32, Path.Combine(carpeta, "captura_plaza.png")),
        };
        return "Capturas: " + string.Join(", ", rutas);
    }

    // Si aparece Temp/ConstruirMapaComercial.pedido: construye todo, captura y deja Temp/ConstruirMapaComercial.resultado
    [InitializeOnLoad]
    static class PedidoExterno
    {
        const string Pedido = "Temp/ConstruirMapaComercial.pedido";
        const string Resultado = "Temp/ConstruirMapaComercial.resultado";

        static PedidoExterno() => EditorApplication.update += Revisar;

        static void Revisar()
        {
            if (!File.Exists(Pedido) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;   // nunca interrumpe una prueba en Play
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
                texto = (ok ? "OK\n" : "OK CON ERRORES DE VERIFICACION\n") + texto;
                if (SceneManager.GetActiveScene().path != Escena) EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
                texto += "\n" + Capturas();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                texto = "ERROR\n" + e;
            }
            finally
            {
                Application.logMessageReceived -= Anotar;
                EditorUtility.ClearProgressBar();
            }
            texto += "\n\nCONSOLA (advertencias y errores): " + (consola.Count == 0 ? "ninguno" : "\n" + string.Join("\n", consola));
            File.WriteAllText(Resultado, texto);
        }
    }
}
