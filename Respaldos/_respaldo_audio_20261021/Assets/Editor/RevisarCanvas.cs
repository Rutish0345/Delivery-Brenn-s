using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// El Canvas de las 3 escenas de juego (HUD, GuiaPedido, PanelPausa y PanelFinal) no lo arma ningún constructor de
// mapa: si se borra, la escena se queda sin interfaz. El 2026-10-21 se borró a mano de SampleScene (junto con una
// edición de setos) y se guardó así. Aquí:
//  - Faltantes(ruta): revisa el .unity en disco (sin abrirlo); lo usan PruebaIntro y PruebaNivel2 al empezar.
//  - Faltantes(escena): revisa una escena abierta; lo usa la verificación de ConstruirMapa.
//  - Al guardar una escena de juego sin Canvas, HUD o PanelFinal, pregunta antes y por defecto NO la guarda.
//  - Temp/RevisarCanvas.pedido → Temp/RevisarCanvas.resultado: Canvas de las 3 escenas y referencias en None /
//    Missing de UIManager, GameManager, LevelManager, GuiaPedido, TiendaMotos y MenuJuego.
public static class RevisarCanvas
{
    public static readonly string[] EscenasDeJuego =
    {
        "Assets/Scenes/SampleScene.unity", "Assets/Scenes/Nivel2_Comercial.unity", "Assets/Scenes/Nivel3_Lluvia.unity",
    };

    // Lo que no puede faltar: el HUD son los hijos directos del Canvas (ConstruirInterfaz), y los paneles
    public static readonly string[] Rutas =
    {
        "Canvas",
        "Canvas/PlacaPedidos", "Canvas/PlacaPedidos/TextoPedidos", "Canvas/PlacaDinero", "Canvas/PlacaDinero/TextoDinero",
        "Canvas/MedidorSatisfaccion", "Canvas/MedidorSatisfaccion/Pista/Relleno", "Canvas/MedidorSatisfaccion/Caritas",
        "Canvas/BotonPausa", "Canvas/GuiaPedido",
        "Canvas/PlacaReloj", "Canvas/PlacaReloj/IconoReloj", "Canvas/PlacaReloj/TextoReloj",
        "Canvas/PanelPausa",
        "Canvas/PanelFinal", "Canvas/PanelFinal/Estrellas", "Canvas/PanelFinal/TextoResumen",
        "Canvas/PanelFinal/BotonJugarDeNuevo", "Canvas/PanelFinal/BotonMenu", "Canvas/PanelFinal/BotonSiguiente",
    };

    static readonly string[] ConReferencias = { "UIManager", "GameManager", "LevelManager", "GuiaPedido", "TiendaMotos", "MenuJuego", "RelojPedido" };

    // ------------------------------------------------------------------ escena en disco (YAML)

    public static List<string> Faltantes(string rutaEscena)
    {
        if (!File.Exists(rutaEscena)) return new List<string> { $"no existe {rutaEscena}" };
        var texto = File.ReadAllText(rutaEscena);
        var nombre = new Dictionary<string, string>();     // GameObject → nombre
        var goDe = new Dictionary<string, string>();       // Transform → GameObject
        var padre = new Dictionary<string, string>();      // Transform → Transform padre
        foreach (var doc in Regex.Split(texto, @"\n--- "))
        {
            var cab = Regex.Match(doc, @"^!u!(\d+) &(-?\d+)");
            if (!cab.Success) continue;
            string clase = cab.Groups[1].Value, id = cab.Groups[2].Value;
            if (clase == "1") nombre[id] = Regex.Match(doc, @"m_Name: (.*)").Groups[1].Value.Trim();
            else if (clase == "4" || clase == "224")
            {
                goDe[id] = Regex.Match(doc, @"m_GameObject: \{fileID: (-?\d+)").Groups[1].Value;
                padre[id] = Regex.Match(doc, @"m_Father: \{fileID: (-?\d+)").Groups[1].Value;
            }
        }
        var rutas = new HashSet<string>();
        foreach (var tr in goDe.Keys)
        {
            var partes = new List<string>();
            for (string t = tr; t != null && t != "0" && goDe.ContainsKey(t); t = padre.TryGetValue(t, out var p) ? p : null)
                partes.Insert(0, nombre.TryGetValue(goDe[t], out var n) ? n : "?");
            rutas.Add(string.Join("/", partes));
        }
        return Rutas.Where(r => !rutas.Contains(r)).ToList();
    }

    // Para las pruebas: un error por cada escena de juego a la que le falte algo
    public static List<string> RevisarEscenasDeJuego()
    {
        var errores = new List<string>();
        foreach (var ruta in EscenasDeJuego)
        {
            var faltan = Faltantes(ruta);
            if (faltan.Count > 0) errores.Add($"{Path.GetFileNameWithoutExtension(ruta)}: falta {string.Join(", ", faltan)}");
        }
        return errores;
    }

    // ------------------------------------------------------------------ escena abierta

    public static List<string> Faltantes(Scene escena)
    {
        var canvas = escena.GetRootGameObjects().FirstOrDefault(g => g.name == "Canvas");
        if (canvas == null) return Rutas.ToList();
        return Rutas.Skip(1).Where(r => canvas.transform.Find(r.Substring("Canvas/".Length)) == null).ToList();
    }

    static bool EsDeJuego(string ruta) => EscenasDeJuego.Contains(ruta);

    // No dejar guardar sin querer una escena de juego sin su interfaz
    class AlGuardar : AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] rutas)
        {
            var quedan = new List<string>();
            foreach (var ruta in rutas)
            {
                var escena = SceneManager.GetSceneByPath(ruta);
                if (!EsDeJuego(ruta) || !escena.IsValid() || !escena.isLoaded) { quedan.Add(ruta); continue; }
                var faltan = Faltantes(escena);
                if (faltan.Count == 0) { quedan.Add(ruta); continue; }
                string msg = $"{escena.name} no tiene: {string.Join(", ", faltan.Take(6))}{(faltan.Count > 6 ? "…" : "")}.\n\n" +
                             "Sin el Canvas el nivel se queda sin HUD, estrellas ni paso al siguiente nivel.";
                Debug.LogError($"RevisarCanvas: {msg}");
                if (!Application.isBatchMode &&
                    EditorUtility.DisplayDialog("Guardar escena sin interfaz", msg, "Guardar igual", "No guardar"))
                    quedan.Add(ruta);
            }
            return quedan.ToArray();
        }
    }

    // ------------------------------------------------------------------ referencias

    // Campos de referencia en None (o Missing) de los componentes con interfaz, en la escena abierta
    public static List<string> ReferenciasVacias(Scene escena)
    {
        var lista = new List<string>();
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var mb in raiz.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !ConReferencias.Contains(mb.GetType().Name)) continue;
                var so = new SerializedObject(mb);
                var p = so.GetIterator();
                while (p.NextVisible(true))
                {
                    if (p.propertyType != SerializedPropertyType.ObjectReference || p.name == "m_Script") continue;
                    if (p.objectReferenceValue != null) continue;
                    bool missing = p.objectReferenceInstanceIDValue != 0;
                    lista.Add($"{mb.GetType().Name}.{p.propertyPath}{(missing ? " (Missing)" : "")}");
                }
            }
        return lista;
    }

    [InitializeOnLoad]
    static class PedidoExterno
    {
        const string Pedido = "Temp/RevisarCanvas.pedido";
        const string Resultado = "Temp/RevisarCanvas.resultado";

        static PedidoExterno() => EditorApplication.update += Revisar;

        static void Revisar()
        {
            if (!File.Exists(Pedido) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Pedido);
            var lineas = new List<string>();
            string volverA = SceneManager.GetActiveScene().path;
            try
            {
                if (SceneManager.GetActiveScene().isDirty) throw new Exception("La escena abierta tiene cambios sin guardar; no la cambio.");
                var vacias = new Dictionary<string, List<string>>();
                foreach (var ruta in EscenasDeJuego)
                {
                    var faltan = Faltantes(ruta);
                    lineas.Add($"{Path.GetFileNameWithoutExtension(ruta)} (disco): " + (faltan.Count == 0 ? "Canvas completo" : "FALTA " + string.Join(", ", faltan)));
                    var escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
                    vacias[ruta] = ReferenciasVacias(escena);
                }
                // lo que está en None en el nivel 1 y asignado en el 2 es lo sospechoso
                var n2 = new HashSet<string>(vacias[EscenasDeJuego[1]]);
                foreach (var ruta in EscenasDeJuego)
                {
                    var v = vacias[ruta];
                    lineas.Add($"{Path.GetFileNameWithoutExtension(ruta)}: {v.Count} referencias en None/Missing" + (v.Count == 0 ? "" : ": " + string.Join(", ", v)));
                }
                var raras = vacias[EscenasDeJuego[0]].Where(r => !n2.Contains(r) || r.Contains("Missing")).ToList();
                lineas.Add(raras.Count == 0 ? "SampleScene: ninguna referencia vacía que en el nivel 2 sí esté asignada." : "SOSPECHOSAS en SampleScene: " + string.Join(", ", raras));
                lineas.Insert(0, raras.Count == 0 && RevisarEscenasDeJuego().Count == 0 ? "OK" : "PROBLEMAS");
            }
            catch (Exception e) { lineas.Insert(0, "ERROR\n" + e); }
            finally
            {
                if (!string.IsNullOrEmpty(volverA) && SceneManager.GetActiveScene().path != volverA)
                    EditorSceneManager.OpenScene(volverA, OpenSceneMode.Single);
            }
            File.WriteAllText(Resultado, string.Join("\n", lineas));
        }
    }
}
