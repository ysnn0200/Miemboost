using Miemboost.Core.Models;

namespace Miemboost.Core.Games;

public static class GameProfileFactory
{
    public static GameProfile Create(
        string name,
        string executablePath,
        BoostMode recommendedMode = BoostMode.Balanced)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Game name cannot be empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path cannot be empty.", nameof(executablePath));
        }

        return new GameProfile(
            Id: Guid.NewGuid().ToString("N"),
            Name: name.Trim(),
            ExecutablePath: executablePath.Trim(),
            RecommendedMode: recommendedMode,
            AutoRestoreOnExit: true,
            AllowedBackgroundProcessNames: [],
            NetworkTargets: []);
    }
}
