namespace DoomSharp.Core.Data;

public sealed class WadFormatException : FormatException
{
    public WadFormatException(string message)
        : base(message)
    {
    }
}
