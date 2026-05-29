namespace Miemboost.Core.Execution;

public sealed class OptimizationActionHandlerRegistry : IOptimizationActionHandlerRegistry
{
    private readonly IReadOnlyDictionary<string, IOptimizationActionHandler> _handlers;

    public OptimizationActionHandlerRegistry(IEnumerable<IOptimizationActionHandler> handlers)
    {
        _handlers = handlers.ToDictionary(handler => handler.ActionId, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGetHandler(string actionId, out IOptimizationActionHandler handler)
    {
        return _handlers.TryGetValue(actionId, out handler!);
    }
}
