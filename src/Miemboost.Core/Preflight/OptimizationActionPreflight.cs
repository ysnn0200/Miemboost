using Miemboost.Core.Models;

namespace Miemboost.Core.Preflight;

public sealed record OptimizationActionPreflight(
    OptimizationActionDescriptor Action,
    OptimizationPreflightStatus Status,
    string Message);
