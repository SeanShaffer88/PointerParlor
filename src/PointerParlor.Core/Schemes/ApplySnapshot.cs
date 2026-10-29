using System.Text.Json;
using System.Text.Json.Serialization;

namespace PointerParlor.Core.Schemes;

/// <summary>A value as it was before a change; a null <see cref="Previous"/> means it did not exist.</summary>
public sealed record SnapshotEntry(string KeyPath, string ValueName, RegValue? Previous);

/// <summary>Library files that must be restored before restoring the registry.</summary>
public sealed record LibraryUndo(string Directory, string? StagedDirectory, string? ManifestJson);

/// <summary>
/// The registry state a change is about to overwrite, recorded before the change is made so
/// "Undo last apply" can put it back exactly, including deleting values that were not there.
/// </summary>
public sealed record ApplySnapshot(string Description, DateTimeOffset CreatedUtc, IReadOnlyList<SnapshotEntry> Entries)
{
    public LibraryUndo? LibraryUndo { get; init; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static ApplySnapshot Capture(string description, IEnumerable<RegistryChange> changes, RegistryReader read, DateTimeOffset now)
    {
        var entries = new List<SnapshotEntry>();
        var seen = new HashSet<(string, string)>();
        foreach (var change in changes)
        {
            var key = (change.KeyPath.ToUpperInvariant(), change.ValueName.ToUpperInvariant());
            if (seen.Add(key))
                entries.Add(new SnapshotEntry(change.KeyPath, change.ValueName, read(change.KeyPath, change.ValueName)));
        }
        return new ApplySnapshot(description, now, entries);
    }

    public IReadOnlyList<RegistryChange> UndoChanges() =>
        Entries.Select(e => new RegistryChange(e.KeyPath, e.ValueName, e.Previous)).ToList();

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static ApplySnapshot? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ApplySnapshot>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
