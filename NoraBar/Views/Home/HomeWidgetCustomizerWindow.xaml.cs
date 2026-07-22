using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NoraBar.ViewModels;

namespace NoraBar.Views.Home;

public partial class HomeWidgetCustomizerWindow : Window
{
    private Point _dragStartPoint;

    public HomeWidgetCustomizerWindow()
    {
        InitializeComponent();
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
