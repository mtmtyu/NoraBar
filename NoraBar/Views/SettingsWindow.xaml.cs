using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NoraBar.ViewModels;
using NoraBar.Views.Island.DesignA_Minimal;
using NoraBar.Views.Island.DesignB_Productivity;

namespace NoraBar.Views
{
    public partial class SettingsWindow : Window
    {
        private MainViewModel? _viewModel;
        private bool _isCloseAnimationCompleted = false;
        private bool _isClosingApp = false;

        public void ForceClose()
        {
            _isClosingApp = true;
            this.Close();
        }

        public void ShowWindow()
        {
            this.Show();
            this.Activate();
            if (this.WindowState == WindowState.Minimized)
            {
                this.WindowState = WindowState.Normal;
            }

            // Clear any active animations that are holding values
            this.RootBorder.BeginAnimation(UIElement.OpacityProperty, null);
            this.WindowScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
            this.WindowScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);

            // Restore properties back to animated states
            this.RootBorder.Opacity = 1.0;
            this.WindowScale.ScaleX = 1.0;
            this.WindowScale.ScaleY = 1.0;
        }

        public SettingsWindow()
        {
            InitializeComponent();
            
            // Clean up event handler on unload to prevent memory leaks
            this.Unloaded += (s, e) =>
            {
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                }
            };
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isClosingApp)
            {
                if (!_isCloseAnimationCompleted)
                {
                    e.Cancel = true;
                    
                    var sb = (System.Windows.Media.Animation.Storyboard)this.Resources["WindowCloseStoryboard"];
                    if (sb != null)
                    {
                        sb = sb.Clone();
                        sb.Completed += (s, ev) =>
                        {
                            _isCloseAnimationCompleted = true;
                            this.Close();
                        };
                        sb.Begin(this);
                    }
                    else
                    {
                        _isCloseAnimationCompleted = true;
                        this.Close();
                    }
                }
            }
            else
            {
                e.Cancel = true;
                if (_viewModel != null)
                {
                    _viewModel.IsPositionEditMode = false;
                }

                var sb = (System.Windows.Media.Animation.Storyboard)this.Resources["WindowCloseStoryboard"];
                if (sb != null)
                {
                    sb = sb.Clone();
                    sb.Completed += (s, ev) =>
                    {
                        this.Hide();
                    };
                    sb.Begin(this);
                }
                else
                {
                    this.Hide();
                }
            }
        }

        private void Window_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is MainViewModel oldVm)
            {
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;
            }
            if (e.NewValue is MainViewModel newVm)
            {
                _viewModel = newVm;
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                UpdatePreview();
                UpdateOverlayVisibilities();
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentVariant) || 
                e.PropertyName == nameof(MainViewModel.ShowProgressBar) ||
                e.PropertyName == nameof(MainViewModel.ShowLyrics))
            {
                Dispatcher.Invoke(UpdatePreview);
            }
            else if (e.PropertyName == nameof(MainViewModel.IsLicenseDialogOpen) || 
                     e.PropertyName == nameof(MainViewModel.IsUpdateDialogOpen))
            {
                Dispatcher.Invoke(UpdateOverlayVisibilities);
            }
        }

        private void UpdateOverlayVisibilities()
        {
            if (_viewModel == null) return;

            bool overlayRequired = _viewModel.IsLicenseDialogOpen || _viewModel.IsUpdateDialogOpen;
            
            if (overlayRequired)
            {
                OverlayDialogs.Visibility = Visibility.Visible;
                
                if (_viewModel.IsLicenseDialogOpen)
                {
                    LicenseDialog.Visibility = Visibility.Visible;
                    UpdateDialog.Visibility = Visibility.Collapsed;
                    
                    var sb = (System.Windows.Media.Animation.Storyboard)this.Resources["DialogOpenStoryboard"];
                    sb?.Begin(LicenseDialog);
                }
                else if (_viewModel.IsUpdateDialogOpen)
                {
                    LicenseDialog.Visibility = Visibility.Collapsed;
                    UpdateDialog.Visibility = Visibility.Visible;
                    
                    var sb = (System.Windows.Media.Animation.Storyboard)this.Resources["DialogOpenStoryboard"];
                    sb?.Begin(UpdateDialog);
                }
                
                var fadeBg = (System.Windows.Media.Animation.Storyboard)this.Resources["OverlayFadeInStoryboard"];
                fadeBg?.Begin(OverlayDialogs);
            }
            else
            {
                var fadeBg = (System.Windows.Media.Animation.Storyboard)this.Resources["OverlayFadeOutStoryboard"];
                if (fadeBg != null)
                {
                    fadeBg = fadeBg.Clone();
                    fadeBg.Completed += (s, ev) =>
                    {
                        if (_viewModel != null && !_viewModel.IsLicenseDialogOpen && !_viewModel.IsUpdateDialogOpen)
                        {
                            OverlayDialogs.Visibility = Visibility.Collapsed;
                            LicenseDialog.Visibility = Visibility.Collapsed;
                            UpdateDialog.Visibility = Visibility.Collapsed;
                        }
                    };
                    fadeBg.Begin(OverlayDialogs);
                }
                else
                {
                    OverlayDialogs.Visibility = Visibility.Collapsed;
                    LicenseDialog.Visibility = Visibility.Collapsed;
                    UpdateDialog.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void UpdatePreview()
        {
            if (_viewModel == null) return;

            double targetWidth = 200;
            double targetHeight = 2;

            UserControl? previewView = null;
            if (_viewModel.CurrentVariant == Models.DesignVariant.MinimalFloatingPill)
            {
                previewView = new DesignAMusicView();
                targetWidth = 450;
                targetHeight = _viewModel.ShowProgressBar ? 106 : 80;
                if (_viewModel.ShowLyrics) targetHeight += 24;
            }
            else
            {
                previewView = new DesignBMusicView();
                targetWidth = 560;
                targetHeight = _viewModel.ShowProgressBar ? 120 : 90;
                if (_viewModel.ShowLyrics) targetHeight += 24;
            }

            // Bind current VM as DataContext for preview so that metadata and progress display correctly
            previewView.DataContext = _viewModel;
            PreviewHost.Content = previewView;
            PreviewHost.Width = targetWidth;
            PreviewHost.Height = targetHeight;
        }
    }
}
