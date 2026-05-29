namespace Miemboost.Core.Diagnostics;

public sealed class DefaultProtectedProcessClassifier : IProtectedProcessClassifier
{
    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "vgc",
        "vgtray",
        "EasyAntiCheat",
        "EasyAntiCheat_EOS",
        "BEService",
        "BEService_x64",
        "FACEIT",
        "EACLauncher",
        "RiotClientServices",
        "Riot Vanguard"
    };

    public bool IsProtectedCandidate(string processName, string? path)
    {
        if (ProtectedNames.Contains(processName))
        {
            return true;
        }

        if (path is null)
        {
            return false;
        }

        return path.Contains("anti-cheat", StringComparison.OrdinalIgnoreCase)
            || path.Contains("anticheat", StringComparison.OrdinalIgnoreCase)
            || path.Contains("vanguard", StringComparison.OrdinalIgnoreCase)
            || path.Contains("battleye", StringComparison.OrdinalIgnoreCase)
            || path.Contains("easyanticheat", StringComparison.OrdinalIgnoreCase);
    }
}
