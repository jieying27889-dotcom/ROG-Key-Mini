using RogKeyMini.Config;
using RogKeyMini.Logging;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace RogKeyMini.Tray;

public sealed class TrayService : IDisposable
{
    private readonly ConfigService _configService;
    private readonly LogService _logService;
    private readonly SynchronizationContext? _syncContext;
    private NotifyIcon? _notifyIcon;

    public TrayService(LogService logService, ConfigService configService)
    {
        _logService = logService;
        _configService = configService;
        _syncContext = SynchronizationContext.Current;
    }

    public event EventHandler? ShowHideRequested;

    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        var autoStartItem = new ToolStripMenuItem("开机自启")
        {
            CheckOnClick = true,
            Checked = AutoStartHelper.IsEnabled()
        };
        autoStartItem.CheckedChanged += (s, e) =>
        {
            AutoStartHelper.SetEnabled(autoStartItem.Checked);
            _logService.Info($"AutoStart state changed to: {autoStartItem.Checked}");
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("显示 / 隐藏悬浮窗", null, (_, _) => ShowHideRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("设置", null, (_, _) => OpenConfigFile());
        menu.Items.Add("查看日志", null, (_, _) => OpenLogFile());
        menu.Items.Add(autoStartItem);
        menu.Items.Add("退出", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        Icon? trayIcon = null;
        try
        {
            using (var currentProcess = Process.GetCurrentProcess())
            {
                var mainModule = currentProcess.MainModule;
                if (mainModule?.FileName != null && File.Exists(mainModule.FileName))
                {
                    trayIcon = Icon.ExtractAssociatedIcon(mainModule.FileName);
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to extract application icon for tray.", ex);
        }

        _notifyIcon = new NotifyIcon
        {
            Text = "RogKeyMini",
            Icon = trayIcon ?? SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => ShowHideRequested?.Invoke(this, EventArgs.Empty);
        _logService.Info("Tray initialized.");
    }

    public void Dispose()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }

    private void OpenLogFile()
    {
        try
        {
            if (!File.Exists(_logService.LogPath))
            {
                File.WriteAllText(_logService.LogPath, string.Empty);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = _logService.LogPath,
                UseShellExecute = true
            });

            _logService.Info("Opened log file from tray.");
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to open log file from tray.", ex);
        }
    }

    private void OpenConfigFile()
    {
        try
        {
            var configPath = _configService.ConfigPath;
            if (!File.Exists(configPath))
            {
                var config = _configService.Load(_logService);
                _configService.Save(config, _logService);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = configPath,
                UseShellExecute = true
            });

            _logService.Info("Opened config file from tray.");
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to open config file from tray.", ex);
        }
    }

    public void ShowNotification(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (_syncContext is not null && SynchronizationContext.Current != _syncContext)
        {
            _syncContext.Post(_ => ShowNotificationCore(title, text, icon), null);
            return;
        }

        ShowNotificationCore(title, text, icon);
    }

    private void ShowNotificationCore(string title, string text, ToolTipIcon icon)
    {
        if (_notifyIcon is not null && _notifyIcon.Visible)
        {
            _notifyIcon.ShowBalloonTip(3000, title, text, icon);
        }
    }
}
