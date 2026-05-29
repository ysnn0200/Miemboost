namespace Miemboost.Core.Models;

public sealed record OptimizationActionDescriptor(
    string Id,
    string Title,
    string Description,
    OptimizationActionKind Kind,
    RiskLevel RiskLevel,
    bool RequiresElevation,
    bool CanRestore);
