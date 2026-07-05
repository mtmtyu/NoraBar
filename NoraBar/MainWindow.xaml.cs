using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        private readonly MainViewModel _viewModel;
        private bool _isClosingApp = false;
        private Views.SettingsWindow? _settingsWindow;
        private NotifyIcon? _notifyIcon;
        private ToolStripMenuItem? _settingsTrayMenuItem;
        private ToolStripMenuItem? _exitTrayMenuItem;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            this.DataContext = _viewModel;

            // Position window at the top center of the screen
            this.Left = (SystemParameters.PrimaryScreenWidth - this.Width) / 2;
            this.Top = 0;

            // Initialize visual state to Idle
            _viewModel.CurrentState = IslandState.Idle;
            UpdateView();

            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Initialize system tray resident features
            InitializeSystemTray();
            UpdateLocalizedShellText();
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            // Open settings window if launched normally (not via startup)
            string[] args = Environment.GetCommandLineArgs();
            if (!args.Contains("--startup"))
            {
                OpenSettings();
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
                e.PropertyName == nameof(MainViewModel.ShowProgressBar))
            {
                Dispatcher.Invoke(UpdateView);
            }
            else if (e.PropertyName == nameof(MainViewModel.SelectedLanguage))
            {
                Dispatcher.Invoke(UpdateLocalizedShellText);
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
                    targetHeight = _viewModel.ShowProgressBar ? 96 : 80;
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
                }
            }

            if (!isIdle)
            {
                // Expand: immediately set content and fade in
                IslandHost.Content = view;
                AnimateSize(targetWidth, targetHeight, false);
            }
            else
            {
                // Shrink: collapse first, fade out, and set content to null when complete
                AnimateSize(targetWidth, targetHeight, true);
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
            if (_isClosingApp) return;
            _viewModel.CurrentState = IslandState.Music;
        }

        private void HudBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isClosingApp) return;
            _viewModel.CurrentState = IslandState.Idle;
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            OpenSettings();
        }

        private void CloseMenu_Click(object sender, RoutedEventArgs e)
        {
            if (_isClosingApp) return;
            _isClosingApp = true;

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
