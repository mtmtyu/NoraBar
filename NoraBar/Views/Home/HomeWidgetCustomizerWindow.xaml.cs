using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NoraBar.Hud.Home;
using NoraBar.ViewModels;

namespace NoraBar.Views.Home;

public partial class HomeWidgetCustomizerWindow : Window
{
    private Point _dragStartPoint;
    private DynamicWidgetHomeView? _previewView;
    private HomeHudViewModel? _previewHomeViewModel;

    public MainViewModel? MainViewModel { get; set; }

    public HomeWidgetCustomizerWindow()
    {
        InitializeComponent();
        DataContextChanged += HomeWidgetCustomizerWindow_DataContextChanged;
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
            _previewHomeViewModel ??= new HomeHudViewModel(MainViewModel);
            _previewView ??= new DynamicWidgetHomeView();

            // Create temporary MainViewModel or update active widgets for preview
            var currentActive = MainViewModel.ActiveHomeWidgets;
            MainViewModel.ActiveHomeWidgets = customizerVm.GetResultConfigs();

            _previewHomeViewModel.Initialize();
            _previewView.DataContext = _previewHomeViewModel;
            _previewView.RebuildWidgets();

            // Restore MainViewModel.ActiveHomeWidgets so uncommitted changes aren't persisted until user clicks Save
            MainViewModel.ActiveHomeWidgets = currentActive;

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
        _dragStartPoint = e.GetPosition(null);
    }

    private void ActiveWidgets_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void ActiveWidgets_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not HomeWidgetCustomizerViewModel vm)
        {
            return;
        }

        if (e.Data.GetData(typeof(HomeWidgetCustomizerItemViewModel)) is HomeWidgetCustomizerItemViewModel droppedData)
        {
            int oldIndex = vm.ActiveWidgets.IndexOf(droppedData);
            Point pos = e.GetPosition(ActiveWidgetsListBox);
            IInputElement target = ActiveWidgetsListBox.InputHitTest(pos);
            ListBoxItem? targetItem = FindParent<ListBoxItem>(target as DependencyObject);

            if (targetItem != null && targetItem.DataContext is HomeWidgetCustomizerItemViewModel targetData)
            {
                int newIndex = vm.ActiveWidgets.IndexOf(targetData);
                if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex)
                {
                    vm.MoveItem(oldIndex, newIndex);
                }
            }
        }
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent)
            {
                return parent;
            }
            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        }
        return null;
    }
}
