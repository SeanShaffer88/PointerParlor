namespace PointerParlor.Core.Schemes;

public enum RegValueType
{
    String,
    ExpandString,
    DWord,
}

/// <summary>A registry value as Core sees it. <see cref="Number"/> is set for DWORDs, <see cref="Text"/> otherwise.</summary>
public sealed record RegValue(RegValueType Type, string? Text = null, int? Number = null)
{
    public static RegValue Str(string text) => new(RegValueType.String, text);
    public static RegValue Expand(string text) => new(RegValueType.ExpandString, text);
    public static RegValue DWord(int number) => new(RegValueType.DWord, Number: number);
}

/// <summary>
/// One planned write under HKEY_CURRENT_USER. A null <see cref="Value"/> means delete the value.
/// An empty <see cref="ValueName"/> is the key's default value.
/// </summary>
public sealed record RegistryChange(string KeyPath, string ValueName, RegValue? Value);

/// <summary>Reads one HKCU value; returns null when it does not exist.</summary>
public delegate RegValue? RegistryReader(string keyPath, string valueName);

public static class RegistryPaths
{
    public const string Cursors = @"Control Panel\Cursors";
    public const string UserSchemes = @"Control Panel\Cursors\Schemes";

    /// <summary>HKLM key holding the schemes that ship with Windows.</summary>
    public const string SystemSchemes = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Control Panel\Cursors\Schemes";

    public const string SchemeSourceValue = "Scheme Source";
    public const string BaseSizeValue = "CursorBaseSize";
}
