namespace RogKeyMini.Config;

public sealed class AppConfig
{
    public WindowConfig Window { get; set; } = new();

    public HotkeysConfig Hotkeys { get; set; } = new();

    public PanelConfig Panel { get; set; } = new();

    public BrightnessConfig Brightness { get; set; } = new();

    public KeyboardBacklightConfig KeyboardBacklight { get; set; } = new();

    public FanConfig Fan { get; set; } = new();

    public CpuPowerConfig CpuPower { get; set; } = new();

    public AltMonitorConfig AltMonitor { get; set; } = new();
}

public sealed class WindowConfig
{
    public string 说明 { get; set; } = "悬浮窗设置：Left(左边距), Top(上边距), Opacity(不透明度0.2-1.0), Topmost(是否置顶)。AutoHideEnabled、DockEdge、RevealSize、HandleOpacity、AutoHideDelayMs 为历史兼容字段，当前版本已不再使用自动靠边隐藏。";

    public double Left { get; set; } = 1200;

    public double Top { get; set; } = 300;

    public double Opacity { get; set; } = 0.95;

    public bool Topmost { get; set; } = true;

    public bool AutoHideEnabled { get; set; } = false;

    public string DockEdge { get; set; } = "Right";

    public double RevealSize { get; set; } = 14;

    public double HandleOpacity { get; set; } = 0.78;

    public int AutoHideDelayMs { get; set; } = 500;
}

public sealed class HotkeysConfig
{
    public string 说明 { get; set; } = "全局快捷键设置：支持注册如 Ctrl+2、Alt+7 等组合键，用于触发固定功能。";

    public string SendF2 { get; set; } = "Ctrl+2";

    public string SendF7 { get; set; } = "Alt+7";

    public string SendMinus { get; set; } = "Alt+9";

    public string SendUnderscore { get; set; } = "Alt+0";

    public string KeyboardBacklightDown { get; set; } = "Alt+[";

    public string ScreenBrightnessDown { get; set; } = "Alt+]";

    public string ToggleWindow { get; set; } = "Ctrl+Shift+1";
}

public sealed class PanelConfig
{
    public string 说明 { get; set; } = "按钮面板设置：Buttons 为界面按钮列表。每个按钮支持 Label(显示文字), Action(动作类型), Gesture(要模拟的键位或组合键), TriggerHotkey(触发该按钮的全局组合键)。固定为每排5个按钮，数量增加时自动换到下一排。";

    public List<PanelButtonConfig> Buttons { get; set; } = PanelButtonConfig.CreateDefaults();
}

public sealed class PanelButtonConfig
{
    public string Label { get; set; } = "F2";

    public string Action { get; set; } = "SendKey";

    public string? Gesture { get; set; } = "F2";

    public string? TriggerHotkey { get; set; } = "Ctrl+2";

    public static List<PanelButtonConfig> CreateDefaults()
    {
        return new List<PanelButtonConfig>
        {
            new() { Label = "F2", Action = "SendKey", Gesture = "F2", TriggerHotkey = "Ctrl+Shift+2" },
            new() { Label = "F7", Action = "SendKey", Gesture = "F7", TriggerHotkey = "Ctrl+Shift+7" },
            new() { Label = "-", Action = "SendKey", Gesture = "-", TriggerHotkey = "Ctrl+Shift+9" },
            new() { Label = "_", Action = "SendKey", Gesture = "_", TriggerHotkey = "Ctrl+Shift+0" },
            new() { Label = "背光-", Action = "KeyboardBacklightDown", TriggerHotkey = "Ctrl+Shift+[" },
            new() { Label = "背光+", Action = "KeyboardBacklightUp", TriggerHotkey = "Ctrl+Shift+]" },
            new() { Label = "亮度-", Action = "ScreenBrightnessDown", TriggerHotkey = "" },
            new() { Label = "OSK", Action = "LaunchOsk", TriggerHotkey = "" },
            new() { Label = "防粘Alt", Action = "ToggleAutoRelease", TriggerHotkey = "" },
            new() { Label = "通知", Action = "ToggleNotification", TriggerHotkey = "" }
        };
    }
}

public sealed class BrightnessConfig
{
    public string 说明 { get; set; } = "屏幕亮度调节：ScreenStep(每次减少的百分比), ScreenMin(最低亮度限制)。";

    public int ScreenStep { get; set; } = 10;

    public int ScreenMin { get; set; } = 0;
}

public sealed class KeyboardBacklightConfig
{
    public string 说明 { get; set; } = "键盘背光设置：LastKnownLevel(当前背光亮度级), MaxLevel(最大亮度级，通常为3)。";

    public int LastKnownLevel { get; set; } = 3;

    public int MaxLevel { get; set; } = 3;
}

public sealed class FanConfig
{
    public bool ManualFanEnabled { get; set; }

    public bool LastManualFanSessionClosedSafely { get; set; } = true;

    public int UpdateIntervalMs { get; set; } = 1000;

    public int TemperatureAverageSeconds { get; set; } = 6;
}

public sealed class CpuPowerConfig
{
    public int MaxProcessorStateAc { get; set; } = 85;

    public string BoostModeAc { get; set; } = "Disabled";
}

public sealed class AltMonitorConfig
{
    public string 说明 { get; set; } = "Alt 键粘连监控：MonitorLeftAlt/MonitorRightAlt(是否监控左右Alt), StuckThresholdMs(判定卡住的长按时长毫秒), AutoReleaseEnabled(是否自动释放), NotificationsEnabled(是否启用提示)。";

    public bool MonitorLeftAlt { get; set; } = true;

    public bool MonitorRightAlt { get; set; } = true;

    public int StuckThresholdMs { get; set; } = 2000;

    public bool AutoReleaseEnabled { get; set; } = true;

    public bool NotificationsEnabled { get; set; } = true;
}
