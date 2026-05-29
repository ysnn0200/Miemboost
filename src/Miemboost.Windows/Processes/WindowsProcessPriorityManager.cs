using System.Diagnostics;
using Miemboost.Core.Processes;

namespace Miemboost.Windows.Processes;

public sealed class WindowsProcessPriorityManager : IProcessPriorityManager
{
    public Task<ManagedProcessPriority?> GetPriorityAsync(
        int processId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var process = Process.GetProcessById(processId);
            return Task.FromResult<ManagedProcessPriority?>(FromProcessPriorityClass(process.PriorityClass));
        }
        catch
        {
            return Task.FromResult<ManagedProcessPriority?>(null);
        }
    }

    public Task SetPriorityAsync(
        int processId,
        ManagedProcessPriority priority,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var process = Process.GetProcessById(processId);
        process.PriorityClass = ToProcessPriorityClass(priority);
        return Task.CompletedTask;
    }

    internal static ManagedProcessPriority FromProcessPriorityClass(ProcessPriorityClass priorityClass)
    {
        return priorityClass switch
        {
            ProcessPriorityClass.Idle => ManagedProcessPriority.Idle,
            ProcessPriorityClass.BelowNormal => ManagedProcessPriority.BelowNormal,
            ProcessPriorityClass.AboveNormal => ManagedProcessPriority.AboveNormal,
            ProcessPriorityClass.High => ManagedProcessPriority.High,
            _ => ManagedProcessPriority.Normal
        };
    }

    internal static ProcessPriorityClass ToProcessPriorityClass(ManagedProcessPriority priority)
    {
        return priority switch
        {
            ManagedProcessPriority.Idle => ProcessPriorityClass.Idle,
            ManagedProcessPriority.BelowNormal => ProcessPriorityClass.BelowNormal,
            ManagedProcessPriority.Normal => ProcessPriorityClass.Normal,
            ManagedProcessPriority.AboveNormal => ProcessPriorityClass.AboveNormal,
            ManagedProcessPriority.High => ProcessPriorityClass.High,
            _ => ProcessPriorityClass.Normal
        };
    }
}
