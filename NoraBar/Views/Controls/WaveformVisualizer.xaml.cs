using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NoraBar.Views.Controls
{
    public partial class WaveformVisualizer : UserControl
    {
        private const int BarCount = 8;
        private readonly Rectangle[] _bars;
        private readonly double _maxHeight = 60.0;
        private readonly double _minHeight = 4.0;

        public static readonly DependencyProperty SpectrumDataProperty =
            DependencyProperty.Register(nameof(SpectrumData), typeof(float[]), typeof(WaveformVisualizer), new PropertyMetadata(null, OnSpectrumDataChanged));

        public float[]? SpectrumData
        {
            get => (float[]?)GetValue(SpectrumDataProperty);
            set => SetValue(SpectrumDataProperty, value);
        }

        public static readonly DependencyProperty BarColorProperty =
            DependencyProperty.Register(nameof(BarColor), typeof(Brush), typeof(WaveformVisualizer), new PropertyMetadata(Brushes.White, OnBarColorChanged));

        public Brush? BarColor
        {
            get => (Brush?)GetValue(BarColorProperty);
            set => SetValue(BarColorProperty, value);
        }

        public WaveformVisualizer()
        {
            InitializeComponent();
            _bars = new Rectangle[BarCount];
            
            for (int i = 0; i < BarCount; i++)
            {
                var rect = new Rectangle
                {
                    Width = 3,
                    Height = _minHeight,
                    RadiusX = 1.5,
                    RadiusY = 1.5,
                    Fill = BarColor ?? Brushes.White,
                    Margin = new Thickness(1, 0, 1, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                _bars[i] = rect;
                BarsContainer.Children.Add(rect);
            }
        }

        private static void OnBarColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformVisualizer visualizer && e.NewValue is Brush brush)
            {
                foreach (var bar in visualizer._bars)
                {
                    bar.Fill = brush;
                }
            }
        }

        private static void OnSpectrumDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaveformVisualizer visualizer && e.NewValue is float[] data)
            {
                visualizer.UpdateBars(data);
            }
        }

        private void UpdateBars(float[]? data)
        {
            if (data == null) return;
            
            int limit = Math.Min(data.Length, BarCount);
            for (int i = 0; i < limit; i++)
            {
                // Smooth out the value visually
                double targetHeight = _minHeight + (data[i] * (_maxHeight - _minHeight));
                if (double.IsNaN(targetHeight) || double.IsInfinity(targetHeight)) targetHeight = _minHeight;
                
                // Simple easing
                double currentHeight = _bars[i].Height;
                _bars[i].Height = currentHeight + (targetHeight - currentHeight) * 0.6;
            }
        }
    }
}
