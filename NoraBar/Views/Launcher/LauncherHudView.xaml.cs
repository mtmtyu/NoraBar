using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NoraBar.Hud.Launcher;

namespace NoraBar.Views.Launcher;

public partial class LauncherHudView : UserControl
{
    private const double CollapsedHeightThreshold = 20;

    public LauncherHudView()
    {
        InitializeComponent();
    }

    private LauncherHudViewModel? ViewModel => DataContext as LauncherHudViewModel;

    internal void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void Item_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: LauncherItemViewModel item } && ViewModel is not null)
        {
            Observe(ViewModel.LoadIconAsync(item));
        }
    }

    private void Item_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: LauncherItemViewModel item } && ViewModel is not null)
        {
            Observe(ViewModel.ActivateAsync(item, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)));
        }
    }

    private void Page_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: LauncherPageEditorViewModel page } && ViewModel is not null)
        {
            if (!ReferenceEquals(ViewModel.CurrentPage, page))
            {
                ViewModel.CurrentPage = page;
                AnimatePageTransition();
            }
        }
    }

    private void AnimatePageTransition()
    {
        var storyboard = new Storyboard();

        var fadeAnim = new DoubleAnimation
        {
            From = 0.35,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(fadeAnim, ContentAreaGrid);
        Storyboard.SetTargetProperty(fadeAnim, new PropertyPath(OpacityProperty));

        var slideAnim = new DoubleAnimation
        {
            From = 6.0,
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(slideAnim, ContentAreaTransform);
        Storyboard.SetTargetProperty(slideAnim, new PropertyPath(TranslateTransform.YProperty));

        storyboard.Children.Add(fadeAnim);
        storyboard.Children.Add(slideAnim);
        storyboard.Begin();
    }

    private void Item_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not Button button
            || button.DataContext is not LauncherItemViewModel item
            || ViewModel is null) return;
        var menu = new ContextMenu();
        menu.Items.Add(CreateMenuItem(ViewModel.Strings.Open, () => ViewModel.ActivateAsync(item, false)));
        if (item.Item.Kind is LauncherItemKind.Win32Application or LauncherItemKind.PackagedApplication)
        {
            menu.Items.Add(CreateMenuItem(ViewModel.Strings.OpenNew, () => ViewModel.ActivateAsync(item, true)));
            var windows = new MenuItem { Header = ViewModel.Strings.SwitchWindow };
            windows.Items.Add(new MenuItem { Header = ViewModel.Strings.Loading, IsEnabled = false });
            menu.Items.Add(windows);
            Observe(PopulateWindowsAsync(windows, item));
            if (item.Item.Kind == LauncherItemKind.Win32Application)
            {
                menu.Items.Add(CreateMenuItem(ViewModel.Strings.RunAsAdministratorMenu, () =>
                    ViewModel.ActivateAsync(new LauncherItemViewModel(item.Item with { RunAsAdministrator = true }, item.IsMissing), true)));
                menu.Items.Add(CreateMenuItem(ViewModel.Strings.OpenFileLocation, () => OpenFileLocationAsync(item)));
            }
        }
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem(ViewModel.Strings.EditInSettings, () => { ViewModel.EditInSettings(item); return Task.CompletedTask; }));
        button.ContextMenu = menu;
    }

    private async Task PopulateWindowsAsync(MenuItem parent, LauncherItemViewModel item)
    {
        IReadOnlyList<LauncherWindow> windows = await ViewModel!.GetWindowsAsync(item);
        await Dispatcher.InvokeAsync(() =>
        {
            parent.Items.Clear();
            foreach (LauncherWindow window in windows)
            {
                var windowItem = new MenuItem { Header = string.IsNullOrWhiteSpace(window.Title) ? ViewModel.Strings.UntitledWindow : window.Title };
                windowItem.Items.Add(CreateMenuItem(ViewModel.Strings.Switch, async () => await ViewModel.FocusWindowAsync(window)));
                windowItem.Items.Add(CreateMenuItem(ViewModel.Strings.Close, async () => await ViewModel.CloseAsync(window)));
                windowItem.Items.Add(CreateMenuItem(ViewModel.Strings.ForceQuit, async () =>
                {
                    if (MessageBox.Show(ViewModel.Strings.ForceQuitConfirmation, "NoraBar",
                            MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        await ViewModel.ForceQuitAsync(window);
                    }
                }));
                parent.Items.Add(windowItem);
            }
            if (parent.Items.Count == 0) parent.Items.Add(new MenuItem { Header = ViewModel.Strings.NoOpenWindows, IsEnabled = false });
        });
    }

    private static Task OpenFileLocationAsync(LauncherItemViewModel item)
    {
        if (File.Exists(item.Item.Target))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.Item.Target}\"") { UseShellExecute = true });
        }
        return Task.CompletedTask;
    }

    private void Root_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.PreviousSize.Height >= CollapsedHeightThreshold
            && e.NewSize.Height < CollapsedHeightThreshold)
        {
            ViewModel?.NotifyCollapsed();
        }
    }

    private void Root_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (ViewModel is null || !ViewModel.IsExpanded || string.IsNullOrEmpty(e.Text)) return;
        SearchBox.Focus();
        SearchBox.CaretIndex = SearchBox.Text.Length;
        SearchBox.SelectedText = e.Text;
        e.Handled = true;
    }

    private void Root_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel is null || !ViewModel.IsExpanded) return;
        switch (e.Key)
        {
            case Key.Up: ViewModel.MoveSearchSelection(-1); e.Handled = true; break;
            case Key.Down: ViewModel.MoveSearchSelection(1); e.Handled = true; break;
            case Key.Enter: Observe(ViewModel.ActivateSelectedSearchResultAsync()); e.Handled = true; break;
            case Key.Escape: ViewModel.SearchQuery = string.Empty; Focus(); e.Handled = true; break;
        }
    }

    private static MenuItem CreateMenuItem(string header, Func<Task> action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => Observe(action());
        return item;
    }

    private static void Observe(Task task) =>
        _ = task.ContinueWith(
            completed => Trace.TraceError(completed.Exception?.GetBaseException().ToString()),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
}
