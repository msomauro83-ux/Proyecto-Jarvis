using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Jarvis;

namespace JarvisTests
{
    internal static class CommandTests
    {
        private static int assertions;
        private static int Main()
        {
            try
            {
                foreach (string invalid in new string[] { null, "", "abre calculadora", "Jarvis, abre calculadora", "Valentina abre calculadora", "Valeria abre calculadora", "Valeabre calculadora", "Vale_ abre calculadora", "Vale", "Vale, ", "hola Vale abre Chrome" }) NullPlan(invalid);
                string command;
                Check(CommandParser.TryWake("  vAlE: abre Chrome", out command), "Prefijo con mayúsculas y espacios");
                Equal("abre Chrome", command, "Comando extraído");
                Check(CommandParser.TryWake("Vale", out command) && command == "", "Vale solo");
                foreach (string known in SpeechGrammar.KnownCommands)
                    Check(CommandParser.Parse("Vale " + known) != null || CommandParser.ParseAssistantCommand("Vale " + known) != AssistantCommand.None, "Cada frase de la gramática tiene una acción: " + known);
                foreach (string launcher in SpeechGrammar.LauncherCommands())
                    Check(CommandParser.Parse("Vale, " + launcher) != null, "El catálogo de voz tiene una acción válida: " + launcher);
                Check(CommandParser.ParseAssistantCommand("Vale, ¿me escuchas?") == AssistantCommand.CheckMicrophone, "Prueba de voz con interrogación");
                Check(CommandParser.ParseAssistantCommand("me escuchas") == AssistantCommand.None, "La prueba de voz exige Vale");
                foreach (string phrase in new string[] { "conversemos", "quiero conversar", "quiero hablar contigo", "hablemos", "vamos a conversar", "inicia una conversación", "modo conversación", "inicia una conversacion", "modo conversacion", "termina la conversación", "termina conversación", "deja de conversar", "vuelve a comandos", "solo comandos", "modo comandos" })
                {
                    Check(CommandParser.ParseAssistantCommand("Vale, " + phrase) == AssistantCommand.None, "No existe un modo de conversación: " + phrase);
                    Check(CommandParser.ParseAssistantCommand(phrase) == AssistantCommand.None, "La conversación no inicia acciones: " + phrase);
                    NullPlan("Vale, " + phrase);
                }
                Check(CommandParser.ParseAssistantCommand("Vale, por favor conversemos.") == AssistantCommand.None, "La cortesía no activa conversación");
                Check(CommandParser.ParseAssistantCommand("Vale, escribe conversemos") == AssistantCommand.None, "El dictado conserva el texto solicitado");
                Check(!SpeechGrammar.Accepts(null), "Un resultado vacío no ejecuta acciones");
                TestPowerCommands();
                foreach (string phrase in new string[] { "Vale, abre Jarvis", "VALE: abre Vale.", "Vale, muéstrate", "Vale, por favor abre Jarvis", "Vale, abre tu ventana por favor." })
                    Check(CommandParser.ParseAssistantCommand(phrase) == AssistantCommand.ShowWindow, "Mostrar la ventana: " + phrase);
                foreach (string phrase in new string[] { "Vale, cierra Jarvis", "Vale, cierra Vale", "Vale, ocúltate", "Vale, ciérrate", "Vale, ve a la bandeja", "Vale, minimízate" })
                    Check(CommandParser.ParseAssistantCommand(phrase) == AssistantCommand.HideWindow, "Ocultar conservando el proceso: " + phrase);
                foreach (string phrase in new string[] { "Vale, pausa el micrófono", "Vale, deja de escuchar", "Vale, pausa" })
                    Check(CommandParser.ParseAssistantCommand(phrase) == AssistantCommand.PauseListening, "Pausar: " + phrase);
                foreach (string phrase in new string[] { null, "", "abre Jarvis", "cierra Jarvis", "Valentina, abre Jarvis", "Jarvis, abre Vale", "Vale, cierra Chrome", "Vale, abre Jarvis y borra documentos", "Vale, escribe cierra Jarvis" })
                    Check(CommandParser.ParseAssistantCommand(phrase) == AssistantCommand.None, "No interceptar otras órdenes: " + phrase);
                NullPlan("Vale, cierra Jarvis");
                NullPlan("Vale, abre Jarvis");
                Plan("VALE: abre la calculadora.", "open_app", "calculator");
                Plan("Vale, abre Bloc de notas", "open_app", "notepad");
                Plan("Vale, abre el explorador de archivos", "open_app", "explorer");
                Plan("Vale, abre navegador", "open_app", "browser");
                Plan("Vale, abre Chrome", "open_app", "chrome");
                Plan("Vale, abre Google Chrome", "open_app", "chrome");
                Plan("Vale, abre configuración", "open_app", "settings");
                Plan("Vale, abre documentos", "open_folder", "documents");
                Plan("Vale, abre descargas", "open_folder", "downloads");
                Plan("Vale, abre el escritorio", "open_folder", "desktop");
                Plan("Vale, muestra el escritorio", "show_desktop", "");
                Plan("Vale, sube el volumen", "volume_up", "");
                Plan("Vale, baja volumen", "volume_down", "");
                Plan("Vale, silencia el audio", "volume_mute", "");
                Plan("Vale, pausa la música", "media_play_pause", "");
                Plan("Vale, siguiente canción", "media_next", "");
                Plan("Vale, canción anterior", "media_previous", "");
                Plan("Vale, toma una captura de pantalla", "screenshot", "");
                Plan("Vale, copia la selección", "shortcut", "copy");
                Plan("Vale, pega", "shortcut", "paste");
                Plan("Vale, deshacer", "shortcut", "undo");
                Plan("Vale, por favor abre la calculadora", "open_app", "calculator");
                Plan("Vale, abre la calculadora por favor.", "open_app", "calculator");
                string unicode = "¡Hola, Mauricio! ¿Cómo estás? 😊";
                Plan("Vale, escribe " + unicode, "type_text", unicode);
                Plan("Vale, busca en YouTube canción Acción Ñandú", "youtube_search", "canción Acción Ñandú");
                Plan("Vale, búscame en Google Jose\u0301 Pe\u0301rez", "web_search", "Jose\u0301 Pe\u0301rez");
                Plan("Vale, escribe: {ENTER} + % ^ (Hola)", "type_text", "{ENTER} + % ^ (Hola)");
                Plan("Vale, escribe primera línea\r\nsegunda\tcolumna", "type_text", "primera línea\r\nsegunda\tcolumna");
                Plan("Vale, escribe Texto con espacios finales  ", "type_text", "Texto con espacios finales  ");
                Plan("Vale, busca en internet Café en Santiago", "web_search", "Café en Santiago");
                foreach (string invalid in new string[] { "Vale, busca en YouTube", "Vale, busca en Google:", "Vale, escribe", "Vale, borra todo", "Vale, elimina mis documentos", "Vale, apaga el equipo", "Vale, reinicia Windows", "Vale, cierra la ventana", "Vale, ejecuta powershell -Command whoami", "Vale, ejecuta cmd.exe /c whoami", @"Vale, abre C:\Windows\System32\cmd.exe", "Vale, abre calc.exe & shutdown /s", "Vale, escribe hola\0mundo", "Vale, busca consulta\noculta", "Vale, escribe " + new string('x', 10001) }) NullPlan(invalid);
                Equal("chrome", WindowsActions.CreatePlan("open_app", "chrome").Value, "Plan local válido");
                Equal(unicode + " {ENTER}", WindowsActions.CreatePlan("type_text", unicode + " {ENTER}").Value, "Unicode exacto en el plan local");
                RejectPlan(null, "chrome");
                RejectPlan("", "chrome");
                RejectPlan("open_app", null);
                RejectPlan("shell", "whoami");
                RejectPlan("open_app", "cmd.exe");
                RejectPlan("open_app", @"C:\Windows\notepad.exe");
                RejectPlan("screenshot", "overwrite.png");
                RejectPlan("volume_up", "100");
                RejectPlan("shortcut", "alt_f4");
                RejectPlan("web_search", "   ");
                RejectPlan("type_text", "bad\0text");
                RejectPlan("web_search", "bad\nquery");
                RejectPlan("type_text", new string('x', 10001));
                RejectPlan("web_search", new string('x', 2049));
                RejectPlan("open_folder", @"C:\Windows");
                bool invalidPlanRejected = false;
                try { WindowsActions.Execute(new CommandPlan { Action = "shell", Value = "whoami" }); } catch (ArgumentException) { invalidPlanRejected = true; }
                Check(invalidPlanRejected, "Execute revalida antes de ejecutar");
                TestSettingsMigration();
                assertions += AppCatalogTests.Run();
                Console.WriteLine("OK: " + assertions + " comprobaciones locales, sin ejecutar acciones.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        private static void TestPowerCommands()
        {
            var phrases = new Dictionary<string, AssistantCommand> {
                { "suspende el PC", AssistantCommand.SuspendComputer }, { "suspende el equipo", AssistantCommand.SuspendComputer },
                { "apaga el PC", AssistantCommand.ShutdownComputer }, { "apaga el equipo", AssistantCommand.ShutdownComputer },
                { "confirma suspensión", AssistantCommand.ConfirmSuspend }, { "confirma el apagado", AssistantCommand.ConfirmShutdown },
                { "cancela la acción", AssistantCommand.CancelPowerAction }, { "cancela el apagado", AssistantCommand.CancelPowerAction },
                { "bloquea el PC", AssistantCommand.LockComputer }, { "bloquea el equipo", AssistantCommand.LockComputer }
            };
            foreach (var phrase in phrases)
            {
                Check(CommandParser.ParseAssistantCommand("Vale, " + phrase.Key) == phrase.Value, "Comando de energía/bloqueo local: " + phrase.Key);
                Check(CommandParser.ParseAssistantCommand(phrase.Key) == AssistantCommand.None, "Energía/bloqueo exige Vale: " + phrase.Key);
                Check(CommandParser.ParseAssistantCommand("Vale, " + phrase.Key + " y borra documentos") == AssistantCommand.None, "No admite acciones compuestas: " + phrase.Key);
                Check(CommandParser.Parse("Vale, " + phrase.Key) == null, "Energía y bloqueo usan sus controles locales específicos");
            }
            foreach (string action in new[] { "shutdown", "suspend", "lock_pc", "confirm_shutdown", "power" })
                RejectPlan(action, "");
            var gate = new PowerCommandConfirmation();
            DateTime now = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);
            AssistantCommand approved;
            Check(!gate.TryConfirm(AssistantCommand.ConfirmShutdown, now, out approved) && approved == AssistantCommand.None, "Confirmar sin solicitud no hace nada");
            gate.Request(AssistantCommand.ShutdownComputer, now);
            Check(gate.TryConfirm(AssistantCommand.ConfirmShutdown, now.AddSeconds(29), out approved) && approved == AssistantCommand.ShutdownComputer, "Confirmación válida dentro del plazo");
            Check(!gate.TryConfirm(AssistantCommand.ConfirmShutdown, now.AddSeconds(29), out approved), "Confirmación de un solo uso");
            gate.Request(AssistantCommand.SuspendComputer, now);
            Check(!gate.TryConfirm(AssistantCommand.ConfirmShutdown, now.AddSeconds(1), out approved), "No confunde apagar con suspender");
            Check(!gate.TryConfirm(AssistantCommand.ConfirmSuspend, now.AddSeconds(2), out approved), "Confirmación distinta cancela la solicitud anterior");
            gate.Request(AssistantCommand.ShutdownComputer, now);
            Check(!gate.TryConfirm(AssistantCommand.ConfirmShutdown, now.AddSeconds(30), out approved), "La autorización caduca a los 30 segundos");
            gate.Request(AssistantCommand.SuspendComputer, now);
            gate.Cancel();
            Check(!gate.TryConfirm(AssistantCommand.ConfirmSuspend, now, out approved), "Cancelar impide ejecutar después");
            gate.Request(AssistantCommand.ShutdownComputer, now);
            gate.Request(AssistantCommand.SuspendComputer, now.AddSeconds(1));
            Check(gate.TryConfirm(AssistantCommand.ConfirmSuspend, now.AddSeconds(2), out approved) && approved == AssistantCommand.SuspendComputer, "La última petición sustituye la anterior");
            gate.Request(AssistantCommand.ShutdownComputer, now);
            Check(!gate.TryConfirm(AssistantCommand.ConfirmShutdown, now.AddSeconds(-1), out approved), "Un retroceso del reloj invalida la confirmación");
            bool rejected = false;
            try { gate.Request(AssistantCommand.LockComputer, now); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "La confirmación solo corresponde a suspender o apagar");
            var shutdown = ComputerPower.CreateShutdownStartInfo();
            Check(shutdown.FileName == System.IO.Path.Combine(Environment.SystemDirectory, "shutdown.exe") && shutdown.Arguments == "/s /t 0" && !shutdown.UseShellExecute && shutdown.CreateNoWindow && String.IsNullOrEmpty(shutdown.Verb), "Apagado local fijo, sin fuerza ni elevación; solo se inspecciona, no se ejecuta");
        }

        private static void TestSettingsMigration()
        {
            AppSettings initial = new AppSettings();
            Check(initial.UserName == "Mauricio" && initial.SpeakLocal, "Configuración local inicial");
            string legacy = "{\"UserName\":\"Mauricio de prueba\",\"SpeakLocal\":false,\"Mode\":\"chatgpt\",\"Model\":\"obsolete-model\",\"Voice\":\"obsolete-voice\",\"ProtectedApiKey\":\"fixture-not-a-credential\",\"ChatGptModel\":\"obsolete-model\",\"ConversationProvider\":\"chatgpt\"}";
            AppSettings migrated = SettingsStore.Deserialize(legacy);
            Check(migrated.UserName == "Mauricio de prueba" && !migrated.SpeakLocal, "Migrar conserva nombre y preferencia de voz");
            var serializer = new JavaScriptSerializer();
            var saved = serializer.Deserialize<Dictionary<string, object>>(serializer.Serialize(migrated));
            Check(saved.Count == 2 && saved.ContainsKey("UserName") && saved.ContainsKey("SpeakLocal"), "Guardar solo incluye los dos ajustes locales");
            Check(SettingsStore.Deserialize("{\"UserName\":\"  \"}").UserName == "Mauricio", "Migrar normaliza un nombre vacío");
            Check(SettingsStore.Deserialize("{}").SpeakLocal, "Un archivo sin preferencia conserva la respuesta hablada");
            bool nullRejected = false;
            try { SettingsStore.Deserialize("null"); } catch (System.IO.InvalidDataException) { nullRejected = true; }
            Check(nullRejected, "Un archivo JSON nulo no produce ajustes nulos");
        }

        private static void Plan(string input, string action, string value)
        {
            CommandPlan plan = CommandParser.Parse(input);
            Check(plan != null, "Debe reconocer: " + input);
            Equal(action, plan.Action, "Acción: " + input);
            Equal(value, plan.Value, "Valor: " + input);
            Check(!plan.RequiresConfirmation, "Acción sin confirmación");
        }
        private static void NullPlan(string input) { Check(CommandParser.Parse(input) == null, "Debe rechazar: " + input); }
        private static void RejectPlan(string action, string value)
        {
            bool rejected = false;
            try { WindowsActions.CreatePlan(action, value); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Debe rechazar acción/valor: " + action + "/" + value);
        }
        private static void Equal(string expected, string actual, string label) { Check(String.Equals(expected, actual, StringComparison.Ordinal), label + " esperado [" + expected + "] obtenido [" + actual + "]"); }
        private static void Check(bool condition, string label) { assertions++; if (!condition) throw new Exception(label); }
    }
}
