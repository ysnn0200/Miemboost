using System.Text.Json;
using Miemboost.Core.Games;
using Miemboost.Core.Models;

namespace Miemboost.Windows.Games;

public sealed class JsonGameProfileStore(string filePath) : IGameProfileStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public async Task<IReadOnlyList<GameProfile>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            return await JsonSerializer.DeserializeAsync<IReadOnlyList<GameProfile>>(
                    stream,
                    SerializerOptions,
                    cancellationToken)
                .ConfigureAwait(false) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task SaveAsync(GameProfile profile, CancellationToken cancellationToken = default)
    {
        var profiles = (await ListAsync(cancellationToken).ConfigureAwait(false))
            .Where(existing => !string.Equals(existing.Id, profile.Id, StringComparison.OrdinalIgnoreCase))
            .Append(profile)
            .OrderBy(profileItem => profileItem.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, profiles, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteAsync(string profileId, CancellationToken cancellationToken = default)
    {
        var profiles = (await ListAsync(cancellationToken).ConfigureAwait(false))
            .Where(existing => !string.Equals(existing.Id, profileId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, profiles, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
    }
}
