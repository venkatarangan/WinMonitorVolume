using System.Reflection;

namespace WinMonitorVolume;

internal static class Program
{
    public const string AppName = "WinMonitorVolume";
    private const string MutexName = @"Local\WinMonitorVolume.SingleInstance";
    private const string ShowEventName = @"Local\WinMonitorVolume.Show";

    public static string Version =>
        typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";

    private static Icon? _icon;
    public static Icon AppIcon => _icon ??= new Icon(typeof(Program).Assembly.GetManifestResourceStream("app.ico")!);

    /// <summary>
    /// Usage:
    ///   WinMonitorVolume.exe            open the window
    ///   WinMonitorVolume.exe --tray     start hidden in the system tray
    ///   WinMonitorVolume.exe --max      set every DDC/CI monitor's speaker volume to 100%, then exit
    ///   WinMonitorVolume.exe --set 50   set every DDC/CI monitor's speaker volume to 50% of its maximum, then exit
    /// </summary>
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("--max", StringComparison.OrdinalIgnoreCase))
            return SetVolumeAndExit(100);
        if (args.Length > 0 && args[0].Equals("--set", StringComparison.OrdinalIgnoreCase))
            return args.Length > 1 && int.TryParse(args[1], out int percent) ? SetVolumeAndExit(percent) : 2;

        bool startInTray = args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase));

        using var mutex = new Mutex(true, MutexName, out bool firstInstance);
        if (!firstInstance)
        {
            // Already running: bring that window up, unless this is the sign-in launch.
            if (!startInTray && EventWaitHandle.TryOpenExisting(ShowEventName, out var existing))
                using (existing) existing.Set();
            return 0;
        }

        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(startInTray, showEvent));
        return 0;
    }

    private static int SetVolumeAndExit(int percent)
    {
        // Right after sign-in the display link may not be ready yet, so keep trying for about 30 seconds.
        for (int attempt = 0; attempt < 15; attempt++)
        {
            if (Ddc.SetVolumePercentAll(percent) > 0) return 0;
            Thread.Sleep(2000);
        }
        return 1;
    }
}
