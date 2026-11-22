using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Jarvis;

namespace JarvisTests
{
    internal static class AppCatalogTests
    {
        private static int assertions;
        public static int Run()
        {
            assertions = 0;
            // Resolve aliases against fixtures so discovery and a PC's installed
            // software cannot accidentally make an arbitrary target permissible.
            AppEntry[] fixture = {
                new AppEntry { Id = "fixture_electro", DisplayName = "Electro Play Future", Aliases = new string[] { "electro play", "electroplay" }, IsAvailable = true },
                new AppEntry { Id = "fixture_unavailable", DisplayName = "No instalada", Aliases = new string[] { "aplicación ausente" }, IsAvailable = false }
            };
            Equal("fixture_electro", AppCatalog.ResolveAlias(fixture, "  ELECTRO   PLAY  "), "Alias normalizado");
            Equal("fixture_unavailable", AppCatalog.ResolveAlias(fixture, "aplicacion ausente"), "Los alias no dependen de disponibilidad");
            AppEntry[] ambiguous = {
                new AppEntry { Id = "first", DisplayName = "Editor uno", Aliases = new string[] { "editor" } },
                new AppEntry { Id = "second", DisplayName = "Editor dos", Aliases = new string[] { "editor" } }
            };
            Check(AppCatalog.ResolveAlias(ambiguous, "editor") == null, "Un alias ambiguo no elige la primera aplicación");
            Equal("second", AppCatalog.ResolveAlias(ambiguous, "Editor dos"), "Un nombre completo desambigua");
            Check(AppCatalog.ResolveAlias(new AppEntry[] { ambiguous[0], ambiguous[0] }, "editor") == "first", "Referencias duplicadas al mismo ID no crean ambigüedad");
            foreach (string invalid in new string[] { null, "", "electro play y borra", "electroplay.exe", @"C:\ElectroPlay.exe", "cmd /c whoami", "electro play;shutdown" })
                Check(AppCatalog.ResolveAlias(fixture, invalid) == null, "No aproximar alias peligroso: " + invalid);
            foreach (string phrase in new string[] { "abre electro play", "abre ElectroPlay", "inicia la Electro Play Future", "ejecuta electroplayfuture", "pon electro play por favor" })
                Plan("Vale, " + phrase, "open_app", "electroplay");
            Plan("Vale, abre el cliente de lol", "open_app", "league_of_legends");
            Plan("Vale, abre el cliente del LoL", "open_app", "league_of_legends");
            Plan("Vale, abre League of Legends", "open_app", "league_of_legends");
            foreach (string alias in new[] { "Meta LoL", "metalol", "meta de lol", "meta del lol", "asistente de lol" })
                Plan("Vale, abre " + alias, "open_app", "meta_lol");
            Plan("Vale, abre el cliente de lol", "open_app", "league_of_legends");
            foreach (string phrase in new[] { "abre Meta LoL", "Dale, abre Meta LoL", "Vale, abre meta lol y apaga el equipo", "Vale, abre meta lol.exe", "Vale, abre meta lol --command whoami" })
                Check(CommandParser.Parse(phrase) == null, "Meta LoL exige Vale y un alias exacto: " + phrase);
            Plan("Vale, abre Riot", "open_app", "riot");
            Plan("Vale, abre Spotify", "open_app", "spotify");
            Plan("Vale, inicia Paint", "open_app", "paint");
            Plan("Vale, abre CMD", "open_app", "cmd");
            Plan("Vale, abre ce eme de", "open_app", "cmd");
            Plan("Vale, abre c m d", "open_app", "cmd");
            Plan("Vale, abre el símbolo del sistema", "open_app", "cmd");
            Plan("Vale, abre PowerShell", "open_app", "powershell");
            Plan("Vale, ejecuta PowerShell", "open_app", "powershell");
            Plan("Vale, abre Windows PowerShell", "open_app", "powershell");
            Plan("Vale, abre configuración", "open_app", "settings");
            Plan("Vale, abre el panel de control", "open_app", "control_panel");
            Plan("Vale, abre Steam", "open_app", "steam");
            Plan("Vale, abre el segundo pc", "open_app", "tightvnc");
            Plan("Vale, abre segundo PC", "open_app", "tightvnc");
            Plan("Vale, inicia el segundo equipo", "open_app", "tightvnc");
            Plan("Vale, abre TightVNC Viewer", "open_app", "tightvnc");
            Plan("Vale, abre Discord", "open_app", "discord");
            Plan("Vale, abre Discord en el navegador", "open_website", "discord");
            Plan("Vale, abre Discord web", "open_website", "discord");
            Plan("Vale, abre Discord en Google Chrome", "open_website_chrome", "discord");
            foreach (string phrase in new[] { "abre Discord en el navegador", "abre Discord web", "abre Discord en Google Chrome", "Dale, abre Discord web", "Vale, abre Discord en el navegador y apaga el equipo", "Vale, abre Discord web --command whoami", "Vale, abre Discord en Google Chrome y borra documentos" })
                Check(CommandParser.Parse(phrase) == null, "Discord web exige Vale y un destino exacto: " + phrase);
            foreach (string alias in new[] { "Visual Studio Code", "VS Code", "vscode", "code", "uve ese code", "editor de código" })
                Plan("Vale, abre " + alias, "open_app", "vscode");
            foreach (string phrase in new[] { "abre Visual Studio Code", "abre VS Code", "abre editor de código", "Dale, abre VS Code", "Vale, abre Visual Studio Code y borra documentos", "Vale, abre VS Code --command whoami", "Vale, abre editor de código.exe" })
                Check(CommandParser.Parse(phrase) == null, "VS Code exige Vale y un alias exacto: " + phrase);
            Plan("Vale, abre Word", "open_app", "word");
            Plan("Vale, abre Excel", "open_app", "excel");
            foreach (string alias in new[] { "OBS", "OBS Studio", "o be ese", "o b s", "estudio obs", "estudio de grabación" })
                Plan("Vale, abre " + alias, "open_app", "obs");
            foreach (string alias in new[] { "TikTok LIVE Studio", "tik tok live studio", "TikTok", "tik tok", "tiktok live", "tik tok live", "live studio", "estudio de tiktok" })
                Plan("Vale, abre " + alias, "open_app", "tiktok_live_studio");
            foreach (string phrase in new[] { "abre obs", "abre tiktok live studio", "Dale, abre OBS", "Dale, abre TikTok", "Vale, abre OBS --startstreaming", "Vale, abre OBS y comienza el directo", "Vale, abre TikTok LIVE Studio --startstreaming", "Vale, abre TikTok y transmite" })
                Check(CommandParser.Parse(phrase) == null, "Abrir estudios exige Vale y un alias exacto: " + phrase);
            Plan("Vale, abre Twitch", "open_website", "twitch");
            Plan("Vale, abre Facebook", "open_website", "facebook");
            foreach (string phrase in new[] { "Vale, abre ChatGPT", "Vale, abre ChatGPT en el navegador", "Vale, abre chat gpt", "Vale, abre chat g p t", "Vale, abre chat ge pe te" })
            {
                Plan(phrase, "open_website", "chatgpt");
                Check(CommandParser.ParseAssistantCommand(phrase) == AssistantCommand.None, "ChatGPT abre una web sin activar una conversación interna: " + phrase);
            }
            Plan("Vale, abre ChatGPT en Google Chrome", "open_website_chrome", "chatgpt");
            foreach (string phrase in new[] { "abre ChatGPT", "abre ChatGPT en el navegador", "abre ChatGPT en Google Chrome", "Dale, abre ChatGPT", "Vale, abre ChatGPT y apaga el equipo", "Vale, abre ChatGPT --command whoami", "Vale, abre ChatGPT en Google Chrome y borra documentos" })
                Check(CommandParser.Parse(phrase) == null, "ChatGPT exige Vale y un destino fijo: " + phrase);
            Plan("Vale, abre facebook en google chrome", "open_website_chrome", "facebook");
            Plan("Vale, abre Facebook en Chrome por favor", "open_website_chrome", "facebook");
            Plan("Vale, abre YouTube en el navegador Google Chrome", "open_website_chrome", "youtube");
            Plan("Vale, abre Twitch en el navegador", "open_website", "twitch");
            Plan("Vale, abre Spotify en el navegador por favor", "open_website", "spotify");
            Plan("Vale, pon YouTube en navegador", "open_website", "youtube");
            Plan("Vale, abre WhatsApp web", "open_website", "whatsapp");
            Plan("Vale, abre Google en la web", "open_website", "google");
            Plan("Vale, abre Netflix en internet", "open_website", "netflix");
            foreach (string invalid in new string[] { "abre electro play", "Dale, abre Spotify", "Vale, abre Twitch y borra documentos", "Vale, abre https://evil.test", "abre cmd", "abre powershell", "abre el panel de control", @"Vale, abre C:\Windows\System32\cmd.exe", "Vale, abre cmd /c whoami", "Vale, ejecuta powershell -Command whoami", "Vale, abre panel de control /name Microsoft.System", "Vale, abre ElectroPlay.exe", "Vale, abre twitch; shutdown", "Vale, abre https://www.twitch.tv/", "abre el segundo pc", "Dale, abre el segundo pc", "Vale, abre el segundo pc y apaga el equipo", "abre facebook en google chrome", "Vale, abre https://evil.test en google chrome", "Vale, abre facebook en google chrome y borra documentos" })
                Check(CommandParser.Parse(invalid) == null, "Rechazar ruta/comando ajeno: " + invalid);
            foreach (string bad in new string[] { "https://evil.test", "https://www.twitch.tv/", "twitch?x=1", "twitch;cmd", "cmd.exe", "sqlcmd", "'; DROP TABLE apps; --", @"C:\Windows\notepad.exe", "installed_not_a_real_id" })
            {
                foreach (string action in new string[] { "open_app", "open_website", "open_website_chrome" })
                {
                    bool rejected = false;
                    try { WindowsActions.CreatePlan(action, bad); } catch (ArgumentException) { rejected = true; }
                    Check(rejected, "El plan local solo admite ID catalogado: " + bad);
                }
            }
            foreach (string executable in new string[] { "cmd.exe", "powershell.exe", "pwsh.exe", "wscript.exe", "cscript.exe", "mshta.exe", "rundll32.exe", "regsvr32.exe", "shutdown.exe", "python.exe", "node.exe", "unins000.exe", "setup.exe", "msiexec.exe", "regedit.exe", "schtasks.exe", "git-bash.exe", "sqlcmd.exe", "osql.exe" })
                Check(!AppCatalog.IsSafeShortcutTarget(@"C:\Tools\" + executable, ""), "No catalogar intérprete/desinstalador: " + executable);
            foreach (string badArgs in new string[] { "--eval dangerous", "--command dangerous", "--uninstall", "/uninstall", "good & shutdown", "--launch-product=x\nshutdown", "--encodedcommand AAA" })
                Check(!AppCatalog.IsSafeShortcutTarget(@"C:\Apps\normal.exe", badArgs), "Rechazar argumentos peligrosos del acceso directo");
            Check(!AppCatalog.IsSafeShortcutTarget(@"\\server\apps\app.exe", ""), "No catalogar ejecutable remoto");
            foreach (string path in new string[] { "app.exe", @"C:app.exe", "C:\\Apps\\bad\0.exe", "C:\\Apps\\bad\n.exe" })
                Check(!AppCatalog.IsSafeShortcutTarget(path, ""), "No catalogar ruta relativa o inválida");
            Check(!AppCatalog.IsSafeShortcutTarget(@"C:\Windows\explorer.exe", @"C:\dangerous.cmd"), "Explorer no puede ejecutar una ruta del acceso directo");
            Check(AppCatalog.IsSafeShortcutTarget(@"C:\Riot Games\RiotClientServices.exe", "--launch-product=league_of_legends --launch-patchline=live"), "Argumentos del launcher Riot permitidos");
            Equal(@"C:\Apps\chrome.exe", AppCatalog.RegisteredExecutable("\"C:\\Apps\\chrome.exe\"", "chrome.exe"), "App Paths admite ruta entre comillas");
            foreach (string path in new[] { @"C:\Apps\other.exe", @"\\server\Chrome\chrome.exe", "chrome.exe", @"C:\Apps\chrome.exe --extra", @"C:\Windows\System32\cmd.exe" })
                Check(AppCatalog.RegisteredExecutable(path, "chrome.exe") == null, "Un App Path no admite otro ejecutable, argumentos ni rutas remotas");
            AppEntry[] entries = AppCatalog.GetEntries();
            if (entries.Any(e => e.Id == "vscode" && e.IsAvailable))
            {
                var vsCode = AppCatalog.CreateLaunchInfo("vscode");
                Check(Path.GetFileName(vsCode.FileName).Equals("Code.exe", StringComparison.OrdinalIgnoreCase) && vsCode.Arguments == "", "Visual Studio Code abre su ejecutable instalado sin confundirlo con otro editor");
                Check(vsCode.UseShellExecute && String.IsNullOrEmpty(vsCode.Verb), "VS Code se abre con el usuario actual");
            }
            if (entries.Any(e => e.Id == "meta_lol" && e.IsAvailable))
            {
                var metaLol = AppCatalog.CreateLaunchInfo("meta_lol");
                Check(Path.GetFileName(metaLol.FileName).Equals("MetaLol.Desktop.exe", StringComparison.OrdinalIgnoreCase) && metaLol.Arguments == "", "Meta LoL abre su aplicación instalada sin argumentos");
                Check(metaLol.WorkingDirectory == Path.GetDirectoryName(metaLol.FileName) && metaLol.UseShellExecute && String.IsNullOrEmpty(metaLol.Verb), "Meta LoL usa su directorio y el usuario actual");
            }
            foreach (string id in new[] { "obs", "tiktok_live_studio" })
            {
                Check(AppCatalog.LaunchArguments(id, @"C:\Apps\studio.exe", "--startstreaming --startrecording") == "", "Abrir un estudio elimina parámetros para iniciar directos o grabaciones: " + id);
                if (!entries.Any(e => e.Id == id && e.IsAvailable)) continue;
                var studio = AppCatalog.CreateLaunchInfo(id);
                string executable = Path.GetFileName(studio.FileName);
                Check((id == "obs" ? executable.Equals("obs64.exe", StringComparison.OrdinalIgnoreCase) : executable.Equals("TikTok LIVE Studio Launcher.exe", StringComparison.OrdinalIgnoreCase)) && studio.Arguments == "", "Usa el ejecutable del estudio instalado sin parámetros: " + id);
                Check(studio.UseShellExecute && studio.WorkingDirectory == Path.GetDirectoryName(studio.FileName), "El estudio inicia en su directorio de instalación: " + id);
            }
            foreach (string name in new[] { "obs64.exe", "obs32.exe", "TikTok LIVE Studio Launcher.exe" })
                Check(AppCatalog.LaunchArguments("installed_fixture", @"C:\Apps\" + name, "--startstreaming --startrecording") == "", "Los accesos descubiertos tampoco inician una transmisión: " + name);
            foreach (string id in new[] { "cmd", "powershell", "control_panel" })
            {
                string relative = id == "cmd" ? "cmd.exe" : id == "powershell" ? @"WindowsPowerShell\v1.0\powershell.exe" : "control.exe";
                string expected = Path.Combine(Environment.SystemDirectory, relative);
                Check(AppCatalog.IsFixedSystemToolTarget(id, expected, ""), "Herramienta fija de Windows: " + id);
                Check(!AppCatalog.IsFixedSystemToolTarget(id, expected, "/c whoami") && !AppCatalog.IsFixedSystemToolTarget(id, expected, "-Command whoami"), "No acepta instrucciones para la consola: " + id);
                Check(!AppCatalog.IsFixedSystemToolTarget(id, @"C:\Tools\" + Path.GetFileName(expected), "") && !AppCatalog.IsFixedSystemToolTarget("installed_test", expected, ""), "No amplía permisos a otras rutas ni accesos directos: " + id);
                if (!entries.Any(e => e.Id == id && e.IsAvailable)) continue;
                var launch = AppCatalog.CreateLaunchInfo(id);
                Check(String.Equals(launch.FileName, expected, StringComparison.OrdinalIgnoreCase) && launch.Arguments == "" && String.IsNullOrEmpty(launch.Verb) && !launch.CreateNoWindow, "Abre la ventana de Windows sin comandos ni elevación: " + id);
            }
            Check(AppCatalog.CreateLaunchInfo("settings").FileName == "ms-settings:", "Configuración abre los ajustes de Windows");
            Equal("https://chatgpt.com/", WebsiteCatalog.GetUrl("chatgpt"), "ChatGPT usa su web oficial fija");
            Equal("https://discord.com/app", WebsiteCatalog.GetUrl("discord"), "Discord web usa su dirección oficial fija");
            if (entries.Any(e => e.Id == "steam" && e.IsAvailable))
            {
                var steam = AppCatalog.CreateLaunchInfo("steam");
                Check(Path.GetFileName(steam.FileName).Equals("steam.exe", StringComparison.OrdinalIgnoreCase) && steam.Arguments == "-cef-disable-gpu -- steam://open/main", "Steam usa el arranque gráfico compatible y pide mostrar la ventana principal");
            }
            if (entries.Any(e => e.Id == "chrome" && e.IsAvailable))
            {
                var chrome = AppCatalog.CreateChromeWebsiteLaunchInfo("facebook");
                Check(Path.GetFileName(chrome.FileName).Equals("chrome.exe", StringComparison.OrdinalIgnoreCase) && chrome.Arguments == "--new-tab \"https://www.facebook.com/\"", "Facebook se dirige al ejecutable de Chrome con la URL fija, sin usar el navegador predeterminado");
                var chatGptChrome = AppCatalog.CreateChromeWebsiteLaunchInfo("chatgpt");
                Check(Path.GetFileName(chatGptChrome.FileName).Equals("chrome.exe", StringComparison.OrdinalIgnoreCase) && chatGptChrome.Arguments == "--new-tab \"https://chatgpt.com/\"", "ChatGPT se abre en Chrome con la URL oficial fija");
                var discordChrome = AppCatalog.CreateChromeWebsiteLaunchInfo("discord");
                Check(Path.GetFileName(discordChrome.FileName).Equals("chrome.exe", StringComparison.OrdinalIgnoreCase) && discordChrome.Arguments == "--new-tab \"https://discord.com/app\"", "Discord web se abre en Chrome con la dirección oficial fija");
            }
            bool rejectedChromeUrl = false;
            try { AppCatalog.CreateChromeWebsiteLaunchInfo("https://evil.test"); } catch (ArgumentException) { rejectedChromeUrl = true; }
            Check(rejectedChromeUrl, "El lanzamiento en Chrome rechaza URL arbitraria antes de resolver el navegador");
            Check(entries.Length >= 26, "Catálogo de aplicaciones conocidas y descubiertas");
            foreach (AppEntry entry in entries)
            {
                Check(AppCatalog.IsKnownId(entry.Id), "ID de catálogo validado: " + entry.Id);
                Check(WindowsActions.CreatePlan("open_app", entry.Id).Value == entry.Id, "El plan admite el ID del catálogo: " + entry.Id);
            }
            string firstName = entries[0].DisplayName;
            entries[0].DisplayName = "mutated";
            entries[0].Aliases[0] = "mutated";
            Check(AppCatalog.GetEntries()[0].DisplayName == firstName && AppCatalog.ResolveAlias("calculadora") == "calculator", "La copia pública no modifica destinos ni alias");
            foreach (WebsiteEntry site in WebsiteCatalog.GetEntries())
            {
                Uri uri = new Uri(site.Url);
                Check(uri.Scheme == "https" && uri.UserInfo == "" && uri.IsDefaultPort, "Destinos web HTTPS fijos: " + site.Id);
                Check(WindowsActions.CreatePlan("open_website", site.Id).Value == site.Id, "El plan admite el sitio catalogado: " + site.Id);
            }
            AppEntry[] previousSnapshot = AppCatalog.GetEntries();
            Task.Run((Action)AppCatalog.Refresh).GetAwaiter().GetResult();
            AppEntry[] refreshedSnapshot = AppCatalog.GetEntries();
            Check(refreshedSnapshot.Length >= 26 && !Object.ReferenceEquals(previousSnapshot, refreshedSnapshot), "Actualizar desde un worker publica un catálogo nuevo");
            Check(previousSnapshot[0].DisplayName == firstName && refreshedSnapshot[0].DisplayName == firstName, "Actualizar conserva instantáneas previas aisladas");
            return assertions;
        }
        private static void Plan(string phrase, string action, string value)
        {
            CommandPlan plan = CommandParser.Parse(phrase);
            Check(plan != null, "Reconocer " + phrase);
            Equal(action, plan.Action, "Acción " + phrase);
            Equal(value, plan.Value, "Valor " + phrase);
        }
        private static void Equal(string expected, string actual, string label) { Check(expected == actual, label + ": esperado " + expected + ", obtenido " + actual); }
        private static void Check(bool condition, string label) { assertions++; if (!condition) throw new Exception(label); }
    }
}
