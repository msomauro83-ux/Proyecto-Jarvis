using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Jarvis
{
    public enum AssistantCommand
    {
        None,
        ShowWindow,
        HideWindow,
        PauseListening,
        CheckMicrophone,
        SuspendComputer,
        ShutdownComputer,
        ConfirmSuspend,
        ConfirmShutdown,
        CancelPowerAction,
        LockComputer
    }

    public sealed class CommandPlan
    {
        public string Action;
        public string Value;
        public string Description;
        public bool RequiresConfirmation;
    }

    public static class CommandParser
    {
        public static bool TryWake(string text, out string command)
        {
            command = String.Empty;
            if (String.IsNullOrWhiteSpace(text)) return false;
            int start = 0;
            while (start < text.Length && Char.IsWhiteSpace(text[start])) start++;
            if (text.Length - start < 4 || String.Compare(text, start, "Vale", 0, 4, StringComparison.OrdinalIgnoreCase) != 0) return false;
            int index = start + 4;
            if (index < text.Length && !Char.IsWhiteSpace(text[index]) && !IsSeparator(text[index])) return false;
            while (index < text.Length && (Char.IsWhiteSpace(text[index]) || IsSeparator(text[index]))) index++;
            command = text.Substring(index);
            return true;
        }

        private static bool IsSeparator(char c) { return ",:;.!?¡¿-–—".IndexOf(c) >= 0; }

        public static AssistantCommand ParseAssistantCommand(string text)
        {
            string command;
            if (text == null || text.Length > 18000 || !TryWake(text, out command)) return AssistantCommand.None;
            int[] offsets;
            string key = FixedKey(Fold(command, out offsets));
            key = Regex.Replace(key, @"^por\s+favor(?:\s*[,;:]\s*|\s+)", "", RegexOptions.CultureInvariant);
            if (key.EndsWith(" por favor", StringComparison.Ordinal)) key = FixedKey(key.Substring(0, key.Length - 10));
            switch (key)
            {
                case "abre jarvis": case "abre vale": case "muestra jarvis": case "muestra vale":
                case "abre tu ventana": case "muestra tu ventana": case "muestrate":
                    return AssistantCommand.ShowWindow;
                case "cierra jarvis": case "cierra vale": case "oculta jarvis": case "oculta vale":
                case "cierra tu ventana": case "oculta tu ventana": case "ocultate": case "cierrate":
                case "ve a la bandeja": case "minimizate":
                    return AssistantCommand.HideWindow;
                case "pausa el microfono": case "deja de escuchar": case "pausa": case "pausa la escucha":
                    return AssistantCommand.PauseListening;
                case "me escuchas": case "me escucha": case "puedes escucharme": case "estas ahi": case "hola":
                    return AssistantCommand.CheckMicrophone;
                case "suspende el pc": case "suspende el equipo": case "suspende el ordenador":
                case "suspender el pc": case "pon el pc en suspension":
                    return AssistantCommand.SuspendComputer;
                case "apaga el pc": case "apaga el equipo": case "apaga el ordenador": case "apagar el pc":
                    return AssistantCommand.ShutdownComputer;
                case "confirma suspension": case "confirma la suspension":
                    return AssistantCommand.ConfirmSuspend;
                case "confirma apagado": case "confirma el apagado":
                    return AssistantCommand.ConfirmShutdown;
                case "cancela la accion": case "cancela el apagado": case "cancela apagado":
                case "cancela la suspension": case "cancela suspension":
                    return AssistantCommand.CancelPowerAction;
                case "bloquea el pc": case "bloquea el equipo": case "bloquea el ordenador":
                    return AssistantCommand.LockComputer;
                default:
                    return AssistantCommand.None;
            }
        }

        public static CommandPlan Parse(string text)
        {
            if (text == null || text.Length > 18000) return null;
            string command;
            if (!TryWake(text, out command) || String.IsNullOrWhiteSpace(command)) return null;
            if (ParseAssistantCommand(text) != AssistantCommand.None) return null;
            Match please = Regex.Match(command, @"^por\s+favor(?:\s*[,;:]\s*|\s+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (please.Success) command = command.Substring(please.Length);
            int[] offsets;
            string folded = Fold(command, out offsets);
            string payload;
            if (TryPayload(command, folded, offsets, @"^(?:escribe|escribir|dicta|teclea)(?:\s*:\s*|\s+)", out payload)) return Plan("type_text", payload);
            if (Regex.IsMatch(folded, @"^(?:busca|buscar|buscame)\s+(?:en\s+)?(?:youtube|internet|google|la\s+web)\s*:?\s*$")) return null;
            if (TryPayload(command, folded, offsets, @"^(?:busca|buscar|buscame)\s+(?:en\s+)?youtube(?:\s*:\s*|\s+)", out payload)) return Plan("youtube_search", payload);
            if (TryPayload(command, folded, offsets, @"^(?:busca|buscar|buscame)\s+(?:en\s+)?(?:internet|google|la\s+web)(?:\s*:\s*|\s+)", out payload)) return Plan("web_search", payload);
            if (TryPayload(command, folded, offsets, @"^(?:busca|buscar|buscame)(?:\s*:\s*|\s+)", out payload)) return Plan("web_search", payload);
            string key = FixedKey(folded);
            if (key.EndsWith(" por favor", StringComparison.Ordinal)) key = FixedKey(key.Substring(0, key.Length - 10));
            Match open = Regex.Match(key, @"^(?:abre|abrir|inicia|iniciar|lanza|lanzar|ejecuta|ejecutar|pon|arranca) (?:(?:el|la|los|las|un|una) )?(.+)$");
            if (open.Success)
            {
                string requested = open.Groups[1].Value;
                Match chromeSuffix = Regex.Match(requested, @"\s+en\s+(?:(?:el\s+)?navegador\s+)?(?:google\s+)?chrome$");
                if (chromeSuffix.Success)
                {
                    string chromeSite = WebsiteCatalog.ResolveAlias(requested.Substring(0, chromeSuffix.Index).Trim());
                    return chromeSite == null ? null : Plan("open_website_chrome", chromeSite);
                }
                Match browserSuffix = Regex.Match(requested, @"\s+(?:en\s+(?:el\s+)?navegador|en\s+la\s+web|en\s+internet)$");
                bool inBrowser = browserSuffix.Success;
                if (inBrowser) requested = requested.Substring(0, browserSuffix.Index).Trim();
                if (!inBrowser) switch (requested)
                {
                    case "documentos": case "mis documentos": return Plan("open_folder", "documents");
                    case "descargas": case "mis descargas": return Plan("open_folder", "downloads");
                    case "escritorio": return Plan("open_folder", "desktop");
                }
                string website = WebsiteCatalog.ResolveAlias(requested);
                // This command explicitly opens the website, including on PCs
                // that also have a desktop app with the same name installed.
                if (website == "chatgpt") return Plan("open_website", website);
                string application = inBrowser ? null : AppCatalog.ResolveAlias(requested);
                if (application != null) return Plan("open_app", application);
                if (website != null) return Plan("open_website", website);
            }
            switch (key)
            {
                case "sube el volumen": case "sube volumen": case "subir el volumen": case "subir volumen": case "aumenta el volumen": case "mas volumen": return Plan("volume_up", "");
                case "baja el volumen": case "baja volumen": case "bajar el volumen": case "bajar volumen": case "reduce el volumen": case "menos volumen": return Plan("volume_down", "");
                case "silencio": case "silencia": case "silencia el audio": case "silencia el volumen": case "silenciar": case "quita el sonido": case "activa el sonido": case "activa el audio": case "mute": return Plan("volume_mute", "");
                case "pausa la musica": case "pausa la reproduccion": case "reanuda": case "reanuda la musica": case "reanuda la reproduccion": case "reproduce": case "reproduce la musica": case "reproduce o pausa": case "reproducir o pausar": return Plan("media_play_pause", "");
                case "siguiente": case "siguiente cancion": case "siguiente pista": case "pasa a la siguiente cancion": return Plan("media_next", "");
                case "anterior": case "cancion anterior": case "pista anterior": case "vuelve a la cancion anterior": return Plan("media_previous", "");
                case "muestra el escritorio": case "muestra escritorio": case "mostrar el escritorio": case "mostrar escritorio": case "ve al escritorio": return Plan("show_desktop", "");
                case "captura de pantalla": case "captura la pantalla": case "haz una captura": case "haz una captura de pantalla": case "toma una captura": case "toma una captura de pantalla": return Plan("screenshot", "");
                case "copia": case "copiar": case "copia la seleccion": case "copiar seleccion": return Plan("shortcut", "copy");
                case "pega": case "pegar": return Plan("shortcut", "paste");
                case "deshaz": case "deshacer": case "deshaz la ultima accion": return Plan("shortcut", "undo");
            }
            return null;
        }

        private static string FixedKey(string value) { return Regex.Replace(value, @"\s+", " ").Trim(' ', '\t', '\r', '\n', '.', ',', ';', ':', '!', '?', '¡', '¿'); }
        private static CommandPlan Plan(string action, string value)
        {
            try { return WindowsActions.CreatePlan(action, value); }
            catch (ArgumentException) { return null; }
        }
        private static bool TryPayload(string original, string folded, int[] offsets, string pattern, out string payload)
        {
            Match match = Regex.Match(folded, pattern, RegexOptions.CultureInvariant);
            payload = match.Success ? original.Substring(offsets[match.Length]) : null;
            return match.Success;
        }
        // Keep source offsets so accent folding never changes the dictated payload.
        private static string Fold(string original, out int[] originalOffsets)
        {
            StringBuilder result = new StringBuilder(original.Length);
            List<int> offsets = new List<int>(original.Length + 1);
            TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(original);
            while (elements.MoveNext())
            {
                int source = elements.ElementIndex;
                string element = elements.GetTextElement().Normalize(NormalizationForm.FormD);
                for (int i = 0; i < element.Length; i++)
                {
                    UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(element, i);
                    if (category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark || category == UnicodeCategory.EnclosingMark) continue;
                    result.Append(Char.ToLowerInvariant(element[i]));
                    offsets.Add(source);
                }
            }
            offsets.Add(original.Length);
            originalOffsets = offsets.ToArray();
            return result.ToString();
        }
    }

    // Confirmation belongs to the local app. It is not persisted, expires
    // after 30 seconds and can be consumed only once.
    internal sealed class PowerCommandConfirmation
    {
        private AssistantCommand pending;
        private DateTime expires;
        public void Request(AssistantCommand action, DateTime now)
        {
            if (action != AssistantCommand.SuspendComputer && action != AssistantCommand.ShutdownComputer) throw new ArgumentException("Acción de energía no válida.");
            pending = action;
            expires = now.AddSeconds(30);
        }
        public bool TryConfirm(AssistantCommand confirmation, DateTime now, out AssistantCommand action)
        {
            action = AssistantCommand.None;
            AssistantCommand expected = confirmation == AssistantCommand.ConfirmSuspend ? AssistantCommand.SuspendComputer : confirmation == AssistantCommand.ConfirmShutdown ? AssistantCommand.ShutdownComputer : AssistantCommand.None;
            bool matches = expected != AssistantCommand.None && pending == expected && now < expires && now >= expires.AddSeconds(-30);
            if (matches) action = pending;
            // A mismatched confirmation cancels the pending request as well.
            Cancel();
            return matches;
        }
        public void Cancel() { pending = AssistantCommand.None; expires = DateTime.MinValue; }
    }

    internal static class ComputerPower
    {
        internal static ProcessStartInfo CreateShutdownStartInfo()
        {
            // /t 0 avoids the implicit force-close associated with a positive
            // Windows shutdown timeout. No /f, remote target or shell is used.
            return new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "shutdown.exe"), "/s /t 0") { UseShellExecute = false, CreateNoWindow = true };
        }
        internal static string Execute(AssistantCommand action)
        {
            if (action == AssistantCommand.LockComputer)
            {
                if (!LockWorkStation()) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows no permitió bloquear la sesión.");
                return "Bloqueo de la sesión solicitado a Windows.";
            }
            if (action == AssistantCommand.SuspendComputer)
            {
                if (!Application.SetSuspendState(PowerState.Suspend, false, false)) throw new InvalidOperationException("Windows no permitió suspender el equipo. Revisa las opciones de energía.");
                return "El equipo ha vuelto de la suspensión. Reactiva el micrófono desde la bandeja para usar la voz.";
            }
            if (action == AssistantCommand.ShutdownComputer)
            {
                using (Process process = Process.Start(CreateShutdownStartInfo()))
                {
                    if (process == null || !process.WaitForExit(5000)) throw new InvalidOperationException("Windows no confirmó la solicitud de apagado.");
                    if (process.ExitCode != 0) throw new InvalidOperationException("Windows rechazó el apagado. Comprueba los permisos de tu usuario.");
                }
                return "Solicitud de apagado enviada a Windows.";
            }
            throw new ArgumentException("Acción de energía no válida.");
        }
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LockWorkStation();
    }

    public static class WindowsActions
    {
        internal static CommandPlan CreatePlan(string action, string value)
        {
            if (action == null || value == null) throw InvalidValue();
            string description;
            switch (action)
            {
                case "open_app":
                    if (!AppCatalog.IsKnownId(value)) throw InvalidValue();
                    description = AppCatalog.GetDisplayName(value) + " abierto.";
                    break;
                case "open_website":
                    if (!WebsiteCatalog.IsKnownId(value)) throw InvalidValue();
                    description = WebsiteCatalog.GetDisplayName(value) + " abierto en el navegador.";
                    break;
                case "open_website_chrome":
                    if (!WebsiteCatalog.IsKnownId(value)) throw InvalidValue();
                    description = WebsiteCatalog.GetDisplayName(value) + " abierto en Google Chrome.";
                    break;
                case "open_folder":
                    switch (value)
                    {
                        case "documents": description = "Carpeta Documentos abierta."; break;
                        case "downloads": description = "Carpeta Descargas abierta."; break;
                        case "desktop": description = "Carpeta Escritorio abierta."; break;
                        default: throw InvalidValue();
                    }
                    break;
                case "web_search": case "youtube_search":
                    ValidatePayload(value, 2048, false);
                    description = (action == "web_search" ? "Busqué en internet: " : "Busqué en YouTube: ") + value; break;
                case "type_text": ValidatePayload(value, 10000, true); description = "Texto escrito."; break;
                case "shortcut":
                    switch (value)
                    {
                        case "copy": description = "Copiar enviado."; break;
                        case "paste": description = "Pegar enviado."; break;
                        case "undo": description = "Deshacer enviado."; break;
                        default: throw InvalidValue();
                    }
                    break;
                case "volume_up": RequireEmpty(value); description = "Volumen subido."; break;
                case "volume_down": RequireEmpty(value); description = "Volumen bajado."; break;
                case "volume_mute": RequireEmpty(value); description = "Silencio del audio alternado."; break;
                case "media_play_pause": RequireEmpty(value); description = "Reproducir o pausar enviado."; break;
                case "media_next": RequireEmpty(value); description = "Siguiente pista enviado."; break;
                case "media_previous": RequireEmpty(value); description = "Pista anterior enviado."; break;
                case "show_desktop": RequireEmpty(value); description = "Vista del escritorio alternada."; break;
                case "screenshot": RequireEmpty(value); description = "Captura de pantalla."; break;
                default: throw new ArgumentException("La acción no está permitida.");
            }
            return new CommandPlan { Action = action, Value = value, Description = description, RequiresConfirmation = false };
        }
        private static ArgumentException InvalidValue() { return new ArgumentException("El valor no está permitido para esta acción."); }
        private static void RequireEmpty(string value) { if (value.Length != 0) throw InvalidValue(); }
        private static void ValidatePayload(string value, int maximum, bool text)
        {
            if (String.IsNullOrWhiteSpace(value) || value.Length > maximum) throw InvalidValue();
            foreach (char c in value)
                if (Char.IsControl(c) && !(text && (c == '\r' || c == '\n' || c == '\t'))) throw InvalidValue();
        }

        public static string Execute(CommandPlan plan)
        {
            if (plan == null) throw new ArgumentNullException("plan");
            CommandPlan permitted = CreatePlan(plan.Action, plan.Value);
            string value = permitted.Value;
            switch (permitted.Action)
            {
                case "open_app": return AppCatalog.Open(value);
                case "open_website": return WebsiteCatalog.Open(value);
                case "open_website_chrome": return AppCatalog.OpenWebsiteInChrome(value);
                case "open_folder": OpenFolder(value); break;
                case "web_search": OpenTarget("https://www.google.com/search?q=" + Uri.EscapeDataString(value)); break;
                case "youtube_search": OpenTarget("https://www.youtube.com/results?search_query=" + Uri.EscapeDataString(value)); break;
                case "type_text": TypeUnicode(value); break;
                case "shortcut": Combination(0x11, value == "copy" ? (ushort)0x43 : value == "paste" ? (ushort)0x56 : (ushort)0x5A); break;
                case "volume_up": Key(0xAF); break;
                case "volume_down": Key(0xAE); break;
                case "volume_mute": Key(0xAD); break;
                case "media_play_pause": Key(0xB3); break;
                case "media_next": Key(0xB0); break;
                case "media_previous": Key(0xB1); break;
                case "show_desktop": Combination(0x5B, 0x44); break;
                case "screenshot": return "Captura guardada en " + Screenshot();
            }
            return permitted.Description;
        }
        private static void OpenFolder(string folder)
        {
            string path;
            switch (folder)
            {
                case "documents": path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); break;
                case "desktop": path = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory); break;
                case "downloads": path = DownloadsFolder(); break;
                default: throw InvalidValue();
            }
            if (String.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || !Directory.Exists(path)) throw new DirectoryNotFoundException("No se encontró la carpeta solicitada.");
            OpenTarget(path);
        }
        private static string DownloadsFolder()
        {
            Guid id = new Guid("374DE290-123F-4565-9164-39C4925E467B");
            IntPtr buffer = IntPtr.Zero;
            try { if (SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out buffer) == 0 && buffer != IntPtr.Zero) return Marshal.PtrToStringUni(buffer); }
            finally { if (buffer != IntPtr.Zero) Marshal.FreeCoTaskMem(buffer); }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }
        private static void OpenTarget(string target)
        {
            using (Process process = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })) { }
        }
        private static string Screenshot()
        {
            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (String.IsNullOrWhiteSpace(pictures) || !Path.IsPathRooted(pictures)) throw new DirectoryNotFoundException("No se encontró la carpeta Imágenes.");
            string directory = Path.Combine(pictures, "Jarvis");
            Directory.CreateDirectory(directory);
            Rectangle bounds = SystemInformation.VirtualScreen;
            if (bounds.Width <= 0 || bounds.Height <= 0) throw new InvalidOperationException("No hay una pantalla disponible para capturar.");
            string path = Path.Combine(directory, "Captura-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N") + ".png");
            using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bitmap)) g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
                using (MemoryStream encoded = new MemoryStream())
                {
                    bitmap.Save(encoded, ImageFormat.Png);
                    encoded.Position = 0;
                    using (FileStream output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) encoded.CopyTo(output);
                }
            }
            return path;
        }
        private static void TypeUnicode(string text)
        {
            List<NativeInput> inputs = new List<NativeInput>(text.Length * 2);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n' && i > 0 && text[i - 1] == '\r') continue;
                if (c == '\r' || c == '\n' || c == '\t')
                {
                    ushort key = c == '\t' ? (ushort)0x09 : (ushort)0x0D;
                    inputs.Add(KeyInput(key, false)); inputs.Add(KeyInput(key, true));
                }
                else { inputs.Add(UnicodeInput(c, false)); inputs.Add(UnicodeInput(c, true)); }
            }
            Send(inputs.ToArray());
        }
        private static void Key(ushort key) { Send(new NativeInput[] { KeyInput(key, false), KeyInput(key, true) }); }
        private static void Combination(ushort modifier, ushort key) { Send(new NativeInput[] { KeyInput(modifier, false), KeyInput(key, false), KeyInput(key, true), KeyInput(modifier, true) }); }
        private static NativeInput KeyInput(ushort key, bool up)
        {
            NativeInput input = new NativeInput();
            input.Type = 1; input.Data.Keyboard.VirtualKey = key; input.Data.Keyboard.Flags = up ? 2U : 0U;
            if (key == 0x5B || key == 0x5C || (key >= 0xA6 && key <= 0xB7)) input.Data.Keyboard.Flags |= 1U;
            return input;
        }
        private static NativeInput UnicodeInput(char c, bool up)
        {
            NativeInput input = new NativeInput();
            input.Type = 1; input.Data.Keyboard.ScanCode = c; input.Data.Keyboard.Flags = 4U | (up ? 2U : 0U);
            return input;
        }
        private static void Send(NativeInput[] inputs)
        {
            if (inputs.Length != 0 && SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeInput))) != (uint)inputs.Length)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows no permitió enviar las teclas a la ventana activa.");
        }
        // Include the complete INPUT union so the size is correct on x64.
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeInput { public uint Type; public InputUnion Data; }
        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MouseInput Mouse;
            [FieldOffset(0)] public KeyboardInput Keyboard;
            [FieldOffset(0)] public HardwareInput Hardware;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput { public int X, Y; public uint MouseData, Flags, Time; public UIntPtr ExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput { public ushort VirtualKey, ScanCode; public uint Flags, Time; public UIntPtr ExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        private struct HardwareInput { public uint Message; public ushort ParameterLow, ParameterHigh; }
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, NativeInput[] inputs, int size);
        [DllImport("shell32.dll")] private static extern int SHGetKnownFolderPath(ref Guid id, uint flags, IntPtr token, out IntPtr path);
    }
}
