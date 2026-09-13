using DoomSharp.Core.Data;

namespace DoomSharp.Core.Graphics;

/// <summary>
/// A DOOM wall texture is a list of patches
/// which are to be combined in a predefined order.
/// </summary>
public sealed class MapTexture
{
    public MapTexture(string name, bool masked, short width, short height, short patchCount, int columnDirectory = 0)
    {
        Name = name;
        Masked = masked;
        Width = width;
        Height = height;
        PatchCount = patchCount;
        ColumnDirectory = columnDirectory;
        Patches = new MapPatch[patchCount];
    }

    public string Name { get; }
    public bool Masked { get; }
    public short Width { get; }
    public short Height { get; }
    public short PatchCount { get; }
    public int ColumnDirectory { get; }
    public MapPatch[] Patches { get; }

    public const int BaseSize = 8 + 4 + 2 + 2 + 2 + 4;
    public int Size => BaseSize + PatchCount * MapPatch.Size;

    public void ReadMapPatches(byte[] data, int offset = 0)
    {
        var reader = new DoomSharp.Core.Data.ByteReader(data, offset);

        // Read map patches
        for (var i = 0; i < PatchCount; i++)
        {
            Patches[i] = MapPatch.FromReader(reader);
        }
    }

    public static MapTexture FromBytes(byte[] data, int offset = 0)
    {
        var reader = new DoomSharp.Core.Data.ByteReader(data, offset);

        var name = reader.ReadName(8);
        var masked = reader.ReadInt32() != 0;
        var width = reader.ReadInt16();
        var height = reader.ReadInt16();
        var columnDirectory = reader.ReadInt32();
        var patchCount = reader.ReadInt16();

        return new MapTexture(name, masked, width, height, patchCount, columnDirectory);
    }
}

/// <summary>
/// Each texture is composed of one or more patches,
/// with patches being lumps stored in the WAD.
/// The lumps are referenced by number, and patched
/// into the rectangular texture space using origin
/// and possibly other attributes.
/// </summary>
public sealed class MapPatch
{
    public MapPatch(short originX, short originY, short patch, short stepDir = 0, short colorMap = 0)
    {
        OriginX = originX;
        OriginY = originY;
        Patch = patch;
        StepDir = stepDir;
        ColorMap = colorMap;
    }

    public short OriginX { get; }
    public short OriginY { get; }
    public short Patch { get; }
    public short StepDir { get; }
    public short ColorMap { get; }

    public const int Size = 2 + 2 + 2 + 2 + 2;

    public static MapPatch FromReader(ByteReader reader)
    {
        var originX = reader.ReadInt16();
        var originY = reader.ReadInt16();
        var patch = reader.ReadInt16();
        var stepDir = reader.ReadInt16();
        var colorMap = reader.ReadInt16();

        return new MapPatch(originX, originY, patch, stepDir, colorMap);
    }
}
