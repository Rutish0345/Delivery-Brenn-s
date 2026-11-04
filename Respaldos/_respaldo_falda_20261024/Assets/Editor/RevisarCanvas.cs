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

// El Canvas de las 3 escenas de juego (HUD con CajaHUD, PanelPausa, PanelFinal, FilaQuejas y PanelDespido) no lo arma ningún constructor de
// mapa: si se borra, la escena se queda sin interfaz. El 2026-10-21 se borró a mano de SampleScene (junto con una
// edición de setos) y se guardó así. Aquí:
//  - Faltantes(ruta): revisa el .unity en disco (sin abrirlo); lo usan PruebaIntro y PruebaNivel2 al empezar.
//  - Faltantes(escena): revisa una escena abierta; lo usa la verificación de ConstruirMapa.
//  - Al guardar una escena de juego sin Canvas, HUD o PanelFinal, pregunta antes y por defecto NO la guarda.
//  - Temp/RevisarCanvas.pedido → Temp/RevisarCanvas.resultado: Canvas de las 3 escenas y referencias en None /
//    Missing de UIManager, GameManager, LevelManager, TiendaMotos y MenuJuego.
//  - Prohibidos: Canvas/GuiaPedido (la píldora de flecha y metros) y FlechaPedido/flecha_borde (el chevron de la orilla).
//  - FaltantesSeleccionNivel(): la pantalla de niveles (ConstruirSeleccionNivel.Requeridas) en disco.
//  - FaltantesMenu(): el menú con la presentación y el PanelMenu (ConstruirMenuPrincipal.Requeridas) en disco.
public static class RevisarCanvas
{
    public static readonly string[] EscenasDeJuego =
    {
        "Assets/Scenes/SampleScene.unity", "Assets/Scenes/Nivel2_Comercial.unity", "Assets/Scenes/Nivel3_Lluvia.unity",
    };

    // Lo que no puede faltar: el HUD (ConstruirInterfaz; arriba a la izquierda la caja de ConstruirCajaHUD), y los paneles
    public static readonly string[] Rutas =
    {
        "Canvas",
        "Canvas/CajaHUD",
        "Canvas/CajaHUD/PlacaPedidos", "Canvas/CajaHUD/PlacaPedidos/TextoPedidos", "Canvas/PlacaDinero", "Canvas/PlacaDinero/TextoDinero",
        "Canvas/MedidorSatisfaccion", "Canvas/MedidorSatisfaccion/Pista/Relleno", "Canvas/MedidorSatisfaccion/Caritas",
        "Canvas/BotonPausa",
        "Canvas/CajaHUD/PlacaReloj", "Canvas/CajaHUD/PlacaReloj/IconoReloj", "Canvas/CajaHUD/PlacaReloj/TextoReloj",
        "Canvas/CajaHUD/FilaQuejas", "Canvas/CajaHUD/FilaQuejas/Queja1", "Canvas/CajaHUD/FilaQuejas/Queja2",
        "Canvas/CajaHUD/PlacaTurbo", "Canvas/CajaHUD/PlacaTurbo/IconoTurbo", "Canvas/CajaHUD/PlacaTurbo/BarraTurbo/BarraTurboRelleno", "Canvas/LineasVelocidad",
        "Canvas/PanelPausa", "Canvas/PanelPausa/PausaSonido/BotonMusica", "Canvas/PanelPausa/PausaSonido/BotonEfectos",
        "Canvas/PanelPausa/PausaMenu", "Canvas/PanelPausa/PausaConfirmar", "Canvas/PanelPausa/PausaConfirmar/PausaConfirmarSi",
        "Canvas/PanelPausa/PausaConfirmar/PausaConfirmarNo",
        "Canvas/PanelFinal", "Canvas/PanelFinal/Estrellas", "Canvas/PanelFinal/TextoResumen",
        "Canvas/PanelFinal/BotonJugarDeNuevo", "Canvas/PanelFinal/BotonMenu", "Canvas/PanelFinal/BotonSiguiente", "Canvas/PanelFinal/BotonNiveles",
        "Canvas/PanelFinal/TextoAviso",
        "Canvas/PanelDespido", "Canvas/PanelDespido/DespidoIlustracion", "Canvas/PanelDespido/DespidoTitular",
        "Canvas/PanelDespido/DespidoTexto", "Canvas/PanelDespido/DespidoBotonReintentar", "Canvas/PanelDespido/DespidoBotonMenu",
    };

    // Lo que ya no debe estar: la píldora de guía (flecha, ícono y metros) y el chevron de la orilla de la pantalla
    public static readonly string[] Prohibidas = { "Canvas/GuiaPedido", "FlechaPedido/flecha_borde" };

    static readonly string[] ConReferencias = { "UIManager", "GameManager", "LevelManager", "TiendaMotos", "MenuJuego", "RelojPedido", "BotonSonido", "PantallaDespido", "TurboHUD" };

    // ------------------------------------------------------------------ escena en disco (YAML)

    public static List<string> Faltantes(string rutaEscena)
    {
        var rutas = RutasEnDisco(rutaEscena);
        if (rutas == null) return new List<string> { $"no existe {rutaEscena}" };
        return Rutas.Where(r => !rutas.Contains(r)).Concat(Prohibidas.Where(rutas.Contains).Select(r => "sobra " + r)).ToList();
    }

    // La selección de nivel: su Canvas con las tarjetas, las placas de arriba, los botones y el recuadro Sí / No
    public static List<string> FaltantesSeleccionNivel()
    {
        var rutas = RutasEnDisco(ConstruirSeleccionNivel.RutaEscena);
        if (rutas == null) return new List<string> { $"no existe {ConstruirSeleccionNivel.RutaEscena}" };
        return ConstruirSeleccionNivel.Requeridas.Select(r => "Canvas/" + r).Prepend("SeleccionNivel").Where(r => !rutas.Contains(r)).ToList();
    }

    // El menú: la presentación y el PanelMenu con todo lo que se toca (ConstruirMenuPrincipal.Requeridas)
    public const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";
    public static List<string> FaltantesMenu()
    {
        var rutas = RutasEnDisco(RutaMenu);
        if (rutas == null) return new List<string> { $"no existe {RutaMenu}" };
        return ConstruirMenuPrincipal.Requeridas.Select(r => "Canvas/" + r).Where(r => !rutas.Contains(r)).ToList();
    }

    // Los créditos (ConstruirCreditos.Requeridas)
    public static List<string> FaltantesCreditos()
    {
        var rutas = RutasEnDisco(ConstruirCreditos.RutaEscena);
        if (rutas == null) return new List<string> { $"no existe {ConstruirCreditos.RutaEscena}" };
        return ConstruirCreditos.Requeridas.Select(r => "Canvas/" + r).Where(r => !rutas.Contains(r)).ToList();
    }

    // Todas las rutas (Raiz/Hijo/Nieto) de los objetos de un .unity, leídas del YAML sin abrir la escena
    static HashSet<string> RutasEnDisco(string rutaEscena)
    {
        if (!File.Exists(rutaEscena)) return null;
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
        return rutas;
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
        var niveles = FaltantesSeleccionNivel();
        if (niveles.Count > 0) errores.Add($"SeleccionNivel: falta {string.Join(", ", niveles)}");
        var menu = FaltantesMenu();
        if (menu.Count > 0) errores.Add($"MenuPrincipal: falta {string.Join(", ", menu)}");
        var creditos = FaltantesCreditos();
        if (creditos.Count > 0) errores.Add($"Creditos: falta {string.Join(", ", creditos)}");
        errores.AddRange(ConstruirSonido.VerificarPrefab());   // sin el prefab el juego va mudo
        return errores;
    }

    // ------------------------------------------------------------------ escena abierta

    public static List<string> Faltantes(Scene escena)
    {
        var raices = escena.GetRootGameObjects();
        var canvas = raices.FirstOrDefault(g => g.name == "Canvas");
        if (canvas == null) return Rutas.ToList();
        var faltan = Rutas.Skip(1).Where(r => canvas.transform.Find(r.Substring("Canvas/".Length)) == null).ToList();
        foreach (var r in Prohibidas)
        {
            int barra = r.IndexOf('/');
            var raiz = raices.FirstOrDefault(g => g.name == r.Substring(0, barra));
            if (raiz != null && raiz.transform.Find(r.Substring(barra + 1)) != null) faltan.Add("sobra " + r);
        }
        return faltan;
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
                // lo que sobra (GuiaPedido, flecha_borde) lo reportan las pruebas, pero no impide guardar
                var faltan = Faltantes(escena).Where(f => !f.StartsWith("sobra ")).ToList();
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
                var niveles = FaltantesSeleccionNivel();
                lineas.Add("SeleccionNivel (disco): " + (niveles.Count == 0 ? "completa" : "FALTA " + string.Join(", ", niveles)));
                var menu = FaltantesMenu();
                lineas.Add("MenuPrincipal (disco): " + (menu.Count == 0 ? "presentación y PanelMenu completos" : "FALTA " + string.Join(", ", menu)));
                var creditos = FaltantesCreditos();
                lineas.Add("Creditos (disco): " + (creditos.Count == 0 ? "completos" : "FALTA " + string.Join(", ", creditos)));
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
