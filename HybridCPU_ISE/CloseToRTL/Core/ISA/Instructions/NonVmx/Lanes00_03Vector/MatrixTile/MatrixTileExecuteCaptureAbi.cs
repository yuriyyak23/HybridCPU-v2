using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;

public enum MatrixTileExecutionCaptureKind : byte
{
    None = 0,
    TileLoad = 1,
    TileStore = 2,
    Macc = 3,
    Transpose = 4,
    Fault = 5,
}

public enum MatrixTileExecutionFaultKind : byte
{
    None = 0,
    MissingMemoryContract = 1,
    MissingMemoryValidation = 2,
    InvalidMemoryShape = 3,
    MemoryRangeUnavailable = 4,
    MissingTileStateSnapshot = 5,
    MissingAccumulatorSnapshot = 6,
    MissingSemanticValidation = 7,
    InvalidSemanticContract = 8,
    ArithmeticOverflow = 9,
    UnsupportedOperation = 10,
    ArithmeticPolicyViolation = 11,
}

public readonly record struct MatrixTileCapturedMemoryWrite(
    ushort Row,
    ulong Address,
    byte[] Data)
{
    public MatrixTileCapturedMemoryWrite DeepClone() =>
        this with { Data = (byte[])Data.Clone() };
}

public readonly record struct MatrixTileCaptureIdentity(
    uint CoreId,
    int OwnerThreadId,
    uint Opcode,
    MatrixTileProjectedOperationKind OperationKind,
    ulong CaptureOrdinal,
    ulong CaptureFingerprint)
{
    public bool IsValid =>
        OwnerThreadId is >= 0 and < Processor.CPU_Core.SmtWays &&
        Opcode != 0 &&
        OperationKind != MatrixTileProjectedOperationKind.Unspecified &&
        CaptureOrdinal != 0 &&
        CaptureFingerprint != 0;
}

public readonly record struct MatrixTileTileImage(
    ushort TileId,
    MatrixTileCanonicalDescriptorAbi Descriptor,
    byte[] Data)
{
    public int PackedByteLength => Data?.Length ?? 0;

    public bool IsCanonicalPacked =>
        Descriptor.IsCanonical &&
        Data is not null &&
        Data.Length == MatrixTileExecuteCaptureAbi.GetPackedByteLength(Descriptor);

    public MatrixTileTileImage DeepClone() =>
        this with { Data = Data is null ? Array.Empty<byte>() : (byte[])Data.Clone() };

    public static MatrixTileTileImage Create(
        ushort tileId,
        MatrixTileCanonicalDescriptorAbi descriptor,
        ReadOnlySpan<byte> data)
    {
        MatrixTileExecuteCaptureAbi.ThrowIfInvalidPackedTileImage(descriptor, data.Length);
        return new MatrixTileTileImage(tileId, descriptor, data.ToArray());
    }
}

public readonly record struct MatrixTileExecutionCaptureRecord(
    MatrixTileExecutionCaptureKind CaptureKind,
    MatrixTileProjectedOperationKind OperationKind,
    string Mnemonic,
    ushort SourceTileId,
    ushort SecondaryTileId,
    ushort DestinationTileId,
    MatrixTileCanonicalDescriptorAbi TileDescriptor,
    MatrixTileCanonicalDescriptorAbi SecondaryTileDescriptor,
    MatrixTileCanonicalDescriptorAbi ResultTileDescriptor,
    MatrixTileTileImage SourceSnapshot,
    MatrixTileTileImage SecondarySnapshot,
    MatrixTileTileImage AccumulatorSnapshot,
    MatrixTileTileImage ResultImage,
    MatrixTileMemoryShapeValidationResult? MemoryValidation,
    MatrixTileSemanticValidationResult? SemanticValidation,
    MatrixTileCapturedMemoryWrite[] PendingStoreWrites,
    MatrixTileExecutionFaultKind FaultKind,
    MatrixTileMemoryFaultKind MemoryFaultKind,
    MatrixTileSemanticFaultKind SemanticFaultKind,
    MatrixTileMemoryFaultPoint FaultPoint,
    bool HasFaultPoint,
    string FaultMessage,
    bool HasFault,
    bool RequiresRetirePublication,
    bool RequiresReplayIdentity,
    bool BlocksArchitecturalSideEffectsBeforeRetire,
    bool UsesFallbackPath)
{
    public MatrixTileCaptureIdentity CaptureIdentity { get; init; }

    public MatrixTileStreamTransferRecord StreamTransfer { get; init; }

    public MatrixTileNumericPolicy? NumericPolicy { get; init; }

    public MatrixTileLayoutPolicy? LayoutPolicy { get; init; }

    public MatrixTilePolicyBoundCaptureIdentity PolicyIdentity { get; init; }

    public bool HasRetireCorrelation => CaptureIdentity.IsValid;

    public MatrixTileExecutionCaptureRecord DeepClone()
    {
        MatrixTileCapturedMemoryWrite[] writes = PendingStoreWrites is null
            ? Array.Empty<MatrixTileCapturedMemoryWrite>()
            : CloneWrites(PendingStoreWrites);

        return this with
        {
            SourceSnapshot = SourceSnapshot.DeepClone(),
            SecondarySnapshot = SecondarySnapshot.DeepClone(),
            AccumulatorSnapshot = AccumulatorSnapshot.DeepClone(),
            ResultImage = ResultImage.DeepClone(),
            PendingStoreWrites = writes,
            StreamTransfer = StreamTransfer.DeepClone()
        };
    }

    private static MatrixTileCapturedMemoryWrite[] CloneWrites(
        MatrixTileCapturedMemoryWrite[] writes)
    {
        var clone = new MatrixTileCapturedMemoryWrite[writes.Length];
        for (int i = 0; i < writes.Length; i++)
        {
            clone[i] = writes[i].DeepClone();
        }

        return clone;
    }
}

public sealed class MatrixTileArchitecturalTileRegisterFile
{
    private readonly Dictionary<(int OwnerThreadId, ushort TileId), MatrixTileTileImage> _tiles = new();

    public int TileCount => _tiles.Count;

    public void WriteTileForRuntimeSeed(
        int ownerThreadId,
        ushort tileId,
        MatrixTileCanonicalDescriptorAbi descriptor,
        ReadOnlySpan<byte> packedData)
    {
        MatrixTileTileImage image = MatrixTileTileImage.Create(tileId, descriptor, packedData);
        _tiles[(ValidateOwnerThreadId(ownerThreadId), tileId)] = image;
    }

    public bool TryCaptureSnapshot(
        int ownerThreadId,
        ushort tileId,
        MatrixTileCanonicalDescriptorAbi expectedDescriptor,
        out MatrixTileTileImage snapshot)
    {
        snapshot = default;
        if (!_tiles.TryGetValue((ValidateOwnerThreadId(ownerThreadId), tileId), out MatrixTileTileImage image))
        {
            return false;
        }

        if (!image.Descriptor.Equals(expectedDescriptor) || !image.IsCanonicalPacked)
        {
            return false;
        }

        snapshot = image.DeepClone();
        return true;
    }

    public bool TryCaptureAnySnapshot(
        int ownerThreadId,
        ushort tileId,
        out MatrixTileTileImage snapshot)
    {
        snapshot = default;
        if (!_tiles.TryGetValue(
                (ValidateOwnerThreadId(ownerThreadId), tileId),
                out MatrixTileTileImage image) ||
            !image.IsCanonicalPacked)
        {
            return false;
        }

        snapshot = image.DeepClone();
        return true;
    }

    public bool RemoveTile(int ownerThreadId, ushort tileId) =>
        _tiles.Remove((ValidateOwnerThreadId(ownerThreadId), tileId));

    public bool ContainsTile(int ownerThreadId, ushort tileId) =>
        _tiles.ContainsKey((ValidateOwnerThreadId(ownerThreadId), tileId));

    private static int ValidateOwnerThreadId(int ownerThreadId)
    {
        if (ownerThreadId is < 0 or >= Processor.CPU_Core.SmtWays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ownerThreadId),
                ownerThreadId,
                $"MTILE architectural owner must be in the range [0, {Processor.CPU_Core.SmtWays - 1}].");
        }

        return ownerThreadId;
    }
}

public static class MatrixTileExecuteCaptureAbi
{
    public const string ExecuteCaptureDecision = "ClosedMatrixTileExecuteCaptureSemantics";
    public const string TileLoadCaptureDecision = "ClosedTileLoadCaptureBuffer";
    public const string TileStoreCaptureDecision = "ClosedTileStorePendingWriteBuffer";
    public const string MaccCaptureDecision = "ClosedMatrixTileMaccCaptureResult";
    public const string TransposeCaptureDecision = "ClosedMatrixTileTransposeCaptureResult";
    public const string DeterministicExceptionCaptureDecision = "ClosedDeterministicExceptionCapture";
    public const string MemoryFaultCaptureDecision = "ClosedMemoryFaultCapture";
    public const string TileStateSnapshotDecision = "ClosedTileStateReadSnapshot";
    public const string AccumulatorSnapshotDecision = "ClosedAccumulatorReadSnapshot";
    public const string RetirePublicationDecision = "NoRetireOwnedTilePublicationOpened";
    public const string ReplayRollbackDecision = "NoReplayRollbackPublicationOpened";
    public const string BypassDecision = "NoCaptureToRetireBypassWithoutRetireOwnership";
    public const string FallbackDecision = "NoScalarVectorDotDscLane7VmxOrBackendFallbackAuthority";

    public const bool HasExecutionCaptureSemantics = true;
    public const bool HasTileLoadCaptureBuffer = true;
    public const bool HasTileStorePendingWriteBuffer = true;
    public const bool HasMaccCaptureResult = true;
    public const bool HasTransposeCaptureResult = true;
    public const bool HasDeterministicExceptionCapture = true;
    public const bool HasMemoryFaultCapture = true;
    public const bool HasTileStateReadSnapshot = true;
    public const bool HasAccumulatorReadSnapshot = true;
    public const bool HasRetirePublication = false;
    public const bool HasReplayRollbackConformance = false;
    public const bool BlocksCaptureToRetireBypass = true;
    public const bool KeepsRetirePublicationNonAuthority = true;
    public const bool KeepsReplayRollbackNonAuthority = true;
    public const bool KeepsCompilerScopeClosed = true;
    public const bool KeepsCompilerHandoffBlocked = true;
    public const bool UsesFallbackPath = false;

    public static int GetPackedByteLength(MatrixTileCanonicalDescriptorAbi descriptor)
    {
        checked
        {
            return descriptor.IsCanonical
                ? descriptor.Rows * descriptor.Columns * descriptor.ElementSizeBytes
                : 0;
        }
    }

    public static void ThrowIfInvalidPackedTileImage(
        MatrixTileCanonicalDescriptorAbi descriptor,
        int byteLength)
    {
        if (!descriptor.IsCanonical)
        {
            throw new InvalidOperationException(
                "MatrixTile packed image requires a canonical tile descriptor.");
        }

        int expectedLength = GetPackedByteLength(descriptor);
        if (byteLength != expectedLength)
        {
            throw new InvalidOperationException(
                $"MatrixTile packed image length {byteLength} does not match descriptor footprint {expectedLength}.");
        }
    }

    public static MatrixTileExecutionCaptureRecord CaptureLoad(
        string mnemonic,
        MatrixTileProjectedOperationKind operationKind,
        ushort sourceTileId,
        ushort secondaryTileId,
        ushort destinationTileId,
        MatrixTileCanonicalDescriptorAbi tileDescriptor,
        MatrixTileCanonicalDescriptorAbi secondaryTileDescriptor,
        MatrixTileCanonicalDescriptorAbi resultTileDescriptor,
        MatrixTileMemoryShapeContract? memoryContract,
        MatrixTileMemoryShapeValidationResult? memoryValidation,
        Func<ulong, int, byte[]?> readMemoryExact)
    {
        if (!memoryContract.HasValue)
        {
            return Fault(
                mnemonic,
                operationKind,
                sourceTileId,
                secondaryTileId,
                destinationTileId,
                tileDescriptor,
                secondaryTileDescriptor,
                resultTileDescriptor,
                MatrixTileExecutionFaultKind.MissingMemoryContract,
                "MTILE_LOAD execution capture requires a Phase03 memory contract.");
        }

        if (!memoryValidation.HasValue)
        {
            return Fault(
                mnemonic,
                operationKind,
                sourceTileId,
                secondaryTileId,
                destinationTileId,
                tileDescriptor,
                secondaryTileDescriptor,
                resultTileDescriptor,
                MatrixTileExecutionFaultKind.MissingMemoryValidation,
                "MTILE_LOAD execution capture requires preserved Phase03 memory validation.");
        }

        if (!memoryValidation.Value.IsMemoryShapeAbiAccepted)
        {
            return MemoryFault(
                mnemonic,
                operationKind,
                sourceTileId,
                secondaryTileId,
                destinationTileId,
                tileDescriptor,
                secondaryTileDescriptor,
                resultTileDescriptor,
                memoryValidation.Value,
                "MTILE_LOAD execution capture rejected an invalid memory shape.");
        }

        MatrixTileMemoryShapeContract contract = memoryContract.Value;
        MatrixTileMemoryShapeValidationResult validation = memoryValidation.Value;
        byte[] packed = new byte[GetPackedByteLength(contract.Descriptor)];
        if (!TryCopyMemoryRowsToPackedTile(
                contract,
                validation,
                readMemoryExact,
                packed,
                out MatrixTileMemoryFaultPoint faultPoint))
        {
            return MemoryFault(
                mnemonic,
                operationKind,
                sourceTileId,
                secondaryTileId,
                destinationTileId,
                tileDescriptor,
                secondaryTileDescriptor,
                resultTileDescriptor,
                MatrixTileMemoryShapeValidationResult.Fault(
                    MatrixTileMemoryFaultKind.PartialMemoryFault,
                    faultPoint,
                    hasFaultPoint: true),
                "MTILE_LOAD execution capture failed to read a complete tile row.");
        }

        MatrixTileTileImage resultImage =
            MatrixTileTileImage.Create(destinationTileId, resultTileDescriptor, packed);
        return Success(
            MatrixTileExecutionCaptureKind.TileLoad,
            operationKind,
            mnemonic,
            sourceTileId,
            secondaryTileId,
            destinationTileId,
            tileDescriptor,
            secondaryTileDescriptor,
            resultTileDescriptor,
            sourceSnapshot: default,
            secondarySnapshot: default,
            accumulatorSnapshot: default,
            resultImage,
            memoryValidation,
            semanticValidation: null,
            pendingStoreWrites: Array.Empty<MatrixTileCapturedMemoryWrite>());
    }

    public static MatrixTileExecutionCaptureRecord CaptureStore(
        string mnemonic,
        MatrixTileProjectedOperationKind operationKind,
        ushort sourceTileId,
        ushort secondaryTileId,
        ushort destinationTileId,
        MatrixTileCanonicalDescriptorAbi tileDescriptor,
        MatrixTileCanonicalDescriptorAbi secondaryTileDescriptor,
        MatrixTileCanonicalDescriptorAbi resultTileDescriptor,
        MatrixTileMemoryShapeContract? memoryContract,
        MatrixTileMemoryShapeValidationResult? memoryValidation,
        MatrixTileTileImage sourceSnapshot)
    {
        if (!sourceSnapshot.IsCanonicalPacked)
        {
            return Fault(
                mnemonic,
                operationKind,
                sourceTileId,
                secondaryTileId,
                destinationTileId,
                tileDescriptor,
                secondaryTileDescriptor,
                resultTileDescriptor,
                MatrixTileExecutionFaultKind.MissingTileStateSnapshot,
                "MTILE_STORE execution capture requires a tile-state source snapshot.");
        }

        if (!memoryContract.HasValue)
        {
            return Fault(
                mnemonic,
                operationKind,
                sourceTileId,
                secondaryTileId,
                destinationTileId,
                tileDescriptor,
                secondaryTileDescriptor,
                resultTileDescriptor,
                MatrixTileExecutionFaultKind.MissingMemoryContract,
                "MTILE_STORE execution capture requires a Phase03 memory contract.");
        }

        if (!memoryValidation.HasValue)
        {
            return Fault(
                mnemonic,
                operationKind,
                sourceTileId,
                secondaryTileId,
                destinationTileId,
                tileDescriptor,
                secondaryTileDescriptor,
                resultTileDescriptor,
                MatrixTileExecutionFaultKind.MissingMemoryValidation,
                "MTILE_STORE execution capture requires preserved Phase03 memory validation.");
        }

        if (!memoryValidation.Value.IsMemoryShapeAbiAccepted)
        {
            return MemoryFault(
                mnemonic,
                operationKind,
                sourceTileId,
                secondaryTileId,
                destinationTileId,
                tileDescriptor,
                secondaryTileDescriptor,
                resultTileDescriptor,
                memoryValidation.Value,
                "MTILE_STORE execution capture rejected an invalid memory shape.");
        }

        MatrixTileCapturedMemoryWrite[] writes =
            CreatePendingStoreWrites(memoryContract.Value, sourceSnapshot);

        return Success(
            MatrixTileExecutionCaptureKind.TileStore,
            operationKind,
            mnemonic,
            sourceTileId,
            secondaryTileId,
            destinationTileId,
            tileDescriptor,
            secondaryTileDescriptor,
            resultTileDescriptor,
            sourceSnapshot,
            secondarySnapshot: default,
            accumulatorSnapshot: default,
            resultImage: default,
            memoryValidation,
            semanticValidation: null,
            writes);
    }

    public static MatrixTileExecutionCaptureRecord CaptureMacc(
        string mnemonic,
        MatrixTileProjectedOperationKind operationKind,
        ushort sourceTileId,
        ushort secondaryTileId,
        ushort destinationTileId,
        MatrixTileCanonicalDescriptorAbi tileDescriptor,
        MatrixTileCanonicalDescriptorAbi secondaryTileDescriptor,
        MatrixTileCanonicalDescriptorAbi resultTileDescriptor,
        MatrixTileSemanticValidationResult? semanticValidation,
        MatrixTileMaccSemanticContract? maccContract,
        MatrixTileTileImage leftSnapshot,
        MatrixTileTileImage rightSnapshot,
        MatrixTileTileImage accumulatorSnapshot)
    {
        if (!semanticValidation.HasValue)
        {
            return Fault(
                mnemonic,
                operationKind,
                sourceTileId,
                secondaryTileId,
                destinationTileId,
                tileDescriptor,
                secondaryTileDescriptor,
                resultTileDescriptor,
                MatrixTileExecutionFaultKind.MissingSemanticValidation,
                "MTILE_MACC execution capture requires preserved Phase04 semantic validation.");
        }

        if (!semanticValidation.Value.IsSemanticAbiAccepted || !maccContract.HasValue)
        {
            return SemanticFault(
                mnemonic,
                operationKind,
                sourceTileId,
                secondaryTileId,
                destinationTileId,
                tileDescriptor,
                secondaryTileDescriptor,
                resultTileDescriptor,
                semanticValidation.Value,
                "MTILE_MACC execution capture rejected an invalid semantic contract.");
        }

        if (!leftSnapshot.IsCanonicalPacked || !rightSnapshot.IsCanonicalPacked)
        {
            return Fault(
                mnemonic,
                operationKind,
                sourceTileId,
                secondaryTileId,
                destinationTileId,
                tileDescriptor,
                secondaryTileDescriptor,
                resultTileDescriptor,
                MatrixTileExecutionFaultKind.MissingTileStateSnapshot,
                "MTILE_MACC execution capture requires left and right tile-state snapshots.");
        }

        if (!accumulatorSnapshot.IsCanonicalPacked)
        {
            return Fault(
                mnemonic,
                operationKind,
                sourceTileId,
                secondaryTileId,
                destinationTileId,
                tileDescriptor,
                secondaryTileDescriptor,
                resultTileDescriptor,
                MatrixTileExecutionFaultKind.MissingAccumulatorSnapshot,
                "MTILE_MACC execution capture requires an accumulator tile snapshot.");
        }

        if (!MatrixTileMaccArithmeticAbi.TryCompute(
                maccContract.Value,
                leftSnapshot,
                rightSnapshot,
                accumulatorSnapshot,
                out MatrixTileTileImage resultImage,
                out MatrixTileMaccArithmeticFaultKind arithmeticFault))
        {
            return Fault(
                mnemonic,
                operationKind,
                sourceTileId,
                secondaryTileId,
                destinationTileId,
                tileDescriptor,
                secondaryTileDescriptor,
                resultTileDescriptor,
                arithmeticFault == MatrixTileMaccArithmeticFaultKind.ArithmeticOverflow
                    ? MatrixTileExecutionFaultKind.ArithmeticOverflow
                    : MatrixTileExecutionFaultKind.ArithmeticPolicyViolation,
                $"MTILE_MACC execution capture failed formal arithmetic policy: {arithmeticFault}.");
        }

        return Success(
            MatrixTileExecutionCaptureKind.Macc,
            operationKind,
            mnemonic,
            sourceTileId,
            secondaryTileId,
            destinationTileId,
            tileDescriptor,
            secondaryTileDescriptor,
            resultTileDescriptor,
            leftSnapshot,
            rightSnapshot,
            accumulatorSnapshot,
            resultImage,
            memoryValidation: null,
            semanticValidation,
            pendingStoreWrites: Array.Empty<MatrixTileCapturedMemoryWrite>());
    }

    public static MatrixTileExecutionCaptureRecord CaptureTranspose(
        string mnemonic,
        MatrixTileProjectedOperationKind operationKind,
        ushort sourceTileId,
        ushort secondaryTileId,
        ushort destinationTileId,
        MatrixTileCanonicalDescriptorAbi tileDescriptor,
        MatrixTileCanonicalDescriptorAbi secondaryTileDescriptor,
        MatrixTileCanonicalDescriptorAbi resultTileDescriptor,
        MatrixTileSemanticValidationResult? semanticValidation,
        MatrixTileTransposeSemanticContract? transposeContract,
        MatrixTileTileImage sourceSnapshot)
    {
        if (!semanticValidation.HasValue)
        {
            return Fault(
                mnemonic,
                operationKind,
                sourceTileId,
                secondaryTileId,
                destinationTileId,
                tileDescriptor,
                secondaryTileDescriptor,
                resultTileDescriptor,
                MatrixTileExecutionFaultKind.MissingSemanticValidation,
                "MTRANSPOSE execution capture requires preserved Phase04 semantic validation.");
        }

        if (!semanticValidation.Value.IsSemanticAbiAccepted || !transposeContract.HasValue)
        {
            return SemanticFault(
                mnemonic,
                operationKind,
                sourceTileId,
                secondaryTileId,
                destinationTileId,
                tileDescriptor,
                secondaryTileDescriptor,
                resultTileDescriptor,
                semanticValidation.Value,
                "MTRANSPOSE execution capture rejected an invalid semantic contract.");
        }

        if (!sourceSnapshot.IsCanonicalPacked)
        {
            return Fault(
                mnemonic,
                operationKind,
                sourceTileId,
                secondaryTileId,
                destinationTileId,
                tileDescriptor,
                secondaryTileDescriptor,
                resultTileDescriptor,
                MatrixTileExecutionFaultKind.MissingTileStateSnapshot,
                "MTRANSPOSE execution capture requires a source tile-state snapshot.");
        }

        MatrixTileTileImage resultImage =
            Transpose(
                sourceSnapshot,
                destinationTileId,
                transposeContract.Value.Destination,
                transposeContract.Value.LayoutPolicy);

        return Success(
            MatrixTileExecutionCaptureKind.Transpose,
            operationKind,
            mnemonic,
            sourceTileId,
            secondaryTileId,
            destinationTileId,
            tileDescriptor,
            secondaryTileDescriptor,
            resultTileDescriptor,
            sourceSnapshot,
            secondarySnapshot: default,
            accumulatorSnapshot: default,
            resultImage,
            memoryValidation: null,
            semanticValidation,
            pendingStoreWrites: Array.Empty<MatrixTileCapturedMemoryWrite>());
    }

    private static MatrixTileExecutionCaptureRecord Success(
        MatrixTileExecutionCaptureKind captureKind,
        MatrixTileProjectedOperationKind operationKind,
        string mnemonic,
        ushort sourceTileId,
        ushort secondaryTileId,
        ushort destinationTileId,
        MatrixTileCanonicalDescriptorAbi tileDescriptor,
        MatrixTileCanonicalDescriptorAbi secondaryTileDescriptor,
        MatrixTileCanonicalDescriptorAbi resultTileDescriptor,
        MatrixTileTileImage sourceSnapshot,
        MatrixTileTileImage secondarySnapshot,
        MatrixTileTileImage accumulatorSnapshot,
        MatrixTileTileImage resultImage,
        MatrixTileMemoryShapeValidationResult? memoryValidation,
        MatrixTileSemanticValidationResult? semanticValidation,
        MatrixTileCapturedMemoryWrite[] pendingStoreWrites)
    {
        return new MatrixTileExecutionCaptureRecord(
            captureKind,
            operationKind,
            mnemonic,
            sourceTileId,
            secondaryTileId,
            destinationTileId,
            tileDescriptor,
            secondaryTileDescriptor,
            resultTileDescriptor,
            sourceSnapshot.DeepClone(),
            secondarySnapshot.DeepClone(),
            accumulatorSnapshot.DeepClone(),
            resultImage.DeepClone(),
            memoryValidation,
            semanticValidation,
            pendingStoreWrites,
            MatrixTileExecutionFaultKind.None,
            MatrixTileMemoryFaultKind.None,
            MatrixTileSemanticFaultKind.None,
            FaultPoint: default,
            HasFaultPoint: false,
            FaultMessage: string.Empty,
            HasFault: false,
            RequiresRetirePublication: true,
            RequiresReplayIdentity: true,
            BlocksArchitecturalSideEffectsBeforeRetire: true,
            UsesFallbackPath: false);
    }

    private static MatrixTileExecutionCaptureRecord Fault(
        string mnemonic,
        MatrixTileProjectedOperationKind operationKind,
        ushort sourceTileId,
        ushort secondaryTileId,
        ushort destinationTileId,
        MatrixTileCanonicalDescriptorAbi tileDescriptor,
        MatrixTileCanonicalDescriptorAbi secondaryTileDescriptor,
        MatrixTileCanonicalDescriptorAbi resultTileDescriptor,
        MatrixTileExecutionFaultKind faultKind,
        string message)
    {
        return new MatrixTileExecutionCaptureRecord(
            MatrixTileExecutionCaptureKind.Fault,
            operationKind,
            mnemonic,
            sourceTileId,
            secondaryTileId,
            destinationTileId,
            tileDescriptor,
            secondaryTileDescriptor,
            resultTileDescriptor,
            SourceSnapshot: default,
            SecondarySnapshot: default,
            AccumulatorSnapshot: default,
            ResultImage: default,
            MemoryValidation: null,
            SemanticValidation: null,
            PendingStoreWrites: Array.Empty<MatrixTileCapturedMemoryWrite>(),
            faultKind,
            MatrixTileMemoryFaultKind.None,
            MatrixTileSemanticFaultKind.None,
            FaultPoint: default,
            HasFaultPoint: false,
            message,
            HasFault: true,
            RequiresRetirePublication: true,
            RequiresReplayIdentity: true,
            BlocksArchitecturalSideEffectsBeforeRetire: true,
            UsesFallbackPath: false);
    }

    private static MatrixTileExecutionCaptureRecord MemoryFault(
        string mnemonic,
        MatrixTileProjectedOperationKind operationKind,
        ushort sourceTileId,
        ushort secondaryTileId,
        ushort destinationTileId,
        MatrixTileCanonicalDescriptorAbi tileDescriptor,
        MatrixTileCanonicalDescriptorAbi secondaryTileDescriptor,
        MatrixTileCanonicalDescriptorAbi resultTileDescriptor,
        MatrixTileMemoryShapeValidationResult memoryValidation,
        string message)
    {
        return Fault(
            mnemonic,
            operationKind,
            sourceTileId,
            secondaryTileId,
            destinationTileId,
            tileDescriptor,
            secondaryTileDescriptor,
            resultTileDescriptor,
            MatrixTileExecutionFaultKind.InvalidMemoryShape,
            message) with
        {
            MemoryValidation = memoryValidation,
            MemoryFaultKind = memoryValidation.FaultKind,
            FaultPoint = memoryValidation.FaultPoint,
            HasFaultPoint = memoryValidation.HasFaultPoint
        };
    }

    private static MatrixTileExecutionCaptureRecord SemanticFault(
        string mnemonic,
        MatrixTileProjectedOperationKind operationKind,
        ushort sourceTileId,
        ushort secondaryTileId,
        ushort destinationTileId,
        MatrixTileCanonicalDescriptorAbi tileDescriptor,
        MatrixTileCanonicalDescriptorAbi secondaryTileDescriptor,
        MatrixTileCanonicalDescriptorAbi resultTileDescriptor,
        MatrixTileSemanticValidationResult semanticValidation,
        string message)
    {
        return Fault(
            mnemonic,
            operationKind,
            sourceTileId,
            secondaryTileId,
            destinationTileId,
            tileDescriptor,
            secondaryTileDescriptor,
            resultTileDescriptor,
            MatrixTileExecutionFaultKind.InvalidSemanticContract,
            message) with
        {
            SemanticValidation = semanticValidation,
            SemanticFaultKind = semanticValidation.FaultKind
        };
    }

    private static bool TryCopyMemoryRowsToPackedTile(
        MatrixTileMemoryShapeContract contract,
        MatrixTileMemoryShapeValidationResult validation,
        Func<ulong, int, byte[]?> readMemoryExact,
        byte[] packed,
        out MatrixTileMemoryFaultPoint faultPoint)
    {
        faultPoint = default;
        MatrixTileCanonicalDescriptorAbi descriptor = contract.Descriptor;
        int rowBytes = checked((int)validation.RowByteCount);
        for (ushort row = 0; row < descriptor.Rows; row++)
        {
            ulong address = checked(contract.BaseAddress + (ulong)row * descriptor.StrideBytes);
            byte[]? rowBytesBuffer = readMemoryExact(address, rowBytes);
            if (rowBytesBuffer is null || rowBytesBuffer.Length != rowBytes)
            {
                faultPoint = MatrixTileMemoryShapeAndFaultAbi.CreatePreciseFaultPoint(
                    contract,
                    row,
                    column: 0);
                return false;
            }

            Buffer.BlockCopy(rowBytesBuffer, 0, packed, row * rowBytes, rowBytes);
        }

        return true;
    }

    private static MatrixTileCapturedMemoryWrite[] CreatePendingStoreWrites(
        MatrixTileMemoryShapeContract contract,
        MatrixTileTileImage sourceSnapshot)
    {
        MatrixTileCanonicalDescriptorAbi descriptor = contract.Descriptor;
        int rowBytes = checked(descriptor.Columns * descriptor.ElementSizeBytes);
        var writes = new MatrixTileCapturedMemoryWrite[descriptor.Rows];
        for (ushort row = 0; row < descriptor.Rows; row++)
        {
            byte[] rowData = new byte[rowBytes];
            Buffer.BlockCopy(sourceSnapshot.Data, row * rowBytes, rowData, 0, rowBytes);
            ulong address = checked(contract.BaseAddress + (ulong)row * descriptor.StrideBytes);
            writes[row] = new MatrixTileCapturedMemoryWrite(row, address, rowData);
        }

        return writes;
    }

    private static MatrixTileTileImage Transpose(
        MatrixTileTileImage source,
        ushort destinationTileId,
        MatrixTileCanonicalDescriptorAbi destinationDescriptor,
        MatrixTileLayoutPolicy layoutPolicy)
    {
        MatrixTileCanonicalDescriptorAbi sourceDescriptor = source.Descriptor;
        int elementSize = sourceDescriptor.ElementSizeBytes;
        byte[] destination = new byte[GetPackedByteLength(destinationDescriptor)];

        for (ushort row = 0; row < sourceDescriptor.Rows; row++)
        {
            for (ushort column = 0; column < sourceDescriptor.Columns; column++)
            {
                int sourceOffset = MatrixTileLayoutPolicyAbi.GetPackedOffset(
                    sourceDescriptor,
                    row,
                    column,
                    layoutPolicy.SourceAddressing);
                int destinationOffset = MatrixTileLayoutPolicyAbi.GetPackedOffset(
                    destinationDescriptor,
                    column,
                    row,
                    layoutPolicy.DestinationAddressing);
                Buffer.BlockCopy(source.Data, sourceOffset, destination, destinationOffset, elementSize);
            }
        }

        return MatrixTileTileImage.Create(destinationTileId, destinationDescriptor, destination);
    }

}
