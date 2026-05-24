using RogKeyMini.Asus;
using RogKeyMini.Config;
using RogKeyMini.Input;
using RogKeyMini.Logging;
using RogKeyMini.UI;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace RogKeyMini;

public partial class MainWindow : Window
{
    private readonly AppConfig _config;
    private readonly ConfigService _configService;
    private readonly LogService _logService;
    private readonly FloatingWindowService _floatingWindowService;
    private readonly KeySender _keySender;
    private readonly ScreenBrightnessService _screenBrightnessService;
    private readonly KeyboardBacklightService _keyboardBacklightService;

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

    public void ToggleVisibility()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _config.Window.Left = Left;
        _config.Window.Top = Top;
        _configService.SaveRuntimeState(_config, _logService);

        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
            _logService.Info("Main window hidden to tray.");
        }

        base.OnClosing(e);
    }

    private void F2Button_OnClick(object sender, RoutedEventArgs e) => SendF2();

    private void F7Button_OnClick(object sender, RoutedEventArgs e) => SendF7();

    private void MinusButton_OnClick(object sender, RoutedEventArgs e) => SendMinus();

    private void UnderscoreButton_OnClick(object sender, RoutedEventArgs e) => SendUnderscore();

    private void KeyboardBacklightButton_OnClick(object sender, RoutedEventArgs e) => DecreaseKeyboardBacklight();

    private void ScreenBrightnessButton_OnClick(object sender, RoutedEventArgs e) => DecreaseScreenBrightness();

    private void OskButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            string windir = Environment.GetEnvironmentVariable("windir") ?? @"C:\Windows";
            string sysnativePath = System.IO.Path.Combine(windir, "Sysnative", "osk.exe");
            string system32Path = System.IO.Path.Combine(windir, "System32", "osk.exe");
 
            string oskPath = System.IO.File.Exists(sysnativePath) ? sysnativePath : system32Path;
 
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = oskPath,
                UseShellExecute = true
            });
            _logService.Info("从界面成功启动屏幕键盘 (OSK)。");
        }
        catch (System.Exception ex)
        {
            _logService.Error("从界面启动屏幕键盘 (OSK) 失败。", ex);
        }
    }
 
    private void AutoReleaseButton_OnClick(object sender, RoutedEventArgs e)
    {
        _config.AltMonitor.AutoReleaseEnabled = !_config.AltMonitor.AutoReleaseEnabled;
        UpdateAutoReleaseButtonVisual();
        _logService.Info($"通过界面切换了自动释放状态。当前状态: {_config.AltMonitor.AutoReleaseEnabled}");
    }
 
    private void UpdateAutoReleaseButtonVisual()
    {
        if (_config.AltMonitor.AutoReleaseEnabled)
        {
            AutoReleaseBtn.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 46, 80)); // ROG红
        }
        else
        {
            AutoReleaseBtn.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(170, 170, 170)); // 浅灰
        }
    }
 
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
