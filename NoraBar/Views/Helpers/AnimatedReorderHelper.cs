using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    private readonly List<int> _containerItemIndices = new();
    private readonly List<double> _initialTopPositions = new();
    private readonly List<double> _containerHeights = new();
    private readonly List<Transform> _originalTransforms = new();
    private readonly List<Point> _originalTransformOrigins = new();
    private readonly List<TranslateTransform> _containerTranslates = new();

    private TransformGroup? _draggedTransformGroup;
    private TranslateTransform? _draggedTranslate;
    private ScaleTransform? _draggedScale;

    private static readonly IEasingFunction EaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };

    private int _originalZIndex;
    private bool _isAnimatingSwap;

    public AnimatedReorderHelper(ItemsControl itemsControl, Action<int, int> onReorderCommitted)
    {
        _itemsControl = itemsControl ?? throw new ArgumentNullException(nameof(itemsControl));
        _onReorderCommitted = onReorderCommitted ?? throw new ArgumentNullException(nameof(onReorderCommitted));
    }

    public void AnimateSwap(int fromIndex, int toIndex)
    {
        if (_isDragging || _isAnimatingSwap)
        {
            return;
        }

        int count = _itemsControl.Items.Count;
        if (fromIndex < 0 || fromIndex >= count || toIndex < 0 || toIndex >= count || fromIndex == toIndex)
        {
            return;
        }

        if (_itemsControl.ItemContainerGenerator.ContainerFromIndex(fromIndex) is not FrameworkElement cFrom ||
            _itemsControl.ItemContainerGenerator.ContainerFromIndex(toIndex) is not FrameworkElement cTo)
        {
            _onReorderCommitted(fromIndex, toIndex);
            return;
        }

        _isAnimatingSwap = true;

        Point posFrom = cFrom.TranslatePoint(new Point(0, 0), _itemsControl);
        Point posTo = cTo.TranslatePoint(new Point(0, 0), _itemsControl);
        double deltaY = posTo.Y - posFrom.Y;

        Transform originalFrom = cFrom.RenderTransform;
        Transform originalTo = cTo.RenderTransform;
        Point originalFromOrigin = cFrom.RenderTransformOrigin;
        Point originalToOrigin = cTo.RenderTransformOrigin;
        TranslateTransform tFrom = CreateTransientTransformGroup(cFrom, out _);
        TranslateTransform tTo = CreateTransientTransformGroup(cTo, out _);

        DoubleAnimation animFrom = new(0, deltaY, TimeSpan.FromMilliseconds(160)) { EasingFunction = EaseOut };
        DoubleAnimation animTo = new(0, -deltaY, TimeSpan.FromMilliseconds(160)) { EasingFunction = EaseOut };

        animFrom.Completed += (s, e) =>
        {
            tFrom.BeginAnimation(TranslateTransform.YProperty, null);
            tTo.BeginAnimation(TranslateTransform.YProperty, null);
            cFrom.RenderTransform = originalFrom;
            cFrom.RenderTransformOrigin = originalFromOrigin;
            cTo.RenderTransform = originalTo;
            cTo.RenderTransformOrigin = originalToOrigin;
            _isAnimatingSwap = false;
            _onReorderCommitted(fromIndex, toIndex);
        };

        tFrom.BeginAnimation(TranslateTransform.YProperty, animFrom);
        tTo.BeginAnimation(TranslateTransform.YProperty, animTo);
    }

    public void HandlePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging || _isAnimatingSwap) return;
        if (IsInteractiveControl(e.OriginalSource as DependencyObject)) return;

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
        _containerItemIndices.Clear();
        _initialTopPositions.Clear();
        _containerHeights.Clear();
        _containerTranslates.Clear();
        _originalTransforms.Clear();
        _originalTransformOrigins.Clear();

        int count = _itemsControl.Items.Count;
        _initialIndex = -1;

        for (int i = 0; i < count; i++)
        {
            if (_itemsControl.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement container)
            {
                _containers.Add(container);
                _containerItemIndices.Add(i);

                Point relativePos = container.TranslatePoint(new Point(0, 0), _itemsControl);
                _initialTopPositions.Add(relativePos.Y);
                _containerHeights.Add(container.ActualHeight);

                _originalTransforms.Add(container.RenderTransform);
                _originalTransformOrigins.Add(container.RenderTransformOrigin);
                TranslateTransform translateTransform = CreateTransientTransformGroup(container, out ScaleTransform? _);
                _containerTranslates.Add(translateTransform);

                if (container == _draggedContainer)
                {
                    _initialIndex = _containers.Count - 1;
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

        _draggedScale = (_draggedContainer.RenderTransform as TransformGroup)?.Children
            .OfType<ScaleTransform>()
            .LastOrDefault();
        _draggedTranslate = _containerTranslates[_initialIndex];
        _draggedTransformGroup = _draggedContainer.RenderTransform as TransformGroup;

        _originalZIndex = Panel.GetZIndex(_draggedContainer);
        Panel.SetZIndex(_draggedContainer, 999);

        if (_draggedScale != null)
        {
            DoubleAnimation scaleAnimX = new(1.0, 1.03, TimeSpan.FromMilliseconds(120)) { EasingFunction = EaseOut };
            DoubleAnimation scaleAnimY = new(1.0, 1.03, TimeSpan.FromMilliseconds(120)) { EasingFunction = EaseOut };
            _draggedScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimX);
            _draggedScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimY);
        }

        _draggedContainer.LostMouseCapture += DraggedContainer_LostMouseCapture;
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

        _draggedContainer.LostMouseCapture -= DraggedContainer_LostMouseCapture;
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

        int fromIndex = _containerItemIndices[_initialIndex];
        int toIndex = _containerItemIndices[_targetIndex];
        FrameworkElement draggedContainer = _draggedContainer;

        dropAnim.Completed += (s, e) =>
        {
            Panel.SetZIndex(draggedContainer, _originalZIndex);
            for (int i = 0; i < _containerTranslates.Count; i++)
            {
                _containerTranslates[i].BeginAnimation(TranslateTransform.YProperty, null);
                _containerTranslates[i].Y = 0;
            }
            RestoreContainerTransforms();

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
        _originalZIndex = 0;
        _containers.Clear();
        _containerItemIndices.Clear();
        _initialTopPositions.Clear();
        _containerHeights.Clear();
        _containerTranslates.Clear();
        _originalTransforms.Clear();
        _originalTransformOrigins.Clear();
    }

    private void DraggedContainer_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        if (_draggedContainer is not null)
        {
            _draggedContainer.LostMouseCapture -= DraggedContainer_LostMouseCapture;
            Panel.SetZIndex(_draggedContainer, _originalZIndex);
        }

        foreach (TranslateTransform translate in _containerTranslates)
        {
            translate.BeginAnimation(TranslateTransform.YProperty, null);
            translate.Y = 0;
        }

        _draggedScale?.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _draggedScale?.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        RestoreContainerTransforms();
        ResetState();
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

    private void RestoreContainerTransforms()
    {
        int count = Math.Min(
            _containers.Count,
            Math.Min(_originalTransforms.Count, _originalTransformOrigins.Count));
        for (int i = 0; i < count; i++)
        {
            _containers[i].RenderTransform = _originalTransforms[i];
            _containers[i].RenderTransformOrigin = _originalTransformOrigins[i];
        }
    }

    private static TranslateTransform CreateTransientTransformGroup(
        FrameworkElement container,
        out ScaleTransform scaleTransform)
    {
        Transform original = container.RenderTransform;
        TransformGroup group = new();
        if (original != Transform.Identity)
        {
            group.Children.Add(original.CloneCurrentValue());
        }

        TranslateTransform translate = new();
        scaleTransform = new ScaleTransform();
        group.Children.Add(translate);
        group.Children.Add(scaleTransform);
        container.RenderTransformOrigin = new Point(0.5, 0.5);
        container.RenderTransform = group;
        return translate;
    }

    private bool IsInteractiveControl(DependencyObject? element)
    {
        while (element != null && element != _itemsControl)
        {
            if (element is ButtonBase or RangeBase or TextBoxBase or Selector or Thumb)
            {
                return true;
            }
            if (element is ItemsControl)
            {
                break;
            }
            element = VisualTreeHelper.GetParent(element);
        }
        return false;
    }
}
