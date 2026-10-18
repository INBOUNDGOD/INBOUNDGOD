using System.Drawing.Drawing2D;
using System.Text.Json;

namespace OpenMic;

public sealed class MainForm : Form
{
    // --- dark palette ---
    private static readonly Color Bg = Color.FromArgb(30, 31, 34);
    private static readonly Color CardColor = Color.FromArgb(43, 45, 49);
    private static readonly Color Border = Color.FromArgb(58, 60, 66);
    private static readonly Color TextColor = Color.FromArgb(227, 229, 232);
    private static readonly Color SubText = Color.FromArgb(168, 171, 178);
    private static readonly Color Accent = Color.FromArgb(88, 101, 242);
    private static readonly Color Green = Color.FromArgb(59, 165, 93);
    private static readonly Color Red = Color.FromArgb(237, 66, 69);

    private readonly ListBox _phones = new();
    private readonly Button _scan = new() { Text = "Scan Wi-Fi", Width = 110, Height = 30 };
    private readonly TextBox _ip = new() { Width = 120 };
    private readonly TextBox _pin = new() { Width = 64 };
    private readonly CheckBox _usb = new() { Text = "USB cable (adb) — lowest latency", AutoSize = true };
    private readonly ComboBox _device = new() { Width = 360, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TrackBar _volume = new() { Minimum = 0, Maximum = 200, Value = 100, Width = 250, TickStyle = TickStyle.None };
    private readonly Label _volumeLbl = new() { AutoSize = true, Text = "100%" };
    private readonly TrackBar _noise = new() { Minimum = 0, Maximum = 100, Value = 0, Width = 250, TickStyle = TickStyle.None };
    private readonly Label _noiseLbl = new() { AutoSize = true, Text = "off" };
    private readonly ComboBox _noiseEngine = new() { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _search = new() { Width = 120 };
    private readonly ComboBox _effect = new() { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _start = new() { Text = "Start", Width = 110, Height = 34 };
    private readonly Button _stop = new() { Text = "Stop", Width = 110, Height = 34, Enabled = false };
    private readonly Label _status = new() { AutoSize = false, Dock = DockStyle.Bottom, Height = 46, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(16, 0, 16, 0) };
    private readonly LinkLabel _update = new() { Text = "", AutoSize = true, Visible = false, LinkColor = Accent };

    private readonly NotifyIcon _tray = new();
    private bool _reallyExit;
    private readonly UpdateService _updater = new();
    private List<Phone> _found = new();
    private Receiver? _rx;

    public MainForm()
    {
        Text = "OpenMic";
        ClientSize = new Size(470, 650);
        BackColor = Bg;
        ForeColor = TextColor;
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
        _update.LinkClicked += (_, _) => _updater.ApplyAndRestart();
        SetupTray();
        FormClosing += OnFormClosing;

        SetStatus(AudioOutput.OutputDevices().Count == 0
            ? "No output devices found."
            : "Ready. Scan for your phone, or type its IP.");
        Shown += async (_, _) => await CheckForUpdate();
    }

    // ---- styled building blocks ----
    private Label Header(string t) => new()
    { Text = t.ToUpperInvariant(), ForeColor = Accent, AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };

    private Label Lbl(string t, bool sub = false) => new()
    { Text = t, ForeColor = sub ? SubText : TextColor, AutoSize = true, BackColor = Color.Transparent };

    private Panel Card(int x, int y, int w, int h)
    {
        var p = new Panel { Left = x, Top = y, Width = w, Height = h, BackColor = CardColor };
        p.Paint += (s, e) =>
        {
            using var pen = new Pen(Border);
            e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
        };
        Controls.Add(p);
        return p;
    }

    private void StyleButton(Button b, Color color)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.BackColor = color;
        b.ForeColor = Color.White;
        b.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        b.Cursor = Cursors.Hand;
    }

    private void StyleInput(Control c)
    {
        c.BackColor = Color.FromArgb(54, 57, 63);
        c.ForeColor = TextColor;
        if (c is ComboBox cb) cb.FlatStyle = FlatStyle.Flat;
        if (c is TextBox tb) tb.BorderStyle = BorderStyle.FixedSingle;
        if (c is TrackBar tr) tr.BackColor = CardColor;
    }

    private void BuildUi()
    {
        // header band
        var head = new Panel { Left = 0, Top = 0, Width = ClientSize.Width, Height = 60, BackColor = Accent };
        head.Controls.Add(new Label { Text = "OpenMic", ForeColor = Color.White, AutoSize = true, Location = new Point(16, 10), Font = new Font("Segoe UI", 16F, FontStyle.Bold) });
        head.Controls.Add(new Label { Text = "Your phone as a PC microphone", ForeColor = Color.FromArgb(220, 223, 255), AutoSize = true, Location = new Point(18, 38), Font = new Font("Segoe UI", 8.5F) });
        Controls.Add(head);

        int x = 16, w = 438, y = 72;

        // 1. Phone
        var c1 = Card(x, y, w, 196);
        c1.Controls.Add(Hdr("1  Phone", 12, 10));
        _phones.SetBounds(12, 32, w - 24, 76);
        StyleInput(_phones); _phones.BorderStyle = BorderStyle.None;
        c1.Controls.Add(_phones);
        StyleButton(_scan, Accent); _scan.Location = new Point(12, 116); c1.Controls.Add(_scan);
        var ipl = Lbl("IP"); ipl.Location = new Point(134, 122); c1.Controls.Add(ipl);
        StyleInput(_ip); _ip.Location = new Point(158, 119); c1.Controls.Add(_ip);
        var pl = Lbl("PIN"); pl.Location = new Point(288, 122); c1.Controls.Add(pl);
        StyleInput(_pin); _pin.Location = new Point(322, 119); c1.Controls.Add(_pin);
        _usb.ForeColor = SubText; _usb.Location = new Point(12, 158); c1.Controls.Add(_usb);
        y += 208;

        // 2. Output + sliders
        var c2 = Card(x, y, w, 188);
        c2.Controls.Add(Hdr("2  Send audio to", 12, 10));
        StyleInput(_device); _device.Location = new Point(12, 32); c2.Controls.Add(_device);
        c2.Controls.Add(Sub("Speakers to hear it, or a virtual cable (\"CABLE Input\") to feed Discord/OBS. Switches live.", 12, 60, w - 24));
        c2.Controls.Add(Lbl2("Volume", 12, 100));
        StyleInput(_volume); _volume.Location = new Point(96, 96); c2.Controls.Add(_volume);
        _volumeLbl.ForeColor = SubText; _volumeLbl.Location = new Point(352, 100); c2.Controls.Add(_volumeLbl);
        c2.Controls.Add(Lbl2("Noise cut", 12, 142));
        StyleInput(_noise); _noise.Location = new Point(96, 138); c2.Controls.Add(_noise);
        _noiseLbl.ForeColor = SubText; _noiseLbl.Location = new Point(352, 142); c2.Controls.Add(_noiseLbl);
        y += 200;

        // 3. Noise engine + voice changer
        var c3 = Card(x, y, w, 120);
        c3.Controls.Add(Hdr("3  Noise suppression & voice", 12, 10));
        c3.Controls.Add(Lbl2("Engine", 12, 36));
        StyleInput(_noiseEngine); _noiseEngine.Location = new Point(96, 32); c3.Controls.Add(_noiseEngine);
        c3.Controls.Add(Lbl2("Voice", 12, 76));
        StyleInput(_search); _search.Location = new Point(96, 72); _search.PlaceholderText = "search…"; c3.Controls.Add(_search);
        StyleInput(_effect); _effect.Location = new Point(224, 72); c3.Controls.Add(_effect);
        y += 132;

        // controls
        StyleButton(_start, Green); _start.Location = new Point(16, y); Controls.Add(_start);
        StyleButton(_stop, Red); _stop.Location = new Point(138, y); Controls.Add(_stop);
        _update.Location = new Point(262, y + 8); Controls.Add(_update);

        _status.BackColor = Color.FromArgb(24, 25, 28);
        _status.ForeColor = SubText;
        Controls.Add(_status);
    }

    private Label Hdr(string t, int lx, int ly) { var l = Header(t); l.Location = new Point(lx, ly); return l; }
    private Label Sub(string t, int lx, int ly, int wd) { var l = Lbl(t, true); l.AutoSize = false; l.SetBounds(lx, ly, wd, 32); return l; }
    private Label Lbl2(string t, int lx, int ly) { var l = Lbl(t); l.Location = new Point(lx, ly); return l; }

    private string? Pin() => string.IsNullOrWhiteSpace(_pin.Text) ? null : _pin.Text.Trim();
    private string Effect() => _effect.SelectedItem?.ToString() ?? "None";

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
                _found = phones;
                _phones.Items.Clear();
                foreach (var p in phones) _phones.Items.Add(p.ToString());
                if (phones.Count > 0) { _phones.SelectedIndex = 0; SetStatus($"Found {phones.Count} phone(s). Pick one and press Start."); }
                else SetStatus("No phones found. Open OpenMic on the phone, press Start there, then Scan again — or type the IP.");
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
        if (version != null)
        {
            _update.Text = $"Update to {version} ready — click to restart";
            _update.Visible = true;
        }
    }

    private void SetStatus(string s) => _status.Text = s;

    // ---- system tray ----
    private void SetupTray()
    {
        _tray.Icon = System.Drawing.SystemIcons.Application;
        _tray.Text = "OpenMic";
        _tray.Visible = true;
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
            e.Cancel = true;
            Hide();
            ShowInTaskbar = false;
            _tray.ShowBalloonTip(2000, "OpenMic", "Still running in the tray. Right-click to quit.", ToolTipIcon.Info);
            return;
        }
        _rx?.Stop();
        SaveSettings();
        _tray.Visible = false;
        _tray.Dispose();
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
            var obj = new Dictionary<string, object?>
            {
                ["ip"] = _ip.Text.Trim(),
                ["pin"] = _pin.Text.Trim(),
                ["usb"] = _usb.Checked,
                ["device"] = _device.SelectedItem?.ToString(),
                ["effect"] = Effect(),
                ["engine"] = _noiseEngine.SelectedItem?.ToString(),
                ["volume"] = _volume.Value / 100.0,
                ["noise"] = _noise.Value / 100.0,
            };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(obj));
        }
        catch { }
    }
}
