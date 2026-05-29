using RogKeyMini.Config;
using RogKeyMini.Interop;
using RogKeyMini.Input;
using RogKeyMini.Logging;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;

namespace RogKeyMini.Hotkeys;

public sealed class GlobalHotkeyService : IDisposable
{
    private readonly LogService _logService;
    private readonly Dictionary<int, RegisteredHotkey> _actions = new();
    private HwndSource? _source;
    private IntPtr _windowHandle;

    private long _lastHotkeyTicks;
    private System.Timers.Timer? _pollTimer;

    public GlobalHotkeyService(LogService logService)
    {
        _logService = logService;
    }

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public void RegisterConfiguredHotkeys(Window window, HotkeysConfig hotkeysConfig, IReadOnlyList<PanelButtonConfig> buttons)
    {
        void RegisterAll()
        {
            if (_windowHandle != IntPtr.Zero)
            {
                return;
            }

            _windowHandle = new WindowInteropHelper(window).Handle;
            _source = HwndSource.FromHwnd(_windowHandle);
            _source?.AddHook(WndProc);

            Register(1, hotkeysConfig.ToggleWindow, HotkeyAction.ToggleWindow);

            int nextId = 100;
            foreach (var button in buttons)
            {
                Register(nextId++, button.TriggerHotkey ?? string.Empty, HotkeyAction.PanelButton, button);
            }

            StartPolling();
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero)
        {
            RegisterAll();
            return;
        }

        window.SourceInitialized += (_, _) => RegisterAll();
    }

    public void Dispose()
    {
        StopPolling();

        foreach (var hotkeyId in _actions.Keys.ToArray())
        {
            NativeMethods.UnregisterHotKey(_windowHandle, hotkeyId);
        }

        _source?.RemoveHook(WndProc);
        _actions.Clear();
    }

    private void Register(int id, string gesture, HotkeyAction action, PanelButtonConfig? buttonConfig = null)
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(gesture) || gesture.Equals("None", StringComparison.OrdinalIgnoreCase) || gesture.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
        {
            _logService.Info($"Hotkey action {action} is disabled (gesture is empty).");
            return;
        }

        if (!KeyGestureParser.TryParseForHotkey(gesture, out var modifiers, out var key))
        {
            _logService.Warn($"Hotkey gesture parse failed: {gesture}.");
            return;
        }

        if (!NativeMethods.RegisterHotKey(_windowHandle, id, modifiers, key))
        {
            var errorCode = Marshal.GetLastWin32Error();
            var errorMessage = new Win32Exception(errorCode).Message;
            _logService.Warn($"RegisterHotKey failed for {gesture}. Win32={errorCode}, {errorMessage}");
            return;
        }

        _actions[id] = new RegisteredHotkey(action, gesture, modifiers, key, buttonConfig);
        _logService.Info($"Registered hotkey {gesture}.");
    }

    private void StartPolling()
    {
        _pollTimer = new System.Timers.Timer(10);
        _pollTimer.Elapsed += (_, _) => PollHotkeys();
        _pollTimer.AutoReset = true;
        _pollTimer.Start();
    }

    private void StopPolling()
    {
        if (_pollTimer != null)
        {
            _pollTimer.Stop();
            _pollTimer.Dispose();
            _pollTimer = null;
        }
    }

    private void PollHotkeys()
    {
        if (_actions.Count == 0) return;

        try
        {
            foreach (var (_, hotkey) in _actions.ToArray())
            {
                if (hotkey.Key == 0) continue;

                if (IsKeyDown(hotkey.Key) && AreModifiersPressed(hotkey.Modifiers))
                {
                    if (!TryAcquireHotkeyCooldown())
                    {
                        return;
                    }

                    _logService.Info($"Polling detected hotkey: {hotkey.Gesture}");

                    var action = hotkey.Action;
                    var gesture = hotkey.Gesture;
                    var modifiers = hotkey.Modifiers;
                    var key = hotkey.Key;
                    var buttonConfig = hotkey.ButtonConfig;

                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(action, gesture, modifiers, key, buttonConfig));
                    });

                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Error("Polling hotkey error.", ex);
        }
    }

    private static bool IsKeyDown(uint vk)
    {
        return (NativeMethods.GetAsyncKeyState((int)vk) & 0x8000) != 0;
    }

    private static bool AreModifiersPressed(uint modifiers)
    {
        if ((modifiers & NativeMethods.MOD_ALT) != 0)
        {
            bool leftAlt = IsKeyDown(0xA4);
            bool rightAlt = IsKeyDown(0xA5);

            if (!leftAlt && !rightAlt)
            {
                // AltGr: 右 Alt 在部分键盘布局上被系统映射为 Ctrl+Alt，
                // GetAsyncKeyState(0xA5) 可能不返回按下，但 RightCtrl 会。
                bool rightCtrl = IsKeyDown(0xA3);
                if (!rightCtrl) return false;
            }
        }

        if ((modifiers & NativeMethods.MOD_CONTROL) != 0)
        {
            if (!IsKeyDown(0xA2) && !IsKeyDown(0xA3))
                return false;
        }

        if ((modifiers & NativeMethods.MOD_SHIFT) != 0)
        {
            if (!IsKeyDown(0xA0) && !IsKeyDown(0xA1))
                return false;
        }

        if ((modifiers & NativeMethods.MOD_WIN) != 0)
        {
            if (!IsKeyDown(0x5B) && !IsKeyDown(0x5C))
                return false;
        }

        return true;
    }

    private bool TryAcquireHotkeyCooldown()
    {
        var now = DateTime.UtcNow.Ticks;
        var last = Interlocked.Read(ref _lastHotkeyTicks);
        if (now - last < TimeSpan.TicksPerMillisecond * 200)
        {
            return false;
        }

        return Interlocked.CompareExchange(ref _lastHotkeyTicks, now, last) == last;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_actions.TryGetValue(id, out var hotkey))
            {
                if (TryAcquireHotkeyCooldown())
                {
                    _logService.Info($"Received WM_HOTKEY message for ID={id}, Action={hotkey.Action}, Gesture={hotkey.Gesture}.");
                    HotkeyPressed?.Invoke(
                        this,
                        new HotkeyPressedEventArgs(hotkey.Action, hotkey.Gesture, hotkey.Modifiers, hotkey.Key, hotkey.ButtonConfig));
                }

                handled = true;
            }
            else
            {
                _logService.Warn($"Received WM_HOTKEY message for unregistered ID={id}.");
            }
        }

        return IntPtr.Zero;
    }
}

public enum HotkeyAction
{
    PanelButton,
    ToggleWindow
}

public sealed class HotkeyPressedEventArgs : EventArgs
{
    public HotkeyPressedEventArgs(HotkeyAction action, string gesture, uint modifiers, uint key, PanelButtonConfig? buttonConfig)
    {
        Action = action;
        Gesture = gesture;
        Modifiers = modifiers;
        Key = key;
        ButtonConfig = buttonConfig;
    }

    public HotkeyAction Action { get; }

    public string Gesture { get; }

    public uint Modifiers { get; }

    public uint Key { get; }

    public PanelButtonConfig? ButtonConfig { get; }
}

internal sealed record RegisteredHotkey(HotkeyAction Action, string Gesture, uint Modifiers, uint Key, PanelButtonConfig? ButtonConfig);
