using Xunit;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxConformanceGoldenArtifactsAndStaticGatesTests
{
    [Fact]
    public void TraceabilityMatrix_CoversClosedPoolsAdrFixturesAndPhase15Entries()
    {
        string phase13 = ReadPlan("13_conformance_golden_artifacts_and_static_gates.md");
        string phase15 = ReadPlan("15_final_readiness_review_and_next_work_order.md");

        Assert.Contains("ADR-VIRT-CONFORMANCE-GATES-2026-06-05", phase13);
        Assert.Contains("VmxConformanceGoldenArtifactsAndStaticGatesTests", phase13);
        Assert.Contains("Phase 13 Conformance Pool Status - 2026-06-05", phase13);
        Assert.Contains("ADR-VIRT-CONFORMANCE-GATES-2026-06-05", phase15);
        Assert.Contains("VmxConformanceGoldenArtifactsAndStaticGatesTests", phase15);

        foreach (ClosedPool pool in ClosedPools())
        {
            string phaseDoc = ReadPlan(pool.PhaseDoc);

            Assert.Contains(pool.Adr, phaseDoc);
            Assert.Contains(pool.Fixture, phaseDoc);
            Assert.Contains(pool.Adr, phase13);
            Assert.Contains(pool.Fixture, phase13);
            Assert.Contains(pool.SourceAnchor, phase13);
            Assert.Contains(pool.Adr, phase15);
            Assert.Contains(pool.Fixture, phase15);
        }

        foreach (string requiredGate in new[]
                 {
                     "source-anchor scan",
                     "forbidden production shortcut scan",
                     "documentation-overclaim scan",
                     "Phase 15 final readiness entry",
                 })
        {
            Assert.Contains(requiredGate, phase13);
        }
    }

    [Fact]
    public void StaticGateProtocol_RequiresNoMatchForPositiveShortcutsWithoutFlaggingDenyVocabulary()
    {
        string phase13 = ReadPlan("13_conformance_golden_artifacts_and_static_gates.md");

        Assert.Contains("MATCH_REQUIRED", phase13);
        Assert.Contains("NO_MATCH_REQUIRED", phase13);
        Assert.Contains("positive production shortcut scans must return `NO_MATCH`", phase13);
        Assert.Contains("Fail-closed vocabulary", phase13);
        Assert.Contains("Documented future-gated names are allowed only in denied", phase13);
        Assert.Contains("proof-only status", phase13);

        foreach (string allowedDenyVocabulary in new[]
                 {
                     "DeniedBackendExecution",
                     "ProjectionOnlyDenied",
                     "CanWrite=false",
                     "AllowedProofOnlyNoExecution",
                     "RuntimeOwnedPublication",
                 })
        {
            Assert.Contains(allowedDenyVocabulary, phase13);
        }
    }

    [Fact]
    public void GeneratedParityAndGoldenArtifactGates_RemainConformanceEvidenceOnly()
    {
        string phase13 = ReadPlan("13_conformance_golden_artifacts_and_static_gates.md");
        string repositoryRoot = FindRepositoryRoot();
        string generatedParityRoot = Path.Combine(
            repositoryRoot,
            "HybridCPU_ISE",
            "CloseToRTL",
            "Core",
            "Virtualization",
            "Conformance",
            "GeneratedParity");

        foreach (string contract in new[]
                 {
                     "GeneratedProjectionParityContract.cs",
                     "GeneratedVmcsProjectionProvenanceContract.cs",
                     "VmcsFieldProjectionSchemaConformanceContract.cs",
                     "CompatAliasSchemaConformanceContract.cs",
                     "VmxCapsBitSchemaConformanceContract.cs",
                 })
        {
            Assert.True(File.Exists(Path.Combine(generatedParityRoot, contract)), contract);
            Assert.Contains(Path.GetFileNameWithoutExtension(contract), phase13);
        }

        foreach (string fixture in new[]
                 {
                     "VmxSpecConformanceTests",
                     "VmxProjectionSchemaAndQuarantineTests",
                     "VmxGeneratedReadOnlyVmReadValueProjectionTests",
                 })
        {
            Assert.Contains(fixture, phase13);
        }

        Assert.Contains(
            "generated schemas and golden artifacts are conformance evidence, not runtime state or authority",
            phase13);
    }

    [Fact]
    public void ConsolidatedPositiveShortcutSourceScan_HasNoRuntimeAuthorityMatches()
    {
        string productionSource = ReadRepositorySources(
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/Compatibility/Frontend/Handlers",
            "HybridCPU_ISE/CloseToRTL/Core/Execution/Dispatch/ExecutionDispatcherV4.VmxCompatibility.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Pipeline/MicroOps/Types/MicroOp.IO.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.VmxRetire.cs");

        foreach (string forbidden in new[]
                 {
                     "BackendExecutionAuthorized: true",
                     "BackendSuccessAuthorized: true",
                     "CompletionPublicationAllowed: true",
                     "RetirePublicationAllowed: true",
                     "TrapCompletionRouteDescriptor.RuntimeOwnedPublication",
                     "CompletionRecord.FromCompatibilityExit",
                     "CompletionRecord.TryFromCompatibilityExit",
                     "VmxRetireEffect.InterceptExit",
                     "VmxRetireEffect.VmCall",
                     "VmxRetireEffect.VmFunc",
                 })
        {
            Assert.DoesNotContain(forbidden, productionSource);
        }

        string vmcsWriteSource = ReadRepositorySources(
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/Compatibility/Generated/VmcsProjection",
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/Compatibility/Frontend/Decode",
            "HybridCPU_ISE/NonRTL/Core/System/Vmcs/V2");

        foreach (string forbidden in new[]
                 {
                     "TryWriteScalarField",
                     "WriteFieldValue(",
                     "WriteKnownScalar",
                     "_scalarValues",
                     "_scalarWritten",
                     "VmcsManager",
                     "IVmcsManager",
                     "VmxExecutionUnit",
                     "VmxRuntimeManager",
                 })
        {
            Assert.DoesNotContain(forbidden, vmcsWriteSource);
        }

        string compilerAndNonVmxSource = ReadRepositorySources(
            "HybridCPU_Compiler/API",
            "HybridCPU_Compiler/Core",
            "HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx");

        foreach (string forbidden in new[]
                 {
                     "InstructionsEnum.VMXON",
                     "InstructionsEnum.VMXOFF",
                     "InstructionsEnum.VMLAUNCH",
                     "InstructionsEnum.VMRESUME",
                     "InstructionsEnum.VMPTRLD",
                     "InstructionsEnum.VMPTRST",
                     "InstructionsEnum.VMCLEAR",
                     "InstructionsEnum.VMWRITE",
                     "InstructionsEnum.VMCALL",
                     "InstructionsEnum.VMFUNC",
                     "VMXON",
                     "VMLAUNCH",
                     "VMRESUME",
                     "VMWRITE",
                     "VMCALL",
                     "SecureComputeDomainDescriptor",
                     "SecureBackendOwnerAdmissionPolicy",
                     "VmxCaps.Secure",
                     "TrapCompletionRouteDescriptor.RuntimeOwnedPublication",
                     "CompletionRecord.FromCompatibilityExit",
                     "VmxRetireEffect.InterceptExit",
                 })
        {
            Assert.DoesNotContain(forbidden, compilerAndNonVmxSource);
        }
    }

    private static ClosedPool[] ClosedPools() =>
    [
        new(
            "05_privileged_execution_state_owner_decision.md",
            "ADR-VIRT-PES-2026-06-04",
            "VmxGuestControlRegisterOwnerDecisionTests",
            "PrivilegedExecutionStateProjectionDenied"),
        new(
            "06_vmcs_write_and_compatibility_control_policy.md",
            "ADR-VIRT-VMCS-WRITE-CONTROL-2026-06-04",
            "VmxVmcsWriteCompatibilityControlPolicyTests",
            "CompatibilityControlValueProjectionDenied"),
        new(
            "07_hypercall_backend_owner_and_vmcall_decision.md",
            "ADR-VIRT-HYPERCALL-BACKEND-2026-06-04",
            "VmxHypercallBackendOwnerDecisionReadinessTests",
            "HypercallBackendAdmissionRequest.MissingNeutralOwner"),
        new(
            "08_trap_completion_route_and_retire_publication.md",
            "ADR-VIRT-TRAP-PUBLICATION-2026-06-04",
            "VmxTrapCompletionRouteRetirePublicationHardeningTests",
            "DeniedCompletionPublication"),
        new(
            "09_nested_virtualization_child_intent_plan.md",
            "ADR-VIRT-NESTED-CHILD-INTENT-2026-06-04",
            "VmxNestedChildIntentHardeningTests",
            "DeniedNestedVmcsAuthority"),
        new(
            "10_memory_io_iommu_lanes_and_stream_boundary.md",
            "ADR-VIRT-MEM-IO-LANE-STREAM-2026-06-05",
            "VmxMemoryIoLaneStreamBoundaryHardeningTests",
            "VmxCompatibilityIoAliasesAreReadOnlyDenied"),
        new(
            "11_capability_evidence_and_securecompute_boundary.md",
            "ADR-VIRT-CAP-EVIDENCE-SECCOMP-2026-06-05",
            "VmxCapabilityEvidenceSecureComputeBoundaryHardeningTests",
            "DeniedVmxCapsAuthority"),
        new(
            "12_compiler_isa_runtime_no_emission_contract.md",
            "ADR-VIRT-COMPILER-NOEMISSION-2026-06-05",
            "VmxCompilerIsaRuntimeNoEmissionContractTests",
            "CompilerVmxAuthority"),
    ];

    private static string ReadPlan(string fileName) =>
        File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "HybridCPU_ISE",
            "docs",
            "ref2",
            "VirtualiztionRefactoringNew",
            fileName));

    private static string ReadRepositorySources(params string[] relativeRoots)
    {
        string repositoryRoot = FindRepositoryRoot();
        return string.Concat(relativeRoots.SelectMany(relativeRoot =>
        {
            string root = Path.Combine(
                repositoryRoot,
                relativeRoot.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(root))
            {
                return new[] { File.ReadAllText(root) };
            }

            if (!Directory.Exists(root))
            {
                throw new DirectoryNotFoundException($"Missing repository source root: {relativeRoot}");
            }

            return Directory
                .GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(static path =>
                    !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                    !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .OrderBy(static path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText);
        }));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE.sln")) ||
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate HybridCPU ISE repository root.");
    }

    private sealed record ClosedPool(
        string PhaseDoc,
        string Adr,
        string Fixture,
        string SourceAnchor);
}
