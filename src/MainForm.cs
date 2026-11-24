using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Jarvis
{
    public sealed class MainForm : Form
    {
        private readonly bool testMode;
        private AppSettings settings;
        private readonly Panel content = new Panel();
        private readonly Label pageTitle = new Label();
        private readonly Label pageSubtitle = new Label();
        private readonly Label sidebarState = new Label();
        private readonly List<Button> navigation = new List<Button>();
        private readonly Queue<string> history = new Queue<string>();
        private Label statusLabel, detailLabel, modeLabel;
        private Label inputDeviceLabel, inputLevelLabel, recognitionLabel;
        private InputMeter inputMeter;
        private string inputDeviceName = "Micrófono predeterminado de Windows";
        private string recognitionStatus = "Activa el micrófono y prueba «Vale, me escuchas».";
        private float inputLevel;
        private Button listenButton;
        private TextBox activity;
        private VoiceOrb orb;
        private NotifyIcon tray;
        private ToolStripMenuItem trayToggle;
        private Icon trayImage;
        private LocalSpeech local;
        private bool showUnavailableApps;
        private bool listening, changing, exiting, hotkeyRegistered, launchListeningPending;
        private int generation;
        private string currentPage = "Inicio";
        private string lastStatus = "Micrófono en pausa";
        private string lastDetail = "Activa la escucha y empieza con «Vale».";
        private string lastVoiceText = "";
        private DateTime lastVoiceTime = DateTime.MinValue;
        private readonly PowerCommandConfirmation powerConfirmation = new PowerCommandConfirmation();
        public bool StartInTray { get; set; }
        public bool StartListeningOnLaunch { get; set; }

        private static readonly Color Back = Color.FromArgb(11, 17, 23);
        private static readonly Color Surface = Color.FromArgb(18, 27, 36);
        private static readonly Color Line = Color.FromArgb(37, 52, 65);
        private static readonly Color Ink = Color.FromArgb(235, 242, 247);
        private static readonly Color Muted = Color.FromArgb(149, 169, 184);
        private static readonly Color Mint = Color.FromArgb(110, 231, 190);

        public MainForm(bool test)
        {
            testMode = test;
            settings = test ? new AppSettings() : SettingsStore.Load();
            Text = "Jarvis · Vale — Pre release";
            Size = new Size(1040, 790);
            MinimumSize = new Size(920, 710);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 10f);
            ForeColor = Ink;
            BackColor = Back;
            Icon = MakeIcon(false);
            BuildShell();
            if (!test) BuildTray();
            ShowPage("Inicio");
            FormClosing += OnClosing;
            Shown += async delegate
            {
                if (StartInTray) Hide();
                if (!testMode && !String.IsNullOrEmpty(SettingsStore.LoadWarning)) AddActivity("Sistema", SettingsStore.LoadWarning);
                if (!testMode && StartListeningOnLaunch) await StartOnLaunchAsync();
            };
            Resize += delegate { if (WindowState == FormWindowState.Minimized && !testMode) Hide(); };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!testMode) hotkeyRegistered = RegisterHotKey(Handle, 77, 0x0001 | 0x0002 | 0x4000, (uint)Keys.V);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == 77) ToggleListening();
            base.WndProc(ref m);
        }

        private static Label LabelAt(string text, float size, Color color, FontStyle style)
        {
            return new Label { Text = text, Font = new Font("Segoe UI", size, style), ForeColor = color, AutoSize = false, BackColor = Color.Transparent };
        }

        private static Button ButtonOf(string text, bool primary)
        {
            Button button = new Button { Text = text, Height = 43, Width = 190, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, BackColor = primary ? Mint : Surface, ForeColor = primary ? Back : Ink, Font = new Font("Segoe UI", 10f, FontStyle.Bold), UseVisualStyleBackColor = false };
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = Line;
            button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(144, 244, 210) : Color.FromArgb(30, 45, 57);
            return button;
        }

        private void BuildShell()
        {
            Panel sidebar = new Panel { Dock = DockStyle.Left, Width = 205, BackColor = Color.FromArgb(14, 22, 30), Padding = new Padding(20) };
            Label brand = LabelAt("J A R V I S", 14, Ink, FontStyle.Bold);
            brand.SetBounds(22, 30, 170, 28);
            Label edition = LabelAt("PRE RELEASE  /  0.4.4", 8, Muted, FontStyle.Regular);
            edition.SetBounds(23, 62, 170, 24);
            Label initial = LabelAt("V", 32, Mint, FontStyle.Bold);
            initial.SetBounds(26, 116, 66, 65);
            Label name = LabelAt("Vale", 16, Ink, FontStyle.Bold);
            name.SetBounds(23, 187, 160, 30);
            Label owner = LabelAt("Tu asistente de escritorio", 8.5f, Muted, FontStyle.Regular);
            owner.SetBounds(23, 222, 170, 35);
            sidebar.Controls.AddRange(new Control[] { brand, edition, initial, name, owner });
            string[] pages = { "Inicio", "Aplicaciones", "Comandos", "Actividad", "Ajustes" };
            for (int i = 0; i < pages.Length; i++)
            {
                string page = pages[i];
                Button nav = ButtonOf(page, false);
                nav.SetBounds(14, 288 + i * 51, 177, 43);
                nav.TextAlign = ContentAlignment.MiddleLeft;
                nav.Padding = new Padding(14, 0, 0, 0);
                nav.FlatAppearance.BorderSize = 0;
                nav.Click += delegate { ShowPage(page); };
                navigation.Add(nav);
                sidebar.Controls.Add(nav);
            }
            Panel bottom = new Panel { Dock = DockStyle.Bottom, Height = 122 };
            sidebarState.Text = "●  En pausa";
            sidebarState.ForeColor = Muted;
            sidebarState.SetBounds(3, 8, 165, 28);
            Label tip = LabelAt("Acciones del PC: di Vale.\nCtrl + Alt + V · pausar", 8.5f, Muted, FontStyle.Regular);
            tip.SetBounds(3, 43, 170, 66);
            bottom.Controls.AddRange(new Control[] { sidebarState, tip });
            sidebar.Controls.Add(bottom);

            Panel main = new Panel { Dock = DockStyle.Fill, Padding = new Padding(30, 23, 30, 18), BackColor = Back };
            Panel heading = new Panel { Dock = DockStyle.Top, Height = 90 };
            pageTitle.Font = new Font("Segoe UI", 25, FontStyle.Bold);
            pageTitle.ForeColor = Ink;
            pageTitle.SetBounds(0, 0, 680, 48);
            pageTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pageSubtitle.Font = new Font("Segoe UI", 10);
            pageSubtitle.ForeColor = Muted;
            pageSubtitle.SetBounds(3, 56, 680, 30);
            pageSubtitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            heading.Controls.AddRange(new Control[] { pageTitle, pageSubtitle });
            content.Dock = DockStyle.Fill;
            content.AutoScroll = true;
            content.BackColor = Back;
            main.Controls.Add(content);
            main.Controls.Add(heading);
            Controls.Add(main);
            Controls.Add(sidebar);
        }

        private void ShowPage(string page)
        {
            currentPage = page;
            content.AutoScroll = true;
            foreach (Button nav in navigation)
            {
                bool selected = nav.Text == page;
                nav.BackColor = selected ? Color.FromArgb(28, 49, 48) : Color.FromArgb(14, 22, 30);
                nav.ForeColor = selected ? Mint : Muted;
            }
            // Dispose timers and fonts belonging to the outgoing page.
            while (content.Controls.Count > 0) content.Controls[0].Dispose();
            statusLabel = detailLabel = modeLabel = null;
            inputDeviceLabel = inputLevelLabel = recognitionLabel = null;
            inputMeter = null;
            listenButton = null;
            activity = null;
            orb = null;
            if (page == "Inicio") BuildHome();
            else if (page == "Aplicaciones") BuildApplications();
            else if (page == "Comandos") BuildCommands();
            else if (page == "Actividad") BuildActivity();
            else BuildSettings();
            RefreshStatus();
        }

        private FlowLayoutPanel PageFlow()
        {
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 0, 0, 12), Margin = Padding.Empty };
            flow.Width = Math.Max(620, content.ClientSize.Width - 21);
            content.Controls.Add(flow);
            EventHandler resize = delegate { if (!flow.IsDisposed) flow.Width = Math.Max(600, content.ClientSize.Width - 21); };
            content.SizeChanged += resize;
            flow.Disposed += delegate { content.SizeChanged -= resize; };
            return flow;
        }

        private static Panel Card(FlowLayoutPanel flow, int height)
        {
            Panel card = new Panel { Width = flow.Width - 3, Height = height, BackColor = Surface, Margin = new Padding(0, 0, 0, 14) };
            flow.SizeChanged += delegate { if (!card.IsDisposed) card.Width = flow.Width - 3; };
            flow.Controls.Add(card);
            return card;
        }

        private void BuildHome()
        {
            pageTitle.Text = "Hola, " + settings.UserName + ".";
            pageSubtitle.Text = "Controla tu PC con la voz. Empieza cada comando con «Vale».";
            FlowLayoutPanel flow = PageFlow();
            Panel hero = Card(flow, 260);
            modeLabel = LabelAt("COMANDOS DEL PC  /  VOZ LOCAL", 8, Mint, FontStyle.Bold);
            modeLabel.SetBounds(23, 19, 370, 24);
            orb = new VoiceOrb { Location = new Point(24, 63), Size = new Size(120, 120), Active = listening };
            statusLabel = LabelAt(lastStatus, 19, Ink, FontStyle.Bold);
            statusLabel.SetBounds(163, 59, hero.Width - 183, 37);
            statusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            detailLabel = LabelAt(lastDetail, 9.5f, Muted, FontStyle.Regular);
            detailLabel.SetBounds(165, 103, hero.Width - 187, 44);
            detailLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            inputDeviceLabel = LabelAt("Entrada: " + inputDeviceName, 8.5f, Muted, FontStyle.Regular);
            inputDeviceLabel.SetBounds(165, 152, hero.Width - 187, 25);
            inputDeviceLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            inputDeviceLabel.AutoEllipsis = true;
            inputMeter = new InputMeter { Location = new Point(165, 189), Size = new Size(hero.Width - 270, 8), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            inputLevelLabel = LabelAt("0 %", 10, Mint, FontStyle.Bold);
            inputLevelLabel.SetBounds(hero.Width - 88, 177, 70, 28);
            inputLevelLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            recognitionLabel = LabelAt(recognitionStatus, 8.5f, Muted, FontStyle.Regular);
            recognitionLabel.SetBounds(165, 211, hero.Width - 187, 43);
            recognitionLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            LinkLabel microphoneSettings = new LinkLabel { Text = "Ajustes de sonido", Location = new Point(23, 212), Size = new Size(135, 27), LinkColor = Mint, ActiveLinkColor = Ink, VisitedLinkColor = Mint, Font = new Font("Segoe UI", 8.5f) };
            microphoneSettings.LinkClicked += delegate { OpenLink("ms-settings:sound"); };
            hero.Controls.AddRange(new Control[] { modeLabel, orb, statusLabel, detailLabel, inputDeviceLabel, inputMeter, inputLevelLabel, recognitionLabel, microphoneSettings });

            Panel controls = Card(flow, 65);
            controls.BackColor = Back;
            listenButton = ButtonOf("Activar micrófono", true);
            listenButton.SetBounds(0, 2, 221, 46);
            listenButton.Click += delegate { ToggleListening(); };
            Button hide = ButtonOf("Ir a la bandeja", false);
            hide.SetBounds(235, 2, 176, 46);
            hide.Click += delegate { HideToTray(); };
            controls.Controls.AddRange(new Control[] { listenButton, hide });

            Panel activityCard = Card(flow, 142);
            Label title = LabelAt("ACTIVIDAD DE ESTA SESIÓN", 8, Muted, FontStyle.Bold);
            title.SetBounds(19, 15, 360, 24);
            activity = new TextBox { Location = new Point(20, 47), Size = new Size(activityCard.Width - 40, 77), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None, BackColor = Surface, ForeColor = Ink, Font = new Font("Segoe UI", 9), TabStop = false };
            activityCard.Controls.AddRange(new Control[] { title, activity });
            RenderHistory();

            Panel textCard = Card(flow, 107);
            Label textLabel = LabelAt("Prueba también por escrito", 9, Muted, FontStyle.Regular);
            textLabel.SetBounds(18, 12, 400, 24);
            TextBox input = new TextBox { Text = "Vale, abre la calculadora", Location = new Point(19, 48), Width = textCard.Width - 153, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, BackColor = Back, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 11) };
            Button send = ButtonOf("Ejecutar", false);
            send.SetBounds(textCard.Width - 121, 43, 103, 36);
            send.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            send.Click += async delegate { string text = input.Text; input.SelectAll(); await SubmitTextAsync(text); };
            input.KeyDown += async delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; string text = input.Text; input.SelectAll(); await SubmitTextAsync(text); }
            };
            textCard.Controls.AddRange(new Control[] { textLabel, input, send });
        }

        private void BuildActivity()
        {
            pageTitle.Text = "Actividad de Vale.";
            pageSubtitle.Text = "Comandos y respuestas de esta sesión.";
            FlowLayoutPanel flow = PageFlow();
            Panel card = Card(flow, 430);
            Label title = LabelAt("ACTIVIDAD DE ESTA SESIÓN", 8, Muted, FontStyle.Bold);
            title.SetBounds(19, 15, 360, 24);
            activity = new TextBox { Location = new Point(20, 47), Size = new Size(card.Width - 40, 360), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None, BackColor = Surface, ForeColor = Ink, Font = new Font("Segoe UI", 9), TabStop = false };
            card.Controls.AddRange(new Control[] { title, activity });
            RenderHistory();
        }

        private void BuildApplications()
        {
            pageTitle.Text = "Tus aplicaciones.";
            pageSubtitle.Text = "Di «Vale, abre» y uno de los nombres del catálogo.";
            FlowLayoutPanel flow = PageFlow();
            Panel tools = Card(flow, 107);
            Button refresh = ButtonOf("Actualizar catálogo", true);
            refresh.SetBounds(18, 12, 210, 42);
            Label summary = LabelAt("Aplicaciones disponibles y sitios para abrir por voz.", 9, Muted, FontStyle.Regular);
            summary.SetBounds(246, 20, tools.Width - 270, 42);
            summary.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            refresh.Click += async delegate
            {
                refresh.Enabled = false;
                try
                {
                    await StopListeningAsync();
                    await Task.Run(() => AppCatalog.Refresh());
                    AddActivity("Sistema", "Catálogo actualizado. Activa la voz para usar la lista nueva.");
                    if (currentPage == "Aplicaciones") ShowPage("Aplicaciones");
                }
                finally { if (!refresh.IsDisposed) refresh.Enabled = true; }
            };
            tools.Controls.AddRange(new Control[] { refresh, summary });
            CheckBox showMissing = new CheckBox { Text = "Mostrar también aplicaciones no instaladas", Checked = showUnavailableApps, ForeColor = Muted, Location = new Point(20, 65), Size = new Size(550, 29) };
            showMissing.CheckedChanged += delegate { showUnavailableApps = showMissing.Checked; ShowPage("Aplicaciones"); };
            tools.Controls.Add(showMissing);
            foreach (AppEntry app in AppCatalog.GetEntries().Where(a => showUnavailableApps || a.IsAvailable).OrderBy(a => a.DisplayName))
            {
                Panel card = Card(flow, 83);
                Label title = LabelAt(app.DisplayName + (app.IsAvailable ? "" : " · no detectada"), 11, app.IsAvailable ? Ink : Muted, FontStyle.Bold);
                title.SetBounds(18, 9, card.Width - 36, 27);
                title.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                string alias = app.Aliases != null && app.Aliases.Length > 0 ? app.Aliases[0] : app.DisplayName;
                Label phrase = LabelAt("«Vale, abre " + alias + "»", 9, Mint, FontStyle.Regular);
                phrase.SetBounds(18, 42, card.Width - 36, 28);
                phrase.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                card.Controls.AddRange(new Control[] { title, phrase });
            }
            foreach (WebsiteEntry website in WebsiteCatalog.GetEntries())
            {
                Panel card = Card(flow, 83);
                Label title = LabelAt(website.DisplayName + " · navegador", 11, Ink, FontStyle.Bold);
                title.SetBounds(18, 9, card.Width - 36, 27);
                Label phrase = LabelAt("«Vale, abre " + website.Aliases[0] + " en el navegador»", 9, Mint, FontStyle.Regular);
                phrase.SetBounds(18, 42, card.Width - 36, 28);
                title.Anchor = phrase.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                card.Controls.AddRange(new Control[] { title, phrase });
            }
        }

        private void BuildCommands()
        {
            pageTitle.Text = "Dilo con Vale.";
            pageSubtitle.Text = "Frases para controlar tu PC con la voz.";
            FlowLayoutPanel flow = PageFlow();
            string[,] rows = {
                { "TUS APLICACIONES", "Vale, abre Electro Play", "Spotify, cliente de LoL, Paint, Steam, Discord y aplicaciones del catálogo." },
                { "META LOL", "Vale, abre Meta LoL", "Abre la aplicación Meta LoL instalada. También «Vale, abre meta de LoL»." },
                { "VISUAL STUDIO CODE", "Vale, abre VS Code", "También «Vale, abre Visual Studio Code» o «Vale, abre editor de código»." },
                { "OBS STUDIO", "Vale, abre estudio de grabación", "Abre OBS. También admite «Vale, abre OBS»; usa la frase completa si Windows confunde las siglas." },
                { "TIKTOK LIVE STUDIO", "Vale, abre el TikTok LIVE Studio", "También «Vale, abre estudio de TikTok». Usa el nombre completo para evitar confusiones." },
                { "SEGUNDO PC", "Vale, abre el segundo pc", "Abre TightVNC Viewer para conectarte a tu otro equipo." },
                { "FACEBOOK EN CHROME", "Vale, abre Facebook en Google Chrome", "Abre Facebook en Chrome aunque tu navegador predeterminado sea otro." },
                { "TERMINALES", "Vale, abre CMD", "También «Vale, abre PowerShell» o «Vale, abre el símbolo del sistema»." },
                { "AJUSTES DE WINDOWS", "Vale, abre configuración", "O «Vale, abre el panel de control»." },
                { "SUSPENDER EL PC", "Vale, pon el PC en suspensión", "Después di «Vale, confirma la suspensión» dentro de 30 segundos." },
                { "APAGAR EL PC", "Vale, apaga el PC", "Después di «Vale, confirma apagado». «Vale, cancela la acción» lo cancela antes de confirmar." },
                { "BLOQUEAR EL PC", "Vale, bloquea el PC", "Bloquea tu sesión como Windows + L. El micrófono queda en pausa." },
                { "SITIOS WEB", "Vale, abre Twitch en el navegador", "También YouTube, Spotify Web, Netflix y los sitios del catálogo." },
                { "DISCORD EN EL NAVEGADOR", "Vale, abre Discord en el navegador", "Abre discord.com/app en tu navegador predeterminado." },
                { "CHATGPT EN EL NAVEGADOR", "Vale, abre ChatGPT", "Abre chatgpt.com en tu navegador predeterminado. También puedes pedirlo en Google Chrome." },
                { "ABRIR Y CERRAR VALE", "Vale, abre Jarvis", "«Vale, cierra Jarvis» oculta la ventana y mantiene la escucha." },
                { "PROBAR LA VOZ", "Vale, me escuchas", "Responde si entendió tu voz. El medidor de Inicio muestra la señal de entrada." },
                { "APLICACIONES", "Vale, abre la calculadora", "También Chrome, bloc de notas, explorador o configuración." },
                { "CARPETAS", "Vale, abre documentos", "Documentos, descargas y escritorio." },
                { "BÚSQUEDAS", "Vale, busca en internet recetas de pan", "O «Vale, busca en YouTube música para concentrarse»." },
                { "AUDIO Y MÚSICA", "Vale, sube el volumen", "Baja el volumen, silencia, pausa la música o siguiente canción." },
                { "ESCRITORIO", "Vale, muestra el escritorio", "O «Vale, toma una captura de pantalla». Se guarda en Imágenes/Jarvis." },
                { "DICTADO", "Vale, escribe hola Mauricio", "Escribe en la ventana activa. Para dictar, deja el cursor en el destino." },
                { "MICRÓFONO", "Vale, pausa el micrófono", "Para reactivar usa la bandeja o Ctrl + Alt + V." }
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                Panel card = Card(flow, 112);
                Label category = LabelAt(rows[i, 0], 8, Mint, FontStyle.Bold);
                category.SetBounds(20, 12, 550, 22);
                Label example = LabelAt("«" + rows[i, 1] + "»", 12, Ink, FontStyle.Bold);
                example.SetBounds(20, 37, card.Width - 35, 30);
                example.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                Label help = LabelAt(rows[i, 2], 9, Muted, FontStyle.Regular);
                help.SetBounds(20, 74, card.Width - 35, 30);
                help.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                card.Controls.AddRange(new Control[] { category, example, help });
            }
        }

        private void BuildSettings()
        {
            pageTitle.Text = "A tu manera.";
            pageSubtitle.Text = "Tu nombre, las confirmaciones de voz y el inicio de Vale.";
            FlowLayoutPanel flow = PageFlow();
            Panel identity = Card(flow, 114);
            Label nameLabel = LabelAt("¿Cómo te llamas?", 10, Ink, FontStyle.Bold);
            nameLabel.SetBounds(20, 15, 350, 25);
            TextBox user = new TextBox { Text = settings.UserName, Location = new Point(21, 54), Width = 315, MaxLength = 40, BackColor = Back, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle };
            Label wake = LabelAt("Nombre del asistente: Vale", 9, Mint, FontStyle.Regular);
            wake.SetBounds(354, 53, 260, 32);
            identity.Controls.AddRange(new Control[] { nameLabel, user, wake });

            Panel options = Card(flow, 155);
            CheckBox speak = new CheckBox { Text = "Confirmar acciones con la voz local de Windows", Checked = settings.SpeakLocal, ForeColor = Ink, Location = new Point(20, 19), Size = new Size(570, 30), FlatStyle = FlatStyle.Flat };
            Label voice = LabelAt("Voz preferida: Microsoft Sabina · Español (México)", 9, Muted, FontStyle.Regular);
            voice.SetBounds(42, 51, 575, 27);
            CheckBox startup = new CheckBox { Text = "Iniciar en la bandeja al entrar a Windows con el micrófono activo", Checked = !testMode && StartupRegistration.IsEnabled, ForeColor = Ink, Location = new Point(20, 91), Size = new Size(620, 35), FlatStyle = FlatStyle.Flat };
            options.Controls.AddRange(new Control[] { speak, voice, startup });
            Panel actions = Card(flow, 71);
            actions.BackColor = Back;
            Button save = ButtonOf("Guardar ajustes", true);
            save.SetBounds(0, 2, 195, 44);
            Label saved = LabelAt("", 9, Muted, FontStyle.Regular);
            saved.SetBounds(215, 4, 410, 60);
            save.Click += async delegate
            {
                if (changing) return;
                changing = true;
                save.Enabled = false;
                try
                {
                    await StopListeningAsync();
                    AppSettings next = new AppSettings { UserName = String.IsNullOrWhiteSpace(user.Text) ? "Mauricio" : user.Text.Trim(), SpeakLocal = speak.Checked };
                    if (!testMode)
                    {
                        SettingsStore.Save(next);
                        StartupRegistration.SetEnabled(startup.Checked);
                    }
                    settings = next;
                    saved.Text = "Guardado. Activa el micrófono desde Inicio.";
                    AddActivity("Sistema", "Ajustes guardados. Micrófono en pausa.");
                }
                catch (Exception ex) { saved.Text = "No pude guardar: " + SafeError(ex); }
                finally { changing = false; if (!save.IsDisposed) save.Enabled = true; RefreshStatus(); }
            };
            actions.Controls.AddRange(new Control[] { save, saved });
        }

        private static void OpenLink(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { MessageBox.Show("No pude abrir la configuración de Windows.", "Vale"); }
        }

        private void BuildTray()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Abrir Vale", null, delegate { Restore(); });
            trayToggle = new ToolStripMenuItem("Activar micrófono", null, delegate { ToggleListening(); });
            menu.Items.Add(trayToggle);
            menu.Items.Add("Comandos", null, delegate { Restore(); ShowPage("Comandos"); });
            menu.Items.Add("Actividad", null, delegate { Restore(); ShowPage("Actividad"); });
            menu.Items.Add("Ajustes", null, delegate { Restore(); ShowPage("Ajustes"); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Salir de Jarvis", null, async delegate { await ExitAsync(); });
            trayImage = MakeIcon(false);
            tray = new NotifyIcon { Icon = trayImage, Text = "Vale · Micrófono en pausa", Visible = true, ContextMenuStrip = menu };
            tray.DoubleClick += delegate { Restore(); };
        }

        private void Restore()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void HideToTray()
        {
            Hide();
            if (tray != null)
            {
                tray.BalloonTipTitle = "Vale está en la bandeja";
                tray.BalloonTipText = listening ? "Te escucho. Empieza cada comando con «Vale»." : "Micrófono en pausa. Haz clic derecho en V para activarlo.";
                tray.ShowBalloonTip(2500);
            }
        }

        private async void ToggleListening()
        {
            launchListeningPending = false;
            await ToggleListeningAsync();
        }

        private async Task StartOnLaunchAsync()
        {
            launchListeningPending = true;
            for (int attempt = 0; attempt < 3 && launchListeningPending && !exiting; attempt++)
            {
                if (attempt > 0)
                {
                    AddActivity("Sistema", "Reintentando el micrófono al iniciar en 5 segundos.");
                    await Task.Delay(5000);
                    if (!launchListeningPending || exiting) break;
                }
                await ToggleListeningAsync();
                if (!launchListeningPending && listening) await StopListeningAsync();
                if (listening) break;
            }
            launchListeningPending = false;
        }

        private async Task ToggleListeningAsync()
        {
            if (testMode || changing || exiting) return;
            changing = true;
            RefreshStatus();
            try
            {
                Exception failure = null;
                try
                {
                    if (listening) await StopListeningAsync();
                    else await StartListeningAsync();
                }
                catch (Exception ex) { failure = ex; }
                if (failure != null)
                {
                    await StopListeningAsync(false);
                    SetStatus("No pude activar la escucha", SafeError(failure));
                    AddActivity("Sistema", SafeError(failure));
                }
            }
            finally { changing = false; RefreshStatus(); }
        }

        private async Task StartListeningAsync()
        {
            int session = ++generation;
            inputDeviceName = await Task.Run(() => AudioInput.DefaultName());
            if (session != generation || exiting) return;
            SetStatus("Preparando el micrófono…", "Cargando el reconocimiento de voz en español.");
            LocalSpeech speech = new LocalSpeech();
            local = speech;
            speech.Heard += s => Ui(async delegate
            {
                if (generation != session || !listening) return;
                await ExecuteLocalAsync(s, true);
            });
            speech.Error += s => Ui(async delegate
            {
                if (generation != session) return;
                await StopListeningAsync(!launchListeningPending);
                SetStatus("Micrófono detenido", s);
                AddActivity("Sistema", s);
            });
            speech.Level += level => Ui(delegate { if (generation == session) UpdateInputLevel(level); });
            speech.Diagnostic += message => Ui(delegate
            {
                if (generation != session) return;
                recognitionStatus = message;
                RefreshAudioUi();
            });
            await Task.Run(() => speech.Start());
            if (generation != session || exiting) { speech.Dispose(); return; }
            listening = true;
            RefreshAudioUi();
            SetStatus("Te escucho, " + settings.UserName, "Di «Vale, abre Electro Play» o «Vale, abre Twitch en el navegador».");
            AddActivity("Sistema", "Escucha local activada. El audio se procesa en este PC.");
            RefreshStatus();
        }

        private Task StopListeningAsync(bool cancelLaunch = true)
        {
            if (cancelLaunch) launchListeningPending = false;
            powerConfirmation.Cancel();
            generation++;
            listening = false;
            inputLevel = 0;
            recognitionStatus = "Micrófono en pausa. La barra mostrará el sonido al activarlo.";
            LocalSpeech oldLocal = local;
            local = null;
            if (oldLocal != null) oldLocal.Dispose();
            SetStatus("Micrófono en pausa", "Activa la escucha y empieza con «Vale».");
            RefreshStatus();
            return Task.FromResult(true);
        }

        private async Task SubmitTextAsync(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return;
            string command;
            if (!CommandParser.TryWake(text, out command))
            {
                AddActivity("Vale", "Empieza el comando con Vale, por ejemplo: Vale, abre Paint.");
                return;
            }
            await ExecuteLocalAsync(text, false);
        }

        internal async Task<bool> HandleAssistantCommandAsync(string text)
        {
            AssistantCommand command = CommandParser.ParseAssistantCommand(text);
            string result;
            switch (command)
            {
                case AssistantCommand.ShowWindow:
                    Restore();
                    result = "Aquí estoy. Ventana de Jarvis abierta.";
                    break;
                case AssistantCommand.HideWindow:
                    HideToTray();
                    result = listening ? "Ventana oculta. Sigo escuchando desde la bandeja." : "Ventana oculta. Activa el micrófono desde la bandeja para abrirme por voz.";
                    break;
                case AssistantCommand.PauseListening:
                    await StopListeningAsync();
                    result = "Micrófono en pausa. Reactívame desde la bandeja.";
                    break;
                case AssistantCommand.CheckMicrophone:
                    result = "Sí, " + settings.UserName + ", te escucho.";
                    break;
                case AssistantCommand.SuspendComputer:
                case AssistantCommand.ShutdownComputer:
                    powerConfirmation.Request(command, DateTime.UtcNow);
                    result = command == AssistantCommand.SuspendComputer
                        ? "Para suspender el PC, di Vale, confirma la suspensión dentro de 30 segundos. Para cancelar, di Vale, cancela la acción."
                        : "Guarda tu trabajo. Para apagar el PC, di Vale, confirma apagado dentro de 30 segundos. Para cancelar, di Vale, cancela la acción.";
                    if (tray != null) { tray.BalloonTipTitle = "Confirmar acción del PC"; tray.BalloonTipText = result; tray.ShowBalloonTip(10000); }
                    break;
                case AssistantCommand.CancelPowerAction:
                    powerConfirmation.Cancel();
                    result = "Acción de energía cancelada.";
                    break;
                case AssistantCommand.ConfirmSuspend:
                case AssistantCommand.ConfirmShutdown:
                    AssistantCommand approved;
                    if (!powerConfirmation.TryConfirm(command, DateTime.UtcNow, out approved))
                    {
                        result = "No hay una solicitud coincidente pendiente o han pasado 30 segundos. Vuelve a pedir la acción.";
                        break;
                    }
                    result = await ExecuteComputerPowerAsync(approved);
                    break;
                case AssistantCommand.LockComputer:
                    powerConfirmation.Cancel();
                    result = await ExecuteComputerPowerAsync(command);
                    break;
                default:
                    return false;
            }
            AddActivity("Vale", result);
            if (settings.SpeakLocal && local != null) local.Say(result);
            return true;
        }

        private async Task<string> ExecuteComputerPowerAsync(AssistantCommand command)
        {
            if (testMode) return "Prueba: acción del PC omitida.";
            try
            {
                await StopListeningAsync();
                AddActivity("Vale", command == AssistantCommand.LockComputer ? "Solicitando bloqueo a Windows." : "Acción confirmada. Enviando solicitud a Windows.");
                return await Task.Run(() => ComputerPower.Execute(command));
            }
            catch (Exception ex) { return "No pude hacerlo: " + SafeError(ex) + " Reactiva el micrófono desde la bandeja para usar la voz."; }
        }

        private async Task ExecuteLocalAsync(string text, bool fromVoice)
        {
            string command;
            if (!CommandParser.TryWake(text, out command)) return;
            if (fromVoice && text == lastVoiceText && DateTime.UtcNow - lastVoiceTime < TimeSpan.FromSeconds(1.2)) return;
            lastVoiceText = text;
            lastVoiceTime = DateTime.UtcNow;
            AddActivity(settings.UserName, text);
            if (await HandleAssistantCommandAsync(text)) return;
            CommandPlan plan = CommandParser.Parse(text);
            if (plan == null)
            {
                string help = "No reconozco ese comando. Consulta Comandos o Aplicaciones para ver las frases disponibles.";
                AddActivity("Vale", help);
                if (settings.SpeakLocal && local != null) local.Say(help);
                return;
            }
            try
            {
                if (RequiresTarget(plan) && ContainsFocus)
                {
                    AddActivity("Vale", "Elige la ventana de destino. Ejecutaré el comando en 3 segundos.");
                    if (!testMode) { HideToTray(); await Task.Delay(3000); }
                    if (fromVoice && !listening) return;
                }
                if (testMode) return;
                string result = WindowsActions.Execute(plan);
                AddActivity("Vale", result);
                if (settings.SpeakLocal && local != null) local.Say(result);
            }
            catch (Exception ex) { AddActivity("Vale", "No pude hacerlo: " + SafeError(ex)); }
        }

        private static bool RequiresTarget(CommandPlan plan)
        {
            return plan.Action == "type_text" || plan.Action == "shortcut";
        }

        private void Ui(Action action)
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            try { if (InvokeRequired) BeginInvoke(action); else action(); }
            catch (InvalidOperationException) { }
        }

        private static string SafeError(Exception ex)
        {
            if (ex is InvalidOperationException || ex is ArgumentException) return ex.Message;
            if (ex is UnauthorizedAccessException) return "Windows no dio permiso. Revisa los permisos del micrófono o de la aplicación de destino.";
            if (ex is System.IO.FileNotFoundException) return ex.Message;
            if (ex is System.IO.IOException) return "No pude acceder al archivo o dispositivo. Revisa que esté disponible.";
            return "Revisa el micrófono y la aplicación de destino, y vuelve a intentarlo.";
        }

        private void AddActivity(string who, string text)
        {
            string safe = (text ?? "").Replace("\r", " ").Replace("\n", " ");
            if (safe.Length > 900) safe = safe.Substring(0, 900) + "…";
            history.Enqueue(DateTime.Now.ToString("HH:mm") + "  " + who + "  ·  " + safe);
            while (history.Count > 60) history.Dequeue();
            RenderHistory();
        }

        private void RenderHistory()
        {
            if (activity == null || activity.IsDisposed) return;
            activity.Text = history.Count == 0 ? "Todo listo, " + settings.UserName + ". Activa el micrófono o prueba un comando por escrito." + Environment.NewLine + Environment.NewLine + "Ejemplo: Vale, abre la calculadora." : String.Join(Environment.NewLine + Environment.NewLine, history.ToArray());
            activity.SelectionStart = activity.TextLength;
            activity.ScrollToCaret();
        }

        private void SetStatus(string title, string detail)
        {
            lastStatus = title;
            lastDetail = detail;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (modeLabel != null) modeLabel.Text = "COMANDOS DEL PC  /  VOZ LOCAL";
            if (statusLabel != null) statusLabel.Text = lastStatus;
            if (detailLabel != null) detailLabel.Text = lastDetail;
            if (orb != null) orb.Active = listening;
            if (listenButton != null) { listenButton.Text = changing ? "Un momento…" : listening ? "Pausar micrófono" : "Activar micrófono"; listenButton.Enabled = !changing; }
            sidebarState.Text = listening ? "●  Escuchando" : "●  En pausa";
            sidebarState.ForeColor = listening ? Mint : Muted;
            RefreshAudioUi();
            if (trayToggle != null) { trayToggle.Text = listening ? "Pausar micrófono" : "Activar micrófono"; trayToggle.Enabled = !changing; }
            if (tray != null)
            {
                tray.Text = listening ? "Vale · Escuchando · Di Vale" : "Vale · Micrófono en pausa";
                bool iconActive = trayImage != null && Convert.ToBoolean(tray.Tag ?? false);
                if (iconActive != listening)
                {
                    Icon previous = trayImage;
                    trayImage = MakeIcon(listening);
                    tray.Icon = trayImage;
                    tray.Tag = listening;
                    if (previous != null) previous.Dispose();
                }
            }
        }

        private void UpdateInputLevel(float level)
        {
            inputLevel = Math.Max(0, Math.Min(1, level));
            if (orb != null) orb.Level = inputLevel;
            RefreshAudioUi();
        }

        private void RefreshAudioUi()
        {
            if (inputDeviceLabel != null) inputDeviceLabel.Text = "Entrada predeterminada: " + inputDeviceName;
            if (inputLevelLabel != null) inputLevelLabel.Text = ((int)(inputLevel * 100)).ToString() + " %";
            if (inputMeter != null) inputMeter.Level = inputLevel;
            if (recognitionLabel != null) recognitionLabel.Text = recognitionStatus;
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (!exiting && !testMode && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
            }
        }

        private async Task ExitAsync()
        {
            if (changing)
            {
                AddActivity("Sistema", "Espera a que termine la operación y vuelve a elegir Salir.");
                return;
            }
            exiting = true;
            await StopListeningAsync();
            Close();
        }

        public void CloseForTest() { exiting = true; Close(); }
        public void ShowPreviewPage(string page)
        {
            if (!testMode) throw new InvalidOperationException();
            ShowPage(page);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                generation++;
                if (local != null) { local.Dispose(); local = null; }
                if (tray != null) { tray.Visible = false; tray.ContextMenuStrip.Dispose(); tray.Dispose(); tray = null; }
                if (trayImage != null) { trayImage.Dispose(); trayImage = null; }
                if (hotkeyRegistered && IsHandleCreated) { UnregisterHotKey(Handle, 77); hotkeyRegistered = false; }
            }
            base.Dispose(disposing);
        }

        private static Icon MakeIcon(bool active)
        {
            using (Bitmap bitmap = new Bitmap(64, 64))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Brush fill = new SolidBrush(active ? Mint : Color.FromArgb(63, 89, 103))) g.FillEllipse(fill, 3, 3, 58, 58);
                using (Pen pen = new Pen(Back, 6)) { pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; g.DrawLines(pen, new PointF[] { new PointF(19, 21), new PointF(32, 44), new PointF(45, 21) }); }
                IntPtr handle = bitmap.GetHicon();
                try { using (Icon borrowed = Icon.FromHandle(handle)) return (Icon)borrowed.Clone(); }
                finally { DestroyIcon(handle); }
            }
        }

        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    }

    internal sealed class VoiceOrb : Control
    {
        private readonly Timer timer;
        private double phase;
        private bool active;
        private float level;
        public bool Active { get { return active; } set { active = value; Invalidate(); } }
        public float Level { get { return level; } set { level = Math.Max(0, Math.Min(1, value)); } }
        public VoiceOrb()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(18, 27, 36);
            timer = new Timer { Interval = 60 };
            timer.Tick += delegate { if (active) { phase += .12; Invalidate(); } };
            timer.Start();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = Width / 2f, cy = Height / 2f;
            float radius = Math.Min(Width, Height) / 2f - 5;
            Color color = active ? Color.FromArgb(110, 231, 190) : Color.FromArgb(91, 126, 144);
            for (int i = 0; i < 3; i++)
            {
                float r = radius - i * 9;
                using (Pen ring = new Pen(Color.FromArgb(32 + i * 22, color), 1f)) g.DrawEllipse(ring, cx - r, cy - r, r * 2, r * 2);
            }
            using (Brush center = new SolidBrush(Color.FromArgb(25, color))) g.FillEllipse(center, cx - 28, cy - 28, 56, 56);
            for (int i = 0; i < 7; i++)
            {
                float h = active ? 8 + (float)(Math.Sin(phase + i * .9) + 1) * (7 + level * 13) : 8 + (3 - Math.Abs(i - 3)) * 6;
                using (Pen bar = new Pen(color, 4)) { bar.StartCap = LineCap.Round; bar.EndCap = LineCap.Round; float x = cx + (i - 3) * 8; g.DrawLine(bar, x, cy - h / 2, x, cy + h / 2); }
            }
        }
        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
    }

    internal sealed class InputMeter : Control
    {
        private float level;
        public float Level { get { return level; } set { level = Math.Max(0, Math.Min(1, value)); Invalidate(); } }
        public InputMeter() { DoubleBuffered = true; BackColor = Color.FromArgb(37, 52, 65); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Brush fill = new SolidBrush(Color.FromArgb(110, 231, 190))) e.Graphics.FillRectangle(fill, 0, 0, Width * level, Height);
        }
    }
}
