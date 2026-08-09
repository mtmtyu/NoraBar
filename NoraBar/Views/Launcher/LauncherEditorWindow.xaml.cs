using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using NoraBar.Hud.Launcher;
using NoraBar.Services;

namespace NoraBar.Views.Launcher;

public partial class LauncherEditorWindow : Window
{
    public LauncherEditorWindow()
    {
        InitializeComponent();
        try
        {
            Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/AppIcon.ico", UriKind.Absolute));
        }
        catch
        {
            // Ignore in unit test context
        }
    }

    private LauncherSettingsViewModel? ViewModel => DataContext as LauncherSettingsViewModel;
    private LauncherLocalization Strings => ViewModel?.Strings ?? new LauncherLocalization(NoraBar.Models.AppLanguage.English);

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void AddPage_Click(object sender, RoutedEventArgs e)
    {
        string? name = Prompt(Strings.AddPageTitle, Strings.PageName, Strings.DefaultPageName);
        if (name is not null) ViewModel?.AddPage(name);
    }

    private void RemovePage_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedPage is { } page) ViewModel.RemovePage(page);
    }

    private void MovePageUp_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedPage is { } page) ViewModel.MovePage(page, -1);
    }

    private void MovePageDown_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedPage is { } page) ViewModel.MovePage(page, 1);
    }

    private void AddGroup_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedPage is not { } page) return;
        string? name = Prompt(Strings.AddGroupTitle, Strings.GroupName, Strings.DefaultGroupName);
        if (name is not null) ViewModel.AddGroup(page, name);
    }

    private void RemoveGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: LauncherGroupEditorViewModel group } && ViewModel?.SelectedPage is { } page)
        {
            ViewModel.RemoveGroup(page, group);
        }
        else if (ViewModel is { SelectedPage: { } selPage, SelectedGroup: { } selGroup })
        {
            ViewModel.RemoveGroup(selPage, selGroup);
        }
    }

    private void MoveGroupUp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: LauncherGroupEditorViewModel group } && ViewModel?.SelectedPage is { } page)
        {
            ViewModel.MoveGroup(page, group, -1);
        }
        else if (ViewModel is { SelectedPage: { } selPage, SelectedGroup: { } selGroup })
        {
            ViewModel.MoveGroup(selPage, selGroup, -1);
        }
    }

    private void MoveGroupDown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: LauncherGroupEditorViewModel group } && ViewModel?.SelectedPage is { } page)
        {
            ViewModel.MoveGroup(page, group, 1);
        }
        else if (ViewModel is { SelectedPage: { } selPage, SelectedGroup: { } selGroup })
        {
            ViewModel.MoveGroup(selPage, selGroup, 1);
        }
    }

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { SelectedGroup: { } group, SelectedItem: { } item })
        {
            ViewModel.RemoveItem(group, item);
        }
    }

    private void AddFile_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { CheckFileExists = true, Multiselect = false, Title = ViewModel?.Strings.ChooseFile };
        if (picker.ShowDialog(this) == true) AddItemToSelectedGroup(CreateFileSystemItem(picker.FileName));
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Multiselect = false, Title = ViewModel?.Strings.ChooseFolder };
        if (picker.ShowDialog(this) == true) AddItemToSelectedGroup(CreateFileSystemItem(picker.FolderName));
    }

    private void AddUrl_Click(object sender, RoutedEventArgs e)
    {
        string? url = Prompt(Strings.AddUrlTitle, Strings.UrlPrompt, "https://");
        if (url is null) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show(Strings.InvalidUrl, "NoraBar", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        AddItemToSelectedGroup(new LauncherItem(CreateId(), uri.Host, LauncherItemKind.Url, uri.AbsoluteUri));
    }

    private void AddInstalled_Click(object sender, RoutedEventArgs e) => Observe(AddFromCatalogAsync(running: false));
    private void AddRunning_Click(object sender, RoutedEventArgs e) => Observe(AddFromCatalogAsync(running: true));

    private async Task AddFromCatalogAsync(bool running)
    {
        if (ViewModel is null) return;
        IReadOnlyList<LauncherItem> items = running
            ? await ViewModel.GetRunningApplicationsAsync(CancellationToken.None)
            : await ViewModel.GetInstalledApplicationsAsync(CancellationToken.None);
        LauncherItem? selected = ShowApplicationPicker(items, running ? Strings.RunningApplicationsTitle : Strings.InstalledApplicationsTitle);
        if (selected is not null) AddItemToSelectedGroup(selected with { Id = CreateId() });
    }

    private void LocateTarget_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedItem is not { } item) return;
        if (item.Kind == LauncherItemKind.Folder)
        {
            var folder = new OpenFolderDialog { Title = Strings.ChooseFolder };
            if (folder.ShowDialog(this) == true) item.Target = folder.FolderName;
        }
        else
        {
            var file = new OpenFileDialog { Title = Strings.ChooseFile, CheckFileExists = true };
            if (file.ShowDialog(this) == true) item.Target = file.FileName;
        }
    }

    private void BrowseIcon_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedItem is not { } item) return;
        var picker = new OpenFileDialog { Filter = Strings.ImageFilter };
        if (picker.ShowDialog(this) == true) item.CustomIconPath = picker.FileName;
    }

    private void Canvas_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Canvas_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        foreach (string path in paths) AddItemToSelectedGroup(CreateFileSystemItem(path));
    }

    private void AddItemToSelectedGroup(LauncherItem item)
    {
        if (ViewModel is null) return;
        if (ViewModel.SelectedGroup is not { } group)
        {
            if (ViewModel.SelectedPage is not { } page) return;
            group = ViewModel.AddGroup(page, Strings.DefaultGroupName);
        }
        ViewModel.AddItem(group, item);
    }

    private LauncherItem? ShowApplicationPicker(IReadOnlyList<LauncherItem> items, string title)
    {
        var list = new ListBox { ItemsSource = items, DisplayMemberPath = "DisplayName", Margin = new Thickness(16), Height = 350 };
        StyleDialogElement(list);
        var window = CreateDialog(title, 500, 480);
        var panel = new DockPanel();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16) };
        var add = new Button
        {
            Content = Strings.Add,
            IsDefault = true,
            MinWidth = 80,
            Height = 30,
            Margin = new Thickness(4),
            Background = FindResource("AccentBrush") as System.Windows.Media.Brush ?? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#0078D4")!,
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0.0),
            Cursor = Cursors.Hand
        };
        add.Click += (_, _) => window.DialogResult = true;
        var cancel = new Button
        {
            Content = Strings.Cancel,
            IsCancel = true,
            MinWidth = 80,
            Height = 30,
            Margin = new Thickness(4),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1AFFFFFF")!,
            Foreground = System.Windows.Media.Brushes.White,
            BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#20FFFFFF")!,
            BorderThickness = new Thickness(1.0),
            Cursor = Cursors.Hand
        };
        buttons.Children.Add(add); buttons.Children.Add(cancel); DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons); panel.Children.Add(list); window.Content = panel;
        return window.ShowDialog() == true ? list.SelectedItem as LauncherItem : null;
    }

    private string? Prompt(string title, string label, string initial)
    {
        var box = new TextBox { Text = initial, Margin = new Thickness(0, 6, 0, 12) };
        var window = CreateDialog(title, 380, 180);
        var panel = new StackPanel { Margin = new Thickness(18) }; panel.Children.Add(new TextBlock { Text = label }); panel.Children.Add(box);
        StyleDialogPanel(panel);
        AddDialogButtons(window, panel); window.Content = panel;
        box.SelectAll(); box.Focus();
        return window.ShowDialog() == true && !string.IsNullOrWhiteSpace(box.Text) ? box.Text.Trim() : null;
    }

    private Window CreateDialog(string title, double width, double height) => new()
    {
        Title = title, Width = width, Height = height, WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Owner = this,
        Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1F1F1F")!,
        Foreground = System.Windows.Media.Brushes.White,
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Yu Gothic UI, Meiryo"),
        ResizeMode = ResizeMode.NoResize
    };

    private void StyleDialogElement(FrameworkElement element)
    {
        if (element is TextBlock tb)
        {
            tb.Foreground = System.Windows.Media.Brushes.White;
            tb.FontSize = 12;
        }
        else if (element is TextBox txt)
        {
            txt.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1AFFFFFF")!;
            txt.Foreground = System.Windows.Media.Brushes.White;
            txt.CaretBrush = System.Windows.Media.Brushes.White;
            txt.BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#20FFFFFF")!;
            txt.Padding = new Thickness(8, 4, 8, 4);
        }
        else if (element is ComboBox cb)
        {
            cb.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1AFFFFFF")!;
            cb.Foreground = System.Windows.Media.Brushes.White;
            cb.BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#20FFFFFF")!;
        }
        else if (element is CheckBox chk)
        {
            chk.Foreground = System.Windows.Media.Brushes.White;
        }
        else if (element is ListBox lb)
        {
            lb.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#0AFFFFFF")!;
            lb.Foreground = System.Windows.Media.Brushes.White;
            lb.BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#20FFFFFF")!;
        }
    }

    private void StyleDialogPanel(Panel panel)
    {
        foreach (UIElement child in panel.Children)
        {
            if (child is FrameworkElement fe) StyleDialogElement(fe);
            if (child is Panel subPanel) StyleDialogPanel(subPanel);
        }
    }

    private void AddDialogButtons(Window window, Panel panel)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var ok = new Button
        {
            Content = Strings.Ok,
            IsDefault = true,
            MinWidth = 80,
            Height = 30,
            Margin = new Thickness(4),
            Background = FindResource("AccentBrush") as System.Windows.Media.Brush ?? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#0078D4")!,
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0.0),
            Cursor = Cursors.Hand
        };
        ok.Click += (_, _) => window.DialogResult = true;

        var cancel = new Button
        {
            Content = Strings.Cancel,
            IsCancel = true,
            MinWidth = 80,
            Height = 30,
            Margin = new Thickness(4),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1AFFFFFF")!,
            Foreground = System.Windows.Media.Brushes.White,
            BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#20FFFFFF")!,
            BorderThickness = new Thickness(1.0),
            Cursor = Cursors.Hand
        };
        row.Children.Add(ok); row.Children.Add(cancel); panel.Children.Add(row);
    }

    private static LauncherItem CreateFileSystemItem(string path)
    {
        bool folder = Directory.Exists(path);
        LauncherItemKind kind = folder ? LauncherItemKind.Folder
            : string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase) ? LauncherItemKind.Win32Application : LauncherItemKind.File;
        return new LauncherItem(CreateId(), folder ? new DirectoryInfo(path).Name : Path.GetFileNameWithoutExtension(path), kind, Path.GetFullPath(path));
    }
    private static string CreateId() => $"item-{Guid.NewGuid():N}";
    private static void Observe(Task task) => _ = task.ContinueWith(completed => System.Diagnostics.Trace.TraceError(completed.Exception?.GetBaseException().ToString()), TaskContinuationOptions.OnlyOnFaulted);
}
