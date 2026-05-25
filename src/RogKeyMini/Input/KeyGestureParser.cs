namespace RogKeyMini.Input;

internal static class KeyGestureParser
{
    private const ushort VkShift = 0x10;
    private const ushort VkControl = 0x11;
    private const ushort VkAlt = 0x12;
    private const ushort VkLeftWin = 0x5B;
    private const ushort VkMinus = 0xBD;

    public static bool TryParseForSend(string gesture, out ParsedKeyGesture? parsed)
    {
        parsed = null;

        if (string.IsNullOrWhiteSpace(gesture))
        {
            return false;
        }

        var modifiers = new List<ushort>();
        ushort key = 0;
        bool keyAssigned = false;

        var parts = gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            switch (part.Trim().ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers.Add(VkControl);
                    break;
                case "ALT":
                case "MENU":
                    modifiers.Add(VkAlt);
                    break;
                case "SHIFT":
                    modifiers.Add(VkShift);
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers.Add(VkLeftWin);
                    break;
                default:
                    if (keyAssigned || !TryParsePrimaryKey(part, modifiers, out key))
                    {
                        return false;
                    }

                    keyAssigned = true;
                    break;
            }
        }

        if (!keyAssigned)
        {
            return false;
        }

        parsed = new ParsedKeyGesture(modifiers.Distinct().ToArray(), key, gesture);
        return true;
    }

    public static bool TryParseForHotkey(string gesture, out uint modifiers, out uint key)
    {
        modifiers = 0;
        key = 0;

        if (string.IsNullOrWhiteSpace(gesture))
        {
            return false;
        }

        var parts = gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            switch (part.Trim().ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= Interop.NativeMethods.MOD_CONTROL;
                    break;
                case "ALT":
                case "MENU":
                    modifiers |= Interop.NativeMethods.MOD_ALT;
                    break;
                case "SHIFT":
                    modifiers |= Interop.NativeMethods.MOD_SHIFT;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= Interop.NativeMethods.MOD_WIN;
                    break;
                default:
                    if (key != 0 || !TryParseSimpleKey(part, out var parsedKey))
                    {
                        return false;
                    }

                    key = parsedKey;
                    break;
            }
        }

        return key != 0;
    }

    private static bool TryParsePrimaryKey(string token, List<ushort> modifiers, out ushort key)
    {
        key = 0;
        var normalized = token.Trim().ToUpperInvariant();

        if (normalized == "_")
        {
            modifiers.Add(VkShift);
            key = VkMinus;
            return true;
        }

        return TryParseSimpleKey(normalized, out key);
    }

    private static bool TryParseSimpleKey(string token, out ushort key)
    {
        key = 0;

        var normalized = token.Trim().ToUpperInvariant();
        if (normalized.Length == 1)
        {
            var c = normalized[0];

            if (c is >= 'A' and <= 'Z')
            {
                key = c;
                return true;
            }

            if (c is >= '0' and <= '9')
            {
                key = c;
                return true;
            }

            if (c == '-')
            {
                key = VkMinus;
                return true;
            }

            if (c == '[')
            {
                key = 0xDB;
                return true;
            }

            if (c == ']')
            {
                key = 0xDD;
                return true;
            }
        }

        if (normalized is "SPACE")
        {
            key = 0x20;
            return true;
        }

        if (normalized is "ENTER" or "RETURN")
        {
            key = 0x0D;
            return true;
        }

        if (normalized is "ESC" or "ESCAPE")
        {
            key = 0x1B;
            return true;
        }

        if (normalized is "TAB")
        {
            key = 0x09;
            return true;
        }

        if (normalized is "BACKSPACE")
        {
            key = 0x08;
            return true;
        }

        if (normalized is "DELETE" or "DEL")
        {
            key = 0x2E;
            return true;
        }

        if (normalized is "INSERT" or "INS")
        {
            key = 0x2D;
            return true;
        }

        if (normalized is "HOME")
        {
            key = 0x24;
            return true;
        }

        if (normalized is "END")
        {
            key = 0x23;
            return true;
        }

        if (normalized is "PAGEUP" or "PGUP")
        {
            key = 0x21;
            return true;
        }

        if (normalized is "PAGEDOWN" or "PGDN")
        {
            key = 0x22;
            return true;
        }

        if (normalized is "LEFT")
        {
            key = 0x25;
            return true;
        }

        if (normalized is "UP")
        {
            key = 0x26;
            return true;
        }

        if (normalized is "RIGHT")
        {
            key = 0x27;
            return true;
        }

        if (normalized is "DOWN")
        {
            key = 0x28;
            return true;
        }

        if (normalized is "MINUS" or "OEM_MINUS" or "OEMMINUS")
        {
            key = VkMinus;
            return true;
        }

        if (normalized is "OEM_4" or "OEM4")
        {
            key = 0xDB;
            return true;
        }

        if (normalized is "OEM_6" or "OEM6")
        {
            key = 0xDD;
            return true;
        }

        if (normalized.StartsWith('F')
            && int.TryParse(normalized[1..], out var functionKeyIndex)
            && functionKeyIndex is >= 1 and <= 24)
        {
            key = (ushort)(0x70 + functionKeyIndex - 1);
            return true;
        }

        return false;
    }
}

internal sealed record ParsedKeyGesture(IReadOnlyList<ushort> Modifiers, ushort Key, string Gesture);
