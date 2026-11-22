using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Jarvis
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            bool preview = args.Length > 0 && args[0] == "--render-preview";
            bool smoke = args.Length > 0 && args[0] == "--smoke-test";
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                if (preview || smoke)
                {
                    using (MainForm form = new MainForm(true))
                    {
                        form.CreateControl();
                        form.Show();
                        Application.DoEvents();
                        if (preview)
                        {
                            if (args.Length < 2) return 2;
                            if (args.Length > 2) { form.ShowPreviewPage(args[2]); Application.DoEvents(); }
                            using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
                            {
                                form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
                                bitmap.Save(Path.GetFullPath(args[1]), System.Drawing.Imaging.ImageFormat.Png);
                            }
                        }
                        else
                        {
                            form.ShowPreviewPage("Comandos");
                            Application.DoEvents();
                            form.ShowPreviewPage("Ajustes");
                            Application.DoEvents();
                            form.ShowPreviewPage("Inicio");
                            Application.DoEvents();
                            IntPtr originalWindow = form.Handle;
                            if (!form.HandleAssistantCommandAsync("Vale, cierra Jarvis").GetAwaiter().GetResult() || form.Visible || form.IsDisposed) return 1;
                            if (!form.HandleAssistantCommandAsync("Vale, abre Jarvis").GetAwaiter().GetResult() || !form.Visible || form.Handle != originalWindow) return 1;
                            form.WindowState = FormWindowState.Minimized;
                            if (!form.HandleAssistantCommandAsync("Vale, abre Vale").GetAwaiter().GetResult() || form.WindowState != FormWindowState.Normal) return 1;
                            if (form.HandleAssistantCommandAsync("cierra Jarvis").GetAwaiter().GetResult() || !form.Visible) return 1;
                            form.ShowPreviewPage("Actividad");
                            form.ShowPreviewPage("Aplicaciones");
                            Application.DoEvents();
                            form.ShowPreviewPage("Inicio");
                        }
                        form.Hide();
                        form.CloseForTest();
                    }
                    return 0;
                }
                bool created;
                AppCatalog.WarmUp();
                using (Mutex mutex = new Mutex(true, @"Local\Jarvis.Vale.Desktop", out created))
                {
                    if (!created)
                    {
                        MessageBox.Show("Jarvis ya está abierto. Busca el icono V en la bandeja de Windows.", "Vale", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return 0;
                    }
                    using (MainForm form = new MainForm(false))
                    {
                        if (Array.IndexOf(args, "--tray") >= 0) form.StartInTray = true;
                        form.StartListeningOnLaunch = Array.IndexOf(args, "--listen") >= 0;
                        Application.Run(form);
                    }
                }
                return 0;
            }
            catch (Exception)
            {
                if (!preview && !smoke)
                    MessageBox.Show("No pude iniciar Jarvis. Comprueba que Windows tenga .NET Framework 4.8 y vuelve a compilar con build.ps1.", "Jarvis · Vale", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
