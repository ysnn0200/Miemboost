using Miemboost.Core.Diagnostics;
using Miemboost.Core.Models;

namespace Miemboost.Core.Games;

public sealed class GameProfileMatcher
{
    public GameProfileMatch? FindRunningMatch(
        IReadOnlyList<GameProfile> profiles,
        IReadOnlyList<ProcessSnapshot> processes)
    {
        foreach (var profile in profiles)
        {
            var match = processes.FirstOrDefault(process =>
                !process.IsProtectedCandidate && Matches(profile, process));

            if (match is not null)
            {
                return new GameProfileMatch(profile, match.ProcessId);
            }
        }

        return null;
    }

    private static bool Matches(GameProfile profile, ProcessSnapshot process)
    {
        if (!string.IsNullOrWhiteSpace(process.MainModulePath)
            && string.Equals(profile.ExecutablePath, process.MainModulePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(profile.Name, process.Name, StringComparison.OrdinalIgnoreCase);
    }
}
