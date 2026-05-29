using System.Diagnostics;
using Miemboost.Core.Processes;

namespace Miemboost.Windows.Processes;

public sealed class WindowsProcessLifetimeReader : IProcessLifetimeReader
{
    public bool IsRunning(int processId)
    {
        if (processId <= 0)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }
}
