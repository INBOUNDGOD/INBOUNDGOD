using System.Text.Json;

namespace OpenMic;

public sealed class MainForm : Form
{
    private readonly ListBox _phones = new() { Height = 86 };
    private readonly Button _scan = new() { Text = "Scan Wi-Fi", Width = 100 };
    private readonly TextBox _ip = new() { Width = 130 };
    private readonly TextBox _pin = new() { Width = 70 };
    private readonly CheckBox _usb = new() { Text = "USB cable (adb) — lower latency", AutoSize = true };
    private readonly ComboBox _device = new() { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TrackBar _volume = new() { Minimum = 0, Maximum = 200, Value = 100, Width = 240, TickFrequency = 25 };
    private readonly Label _volumeLbl = new() { AutoSize = true, Text = "100%" };
    private readonly TrackBar _noise = new() { Minimum = 0, Maximum = 100, Value = 0, Width = 240, TickFrequency = 20 };
    private readonly Label _noiseLbl = new() { AutoSize = true, Text = "off" };
    private readonly TextBox _search = new() { Width = 110 };
    private readonly ComboBox _effect = new() { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _start = new() { Text = "Start", Width = 90 };
    private readonly Button _stop = new() { Text = "Stop", Width = 90, Enabled = false };
    private readonly Label _status = new() { AutoSize = false, Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(10, 6, 10, 6) };
    private readonly LinkLabel _update = new() { Text = "", AutoSize = true, Visible = false };

    private readonly UpdateService _updater = new();
    private List<Phone> _found = new();
    private Receiver? _rx;

    private readonly NotifyIcon _tray = new();
    private bool _reallyExit;

    public MainForm()
    {
        Text = "OpenMic";
        ClientSize = new Size(440, 600);
        Font = new Font("Segoe UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BuildUi();

        foreach (var d in AudioOutput.OutputDevices()) _device.Items.Add(d);
        foreach (var p in Fx.Presets) _effect.Items.Add(p);
        _effect.SelectedIndex = 0;
        LoadSettings();

        _scan.Click += (_, _) => Scan();
        _start.Click += (_, _) => Start();
        _stop.Click += (_, _) => _rx?.Stop();
        _device.SelectedIndexChanged += (_, _) => { _rx?.SetDevice(_device.Text); SaveSettings(); };
        _effect.SelectedIndexChanged += (_, _) => { _rx?.SetEffect(Effect()); SaveSettings(); };
        _search.TextChanged += (_, _) => FilterEffects();
        _volume.Scroll += (_, _) => { _volumeLbl.Text = _volume.Value + "%"; _rx?.SetVolume(_volume.Value / 100.0); };
        _noise.Scroll += (_, _) => { _noiseLbl.Text = _noise.Value == 0 ? "off" : _noise.Value + "%"; _rx?.SetNoise(_noise.Value / 100.0); };
        _update.LinkClicked += (_, _) => _updater.ApplyAndRestart();
        SetupTray();
        FormClosing += OnFormClosing;

        SetStatus(AudioOutput.OutputDevices().Count == 0
            ? "No output devices found."
            : "Idle. Scan for your phone, or type its IP.");

        Shown += async (_, _) => await CheckForUpdate();
    }

    private void BuildUi()
    {
        int y = 12;
        Controls.Add(new Label { Text = "OpenMic", Font = new Font("Segoe UI", 15F, FontStyle.Bold), AutoSize = true, Location = new Point(12, y) });
        y += 34;
        Controls.Add(new Label { Text = "Turn your phone into a PC microphone.", AutoSize = true, Location = new Point(12, y) });
        y += 28;

        Controls.Add(new Label { Text = "1. Phone", Font = new Font("Segoe UI", 9F, FontStyle.Bold), AutoSize = true, Location = new Point(12, y) });
        y += 20;
        _phones.SetBounds(12, y, 416, 86); Controls.Add(_phones); y += 92;
        _scan.Location = new Point(12, y); Controls.Add(_scan);
        Controls.Add(new Label { Text = "or IP:", AutoSize = true, Location = new Point(122, y + 4) });
        _ip.Location = new Point(166, y); Controls.Add(_ip);
        Controls.Add(new Label { Text = "PIN:", AutoSize = true, Location = new Point(306, y + 4) });
        _pin.Location = new Point(348, y); Controls.Add(_pin);
        y += 32;
        _usb.Location = new Point(12, y); Controls.Add(_usb);
        y += 30;

        Controls.Add(new Label { Text = "2. Send audio to", Font = new Font("Segoe UI", 9F, FontStyle.Bold), AutoSize = true, Location = new Point(12, y) });
        y += 20;
        _device.Location = new Point(12, y); Controls.Add(_device); y += 28;
        Controls.Add(new Label { Text = "Speakers to hear it, or a virtual cable (\"CABLE Input\") to feed other apps. Switches live.", AutoSize = false, Width = 416, Height = 30, ForeColor = Color.Gray, Location = new Point(12, y) });
        y += 32;
        Controls.Add(new Label { Text = "Volume", AutoSize = true, Location = new Point(12, y + 2) });
        _volume.Location = new Point(70, y); Controls.Add(_volume);
        _volumeLbl.Location = new Point(316, y + 2); Controls.Add(_volumeLbl);
        y += 44;
        Controls.Add(new Label { Text = "Noise cut", AutoSize = true, Location = new Point(12, y + 2) });
        _noise.Location = new Point(70, y); Controls.Add(_noise);
        _noiseLbl.Location = new Point(316, y + 2); Controls.Add(_noiseLbl);
        y += 48;

        Controls.Add(new Label { Text = "3. Voice changer", Font = new Font("Segoe UI", 9F, FontStyle.Bold), AutoSize = true, Location = new Point(12, y) });
        y += 20;
        Controls.Add(new Label { Text = "search:", AutoSize = true, Location = new Point(12, y + 4) });
        _search.Location = new Point(64, y); Controls.Add(_search);
        _effect.Location = new Point(184, y); Controls.Add(_effect);
        y += 36;
        Controls.Add(new Label { Text = "Sliders and voice changer all change live while streaming.", AutoSize = true, ForeColor = Color.Gray, Location = new Point(12, y) });
        y += 28;

        _start.Location = new Point(12, y); Controls.Add(_start);
        _stop.Location = new Point(110, y); Controls.Add(_stop);
        _update.Location = new Point(210, y + 4); Controls.Add(_update);

        Controls.Add(_status);
    }

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
                else SetStatus("No phones found. Open OpenMic on the phone and press Start there, then Scan again — or type the IP.");
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
            Effect(), _volume.Value / 100.0, _noise.Value / 100.0);
        _rx.Status += s => BeginInvoke(() => SetStatus(s));
        _rx.Stats += (f, l) => BeginInvoke(() => SetStatus($"Streaming — {f} frames, {l} lost"));
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

    // ---- system tray (keep streaming when the window is closed) ----
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

    private void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        ShowInTaskbar = true;
        Activate();
    }

    private void ExitApp()
    {
        _reallyExit = true;
        Close();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        // Clicking the window's X hides to the tray and keeps streaming.
        if (!_reallyExit && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            ShowInTaskbar = false;
            _tray.ShowBalloonTip(2000, "OpenMic",
                "Still running in the tray. Right-click the icon to quit.",
                ToolTipIcon.Info);
            return;
        }
        _rx?.Stop();
        SaveSettings();
        _tray.Visible = false;
        _tray.Dispose();
    }

    // ---- settings (%APPDATA%/OpenMic/settings.json) ----
    private static string SettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "OpenMic", "settings.json");

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
            if (r.TryGetProperty("device", out var dv) && _device.Items.Contains(dv.GetString()))
                _device.SelectedItem = dv.GetString();
            if (r.TryGetProperty("effect", out var ef) && _effect.Items.Contains(ef.GetString()))
                _effect.SelectedItem = ef.GetString();
            if (r.TryGetProperty("volume", out var vol))
            { _volume.Value = Math.Clamp((int)(vol.GetDouble() * 100), 0, 200); _volumeLbl.Text = _volume.Value + "%"; }
            if (r.TryGetProperty("noise", out var ns))
            { _noise.Value = Math.Clamp((int)(ns.GetDouble() * 100), 0, 100); _noiseLbl.Text = _noise.Value == 0 ? "off" : _noise.Value + "%"; }
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
                ["volume"] = _volume.Value / 100.0,
                ["noise"] = _noise.Value / 100.0,
            };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(obj));
        }
        catch { }
    }
}
