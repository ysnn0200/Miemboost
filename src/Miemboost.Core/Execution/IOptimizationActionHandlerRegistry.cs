namespace Miemboost.Core.Execution;

public interface IOptimizationActionHandlerRegistry
{
    bool TryGetHandler(string actionId, out IOptimizationActionHandler handler);
}
