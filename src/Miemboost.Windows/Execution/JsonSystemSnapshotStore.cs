using System.Text.Json;
using Miemboost.Core.Execution;
using Miemboost.Core.Models;

namespace Miemboost.Windows.Execution;

public sealed class JsonSystemSnapshotStore(string directoryPath) : ISystemSnapshotStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public async Task SaveAsync(SystemSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directoryPath);

        var filePath = GetSnapshotPath(snapshot.Id);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, snapshot, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<SystemSnapshot?> GetAsync(
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetSnapshotPath(snapshotId);

        if (!File.Exists(filePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<SystemSnapshot>(stream, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SystemSnapshot>> ListRecentAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0 || !Directory.Exists(directoryPath))
        {
            return [];
        }

        var files = Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Take(limit)
            .ToArray();

        var snapshots = new List<SystemSnapshot>(files.Length);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = await ReadSnapshotOrNullAsync(file.FullName, cancellationToken)
                .ConfigureAwait(false);

            if (snapshot is not null)
            {
                snapshots.Add(snapshot);
            }
        }

        return snapshots;
    }

    private async Task<SystemSnapshot?> ReadSnapshotOrNullAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(filePath);
            return await JsonSerializer.DeserializeAsync<SystemSnapshot>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private string GetSnapshotPath(string snapshotId)
    {
        var safeSnapshotId = string.Concat(snapshotId.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));

        return Path.Combine(directoryPath, $"{safeSnapshotId}.json");
    }
}
