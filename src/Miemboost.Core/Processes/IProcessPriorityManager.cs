namespace Miemboost.Core.Processes;

public interface IProcessPriorityManager
{
    Task<ManagedProcessPriority?> GetPriorityAsync(
        int processId,
        CancellationToken cancellationToken = default);

    Task SetPriorityAsync(
        int processId,
        ManagedProcessPriority priority,
        CancellationToken cancellationToken = default);
}
