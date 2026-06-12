namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;

public enum MatrixTileNumericElementKind : byte
{
    Unspecified = 0,
    SignedInteger = 1,
    UnsignedInteger = 2,
}

public enum MatrixTileAccumulatorPolicyKind : byte
{
    Unspecified = 0,
    WideningIntegerAccumulatorWithOverflowTrap = 1,
}

public enum MatrixTileTransposeAliasPolicyKind : byte
{
    Unspecified = 0,
    OutOfPlaceOrSquareInPlaceOnly = 1,
}

public enum MatrixTileSemanticFaultKind : byte
{
    None = 0,
    ZeroDescriptor = 1,
    ReservedDescriptor = 2,
    UnsupportedElementSize = 3,
    UnsupportedElementKind = 4,
    MaccInnerDimensionMismatch = 5,
    MaccAccumulatorShapeMismatch = 6,
    MaccAccumulatorElementSizeMismatch = 7,
    TransposeShapeMismatch = 8,
    TransposeElementSizeMismatch = 9,
    TransposeInPlaceRequiresSquareShape = 10,
    TransposeInPlaceDescriptorMismatch = 11,
}

public readonly record struct MatrixTileMaccSemanticContract(
    MatrixTileCanonicalDescriptorAbi Left,
    MatrixTileCanonicalDescriptorAbi Right,
    MatrixTileCanonicalDescriptorAbi Accumulator,
    MatrixTileNumericElementKind ElementKind,
    MatrixTileAccumulatorPolicyKind AccumulatorPolicy);

public readonly record struct MatrixTileTransposeSemanticContract(
    MatrixTileCanonicalDescriptorAbi Source,
    MatrixTileCanonicalDescriptorAbi Destination,
    ushort SourceTileId,
    ushort DestinationTileId,
    MatrixTileTransposeAliasPolicyKind AliasPolicy);

public readonly record struct MatrixTileSemanticValidationResult(
    bool IsValid,
    MatrixTileSemanticFaultKind FaultKind,
    MatrixTileCanonicalDescriptorAbi ResultDescriptor,
    ushort ResultElementSizeBytes,
    bool RequiresRetirePublication,
    bool RequiresReplayIdentity,
    bool UsesFallbackPath)
{
    public static MatrixTileSemanticValidationResult Valid(
        MatrixTileCanonicalDescriptorAbi resultDescriptor,
        ushort resultElementSizeBytes)
    {
        return new MatrixTileSemanticValidationResult(
            IsValid: true,
            FaultKind: MatrixTileSemanticFaultKind.None,
            resultDescriptor,
            resultElementSizeBytes,
            RequiresRetirePublication: true,
            RequiresReplayIdentity: true,
            UsesFallbackPath: false);
    }

    public static MatrixTileSemanticValidationResult Fault(MatrixTileSemanticFaultKind faultKind)
    {
        return new MatrixTileSemanticValidationResult(
            IsValid: false,
            faultKind,
            ResultDescriptor: default,
            ResultElementSizeBytes: 0,
            RequiresRetirePublication: false,
            RequiresReplayIdentity: false,
            UsesFallbackPath: false);
    }
}

public static class MatrixTileAccumulatorAndTransposePolicyAbi
{
    public const string AccumulatorTransposeDecision = "ClosedAccumulatorAndTransposeSemanticAbi";
    public const string MaccAccumulatorDecision = "ClosedAccumulatorTileAbi";
    public const string MaccShapeDecision = "MaccRowsKColumnsShapeCompatibilityValidated";
    public const string MaccDtypeDecision = "WideningIntegerAccumulatorDtypePolicySelected";
    public const string MaccExceptionDecision = "AccumulatorOverflowTrapPolicySelected";
    public const string MaccRetireReplayDecision = "RetireOwnedAccumulatorPublicationAndReplayIdentitySelected";
    public const string TransposeCarrierDecision = "ClosedTransposePolicyAbi";
    public const string TransposeAliasDecision = "OutOfPlaceOrSquareInPlaceAliasPolicySelected";
    public const string TransposeLayoutDecision = "RowMajorTransposeShapePermutationSelected";
    public const string TransposeRetireReplayDecision = "RetireOwnedTransposePublicationAndReplayIdentitySelected";
    public const string FallbackDecision = "VectorTransposeOrExternalBackendEvidenceIsNotMtileAuthority";

    public const bool HasAccumulatorTileAbi = true;
    public const bool HasTransposePolicyAbi = true;
    public const bool HasAccumulatorTileStateOwner = true;
    public const bool HasAccumulatorTileFootprintPolicy = true;
    public const bool HasAccumulatorDtypePolicy = true;
    public const bool HasMaccShapeCompatibilityPolicy = true;
    public const bool HasMaccExceptionOrSaturationPolicy = true;
    public const bool HasTransposePolicyCarrier = true;
    public const bool HasTransposeSourceDestinationAliasPolicy = true;
    public const bool HasInPlaceTransposePolicy = true;
    public const bool HasTransposeLayoutPermutationPolicy = true;
    public const bool HasInvalidShapeTypeAliasDeterminism = true;
    public const bool KeepsVectorTransposeNonAuthority = true;
    public const bool KeepsExternalBackendNonAuthority = true;
    public const bool KeepsCompilerMatrixIrIndependent = true;
    public const bool KeepsCompilerHandoffBlocked = true;

    public static MatrixTileMaccSemanticContract CreateMaccContract(
        MatrixTileCanonicalDescriptorAbi left,
        MatrixTileCanonicalDescriptorAbi right,
        MatrixTileCanonicalDescriptorAbi accumulator,
        MatrixTileNumericElementKind elementKind = MatrixTileNumericElementKind.SignedInteger)
    {
        return new MatrixTileMaccSemanticContract(
            left,
            right,
            accumulator,
            elementKind,
            MatrixTileAccumulatorPolicyKind.WideningIntegerAccumulatorWithOverflowTrap);
    }

    public static MatrixTileTransposeSemanticContract CreateTransposeContract(
        MatrixTileCanonicalDescriptorAbi source,
        MatrixTileCanonicalDescriptorAbi destination,
        ushort sourceTileId,
        ushort destinationTileId)
    {
        return new MatrixTileTransposeSemanticContract(
            source,
            destination,
            sourceTileId,
            destinationTileId,
            MatrixTileTransposeAliasPolicyKind.OutOfPlaceOrSquareInPlaceOnly);
    }

    public static MatrixTileSemanticValidationResult ValidateMacc(
        MatrixTileMaccSemanticContract contract)
    {
        MatrixTileSemanticFaultKind descriptorFault = ValidateDescriptors(
            contract.Left,
            contract.Right,
            contract.Accumulator);
        if (descriptorFault != MatrixTileSemanticFaultKind.None)
        {
            return MatrixTileSemanticValidationResult.Fault(descriptorFault);
        }

        if (!IsSupportedElementKind(contract.ElementKind))
        {
            return MatrixTileSemanticValidationResult.Fault(MatrixTileSemanticFaultKind.UnsupportedElementKind);
        }

        if (!IsSupportedSourceElementSize(contract.Left.ElementSizeBytes) ||
            contract.Left.ElementSizeBytes != contract.Right.ElementSizeBytes)
        {
            return MatrixTileSemanticValidationResult.Fault(MatrixTileSemanticFaultKind.UnsupportedElementSize);
        }

        if (contract.Left.Columns != contract.Right.Rows)
        {
            return MatrixTileSemanticValidationResult.Fault(MatrixTileSemanticFaultKind.MaccInnerDimensionMismatch);
        }

        if (contract.Accumulator.Rows != contract.Left.Rows ||
            contract.Accumulator.Columns != contract.Right.Columns)
        {
            return MatrixTileSemanticValidationResult.Fault(MatrixTileSemanticFaultKind.MaccAccumulatorShapeMismatch);
        }

        ushort expectedAccumulatorElementSize = GetAccumulatorElementSizeBytes(contract.Left.ElementSizeBytes);
        if (contract.Accumulator.ElementSizeBytes != expectedAccumulatorElementSize)
        {
            return MatrixTileSemanticValidationResult.Fault(MatrixTileSemanticFaultKind.MaccAccumulatorElementSizeMismatch);
        }

        return MatrixTileSemanticValidationResult.Valid(
            contract.Accumulator,
            expectedAccumulatorElementSize);
    }

    public static MatrixTileSemanticValidationResult ValidateTranspose(
        MatrixTileTransposeSemanticContract contract)
    {
        MatrixTileSemanticFaultKind descriptorFault = ValidateDescriptors(
            contract.Source,
            contract.Destination);
        if (descriptorFault != MatrixTileSemanticFaultKind.None)
        {
            return MatrixTileSemanticValidationResult.Fault(descriptorFault);
        }

        if (contract.Source.ElementSizeBytes != contract.Destination.ElementSizeBytes)
        {
            return MatrixTileSemanticValidationResult.Fault(MatrixTileSemanticFaultKind.TransposeElementSizeMismatch);
        }

        bool isInPlace = contract.SourceTileId == contract.DestinationTileId;
        if (isInPlace)
        {
            if (contract.AliasPolicy != MatrixTileTransposeAliasPolicyKind.OutOfPlaceOrSquareInPlaceOnly)
            {
                return MatrixTileSemanticValidationResult.Fault(MatrixTileSemanticFaultKind.TransposeInPlaceRequiresSquareShape);
            }

            if (contract.Source.Rows != contract.Source.Columns)
            {
                return MatrixTileSemanticValidationResult.Fault(MatrixTileSemanticFaultKind.TransposeInPlaceRequiresSquareShape);
            }

            if (contract.Destination.Rows != contract.Source.Rows ||
                contract.Destination.Columns != contract.Source.Columns ||
                contract.Destination.StrideBytes != contract.Source.StrideBytes)
            {
                return MatrixTileSemanticValidationResult.Fault(MatrixTileSemanticFaultKind.TransposeInPlaceDescriptorMismatch);
            }
        }
        else if (contract.Destination.Rows != contract.Source.Columns ||
                 contract.Destination.Columns != contract.Source.Rows)
        {
            return MatrixTileSemanticValidationResult.Fault(MatrixTileSemanticFaultKind.TransposeShapeMismatch);
        }

        return MatrixTileSemanticValidationResult.Valid(
            contract.Destination,
            contract.Destination.ElementSizeBytes);
    }

    public static ushort GetAccumulatorElementSizeBytes(ushort sourceElementSizeBytes)
    {
        return sourceElementSizeBytes switch
        {
            1 => 4,
            2 => 4,
            4 => 8,
            8 => 8,
            _ => 0,
        };
    }

    public static bool IsSupportedElementKind(MatrixTileNumericElementKind elementKind) =>
        elementKind is MatrixTileNumericElementKind.SignedInteger or MatrixTileNumericElementKind.UnsignedInteger;

    public static bool IsSupportedSourceElementSize(ushort elementSizeBytes) =>
        elementSizeBytes is 1 or 2 or 4 or 8;

    private static MatrixTileSemanticFaultKind ValidateDescriptors(
        params MatrixTileCanonicalDescriptorAbi[] descriptors)
    {
        foreach (MatrixTileCanonicalDescriptorAbi descriptor in descriptors)
        {
            if (descriptor.IsZeroEncoding)
            {
                return MatrixTileSemanticFaultKind.ZeroDescriptor;
            }

            if (descriptor.IsReservedEncoding)
            {
                return MatrixTileSemanticFaultKind.ReservedDescriptor;
            }
        }

        return MatrixTileSemanticFaultKind.None;
    }
}
