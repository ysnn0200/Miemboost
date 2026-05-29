using Miemboost.Core.Models;

namespace Miemboost.Core.Games;

public static class GameProfileUpdater
{
    public static GameProfile AddAllowedBackgroundProcess(
        GameProfile profile,
        string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            throw new ArgumentException("Process name cannot be empty.", nameof(processName));
        }

        var names = profile.AllowedBackgroundProcessNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Append(processName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return profile with
        {
            AllowedBackgroundProcessNames = names
        };
    }
}
