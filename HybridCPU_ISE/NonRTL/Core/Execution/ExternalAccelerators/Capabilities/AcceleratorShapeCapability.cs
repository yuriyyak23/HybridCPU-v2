using System;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;

public sealed class AcceleratorShapeCapability
{
    public AcceleratorShapeCapability(
        string shapeKind,
        ulong minElements,
        ulong maxElements,
        byte minRank = 0,
        byte maxRank = byte.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(shapeKind))
        {
            throw new ArgumentException("Shape kind is required.", nameof(shapeKind));
        }

        if (minElements > maxElements)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minElements),
                "Minimum elements must not exceed maximum elements.");
        }

        if (minRank > maxRank)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minRank),
                "Minimum rank must not exceed maximum rank.");
        }

        ShapeKind = shapeKind;
        MinElements = minElements;
        MaxElements = maxElements;
        MinRank = minRank;
        MaxRank = maxRank;
    }

    public string ShapeKind { get; }

    public ulong MinElements { get; }

    public ulong MaxElements { get; }

    public byte MinRank { get; }

    public byte MaxRank { get; }

    public bool SupportsShape(string shapeKind, ulong elementCount, byte rank = 0)
    {
        if (string.IsNullOrWhiteSpace(shapeKind))
        {
            return false;
        }

        return string.Equals(ShapeKind, shapeKind, StringComparison.OrdinalIgnoreCase) &&
               elementCount >= MinElements &&
               elementCount <= MaxElements &&
               rank >= MinRank &&
               rank <= MaxRank;
    }
}
