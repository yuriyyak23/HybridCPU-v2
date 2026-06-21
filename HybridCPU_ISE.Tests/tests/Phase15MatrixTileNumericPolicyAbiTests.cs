using System;
using System.Linq;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class MatrixTileNumericPolicyAbiTests
{
    [Fact]
    public void SupportedProfileMatrix_IsExactAndDoesNotTreatVocabularyAsSupport()
    {
        MatrixTileNumericProfileRow[] rows = MatrixTileNumericPolicyAbi.SupportedProfiles;

        Assert.Equal(10, rows.Length);
        Assert.Equal(10, rows.Count(static row => row.IsExecutionSupported));
        Assert.Empty(rows.Where(static row => !row.IsExecutionSupported));
        Assert.Contains(
            rows,
            static row =>
                row.ProfileId == MatrixTileNumericProfileId.Binary32ToBinary32 &&
                row.IsExecutionSupported);
        Assert.Contains(
            rows,
            static row =>
                row.ProfileId == MatrixTileNumericProfileId.Binary64ToBinary64 &&
                row.IsExecutionSupported);
    }

    [Fact]
    public void SupportedPolicies_RoundTripWithStableNonZeroFingerprint()
    {
        foreach (MatrixTileNumericProfileRow row in
                 MatrixTileNumericPolicyAbi.SupportedProfiles.Where(
                     static row => row.IsExecutionSupported))
        {
            MatrixTileNumericPolicy first =
                MatrixTileNumericPolicyAbi.CreateSupportedPolicy(row.ProfileId);
            MatrixTileNumericPolicy second =
                MatrixTileNumericPolicyAbi.CreateSupportedPolicy(row.ProfileId);
            MatrixTileNumericPolicyValidationResult validation =
                MatrixTileNumericPolicyAbi.Validate(first);

            Assert.True(validation.IsValid, row.ProfileId.ToString());
            Assert.Equal(row, validation.Profile);
            Assert.NotEqual(0UL, first.Fingerprint);
            Assert.Equal(first, second);
            Assert.Equal(
                first.Fingerprint,
                MatrixTileNumericPolicyAbi.ComputeFingerprint(first));
            Assert.Equal(
                MatrixTileNumericSaturationMode.Disabled,
                first.SaturationMode);
            Assert.Equal(
                first.Signedness == MatrixTileNumericSignedness.NotApplicable
                    ? MatrixTileNumericOverflowMode.Ieee754Infinity
                    : MatrixTileNumericOverflowMode.TrapOnFinalAccumulatorEncoding,
                first.OverflowMode);
        }
    }

    [Fact]
    public void MissingReservedUnsupportedAndTamperedPoliciesFailClosed()
    {
        Assert.Equal(
            MatrixTileNumericPolicyFaultKind.MissingPolicy,
            MatrixTileNumericPolicyAbi.Validate(null).FaultKind);
        Assert.Equal(
            MatrixTileNumericPolicyFaultKind.UnsupportedAbiVersion,
            MatrixTileNumericPolicyAbi.Validate(
                default(MatrixTileNumericPolicy) with
                {
                    AbiVersion = MatrixTileNumericPolicyAbi.CurrentAbiVersion + 1
                }).FaultKind);

        MatrixTileNumericPolicy reserved = default(MatrixTileNumericPolicy) with
        {
            AbiVersion = MatrixTileNumericPolicyAbi.CurrentAbiVersion,
            ProfileId = (MatrixTileNumericProfileId)byte.MaxValue
        };
        Assert.Equal(
            MatrixTileNumericPolicyFaultKind.ReservedProfile,
            MatrixTileNumericPolicyAbi.Validate(reserved).FaultKind);

        MatrixTileNumericPolicy valid =
            MatrixTileNumericPolicyAbi.CreateSupportedPolicy(
                MatrixTileNumericProfileId.SignedInt8ToInt32);
        Assert.Equal(
            MatrixTileNumericPolicyFaultKind.FingerprintMismatch,
            MatrixTileNumericPolicyAbi.Validate(
                valid with { Fingerprint = valid.Fingerprint ^ 1UL }).FaultKind);
        Assert.Equal(
            MatrixTileNumericPolicyFaultKind.FingerprintMismatch,
            MatrixTileNumericPolicyAbi.Validate(
                valid with
                {
                    AddRule = MatrixTileNumericAddRule.SeparatelyRoundedIeee754Add
                }).FaultKind);
    }

    [Fact]
    public void RuntimeProjection_RequiresExplicitPolicyAndRejectsDtypeMismatch()
    {
        VectorInstructionPayload payload = CreatePayload(DataTypeEnum.INT8);
        MatrixTileInstructionIrProjection missing =
            MatrixTileIrProjectionAndMaterializer.ProjectDecodedVectorPayload(
                InstructionsEnum.MTILE_MACC,
                payload,
                immediate: 2,
                requireExplicitNumericPolicy: true);

        Assert.Equal(MatrixTileIrProjectionFaultKind.NumericPolicyFault, missing.FaultKind);
        Assert.Equal(
            MatrixTileNumericPolicyFaultKind.MissingPolicy,
            missing.NumericPolicyValidation!.Value.FaultKind);

        MatrixTileNumericPolicy signedPolicy =
            MatrixTileNumericPolicyAbi.CreateSupportedPolicy(
                MatrixTileNumericProfileId.SignedInt8ToInt32);
        MatrixTileInstructionIrProjection legal =
            MatrixTileIrProjectionAndMaterializer.ProjectDecodedVectorPayload(
                InstructionsEnum.MTILE_MACC,
                payload with { MatrixTileNumericPolicy = signedPolicy },
                immediate: 2,
                requireExplicitNumericPolicy: true);

        Assert.True(legal.IsRuntimeLegal);
        Assert.True(legal.HasExplicitNumericPolicyProjection);
        Assert.Equal(signedPolicy, legal.MaccContract!.Value.NumericPolicy);
        Assert.Equal((ushort)4, legal.ResultTileDescriptor.ElementSizeBytes);

        MatrixTileNumericPolicy unsignedPolicy =
            MatrixTileNumericPolicyAbi.CreateSupportedPolicy(
                MatrixTileNumericProfileId.UnsignedInt8ToUInt32);
        MatrixTileInstructionIrProjection mismatch =
            MatrixTileIrProjectionAndMaterializer.ProjectDecodedVectorPayload(
                InstructionsEnum.MTILE_MACC,
                payload with { MatrixTileNumericPolicy = unsignedPolicy },
                immediate: 2,
                requireExplicitNumericPolicy: true);

        Assert.Equal(MatrixTileIrProjectionFaultKind.NumericPolicyFault, mismatch.FaultKind);
        Assert.Equal(
            MatrixTileNumericPolicyFaultKind.ContradictoryElementType,
            mismatch.NumericPolicyValidation!.Value.FaultKind);
    }

    [Fact]
    public void RuntimeMaterializer_RejectsMissingPolicyBeforeMicroOpCreation()
    {
        DecoderContext missing = CreateContext(numericPolicy: null);
        DecodeProjectionFaultException exception =
            Assert.Throws<DecodeProjectionFaultException>(
                () => InstructionRegistry.CreateMicroOp(
                    (uint)InstructionsEnum.MTILE_MACC,
                    missing));
        Assert.Contains("NumericPolicyFault", exception.Message, StringComparison.Ordinal);

        MatrixTileNumericPolicy policy =
            MatrixTileNumericPolicyAbi.CreateSupportedPolicy(
                MatrixTileNumericProfileId.SignedInt8ToInt32);
        DecoderContext explicitContext = CreateContext(policy);
        MatrixTileMicroOp microOp = Assert.IsType<MtileMaccMicroOp>(
            InstructionRegistry.CreateMicroOp(
                (uint)InstructionsEnum.MTILE_MACC,
                explicitContext));

        Assert.Equal(policy, microOp.MaccContract!.Value.NumericPolicy);
        Assert.True(microOp.MaccContract.Value.HasExplicitNumericPolicy);
        Assert.False(microOp.UsesFallbackPath);
    }

    [Fact]
    public void NumericSensitivePackageAndCompilerHandoffAreClosedAfterSidebandConformance()
    {
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasExplicitNumericPolicyAbi);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase15SupportedNumericProfileMatrix);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase16FormalMaccArithmetic);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase16LayoutPolicyAbi);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase17PolicyBoundCaptureIdentity);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase17PolicyBoundReplayIdentity);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase18MachineReadableNumericLayoutGoldenCorpus);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase19RuntimeNumericEvidenceReady);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase19CompilerCarrierConformanceReady);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase19CompilerNoFallbackConformanceReady);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase19CompilerRuntimeRejectionConformanceReady);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase19CompilerSidebandConformanceReady);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase19PackageReclosureReady);
        Assert.Equal(
            "ClosedCompilerMatrixTileLoweredAnnotationsCarryNumericLayoutPolicySidebands",
            MatrixTileRuntimeIsaPackageContract.Phase19CompilerConformanceDecision);
        Assert.Equal(
            "NonePhase19CompilerSidebandConformanceClosed",
            MatrixTileRuntimeIsaPackageContract.Phase19CompilerConformanceBlocker);
        Assert.True(MatrixTileRuntimeIsaPackageContract.NumericSensitivePackageReadiness);
        Assert.True(MatrixTileRuntimeIsaPackageContract.NumericSensitiveClosesGoldenArtifacts);
        Assert.True(MatrixTileRuntimeIsaPackageContract.PositiveNumericHandoffReady);
        Assert.False(
            MatrixTileRuntimeIsaPackageContract.StatusCatalogPromotionIsProvisionalDuringNumericReclosure);
        MatrixTileRuntimeIsaPackageContract.RequirePositiveCompilerEmissionReadiness();
    }

    private static VectorInstructionPayload CreatePayload(DataTypeEnum dataType) =>
        new(
            PrimaryPointer: 1,
            SecondaryPointer: (3UL << 16) | 2UL,
            StreamLength: 4,
            Stride: 1,
            RowStride: 2,
            Indexed: false,
            Is2D: true,
            TailAgnostic: false,
            MaskAgnostic: false,
            Saturating: false,
            PredicateMask: 0,
            DataType: (byte)dataType)
        {
            MatrixTileLayoutPolicy = MatrixTileLayoutPolicyAbi.CreateMaccPolicy()
        };

    private static DecoderContext CreateContext(
        MatrixTileNumericPolicy? numericPolicy) =>
        new()
        {
            OpCode = (uint)InstructionsEnum.MTILE_MACC,
            Immediate = 2,
            HasImmediate = true,
            DataType = (byte)DataTypeEnum.INT8,
            HasDataType = true,
            VectorPrimaryPointer = 1,
            VectorSecondaryPointer = (3UL << 16) | 2UL,
            VectorStreamLength = 4,
            VectorStride = 1,
            VectorRowStride = 2,
            MatrixTileNumericPolicy = numericPolicy,
            MatrixTileLayoutPolicy = MatrixTileLayoutPolicyAbi.CreateMaccPolicy(),
            HasVectorPayload = true,
            HasVectorAddressingContour = true,
            Is2DAddressing = true,
            IndexedAddressing = false,
            PredicateMask = 0
        };
}
