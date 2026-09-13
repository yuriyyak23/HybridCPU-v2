using DoomSharp.Core.Data;
namespace DoomSharp.Core.GameLogic;

/// <summary>
/// Thing definition, position, orientation and type,
/// plus skill/visibility flags and attributes.
/// </summary>
public sealed class MapThing
{
    public const int SizeOfStruct = 2 + 2 + 2 + 2 + 2;

    public MapThing(short x = 0, short y = 0, short angle = 0, short type = 0, short options = 0)
    {
        X = x;
        Y = y;
        Angle = angle;
        Type = type;
        Options = options;
    }

    public short X { get; }
    public short Y { get; }
    public short Angle { get; }
    public short Type { get; }
    public short Options { get; }

    public MapThing WithType(short type) => new(X, Y, Angle, type, Options);

    public static MapThing FromWadData(ByteReader reader)
    {
        return new MapThing(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(),
            reader.ReadInt16(), reader.ReadInt16());
    }
}
