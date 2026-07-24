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

        _draggedTransformGroup = new TransformGroup();
        _draggedScale = new ScaleTransform(1.05, 1.05);
        _draggedTranslate = new TranslateTransform(0, 0);

        _draggedTransformGroup.Children.Add(_draggedScale);
        _draggedTransformGroup.Children.Add(_draggedTranslate);
        itemContainer.RenderTransform = _draggedTransformGroup;
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

    public void EndDrag()
    {
        if (!_isDragging || _draggedContainer == null) return;

        _isDragging = false;

        // Reset elevation effect
        _draggedContainer.Effect = _originalEffect;
        Panel.SetZIndex(_draggedContainer, _originalZIndex);

        // Reset transforms
        foreach (TranslateTransform tt in _itemTranslates)
        {
            tt.BeginAnimation(TranslateTransform.XProperty, null);
            tt.BeginAnimation(TranslateTransform.YProperty, null);
            tt.X = 0;
            tt.Y = 0;
        }

        _draggedContainer.RenderTransform = Transform.Identity;

        if (_initialIndex >= 0 && _targetIndex >= 0 && _initialIndex != _targetIndex)
        {
            _onReorderCommitted(_initialIndex, _targetIndex);
        }

        _draggedContainer = null;
        _initialIndex = -1;
        _targetIndex = -1;
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
