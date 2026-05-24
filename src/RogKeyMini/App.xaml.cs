using RogKeyMini.Asus;
using RogKeyMini.Config;
using RogKeyMini.Hotkeys;
using RogKeyMini.Input;
using RogKeyMini.Logging;
using RogKeyMini.Tray;
using RogKeyMini.UI;
using System.Windows;
using System.Windows.Threading;

namespace RogKeyMini;

public partial class App : System.Windows.Application
{
    private LogService? _logService;
    private ConfigService? _configService;
    private AppConfig? _config;
    private MainWindow? _mainWindow;
    private TrayService? _trayService;
    private GlobalHotkeyService? _hotkeyService;
    private AsusAcpiService? _asusAcpiService;
    private AltKeyMonitorService? _altKeyMonitor;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _logService = new LogService();
        HookGlobalExceptionLogging();
        _configService = new ConfigService();
        _config = _configService.Load(_logService);

        _logService.Info("Application starting.");

        var floatingWindowService = new FloatingWindowService(_logService);
        var keySender = new KeySender(_logService);
        _asusAcpiService = new AsusAcpiService(_logService);
        var screenBrightnessService = new ScreenBrightnessService(_logService);
        var keyboardBacklightService = new KeyboardBacklightService(_asusAcpiService, _config, _configService, _logService);

        _mainWindow = new MainWindow(
            _config,
            _configService,
            _logService,
            floatingWindowService,
            keySender,
            screenBrightnessService,
            keyboardBacklightService);

        _hotkeyService = new GlobalHotkeyService(_logService);
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        _hotkeyService.RegisterDefaults(_mainWindow, _config.Hotkeys);

        _trayService = new TrayService(_logService, _configService);
        _trayService.ShowHideRequested += (_, _) => _mainWindow.ToggleVisibility();
        _trayService.ExitRequested += (_, _) => ExitApplication();
        _trayService.Initialize();

        if (_config is not null && _trayService is not null)
        {
            _altKeyMonitor = new AltKeyMonitorService(_logService, keySender, _trayService, _config.AltMonitor);
            _altKeyMonitor.Start();
        }

        _mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logService?.Info("Application exiting.");
        _hotkeyService?.Dispose();
        _trayService?.Dispose();
        _asusAcpiService?.Dispose();
        _altKeyMonitor?.Dispose();

        if (_configService is not null && _config is not null && _logService is not null)
        {
            _configService.SaveRuntimeState(_config, _logService);
        }

        base.OnExit(e);
    }

    private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        if (_mainWindow is null)
        {
            return;
        }

        switch (e.Action)
        {
            case HotkeyAction.SendF2:
                _mainWindow.SendF2();
                break;
            case HotkeyAction.SendF7:
                _mainWindow.SendF7();
                break;
            case HotkeyAction.SendMinus:
                _mainWindow.SendMinus((ushort)e.Key);
                break;
            case HotkeyAction.SendUnderscore:
                _mainWindow.SendUnderscore((ushort)e.Key);
                break;
            case HotkeyAction.KeyboardBacklightDown:
                _mainWindow.DecreaseKeyboardBacklight();
                break;
            case HotkeyAction.ScreenBrightnessDown:
                _mainWindow.DecreaseScreenBrightness();
                break;
            case HotkeyAction.ToggleWindow:
                _mainWindow.ToggleVisibility();
                break;
        }
    }

    private void ExitApplication()
    {
        if (_mainWindow is not null)
        {
            _mainWindow.AllowClose = true;
            _mainWindow.Close();
        }

        Shutdown();
    }

    private void HookGlobalExceptionLogging()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logService?.Error("Dispatcher unhandled exception.", e.Exception);
    }

    private void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _logService?.Error($"AppDomain unhandled exception. IsTerminating={e.IsTerminating}.", exception);
            return;
        }

        _logService?.Error($"AppDomain unhandled non-exception object. IsTerminating={e.IsTerminating}.");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logService?.Error("TaskScheduler unobserved task exception.", e.Exception);
        e.SetObserved();
    }
}
