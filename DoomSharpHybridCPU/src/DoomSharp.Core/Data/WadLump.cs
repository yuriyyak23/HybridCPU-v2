namespace DoomSharp.Core.Data;

public sealed class WadLump
{
    public WadLump(WadFile file, WadFile.FileLump lump)
    {
        File = file;
        Lump = lump;
    }

    public WadFile File { get; }
    public WadFile.FileLump Lump { get; }
    public byte[]? Data { get; set; }
    public PurgeTag Tag { get; set; } = PurgeTag.Cache;
}
