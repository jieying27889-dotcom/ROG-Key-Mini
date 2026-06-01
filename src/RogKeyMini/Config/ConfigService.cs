using RogKeyMini.Logging;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace RogKeyMini.Config;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _configPath;
    private readonly string _defaultConfigPath;

    public ConfigService()
    {
        var directory = AppContext.BaseDirectory;
        _configPath = Path.Combine(directory, "config.json");
        _defaultConfigPath = Path.Combine(directory, "Config", "default_config.json");
    }

    public string ConfigPath => _configPath;

    public AppConfig Load(LogService logService)
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                var defaultConfig = LoadDefaultConfig(logService);
                Save(defaultConfig, logService);
                return defaultConfig;
            }

            var json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);

            if (config is null)
            {
                throw new InvalidOperationException("Config file deserialized to null.");
            }

            var result = NormalizeConfig(config, logService, out bool normalized);
            if (normalized)
            {
                Save(result, logService);
                logService.Warn("检测到配置字段缺失或越界，已自动补全并保存回 config.json。");
            }

            return result;
        }
        catch (Exception ex)
        {
            logService.Error("Failed to load config. Falling back to defaults.", ex);
            return LoadDefaultConfig(logService);
        }
    }

    public void Save(AppConfig config, LogService logService)
    {
        try
        {
            var normalizedConfig = NormalizeConfig(config, logService, out _);
            var json = JsonSerializer.Serialize(normalizedConfig, JsonOptions);
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            logService.Error("Failed to save config.", ex);
        }
    }

    public void SaveRuntimeState(AppConfig runtimeConfig, LogService logService)
    {
        try
        {
            var diskConfig = Load(logService);
            diskConfig.Window.Left = runtimeConfig.Window.Left;
            diskConfig.Window.Top = runtimeConfig.Window.Top;
            diskConfig.Window.AutoHideEnabled = false;
            diskConfig.KeyboardBacklight.LastKnownLevel = runtimeConfig.KeyboardBacklight.LastKnownLevel;
            Save(diskConfig, logService);
        }
        catch (Exception ex)
        {
            logService.Error("Failed to save runtime state.", ex);
        }
    }

    public AppConfig Clone(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        var clone = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
        if (clone is null)
        {
            throw new InvalidOperationException("Failed to clone app config.");
        }

        return clone;
    }

    private AppConfig LoadDefaultConfig(LogService logService)
    {
        try
        {
            if (!File.Exists(_defaultConfigPath))
            {
                logService.Warn($"Default config file not found at {_defaultConfigPath}. Using code defaults.");
                return new AppConfig();
            }

            var json = File.ReadAllText(_defaultConfigPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);

            if (config is null)
            {
                throw new InvalidOperationException("Default config file deserialized to null.");
            }

            logService.Info($"Loaded defaults from {_defaultConfigPath}.");
            return NormalizeConfig(config, logService, out _);
        }
        catch (Exception ex)
        {
            logService.Error("Failed to load default config file. Using code defaults.", ex);
            return new AppConfig();
        }
    }

    private static AppConfig NormalizeConfig(AppConfig config, LogService logService, out bool normalized)
    {
        normalized = false;

        if (config.Window is null)
        {
            config.Window = new WindowConfig();
            normalized = true;
        }
        else if (string.IsNullOrWhiteSpace(config.Window.说明))
        {
            config.Window.说明 = new WindowConfig().说明;
            normalized = true;
        }

        if (config.Hotkeys is null)
        {
            config.Hotkeys = new HotkeysConfig();
            normalized = true;
        }
        else if (string.IsNullOrWhiteSpace(config.Hotkeys.说明))
        {
            config.Hotkeys.说明 = new HotkeysConfig().说明;
            normalized = true;
        }

        if (config.Panel is null)
        {
            config.Panel = new PanelConfig();
            normalized = true;
        }
        else if (string.IsNullOrWhiteSpace(config.Panel.说明))
        {
            config.Panel.说明 = new PanelConfig().说明;
            normalized = true;
        }

        if (config.Brightness is null)
        {
            config.Brightness = new BrightnessConfig();
            normalized = true;
        }
        else if (string.IsNullOrWhiteSpace(config.Brightness.说明))
        {
            config.Brightness.说明 = new BrightnessConfig().说明;
            normalized = true;
        }

        if (config.KeyboardBacklight is null)
        {
            config.KeyboardBacklight = new KeyboardBacklightConfig();
            normalized = true;
        }
        else if (string.IsNullOrWhiteSpace(config.KeyboardBacklight.说明))
        {
            config.KeyboardBacklight.说明 = new KeyboardBacklightConfig().说明;
            normalized = true;
        }

        if (config.AltMonitor is null)
        {
            config.AltMonitor = new AltMonitorConfig();
            normalized = true;
        }
        else if (string.IsNullOrWhiteSpace(config.AltMonitor.说明))
        {
            config.AltMonitor.说明 = new AltMonitorConfig().说明;
            normalized = true;
        }

        if (config.Fan is null)
        {
            config.Fan = new FanConfig();
            normalized = true;
        }

        if (config.CpuPower is null)
        {
            config.CpuPower = new CpuPowerConfig();
            normalized = true;
        }

        if (config.Panel.Buttons is null || config.Panel.Buttons.Count == 0)
        {
            config.Panel.Buttons = PanelButtonConfig.CreateDefaults();
            normalized = true;
        }
        else
        {
            for (int i = 0; i < config.Panel.Buttons.Count; i++)
            {
                var button = config.Panel.Buttons[i] ?? new PanelButtonConfig();
                var fallback = new PanelButtonConfig();

                if (string.IsNullOrWhiteSpace(button.Label))
                {
                    button.Label = fallback.Label;
                    normalized = true;
                }

                if (string.IsNullOrWhiteSpace(button.Action))
                {
                    button.Action = fallback.Action;
                    normalized = true;
                }

                if (string.Equals(button.Action, "SendKey", StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(button.Gesture))
                {
                    button.Gesture = fallback.Gesture;
                    normalized = true;
                }

                if (!string.Equals(button.Action, "SendKey", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(button.Gesture)
                    && IsBuiltInAction(button.Action))
                {
                    button.Gesture = null;
                    normalized = true;
                }

                if (string.IsNullOrWhiteSpace(button.TriggerHotkey))
                {
                    var migratedHotkey = GetLegacyTriggerHotkey(button, config.Hotkeys);
                    if (!string.IsNullOrWhiteSpace(migratedHotkey))
                    {
                        button.TriggerHotkey = migratedHotkey;
                        normalized = true;
                    }
                }

                config.Panel.Buttons[i] = button;
            }
        }

        if (string.IsNullOrWhiteSpace(config.Hotkeys.SendF2))
        {
            config.Hotkeys.SendF2 = new HotkeysConfig().SendF2;
            normalized = true;
        }
        if (string.IsNullOrWhiteSpace(config.Hotkeys.SendF7))
        {
            config.Hotkeys.SendF7 = new HotkeysConfig().SendF7;
            normalized = true;
        }
        if (string.IsNullOrWhiteSpace(config.Hotkeys.SendMinus))
        {
            config.Hotkeys.SendMinus = new HotkeysConfig().SendMinus;
            normalized = true;
        }
        if (string.IsNullOrWhiteSpace(config.Hotkeys.SendUnderscore))
        {
            config.Hotkeys.SendUnderscore = new HotkeysConfig().SendUnderscore;
            normalized = true;
        }
        if (string.IsNullOrWhiteSpace(config.Hotkeys.KeyboardBacklightDown))
        {
            config.Hotkeys.KeyboardBacklightDown = new HotkeysConfig().KeyboardBacklightDown;
            normalized = true;
        }
        if (string.IsNullOrWhiteSpace(config.Hotkeys.ScreenBrightnessDown))
        {
            config.Hotkeys.ScreenBrightnessDown = new HotkeysConfig().ScreenBrightnessDown;
            normalized = true;
        }
        if (string.IsNullOrWhiteSpace(config.Hotkeys.ToggleWindow))
        {
            config.Hotkeys.ToggleWindow = new HotkeysConfig().ToggleWindow;
            normalized = true;
        }
        if (string.IsNullOrWhiteSpace(config.CpuPower.BoostModeAc))
        {
            config.CpuPower.BoostModeAc = new CpuPowerConfig().BoostModeAc;
            normalized = true;
        }
        if (string.IsNullOrWhiteSpace(config.Window.DockEdge))
        {
            config.Window.DockEdge = new WindowConfig().DockEdge;
            normalized = true;
        }

        var safeOpacity = Math.Clamp(config.Window.Opacity, 0.2, 1.0);
        if (config.Window.Opacity != safeOpacity)
        {
            config.Window.Opacity = safeOpacity;
            normalized = true;
        }

        var safeRevealSize = Math.Clamp(config.Window.RevealSize, 12, 40);
        if (Math.Abs(config.Window.RevealSize - safeRevealSize) > double.Epsilon)
        {
            config.Window.RevealSize = safeRevealSize;
            normalized = true;
        }

        var safeHandleOpacity = Math.Clamp(config.Window.HandleOpacity, 0.1, 1.0);
        if (Math.Abs(config.Window.HandleOpacity - safeHandleOpacity) > double.Epsilon)
        {
            config.Window.HandleOpacity = safeHandleOpacity;
            normalized = true;
        }

        var safeAutoHideDelayMs = Math.Clamp(config.Window.AutoHideDelayMs, 0, 5000);
        if (config.Window.AutoHideDelayMs != safeAutoHideDelayMs)
        {
            config.Window.AutoHideDelayMs = safeAutoHideDelayMs;
            normalized = true;
        }

        var safeScreenStep = Math.Clamp(config.Brightness.ScreenStep, 1, 100);
        if (config.Brightness.ScreenStep != safeScreenStep)
        {
            config.Brightness.ScreenStep = safeScreenStep;
            normalized = true;
        }

        var safeScreenMin = Math.Clamp(config.Brightness.ScreenMin, 0, 100);
        if (config.Brightness.ScreenMin != safeScreenMin)
        {
            config.Brightness.ScreenMin = safeScreenMin;
            normalized = true;
        }

        var safeKeyboardMaxLevel = Math.Clamp(config.KeyboardBacklight.MaxLevel, 1, 3);
        if (config.KeyboardBacklight.MaxLevel != safeKeyboardMaxLevel)
        {
            config.KeyboardBacklight.MaxLevel = safeKeyboardMaxLevel;
            normalized = true;
        }

        var safeKeyboardLevel = Math.Clamp(config.KeyboardBacklight.LastKnownLevel, 0, config.KeyboardBacklight.MaxLevel);
        if (config.KeyboardBacklight.LastKnownLevel != safeKeyboardLevel)
        {
            config.KeyboardBacklight.LastKnownLevel = safeKeyboardLevel;
            normalized = true;
        }

        var safeMaxProcessorStateAc = Math.Clamp(config.CpuPower.MaxProcessorStateAc, 1, 100);
        if (config.CpuPower.MaxProcessorStateAc != safeMaxProcessorStateAc)
        {
            config.CpuPower.MaxProcessorStateAc = safeMaxProcessorStateAc;
            normalized = true;
        }

        var safeUpdateIntervalMs = Math.Clamp(config.Fan.UpdateIntervalMs, 250, 10000);
        if (config.Fan.UpdateIntervalMs != safeUpdateIntervalMs)
        {
            config.Fan.UpdateIntervalMs = safeUpdateIntervalMs;
            normalized = true;
        }

        var safeTemperatureAverageSeconds = Math.Clamp(config.Fan.TemperatureAverageSeconds, 1, 60);
        if (config.Fan.TemperatureAverageSeconds != safeTemperatureAverageSeconds)
        {
            config.Fan.TemperatureAverageSeconds = safeTemperatureAverageSeconds;
            normalized = true;
        }

        var safeStuckThresholdMs = Math.Clamp(config.AltMonitor.StuckThresholdMs, 200, 10000);
        if (config.AltMonitor.StuckThresholdMs != safeStuckThresholdMs)
        {
            config.AltMonitor.StuckThresholdMs = safeStuckThresholdMs;
            normalized = true;
        }

        if (!IsValidDockEdge(config.Window.DockEdge))
        {
            config.Window.DockEdge = new WindowConfig().DockEdge;
            normalized = true;
        }

        if (normalized)
        {
            logService.Warn("Config was missing required values and has been normalized in memory.");
        }

        return config;
    }
    private static bool IsValidDockEdge(string? dockEdge)
    {
        return dockEdge is "Left" or "Right";
    }

    private static bool IsBuiltInAction(string? action)
    {
        return action?.Trim() switch
        {
            "KeyboardBacklightDown" => true,
            "KeyboardBacklightUp" => true,
            "ScreenBrightnessDown" => true,
            "LaunchOsk" => true,
            "ToggleAutoRelease" => true,
            "ToggleNotification" => true,
            _ => false
        };
    }

    private static string? GetLegacyTriggerHotkey(PanelButtonConfig button, HotkeysConfig hotkeys)
    {
        if (string.Equals(button.Action, "KeyboardBacklightDown", StringComparison.OrdinalIgnoreCase))
        {
            return hotkeys.KeyboardBacklightDown;
        }

        if (string.Equals(button.Action, "ScreenBrightnessDown", StringComparison.OrdinalIgnoreCase))
        {
            return hotkeys.ScreenBrightnessDown;
        }

        if (!string.Equals(button.Action, "SendKey", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return button.Gesture?.Trim() switch
        {
            "F2" => hotkeys.SendF2,
            "F7" => hotkeys.SendF7,
            "-" => hotkeys.SendMinus,
            "_" => hotkeys.SendUnderscore,
            _ => null
        };
    }
}
