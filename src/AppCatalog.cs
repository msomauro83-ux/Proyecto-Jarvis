using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace Jarvis
{
    public sealed class AppEntry
    {
        public string Id;
        public string DisplayName;
        public string[] Aliases;
        public bool IsAvailable;
    }

    // Only identifiers from this catalogue cross the parser/execution boundary. A
    // discovered launch target comes from an existing Windows shortcut or AUMID,
    // never from an executable path supplied by a spoken or written command.
    public static class AppCatalog
    {
        private sealed class Target
        {
            public string Executable, Arguments, Shortcut, Aumid, Uri;
        }
        private sealed class Record
        {
            public AppEntry Entry;
            public Target Target;
            public bool Discovered;
        }
        private static readonly object Gate = new object();
        private static List<Record> records;

        public static void WarmUp()
        {
            lock (Gate)
            {
                if (records != null) return;
                records = DiscoverSnapshot();
            }
        }
        public static void Refresh() { lock (Gate) records = DiscoverSnapshot(); }
        private static List<Record> DiscoverSnapshot()
        {
            List<Record> result = CreateKnown();
            // Windows shell automation works reliably in an STA, including
            // when the UI refreshes the catalogue from Task.Run.
            Thread discovery = new Thread(delegate() { try { Discover(result); } catch (Exception ex) { if (ex is OutOfMemoryException) throw; } });
            discovery.IsBackground = true;
            discovery.SetApartmentState(ApartmentState.STA);
            discovery.Start();
            // A timed-out worker owns only its unpublished snapshot; it can
            // never continue mutating the catalogue currently used by callers.
            return discovery.Join(10000) ? result : CreateKnown();
        }
        public static AppEntry[] GetEntries()
        {
            WarmUp();
            lock (Gate)
            {
                List<AppEntry> result = new List<AppEntry>();
                foreach (Record record in records) result.Add(Clone(record.Entry));
                return result.ToArray();
            }
        }
        public static string ResolveAlias(string name) { return ResolveAlias(GetEntries(), name); }
        internal static string ResolveAlias(IEnumerable<AppEntry> entries, string name)
        {
            string key = NormalizeAlias(name);
            if (key.Length == 0) return null;
            string resolved = null;
            foreach (AppEntry entry in entries)
            {
                bool matches = NormalizeAlias(entry.DisplayName) == key;
                foreach (string alias in entry.Aliases ?? new string[0]) if (NormalizeAlias(alias) == key) matches = true;
                if (!matches) continue;
                if (resolved != null && !String.Equals(resolved, entry.Id, StringComparison.Ordinal)) return null;
                resolved = entry.Id;
            }
            return resolved;
        }
        public static bool IsKnownId(string id)
        {
            WarmUp();
            lock (Gate) return Find(id) != null;
        }
        public static string GetDisplayName(string id)
        {
            WarmUp();
            lock (Gate) { Record record = Find(id); return record == null ? null : record.Entry.DisplayName; }
        }
        public static string Open(string id)
        {
            Start(CreateLaunchInfo(id));
            return GetDisplayName(id) + " abierto.";
        }
        internal static ProcessStartInfo CreateLaunchInfo(string id)
        {
            WarmUp();
            Record record;
            lock (Gate) record = Find(id);
            if (record == null) throw new ArgumentException("La aplicación no pertenece al catálogo de Jarvis.");
            Target target = record.Target;
            if (target == null) throw new FileNotFoundException("No encontré " + record.Entry.DisplayName + " instalada. Instálala y pulsa Actualizar catálogo en Aplicaciones.");
            if (target.Shortcut != null)
            {
                // Re-resolve the shortcut immediately before launch. Replacing a
                // shortcut with a shell/script cannot change the allowed action.
                Target current = ReadShortcut(target.Shortcut);
                if (current == null || !String.Equals(current.Executable, target.Executable, StringComparison.OrdinalIgnoreCase) || current.Arguments != target.Arguments)
                    throw new InvalidOperationException("El acceso directo cambió. Pulsa Actualizar catálogo en Aplicaciones.");
            }
            if (target.Executable != null)
            {
                bool fixedSystemTool = !record.Discovered && IsFixedSystemToolTarget(id, target.Executable, target.Arguments);
                if ((!fixedSystemTool && !IsSafeShortcutTarget(target.Executable, target.Arguments)) || !File.Exists(target.Executable)) throw new FileNotFoundException("Ya no está disponible " + record.Entry.DisplayName + ".");
                // An already running Steam client can ignore a second bare exe launch.
                // Ask that same executable to show its main window explicitly.
                // This PC's Steam UI failed with GPU acceleration. Keep the
                // confirmed workaround on launches from Jarvis; no game or
                // account settings are changed and existing games are not restarted.
                string arguments = LaunchArguments(id, target.Executable, target.Arguments);
                return new ProcessStartInfo(target.Executable, arguments) { UseShellExecute = true, WorkingDirectory = fixedSystemTool ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : Path.GetDirectoryName(target.Executable) };
            }
            else if (target.Aumid != null)
            {
                if (!IsAumid(target.Aumid)) throw new ArgumentException("La aplicación de Windows no es válida.");
                return new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), "shell:AppsFolder\\" + target.Aumid) { UseShellExecute = true };
            }
            else if (target.Uri == "ms-settings:" || target.Uri == "https://www.google.com/") return new ProcessStartInfo(target.Uri) { UseShellExecute = true };
            else throw new ArgumentException("El destino de la aplicación no está permitido.");
        }
        internal static string LaunchArguments(string id, string executable, string arguments)
        {
            if (id == "steam") return "-cef-disable-gpu -- steam://open/main";
            // Opening a streaming app never authorizes starting a broadcast or
            // recording, even if a discovered shortcut contains startup flags.
            string name = Path.GetFileName(executable);
            if (id == "obs" || id == "tiktok_live_studio" || String.Equals(name, "obs64.exe", StringComparison.OrdinalIgnoreCase) || String.Equals(name, "obs32.exe", StringComparison.OrdinalIgnoreCase) || String.Equals(name, "TikTok LIVE Studio Launcher.exe", StringComparison.OrdinalIgnoreCase)) return "";
            return arguments ?? "";
        }
        public static string OpenWebsiteInChrome(string websiteId)
        {
            Start(CreateChromeWebsiteLaunchInfo(websiteId));
            return WebsiteCatalog.GetDisplayName(websiteId) + " abierto en Google Chrome.";
        }
        internal static ProcessStartInfo CreateChromeWebsiteLaunchInfo(string websiteId)
        {
            string url = WebsiteCatalog.GetUrl(websiteId);
            ProcessStartInfo chrome = CreateLaunchInfo("chrome");
            if (!String.Equals(Path.GetFileName(chrome.FileName), "chrome.exe", StringComparison.OrdinalIgnoreCase))
                throw new FileNotFoundException("No encontré Google Chrome instalado. Instálalo y pulsa Actualizar catálogo.");
            // The URL is from the fixed website catalogue, never from tool arguments.
            chrome.Arguments = "--new-tab \"" + url + "\"";
            return chrome;
        }
        private static void Start(ProcessStartInfo start) { using (Process process = Process.Start(start)) { } }
        private static Record Find(string id)
        {
            if (String.IsNullOrEmpty(id)) return null;
            foreach (Record record in records) if (String.Equals(record.Entry.Id, id, StringComparison.Ordinal)) return record;
            return null;
        }
        private static AppEntry Clone(AppEntry entry) { return new AppEntry { Id = entry.Id, DisplayName = entry.DisplayName, Aliases = (string[])entry.Aliases.Clone(), IsAvailable = entry.IsAvailable }; }
        private static List<Record> CreateKnown()
        {
            List<Record> list = new List<Record>();
            Add(list, "calculator", "Calculadora", new string[] { "calculadora", "calculator" }, Windows("calc.exe"));
            Add(list, "notepad", "Bloc de notas", new string[] { "bloc de notas", "notepad" }, Windows("notepad.exe"));
            Add(list, "explorer", "Explorador de archivos", new string[] { "explorador", "explorador de archivos", "archivos", "este equipo" }, Executable(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), ""));
            Add(list, "browser", "Navegador", new string[] { "navegador", "navegador web", "internet", "google" }, new Target { Uri = "https://www.google.com/" });
            Add(list, "settings", "Configuración de Windows", new string[] { "configuracion", "configuracion de windows", "ajustes", "ajustes de windows" }, new Target { Uri = "ms-settings:" });
            Add(list, "control_panel", "Panel de control", new string[] { "panel de control", "panel de control de windows" }, Windows("control.exe"));
            Add(list, "cmd", "CMD", new string[] { "cmd", "ce eme de", "c m d", "símbolo del sistema", "consola de comandos" }, Windows("cmd.exe"));
            Add(list, "powershell", "Windows PowerShell", new string[] { "powershell", "windows powershell", "power shell" }, Windows(@"WindowsPowerShell\v1.0\powershell.exe"));
            Add(list, "chrome", "Google Chrome", new string[] { "chrome", "google chrome" }, RegisteredProgram("chrome.exe") ?? Program("Google\\Chrome\\Application\\chrome.exe"));
            Add(list, "edge", "Microsoft Edge", new string[] { "edge", "microsoft edge" }, RegisteredProgram("msedge.exe") ?? Program("Microsoft\\Edge\\Application\\msedge.exe"));
            Add(list, "opera", "Opera GX", new string[] { "opera", "opera gx", "navegador opera", "navegador opera gx" }, LocalProgram("Opera GX\\opera.exe"));
            Add(list, "firefox", "Mozilla Firefox", new string[] { "firefox", "mozilla firefox" }, Program("Mozilla Firefox\\firefox.exe"));
            Add(list, "paint", "Paint", new string[] { "paint", "microsoft paint" }, Windows("mspaint.exe"));
            Add(list, "spotify", "Spotify", new string[] { "spotify", "aplicacion spotify" }, Executable(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spotify\\Spotify.exe"), ""));
            Add(list, "electroplay", "Electro Play Future", new string[] { "electro play", "electroplay", "electro play future", "electroplayfuture", "electro" }, LocalProgram("Electro Play Future\\ElectroPlayFuture.exe"));
            Add(list, "league_of_legends", "League of Legends", new string[] { "lol", "el lol", "league of legends", "cliente de lol", "cliente del lol", "cliente lol", "cliente de league of legends", "league" }, Riot("--launch-product=league_of_legends --launch-patchline=live"));
            Add(list, "meta_lol", "Meta LoL", new string[] { "meta lol", "metalol", "meta de lol", "meta del lol", "asistente de lol" }, RegisteredProgram("MetaLol.Desktop.exe") ?? LocalProgram("Meta LoL\\MetaLol.Desktop.exe") ?? Program("Meta LoL\\MetaLol.Desktop.exe"));
            Add(list, "riot", "Riot Client", new string[] { "riot", "riot client", "cliente riot", "cliente de riot" }, Riot(""));
            Add(list, "valorant", "VALORANT", new string[] { "valorant" }, Riot("--launch-product=valorant --launch-patchline=live"));
            Add(list, "tft", "Teamfight Tactics", new string[] { "tft", "teamfight tactics" }, null);
            Add(list, "steam", "Steam", new string[] { "steam" }, SteamProgram() ?? Program("Steam\\steam.exe"));
            Add(list, "tightvnc", "TightVNC Viewer", new string[] { "segundo pc", "segundo equipo", "tightvnc viewer", "tightvnc", "visor vnc", "tvnviewer" }, RegisteredProgram("tvnviewer.exe") ?? Program("TightVNC\\tvnviewer.exe"));
            Add(list, "discord", "Discord", new string[] { "discord" }, null);
            Add(list, "vscode", "Visual Studio Code", new string[] { "visual studio code", "vs code", "vscode", "code", "uve ese code", "editor de código" }, RegisteredProgram("Code.exe") ?? LocalProgram("Microsoft VS Code\\Code.exe") ?? Program("Microsoft VS Code\\Code.exe"));
            Add(list, "word", "Microsoft Word", new string[] { "word", "microsoft word" }, Office("WINWORD.EXE"));
            Add(list, "excel", "Microsoft Excel", new string[] { "excel", "microsoft excel" }, Office("EXCEL.EXE"));
            Add(list, "powerpoint", "Microsoft PowerPoint", new string[] { "powerpoint", "power point", "microsoft powerpoint" }, Office("POWERPNT.EXE"));
            Add(list, "outlook", "Microsoft Outlook", new string[] { "outlook", "microsoft outlook" }, Office("OUTLOOK.EXE"));
            Add(list, "obs", "OBS Studio", new string[] { "obs", "obs studio", "o be ese", "o b s", "estudio obs", "estudio de grabación" }, RegisteredProgram("obs64.exe") ?? Program("obs-studio\\bin\\64bit\\obs64.exe") ?? LocalProgram("obs-studio\\bin\\64bit\\obs64.exe"));
            Add(list, "tiktok_live_studio", "TikTok LIVE Studio", new string[] { "tiktok live studio", "tik tok live studio", "tiktok", "tik tok", "tiktok live", "tik tok live", "live studio", "estudio de tiktok" }, RegisteredProgram("TikTok LIVE Studio Launcher.exe") ?? Program("TikTok LIVE Studio\\TikTok LIVE Studio Launcher.exe") ?? LocalProgram("TikTok LIVE Studio\\TikTok LIVE Studio Launcher.exe"));
            Add(list, "vlc", "VLC", new string[] { "vlc", "vlc media player" }, Program("VideoLAN\\VLC\\vlc.exe"));
            Add(list, "snipping_tool", "Recortes", new string[] { "recortes", "herramienta recortes", "herramienta de recortes", "snipping tool" }, Windows("SnippingTool.exe"));
            Add(list, "task_manager", "Administrador de tareas", new string[] { "administrador de tareas", "task manager" }, Windows("Taskmgr.exe"));
            return list;
        }
        private static void Add(List<Record> list, string id, string display, string[] aliases, Target target)
        {
            list.Add(new Record { Entry = new AppEntry { Id = id, DisplayName = display, Aliases = aliases, IsAvailable = target != null }, Target = target });
        }
        // These built-ins open a window only, with no transcript-derived arguments.
        // Discovered shortcuts still cannot introduce interpreters or control.exe.
        internal static bool IsFixedSystemToolTarget(string id, string executable, string arguments)
        {
            string relative = id == "cmd" ? "cmd.exe" : id == "powershell" ? @"WindowsPowerShell\v1.0\powershell.exe" : id == "control_panel" ? "control.exe" : null;
            return relative != null && arguments == "" && String.Equals(executable, Path.Combine(Environment.SystemDirectory, relative), StringComparison.OrdinalIgnoreCase);
        }
        private static Target Windows(string name) { return Executable(Path.Combine(Environment.SystemDirectory, name), ""); }
        private static Target Executable(string path, string arguments) { return !String.IsNullOrEmpty(path) && File.Exists(path) ? new Target { Executable = path, Arguments = arguments } : null; }
        private static Target Program(string relative)
        {
            foreach (Environment.SpecialFolder folder in new Environment.SpecialFolder[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.LocalApplicationData })
            {
                string root = Environment.GetFolderPath(folder);
                Target target = String.IsNullOrEmpty(root) ? null : Executable(Path.Combine(root, relative), "");
                if (target != null) return target;
            }
            return null;
        }
        private static Target LocalProgram(string relative)
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return String.IsNullOrEmpty(root) ? null : Executable(Path.Combine(root, "Programs", relative), "");
        }
        private static Target Office(string name)
        {
            return RegisteredProgram(name) ?? Program("Microsoft Office\\root\\Office16\\" + name) ??
                Program("Microsoft Office\\Office16\\" + name) ?? Program("Microsoft Office\\Office15\\" + name) ?? Program("Microsoft Office\\Office14\\" + name);
        }
        internal static string RegisteredExecutable(string value, string expectedName)
        {
            if (String.IsNullOrWhiteSpace(value)) return null;
            string path = Environment.ExpandEnvironmentVariables(value.Trim());
            if (path.Length > 1 && path[0] == '"' && path[path.Length - 1] == '"') path = path.Substring(1, path.Length - 2);
            return IsSafeShortcutTarget(path, "") && String.Equals(Path.GetFileName(path), expectedName, StringComparison.OrdinalIgnoreCase) ? path : null;
        }
        private static Target RegisteredProgram(string name)
        {
            foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
                foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                    try
                    {
                        using (RegistryKey root = RegistryKey.OpenBaseKey(hive, view))
                        using (RegistryKey key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\" + name))
                        {
                            string path = key == null ? null : RegisteredExecutable(key.GetValue("") as string, name);
                            Target target = Executable(path, "");
                            if (target != null) return target;
                        }
                    }
                    catch (System.Security.SecurityException) { }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
            return null;
        }
        private static Target SteamProgram()
        {
            Target registered = RegisteredProgram("steam.exe");
            if (registered != null) return registered;
            foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
                foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                    try
                    {
                        using (RegistryKey root = RegistryKey.OpenBaseKey(hive, view))
                        using (RegistryKey key = root.OpenSubKey(@"Software\Valve\Steam"))
                        {
                            if (key == null) continue;
                            string path = RegisteredExecutable(key.GetValue("SteamExe") as string, "steam.exe");
                            string directory = key.GetValue("InstallPath") as string ?? key.GetValue("SteamPath") as string;
                            if (path == null && !String.IsNullOrWhiteSpace(directory)) path = RegisteredExecutable(Path.Combine(directory, "steam.exe"), "steam.exe");
                            Target target = Executable(path, "");
                            if (target != null) return target;
                        }
                    }
                    catch (System.Security.SecurityException) { }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                    catch (ArgumentException) { }
            return null;
        }
        private static Target Riot(string arguments) { return Executable(Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "Riot Games", "Riot Client", "RiotClientServices.exe"), arguments); }
        private static void Discover(List<Record> list)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Environment.SpecialFolder folder in new Environment.SpecialFolder[] { Environment.SpecialFolder.Programs, Environment.SpecialFolder.CommonPrograms, Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.CommonDesktopDirectory })
            {
                string root = Environment.GetFolderPath(folder);
                if (!String.IsNullOrEmpty(root)) DiscoverShortcuts(list, root, seen, 0);
            }
            DiscoverPackagedApps(list);
        }
        private static void DiscoverShortcuts(List<Record> list, string directory, HashSet<string> seen, int depth)
        {
            if (depth > 6 || !Directory.Exists(directory)) return;
            if (Regex.IsMatch(Path.GetFileName(directory), @"^(?:Administrative Tools|Herramientas administrativas)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return;
            try
            {
                foreach (string path in Directory.GetFiles(directory, "*.lnk"))
                {
                    if (!seen.Add(path)) continue;
                    string name = Path.GetFileNameWithoutExtension(path);
                    if (!IsSafeDisplayName(name)) continue;
                    Target target = ReadShortcut(path);
                    if (target != null) Register(list, name, target, target.Executable + "|" + target.Arguments);
                }
                foreach (string child in Directory.GetDirectories(directory))
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) DiscoverShortcuts(list, child, seen, depth + 1);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        private static Target ReadShortcut(string path)
        {
            if (!File.Exists(path) || !String.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase)) return null;
            object shell = null, link = null;
            try
            {
                Type type = Type.GetTypeFromProgID("WScript.Shell");
                if (type == null) return null;
                shell = Activator.CreateInstance(type);
                link = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                string executable = Convert.ToString(link.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, link, null), CultureInfo.InvariantCulture);
                string arguments = Convert.ToString(link.GetType().InvokeMember("Arguments", BindingFlags.GetProperty, null, link, null), CultureInfo.InvariantCulture);
                if (!IsSafeShortcutTarget(executable, arguments) || !File.Exists(executable)) return null;
                return new Target { Executable = executable, Arguments = arguments, Shortcut = path };
            }
            catch (Exception ex) { if (ex is OutOfMemoryException) throw; return null; }
            finally { Release(link); Release(shell); }
        }
        private static void DiscoverPackagedApps(List<Record> list)
        {
            object shell = null, folder = null, items = null;
            try
            {
                Type type = Type.GetTypeFromProgID("Shell.Application");
                if (type == null) return;
                shell = Activator.CreateInstance(type);
                folder = shell.GetType().InvokeMember("NameSpace", BindingFlags.InvokeMethod, null, shell, new object[] { "shell:AppsFolder" });
                if (folder == null) return;
                items = folder.GetType().InvokeMember("Items", BindingFlags.InvokeMethod, null, folder, null);
                int count = Convert.ToInt32(items.GetType().InvokeMember("Count", BindingFlags.GetProperty, null, items, null), CultureInfo.InvariantCulture);
                for (int i = 0; i < Math.Min(count, 1500); i++)
                {
                    object item = null;
                    try
                    {
                        item = items.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, items, new object[] { i });
                        string name = Convert.ToString(item.GetType().InvokeMember("Name", BindingFlags.GetProperty, null, item, null), CultureInfo.InvariantCulture);
                        string aumid = Convert.ToString(item.GetType().InvokeMember("Path", BindingFlags.GetProperty, null, item, null), CultureInfo.InvariantCulture);
                        if (IsSafeDisplayName(name) && IsAumid(aumid)) Register(list, name, new Target { Aumid = aumid }, aumid);
                    }
                    catch (Exception ex) { if (ex is OutOfMemoryException) throw; }
                    finally { Release(item); }
                }
            }
            catch (Exception ex) { if (ex is OutOfMemoryException) throw; }
            finally { Release(items); Release(folder); Release(shell); }
        }
        private static void Register(List<Record> list, string name, Target target, string identity)
        {
            string key = NormalizeAlias(name);
            foreach (Record existing in list)
            {
                bool matches = NormalizeAlias(existing.Entry.DisplayName) == key;
                foreach (string alias in existing.Entry.Aliases) if (NormalizeAlias(alias) == key) matches = true;
                if (matches)
                {
                    if (existing.Target == null) { existing.Target = target; existing.Entry.IsAvailable = true; existing.Discovered = true; return; }
                    if (SameTarget(existing.Target, target)) return;
                    // A known, fixed install location is deliberate. Conflicting
                    // discovered apps remain separate so ResolveAlias refuses
                    // their shared name instead of silently selecting one.
                    if (!existing.Discovered) return;
                }
                if (SameTarget(existing.Target, target)) return;
            }
            string id;
            using (SHA256 hash = SHA256.Create()) id = "installed_" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity.ToLowerInvariant())), 0, 10).Replace("-", "").ToLowerInvariant();
            List<string> aliases = new List<string>();
            aliases.Add(name);
            if (target.Executable != null)
            {
                string executableName = Path.GetFileNameWithoutExtension(target.Executable);
                if (executableName.Length > 2 && Regex.IsMatch(executableName, @"^[A-Za-z0-9_-]+$") && !Regex.IsMatch(executableName, @"^(?:update|launcher|main|start|app|application|client|game|bootstrap|bootstrapper)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) aliases.Add(executableName);
            }
            Add(list, id, name, aliases.ToArray(), target);
            list[list.Count - 1].Discovered = true;
        }
        private static bool SameTarget(Target first, Target second)
        {
            if (first == null || second == null) return false;
            if (first.Executable != null && second.Executable != null) return String.Equals(first.Executable, second.Executable, StringComparison.OrdinalIgnoreCase) && first.Arguments == second.Arguments;
            return first.Aumid != null && first.Aumid == second.Aumid || first.Uri != null && first.Uri == second.Uri;
        }
        internal static string NormalizeAlias(string name)
        {
            if (String.IsNullOrWhiteSpace(name) || name.Length > 160) return "";
            StringBuilder folded = new StringBuilder();
            foreach (char c in name.Normalize(NormalizationForm.FormD)) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) folded.Append(Char.ToLowerInvariant(c));
            return Regex.Replace(folded.ToString(), @"\s+", " ").Trim();
        }
        internal static bool IsSafeShortcutTarget(string executable, string arguments)
        {
            if (String.IsNullOrWhiteSpace(executable) || executable.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || !Regex.IsMatch(executable, @"^[A-Za-z]:[\\/]") || !Path.IsPathRooted(executable) || executable.StartsWith(@"\\", StringComparison.Ordinal) || !String.Equals(Path.GetExtension(executable), ".exe", StringComparison.OrdinalIgnoreCase)) return false;
            string name = Path.GetFileNameWithoutExtension(executable).ToLowerInvariant();
            if (Regex.IsMatch(name, @"^(?:cmd|powershell|powershell_ise|pwsh|wscript|cscript|mshta|rundll32|regsvr32|regedit|shutdown|taskkill|diskpart|format|bcdedit|schtasks|sc|net|netsh|wmic|msiexec|installutil|reg|csc|vbc|csi|msbuild|dotnet|sqlcmd|osql|python(?:w|\d*)?|py|node|bash|sh|wsl|wt|conhost|git-bash|git-cmd|control|cleanmgr|dfrgui|mdsched|iscsicpl|odbcad32|msconfig|recoverydrive|rstrui|slui|optionalfeatures|dism|sfc)$") || Regex.IsMatch(name, @"(?:unins|uninstall|installer|setup|hibernat|hibernate)")) return false;
            string args = arguments ?? "";
            if (name == "explorer" && args.Length != 0) return false;
            return args.Length <= 2048 && !Regex.IsMatch(args, @"[\x00-\x1f<>|&;`]|–encodedcommand|(?:^|\s)(?:--?uninstall|/uninstall|/s(?:hutdown)?|--?eval|--?command|--?execute|--?encodedcommand)(?:\s|=|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        private static bool IsSafeDisplayName(string name)
        {
            string key = NormalizeAlias(name);
            return key.Length > 1 && !Regex.IsMatch(key, @"(?:uninstall|desinstal|instala|setup|hibern|apagar|shutdown|terminal|powershell|command prompt|simbolo del sistema|git bash|git cmd|consola|restablecer|resetear|registry editor|editor del registro|administrative tools|herramientas administrativas|run$|^ejecutar$)");
        }
        private static bool IsAumid(string value) { return !String.IsNullOrEmpty(value) && Regex.IsMatch(value, @"^[A-Za-z0-9][A-Za-z0-9._-]{2,180}![A-Za-z0-9._-]{1,120}$", RegexOptions.CultureInvariant); }
        private static void Release(object value) { if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
    }

    public sealed class WebsiteEntry
    {
        public string Id, DisplayName, Url;
        public string[] Aliases;
    }
    public static class WebsiteCatalog
    {
        private static readonly WebsiteEntry[] Sites = {
            Site("chatgpt", "ChatGPT", "https://chatgpt.com/", "chatgpt", "chat gpt", "chat g p t", "chat ge pe te"),
            Site("twitch", "Twitch", "https://www.twitch.tv/", "twitch"),
            Site("youtube", "YouTube", "https://www.youtube.com/", "youtube", "you tube"),
            Site("spotify", "Spotify web", "https://open.spotify.com/", "spotify", "spotify web"),
            Site("netflix", "Netflix", "https://www.netflix.com/", "netflix"),
            Site("gmail", "Gmail", "https://mail.google.com/", "gmail", "correo de google"),
            Site("maps", "Google Maps", "https://maps.google.com/", "maps", "google maps", "mapas"),
            Site("google", "Google", "https://www.google.com/", "google"),
            Site("whatsapp", "WhatsApp web", "https://web.whatsapp.com/", "whatsapp", "whatsapp web"),
            Site("discord", "Discord web", "https://discord.com/app", "discord", "discord web"),
            Site("reddit", "Reddit", "https://www.reddit.com/", "reddit"),
            Site("facebook", "Facebook", "https://www.facebook.com/", "facebook"),
            Site("instagram", "Instagram", "https://www.instagram.com/", "instagram"),
            Site("x", "X", "https://x.com/", "twitter", "x"),
            Site("prime_video", "Prime Video", "https://www.primevideo.com/", "prime video", "amazon prime video"),
            Site("disney_plus", "Disney Plus", "https://www.disneyplus.com/", "disney plus", "disney"),
            Site("kick", "Kick", "https://kick.com/", "kick"),
            Site("github", "GitHub", "https://github.com/", "github", "git hub"),
            Site("wikipedia", "Wikipedia", "https://es.wikipedia.org/", "wikipedia")
        };
        private static WebsiteEntry Site(string id, string name, string url, params string[] aliases) { return new WebsiteEntry { Id = id, DisplayName = name, Url = url, Aliases = aliases }; }
        public static WebsiteEntry[] GetEntries()
        {
            List<WebsiteEntry> result = new List<WebsiteEntry>();
            foreach (WebsiteEntry site in Sites) result.Add(new WebsiteEntry { Id = site.Id, DisplayName = site.DisplayName, Url = site.Url, Aliases = (string[])site.Aliases.Clone() });
            return result.ToArray();
        }
        public static string ResolveAlias(string name)
        {
            string key = AppCatalog.NormalizeAlias(name);
            foreach (WebsiteEntry site in Sites) foreach (string alias in site.Aliases) if (AppCatalog.NormalizeAlias(alias) == key) return site.Id;
            return null;
        }
        public static bool IsKnownId(string id) { return Find(id) != null; }
        public static string GetDisplayName(string id) { WebsiteEntry site = Find(id); return site == null ? null : site.DisplayName; }
        internal static string GetUrl(string id)
        {
            WebsiteEntry site = Find(id);
            if (site == null) throw new ArgumentException("El sitio no pertenece al catálogo de Jarvis.");
            return site.Url;
        }
        private static WebsiteEntry Find(string id) { foreach (WebsiteEntry site in Sites) if (site.Id == id) return site; return null; }
        public static string Open(string id)
        {
            WebsiteEntry site = Find(id);
            if (site == null) throw new ArgumentException("El sitio no pertenece al catálogo de Jarvis.");
            using (Process process = Process.Start(new ProcessStartInfo(site.Url) { UseShellExecute = true })) { }
            return site.DisplayName + " abierto en el navegador.";
        }
    }
}
