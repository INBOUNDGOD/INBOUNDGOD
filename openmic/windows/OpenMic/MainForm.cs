using System.Text.Json;

namespace OpenMic;

public sealed class MainForm : Form
{
    // clean light palette (native controls render correctly on light)
    private static readonly Color BgColor = Color.FromArgb(247, 248, 250);
    private static readonly Color CardBg = Color.White;
    private static readonly Color BorderColor = Color.FromArgb(225, 228, 232);
    private static readonly Color Ink = Color.FromArgb(28, 30, 38);
    private static readonly Color SubInk = Color.FromArgb(104, 110, 122);
    private static readonly Color Accent = Color.FromArgb(79, 70, 229);   // indigo
    private static readonly Color GreenColor = Color.FromArgb(34, 139, 69);
    private static readonly Color RedColor = Color.FromArgb(200, 60, 64);

    private const int CardW = 432;

    private readonly ListBox _phones = new();
    private readonly Button _scan = new() { Text = "Scan Wi-Fi", Width = 104, Height = 28 };
    private readonly TextBox _ip = new() { Width = 118 };
    private readonly TextBox _pin = new() { Width = 60 };
    private readonly CheckBox _usb = new() { Text = "USB cable (adb) — lowest latency", AutoSize = true };
    private readonly ComboBox _device = new() { Width = CardW - 24, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TrackBar _volume = new() { Minimum = 0, Maximum = 200, Value = 100, Width = 250, TickStyle = TickStyle.None };
    private readonly Label _volumeLbl = new() { AutoSize = true, Text = "100%" };
    private readonly TrackBar _noise = new() { Minimum = 0, Maximum = 100, Value = 0, Width = 250, TickStyle = TickStyle.None };
    private readonly Label _noiseLbl = new() { AutoSize = true, Text = "off" };
    private readonly ComboBox _noiseEngine = new() { Width = CardW - 100, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _search = new() { Width = 110 };
    private readonly ComboBox _effect = new() { Width = CardW - 150, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _start = new() { Text = "Start", Width = 130, Height = 36 };
    private readonly Button _stop = new() { Text = "Stop", Width = 130, Height = 36, Enabled = false };
    private readonly Label _update = new() { AutoSize = true, Visible = false, ForeColor = Accent, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9F, FontStyle.Underline) };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 40, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(16, 0, 16, 0), BackColor = Color.FromArgb(238, 240, 243), ForeColor = SubInk };

    private readonly NotifyIcon _tray = new();
    private bool _reallyExit;
    private readonly UpdateService _updater = new();
    private List<Phone> _found = new();
    private Receiver? _rx;

    public MainForm()
    {
        Text = "OpenMic";
        ClientSize = new Size(480, 660);
        BackColor = BgColor;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 9.5F);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BuildUi();

        foreach (var d in AudioOutput.OutputDevices()) _device.Items.Add(d);
        foreach (var p in Fx.Presets) _effect.Items.Add(p);
        _effect.SelectedIndex = 0;
        foreach (var e in Fx.NoiseEngines) _noiseEngine.Items.Add(e);
        _noiseEngine.SelectedIndex = 0;
        LoadSettings();

        _scan.Click += (_, _) => Scan();
        _start.Click += (_, _) => Start();
        _stop.Click += (_, _) => _rx?.Stop();
        _device.SelectedIndexChanged += (_, _) => { _rx?.SetDevice(_device.Text); SaveSettings(); };
        _effect.SelectedIndexChanged += (_, _) => { _rx?.SetEffect(Effect()); SaveSettings(); };
        _noiseEngine.SelectedIndexChanged += (_, _) => { _rx?.SetNoiseEngine(_noiseEngine.Text); SaveSettings(); };
        _search.TextChanged += (_, _) => FilterEffects();
        _volume.Scroll += (_, _) => { _volumeLbl.Text = _volume.Value + "%"; _rx?.SetVolume(_volume.Value / 100.0); };
        _noise.Scroll += (_, _) => { _noiseLbl.Text = _noise.Value == 0 ? "off" : _noise.Value + "%"; _rx?.SetNoise(_noise.Value / 100.0); };
        _update.Click += (_, _) => _updater.ApplyAndRestart();
        SetupTray();
        FormClosing += OnFormClosing;

        SetStatus(AudioOutput.OutputDevices().Count == 0 ? "No output devices found."
            : "Ready. Scan for your phone, or type its IP.");
        Shown += async (_, _) => await CheckForUpdate();
    }

    private void BuildUi()
    {
        // scrolling card stack fills the space between the header and status bar,
        // so controls can never overlap the status text again.
        var content = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(16, 12, 16, 12),
            BackColor = BgColor,
        };

        content.Controls.Add(PhoneCard());
        content.Controls.Add(OutputCard());
        content.Controls.Add(VoiceCard());
        content.Controls.Add(ButtonsCard());

        var header = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = Accent };
        header.Controls.Add(new Label { Text = "OpenMic", ForeColor = Color.White, AutoSize = true, Location = new Point(16, 10), Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold) });
        header.Controls.Add(new Label { Text = "Your phone as a PC microphone", ForeColor = Color.FromArgb(222, 224, 252), AutoSize = true, Location = new Point(18, 38), Font = new Font("Segoe UI", 8.5F) });

        // add order matters for docking: Fill first, then Bottom, then Top
        Controls.Add(content);
        Controls.Add(_status);
        Controls.Add(header);
    }

    // ---- card helpers ----
    private Panel Card(int height)
    {
        var p = new Panel { Width = CardW, Height = height, BackColor = CardBg, Margin = new Padding(0, 0, 0, 12) };
        p.Paint += (_, e) => { using var pen = new Pen(BorderColor); e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1); };
        return p;
    }
    private Label Title(string t) => new() { Text = t, ForeColor = Accent, AutoSize = true, Location = new Point(12, 9), Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold) };
    private Label Lbl(string t, int x, int y, bool sub = false, int w = 0)
    {
        var l = new Label { Text = t, ForeColor = sub ? SubInk : Ink, Location = new Point(x, y), BackColor = Color.Transparent };
        if (w > 0) { l.AutoSize = false; l.Size = new Size(w, 32); } else l.AutoSize = true;
        return l;
    }

    private Panel PhoneCard()
    {
        var c = Card(214);
        c.Controls.Add(Title("1  ·  Phone"));
        _phones.SetBounds(12, 34, CardW - 24, 80); _phones.BorderStyle = BorderStyle.FixedSingle;
        c.Controls.Add(_phones);
        _scan.Location = new Point(12, 122); c.Controls.Add(_scan);
        c.Controls.Add(Lbl("or IP", 124, 127, true));
        _ip.Location = new Point(162, 124); c.Controls.Add(_ip);
        c.Controls.Add(Lbl("PIN", 290, 127, true));
        _pin.Location = new Point(324, 124); c.Controls.Add(_pin);
        _usb.ForeColor = SubInk; _usb.Location = new Point(12, 160); c.Controls.Add(_usb);
        return c;
    }

    private Panel OutputCard()
    {
        var c = Card(206);
        c.Controls.Add(Title("2  ·  Send audio to"));
        _device.Location = new Point(12, 34); c.Controls.Add(_device);
        c.Controls.Add(Lbl("Speakers to hear it, or a virtual cable (\"CABLE Input\") to feed Discord/OBS. Switches live.", 12, 62, true, CardW - 24));
        c.Controls.Add(Lbl("Volume", 12, 112)); _volume.Location = new Point(96, 108); c.Controls.Add(_volume);
        _volumeLbl.ForeColor = SubInk; _volumeLbl.Location = new Point(352, 112); c.Controls.Add(_volumeLbl);
        c.Controls.Add(Lbl("Noise cut", 12, 158)); _noise.Location = new Point(96, 154); c.Controls.Add(_noise);
        _noiseLbl.ForeColor = SubInk; _noiseLbl.Location = new Point(352, 158); c.Controls.Add(_noiseLbl);
        return c;
    }

    private Panel VoiceCard()
    {
        var c = Card(118);
        c.Controls.Add(Title("3  ·  Noise engine & voice"));
        c.Controls.Add(Lbl("Engine", 12, 38, true)); _noiseEngine.Location = new Point(84, 34); c.Controls.Add(_noiseEngine);
        c.Controls.Add(Lbl("Voice", 12, 78, true));
        _search.Location = new Point(84, 74); _search.PlaceholderText = "search…"; c.Controls.Add(_search);
        _effect.Location = new Point(84 + _search.Width + 8, 74); c.Controls.Add(_effect);
        return c;
    }

    private Panel ButtonsCard()
    {
        var c = new Panel { Width = CardW, Height = 52, BackColor = BgColor };
        StyleButton(_start, GreenColor); _start.Location = new Point(0, 8); c.Controls.Add(_start);
        StyleButton(_stop, RedColor); _stop.Location = new Point(142, 8); c.Controls.Add(_stop);
        _update.Location = new Point(284, 18); c.Controls.Add(_update);
        return c;
    }

    private void StyleButton(Button b, Color color)
    {
        b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0;
        b.BackColor = color; b.ForeColor = Color.White;
        b.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold); b.Cursor = Cursors.Hand;
    }

    private string Effect() => _effect.SelectedItem?.ToString() ?? "None";
    private string? Pin() => string.IsNullOrWhiteSpace(_pin.Text) ? null : _pin.Text.Trim();

    private void FilterEffects()
    {
        string q = _search.Text.Trim().ToLowerInvariant();
        var matches = Fx.Presets.Where(p => p.ToLowerInvariant().Contains(q)).ToArray();
        if (matches.Length == 0) matches = Fx.Presets;
        string current = Effect();
        _effect.Items.Clear();
        _effect.Items.AddRange(matches);
        _effect.SelectedItem = _effect.Items.Contains(current) ? current : matches[0];
    }

    private void Scan()
    {
        _scan.Enabled = false; _scan.Text = "Scanning...";
        SetStatus("Searching the Wi-Fi for phones (3s)...");
        Task.Run(() =>
        {
            var phones = Discovery.Discover();
            BeginInvoke(() =>
            {
                _found = phones; _phones.Items.Clear();
                foreach (var p in phones) _phones.Items.Add(p.ToString());
                if (phones.Count > 0) { _phones.SelectedIndex = 0; SetStatus($"Found {phones.Count} phone(s). Pick one and press Start."); }
                else SetStatus("No phones found. Open OpenMic on the phone, press Start, then Scan again — or type the IP.");
                _scan.Enabled = true; _scan.Text = "Scan Wi-Fi";
            });
        });
    }

    private (string? host, int port) SelectedPhone()
    {
        if (!string.IsNullOrWhiteSpace(_ip.Text)) return (_ip.Text.Trim(), Protocol.DefaultPort);
        int i = _phones.SelectedIndex;
        if (i >= 0 && i < _found.Count) return (_found[i].Ip, _found[i].Port);
        return (null, Protocol.DefaultPort);
    }

    private void Start()
    {
        string? host; int port;
        if (_usb.Checked)
        {
            string err = Usb.Forward(Protocol.DefaultPort);
            if (err != "") { SetStatus("USB: " + err); return; }
            host = "127.0.0.1"; port = Protocol.DefaultPort;
        }
        else
        {
            (host, port) = SelectedPhone();
            if (host == null) { SetStatus("Pick a scanned phone or type its IP first."); return; }
        }
        SaveSettings();

        _rx = new Receiver(host, port, Pin(), _device.SelectedItem?.ToString(),
            Effect(), _volume.Value / 100.0, _noise.Value / 100.0, _noiseEngine.Text);
        _rx.Status += s => BeginInvoke(() => SetStatus(s));
        _rx.Stats += (f, l) => BeginInvoke(() => SetStatus($"● Streaming — {f} frames, {l} lost"));
        _rx.Stopped += ex => BeginInvoke(() =>
        {
            SetStatus(ex == null ? "Stopped." : "Error: " + ex.Message);
            _start.Enabled = true; _stop.Enabled = false; _rx = null;
        });
        _rx.Start();
        _start.Enabled = false; _stop.Enabled = true;
    }

    private async Task CheckForUpdate()
    {
        string? version = await _updater.CheckAndPrepareAsync();
        if (version != null) { _update.Text = $"Update to {version} ready — click to restart"; _update.Visible = true; }
    }

    private void SetStatus(string s) => _status.Text = s;

    // ---- system tray ----
    private void SetupTray()
    {
        _tray.Icon = System.Drawing.SystemIcons.Application;
        _tray.Text = "OpenMic"; _tray.Visible = true;
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open OpenMic", null, (_, _) => ShowFromTray());
        menu.Items.Add("Quit", null, (_, _) => ExitApp());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowFromTray();
    }
    private void ShowFromTray() { Show(); WindowState = FormWindowState.Normal; ShowInTaskbar = true; Activate(); }
    private void ExitApp() { _reallyExit = true; Close(); }
    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_reallyExit && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true; Hide(); ShowInTaskbar = false;
            _tray.ShowBalloonTip(2000, "OpenMic", "Still running in the tray. Right-click to quit.", ToolTipIcon.Info);
            return;
        }
        _rx?.Stop(); SaveSettings(); _tray.Visible = false; _tray.Dispose();
    }

    // ---- settings ----
    private static string SettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenMic", "settings.json");

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(SettingsPath));
            var r = doc.RootElement;
            if (r.TryGetProperty("ip", out var ip)) _ip.Text = ip.GetString() ?? "";
            if (r.TryGetProperty("pin", out var pin)) _pin.Text = pin.GetString() ?? "";
            if (r.TryGetProperty("usb", out var usb)) _usb.Checked = usb.GetBoolean();
            if (r.TryGetProperty("device", out var dv) && _device.Items.Contains(dv.GetString())) _device.SelectedItem = dv.GetString();
            if (r.TryGetProperty("effect", out var ef) && _effect.Items.Contains(ef.GetString())) _effect.SelectedItem = ef.GetString();
            if (r.TryGetProperty("engine", out var en) && _noiseEngine.Items.Contains(en.GetString())) _noiseEngine.SelectedItem = en.GetString();
            if (r.TryGetProperty("volume", out var vol)) { _volume.Value = Math.Clamp((int)(vol.GetDouble() * 100), 0, 200); _volumeLbl.Text = _volume.Value + "%"; }
            if (r.TryGetProperty("noise", out var ns)) { _noise.Value = Math.Clamp((int)(ns.GetDouble() * 100), 0, 100); _noiseLbl.Text = _noise.Value == 0 ? "off" : _noise.Value + "%"; }
        }
        catch { }
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["ip"] = _ip.Text.Trim(), ["pin"] = _pin.Text.Trim(), ["usb"] = _usb.Checked,
                ["device"] = _device.SelectedItem?.ToString(), ["effect"] = Effect(),
                ["engine"] = _noiseEngine.SelectedItem?.ToString(),
                ["volume"] = _volume.Value / 100.0, ["noise"] = _noise.Value / 100.0,
            }));
        }
        catch { }
    }
}
