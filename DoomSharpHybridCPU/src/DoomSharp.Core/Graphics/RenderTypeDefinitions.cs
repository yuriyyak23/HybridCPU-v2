using DoomSharp.Core.Data;
using DoomSharp.Core.GameLogic;

namespace DoomSharp.Core.Graphics;

/// <summary>
/// Your plain vanilla vertex.
/// Note: transformed values not buffered locally,
///  like some DOOM-alikes ("wt", "WebView") did.
/// </summary>
public sealed class Vertex
{
    public Vertex(Fixed x, Fixed y)
    {
        X = x;
        Y = y;
    }

    public Fixed X { get; }
    public Fixed Y { get; }
}

public sealed class Sector
{
    public Sector(Fixed floorHeight, Fixed ceilingHeight, short floorPic, short ceilingPic, short lightLevel, short tag)
    {
        FloorHeight = floorHeight;
        CeilingHeight = ceilingHeight;
        FloorPic = floorPic;
        CeilingPic = ceilingPic;
        LightLevel = lightLevel;
        Tag = tag;
    }

    public short Special { get; set; }

    // 0 = untraversed, 1,2 = sndlines -1


    // mapblock bounding box for height changes
    public int[] BlockBox { get; } = new int[4];


    // if == validcount, already checked
    public int ValidCount { get; set; }

    // list of mobjs in sector
    public MapObject? ThingList { get; set; }

    // thinker_t for reversable actions
    public Thinker? SpecialData { get; set; }

    public int LineCount { get; set; }
    
    public Line[] Lines { get; set; } = Array.Empty<Line>();
    
    public short FloorPic { get; set; }
    public Fixed FloorHeight { get; set; }
    public Fixed CeilingHeight { get; set; }
    public short LightLevel { get; set; }
    public short CeilingPic { get; set; }
    public short Tag { get; set; }

    public static Sector ReadFromWadData(ByteReader reader)
    {
        var floorHeight = Fixed.FromInt(reader.ReadInt16());
        var ceilingHeight = Fixed.FromInt(reader.ReadInt16());
        var floorPic = reader.ReadName(8);
        var ceilingPic = reader.ReadName(8);
        var lightLevel = reader.ReadInt16();
        var special = reader.ReadInt16();
        var tag = reader.ReadInt16();

        return new Sector(
            floorHeight,
            ceilingHeight,
            (short)DoomGame.Instance.Renderer.FlatNumForName(floorPic),
            (short)DoomGame.Instance.Renderer.FlatNumForName(ceilingPic),
            lightLevel,
            tag)
        {
            Special = special
        };
    }
}

public sealed class SideDef
{
    public SideDef(Fixed textureOffset, Fixed rowOffset, Sector sector)
    {
        TextureOffset = textureOffset;
        RowOffset = rowOffset;
        Sector = sector;
    }

    public Fixed TextureOffset { get; set; }
    public Fixed RowOffset { get; }
    public Sector Sector { get; }

    public int TopTexture { get; set; }
    public int BottomTexture { get; set; }
    public int MidTexture { get; set; }

    public static SideDef ReadFromWadData(ByteReader reader, Sector[] sectors)
    {
        var textureOffset = reader.ReadInt16();
        var rowOffset = reader.ReadInt16();
        var topTexture = reader.ReadName(8);
        var bottomTexture = reader.ReadName(8);
        var midTexture = reader.ReadName(8);
        var sector = reader.ReadInt16();

        return new SideDef(
            Fixed.FromInt(textureOffset),
            Fixed.FromInt(rowOffset),
            sectors[sector]
        )
        {
            TopTexture = DoomGame.Instance.Renderer.TextureNumForName(topTexture),
            BottomTexture = DoomGame.Instance.Renderer.TextureNumForName(bottomTexture),
            MidTexture = DoomGame.Instance.Renderer.TextureNumForName(midTexture),
        };
    }
}

public enum SlopeType
{
    Horizontal,
    Vertical,
    Positive,
    Negative
}

public class Line
{
    public Line(Vertex v1, Vertex v2, short flags, short special, short tag)
    {
        V1 = v1;
        V2 = v2;
        Flags = flags;
        Special = special;
        Tag = tag;

        Dx = v2.X - v1.X;
        Dy = v2.Y - v1.Y;
    }

    // Vertices, from v1 to v2.
    public Vertex V1 { get; }
    public Vertex V2 { get; }

    // Precalculated v2 - v1 for side checking
    public Fixed Dx { get; }
    public Fixed Dy { get; }

    // Animation related
    public short Flags { get; set; }
    public short Special { get; set; }
    public short Tag { get; set; }

    // Visual appearance: SideDefs.
    //  SideNum[1] will be -1 if one sided.
    public int[] SideNum { get; } = new int[2];

    // Neat. Another bounding box, for the extent
    //  of the LineDef.
    public int[] BoundingBox { get; } = new int[4];

    // To aid move clipping.
    public SlopeType SlopeType { get; set; }

    // Front and back sector.
    // Note: redundant? Can be retrieved from SideDefs.
    public Sector? FrontSector { get; set; }
    public Sector? BackSector { get; set; }

    // if == validcount, already checked
    public int ValidCount { get; set; }

    // thinker_t for reversable actions
    public Thinker? SpecialData { get; set; }

    public static Line ReadFromWadData(ByteReader reader, Vertex[] vertices)
    {
        var v1 = reader.ReadInt16();
        var v2 = reader.ReadInt16();
        var flags = reader.ReadInt16();
        var special = reader.ReadInt16();
        var tag = reader.ReadInt16();

        return new Line(vertices[v1], vertices[v2], flags, special, tag)
        {
            SideNum =
            {
                [0] = reader.ReadInt16(),
                [1] = reader.ReadInt16()
            }
        };
    }

    /// <summary>
    /// Returns the sector next to the current one, or <c>null</c> if not two-sided line.
    /// </summary>
    public Sector? GetNextSector(Sector sector)
    {
        if ((Flags & Constants.Line.TwoSided) == 0)
        {
            return null;
        }

        return FrontSector == sector ? BackSector : FrontSector;
    }
}

/// <summary>
/// A SubSector.
/// References a Sector.
/// Basically, this is a list of LineSegs,
///  indicating the visible walls that define
///  (all or some) sides of a convex BSP leaf.
/// </summary>
public sealed class SubSector
{
    public SubSector(short numLines, short firstLine)
    {
        NumLines = numLines;
        FirstLine = firstLine;
    }

    public short NumLines { get; }
    public short FirstLine { get; }
    public Sector? Sector { get; set; }

    public static SubSector ReadFromWadData(ByteReader reader)
    {
        return new SubSector(reader.ReadInt16(), reader.ReadInt16());
    }
}

/// <summary>
/// The LineSeg
/// </summary>
public sealed class Segment
{
    public Segment(Vertex v1, Vertex v2, Fixed offset, Angle angle, SideDef sideDef, Line lineDef, Sector frontSector)
    {
        V1 = v1;
        V2 = v2;
        Offset = offset;
        Angle = angle;
        SideDef = sideDef;
        LineDef = lineDef;
        FrontSector = frontSector;
    }

    public Vertex V1 { get; }
    public Vertex V2 { get; }
    public Fixed Offset { get; }
    public Angle Angle { get; }
    public SideDef SideDef { get; }
    public Line LineDef { get; }
    public Sector FrontSector { get; }
    public Sector? BackSector { get; private set; }
    public static Segment ReadFromWadData(ByteReader reader, Vertex[] vertices, SideDef[] sides, Line[] lines)
    {
        var v1 = vertices[reader.ReadInt16()];
        var v2 = vertices[reader.ReadInt16()];

        var angle = reader.ReadInt16() << 16;
        var lineDef = lines[reader.ReadInt16()];

        var side = reader.ReadInt16();
        var sideDef = sides[lineDef.SideNum[side]];
        var offset = Fixed.FromInt(reader.ReadInt16());

        var frontSector = sideDef.Sector;
        Sector? backSector = null;
        if ((lineDef.Flags & Constants.Line.TwoSided) != 0)
        {
            backSector = sides[lineDef.SideNum[side ^ 1]].Sector;
        }

        return new Segment(v1, v2, offset, new Angle(angle), sideDef, lineDef, frontSector)
        {
            BackSector = backSector
        };
    }
}

/// <summary>
/// BSP Node
/// </summary>
public class Node : DividerLine
{
    public Node(Fixed x, Fixed y, Fixed dx, Fixed dy)
    {
        X = x;
        Y = y;
        Dx = dx;
        Dy = dy;

        BoundingBox = new int[2][];
        for (var i = 0; i < 2; i++)
        {
            BoundingBox[i] = new int[4];
        }
    }

    /// <summary>
    /// Bounding box for each child.
    /// </summary>
    public int[][] BoundingBox { get; }

    /// <summary>
    /// If NF_SUBSECTOR its a subsector.
    /// </summary>
    public int[] Children { get; } = new int[2];

    public static Node ReadFromWadData(ByteReader reader)
    {
        var x = Fixed.FromInt(reader.ReadInt16());
        var y = Fixed.FromInt(reader.ReadInt16());
        var dx = Fixed.FromInt(reader.ReadInt16());
        var dy = Fixed.FromInt(reader.ReadInt16());

        var node = new Node(x, y, dx, dy);

        for (var i = 0; i < 2; i++)
        {
            for (var j = 0; j < 4; j++)
            {
                node.BoundingBox[i][j] = reader.ReadInt16() << Constants.FracBits;
            }
        }

        for (var i = 0; i < 2; i++)
        {
            node.Children[i] = reader.ReadUInt16();
        }

        return node;
    }
}
