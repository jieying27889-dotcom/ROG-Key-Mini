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
                logService.Warn("检测到配置字段不完整（如缺少新增的Alt键监控属性或中文说明），已将补全后的配置保存回 config.json 文件。");
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
            diskConfig.KeyboardBacklight.LastKnownLevel = runtimeConfig.KeyboardBacklight.LastKnownLevel;
            Save(diskConfig, logService);
        }
        catch (Exception ex)
        {
            logService.Error("Failed to save runtime state.", ex);
        }
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
        if (string.IsNullOrWhiteSpace(config.Window.说明))
        {
            config.Window.说明 = new WindowConfig().说明;
            normalized = true;
        }

        if (config.Hotkeys is null)
        {
            config.Hotkeys = new HotkeysConfig();
            normalized = true;
        }
        if (string.IsNullOrWhiteSpace(config.Hotkeys.说明))
        {
            config.Hotkeys.说明 = new HotkeysConfig().说明;
            normalized = true;
        }

        if (config.Brightness is null)
        {
            config.Brightness = new BrightnessConfig();
            normalized = true;
        }
        if (string.IsNullOrWhiteSpace(config.Brightness.说明))
        {
            config.Brightness.说明 = new BrightnessConfig().说明;
            normalized = true;
        }

        if (config.KeyboardBacklight is null)
        {
            config.KeyboardBacklight = new KeyboardBacklightConfig();
            normalized = true;
        }
        if (string.IsNullOrWhiteSpace(config.KeyboardBacklight.说明))
        {
            config.KeyboardBacklight.说明 = new KeyboardBacklightConfig().说明;
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
 
        if (config.AltMonitor is null)
        {
            config.AltMonitor = new AltMonitorConfig();
            normalized = true;
        }
        if (string.IsNullOrWhiteSpace(config.AltMonitor.说明))
        {
            config.AltMonitor.说明 = new AltMonitorConfig().说明;
            normalized = true;
        }

        if (config.Hotkeys.SendF2 is null)
        {
            config.Hotkeys.SendF2 = new HotkeysConfig().SendF2;
            normalized = true;
        }

        if (config.Hotkeys.SendF7 is null)
        {
            config.Hotkeys.SendF7 = new HotkeysConfig().SendF7;
            normalized = true;
        }

        if (config.Hotkeys.SendMinus is null)
        {
            config.Hotkeys.SendMinus = new HotkeysConfig().SendMinus;
            normalized = true;
        }

        if (config.Hotkeys.SendUnderscore is null)
        {
            config.Hotkeys.SendUnderscore = new HotkeysConfig().SendUnderscore;
            normalized = true;
        }

        if (config.Hotkeys.KeyboardBacklightDown is null)
        {
            config.Hotkeys.KeyboardBacklightDown = new HotkeysConfig().KeyboardBacklightDown;
            normalized = true;
        }

        if (config.Hotkeys.ScreenBrightnessDown is null)
        {
            config.Hotkeys.ScreenBrightnessDown = new HotkeysConfig().ScreenBrightnessDown;
            normalized = true;
        }

        if (config.Hotkeys.ToggleWindow is null)
        {
            config.Hotkeys.ToggleWindow = new HotkeysConfig().ToggleWindow;
            normalized = true;
        }

        if (string.IsNullOrWhiteSpace(config.CpuPower.BoostModeAc))
        {
            config.CpuPower.BoostModeAc = new CpuPowerConfig().BoostModeAc;
            normalized = true;
        }

        var safeOpacity = Math.Clamp(config.Window.Opacity, 0.2, 1.0);
        if (config.Window.Opacity != safeOpacity)
        {
            config.Window.Opacity = safeOpacity;
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

        if (normalized)
        {
            logService.Warn("Config was missing required values and has been normalized in memory.");
        }

        return config;
    }
}
