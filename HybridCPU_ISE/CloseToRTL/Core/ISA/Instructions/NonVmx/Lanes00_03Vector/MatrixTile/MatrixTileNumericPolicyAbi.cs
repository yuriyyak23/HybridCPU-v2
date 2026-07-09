using System;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;

public enum MatrixTileNumericProfileId : byte
{
    Unspecified = 0,
    SignedInt8ToInt32 = 1,
    UnsignedInt8ToUInt32 = 2,
    SignedInt16ToInt32 = 3,
    UnsignedInt16ToUInt32 = 4,
    SignedInt32ToInt64 = 5,
    UnsignedInt32ToUInt64 = 6,
    SignedInt64ToInt64 = 7,
    UnsignedInt64ToUInt64 = 8,
    Binary32ToBinary32 = 9,
    Binary64ToBinary64 = 10,
}

public enum MatrixTileNumericSignedness : byte
{
    Unspecified = 0,
    Signed = 1,
    Unsigned = 2,
    NotApplicable = 3,
}

public enum MatrixTileNumericWideningRule : byte
{
    Unspecified = 0,
    SignExtendToAccumulator = 1,
    ZeroExtendToAccumulator = 2,
    PreserveBinaryFormat = 3,
}

public enum MatrixTileNumericMultiplyRule : byte
{
    Unspecified = 0,
    ExactIntegerProduct = 1,
    SeparatelyRoundedIeee754Product = 2,
}

public enum MatrixTileNumericAddRule : byte
{
    Unspecified = 0,
    ExactUnboundedIntermediate = 1,
    SeparatelyRoundedIeee754Add = 2,
}

public enum MatrixTileNumericRoundingMode : byte
{
    Unspecified = 0,
    NotApplicableExactInteger = 1,
    ToNearestTiesToEven = 2,
}

public enum MatrixTileNumericSaturationMode : byte
{
    Unspecified = 0,
    Disabled = 1,
}

public enum MatrixTileNumericOverflowMode : byte
{
    Unspecified = 0,
    TrapOnFinalAccumulatorEncoding = 1,
    Ieee754Infinity = 2,
}

public enum MatrixTileNumericNaNPolicy : byte
{
    Unspecified = 0,
    NotApplicable = 1,
    CanonicalQuietNaN = 2,
}

public enum MatrixTileNumericInfinityPolicy : byte
{
    Unspecified = 0,
    NotApplicable = 1,
    PreserveIeee754Infinity = 2,
}

public enum MatrixTileNumericDenormalPolicy : byte
{
    Unspecified = 0,
    NotApplicable = 1,
    Preserve = 2,
}

public enum MatrixTileNumericReproducibilityMode : byte
{
    Unspecified = 0,
    ExactIntegerLittleEndian = 1,
    DeterministicIeee754LittleEndian = 2,
}

public enum MatrixTileNumericExceptionPolicy : byte
{
    Unspecified = 0,
    CaptureTypedArithmeticFault = 1,
    PublishCanonicalIeee754Result = 2,
}

public enum MatrixTileNumericPolicyFaultKind : byte
{
    None = 0,
    MissingPolicy = 1,
    UnsupportedAbiVersion = 2,
    ReservedProfile = 3,
    UnsupportedProfile = 4,
    FingerprintMismatch = 5,
    ContradictoryElementType = 6,
    ContradictoryAccumulatorType = 7,
    ContradictoryPublishFormat = 8,
    ContradictorySignedness = 9,
    ContradictoryRule = 10,
}

public readonly record struct MatrixTileNumericPolicy(
    ushort AbiVersion,
    MatrixTileNumericProfileId ProfileId,
    DataTypeEnum ElementType,
    DataTypeEnum AccumulatorType,
    DataTypeEnum PublishFormat,
    MatrixTileNumericSignedness Signedness,
    MatrixTileNumericWideningRule WideningRule,
    MatrixTileNumericMultiplyRule MultiplyRule,
    MatrixTileNumericAddRule AddRule,
    MatrixTileNumericRoundingMode RoundingMode,
    MatrixTileNumericSaturationMode SaturationMode,
    MatrixTileNumericOverflowMode OverflowMode,
    MatrixTileNumericNaNPolicy NaNPolicy,
    MatrixTileNumericInfinityPolicy InfinityPolicy,
    MatrixTileNumericDenormalPolicy DenormalPolicy,
    MatrixTileNumericReproducibilityMode ReproducibilityMode,
    MatrixTileNumericExceptionPolicy ExceptionPolicy,
    ulong Fingerprint);

public readonly record struct MatrixTileNumericProfileRow(
    MatrixTileNumericProfileId ProfileId,
    DataTypeEnum ElementType,
    DataTypeEnum AccumulatorType,
    DataTypeEnum PublishFormat,
    bool IsExecutionSupported,
    string SupportDecision);

public readonly record struct MatrixTileNumericPolicyValidationResult(
    bool IsValid,
    MatrixTileNumericPolicyFaultKind FaultKind,
    MatrixTileNumericProfileRow Profile)
{
    public bool IsRuntimeOwnedNumericPolicyAccepted => IsValid;

    public static MatrixTileNumericPolicyValidationResult Valid(
        MatrixTileNumericProfileRow profile) =>
        new(true, MatrixTileNumericPolicyFaultKind.None, profile);

    public static MatrixTileNumericPolicyValidationResult Fault(
        MatrixTileNumericPolicyFaultKind faultKind,
        MatrixTileNumericProfileRow profile = default) =>
        new(false, faultKind, profile);
}

public static class MatrixTileNumericPolicyAbi
{
    public const ushort CurrentAbiVersion = 1;
    public const string PolicyDecision = "ClosedVersionedRuntimeOwnedMatrixTileNumericPolicyAbi";
    public const string IntegerArithmeticDecision = "ExactIntegerProductAndUnboundedSumFinalEncodingTrap";
    public const string FloatingPolicyDecision = "Binary32AndBinary64SoftwareIeee754ArithmeticRatified";
    public const string ByteOrderDecision = "CanonicalLittleEndianPackedElementEncoding";
    public const string MissingPolicyDecision = "MissingReservedContradictoryOrUnsupportedPolicyFailsClosedBeforeArithmetic";
    public const bool HasImplicitNumericDefaults = false;
    public const bool UsesHostFloatingPointModeAsAuthority = false;
    public const bool UsesCompilerMetadataAsNumericAuthority = false;

    private static readonly MatrixTileNumericProfileRow[] ProfileTable =
    [
        Supported(MatrixTileNumericProfileId.SignedInt8ToInt32, DataTypeEnum.INT8, DataTypeEnum.INT32),
        Supported(MatrixTileNumericProfileId.UnsignedInt8ToUInt32, DataTypeEnum.UINT8, DataTypeEnum.UINT32),
        Supported(MatrixTileNumericProfileId.SignedInt16ToInt32, DataTypeEnum.INT16, DataTypeEnum.INT32),
        Supported(MatrixTileNumericProfileId.UnsignedInt16ToUInt32, DataTypeEnum.UINT16, DataTypeEnum.UINT32),
        Supported(MatrixTileNumericProfileId.SignedInt32ToInt64, DataTypeEnum.INT32, DataTypeEnum.INT64),
        Supported(MatrixTileNumericProfileId.UnsignedInt32ToUInt64, DataTypeEnum.UINT32, DataTypeEnum.UINT64),
        Supported(MatrixTileNumericProfileId.SignedInt64ToInt64, DataTypeEnum.INT64, DataTypeEnum.INT64),
        Supported(MatrixTileNumericProfileId.UnsignedInt64ToUInt64, DataTypeEnum.UINT64, DataTypeEnum.UINT64),
        Supported(
            MatrixTileNumericProfileId.Binary32ToBinary32,
            DataTypeEnum.FLOAT32,
            DataTypeEnum.FLOAT32),
        Supported(
            MatrixTileNumericProfileId.Binary64ToBinary64,
            DataTypeEnum.FLOAT64,
            DataTypeEnum.FLOAT64),
    ];

    public static MatrixTileNumericProfileRow[] SupportedProfiles =>
        (MatrixTileNumericProfileRow[])ProfileTable.Clone();

    public static MatrixTileNumericPolicy CreateSupportedPolicy(
        MatrixTileNumericProfileId profileId)
    {
        MatrixTileNumericProfileRow row = GetProfile(profileId);
        if (!row.IsExecutionSupported)
        {
            throw new ArgumentOutOfRangeException(
                nameof(profileId),
                profileId,
                $"MatrixTile numeric profile is not executable: {row.SupportDecision}.");
        }

        MatrixTileNumericPolicy policy = CreateWithoutFingerprint(row);
        return policy with { Fingerprint = ComputeFingerprint(policy) };
    }

    public static MatrixTileNumericPolicyValidationResult Validate(
        MatrixTileNumericPolicy? policy)
    {
        if (!policy.HasValue)
        {
            return MatrixTileNumericPolicyValidationResult.Fault(
                MatrixTileNumericPolicyFaultKind.MissingPolicy);
        }

        MatrixTileNumericPolicy value = policy.Value;
        if (value.AbiVersion != CurrentAbiVersion)
        {
            return MatrixTileNumericPolicyValidationResult.Fault(
                MatrixTileNumericPolicyFaultKind.UnsupportedAbiVersion);
        }

        if (value.ProfileId == MatrixTileNumericProfileId.Unspecified)
        {
            return MatrixTileNumericPolicyValidationResult.Fault(
                MatrixTileNumericPolicyFaultKind.ReservedProfile);
        }

        if (!TryGetProfile(value.ProfileId, out MatrixTileNumericProfileRow profile))
        {
            return MatrixTileNumericPolicyValidationResult.Fault(
                MatrixTileNumericPolicyFaultKind.ReservedProfile);
        }

        if (!profile.IsExecutionSupported)
        {
            return MatrixTileNumericPolicyValidationResult.Fault(
                MatrixTileNumericPolicyFaultKind.UnsupportedProfile,
                profile);
        }

        if (value.Fingerprint == 0 || value.Fingerprint != ComputeFingerprint(value))
        {
            return MatrixTileNumericPolicyValidationResult.Fault(
                MatrixTileNumericPolicyFaultKind.FingerprintMismatch,
                profile);
        }

        if (value.ElementType != profile.ElementType)
        {
            return MatrixTileNumericPolicyValidationResult.Fault(
                MatrixTileNumericPolicyFaultKind.ContradictoryElementType,
                profile);
        }

        if (value.AccumulatorType != profile.AccumulatorType)
        {
            return MatrixTileNumericPolicyValidationResult.Fault(
                MatrixTileNumericPolicyFaultKind.ContradictoryAccumulatorType,
                profile);
        }

        if (value.PublishFormat != profile.PublishFormat)
        {
            return MatrixTileNumericPolicyValidationResult.Fault(
                MatrixTileNumericPolicyFaultKind.ContradictoryPublishFormat,
                profile);
        }

        MatrixTileNumericPolicy expected = CreateWithoutFingerprint(profile);
        if (value.Signedness != expected.Signedness)
        {
            return MatrixTileNumericPolicyValidationResult.Fault(
                MatrixTileNumericPolicyFaultKind.ContradictorySignedness,
                profile);
        }

        if (value.WideningRule != expected.WideningRule ||
            value.MultiplyRule != expected.MultiplyRule ||
            value.AddRule != expected.AddRule ||
            value.RoundingMode != expected.RoundingMode ||
            value.SaturationMode != expected.SaturationMode ||
            value.OverflowMode != expected.OverflowMode ||
            value.NaNPolicy != expected.NaNPolicy ||
            value.InfinityPolicy != expected.InfinityPolicy ||
            value.DenormalPolicy != expected.DenormalPolicy ||
            value.ReproducibilityMode != expected.ReproducibilityMode ||
            value.ExceptionPolicy != expected.ExceptionPolicy)
        {
            return MatrixTileNumericPolicyValidationResult.Fault(
                MatrixTileNumericPolicyFaultKind.ContradictoryRule,
                profile);
        }

        return MatrixTileNumericPolicyValidationResult.Valid(profile);
    }

    public static bool TryCreateLegacyPhase14Policy(
        DataTypeEnum elementType,
        out MatrixTileNumericPolicy policy)
    {
        MatrixTileNumericProfileId profileId = elementType switch
        {
            DataTypeEnum.INT8 => MatrixTileNumericProfileId.SignedInt8ToInt32,
            DataTypeEnum.UINT8 => MatrixTileNumericProfileId.UnsignedInt8ToUInt32,
            DataTypeEnum.INT16 => MatrixTileNumericProfileId.SignedInt16ToInt32,
            DataTypeEnum.UINT16 => MatrixTileNumericProfileId.UnsignedInt16ToUInt32,
            DataTypeEnum.INT32 => MatrixTileNumericProfileId.SignedInt32ToInt64,
            DataTypeEnum.UINT32 => MatrixTileNumericProfileId.UnsignedInt32ToUInt64,
            DataTypeEnum.INT64 => MatrixTileNumericProfileId.SignedInt64ToInt64,
            DataTypeEnum.UINT64 => MatrixTileNumericProfileId.UnsignedInt64ToUInt64,
            _ => MatrixTileNumericProfileId.Unspecified,
        };

        if (profileId == MatrixTileNumericProfileId.Unspecified)
        {
            policy = default;
            return false;
        }

        policy = CreateSupportedPolicy(profileId);
        return true;
    }

    public static int GetElementSizeBytes(DataTypeEnum dataType) =>
        DataTypeUtils.SizeOf(dataType);

    public static ulong ComputeFingerprint(MatrixTileNumericPolicy policy)
    {
        ulong hash = 14695981039346656037UL;
        Mix(ref hash, policy.AbiVersion);
        Mix(ref hash, (byte)policy.ProfileId);
        Mix(ref hash, (byte)policy.ElementType);
        Mix(ref hash, (byte)policy.AccumulatorType);
        Mix(ref hash, (byte)policy.PublishFormat);
        Mix(ref hash, (byte)policy.Signedness);
        Mix(ref hash, (byte)policy.WideningRule);
        Mix(ref hash, (byte)policy.MultiplyRule);
        Mix(ref hash, (byte)policy.AddRule);
        Mix(ref hash, (byte)policy.RoundingMode);
        Mix(ref hash, (byte)policy.SaturationMode);
        Mix(ref hash, (byte)policy.OverflowMode);
        Mix(ref hash, (byte)policy.NaNPolicy);
        Mix(ref hash, (byte)policy.InfinityPolicy);
        Mix(ref hash, (byte)policy.DenormalPolicy);
        Mix(ref hash, (byte)policy.ReproducibilityMode);
        Mix(ref hash, (byte)policy.ExceptionPolicy);
        return hash;
    }

    private static MatrixTileNumericPolicy CreateWithoutFingerprint(
        MatrixTileNumericProfileRow profile)
    {
        bool signed = DataTypeUtils.IsSignedInteger(profile.ElementType);
        bool unsigned = DataTypeUtils.IsUnsignedInteger(profile.ElementType);
        bool floating = DataTypeUtils.IsFloatingPoint(profile.ElementType);

        return new MatrixTileNumericPolicy(
            CurrentAbiVersion,
            profile.ProfileId,
            profile.ElementType,
            profile.AccumulatorType,
            profile.PublishFormat,
            signed
                ? MatrixTileNumericSignedness.Signed
                : unsigned
                    ? MatrixTileNumericSignedness.Unsigned
                    : MatrixTileNumericSignedness.NotApplicable,
            signed
                ? MatrixTileNumericWideningRule.SignExtendToAccumulator
                : unsigned
                    ? MatrixTileNumericWideningRule.ZeroExtendToAccumulator
                    : MatrixTileNumericWideningRule.PreserveBinaryFormat,
            floating
                ? MatrixTileNumericMultiplyRule.SeparatelyRoundedIeee754Product
                : MatrixTileNumericMultiplyRule.ExactIntegerProduct,
            floating
                ? MatrixTileNumericAddRule.SeparatelyRoundedIeee754Add
                : MatrixTileNumericAddRule.ExactUnboundedIntermediate,
            floating
                ? MatrixTileNumericRoundingMode.ToNearestTiesToEven
                : MatrixTileNumericRoundingMode.NotApplicableExactInteger,
            MatrixTileNumericSaturationMode.Disabled,
            floating
                ? MatrixTileNumericOverflowMode.Ieee754Infinity
                : MatrixTileNumericOverflowMode.TrapOnFinalAccumulatorEncoding,
            floating
                ? MatrixTileNumericNaNPolicy.CanonicalQuietNaN
                : MatrixTileNumericNaNPolicy.NotApplicable,
            floating
                ? MatrixTileNumericInfinityPolicy.PreserveIeee754Infinity
                : MatrixTileNumericInfinityPolicy.NotApplicable,
            floating
                ? MatrixTileNumericDenormalPolicy.Preserve
                : MatrixTileNumericDenormalPolicy.NotApplicable,
            floating
                ? MatrixTileNumericReproducibilityMode.DeterministicIeee754LittleEndian
                : MatrixTileNumericReproducibilityMode.ExactIntegerLittleEndian,
            floating
                ? MatrixTileNumericExceptionPolicy.PublishCanonicalIeee754Result
                : MatrixTileNumericExceptionPolicy.CaptureTypedArithmeticFault,
            Fingerprint: 0);
    }

    private static MatrixTileNumericProfileRow GetProfile(
        MatrixTileNumericProfileId profileId)
    {
        if (TryGetProfile(profileId, out MatrixTileNumericProfileRow profile))
        {
            return profile;
        }

        throw new ArgumentOutOfRangeException(
            nameof(profileId),
            profileId,
            "Unknown MatrixTile numeric profile.");
    }

    private static bool TryGetProfile(
        MatrixTileNumericProfileId profileId,
        out MatrixTileNumericProfileRow profile)
    {
        foreach (MatrixTileNumericProfileRow row in ProfileTable)
        {
            if (row.ProfileId == profileId)
            {
                profile = row;
                return true;
            }
        }

        profile = default;
        return false;
    }

    private static MatrixTileNumericProfileRow Supported(
        MatrixTileNumericProfileId profileId,
        DataTypeEnum elementType,
        DataTypeEnum accumulatorType) =>
        new(
            profileId,
            elementType,
            accumulatorType,
            accumulatorType,
            IsExecutionSupported: true,
            SupportDecision: "Phase15IntegerProfileSupported");

    private static MatrixTileNumericProfileRow Unsupported(
        MatrixTileNumericProfileId profileId,
        DataTypeEnum elementType,
        DataTypeEnum accumulatorType,
        string reason) =>
        new(
            profileId,
            elementType,
            accumulatorType,
            accumulatorType,
            IsExecutionSupported: false,
            SupportDecision: reason);

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
