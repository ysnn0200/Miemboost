namespace Miemboost.Core.Safety;

public sealed record SafetyDecision(
    string ActionId,
    bool IsAllowed,
    string? Reason)
{
    public static SafetyDecision Allowed(string actionId) => new(actionId, true, null);

    public static SafetyDecision Blocked(string actionId, string reason) => new(actionId, false, reason);
}
