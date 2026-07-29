using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NoraBar.Hud.Home;
using NoraBar.Services;
using NoraBar.ViewModels;
using NoraBar.Views.Helpers;

namespace NoraBar.Views.Home;

public partial class HomeWidgetCustomizerWindow : Window
{
    private DynamicWidgetHomeView? _previewView;
    private HomeHudViewModel? _previewHomeViewModel;
    private readonly AnimatedReorderHelper _reorderHelper;

    public MainViewModel? MainViewModel { get; set; }

    public HomeWidgetCustomizerWindow()
    {
        InitializeComponent();
        _reorderHelper = new AnimatedReorderHelper(ActiveWidgetsListBox, (fromIdx, toIdx) =>
        {
            if (DataContext is HomeWidgetCustomizerViewModel vm)
            {
                vm.MoveItem(fromIdx, toIdx);
            }
        });

        DataContextChanged += HomeWidgetCustomizerWindow_DataContextChanged;
        Closed += HomeWidgetCustomizerWindow_Closed;
    }

    private void HomeWidgetCustomizerWindow_Closed(object? sender, EventArgs e) =>
        CleanupPreviewResources();

    internal void CleanupPreviewResources()
    {
        HomeWidgetCustomizerViewModel? customizerViewModel =
            DataContext as HomeWidgetCustomizerViewModel;
        DynamicWidgetHomeView? previewView = _previewView;
        HomeHudViewModel? previewHomeViewModel = _previewHomeViewModel;

        BestEffortResourceReleaser.ReleaseAllAndReport(
            static exception => Trace.TraceError(
                $"Home widget preview cleanup failed: {exception}"),
            () =>
            {
                if (customizerViewModel is not null)
                {
                    customizerViewModel.PreviewInvalidated -= CustomizerVm_PreviewInvalidated;
                }
            },
            () => DataContextChanged -= HomeWidgetCustomizerWindow_DataContextChanged,
            () => previewView?.Dispose(),
            () => _previewView = null,
            () => LivePreviewHost.Content = null,
            () => previewHomeViewModel?.Dispose(),
            () => _previewHomeViewModel = null);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateLivePreview();
    }

    private void HomeWidgetCustomizerWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is HomeWidgetCustomizerViewModel oldVm)
        {
            oldVm.PreviewInvalidated -= CustomizerVm_PreviewInvalidated;
        }

        if (e.NewValue is HomeWidgetCustomizerViewModel newVm)
        {
            newVm.PreviewInvalidated += CustomizerVm_PreviewInvalidated;
            UpdateLivePreview();
        }
    }

    private void CustomizerVm_PreviewInvalidated(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(UpdateLivePreview);
    }

    private void UpdateLivePreview()
    {
        if (DataContext is not HomeWidgetCustomizerViewModel customizerVm)
        {
            return;
        }

        if (MainViewModel != null)
        {
            if (_previewHomeViewModel is null)
            {
                _previewHomeViewModel = new HomeHudViewModel(MainViewModel);
                _previewHomeViewModel.Initialize();
                _previewHomeViewModel.Start();
            }

            _previewHomeViewModel.OverrideWidgets = customizerVm.GetResultConfigs();
            _previewHomeViewModel.OverrideMaxWidgetWidth = customizerVm.MaxWidgetWidth;
            _previewHomeViewModel.OverrideMaxWidgetHeight = customizerVm.MaxWidgetHeight;

            if (_previewView is null)
            {
                _previewView = new DynamicWidgetHomeView();
                _previewView.DataContext = _previewHomeViewModel;
            }

            LivePreviewHost.Content = _previewView;
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        CancelAndClose();
        e.Handled = true;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left
            || IsInsideButton(e.OriginalSource as DependencyObject))
        {
            return;
        }

        DragMove();
        e.Handled = true;
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is Button)
            {
                return true;
            }

            current = current is Visual
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return false;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) =>
        CancelAndClose();

    private void CancelButton_Click(object sender, RoutedEventArgs e) =>
        CancelAndClose();

    private void CancelAndClose()
    {
        DialogResult = false;
        Close();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void ActiveWidgets_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _reorderHelper.HandlePreviewMouseLeftButtonDown(sender, e);
    }

    private void ActiveWidgets_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        _reorderHelper.HandlePreviewMouseMove(sender, e);
    }

    private void ActiveWidgets_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _reorderHelper.HandlePreviewMouseLeftButtonUp(sender, e);
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is HomeWidgetCustomizerItemViewModel item && DataContext is HomeWidgetCustomizerViewModel vm)
        {
            int index = vm.ActiveWidgets.IndexOf(item);
            if (index > 0)
            {
                _reorderHelper.AnimateSwap(index, index - 1);
            }
        }
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is HomeWidgetCustomizerItemViewModel item && DataContext is HomeWidgetCustomizerViewModel vm)
        {
            int index = vm.ActiveWidgets.IndexOf(item);
            if (index >= 0 && index < vm.ActiveWidgets.Count - 1)
            {
                _reorderHelper.AnimateSwap(index, index + 1);
            }
        }
    }
}
