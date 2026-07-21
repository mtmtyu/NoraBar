using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NoraBar.Hud;
using NoraBar.Services;
using NoraBar.ViewModels;
using NoraBar.Models;

using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using ToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;
using ToolStripSeparator = System.Windows.Forms.ToolStripSeparator;

namespace NoraBar;

public partial class MainWindow : Window
{
    private const double CollapsedWidth = 200;
    private const double CollapsedHeight = 2;
    private const int AnimationDurationMilliseconds = 400;
    private const double ExitOffset = -150;

    private readonly MainViewModel _viewModel;
    private readonly HudRouter _hudRouter;
    private readonly Func<Task> _requestShutdownAsync;
    private Views.SettingsWindow? _settingsWindow;
    private NotifyIcon? _notifyIcon;
    private ToolStripMenuItem? _settingsTrayMenuItem;
    private ToolStripMenuItem? _exitTrayMenuItem;
    private bool _allowClose;
    private bool _isDragging;
    private double _dragStartLeft;
    private double _dragStartMouseX;
    private int _shutdownRequested;
    private int _shutdownForwarded;
    private int _hudRouterDetached;
    private int _shellResourcesReleased;
    private int _presentationRevision;

    public MainWindow(
        MainViewModel viewModel,
        HudRouter hudRouter,
        Func<Task> requestShutdownAsync)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(hudRouter);
        ArgumentNullException.ThrowIfNull(requestShutdownAsync);

        _viewModel = viewModel;
        _hudRouter = hudRouter;
        _requestShutdownAsync = requestShutdownAsync;

        InitializeComponent();
        DataContext = _viewModel;

        _hudRouter.StateChanged += HudRouter_PresentationChanged;
        _hudRouter.PresentationChanged += HudRouter_PresentationChanged;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        ApplyWindowPosition();
        InitializeSystemTray();
        UpdateLocalizedShellText();
    }

    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        string[] args = Environment.GetCommandLineArgs();
        if (!args.Contains("--startup"))
        {
            OpenSettings();
        }

        if (_viewModel.CheckUpdateOnStartup)
        {
            bool hasUpdate = await _viewModel.CheckForUpdatesSilentlyAsync();
            if (hasUpdate)
            {
                OpenSettings();
            }
        }
    }

    internal void RefreshHudPresentation()
    {
        if (IsShutdownRequested)
        {
            return;
        }

        HudRouterSnapshot snapshot = _hudRouter.GetSnapshot();
        if (!snapshot.IsInitialized
            || snapshot.IsShuttingDown
            || snapshot.CurrentModule is null)
        {
            AnimateSize(CollapsedWidth, CollapsedHeight, collapseContent: true);
            return;
        }

        if (snapshot.PresentationState == HudPresentationState.Collapsed)
        {
            AnimateSize(CollapsedWidth, CollapsedHeight, collapseContent: true);
            return;
        }

        bool suppressExpansion = _viewModel.DisableExpandOnFullscreen
            && FullscreenDetector.IsFullscreenAppActive(this);
        if (!HudPresentationEvaluator.TryEvaluate(
                snapshot,
                suppressExpansion,
                out HudPresentationEvaluation evaluation))
        {
            IslandHost.Content = null;
            AnimateSize(CollapsedWidth, CollapsedHeight, collapseContent: true);
            return;
        }

        IslandHost.Content = evaluation.View;
        AnimateSize(
            GetPresentationWidth(evaluation.PreferredSize.Width),
            GetPresentationHeight(evaluation.PreferredSize.Height),
            collapseContent: false);
    }

    internal void DetachHudRouter()
    {
        if (Interlocked.Exchange(ref _hudRouterDetached, 1) != 0)
        {
            return;
        }

        _hudRouter.StateChanged -= HudRouter_PresentationChanged;
        _hudRouter.PresentationChanged -= HudRouter_PresentationChanged;
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
    }

    internal void ReleaseShellResources()
    {
        if (Interlocked.Exchange(ref _shellResourcesReleased, 1) != 0)
        {
            return;
        }

        Views.SettingsWindow? settingsWindow = _settingsWindow;
        NotifyIcon? notifyIcon = _notifyIcon;
        _settingsWindow = null;
        _notifyIcon = null;

        var releaseOperations = new List<Action>();
        if (settingsWindow is not null)
        {
            releaseOperations.Add(settingsWindow.ForceClose);
        }

        if (notifyIcon is not null)
        {
            releaseOperations.Add(() => notifyIcon.Visible = false);
            releaseOperations.Add(notifyIcon.Dispose);
        }

        BestEffortResourceReleaser.ReleaseAll(releaseOperations.ToArray());
    }

    internal void AllowClose()
    {
        _allowClose = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            RequestShutdownFromWindow();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        DetachHudRouter();
        ReleaseShellResources();
        base.OnClosed(e);
    }

    internal bool IsShutdownRequested => Volatile.Read(ref _shutdownRequested) != 0;

    private void HudRouter_PresentationChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.CheckAccess())
        {
            RefreshHudPresentation();
            return;
        }

        Dispatcher.BeginInvoke(RefreshHudPresentation);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedLanguage))
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
        else if (e.PropertyName == nameof(MainViewModel.HudNavigationPlacement))
        {
            Dispatcher.Invoke(RefreshHudPresentation);
        }
        else
        {
            _ = HudPresentationRefreshScheduler.TrySchedule(
                e.PropertyName,
                callback =>
                {
                    Dispatcher.BeginInvoke(callback);
                },
                RefreshHudPresentation);
        }
    }

    private void ApplyWindowPosition()
    {
        if (_viewModel.HasCustomPosition)
        {
            Left = _viewModel.WindowLeft;
            Point deviceTopLeft = DipsToDevice(
                new Point(_viewModel.WindowLeft, _viewModel.WindowTop));
            Point deviceSize = DipsToDevice(new Point(Width, Height));
            var rect = new System.Drawing.Rectangle(
                (int)deviceTopLeft.X,
                (int)deviceTopLeft.Y,
                (int)deviceSize.X,
                (int)deviceSize.Y);
            System.Windows.Forms.Screen screen =
                System.Windows.Forms.Screen.FromRectangle(rect);
            Top = GetScreenBoundsInDips(screen).Top;
            return;
        }

        System.Windows.Forms.Screen? primaryScreen =
            System.Windows.Forms.Screen.PrimaryScreen;
        if (primaryScreen is null)
        {
            return;
        }

        Rect bounds = GetScreenBoundsInDips(primaryScreen);
        Left = bounds.Left + (bounds.Width - Width) / 2;
        Top = bounds.Top;
    }

    private Point DipsToDevice(Point point)
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        return new Point(point.X * dpi.DpiScaleX, point.Y * dpi.DpiScaleY);
    }

    private Point DeviceToDips(Point point)
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        return new Point(point.X / dpi.DpiScaleX, point.Y / dpi.DpiScaleY);
    }

    private Rect GetScreenBoundsInDips(System.Windows.Forms.Screen screen)
    {
        Point topLeft = DeviceToDips(
            new Point(screen.Bounds.Left, screen.Bounds.Top));
        Point bottomRight = DeviceToDips(
            new Point(screen.Bounds.Right, screen.Bounds.Bottom));
        return new Rect(topLeft, bottomRight);
    }

    private System.Windows.Forms.Screen GetScreenFromDips(Point point)
    {
        Point devicePoint = DipsToDevice(point);
        return System.Windows.Forms.Screen.FromPoint(
            new System.Drawing.Point((int)devicePoint.X, (int)devicePoint.Y));
    }

    private Point GetMouseScreenPositionInDips(MouseEventArgs e)
    {
        return DeviceToDips(PointToScreen(e.GetPosition(this)));
    }

    private void UpdateEditModeVisuals()
    {
        if (_viewModel.IsPositionEditMode)
        {
            HudBorder.Background = new SolidColorBrush(
                Color.FromArgb(0x40, 0x00, 0xFF, 0x00));
            HudBorder.Cursor = Cursors.SizeAll;
            _hudRouter.SetPresentationState(HudPresentationState.Expanded);
            return;
        }

        HudBorder.Background = new SolidColorBrush(
            Color.FromArgb(0x01, 0xFF, 0xFF, 0xFF));
        HudBorder.Cursor = Cursors.Arrow;
        if (!HudBorder.IsMouseOver)
        {
            _hudRouter.CollapseFromPointerLeave();
        }
    }

    private void InitializeSystemTray()
    {
        _notifyIcon = new NotifyIcon();

        try
        {
            string? appPath = Environment.ProcessPath
                ?? Assembly.GetExecutingAssembly().Location;
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
        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(OpenSettings);

        var contextMenu = new ContextMenuStrip();
        _settingsTrayMenuItem = new ToolStripMenuItem();
        _settingsTrayMenuItem.Click += (_, _) => Dispatcher.Invoke(OpenSettings);
        contextMenu.Items.Add(_settingsTrayMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());

        _exitTrayMenuItem = new ToolStripMenuItem();
        _exitTrayMenuItem.Click += (_, _) => Dispatcher.Invoke(RequestShutdownFromWindow);
        contextMenu.Items.Add(_exitTrayMenuItem);
        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    private void OpenSettings()
    {
        if (IsShutdownRequested)
        {
            return;
        }

        if (_settingsWindow is not null)
        {
            _settingsWindow.ShowWindow();
            return;
        }

        _settingsWindow = new Views.SettingsWindow
        {
            DataContext = _viewModel
        };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.ShowWindow();
    }

    private void UpdateLocalizedShellText()
    {
        string settingsText = LocalizationService.GetText(
            _viewModel.SelectedLanguage,
            LocalizationKey.SettingsMenu);
        string exitText = LocalizationService.GetText(
            _viewModel.SelectedLanguage,
            LocalizationKey.Exit);

        SettingsContextMenuItem.Header = settingsText;
        ExitContextMenuItem.Header = exitText;

        if (_settingsTrayMenuItem is not null)
        {
            _settingsTrayMenuItem.Text = settingsText;
        }

        if (_exitTrayMenuItem is not null)
        {
            _exitTrayMenuItem.Text = exitText;
        }
    }

    private void AnimateSize(
        double targetWidth,
        double targetHeight,
        bool collapseContent)
    {
        int revision = Interlocked.Increment(ref _presentationRevision);
        var duration = TimeSpan.FromMilliseconds(AnimationDurationMilliseconds);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var widthAnimation = new DoubleAnimation(targetWidth, duration)
        {
            EasingFunction = easing
        };
        var heightAnimation = new DoubleAnimation(targetHeight, duration)
        {
            EasingFunction = easing
        };
        var opacityAnimation = new DoubleAnimation(
            collapseContent ? 0.0 : 1.0,
            duration)
        {
            EasingFunction = easing
        };

        if (collapseContent)
        {
            widthAnimation.Completed += (_, _) =>
            {
                if (revision == Volatile.Read(ref _presentationRevision))
                {
                    IslandHost.Content = null;
                }
            };
        }
        else
        {
            HudPresentationHost.Opacity = 0.0;
        }

        HudBorder.BeginAnimation(WidthProperty, widthAnimation);
        HudBorder.BeginAnimation(HeightProperty, heightAnimation);
        HudPresentationHost.BeginAnimation(OpacityProperty, opacityAnimation);
    }

    private void HudBorder_MouseEnter(object sender, MouseEventArgs e)
    {
        if (IsShutdownRequested || _viewModel.IsPositionEditMode)
        {
            return;
        }

        _hudRouter.SetPresentationState(HudPresentationState.Expanded);
    }

    private void HudBorder_MouseLeave(object sender, MouseEventArgs e)
    {
        if (IsShutdownRequested || _viewModel.IsPositionEditMode)
        {
            return;
        }

        _hudRouter.CollapseFromPointerLeave();
    }

    private void HudBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.IsPositionEditMode
            && e.LeftButton == MouseButtonState.Pressed)
        {
            _isDragging = true;
            _dragStartLeft = Left;
            _dragStartMouseX = GetMouseScreenPositionInDips(e).X;
            HudBorder.CaptureMouse();
        }
    }

    private void HudBorder_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || !_viewModel.IsPositionEditMode)
        {
            return;
        }

        Point currentMousePosition = GetMouseScreenPositionInDips(e);
        double difference = currentMousePosition.X - _dragStartMouseX;
        Left = _dragStartLeft + difference;

        System.Windows.Forms.Screen screen = GetScreenFromDips(currentMousePosition);
        Top = GetScreenBoundsInDips(screen).Top;
    }

    private void HudBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        HudBorder.ReleaseMouseCapture();

        System.Windows.Forms.Screen screen =
            GetScreenFromDips(GetMouseScreenPositionInDips(e));
        Top = GetScreenBoundsInDips(screen).Top;
        _viewModel.WindowLeft = Left;
        _viewModel.WindowTop = Top;
        _viewModel.HasCustomPosition = true;
    }

    private async void HudNavigation_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        HudNavigationViewModel? navigation = _viewModel.HudNavigation;
        if (navigation is null || e.Delta == 0)
        {
            return;
        }

        e.Handled = true;
        await navigation.NavigateRelativeAsync(e.Delta < 0 ? 1 : -1);
    }

    private double GetPresentationWidth(double moduleWidth)
    {
        HudNavigationViewModel? navigation = _viewModel.HudNavigation;
        return navigation is { ShowNavigation: true }
            && _viewModel.HudNavigationPlacement == HudNavigationPlacement.RightRail
                ? moduleWidth + 48
                : moduleWidth;
    }

    private double GetPresentationHeight(double moduleHeight)
    {
        HudNavigationViewModel? navigation = _viewModel.HudNavigation;
        return navigation is { ShowNavigation: true }
            && _viewModel.HudNavigationPlacement == HudNavigationPlacement.TopTabs
                ? moduleHeight + 40
                : moduleHeight;
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        OpenSettings();
    }

    private void CloseMenu_Click(object sender, RoutedEventArgs e)
    {
        RequestShutdownFromWindow();
    }

    private void RequestShutdownFromWindow()
    {
        if (Interlocked.Exchange(ref _shutdownRequested, 1) != 0)
        {
            return;
        }

        HudBorder.IsHitTestVisible = false;
        var duration = TimeSpan.FromMilliseconds(AnimationDurationMilliseconds);
        var easing = new CubicEase { EasingMode = EasingMode.EaseIn };
        var slideAnimation = new DoubleAnimation(ExitOffset, duration)
        {
            EasingFunction = easing
        };
        var opacityAnimation = new DoubleAnimation(0.0, duration)
        {
            EasingFunction = easing
        };

        slideAnimation.Completed += ShutdownAnimation_Completed;
        HudTransform.BeginAnimation(TranslateTransform.YProperty, slideAnimation);
        HudBorder.BeginAnimation(OpacityProperty, opacityAnimation);
    }

    private async void ShutdownAnimation_Completed(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _shutdownForwarded, 1) != 0)
        {
            return;
        }

        try
        {
            await _requestShutdownAsync();
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
            if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
            {
                MessageBox.Show(
                    $"NoraBarの終了処理中にエラーが発生しました。{Environment.NewLine}{exception.Message}",
                    "NoraBar",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
