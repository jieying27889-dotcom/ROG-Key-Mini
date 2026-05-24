using RogKeyMini.Logging;
using System.Management;

namespace RogKeyMini.Asus;

public sealed class ScreenBrightnessService
{
    private readonly LogService _logService;

    public ScreenBrightnessService(LogService logService)
    {
        _logService = logService;
    }

    public bool TryDecrease(int step, int minimum)
    {
        try
        {
            var normalizedStep = Math.Clamp(step, 1, 100);
            var normalizedMinimum = Math.Clamp(minimum, 0, 100);

            if (!TryGetCurrentBrightness(out var currentBrightness))
            {
                _logService.Warn("Failed to read current screen brightness.");
                return false;
            }

            var targetBrightness = Math.Max(normalizedMinimum, currentBrightness - normalizedStep);
            if (targetBrightness == currentBrightness)
            {
                _logService.Info($"Screen brightness already at floor {currentBrightness}%.");
                return true;
            }

            if (!TrySetBrightness((byte)targetBrightness))
            {
                _logService.Warn($"Failed to set screen brightness to {targetBrightness}%.");
                return false;
            }

            _logService.Info($"Screen brightness changed from {currentBrightness}% to {targetBrightness}%.");
            return true;
        }
        catch (Exception ex)
        {
            _logService.Error("Screen brightness decrease threw an exception.", ex);
            return false;
        }
    }

    private bool TryGetCurrentBrightness(out byte brightness)
    {
        brightness = 0;

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\WMI",
                "SELECT Active, CurrentBrightness FROM WmiMonitorBrightness");

            using var results = searcher.Get();

            ManagementObject? fallback = null;
            foreach (ManagementObject item in results)
            {
                fallback ??= item;

                if (IsActive(item))
                {
                    brightness = (byte)item["CurrentBrightness"];
                    return true;
                }
            }

            if (fallback is null)
            {
                return false;
            }

            brightness = (byte)fallback["CurrentBrightness"];
            return true;
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to query screen brightness through WMI.", ex);
            return false;
        }
    }

    private bool TrySetBrightness(byte brightness)
    {
        try
        {
            using var managementClass = new ManagementClass("root\\WMI", "WmiMonitorBrightnessMethods", null);
            using var instances = managementClass.GetInstances();

            var anySucceeded = false;
            foreach (ManagementObject item in instances)
            {
                if (!IsActive(item))
                {
                    continue;
                }

                item.InvokeMethod("WmiSetBrightness", new object[] { uint.MinValue, brightness });
                anySucceeded = true;
            }

            if (anySucceeded)
            {
                return true;
            }

            foreach (ManagementObject item in instances)
            {
                item.InvokeMethod("WmiSetBrightness", new object[] { uint.MinValue, brightness });
                anySucceeded = true;
            }

            return anySucceeded;
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to set screen brightness through WMI.", ex);
            return false;
        }
    }

    private static bool IsActive(ManagementBaseObject item)
    {
        return item["Active"] is bool active && active;
    }
}
