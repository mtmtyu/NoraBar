using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NoraBar.Models;
using NoraBar.Services;
using NoraBar.ViewModels;
using NoraBar.Views.Island.DesignA_Minimal;
using NoraBar.Views.Island.DesignB_Productivity;

using NotifyIcon = System.Windows.Forms.NotifyIcon;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using ToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;
using ToolStripSeparator = System.Windows.Forms.ToolStripSeparator;
using Icon = System.Drawing.Icon;
using SystemIcons = System.Drawing.SystemIcons;

namespace NoraBar
{
    public partial class MainWindow : Window
    {
        private static readonly TimeSpan FullscreenRecheckInterval = TimeSpan.FromMilliseconds(500);
        private readonly MainViewModel _viewModel;
        private readonly DispatcherTimer _fullscreenRecheckTimer;
        private bool _isClosingApp = false;
        private Views.SettingsWindow? _settingsWindow;
        private NotifyIcon? _notifyIcon;
        private ToolStripMenuItem? _settingsTrayMenuItem;
        private ToolStripMenuItem? _exitTrayMenuItem;
        private System.Threading.CancellationTokenSource? _updateCheckCts;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            _fullscreenRecheckTimer = new DispatcherTimer
            {
                Interval = FullscreenRecheckInterval
            };
            _fullscreenRecheckTimer.Tick += FullscreenRecheckTimer_Tick;
            this.DataContext = _viewModel;

            ApplyWindowPosition();

            // Initialize visual state to Idle
            _viewModel.CurrentState = IslandState.Idle;
            UpdateView();

            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Initialize system tray resident features
            InitializeSystemTray();
            UpdateLocalizedShellText();
        }

        protected override async void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            // Open settings window if launched normally (not via startup)
            string[] args = Environment.GetCommandLineArgs();
            if (!args.Contains("--startup"))
            {
                OpenSettings();
            }

            if (_viewModel.CheckUpdateOnStartup)
            {
                _updateCheckCts = new System.Threading.CancellationTokenSource();
                bool hasUpdate = await _viewModel.CheckForUpdatesSilentlyAsync(_updateCheckCts.Token);
                if (hasUpdate && !_isClosingApp)
                {
                    OpenSettings();
                }
            }
        }

        private void ApplyWindowPosition()
        {
            if (_viewModel.HasCustomPosition)
            {
                this.Left = _viewModel.WindowLeft;
                var deviceTopLeft = DipsToDevice(new Point(_viewModel.WindowLeft, _viewModel.WindowTop));
                var deviceSize = DipsToDevice(new Point(this.Width, this.Height));
                var rect = new System.Drawing.Rectangle(
                    (int)deviceTopLeft.X,
                    (int)deviceTopLeft.Y,
                    (int)deviceSize.X,
                    (int)deviceSize.Y);
                var screen = System.Windows.Forms.Screen.FromRectangle(rect);
                this.Top = GetScreenBoundsInDips(screen).Top;
            }
            else
            {
                var screen = System.Windows.Forms.Screen.PrimaryScreen;
                if (screen != null)
                {
                    var bounds = GetScreenBoundsInDips(screen);
                    this.Left = bounds.Left + (bounds.Width - this.Width) / 2;
                    this.Top = bounds.Top;
                }
            }
        }

        private Point DipsToDevice(Point point)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            return new Point(point.X * dpi.DpiScaleX, point.Y * dpi.DpiScaleY);
        }

        private Point DeviceToDips(Point point)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            return new Point(point.X / dpi.DpiScaleX, point.Y / dpi.DpiScaleY);
        }

        private Rect GetScreenBoundsInDips(System.Windows.Forms.Screen screen)
        {
            var topLeft = DeviceToDips(new Point(screen.Bounds.Left, screen.Bounds.Top));
            var bottomRight = DeviceToDips(new Point(screen.Bounds.Right, screen.Bounds.Bottom));
            return new Rect(topLeft, bottomRight);
        }

        private System.Windows.Forms.Screen GetScreenFromDips(Point point)
        {
            var devicePoint = DipsToDevice(point);
            return System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)devicePoint.X, (int)devicePoint.Y));
        }

        private Point GetMouseScreenPositionInDips(MouseEventArgs e)
        {
            return DeviceToDips(PointToScreen(e.GetPosition(this)));
        }

        private void UpdateEditModeVisuals()
        {
            if (_viewModel.IsPositionEditMode)
            {
                HudBorder.Background = new SolidColorBrush(Color.FromArgb(0x40, 0x00, 0xFF, 0x00));
                HudBorder.Cursor = Cursors.SizeAll;
                _viewModel.CurrentState = IslandState.Music;
            }
            else
            {
                HudBorder.Background = new SolidColorBrush(Color.FromArgb(0x01, 0xFF, 0xFF, 0xFF));
                HudBorder.Cursor = Cursors.Arrow;
                if (!HudBorder.IsMouseOver)
                {
                    _viewModel.CurrentState = IslandState.Idle;
                }
            }
        }

        private void InitializeSystemTray()
        {
            _notifyIcon = new NotifyIcon();

            try
            {
                string? appPath = System.Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(appPath))
                {
                    _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(appPath);
                }
            }
            catch
            {
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }

            _notifyIcon.Text = "NoraBar";
            _notifyIcon.Visible = true;

            // Double click to open settings
            _notifyIcon.DoubleClick += (s, e) => Dispatcher.Invoke(() => OpenSettings());

            // Build Tray Menu
            var contextMenu = new ContextMenuStrip();

            _settingsTrayMenuItem = new ToolStripMenuItem();
            _settingsTrayMenuItem.Click += (s, e) => Dispatcher.Invoke(() => OpenSettings());
            contextMenu.Items.Add(_settingsTrayMenuItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            _exitTrayMenuItem = new ToolStripMenuItem();
            _exitTrayMenuItem.Click += (s, e) => Dispatcher.Invoke(() => CloseApplication());
            contextMenu.Items.Add(_exitTrayMenuItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void OpenSettings()
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.ShowWindow();
                return;
            }

            _settingsWindow = new Views.SettingsWindow
            {
                DataContext = _viewModel
            };
            _settingsWindow.Closed += (s, e) => _settingsWindow = null;
            _settingsWindow.ShowWindow();
        }

        private void CloseApplication()
        {
            CloseMenu_Click(this, new RoutedEventArgs());
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentVariant) ||
                e.PropertyName == nameof(MainViewModel.CurrentState) ||
                e.PropertyName == nameof(MainViewModel.ShowProgressBar) ||
                e.PropertyName == nameof(MainViewModel.ShowLyrics) ||
                e.PropertyName == nameof(MainViewModel.DisableExpandOnFullscreen))
            {
                Dispatcher.Invoke(UpdateView);
            }
            else if (e.PropertyName == nameof(MainViewModel.SelectedLanguage))
            {
                Dispatcher.Invoke(UpdateLocalizedShellText);
            }
            else if (e.PropertyName == nameof(MainViewModel.HasCustomPosition))
            {
                Dispatcher.Invoke(ApplyWindowPosition);
            }
            else if (e.PropertyName == nameof(MainViewModel.IsPositionEditMode))
            {
                Dispatcher.Invoke(UpdateEditModeVisuals);
            }
        }

        private void UpdateLocalizedShellText()
        {
            string settingsText = LocalizationService.GetText(_viewModel.SelectedLanguage, LocalizationKey.SettingsMenu);
            string exitText = LocalizationService.GetText(_viewModel.SelectedLanguage, LocalizationKey.Exit);

            SettingsContextMenuItem.Header = settingsText;
            ExitContextMenuItem.Header = exitText;

            if (_settingsTrayMenuItem != null)
            {
                _settingsTrayMenuItem.Text = settingsText;
            }

            if (_exitTrayMenuItem != null)
            {
                _exitTrayMenuItem.Text = exitText;
            }
        }

        private void UpdateView()
        {
            if (_isClosingApp) return;

            _fullscreenRecheckTimer.Stop();
            double targetWidth = 200;
            double targetHeight = 2;
            bool isIdle = _viewModel.CurrentState == IslandState.Idle;

            UserControl? view = null;

            if (_viewModel.CurrentVariant == DesignVariant.MinimalFloatingPill)
            {
                if (isIdle)
                {
                    targetWidth = 200;
                    targetHeight = 2;
                }
                else // Music State
                {
                    view = new DesignAMusicView();
                    targetWidth = 450;
                    targetHeight = _viewModel.ShowProgressBar ? 106 : 80;
                    if (_viewModel.ShowLyrics) targetHeight += 24;
                }
            }
            else // ProductivityCommandIsland (Design B)
            {
                if (isIdle)
                {
                    targetWidth = 200;
                    targetHeight = 2;
                }
                else // Music State
                {
                    view = new DesignBMusicView();
                    targetWidth = 560;
                    targetHeight = _viewModel.ShowProgressBar ? 120 : 90;
                    if (_viewModel.ShowLyrics) targetHeight += 24;
                }
            }

            if (!isIdle)
            {
                if (_viewModel.DisableExpandOnFullscreen && FullscreenDetector.IsFullscreenAppActive(this))
                {
                    // Block expansion: maintain the collapsed state and do not show music view
                    IslandHost.Content = null;
                    AnimateSize(200, 2, true);
                    return;
                }

                // Expand: immediately set content and fade in
                IslandHost.Content = view;
                AnimateSize(targetWidth, targetHeight, false);
                if (_viewModel.DisableExpandOnFullscreen)
                {
                    _fullscreenRecheckTimer.Start();
                }
            }
            else
            {
                // Shrink: collapse first, fade out, and set content to null when complete
                AnimateSize(targetWidth, targetHeight, true);
            }
        }

        private void FullscreenRecheckTimer_Tick(object? sender, EventArgs e)
        {
            if (_viewModel.CurrentState != IslandState.Idle &&
                _viewModel.DisableExpandOnFullscreen &&
                FullscreenDetector.IsFullscreenAppActive(this))
            {
                UpdateView();
            }
        }

        private void AnimateSize(double targetWidth, double targetHeight, bool isClosing)
        {
            var duration = TimeSpan.FromMilliseconds(400);
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

            var widthAnim = new DoubleAnimation(targetWidth, duration) { EasingFunction = easing };
            var heightAnim = new DoubleAnimation(targetHeight, duration) { EasingFunction = easing };
            var opacityAnim = new DoubleAnimation(isClosing ? 0.0 : 1.0, duration) { EasingFunction = easing };

            if (isClosing)
            {
                widthAnim.Completed += (s, e) =>
                {
                    if (_viewModel.CurrentState == IslandState.Idle)
                    {
                        IslandHost.Content = null;
                    }
                };
            }
            else
            {
                IslandHost.Opacity = 0.0;
            }

            HudBorder.BeginAnimation(WidthProperty, widthAnim);
            HudBorder.BeginAnimation(HeightProperty, heightAnim);
            IslandHost.BeginAnimation(OpacityProperty, opacityAnim);
        }

        private void HudBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            if (_isClosingApp || _viewModel.IsPositionEditMode) return;
            _viewModel.CurrentState = IslandState.Music;
        }

        private void HudBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isClosingApp || _viewModel.IsPositionEditMode) return;
            _viewModel.CurrentState = IslandState.Idle;
        }

        private bool _isDragging = false;
        private double _dragStartLeft;
        private double _dragStartMouseX;

        private void HudBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.IsPositionEditMode && e.LeftButton == MouseButtonState.Pressed)
            {
                _isDragging = true;
                _dragStartLeft = this.Left;
                _dragStartMouseX = GetMouseScreenPositionInDips(e).X;
                HudBorder.CaptureMouse();
            }
        }

        private void HudBorder_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _viewModel.IsPositionEditMode)
            {
                var currentMousePosition = GetMouseScreenPositionInDips(e);
                var currentMouseX = currentMousePosition.X;
                var diffX = currentMouseX - _dragStartMouseX;
                this.Left = _dragStartLeft + diffX;

                var screen = GetScreenFromDips(currentMousePosition);
                this.Top = GetScreenBoundsInDips(screen).Top;
            }
        }

        private void HudBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                HudBorder.ReleaseMouseCapture();

                var screen = GetScreenFromDips(GetMouseScreenPositionInDips(e));
                this.Top = GetScreenBoundsInDips(screen).Top;

                _viewModel.WindowLeft = this.Left;
                _viewModel.WindowTop = this.Top;
                _viewModel.HasCustomPosition = true;
            }
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            OpenSettings();
        }

        private void CloseMenu_Click(object sender, RoutedEventArgs e)
        {
            if (_isClosingApp) return;
            _isClosingApp = true;

            _updateCheckCts?.Cancel();

            // Prevent interaction during exit transition
            HudBorder.IsHitTestVisible = false;

            var duration = TimeSpan.FromMilliseconds(400);
            var easing = new CubicEase { EasingMode = EasingMode.EaseIn };

            var slideAnim = new DoubleAnimation(-150, duration) { EasingFunction = easing };
            var opacityAnim = new DoubleAnimation(0.0, duration) { EasingFunction = easing };

            slideAnim.Completed += (s, ev) =>
            {
                this.Close();
            };

            HudTransform.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            HudBorder.BeginAnimation(OpacityProperty, opacityAnim);
        }

        protected override void OnClosed(EventArgs e)
        {
            _fullscreenRecheckTimer.Stop();
            _viewModel.Music.Cleanup();

            if (_settingsWindow != null)
            {
                _settingsWindow.ForceClose();
            }

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            base.OnClosed(e);
        }
    }
}
