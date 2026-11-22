using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Recognition;

namespace Jarvis
{
    public static class SpeechGrammar
    {
        public const string CommandGrammarName = "Vale.Commands";
        public const string LauncherGrammarName = "Vale.Launchers";
        public const string DictationGrammarName = "Vale.Dictation";
        private static readonly string[] Commands = {
            "abre jarvis", "abre vale", "muestra jarvis", "muestra vale", "abre tu ventana", "muestra tu ventana", "muéstrate",
            "cierra jarvis", "cierra vale", "oculta jarvis", "oculta vale", "cierra tu ventana", "oculta tu ventana", "ocúltate", "ciérrate", "ve a la bandeja", "minimízate",
            "me escuchas", "me escucha", "puedes escucharme", "estás ahí", "hola",
            "pausa el micrófono", "deja de escuchar", "pausa", "pausa la escucha",
            "abre calculadora", "abre la calculadora", "abre bloc de notas", "abre el bloc de notas",
            "abre chrome", "abre google chrome", "abre navegador", "abre el navegador", "abre configuración", "abre la configuración", "abre ajustes",
            "abre documentos", "abre mis documentos", "abre descargas", "abre el escritorio", "abre explorador", "abre el explorador", "abre el explorador de archivos",
            "sube el volumen", "sube volumen", "baja el volumen", "baja volumen", "silencio", "silencia el audio", "silencia el volumen", "quita el sonido", "activa el sonido",
            "pausa la música", "pausa la reproducción", "reanuda la música", "reanuda la reproducción", "reproduce", "reproduce la música",
            "siguiente", "siguiente canción", "siguiente pista", "anterior", "canción anterior",
            "muestra el escritorio", "captura de pantalla", "toma una captura", "toma una captura de pantalla", "haz una captura de pantalla",
            "copia", "copia la selección", "pega", "deshaz", "deshacer",
            "suspende el pc", "suspende el equipo", "suspende el ordenador", "suspender el pc", "pon el pc en suspensión",
            "apaga el pc", "apaga el equipo", "apaga el ordenador", "apagar el pc",
            "confirma suspensión", "confirma la suspensión", "confirma apagado", "confirma el apagado",
            "cancela la acción", "cancela el apagado", "cancela apagado", "cancela la suspensión", "cancela suspensión",
            "bloquea el pc", "bloquea el equipo", "bloquea el ordenador"
        };
        public static string[] KnownCommands
        {
            get
            {
                return (string[])Commands.Clone();
            }
        }
        public static Grammar CreateCommandGrammar(CultureInfo culture)
        {
            return CreateGrammar(culture, Commands, CommandGrammarName);
        }
        public static string[] LauncherCommands()
        {
            HashSet<string> phrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AppEntry app in AppCatalog.GetEntries())
                foreach (string alias in app.Aliases)
                {
                    if (alias.Length > 70 || CommandParser.Parse("Vale, abre " + alias) == null) continue;
                    phrases.Add("abre " + alias);
                    phrases.Add("inicia " + alias);
                    phrases.Add("abre el " + alias);
                    phrases.Add("abre la " + alias);
                }
            foreach (WebsiteEntry site in WebsiteCatalog.GetEntries())
                foreach (string alias in site.Aliases)
                {
                    phrases.Add("abre " + alias + " en el navegador");
                    phrases.Add("abre " + alias + " en google chrome");
                    phrases.Add("abre " + alias + " en chrome");
                    if (CommandParser.Parse("Vale, abre " + alias) != null) phrases.Add("abre " + alias);
                }
            return phrases.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        public static Grammar CreateLauncherGrammar(CultureInfo culture)
        {
            return CreateGrammar(culture, LauncherCommands(), LauncherGrammarName);
        }
        private static Grammar CreateGrammar(CultureInfo culture, string[] phrases, string name)
        {
            GrammarBuilder builder = new GrammarBuilder { Culture = culture };
            // Model commands with and without a wake word, including common acoustic confusions.
            // This lets the engine report "abre..." or "dale..." instead of inventing "vale".
            // Accepts() still requires an actual leading Vale before anything can execute.
            builder.Append(new Choices("vale", "dale", "sale", "oye", "hola"), 0, 1);
            builder.Append("por favor", 0, 1);
            builder.Append(new Choices(phrases));
            builder.Append("por favor", 0, 1);
            return new Grammar(builder) { Name = name };
        }
        public static DictationGrammar CreateDictationGrammar()
        {
            // Unconstrained dictation competes with commands and never forces a Vale prefix into arbitrary audio.
            return new DictationGrammar { Name = DictationGrammarName };
        }
        public static bool Accepts(RecognitionResult result)
        {
            if (result == null || result.Words.Count == 0) return false;
            bool fixedCommand = result.Grammar != null && (result.Grammar.Name == CommandGrammarName || result.Grammar.Name == LauncherGrammarName);
            // Desktop Spanish assigns 0.44-0.52 to clean synthetic commands on this engine.
            // Keep stricter thresholds for free dictation, and verify the actual wake word independently.
            float minimum = fixedCommand ? .40f : .55f;
            float minimumWake = fixedCommand ? .45f : .55f;
            if (result.Confidence < minimum) return false;
            string command;
            if (!CommandParser.TryWake(result.Text, out command) || String.IsNullOrWhiteSpace(command)) return false;
            return String.Equals(result.Words[0].Text, "vale", StringComparison.OrdinalIgnoreCase) && result.Words[0].Confidence >= minimumWake;
        }
    }
}
