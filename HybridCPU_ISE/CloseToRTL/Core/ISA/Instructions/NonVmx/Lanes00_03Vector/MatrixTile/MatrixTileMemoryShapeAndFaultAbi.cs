namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;

public enum MatrixTileMemoryOperationKind : byte
{
    Unspecified = 0,
    Load = 1,
    Store = 2,
}

public enum MatrixTileEffectiveAddressSourceKind : byte
{
    Unspecified = 0,
    ExplicitRuntimeTileMemoryOperand = 1,
}

public enum MatrixTileMemoryOrderingPolicyKind : byte
{
    Unspecified = 0,
    RetireOrderedAllOrNone = 1,
}

public enum MatrixTileMemoryPublicationPolicyKind : byte
{
    Unspecified = 0,
    RetireStagedLoadPublication = 1,
    RetireStagedStoreCommit = 2,
}

public enum MatrixTileMemoryFaultKind : byte
{
    None = 0,
    UnsupportedOperation = 1,
    UnsupportedEffectiveAddressSource = 2,
    ZeroDescriptor = 3,
    ReservedDescriptor = 4,
    UnsupportedElementSize = 5,
    RowByteOverflow = 6,
    StrideTooSmall = 7,
    AlignmentFault = 8,
    AddressOverflow = 9,
    InvalidPageSize = 10,
    PartialMemoryFault = 11,
}

public readonly record struct MatrixTileMemoryFaultPoint(
    ushort Row,
    ushort Column,
    uint ByteOffsetInRow,
    ulong Address,
    bool IsStore);

public readonly record struct MatrixTileMemoryShapeContract(
    MatrixTileMemoryOperationKind Operation,
    MatrixTileEffectiveAddressSourceKind EffectiveAddressSource,
    MatrixTileCanonicalDescriptorAbi Descriptor,
    ulong BaseAddress,
    ushort PageSizeBytes);

public readonly record struct MatrixTileMemoryShapeValidationResult(
    bool IsValid,
    MatrixTileMemoryFaultKind FaultKind,
    MatrixTileMemoryFaultPoint FaultPoint,
    bool HasFaultPoint,
    ulong FirstByteAddress,
    ulong LastByteAddress,
    ulong TotalByteFootprint,
    uint RowByteCount,
    bool CrossesPageBoundary,
    MatrixTileMemoryPublicationPolicyKind PublicationPolicy,
    MatrixTileMemoryOrderingPolicyKind OrderingPolicy)
{
    public bool IsMemoryShapeAbiAccepted => IsValid;

    public static MatrixTileMemoryShapeValidationResult Valid(
        ulong firstByteAddress,
        ulong lastByteAddress,
        ulong totalByteFootprint,
        uint rowByteCount,
        bool crossesPageBoundary,
        MatrixTileMemoryPublicationPolicyKind publicationPolicy)
    {
        return new MatrixTileMemoryShapeValidationResult(
            IsValid: true,
            FaultKind: MatrixTileMemoryFaultKind.None,
            FaultPoint: default,
            HasFaultPoint: false,
            firstByteAddress,
            lastByteAddress,
            totalByteFootprint,
            rowByteCount,
            crossesPageBoundary,
            publicationPolicy,
            MatrixTileMemoryOrderingPolicyKind.RetireOrderedAllOrNone);
    }

    public static MatrixTileMemoryShapeValidationResult Fault(
        MatrixTileMemoryFaultKind faultKind,
        MatrixTileMemoryFaultPoint faultPoint = default,
        bool hasFaultPoint = false)
    {
        return new MatrixTileMemoryShapeValidationResult(
            IsValid: false,
            faultKind,
            faultPoint,
            hasFaultPoint,
            FirstByteAddress: 0,
            LastByteAddress: 0,
            TotalByteFootprint: 0,
            RowByteCount: 0,
            CrossesPageBoundary: false,
            MatrixTileMemoryPublicationPolicyKind.Unspecified,
            MatrixTileMemoryOrderingPolicyKind.Unspecified);
    }
}

public static class MatrixTileMemoryShapeAndFaultAbi
{
    public const ushort DefaultPageSizeBytes = 4096;

    public const string MemoryShapeFaultDecision = "ClosedTileMemoryShapeAndFaultAbi";
    public const string EffectiveAddressDecision = "ExplicitRuntimeTileMemoryOperandEaSelected";
    public const string ShapeValidationDecision = "DescriptorRowsColumnsElementStrideAndAddressOverflowValidated";
    public const string AlignmentDecision = "ElementSizeAlignedBaseAndStrideRequired";
    public const string PageCrossingDecision = "PageCrossingAllowedWithPreciseFaultPoint";
    public const string FaultReplayDecision = "PreciseRowColumnFaultPointAndReplayIdentitySelected";
    public const string SideEffectOwnerDecision = "RetireOwnedLoadPublicationAndStoreCommitSelected";
    public const string MemoryOrderingDecision = "AllOrNoneRetireOrderedMemorySideEffects";
    public const string EaFallbackDecision = "MemoryEaFallbackIsNotTileMemoryExecutionAuthority";

    public const bool HasTileMemoryShapeFaultAbi = true;
    public const bool HasEffectiveAddressSource = true;
    public const bool HasShapeValidation = true;
    public const bool HasAddressOverflowValidation = true;
    public const bool HasAlignmentPolicy = true;
    public const bool HasPageCrossingPolicy = true;
    public const bool HasPartialFaultPolicy = true;
    public const bool HasMemoryOrderingPolicy = true;
    public const bool HasLoadStoreSideEffectOwner = true;
    public const bool HasRetireRollbackPolicy = true;
    public const bool KeepsEaFallbackNonAuthority = true;
    public const bool KeepsMemorySideEffectsUnopened = true;
    public const bool KeepsCompilerHandoffBlocked = true;

    public static MatrixTileMemoryShapeContract CreateLoadContract(
        MatrixTileCanonicalDescriptorAbi descriptor,
        ulong baseAddress,
        ushort pageSizeBytes = DefaultPageSizeBytes)
    {
        return new MatrixTileMemoryShapeContract(
            MatrixTileMemoryOperationKind.Load,
            MatrixTileEffectiveAddressSourceKind.ExplicitRuntimeTileMemoryOperand,
            descriptor,
            baseAddress,
            pageSizeBytes);
    }

    public static MatrixTileMemoryShapeContract CreateStoreContract(
        MatrixTileCanonicalDescriptorAbi descriptor,
        ulong baseAddress,
        ushort pageSizeBytes = DefaultPageSizeBytes)
    {
        return new MatrixTileMemoryShapeContract(
            MatrixTileMemoryOperationKind.Store,
            MatrixTileEffectiveAddressSourceKind.ExplicitRuntimeTileMemoryOperand,
            descriptor,
            baseAddress,
            pageSizeBytes);
    }

    public static MatrixTileMemoryShapeValidationResult Validate(
        MatrixTileMemoryShapeContract contract)
    {
        if (contract.Operation is not MatrixTileMemoryOperationKind.Load and not MatrixTileMemoryOperationKind.Store)
        {
            return MatrixTileMemoryShapeValidationResult.Fault(MatrixTileMemoryFaultKind.UnsupportedOperation);
        }

        if (contract.EffectiveAddressSource != MatrixTileEffectiveAddressSourceKind.ExplicitRuntimeTileMemoryOperand)
        {
            return MatrixTileMemoryShapeValidationResult.Fault(MatrixTileMemoryFaultKind.UnsupportedEffectiveAddressSource);
        }

        if (contract.PageSizeBytes == 0)
        {
            return MatrixTileMemoryShapeValidationResult.Fault(MatrixTileMemoryFaultKind.InvalidPageSize);
        }

        MatrixTileCanonicalDescriptorAbi descriptor = contract.Descriptor;
        if (descriptor.IsZeroEncoding)
        {
            return MatrixTileMemoryShapeValidationResult.Fault(MatrixTileMemoryFaultKind.ZeroDescriptor);
        }

        if (descriptor.IsReservedEncoding)
        {
            return MatrixTileMemoryShapeValidationResult.Fault(MatrixTileMemoryFaultKind.ReservedDescriptor);
        }

        if (!IsSupportedElementSize(descriptor.ElementSizeBytes))
        {
            return MatrixTileMemoryShapeValidationResult.Fault(MatrixTileMemoryFaultKind.UnsupportedElementSize);
        }

        ulong rowBytes = (ulong)descriptor.Columns * descriptor.ElementSizeBytes;
        if (rowBytes == 0 || rowBytes > uint.MaxValue)
        {
            return MatrixTileMemoryShapeValidationResult.Fault(MatrixTileMemoryFaultKind.RowByteOverflow);
        }

        if (descriptor.StrideBytes < rowBytes)
        {
            return MatrixTileMemoryShapeValidationResult.Fault(MatrixTileMemoryFaultKind.StrideTooSmall);
        }

        if (!IsElementAligned(contract.BaseAddress, descriptor.ElementSizeBytes) ||
            !IsElementAligned(descriptor.StrideBytes, descriptor.ElementSizeBytes))
        {
            MatrixTileMemoryFaultPoint point = new(
                Row: 0,
                Column: 0,
                ByteOffsetInRow: 0,
                Address: contract.BaseAddress,
                IsStore: contract.Operation == MatrixTileMemoryOperationKind.Store);
            return MatrixTileMemoryShapeValidationResult.Fault(
                MatrixTileMemoryFaultKind.AlignmentFault,
                point,
                hasFaultPoint: true);
        }

        if (!TryGetTotalByteFootprint(descriptor, rowBytes, out ulong totalBytes))
        {
            return MatrixTileMemoryShapeValidationResult.Fault(MatrixTileMemoryFaultKind.AddressOverflow);
        }

        if (totalBytes == 0 || contract.BaseAddress > ulong.MaxValue - (totalBytes - 1))
        {
            return MatrixTileMemoryShapeValidationResult.Fault(MatrixTileMemoryFaultKind.AddressOverflow);
        }

        ulong lastByteAddress = contract.BaseAddress + totalBytes - 1;
        bool crossesPageBoundary = CrossesPageBoundary(
            contract.BaseAddress,
            lastByteAddress,
            contract.PageSizeBytes);

        return MatrixTileMemoryShapeValidationResult.Valid(
            contract.BaseAddress,
            lastByteAddress,
            totalBytes,
            (uint)rowBytes,
            crossesPageBoundary,
            GetPublicationPolicy(contract.Operation));
    }

    public static MatrixTileMemoryShapeValidationResult ProjectPartialMemoryFault(
        MatrixTileMemoryShapeContract contract,
        ushort row,
        ushort column,
        ushort byteOffsetInElement = 0)
    {
        MatrixTileMemoryFaultPoint point = CreatePreciseFaultPoint(
            contract,
            row,
            column,
            byteOffsetInElement);

        return MatrixTileMemoryShapeValidationResult.Fault(
            MatrixTileMemoryFaultKind.PartialMemoryFault,
            point,
            hasFaultPoint: true);
    }

    public static MatrixTileMemoryFaultPoint CreatePreciseFaultPoint(
        MatrixTileMemoryShapeContract contract,
        ushort row,
        ushort column,
        ushort byteOffsetInElement = 0)
    {
        MatrixTileMemoryShapeValidationResult validation = Validate(contract);
        if (!validation.IsMemoryShapeAbiAccepted)
        {
            throw new System.InvalidOperationException(
                $"Cannot project a precise MTILE memory fault point from invalid shape: {validation.FaultKind}.");
        }

        MatrixTileCanonicalDescriptorAbi descriptor = contract.Descriptor;
        if (row >= descriptor.Rows)
        {
            throw new System.ArgumentOutOfRangeException(nameof(row), row, "Tile memory fault row is outside the descriptor shape.");
        }

        if (column >= descriptor.Columns)
        {
            throw new System.ArgumentOutOfRangeException(nameof(column), column, "Tile memory fault column is outside the descriptor shape.");
        }

        if (byteOffsetInElement >= descriptor.ElementSizeBytes)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(byteOffsetInElement),
                byteOffsetInElement,
                "Tile memory fault byte offset is outside the element width.");
        }

        ulong byteOffsetInRow = (ulong)column * descriptor.ElementSizeBytes + byteOffsetInElement;
        ulong address = contract.BaseAddress + (ulong)row * descriptor.StrideBytes + byteOffsetInRow;

        return new MatrixTileMemoryFaultPoint(
            row,
            column,
            (uint)byteOffsetInRow,
            address,
            contract.Operation == MatrixTileMemoryOperationKind.Store);
    }

    public static bool IsSupportedElementSize(ushort elementSizeBytes) =>
        elementSizeBytes is 1 or 2 or 4 or 8;

    public static bool IsElementAligned(ulong value, ushort elementSizeBytes) =>
        IsSupportedElementSize(elementSizeBytes) && value % elementSizeBytes == 0;

    public static MatrixTileMemoryPublicationPolicyKind GetPublicationPolicy(
        MatrixTileMemoryOperationKind operation)
    {
        return operation switch
        {
            MatrixTileMemoryOperationKind.Load => MatrixTileMemoryPublicationPolicyKind.RetireStagedLoadPublication,
            MatrixTileMemoryOperationKind.Store => MatrixTileMemoryPublicationPolicyKind.RetireStagedStoreCommit,
            _ => MatrixTileMemoryPublicationPolicyKind.Unspecified,
        };
    }

    private static bool TryGetTotalByteFootprint(
        MatrixTileCanonicalDescriptorAbi descriptor,
        ulong rowBytes,
        out ulong totalBytes)
    {
        ulong interRowBytes = (ulong)(descriptor.Rows - 1) * descriptor.StrideBytes;
        if (interRowBytes > ulong.MaxValue - rowBytes)
        {
            totalBytes = 0;
            return false;
        }

        totalBytes = interRowBytes + rowBytes;
        return true;
    }

    private static bool CrossesPageBoundary(
        ulong firstByteAddress,
        ulong lastByteAddress,
        ushort pageSizeBytes)
    {
        return firstByteAddress / pageSizeBytes != lastByteAddress / pageSizeBytes;
    }
}
