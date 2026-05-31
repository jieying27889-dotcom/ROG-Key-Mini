using RogKeyMini.Config;
using RogKeyMini.Logging;

namespace RogKeyMini.Asus;

public sealed class KeyboardBacklightService
{
    private readonly AsusAcpiService _asusAcpiService;
    private readonly AsusHidService _asusHidService;
    private readonly AppConfig _config;
    private readonly ConfigService _configService;
    private readonly LogService _logService;
    private bool _hidFallbackLevelBootstrapped;

    public KeyboardBacklightService(
        AsusAcpiService asusAcpiService,
        AppConfig config,
        ConfigService configService,
        LogService logService)
    {
        _asusAcpiService = asusAcpiService;
        _asusHidService = new AsusHidService(logService);
        _config = config;
        _configService = configService;
        _logService = logService;
    }

    public bool TryDecrease()
    {
        try
        {
            var lastKnownLevel = GetLastKnownLevel();
            var nextLevel = Math.Max(0, lastKnownLevel - 1);

            if (_asusAcpiService.TrySendKeyboardBacklightDown())
            {
                SaveLastKnownLevel(nextLevel);
                return true;
            }

            _logService.Warn(
                $"Keyboard backlight ACPI path failed. Trying HID fallback with estimated level {lastKnownLevel} -> {nextLevel}.");

            if (!_hidFallbackLevelBootstrapped && lastKnownLevel == 0)
            {
                lastKnownLevel = GetSafeMaxLevel();
                nextLevel = Math.Max(0, lastKnownLevel - 1);
                _hidFallbackLevelBootstrapped = true;
                _logService.Warn(
                    $"Keyboard backlight fallback level was unsynchronized. Bootstrapping estimate to {lastKnownLevel} before HID decrease.");
            }

            if (_asusHidService.TrySetKeyboardBacklightLevel(nextLevel))
            {
                SaveLastKnownLevel(nextLevel, "HID");
                return true;
            }

            _logService.Warn("Keyboard backlight decrease failed on both ACPI and HID paths.");
            return false;
        }
        catch (Exception ex)
        {
            _logService.Error("Keyboard backlight decrease threw an exception.", ex);
            return false;
        }
    }

    public bool TryIncrease()
    {
        try
        {
            var lastKnownLevel = GetLastKnownLevel();
            var nextLevel = Math.Min(GetSafeMaxLevel(), lastKnownLevel + 1);

            if (_asusAcpiService.TrySendKeyboardBacklightUp())
            {
                SaveLastKnownLevel(nextLevel);
                return true;
            }

            _logService.Warn(
                $"Keyboard backlight ACPI path failed. Trying HID fallback with estimated level {lastKnownLevel} -> {nextLevel}.");

            if (_asusHidService.TrySetKeyboardBacklightLevel(nextLevel))
            {
                SaveLastKnownLevel(nextLevel, "HID");
                return true;
            }

            _logService.Warn("Keyboard backlight increase failed on both ACPI and HID paths.");
            return false;
        }
        catch (Exception ex)
        {
            _logService.Error("Keyboard backlight increase threw an exception.", ex);
            return false;
        }
    }

    private int GetLastKnownLevel()
    {
        return Math.Clamp(
            _config.KeyboardBacklight.LastKnownLevel,
            0,
            GetSafeMaxLevel());
    }

    private void SaveLastKnownLevel(int level)
    {
        SaveLastKnownLevel(level, "ACPI");
    }

    private void SaveLastKnownLevel(int level, string source)
    {
        var safeMaxLevel = GetSafeMaxLevel();
        var safeLevel = Math.Clamp(level, 0, safeMaxLevel);
        _config.KeyboardBacklight.LastKnownLevel = safeLevel;
        _configService.SaveRuntimeState(_config, _logService);
        _logService.Info(
            $"Keyboard backlight estimated level saved as {safeLevel} after {source} success. This is an estimate, not a hardware readback.");
    }

    private int GetSafeMaxLevel()
    {
        return Math.Clamp(Math.Max(1, _config.KeyboardBacklight.MaxLevel), 1, 3);
    }
}
