using RogKeyMini.Config;
using RogKeyMini.Interop;
using RogKeyMini.Logging;
using System.Windows;
using System.Windows.Interop;

namespace RogKeyMini.UI;

public sealed class FloatingWindowService
{
    private readonly LogService _logService;

    public FloatingWindowService(LogService logService)
    {
        _logService = logService;
    }

    public void Attach(Window window, WindowConfig config)
    {
        window.Left = config.Left;
        window.Top = config.Top;
        window.Topmost = config.Topmost;
        window.Opacity = config.Opacity;

        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE).ToInt64();
            style |= NativeMethods.WS_EX_TOOLWINDOW;
            style |= NativeMethods.WS_EX_NOACTIVATE;
            NativeMethods.SetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE, new IntPtr(style));
            _logService.Info("Applied floating window extended styles.");
        };
    }
}

