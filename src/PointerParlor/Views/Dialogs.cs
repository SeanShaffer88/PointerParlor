using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PointerParlor.Core;
using PointerParlor.Core.Import;

namespace PointerParlor.Views;

/// <summary>Dialogs built in code: naming, confirming, and reviewing an import.</summary>
public static class Dialogs
{
    /// <summary>Asks for a set name; returns null when cancelled.</summary>
    public static async Task<string?> AskForNameAsync(XamlRoot root, string title, string message, string initial, string primary)
    {
        var box = new TextBox { Text = initial, Header = "Name", MaxLength = 60 };
        box.SelectAll();
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = title,
            Content = new StackPanel { Spacing = 12, Children = { new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, box } },
            PrimaryButtonText = primary,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        box.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(box.Text);
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? box.Text.Trim() : null;
    }

    public static async Task<bool> ConfirmAsync(XamlRoot root, string title, string message, string primary)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = primary,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>
    /// Shows what an import will create and lets the user fix pointer assignments before anything is copied.
    /// Returns the chosen name, or null when cancelled. Changes are written back into <paramref name="plan"/>.
    /// </summary>
    public static async Task<string?> ReviewImportAsync(XamlRoot root, ImportPlan plan)
    {
        const string DefaultChoice = "Windows default";
        var candidates = plan.Assignments.Values.Concat(plan.Unassigned).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var nameBox = new TextBox { Text = plan.SuggestedName, Header = "Set name", MaxLength = 60 };
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(nameBox);

        if (plan.Warnings.Count > 0)
        {
            panel.Children.Add(new InfoBar
            {
                IsOpen = true,
                IsClosable = false,
                Severity = InfoBarSeverity.Warning,
                Title = "Some files need attention",
                Message = string.Join(Environment.NewLine, plan.Warnings),
            });
        }

        panel.Children.Add(new TextBlock
        {
            Text = "Check which file each pointer uses. Pointers set to Windows default keep the standard Windows cursor.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8,
        });

        var choices = new Dictionary<CursorRole, ComboBox>();
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        foreach (var info in CursorRoles.All)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = new TextBlock { Text = info.DisplayName, VerticalAlignment = VerticalAlignment.Center };
            var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            combo.Items.Add(DefaultChoice);
            foreach (var file in candidates)
                combo.Items.Add(Path.GetFileName(file));
            combo.SelectedIndex = plan.Assignments.TryGetValue(info.Role, out var assigned)
                ? candidates.FindIndex(c => string.Equals(c, assigned, StringComparison.OrdinalIgnoreCase)) + 1
                : 0;
            Grid.SetRow(label, (int)info.Role);
            Grid.SetRow(combo, (int)info.Role);
            Grid.SetColumn(combo, 1);
            grid.Children.Add(label);
            grid.Children.Add(combo);
            choices[info.Role] = combo;
        }
        panel.Children.Add(grid);

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = "Import cursors",
            Content = new ScrollViewer { Content = panel, MaxHeight = 560, Padding = new Thickness(0, 0, 12, 0) },
            PrimaryButtonText = "Import",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };

        void Validate() => dialog.IsPrimaryButtonEnabled =
            !string.IsNullOrWhiteSpace(nameBox.Text) && choices.Values.Any(c => c.SelectedIndex > 0);
        nameBox.TextChanged += (_, _) => Validate();
        foreach (var combo in choices.Values)
            combo.SelectionChanged += (_, _) => Validate();
        Validate();

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return null;

        plan.Assignments.Clear();
        foreach (var (role, combo) in choices)
        {
            if (combo.SelectedIndex > 0)
                plan.Assignments[role] = candidates[combo.SelectedIndex - 1];
        }
        return nameBox.Text.Trim();
    }
}
