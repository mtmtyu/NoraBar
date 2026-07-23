using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NoraBar.Views.Controls
{
    public partial class IconButton : UserControl
    {
        public static readonly DependencyProperty IconTextProperty =
            DependencyProperty.Register(nameof(IconText), typeof(string), typeof(IconButton), new PropertyMetadata("\uE711"));

        public string IconText
        {
            get => (string)GetValue(IconTextProperty);
            set => SetValue(IconTextProperty, value);
        }

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(IconButton), new PropertyMetadata(null));

        public ICommand? Command
        {
            get => (ICommand?)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        static IconButton()
        {
            WidthProperty.OverrideMetadata(typeof(IconButton), new FrameworkPropertyMetadata(40.0));
            HeightProperty.OverrideMetadata(typeof(IconButton), new FrameworkPropertyMetadata(40.0));
            FontSizeProperty.OverrideMetadata(typeof(IconButton), new FrameworkPropertyMetadata(16.0));
        }

        public IconButton()
        {
            InitializeComponent();
        }
    }
}
