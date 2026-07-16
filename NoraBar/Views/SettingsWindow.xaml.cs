using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using NoraBar.Hud.Music;
using NoraBar.ViewModels;

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
                    _viewModel.Music.PropertyChanged -= MusicViewModel_PropertyChanged;
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
                oldVm.Music.PropertyChanged -= MusicViewModel_PropertyChanged;
            }
            if (e.NewValue is MainViewModel newVm)
            {
                _viewModel = newVm;
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                _viewModel.Music.PropertyChanged += MusicViewModel_PropertyChanged;
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
                     e.PropertyName == nameof(MainViewModel.IsUpdateDialogOpen) ||
                     e.PropertyName == nameof(MainViewModel.IsResetDialogOpen))
            {
                Dispatcher.Invoke(UpdateOverlayVisibilities);
            }
        }

        private void MusicViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MusicViewModel.HasMultipleSessions))
            {
                Dispatcher.Invoke(UpdatePreview);
            }
        }

        private void UpdateOverlayVisibilities()
        {
            if (_viewModel == null) return;

            bool overlayRequired = _viewModel.IsLicenseDialogOpen || _viewModel.IsUpdateDialogOpen || _viewModel.IsResetDialogOpen;
            
            if (overlayRequired)
            {
                OverlayDialogs.Visibility = Visibility.Visible;
                
                if (_viewModel.IsLicenseDialogOpen)
                {
                    LicenseDialog.Visibility = Visibility.Visible;
                    UpdateDialog.Visibility = Visibility.Collapsed;
                    ResetDialog.Visibility = Visibility.Collapsed;
                    
                    var sb = (System.Windows.Media.Animation.Storyboard)this.Resources["DialogOpenStoryboard"];
                    sb?.Begin(LicenseDialog);
                }
                else if (_viewModel.IsUpdateDialogOpen)
                {
                    LicenseDialog.Visibility = Visibility.Collapsed;
                    UpdateDialog.Visibility = Visibility.Visible;
                    ResetDialog.Visibility = Visibility.Collapsed;
                    
                    var sb = (System.Windows.Media.Animation.Storyboard)this.Resources["DialogOpenStoryboard"];
                    sb?.Begin(UpdateDialog);
                }
                else if (_viewModel.IsResetDialogOpen)
                {
                    LicenseDialog.Visibility = Visibility.Collapsed;
                    UpdateDialog.Visibility = Visibility.Collapsed;
                    ResetDialog.Visibility = Visibility.Visible;
                    
                    var sb = (System.Windows.Media.Animation.Storyboard)this.Resources["DialogOpenStoryboard"];
                    sb?.Begin(ResetDialog);
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
                        if (_viewModel != null && !_viewModel.IsLicenseDialogOpen && !_viewModel.IsUpdateDialogOpen && !_viewModel.IsResetDialogOpen)
                        {
                            OverlayDialogs.Visibility = Visibility.Collapsed;
                            LicenseDialog.Visibility = Visibility.Collapsed;
                            UpdateDialog.Visibility = Visibility.Collapsed;
                            ResetDialog.Visibility = Visibility.Collapsed;
                        }
                    };
                    fadeBg.Begin(OverlayDialogs);
                }
                else
                {
                    OverlayDialogs.Visibility = Visibility.Collapsed;
                    LicenseDialog.Visibility = Visibility.Collapsed;
                    UpdateDialog.Visibility = Visibility.Collapsed;
                    ResetDialog.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void UpdatePreview()
        {
            if (_viewModel == null) return;

            MusicHudPreview preview = MusicHudPreviewFactory.Create(
                _viewModel.CurrentVariant,
                _viewModel.ShowProgressBar,
                _viewModel.ShowLyrics,
                _viewModel.Music.HasMultipleSessions,
                _viewModel);

            PreviewHost.Content = preview.View;
            PreviewHost.Width = preview.PreferredSize.Width;
            PreviewHost.Height = preview.PreferredSize.Height;
        }
    }
}
