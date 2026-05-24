using RogKeyMini.Logging;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace RogKeyMini.Asus;

public sealed class AsusAcpiService : IDisposable
{
    private const string FileName = @"\\.\ATKACPI";
    private const uint ControlCode = 0x0022240C;
    private const uint MethodDevs = 0x53564544;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;

    private const uint UniversalControl = 0x00100021;
    private const int KeyboardBacklightDown = 0x00C5;

    private static readonly IntPtr InvalidHandleValue = new(-1);

    private readonly LogService _logService;
    private IntPtr _handle = IntPtr.Zero;
    private bool _connectAttempted;
    private bool _disposed;

    public AsusAcpiService(LogService logService)
    {
        _logService = logService;
    }

    public bool IsAvailable => EnsureConnected();

    public bool TrySendKeyboardBacklightDown()
    {
        try
        {
            if (!EnsureConnected())
            {
                return false;
            }

            if (!TryDeviceSet(UniversalControl, KeyboardBacklightDown, "KeyboardBacklightDown", out var result))
            {
                return false;
            }

            if (result is not 0 and not 1)
            {
                _logService.Warn($"Keyboard backlight down returned unexpected ACPI result {result}.");
                return false;
            }

            _logService.Info($"Keyboard backlight down command sent through Asus ACPI. Result={result}.");
            return true;
        }
        catch (Exception ex)
        {
            _logService.Error("Keyboard backlight down threw an exception.", ex);
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CloseHandleIfNeeded();
        GC.SuppressFinalize(this);
    }

    private bool EnsureConnected()
    {
        if (_disposed)
        {
            _logService.Warn("Asus ACPI connection requested after disposal.");
            return false;
        }

        if (IsValidHandle(_handle))
        {
            return true;
        }

        if (_connectAttempted)
        {
            return false;
        }

        try
        {
            _connectAttempted = true;
            _handle = CreateFile(
                FileName,
                GenericRead | GenericWrite,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                FileAttributeNormal,
                IntPtr.Zero);

            if (!IsValidHandle(_handle))
            {
                var errorCode = Marshal.GetLastWin32Error();
                var message = new Win32Exception(errorCode).Message;
                _logService.Warn($"Failed to open Asus ACPI device {FileName}. Win32={errorCode}, {message}");
                _handle = IntPtr.Zero;
                return false;
            }

            _logService.Info($"Connected to Asus ACPI device {FileName}.");
            return true;
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to connect to Asus ACPI device.", ex);
            _handle = IntPtr.Zero;
            return false;
        }
    }

    private bool TryDeviceSet(uint deviceId, int status, string logName, out int result)
    {
        result = -1;

        try
        {
            var args = new byte[8];
            BitConverter.GetBytes(deviceId).CopyTo(args, 0);
            BitConverter.GetBytes((uint)status).CopyTo(args, 4);

            var response = CallMethod(MethodDevs, args);
            if (response.Length < sizeof(int))
            {
                _logService.Warn($"{logName} ACPI response too short: {response.Length} byte(s).");
                return false;
            }

            result = BitConverter.ToInt32(response, 0);
            _logService.Info($"{logName} ACPI set status={status}, result={result}.");
            return true;
        }
        catch (Exception ex)
        {
            _logService.Error($"{logName} ACPI call failed.", ex);
            return false;
        }
    }

    private byte[] CallMethod(uint methodId, byte[] args)
    {
        var acpiBuffer = new byte[8 + args.Length];
        var outBuffer = new byte[16];

        BitConverter.GetBytes(methodId).CopyTo(acpiBuffer, 0);
        BitConverter.GetBytes((uint)args.Length).CopyTo(acpiBuffer, 4);
        Array.Copy(args, 0, acpiBuffer, 8, args.Length);

        Control(ControlCode, acpiBuffer, outBuffer);
        return outBuffer;
    }

    private void Control(uint ioControlCode, byte[] inputBuffer, byte[] outputBuffer)
    {
        if (!EnsureConnected())
        {
            throw new InvalidOperationException("Asus ACPI device is not connected.");
        }

        uint bytesReturned = 0;
        var success = DeviceIoControl(
            _handle,
            ioControlCode,
            inputBuffer,
            (uint)inputBuffer.Length,
            outputBuffer,
            (uint)outputBuffer.Length,
            ref bytesReturned,
            IntPtr.Zero);

        if (!success)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "DeviceIoControl failed.");
        }
    }

    private void CloseHandleIfNeeded()
    {
        if (!IsValidHandle(_handle))
        {
            _handle = IntPtr.Zero;
            return;
        }

        try
        {
            CloseHandle(_handle);
            _logService.Info("Asus ACPI handle closed.");
        }
        catch (Exception ex)
        {
            _logService.Error("Closing Asus ACPI handle threw an exception.", ex);
        }
        finally
        {
            _handle = IntPtr.Zero;
        }
    }

    private static bool IsValidHandle(IntPtr handle)
    {
        return handle != IntPtr.Zero && handle != InvalidHandleValue;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        IntPtr hDevice,
        uint dwIoControlCode,
        byte[] lpInBuffer,
        uint nInBufferSize,
        byte[] lpOutBuffer,
        uint nOutBufferSize,
        ref uint lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
