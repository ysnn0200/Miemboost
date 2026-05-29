using Miemboost.Core.Models;

namespace Miemboost.Core.Execution;

public sealed record OptimizationExecutionContext(
    OptimizationPlan Plan,
    SystemSnapshot Snapshot);
