using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Pone al día los 3 niveles sin reconstruir el mapa, en una sola pasada por escena: el panel final (con "Niveles"),
// la caja del HUD (sin GuiaPedido) y la flecha guía (sin el chevron de la orilla). Cada escena se guarda solo si
// pasan las tres verificaciones y RevisarCanvas no echa de menos nada; si no, se deja sin guardar y se dice por qué.
// No corre con cambios sin guardar (salvo que el pedido diga "descartar" y la escena abierta sea uno de los 3 niveles:
// entonces se vuelve a abrir desde el disco, p. ej. tras una pasada que no guardó). Vuelve a abrir la escena que estaba abierta. Después corre ConstruirSeleccionNivel.
// Temp/ActualizarNiveles.pedido → Temp/ActualizarNiveles.resultado
[InitializeOnLoad]
static class ActualizarNiveles
{
    const string Pedido = "Temp/ActualizarNiveles.pedido";
    const string Resultado = "Temp/ActualizarNiveles.resultado";

    static ActualizarNiveles() => EditorApplication.update += Revisar;

    [MenuItem("Delivery Brenn's/Actualizar los 3 niveles (panel final, HUD y flecha)")]
    static void Menu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorUtility.DisplayDialog("Actualizar niveles", Aplicar(false, out _), "OK");
    }

    static string Aplicar(bool descartar, out bool ok)
    {
        ok = true;
        var lineas = new List<string>();
        string abierta = SceneManager.GetActiveScene().path;
        try
        {
            if (SceneManager.GetActiveScene().isDirty && !(descartar && RevisarCanvas.EscenasDeJuego.Contains(abierta))) throw new Exception($"{abierta} tiene cambios sin guardar; no la cambio.");
            foreach (var ruta in RevisarCanvas.EscenasDeJuego)
            {
                var escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
                var errores = new List<string>();

                if (!ConstruirPanelFinal.Construir()) errores.Add("PanelFinal: no se construyó (ver consola).");
                string panel = ConstruirPanelFinal.Verificar(out bool panelOk);
                if (!panelOk) errores.Add(panel);

                string caja = ConstruirCajaHUD.Crear();
                errores.AddRange(ConstruirCajaHUD.Verificar());

                ConstruirMarcadorPedido.CrearFlecha();
                string flecha = ConstruirMarcadorPedido.RevisarFlechaYEntrega(errores);

                errores.AddRange(RevisarCanvas.Faltantes(escena).Select(f => "RevisarCanvas: " + f));
                if (errores.Count > 0)
                {
                    ok = false;
                    lineas.Add($"ERROR en {escena.name} (no se guardó):\n- " + string.Join("\n- ", errores));
                    continue;
                }
                EditorSceneManager.MarkSceneDirty(escena);
                bool guardada = EditorSceneManager.SaveScene(escena);
                ok &= guardada;
                lineas.Add($"{escena.name}: {(guardada ? "guardada" : "NO SE PUDO GUARDAR")}. {caja} {flecha}");
            }
        }
        catch (Exception e)
        {
            ok = false;
            lineas.Add("ERROR " + e);
        }
        finally
        {
            if (abierta.Length > 0 && File.Exists(abierta) && SceneManager.GetActiveScene().path != abierta && !SceneManager.GetActiveScene().isDirty)
                EditorSceneManager.OpenScene(abierta, OpenSceneMode.Single);
        }
        return string.Join("\n", lineas);
    }

    static void Revisar()
    {
        if (!File.Exists(Pedido) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        bool descartar = File.ReadAllText(Pedido).Trim() == "descartar";
        File.Delete(Pedido);
        var consola = new List<string>();
        void Anotar(string msg, string pila, LogType tipo)
        {
            if (tipo != LogType.Log) consola.Add($"[{tipo}] {msg}" + (tipo == LogType.Exception ? "\n" + pila : ""));
        }
        Application.logMessageReceived += Anotar;
        string texto;
        try { texto = Aplicar(descartar, out bool ok) is var r && ok ? "OK\n" + r : "CON ERRORES\n" + r; }
        catch (Exception e) { texto = "ERROR\n" + e; }
        finally { Application.logMessageReceived -= Anotar; }
        texto += "\n\nCONSOLA (advertencias y errores): " + (consola.Count == 0 ? "ninguno" : "\n" + string.Join("\n", consola));
        File.WriteAllText(Resultado, texto);
    }
}
