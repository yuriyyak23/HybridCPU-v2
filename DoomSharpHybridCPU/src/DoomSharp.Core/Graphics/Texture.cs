namespace DoomSharp.Core.Graphics;

/// <summary>
/// A maptexturedef_t describes a rectangular texture,
///  which is composed of one or more mappatch_t structures
///  that arrange graphic patches.
/// </summary>
public sealed class Texture
{
    public Texture(string name, short width, short height, short patchCount)
    {
        Name = name;
        Width = width;
        Height = height;
        PatchCount = patchCount;
        Patches = new TexturePatch[patchCount];
    }

    public string Name { get; }
    public short Width { get; }
    public short Height { get; }
    public short PatchCount { get; }
    public TexturePatch[] Patches { get; }
}

/// <summary>
/// A single patch from a texture definition,
///  basically a rectangular area within
///  the texture rectangle.
/// </summary>
public sealed class TexturePatch
{
    public TexturePatch(int originX, int originY, int patch)
    {
        OriginX = originX;
        OriginY = originY;
        Patch = patch;
    }

    public int OriginX { get; }
    public int OriginY { get; }
    public int Patch { get; }
}
