using System;
using System.Collections.Generic;
using System.Linq;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;

public sealed class AcceleratorOperationCapability
{
    public AcceleratorOperationCapability(
        string operationKind,
        IReadOnlyList<string> supportedDatatypes,
        IReadOnlyList<AcceleratorShapeCapability>? supportedShapes = null)
    {
        if (string.IsNullOrWhiteSpace(operationKind))
        {
            throw new ArgumentException("Operation kind is required.", nameof(operationKind));
        }

        ArgumentNullException.ThrowIfNull(supportedDatatypes);

        OperationKind = operationKind;
        SupportedDatatypes = supportedDatatypes
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        SupportedShapes = (supportedShapes ?? Array.Empty<AcceleratorShapeCapability>()).ToArray();
    }

    public string OperationKind { get; }

    public IReadOnlyList<string> SupportedDatatypes { get; }

    public IReadOnlyList<AcceleratorShapeCapability> SupportedShapes { get; }

    public bool SupportsDatatype(string datatype)
    {
        if (string.IsNullOrWhiteSpace(datatype))
        {
            return false;
        }

        return SupportedDatatypes.Contains(datatype, StringComparer.OrdinalIgnoreCase);
    }

    public bool SupportsShape(string shapeKind, ulong elementCount, byte rank = 0)
    {
        return SupportedShapes.Any(shape => shape.SupportsShape(shapeKind, elementCount, rank));
    }
}
