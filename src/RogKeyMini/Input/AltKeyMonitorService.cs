using RogKeyMini.Config;
using RogKeyMini.Logging;
using RogKeyMini.Interop;
using RogKeyMini.Tray;
using System.Windows.Forms;

namespace RogKeyMini.Input;

public sealed class AltKeyMonitorService : IDisposable
{
    private readonly LogService _logService;
    private readonly KeySender _keySender;
    private readonly TrayService _trayService;
    private readonly AltMonitorConfig _config;

    private CancellationTokenSource? _cts;
    private Task? _monitorTask;

    public AltKeyMonitorService(
        LogService logService,
        KeySender keySender,
        TrayService trayService,
        AltMonitorConfig config)
    {
        _logService = logService;
        _keySender = keySender;
        _trayService = trayService;
        _config = config;
    }

    public void Start()
    {
        if (_monitorTask != null) return;
        
        // 如果左右 Alt 都不监控，就直接不开启线程
        if (!_config.MonitorLeftAlt && !_config.MonitorRightAlt)
        {
            _logService.Info("AltKeyMonitorService 已通过配置禁用（左右 Alt 键的监控均为 false）。");
            return;
        }

        _cts = new CancellationTokenSource();
        _monitorTask = Task.Run(() => MonitorLoop(_cts.Token));
        _logService.Info("AltKeyMonitorService 后台监控任务已启动。");
    }

    private async Task MonitorLoop(CancellationToken token)
    {
        const int checkIntervalMs = 100;
        int leftHeldMs = 0;
        int rightHeldMs = 0;

        while (!token.IsCancellationRequested)
        {
            if (_keySender.IsSending)
            {
                leftHeldMs = 0;
                rightHeldMs = 0;
            }
            else
            {
                try
                {
                    // 1. 监控左 Alt (VK_LMENU = 0xA4)
                    if (_config.MonitorLeftAlt)
                    {
                        bool leftPressed = (NativeMethods.GetAsyncKeyState(0xA4) & 0x8000) != 0;
                        if (leftPressed)
                        {
                            leftHeldMs += checkIntervalMs;
                            if (leftHeldMs >= _config.StuckThresholdMs)
                            {
                                HandleStuckKey(0xA4, "左 Alt");
                                leftHeldMs = 0; // 重置计时，防止高频报警
                            }
                        }
                        else
                        {
                            leftHeldMs = 0;
                        }
                    }
 
                    // 2. 监控右 Alt (VK_RMENU = 0xA5)
                    if (_config.MonitorRightAlt)
                    {
                        bool rightPressed = (NativeMethods.GetAsyncKeyState(0xA5) & 0x8000) != 0;
                        if (rightPressed)
                        {
                            rightHeldMs += checkIntervalMs;
                            if (rightHeldMs >= _config.StuckThresholdMs)
                            {
                                HandleStuckKey(0xA5, "右 Alt");
                                rightHeldMs = 0; // 重置计时，防止高频报警
                            }
                        }
                        else
                        {
                            rightHeldMs = 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logService.Error("AltKeyMonitorService 后台循环中出现异常。", ex);
                }
            }

            try
            {
                await Task.Delay(checkIntervalMs, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private void HandleStuckKey(ushort vkCode, string keyName)
    {
        _logService.Warn($"检测到 {keyName} 键被异常长按/卡住！");

        // 第一次通知：检测到卡住
        if (_config.NotificationsEnabled)
        {
            _trayService.ShowNotification(
                $"{keyName} 键状态异常",
                $"检测到 {keyName} 键处于异常按下（卡住）状态。",
                ToolTipIcon.Warning);
        }

        // 自动释放
        if (_config.AutoReleaseEnabled)
        {
            // 播放一个系统提示音
            try
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
            catch
            {
                // 忽略声音播放失败
            }

            // 发送 KeyUp 模拟
            _keySender.ReleaseKey(vkCode, keyName);

            // 第二次通知：自动释放完毕
            if (_config.NotificationsEnabled)
            {
                _trayService.ShowNotification(
                    $"{keyName} 已释放",
                    $"系统已自动模拟弹起事件，成功释放了 {keyName} 键。",
                    ToolTipIcon.Info);
            }
        }
    }

    public void Dispose()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            try
            {
                _monitorTask?.Wait(500);
            }
            catch
            {
                // 忽略等待线程结束的异常
            }
            _cts.Dispose();
            _cts = null;
        }

        _monitorTask?.Dispose();
        _monitorTask = null;
        _logService.Info("AltKeyMonitorService 后台监控已安全停止并销毁。");
    }
}
