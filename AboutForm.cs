using System.Diagnostics;

namespace WinMonitorVolume;

internal sealed class AboutForm : Form
{
    private const string MangoidiotsUrl = "https://mangoidiots.com";
    private const string MonitorControlUrl = "https://github.com/MonitorControl/";

    public AboutForm()
    {
        Text = $"About {Program.AppName}";
        Icon = Program.AppIcon;
        Font = new Font("Segoe UI", 10);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(16) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380));

        var logo = new PictureBox
        {
            Image = new Icon(Program.AppIcon, 64, 64).ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(64, 64),
            Margin = new Padding(0, 0, 16, 0),
        };
        layout.Controls.Add(logo, 0, 0);
        layout.SetRowSpan(logo, 6);

        layout.Controls.Add(new Label { Text = Program.AppName, AutoSize = true, Font = new Font("Segoe UI", 14, FontStyle.Bold) }, 1, 0);
        layout.Controls.Add(new Label { Text = $"Version {Program.Version}", AutoSize = true, ForeColor = SystemColors.GrayText }, 1, 1);
        layout.Controls.Add(Paragraph(
            "Sets your monitor's built-in speaker volume from Windows over DDC/CI, so you don't have to " +
            "reach for the control knob on the monitor. Windows' own volume only works inside the limit " +
            "set on the monitor; this app changes that limit."), 1, 2);
        layout.Controls.Add(Paragraph("Tested on Windows 11 with an LG monitor connected by a USB-C cable."), 1, 3);
        layout.Controls.Add(Link("Written by Mangoidiots.com using Claude AI Opus.", "Mangoidiots.com", MangoidiotsUrl), 1, 4);
        layout.Controls.Add(Link("Inspired by the MonitorControl app for macOS.", "MonitorControl", MonitorControlUrl), 1, 5);

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(3, 12, 3, 3) };
        layout.Controls.Add(ok, 1, 6);
        AcceptButton = CancelButton = ok;

        Controls.Add(layout);
    }

    private static Label Paragraph(string text) =>
        new() { Text = text, AutoSize = true, MaximumSize = new Size(380, 0), Margin = new Padding(3, 10, 3, 0) };

    private static LinkLabel Link(string text, string linkText, string url)
    {
        var label = new LinkLabel { Text = text, AutoSize = true, MaximumSize = new Size(380, 0), Margin = new Padding(3, 10, 3, 0) };
        label.LinkArea = new LinkArea(text.IndexOf(linkText, StringComparison.Ordinal), linkText.Length);
        label.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        return label;
    }
}
