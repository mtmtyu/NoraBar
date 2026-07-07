using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NoraBar.Models;

namespace NoraBar.Controls
{
    public partial class MarqueeTextBlock : UserControl
    {
        private Storyboard? _scrollStoryboard;
        private bool _isHovering = false;
        private const double ScrollSpeedPixelsPerSecond = 50.0;
        private const double Spacing = 30.0; // Space between original and duplicate text

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(MarqueeTextBlock), new PropertyMetadata(string.Empty, OnTextChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TextStyleProperty =
            DependencyProperty.Register(nameof(TextStyle), typeof(Style), typeof(MarqueeTextBlock), new PropertyMetadata(null));

        public Style TextStyle
        {
            get => (Style)GetValue(TextStyleProperty);
            set => SetValue(TextStyleProperty, value);
        }

        public static readonly DependencyProperty TextTrimmingProperty =
            DependencyProperty.Register(nameof(TextTrimming), typeof(TextTrimming), typeof(MarqueeTextBlock), new PropertyMetadata(TextTrimming.None));

        public TextTrimming TextTrimming
        {
            get => (TextTrimming)GetValue(TextTrimmingProperty);
            set => SetValue(TextTrimmingProperty, value);
        }

        public static readonly DependencyProperty ScrollModeProperty =
            DependencyProperty.Register(nameof(ScrollMode), typeof(TextScrollMode), typeof(MarqueeTextBlock), new PropertyMetadata(TextScrollMode.Always, OnScrollModeChanged));

        public TextScrollMode ScrollMode
        {
            get => (TextScrollMode)GetValue(ScrollModeProperty);
            set => SetValue(ScrollModeProperty, value);
        }

        public MarqueeTextBlock()
        {
            InitializeComponent();
            Loaded += MarqueeTextBlock_Loaded;
            Unloaded += MarqueeTextBlock_Unloaded;
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.Property == FontSizeProperty)
            {
                ApplyLocalFontSize();
            }
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarqueeTextBlock control)
            {
                control.HiddenTextBlock.Text = control.Text;
                control.MainTextBlock.Text = control.Text;
                control.DuplicateTextBlock.Text = control.Text;
                control.UpdateScrollAnimation();
            }
        }

        private static void OnScrollModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarqueeTextBlock control)
            {
                control.UpdateScrollAnimation();
            }
        }

        private void MarqueeTextBlock_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyLocalFontSize();
            UpdateScrollAnimation();
        }

        private void MarqueeTextBlock_Unloaded(object sender, RoutedEventArgs e)
        {
            StopAnimation();
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateScrollAnimation();
        }

        private void UserControl_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _isHovering = true;
            if (ScrollMode == TextScrollMode.HoverOnly)
            {
                UpdateScrollAnimation();
            }
        }

        private void UserControl_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _isHovering = false;
            if (ScrollMode == TextScrollMode.HoverOnly)
            {
                UpdateScrollAnimation();
            }
        }

        private void ApplyLocalFontSize()
        {
            if (MainTextBlock == null || DuplicateTextBlock == null || HiddenTextBlock == null)
            {
                return;
            }

            if (ReadLocalValue(FontSizeProperty) == DependencyProperty.UnsetValue)
            {
                MainTextBlock.ClearValue(TextBlock.FontSizeProperty);
                DuplicateTextBlock.ClearValue(TextBlock.FontSizeProperty);
                HiddenTextBlock.ClearValue(TextBlock.FontSizeProperty);
                return;
            }

            MainTextBlock.FontSize = FontSize;
            DuplicateTextBlock.FontSize = FontSize;
            HiddenTextBlock.FontSize = FontSize;
        }

        private void UpdateScrollAnimation()
        {
            StopAnimation();

            // Use ContextIdle to ensure layout passes have completed before measuring
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (string.IsNullOrEmpty(Text) || ActualWidth == 0)
                {
                    ResetPositions();
                    return;
                }

                // Clear width restriction before measuring to get true text width
                MainTextBlock.ClearValue(WidthProperty);
                MainTextBlock.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
                double textWidth = MainTextBlock.DesiredSize.Width;

                bool shouldScroll = false;
                if (textWidth > ActualWidth)
                {
                    if (ScrollMode == TextScrollMode.Always)
                    {
                        shouldScroll = true;
                    }
                    else if (ScrollMode == TextScrollMode.HoverOnly && _isHovering)
                    {
                        shouldScroll = true;
                    }
                }

                if (shouldScroll)
                {
                    StartAnimation(textWidth);
                }
                else
                {
                    ResetPositions();
                }
            }), System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        private void ResetPositions()
        {
            MainTransform.X = 0;
            
            // If scrolling is disabled or conditions are not met, enable ellipsis if configured
            if (ScrollMode == TextScrollMode.Disabled || (ScrollMode == TextScrollMode.HoverOnly && !_isHovering))
            {
                MainTextBlock.Width = ActualWidth;
            }
            else
            {
                MainTextBlock.ClearValue(WidthProperty); // Let it render full size if not scrolling but has space
            }

            DuplicateTextBlock.Visibility = Visibility.Collapsed;
        }

        private void StartAnimation(double textWidth)
        {
            MainTextBlock.ClearValue(WidthProperty);
            DuplicateTextBlock.ClearValue(WidthProperty);
            
            DuplicateTextBlock.Visibility = Visibility.Visible;
            
            double totalDistance = textWidth + Spacing;
            double durationSeconds = totalDistance / ScrollSpeedPixelsPerSecond;
            TimeSpan duration = TimeSpan.FromSeconds(durationSeconds);

            // Setup duplicate position
            MainTransform.X = 0;
            DuplicateTransform.X = totalDistance;

            var mainAnim = new DoubleAnimation
            {
                From = 0,
                To = -totalDistance,
                Duration = new Duration(duration),
                RepeatBehavior = RepeatBehavior.Forever
            };

            var duplicateAnim = new DoubleAnimation
            {
                From = totalDistance,
                To = 0,
                Duration = new Duration(duration),
                RepeatBehavior = RepeatBehavior.Forever
            };

            _scrollStoryboard = new Storyboard();
            _scrollStoryboard.Children.Add(mainAnim);
            _scrollStoryboard.Children.Add(duplicateAnim);

            // Target the UIElements and use PropertyPath for RenderTransform
            Storyboard.SetTarget(mainAnim, MainTextBlock);
            Storyboard.SetTargetProperty(mainAnim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

            Storyboard.SetTarget(duplicateAnim, DuplicateTextBlock);
            Storyboard.SetTargetProperty(duplicateAnim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

            _scrollStoryboard.Begin(this, isControllable: true);
        }

        private void StopAnimation()
        {
            if (_scrollStoryboard != null)
            {
                _scrollStoryboard.Stop(this);
                _scrollStoryboard = null;
            }
        }
    }
}
