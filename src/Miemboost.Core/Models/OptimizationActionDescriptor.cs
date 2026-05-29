namespace Miemboost.Core.Models;

public sealed record OptimizationActionDescriptor(
    string Id,
    string Title,
    string Description,
    OptimizationActionKind Kind,
    RiskLevel RiskLevel,
    bool RequiresElevation,
    bool CanRestore,
    IReadOnlyDictionary<string, string>? Parameters = null)
{
    public IReadOnlyDictionary<string, string> Parameters { get; init; } =
        Parameters ?? new Dictionary<string, string>();
}
