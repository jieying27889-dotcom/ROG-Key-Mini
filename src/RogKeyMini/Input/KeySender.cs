using RogKeyMini.Logging;
using RogKeyMini.Interop;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RogKeyMini.Input;

public sealed class KeySender
{
    private readonly LogService _logService;
    private static readonly object InputLock = new();

    public bool IsSending { get; private set; }

    public KeySender(LogService logService)
    {
        _logService = logService;
    }

    private static readonly ushort[] Modifiers = new ushort[]
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

    public void SendF2() => SendVirtualKey(0x71, "F2");

    public void SendF7() => SendVirtualKey(0x76, "F7");

    public void SendMinus() => SendVirtualKey(0xBD, "-");

    public void SendMinus(ushort triggerKey) => SendAfterHotkeyRelease(triggerKey, SendMinus, "-");

    public void SendUnderscore() => SendUnderscoreCore(null);

    public void SendUnderscore(ushort triggerKey) => SendAfterHotkeyRelease(triggerKey, SendUnderscore, "_");

    private void SendUnderscoreCore(ushort? triggerKey, bool restoreModifiers = true)
    {
        const ushort vkShift = 0x10;
        const ushort vkMinus = 0xBD;

        var core = new[]
        {
            CreateKeyInput(vkShift, 0),
            CreateKeyInput(vkMinus, 0),
            CreateKeyInput(vkMinus, NativeMethods.KEYEVENTF_KEYUP),
            CreateKeyInput(vkShift, NativeMethods.KEYEVENTF_KEYUP)
        };

        SendKeysWithReleasedContext(core, "_", triggerKey, restoreModifiers);
    }

    private void SendVirtualKey(ushort virtualKey, string name, ushort? triggerKey = null, bool restoreModifiers = true)
    {
        var core = new[]
        {
            CreateKeyInput(virtualKey, 0),
            CreateKeyInput(virtualKey, NativeMethods.KEYEVENTF_KEYUP)
        };

        SendKeysWithReleasedContext(core, name, triggerKey, restoreModifiers);
    }

    private void SendKeysWithReleasedContext(
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
                var activeModifiers = new System.Collections.Generic.List<ushort>();
                foreach (var mod in Modifiers)
                {
                    if ((NativeMethods.GetAsyncKeyState(mod) & 0x8000) != 0)
                    {
                        activeModifiers.Add(mod);
                    }
                }
 
                var releaseInputs = new System.Collections.Generic.List<NativeMethods.INPUT>();
 
                // 1. 先释放触发热键的主键，避免 Alt+9 / Alt+0 仍处于按下状态时干扰后续注入。
                if (triggerKey is ushort key)
                {
                    releaseInputs.Add(CreateKeyInput(key, NativeMethods.KEYEVENTF_KEYUP));
                }

                // 2. 释放所有正处于按下状态的修饰键
                foreach (var mod in activeModifiers)
                {
                    releaseInputs.Add(CreateKeyInput(mod, NativeMethods.KEYEVENTF_KEYUP));
                }

                if (releaseInputs.Count > 0)
                {
                    var releaseBatch = releaseInputs.ToArray();
                    var released = NativeMethods.SendInput(
                        (uint)releaseBatch.Length,
                        releaseBatch,
                        Marshal.SizeOf<NativeMethods.INPUT>());

                    if (released != releaseBatch.Length)
                    {
                        _logService.Warn($"SendInput failed while releasing hotkey context for {name}. Sent={released}.");
                        return;
                    }
                }

                var sent = NativeMethods.SendInput((uint)coreInputs.Length, coreInputs, Marshal.SizeOf<NativeMethods.INPUT>());

                if (sent != coreInputs.Length)
                {
                    _logService.Warn($"SendInput failed for {name}. Sent={sent}.");
                    return;
                }

                if (restoreModifiers && activeModifiers.Count > 0)
                {
                    var restoreInputs = new System.Collections.Generic.List<NativeMethods.INPUT>();

                    // 单独分批恢复仍被物理按住的修饰键，避免输入框把目标键误判成 Alt 组合，
                    // 同时也不让键盘逻辑状态长期停留在“Alt 已弹起”的异常状态。
                    Thread.Sleep(30);

                    for (int i = activeModifiers.Count - 1; i >= 0; i--)
                    {
                        var modifier = activeModifiers[i];
                        if ((NativeMethods.GetAsyncKeyState(modifier) & 0x8000) != 0)
                        {
                            restoreInputs.Add(CreateKeyInput(modifier, 0));
                        }
                    }

                    if (restoreInputs.Count > 0)
                    {
                        var restoreBatch = restoreInputs.ToArray();
                        var restored = NativeMethods.SendInput(
                            (uint)restoreBatch.Length,
                            restoreBatch,
                            Marshal.SizeOf<NativeMethods.INPUT>());

                        if (restored != restoreBatch.Length)
                        {
                            _logService.Warn($"SendInput partially restored modifiers for {name}. Sent={restored}/{restoreBatch.Length}.");
                        }
                    }
                }
 
                _logService.Info($"Sent key {name}.");
            }
            catch (Exception ex)
            {
                _logService.Error($"Failed to send key {name}.", ex);
            }
            finally
            {
                IsSending = false;
            }
        }
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
