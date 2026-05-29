namespace Miemboost.Core.Processes;

public interface IProcessPriorityManager
{
    Task<string?> GetProcessNameAsync(
        int processId,
        CancellationToken cancellationToken = default);

    Task<ManagedProcessPriority?> GetPriorityAsync(
        int processId,
        CancellationToken cancellationToken = default);

    Task SetPriorityAsync(
        int processId,
        ManagedProcessPriority priority,
        CancellationToken cancellationToken = default);
}
