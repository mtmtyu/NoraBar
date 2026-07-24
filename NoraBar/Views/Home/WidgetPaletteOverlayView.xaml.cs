using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NoraBar.Hud.Home.Widgets;
using NoraBar.ViewModels;

namespace NoraBar.Views.Home;

public partial class WidgetPaletteOverlayView : UserControl
{
    private Point _dragStartPoint;
    private HomeWidgetCustomizerItemViewModel? _draggedItem;

    public WidgetPaletteOverlayView()
    {
        InitializeComponent();
    }

    private void DoneButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mainVm)
        {
            mainVm.IsWidgetEditMode = false;
        }
    }

    private void CatalogItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is HomeWidgetCustomizerItemViewModel item)
        {
            _dragStartPoint = e.GetPosition(this);
            _draggedItem = item;
        }
    }

    private void CatalogItem_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null && sender is FrameworkElement fe)
        {
            Point currentPos = e.GetPosition(this);
            Vector diff = _dragStartPoint - currentPos;
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                HomeWidgetConfig config = _draggedItem.ToConfig();
                DataObject dragData = new DataObject("NoraBarCatalogWidgetConfig", config);
                DragDrop.DoDragDrop(fe, dragData, DragDropEffects.Copy);
                _draggedItem = null;
            }
        }
    }

    private void AddCatalogItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is HomeWidgetCustomizerItemViewModel item)
        {
            if (DataContext is MainViewModel mainVm)
            {
                string newId = $"widget_{item.Type.ToString().ToLowerInvariant()}_{Guid.NewGuid():N}";
                List<HomeWidgetConfig> current = mainVm.ActiveHomeWidgets.ToList();
                current.Add(new HomeWidgetConfig(newId, item.Type, item.Style));
                mainVm.ActiveHomeWidgets = current.AsReadOnly();
            }
        }
    }
}
