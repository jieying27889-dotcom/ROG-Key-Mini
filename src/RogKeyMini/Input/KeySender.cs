using RogKeyMini.Interop;
using RogKeyMini.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RogKeyMini.Input;

public sealed class KeySender
{
    private readonly LogService _logService;
    private static readonly object InputLock = new();

    private static readonly ushort[] PhysicalModifiers =
    {
        0xA0, // VK_LSHIFT
        0xA1, // VK_RSHIFT
        0xA2, // VK_LCONTROL
        0xA3, // VK_RCONTROL
        0xA4, // VK_LMENU
        0xA5, // VK_RMENU
        0x5B, // VK_LWIN
        0x5C  // VK_RWIN
    };

    public KeySender(LogService logService)
    {
        _logService = logService;
    }

    private volatile bool _isSending;
    public bool IsSending
    {
        get => _isSending;
        private set => _isSending = value;
    }

    public void SendF2() => SendVirtualKey(0x71, "F2");

    public void SendF7() => SendVirtualKey(0x76, "F7");

    public void SendMinus() => SendVirtualKey(0xBD, "-");

    public void SendMinus(ushort triggerKey) => SendAfterHotkeyRelease(triggerKey, SendMinus, "-");

    public void SendUnderscore() => SendGesture("_", "_");

    public void SendUnderscore(ushort triggerKey) => SendAfterHotkeyRelease(triggerKey, SendUnderscore, "_");

    public void SendGesture(string gesture, string? nameOverride = null)
    {
        if (!KeyGestureParser.TryParseForSend(gesture, out var parsed) || parsed is null)
        {
            _logService.Warn($"Unsupported send gesture: {gesture}.");
            return;
        }

        var coreInputs = BuildGestureInputs(parsed);
        SendInputsWithReleasedContext(coreInputs, nameOverride ?? parsed.Gesture, null, true);
    }

    public void SendGestureAfterHotkeyRelease(string gesture, ushort triggerKey, string? nameOverride = null)
    {
        SendAfterHotkeyRelease(triggerKey, () => SendGesture(gesture, nameOverride), nameOverride ?? gesture);
    }

    public void ReleaseKey(ushort virtualKey, string name)
    {
        lock (InputLock)
        {
            try
            {
                var inputs = new[]
                {
                    CreateKeyInput(virtualKey, NativeMethods.KEYEVENTF_KEYUP)
                };

                var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
                if (sent != inputs.Length)
                {
                    _logService.Warn($"释放 {name} 键的 SendInput 失败。发送数={sent}。");
                    return;
                }

                _logService.Info($"成功发送 {name} 键的 KeyUp 信号。");
            }
            catch (Exception ex)
            {
                _logService.Error($"释放 {name} 键失败。", ex);
            }
        }
    }

    private void SendVirtualKey(ushort virtualKey, string name, ushort? triggerKey = null, bool restoreModifiers = true)
    {
        var coreInputs = new[]
        {
            CreateKeyInput(virtualKey, 0),
            CreateKeyInput(virtualKey, NativeMethods.KEYEVENTF_KEYUP)
        };

        SendInputsWithReleasedContext(coreInputs, name, triggerKey, restoreModifiers);
    }

    private void SendInputsWithReleasedContext(
        NativeMethods.INPUT[] coreInputs,
        string name,
        ushort? triggerKey,
        bool restoreModifiers)
    {
        lock (InputLock)
        {
            IsSending = true;
            try
            {
                var activeModifiers = GetPressedModifiers();
                var releaseInputs = new List<NativeMethods.INPUT>();

                if (triggerKey is ushort key)
                {
                    releaseInputs.Add(CreateKeyInput(key, NativeMethods.KEYEVENTF_KEYUP));
                }

                foreach (var modifier in activeModifiers)
                {
                    releaseInputs.Add(CreateKeyInput(modifier, NativeMethods.KEYEVENTF_KEYUP));
                }

                if (!SendInputBatch(releaseInputs, $"release hotkey context for {name}"))
                {
                    return;
                }

                if (activeModifiers.Count > 0)
                {
                    Thread.Sleep(50);
                }

                if (!SendInputBatch(coreInputs, name))
                {
                    return;
                }

                if (restoreModifiers)
                {
                    RestoreStillPressedModifiers(activeModifiers, name);
                }

                _logService.Info($"Sent key gesture {name}.");
            }
            catch (Exception ex)
            {
                _logService.Error($"Failed to send key gesture {name}.", ex);
            }
            finally
            {
                IsSending = false;
            }
        }
    }

    private static NativeMethods.INPUT[] BuildGestureInputs(ParsedKeyGesture gesture)
    {
        var inputs = new List<NativeMethods.INPUT>();

        foreach (var modifier in gesture.Modifiers)
        {
            inputs.Add(CreateKeyInput(modifier, 0));
        }

        inputs.Add(CreateKeyInput(gesture.Key, 0));
        inputs.Add(CreateKeyInput(gesture.Key, NativeMethods.KEYEVENTF_KEYUP));

        for (int i = gesture.Modifiers.Count - 1; i >= 0; i--)
        {
            inputs.Add(CreateKeyInput(gesture.Modifiers[i], NativeMethods.KEYEVENTF_KEYUP));
        }

        return inputs.ToArray();
    }

    private List<ushort> GetPressedModifiers()
    {
        var pressed = new List<ushort>();
        foreach (var modifier in PhysicalModifiers)
        {
            if ((NativeMethods.GetAsyncKeyState(modifier) & 0x8000) != 0)
            {
                pressed.Add(modifier);
            }
        }

        // AltGr workaround: 系统可能把右 Alt 当作 Ctrl+Alt (AltGr)。
        // GetAsyncKeyState 可能只报告 RA=True，但系统内部同时有 Ctrl 活跃。
        // 必须同时释放 VK_RCONTROL 和 VK_LMENU 才能完全退出 AltGr 状态。
        if (pressed.Contains((ushort)0xA5))
        {
            if (!pressed.Contains((ushort)0xA3)) pressed.Add((ushort)0xA3);
            if (!pressed.Contains((ushort)0xA4)) pressed.Add((ushort)0xA4);
        }

        return pressed;
    }

    private bool SendInputBatch(IReadOnlyList<NativeMethods.INPUT> batch, string name)
    {
        if (batch.Count == 0)
        {
            return true;
        }

        var sent = NativeMethods.SendInput((uint)batch.Count, batch.ToArray(), Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent == batch.Count)
        {
            return true;
        }

        _logService.Warn($"SendInput failed for {name}. Sent={sent}/{batch.Count}.");
        return false;
    }

    private void RestoreStillPressedModifiers(IReadOnlyList<ushort> previouslyPressedModifiers, string name)
    {
        if (previouslyPressedModifiers.Count == 0)
        {
            return;
        }

        Thread.Sleep(30);

        var restoreInputs = new List<NativeMethods.INPUT>();
        for (int i = previouslyPressedModifiers.Count - 1; i >= 0; i--)
        {
            var modifier = previouslyPressedModifiers[i];
            if ((NativeMethods.GetAsyncKeyState(modifier) & 0x8000) != 0)
            {
                restoreInputs.Add(CreateKeyInput(modifier, 0));
            }
        }

        if (restoreInputs.Count == 0)
        {
            return;
        }

        var restored = NativeMethods.SendInput((uint)restoreInputs.Count, restoreInputs.ToArray(), Marshal.SizeOf<NativeMethods.INPUT>());
        if (restored != restoreInputs.Count)
        {
            _logService.Warn($"SendInput partially restored modifiers for {name}. Sent={restored}/{restoreInputs.Count}.");
        }
    }

    private static NativeMethods.INPUT CreateKeyInput(ushort virtualKey, uint flags)
    {
        var scanCode = (ushort)NativeMethods.MapVirtualKey(virtualKey, 0);
        return new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = virtualKey,
                    wScan = scanCode,
                    dwFlags = flags,
                    dwExtraInfo = IntPtr.Zero,
                    time = 0
                }
            }
        };
    }

    private void SendAfterHotkeyRelease(ushort triggerKey, Action sendAction, string name)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await WaitForHotkeyReleaseAsync(triggerKey);
                sendAction();
            }
            catch (Exception ex)
            {
                _logService.Error($"Failed to defer hotkey send for {name}.", ex);
            }
        });
    }

    private static async Task WaitForHotkeyReleaseAsync(ushort triggerKey)
    {
        const int pollIntervalMs = 10;
        const int timeoutMs = 500;

        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < timeoutMs)
        {
            if (!IsKeyPressed(triggerKey)
                && !IsKeyPressed(0xA4)
                && !IsKeyPressed(0xA5)
                && !IsKeyPressed(0xA2)
                && !IsKeyPressed(0xA3))
            {
                return;
            }

            await Task.Delay(pollIntervalMs);
        }
    }

    private static bool IsKeyPressed(int virtualKey)
    {
        return (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }
}
