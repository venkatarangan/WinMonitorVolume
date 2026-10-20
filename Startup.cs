using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace WinMonitorVolume;

internal enum StartupMode { Off, Tray, SetVolume }

/// <summary>Start-with-Windows setting, stored in the current user's Run key (no admin rights needed).</summary>
internal static class Startup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static (StartupMode Mode, int Percent) Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        if (key?.GetValue(Program.AppName) is not string command) return (StartupMode.Off, 100);
        if (command.Contains("--tray", StringComparison.OrdinalIgnoreCase)) return (StartupMode.Tray, 100);
        var m = Regex.Match(command, @"--set\s+(\d+)", RegexOptions.IgnoreCase);
        if (m.Success) return (StartupMode.SetVolume, int.Parse(m.Groups[1].Value));
        if (command.Contains("--max", StringComparison.OrdinalIgnoreCase)) return (StartupMode.SetVolume, 100);
        return (StartupMode.Off, 100);
    }

    public static void Write(StartupMode mode, int percent)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        string exe = $"\"{Environment.ProcessPath}\"";
        switch (mode)
        {
            case StartupMode.Tray: key.SetValue(Program.AppName, $"{exe} --tray"); break;
            case StartupMode.SetVolume: key.SetValue(Program.AppName, $"{exe} --set {percent}"); break;
            default: key.DeleteValue(Program.AppName, throwOnMissingValue: false); break;
        }
    }
}
