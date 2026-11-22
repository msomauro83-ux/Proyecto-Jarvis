using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace Jarvis
{
    public sealed class AppSettings
    {
        public string UserName { get; set; }
        public bool SpeakLocal { get; set; }

        public AppSettings()
        {
            UserName = "Mauricio";
            SpeakLocal = true;
        }
    }

    public static class SettingsStore
    {
        public static string DataDirectory { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data"); } }
        public static string SettingsPath { get { return Path.Combine(DataDirectory, "settings.json"); } }
        public static string LoadWarning { get; private set; }

        public static AppSettings Load()
        {
            LoadWarning = null;
            if (!File.Exists(SettingsPath)) return new AppSettings();
            try
            {
                string saved = File.ReadAllText(SettingsPath, Encoding.UTF8);
                AppSettings settings = Deserialize(saved);
                // Migrate older settings to the two local preferences only.
                if (saved != new JavaScriptSerializer().Serialize(settings))
                {
                    try { Save(settings); }
                    catch (IOException) { LoadWarning = "Los ajustes se cargaron, pero no pude actualizar su archivo. Vuelve a guardarlos en Ajustes."; }
                    catch (UnauthorizedAccessException) { LoadWarning = "Los ajustes se cargaron, pero no tengo permiso para actualizar su archivo."; }
                }
                return settings;
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidOperationException)) throw;
                LoadWarning = "No pude leer los ajustes. Revisa el archivo data/settings.json o vuelve a guardarlos.";
                return new AppSettings();
            }
        }

        internal static AppSettings Deserialize(string json)
        {
            AppSettings settings = new JavaScriptSerializer().Deserialize<AppSettings>(json);
            if (settings == null) throw new InvalidDataException("Los ajustes no contienen un objeto válido.");
            if (String.IsNullOrWhiteSpace(settings.UserName)) settings.UserName = "Mauricio";
            return settings;
        }

        public static void Save(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            if (String.IsNullOrWhiteSpace(settings.UserName)) settings.UserName = "Mauricio";
            Directory.CreateDirectory(DataDirectory);
            string temporary = SettingsPath + ".tmp";
            File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(settings), new UTF8Encoding(false));
            if (File.Exists(SettingsPath)) File.Replace(temporary, SettingsPath, null);
            else File.Move(temporary, SettingsPath);
        }
    }

    public static class StartupRegistration
    {
        private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "JarvisVale";

        public static bool IsEnabled
        {
            get
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
                    return key != null && key.GetValue(ValueName) != null;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                if (enabled) key.SetValue(ValueName, "\"" + System.Windows.Forms.Application.ExecutablePath + "\" --tray");
                else key.DeleteValue(ValueName, false);
            }
        }
    }
}
