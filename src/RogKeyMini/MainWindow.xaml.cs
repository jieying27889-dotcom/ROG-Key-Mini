using RogKeyMini.Asus;
using RogKeyMini.Config;
using RogKeyMini.Input;
using RogKeyMini.Logging;
using RogKeyMini.UI;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using WpfButton = System.Windows.Controls.Button;
using WpfPoint = System.Windows.Point;

namespace RogKeyMini;

public partial class MainWindow : Window
{
    private const int ButtonsPerRow = 5;
    private const double WindowWidthPixels = 520;
    private const double WindowBaseHeightPixels = 92;
    private const double WindowRowHeightPixels = 62;

    private readonly AppConfig _config;
    private readonly ConfigService _configService;
    private readonly LogService _logService;
    private readonly FloatingWindowService _floatingWindowService;
    private readonly KeySender _keySender;
    private readonly ScreenBrightnessService _screenBrightnessService;
    private readonly KeyboardBacklightService _keyboardBacklightService;

    private WpfButton? _autoReleaseButton;
    private double _windowHeight;
    private double _windowWidth;
    private double _windowLeft;
    private double _windowTop;

    public MainWindow(
        AppConfig config,
        ConfigService configService,
        LogService logService,
        FloatingWindowService floatingWindowService,
        KeySender keySender,
        ScreenBrightnessService screenBrightnessService,
        KeyboardBacklightService keyboardBacklightService)
    {
        _config = config;
        _configService = configService;
        _logService = logService;
        _floatingWindowService = floatingWindowService;
        _keySender = keySender;
        _screenBrightnessService = screenBrightnessService;
        _keyboardBacklightService = keyboardBacklightService;

        InitializeComponent();

        _floatingWindowService.Attach(this, _config.Window);
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        Loaded += OnLoaded;

        BuildButtons();
        UpdateAutoReleaseButtonVisual();
    }

    public bool AllowClose { get; set; }

    public void SendF2() => _keySender.SendF2();

    public void SendF7() => _keySender.SendF7();

    public void SendMinus() => _keySender.SendMinus();

    public void SendMinus(ushort triggerKey) => _keySender.SendMinus(triggerKey);

    public void SendUnderscore() => _keySender.SendUnderscore();

    public void SendUnderscore(ushort triggerKey) => _keySender.SendUnderscore(triggerKey);

    public void DecreaseScreenBrightness()
    {
        _screenBrightnessService.TryDecrease(_config.Brightness.ScreenStep, _config.Brightness.ScreenMin);
    }

    public void DecreaseKeyboardBacklight()
    {
        _keyboardBacklightService.TryDecrease();
    }

    public void ExecuteConfiguredButton(PanelButtonConfig buttonConfig)
    {
        ExecuteButtonAction(buttonConfig);
    }

    public void ToggleVisibility()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        Show();
        ActivateWindowFromConfig();
    }

    public void ReloadFromConfig()
    {
        Topmost = _config.Window.Topmost;
        Opacity = _config.Window.Opacity;
        BuildButtons();
        UpdateAutoReleaseButtonVisual();
        ApplyWindowPosition(ClampWindowPosition(new WpfPoint(_config.Window.Left, _config.Window.Top)));
    }

    public void ResetToPrimaryScreenCenter()
    {
        var screen = Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];
        var workArea = screen.WorkingArea;
        var targetLeft = workArea.Left + Math.Max(0, (workArea.Width - _windowWidth) / 2d);
        var targetTop = workArea.Top + Math.Max(0, (workArea.Height - _windowHeight) / 2d);

        if (!IsVisible)
        {
            Show();
        }

        ApplyWindowPosition(new WpfPoint(targetLeft, targetTop));
        PersistWindowRuntimeState();
        Activate();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        PersistWindowRuntimeState();
        _configService.SaveRuntimeState(_config, _logService);

        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
            _logService.Info("Main window hidden to tray.");
        }

        base.OnClosing(e);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyWindowPosition(ClampWindowPosition(_windowLeft == 0 && _windowTop == 0
            ? new WpfPoint(Left, Top)
            : new WpfPoint(_windowLeft, _windowTop)));
    }

    private void BuildButtons()
    {
        ButtonsHost.Children.Clear();
        _autoReleaseButton = null;

        var buttons = _config.Panel.Buttons ?? PanelButtonConfig.CreateDefaults();
        foreach (var buttonConfig in buttons)
        {
            var button = new WpfButton
            {
                Style = (Style)FindResource("RogButtonStyle"),
                Tag = buttonConfig,
                ToolTip = BuildButtonToolTip(buttonConfig),
                Content = CreateButtonContent(buttonConfig.Label)
            };
            button.Click += DynamicButton_OnClick;

            if (NormalizeAction(buttonConfig.Action) == PanelButtonAction.ToggleAutoRelease)
            {
                _autoReleaseButton = button;
            }

            ButtonsHost.Children.Add(button);
        }

        var rows = Math.Max(1, (int)Math.Ceiling(buttons.Count / (double)ButtonsPerRow));
        ButtonsHost.Rows = rows;
        _windowWidth = WindowWidthPixels;
        _windowHeight = WindowBaseHeightPixels + rows * WindowRowHeightPixels;
        Width = _windowWidth;
        Height = _windowHeight;

        _windowLeft = Left;
        _windowTop = Top;
    }

    private static object CreateButtonContent(string label)
    {
        return new TextBlock
        {
            Text = label,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        };
    }

    private static string BuildButtonToolTip(PanelButtonConfig buttonConfig)
    {
        return string.Equals(buttonConfig.Action, "SendKey", StringComparison.OrdinalIgnoreCase)
            ? $"模拟键位：{buttonConfig.Gesture}"
            : $"动作：{buttonConfig.Action}";
    }

    private void DynamicButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton { Tag: PanelButtonConfig buttonConfig })
        {
            return;
        }

        ExecuteButtonAction(buttonConfig);
    }

    private void ExecuteButtonAction(PanelButtonConfig buttonConfig)
    {
        switch (NormalizeAction(buttonConfig.Action))
        {
            case PanelButtonAction.SendKey:
                if (string.IsNullOrWhiteSpace(buttonConfig.Gesture))
                {
                    _logService.Warn($"按钮 {buttonConfig.Label} 未配置 Gesture。");
                    return;
                }

                _keySender.SendGesture(buttonConfig.Gesture, buttonConfig.Label);
                break;
            case PanelButtonAction.KeyboardBacklightDown:
                DecreaseKeyboardBacklight();
                break;
            case PanelButtonAction.ScreenBrightnessDown:
                DecreaseScreenBrightness();
                break;
            case PanelButtonAction.LaunchOsk:
                LaunchOsk();
                break;
            case PanelButtonAction.ToggleAutoRelease:
                ToggleAutoRelease();
                break;
            default:
                _logService.Warn($"未知按钮动作：{buttonConfig.Action}。");
                break;
        }
    }

    private void LaunchOsk()
    {
        try
        {
            string windowsDirectory = Environment.GetEnvironmentVariable("windir") ?? @"C:\Windows";
            string sysnativePath = System.IO.Path.Combine(windowsDirectory, "Sysnative", "osk.exe");
            string system32Path = System.IO.Path.Combine(windowsDirectory, "System32", "osk.exe");
            string oskPath = System.IO.File.Exists(sysnativePath) ? sysnativePath : system32Path;

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = oskPath,
                UseShellExecute = true
            });
            _logService.Info("已从界面启动屏幕键盘(OSK)。");
        }
        catch (Exception ex)
        {
            _logService.Error("从界面启动屏幕键盘(OSK)失败。", ex);
        }
    }

    private void ToggleAutoRelease()
    {
        _config.AltMonitor.AutoReleaseEnabled = !_config.AltMonitor.AutoReleaseEnabled;
        UpdateAutoReleaseButtonVisual();
        _configService.Save(_config, _logService);
        _logService.Info($"通过界面切换了自动释放状态。当前状态：{_config.AltMonitor.AutoReleaseEnabled}");
    }

    private void UpdateAutoReleaseButtonVisual()
    {
        if (_autoReleaseButton is null)
        {
            return;
        }

        _autoReleaseButton.Foreground = _config.AltMonitor.AutoReleaseEnabled
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 46, 80))
            : new SolidColorBrush(System.Windows.Media.Color.FromRgb(170, 170, 170));
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        finally
        {
            CaptureWindowPosition();
            ApplyWindowPosition(ClampWindowPosition(new WpfPoint(_windowLeft, _windowTop)));
            PersistWindowRuntimeState();
        }
    }

    private void ActivateWindowFromConfig()
    {
        ApplyWindowPosition(ClampWindowPosition(new WpfPoint(_config.Window.Left, _config.Window.Top)));
        Activate();
    }

    private void CaptureWindowPosition()
    {
        _windowLeft = Left;
        _windowTop = Top;
    }

    private void ApplyWindowPosition(WpfPoint point)
    {
        Width = _windowWidth;
        Height = _windowHeight;
        Left = point.X;
        Top = point.Y;
        _windowLeft = point.X;
        _windowTop = point.Y;
    }

    private WpfPoint ClampWindowPosition(WpfPoint point)
    {
        var workArea = GetCurrentWorkArea();
        var maxLeft = Math.Max(workArea.Left, workArea.Right - _windowWidth);
        var maxTop = Math.Max(workArea.Top, workArea.Bottom - _windowHeight);
        var safeLeft = Math.Clamp(point.X, workArea.Left, maxLeft);
        var safeTop = Math.Clamp(point.Y, workArea.Top, maxTop);
        return new WpfPoint(safeLeft, safeTop);
    }

    private Rect GetCurrentWorkArea()
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var screen = handle == IntPtr.Zero
            ? (Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0])
            : Forms.Screen.FromHandle(handle);
        var workingArea = screen.WorkingArea;
        return new Rect(workingArea.Left, workingArea.Top, workingArea.Width, workingArea.Height);
    }

    private void PersistWindowRuntimeState()
    {
        _config.Window.Left = _windowLeft;
        _config.Window.Top = _windowTop;
        _config.Window.AutoHideEnabled = false;
    }

    private static PanelButtonAction NormalizeAction(string? action)
    {
        return action?.Trim().ToUpperInvariant() switch
        {
            "SENDKEY" => PanelButtonAction.SendKey,
            "KEYBOARDBACKLIGHTDOWN" => PanelButtonAction.KeyboardBacklightDown,
            "SCREENBRIGHTNESSDOWN" => PanelButtonAction.ScreenBrightnessDown,
            "LAUNCHOSK" => PanelButtonAction.LaunchOsk,
            "TOGGLEAUTORELEASE" => PanelButtonAction.ToggleAutoRelease,
            _ => PanelButtonAction.Unknown
        };
    }

}

internal enum PanelButtonAction
{
    Unknown,
    SendKey,
    KeyboardBacklightDown,
    ScreenBrightnessDown,
    LaunchOsk,
    ToggleAutoRelease
}
