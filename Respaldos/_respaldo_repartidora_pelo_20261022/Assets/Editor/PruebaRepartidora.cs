using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Prueba en Play de las dos rutas, empezando en MenuPrincipal: repartidor (ruta 0) y repartidora (ruta 1).
// Menú (nombre) → Seleccion (A/D, clic en las pestañas, Enter o CONFIRMAR; ficha, retrato, música del menú) → Intro de
// 6 viñetas (imagen, titular y texto de cada una según el género) → nivel 1 a pie (set de sprites, animación en las 4
// direcciones, el mismo collider) → ELIGE TU MOTO → nivel 2 con las 4 motos (sprites de cada una en 3 vistas, cajas de
// colisión iguales) → ELIGE TU MOTO → nivel 3 en moto → panel final con el texto de cierre según el género.
// Capturas 1920x1080: la selección con cada pestaña, la viñeta del salón, "TE NECESITAMOS" de los dos, la repartidora a
// pie (frente, espalda, lado, x3) y en las 4 motos (3 vistas, x3), ELIGE TU MOTO y el panel final de cada ruta.
// Para tener las 4 motos se marcan como compradas; al final el avance guardado (dinero, motos, moto equipada, nombre y
// género) queda como estaba. Temp/PruebaRepartidora.pedido (dentro: carpeta) → Temp/PruebaRepartidora.resultado
[InitializeOnLoad]
public static class PruebaRepartidora
{
    const string Pedido = "Temp/PruebaRepartidora.pedido";
    const string Resultado = "Temp/PruebaRepartidora.resultado";
    const string RutaMenu = "Assets/Scenes/MenuPrincipal.unity";
    const string P = "PruebaRepartidora.";
    static readonly string[] ClavesInt = { "dinero_total", "motos_compradas", "moto_equipada", "genero_jugador" };
    static readonly string[] Nombres = { "Bruno", "Valeria" };
    static readonly (string nombre, Vector2 dir, string vista)[] Direcciones =
        { ("frente", Vector2.down, "frente"), ("espalda", Vector2.up, "espalda"), ("lado", Vector2.right, "lado") };

    static bool enganchado;

    static PruebaRepartidora()
    {
        if (enganchado) return;
        enganchado = true;
        EditorApplication.update += Revisar;
        Application.logMessageReceived += Anotar;
    }

    static int Paso { get => SessionState.GetInt(P + "paso", 0); set => SessionState.SetInt(P + "paso", value); }
    static int Ruta { get => SessionState.GetInt(P + "ruta", 0); set => SessionState.SetInt(P + "ruta", value); }
    static int Indice { get => SessionState.GetInt(P + "i", 0); set => SessionState.SetInt(P + "i", value); }
    static int Sub { get => SessionState.GetInt(P + "sub", 0); set => SessionState.SetInt(P + "sub", value); }
    static bool Chica => Ruta == 1;
    static string R => Chica ? "[repartidora]" : "[repartidor]";
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
    static string Escena => SceneManager.GetActiveScene().name;
    static GameManager GM => GameManager.Instance;
    static PlayerController Jugador => Object.FindFirstObjectByType<PlayerController>();
    static SeleccionPersonaje Sel => Object.FindFirstObjectByType<SeleccionPersonaje>();
    static IntroHistoria Intro => Object.FindFirstObjectByType<IntroHistoria>();
    static string Carpeta => SessionState.GetString(P + "carpeta", "Temp");
    static void Tecla(Key k) => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k));
    static void Soltar() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());

    static void Revisar()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (Paso == 0)
        {
            if (!File.Exists(Pedido) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var carpeta = File.ReadAllText(Pedido).Trim();
            File.Delete(Pedido);
            Directory.CreateDirectory(carpeta);
            SessionState.SetString(P + "carpeta", carpeta);
            SessionState.SetString(P + "log", "");
            SessionState.SetString(P + "consola", "");
            SessionState.SetInt(P + "fallos", 0);
            SessionState.SetFloat(P + "desde", 0);
            Sub = 0;
            foreach (var k in ClavesInt)
                SessionState.SetString(P + "pp." + k, PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k).ToString() : "");
            SessionState.SetString(P + "pp.nombre", PlayerPrefs.HasKey("nombre_jugador") ? "=" + PlayerPrefs.GetString("nombre_jugador") : "");
            PlayerPrefs.SetInt("motos_compradas", 15);   // las 4 motos, para probarlas
            PlayerPrefs.SetInt("genero_jugador", 0);
            PlayerPrefs.Save();
            var inicio = EditorSceneManager.playModeStartScene;
            SessionState.SetString(P + "inicio", inicio != null ? AssetDatabase.GetAssetPath(inicio) : "");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaMenu);
            Log("Entrando a Play desde MenuPrincipal.");
            Ruta = 0;
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
            // ---------------------------------------------------------------- menú
            case 1:
                Application.runInBackground = true;
                if (!EsperarA(() => Escena == "MenuPrincipal" && Time.timeSinceLevelLoad > 0.4f && EventSystem.current != null
                                    && Buscar("Canvas/BotonIniciar") != null, 15, "el menú")) return;
                PruebaIntro.EnfocarGameView();
                if (Ruta == 0) PruebaIntro.EntradaSinFoco();
                Log($"\n===== RUTA {Ruta}: {(Chica ? "REPARTIDORA" : "REPARTIDOR")} ({Nombres[Ruta]}) =====");
                Log("1) Menú: nombre e Iniciar juego");
                Buscar("Canvas/CampoNombre").GetComponent<TMP_InputField>().text = Nombres[Ruta];
                SessionState.SetFloat(P + "tMenu", AudioManager.Instance.FuenteMusica.time);
                Clic("Canvas/BotonIniciar");
                Siguiente(0.5f);
                break;

            // ---------------------------------------------------------------- selección
            case 2:
                if (!EsperarA(() => Escena == "Seleccion" && Sel != null && Time.timeSinceLevelLoad > 0.5f, 10, "la selección")) return;
                Log("2) ELIGE A TU REPARTIDOR");
                Comprobar(Escena == "Seleccion", "Iniciar juego carga Seleccion");
                Comprobar(Sel.Actual == 0, $"empieza en lo guardado (Progreso.Genero = {Progreso.Genero}): pestaña {Sel.Actual}");
                float tMenu = SessionState.GetFloat(P + "tMenu", 0), tAhora = AudioManager.Instance.FuenteMusica.time;
                Comprobar(MusicaActual() == "musica_menu" && AudioManager.Instance.FuenteMusica.isPlaying && tAhora > tMenu,
                          $"sigue la música del menú sin reiniciarse ({MusicaActual()}, {tMenu:0.0} → {tAhora:0.0} s)");
                Ficha(0);
                Tecla(Key.D);
                Log("  tecla D");
                Siguiente(0.12f);
                break;
            case 3:
                if (Sub == 0)
                {
                    Soltar();
                    Comprobar(Sel.Actual == 1, "D elige REPARTIDORA");
                    Ficha(1);
                    if (Ruta == 0) CapturarUI("Seleccion_repartidora.png");
                    Sub = 1;
                    Esperar(0.2f);   // el Canvas vuelve a Overlay en el siguiente cuadro
                    break;
                }
                Sub = 0;
                if (Ruta == 0) Clic("Canvas/Pestanas/PestanaRepartidor");
                Siguiente(0.3f);
                break;
            case 4:
                if (Ruta == 0 && Sub == 0)
                {
                    Comprobar(Sel.Actual == 0, "clic en la pestaña REPARTIDOR");
                    Ficha(0);
                    CapturarUI("Seleccion_repartidor.png");
                    Sub = 1;
                    Esperar(0.2f);
                    break;
                }
                Sub = 0;
                if (Ruta == 0)
                {
                    Clic("Canvas/Confirmar");
                    Log("  clic en CONFIRMAR");
                }
                else
                {
                    Tecla(Key.Enter);
                    Log("  Enter");
                }
                Siguiente(0.12f);
                break;
            case 5:
                Soltar();
                Siguiente(0.3f);
                break;

            // ---------------------------------------------------------------- intro
            case 6:
                if (!EsperarA(() => Escena == "Intro" && Intro != null, 10, "la Intro")) return;
                Log("3) Intro");
                Comprobar(Progreso.Genero == Ruta && Progreso.NombreJugador == Nombres[Ruta], $"se guardó Genero = {Progreso.Genero} y el nombre \"{Progreso.NombreJugador}\"");
                var lista = new SerializedObject(Intro).FindProperty("vinetas");
                Comprobar(lista.arraySize == 6, $"la intro tiene {lista.arraySize} viñetas");
                Indice = 0;
                Siguiente(0.3f);
                break;
            case 7:
            {
                var so = new SerializedObject(Intro);
                var texto = so.FindProperty("texto").objectReferenceValue as TextMeshProUGUI;
                var placa = so.FindProperty("indicadorSiguiente").objectReferenceValue as GameObject;
                var v = ConstruirIntro.Vinetas[Indice];
                string esperado = Texto.ConNombre(Chica && v.textoRepartidora.Length > 0 ? v.textoRepartidora : v.texto);
                if (!EsperarA(() => texto.text == esperado && placa.activeInHierarchy, 12, $"la viñeta {Indice + 1}")) return;
                var titular = (so.FindProperty("titular").objectReferenceValue as TextMeshProUGUI).text;
                var imagen = (so.FindProperty("imagen").objectReferenceValue as Image).sprite?.name;
                string img = Chica && v.archivoRepartidora.Length > 0 ? v.archivoRepartidora : v.archivo;
                Comprobar(imagen == img && titular == Texto.ConNombre(v.titular),
                          $"viñeta {Indice + 1}: {imagen} / \"{titular}\" / \"{(texto.text.Length > 60 ? texto.text.Substring(0, 60) + "…" : texto.text)}\"");
                if (Indice == 1 && Ruta == 0) CapturarUI("Intro_vineta2_salon.png");
                if (Indice == 3) CapturarUI($"Intro_te_necesitamos_{(Chica ? "repartidora" : "repartidor")}.png");
                if (Indice == 5)
                {
                    Comprobar(texto.text.Contains(Chica ? "la mejor repartidora del Salón Brenn's" : "el mejor repartidor del Salón Brenn's"),
                              "la última viñeta habla de " + (Chica ? "la mejor repartidora" : "el mejor repartidor"));
                    if (Chica) CapturarUI("Intro_vineta6_repartidora.png");
                }
                Tecla(Key.Space);
                Siguiente(0.12f);
                break;
            }
            case 8:
                Soltar();
                Indice++;
                if (Indice < 6) { Paso = 7; Esperar(0.55f); }
                else Siguiente(0.3f);
                break;

            // ---------------------------------------------------------------- nivel 1 a pie
            case 9:
            {
                if (!EsperarA(() => Escena == "SampleScene" && GM != null && GM.PuntoActual != null && Time.timeSinceLevelLoad > 0.5f, 15, "el nivel 1")) return;
                Log("4) Nivel 1 a pie");
                var pc = Jugador;
                var anim = pc.GetComponent<Animator>();
                Comprobar(!pc.EnMoto, "va a pie");
                Comprobar(anim.runtimeAnimatorController.name == (Chica ? "RepartidoraOverride" : "RepartidorController"),
                          $"Animator con {anim.runtimeAnimatorController.name}");
                var caja = pc.GetComponent<BoxCollider2D>();
                Caja("pie", caja);
                foreach (var raiz in new[] { "AutosCalle", "Peatones" })
                    SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == raiz)?.SetActive(false);
                Camera.main.aspect = 16f / 9f;
                Indice = 0;
                Siguiente(0.2f);
                break;
            }
            case 10:   // caminar en cada dirección y revisar el cuadro
                Jugador.DireccionForzada = Direcciones[Indice].dir;
                Siguiente(0.45f);
                break;
            case 11:
            {
                var pc = Jugador;
                var sr = pc.GetComponent<SpriteRenderer>();
                string hoja = Chica ? "repartidora_sheet_" : "repartidor_sheet_";
                int fila = Indice;   // 0 frente (0-4), 1 espalda (5-9), 2 lado (10-14)
                int n = int.TryParse(sr.sprite.name.Replace(hoja, ""), out var k) ? k : -1;
                Comprobar(sr.sprite.name.StartsWith(hoja) && n / 5 == fila && pc.Orientacion.ToLower() == Direcciones[Indice].vista,
                          $"caminando de {Direcciones[Indice].nombre}: {sr.sprite.name} ({pc.Orientacion})");
                if (Chica) Capturar($"Repartidora_a_pie_{Direcciones[Indice].nombre}.png", 3f);
                Indice++;
                if (Indice < Direcciones.Length) { Paso = 10; break; }
                pc.DireccionForzada = Vector2.zero;
                Siguiente(0.3f);
                break;
            }
            case 12:
                Jugador.DireccionForzada = null;
                Log($"  se entregan los {GM.PedidosMeta} pedidos");
                Siguiente(0.2f);
                break;
            case 13:   // nivel 1, 2 y 3: entregar todo
            case 21:
            case 26:
                if (GM.NivelTerminado) { Siguiente(2.5f); break; }
                GM.Entregar(GM.PuntoActual);
                Esperar(0.35f);
                break;
            case 14:
            case 22:
            {
                if (!EsperarA(() => Panel() != null && Panel().gameObject.activeInHierarchy, 10, "el panel final")) return;
                Clic(RutaDe(Panel().Find("BotonSiguiente")));
                Log("  Siguiente nivel");
                Siguiente(0.5f);
                break;
            }

            // ---------------------------------------------------------------- ELIGE TU MOTO (niveles 2 y 3)
            case 15:
            case 23:
            {
                string escena = Paso == 15 ? "Nivel2_Comercial" : "Nivel3_Lluvia";
                if (!EsperarA(() => Escena == escena && Object.FindFirstObjectByType<TiendaMotos>() != null
                                    && Object.FindFirstObjectByType<TiendaMotos>().Abierta, 15, $"{escena} con ELIGE TU MOTO")) return;
                if (Sub == 0)
                {
                    Log($"{(Paso == 15 ? 5 : 7)}) {escena}: ELIGE TU MOTO");
                    Progreso.Equipar(Paso == 15 ? 0 : 1);
                    if (Paso == 15) CapturarUI($"EligeTuMoto_{(Chica ? "repartidora" : "repartidor")}.png");
                    Sub = 1;
                    Esperar(0.2f);
                    break;
                }
                Sub = 0;
                var ventana = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "TiendaMotos_Fondo");
                Clic(RutaDe(ventana.Find("Ventana/BotonEmpezar")));
                Siguiente(0.5f);
                break;
            }
            case 16:
                if (!EsperarA(() => !GM.EnEspera && GM.PuntoActual != null && Time.timeScale == 1f && Time.timeSinceLevelLoad > 0.5f, 10, "que empiece el nivel 2")) return;
                foreach (var raiz in new[] { "AutosCalle", "Peatones" })
                    SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == raiz)?.SetActive(false);
                Camera.main.aspect = 16f / 9f;
                Log("6) Nivel 2: las 4 motos");
                Indice = 0;
                Sub = 0;
                Siguiente(0.2f);
                break;
            case 17:   // equipar la moto Indice
                Progreso.Equipar(Indice);
                Comprobar(Jugador.EnMoto, $"{Progreso.Motos[Indice].nombre}: va en moto");
                Sub = 0;
                Siguiente(0.1f);
                break;
            case 18:   // rodar en la dirección Sub
                Jugador.DireccionForzada = Direcciones[Sub].dir;
                Siguiente(0.35f);
                break;
            case 19:
            {
                var pc = Jugador;
                var sr = pc.GetComponent<SpriteRenderer>();
                string id = Progreso.Motos[Indice].id;
                string prefijo = $"{id}{(Chica ? "_chica" : "")}_{Direcciones[Sub].vista}_";
                Comprobar(sr.sprite.name.StartsWith(prefijo), $"{id} de {Direcciones[Sub].nombre}: {sr.sprite.name}");
                Caja($"{id}_{Direcciones[Sub].vista}", pc.GetComponent<BoxCollider2D>());
                if (Chica) Capturar($"Repartidora_{id}_{Direcciones[Sub].nombre}.png", 3f);
                Sub++;
                if (Sub < Direcciones.Length) { Paso = 18; break; }
                pc.DireccionForzada = Vector2.zero;
                Indice++;
                if (Indice < Progreso.Motos.Length) { Paso = 17; Esperar(0.2f); break; }
                Siguiente(0.2f);
                break;
            }
            case 20:
                Jugador.DireccionForzada = null;
                Progreso.Equipar(0);
                Log($"  se entregan los {GM.PedidosMeta} pedidos");
                Siguiente(0.2f);
                break;
            case 24:
                if (!EsperarA(() => !GM.EnEspera && GM.PuntoActual != null && Time.timeScale == 1f && Time.timeSinceLevelLoad > 0.5f, 10, "que empiece el nivel 3")) return;
            {
                var pc = Jugador;
                Log("8) Nivel 3 (lluvia, de noche)");
                string id = Progreso.Motos[Progreso.MotoEquipada].id;   // la que dejó elegida ELIGE TU MOTO
                Comprobar(pc.EnMoto && pc.GetComponent<SpriteRenderer>().sprite.name.StartsWith($"{id}{(Chica ? "_chica" : "")}_"),
                          $"en moto ({id}): {pc.GetComponent<SpriteRenderer>().sprite.name}");
                Siguiente(0.2f);
                break;
            }
            case 25:
                Log($"  se entregan los {GM.PedidosMeta} pedidos");
                Siguiente(0.2f);
                break;
            case 27:
            {
                if (!EsperarA(() => Panel() != null && Panel().gameObject.activeInHierarchy, 10, "el panel final del nivel 3")) return;
                var cierre = Panel().Find("TextoCierre").GetComponent<TMP_Text>().text;
                var titulo = Panel().Find("Titulo").GetComponent<TMP_Text>().text;
                string esperado = $"Entregaste todo bajo la lluvia. El Salón Brenn's ya tiene a su mejor {(Chica ? "repartidora" : "repartidor")}: {Nombres[Ruta]}.";
                Comprobar(titulo == "¡TE QUEDASTE CON EL PUESTO!" && cierre == esperado, $"9) final: \"{titulo}\" / \"{cierre}\"");
                Siguiente(2f);
                break;
            }
            case 28:
                CapturarUI($"Final_{(Chica ? "repartidora" : "repartidor")}.png");
                Camera.main.ResetAspect();
                if (Ruta == 0)
                {
                    Ruta = 1;
                    SceneManager.LoadScene("MenuPrincipal");
                    Paso = 1;
                    Esperar(0.5f);
                    break;
                }
                Paso = 90;
                EditorApplication.isPlaying = false;
                Esperar(1f);
                break;

            case 90:
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                PruebaIntro.RestaurarEntrada();
                foreach (var k in ClavesInt)
                {
                    var v = SessionState.GetString(P + "pp." + k, null);
                    if (v == null) continue;
                    if (v == "") PlayerPrefs.DeleteKey(k); else PlayerPrefs.SetInt(k, int.Parse(v));
                }
                var nombre = SessionState.GetString(P + "pp.nombre", "");
                if (nombre == "") PlayerPrefs.DeleteKey("nombre_jugador"); else PlayerPrefs.SetString("nombre_jugador", nombre.Substring(1));
                PlayerPrefs.Save();
                Log($"\nAvance restaurado: dinero {PlayerPrefs.GetInt("dinero_total", 0)}, motos {PlayerPrefs.GetInt("motos_compradas", 0)}, " +
                    $"equipada {PlayerPrefs.GetInt("moto_equipada", -1)}, género {PlayerPrefs.GetInt("genero_jugador", 0)}, nombre \"{PlayerPrefs.GetString("nombre_jugador", "")}\"");
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

    // la ficha de la selección para el índice i
    static void Ficha(int i)
    {
        var so = new SerializedObject(Sel);
        string T(string campo) => (so.FindProperty(campo).objectReferenceValue as TMP_Text)?.text;
        var f = ConstruirSeleccion.Fichas[i];
        string retrato = (so.FindProperty("retrato").objectReferenceValue as Image).sprite?.name;
        var pestana = (so.FindProperty("pestanas").GetArrayElementAtIndex(i).objectReferenceValue as Image).sprite?.name;
        Comprobar(retrato == (i == 0 ? "repartidor_sheet_0" : "repartidora_sheet_0") && pestana == "seleccion_pestana_activa" &&
                  T("cita") == "“" + f.cita.Replace("{nombre}", Nombres[Ruta].ToUpperInvariant()) + "”" &&
                  T("nombre") == "<b>Nombre:</b> " + Nombres[Ruta] && T("puesto") == "<b>Puesto:</b> " + f.puesto &&
                  T("item") == "<b>Ítem favorito:</b> mochila de reparto" && T("dato") == "<b>Dato:</b> " + f.dato,
                  $"ficha de {f.pestana}: retrato {retrato}, {T("cita")}, {T("puesto")?.Replace("<b>", "").Replace("</b>", "")}");
        var igual = Buscar("Canvas/Ventana/FranjaIgual/Texto")?.GetComponent<TMP_Text>();
        if (i == 0) Comprobar(igual != null && igual.text == ConstruirSeleccion.Igual && igual.isActiveAndEnabled, $"se ve \"{igual?.text}\"");
    }

    // el collider en cada modo es el mismo para los dos
    static void Caja(string modo, BoxCollider2D caja)
    {
        string v = $"{caja.size.x:0.###}x{caja.size.y:0.###}@{caja.offset.x:0.###},{caja.offset.y:0.###}";
        string clave = P + "caja." + modo;
        if (Ruta == 0) { SessionState.SetString(clave, v); return; }
        string antes = SessionState.GetString(clave, "");
        Comprobar(antes == v, $"collider {modo}: {v} (el repartidor: {antes})");
    }

    static string MusicaActual() => AudioManager.Instance != null ? AudioManager.Instance.MusicaActual : null;

    static Transform Panel() => Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "PanelFinal");
    static string RutaDe(Transform t) => t.parent == null ? t.name : RutaDe(t.parent) + "/" + t.name;

    static Transform Buscar(string ruta)
    {
        var partes = ruta.Split(new[] { '/' }, 2);
        var raiz = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == partes[0])?.transform;
        return raiz == null ? null : partes.Length == 1 ? raiz : raiz.Find(partes[1]);
    }

    static void Clic(string ruta)
    {
        var t = Buscar(ruta);
        if (t == null || !t.gameObject.activeInHierarchy) { Fallo($"{ruta} no existe o está apagado"); return; }
        var datos = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, t.position) };
        var golpes = new List<RaycastResult>();
        EventSystem.current.RaycastAll(datos, golpes);
        var arriba = golpes.Count > 0 ? golpes[0].gameObject : null;
        if (arriba == null || !(arriba.transform == t || arriba.transform.IsChildOf(t)))
        {
            Fallo($"clic en {ruta}: lo tapa {(arriba != null ? arriba.name : "nada")}");
            return;
        }
        ExecuteEvents.ExecuteHierarchy(arriba, datos, ExecuteEvents.pointerClickHandler);
    }

    static bool EsperarA(Func<bool> condicion, float limite, string que)
    {
        float ahora = (float)EditorApplication.timeSinceStartup;
        if (SessionState.GetFloat(P + "desde", 0) == 0) SessionState.SetFloat(P + "desde", ahora);
        bool listo = false;
        try { listo = condicion(); } catch (NullReferenceException) { } catch (InvalidOperationException) { } catch (MissingReferenceException) { }
        if (!listo && ahora - SessionState.GetFloat(P + "desde", 0) < limite) { Esperar(0.05f); return false; }
        SessionState.SetFloat(P + "desde", 0);
        if (listo) return true;
        Fallo($"se acabó el tiempo esperando {que} (escena {Escena}); se detiene la prueba");
        Paso = 90;
        EditorApplication.isPlaying = false;
        Esperar(1f);
        return false;
    }

    // el mundo (sin HUD); zoom > 1: acercamiento centrado en el personaje
    static void Capturar(string archivo, float zoom)
    {
        var ruta = Path.Combine(Carpeta, archivo);
        var c = Camera.main;
        var rt = new RenderTexture(1920, 1080, 24);
        var pos = c.transform.position;
        float tam = c.orthographicSize;
        try
        {
            var pc = Jugador.transform.position;
            c.transform.position = new Vector3(pc.x, pc.y + 1.1f, pos.z);
            c.orthographicSize = tam / zoom;
            c.targetTexture = rt;
            c.Render();
            Guardar(rt, ruta);
        }
        finally
        {
            c.targetTexture = null;
            c.orthographicSize = tam;
            c.transform.position = pos;
            rt.Release();
        }
        Log($"  captura: {ruta}");
    }

    // la pantalla con la interfaz: los Canvas en Overlay pasan un momento a Screen Space - Camera
    static void CapturarUI(string archivo)
    {
        var ruta = Path.Combine(Carpeta, archivo);
        var c = Camera.main;
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(x => x.isRootCanvas && x.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
        var antes = canvases.Select(x => (x.sortingLayerID, x.sortingOrder)).ToList();
        var rt = new RenderTexture(1920, 1080, 24);
        try
        {
            c.targetTexture = rt;
            foreach (var x in canvases)
            {
                x.renderMode = RenderMode.ScreenSpaceCamera;
                x.worldCamera = c;
                x.sortingLayerID = SortingLayer.layers.Last().id;
                x.sortingOrder = Mathf.Min(short.MaxValue, 30000 + antes[canvases.IndexOf(x)].sortingOrder);
                x.planeDistance = c.nearClipPlane + 1;
            }
            Canvas.ForceUpdateCanvases();
            c.Render();
            Guardar(rt, ruta);
        }
        finally
        {
            c.targetTexture = null;
            for (int i = 0; i < canvases.Count; i++)
            {
                canvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
                canvases[i].sortingLayerID = antes[i].sortingLayerID;
                canvases[i].sortingOrder = antes[i].sortingOrder;
            }
            Canvas.ForceUpdateCanvases();
            rt.Release();
        }
        Log($"  captura: {ruta}");
    }

    static void Guardar(RenderTexture rt, string ruta)
    {
        RenderTexture.active = rt;
        var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        tex.Apply();
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        RenderTexture.active = null;
    }
}
