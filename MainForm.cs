namespace WinMonitorVolume;

internal sealed class MainForm : Form
{
    private static readonly int[] StartupPercents = { 0, 50, 100 };

    private readonly ComboBox _monitorBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly TrackBar _slider = new() { Minimum = 0, Maximum = 100, TickFrequency = 10, Dock = DockStyle.Fill };
    private readonly Label _valueLabel = new() { Text = "–", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 14, FontStyle.Bold) };
    private readonly Button _maxButton = new() { Text = "Set to MAX", AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), Padding = new Padding(10, 4, 10, 4) };
    private readonly Button _refreshButton = new() { Text = "Refresh", AutoSize = true };
    private readonly RadioButton _startOff = new() { Text = "Don't start automatically", AutoSize = true };
    private readonly RadioButton _startTray = new() { Text = "Start this app in the system tray", AutoSize = true };
    private readonly RadioButton _startSet = new() { Text = "Set the volume to", AutoSize = true };
    private readonly ComboBox _startPercent = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    private readonly Label _status = new() { AutoSize = true, Dock = DockStyle.Fill, ForeColor = SystemColors.GrayText, MaximumSize = new Size(440, 0) };
    private readonly NotifyIcon _tray = new();
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 150 };
    private readonly RegisteredWaitHandle _showWait;

    private List<Ddc.Monitor> _monitors = new();
    private uint _max = 100;
    private bool _updating;
    private bool _loadingStartup;
    private bool _allowShow;
    private bool _exiting;
    private bool _trayHintShown;
    private readonly bool _startedInTray;

    public MainForm(bool startInTray, EventWaitHandle showEvent)
    {
        _startedInTray = startInTray;
        _allowShow = !startInTray;

        Text = Program.AppName;
        Icon = Program.AppIcon;
        Font = new Font("Segoe UI", 10);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        // Menu
        var menu = new MenuStrip();
        var appMenu = new ToolStripMenuItem("&App");
        appMenu.DropDownItems.Add("&Refresh monitors", null, (_, _) => LoadMonitors());
        appMenu.DropDownItems.Add(new ToolStripSeparator());
        appMenu.DropDownItems.Add("E&xit", null, (_, _) => ExitApp());
        var helpMenu = new ToolStripMenuItem("&Help");
        helpMenu.DropDownItems.Add($"&About {Program.AppName}", null, (_, _) => ShowAbout());
        menu.Items.Add(appMenu);
        menu.Items.Add(helpMenu);
        MainMenuStrip = menu;

        // Volume controls
        var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(12) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label { Text = "Monitor:", AutoSize = true }, 0, 0);
        layout.Controls.Add(_monitorBox, 0, 1);
        layout.Controls.Add(_refreshButton, 1, 1);
        layout.Controls.Add(new Label { Text = "Speaker volume:", AutoSize = true, Margin = new Padding(3, 12, 3, 0) }, 0, 2);
        layout.Controls.Add(_slider, 0, 3);
        layout.Controls.Add(_valueLabel, 1, 3);
        layout.Controls.Add(_maxButton, 0, 4);
        layout.SetColumnSpan(_maxButton, 2);
        _maxButton.Anchor = AnchorStyles.None;

        // Start-with-Windows options
        foreach (int p in StartupPercents) _startPercent.Items.Add($"{p}%");
        var startGrid = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, Dock = DockStyle.Fill };
        startGrid.Controls.Add(_startOff, 0, 0);
        startGrid.SetColumnSpan(_startOff, 3);
        startGrid.Controls.Add(_startTray, 0, 1);
        startGrid.SetColumnSpan(_startTray, 3);
        startGrid.Controls.Add(_startSet, 0, 2);
        startGrid.Controls.Add(_startPercent, 1, 2);
        startGrid.Controls.Add(new Label { Text = "and exit (no window)", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 2);
        var startBox = new GroupBox { Text = "When I sign in to Windows", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, Margin = new Padding(3, 12, 3, 3) };
        startBox.Controls.Add(startGrid);
        layout.Controls.Add(startBox, 0, 5);
        layout.SetColumnSpan(startBox, 2);

        layout.Controls.Add(_status, 0, 6);
        layout.SetColumnSpan(_status, 2);

        // Form.AutoSize doesn't account for a docked menu, so size the window to the content ourselves.
        Controls.Add(menu);
        Controls.Add(layout);
        void FitToContent()
        {
            layout.Location = new Point(0, menu.Height);
            ClientSize = new Size(layout.Width, menu.Height + layout.Height);
        }
        layout.SizeChanged += (_, _) => FitToContent();
        FitToContent();

        // Tray icon
        var trayMenu = new ContextMenuStrip();
        var openItem = trayMenu.Items.Add($"Open {Program.AppName}", null, (_, _) => ShowFromTray());
        openItem.Font = new Font(openItem.Font, FontStyle.Bold);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Volume 100%", null, (_, _) => SetAllFromTray(100));
        trayMenu.Items.Add("Volume 50%", null, (_, _) => SetAllFromTray(50));
        trayMenu.Items.Add("Volume 0%", null, (_, _) => SetAllFromTray(0));
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("About", null, (_, _) => ShowAbout());
        trayMenu.Items.Add("Exit", null, (_, _) => ExitApp());
        _tray.Icon = new Icon(Program.AppIcon, SystemInformation.SmallIconSize);
        _tray.Text = Program.AppName;
        _tray.ContextMenuStrip = trayMenu;
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowFromTray(); };

        // Events
        _refreshButton.Click += (_, _) => LoadMonitors();
        _monitorBox.SelectedIndexChanged += (_, _) => ReadVolume();
        _maxButton.Click += (_, _) => SetVolume(_max, updateSlider: true);
        _slider.ValueChanged += (_, _) =>
        {
            _valueLabel.Text = _slider.Value.ToString();
            if (!_updating) { _debounce.Stop(); _debounce.Start(); }
        };
        _debounce.Tick += (_, _) => { _debounce.Stop(); SetVolume((uint)_slider.Value, updateSlider: false); };
        foreach (var rb in new[] { _startOff, _startTray, _startSet })
            rb.CheckedChanged += (s, _) => { if (((RadioButton)s!).Checked) ApplyStartup(); };
        _startPercent.SelectedIndexChanged += (_, _) => ApplyStartup();

        LoadStartup();

        // A second launch of the app signals this event to bring this window up.
        _showWait = ThreadPool.RegisterWaitForSingleObject(showEvent,
            (_, _) => { if (IsHandleCreated) BeginInvoke(ShowFromTray); }, null, Timeout.Infinite, false);
    }

    private StartupMode SelectedStartupMode =>
        _startTray.Checked ? StartupMode.Tray : _startSet.Checked ? StartupMode.SetVolume : StartupMode.Off;

    private int SelectedStartupPercent => StartupPercents[Math.Max(0, _startPercent.SelectedIndex)];

    /// <summary>Closing the window hides it to the tray when the app is meant to live there.</summary>
    private bool KeepInTray => _startedInTray || SelectedStartupMode == StartupMode.Tray;

    private Ddc.Monitor? Selected => _monitorBox.SelectedItem as Ddc.Monitor;

    // ---- Window / tray lifecycle ----

    protected override void SetVisibleCore(bool value)
    {
        // When started with --tray, keep the window hidden until the user opens it from the tray.
        if (!_allowShow)
        {
            value = false;
            if (!IsHandleCreated) CreateHandle();
        }
        base.SetVisibleCore(value);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        // Re-read monitors each time the window appears: handles go stale after sleep or display changes.
        if (Visible) BeginInvoke(LoadMonitors);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exiting && e.CloseReason == CloseReason.UserClosing && KeepInTray)
        {
            e.Cancel = true;
            Hide();
            if (!_trayHintShown)
            {
                _trayHintShown = true;
                _tray.ShowBalloonTip(3000, Program.AppName, "Still running in the system tray. Right-click the icon for options.", ToolTipIcon.Info);
            }
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _showWait.Unregister(null);
        _tray.Visible = false;
        _tray.Dispose();
        DisposeMonitors();
        base.OnFormClosed(e);
    }

    private void ShowFromTray()
    {
        _allowShow = true;
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ExitApp()
    {
        _exiting = true;
        Close();
    }

    private void ShowAbout()
    {
        using var about = new AboutForm();
        if (Visible) about.ShowDialog(this);
        else { about.StartPosition = FormStartPosition.CenterScreen; about.ShowDialog(); }
    }

    private async void SetAllFromTray(int percent)
    {
        int count = await Task.Run(() => Ddc.SetVolumePercentAll(percent));
        if (count == 0)
            _tray.ShowBalloonTip(3000, Program.AppName, "No monitor answered. Check that DDC/CI is on in the monitor's menu.", ToolTipIcon.Warning);
        if (Visible) ReadVolume();
    }

    // ---- Start with Windows ----

    private void LoadStartup()
    {
        _loadingStartup = true;
        var (mode, percent) = Startup.Read();
        int idx = Array.IndexOf(StartupPercents, percent);
        _startPercent.SelectedIndex = idx >= 0 ? idx : StartupPercents.Length - 1;
        (mode switch { StartupMode.Tray => _startTray, StartupMode.SetVolume => _startSet, _ => _startOff }).Checked = true;
        _startPercent.Enabled = _startSet.Checked;
        _tray.Visible = KeepInTray;
        _loadingStartup = false;
    }

    private void ApplyStartup()
    {
        _startPercent.Enabled = _startSet.Checked;
        _tray.Visible = KeepInTray;
        if (_loadingStartup) return;
        try
        {
            Startup.Write(SelectedStartupMode, SelectedStartupPercent);
            SetStatus(SelectedStartupMode switch
            {
                StartupMode.Tray => "At sign-in, the app will start in the system tray.",
                StartupMode.SetVolume => $"At sign-in, the monitor volume will be set to {SelectedStartupPercent}% and the app will exit.",
                _ => "The app won't start automatically.",
            }, false);
        }
        catch (Exception ex)
        {
            SetStatus("Couldn't save the startup setting: " + ex.Message, true);
        }
    }

    // ---- Volume ----

    private void DisposeMonitors()
    {
        foreach (var m in _monitors) m.Dispose();
        _monitors.Clear();
    }

    private void LoadMonitors()
    {
        UseWaitCursor = true;
        DisposeMonitors();
        _monitorBox.Items.Clear();
        _monitors = Ddc.GetMonitors();

        // Prefer a monitor that actually answers the volume query, otherwise the first.
        int pick = -1;
        foreach (var m in _monitors)
        {
            _monitorBox.Items.Add(m);
            if (pick < 0 && m.TryGet(Ddc.VcpSpeakerVolume, out _, out uint max) && max > 0) pick = m.Index;
        }
        UseWaitCursor = false;

        if (_monitors.Count == 0) { SetEnabled(false); SetStatus("No monitors found.", true); return; }
        _monitorBox.SelectedIndex = pick >= 0 ? pick : 0;
    }

    private void ReadVolume()
    {
        var m = Selected;
        if (m == null) return;
        if (m.TryGet(Ddc.VcpSpeakerVolume, out uint cur, out uint max) && max > 0)
        {
            _max = max;
            _updating = true;
            _slider.Maximum = (int)max;
            _slider.TickFrequency = Math.Max(1, (int)max / 10);
            _slider.Value = (int)Math.Min(cur, max);
            _valueLabel.Text = cur.ToString();
            _updating = false;
            SetEnabled(true);
            SetStatus($"Current volume {cur} of {max}.", false);
        }
        else
        {
            SetEnabled(false);
            _valueLabel.Text = "–";
            SetStatus("This monitor did not answer. Turn on DDC/CI in the monitor's on-screen menu, or pick another monitor.", true);
        }
    }

    private void SetVolume(uint value, bool updateSlider)
    {
        var m = Selected;
        if (m == null) return;
        if (m.SetVolume(value))
        {
            if (updateSlider)
            {
                _updating = true;
                _slider.Value = (int)Math.Min(value, (uint)_slider.Maximum);
                _valueLabel.Text = value.ToString();
                _updating = false;
            }
            SetStatus($"Volume set to {value} of {_max}.", false);
        }
        else
        {
            SetStatus("Failed to set volume over DDC/CI.", true);
        }
    }

    private void SetEnabled(bool on) { _slider.Enabled = on; _maxButton.Enabled = on; }

    private void SetStatus(string text, bool error)
    {
        _status.Text = text;
        _status.ForeColor = error ? Color.Firebrick : SystemColors.GrayText;
    }
}
