using System.Runtime.InteropServices;
using Microsoft.Win32;
using PointerParlor.Core;
using PointerParlor.Core.Schemes;

namespace PointerParlor.Services;

/// <summary>
/// The only code that changes Windows: carries out SchemePlanner changes under HKEY_CURRENT_USER
/// and asks Windows to reload its cursors.
/// </summary>
public static partial class RegistryApplier
{
    private const uint SPI_SETCURSORS = 0x0057;
    private const uint SPIF_UPDATEINIFILE = 0x01;
    private const uint SPIF_SENDCHANGE = 0x02;

    public static RegValue? Read(string keyPath, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        if (key is null)
            return null;
        var raw = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (raw is null)
            return null;
        return key.GetValueKind(valueName) switch
        {
            RegistryValueKind.DWord => RegValue.DWord(Convert.ToInt32(raw)),
            RegistryValueKind.ExpandString => RegValue.Expand(raw.ToString() ?? ""),
            _ => RegValue.Str(raw.ToString() ?? ""),
        };
    }

    public static void Apply(IEnumerable<RegistryChange> changes)
    {
        foreach (var change in changes)
        {
            using var key = Registry.CurrentUser.CreateSubKey(change.KeyPath, writable: true);
            if (change.Value is null)
            {
                key.DeleteValue(change.ValueName, throwOnMissingValue: false);
                continue;
            }
            switch (change.Value.Type)
            {
                case RegValueType.DWord:
                    key.SetValue(change.ValueName, change.Value.Number ?? 0, RegistryValueKind.DWord);
                    break;
                case RegValueType.ExpandString:
                    key.SetValue(change.ValueName, change.Value.Text ?? "", RegistryValueKind.ExpandString);
                    break;
                default:
                    key.SetValue(change.ValueName, change.Value.Text ?? "", RegistryValueKind.String);
                    break;
            }
        }
    }

    /// <summary>Tells Windows to reload the cursors from the registry so changes show at once.</summary>
    public static void ReloadCursors()
    {
        if (!SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE))
            throw new InvalidOperationException($"Windows did not reload the cursors (error {Marshal.GetLastPInvokeError()}).");
    }

    /// <summary>The cursor path for every role as stored, with %variables% left in place.</summary>
    public static Dictionary<CursorRole, string> ReadCurrentCursors()
    {
        var result = new Dictionary<CursorRole, string>();
        foreach (var info in CursorRoles.All)
        {
            if (Read(RegistryPaths.Cursors, info.RegistryName) is { Text: { Length: > 0 } text })
                result[info.Role] = text;
        }
        return result;
    }

    /// <summary>The schemes that ship with Windows, name to scheme string.</summary>
    public static Dictionary<string, string> ReadSystemSchemes()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var key = Registry.LocalMachine.OpenSubKey(RegistryPaths.SystemSchemes);
        if (key is null)
            return result;
        foreach (var name in key.GetValueNames())
        {
            if (key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is string scheme)
                result[name] = scheme;
        }
        return result;
    }

    public static int ReadPointerSize() =>
        Read(RegistryPaths.Cursors, RegistryPaths.BaseSizeValue)?.Number is int size and >= SchemePlanner.MinPointerSize
            ? size
            : SchemePlanner.MinPointerSize;

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(uint action, uint param, IntPtr vparam, uint winIni);
}
