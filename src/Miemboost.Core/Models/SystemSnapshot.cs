namespace Miemboost.Core.Models;

public sealed record SystemSnapshot(
    string Id,
    string PlanId,
    string? PreviousPowerPlanId,
    IReadOnlyList<ProcessPrioritySnapshot> ProcessPriorities,
    IReadOnlyList<PausedProcessSnapshot> PausedProcesses,
    DateTimeOffset CreatedAt);

public sealed record ProcessPrioritySnapshot(
    int ProcessId,
    string ProcessName,
    string PreviousPriorityClass);

public sealed record PausedProcessSnapshot(
    int ProcessId,
    string ProcessName,
    string? MainModulePath,
    string PauseStrategy);
