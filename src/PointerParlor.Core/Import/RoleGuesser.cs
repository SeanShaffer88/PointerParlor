using System.Text;

namespace PointerParlor.Core.Import;

/// <summary>
/// Guesses a pointer role from a file name such as "Link Select.cur", "busy.ani" or "dgn1.cur".
/// Rules are checked in order, most specific first ("handwriting" before "hand", "uparrow" before "arrow").
/// </summary>
public static class RoleGuesser
{
    // (role, substrings matched against the name with separators removed, whole words matched against tokens)
    private static readonly (CursorRole Role, string[] Contains, string[] Words)[] Rules =
    [
        (CursorRole.NWPen, ["handwrit", "nwpen", "handwriting"], ["pen"]),
        (CursorRole.AppStarting, ["appstarting", "working", "background", "progress", "workinbackground"], ["work"]),
        (CursorRole.Wait, ["busy", "wait", "loading", "hourglass"], []),
        (CursorRole.Help, ["help", "question"], []),
        (CursorRole.Crosshair, ["precision", "crosshair", "cross"], []),
        (CursorRole.IBeam, ["ibeam", "textselect", "beam"], ["text"]),
        (CursorRole.No, ["unavailable", "unavail", "notallowed", "forbidden", "nodrop"], ["no", "unavailiable"]),
        (CursorRole.SizeNWSE, ["nwse", "dgn1", "diag1", "diagonal1", "diagonalresize1"], []),
        (CursorRole.SizeNESW, ["nesw", "dgn2", "diag2", "diagonal2", "diagonalresize2"], []),
        (CursorRole.SizeNS, ["sizens", "vert", "vertical", "nsresize"], ["ns"]),
        (CursorRole.SizeWE, ["sizewe", "horz", "horiz", "horizontal", "ewresize"], ["we", "ew"]),
        (CursorRole.SizeAll, ["move", "sizeall", "allscroll"], []),
        (CursorRole.UpArrow, ["alternate", "uparrow"], ["alt", "up"]),
        (CursorRole.Hand, ["link", "hand"], []),
        (CursorRole.Pin, ["location"], ["pin"]),
        (CursorRole.Person, ["person"], []),
        (CursorRole.Arrow, ["arrow", "normal", "pointer", "default"], ["select"]),
    ];

    public static CursorRole? Guess(string fileNameOrKey)
    {
        var name = Path.GetFileNameWithoutExtension(fileNameOrKey).ToLowerInvariant();
        var tokens = new List<string>();
        var compact = new StringBuilder();
        var current = new StringBuilder();
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                compact.Append(c);
                current.Append(c);
            }
            else if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }
        if (current.Length > 0)
            tokens.Add(current.ToString());

        var compactName = compact.ToString();
        foreach (var (role, contains, words) in Rules)
        {
            if (contains.Any(compactName.Contains) || words.Any(tokens.Contains))
                return role;
        }
        return null;
    }
}
