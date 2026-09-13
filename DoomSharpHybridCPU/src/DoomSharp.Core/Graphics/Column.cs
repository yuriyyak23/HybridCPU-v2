namespace DoomSharp.Core.Graphics;

public sealed class Column
{
    public Column(byte topDelta, byte length, byte[] pixels)
        : this(topDelta, length, pixels, 0)
    {
    }

    public Column(byte topDelta, int length, byte[] pixels, int pixelOffset)
    {
        if (pixels is null)
            throw new ArgumentNullException(nameof(pixels));
        if (pixelOffset < 0 || length < 0 || pixelOffset > pixels.Length - length)
            throw new ArgumentOutOfRangeException(nameof(pixelOffset));

        TopDelta = topDelta;
        Length = length;
        Pixels = pixels;
        PixelOffset = pixelOffset;
    }

    public byte TopDelta { get; }
    public int Length { get; }
    public byte[] Pixels { get; }
    public int PixelOffset { get; }
    public Column? Next { get; set; }
}
