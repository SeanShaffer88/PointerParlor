namespace PointerParlor.Core;

/// <summary>
/// The 17 Windows pointer roles, in the order Windows stores them in a scheme string
/// (HKCU\Control Panel\Cursors\Schemes). The order is load-bearing: do not reorder.
/// </summary>
public enum CursorRole
{
    Arrow,
    Help,
    AppStarting,
    Wait,
    Crosshair,
    IBeam,
    NWPen,
    No,
    SizeNS,
    SizeWE,
    SizeNWSE,
    SizeNESW,
    SizeAll,
    UpArrow,
    Hand,
    Pin,
    Person,
}

public sealed record CursorRoleInfo(CursorRole Role, string RegistryName, string DisplayName, bool UsuallyAnimated);

public static class CursorRoles
{
    public static IReadOnlyList<CursorRoleInfo> All { get; } =
    [
        new(CursorRole.Arrow, "Arrow", "Normal Select", false),
        new(CursorRole.Help, "Help", "Help Select", false),
        new(CursorRole.AppStarting, "AppStarting", "Working in Background", true),
        new(CursorRole.Wait, "Wait", "Busy", true),
        new(CursorRole.Crosshair, "Crosshair", "Precision Select", false),
        new(CursorRole.IBeam, "IBeam", "Text Select", false),
        new(CursorRole.NWPen, "NWPen", "Handwriting", false),
        new(CursorRole.No, "No", "Unavailable", false),
        new(CursorRole.SizeNS, "SizeNS", "Vertical Resize", false),
        new(CursorRole.SizeWE, "SizeWE", "Horizontal Resize", false),
        new(CursorRole.SizeNWSE, "SizeNWSE", "Diagonal Resize 1", false),
        new(CursorRole.SizeNESW, "SizeNESW", "Diagonal Resize 2", false),
        new(CursorRole.SizeAll, "SizeAll", "Move", false),
        new(CursorRole.UpArrow, "UpArrow", "Alternate Select", false),
        new(CursorRole.Hand, "Hand", "Link Select", false),
        new(CursorRole.Pin, "Pin", "Location Select", false),
        new(CursorRole.Person, "Person", "Person Select", false),
    ];

    public static CursorRoleInfo Info(CursorRole role) => All[(int)role];

    public static bool TryParseRegistryName(string name, out CursorRole role)
    {
        foreach (var info in All)
        {
            if (string.Equals(info.RegistryName, name, StringComparison.OrdinalIgnoreCase))
            {
                role = info.Role;
                return true;
            }
        }
        role = default;
        return false;
    }
}
