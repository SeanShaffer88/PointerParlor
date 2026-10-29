using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using PointerParlor.Core;
using PointerParlor.Core.Library;

namespace PointerParlor.ViewModels;

public sealed record PreviewItem(string Path);

/// <summary>A set's card in the gallery.</summary>
public sealed class SetCard
{
    // Pointers that show off a set's style best, in the order shown on the card.
    private static readonly CursorRole[] ShowcaseRoles = [CursorRole.Arrow, CursorRole.Hand, CursorRole.IBeam, CursorRole.Wait, CursorRole.Help, CursorRole.SizeAll];

    public SetCard(CursorSet set, bool isActive)
    {
        Set = set;
        IsActive = isActive;
        Previews = ShowcaseRoles.Where(set.Paths.ContainsKey).Take(4).Select(r => new PreviewItem(set.Paths[r])).ToList();
        var count = set.Paths.Count;
        Subtitle = count == CursorRoles.All.Count ? "All 17 pointers" : $"{count} of 17 pointers";
        if (!string.IsNullOrWhiteSpace(set.Manifest.Author))
            Subtitle += " · " + set.Manifest.Author;
    }

    public CursorSet Set { get; }
    public string Name => Set.Name;
    public string Subtitle { get; }
    public bool IsActive { get; }
    public Visibility ActiveVisibility => IsActive ? Visibility.Visible : Visibility.Collapsed;
    public IReadOnlyList<PreviewItem> Previews { get; }
}

/// <summary>A gallery group (Signature, Openly licensed, Windows, Imported, My sets).</summary>
public sealed class SetGroup(string title, IEnumerable<SetCard> cards) : List<SetCard>(cards)
{
    public string Title { get; } = title;

    public static string TitleFor(SetKind kind) => kind switch
    {
        SetKind.Signature => "PointerParlor signature sets",
        SetKind.OpenLicense => "Openly licensed sets",
        SetKind.Windows => "Included with Windows",
        SetKind.Imported => "Imported",
        _ => "My sets",
    };
}

/// <summary>One of the 17 pointers on a set's detail page.</summary>
public sealed class RoleTile : ObservableObject
{
    private bool _isApplied;

    public RoleTile(CursorRoleInfo info, string? path, bool isApplied, Action<CursorRole> apply)
    {
        Info = info;
        Path = path;
        _isApplied = isApplied;
        ApplyCommand = new RelayCommand(() => apply(info.Role), () => Path is not null && !IsApplied);
    }

    public CursorRoleInfo Info { get; }
    public string DisplayName => Info.DisplayName;
    public string? Path { get; }
    public string FileName => Path is null ? "Uses the Windows default" : System.IO.Path.GetFileName(Path);
    public Visibility PreviewVisibility => Path is null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility MissingVisibility => Path is null ? Visibility.Visible : Visibility.Collapsed;
    public RelayCommand ApplyCommand { get; }
    public ICommand Command => ApplyCommand;

    public bool IsApplied
    {
        get => _isApplied;
        set
        {
            if (SetProperty(ref _isApplied, value))
            {
                OnPropertyChanged(nameof(ApplyLabel));
                ApplyCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ApplyLabel => IsApplied ? "In use" : "Apply";
}
