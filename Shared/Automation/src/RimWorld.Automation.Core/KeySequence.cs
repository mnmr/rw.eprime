using System;
using System.Collections.Generic;

namespace RimWorld.Automation.Core;

[Flags]
public enum KeyModifiers { None = 0, Shift = 1, Control = 2, Alt = 4 }

public readonly struct KeyStroke
{
    public readonly string Key;
    public readonly char Character;
    public readonly KeyModifiers Modifiers;
    public KeyStroke(string key, char character, KeyModifiers modifiers)
    { Key = key; Character = character; Modifiers = modifiers; }
}

// Converts the existing action scripts' SendKeys notation into detached key strokes.
// Parsing completes before dispatch so malformed input cannot partially type a command.
public static class KeySequence
{
    public static KeyStroke[] Parse(string text)
    {
        if (text == null || text.Length > 8192) throw new FormatException("Key sequence is missing or too long.");
        var result = new List<KeyStroke>();
        int index = 0;
        ParseGroup(text, ref index, KeyModifiers.None, false, 0, result);
        return result.ToArray();
    }

    private static void ParseGroup(string text, ref int index, KeyModifiers inherited,
        bool grouped, int depth, List<KeyStroke> result)
    {
        if (depth > 16) throw new FormatException("Too many nested key groups.");
        while (index < text.Length)
        {
            if (text[index] == ')')
            {
                if (!grouped) throw new FormatException("Unmatched closing parenthesis.");
                index++;
                return;
            }
            KeyModifiers modifiers = inherited;
            while (index < text.Length && (text[index] == '^' || text[index] == '+' || text[index] == '%'))
                modifiers |= text[index++] switch { '^' => KeyModifiers.Control, '+' => KeyModifiers.Shift, _ => KeyModifiers.Alt };
            if (index == text.Length) throw new FormatException("Modifier has no following key.");
            char character = text[index++];
            if (character == '(')
            {
                ParseGroup(text, ref index, modifiers, true, depth + 1, result);
                continue;
            }
            string key = "None";
            int count = 1;
            if (character == '{')
            {
                // SendKeys escapes braces as '{{}' and '{}}'.
                if (index + 1 < text.Length && (text[index] == '{' || text[index] == '}') && text[index + 1] == '}')
                { character = text[index]; index += 2; }
                else
                {
                    int end = text.IndexOf('}', index);
                    if (end < 0) throw new FormatException("Unclosed key name.");
                    string[] parts = text.Substring(index, end - index).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 1 || parts.Length > 2 || (parts.Length == 2 && (!int.TryParse(parts[1], out count) || count < 1)))
                        throw new FormatException("Invalid key repetition.");
                    if (parts[0].Length == 1) character = parts[0][0];
                    else { key = NamedKey(parts[0]); character = key == "Space" ? ' ' : '\0'; }
                    index = end + 1;
                }
            }
            else if (character == '~') { key = "Return"; character = '\0'; }
            else if (character == ')' || character == '}') throw new FormatException("Unmatched key delimiter.");
            if (character != '\0')
            {
                if (character >= 'A' && character <= 'Z') modifiers |= KeyModifiers.Shift;
                char lower = char.ToLowerInvariant(character);
                if ((modifiers & KeyModifiers.Shift) != 0 && lower >= 'a' && lower <= 'z')
                    character = char.ToUpperInvariant(character);
                if (lower >= 'a' && lower <= 'z') key = char.ToUpperInvariant(lower).ToString();
                else if (lower >= '0' && lower <= '9') key = "Alpha" + lower;
                else key = lower switch { ' ' => "Space", '[' => "LeftBracket", ']' => "RightBracket", '.' => "Period", ',' => "Comma", '-' => "Minus", '=' => "Equals", '/' => "Slash", '\\' => "Backslash", ';' => "Semicolon", '\'' => "Quote", '`' => "BackQuote", _ => "None" };
            }
            if (count > 8192 - result.Count) throw new FormatException("Key sequence exceeds 8192 strokes.");
            for (int i = 0; i < count; i++) result.Add(new KeyStroke(key, character, modifiers));
        }
        if (grouped) throw new FormatException("Unclosed key group.");
    }

    private static string NamedKey(string name)
    {
        string upper = name.ToUpperInvariant();
        if (upper[0] == 'F' && int.TryParse(upper.Substring(1), out int f) && f >= 1 && f <= 15) return "F" + f;
        return upper switch
        {
            "ENTER" or "RETURN" => "Return", "ESC" or "ESCAPE" => "Escape",
            "BS" or "BKSP" or "BACKSPACE" => "Backspace", "DEL" or "DELETE" => "Delete",
            "TAB" => "Tab", "HOME" => "Home", "END" => "End", "PGUP" => "PageUp", "PGDN" => "PageDown",
            "LEFT" => "LeftArrow", "RIGHT" => "RightArrow", "UP" => "UpArrow", "DOWN" => "DownArrow",
            "INSERT" or "INS" => "Insert", "SPACE" => "Space",
            _ => throw new FormatException("Unsupported key: " + name)
        };
    }
}
