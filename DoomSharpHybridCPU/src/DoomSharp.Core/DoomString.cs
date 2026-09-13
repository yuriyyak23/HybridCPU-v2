namespace DoomSharp.Core;

public static class DoomString
{
    public static char AsciiUpper(char value) => value is >= 'a' and <= 'z'
        ? (char)(value - ('a' - 'A'))
        : value;

    // Save-game labels are restricted by the menu input path to the Doom font
    // plus ASCII space. Keep that domain contract explicit instead of pulling
    // the full Unicode White_Space table into the guest runtime closure.
    public static bool IsNullOrSpaces(string? value)
    {
        if (value is null || value.Length == 0)
            return true;

        for (var index = 0; index < value.Length; index++)
            if (value[index] != ' ')
                return false;

        return true;
    }

    public static string DecimalDigit(int value) => value switch
    {
        0 => "0", 1 => "1", 2 => "2", 3 => "3", 4 => "4",
        5 => "5", 6 => "6", 7 => "7", 8 => "8", 9 => "9",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string DecimalTwoDigits(int value)
    {
        if (value < 0 || value > 99)
            throw new ArgumentOutOfRangeException(nameof(value));
        return string.Concat(DecimalDigit(value / 10), DecimalDigit(value % 10));
    }

    public static string DecimalInt(int value)
    {
        long magnitude = value;
        var negative = magnitude < 0;
        if (negative) magnitude = -magnitude;

        var digits = 1;
        for (var remaining = magnitude; remaining >= 10; remaining /= 10)
            digits++;
        var chars = new char[digits + (negative ? 1 : 0)];
        var index = chars.Length - 1;
        do
        {
            chars[index--] = (char)('0' + magnitude % 10);
            magnitude /= 10;
        } while (magnitude != 0);
        if (negative) chars[0] = '-';
        return new string(chars);
    }

    public static string HexUInt(uint value)
    {
        var digits = 1;
        for (var remaining = value; remaining >= 16; remaining >>= 4)
            digits++;

        var chars = new char[digits];
        var index = digits - 1;
        do
        {
            var digit = (int)(value & 15);
            chars[index--] = (char)(digit < 10 ? '0' + digit : 'a' + digit - 10);
            value >>= 4;
        } while (value != 0);
        return new string(chars);
    }

    public static string HexInt(int value) => HexUInt(unchecked((uint)value));

    public static string AppendChar(string value, char suffix)
    {
        var chars = new char[value.Length + 1];
        for (var index = 0; index < value.Length; index++)
            chars[index] = value[index];
        chars[value.Length] = suffix;
        return new string(chars);
    }

    public static string MapLumpName(bool commercial, int episode, int map)
    {
        if (commercial)
            return string.Concat("map", DecimalTwoDigits(map));
        return string.Concat(string.Concat("E", DecimalDigit(episode), "M"), DecimalDigit(map));
    }
}
