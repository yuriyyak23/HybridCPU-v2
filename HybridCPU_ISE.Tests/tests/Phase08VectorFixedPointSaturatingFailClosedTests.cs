using System;
using System.Linq;
using System.Reflection;
using HybridCPU.Compiler.Core.IR;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;
using CloseToRtlVavg = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VavgInstruction;
using CloseToRtlVavgR = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VavgRInstruction;
using CloseToRtlVclip = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VclipInstruction;
using CloseToRtlVmulSat = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VmulSatInstruction;
using CloseToRtlVscanMax = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PrefixScan.VscanMaxInstruction;
using CloseToRtlVscanMin = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PrefixScan.VscanMinInstruction;
using CloseToRtlVsllSat = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VsllSatInstruction;
using CloseToRtlVsraSat = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VsraSatInstruction;
using CloseToRtlVsrlSat = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VsrlSatInstruction;
using CloseToRtlVsubSat = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VsubSatInstruction;

namespace HybridCPU_ISE.Tests.Phase08;

public sealed class Phase08VectorFixedPointSaturatingFailClosedTests
{
    private static readonly string[] Phase08Mnemonics =
    [
        "VSUB.SAT", "VMUL.SAT", "VSLL.SAT", "VSRL.SAT", "VSRA.SAT",
        "VAVG", "VAVG.R", "VCLIP", "VSCAN.MIN", "VSCAN.MAX"
    ];

    [Fact]
    public void Phase08Rows_RemainReservedNoAllocationAndDoNotPublishOpcodeSurface()
    {
        foreach (string mnemonic in Phase08Mnemonics)
        {
            Assert.True(InstructionSupportStatusCatalog.TryGetExplicitStatus(
                mnemonic,
                out InstructionSupportStatus status));
            Assert.Equal(IsaInstructionStatus.Reserved, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.None, status.RuntimeEvidence);
            Assert.False(status.HasNumericOpcode);
            Assert.False(status.HasRuntimeOpcodeMetadata);
            Assert.False(status.HasCanonicalDecoderAcceptance);
            Assert.False(status.HasRegistryFactory);
            Assert.False(status.HasExecutionSemantics);
            Assert.False(status.IsExecutableClaim);
            Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
            Assert.DoesNotContain(mnemonic, IsaV4Surface.PipelineClassMap.Keys);
            Assert.False(HasEnumOrRegistryMnemonic(mnemonic));

            string expectedExtension = mnemonic.StartsWith("VSCAN.", StringComparison.Ordinal)
                ? "VectorScanSegmentMovement"
                : "VectorSaturatingFixedPoint";
            Assert.Equal(expectedExtension, status.ExtensionName);
        }
    }

    [Fact]
    public void FixedPointSaturationRows_HaveCompilerVisiblePlanningOnlyNoEmissionContracts()
    {
        Assert.Equal(
            CompilerFailClosedEmissionInventory.VectorFixedPointSaturationMnemonics.Order(StringComparer.Ordinal),
            CompilerVectorVlmBlockedAbiContract.AllVlmBlockedRows
                .Where(static contract => contract.AbiClass == CompilerVectorVlmBlockedAbiClass.FixedPointSaturation)
                .Select(static contract => contract.Mnemonic)
                .Order(StringComparer.Ordinal));

        foreach (CompilerFailClosedEmissionRow row in CompilerFailClosedEmissionInventory.VectorFixedPointSaturationRows)
        {
            CompilerVectorVlmBlockedAbiContract contract = Assert.Single(
                CompilerVectorVlmBlockedAbiContract.AllVlmBlockedRows,
                contract => contract.Mnemonic == row.Mnemonic);

            bool isSaturatingShift = row.Mnemonic is "VSLL.SAT" or "VSRL.SAT" or "VSRA.SAT";
            bool mayRemainReserved = row.Mnemonic is "VSRL.SAT" or "VSRA.SAT";
            Assert.Equal("VectorFixedPointSaturationVlmBlocked", contract.ExtensionName);
            Assert.Equal("VectorFixedPointSaturatingFailClosed", contract.EvidenceBoundary);
            Assert.False(contract.CompilerEmissionAllowed);
            Assert.False(contract.CompilerHelperAllowed);
            Assert.False(contract.TypedFacadeAllowed);
            Assert.False(contract.TypedHelperAllowed);
            Assert.True(contract.RequiresSaturatingPolicyAbi);
            Assert.True(contract.RequiresSignednessWidthClampPolicy);
            Assert.True(contract.RequiresElementWidthLmulVlAbi);
            Assert.True(contract.RequiresSignednessAbi);
            Assert.True(contract.RequiresOverflowPolicyAbi);
            Assert.True(contract.RequiresVlmMaterializationPolicy);
            Assert.True(contract.RequiresStagedPublicationRetirePolicy);
            Assert.True(contract.RequiresReplayRollbackGoldenEvidence);
            Assert.Equal(isSaturatingShift, contract.RequiresShiftOperandAbi);
            Assert.Equal(isSaturatingShift, contract.RequiresSaturatingShiftPolicyAbi);
            Assert.Equal(isSaturatingShift, contract.RequiresSaturatingShiftMeaningDecision);
            Assert.Equal(mayRemainReserved, contract.MayRemainReservedIfNonMeaningful);
            Assert.True(contract.SeparateFromClosedVaddSat);
            Assert.True(contract.NoVaddSatFallback);
            Assert.True(contract.NoBaseVectorArithmeticFallback);
            Assert.True(contract.NoBaseVectorShiftFallback);
            Assert.True(contract.NoScalarHelperFallback);
            Assert.True(contract.NoLane6StreamFallback);
            Assert.True(contract.NoLane7AcceleratorFallback);
            Assert.True(contract.NoVmxSpecificPathFallback);
            Assert.True(contract.NoExecutableRowAliasPromotion);
            Assert.True(contract.RejectsSaturatingAddAliasPromotion);
            Assert.True(contract.RejectsAverageClipAliasPromotion);
            Assert.True(contract.RejectsBaseArithmeticOrShiftAliasPromotion);
            Assert.Contains("SaturatingPolicyAbi", contract.RequiredPolicyDecisions);
            Assert.Contains("VlmMaterializationPolicy", contract.RequiredPolicyDecisions);
            Assert.Contains("StagedPublicationRetirePolicy", contract.RequiredPolicyDecisions);
            Assert.Contains("ReplayRollbackGoldenEvidence", contract.RequiredPolicyDecisions);
            Assert.Contains("RuntimeMaterializerEvidence", contract.RequiredPolicyDecisions);
            Assert.Contains("SeparateFromClosedVaddSat", contract.RequiredPolicyDecisions);
            Assert.Contains("NoVaddSatFallback", contract.RequiredPolicyDecisions);
            Assert.Contains("NoBaseVectorArithmeticFallback", contract.RequiredPolicyDecisions);
            Assert.Contains("NoBaseVectorShiftFallback", contract.RequiredPolicyDecisions);
            Assert.Contains("NoScalarHelperFallback", contract.RequiredPolicyDecisions);
            Assert.Contains("NoLane6StreamFallback", contract.RequiredPolicyDecisions);
            Assert.Contains("NoLane7AcceleratorFallback", contract.RequiredPolicyDecisions);
            Assert.Contains("NoVmxSpecificPathFallback", contract.RequiredPolicyDecisions);
            Assert.Contains("NoExecutableRowAliasPromotion", contract.RequiredPolicyDecisions);
            if (isSaturatingShift)
            {
                Assert.Contains("SaturatingShiftPolicyAbi", contract.RequiredPolicyDecisions);
            }

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                contract.RequireCompilerHelperAuthority);
            Assert.Contains($"{row.Mnemonic} typed compiler helper emission is blocked", exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FixedPointAverageClipRows_HaveCompilerVisiblePlanningOnlyNoEmissionContracts()
    {
        Assert.Equal(
            CompilerFailClosedEmissionInventory.VectorFixedPointAverageClipMnemonics.Order(StringComparer.Ordinal),
            CompilerVectorVlmBlockedAbiContract.AllVlmBlockedRows
                .Where(static contract => contract.AbiClass == CompilerVectorVlmBlockedAbiClass.FixedPointAverageClip)
                .Select(static contract => contract.Mnemonic)
                .Order(StringComparer.Ordinal));

        foreach (CompilerFailClosedEmissionRow row in CompilerFailClosedEmissionInventory.VectorFixedPointAverageClipRows)
        {
            CompilerVectorVlmBlockedAbiContract contract = Assert.Single(
                CompilerVectorVlmBlockedAbiContract.AllVlmBlockedRows,
                contract => contract.Mnemonic == row.Mnemonic);

            bool isAverage = row.Mnemonic is "VAVG" or "VAVG.R";
            bool isRoundedAverage = row.Mnemonic is "VAVG.R";
            bool isClip = row.Mnemonic is "VCLIP";
            Assert.Equal("VectorFixedPointAverageClipVlmBlocked", contract.ExtensionName);
            Assert.Equal("VectorFixedPointSaturatingFailClosed", contract.EvidenceBoundary);
            Assert.False(contract.CompilerEmissionAllowed);
            Assert.False(contract.CompilerHelperAllowed);
            Assert.False(contract.TypedFacadeAllowed);
            Assert.False(contract.TypedHelperAllowed);
            Assert.True(contract.IsFixedPointAverageClip);
            Assert.Equal(isAverage, contract.IsFixedPointAverage);
            Assert.Equal(isRoundedAverage, contract.IsRoundedFixedPointAverage);
            Assert.Equal(isClip, contract.IsFixedPointClip);
            Assert.True(contract.RequiresElementWidthLmulVlAbi);
            Assert.True(contract.RequiresSignednessAbi);
            Assert.True(contract.RequiresRoundingTruncationPolicyAbi);
            Assert.True(contract.RequiresOverflowPolicyAbi);
            Assert.True(contract.RequiresVlmMaterializationPolicy);
            Assert.True(contract.RequiresStagedPublicationRetirePolicy);
            Assert.True(contract.RequiresReplayRollbackGoldenEvidence);
            Assert.Equal(isAverage, contract.RequiresAveragePolicyAbi);
            Assert.Equal(isRoundedAverage, contract.RequiresRoundingPolicyAbi);
            Assert.Equal(isClip, contract.RequiresClipBoundsAbi);
            Assert.Equal(isClip, contract.RequiresNarrowingPolicyAbi);
            Assert.Equal(isClip, contract.RequiresResultWidthPolicyAbi);
            Assert.True(contract.NoVaddSatFallback);
            Assert.True(contract.NoFixedPointSaturationFallback);
            Assert.True(contract.NoBaseVectorArithmeticFallback);
            Assert.True(contract.NoBaseVectorShiftFallback);
            Assert.True(contract.NoNarrowWidenConvertFallback);
            Assert.True(contract.NoScalarHelperFallback);
            Assert.True(contract.NoLane6StreamFallback);
            Assert.True(contract.NoLane7AcceleratorFallback);
            Assert.True(contract.NoVmxSpecificPathFallback);
            Assert.True(contract.NoExecutableRowAliasPromotion);
            Assert.Contains("RoundingTruncationPolicyAbi", contract.RequiredPolicyDecisions);
            Assert.Contains("VlmMaterializationPolicy", contract.RequiredPolicyDecisions);
            Assert.Contains("StagedPublicationRetirePolicy", contract.RequiredPolicyDecisions);
            Assert.Contains("ReplayRollbackGoldenEvidence", contract.RequiredPolicyDecisions);
            Assert.Contains("NoVaddSatFallback", contract.RequiredPolicyDecisions);
            Assert.Contains("NoFixedPointSaturationFallback", contract.RequiredPolicyDecisions);
            Assert.Contains("NoBaseVectorArithmeticFallback", contract.RequiredPolicyDecisions);
            Assert.Contains("NoBaseVectorShiftFallback", contract.RequiredPolicyDecisions);
            Assert.Contains("NoNarrowWidenConvertFallback", contract.RequiredPolicyDecisions);
            Assert.Contains("NoScalarHelperFallback", contract.RequiredPolicyDecisions);
            Assert.Contains("NoLane6StreamFallback", contract.RequiredPolicyDecisions);
            Assert.Contains("NoLane7AcceleratorFallback", contract.RequiredPolicyDecisions);
            Assert.Contains("NoVmxSpecificPathFallback", contract.RequiredPolicyDecisions);
            Assert.Contains("NoExecutableRowAliasPromotion", contract.RequiredPolicyDecisions);
            if (isAverage)
            {
                Assert.Contains("AveragePolicyAbi", contract.RequiredPolicyDecisions);
            }

            if (isRoundedAverage)
            {
                Assert.Contains("RoundingPolicyAbi", contract.RequiredPolicyDecisions);
            }

            if (isClip)
            {
                Assert.Contains("ClipBoundsAbi", contract.RequiredPolicyDecisions);
                Assert.Contains("NarrowingPolicyAbi", contract.RequiredPolicyDecisions);
                Assert.Contains("ResultWidthPolicyAbi", contract.RequiredPolicyDecisions);
            }

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                contract.RequireCompilerHelperAuthority);
            Assert.Contains($"{row.Mnemonic} typed compiler helper emission is blocked", exception.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(typeof(CloseToRtlVsubSat), "VSUB.SAT", false, false, "saturating subtract")]
    [InlineData(typeof(CloseToRtlVmulSat), "VMUL.SAT", false, false, "saturating multiply")]
    [InlineData(typeof(CloseToRtlVsllSat), "VSLL.SAT", true, false, "saturating left shift")]
    [InlineData(typeof(CloseToRtlVsrlSat), "VSRL.SAT", true, true, "saturating logical right shift")]
    [InlineData(typeof(CloseToRtlVsraSat), "VSRA.SAT", true, true, "saturating arithmetic right shift")]
    public void SaturatingArithmeticAndShiftRows_RecordPhase08ANegativeDecisionGate(
        Type templateType,
        string mnemonic,
        bool expectsShiftAbi,
        bool mayRemainReserved,
        string policyFragment)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("VectorFixedPointSaturatingFailClosed", GetConstant<string>(templateType, "EvidenceBoundary"));
        AssertCommonFailClosedMarkers(templateType);
        Assert.True(GetConstant<bool>(templateType, "RequiresSaturatingPolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresElementWidthAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresSignednessAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresClampPolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresOverflowPolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresMaskTailPolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "SeparateFromClosedVaddSat"));
        Assert.Contains(policyFragment, GetConstant<string>(templateType, "SaturationPolicy"), StringComparison.Ordinal);

        if (expectsShiftAbi)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresShiftOperandAbi"));
            Assert.True(GetConstant<bool>(templateType, "RequiresSaturatingShiftMeaningDecision"));
            Assert.Contains("saturation", GetConstant<string>(templateType, "ShiftMeaningPolicy"), StringComparison.Ordinal);
        }

        if (mayRemainReserved)
        {
            Assert.True(GetConstant<bool>(templateType, "MayRemainReservedIfNonMeaningful"));
        }
    }

    [Theory]
    [InlineData(typeof(CloseToRtlVavg), "VAVG", false, "fixed-point average")]
    [InlineData(typeof(CloseToRtlVavgR), "VAVG.R", true, "rounded fixed-point average")]
    public void AverageRows_RecordPhase08BNegativeDecisionGate(
        Type templateType,
        string mnemonic,
        bool expectsRoundingPolicy,
        string policyFragment)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("VectorFixedPointSaturatingFailClosed", GetConstant<string>(templateType, "EvidenceBoundary"));
        AssertCommonFailClosedMarkers(templateType);
        Assert.True(GetConstant<bool>(templateType, "RequiresAveragePolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresElementWidthAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresSignednessAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresRoundingTruncationPolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresOverflowPolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresMaskTailPolicyAbi"));
        Assert.Contains(policyFragment, GetConstant<string>(templateType, "AveragePolicy"), StringComparison.Ordinal);

        if (expectsRoundingPolicy)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresRoundingPolicyAbi"));
        }
    }

    [Fact]
    public void Vclip_RecordsPhase08BClipAndNarrowingDecisionGate()
    {
        Type templateType = typeof(CloseToRtlVclip);

        Assert.Equal("VCLIP", GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("VectorFixedPointSaturatingFailClosed", GetConstant<string>(templateType, "EvidenceBoundary"));
        AssertCommonFailClosedMarkers(templateType);
        Assert.True(GetConstant<bool>(templateType, "RequiresClipBoundsAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresNarrowingPolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresResultWidthAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresSignednessAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresRoundingTruncationPolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresMaskTailPolicyAbi"));
        Assert.Contains("bounds encoding", GetConstant<string>(templateType, "ClipPolicy"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(CloseToRtlVscanMin), "VSCAN.MIN", "prefix min")]
    [InlineData(typeof(CloseToRtlVscanMax), "VSCAN.MAX", "prefix max")]
    public void PrefixScanRows_RecordPhase08CNegativeDecisionGate(
        Type templateType,
        string mnemonic,
        string policyFragment)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("VectorScanContourFailClosed", GetConstant<string>(templateType, "EvidenceBoundary"));
        AssertCommonFailClosedMarkers(templateType);
        Assert.True(GetConstant<bool>(templateType, "RequiresPrefixScanPolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresElementTypeSideband"));
        Assert.True(GetConstant<bool>(templateType, "RequiresSignednessAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresTailPolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresMaskPolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "SeparateFromClosedVscanSum"));
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayDeterminism"));
        Assert.Contains(policyFragment, GetConstant<string>(templateType, "ScanPolicy"), StringComparison.Ordinal);
    }

    [Fact]
    public void ClosedVaddSatAndVscanSumEvidence_DoesNotOpenPhase08Rows()
    {
        InstructionSupportStatus vaddSat = InstructionSupportStatusCatalog.GetStatus("VADD.SAT");
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, vaddSat.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, vaddSat.RuntimeEvidence);
        Assert.Equal("VectorSaturatingAddPolicy", vaddSat.ExtensionName);
        Assert.True(OpcodeRegistry.SupportsSaturatingAddPolicy((uint)InstructionsEnum.VADD));
        Assert.False(OpcodeRegistry.SupportsSaturatingAddPolicy((uint)InstructionsEnum.VSUB));
        Assert.False(OpcodeRegistry.SupportsSaturatingAddPolicy((uint)InstructionsEnum.VMUL));
        Assert.False(OpcodeRegistry.SupportsSaturatingAddPolicy((uint)InstructionsEnum.VSLL));

        VectorLegalityMatrixRow binaryRow = VectorLegalityMatrix.GetRow(InstructionsEnum.VADD);
        Assert.Equal("VectorBinaryComputeCarrier", binaryRow.FamilyName);
        Assert.Equal(VectorContourLegalityStatus.Executable, binaryRow.OneDimensional);

        InstructionSupportStatus vscanSum = InstructionSupportStatusCatalog.GetStatus("VSCAN.SUM");
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, vscanSum.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, vscanSum.RuntimeEvidence);

        VectorLegalityMatrixRow scanRow = VectorLegalityMatrix.GetRow(InstructionsEnum.VSCAN_SUM);
        Assert.Equal("VectorScanPrefixPublication", scanRow.FamilyName);
        Assert.Equal([InstructionsEnum.VSCAN_SUM], scanRow.Opcodes);
        Assert.DoesNotContain(VectorLegalityMatrix.Rows, row =>
            row.FamilyName == "VectorFixedPointSaturatingFailClosed" ||
            row.FamilyName == "VectorScanContourFailClosed");
    }

    private static void AssertCommonFailClosedMarkers(Type templateType)
    {
        Assert.Equal("Lanes00_03Vector", GetConstant<string>(templateType, "ExecutionLaneBinding"));
        Assert.Equal("GenericRuntimeOnly", GetConstant<string>(templateType, "VmxBoundary"));
        Assert.True(GetConstant<bool>(templateType, "RequiresVectorLegalityMatrixClosure"));
        Assert.True(GetConstant<bool>(templateType, "RequiresDecoderEncoderAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresInstructionIrProjection"));
        Assert.True(GetConstant<bool>(templateType, "RequiresRegistryMaterializer"));
        Assert.True(GetConstant<bool>(templateType, "RequiresTypedVectorMicroOp"));
        Assert.True(GetConstant<bool>(templateType, "RequiresRetireStagedPublication"));
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayRollbackConformance"));
        Assert.True(GetConstant<bool>(templateType, "RequiresGoldenArtifacts"));
        Assert.True(GetConstant<bool>(templateType, "NoDescriptorFallback"));
        Assert.True(GetConstant<bool>(templateType, "NoHiddenScalarLowering"));
        Assert.True(GetConstant<bool>(templateType, "NoMultiOpEmission"));
        Assert.True(GetConstant<bool>(templateType, "NoVmxSpecificPath"));
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"));
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"));
        Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));
    }

    private static bool HasEnumOrRegistryMnemonic(string mnemonic)
    {
        string enumCandidate = mnemonic.Replace(".", "_", StringComparison.Ordinal);
        bool hasEnum = Enum.GetNames<InstructionsEnum>().Contains(enumCandidate);
        bool hasRegistryMnemonic = OpcodeRegistry.Opcodes.Any(info =>
            string.Equals(info.Mnemonic, mnemonic, StringComparison.OrdinalIgnoreCase));

        return hasEnum || hasRegistryMnemonic;
    }

    private static T GetConstant<T>(Type type, string name)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{type.FullName}.{name} was not found.");
        Assert.True(field.IsLiteral, $"{type.FullName}.{name} must remain a const template marker.");
        return Assert.IsType<T>(field.GetRawConstantValue());
    }
}
