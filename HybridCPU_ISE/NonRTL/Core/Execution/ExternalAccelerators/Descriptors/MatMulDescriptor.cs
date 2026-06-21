using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

[Flags]
public enum MatMulLayoutFlags : ushort
{
    None = 0,
    RowMajorA = 1 << 0,
    RowMajorB = 1 << 1,
    RowMajorC = 1 << 2,
    TransposeA = 1 << 3,
    TransposeB = 1 << 4
}

public readonly record struct MatMulDatatypeTriple(
    AcceleratorDatatype InputDatatype,
    AcceleratorDatatype AccumulatorDatatype,
    AcceleratorDatatype OutputDatatype);

public sealed record MatMulDescriptor
{
    public required ulong ABase { get; init; }

    public required ulong BBase { get; init; }

    public required ulong CBase { get; init; }

    public required uint M { get; init; }

    public required uint N { get; init; }

    public required uint K { get; init; }

    public required uint Lda { get; init; }

    public required uint Ldb { get; init; }

    public required uint Ldc { get; init; }

    public required uint TileM { get; init; }

    public required uint TileN { get; init; }

    public required uint TileK { get; init; }

    public required MatMulDatatypeTriple Datatypes { get; init; }

    public required MatMulLayoutFlags LayoutFlags { get; init; }

    public AcceleratorPartialCompletionPolicy PartialPolicy { get; init; } =
        AcceleratorPartialCompletionPolicy.AllOrNone;
}

public sealed record MatMulFootprint
{
    public required AcceleratorMemoryRange ARange { get; init; }

    public required AcceleratorMemoryRange BRange { get; init; }

    public required AcceleratorMemoryRange CRange { get; init; }

    public IReadOnlyList<AcceleratorMemoryRange> SourceRanges =>
        new[] { ARange, BRange };

    public IReadOnlyList<AcceleratorMemoryRange> DestinationRanges =>
        new[] { CRange };
}

public sealed record MatMulDescriptorValidationResult
{
    private MatMulDescriptorValidationResult(
        bool isValid,
        AcceleratorDescriptorFault fault,
        MatMulDescriptor? descriptor,
        MatMulFootprint? footprint,
        string message)
    {
        IsValid = isValid;
        Fault = fault;
        Descriptor = descriptor;
        Footprint = footprint;
        Message = message;
    }

    public bool IsValid { get; }

    public bool IsRejected => !IsValid;

    public AcceleratorDescriptorFault Fault { get; }

    public MatMulDescriptor? Descriptor { get; }

    public MatMulFootprint? Footprint { get; }

    public string Message { get; }

    public bool GrantsCommandSubmissionAuthority => false;

    public bool GrantsExecutionAuthority => false;

    public bool GrantsCommitAuthority => false;

    public static MatMulDescriptorValidationResult Valid(
        MatMulDescriptor descriptor,
        MatMulFootprint footprint,
        string message) =>
        new(
            isValid: true,
            AcceleratorDescriptorFault.None,
            descriptor,
            footprint,
            message);

    public static MatMulDescriptorValidationResult Reject(
        AcceleratorDescriptorFault fault,
        string message)
    {
        if (fault == AcceleratorDescriptorFault.None)
        {
            throw new ArgumentException(
                "Use Valid for accepted MatMul descriptors.",
                nameof(fault));
        }

        return new MatMulDescriptorValidationResult(
            isValid: false,
            fault,
            descriptor: null,
            footprint: null,
            message);
    }
}

public sealed class MatMulDescriptorValidator
{
    public const ulong MaxOutputElements = 4096;

    private const MatMulLayoutFlags SupportedLayoutMask =
        MatMulLayoutFlags.RowMajorA |
        MatMulLayoutFlags.RowMajorB |
        MatMulLayoutFlags.RowMajorC |
        MatMulLayoutFlags.TransposeA |
        MatMulLayoutFlags.TransposeB;

    public MatMulDescriptorValidationResult Validate(
        MatMulDescriptor descriptor,
        AcceleratorCommandDescriptor? commandDescriptor = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        MatMulDescriptorValidationResult shape = ValidateShape(descriptor);
        if (shape.IsRejected)
        {
            return shape;
        }

        MatMulDescriptorValidationResult datatypes = ValidateDatatypes(descriptor);
        if (datatypes.IsRejected)
        {
            return datatypes;
        }

        MatMulDescriptorValidationResult strides = ValidateStrides(descriptor);
        if (strides.IsRejected)
        {
            return strides;
        }

        MatMulDescriptorValidationResult footprint = NormalizeFootprints(descriptor);
        if (footprint.IsRejected)
        {
            return footprint;
        }

        if (commandDescriptor is not null)
        {
            MatMulDescriptorValidationResult binding =
                ValidateCommandDescriptorBinding(
                    descriptor,
                    footprint.Footprint!,
                    commandDescriptor);
            if (binding.IsRejected)
            {
                return binding;
            }
        }

        return footprint;
    }

    public MatMulDescriptorValidationResult ValidateShape(MatMulDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (descriptor.M == 0 || descriptor.N == 0 || descriptor.K == 0)
        {
            return MatMulDescriptorValidationResult.Reject(
                AcceleratorDescriptorFault.UnsupportedShape,
                "MatMul descriptor dimensions M, N, and K must be non-zero.");
        }

        if (descriptor.TileM == 0 ||
            descriptor.TileN == 0 ||
            descriptor.TileK == 0 ||
            descriptor.TileM > descriptor.M ||
            descriptor.TileN > descriptor.N ||
            descriptor.TileK > descriptor.K)
        {
            return MatMulDescriptorValidationResult.Reject(
                AcceleratorDescriptorFault.UnsupportedShape,
                "MatMul tile dimensions must be non-zero and must not exceed M, N, or K.");
        }

        if (!TryMul(descriptor.M, descriptor.N, out ulong outputElements) ||
            !TryMul(outputElements, descriptor.K, out _))
        {
            return MatMulDescriptorValidationResult.Reject(
                AcceleratorDescriptorFault.RangeOverflow,
                "MatMul descriptor shape overflows the v1 resource model.");
        }

        if (outputElements > MaxOutputElements)
        {
            return MatMulDescriptorValidationResult.Reject(
                AcceleratorDescriptorFault.UnsupportedShape,
                "MatMul descriptor output shape exceeds the v1 provider-supported element limit.");
        }

        return MatMulDescriptorValidationResult.Valid(
            descriptor,
            new MatMulFootprint
            {
                ARange = default,
                BRange = default,
                CRange = default
            },
            "MatMul shape accepted.");
    }

    public MatMulDescriptorValidationResult ValidateStrides(MatMulDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        MatMulLayoutFlags unknown = descriptor.LayoutFlags & ~SupportedLayoutMask;
        if (unknown != 0 ||
            !descriptor.LayoutFlags.HasFlag(MatMulLayoutFlags.RowMajorA) ||
            !descriptor.LayoutFlags.HasFlag(MatMulLayoutFlags.RowMajorB) ||
            !descriptor.LayoutFlags.HasFlag(MatMulLayoutFlags.RowMajorC))
        {
            return MatMulDescriptorValidationResult.Reject(
                AcceleratorDescriptorFault.UnsupportedShape,
                "MatMul v1 accepts explicit row-major A, B, and C layout flags only.");
        }

        if (descriptor.LayoutFlags.HasFlag(MatMulLayoutFlags.TransposeA) ||
            descriptor.LayoutFlags.HasFlag(MatMulLayoutFlags.TransposeB))
        {
            return MatMulDescriptorValidationResult.Reject(
                AcceleratorDescriptorFault.UnsupportedShape,
                "MatMul v1 descriptor validation rejects transposed layouts conservatively.");
        }

        if (descriptor.Lda < descriptor.K ||
            descriptor.Ldb < descriptor.N ||
            descriptor.Ldc < descriptor.N)
        {
            return MatMulDescriptorValidationResult.Reject(
                AcceleratorDescriptorFault.UnsupportedShape,
                "MatMul strides must cover the logical row width for row-major A, B, and C.");
        }

        ushort alignment = (ushort)Math.Max(
            DatatypeSizeBytes(descriptor.Datatypes.InputDatatype),
            DatatypeSizeBytes(descriptor.Datatypes.OutputDatatype));
        if (!IsAligned(descriptor.ABase, alignment) ||
            !IsAligned(descriptor.BBase, alignment) ||
            !IsAligned(descriptor.CBase, alignment))
        {
            return MatMulDescriptorValidationResult.Reject(
                AcceleratorDescriptorFault.AlignmentFault,
                "MatMul base addresses must satisfy datatype alignment.");
        }

        return MatMulDescriptorValidationResult.Valid(
            descriptor,
            new MatMulFootprint
            {
                ARange = default,
                BRange = default,
                CRange = default
            },
            "MatMul strides accepted.");
    }

    public MatMulDescriptorValidationResult ValidateDatatypes(MatMulDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (descriptor.PartialPolicy != AcceleratorPartialCompletionPolicy.AllOrNone)
        {
            return MatMulDescriptorValidationResult.Reject(
                AcceleratorDescriptorFault.UnsupportedPartialCompletionPolicy,
                "MatMul v1 requires AllOrNone partial completion policy.");
        }

        MatMulDatatypeTriple triple = descriptor.Datatypes;
        bool supported =
            triple is { InputDatatype: AcceleratorDatatype.Float32, AccumulatorDatatype: AcceleratorDatatype.Float32, OutputDatatype: AcceleratorDatatype.Float32 } ||
            triple is { InputDatatype: AcceleratorDatatype.Float64, AccumulatorDatatype: AcceleratorDatatype.Float64, OutputDatatype: AcceleratorDatatype.Float64 } ||
            triple is { InputDatatype: AcceleratorDatatype.Int32, AccumulatorDatatype: AcceleratorDatatype.Int32, OutputDatatype: AcceleratorDatatype.Int32 };
        if (!supported)
        {
            return MatMulDescriptorValidationResult.Reject(
                AcceleratorDescriptorFault.UnsupportedDatatype,
                "MatMul v1 supports only homogeneous f32, f64, or int32 datatype triples.");
        }

        return MatMulDescriptorValidationResult.Valid(
            descriptor,
            new MatMulFootprint
            {
                ARange = default,
                BRange = default,
                CRange = default
            },
            "MatMul datatype triple accepted.");
    }

    public MatMulDescriptorValidationResult NormalizeFootprints(MatMulDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        ushort inputBytes = DatatypeSizeBytes(descriptor.Datatypes.InputDatatype);
        ushort outputBytes = DatatypeSizeBytes(descriptor.Datatypes.OutputDatatype);
        if (!TryMatrixSpanBytes(descriptor.M, descriptor.K, descriptor.Lda, inputBytes, out ulong aBytes) ||
            !TryMatrixSpanBytes(descriptor.K, descriptor.N, descriptor.Ldb, inputBytes, out ulong bBytes) ||
            !TryMatrixSpanBytes(descriptor.M, descriptor.N, descriptor.Ldc, outputBytes, out ulong cBytes))
        {
            return MatMulDescriptorValidationResult.Reject(
                AcceleratorDescriptorFault.RangeOverflow,
                "MatMul descriptor footprint overflows UInt64.");
        }

        if (!IsRangeValid(descriptor.ABase, aBytes) ||
            !IsRangeValid(descriptor.BBase, bBytes) ||
            !IsRangeValid(descriptor.CBase, cBytes))
        {
            return MatMulDescriptorValidationResult.Reject(
                AcceleratorDescriptorFault.RangeOverflow,
                "MatMul descriptor base plus footprint length overflows UInt64.");
        }

        var footprint = new MatMulFootprint
        {
            ARange = new AcceleratorMemoryRange(descriptor.ABase, aBytes),
            BRange = new AcceleratorMemoryRange(descriptor.BBase, bBytes),
            CRange = new AcceleratorMemoryRange(descriptor.CBase, cBytes)
        };

        if (HasAmbiguousAliases(footprint, out string aliasMessage))
        {
            return MatMulDescriptorValidationResult.Reject(
                AcceleratorDescriptorFault.AliasAmbiguousFootprint,
                aliasMessage);
        }

        return MatMulDescriptorValidationResult.Valid(
            descriptor,
            footprint,
            "MatMul descriptor footprints normalized as non-authoritative read/write evidence.");
    }

    internal static ushort DatatypeSizeBytes(AcceleratorDatatype datatype) =>
        datatype switch
        {
            AcceleratorDatatype.Float32 => sizeof(float),
            AcceleratorDatatype.Float64 => sizeof(double),
            AcceleratorDatatype.Int32 => sizeof(int),
            _ => 0
        };

    private static MatMulDescriptorValidationResult ValidateCommandDescriptorBinding(
        MatMulDescriptor descriptor,
        MatMulFootprint footprint,
        AcceleratorCommandDescriptor commandDescriptor)
    {
        if (!MatMulCapabilityProvider.Matches(commandDescriptor))
        {
            return MatMulDescriptorValidationResult.Reject(
                AcceleratorDescriptorFault.UnsupportedOperation,
                "MatMul typed descriptor is not bound to a ReferenceMatMul L7-SDC command descriptor.");
        }

        if (!commandDescriptor.OwnerGuardDecision.IsAllowed)
        {
            return MatMulDescriptorValidationResult.Reject(
                AcceleratorDescriptorFault.OwnerDomainFault,
                "MatMul typed descriptor requires the carrier command descriptor to be owner/domain guard-backed.");
        }

        if (commandDescriptor.PartialCompletionPolicy != descriptor.PartialPolicy)
        {
            return MatMulDescriptorValidationResult.Reject(
                AcceleratorDescriptorFault.UnsupportedPartialCompletionPolicy,
                "MatMul typed descriptor partial policy does not match the command descriptor.");
        }

        if (commandDescriptor.Datatype != descriptor.Datatypes.OutputDatatype)
        {
            return MatMulDescriptorValidationResult.Reject(
                AcceleratorDescriptorFault.UnsupportedDatatype,
                "MatMul command descriptor datatype must match the typed output datatype.");
        }

        if (commandDescriptor.Shape != AcceleratorShapeKind.Matrix2D ||
            commandDescriptor.ShapeRank != 2)
        {
            return MatMulDescriptorValidationResult.Reject(
                AcceleratorDescriptorFault.UnsupportedShape,
                "MatMul command descriptor must carry a rank-2 matrix shape.");
        }

        if (!TryMul(descriptor.M, descriptor.N, out ulong outputElements) ||
            commandDescriptor.ElementCount != outputElements)
        {
            return MatMulDescriptorValidationResult.Reject(
                AcceleratorDescriptorFault.UnsupportedShape,
                "MatMul command descriptor element count must match M*N from the typed descriptor.");
        }

        if (!RangesEqual(
                AcceleratorDescriptorParser.NormalizeMemoryRanges(footprint.SourceRanges),
                commandDescriptor.NormalizedFootprint.SourceRanges) ||
            !RangesEqual(
                AcceleratorDescriptorParser.NormalizeMemoryRanges(footprint.DestinationRanges),
                commandDescriptor.NormalizedFootprint.DestinationRanges))
        {
            return MatMulDescriptorValidationResult.Reject(
                AcceleratorDescriptorFault.NormalizedFootprintHashMismatch,
                "MatMul typed descriptor footprints must match the guard-accepted L7-SDC command descriptor footprint.");
        }

        return MatMulDescriptorValidationResult.Valid(
            descriptor,
            footprint,
            "MatMul typed descriptor is bound to the guard-accepted L7-SDC command descriptor.");
    }

    private static bool TryMatrixSpanBytes(
        uint rows,
        uint cols,
        uint stride,
        ushort elementSize,
        out ulong bytes)
    {
        bytes = 0;
        if (rows == 0 || cols == 0 || stride < cols || elementSize == 0)
        {
            return false;
        }

        ulong logicalElements = ((ulong)(rows - 1) * stride) + cols;
        if (!TryMul(logicalElements, elementSize, out bytes))
        {
            return false;
        }

        return bytes != 0;
    }

    private static bool TryMul(ulong left, ulong right, out ulong result)
    {
        result = 0;
        if (left != 0 && right > ulong.MaxValue / left)
        {
            return false;
        }

        result = left * right;
        return true;
    }

    private static bool IsRangeValid(ulong address, ulong length) =>
        length != 0 && address <= ulong.MaxValue - length;

    private static bool IsAligned(ulong address, ushort alignment) =>
        alignment != 0 && (address % alignment) == 0;

    private static bool HasAmbiguousAliases(
        MatMulFootprint footprint,
        out string message)
    {
        if (RangesOverlap(footprint.ARange, footprint.BRange))
        {
            message = "MatMul descriptor rejects overlapping A and B source footprints conservatively.";
            return true;
        }

        if (RangesOverlap(footprint.ARange, footprint.CRange))
        {
            message = "MatMul descriptor rejects overlapping A source and C destination footprints conservatively.";
            return true;
        }

        if (RangesOverlap(footprint.BRange, footprint.CRange))
        {
            message = "MatMul descriptor rejects overlapping B source and C destination footprints conservatively.";
            return true;
        }

        message = string.Empty;
        return false;
    }

    private static bool RangesOverlap(
        AcceleratorMemoryRange left,
        AcceleratorMemoryRange right)
    {
        if (left.Length == 0 ||
            right.Length == 0 ||
            left.Address > ulong.MaxValue - left.Length ||
            right.Address > ulong.MaxValue - right.Length)
        {
            return false;
        }

        ulong leftEnd = left.Address + left.Length;
        ulong rightEnd = right.Address + right.Length;
        return left.Address < rightEnd && right.Address < leftEnd;
    }

    private static bool RangesEqual(
        IReadOnlyList<AcceleratorMemoryRange> left,
        IReadOnlyList<AcceleratorMemoryRange> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Count; index++)
        {
            if (!left[index].Equals(right[index]))
            {
                return false;
            }
        }

        return true;
    }
}
