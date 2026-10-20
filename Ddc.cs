using System.Runtime.InteropServices;

namespace WinMonitorVolume;

/// <summary>DDC/CI access to physical monitors via the Windows Monitor Configuration API (dxva2.dll).</summary>
internal static class Ddc
{
    public const byte VcpSpeakerVolume = 0x62;
    public const byte VcpAudioMute = 0x8D;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint count);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint count, [Out] PHYSICAL_MONITOR[] monitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr hMonitor, byte code, IntPtr type, out uint current, out uint max);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool SetVCPFeature(IntPtr hMonitor, byte code, uint value);

    public sealed class Monitor : IDisposable
    {
        public IntPtr Handle { get; }
        public string Name { get; }
        public int Index { get; }

        internal Monitor(IntPtr handle, string name, int index) { Handle = handle; Name = name; Index = index; }

        public override string ToString() => $"{Index + 1}: {(string.IsNullOrWhiteSpace(Name) ? "Monitor" : Name)}";

        /// <summary>Reads a VCP value. Returns false if the monitor did not answer (DDC/CI off or unsupported).</summary>
        public bool TryGet(byte code, out uint current, out uint max)
        {
            // DDC/CI is slow and occasionally drops replies; retry a few times.
            for (int i = 0; i < 3; i++)
            {
                if (GetVCPFeatureAndVCPFeatureReply(Handle, code, IntPtr.Zero, out current, out max)) return true;
                Thread.Sleep(50);
            }
            current = max = 0;
            return false;
        }

        public bool TrySet(byte code, uint value)
        {
            for (int i = 0; i < 3; i++)
            {
                if (SetVCPFeature(Handle, code, value)) return true;
                Thread.Sleep(50);
            }
            return false;
        }

        /// <summary>Sets speaker volume and unmutes the speakers when the volume is above zero.</summary>
        public bool SetVolume(uint value)
        {
            if (!TrySet(VcpSpeakerVolume, value)) return false;
            if (value > 0) TrySet(VcpAudioMute, 2); // 2 = unmute
            return true;
        }

        public void Dispose() => DestroyPhysicalMonitor(Handle);
    }

    public static List<Monitor> GetMonitors()
    {
        var result = new List<Monitor>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr _, ref RECT _, IntPtr _) =>
        {
            if (GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out uint count) && count > 0)
            {
                var arr = new PHYSICAL_MONITOR[count];
                if (GetPhysicalMonitorsFromHMONITOR(hMon, count, arr))
                    foreach (var pm in arr)
                        result.Add(new Monitor(pm.hPhysicalMonitor, pm.szPhysicalMonitorDescription, result.Count));
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }

    /// <summary>Sets every monitor that supports DDC/CI volume to a percentage of its maximum. Returns how many were set.</summary>
    public static int SetVolumePercentAll(int percent)
    {
        int ok = 0;
        foreach (var m in GetMonitors())
        {
            using (m)
            {
                if (m.TryGet(VcpSpeakerVolume, out _, out uint max) && max > 0 &&
                    m.SetVolume((uint)Math.Round(max * Math.Clamp(percent, 0, 100) / 100.0)))
                    ok++;
            }
        }
        return ok;
    }
}
