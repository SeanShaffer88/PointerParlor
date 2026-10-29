using System.Text.Json;
using System.Text.Json.Nodes;

namespace PointerParlor.Core;

/// <summary>Where PointerParlor keeps its files under %LocalAppData%.</summary>
public sealed class AppPaths(string root)
{
    public static AppPaths ForCurrentUser() =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PointerParlor"));

    public string Root { get; } = root;
    public string Library => Path.Combine(Root, "Library");
    public string Settings => Path.Combine(Root, "settings.json");
    public string UndoSnapshot => Path.Combine(Root, "undo.json");
    public string Temp => Path.Combine(Root, "Temp");
    public string Logs => Path.Combine(Root, "Logs");
}

public enum ThemeChoice
{
    System,
    Light,
    Dark,
}

/// <summary>
/// The settings file. It carries a schema version and only stores values the user changed:
/// defaults live in code, so a new default in a later release reaches everyone who never changed it.
/// </summary>
public sealed class AppSettings
{
    public const int SchemaVersion = 1;

    private readonly string _path;
    private readonly JsonObject _values;

    private AppSettings(string path, JsonObject values)
    {
        _path = path;
        _values = values;
    }

    public static AppSettings Load(string path)
    {
        JsonObject values;
        try
        {
            values = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? [] : [];
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            values = []; // an unreadable file falls back to defaults rather than blocking startup
        }
        // Future schema migrations run here, keyed on values["schemaVersion"].
        values["schemaVersion"] = SchemaVersion;
        return new AppSettings(path, values);
    }

    public ThemeChoice Theme
    {
        get => Enum.TryParse<ThemeChoice>((string?)_values["theme"], out var t) ? t : ThemeChoice.System;
        set => _values["theme"] = value.ToString();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, _values.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _path, overwrite: true);
    }
}
