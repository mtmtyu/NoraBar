using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

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

    private TransformGroup? _draggedTransformGroup;
    private TranslateTransform? _draggedTranslate;
    private ScaleTransform? _draggedScale;
    private Transform? _originalRenderTransform;
    private Effect? _originalEffect;
    private int _originalZIndex;

    private static readonly IEasingFunction EaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };

    public WrapPanelAnimatedReorderHelper(Panel containerPanel, Action<int, int> onReorderCommitted)
    {
        _containerPanel = containerPanel ?? throw new ArgumentNullException(nameof(containerPanel));
        _onReorderCommitted = onReorderCommitted ?? throw new ArgumentNullException(nameof(onReorderCommitted));
    }

    public void StartDrag(FrameworkElement itemContainer, Point startMousePos, int index)
    {
        if (_isDragging) return;

        _isDragging = true;
        _draggedContainer = itemContainer;
        _initialIndex = index;
        _targetIndex = index;
        _dragStartPoint = startMousePos;

        _items.Clear();
        _initialPositions.Clear();
        _itemTranslates.Clear();

        foreach (UIElement child in _containerPanel.Children)
        {
            if (child is FrameworkElement fe)
            {
                _items.Add(fe);
                Point pos = fe.TranslatePoint(new Point(0, 0), _containerPanel);
                _initialPositions.Add(pos);

                TranslateTransform tt = EnsureTranslateTransform(fe);
                _itemTranslates.Add(tt);
            }
        }

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

        _originalRenderTransform = itemContainer.RenderTransform;
        _draggedTransformGroup = new TransformGroup();
        _draggedScale = new ScaleTransform(1.05, 1.05);
        _draggedTranslate = new TranslateTransform(0, 0);

        _draggedTransformGroup.Children.Add(_draggedScale);
        _draggedTransformGroup.Children.Add(_draggedTranslate);
        itemContainer.RenderTransform = _draggedTransformGroup;
        itemContainer.LostMouseCapture += DraggedContainer_LostMouseCapture;
        itemContainer.CaptureMouse();
    }

    public void UpdateDrag(Point currentMousePos)
    {
        if (!_isDragging || _draggedContainer == null || _draggedTranslate == null) return;

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
            translate.X = 0;
            translate.Y = 0;
        }

        draggedContainer.RenderTransform = _originalRenderTransform ?? Transform.Identity;
        ClearState();

        if (commit && fromIndex >= 0 && toIndex >= 0 && fromIndex != toIndex)
        {
            _onReorderCommitted(fromIndex, toIndex);
        }
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
        _draggedTransformGroup = null;
        _draggedTranslate = null;
        _draggedScale = null;
        _originalRenderTransform = null;
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
        for (int i = 0; i < _items.Count; i++)
        {
            if (i == _initialIndex) continue;

            int effectiveIndex = i;
            if (_initialIndex < _targetIndex && i > _initialIndex && i <= _targetIndex)
            {
                effectiveIndex = i - 1;
            }
            else if (_initialIndex > _targetIndex && i >= _targetIndex && i < _initialIndex)
            {
                effectiveIndex = i + 1;
            }

            Point originPos = _initialPositions[i];
            Point targetPos = _initialPositions[Math.Clamp(effectiveIndex, 0, _items.Count - 1)];

            double targetShiftX = targetPos.X - originPos.X;
            double targetShiftY = targetPos.Y - originPos.Y;

            TranslateTransform tt = _itemTranslates[i];
            DoubleAnimation animX = new(tt.X, targetShiftX, TimeSpan.FromMilliseconds(150)) { EasingFunction = EaseOut };
            DoubleAnimation animY = new(tt.Y, targetShiftY, TimeSpan.FromMilliseconds(150)) { EasingFunction = EaseOut };

            tt.BeginAnimation(TranslateTransform.XProperty, animX);
            tt.BeginAnimation(TranslateTransform.YProperty, animY);
        }
    }

    private static TranslateTransform EnsureTranslateTransform(FrameworkElement element)
    {
        if (element.RenderTransform is TranslateTransform existingTt)
        {
            return existingTt;
        }

        if (element.RenderTransform is TransformGroup group)
        {
            foreach (Transform child in group.Children)
            {
                if (child is TranslateTransform ttChild) return ttChild;
            }
            TranslateTransform newTt = new TranslateTransform();
            group.Children.Add(newTt);
            return newTt;
        }

        TranslateTransform tt = new TranslateTransform();
        element.RenderTransform = tt;
        return tt;
    }
}
