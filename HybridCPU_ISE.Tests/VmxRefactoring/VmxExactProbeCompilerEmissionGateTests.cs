using HybridCPU.Compiler.Core;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxExactProbeCompilerEmissionGateTests
{
    [Fact]
    public void DefaultDecision_IsDisabledAndEmitsNothing()
    {
        Assert.Equal(
            CompilerExactProbeEmissionContract.DecisionId,
            Phase38VirtualizationDecisionSpecV2.Instance.DecisionId);
        Assert.Equal(
            Phase38VirtualizationDecisionAcceptanceV2.ExpectedSpecDigest,
            CompilerExactProbeEmissionContract.SpecDigest);

        HybridCpuThreadCompilerContext compiler = new(virtualThreadId: 0);

        CompilerExactProbeEmissionResult result = compiler.CompileProbeNoStateV1WithDecision(
            CompilerExactProbeEmissionRequest.Exact(
                CompilerEmissionDecisionV1.DefaultDisabled,
                rs1Register: 5));

        Assert.False(result.Emitted);
        Assert.Equal(CompilerExactProbeEmissionDecisionKind.DeniedDisabled, result.DecisionKind);
        Assert.Null(result.Plan);
        Assert.Equal(0, compiler.InstructionCount);
    }

    [Fact]
    public void ExactEnabledDecision_EmitsOnlyAcceptedCarrierWithoutAuthority()
    {
        HybridCpuThreadCompilerContext compiler = new(virtualThreadId: 2)
        {
            DomainTag = 0x44
        };

        CompilerExactProbeEmissionResult result = compiler.CompileProbeNoStateV1WithDecision(
            CompilerExactProbeEmissionRequest.Exact(
                CompilerEmissionDecisionV1.ExactProbeEnabled(),
                rs1Register: 5));

        Assert.True(result.Emitted);
        Assert.Equal(CompilerExactProbeEmissionDecisionKind.EmittedExactProbe, result.DecisionKind);
        CompilerExactProbeEmissionPlan plan = Assert.IsType<CompilerExactProbeEmissionPlan>(result.Plan);
        Assert.Equal((uint)InstructionsEnum.VMCALL, plan.EncodedInstruction.OpCode);
        Assert.True(VLIW_Instruction.TryUnpackArchRegs(
            plan.EncodedInstruction.Word1,
            out byte rd,
            out byte rs1,
            out byte rs2));
        Assert.Equal((byte)0, rd);
        Assert.Equal((byte)5, rs1);
        Assert.Equal((byte)0, rs2);
        Assert.Equal((ushort)0, plan.EncodedInstruction.Immediate);
        Assert.Equal(0UL, plan.EncodedInstruction.Word2);
        Assert.Equal(0UL, plan.EncodedInstruction.Word3);

        Assert.Equal(CompilerExactProbeEmissionContract.DecisionId, plan.Metadata.ReferencedDecisionId);
        Assert.Equal(CompilerExactProbeEmissionContract.SpecDigest, plan.Metadata.ReferencedSpecDigest);
        Assert.Equal(CompilerExactProbeEmissionContract.OperationNamespace, plan.Metadata.OperationNamespace);
        Assert.Equal(CompilerExactProbeEmissionContract.NumericLeaf, plan.Metadata.NumericLeaf);
        Assert.Equal(CompilerExactProbeEmissionContract.OperationId, plan.Metadata.OperationId);
        Assert.Equal(CompilerExactProbeEmissionContract.OperandAbi, plan.Metadata.OperandAbi);
        Assert.Equal(CompilerExactProbeEmissionContract.SchedulingConstraints, plan.Metadata.SchedulingBundleConstraints);
        Assert.Equal(CompilerExactProbeEmissionContract.AdjacentOperationDenials, plan.Metadata.AdjacentOperationDenials);
        Assert.False(plan.Metadata.IsRuntimeAuthority);
        Assert.False(plan.Metadata.RuntimeCorrectnessDependsOnMetadata);
        Assert.Equal("CompilerEvidenceOnly", CompilerExactProbeEmissionMetadata.AuthorityClassification);
        Assert.Equal(CompilerVirtualizationCarrierKind.VmCall, plan.Intent.CarrierKind);
        Assert.Equal(CompilerExactProbeEmissionContract.OperationNamespace, plan.Intent.OperationNamespace);
        Assert.Equal(CompilerExactProbeEmissionContract.OperationId, plan.Intent.OperationId);
        Assert.Equal(1UL, plan.Intent.NumericLeaf);
        Assert.True(plan.LegalityFacts.IsCompileTimeLegal);
        Assert.True(plan.RuntimeAdmissionRemainsRequired);

        Assert.Equal(1, compiler.InstructionCount);
        VLIW_Instruction recorded = Assert.Single(compiler.GetCompiledInstructions().ToArray());
        Assert.Equal((uint)InstructionsEnum.VMCALL, recorded.OpCode);
        Assert.Equal((byte)2, recorded.VirtualThreadId);

        HybridCpuCompiledProgram compiled = compiler.CompileProgram();
        IrInstruction ir = Assert.Single(compiled.BundleLayout.Program.Instructions);
        Assert.Equal(plan.Intent, ir.VirtualizationIntent);
        Assert.Equal(plan.LegalityFacts, ir.VirtualizationLegalityFacts);
        Assert.Equal(InstructionClass.Vmx, ir.InstructionClass);
        Assert.Equal(SerializationClass.VmxSerial, ir.SerializationClass);
        Assert.Equal(SlotClass.SystemSingleton, ir.Annotation.RequiredSlotClass);
        Assert.Equal(IrSlotBindingKind.HardPinned, ir.Annotation.BindingKind);
        Assert.Equal(IrIssueSlotMask.Slot7, ir.Annotation.StructurallyAllowedSlots);
        Assert.True((ir.Annotation.Serialization & IrSerializationKind.ExclusiveCycle) != 0);

        IrMaterializedBundle bundle = Assert.Single(
            compiled.BundleLayout.BlockResults.SelectMany(static block => block.Bundles));
        Assert.True(bundle.TryGetSlotForInstruction(ir.Index, out IrMaterializedBundleSlot? slot));
        Assert.Equal(7, slot!.SlotIndex);
        Assert.Equal(1, bundle.IssuedInstructionCount);
        IrBundleAdmissionResult admission = Assert.Single(compiled.AdmissibilityAgreement.BundleResults);
        Assert.True(admission.TypedSlotFactsValid);
        Assert.Equal((byte)1, admission.TypedSlotFacts.SystemSingletonCount);
        Assert.Equal((byte)0, admission.TypedSlotFacts.DmaStreamCount);
        Assert.Contains(
            compiled.LoweredBundles.SelectMany(static bundle =>
                Enumerable.Range(0, 8).Select(bundle.GetInstruction)),
            static instruction => instruction.OpCode == (uint)InstructionsEnum.VMCALL);
    }

    [Fact]
    public void WrongIdentityLeafProfileAndOperandAbi_AreDeniedWithoutEmission()
    {
        CompilerEmissionDecisionV1 exact = CompilerEmissionDecisionV1.ExactProbeEnabled();
        CompilerExactProbeEmissionRequest baseline =
            CompilerExactProbeEmissionRequest.Exact(exact, rs1Register: 7);

        CompilerExactProbeEmissionRequest[] denied =
        [
            baseline with { Decision = exact with { ReferencedDecisionId = "D2-WRONG" } },
            baseline with { Decision = exact with { ReferencedSpecDigest = new string('0', 64) } },
            baseline with { Decision = exact with { OperationNamespace = "HybridCPU.VMCALL.Runtime.v2" } },
            baseline with { Decision = exact with { OperationId = "PROBE_OTHER" } },
            baseline with { Decision = exact with { NumericLeaf = 0x0000 } },
            baseline with { Decision = exact with { NumericLeaf = 0x0002 } },
            baseline with { Decision = exact with { EmissionProfileVersion = "wrong" } },
            baseline with { Decision = exact with { RequiredCompilerFeatureProfile = "wrong" } },
            baseline with
            {
                Decision = exact with
                {
                    RequiredSchedulingBundleConstraints =
                        exact.RequiredSchedulingBundleConstraints with { RequiredLane = 6 }
                }
            },
            baseline with
            {
                Decision = exact with
                {
                    RequiredSchedulingBundleConstraints =
                        exact.RequiredSchedulingBundleConstraints with { RequiresNonStealable = false }
                }
            },
            baseline with
            {
                Decision = exact with
                {
                    AdjacentOperationDenials =
                        exact.AdjacentOperationDenials with { DenyAdjacentLeaves = false }
                }
            },
            baseline with { Decision = exact with { OperandAbiVersion = 2 } },
            baseline with { Decision = exact with { OperandAbi = "Rs1=immediate" } },
            baseline with { OperationNamespace = "HybridCPU.VMCALL.Runtime.v2" },
            baseline with { OperationId = "PROBE_OTHER" },
            baseline with { NumericLeaf = 0 },
            baseline with { NumericLeaf = 2 },
            baseline with { NumericLeaf = 0x1_0001 },
            baseline with { KnownRs1Value = 0 },
            baseline with { KnownRs1Value = 2 },
            baseline with { KnownRs1Value = 0x1_0001 },
            baseline with { Rs1Register = 0 },
            baseline with { Rs1Register = 32 },
            baseline with { Rs2Register = 1 },
            baseline with { RdRegister = 1 }
        ];

        foreach (CompilerExactProbeEmissionRequest request in denied)
        {
            HybridCpuThreadCompilerContext compiler = new(virtualThreadId: 0);
            CompilerExactProbeEmissionResult result =
                compiler.CompileProbeNoStateV1WithDecision(request);

            Assert.False(result.Emitted);
            Assert.NotEqual(CompilerExactProbeEmissionDecisionKind.EmittedExactProbe, result.DecisionKind);
            Assert.Null(result.Plan);
            Assert.Equal(0, compiler.InstructionCount);
        }

        var stealableCompiler = new HybridCpuThreadCompilerContext(0);
        CompilerExactProbeEmissionResult stealable =
            stealableCompiler.CompileProbeNoStateV1WithDecision(
                baseline,
                StealabilityPolicy.Stealable);
        Assert.False(stealable.Emitted);
        Assert.Equal(CompilerExactProbeEmissionDecisionKind.DeniedSchedulingContract, stealable.DecisionKind);
        Assert.Equal(0, stealableCompiler.InstructionCount);
    }

    [Fact]
    public void GenericCompilerIngress_CannotBypassExactDecision()
    {
        HybridCpuThreadCompilerContext compiler = new(virtualThreadId: 0);

        InvalidOperationException compile = Assert.Throws<InvalidOperationException>(() =>
            compiler.CompileInstruction(
                (uint)InstructionsEnum.VMCALL,
                dataType: 0,
                predicate: 0,
                immediate: 0,
                destSrc1: VLIW_Instruction.PackArchRegs(0, 5, 0),
                src2: 0,
                streamLength: 0,
                stride: 0,
                StealabilityPolicy.NotStealable));
        Assert.Contains("CompilerEmissionDecisionV1", compile.Message, StringComparison.Ordinal);

        InvalidOperationException insert = Assert.Throws<InvalidOperationException>(() =>
            compiler.InsertInstruction(
                instructionIndex: 0,
                opCode: (uint)InstructionsEnum.VMCALL,
                dataType: 0,
                predicate: 0,
                immediate: 0,
                destSrc1: VLIW_Instruction.PackArchRegs(0, 5, 0),
                src2: 0,
                streamLength: 0,
                stride: 0,
                StealabilityPolicy.NotStealable));
        Assert.Contains("CompilerEmissionDecisionV1", insert.Message, StringComparison.Ordinal);
        Assert.Equal(0, compiler.InstructionCount);

        VLIW_Instruction rawCarrier = new()
        {
            OpCode = (uint)InstructionsEnum.VMCALL,
            Word1 = VLIW_Instruction.PackArchRegs(0, 5, 0)
        };
        InvalidOperationException canonical = Assert.Throws<InvalidOperationException>(() =>
            HybridCpuCanonicalCompiler.CompileProgram(0, [rawCarrier]));
        Assert.Contains("intent binding", canonical.Message, StringComparison.Ordinal);

        InvalidOperationException irBuilder = Assert.Throws<InvalidOperationException>(() =>
            new HybridCpuIrBuilder().BuildProgram(0, [rawCarrier]));
        Assert.Contains("intent binding", irBuilder.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ForgedOrMismatchedCompilerIntentBinding_CannotReachCanonicalIr()
    {
        CompilerExactProbeEmissionRequest request = CompilerExactProbeEmissionRequest.Exact(
            CompilerEmissionDecisionV1.ExactProbeEnabled(),
            rs1Register: 5);
        var context = new HybridCpuThreadCompilerContext(0);
        CompilerExactProbeEmissionPlan plan = Assert.IsType<CompilerExactProbeEmissionPlan>(
            context.CompileProbeNoStateV1WithDecision(request).Plan);
        VLIW_Instruction carrier = Assert.Single(context.GetCompiledInstructions().ToArray());

        CompilerExactProbeEmissionPlan forged = plan with
        {
            Metadata = plan.Metadata with { OperationId = "FORGED" }
        };
        InvalidOperationException forgedFailure = Assert.Throws<InvalidOperationException>(() =>
            HybridCpuCanonicalCompiler.CompileProgram(
                0,
                [carrier],
                virtualizationIntentBindings: [new(0, forged)]));
        Assert.Contains("forged, stale", forgedFailure.Message, StringComparison.Ordinal);

        VLIW_Instruction mutated = carrier;
        mutated.Word1 = VLIW_Instruction.PackArchRegs(1, 5, 0);
        InvalidOperationException carrierFailure = Assert.Throws<InvalidOperationException>(() =>
            HybridCpuCanonicalCompiler.CompileProgram(
                0,
                [mutated],
                virtualizationIntentBindings: [new(0, plan)]));
        Assert.Contains("does not match", carrierFailure.Message, StringComparison.Ordinal);

        var forgedScheduling = new VliwBundleAnnotations(
        [
            InstructionSlotMetadata.Default with
            {
                SlotMetadata = YAKSys_Hybrid_CPU.Core.SlotMetadata.Default with
                {
                    StealabilityPolicy = StealabilityPolicy.Stealable
                }
            }
        ]);
        InvalidOperationException schedulingFailure = Assert.Throws<InvalidOperationException>(() =>
            HybridCpuCanonicalCompiler.CompileProgram(
                0,
                [carrier],
                bundleAnnotations: forgedScheduling,
                virtualizationIntentBindings: [new(0, plan)]));
        Assert.Contains("non-stealable", schedulingFailure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IntentBinding_SurvivesCompilerMutationAndExactCarrierRemainsDeterministic()
    {
        HybridCpuThreadCompilerContext first = CreateMixedCompilerProgram();
        HybridCpuCompiledProgram firstCompiled = first.CompileProgram();
        HybridCpuCompiledProgram secondCompiled = CreateMixedCompilerProgram().CompileProgram();

        IrInstruction vmCall = Assert.Single(
            firstCompiled.BundleLayout.Program.Instructions,
            static instruction => instruction.Opcode == InstructionsEnum.VMCALL);
        Assert.NotNull(vmCall.VirtualizationIntent);
        Assert.NotNull(vmCall.VirtualizationLegalityFacts);

        IrMaterializedBundle vmCallBundle = Assert.Single(
            firstCompiled.BundleLayout.BlockResults.SelectMany(static block => block.Bundles),
            bundle => bundle.TryGetSlotForInstruction(vmCall.Index, out _));
        Assert.Equal(1, vmCallBundle.IssuedInstructionCount);
        Assert.True(vmCallBundle.TryGetSlotForInstruction(vmCall.Index, out IrMaterializedBundleSlot? slot));
        Assert.Equal(7, slot!.SlotIndex);

        Assert.Equal(
            SerializeCompilerCarrier(firstCompiled),
            SerializeCompilerCarrier(secondCompiled));

        first.Reset();
        Assert.Empty(first.BuildIrProgram().Instructions);
    }

    [Fact]
    public void ExactGateSources_DoNotMintRuntimeAuthorityOrOpenAdjacentVirtualization()
    {
        string repoRoot = TestHelpers.CompatFreezeScanner.FindRepoRoot();
        string[] paths =
        [
            Path.Combine(repoRoot, "HybridCPU_Compiler", "Core", "IR", "Model", "CompilerExactProbeEmissionDecisionV1.cs"),
            Path.Combine(repoRoot, "HybridCPU_Compiler", "Core", "IR", "Construction", "CompilerExactProbeEmissionLowerer.cs"),
            Path.Combine(repoRoot, "HybridCPU_Compiler", "Core", "IR", "Construction", "CompilerVirtualizationIngressValidator.cs"),
            Path.Combine(repoRoot, "HybridCPU_Compiler", "API", "Threading", "ThreadCompilerContext.ExactProbe.cs")
        ];
        string source = string.Join(Environment.NewLine, paths.Select(File.ReadAllText));

        Assert.DoesNotContain("CapabilityGrant(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DomainAuthority(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("VirtualizationAdmissionCertificate(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DomainHypercallExecutionReceipt(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DomainHypercallCompletionReceipt(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DomainHypercallRetirePermit(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BackendExecutionAuthorized: true", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InstructionsEnum.VMREAD", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InstructionsEnum.VMWRITE", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InstructionsEnum.VMFUNC", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SecureComputeDomainDescriptor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SecureBackendOwner", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RuntimeBoundaryAdmissionService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DomainHypercallRuntimeExecutor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DomainHypercallCompletionOwner", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DomainHypercallRetireOwner", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CompilerProductionSources_DoNotCallRuntimeAdmissionExecutionCompletionOrRetireOwners()
    {
        string repoRoot = TestHelpers.CompatFreezeScanner.FindRepoRoot();
        string compilerRoot = Path.Combine(repoRoot, "HybridCPU_Compiler");
        string source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(compilerRoot, "*.cs", SearchOption.AllDirectories)
                .Where(static path =>
                    !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                    !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .OrderBy(static path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));

        string[] forbiddenRuntimeOwners =
        [
            "SafetyVerifier",
            "RuntimeBoundaryAdmissionService",
            "DomainHypercallRuntimeExecutor",
            "DomainHypercallCompletionOwner",
            "DomainHypercallRetireOwner",
            "IssueVirtualizationAdmissionAfterStageB",
            "IssueVirtualizationE2",
            "AttachVirtualizationAdmission",
            "VirtualizationAdmissionCertificate",
            "VirtualizationOperationAdmissionCertificate",
            "RuntimeCapabilityGrantOwner",
            "VirtualizationRestoreGenerationOwner"
        ];

        foreach (string forbidden in forbiddenRuntimeOwners)
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("CompilerVmxAuthority.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("VirtualizationLoweringBoundary.", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeAdmissionAndCompatibility_DoNotConsumeCompilerDecisionOrMetadata()
    {
        string repoRoot = TestHelpers.CompatFreezeScanner.FindRepoRoot();
        string closeToHsl = Path.Combine(repoRoot, "HybridCPU_ISE", "CloseToHSL", "Core");
        string runtimeSource = string.Join(
            Environment.NewLine,
            new[]
            {
                Path.Combine(closeToHsl, "Runtime"),
                Path.Combine(closeToHsl, "Pipeline"),
                Path.Combine(closeToHsl, "Virtualization")
            }
            .SelectMany(static root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));

        Assert.DoesNotContain(nameof(CompilerEmissionDecisionV1), runtimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(CompilerExactProbeEmissionMetadata), runtimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(CompilerExactProbeEmissionPlan), runtimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("HybridCPU.Compiler.Core.IR.VirtualizationCompilerIntent", runtimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(CompilerVirtualizationLegalityFacts), runtimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(CompilerVirtualizationIntentBinding), runtimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CompilerEmissionEnabled", runtimeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptedCompiledCarrier_MissingCapabilityAndWrongDomainRemainRuntimeDenied()
    {
        RuntimeFixture missingCapability = CreateRuntimeFixtureFromCompiledCarrier();
        DomainRuntimeContext noCapabilityDomain = new(
            missingCapability.Request.DomainContext!.Execution,
            memory: null,
            io: null,
            CapabilityDescriptorSet.Empty,
            secureCompute: null,
            domainTag: 7,
            addressSpaceTag: 0);
        VirtualizationE2Result missingCapabilityResult =
            missingCapability.Verifier.IssueVirtualizationE2(
                missingCapability.Request with { DomainContext = noCapabilityDomain });
        Assert.False(missingCapabilityResult.IsIssued);
        Assert.Null(missingCapabilityResult.Certificate);

        RuntimeFixture wrongDomain = CreateRuntimeFixtureFromCompiledCarrier();
        DomainRuntimeContext foreignDomain = CreateDomainContext(
            domainTag: 8,
            wrongDomain.Grant);
        VirtualizationE2Result wrongDomainResult =
            wrongDomain.Verifier.IssueVirtualizationE2(
                wrongDomain.Request with { DomainContext = foreignDomain });
        Assert.False(wrongDomainResult.IsIssued);
        Assert.Null(wrongDomainResult.Certificate);
    }

    [Fact]
    public void AcceptedCompiledCarrier_RevokedAndStaleGrantRemainRuntimeDenied()
    {
        RuntimeFixture revokedBeforeAdmission = CreateRuntimeFixtureFromCompiledCarrier();
        revokedBeforeAdmission.CapabilityOwner.RevokeAll();
        Assert.Equal(
            VirtualizationE2Decision.CapabilityLeaseNotLive,
            revokedBeforeAdmission.Verifier
                .IssueVirtualizationE2(revokedBeforeAdmission.Request)
                .Decision);

        RuntimeFixture revokedAfterAdmission = CreateRuntimeFixtureFromCompiledCarrier();
        SafetyVerifier.VirtualizationOperationAdmissionCertificate e2 =
            Assert.IsType<SafetyVerifier.VirtualizationOperationAdmissionCertificate>(
                revokedAfterAdmission.Verifier
                    .IssueVirtualizationE2(revokedAfterAdmission.Request)
                    .Certificate);
        revokedAfterAdmission.CapabilityOwner.RevokeAll();
        Assert.Equal(
            VirtualizationE2Decision.CapabilityLeaseNotLive,
            revokedAfterAdmission.Verifier
                .ValidateVirtualizationE2(e2, revokedAfterAdmission.RestoreOwner)
                .Decision);
    }

    [Fact]
    public void CompilerKillSwitchChangesOnlyProduction_NotRuntimeAdmissionRules()
    {
        var disabledCompiler = new HybridCpuThreadCompilerContext(0);
        Assert.False(disabledCompiler.CompileProbeNoStateV1WithDecision(
            CompilerExactProbeEmissionRequest.Exact(
                CompilerEmissionDecisionV1.DefaultDisabled,
                rs1Register: 5)).Emitted);
        Assert.Equal(0, disabledCompiler.InstructionCount);

        RuntimeFixture enabledCarrier = CreateRuntimeFixtureFromCompiledCarrier();
        DomainRuntimeContext noCapabilityDomain = new(
            enabledCarrier.Request.DomainContext!.Execution,
            null,
            null,
            CapabilityDescriptorSet.Empty,
            null,
            7,
            0);
        Assert.False(enabledCarrier.Verifier.IssueVirtualizationE2(
            enabledCarrier.Request with { DomainContext = noCapabilityDomain }).IsIssued);
    }

    private static RuntimeFixture CreateRuntimeFixtureFromCompiledCarrier()
    {
        var compiler = new HybridCpuThreadCompilerContext(0) { DomainTag = 7 };
        Assert.True(compiler.CompileProbeNoStateV1WithDecision(
            CompilerExactProbeEmissionRequest.Exact(
                CompilerEmissionDecisionV1.ExactProbeEnabled(),
                rs1Register: 5)).Emitted);
        HybridCpuCompiledProgram compiled = compiler.CompileProgram();
        VLIW_Instruction encoded = compiled.LoweredBundles[0].GetInstruction(7);
        Assert.Equal((uint)InstructionsEnum.VMCALL, encoded.OpCode);
        Assert.True(VLIW_Instruction.TryUnpackArchRegs(
            encoded.Word1,
            out byte rd,
            out byte rs1,
            out byte rs2));

        var carrier = new VmxMicroOp
        {
            OpCode = IsaOpcodeValues.VMCALL,
            OwnerThreadId = 0,
            VirtualThreadId = 0,
            OwnerContextId = 42,
            Rd = rd,
            Rs1 = rs1,
            Rs2 = rs2,
            Instruction = new InstructionIR
            {
                CanonicalOpcode = new IsaOpcode(IsaOpcodeValues.VMCALL),
                Class = InstructionClass.Vmx,
                SerializationClass = SerializationClass.VmxSerial,
                Rd = rd,
                Rs1 = rs1,
                Rs2 = rs2,
                Imm = encoded.Immediate,
            },
        };
        carrier.Placement = carrier.Placement with { DomainTag = 7 };
        carrier.RefreshWriteMetadata();

        var verifier = new SafetyVerifier();
        ReplayPhaseContext replay = new(
            true, 17, 0x4000, 1, 0, 0, 0, ReplayPhaseInvalidationReason.None);
        SmtBundleMetadata4Way bundle = new(0, 42, 7, 7, 7, 1);
        SafetyVerifier.VirtualizationAdmissionCertificate e1 =
            Assert.IsType<SafetyVerifier.VirtualizationAdmissionCertificate>(
                verifier.IssueVirtualizationAdmissionAfterStageB(
                    replay,
                    bundle,
                    carrier,
                    sourceSlotId: 7,
                    selectedLane: 7).Certificate);
        carrier.AttachVirtualizationAdmission(e1);
        VirtualizationOperationOwnerSnapshot owner =
            Phase38VirtualizationOperationOwnerSnapshotRegistry.ExactSnapshot;
        VirtualizationOperandSnapshot operand = Assert.IsType<VirtualizationOperandSnapshot>(
            new VirtualizationOperandSnapshotMaterializer()
                .CaptureAfterValidatedE1(carrier, e1, 1, 1, owner).Snapshot);
        carrier.AttachVirtualizationOperandSnapshot(operand);

        CapabilityGrant grant = new(
            RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
            CapabilityGrantScope.DomainGranted,
            isGranted: true,
            ownerDomainId: 7,
            CapabilityDelegationPolicy.NonDelegable,
            CapabilityRevocationPolicy.RuntimeRevocable,
            CapabilityMigrationClass.DomainLocal,
            CapabilityEvidenceVisibility.HostOnly,
            CapabilityFrontendProjectionPolicy.NeverProject);
        var capabilityOwner = new RuntimeCapabilityGrantOwner();
        RuntimeCapabilityGrantLease lease = capabilityOwner.Issue(grant);
        DomainRuntimeContext domain = CreateDomainContext(7, grant);
        RootAuthorityDescriptor root = new(
            RootAuthorityClass.RuntimeRoot,
            authorityEpoch: 1,
            RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
            allowCompatibilityFrontendActivation: false,
            allowAuthoritativeStateMutation: false);
        var restoreOwner = new VirtualizationRestoreGenerationOwner();
        var lifecycleGate = new DomainHypercallLifecycleGate(7);
        Assert.True(lifecycleGate.TryActivateExact(DomainHypercallExactActivationRequest.Phase38Exact));
        VirtualizationE2IssueRequest request = new(
            replay,
            bundle,
            carrier,
            7,
            7,
            e1,
            owner,
            operand,
            domain,
            root,
            capabilityOwner,
            lease,
            restoreOwner,
            lifecycleGate);
        return new(verifier, grant, capabilityOwner, restoreOwner, request);
    }

    private static HybridCpuThreadCompilerContext CreateMixedCompilerProgram()
    {
        var compiler = new HybridCpuThreadCompilerContext(virtualThreadId: 3)
        {
            DomainTag = 7
        };
        Assert.True(compiler.CompileProbeNoStateV1WithDecision(
            CompilerExactProbeEmissionRequest.Exact(
                CompilerEmissionDecisionV1.ExactProbeEnabled(),
                rs1Register: 5)).Emitted);
        compiler.InsertInstruction(
            instructionIndex: 0,
            opCode: (uint)InstructionsEnum.ADD,
            dataType: (byte)DataTypeEnum.INT32,
            predicate: 0,
            immediate: 0,
            destSrc1: VLIW_Instruction.PackArchRegs(1, 2, 3),
            src2: 0,
            streamLength: 0,
            stride: 0,
            StealabilityPolicy.NotStealable);
        compiler.CompileInstruction(
            (uint)InstructionsEnum.ADD,
            (byte)DataTypeEnum.INT32,
            predicate: 0,
            immediate: 0,
            destSrc1: VLIW_Instruction.PackArchRegs(4, 1, 2),
            src2: 0,
            streamLength: 0,
            stride: 0,
            StealabilityPolicy.NotStealable);
        return compiler;
    }

    private static string SerializeCompilerCarrier(HybridCpuCompiledProgram compiled) =>
        string.Join(
            "|",
            compiled.LoweredBundles.SelectMany(static bundle =>
                Enumerable.Range(0, BundleMetadata.BundleSlotCount)
                    .Select(slot => bundle.GetInstruction(slot))
                    .Select(static instruction =>
                        $"{instruction.OpCode:X8}:{instruction.Word0:X16}:{instruction.Word1:X16}:{instruction.Word2:X16}:{instruction.Word3:X16}")));

    private static DomainRuntimeContext CreateDomainContext(
        ulong domainTag,
        CapabilityGrant grant) =>
        new(
            new ExecutionDomainDescriptor(
                domainTag,
                new YAKSys_Hybrid_CPU.Core.BundleLegalityDescriptor(),
                schedulingBudget: null,
                extension: null,
                compatibilityProjectionEnabled: false),
            memory: null,
            io: null,
            new CapabilityDescriptorSet(new CapabilityGrantCollection([grant])),
            secureCompute: null,
            domainTag,
            addressSpaceTag: 0);

    private sealed record RuntimeFixture(
        SafetyVerifier Verifier,
        CapabilityGrant Grant,
        RuntimeCapabilityGrantOwner CapabilityOwner,
        VirtualizationRestoreGenerationOwner RestoreOwner,
        VirtualizationE2IssueRequest Request);
}
