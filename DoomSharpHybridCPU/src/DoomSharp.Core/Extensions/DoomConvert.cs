namespace DoomSharp.Core.Extensions;

public static class DoomConvert
{
    public static short ToInt16(byte[] data)
    {
        if (data.Length != 4)
        {
            return 0;
        }

        return (short)((data[1] << 8) + data[0]);
    }

    public static int ToInt32(byte[] data)
    {
        if (data.Length != 4)
        {
            return 0;
        }

        return ToInt32(data, 0);
    }

    public static int ToInt32(byte[] data, int offset)
    {
        return (data[offset + 3] << 24) + (data[offset + 2] << 16)
            + (data[offset + 1] << 8) + data[offset];
    }
}