using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    private void OpenEditor_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        var editorWindow = new LauncherEditorWindow
        {
            Owner = Window.GetWindow(this),
            DataContext = ViewModel
        };
        editorWindow.ShowDialog();
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
        ViewModel?.ApplyShortcuts(out _, out _);
    }

    private LauncherRule? ShowRuleEditor(LauncherRule? existing, IReadOnlyList<LauncherPageEditorViewModel> pages)
    {
        if (ViewModel is null) return null;
        var pageCombo = new ComboBox { ItemsSource = pages, DisplayMemberPath = "DisplayName", SelectedValuePath = "Id", Margin = new Thickness(0, 4, 0, 12) };
        if (existing is not null) pageCombo.SelectedValue = existing.TargetPageId; else pageCombo.SelectedIndex = 0;

        var priorityBox = new TextBox { Text = (existing?.Priority ?? 0).ToString(), Margin = new Thickness(0, 4, 0, 12) };
        var weekdaysBox = new TextBox { Text = string.Join(',', existing?.Conditions?.Weekdays ?? Array.Empty<DayOfWeek>()), Margin = new Thickness(0, 4, 0, 12) };
        var timeRangeBox = new TextBox { Text = FormatTimeRange(existing?.Conditions), Margin = new Thickness(0, 4, 0, 12) };
        var foregroundBox = new TextBox { Text = existing?.Conditions?.ForegroundApplicationId ?? string.Empty, Margin = new Thickness(0, 4, 0, 12) };
        var enabledCheck = new CheckBox { Content = Strings.Enabled, IsChecked = existing?.IsEnabled ?? true, Margin = new Thickness(0, 4, 0, 12) };

        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = Strings.TargetPage }); panel.Children.Add(pageCombo);
        panel.Children.Add(new TextBlock { Text = Strings.Priority }); panel.Children.Add(priorityBox);
        panel.Children.Add(new TextBlock { Text = Strings.Weekdays }); panel.Children.Add(new TextBlock { Text = Strings.WeekdayHint, FontSize = 10, Foreground = System.Windows.Media.Brushes.Gray }); panel.Children.Add(weekdaysBox);
        panel.Children.Add(new TextBlock { Text = Strings.TimeRange }); panel.Children.Add(new TextBlock { Text = Strings.TimeRangeHint, FontSize = 10, Foreground = System.Windows.Media.Brushes.Gray }); panel.Children.Add(timeRangeBox);
        panel.Children.Add(new TextBlock { Text = Strings.ForegroundIdentity }); panel.Children.Add(foregroundBox);
        panel.Children.Add(enabledCheck);

        StyleDialogPanel(panel);
        var window = CreateDialog(Strings.LauncherRuleTitle, 430, 520);
        AddDialogButtons(window, panel);
        window.Content = panel;

        if (window.ShowDialog() != true) return null;
        if (!int.TryParse(priorityBox.Text, out int parsedPriority)
            || !TryParseWeekdays(weekdaysBox.Text, out var parsedDays)
            || !TryParseTimeRange(timeRangeBox.Text, out int? start, out int? end)
            || pageCombo.SelectedValue is not string pageId)
        {
            MessageBox.Show(Strings.InvalidRuleCondition, "NoraBar", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        return new LauncherRule(existing?.Id ?? $"rule-{Guid.NewGuid():N}", enabledCheck.IsChecked == true, parsedPriority, pageId,
            new LauncherRuleConditions(parsedDays, start, end, EmptyToNull(foregroundBox.Text)));
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
