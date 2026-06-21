namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;

public enum MatrixTileLayoutProfileId : byte
{
    Unspecified = 0,
    MaccCanonicalRowMajorAscendingK = 1,
    TransposeCanonicalRowMajor = 2,
}

public enum MatrixTileElementAddressingKind : byte
{
    Unspecified = 0,
    NotApplicable = 1,
    CanonicalPackedRowMajor = 2,
    ColumnMajor = 3,
    Blocked = 4,
    Interleaved = 5,
}

public enum MatrixTileKIterationOrderKind : byte
{
    Unspecified = 0,
    NotApplicable = 1,
    AscendingZeroToKMinusOne = 2,
}

public enum MatrixTileTransposePermutationKind : byte
{
    Unspecified = 0,
    NotApplicable = 1,
    DestinationColumnRowFromSourceRowColumn = 2,
}

public enum MatrixTileLayoutPolicyFaultKind : byte
{
    None = 0,
    MissingPolicy = 1,
    UnsupportedAbiVersion = 2,
    ReservedProfile = 3,
    UnsupportedOperation = 4,
    FingerprintMismatch = 5,
    ContradictoryRule = 6,
    DescriptorLayoutMismatch = 7,
}

public readonly record struct MatrixTileLayoutPolicy(
    ushort AbiVersion,
    MatrixTileLayoutProfileId ProfileId,
    MatrixTileProjectedOperationKind OperationKind,
    MatrixTileElementAddressingKind SourceAddressing,
    MatrixTileElementAddressingKind SecondaryAddressing,
    MatrixTileElementAddressingKind DestinationAddressing,
    MatrixTileKIterationOrderKind KIterationOrder,
    MatrixTileTransposePermutationKind TransposePermutation,
    MatrixTileTransposeAliasPolicyKind TransposeAliasPolicy,
    ulong Fingerprint);

public readonly record struct MatrixTileLayoutPolicyValidationResult(
    bool IsValid,
    MatrixTileLayoutPolicyFaultKind FaultKind)
{
    public static MatrixTileLayoutPolicyValidationResult Valid() =>
        new(true, MatrixTileLayoutPolicyFaultKind.None);

    public static MatrixTileLayoutPolicyValidationResult Fault(
        MatrixTileLayoutPolicyFaultKind faultKind) =>
        new(false, faultKind);
}

public static class MatrixTileLayoutPolicyAbi
{
    public const ushort CurrentAbiVersion = 1;
    public const string PolicyDecision = "ClosedVersionedRuntimeOwnedMatrixTileLayoutPolicyAbi";
    public const string MaccLayoutDecision = "CanonicalPackedRowMajorWithAscendingKOrder";
    public const string TransposeLayoutDecision = "CanonicalPackedRowMajorCoordinatePermutation";
    public const string UnsupportedLayoutDecision = "ColumnMajorBlockedAndInterleavedFailClosed";
    public const bool HasImplicitLayoutDefaults = false;
    public const bool UsesDescriptorLayoutAsPolicyAuthority = false;
    public const bool UsesCompilerMetadataAsLayoutAuthority = false;

    public static MatrixTileLayoutPolicy CreateMaccPolicy()
    {
        MatrixTileLayoutPolicy policy = new(
            CurrentAbiVersion,
            MatrixTileLayoutProfileId.MaccCanonicalRowMajorAscendingK,
            MatrixTileProjectedOperationKind.Macc,
            MatrixTileElementAddressingKind.CanonicalPackedRowMajor,
            MatrixTileElementAddressingKind.CanonicalPackedRowMajor,
            MatrixTileElementAddressingKind.CanonicalPackedRowMajor,
            MatrixTileKIterationOrderKind.AscendingZeroToKMinusOne,
            MatrixTileTransposePermutationKind.NotApplicable,
            MatrixTileTransposeAliasPolicyKind.Unspecified,
            Fingerprint: 0);
        return policy with { Fingerprint = ComputeFingerprint(policy) };
    }

    public static MatrixTileLayoutPolicy CreateTransposePolicy()
    {
        MatrixTileLayoutPolicy policy = new(
            CurrentAbiVersion,
            MatrixTileLayoutProfileId.TransposeCanonicalRowMajor,
            MatrixTileProjectedOperationKind.Transpose,
            MatrixTileElementAddressingKind.CanonicalPackedRowMajor,
            MatrixTileElementAddressingKind.NotApplicable,
            MatrixTileElementAddressingKind.CanonicalPackedRowMajor,
            MatrixTileKIterationOrderKind.NotApplicable,
            MatrixTileTransposePermutationKind.DestinationColumnRowFromSourceRowColumn,
            MatrixTileTransposeAliasPolicyKind.OutOfPlaceOrSquareInPlaceOnly,
            Fingerprint: 0);
        return policy with { Fingerprint = ComputeFingerprint(policy) };
    }

    public static MatrixTileLayoutPolicyValidationResult Validate(
        MatrixTileLayoutPolicy? policy,
        MatrixTileProjectedOperationKind expectedOperationKind)
    {
        if (!policy.HasValue)
        {
            return MatrixTileLayoutPolicyValidationResult.Fault(
                MatrixTileLayoutPolicyFaultKind.MissingPolicy);
        }

        MatrixTileLayoutPolicy value = policy.Value;
        if (value.AbiVersion != CurrentAbiVersion)
        {
            return MatrixTileLayoutPolicyValidationResult.Fault(
                MatrixTileLayoutPolicyFaultKind.UnsupportedAbiVersion);
        }

        if (value.ProfileId == MatrixTileLayoutProfileId.Unspecified)
        {
            return MatrixTileLayoutPolicyValidationResult.Fault(
                MatrixTileLayoutPolicyFaultKind.ReservedProfile);
        }

        if (value.OperationKind != expectedOperationKind)
        {
            return MatrixTileLayoutPolicyValidationResult.Fault(
                MatrixTileLayoutPolicyFaultKind.UnsupportedOperation);
        }

        MatrixTileLayoutPolicy expected = expectedOperationKind switch
        {
            MatrixTileProjectedOperationKind.Macc => CreateMaccPolicy(),
            MatrixTileProjectedOperationKind.Transpose => CreateTransposePolicy(),
            _ => default,
        };
        if (expected.Equals(default(MatrixTileLayoutPolicy)))
        {
            return MatrixTileLayoutPolicyValidationResult.Fault(
                MatrixTileLayoutPolicyFaultKind.UnsupportedOperation);
        }

        if (value.Fingerprint == 0 || value.Fingerprint != ComputeFingerprint(value))
        {
            return MatrixTileLayoutPolicyValidationResult.Fault(
                MatrixTileLayoutPolicyFaultKind.FingerprintMismatch);
        }

        return value == expected
            ? MatrixTileLayoutPolicyValidationResult.Valid()
            : MatrixTileLayoutPolicyValidationResult.Fault(
                MatrixTileLayoutPolicyFaultKind.ContradictoryRule);
    }

    public static MatrixTileLayoutPolicyValidationResult ValidateDescriptors(
        MatrixTileLayoutPolicy policy,
        params MatrixTileCanonicalDescriptorAbi[] descriptors)
    {
        for (int index = 0; index < descriptors.Length; index++)
        {
            if (descriptors[index].Layout != MatrixTileDescriptorLayoutKind.RowMajor)
            {
                return MatrixTileLayoutPolicyValidationResult.Fault(
                    MatrixTileLayoutPolicyFaultKind.DescriptorLayoutMismatch);
            }
        }

        return MatrixTileLayoutPolicyValidationResult.Valid();
    }

    public static int GetPackedOffset(
        MatrixTileCanonicalDescriptorAbi descriptor,
        ushort row,
        ushort column,
        MatrixTileElementAddressingKind addressing)
    {
        if (addressing != MatrixTileElementAddressingKind.CanonicalPackedRowMajor ||
            descriptor.Layout != MatrixTileDescriptorLayoutKind.RowMajor ||
            row >= descriptor.Rows ||
            column >= descriptor.Columns)
        {
            throw new InvalidOperationException(
                "MatrixTile element addressing requires an in-range canonical row-major coordinate.");
        }

        checked
        {
            return ((row * descriptor.Columns) + column) * descriptor.ElementSizeBytes;
        }
    }

    public static ulong ComputeFingerprint(MatrixTileLayoutPolicy policy)
    {
        ulong hash = 14695981039346656037UL;
        Mix(ref hash, policy.AbiVersion);
        Mix(ref hash, (byte)policy.ProfileId);
        Mix(ref hash, (byte)policy.OperationKind);
        Mix(ref hash, (byte)policy.SourceAddressing);
        Mix(ref hash, (byte)policy.SecondaryAddressing);
        Mix(ref hash, (byte)policy.DestinationAddressing);
        Mix(ref hash, (byte)policy.KIterationOrder);
        Mix(ref hash, (byte)policy.TransposePermutation);
        Mix(ref hash, (byte)policy.TransposeAliasPolicy);
        return hash;
    }

    private static void Mix(ref ulong hash, ushort value)
    {
        Mix(ref hash, (byte)value);
        Mix(ref hash, (byte)(value >> 8));
    }

    private static void Mix(ref ulong hash, byte value)
    {
        hash ^= value;
        hash *= 1099511628211UL;
    }

}
