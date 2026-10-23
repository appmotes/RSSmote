using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using RSSmote.App.Controls.Headless;
using RSSmote.App.ViewModels;

namespace RSSmote.Mobile.Views;

/// <summary>The chips follow the picker: rebuilt when its folders or its choice change.</summary>
public partial class FolderChips : UserControl
{
    private FolderPickerViewModel? _picker;

    public FolderChips()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as FolderPickerViewModel);
        NewName.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && _picker?.AddFolderCommand.CanExecute(null) == true)
                _picker.AddFolderCommand.Execute(null);
        };
    }

    private void Attach(FolderPickerViewModel? picker)
    {
        if (_picker is not null)
        {
            _picker.PropertyChanged -= OnPickerChanged;
            _picker.Folders.CollectionChanged -= OnFoldersChanged;
        }
        _picker = picker;
        if (_picker is not null)
        {
            _picker.PropertyChanged += OnPickerChanged;
            _picker.Folders.CollectionChanged += OnFoldersChanged;
        }
        Rebuild();
    }

    private void OnFoldersChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Rebuild();

    /// <summary>A folder just added is picked, so the field that named it closes.</summary>
    private void OnPickerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FolderPickerViewModel.Selected))
            return;
        NewRow.IsVisible = false;
        Rebuild();
    }

    private void Rebuild()
    {
        Chips.Children.Clear();
        if (_picker is not { } picker)
            return;
        Chips.Children.Add(Chip("None", picker.Selected is null, () => picker.SelectNoneCommand.Execute(null)));
        foreach (var choice in picker.Folders)
            Chips.Children.Add(Chip(choice.Folder.Title, choice.Folder.Id == picker.Selected?.Id,
                () => picker.ToggleCommand.Execute(choice)));
        var add = Chip("New", false, () =>
        {
            NewRow.IsVisible = !NewRow.IsVisible;
            if (NewRow.IsVisible)
                NewName.Focus();
        });
        add.Classes.Add("add");
        add.Content = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 4,
            Children =
            {
                new Icon { Data = Avalonia.Application.Current?.FindResource("Icon.Plus") as Geometry, Size = 16,
                           VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
                new TextBlock { Text = "New", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
            },
        };
        Chips.Children.Add(add);
    }

    private static Button Chip(string title, bool on, Action click)
    {
        var chip = new Button { Content = title, Classes = { "phoneChip" } };
        chip.Classes.Set("on", on);
        chip.Click += (_, _) => click();
        return chip;
    }
}
