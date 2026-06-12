using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HybridCPU.Compiler.Core.IR;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;
using CloseToRtlAccelBindQueue = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.AcceleratorControl.QueueBinding.AccelBindQueueInstruction;
using CloseToRtlAccelClose = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.AcceleratorControl.Lifecycle.AccelCloseInstruction;
using CloseToRtlAccelOpen = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.AcceleratorControl.Lifecycle.AccelOpenInstruction;
using CloseToRtlAccelQueryAbi = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.AcceleratorControl.Topology.AccelQueryAbiInstruction;
using CloseToRtlAccelQueryTopology = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.AcceleratorControl.Topology.AccelQueryTopologyInstruction;
using CloseToRtlAccelUnbindQueue = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.AcceleratorControl.QueueBinding.AccelUnbindQueueInstruction;

namespace HybridCPU_ISE.Tests.Phase14;

public sealed class Phase14Lane7AcceleratorControlFailClosedTests
{
    private static IReadOnlyList<string> AcceleratorControlMnemonics =>
        CompilerFailClosedEmissionInventory.Lane7AcceleratorControlMnemonics;

    private static IReadOnlyList<string> CompilerForbiddenTokens =>
        CompilerFailClosedEmissionInventory.Lane7AcceleratorControlCompilerSourceFragments;

    private static readonly string[] VmxForbiddenTokens =
    [
        "ACCEL_QUERY_ABI",
        "ACCEL_QUERY_TOPOLOGY",
        "ACCEL_OPEN",
        "ACCEL_CLOSE",
        "ACCEL_BIND_QUEUE",
        "ACCEL_UNBIND_QUEUE",
        "AccelQueryAbiInstruction",
        "AccelQueryTopologyInstruction",
        "AccelOpenInstruction",
        "AccelCloseInstruction",
        "AccelBindQueueInstruction",
        "AccelUnbindQueueInstruction"
    ];

    [Fact]
    public void Phase14Rows_RemainReservedWithoutProductionPublication()
    {
        foreach (string mnemonic in AcceleratorControlMnemonics)
        {
            AssertReservedNoAllocationRow(mnemonic);
        }
    }

    [Fact]
    public void Phase14Docs_RecordNegativeGateNotExecutableClosure()
    {
        string phase14 = ReadProjectFile(
            "HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx/Docs/ImplPlan/PHASE_14_LANE7_ACCELERATOR_CONTROL.md");
        string tracking = ReadProjectFile(
            "HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx/Docs/NON_VMX_CLOSE_TO_RTL_IMPLEMENTATION_PLAN.md");

        Assert.Contains("Phase 14 is closed only as a negative production decision gate", phase14);
        Assert.Contains("does not open executable closure", phase14);
        Assert.Contains("Phase 14 negative decision gate closed", tracking);
        Assert.Contains("does not allocate opcodes", tracking);
    }

    [Theory]
    [InlineData(typeof(CloseToRtlAccelQueryAbi), "ACCEL_QUERY_ABI", "RequiresAcceleratorAbiQueryContract", "RequiresBoundedCapabilityResultFootprint")]
    [InlineData(typeof(CloseToRtlAccelQueryTopology), "ACCEL_QUERY_TOPOLOGY", "RequiresAcceleratorTopologyAbi", "RequiresBoundedTopologyResultFootprint")]
    public void TopologyCapabilityLeafMarkers_RecordPhase14NegativeDecisionGate(
        Type templateType,
        string mnemonic,
        string requiredCapabilityMarker,
        string boundedResultMarker)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("Lane7AcceleratorControlDeferred", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.Equal("Phase14NegativeDecisionGate", GetConstant<string>(templateType, "ProductionDecision"));
        Assert.Equal("Lane7AcceleratorCapabilityProductionPathOnly", GetConstant<string>(templateType, "CapabilityAuthorityBoundary"));
        AssertCommonAcceleratorFailClosedMarkers(templateType);
        Assert.True(GetConstant<bool>(templateType, "IsAcceleratorCapabilityQuery"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresCapabilityAuthority"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, boundedResultMarker), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresResultScrubbingPolicy"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayStableCapabilityModel"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresBackendCapabilityAuthority"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoCapabilityPublicationBeforeAuthority"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, requiredCapabilityMarker), templateType.FullName);
    }

    [Theory]
    [InlineData(typeof(CloseToRtlAccelOpen), "ACCEL_OPEN")]
    [InlineData(typeof(CloseToRtlAccelClose), "ACCEL_CLOSE")]
    public void LifecycleLeafMarkers_RecordPhase14NegativeDecisionGate(
        Type templateType,
        string mnemonic)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("Lane7AcceleratorControlDeferred", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.Equal("Phase14NegativeDecisionGate", GetConstant<string>(templateType, "ProductionDecision"));
        Assert.Equal("Lane7AcceleratorLifecycleProductionPathOnly", GetConstant<string>(templateType, "AcceleratorAuthorityBoundary"));
        AssertCommonAcceleratorFailClosedMarkers(templateType);
        Assert.True(GetConstant<bool>(templateType, "IsAcceleratorLifecycleControl"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresAcceleratorRuntimeAuthority"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresDeviceAuthority"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresTokenAuthority"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresHandleNamespaceAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresOpenCloseLifecycleAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayStableLifecycleModel"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoLifecycleStatePublicationBeforeRetire"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoBackendAdmissionBeforeAuthority"), templateType.FullName);
    }

    [Theory]
    [InlineData(typeof(CloseToRtlAccelBindQueue), "ACCEL_BIND_QUEUE")]
    [InlineData(typeof(CloseToRtlAccelUnbindQueue), "ACCEL_UNBIND_QUEUE")]
    public void QueueBindingLeafMarkers_RecordPhase14NegativeDecisionGate(
        Type templateType,
        string mnemonic)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("Lane7AcceleratorControlDeferred", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.Equal("Phase14NegativeDecisionGate", GetConstant<string>(templateType, "ProductionDecision"));
        Assert.Equal("Lane7AcceleratorQueueBindingProductionPathOnly", GetConstant<string>(templateType, "AcceleratorAuthorityBoundary"));
        AssertCommonAcceleratorFailClosedMarkers(templateType);
        Assert.True(GetConstant<bool>(templateType, "IsAcceleratorQueueBindingControl"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresAcceleratorRuntimeAuthority"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresQueueAuthority"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresTokenAuthority"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresLane6TokenAuthorityGate"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresBindUnbindQueueAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayStableQueueBindingModel"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoQueueBindingPublicationBeforeRetire"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoQueueBindingBeforeTokenAuthority"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "ExistingLane6DmaEvidenceIsInsufficient"), templateType.FullName);
    }

    [Fact]
    public void ExistingLane7AndTopologyEvidence_DoesNotAuthorizePhase14Rows()
    {
        foreach (string existingMnemonic in new[]
                 { "ACCEL_QUERY_CAPS", "ACCEL_SUBMIT", "ACCEL_POLL", "ACCEL_WAIT", "ACCEL_CANCEL", "ACCEL_FENCE", "ACCEL_STATUS" })
        {
            if (!InstructionSupportStatusCatalog.TryGetExplicitStatus(
                    existingMnemonic,
                    out InstructionSupportStatus status))
            {
                continue;
            }

            Assert.True(status.IsExecutableClaim || status.Status is not IsaInstructionStatus.OptionalEnabled);
        }

        foreach (string mnemonic in AcceleratorControlMnemonics)
        {
            AssertReservedNoAllocationRow(mnemonic);
        }
    }

    [Fact]
    public void VectorLegalityMatrix_DoesNotTreatPhase14RowsAsExecutableVectorContours()
    {
        foreach (string mnemonic in AcceleratorControlMnemonics)
        {
            Assert.DoesNotContain(
                VectorLegalityMatrix.Rows,
                row =>
                    row.FamilyName.Contains(mnemonic, StringComparison.Ordinal) ||
                    row.RuntimeEvidenceNote.Contains(mnemonic, StringComparison.Ordinal));
        }

        Assert.DoesNotContain(VectorLegalityMatrix.Rows, row =>
            row.FamilyName is
                "Lane7AcceleratorControlDeferred" or
                "Lane7AcceleratorCapabilityProductionPathOnly" or
                "Lane7AcceleratorLifecycleProductionPathOnly" or
                "Lane7AcceleratorQueueBindingProductionPathOnly");
    }

    [Fact]
    public void CompilerFacade_DoesNotExposePhase14HelpersOrHiddenLowering()
    {
        List<string> failures = [];
        foreach (string path in CompilerSourceScanner.EnumerateCompilerSourceFiles())
        {
            string source = File.ReadAllText(path);
            foreach (string token in CompilerForbiddenTokens)
            {
                if (source.Contains(token, StringComparison.Ordinal))
                {
                    failures.Add($"{Relative(path)} contains forbidden compiler token `{token}`.");
                }
            }
        }

        Assert.True(
            failures.Count == 0,
            "Phase 14 compiler helpers and hidden lowering must remain closed:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void CompilerDeferredAbi_RecordsAcceleratorControlPolicyWithoutEmissionAuthority()
    {
        CompilerLane7DeferredAbiContract[] rows =
        [
            .. CompilerLane7DeferredAbiContract.AcceleratorControlRows
        ];

        Assert.Equal(
            [
                "ACCEL_BIND_QUEUE",
                "ACCEL_CLOSE",
                "ACCEL_OPEN",
                "ACCEL_QUERY_ABI",
                "ACCEL_QUERY_TOPOLOGY",
                "ACCEL_UNBIND_QUEUE"
            ],
            rows.Select(static row => row.Mnemonic).Order(StringComparer.Ordinal).ToArray());

        foreach (CompilerLane7DeferredAbiContract contract in rows)
        {
            CompilerLane7DeferredAbiAssertions.AssertNoEmissionAuthority(
                contract,
                contract.Mnemonic,
                contract.AbiClass,
                "Lane7TopologyQueue",
                "Lane7AcceleratorControlDeferred");
            Assert.True(contract.RequiresOwnerDomainGuard);
            Assert.True(contract.RequiresCommandQueueSemantics);
            Assert.True(contract.RequiresNoHostEvidenceLeak);
            Assert.True(contract.RequiresMigrationCheckpointPolicy);
            Assert.True(contract.RequiresFutureVirtualizationBoundaryPolicy);
            Assert.True(contract.VmxMigrationCheckpointEvidenceIsInsufficient);
            Assert.True(contract.ExistingAccelSubmitEvidenceIsInsufficient);
            Assert.True(contract.ExistingAccelQueryCapsEvidenceIsInsufficient);
            Assert.True(contract.ExistingTopologyQueueTaxonomyEvidenceIsInsufficient);
            Assert.True(contract.ExistingLane7ControlPlaneEvidenceIsInsufficient);
            CompilerLane7DeferredAbiAssertions.AssertHiddenLoweringBlocked(contract);
            Assert.True(contract.NoGenericSystemOpFallback);
            Assert.True(contract.NoLane6DmaFallback);
            Assert.True(contract.NoLane7SubmitFallback);
            Assert.True(contract.NoExternalBackendFallback);
            CompilerLane7DeferredAbiAssertions.AssertPolicyDecisions(
                contract,
                "OwnerDomainGuard",
                "CommandQueueSemantics",
                "MigrationCheckpointPolicy",
                "FutureVirtualizationBoundaryPolicy");

            switch (contract.AbiClass)
            {
                case CompilerLane7DeferredAbiClass.AcceleratorCapability:
                    Assert.True(contract.IsAcceleratorCapabilityQuery);
                    Assert.False(contract.IsAcceleratorLifecycleControl);
                    Assert.False(contract.IsAcceleratorQueueBindingControl);
                    Assert.True(contract.RequiresCapabilityAuthority);
                    Assert.True(contract.RequiresResultScrubbingPolicy);
                    Assert.True(contract.RequiresRetireOwnedPublication);
                    Assert.True(contract.RequiresReplayStableCapabilityModel);
                    Assert.True(contract.RequiresBackendCapabilityAuthority);
                    Assert.True(contract.RequiresGuestVisibleCapabilityPolicy);
                    Assert.True(contract.VmxCapabilityEvidenceIsInsufficient);
                    Assert.True(contract.NoCapabilityPublicationBeforeAuthority);
                    CompilerLane7DeferredAbiAssertions.AssertPolicyDecisions(
                        contract,
                        "CapabilityAuthority",
                        "ResultScrubbingPolicy",
                        "RetireOwnedPublication",
                        "ReplayStableCapabilityModel",
                        "BackendCapabilityAuthority",
                        "GuestVisibleCapabilityPolicy",
                        "NoCapabilityPublicationBeforeAuthority");

                    if (contract.Mnemonic == "ACCEL_QUERY_ABI")
                    {
                        Assert.True(contract.RequiresAcceleratorAbiQueryContract);
                        Assert.False(contract.RequiresAcceleratorTopologyAbi);
                        Assert.True(contract.RequiresBoundedCapabilityResultFootprint);
                        Assert.False(contract.RequiresBoundedTopologyResultFootprint);
                        CompilerLane7DeferredAbiAssertions.AssertPolicyDecisions(
                            contract,
                            "AcceleratorAbiQueryContract",
                            "BoundedCapabilityResultFootprint");
                    }
                    else
                    {
                        Assert.False(contract.RequiresAcceleratorAbiQueryContract);
                        Assert.True(contract.RequiresAcceleratorTopologyAbi);
                        Assert.False(contract.RequiresBoundedCapabilityResultFootprint);
                        Assert.True(contract.RequiresBoundedTopologyResultFootprint);
                        CompilerLane7DeferredAbiAssertions.AssertPolicyDecisions(
                            contract,
                            "AcceleratorTopologyAbi",
                            "BoundedTopologyResultFootprint");
                    }

                    break;
                case CompilerLane7DeferredAbiClass.AcceleratorLifecycle:
                    Assert.False(contract.IsAcceleratorCapabilityQuery);
                    Assert.True(contract.IsAcceleratorLifecycleControl);
                    Assert.False(contract.IsAcceleratorQueueBindingControl);
                    Assert.True(contract.RequiresAcceleratorRuntimeAuthority);
                    Assert.True(contract.RequiresDeviceAuthority);
                    Assert.True(contract.RequiresTokenAuthority);
                    Assert.True(contract.RequiresHandleNamespaceAbi);
                    Assert.True(contract.RequiresOpenCloseLifecycleAbi);
                    Assert.True(contract.RequiresReplayStableLifecycleModel);
                    Assert.True(contract.RequiresRetireOwnedSideEffectPublication);
                    Assert.True(contract.VmxBackendAuthorityEvidenceIsInsufficient);
                    Assert.True(contract.NoLifecycleStatePublicationBeforeRetire);
                    Assert.True(contract.NoBackendAdmissionBeforeAuthority);
                    CompilerLane7DeferredAbiAssertions.AssertPolicyDecisions(
                        contract,
                        "AcceleratorRuntimeAuthority",
                        "DeviceAuthority",
                        "TokenAuthority",
                        "HandleNamespaceAbi",
                        "OpenCloseLifecycleAbi",
                        "ReplayStableLifecycleModel",
                        "RetireOwnedSideEffectPublication",
                        "NoLifecycleStatePublicationBeforeRetire",
                        "NoBackendAdmissionBeforeAuthority");
                    break;
                case CompilerLane7DeferredAbiClass.AcceleratorQueueBinding:
                    Assert.False(contract.IsAcceleratorCapabilityQuery);
                    Assert.False(contract.IsAcceleratorLifecycleControl);
                    Assert.True(contract.IsAcceleratorQueueBindingControl);
                    Assert.True(contract.RequiresAcceleratorRuntimeAuthority);
                    Assert.True(contract.RequiresQueueAuthority);
                    Assert.True(contract.RequiresTokenAuthority);
                    Assert.True(contract.RequiresLane6TokenAuthorityGate);
                    Assert.True(contract.RequiresBindUnbindQueueAbi);
                    Assert.True(contract.RequiresQueueOwnershipModel);
                    Assert.True(contract.RequiresReplayStableQueueBindingModel);
                    Assert.True(contract.RequiresQueueBindUnbindOrderingModel);
                    Assert.True(contract.RequiresRetireOwnedSideEffectPublication);
                    Assert.True(contract.VmxBackendAuthorityEvidenceIsInsufficient);
                    Assert.True(contract.ExistingLane6DmaEvidenceIsInsufficient);
                    Assert.True(contract.NoQueueBindingPublicationBeforeRetire);
                    Assert.True(contract.NoQueueBindingBeforeTokenAuthority);
                    CompilerLane7DeferredAbiAssertions.AssertPolicyDecisions(
                        contract,
                        "AcceleratorRuntimeAuthority",
                        "QueueAuthority",
                        "TokenAuthority",
                        "Lane6TokenAuthorityGate",
                        "BindUnbindQueueAbi",
                        "QueueOwnershipModel",
                        "ReplayStableQueueBindingModel",
                        "QueueBindUnbindOrderingModel",
                        "RetireOwnedSideEffectPublication",
                        "NoQueueBindingPublicationBeforeRetire",
                        "NoQueueBindingBeforeTokenAuthority");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(contract.AbiClass), contract.AbiClass, "Unsupported accelerator-control ABI class.");
            }

            CompilerLane7DeferredAbiAssertions.AssertEmissionAuthorityThrows(contract);
        }

        string compilerSource = CompilerSourceScanner.ReadAllCompilerSource();
        CompilerLane7DeferredAbiAssertions.AssertCompilerSourceCarriesMetadataButNoEmission(
            compilerSource,
            ("ACCEL_QUERY_ABI", "AccelQueryAbi"),
            ("ACCEL_QUERY_TOPOLOGY", "AccelQueryTopology"),
            ("ACCEL_OPEN", "AccelOpen"),
            ("ACCEL_CLOSE", "AccelClose"),
            ("ACCEL_BIND_QUEUE", "AccelBindQueue"),
            ("ACCEL_UNBIND_QUEUE", "AccelUnbindQueue"));
    }

    [Fact]
    public void VmxBackendOrHostEvidence_IsNotUsedAsHiddenPhase14Integration()
    {
        List<string> failures = [];
        foreach (string path in EnumerateVmxProductionSources())
        {
            string source = File.ReadAllText(path);
            foreach (string token in VmxForbiddenTokens)
            {
                if (source.Contains(token, StringComparison.Ordinal))
                {
                    failures.Add($"{Relative(path)} contains forbidden VMX integration token `{token}`.");
                }
            }
        }

        Assert.True(
            failures.Count == 0,
            "Phase 14 Non-VMX accelerator controls must not be integrated through VMX backend or host evidence:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));
    }

    private static void AssertReservedNoAllocationRow(string mnemonic)
    {
        Assert.True(InstructionSupportStatusCatalog.TryGetExplicitStatus(
            mnemonic,
            out InstructionSupportStatus status));
        Assert.Equal(IsaInstructionStatus.Reserved, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.None, status.RuntimeEvidence);
        Assert.Equal("Lane7TopologyQueue", status.ExtensionName);
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

    private static void AssertCommonAcceleratorFailClosedMarkers(Type templateType)
    {
        Assert.Equal("GenericRuntimeOnly", GetConstant<string>(templateType, "VmxBoundary"));
        Assert.True(GetConstant<bool>(templateType, "RequiresNoHostEvidenceLeak"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresOwnerDomainGuard"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresDecoderEncoderAbiPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresInstructionIrProjectionPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresLane7MaterializerPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresTypedAcceleratorControlMicroOpPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresCommandQueueSemantics"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresConformanceAndGoldenArtifacts"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresMigrationCheckpointPolicy"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresFutureVirtualizationBoundaryPolicy"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoHostEvidenceLeak"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoHostOwnedEvidencePublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoHiddenScalarLowering"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoMultiOpEmission"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoGenericSystemOpFallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoLane6DmaFallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoLane7SubmitFallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoExternalBackendFallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoVmxSpecificPath"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "ExistingAccelSubmitEvidenceIsInsufficient"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "ExistingAccelQueryCapsEvidenceIsInsufficient"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "ExistingTopologyQueueTaxonomyEvidenceIsInsufficient"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "ExistingLane7ControlPlaneEvidenceIsInsufficient"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "RequiresImmediateVmxProjection"), templateType.FullName);
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

    private static IEnumerable<string> EnumerateVmxProductionSources()
    {
        string root = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        if (!Directory.Exists(root))
        {
            return Enumerable.Empty<string>();
        }

        return Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "NonVmx" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(path =>
                path.Contains("Vmx", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("Virtualization", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private static string ReadProjectFile(string relativePath) =>
        File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string Relative(string path) =>
        Path.GetRelativePath(FindRepositoryRoot(), path).Replace('\\', '/');

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HybridCPU v2.slnx")) ||
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate HybridCPU ISE repository root.");
    }
}
