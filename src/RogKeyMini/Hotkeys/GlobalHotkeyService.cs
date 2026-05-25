using RogKeyMini.Config;
using RogKeyMini.Interop;
using RogKeyMini.Input;
using RogKeyMini.Logging;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RogKeyMini.Hotkeys;

public sealed class GlobalHotkeyService : IDisposable
{
    private readonly LogService _logService;
    private readonly Dictionary<int, RegisteredHotkey> _actions = new();
    private HwndSource? _source;
    private IntPtr _windowHandle;

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

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_actions.TryGetValue(id, out var hotkey))
            {
                _logService.Info($"Received WM_HOTKEY message for ID={id}, Action={hotkey.Action}, Gesture={hotkey.Gesture}.");
                HotkeyPressed?.Invoke(
                    this,
                    new HotkeyPressedEventArgs(hotkey.Action, hotkey.Gesture, hotkey.Modifiers, hotkey.Key, hotkey.ButtonConfig));
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
