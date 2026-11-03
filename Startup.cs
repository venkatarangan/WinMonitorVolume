using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace WinMonitorVolume;

internal enum StartupMode { Off, Tray, SetVolume }

/// <summary>
/// Start-with-Windows setting.
/// Unpackaged (zip / Install.ps1): stored in the current user's Run key (no admin rights needed).
/// Packaged (Microsoft Store MSIX): the Run key is virtualized and ignored by Windows, so the manifest's
/// StartupTask is switched on/off instead, and the chosen mode is kept in a small settings file.
/// </summary>
internal static class Startup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string StartupTaskId = "WinMonitorVolumeStartup";

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int length, char[]? name);

    private static bool? _packaged;
    /// <summary>True when running from an MSIX package (Microsoft Store build).</summary>
    public static bool IsPackaged => _packaged ??= DetectPackaged();

    private static bool DetectPackaged()
    {
        try { int len = 0; return GetCurrentPackageFullName(ref len, null) != 15700 /* APPMODEL_ERROR_NO_PACKAGE */; }
        catch { return false; }
    }

    private static string SettingsFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Program.AppName, "startup.txt");

    /// <summary>For the packaged build: was this process started by the sign-in startup task?</summary>
    public static bool LaunchedByStartupTask()
    {
        try { return AppInstance.GetActivatedEventArgs()?.Kind == Windows.ApplicationModel.Activation.ActivationKind.StartupTask; }
        catch { return false; }
    }

    private static T Wait<T>(Windows.Foundation.IAsyncOperation<T> op) => Task.Run(() => op.AsTask()).GetAwaiter().GetResult();

    public static (StartupMode Mode, int Percent) Read()
    {
        if (IsPackaged) return ReadPackaged();
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        if (key?.GetValue(Program.AppName) is not string command) return (StartupMode.Off, 100);
        return Parse(command);
    }

    public static void Write(StartupMode mode, int percent)
    {
        if (IsPackaged) { WritePackaged(mode, percent); return; }
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        string exe = $"\"{Environment.ProcessPath}\"";
        switch (mode)
        {
            case StartupMode.Tray: key.SetValue(Program.AppName, $"{exe} --tray"); break;
            case StartupMode.SetVolume: key.SetValue(Program.AppName, $"{exe} --set {percent}"); break;
            default: key.DeleteValue(Program.AppName, throwOnMissingValue: false); break;
        }
    }

    private static (StartupMode Mode, int Percent) Parse(string command)
    {
        if (command.Contains("--tray", StringComparison.OrdinalIgnoreCase)) return (StartupMode.Tray, 100);
        var m = Regex.Match(command, @"--set\s+(\d+)", RegexOptions.IgnoreCase);
        if (m.Success) return (StartupMode.SetVolume, int.Parse(m.Groups[1].Value));
        if (command.Contains("--max", StringComparison.OrdinalIgnoreCase)) return (StartupMode.SetVolume, 100);
        return (StartupMode.Off, 100);
    }

    private static (StartupMode Mode, int Percent) ReadPackaged()
    {
        try
        {
            var task = Wait(StartupTask.GetAsync(StartupTaskId));
            if (task.State is not (StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy)) return (StartupMode.Off, 100);
            return File.Exists(SettingsFile) ? Parse(File.ReadAllText(SettingsFile)) : (StartupMode.Tray, 100);
        }
        catch { return (StartupMode.Off, 100); }
    }

    private static void WritePackaged(StartupMode mode, int percent)
    {
        var task = Wait(StartupTask.GetAsync(StartupTaskId));
        if (mode == StartupMode.Off) { task.Disable(); return; }

        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        File.WriteAllText(SettingsFile, mode == StartupMode.Tray ? "--tray" : $"--set {percent}");

        if (task.State == StartupTaskState.DisabledByUser)
            throw new InvalidOperationException("Startup is turned off for this app in Windows. Turn it on in Settings > Apps > Startup.");
        if (task.State is StartupTaskState.Disabled)
        {
            var state = Wait(task.RequestEnableAsync());
            if (state != StartupTaskState.Enabled)
                throw new InvalidOperationException("Windows did not allow the app to start at sign-in.");
        }
    }
}
