using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NoraBar.Hud.Home;
using NoraBar.Hud.Home.Widgets;
using NoraBar.ViewModels;
using NoraBar.Views.Home.Widgets;

namespace NoraBar.Views.Home;

public partial class DynamicWidgetHomeView : UserControl
{
    private Point _dragStartPoint;
    private int _draggedWidgetIndex = -1;

    public DynamicWidgetHomeView()
    {
        InitializeComponent();
        DataContextChanged += DynamicWidgetHomeView_DataContextChanged;
    }

    private void DynamicWidgetHomeView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged oldVm)
        {
            oldVm.PropertyChanged -= ViewModel_PropertyChanged;
        }

        if (e.NewValue is INotifyPropertyChanged newVm)
        {
            newVm.PropertyChanged += ViewModel_PropertyChanged;
        }

        RebuildWidgets();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HomeHudViewModel.ActiveWidgets)
            or nameof(HomeHudViewModel.MaxWidgetWidth)
            or nameof(HomeHudViewModel.MaxWidgetHeight)
            or nameof(HomeHudViewModel.IsWidgetEditMode))
        {
            RebuildWidgets();
        }
    }

    public void RebuildWidgets()
    {
        WidgetsContainer.Children.Clear();

        if (DataContext is not HomeHudViewModel vm || vm.ActiveWidgets is null)
        {
            return;
        }

        WidgetsScrollViewer.MaxHeight = Math.Max(50.0, vm.MaxWidgetHeight - 16.0);
        WidgetsContainer.MaxWidth = Math.Max(100.0, vm.MaxWidgetWidth - 24.0);

        if (vm.IsWidgetEditMode)
        {
            RootIslandBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0xC0, 0x64, 0xB5, 0xF6));
            RootIslandBorder.BorderThickness = new Thickness(2);
        }
        else
        {
            RootIslandBorder.ClearValue(Border.BorderBrushProperty);
            RootIslandBorder.ClearValue(Border.BorderThicknessProperty);
        }

        bool first = true;
        for (int i = 0; i < vm.ActiveWidgets.Count; i++)
        {
            HomeWidgetConfig widget = vm.ActiveWidgets[i];
            if (!first && !vm.IsWidgetEditMode)
            {
                Border separator = new Border
                {
                    BorderBrush = Brushes.White,
                    Opacity = 0.12,
                    BorderThickness = new Thickness(1, 0, 0, 0),
                    Margin = new Thickness(10, 4, 10, 4)
                };
                WidgetsContainer.Children.Add(separator);
            }
            first = false;

            UIElement? element = CreateWidgetElement(widget, vm);
            if (element != null)
            {
                FrameworkElement wrapped = WrapWidgetContainer(element, widget, i, vm);
                WidgetsContainer.Children.Add(wrapped);
            }
        }
    }

    private FrameworkElement WrapWidgetContainer(UIElement innerWidget, HomeWidgetConfig widgetConfig, int index, HomeHudViewModel vm)
    {
        if (!vm.IsWidgetEditMode)
        {
            return (FrameworkElement)innerWidget;
        }

        Grid container = new Grid
        {
            Margin = new Thickness(4),
            Tag = index,
            Cursor = Cursors.SizeAll
        };

        Border border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x64, 0xB5, 0xF6)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4),
            Background = new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF))
        };
        border.Child = innerWidget;
        container.Children.Add(border);

        Button deleteButton = new Button
        {
            Content = "\uE711",
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 10,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)),
            Width = 20,
            Height = 20,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -6, -6, 0),
            Cursor = Cursors.Hand,
            ToolTip = "Remove Widget"
        };
        ControlTemplate btnTemplate = new ControlTemplate(typeof(Button));
        FrameworkElementFactory borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        FrameworkElementFactory contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFactory.AppendChild(contentFactory);
        btnTemplate.VisualTree = borderFactory;
        deleteButton.Template = btnTemplate;

        deleteButton.Click += (s, e) =>
        {
            e.Handled = true;
            RemoveWidgetAt(index, vm);
        };
        container.Children.Add(deleteButton);

        container.PreviewMouseLeftButtonDown += (s, e) =>
        {
            _dragStartPoint = e.GetPosition(this);
            _draggedWidgetIndex = index;
        };

        container.PreviewMouseMove += (s, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedWidgetIndex >= 0)
            {
                Point currentPos = e.GetPosition(this);
                Vector diff = _dragStartPoint - currentPos;
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    DataObject dragData = new DataObject("NoraBarWidgetReorderIndex", _draggedWidgetIndex);
                    DragDrop.DoDragDrop(container, dragData, DragDropEffects.Move);
                    _draggedWidgetIndex = -1;
                }
            }
        };

        return container;
    }

    private static void RemoveWidgetAt(int index, HomeHudViewModel vm)
    {
        List<HomeWidgetConfig> current = vm.ActiveWidgets.ToList();
        if (index >= 0 && index < current.Count)
        {
            current.RemoveAt(index);
            UpdateWidgets(vm, current);
        }
    }

    private static void UpdateWidgets(HomeHudViewModel vm, List<HomeWidgetConfig> newList)
    {
        vm.UpdateActiveWidgets(newList.AsReadOnly());
    }

    private void DynamicWidgetHomeView_DragOver(object sender, DragEventArgs e)
    {
        if (DataContext is not HomeHudViewModel vm || !vm.IsWidgetEditMode)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent("NoraBarWidgetReorderIndex") || e.Data.GetDataPresent("NoraBarCatalogWidgetConfig"))
        {
            e.Effects = e.Data.GetDataPresent("NoraBarCatalogWidgetConfig") ? DragDropEffects.Copy : DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void DynamicWidgetHomeView_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not HomeHudViewModel vm || !vm.IsWidgetEditMode)
        {
            return;
        }

        List<HomeWidgetConfig> currentWidgets = vm.ActiveWidgets.ToList();

        if (e.Data.GetDataPresent("NoraBarCatalogWidgetConfig") && e.Data.GetData("NoraBarCatalogWidgetConfig") is HomeWidgetConfig catalogConfig)
        {
            string newId = $"widget_{catalogConfig.Type.ToString().ToLowerInvariant()}_{Guid.NewGuid():N}";
            HomeWidgetConfig newWidget = new HomeWidgetConfig(newId, catalogConfig.Type, catalogConfig.Style);
            currentWidgets.Add(newWidget);
            UpdateWidgets(vm, currentWidgets);
            return;
        }

        if (e.Data.GetDataPresent("NoraBarWidgetReorderIndex") && e.Data.GetData("NoraBarWidgetReorderIndex") is int fromIndex)
        {
            Point dropPoint = e.GetPosition(WidgetsContainer);
            int targetIndex = CalculateDropIndex(dropPoint);

            if (fromIndex >= 0 && fromIndex < currentWidgets.Count && targetIndex >= 0 && targetIndex <= currentWidgets.Count && fromIndex != targetIndex)
            {
                HomeWidgetConfig item = currentWidgets[fromIndex];
                currentWidgets.RemoveAt(fromIndex);
                int insertAt = targetIndex > fromIndex ? targetIndex - 1 : targetIndex;
                insertAt = Math.Clamp(insertAt, 0, currentWidgets.Count);
                currentWidgets.Insert(insertAt, item);
                UpdateWidgets(vm, currentWidgets);
            }
        }
    }

    private int CalculateDropIndex(Point dropPoint)
    {
        int index = 0;
        foreach (UIElement child in WidgetsContainer.Children)
        {
            if (child is FrameworkElement fe)
            {
                Point pos = fe.TranslatePoint(new Point(0, 0), WidgetsContainer);
                if (dropPoint.X < pos.X + (fe.ActualWidth / 2.0) && dropPoint.Y < pos.Y + fe.ActualHeight)
                {
                    return index;
                }
                index++;
            }
        }
        return WidgetsContainer.Children.Count;
    }

    private static UIElement? CreateWidgetElement(HomeWidgetConfig widget, HomeHudViewModel vm)
    {
        switch (widget.Type)
        {
            case HomeWidgetType.DigitalClock:
                DigitalClockWidgetView clockView = new DigitalClockWidgetView { DataContext = vm };
                clockView.SetStyle(widget.Style);
                return clockView;

            case HomeWidgetType.MediaControls:
                MediaControlsWidgetView mediaView = new MediaControlsWidgetView { DataContext = vm };
                mediaView.SetStyle(widget.Style);
                return mediaView;

            default:
                return null;
        }
    }
}
