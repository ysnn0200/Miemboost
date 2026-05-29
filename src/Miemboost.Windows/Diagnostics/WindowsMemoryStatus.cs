using System.Runtime.InteropServices;

namespace Miemboost.Windows.Diagnostics;

internal static class WindowsMemoryStatus
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;

        public MemoryStatusEx()
        {
            Length = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);

    public static (ulong TotalBytes, ulong AvailableBytes) Capture()
    {
        var status = new MemoryStatusEx();

        if (!GlobalMemoryStatusEx(status))
        {
            throw new InvalidOperationException("Unable to read Windows memory status.");
        }

        return (status.TotalPhys, status.AvailPhys);
    }
}
