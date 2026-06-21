using System;

namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;

public enum MatrixTileRetirePublicationKind : byte
{
    None = 0,
    TileState = 1,
    MemoryStore = 2,
    Accumulator = 3,
    Fault = 4,
}

public enum MatrixTileRetireFaultKind : byte
{
    None = 0,
    CapturedExecutionFault = 1,
    MemoryCommitFault = 2,
}

public readonly record struct MatrixTileRetireOutcome(
    MatrixTileCaptureIdentity CaptureIdentity,
    MatrixTileRetirePublicationKind PublicationKind,
    MatrixTileRetireFaultKind RetireFaultKind,
    MatrixTileExecutionFaultKind ExecutionFaultKind,
    MatrixTileMemoryFaultKind MemoryFaultKind,
    MatrixTileSemanticFaultKind SemanticFaultKind,
    MatrixTileMemoryFaultPoint FaultPoint,
    bool HasFaultPoint,
    string Message,
    bool PublishedArchitecturalState,
    bool CommittedMemory,
    bool FaultRetired)
{
    public bool IsSuccess =>
        !FaultRetired &&
        RetireFaultKind == MatrixTileRetireFaultKind.None;

    public static MatrixTileRetireOutcome Published(
        MatrixTileCaptureIdentity captureIdentity,
        MatrixTileRetirePublicationKind publicationKind) =>
        new(
            captureIdentity,
            publicationKind,
            MatrixTileRetireFaultKind.None,
            MatrixTileExecutionFaultKind.None,
            MatrixTileMemoryFaultKind.None,
            MatrixTileSemanticFaultKind.None,
            FaultPoint: default,
            HasFaultPoint: false,
            Message: string.Empty,
            PublishedArchitecturalState:
                publicationKind is MatrixTileRetirePublicationKind.TileState
                    or MatrixTileRetirePublicationKind.Accumulator,
            CommittedMemory: publicationKind == MatrixTileRetirePublicationKind.MemoryStore,
            FaultRetired: false);

    public static MatrixTileRetireOutcome Fault(
        in MatrixTileExecutionCaptureRecord capture,
        MatrixTileRetireFaultKind retireFaultKind,
        string message) =>
        new(
            capture.CaptureIdentity,
            MatrixTileRetirePublicationKind.Fault,
            retireFaultKind,
            capture.FaultKind,
            capture.MemoryFaultKind,
            capture.SemanticFaultKind,
            capture.FaultPoint,
            capture.HasFaultPoint,
            message,
            PublishedArchitecturalState: false,
            CommittedMemory: false,
            FaultRetired: true);
}

public sealed class MatrixTileRetireValidationException : InvalidOperationException
{
    public MatrixTileRetireValidationException(string message)
        : base(message)
    {
    }
}

public sealed class MatrixTileRetireFaultException : InvalidOperationException
{
    public MatrixTileRetireFaultException(MatrixTileRetireOutcome outcome)
        : base(outcome.Message)
    {
        Outcome = outcome;
    }

    public MatrixTileRetireOutcome Outcome { get; }
}

public static class MatrixTileRetirePublicationAbi
{
    public const string RetirePublicationDecision = "ClosedMatrixTileRetirePublicationAndCommit";
    public const string LoadPublicationDecision = "RetireOwnedTileLoadPublication";
    public const string StoreCommitDecision = "RetireOwnedAllOrNoneTileStoreCommit";
    public const string MaccPublicationDecision = "RetireOwnedAccumulatorPublication";
    public const string TransposePublicationDecision = "RetireOwnedTransposeDestinationPublication";
    public const string FaultRetirementDecision = "DeterministicFaultRetirementWithoutPartialPublication";
    public const string WritebackOwnershipDecision = "MatrixTileMicroOpWriteBackOwnsCaptureConsumption";
    public const string SideEffectOrderingDecision = "ValidateThenPublishSingleArchitecturalEffectAtRetire";
    public const string ArchitecturalVisibilityDecision = "CaptureInvisibleUntilRetirePublication";
    public const string DuplicateRetireDecision = "DuplicateCancelledAndMismatchedRetireFailClosed";
    public const string CorrelationDecision = "CoreOwnerOpcodeOperationAndCaptureOrdinalCorrelated";
    public const string FallbackDecision = "NoScalarVectorBaseMemoryDotDscLane7VmxBackendOrCompilerFallback";

    public const bool HasRetirePublicationAndCommit = true;
    public const bool HasTileLoadRetirePublication = true;
    public const bool HasTileStoreRetireCommit = true;
    public const bool HasAccumulatorRetirePublication = true;
    public const bool HasTransposeRetirePublication = true;
    public const bool HasFaultRetirementPolicy = true;
    public const bool HasWritebackOwnership = true;
    public const bool HasSideEffectPublicationOrdering = true;
    public const bool HasArchitecturalStateVisibilityRules = true;
    public const bool HasCaptureToRetireCorrelation = true;
    public const bool RejectsDuplicateRetire = true;
    public const bool RejectsCancelledRetire = true;
    public const bool BlocksExecuteCaptureToRetireBypass = true;
    public const bool KeepsHostOwnedEvidenceNonArchitectural = true;
    public const bool KeepsReplayRollbackNonAuthority = true;
    public const bool KeepsCompilerScopeClosed = true;
    public const bool UsesFallbackPath = false;

    internal static MatrixTileCaptureIdentity CreateCaptureIdentity(
        uint coreId,
        int ownerThreadId,
        uint opcode,
        MatrixTileProjectedOperationKind operationKind,
        ulong captureOrdinal,
        in MatrixTileExecutionCaptureRecord capture)
    {
        MatrixTilePolicyBoundCaptureIdentity policyIdentity = capture.PolicyIdentity;
        if (!policyIdentity.IsValid ||
            policyIdentity.IdentityFingerprint !=
                MatrixTilePolicyBoundIdentityAbi.ComputeIdentityFingerprint(policyIdentity))
        {
            throw Validation(
                "MTILE capture identity creation requires a policy-bound Phase17 capture envelope.");
        }

        return new MatrixTileCaptureIdentity(
            coreId,
            ownerThreadId,
            opcode,
            operationKind,
            captureOrdinal,
            ComputeCaptureFingerprint(capture));
    }

    public static MatrixTileRetireOutcome Retire(
        ref Processor.CPU_Core core,
        in MatrixTileExecutionCaptureRecord capture,
        MatrixTileCaptureIdentity expectedIdentity)
    {
        ValidateForRetire(core, capture, expectedIdentity);

        if (capture.HasFault)
        {
            return MatrixTileRetireOutcome.Fault(
                capture,
                MatrixTileRetireFaultKind.CapturedExecutionFault,
                CreateFaultRetirementMessage(capture));
        }

        return capture.OperationKind switch
        {
            MatrixTileProjectedOperationKind.Load =>
                RetireTilePublication(
                    ref core,
                    capture,
                    MatrixTileExecutionCaptureKind.TileLoad,
                    MatrixTileRetirePublicationKind.TileState),
            MatrixTileProjectedOperationKind.Store =>
                RetireStore(ref core, capture),
            MatrixTileProjectedOperationKind.Macc =>
                RetireTilePublication(
                    ref core,
                    capture,
                    MatrixTileExecutionCaptureKind.Macc,
                    MatrixTileRetirePublicationKind.Accumulator),
            MatrixTileProjectedOperationKind.Transpose =>
                RetireTilePublication(
                    ref core,
                    capture,
                    MatrixTileExecutionCaptureKind.Transpose,
                    MatrixTileRetirePublicationKind.TileState),
            _ => throw Validation(
                $"MTILE retire rejected unsupported operation kind {capture.OperationKind}.")
        };
    }

    internal static void ValidateForRetire(
        in Processor.CPU_Core core,
        in MatrixTileExecutionCaptureRecord capture,
        MatrixTileCaptureIdentity expectedIdentity)
    {
        ValidateCorrelation(core, capture, expectedIdentity);
        if (!MatrixTilePolicyBoundIdentityAbi.ValidateCapture(
                capture,
                core.GetMatrixTileReplayInvalidationEpoch()))
        {
            throw Validation(
                "MTILE retire rejected a capture whose policy-bound identity envelope no longer matches runtime state.");
        }

        ValidateEnvelope(capture);
        MatrixTileStreamTransferAbi.ValidateCapture(capture);

        if (capture.HasFault)
        {
            ValidateFaultCapture(capture);
            return;
        }

        switch (capture.OperationKind)
        {
            case MatrixTileProjectedOperationKind.Load:
                ValidateTilePublicationCapture(
                    capture,
                    MatrixTileExecutionCaptureKind.TileLoad);
                if (!capture.MemoryValidation.HasValue ||
                    !capture.MemoryValidation.Value.IsValid)
                {
                    throw Validation(
                        "MTILE_LOAD retire requires preserved valid memory-shape validation.");
                }

                break;

            case MatrixTileProjectedOperationKind.Store:
                ValidateStoreCapture(capture);
                ValidateStoreWrites(capture);
                break;

            case MatrixTileProjectedOperationKind.Macc:
                ValidateTilePublicationCapture(
                    capture,
                    MatrixTileExecutionCaptureKind.Macc);
                if (!capture.SemanticValidation.HasValue ||
                    !capture.SemanticValidation.Value.IsValid ||
                    !capture.AccumulatorSnapshot.IsCanonicalPacked)
                {
                    throw Validation(
                        "MTILE_MACC retire requires a valid semantic result and accumulator snapshot.");
                }

                break;

            case MatrixTileProjectedOperationKind.Transpose:
                ValidateTilePublicationCapture(
                    capture,
                    MatrixTileExecutionCaptureKind.Transpose);
                if (!capture.SemanticValidation.HasValue ||
                    !capture.SemanticValidation.Value.IsValid ||
                    !capture.SourceSnapshot.IsCanonicalPacked)
                {
                    throw Validation(
                        "MTRANSPOSE retire requires a valid semantic result and source snapshot.");
                }

                break;

            default:
                throw Validation(
                    $"MTILE retire rejected unsupported operation kind {capture.OperationKind}.");
        }
    }

    private static MatrixTileRetireOutcome RetireTilePublication(
        ref Processor.CPU_Core core,
        in MatrixTileExecutionCaptureRecord capture,
        MatrixTileExecutionCaptureKind expectedCaptureKind,
        MatrixTileRetirePublicationKind publicationKind)
    {
        ValidateTilePublicationCapture(capture, expectedCaptureKind);

        core.PublishRetiredMatrixTile(
            capture.CaptureIdentity.OwnerThreadId,
            capture.ResultImage);
        return MatrixTileRetireOutcome.Published(
            capture.CaptureIdentity,
            publicationKind);
    }

    private static MatrixTileRetireOutcome RetireStore(
        ref Processor.CPU_Core core,
        in MatrixTileExecutionCaptureRecord capture)
    {
        ValidateStoreCapture(capture);
        if (!core.TryCommitRetiredMatrixTileStoreAllOrNone(
                capture.PendingStoreWrites,
                out string failureMessage))
        {
            return MatrixTileRetireOutcome.Fault(
                capture,
                MatrixTileRetireFaultKind.MemoryCommitFault,
                failureMessage);
        }

        return MatrixTileRetireOutcome.Published(
            capture.CaptureIdentity,
            MatrixTileRetirePublicationKind.MemoryStore);
    }

    private static void ValidateTilePublicationCapture(
        in MatrixTileExecutionCaptureRecord capture,
        MatrixTileExecutionCaptureKind expectedCaptureKind)
    {
        if (capture.CaptureKind != expectedCaptureKind ||
            !capture.ResultImage.IsCanonicalPacked ||
            capture.ResultImage.TileId != capture.DestinationTileId ||
            !capture.ResultImage.Descriptor.Equals(capture.ResultTileDescriptor) ||
            capture.PendingStoreWrites is not { Length: 0 })
        {
            throw Validation(
                $"{capture.Mnemonic} retire rejected a malformed staged tile publication.");
        }
    }

    private static void ValidateStoreCapture(
        in MatrixTileExecutionCaptureRecord capture)
    {
        if (capture.CaptureKind != MatrixTileExecutionCaptureKind.TileStore ||
            !capture.SourceSnapshot.IsCanonicalPacked ||
            capture.SourceSnapshot.TileId != capture.SourceTileId ||
            !capture.SourceSnapshot.Descriptor.Equals(capture.TileDescriptor) ||
            !capture.MemoryValidation.HasValue ||
            !capture.MemoryValidation.Value.IsValid)
        {
            throw Validation(
                "MTILE_STORE retire rejected a malformed staged store capture.");
        }
    }

    private static void ValidateCorrelation(
        in Processor.CPU_Core core,
        in MatrixTileExecutionCaptureRecord capture,
        MatrixTileCaptureIdentity expectedIdentity)
    {
        if (!expectedIdentity.IsValid ||
            !capture.HasRetireCorrelation ||
            capture.CaptureIdentity != expectedIdentity)
        {
            throw Validation(
                "MTILE retire rejected a missing or mismatched capture correlation identity.");
        }

        if (capture.CaptureIdentity.CaptureFingerprint !=
            ComputeCaptureFingerprint(capture))
        {
            throw Validation(
                "MTILE retire rejected a capture whose staged payload no longer matches its execute correlation.");
        }

        if (capture.CaptureIdentity.CoreId != core.CoreID)
        {
            throw Validation(
                $"MTILE retire capture belongs to core {capture.CaptureIdentity.CoreId} but retire owner is core {core.CoreID}.");
        }

        MatrixTilePolicyBoundCaptureIdentity policyIdentity = capture.PolicyIdentity;
        if (policyIdentity.OwnerThreadId != capture.CaptureIdentity.OwnerThreadId ||
            policyIdentity.Opcode != capture.CaptureIdentity.Opcode ||
            policyIdentity.OperationKind != capture.CaptureIdentity.OperationKind)
        {
            throw Validation(
                "MTILE retire rejected a capture whose policy-bound owner/opcode identity diverged from capture correlation.");
        }

        uint expectedOpcode = GetOpcode(capture.OperationKind);
        string expectedMnemonic = GetMnemonic(capture.OperationKind);
        if (capture.CaptureIdentity.Opcode != expectedOpcode ||
            capture.CaptureIdentity.OperationKind != capture.OperationKind ||
            !string.Equals(capture.Mnemonic, expectedMnemonic, StringComparison.Ordinal))
        {
            throw Validation(
                "MTILE retire rejected a wrong-instruction capture correlation.");
        }
    }

    private static void ValidateEnvelope(in MatrixTileExecutionCaptureRecord capture)
    {
        if (!capture.RequiresRetirePublication ||
            !capture.BlocksArchitecturalSideEffectsBeforeRetire ||
            capture.UsesFallbackPath)
        {
            throw Validation(
                $"{capture.Mnemonic} retire requires a validated Phase09 capture with retire-owned publication and no fallback path.");
        }

        if (capture.HasFault !=
            (capture.CaptureKind == MatrixTileExecutionCaptureKind.Fault))
        {
            throw Validation(
                $"{capture.Mnemonic} retire rejected an inconsistent success/fault capture envelope.");
        }
    }

    private static void ValidateFaultCapture(in MatrixTileExecutionCaptureRecord capture)
    {
        if (capture.FaultKind == MatrixTileExecutionFaultKind.None ||
            capture.ResultImage.IsCanonicalPacked ||
            capture.PendingStoreWrites is not { Length: 0 })
        {
            throw Validation(
                $"{capture.Mnemonic} retire rejected a fault capture carrying publishable side effects.");
        }
    }

    private static void ValidateStoreWrites(in MatrixTileExecutionCaptureRecord capture)
    {
        MatrixTileCapturedMemoryWrite[]? writes = capture.PendingStoreWrites;
        if (writes == null || writes.Length != capture.TileDescriptor.Rows)
        {
            throw Validation(
                "MTILE_STORE retire requires exactly one staged write per tile row.");
        }

        int rowByteLength = checked(
            capture.TileDescriptor.Columns *
            capture.TileDescriptor.ElementSizeBytes);
        ulong firstByteAddress = capture.MemoryValidation!.Value.FirstByteAddress;

        for (int index = 0; index < writes.Length; index++)
        {
            MatrixTileCapturedMemoryWrite write = writes[index];
            ulong expectedAddress = checked(
                firstByteAddress +
                ((ulong)index * capture.TileDescriptor.StrideBytes));
            if (write.Row != index ||
                write.Address != expectedAddress ||
                write.Data == null ||
                write.Data.Length != rowByteLength)
            {
                throw Validation(
                    $"MTILE_STORE retire rejected malformed staged row {index}.");
            }
        }
    }

    private static string CreateFaultRetirementMessage(
        in MatrixTileExecutionCaptureRecord capture)
    {
        string message = string.IsNullOrWhiteSpace(capture.FaultMessage)
            ? "captured runtime fault"
            : capture.FaultMessage;
        return $"{capture.Mnemonic} retired fault without architectural publication: {message}";
    }

    private static ulong ComputeCaptureFingerprint(
        in MatrixTileExecutionCaptureRecord capture)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offsetBasis;

        static void AddByte(ref ulong current, byte value)
        {
            current ^= value;
            current *= prime;
        }

        static void AddUInt16(ref ulong current, ushort value)
        {
            AddByte(ref current, (byte)value);
            AddByte(ref current, (byte)(value >> 8));
        }

        static void AddUInt32(ref ulong current, uint value)
        {
            for (int shift = 0; shift < 32; shift += 8)
            {
                AddByte(ref current, (byte)(value >> shift));
            }
        }

        static void AddUInt64(ref ulong current, ulong value)
        {
            for (int shift = 0; shift < 64; shift += 8)
            {
                AddByte(ref current, (byte)(value >> shift));
            }
        }

        static void AddString(ref ulong current, string? value)
        {
            if (value == null)
            {
                AddByte(ref current, 0xFF);
                return;
            }

            foreach (char character in value)
            {
                AddUInt16(ref current, character);
            }

            AddByte(ref current, 0);
        }

        static void AddDescriptor(
            ref ulong current,
            MatrixTileCanonicalDescriptorAbi descriptor)
        {
            AddUInt16(ref current, descriptor.Rows);
            AddUInt16(ref current, descriptor.Columns);
            AddUInt16(ref current, descriptor.ElementSizeBytes);
            AddUInt32(ref current, descriptor.StrideBytes);
            AddByte(ref current, (byte)descriptor.Layout);
        }

        static void AddImage(ref ulong current, MatrixTileTileImage image)
        {
            AddUInt16(ref current, image.TileId);
            AddDescriptor(ref current, image.Descriptor);
            byte[]? data = image.Data;
            AddUInt32(ref current, checked((uint)(data?.Length ?? 0)));
            if (data == null)
            {
                return;
            }

            for (int index = 0; index < data.Length; index++)
            {
                AddByte(ref current, data[index]);
            }
        }

        static void AddNumericPolicy(
            ref ulong current,
            MatrixTileNumericPolicy? policy)
        {
            AddByte(ref current, policy.HasValue ? (byte)1 : (byte)0);
            if (!policy.HasValue)
            {
                return;
            }

            MatrixTileNumericPolicy value = policy.Value;
            AddUInt16(ref current, value.AbiVersion);
            AddByte(ref current, (byte)value.ProfileId);
            AddUInt32(ref current, unchecked((uint)value.ElementType));
            AddUInt32(ref current, unchecked((uint)value.AccumulatorType));
            AddUInt32(ref current, unchecked((uint)value.PublishFormat));
            AddByte(ref current, (byte)value.Signedness);
            AddByte(ref current, (byte)value.WideningRule);
            AddByte(ref current, (byte)value.MultiplyRule);
            AddByte(ref current, (byte)value.AddRule);
            AddByte(ref current, (byte)value.RoundingMode);
            AddByte(ref current, (byte)value.SaturationMode);
            AddByte(ref current, (byte)value.OverflowMode);
            AddByte(ref current, (byte)value.NaNPolicy);
            AddByte(ref current, (byte)value.InfinityPolicy);
            AddByte(ref current, (byte)value.DenormalPolicy);
            AddByte(ref current, (byte)value.ReproducibilityMode);
            AddByte(ref current, (byte)value.ExceptionPolicy);
            AddUInt64(ref current, value.Fingerprint);
        }

        static void AddLayoutPolicy(
            ref ulong current,
            MatrixTileLayoutPolicy? policy)
        {
            AddByte(ref current, policy.HasValue ? (byte)1 : (byte)0);
            if (!policy.HasValue)
            {
                return;
            }

            MatrixTileLayoutPolicy value = policy.Value;
            AddUInt16(ref current, value.AbiVersion);
            AddByte(ref current, (byte)value.ProfileId);
            AddByte(ref current, (byte)value.OperationKind);
            AddByte(ref current, (byte)value.SourceAddressing);
            AddByte(ref current, (byte)value.SecondaryAddressing);
            AddByte(ref current, (byte)value.DestinationAddressing);
            AddByte(ref current, (byte)value.KIterationOrder);
            AddByte(ref current, (byte)value.TransposePermutation);
            AddByte(ref current, (byte)value.TransposeAliasPolicy);
            AddUInt64(ref current, value.Fingerprint);
        }

        static void AddPolicyIdentity(
            ref ulong current,
            MatrixTilePolicyBoundCaptureIdentity identity)
        {
            AddUInt16(ref current, identity.AbiVersion);
            AddUInt32(ref current, unchecked((uint)identity.OwnerThreadId));
            AddUInt32(ref current, unchecked((uint)identity.MemoryDomainId));
            AddUInt32(ref current, identity.Opcode);
            AddByte(ref current, (byte)identity.OperationKind);
            AddUInt16(ref current, identity.NumericPolicyAbiVersion);
            AddUInt64(ref current, identity.NumericPolicyFingerprint);
            AddUInt16(ref current, identity.LayoutPolicyAbiVersion);
            AddUInt64(ref current, identity.LayoutPolicyFingerprint);
            AddUInt64(ref current, identity.DescriptorFingerprint);
            AddByte(ref current, (byte)identity.ResourceClass);
            AddByte(ref current, (byte)identity.SlotClass);
            AddByte(ref current, identity.EligibleLaneMask);
            AddByte(ref current, identity.PhysicalLaneId);
            AddUInt64(ref current, identity.SourceSnapshotFingerprint);
            AddUInt64(ref current, identity.SecondarySnapshotFingerprint);
            AddUInt64(ref current, identity.AccumulatorSnapshotFingerprint);
            AddUInt64(ref current, identity.FootprintBytes);
            AddUInt64(ref current, identity.ReplayEpoch);
            AddUInt64(ref current, identity.DependencyFingerprint);
            AddByte(ref current, (byte)identity.PublicationSurface);
            AddUInt64(ref current, identity.IdentityFingerprint);
        }

        AddByte(ref hash, (byte)capture.CaptureKind);
        AddByte(ref hash, (byte)capture.OperationKind);
        AddString(ref hash, capture.Mnemonic);
        AddUInt16(ref hash, capture.SourceTileId);
        AddUInt16(ref hash, capture.SecondaryTileId);
        AddUInt16(ref hash, capture.DestinationTileId);
        AddDescriptor(ref hash, capture.TileDescriptor);
        AddDescriptor(ref hash, capture.SecondaryTileDescriptor);
        AddDescriptor(ref hash, capture.ResultTileDescriptor);
        AddImage(ref hash, capture.SourceSnapshot);
        AddImage(ref hash, capture.SecondarySnapshot);
        AddImage(ref hash, capture.AccumulatorSnapshot);
        AddImage(ref hash, capture.ResultImage);
        MatrixTileStreamTransferRecord transfer = capture.StreamTransfer;
        AddUInt32(ref hash, transfer.CoreId);
        AddUInt32(ref hash, unchecked((uint)transfer.OwnerThreadId));
        AddUInt32(ref hash, transfer.Opcode);
        AddByte(ref hash, (byte)transfer.OperationKind);
        AddByte(ref hash, (byte)transfer.ResourceClass);
        AddByte(ref hash, (byte)transfer.SlotClass);
        AddByte(ref hash, transfer.PhysicalLaneId);
        AddByte(ref hash, transfer.StreamEngineChannel);
        AddByte(ref hash, (byte)transfer.Direction);
        AddUInt16(ref hash, transfer.ExpectedRows);
        AddUInt64(ref hash, transfer.TotalBytes);
        AddUInt64(ref hash, transfer.TransferFingerprint);
        AddByte(ref hash, transfer.Completed ? (byte)1 : (byte)0);
        MatrixTileStreamWindowIdentity[]? transferWindows = transfer.Windows;
        AddUInt32(ref hash, checked((uint)(transferWindows?.Length ?? 0)));
        if (transferWindows != null)
        {
            for (int index = 0; index < transferWindows.Length; index++)
            {
                MatrixTileStreamWindowIdentity window = transferWindows[index];
                AddUInt16(ref hash, window.Row);
                AddUInt16(ref hash, window.Window);
                AddUInt64(ref hash, window.Address);
                AddUInt32(ref hash, window.ByteCount);
                AddUInt64(ref hash, window.DataFingerprint);
            }
        }

        AddByte(ref hash, capture.MemoryValidation.HasValue ? (byte)1 : (byte)0);
        if (capture.MemoryValidation.HasValue)
        {
            MatrixTileMemoryShapeValidationResult validation =
                capture.MemoryValidation.Value;
            AddByte(ref hash, validation.IsValid ? (byte)1 : (byte)0);
            AddByte(ref hash, (byte)validation.FaultKind);
            AddUInt16(ref hash, validation.FaultPoint.Row);
            AddUInt16(ref hash, validation.FaultPoint.Column);
            AddUInt32(ref hash, validation.FaultPoint.ByteOffsetInRow);
            AddUInt64(ref hash, validation.FaultPoint.Address);
            AddByte(ref hash, validation.FaultPoint.IsStore ? (byte)1 : (byte)0);
            AddByte(ref hash, validation.HasFaultPoint ? (byte)1 : (byte)0);
            AddUInt64(ref hash, validation.FirstByteAddress);
            AddUInt64(ref hash, validation.LastByteAddress);
            AddUInt64(ref hash, validation.TotalByteFootprint);
            AddUInt32(ref hash, validation.RowByteCount);
            AddByte(ref hash, validation.CrossesPageBoundary ? (byte)1 : (byte)0);
            AddByte(ref hash, (byte)validation.PublicationPolicy);
            AddByte(ref hash, (byte)validation.OrderingPolicy);
        }

        AddByte(ref hash, capture.SemanticValidation.HasValue ? (byte)1 : (byte)0);
        if (capture.SemanticValidation.HasValue)
        {
            MatrixTileSemanticValidationResult validation =
                capture.SemanticValidation.Value;
            AddByte(ref hash, validation.IsValid ? (byte)1 : (byte)0);
            AddByte(ref hash, (byte)validation.FaultKind);
            AddDescriptor(ref hash, validation.ResultDescriptor);
            AddUInt16(ref hash, validation.ResultElementSizeBytes);
            AddByte(ref hash, validation.RequiresRetirePublication ? (byte)1 : (byte)0);
            AddByte(ref hash, validation.RequiresReplayIdentity ? (byte)1 : (byte)0);
            AddByte(ref hash, validation.UsesFallbackPath ? (byte)1 : (byte)0);
        }

        MatrixTileCapturedMemoryWrite[]? writes = capture.PendingStoreWrites;
        AddUInt32(ref hash, checked((uint)(writes?.Length ?? 0)));
        if (writes != null)
        {
            for (int index = 0; index < writes.Length; index++)
            {
                MatrixTileCapturedMemoryWrite write = writes[index];
                AddUInt16(ref hash, write.Row);
                AddUInt64(ref hash, write.Address);
                byte[]? data = write.Data;
                AddUInt32(ref hash, checked((uint)(data?.Length ?? 0)));
                if (data == null)
                {
                    continue;
                }

                for (int byteIndex = 0; byteIndex < data.Length; byteIndex++)
                {
                    AddByte(ref hash, data[byteIndex]);
                }
            }
        }

        AddByte(ref hash, (byte)capture.FaultKind);
        AddByte(ref hash, (byte)capture.MemoryFaultKind);
        AddByte(ref hash, (byte)capture.SemanticFaultKind);
        AddUInt16(ref hash, capture.FaultPoint.Row);
        AddUInt16(ref hash, capture.FaultPoint.Column);
        AddUInt32(ref hash, capture.FaultPoint.ByteOffsetInRow);
        AddUInt64(ref hash, capture.FaultPoint.Address);
        AddByte(ref hash, capture.FaultPoint.IsStore ? (byte)1 : (byte)0);
        AddByte(ref hash, capture.HasFaultPoint ? (byte)1 : (byte)0);
        AddString(ref hash, capture.FaultMessage);
        AddByte(ref hash, capture.HasFault ? (byte)1 : (byte)0);
        AddByte(ref hash, capture.RequiresRetirePublication ? (byte)1 : (byte)0);
        AddByte(ref hash, capture.RequiresReplayIdentity ? (byte)1 : (byte)0);
        AddByte(ref hash, capture.BlocksArchitecturalSideEffectsBeforeRetire ? (byte)1 : (byte)0);
        AddByte(ref hash, capture.UsesFallbackPath ? (byte)1 : (byte)0);
        AddNumericPolicy(ref hash, capture.NumericPolicy);
        AddLayoutPolicy(ref hash, capture.LayoutPolicy);
        AddPolicyIdentity(ref hash, capture.PolicyIdentity);

        return hash == 0 ? 1UL : hash;
    }

    private static uint GetOpcode(MatrixTileProjectedOperationKind operationKind) =>
        operationKind switch
        {
            MatrixTileProjectedOperationKind.Load => Processor.CPU_Core.IsaOpcodeValues.MTILE_LOAD,
            MatrixTileProjectedOperationKind.Store => Processor.CPU_Core.IsaOpcodeValues.MTILE_STORE,
            MatrixTileProjectedOperationKind.Macc => Processor.CPU_Core.IsaOpcodeValues.MTILE_MACC,
            MatrixTileProjectedOperationKind.Transpose => Processor.CPU_Core.IsaOpcodeValues.MTRANSPOSE,
            _ => 0
        };

    private static string GetMnemonic(MatrixTileProjectedOperationKind operationKind) =>
        operationKind switch
        {
            MatrixTileProjectedOperationKind.Load => MtileLoadInstruction.Mnemonic,
            MatrixTileProjectedOperationKind.Store => MtileStoreInstruction.Mnemonic,
            MatrixTileProjectedOperationKind.Macc => MtileMaccInstruction.Mnemonic,
            MatrixTileProjectedOperationKind.Transpose => MtransposeInstruction.Mnemonic,
            _ => string.Empty
        };

    private static MatrixTileRetireValidationException Validation(string message) =>
        new(message);
}
