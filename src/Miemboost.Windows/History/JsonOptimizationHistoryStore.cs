using System.Text.Json;
using Miemboost.Core.History;

namespace Miemboost.Windows.History;

public sealed class JsonOptimizationHistoryStore(string filePath) : IOptimizationHistoryStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public async Task AddAsync(
        OptimizationHistoryEntry entry,
        CancellationToken cancellationToken = default)
    {
        var entries = (await ListRecentAsync(200, cancellationToken).ConfigureAwait(false)).ToList();
        entries.Insert(0, entry);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, entries.Take(200).ToArray(), SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OptimizationHistoryEntry>> ListRecentAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0 || !File.Exists(filePath))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            var entries = await JsonSerializer
                .DeserializeAsync<IReadOnlyList<OptimizationHistoryEntry>>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            return entries?.Take(limit).ToArray() ?? [];
        }
        catch
        {
            return [];
        }
    }
}
