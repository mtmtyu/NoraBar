using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using NoraBar.Hud.Home.Widgets;

namespace NoraBar.Views.Helpers;

public sealed class WrapPanelAnimatedReorderHelper
{
    private readonly Panel _containerPanel;
    private readonly Action<int, int> _onReorderCommitted;

    private bool _isDragging;
    private Point _dragStartPoint;
    private FrameworkElement? _draggedContainer;
    private int _initialIndex = -1;
    private int _targetIndex = -1;

    private readonly List<FrameworkElement> _items = new();
    private readonly List<Point> _initialPositions = new();
    private readonly List<TranslateTransform> _itemTranslates = new();
    private readonly List<Transform?> _originalRenderTransforms = new();
    private readonly List<Point> _originalRenderTransformOrigins = new();

    private TransformGroup? _draggedTransformGroup;
    private TranslateTransform? _draggedTranslate;
    private ScaleTransform? _draggedScale;
    private Effect? _originalEffect;
    private int _originalZIndex;

    private readonly IEasingFunction _easeOut =
        new CubicEase { EasingMode = EasingMode.EaseOut };

    public WrapPanelAnimatedReorderHelper(Panel containerPanel, Action<int, int> onReorderCommitted)
    {
        _containerPanel = containerPanel ?? throw new ArgumentNullException(nameof(containerPanel));
        _onReorderCommitted = onReorderCommitted ?? throw new ArgumentNullException(nameof(onReorderCommitted));
    }

    public void StartDrag(FrameworkElement itemContainer, Point startMousePos, int index)
    {
        ArgumentNullException.ThrowIfNull(itemContainer);
        if (_isDragging) return;

        ClearState();

        foreach (UIElement child in _containerPanel.Children)
        {
            if (child is FrameworkElement fe)
            {
                _items.Add(fe);
                Point pos = fe.TranslatePoint(new Point(0, 0), _containerPanel);
                _initialPositions.Add(pos);
            }
        }

        if (index < 0
            || index >= _items.Count
            || !ReferenceEquals(_items[index], itemContainer))
        {
            ClearState();
            return;
        }

        foreach (FrameworkElement item in _items)
        {
            _originalRenderTransforms.Add(item.RenderTransform);
            _originalRenderTransformOrigins.Add(item.RenderTransformOrigin);
        }

        for (int i = 0; i < _items.Count; i++)
        {
            FrameworkElement item = _items[i];
            if (i == index) continue; // Dragged item handled separately
            
            _itemTranslates.Add(CreateTemporaryTransform(item, _originalRenderTransforms[i]));
        }
        // Add a placeholder for the dragged item so indices align
        _itemTranslates.Insert(index, new TranslateTransform());

        _isDragging = true;
        _draggedContainer = itemContainer;
        _initialIndex = index;
        _targetIndex = index;
        _dragStartPoint = startMousePos;

        // Elevate dragged item visually
        _originalZIndex = Panel.GetZIndex(itemContainer);
        Panel.SetZIndex(itemContainer, 999);

        _originalEffect = itemContainer.Effect;
        itemContainer.Effect = new DropShadowEffect
        {
            BlurRadius = 16,
            ShadowDepth = 6,
            Opacity = 0.5,
            Color = Colors.Black
        };

        _draggedTransformGroup = new TransformGroup();
        _draggedScale = new ScaleTransform(1.05, 1.05);
        _draggedTranslate = new TranslateTransform(0, 0);

        _draggedTransformGroup.Children.Add(_originalRenderTransforms[index]?.CloneCurrentValue() ?? Transform.Identity);
        _draggedTransformGroup.Children.Add(_draggedScale);
        _draggedTransformGroup.Children.Add(_draggedTranslate);
        itemContainer.RenderTransform = _draggedTransformGroup;
        itemContainer.LostMouseCapture += DraggedContainer_LostMouseCapture;
        itemContainer.CaptureMouse();
    }

    public void UpdateDrag(Point currentMousePos)
    {
        if (!_isDragging || _draggedContainer == null || _draggedTranslate == null) return;
        if (!HasValidDragState())
        {
            FinishDrag(commit: false);
            return;
        }

        double deltaX = currentMousePos.X - _dragStartPoint.X;
        double deltaY = currentMousePos.Y - _dragStartPoint.Y;

        _draggedTranslate.X = deltaX;
        _draggedTranslate.Y = deltaY;

        int newTargetIndex = CalculateTargetIndex(currentMousePos);
        if (newTargetIndex != _targetIndex)
        {
            _targetIndex = newTargetIndex;
            AnimateShiftPositions();
        }
    }

    public void EndDrag() => FinishDrag(commit: true);

    public void CancelDrag() => FinishDrag(commit: false);

    private void DraggedContainer_LostMouseCapture(object sender, MouseEventArgs e) =>
        FinishDrag(commit: false);

    private void FinishDrag(bool commit)
    {
        FrameworkElement? draggedContainer = _draggedContainer;
        if (!_isDragging || draggedContainer is null)
        {
            ClearState();
            return;
        }

        bool canCommit = commit && HasValidDragState();
        int fromIndex = _initialIndex;
        int toIndex = _targetIndex;
        _isDragging = false;
        draggedContainer.LostMouseCapture -= DraggedContainer_LostMouseCapture;
        if (draggedContainer.IsMouseCaptured)
        {
            draggedContainer.ReleaseMouseCapture();
        }

        draggedContainer.Effect = _originalEffect;
        Panel.SetZIndex(draggedContainer, _originalZIndex);
        foreach (TranslateTransform translate in _itemTranslates)
        {
            translate.BeginAnimation(TranslateTransform.XProperty, null);
            translate.BeginAnimation(TranslateTransform.YProperty, null);
        }

        for (int i = 0; i < _items.Count; i++)
        {
            FrameworkElement item = _items[i];
            item.RenderTransform = _originalRenderTransforms[i] ?? Transform.Identity;
            item.RenderTransformOrigin = _originalRenderTransformOrigins[i];
        }

        ClearState();

        if (canCommit && fromIndex != toIndex)
        {
            _onReorderCommitted(fromIndex, toIndex);
        }
    }

    private bool HasValidDragState()
    {
        if (_initialIndex < 0
            || _initialIndex >= _items.Count
            || _targetIndex < 0
            || _targetIndex >= _items.Count
            || _draggedContainer is null
            || !ReferenceEquals(_items[_initialIndex], _draggedContainer))
        {
            return false;
        }

        int itemIndex = 0;
        foreach (UIElement child in _containerPanel.Children)
        {
            if (child is not FrameworkElement item)
            {
                continue;
            }

            if (itemIndex >= _items.Count || !ReferenceEquals(_items[itemIndex], item))
            {
                return false;
            }

            itemIndex++;
        }

        return itemIndex == _items.Count;
    }

    private void ClearState()
    {
        _isDragging = false;
        _draggedContainer = null;
        _initialIndex = -1;
        _targetIndex = -1;
        _items.Clear();
        _initialPositions.Clear();
        _itemTranslates.Clear();
        _originalRenderTransforms.Clear();
        _originalRenderTransformOrigins.Clear();
        _draggedTransformGroup = null;
        _draggedTranslate = null;
        _draggedScale = null;
        _originalEffect = null;
        _originalZIndex = 0;
    }

    private int CalculateTargetIndex(Point mousePos)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            FrameworkElement item = _items[i];
            Point pos = _initialPositions[i];
            if (mousePos.X < pos.X + (item.ActualWidth / 2.0) && mousePos.Y < pos.Y + item.ActualHeight)
            {
                return i;
            }
        }
        return _items.Count - 1;
    }

    private void AnimateShiftPositions()
    {
        IReadOnlyList<Point> reorderedPositions = CalculateReorderedPositions();

        for (int i = 0; i < _items.Count; i++)
        {
            if (i == _initialIndex) continue;

            Point originPos = _initialPositions[i];
            Point targetPos = reorderedPositions[i];

            double targetShiftX = targetPos.X - originPos.X;
            double targetShiftY = targetPos.Y - originPos.Y;

            TranslateTransform tt = _itemTranslates[i];
            DoubleAnimation animX = new(tt.X, targetShiftX, TimeSpan.FromMilliseconds(150)) { EasingFunction = _easeOut };
            DoubleAnimation animY = new(tt.Y, targetShiftY, TimeSpan.FromMilliseconds(150)) { EasingFunction = _easeOut };

            tt.BeginAnimation(TranslateTransform.XProperty, animX);
            tt.BeginAnimation(TranslateTransform.YProperty, animY);
        }
    }

    private IReadOnlyList<Point> CalculateReorderedPositions()
    {
        var reorderedIndices = Enumerable.Range(0, _items.Count).ToList();
        int draggedIndex = reorderedIndices[_initialIndex];
        reorderedIndices.RemoveAt(_initialIndex);
        reorderedIndices.Insert(_targetIndex, draggedIndex);

        HomeWidgetLayoutSize[] reorderedSizes = reorderedIndices
            .Select(index => new HomeWidgetLayoutSize(
                _items[index].ActualWidth,
                _items[index].ActualHeight))
            .ToArray();
        HomeWidgetLayoutPlan plan = HomeWidgetLayoutMetrics.CreatePlan(
            reorderedSizes,
            _containerPanel.ActualWidth);

        var positions = new Point[_items.Count];
        for (int reorderedIndex = 0; reorderedIndex < reorderedIndices.Count; reorderedIndex++)
        {
            int originalIndex = reorderedIndices[reorderedIndex];
            HomeWidgetLayoutPlacement placement = plan.Placements[reorderedIndex];
            positions[originalIndex] = new Point(placement.X, placement.Y);
        }

        return positions;
    }

    private static TranslateTransform CreateTemporaryTransform(FrameworkElement element, Transform? original)
    {
        TranslateTransform tt = new TranslateTransform();
        if (original == null || original == Transform.Identity)
        {
            element.RenderTransform = tt;
        }
        else
        {
            TransformGroup group = new TransformGroup();
            group.Children.Add(original.CloneCurrentValue());
            group.Children.Add(tt);
            element.RenderTransform = group;
        }
        return tt;
    }
}
