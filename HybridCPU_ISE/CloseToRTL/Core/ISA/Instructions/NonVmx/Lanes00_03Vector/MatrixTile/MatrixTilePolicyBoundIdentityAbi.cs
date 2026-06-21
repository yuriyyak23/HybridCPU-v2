using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;

public readonly record struct MatrixTilePolicyBoundCaptureIdentity(
    ushort AbiVersion,
    int OwnerThreadId,
    int MemoryDomainId,
    uint Opcode,
    MatrixTileProjectedOperationKind OperationKind,
    ushort NumericPolicyAbiVersion,
    ulong NumericPolicyFingerprint,
    ushort LayoutPolicyAbiVersion,
    ulong LayoutPolicyFingerprint,
    ulong DescriptorFingerprint,
    MatrixTileRuntimeResourceClass ResourceClass,
    SlotClass SlotClass,
    byte EligibleLaneMask,
    byte PhysicalLaneId,
    ulong SourceSnapshotFingerprint,
    ulong SecondarySnapshotFingerprint,
    ulong AccumulatorSnapshotFingerprint,
    ulong FootprintBytes,
    ulong ReplayEpoch,
    ulong DependencyFingerprint,
    MatrixTileRetirePublicationKind PublicationSurface,
    ulong IdentityFingerprint)
{
    public bool IsValid =>
        AbiVersion == MatrixTilePolicyBoundIdentityAbi.CurrentAbiVersion &&
        OwnerThreadId is >= 0 and < Processor.CPU_Core.SmtWays &&
        MemoryDomainId == OwnerThreadId &&
        Opcode != 0 &&
        OperationKind != MatrixTileProjectedOperationKind.Unspecified &&
        DescriptorFingerprint != 0 &&
        ResourceClass != MatrixTileRuntimeResourceClass.None &&
        SlotClass != SlotClass.Unclassified &&
        EligibleLaneMask != 0 &&
        FootprintBytes != 0 &&
        ReplayEpoch != 0 &&
        DependencyFingerprint != 0 &&
        PublicationSurface != MatrixTileRetirePublicationKind.None &&
        IdentityFingerprint != 0 &&
        (OperationKind != MatrixTileProjectedOperationKind.Macc ||
            (NumericPolicyAbiVersion == MatrixTileNumericPolicyAbi.CurrentAbiVersion &&
             NumericPolicyFingerprint != 0 &&
             LayoutPolicyAbiVersion == MatrixTileLayoutPolicyAbi.CurrentAbiVersion &&
             LayoutPolicyFingerprint != 0)) &&
        (OperationKind != MatrixTileProjectedOperationKind.Transpose ||
            (LayoutPolicyAbiVersion == MatrixTileLayoutPolicyAbi.CurrentAbiVersion &&
             LayoutPolicyFingerprint != 0));
}

public static class MatrixTilePolicyBoundIdentityAbi
{
    public const ushort CurrentAbiVersion = 1;
    public const string CaptureIdentityDecision =
        "PolicyDescriptorResourceDependencyEpochAndPublicationBoundCaptureIdentity";
    public const string ReplayIdentityDecision =
        "NumericAndLayoutPolicyFingerprintsAreInseparableFromReplayIdentity";
    public const string EpochDecision =
        "CoreOwnedMatrixTileReplayInvalidationEpoch";
    public const bool UsesExistingRetirePublication = true;
    public const bool UsesExistingReplayRollbackJournal = true;
    public const bool UsesSrfOrHostEvidenceAsAuthority = false;

    public static MatrixTileExecutionCaptureRecord Bind(
        in MatrixTileExecutionCaptureRecord capture,
        in MatrixTileMaterializedInstruction materializedInstruction,
        in MatrixTileMicroOpDependencyMetadata dependency,
        int ownerThreadId,
        int memoryDomainId,
        ulong replayEpoch)
    {
        MatrixTileNumericPolicy? numericPolicy =
            materializedInstruction.Projection.MaccContract?.NumericPolicy;
        MatrixTileLayoutPolicy? layoutPolicy =
            materializedInstruction.Projection.MaccContract?.LayoutPolicy ??
            materializedInstruction.Projection.TransposeContract?.LayoutPolicy;
        MatrixTileRuntimeResourceClass resourceClass =
            MatrixTileResourceContour.Classify((uint)materializedInstruction.Opcode);
        SlotClass slotClass = MatrixTileResourceContour.ResolveSlotClass(resourceClass);
        byte laneMask = SlotClassLaneMap.GetLaneMask(slotClass);
        byte physicalLaneId = capture.StreamTransfer.TransferFingerprint != 0
            ? capture.StreamTransfer.PhysicalLaneId
            : byte.MaxValue;
        ulong descriptorFootprintBytes = checked((ulong)
            MatrixTileExecuteCaptureAbi.GetPackedByteLength(
                materializedInstruction.ResultTileDescriptor));
        ulong memoryFootprintBytes = capture.MemoryValidation.HasValue
            ? capture.MemoryValidation.Value.TotalByteFootprint
            : 0;
        ulong footprintBytes = memoryFootprintBytes != 0
            ? memoryFootprintBytes
            : descriptorFootprintBytes;

        MatrixTilePolicyBoundCaptureIdentity identity = new(
            CurrentAbiVersion,
            ownerThreadId,
            memoryDomainId,
            (uint)materializedInstruction.Opcode,
            materializedInstruction.OperationKind,
            numericPolicy?.AbiVersion ?? 0,
            numericPolicy?.Fingerprint ?? 0,
            layoutPolicy?.AbiVersion ?? 0,
            layoutPolicy?.Fingerprint ?? 0,
            ComputeDescriptorFingerprint(materializedInstruction),
            resourceClass,
            slotClass,
            laneMask,
            physicalLaneId,
            ComputeImageFingerprint(capture.SourceSnapshot),
            ComputeImageFingerprint(capture.SecondarySnapshot),
            ComputeImageFingerprint(capture.AccumulatorSnapshot),
            footprintBytes,
            replayEpoch,
            ComputeDependencyFingerprint(dependency),
            GetPublicationSurface(materializedInstruction.OperationKind),
            IdentityFingerprint: 0);
        identity = identity with
        {
            IdentityFingerprint = ComputeIdentityFingerprint(identity)
        };

        return capture with
        {
            NumericPolicy = numericPolicy,
            LayoutPolicy = layoutPolicy,
            PolicyIdentity = identity
        };
    }

    public static bool ValidateCapture(
        in MatrixTileExecutionCaptureRecord capture,
        ulong currentReplayEpoch)
    {
        MatrixTilePolicyBoundCaptureIdentity identity = capture.PolicyIdentity;
        if (!identity.IsValid ||
            identity.ReplayEpoch != currentReplayEpoch ||
            identity.IdentityFingerprint != ComputeIdentityFingerprint(identity) ||
            identity.OperationKind != capture.OperationKind ||
            identity.DescriptorFingerprint != ComputeDescriptorFingerprint(capture) ||
            identity.SourceSnapshotFingerprint != ComputeImageFingerprint(capture.SourceSnapshot) ||
            identity.SecondarySnapshotFingerprint != ComputeImageFingerprint(capture.SecondarySnapshot) ||
            identity.AccumulatorSnapshotFingerprint != ComputeImageFingerprint(capture.AccumulatorSnapshot) ||
            identity.PublicationSurface != GetPublicationSurface(capture.OperationKind))
        {
            return false;
        }

        if (capture.OperationKind == MatrixTileProjectedOperationKind.Macc)
        {
            if (!capture.NumericPolicy.HasValue ||
                !capture.LayoutPolicy.HasValue ||
                !MatrixTileNumericPolicyAbi.Validate(capture.NumericPolicy).IsValid ||
                !MatrixTileLayoutPolicyAbi.Validate(
                    capture.LayoutPolicy,
                    MatrixTileProjectedOperationKind.Macc).IsValid ||
                capture.NumericPolicy.Value.AbiVersion != identity.NumericPolicyAbiVersion ||
                capture.NumericPolicy.Value.Fingerprint != identity.NumericPolicyFingerprint ||
                capture.LayoutPolicy.Value.AbiVersion != identity.LayoutPolicyAbiVersion ||
                capture.LayoutPolicy.Value.Fingerprint != identity.LayoutPolicyFingerprint)
            {
                return false;
            }
        }
        else if (capture.OperationKind == MatrixTileProjectedOperationKind.Transpose)
        {
            if (capture.NumericPolicy.HasValue ||
                !capture.LayoutPolicy.HasValue ||
                !MatrixTileLayoutPolicyAbi.Validate(
                    capture.LayoutPolicy,
                    MatrixTileProjectedOperationKind.Transpose).IsValid ||
                capture.LayoutPolicy.Value.AbiVersion != identity.LayoutPolicyAbiVersion ||
                capture.LayoutPolicy.Value.Fingerprint != identity.LayoutPolicyFingerprint)
            {
                return false;
            }
        }
        else if (capture.NumericPolicy.HasValue || capture.LayoutPolicy.HasValue)
        {
            return false;
        }

        return true;
    }

    public static bool MatchesMaterializedInstruction(
        in MatrixTilePolicyBoundCaptureIdentity identity,
        in MatrixTileMaterializedInstruction materializedInstruction)
    {
        MatrixTileNumericPolicy? numericPolicy =
            materializedInstruction.Projection.MaccContract?.NumericPolicy;
        MatrixTileLayoutPolicy? layoutPolicy =
            materializedInstruction.Projection.MaccContract?.LayoutPolicy ??
            materializedInstruction.Projection.TransposeContract?.LayoutPolicy;
        MatrixTileRuntimeResourceClass resourceClass =
            MatrixTileResourceContour.Classify((uint)materializedInstruction.Opcode);
        SlotClass slotClass = MatrixTileResourceContour.ResolveSlotClass(resourceClass);

        return identity.Opcode == (uint)materializedInstruction.Opcode &&
               identity.OperationKind == materializedInstruction.OperationKind &&
               identity.NumericPolicyAbiVersion == (numericPolicy?.AbiVersion ?? 0) &&
               identity.NumericPolicyFingerprint == (numericPolicy?.Fingerprint ?? 0) &&
               identity.LayoutPolicyAbiVersion == (layoutPolicy?.AbiVersion ?? 0) &&
               identity.LayoutPolicyFingerprint == (layoutPolicy?.Fingerprint ?? 0) &&
               identity.DescriptorFingerprint ==
                   ComputeDescriptorFingerprint(materializedInstruction) &&
               identity.ResourceClass == resourceClass &&
               identity.SlotClass == slotClass &&
               identity.EligibleLaneMask == SlotClassLaneMap.GetLaneMask(slotClass) &&
               identity.PublicationSurface ==
                   GetPublicationSurface(materializedInstruction.OperationKind);
    }

    public static ulong ComputeDependencyFingerprint(
        in MatrixTileMicroOpDependencyMetadata dependency)
    {
        ulong hash = BeginHash();
        AddByte(ref hash, (byte)dependency.OperationKind);
        AddDescriptor(ref hash, dependency.TileDescriptor);
        AddDescriptor(ref hash, dependency.SecondaryTileDescriptor);
        AddDescriptor(ref hash, dependency.ResultTileDescriptor);
        AddBool(ref hash, dependency.HasTileMemoryDependencyMetadata);
        AddBool(ref hash, dependency.HasTileRegisterDependencyMetadata);
        AddBool(ref hash, dependency.HasAccumulatorDependencyMetadata);
        AddBool(ref hash, dependency.HasTransposePolicyDependencyMetadata);
        AddBool(ref hash, dependency.ReadsTileState);
        AddBool(ref hash, dependency.WritesTileState);
        AddBool(ref hash, dependency.ReadsAccumulator);
        AddBool(ref hash, dependency.WritesAccumulator);
        AddUInt16(ref hash, dependency.SourceTileId);
        AddUInt16(ref hash, dependency.SecondaryTileId);
        AddUInt16(ref hash, dependency.DestinationTileId);
        return FinishHash(hash);
    }

    public static ulong ComputeDependencyFingerprint(
        in MatrixTileMaterializedInstruction materializedInstruction) =>
        ComputeDependencyFingerprint(
            CreateDependencyMetadata(materializedInstruction.Projection));

    public static ulong ComputeDescriptorFingerprint(
        in MatrixTileMaterializedInstruction materializedInstruction)
    {
        ulong hash = BeginHash();
        AddDescriptor(ref hash, materializedInstruction.TileDescriptor);
        AddDescriptor(ref hash, materializedInstruction.SecondaryTileDescriptor);
        AddDescriptor(ref hash, materializedInstruction.ResultTileDescriptor);
        return FinishHash(hash);
    }

    public static ulong ComputeDescriptorFingerprint(
        in MatrixTileExecutionCaptureRecord capture)
    {
        ulong hash = BeginHash();
        AddDescriptor(ref hash, capture.TileDescriptor);
        AddDescriptor(ref hash, capture.SecondaryTileDescriptor);
        AddDescriptor(ref hash, capture.ResultTileDescriptor);
        return FinishHash(hash);
    }

    public static ulong ComputeImageFingerprint(MatrixTileTileImage image)
    {
        if (!image.IsCanonicalPacked)
        {
            return 0;
        }

        ulong hash = BeginHash();
        AddUInt16(ref hash, image.TileId);
        AddDescriptor(ref hash, image.Descriptor);
        for (int index = 0; index < image.Data.Length; index++)
        {
            AddByte(ref hash, image.Data[index]);
        }

        return FinishHash(hash);
    }

    public static ulong ComputeIdentityFingerprint(
        MatrixTilePolicyBoundCaptureIdentity identity)
    {
        ulong hash = BeginHash();
        AddUInt16(ref hash, identity.AbiVersion);
        AddUInt32(ref hash, unchecked((uint)identity.OwnerThreadId));
        AddUInt32(ref hash, unchecked((uint)identity.MemoryDomainId));
        AddUInt32(ref hash, identity.Opcode);
        AddByte(ref hash, (byte)identity.OperationKind);
        AddUInt16(ref hash, identity.NumericPolicyAbiVersion);
        AddUInt64(ref hash, identity.NumericPolicyFingerprint);
        AddUInt16(ref hash, identity.LayoutPolicyAbiVersion);
        AddUInt64(ref hash, identity.LayoutPolicyFingerprint);
        AddUInt64(ref hash, identity.DescriptorFingerprint);
        AddByte(ref hash, (byte)identity.ResourceClass);
        AddByte(ref hash, (byte)identity.SlotClass);
        AddByte(ref hash, identity.EligibleLaneMask);
        AddByte(ref hash, identity.PhysicalLaneId);
        AddUInt64(ref hash, identity.SourceSnapshotFingerprint);
        AddUInt64(ref hash, identity.SecondarySnapshotFingerprint);
        AddUInt64(ref hash, identity.AccumulatorSnapshotFingerprint);
        AddUInt64(ref hash, identity.FootprintBytes);
        AddUInt64(ref hash, identity.ReplayEpoch);
        AddUInt64(ref hash, identity.DependencyFingerprint);
        AddByte(ref hash, (byte)identity.PublicationSurface);
        return FinishHash(hash);
    }

    private static MatrixTileRetirePublicationKind GetPublicationSurface(
        MatrixTileProjectedOperationKind operationKind) =>
        operationKind switch
        {
            MatrixTileProjectedOperationKind.Load =>
                MatrixTileRetirePublicationKind.TileState,
            MatrixTileProjectedOperationKind.Store =>
                MatrixTileRetirePublicationKind.MemoryStore,
            MatrixTileProjectedOperationKind.Macc =>
                MatrixTileRetirePublicationKind.Accumulator,
            MatrixTileProjectedOperationKind.Transpose =>
                MatrixTileRetirePublicationKind.TileState,
            _ => MatrixTileRetirePublicationKind.None,
        };

    private static MatrixTileMicroOpDependencyMetadata CreateDependencyMetadata(
        in MatrixTileInstructionIrProjection projection)
    {
        VectorInstructionPayload payload = projection.SourcePayload;
        ushort sourceTileId = GetTileId(payload.PrimaryPointer);
        ushort secondaryTileId = GetTileId(payload.SecondaryPointer);
        ushort destinationTileId = projection.OperationKind == MatrixTileProjectedOperationKind.Macc
            ? GetHighTileIdOrDefault(payload.SecondaryPointer, secondaryTileId)
            : projection.TransposeContract?.DestinationTileId ?? secondaryTileId;

        return projection.OperationKind switch
        {
            MatrixTileProjectedOperationKind.Load => new MatrixTileMicroOpDependencyMetadata(
                projection.OperationKind,
                projection.TileDescriptor,
                projection.SecondaryTileDescriptor,
                projection.ResultTileDescriptor,
                HasTileMemoryDependencyMetadata: true,
                HasTileRegisterDependencyMetadata: true,
                HasAccumulatorDependencyMetadata: false,
                HasTransposePolicyDependencyMetadata: false,
                ReadsTileState: false,
                WritesTileState: true,
                ReadsAccumulator: false,
                WritesAccumulator: false,
                SourceTileId: 0,
                SecondaryTileId: secondaryTileId,
                DestinationTileId: destinationTileId),

            MatrixTileProjectedOperationKind.Store => new MatrixTileMicroOpDependencyMetadata(
                projection.OperationKind,
                projection.TileDescriptor,
                projection.SecondaryTileDescriptor,
                projection.ResultTileDescriptor,
                HasTileMemoryDependencyMetadata: true,
                HasTileRegisterDependencyMetadata: true,
                HasAccumulatorDependencyMetadata: false,
                HasTransposePolicyDependencyMetadata: false,
                ReadsTileState: true,
                WritesTileState: false,
                ReadsAccumulator: false,
                WritesAccumulator: false,
                SourceTileId: secondaryTileId,
                SecondaryTileId: secondaryTileId,
                DestinationTileId: destinationTileId),

            MatrixTileProjectedOperationKind.Macc => new MatrixTileMicroOpDependencyMetadata(
                projection.OperationKind,
                projection.TileDescriptor,
                projection.SecondaryTileDescriptor,
                projection.ResultTileDescriptor,
                HasTileMemoryDependencyMetadata: false,
                HasTileRegisterDependencyMetadata: true,
                HasAccumulatorDependencyMetadata: true,
                HasTransposePolicyDependencyMetadata: false,
                ReadsTileState: true,
                WritesTileState: true,
                ReadsAccumulator: true,
                WritesAccumulator: true,
                SourceTileId: sourceTileId,
                SecondaryTileId: secondaryTileId,
                DestinationTileId: destinationTileId),

            MatrixTileProjectedOperationKind.Transpose => new MatrixTileMicroOpDependencyMetadata(
                projection.OperationKind,
                projection.TileDescriptor,
                projection.SecondaryTileDescriptor,
                projection.ResultTileDescriptor,
                HasTileMemoryDependencyMetadata: false,
                HasTileRegisterDependencyMetadata: true,
                HasAccumulatorDependencyMetadata: false,
                HasTransposePolicyDependencyMetadata: true,
                ReadsTileState: true,
                WritesTileState: true,
                ReadsAccumulator: false,
                WritesAccumulator: false,
                SourceTileId: projection.TransposeContract?.SourceTileId ?? sourceTileId,
                SecondaryTileId: secondaryTileId,
                DestinationTileId: destinationTileId),

            _ => default
        };
    }

    private static ushort GetTileId(ulong pointer) =>
        checked((ushort)(pointer & 0xFFFF));

    private static ushort GetHighTileIdOrDefault(
        ulong pointer,
        ushort fallback)
    {
        ulong high = (pointer >> 16) & 0xFFFF;
        return high == 0 ? fallback : checked((ushort)high);
    }

    private static ulong BeginHash() => 14695981039346656037UL;

    private static ulong FinishHash(ulong hash) => hash == 0 ? 1UL : hash;

    private static void AddBool(ref ulong hash, bool value) =>
        AddByte(ref hash, value ? (byte)1 : (byte)0);

    private static void AddByte(ref ulong hash, byte value)
    {
        hash ^= value;
        hash *= 1099511628211UL;
    }

    private static void AddUInt16(ref ulong hash, ushort value)
    {
        AddByte(ref hash, (byte)value);
        AddByte(ref hash, (byte)(value >> 8));
    }

    private static void AddUInt32(ref ulong hash, uint value)
    {
        for (int shift = 0; shift < 32; shift += 8)
        {
            AddByte(ref hash, (byte)(value >> shift));
        }
    }

    private static void AddUInt64(ref ulong hash, ulong value)
    {
        for (int shift = 0; shift < 64; shift += 8)
        {
            AddByte(ref hash, (byte)(value >> shift));
        }
    }

    private static void AddDescriptor(
        ref ulong hash,
        MatrixTileCanonicalDescriptorAbi descriptor)
    {
        AddUInt16(ref hash, descriptor.Rows);
        AddUInt16(ref hash, descriptor.Columns);
        AddUInt16(ref hash, descriptor.ElementSizeBytes);
        AddUInt32(ref hash, descriptor.StrideBytes);
        AddByte(ref hash, (byte)descriptor.Layout);
    }
}
