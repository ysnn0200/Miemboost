namespace Miemboost.Core.Preflight;

public sealed record OptimizationPreflightReport(
    IReadOnlyList<OptimizationActionPreflight> Actions)
{
    public bool HasBlockingIssues => Actions.Any(action => action.Status != OptimizationPreflightStatus.Ready);

    public int ReadyCount => Actions.Count(action => action.Status == OptimizationPreflightStatus.Ready);

    public int BlockedCount => Actions.Count(action => action.Status != OptimizationPreflightStatus.Ready);
}
