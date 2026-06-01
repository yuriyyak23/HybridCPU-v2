using System;
using System.Linq;
using System.Reflection;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;
using CloseToRtlDsc2 = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.CarrierV2.Dsc2DescriptorCarrier;
using CloseToRtlDscCancel = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.QueueLifecycle.DscCancelInstruction;
using CloseToRtlDscCommit = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.QueueLifecycle.DscCommitInstruction;
using CloseToRtlDscFence = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.QueueLifecycle.DscFenceInstruction;
using CloseToRtlDscPoll = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.QueueLifecycle.DscPollInstruction;
using CloseToRtlDscQueryBackend = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.Queries.DscQueryBackendInstruction;
using CloseToRtlDscQueryShape = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.Queries.DscQueryShapeInstruction;
using CloseToRtlDscWait = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.QueueLifecycle.DscWaitInstruction;

namespace HybridCPU_ISE.Tests.Phase11;

public sealed class Phase11Lane6QueueQueryDsc2FailClosedTests
{
    private static readonly string[] QueueControlMnemonics =
    [
        "DSC_POLL",
        "DSC_WAIT",
        "DSC_CANCEL",
        "DSC_FENCE",
        "DSC_COMMIT"
    ];

    private static readonly string[] QueryMnemonics =
    [
        "DSC_QUERY_BACKEND",
        "DSC_QUERY_SHAPE"
    ];

    [Fact]
    public void Phase11Rows_RemainReservedOrParserOnlyWithoutProductionPublication()
    {
        foreach (string mnemonic in QueueControlMnemonics)
        {
            AssertReservedNoAllocationRow(mnemonic, "Lane6QueueControl");
        }

        foreach (string mnemonic in QueryMnemonics)
        {
            AssertReservedNoAllocationRow(mnemonic, "Lane6DscQuery");
        }

        AssertParserOnlyDsc2Row();
    }

    [Theory]
    [InlineData(typeof(CloseToRtlDscPoll), "DSC_POLL", "RequiresRetireOwnedPublication", "")]
    [InlineData(typeof(CloseToRtlDscWait), "DSC_WAIT", "RequiresCommandScopeAbi", "RequiresRetireOwnedPublication")]
    [InlineData(typeof(CloseToRtlDscCancel), "DSC_CANCEL", "RequiresCommandScopeAbi", "RequiresRetireOwnedSideEffect")]
    [InlineData(typeof(CloseToRtlDscFence), "DSC_FENCE", "RequiresQueueOrderingAbi", "RequiresRetireOwnedSideEffect")]
    [InlineData(typeof(CloseToRtlDscCommit), "DSC_COMMIT", "RequiresStagedCommitAuthority", "RequiresRetireOwnedSideEffect")]
    public void QueueLeafMarkers_RecordPhase11NegativeDecisionGate(
        Type templateType,
        string mnemonic,
        string requiredMarker,
        string optionalMarker)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "OperandShape")));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "ParameterDescriptor")));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "MicroOpShape")));
        Assert.Equal("Lane6QueueControlNoExecution", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.Equal("GenericLane6QueueRuntimeOnly", GetConstant<string>(templateType, "QueueAuthorityBoundary"));
        AssertCommonQueueQueryFailClosedMarkers(templateType);
        Assert.True(GetConstant<bool>(templateType, "IsQueueControlOwned"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresQueueAuthority"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresTokenNamespaceAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresQueueHandleAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresTokenLifecycleAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresQueueOwnershipModel"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresQueueStateModel"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresQueueRollbackJournal"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresQueueRuntimeAdmission"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresTypedQueueMicroOp"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresQueueCommandEncoding"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoRetirePublicationBeforeQueueAuthority"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, requiredMarker), templateType.FullName);

        if (!string.IsNullOrEmpty(optionalMarker))
        {
            Assert.True(GetConstant<bool>(templateType, optionalMarker), templateType.FullName);
        }
    }

    [Theory]
    [InlineData(typeof(CloseToRtlDscQueryBackend), "DSC_QUERY_BACKEND", "RequiresBackendCapabilityAbi")]
    [InlineData(typeof(CloseToRtlDscQueryShape), "DSC_QUERY_SHAPE", "RequiresShapeQueryAbi")]
    public void QueryLeafMarkers_RecordPhase11NegativeDecisionGate(
        Type templateType,
        string mnemonic,
        string queryMarker)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "OperandShape")));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "ParameterDescriptor")));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "MicroOpShape")));
        Assert.Equal("Lane6CapabilityQueryNoExecution", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.Equal("GenericLane6CapabilityQueryRuntimeOnly", GetConstant<string>(templateType, "QueryAuthorityBoundary"));
        AssertCommonQueueQueryFailClosedMarkers(templateType);
        Assert.True(GetConstant<bool>(templateType, "IsCapabilityQuery"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "IsReadOnlyQuery"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresCapabilityQueryAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresQuerySelectorAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresCapabilityResultAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresResultScrubbingPolicy"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresQueryRuntimeAdmission"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresTypedQueryMicroOp"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresBoundedResultFootprint"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRetireOwnedPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayStableResult"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayRollbackConformance"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoHostEvidenceLeak"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoRetirePublicationBeforeQueryAuthority"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, queryMarker), templateType.FullName);
    }

    [Fact]
    public void Dsc2LeafMarkers_RecordPhase11ParserOnlyNegativeDecisionGate()
    {
        Type templateType = typeof(CloseToRtlDsc2);

        Assert.Equal("DSC2", GetConstant<string>(templateType, "Mnemonic"));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "OperandShape")));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "ParameterDescriptor")));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "MicroOpShape")));
        Assert.Equal("Lane06DmaStream", GetConstant<string>(templateType, "ExecutionLaneBinding"));
        Assert.Equal("ParserOnlyCarrierNoExecution", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.Equal("ParserOnlyLane6DescriptorV2Carrier", GetConstant<string>(templateType, "CarrierAuthorityBoundary"));
        Assert.Equal("GenericRuntimeOnly", GetConstant<string>(templateType, "VmxBoundary"));
        Assert.Equal("Phase11NegativeDecisionGate", GetConstant<string>(templateType, "ProductionDecision"));
        Assert.True(GetConstant<bool>(templateType, "IsDescriptorOwned"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "IsCarrierOnly"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "IsParserOnly"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresDescriptorV2Adr"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresDescriptorV2ParserManifest"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresBackwardCompatibleDecoder"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresDescriptorV2ExecutionPolicy"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresDescriptorV2AdmissionPolicy"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRuntimeAdmission"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRetireCommitAuthority"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresDescriptorV2RetireReplayPolicy"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayDeterminism"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresParserOnlyConformance"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresDescriptorV2GoldenArtifacts"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoDsc2ExecutionBeforeAdr"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoDescriptorV2ExecutionBeforeAdr"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "ParserAcceptanceIsNotExecutionEvidence"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoGuestVisibleHostEvidence"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoHostEvidenceLeak"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoHostOwnedEvidencePublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "ExistingDmaStreamComputeEvidenceIsInsufficient"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "ExistingDscStatusEvidenceIsInsufficient"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "ExistingDscQueryCapsEvidenceIsInsufficient"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "Phase10DescriptorOpEvidenceIsInsufficient"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoScalarOpcodePublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoExecutableDecoderEncoderAbiPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoInstructionIrProjectionPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoRegistryMaterializerPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoTypedMicroOpPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoSchedulerLaneBindingPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoRuntimeAdmissionPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoExecutionCapturePublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoRetireCommitPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoReplayRollbackPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoCompilerHelperEmission"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoHiddenScalarLowering"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoHiddenVectorLowering"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoMultiOpEmission"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoParserToExecutionPromotion"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoDmaStreamComputeFallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoDscStatusFallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoDscQueryCapsFallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoQueueRuntimeFallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoLane7Fallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoExternalBackendFallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoVmxSpecificPath"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "HasScalarOpcodeAllocation"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"), templateType.FullName);
        AssertNoStaticOpcodeOrExecuteSurface(templateType);
    }

    [Fact]
    public void ClosedDscStatusAndQueryCapsEvidence_DoesNotAuthorizePhase11Rows()
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("DSC_STATUS");
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.True(status.IsExecutableClaim);
        Assert.True(Enum.IsDefined(InstructionsEnum.DSC_STATUS));
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.DSC_STATUS));

        InstructionSupportStatus queryCaps = InstructionSupportStatusCatalog.GetStatus("DSC_QUERY_CAPS");
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, queryCaps.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, queryCaps.RuntimeEvidence);
        Assert.True(queryCaps.IsExecutableClaim);
        Assert.True(Enum.IsDefined(InstructionsEnum.DSC_QUERY_CAPS));
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.DSC_QUERY_CAPS));

        foreach (string mnemonic in QueueControlMnemonics)
        {
            AssertReservedNoAllocationRow(mnemonic, "Lane6QueueControl");
        }

        foreach (string mnemonic in QueryMnemonics)
        {
            AssertReservedNoAllocationRow(mnemonic, "Lane6DscQuery");
        }

        AssertParserOnlyDsc2Row();
    }

    [Fact]
    public void VectorLegalityMatrix_DoesNotTreatLane6QueueQueryDsc2AsExecutableVectorContours()
    {
        foreach (string mnemonic in QueueControlMnemonics.Concat(QueryMnemonics).Append("DSC2"))
        {
            Assert.DoesNotContain(
                VectorLegalityMatrix.Rows,
                row =>
                    row.FamilyName.Contains(mnemonic, StringComparison.Ordinal) ||
                    row.RuntimeEvidenceNote.Contains(mnemonic, StringComparison.Ordinal));
        }

        Assert.DoesNotContain(VectorLegalityMatrix.Rows, row =>
            row.FamilyName is
                "Lane6QueueControlNoExecution" or
                "Lane6CapabilityQueryNoExecution" or
                "ParserOnlyCarrierNoExecution");
    }

    private static void AssertReservedNoAllocationRow(string mnemonic, string expectedExtension)
    {
        Assert.True(InstructionSupportStatusCatalog.TryGetExplicitStatus(
            mnemonic,
            out InstructionSupportStatus status));
        Assert.Equal(IsaInstructionStatus.Reserved, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.None, status.RuntimeEvidence);
        Assert.Equal(expectedExtension, status.ExtensionName);
        Assert.False(status.HasNumericOpcode);
        Assert.False(status.HasRuntimeOpcodeMetadata);
        Assert.False(status.HasCanonicalDecoderAcceptance);
        Assert.False(status.HasRegistryFactory);
        Assert.False(status.HasExecutionSemantics);
        Assert.False(status.IsExecutableClaim);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalDisabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ParserOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.DescriptorOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.PipelineClassMap.Keys);
        Assert.False(HasEnum(mnemonic));
        Assert.False(HasIsaOpcodeValue(mnemonic));
        Assert.False(HasRegistryMnemonic(mnemonic));
    }

    private static void AssertParserOnlyDsc2Row()
    {
        Assert.True(InstructionSupportStatusCatalog.TryGetExplicitStatus(
            "DSC2",
            out InstructionSupportStatus status));
        Assert.Equal(IsaInstructionStatus.ParserOnly, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.DeclaredOnly, status.RuntimeEvidence);
        Assert.Equal("Lane6DSC", status.ExtensionName);
        Assert.False(status.HasNumericOpcode);
        Assert.False(status.HasRuntimeOpcodeMetadata);
        Assert.False(status.HasCanonicalDecoderAcceptance);
        Assert.False(status.HasRegistryFactory);
        Assert.False(status.HasExecutionSemantics);
        Assert.False(status.IsExecutableClaim);
        Assert.Contains("DSC2", IsaV4Surface.ParserOnlyOpcodes);
        Assert.DoesNotContain("DSC2", IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain("DSC2", IsaV4Surface.OptionalDisabledOpcodes);
        Assert.DoesNotContain("DSC2", IsaV4Surface.DescriptorOnlyOpcodes);
        Assert.DoesNotContain("DSC2", IsaV4Surface.PipelineClassMap.Keys);
        Assert.False(HasEnum("DSC2"));
        Assert.False(HasIsaOpcodeValue("DSC2"));
        Assert.False(HasRegistryMnemonic("DSC2"));
    }

    private static void AssertCommonQueueQueryFailClosedMarkers(Type templateType)
    {
        Assert.Equal("Lane06DmaStream", GetConstant<string>(templateType, "ExecutionLaneBinding"));
        Assert.Equal("GenericRuntimeOnly", GetConstant<string>(templateType, "VmxBoundary"));
        Assert.Equal("Phase11NegativeDecisionGate", GetConstant<string>(templateType, "ProductionDecision"));
        Assert.True(GetConstant<bool>(templateType, "RequiresDecoderEncoderAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresInstructionIrProjection"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRegistryMaterializer"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresSchedulerLaneBinding"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresFutureRetireReplayEvidence"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresGoldenArtifacts"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoGuestVisibleHostEvidence"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoHostOwnedEvidencePublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "ExistingDscStatusEvidenceIsInsufficient"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "ExistingDscQueryCapsEvidenceIsInsufficient"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "Dsc2ParserEvidenceIsInsufficient"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoScalarOpcodePublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoDecoderEncoderAbiPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoInstructionIrProjectionPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoRegistryMaterializerPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoTypedMicroOpPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoSchedulerLaneBindingPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoExecutionCapturePublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoReplayRollbackPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoCompilerHelperEmission"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoHiddenScalarLowering"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoHiddenVectorLowering"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoMultiOpEmission"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoDmaStreamComputeFallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoDscStatusFallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoDscQueryCapsFallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoDsc2Fallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoLane7Fallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoExternalBackendFallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoVmxSpecificPath"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "RequiresImmediateVmxProjection"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresFutureVirtualizationBoundaryPolicy"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "HasScalarOpcodeAllocation"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"), templateType.FullName);
        AssertNoStaticOpcodeOrExecuteSurface(templateType);
    }

    private static void AssertNoStaticOpcodeOrExecuteSurface(Type templateType)
    {
        Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetField("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));
    }

    private static bool HasEnum(string mnemonic)
    {
        string enumCandidate = mnemonic.Replace(".", "_", StringComparison.Ordinal);
        return Enum.GetNames<InstructionsEnum>().Contains(enumCandidate);
    }

    private static bool HasIsaOpcodeValue(string mnemonic)
    {
        string enumCandidate = mnemonic.Replace(".", "_", StringComparison.Ordinal);
        return typeof(Processor.CPU_Core.IsaOpcodeValues).GetField(
            enumCandidate,
            BindingFlags.Public | BindingFlags.Static) is not null;
    }

    private static bool HasRegistryMnemonic(string mnemonic) =>
        OpcodeRegistry.Opcodes.Any(info =>
            string.Equals(info.Mnemonic, mnemonic, StringComparison.OrdinalIgnoreCase));

    private static T GetConstant<T>(Type type, string name)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{type.FullName}.{name} was not found.");
        Assert.True(field.IsLiteral, $"{type.FullName}.{name} must remain a const template marker.");
        return Assert.IsType<T>(field.GetRawConstantValue());
    }
}
