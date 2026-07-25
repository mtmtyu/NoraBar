using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NoraBar.Hud.Home;
using NoraBar.Hud.Home.Widgets;
using NoraBar.Services;
using NoraBar.Views.Helpers;
using NoraBar.Views.Home.Widgets;

namespace NoraBar.Views.Home;

public partial class DynamicWidgetHomeView : UserControl, IDisposable, IHomeHudManagedResource
{
    private readonly object _managedResourcesLock = new();
    private readonly List<IHomeHudManagedResource> _managedChildResources = [];
    private readonly Action<Exception> _reportCleanupFailure;
    private Point _dragStartPoint;
    private int _draggedWidgetIndex = -1;
    private WrapPanelAnimatedReorderHelper? _reorderHelper;
    private DispatcherOperation? _pendingRebuild;
    private IHomeWidgetPresentationSource? _subscribedSource;
    private bool _isDisposed;

    internal int RebuildCount { get; private set; }
    internal bool HasPendingRebuild => _pendingRebuild is not null;

    public DynamicWidgetHomeView()
        : this(static exception => Trace.TraceError(
            $"Home widget rebuild cleanup failed: {exception}"))
    {
    }

    internal DynamicWidgetHomeView(Action<Exception> reportCleanupFailure)
    {
        ArgumentNullException.ThrowIfNull(reportCleanupFailure);
        _reportCleanupFailure = reportCleanupFailure;
        InitializeComponent();
        DataContextChanged += DynamicWidgetHomeView_DataContextChanged;
    }

    private void DynamicWidgetHomeView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is IHomeWidgetPresentationSource oldVm)
        {
            oldVm.PropertyChanged -= ViewModel_PropertyChanged;
            if (ReferenceEquals(_subscribedSource, oldVm))
            {
                _subscribedSource = null;
            }
        }

        if (e.NewValue is IHomeWidgetPresentationSource newVm)
        {
            newVm.PropertyChanged += ViewModel_PropertyChanged;
            _subscribedSource = newVm;
        }

        RebuildWidgets();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IHomeWidgetPresentationSource.ActiveWidgets)
            or nameof(IHomeWidgetPresentationSource.MaxWidgetWidth)
            or nameof(IHomeWidgetPresentationSource.MaxWidgetHeight)
            or nameof(IHomeWidgetPresentationSource.IsWidgetEditMode))
        {
            ScheduleRebuild();
        }
    }

    private void ScheduleRebuild()
    {
        if (_isDisposed || _pendingRebuild is not null)
        {
            return;
        }

        _pendingRebuild = Dispatcher.InvokeAsync(
            () =>
            {
                _pendingRebuild = null;
                try
                {
                    RebuildWidgets();
                }
                catch (Exception exception)
                {
                    BestEffortResourceReleaser.ReleaseAllAndReport(
                        _reportCleanupFailure,
                        () => throw exception);
                }
            },
            DispatcherPriority.DataBind);
    }

    private void CancelPendingRebuild()
    {
        if (_pendingRebuild?.Status == DispatcherOperationStatus.Pending)
        {
            _pendingRebuild.Abort();
        }

        _pendingRebuild = null;
    }

    public void RebuildWidgets()
    {
        CancelPendingRebuild();
        if (_isDisposed)
        {
            return;
        }

        RebuildCount++;
        _reorderHelper = null;
        DisposeAndClearChildViews();

        if (DataContext is not IHomeWidgetPresentationSource vm)
        {
            return;
        }

        double viewportWidth = HomeWidgetLayoutMetrics.NormalizeMaxWidth(vm.MaxWidgetWidth);
        double viewportHeight = HomeWidgetLayoutMetrics.NormalizeMaxHeight(vm.MaxWidgetHeight);
        double contentWidth = Math.Max(
            1.0,
            viewportWidth - HomeWidgetLayoutMetrics.RootHorizontalPadding);
        double contentHeight = Math.Max(
            1.0,
            viewportHeight - HomeWidgetLayoutMetrics.RootVerticalPadding);

        WidgetsScrollViewer.Width = contentWidth;
        WidgetsScrollViewer.MaxHeight = contentHeight;
        WidgetsContainer.Width = contentWidth;
        EditOutlineBorder.Visibility = vm.IsWidgetEditMode
            ? Visibility.Visible
            : Visibility.Collapsed;

        for (int i = 0; i < vm.ActiveWidgets.Count; i++)
        {
            HomeWidgetConfig widget = vm.ActiveWidgets[i];
            UIElement? element = CreateWidgetElement(widget, vm);
            if (element is null)
            {
                continue;
            }

            FrameworkElement wrapped = WrapWidgetContainer(element, widget, i, vm);
            WidgetsContainer.Children.Add(wrapped);
            if (element is IHomeHudManagedResource managedResource)
            {
                lock (_managedResourcesLock)
                {
                    _managedChildResources.Add(managedResource);
                }
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        CancelPendingRebuild();
        DataContextChanged -= DynamicWidgetHomeView_DataContextChanged;
        if (DataContext is IHomeWidgetPresentationSource viewModel)
        {
            viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _subscribedSource = null;

        _reorderHelper = null;
        DisposeAndClearChildViews();
        DataContext = null;
    }

    private void DisposeAndClearChildViews()
    {
        IHomeHudManagedResource[] managedResources = SnapshotManagedChildResources();
        var resourcesRequiringRelease = new HashSet<IHomeHudManagedResource>(
            managedResources.Where(resource => resource is not IDisposable),
            ReferenceEqualityComparer.Instance);
        BestEffortResourceReleaser.ReleaseAllAndReport(
            _reportCleanupFailure,
            () => DisposeChildViews(WidgetsContainer, resourcesRequiringRelease),
            () => ReleaseManagedResources(resourcesRequiringRelease),
            WidgetsContainer.Children.Clear,
            ClearManagedChildResources);
    }

    public void ReleaseManagedResources()
    {
        IHomeWidgetPresentationSource? source = Interlocked.Exchange(
            ref _subscribedSource,
            null);
        IHomeHudManagedResource[] children;
        lock (_managedResourcesLock)
        {
            children = _managedChildResources.ToArray();
            _managedChildResources.Clear();
        }

        BestEffortResourceReleaser.ReleaseAll(
            [
                () =>
                {
                    if (source is not null)
                    {
                        source.PropertyChanged -= ViewModel_PropertyChanged;
                    }
                },
                .. children.Select<IHomeHudManagedResource, Action>(child =>
                    child.ReleaseManagedResources)
            ]);
    }

    private void ClearManagedChildResources()
    {
        lock (_managedResourcesLock)
        {
            _managedChildResources.Clear();
        }
    }

    private IHomeHudManagedResource[] SnapshotManagedChildResources()
    {
        lock (_managedResourcesLock)
        {
            return _managedChildResources.ToArray();
        }
    }

    private static void ReleaseManagedResources(
        IEnumerable<IHomeHudManagedResource> resources)
    {
        BestEffortResourceReleaser.ReleaseAll(
            resources
                .Select<IHomeHudManagedResource, Action>(resource =>
                    resource.ReleaseManagedResources)
                .ToArray());
    }

    internal static void DisposeChildViews(Panel container)
    {
        var resourcesRequiringRelease = new HashSet<IHomeHudManagedResource>(
            ReferenceEqualityComparer.Instance);
        BestEffortResourceReleaser.ReleaseAll(
            () => DisposeChildViews(container, resourcesRequiringRelease),
            () => ReleaseManagedResources(resourcesRequiringRelease));
    }

    private static void DisposeChildViews(
        Panel container,
        ISet<IHomeHudManagedResource> resourcesRequiringRelease)
    {
        ArgumentNullException.ThrowIfNull(container);

        BestEffortResourceReleaser.ReleaseAll(
            container.Children
                .Cast<UIElement>()
                .Select<UIElement, Action>(child =>
                    () => DisposeElement(child, resourcesRequiringRelease))
                .ToArray());
    }

    private static void DisposeElement(
        DependencyObject element,
        ISet<IHomeHudManagedResource> resourcesRequiringRelease)
    {
        if (element is IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch
            {
                if (element is IHomeHudManagedResource managedResource)
                {
                    resourcesRequiringRelease.Add(managedResource);
                }

                throw;
            }

            return;
        }

        if (element is IHomeHudManagedResource managedOnlyResource)
        {
            resourcesRequiringRelease.Add(managedOnlyResource);
        }

        int childCount = VisualTreeHelper.GetChildrenCount(element);
        for (int index = 0; index < childCount; index++)
        {
            DisposeElement(
                VisualTreeHelper.GetChild(element, index),
                resourcesRequiringRelease);
        }
    }

    private FrameworkElement WrapWidgetContainer(
        UIElement innerWidget,
        HomeWidgetConfig widgetConfig,
        int index,
        IHomeWidgetPresentationSource vm)
    {
        HomeWidgetLayoutSize size = HomeWidgetLayoutMetrics.GetSize(widgetConfig.Style);
        var container = new Grid
        {
            Width = size.Width,
            Height = size.Height,
            Tag = index,
            ClipToBounds = false
        };

        if (innerWidget is FrameworkElement innerElement)
        {
            innerElement.HorizontalAlignment = HorizontalAlignment.Stretch;
            innerElement.VerticalAlignment = VerticalAlignment.Stretch;
        }

        container.Children.Add(innerWidget);

        if (!vm.IsWidgetEditMode)
        {
            return container;
        }

        container.Cursor = Cursors.SizeAll;

        var selectionBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(0xA0, 0x64, 0xB5, 0xF6)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF)),
            IsHitTestVisible = false
        };
        container.Children.Add(selectionBorder);

        var deleteButton = new Button
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

        var buttonTemplate = new ControlTemplate(typeof(Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(
            Border.BackgroundProperty,
            new TemplateBindingExtension(Button.BackgroundProperty));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));

        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(
            ContentPresenter.HorizontalAlignmentProperty,
            HorizontalAlignment.Center);
        contentFactory.SetValue(
            ContentPresenter.VerticalAlignmentProperty,
            VerticalAlignment.Center);

        borderFactory.AppendChild(contentFactory);
        buttonTemplate.VisualTree = borderFactory;
        deleteButton.Template = buttonTemplate;

        deleteButton.Click += (_, e) =>
        {
            e.Handled = true;
            RemoveWidgetAt(index, vm);
        };
        container.Children.Add(deleteButton);

        container.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource is Button
                || IsDescendantOfButton(e.OriginalSource as DependencyObject))
            {
                return;
            }

            _dragStartPoint = e.GetPosition(WidgetsContainer);
            _draggedWidgetIndex = index;
            EnsureReorderHelper(vm);
            _reorderHelper?.StartDrag(container, _dragStartPoint, index);
            container.CaptureMouse();
            e.Handled = true;
        };

        container.PreviewMouseMove += (_, e) =>
        {
            if (container.IsMouseCaptured && _draggedWidgetIndex >= 0)
            {
                Point currentPosition = e.GetPosition(WidgetsContainer);
                _reorderHelper?.UpdateDrag(currentPosition);
            }
        };

        container.PreviewMouseLeftButtonUp += (_, _) =>
        {
            if (!container.IsMouseCaptured)
            {
                return;
            }

            container.ReleaseMouseCapture();
            _reorderHelper?.EndDrag();
            _draggedWidgetIndex = -1;
        };

        return container;
    }

    private void EnsureReorderHelper(IHomeWidgetPresentationSource vm)
    {
        _reorderHelper ??= new WrapPanelAnimatedReorderHelper(WidgetsContainer, (fromIndex, toIndex) =>
        {
            List<HomeWidgetConfig> currentWidgets = vm.ActiveWidgets.ToList();
            if (fromIndex < 0
                || fromIndex >= currentWidgets.Count
                || toIndex < 0
                || toIndex >= currentWidgets.Count
                || fromIndex == toIndex)
            {
                return;
            }

            HomeWidgetConfig item = currentWidgets[fromIndex];
            currentWidgets.RemoveAt(fromIndex);
            currentWidgets.Insert(toIndex, item);
            UpdateWidgets(vm, currentWidgets);
        });
    }

    private static bool IsDescendantOfButton(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is Button)
            {
                return true;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return false;
    }

    private static void RemoveWidgetAt(int index, IHomeWidgetPresentationSource vm)
    {
        List<HomeWidgetConfig> current = vm.ActiveWidgets.ToList();
        if (index < 0 || index >= current.Count)
        {
            return;
        }

        current.RemoveAt(index);
        UpdateWidgets(vm, current);
    }

    private static void UpdateWidgets(IHomeWidgetPresentationSource vm, List<HomeWidgetConfig> newList)
    {
        vm.UpdateActiveWidgets(newList.AsReadOnly());
    }

    private void DynamicWidgetHomeView_DragOver(object sender, DragEventArgs e)
    {
        if (DataContext is not IHomeWidgetPresentationSource vm || !vm.IsWidgetEditMode)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent("NoraBarWidgetReorderIndex")
            || e.Data.GetDataPresent("NoraBarCatalogWidgetConfig"))
        {
            e.Effects = e.Data.GetDataPresent("NoraBarCatalogWidgetConfig")
                ? DragDropEffects.Copy
                : DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void DynamicWidgetHomeView_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not IHomeWidgetPresentationSource vm || !vm.IsWidgetEditMode)
        {
            return;
        }

        List<HomeWidgetConfig> currentWidgets = vm.ActiveWidgets.ToList();

        if (e.Data.GetDataPresent("NoraBarCatalogWidgetConfig")
            && e.Data.GetData("NoraBarCatalogWidgetConfig") is HomeWidgetConfig catalogConfig)
        {
            string newId = $"widget_{catalogConfig.Type.ToString().ToLowerInvariant()}_{Guid.NewGuid():N}";
            var newWidget = new HomeWidgetConfig(newId, catalogConfig.Type, catalogConfig.Style);
            currentWidgets.Add(newWidget);
            UpdateWidgets(vm, currentWidgets);
            return;
        }

        if (e.Data.GetDataPresent("NoraBarWidgetReorderIndex")
            && e.Data.GetData("NoraBarWidgetReorderIndex") is int fromIndex)
        {
            Point dropPoint = e.GetPosition(WidgetsContainer);
            int targetIndex = CalculateDropIndex(dropPoint);

            if (fromIndex >= 0
                && fromIndex < currentWidgets.Count
                && targetIndex >= 0
                && targetIndex <= currentWidgets.Count
                && fromIndex != targetIndex)
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
            if (child is FrameworkElement element)
            {
                Point position = element.TranslatePoint(new Point(0, 0), WidgetsContainer);
                if (dropPoint.X < position.X + (element.ActualWidth / 2.0)
                    && dropPoint.Y < position.Y + element.ActualHeight)
                {
                    return index;
                }

                index++;
            }
        }

        return WidgetsContainer.Children.Count;
    }

    private static UIElement? CreateWidgetElement(HomeWidgetConfig widget, IHomeWidgetPresentationSource vm)
    {
        switch (widget.Type)
        {
            case HomeWidgetType.DigitalClock:
                var clockView = new DigitalClockWidgetView { DataContext = vm };
                clockView.SetStyle(widget.Style);
                return clockView;

            case HomeWidgetType.MediaControls:
                var mediaView = new MediaControlsWidgetView { DataContext = vm };
                mediaView.SetStyle(widget.Style);
                return mediaView;

            default:
                return null;
        }
    }
}
