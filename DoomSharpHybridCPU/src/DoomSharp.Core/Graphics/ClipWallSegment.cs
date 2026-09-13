namespace DoomSharp.Core.Graphics;

public sealed class ClipWallSegment
{
    public int First { get; set; }
    public int Last { get; set; }

    public void CopyFrom(ClipWallSegment source)
    {
        First = source.First;
        Last = source.Last;
    }
}
