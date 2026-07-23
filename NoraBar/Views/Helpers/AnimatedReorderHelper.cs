using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NoraBar.Views.Helpers;

public sealed class AnimatedReorderHelper
{
    private readonly ItemsControl _itemsControl;
    private readonly Action<int, int> _onReorderCommitted;

    private bool _isDragging;
    private Point _dragStartPoint;
    private FrameworkElement? _draggedContainer;
    private int _initialIndex = -1;
    private int _targetIndex = -1;

    private readonly List<FrameworkElement> _containers = new();
    private readonly List<double> _initialTopPositions = new();
    private readonly List<double> _containerHeights = new();
    private readonly List<TranslateTransform> _containerTranslates = new();

    private TransformGroup? _draggedTransformGroup;
    private TranslateTransform? _draggedTranslate;
    private ScaleTransform? _draggedScale;

    private static readonly IEasingFunction EaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };

    public AnimatedReorderHelper(ItemsControl itemsControl, Action<int, int> onReorderCommitted)
    {
        _itemsControl = itemsControl ?? throw new ArgumentNullException(nameof(itemsControl));
        _onReorderCommitted = onReorderCommitted ?? throw new ArgumentNullException(nameof(onReorderCommitted));
    }

    public void HandlePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging) return;

        FrameworkElement? container = FindItemContainer(e.OriginalSource as DependencyObject);
        if (container == null) return;

        _draggedContainer = container;
        _dragStartPoint = e.GetPosition(_itemsControl);
    }

    public void HandlePreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedContainer == null)
        {
            return;
        }

        Point currentPos = e.GetPosition(_itemsControl);
        Vector diff = _dragStartPoint - currentPos;

        if (!_isDragging)
        {
            if (Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance ||
                Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance)
            {
                StartDrag(e);
            }
        }

        if (_isDragging && _draggedTranslate != null && _initialIndex >= 0)
        {
            double deltaY = currentPos.Y - _dragStartPoint.Y;
            _draggedTranslate.Y = deltaY;

            UpdateItemPositions(deltaY);
        }
    }

    public void HandlePreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            EndDrag();
        }
        else
        {
            _draggedContainer = null;
        }
    }

    private void StartDrag(MouseEventArgs e)
    {
        _containers.Clear();
        _initialTopPositions.Clear();
        _containerHeights.Clear();
        _containerTranslates.Clear();

        int count = _itemsControl.Items.Count;
        _initialIndex = -1;

        for (int i = 0; i < count; i++)
        {
            if (_itemsControl.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement container)
            {
                _containers.Add(container);

                Point relativePos = container.TranslatePoint(new Point(0, 0), _itemsControl);
                _initialTopPositions.Add(relativePos.Y);
                _containerHeights.Add(container.ActualHeight);

                TranslateTransform translateTransform = EnsureTransformGroup(container, out ScaleTransform? _);
                _containerTranslates.Add(translateTransform);

                if (container == _draggedContainer)
                {
                    _initialIndex = i;
                }
            }
        }

        if (_initialIndex < 0 || _draggedContainer == null)
        {
            _draggedContainer = null;
            return;
        }

        _targetIndex = _initialIndex;
        _isDragging = true;

        EnsureTransformGroup(_draggedContainer, out _draggedScale);
        _draggedTranslate = _containerTranslates[_initialIndex];
        _draggedTransformGroup = _draggedContainer.RenderTransform as TransformGroup;

        Panel.SetZIndex(_draggedContainer, 999);

        if (_draggedScale != null)
        {
            DoubleAnimation scaleAnimX = new(1.0, 1.03, TimeSpan.FromMilliseconds(120)) { EasingFunction = EaseOut };
            DoubleAnimation scaleAnimY = new(1.0, 1.03, TimeSpan.FromMilliseconds(120)) { EasingFunction = EaseOut };
            _draggedScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimX);
            _draggedScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimY);
        }

        _draggedContainer.CaptureMouse();
    }

    private void UpdateItemPositions(double deltaY)
    {
        if (_initialIndex < 0 || _containers.Count == 0) return;

        double draggedCenterY = _initialTopPositions[_initialIndex] + (_containerHeights[_initialIndex] / 2.0) + deltaY;

        int newTargetIndex = 0;
        double minDistance = double.MaxValue;

        for (int i = 0; i < _containers.Count; i++)
        {
            double itemCenterY = _initialTopPositions[i] + (_containerHeights[i] / 2.0);
            double dist = Math.Abs(draggedCenterY - itemCenterY);
            if (dist < minDistance)
            {
                minDistance = dist;
                newTargetIndex = i;
            }
        }

        if (newTargetIndex != _targetIndex)
        {
            _targetIndex = newTargetIndex;
            AnimateNonDraggedItems();
        }
    }

    private void AnimateNonDraggedItems()
    {
        double draggedHeight = _containerHeights[_initialIndex];

        for (int i = 0; i < _containers.Count; i++)
        {
            if (i == _initialIndex) continue;

            double targetOffset = 0;
            if (_initialIndex < _targetIndex && i > _initialIndex && i <= _targetIndex)
            {
                targetOffset = -draggedHeight;
            }
            else if (_initialIndex > _targetIndex && i >= _targetIndex && i < _initialIndex)
            {
                targetOffset = draggedHeight;
            }

            TranslateTransform translate = _containerTranslates[i];
            DoubleAnimation anim = new(translate.Y, targetOffset, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = EaseOut
            };
            translate.BeginAnimation(TranslateTransform.YProperty, anim);
        }
    }

    private void EndDrag()
    {
        if (!_isDragging || _draggedContainer == null || _draggedTranslate == null)
        {
            ResetState();
            return;
        }

        _draggedContainer.ReleaseMouseCapture();

        double landingY = 0;
        if (_targetIndex >= 0 && _targetIndex < _initialTopPositions.Count && _initialIndex >= 0)
        {
            landingY = _initialTopPositions[_targetIndex] - _initialTopPositions[_initialIndex];
        }

        DoubleAnimation dropAnim = new(_draggedTranslate.Y, landingY, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = EaseOut
        };

        if (_draggedScale != null)
        {
            DoubleAnimation scaleAnimX = new(1.0, TimeSpan.FromMilliseconds(150)) { EasingFunction = EaseOut };
            DoubleAnimation scaleAnimY = new(1.0, TimeSpan.FromMilliseconds(150)) { EasingFunction = EaseOut };
            _draggedScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimX);
            _draggedScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimY);
        }

        int fromIndex = _initialIndex;
        int toIndex = _targetIndex;
        FrameworkElement draggedContainer = _draggedContainer;

        dropAnim.Completed += (s, e) =>
        {
            Panel.SetZIndex(draggedContainer, 0);

            for (int i = 0; i < _containerTranslates.Count; i++)
            {
                _containerTranslates[i].BeginAnimation(TranslateTransform.YProperty, null);
                _containerTranslates[i].Y = 0;
            }

            ResetState();

            if (fromIndex >= 0 && toIndex >= 0 && fromIndex != toIndex)
            {
                _onReorderCommitted(fromIndex, toIndex);
            }
        };

        _draggedTranslate.BeginAnimation(TranslateTransform.YProperty, dropAnim);
    }

    private void ResetState()
    {
        _isDragging = false;
        _draggedContainer = null;
        _draggedTranslate = null;
        _draggedScale = null;
        _draggedTransformGroup = null;
        _initialIndex = -1;
        _targetIndex = -1;
        _containers.Clear();
        _initialTopPositions.Clear();
        _containerHeights.Clear();
        _containerTranslates.Clear();
    }

    private FrameworkElement? FindItemContainer(DependencyObject? child)
    {
        while (child != null && child != _itemsControl)
        {
            if (_itemsControl.ItemContainerGenerator.IndexFromContainer(child) >= 0 && child is FrameworkElement elem)
            {
                return elem;
            }
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private static TranslateTransform EnsureTransformGroup(FrameworkElement container, out ScaleTransform scaleTransform)
    {
        TranslateTransform? translate = null;
        ScaleTransform? scale = null;

        if (container.RenderTransform is TransformGroup tg)
        {
            foreach (Transform t in tg.Children)
            {
                if (t is TranslateTransform tt) translate = tt;
                if (t is ScaleTransform st) scale = st;
            }
        }

        if (translate == null || scale == null)
        {
            container.RenderTransformOrigin = new Point(0.5, 0.5);
            TransformGroup group = new();
            translate = new TranslateTransform();
            scale = new ScaleTransform();
            group.Children.Add(translate);
            group.Children.Add(scale);
            container.RenderTransform = group;
        }

        scaleTransform = scale;
        return translate;
    }
}
