using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NoraBar.Hud.Home;
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
        Closed += (s, e) =>
        {
            _previewHomeViewModel?.Dispose();
            _previewHomeViewModel = null;
        };
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

            if (_previewView is null)
            {
                _previewView = new DynamicWidgetHomeView();
                _previewView.DataContext = _previewHomeViewModel;
            }
            else
            {
                _previewView.RebuildWidgets();
            }

            LivePreviewHost.Content = _previewView;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
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
}
