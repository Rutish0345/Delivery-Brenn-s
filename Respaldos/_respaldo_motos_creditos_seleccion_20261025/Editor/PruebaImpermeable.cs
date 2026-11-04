using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Prueba en Play del atuendo de LLUVIA (ConstruirImpermeable) en Nivel3_Lluvia, de noche y con lluvia:
// A) Antes del Play: solo Nivel3_Lluvia tiene atuendoLluvia encendido (SampleScene y Nivel2_Comercial no).
// B) Con el repartidor y luego con la repartidora (Progreso.Genero): a pie, Animator con su LluviaOverride y caminando en
//    las 4 direcciones con cuadros de su hoja _lluvia (de la fila de esa vista, más de uno: la animación avanza); en las
//    4 motos, rodando en las 4 direcciones con los cuadros <id>[_chica]_lluvia_<vista>_<n>.
// Capturas del mundo (x3, centradas en el personaje): a pie de frente, espalda y lado, y cada moto de lado (y la
// clásica de frente y de espaldas). La consola debe quedar limpia. Al final el género guardado queda como estaba.
// Temp/PruebaImpermeable.pedido (dentro: carpeta para las capturas) → Temp/PruebaImpermeable.resultado
[InitializeOnLoad]
public static class PruebaImpermeable
{
    const string Pedido = "Temp/PruebaImpermeable.pedido";
    const string Resultado = "Temp/PruebaImpermeable.resultado";
    const string P = "PruebaImpermeable.";

    static readonly (string nombre, string vista, Vector2 dir)[] Direcciones =
    {
        ("abajo", "frente", Vector2.down), ("arriba", "espalda", Vector2.up),
        ("derecha", "lado", Vector2.right), ("izquierda", "lado", Vector2.left),
    };

    static bool enganchado;

    static PruebaImpermeable()
    {
        if (enganchado) return;
        enganchado = true;
        EditorApplication.update += Revisar;
        Application.logMessageReceived += Anotar;
    }

    static int Paso { get => SessionState.GetInt(P + "paso", 0); set => SessionState.SetInt(P + "paso", value); }
    static int Ronda { get => SessionState.GetInt(P + "ronda", 0); set => SessionState.SetInt(P + "ronda", value); }
    static int Indice { get => SessionState.GetInt(P + "indice", 0); set => SessionState.SetInt(P + "indice", value); }
    static int Sub { get => SessionState.GetInt(P + "sub", 0); set => SessionState.SetInt(P + "sub", value); }
    static bool Chica => Ronda == 1;
    static string Quien => Chica ? "repartidora" : "repartidor";
    static void Log(string s) => SessionState.SetString(P + "log", SessionState.GetString(P + "log", "") + s + "\n");
    static void Fallo(string s) { Log("  FALLO: " + s); SessionState.SetInt(P + "fallos", SessionState.GetInt(P + "fallos", 0) + 1); }
    static void Comprobar(bool ok, string que) { if (ok) Log("  ok: " + que); else Fallo(que); }
    static void Anotar(string msg, string pila, LogType tipo)
    {
        if (Paso == 0 || tipo == LogType.Log) return;
        SessionState.SetString(P + "consola", SessionState.GetString(P + "consola", "") + $"[{tipo}] {msg}\n{(tipo == LogType.Exception ? pila : "")}");
    }
    static void Esperar(float s) => SessionState.SetFloat(P + "espera", (float)EditorApplication.timeSinceStartup + s);
    static bool Esperando => EditorApplication.timeSinceStartup < SessionState.GetFloat(P + "espera", 0);
    static void Siguiente(float espera) { Paso++; Esperar(espera); }
    static string Carpeta => SessionState.GetString(P + "carpeta", "Temp");
    static PlayerController Jugador => Object.FindFirstObjectByType<PlayerController>();
    static GameManager GM => GameManager.Instance;

    static void Revisar()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (Paso == 0)
        {
            if (!File.Exists(Pedido) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetString(P + "carpeta", File.ReadAllText(Pedido).Trim());
            File.Delete(Pedido);
            SessionState.SetString(P + "log", "");
            SessionState.SetString(P + "consola", "");
            SessionState.SetInt(P + "fallos", 0);
            SessionState.SetBool(P + "cargado", false);
            SessionState.SetFloat(P + "desde", 0);
            SessionState.SetInt(P + "generoAntes", Progreso.Genero);
            Ronda = 0;
            Progreso.Genero = 0;
            ProgresoPrueba.Preparar(motos: 0b1111, todosLosNiveles: true);
            Log("A) Solo Nivel3_Lluvia lleva el atuendo de lluvia");
            foreach (var (ruta, debe) in new[] { (ConstruirImpermeable.Escena, true), ("Assets/Scenes/SampleScene.unity", false),
                                                ("Assets/Scenes/Nivel2_Comercial.unity", false) })
                Comprobar(File.ReadAllText(ruta).Contains("atuendoLluvia: 1") == debe,
                          $"{Path.GetFileNameWithoutExtension(ruta)}: atuendo de lluvia {(debe ? "encendido" : "apagado")}");
            var inicio = EditorSceneManager.playModeStartScene;
            SessionState.SetString(P + "inicio", inicio != null ? AssetDatabase.GetAssetPath(inicio) : "");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ConstruirImpermeable.Escena);
            Log("Entrando a Play en Nivel3_Lluvia.");
            Paso = 1;
            Esperar(2f);
            EditorApplication.isPlaying = true;
            return;
        }
        if (Esperando) return;
        try { Avanzar(); }
        catch (Exception e) { Fallo($"excepción en el paso {Paso}: {e}"); Paso = 90; EditorApplication.isPlaying = false; }
    }

    static void Avanzar()
    {
        if (Paso < 90 && !EditorApplication.isPlaying) { Fallo($"Play se cerró en el paso {Paso}"); Paso = 90; return; }
        switch (Paso)
        {
            case 1:   // ELIGE TU MOTO abre con el tiempo en 0: Empezar
            {
                Application.runInBackground = true;
                // "Play siempre desde el menú" (IniciarDesdeMenu) puede ganarle a playModeStartScene: se carga el nivel a mano
                if (SceneManager.GetActiveScene().name == "MenuPrincipal" && !SessionState.GetBool(P + "cargado", false))
                {
                    SessionState.SetBool(P + "cargado", true);
                    Log("  (Play abrió MenuPrincipal: se carga Nivel3_Lluvia)");
                    SceneManager.LoadScene("Nivel3_Lluvia");
                    Esperar(0.5f);
                    break;
                }
                if (!EsperarA(() => SceneManager.GetActiveScene().name == "Nivel3_Lluvia" && Object.FindFirstObjectByType<TiendaMotos>() != null
                                    && Object.FindFirstObjectByType<TiendaMotos>().Abierta, 20,
                              $"ELIGE TU MOTO (escena {SceneManager.GetActiveScene().name}, cuadro {Time.frameCount}, " +
                              $"timeScale {Time.timeScale}, tienda {Object.FindFirstObjectByType<TiendaMotos>() != null}, foco {Application.isFocused})")) return;
                if (Object.FindFirstObjectByType<TiendaMotos>()?.Abierta != true) { Paso = 90; EditorApplication.isPlaying = false; break; }
                if (Sub == 0) { Sub = 1; Esperar(0.8f); break; }
                Sub = 0;
                var ventana = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "TiendaMotos_Fondo");
                var boton = ventana.Find("Ventana/BotonEmpezar");
                ExecuteEvents.Execute(boton.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
                Siguiente(0.5f);
                break;
            }
            case 2:
                if (!EsperarA(() => GM != null && !GM.EnEspera && GM.PuntoActual != null && Time.timeScale == 1f, 10, "que empiece el nivel 3")) return;
                foreach (var raiz in new[] { "AutosCalle", "Peatones" })
                    SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == raiz)?.SetActive(false);
                Camera.main.aspect = 16f / 9f;
                Comprobar(Jugador.AtuendoLluvia, "el PlayerController de la lluvia tiene el atuendo encendido");
                Siguiente(0.1f);
                break;

            // ---------------------------------------------------------------- una ronda por personaje
            case 3:
                Progreso.Genero = Ronda;
                Jugador.SetMotosHabilitadas(false);
                Log($"B{Ronda + 1}) {Quien} a pie");
                Siguiente(0.3f);
                break;
            case 4:
            {
                var anim = Jugador.GetComponent<Animator>();
                string esperado = Chica ? "RepartidoraLluviaOverride" : "RepartidorLluviaOverride";
                Comprobar(!Jugador.EnMoto && anim.runtimeAnimatorController.name == esperado, $"a pie, Animator con {anim.runtimeAnimatorController.name}");
                Indice = 0;
                Siguiente(0.1f);
                break;
            }
            case 5:   // caminar en la dirección Indice
                Jugador.DireccionForzada = Direcciones[Indice].dir;
                SessionState.SetString(P + "cuadros", "");
                Sub = 0;
                Siguiente(0.25f);
                break;
            case 6:
            {
                var sr = Jugador.GetComponent<SpriteRenderer>();
                var d = Direcciones[Indice];
                SessionState.SetString(P + "cuadros", SessionState.GetString(P + "cuadros", "") + sr.sprite.name + " ");
                if (++Sub < 4) { Esperar(0.13f); break; }
                string hoja = (Chica ? "repartidora" : "repartidor") + "_sheet_lluvia_";
                int fila = d.vista == "frente" ? 0 : d.vista == "espalda" ? 1 : 2;
                var vistos = SessionState.GetString(P + "cuadros", "").Trim().Split(' ');
                bool deLaFila = vistos.All(v => v.StartsWith(hoja) && int.TryParse(v.Substring(hoja.Length), out var k) && k / 5 == fila);
                Comprobar(deLaFila && vistos.Distinct().Count() > 1 && sr.flipX == (d.nombre == "izquierda") && Jugador.Orientacion.ToLower() == d.vista,
                          $"caminando a {d.nombre}: {string.Join(", ", vistos.Distinct())}");
                if (d.nombre != "izquierda") Capturar($"Lluvia_{Quien}_a_pie_{d.nombre}.png", 3f);
                Indice++;
                if (Indice < Direcciones.Length) { Paso = 5; break; }
                Jugador.DireccionForzada = Vector2.zero;
                Jugador.SetMotosHabilitadas(true);
                Log($"   {Quien} en las 4 motos");
                Indice = 0;
                Siguiente(0.2f);
                break;
            }
            case 7:   // equipar la moto Indice
                Progreso.Equipar(Indice);
                Comprobar(Jugador.EnMoto, $"{Progreso.Motos[Indice].nombre}: va en moto");
                Sub = 0;
                Siguiente(0.1f);
                break;
            case 8:
                Jugador.DireccionForzada = Direcciones[Sub].dir;
                Siguiente(0.35f);
                break;
            case 9:
            {
                var sr = Jugador.GetComponent<SpriteRenderer>();
                string id = Progreso.Motos[Indice].id;
                var d = Direcciones[Sub];
                string prefijo = $"{id}{(Chica ? "_chica" : "")}_lluvia_{d.vista}_";
                Comprobar(sr.sprite.name.StartsWith(prefijo), $"{id} a {d.nombre}: {sr.sprite.name}");
                if (d.nombre == "derecha" || (id == "clasica_azul" && d.nombre != "izquierda"))
                    Capturar($"Lluvia_{Quien}_{id}_{d.nombre}.png", 3f);
                Sub++;
                if (Sub < Direcciones.Length) { Paso = 8; break; }
                Jugador.DireccionForzada = Vector2.zero;
                Indice++;
                if (Indice < Progreso.Motos.Length) { Paso = 7; Esperar(0.2f); break; }
                Ronda++;
                if (Ronda < 2) { Paso = 3; Esperar(0.2f); break; }
                Jugador.DireccionForzada = null;
                Paso = 90;
                EditorApplication.isPlaying = false;
                Esperar(1f);
                break;
            }

            case 90:
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Progreso.Genero = SessionState.GetInt(P + "generoAntes", 0);
                ProgresoPrueba.Limpiar();
                var inicio = SessionState.GetString(P + "inicio", "");
                EditorSceneManager.playModeStartScene = inicio.Length > 0 ? AssetDatabase.LoadAssetAtPath<SceneAsset>(inicio) : null;
                int fallos = SessionState.GetInt(P + "fallos", 0);
                string consola = SessionState.GetString(P + "consola", "");
                File.WriteAllText(Resultado, (fallos == 0 ? "OK" : $"{fallos} FALLOS") + "\n" + SessionState.GetString(P + "log", "") +
                                  "\nCONSOLA (advertencias y errores en Play): " + (consola.Length == 0 ? "ninguno" : "\n" + consola));
                Paso = 0;
                break;
            }
        }
    }

    static bool EsperarA(Func<bool> condicion, float limite, string que)
    {
        float ahora = (float)EditorApplication.timeSinceStartup;
        if (SessionState.GetFloat(P + "desde", 0) == 0) SessionState.SetFloat(P + "desde", ahora);
        bool listo = false;
        try { listo = condicion(); } catch (NullReferenceException) { } catch (InvalidOperationException) { }
        if (!listo && ahora - SessionState.GetFloat(P + "desde", 0) < limite) { Esperar(0.05f); return false; }
        if (!listo) Fallo($"se acabó el tiempo esperando {que}");
        SessionState.SetFloat(P + "desde", 0);
        return true;
    }

    // el mundo (sin HUD), con la noche y la lluvia; zoom > 1: acercamiento centrado en el personaje
    static void Capturar(string archivo, float zoom)
    {
        var ruta = Path.Combine(Carpeta, archivo);
        var c = Camera.main;
        var rt = new RenderTexture(1920, 1080, 24);
        var pos = c.transform.position;
        float tam = c.orthographicSize;
        try
        {
            var pj = Jugador.transform.position;
            c.transform.position = new Vector3(pj.x, pj.y + 1.1f, pos.z);
            c.orthographicSize = tam / zoom;
            c.targetTexture = rt;
            c.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            File.WriteAllBytes(ruta, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        finally
        {
            RenderTexture.active = null;
            c.targetTexture = null;
            c.orthographicSize = tam;
            c.transform.position = pos;
            rt.Release();
        }
        Log($"  captura: {ruta}");
    }
}
