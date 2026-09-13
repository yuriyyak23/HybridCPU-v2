namespace DoomSharp.Core.Graphics;

/// <summary>
/// Patches.
/// A patch holds one or more columns.
/// Patches are used for sprites and all masked pictures,
/// and we compose textures from the TEXTURE1/2 lists
/// of patches.
/// </summary>
public sealed class Patch
{
    public Patch(ushort width, ushort height, short leftOffset, short topOffset, uint[] columnOffsets, Column?[] columns)
    {
        Width = width;
        Height = height;
        LeftOffset = leftOffset;
        TopOffset = topOffset;
        ColumnOffsets = columnOffsets;
        Columns = columns;
    }

    public ushort Width { get; }
    public ushort Height { get; }
    public short LeftOffset { get; }
    public short TopOffset { get; }
    public uint[] ColumnOffsets { get; }
    public Column?[] Columns { get; }
    public static Patch FromBytes(byte[] patchData)
    {
        var reader = new DoomSharp.Core.Data.ByteReader(patchData);

        var width = reader.ReadUInt16();
        var height = reader.ReadUInt16();
        var left = reader.ReadInt16();
        var top = reader.ReadInt16();

        var offsets = new uint[width];
        for (var i = 0; i < width; i++)
        {
            offsets[i] = reader.ReadUInt32();
        }

        var columns = new Column?[width];
        for (var i = 0; i < width; i++)
        {
            reader.Seek((int)offsets[i]);

            Column? currentColumn = null;
            var rowStart = reader.ReadByte();
            if (rowStart == 255)
            {
                columns[i] = null;
                continue;
            }

            while (rowStart != 255)
            {
                var pixelCount = reader.ReadByte();
                _ = reader.ReadByte(); // dummy value
                var pixels = reader.ReadBytes(pixelCount);
                _ = reader.ReadByte(); // dummy value

                var column = new Column(rowStart, pixelCount, pixels);
                if (currentColumn is null)
                {
                    columns[i] = column;
                }
                else
                {
                    currentColumn.Next = column;
                }
                
                currentColumn = column;
                rowStart = reader.ReadByte();
            }
        }

        return new Patch(width, height, left, top, offsets, columns);
    }

    public Column? GetColumnByOffset(uint offset, int skip = 0)
    {
        var i = 0;
        foreach (var columnOffset in ColumnOffsets)
        {
            if (columnOffset == offset)
            {
                return i + skip >= Columns.Length ? Columns[i + skip - Columns.Length] : Columns[i + skip];
            }

            i++;
        }

        return null;
    }
}
