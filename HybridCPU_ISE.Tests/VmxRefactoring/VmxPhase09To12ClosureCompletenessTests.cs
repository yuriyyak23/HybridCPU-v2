namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase09To12ClosureCompletenessTests
{
    [Fact]
    public void Phase09Through12Docs_HaveAdrFixturePathsAndFinalReadinessTraceability()
    {
        string phase13 = ReadPlan("13_conformance_golden_artifacts_and_static_gates.md");
        string phase15 = ReadPlan("15_final_readiness_review_and_next_work_order.md");

        foreach (PhaseClosure closure in PhaseClosures())
        {
            string doc = ReadPlan(closure.Doc);

            Assert.Contains($"## Closure Decision - {closure.Adr}", doc);
            Assert.Contains(closure.Fixture, doc);
            Assert.Contains(closure.FixturePath, doc);
            Assert.Contains($"FullyQualifiedName~{closure.Fixture}", doc);
            Assert.Contains(closure.PrimaryAnchor, doc);
            Assert.Contains(closure.ForbiddenShortcutText, doc);
            Assert.Contains(closure.DocOverclaimText, doc);

            Assert.Contains(closure.Adr, phase13);
            Assert.Contains(closure.Fixture, phase13);
            Assert.Contains(closure.Adr, phase15);
            Assert.Contains(closure.Fixture, phase15);
        }
    }

    [Fact]
    public void Phase09Through12Docs_DoNotClaimActivationOrPositiveBackendPaths()
    {
        string combined = string.Join(
            Environment.NewLine,
            PhaseClosures().Select(closure => RemoveStaticGateCommandLines(ReadPlan(closure.Doc))));

        foreach (string forbidden in new[]
                 {
                     "runtime activation approved",
                     "activation approved",
                     "VMWRITE allowed",
                     "VMCALL backend success allowed",
                     "completion publication allowed",
                     "retire publication allowed",
                     "SecureCompute supported via VMX",
                     "examples production authority",
                 })
        {
            Assert.DoesNotContain(forbidden, combined, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Phase09Through12FocusedFixtures_ArePresentAndRemainStaticOrNegativeClosureGuards()
    {
        string repositoryRoot = FindRepositoryRoot();

        foreach (PhaseClosure closure in PhaseClosures())
        {
            string fixturePath = Path.Combine(
                repositoryRoot,
                closure.FixturePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(fixturePath), closure.FixturePath);

            string fixture = File.ReadAllText(fixturePath);
            Assert.Contains($"public sealed class {closure.Fixture}", fixture);
            Assert.Contains(closure.RequiredNegativeMarker, fixture);
        }
    }

    private static PhaseClosure[] PhaseClosures() =>
    [
        new(
            Doc: "09_nested_virtualization_child_intent_plan.md",
            Adr: "ADR-VIRT-NESTED-CHILD-INTENT-2026-06-04",
            Fixture: "VmxNestedChildIntentHardeningTests",
            FixturePath: "HybridCPU_ISE.Tests/VmxRefactoring/VmxNestedChildIntentHardeningTests.cs",
            PrimaryAnchor: "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Nested/SecureChildDomainIntentDescriptor.cs",
            ForbiddenShortcutText: "Forbidden source scan for nested execution units",
            DocOverclaimText: "Documentation overclaim scan for current nested backend success",
            RequiredNegativeMarker: "DeniedNestedVmcsAuthority"),
        new(
            Doc: "10_memory_io_iommu_lanes_and_stream_boundary.md",
            Adr: "ADR-VIRT-MEM-IO-LANE-STREAM-2026-06-05",
            Fixture: "VmxMemoryIoLaneStreamBoundaryHardeningTests",
            FixturePath: "HybridCPU_ISE.Tests/VmxRefactoring/VmxMemoryIoLaneStreamBoundaryHardeningTests.cs",
            PrimaryAnchor: "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/Admission/Memory/MemoryDomainRuntime.cs",
            ForbiddenShortcutText: "Static scan that VMX frontend and SecureCompute projection code do not reference",
            DocOverclaimText: "Static scan for stream/L7 claim language in documentation",
            RequiredNegativeMarker: "RuntimeAuthorityRequired"),
        new(
            Doc: "11_capability_evidence_and_securecompute_boundary.md",
            Adr: "ADR-VIRT-CAP-EVIDENCE-SECCOMP-2026-06-05",
            Fixture: "VmxCapabilityEvidenceSecureComputeBoundaryHardeningTests",
            FixturePath: "HybridCPU_ISE.Tests/VmxRefactoring/VmxCapabilityEvidenceSecureComputeBoundaryHardeningTests.cs",
            PrimaryAnchor: "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Backend/SecureBackendOwnerAdmissionPolicy.cs",
            ForbiddenShortcutText: "Static doc/source scans for VMX/VMCS/`VmxCaps` SecureCompute authority claims",
            DocOverclaimText: "Do not claim SecureCompute production readiness",
            RequiredNegativeMarker: "DeniedVmxCapsAuthority"),
        new(
            Doc: "12_compiler_isa_runtime_no_emission_contract.md",
            Adr: "ADR-VIRT-COMPILER-NOEMISSION-2026-06-05",
            Fixture: "VmxCompilerIsaRuntimeNoEmissionContractTests",
            FixturePath: "HybridCPU_ISE.Tests/VmxRefactoring/VmxCompilerIsaRuntimeNoEmissionContractTests.cs",
            PrimaryAnchor: "HybridCPU_Compiler/API/Threading/HybridCpuThreadCompilerContext.cs",
            ForbiddenShortcutText: "Static scans for VMX backend emission claims",
            DocOverclaimText: "Do not declare examples as conformance authority",
            RequiredNegativeMarker: "CompilerHelperEmittable"),
    ];

    private static string ReadPlan(string fileName) =>
        File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "HybridCPU_ISE",
            "docs",
            "ref2",
            "VirtualiztionRefactoringNew",
            fileName));

    private static string RemoveStaticGateCommandLines(string text) =>
        string.Join(
            Environment.NewLine,
            text
                .Split([Environment.NewLine], StringSplitOptions.None)
                .Where(static line =>
                    !line.Contains("rg -n", StringComparison.OrdinalIgnoreCase) &&
                    !line.Contains("scan", StringComparison.OrdinalIgnoreCase) &&
                    !line.Contains("expected `NO_MATCH`", StringComparison.OrdinalIgnoreCase)));

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

    private sealed record PhaseClosure(
        string Doc,
        string Adr,
        string Fixture,
        string FixturePath,
        string PrimaryAnchor,
        string ForbiddenShortcutText,
        string DocOverclaimText,
        string RequiredNegativeMarker);
}
