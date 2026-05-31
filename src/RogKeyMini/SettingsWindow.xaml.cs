using RogKeyMini.Config;
using RogKeyMini.Input;
using RogKeyMini.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;

namespace RogKeyMini;

public partial class SettingsWindow : Window
{
    private readonly ConfigService _configService;
    private readonly LogService _logService;
    private readonly Action _applyChanges;
    private readonly AppConfig _runtimeConfig;
    private readonly AppConfig _editingConfig;

    public SettingsWindow(
        AppConfig runtimeConfig,
        ConfigService configService,
        LogService logService,
        Action applyChanges)
    {
        _runtimeConfig = runtimeConfig;
        _configService = configService;
        _logService = logService;
        _applyChanges = applyChanges;
        _editingConfig = _configService.Clone(runtimeConfig);

        InitializeComponent();

        ActionOptions = new[]
        {
            "SendKey",
            "KeyboardBacklightDown",
            "KeyboardBacklightUp",
            "ScreenBrightnessDown",
            "LaunchOsk",
            "ToggleAutoRelease"
        };

        Buttons = new ObservableCollection<PanelButtonConfig>(
            (_editingConfig.Panel.Buttons ?? PanelButtonConfig.CreateDefaults())
            .Select(button => new PanelButtonConfig
            {
                Label = button.Label,
                Action = button.Action,
                Gesture = button.Gesture,
                TriggerHotkey = FormatHotkeyForEditor(button.TriggerHotkey ?? ResolveLegacyTriggerHotkey(button))
            }));

        ButtonsGrid.ItemsSource = Buttons;
        DataContext = this;

        HotkeyToggleWindowTextBox.Text = FormatHotkeyForEditor(_editingConfig.Hotkeys.ToggleWindow);
    }

    public ObservableCollection<PanelButtonConfig> Buttons { get; }

    public IReadOnlyList<string> ActionOptions { get; }

    private void AddButton_OnClick(object sender, RoutedEventArgs e)
    {
        var button = new PanelButtonConfig
        {
            Label = $"按钮{Buttons.Count + 1}",
            Action = "SendKey",
            Gesture = "F1",
            TriggerHotkey = string.Empty
        };

        Buttons.Add(button);
        ButtonsGrid.SelectedItem = button;
        ButtonsGrid.ScrollIntoView(button);
    }

    private void RemoveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ButtonsGrid.SelectedItem is not PanelButtonConfig selected)
        {
            return;
        }

        Buttons.Remove(selected);
    }

    private void MoveUpButton_OnClick(object sender, RoutedEventArgs e)
    {
        MoveSelectedButton(-1);
    }

    private void MoveDownButton_OnClick(object sender, RoutedEventArgs e)
    {
        MoveSelectedButton(1);
    }

    private void ResetButtons_OnClick(object sender, RoutedEventArgs e)
    {
        Buttons.Clear();
        foreach (var button in PanelButtonConfig.CreateDefaults())
        {
            Buttons.Add(new PanelButtonConfig
            {
                Label = button.Label,
                Action = button.Action,
                Gesture = button.Gesture,
                TriggerHotkey = FormatHotkeyForEditor(button.TriggerHotkey)
            });
        }
    }

    private void MoveSelectedButton(int direction)
    {
        if (ButtonsGrid.SelectedItem is not PanelButtonConfig selected)
        {
            return;
        }

        int currentIndex = Buttons.IndexOf(selected);
        int targetIndex = currentIndex + direction;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= Buttons.Count)
        {
            return;
        }

        Buttons.Move(currentIndex, targetIndex);
        ButtonsGrid.SelectedItem = selected;
    }

    private void OpenConfigFile_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var configPath = _configService.ConfigPath;
            if (!System.IO.File.Exists(configPath))
            {
                _configService.Save(_runtimeConfig, _logService);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = configPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to open config file from settings window.", ex);
            System.Windows.MessageBox.Show(this, "打开配置文件失败。", "RogKeyMini", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryValidateForm())
        {
            return;
        }

        _editingConfig.Window.AutoHideEnabled = false;
        _editingConfig.Hotkeys.ToggleWindow = NormalizeHotkeyValue(HotkeyToggleWindowTextBox.Text);
        _editingConfig.Panel.Buttons = Buttons
            .Select(button => new PanelButtonConfig
            {
                Label = button.Label,
                Action = button.Action,
                Gesture = string.Equals(button.Action, "SendKey", StringComparison.OrdinalIgnoreCase)
                    ? button.Gesture?.Trim()
                    : null,
                TriggerHotkey = NormalizeHotkeyValue(button.TriggerHotkey)
            })
            .ToList();
        SyncLegacyHotkeysFromButtons(_editingConfig);

        CopyConfig(_editingConfig, _runtimeConfig);
        _configService.Save(_runtimeConfig, _logService);
        _applyChanges();
        _logService.Info("Settings window saved configuration and applied changes.");
        Close();
    }

    private bool TryValidateForm()
    {
        if (Buttons.Count == 0)
        {
            System.Windows.MessageBox.Show(this, "至少需要保留一个按钮。", "RogKeyMini", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        foreach (var button in Buttons)
        {
            if (string.IsNullOrWhiteSpace(button.Label))
            {
                System.Windows.MessageBox.Show(this, "按钮文字不能为空。", "RogKeyMini", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            button.Action = NormalizeAction(button.Action);

            if (string.IsNullOrWhiteSpace(button.Action))
            {
                System.Windows.MessageBox.Show(this, "按钮动作不能为空。", "RogKeyMini", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.Equals(button.Action, "SendKey", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(button.Gesture))
            {
                System.Windows.MessageBox.Show(this, $"按钮“{button.Label}”使用 SendKey 时必须填写键位。", "RogKeyMini", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var triggerHotkey = NormalizeHotkeyValue(button.TriggerHotkey);
            if (!string.Equals(triggerHotkey, "Disabled", StringComparison.OrdinalIgnoreCase)
                && !KeyGestureParser.TryParseForHotkey(triggerHotkey, out _, out _))
            {
                System.Windows.MessageBox.Show(this, $"按钮“{button.Label}”的触发组合键格式无效：{triggerHotkey}。示例：Ctrl+2、Alt+7、Ctrl+Shift+1。", "RogKeyMini", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        var toggleWindowHotkey = NormalizeHotkeyValue(HotkeyToggleWindowTextBox.Text);
        if (!string.Equals(toggleWindowHotkey, "Disabled", StringComparison.OrdinalIgnoreCase)
            && !KeyGestureParser.TryParseForHotkey(toggleWindowHotkey, out _, out _))
        {
            System.Windows.MessageBox.Show(this, $"“显示/隐藏悬浮窗”的组合键格式无效：{toggleWindowHotkey}。示例：Ctrl+2、Alt+7、Ctrl+Shift+1。", "RogKeyMini", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    private static void CopyConfig(AppConfig source, AppConfig target)
    {
        target.Window = source.Window;
        target.Hotkeys = source.Hotkeys;
        target.Panel = source.Panel;
        target.Brightness = source.Brightness;
        target.KeyboardBacklight = source.KeyboardBacklight;
        target.Fan = source.Fan;
        target.CpuPower = source.CpuPower;
        target.AltMonitor = source.AltMonitor;
    }

    private static string FormatHotkeyForEditor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "Disabled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "None", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return value;
    }

    private static string NormalizeHotkeyValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Disabled" : value.Trim();
    }

    private string? ResolveLegacyTriggerHotkey(PanelButtonConfig button)
    {
        if (string.Equals(button.Action, "KeyboardBacklightDown", StringComparison.OrdinalIgnoreCase))
        {
            return _editingConfig.Hotkeys.KeyboardBacklightDown;
        }

        if (string.Equals(button.Action, "ScreenBrightnessDown", StringComparison.OrdinalIgnoreCase))
        {
            return _editingConfig.Hotkeys.ScreenBrightnessDown;
        }

        if (!string.Equals(button.Action, "SendKey", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return button.Gesture?.Trim() switch
        {
            "F2" => _editingConfig.Hotkeys.SendF2,
            "F7" => _editingConfig.Hotkeys.SendF7,
            "-" => _editingConfig.Hotkeys.SendMinus,
            "_" => _editingConfig.Hotkeys.SendUnderscore,
            _ => null
        };
    }

    private static string NormalizeAction(string? value)
    {
        return value?.Trim() switch
        {
            "KeyboardBacklightDown" => "KeyboardBacklightDown",
            "KeyboardBacklightUp" => "KeyboardBacklightUp",
            "ScreenBrightnessDown" => "ScreenBrightnessDown",
            "LaunchOsk" => "LaunchOsk",
            "ToggleAutoRelease" => "ToggleAutoRelease",
            _ => "SendKey"
        };
    }

    private static void SyncLegacyHotkeysFromButtons(AppConfig config)
    {
        config.Hotkeys.SendF2 = FindButtonTrigger(config.Panel.Buttons, "SendKey", "F2", config.Hotkeys.SendF2);
        config.Hotkeys.SendF7 = FindButtonTrigger(config.Panel.Buttons, "SendKey", "F7", config.Hotkeys.SendF7);
        config.Hotkeys.SendMinus = FindButtonTrigger(config.Panel.Buttons, "SendKey", "-", config.Hotkeys.SendMinus);
        config.Hotkeys.SendUnderscore = FindButtonTrigger(config.Panel.Buttons, "SendKey", "_", config.Hotkeys.SendUnderscore);
        config.Hotkeys.KeyboardBacklightDown = FindButtonTrigger(config.Panel.Buttons, "KeyboardBacklightDown", null, config.Hotkeys.KeyboardBacklightDown);
        config.Hotkeys.ScreenBrightnessDown = FindButtonTrigger(config.Panel.Buttons, "ScreenBrightnessDown", null, config.Hotkeys.ScreenBrightnessDown);
    }

    private static string FindButtonTrigger(IEnumerable<PanelButtonConfig> buttons, string action, string? gesture, string fallback)
    {
        foreach (var button in buttons)
        {
            if (!string.Equals(button.Action, action, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (gesture is not null && !string.Equals(button.Gesture, gesture, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return NormalizeHotkeyValue(button.TriggerHotkey);
        }

        return fallback;
    }
}
