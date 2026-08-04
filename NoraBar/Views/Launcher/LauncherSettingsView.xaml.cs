using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using NoraBar.Hud.Launcher;
using NoraBar.Services;

namespace NoraBar.Views.Launcher;

public partial class LauncherSettingsView : UserControl
{
    public LauncherSettingsView()
    {
        InitializeComponent();
    }

    private LauncherSettingsViewModel? ViewModel => DataContext as LauncherSettingsViewModel;
    private LauncherLocalization Strings => ViewModel?.Strings ?? new LauncherLocalization(NoraBar.Models.AppLanguage.English);

    private void AddPage_Click(object sender, RoutedEventArgs e)
    {
        string? name = Prompt(Strings.AddPageTitle, Strings.PageName, Strings.DefaultPageName);
        if (name is not null) ViewModel?.AddPage(name);
    }
    private void RemovePage_Click(object sender, RoutedEventArgs e) { if (ViewModel?.SelectedPage is { } page) ViewModel.RemovePage(page); }
    private void MovePageUp_Click(object sender, RoutedEventArgs e) { if (ViewModel?.SelectedPage is { } page) ViewModel.MovePage(page, -1); }
    private void MovePageDown_Click(object sender, RoutedEventArgs e) { if (ViewModel?.SelectedPage is { } page) ViewModel.MovePage(page, 1); }

    private void AddGroup_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedPage is not { } page) return;
        string? name = Prompt(Strings.AddGroupTitle, Strings.GroupName, Strings.DefaultGroupName);
        if (name is not null) ViewModel.AddGroup(page, name);
    }
    private void RemoveGroup_Click(object sender, RoutedEventArgs e) { if (ViewModel is { SelectedPage: { } page, SelectedGroup: { } group }) ViewModel.RemoveGroup(page, group); }
    private void MoveGroupUp_Click(object sender, RoutedEventArgs e) { if (ViewModel is { SelectedPage: { } page, SelectedGroup: { } group }) ViewModel.MoveGroup(page, group, -1); }
    private void MoveGroupDown_Click(object sender, RoutedEventArgs e) { if (ViewModel is { SelectedPage: { } page, SelectedGroup: { } group }) ViewModel.MoveGroup(page, group, 1); }
    private void RemoveItem_Click(object sender, RoutedEventArgs e) { if (ViewModel is { SelectedGroup: { } group, SelectedItem: { } item }) ViewModel.RemoveItem(group, item); }
    private void MoveItemUp_Click(object sender, RoutedEventArgs e) { if (ViewModel is { SelectedGroup: { } group, SelectedItem: { } item }) ViewModel.MoveItem(group, item, -1); }
    private void MoveItemDown_Click(object sender, RoutedEventArgs e) { if (ViewModel is { SelectedGroup: { } group, SelectedItem: { } item }) ViewModel.MoveItem(group, item, 1); }

    private void AddFile_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { CheckFileExists = true, Multiselect = false, Title = ViewModel?.Strings.ChooseFile };
        if (picker.ShowDialog(Window.GetWindow(this)) == true) ShowItemEditorBeforeAdd(CreateFileSystemItem(picker.FileName));
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Multiselect = false, Title = ViewModel?.Strings.ChooseFolder };
        if (picker.ShowDialog(Window.GetWindow(this)) == true) ShowItemEditorBeforeAdd(CreateFileSystemItem(picker.FolderName));
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
        ShowItemEditorBeforeAdd(new LauncherItem(CreateId(), uri.Host, LauncherItemKind.Url, uri.AbsoluteUri));
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
        if (selected is not null) ShowItemEditorBeforeAdd(selected with { Id = CreateId() });
    }

    private void LocateTarget_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedItem is not { } item) return;
        if (item.Kind == LauncherItemKind.Folder)
        {
            var folder = new OpenFolderDialog { Title = Strings.ChooseFolder };
            if (folder.ShowDialog(Window.GetWindow(this)) == true) item.Target = folder.FolderName;
        }
        else
        {
            var file = new OpenFileDialog { Title = Strings.ChooseFile, CheckFileExists = true };
            if (file.ShowDialog(Window.GetWindow(this)) == true) item.Target = file.FileName;
        }
    }

    private void BrowseIcon_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedItem is not { } item) return;
        var picker = new OpenFileDialog { Filter = Strings.ImageFilter };
        if (picker.ShowDialog(Window.GetWindow(this)) == true) item.CustomIconPath = picker.FileName;
    }

    private void Root_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Root_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        foreach (string path in paths) ShowItemEditorBeforeAdd(CreateFileSystemItem(path));
    }

    private void ClearUsage_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(Strings.ClearUsageConfirmation, "NoraBar", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes
            && ViewModel is not null)
        {
            Observe(ViewModel.ClearUsageAsync(CancellationToken.None));
        }
    }

    private void AddRule_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        LauncherRule? rule = ShowRuleEditor(null, ViewModel.Pages);
        if (rule is not null) ViewModel.AddRule(rule);
    }
    private void EditRule_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || RulesList.SelectedItem is not LauncherRule selected) return;
        LauncherRule? edited = ShowRuleEditor(selected, ViewModel.Pages);
        if (edited is not null) ViewModel.ReplaceRule(selected, edited);
    }
    private void DeleteRule_Click(object sender, RoutedEventArgs e) { if (ViewModel is not null && RulesList.SelectedItem is LauncherRule rule) ViewModel.RemoveRule(rule); }
    private void MoveRuleUp_Click(object sender, RoutedEventArgs e) { if (ViewModel is not null && RulesList.SelectedItem is LauncherRule rule) ViewModel.MoveRule(rule, -1); }
    private void MoveRuleDown_Click(object sender, RoutedEventArgs e) { if (ViewModel is not null && RulesList.SelectedItem is LauncherRule rule) ViewModel.MoveRule(rule, 1); }

    private void ApplyShortcuts_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null && ViewModel.ApplyShortcuts(out _, out _))
        {
            MessageBox.Show(Strings.ShortcutsSaved, "NoraBar", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ShowItemEditorBeforeAdd(LauncherItem item)
    {
        if (ViewModel?.SelectedGroup is not { } group) return;
        LauncherItem? edited = ShowItemEditor(item);
        if (edited is not null) ViewModel.AddItem(group, edited);
    }

    private LauncherItem? ShowItemEditor(LauncherItem item)
    {
        var name = new TextBox { Text = item.DisplayName, Margin = new Thickness(0, 4, 0, 8) };
        var target = new TextBox { Text = item.Target, Margin = new Thickness(0, 4, 0, 8) };
        var arguments = new TextBox { Text = item.Arguments ?? string.Empty, Margin = new Thickness(0, 4, 0, 8) };
        var admin = new CheckBox { Content = ViewModel?.Strings.RunAsAdministrator, IsChecked = item.RunAsAdministrator, Margin = new Thickness(0, 4, 0, 12) };
        var window = CreateDialog(Strings.LauncherItemTitle, 430, 350);
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = ViewModel?.Strings.Name }); panel.Children.Add(name);
        panel.Children.Add(new TextBlock { Text = ViewModel?.Strings.Target }); panel.Children.Add(target);
        panel.Children.Add(new TextBlock { Text = ViewModel?.Strings.Arguments }); panel.Children.Add(arguments); panel.Children.Add(admin);
        StyleDialogPanel(panel);
        AddDialogButtons(window, panel);
        window.Content = panel;
        return window.ShowDialog() == true && !string.IsNullOrWhiteSpace(name.Text) && !string.IsNullOrWhiteSpace(target.Text)
            ? item with { DisplayName = name.Text.Trim(), Target = target.Text.Trim(), Arguments = EmptyToNull(arguments.Text), RunAsAdministrator = admin.IsChecked == true }
            : null;
    }

    private LauncherRule? ShowRuleEditor(LauncherRule? existing, IReadOnlyList<LauncherPageEditorViewModel> pages)
    {
        if (pages.Count == 0) return null;
        var target = new ComboBox { ItemsSource = pages, DisplayMemberPath = "DisplayName", SelectedValuePath = "Id", SelectedValue = existing?.TargetPageId ?? pages[0].Id, Margin = new Thickness(0, 4, 0, 8) };
        var priority = new TextBox { Text = (existing?.Priority ?? 0).ToString(), Margin = new Thickness(0, 4, 0, 8) };
        var weekdays = new TextBox { Text = existing?.Conditions.Weekdays is { Count: > 0 } days ? string.Join(',', days) : string.Empty, ToolTip = ViewModel?.Strings.WeekdayHint, Margin = new Thickness(0, 4, 0, 8) };
        var time = new TextBox { Text = FormatTimeRange(existing?.Conditions), ToolTip = ViewModel?.Strings.TimeRangeHint, Margin = new Thickness(0, 4, 0, 8) };
        var foreground = new TextBox { Text = existing?.Conditions.ForegroundApplicationId ?? string.Empty, Margin = new Thickness(0, 4, 0, 8) };
        var enabled = new CheckBox { Content = ViewModel?.Strings.Enabled, IsChecked = existing?.IsEnabled ?? true, Margin = new Thickness(0, 4, 0, 10) };
        var window = CreateDialog(Strings.LauncherRuleTitle, 430, 520);
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = ViewModel?.Strings.TargetPage }); panel.Children.Add(target);
        panel.Children.Add(new TextBlock { Text = ViewModel?.Strings.Priority }); panel.Children.Add(priority);
        panel.Children.Add(new TextBlock { Text = ViewModel?.Strings.Weekdays }); panel.Children.Add(weekdays);
        panel.Children.Add(new TextBlock { Text = ViewModel?.Strings.TimeRange }); panel.Children.Add(time);
        panel.Children.Add(new TextBlock { Text = ViewModel?.Strings.ForegroundIdentity }); panel.Children.Add(foreground); panel.Children.Add(enabled);
        StyleDialogPanel(panel);
        AddDialogButtons(window, panel); window.Content = panel;
        if (window.ShowDialog() != true || target.SelectedValue is not string pageId || !int.TryParse(priority.Text, out int parsedPriority)) return null;
        if (!TryParseWeekdays(weekdays.Text, out IReadOnlyList<DayOfWeek>? parsedDays)
            || !TryParseTimeRange(time.Text, out int? start, out int? end))
        {
            MessageBox.Show(Strings.InvalidRuleCondition, "NoraBar", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
        return new LauncherRule(existing?.Id ?? $"rule-{Guid.NewGuid():N}", enabled.IsChecked == true, parsedPriority, pageId,
            new LauncherRuleConditions(parsedDays, start, end, EmptyToNull(foreground.Text)));
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

    private static Window CreateDialog(string title, double width, double height) => new()
    {
        Title = title, Width = width, Height = height, WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive),
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
    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string FormatTimeRange(LauncherRuleConditions? conditions) => conditions?.StartMinuteOfDay is int start && conditions.EndMinuteOfDay is int end ? $"{start / 60:00}:{start % 60:00}-{end / 60:00}:{end % 60:00}" : string.Empty;
    private static bool TryParseWeekdays(string text, out IReadOnlyList<DayOfWeek>? weekdays)
    {
        weekdays = null; if (string.IsNullOrWhiteSpace(text)) return true;
        var values = new List<DayOfWeek>(); foreach (string part in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) { if (!Enum.TryParse(part, true, out DayOfWeek day)) return false; values.Add(day); }
        weekdays = values; return true;
    }
    private static bool TryParseTimeRange(string text, out int? start, out int? end)
    {
        start = null; end = null; if (string.IsNullOrWhiteSpace(text)) return true;
        string[] parts = text.Split('-', StringSplitOptions.TrimEntries); if (parts.Length != 2 || !TimeOnly.TryParse(parts[0], out TimeOnly startTime) || !TimeOnly.TryParse(parts[1], out TimeOnly endTime)) return false;
        start = startTime.Hour * 60 + startTime.Minute; end = endTime.Hour * 60 + endTime.Minute; return true;
    }
    private static void Observe(Task task) => _ = task.ContinueWith(completed => System.Diagnostics.Trace.TraceError(completed.Exception?.GetBaseException().ToString()), TaskContinuationOptions.OnlyOnFaulted);
}
