using System;
using System.IO;
using HybridCPU.Compiler.Core.IR;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Backends;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class Ex1Phase13DependencyOrderTests
{
    [Fact]
    public void Phase13_DependencyGraphNamesMajorFutureClaimsAndBlockers()
    {
        string phase13 = ReadRepoFile(
            "Documentation/Refactoring/Phases Ex1/13_Dependency_Graph_And_Execution_Order.md");
        string adr13 = ReadRepoFile(
            "Documentation/Refactoring/Phases Ex1/ADR_13_Dependency_Graph_And_Execution_Order.md");
        string combined = phase13 + Environment.NewLine + adr13;

        AssertContainsAll(
            phase13,
            "Executable Lane6 DSC",
            "Async DMA Overlap",
            "Executable External Accelerator ISA",
            "Cache Visibility Claims",
            "Coherent DMA Claim",
            "Compiler Backend Production DSC Lowering",
            "Compiler Backend Production ACCEL Lowering");

        AssertContainsAll(
            combined,
            "Phase 02",
            "Phase 03",
            "Phase 04",
            "Phase 05",
            "Phase 06",
            "Phase 07",
            "Phase 08",
            "Phase 09",
            "Phase 11",
            "Phase 12");

        AssertContainsAll(
            combined,
            "token scheduler and completion model",
            "CPU load/store/atomic hooks",
            "fence/wait/poll semantics",
            "replay/squash/trap/context-switch cancellation",
            "cache flush/invalidate protocol",
            "future coherent-DMA ADR",
            "compiler/backend conformance",
            "documentation migration");
    }

    [Fact]
    public void Phase13_DocsRemainPlanningOnlyAndApproveNoExecutableClaims()
    {
        string combined = ReadPhase13AndAdr13();

        AssertContainsAll(
            combined,
            "Planning dependency graph. Documentation only.",
            "This ADR is documentation only. It does not approve implementation",
            "Do not treat the graph as implementation approval",
            "Do not approve executable lane6 DSC",
            "Do not approve executable L7",
            "Do not approve DSC2 execution",
            "Do not approve coherent DMA",
            "Do not approve compiler/backend production lowering",
            "does not approve executable DSC/L7/DSC2/coherent DMA or compiler/backend production lowering");

        Assert.False(DmaStreamComputeDescriptorParser.ExecutionEnabled);
        Assert.DoesNotContain(
            "dependency graph approves implementation",
            combined,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "current executable DSC2",
            combined,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Phase13_DownstreamEvidenceCannotSatisfyUpstreamExecutionGates()
    {
        string combined = ReadPhase13AndAdr13();

        AssertContainsAll(
            combined,
            "parser-only DSC2",
            "model token stores",
            "L7 fake backend",
            "IOMMU backend infrastructure",
            "conflict/cache observers",
            "compiler sideband emission",
            "must not satisfy upstream executable gates");

        var fakeBackend = new FakeMatMulExternalAcceleratorBackend();
        Assert.True(fakeBackend.IsTestOnly);

        CompilerBackendLoweringDecision parserOnlyDecision =
            CompilerBackendLoweringContract.EvaluateProductionDscLowering(
                new CompilerBackendLoweringRequest
                {
                    Surface = CompilerBackendLoweringSurface.Lane6DmaStreamCompute,
                    State = CompilerBackendCapabilityState.ProductionExecutable,
                    AvailableRequirements = CompilerBackendLoweringContract.FutureDscRequiredRequirements,
                    UsesParserValidationOnly = true
                });
        CompilerBackendLoweringDecision descriptorOnlyDecision =
            CompilerBackendLoweringContract.EvaluateProductionDscLowering(
                new CompilerBackendLoweringRequest
                {
                    Surface = CompilerBackendLoweringSurface.Lane6DmaStreamCompute,
                    State = CompilerBackendCapabilityState.DescriptorOnly,
                    UsesDescriptorEvidenceOnly = true
                });
        CompilerBackendLoweringDecision l7ModelHelperDecision =
            CompilerBackendLoweringContract.EvaluateProductionL7Lowering(
                new CompilerBackendLoweringRequest
                {
                    Surface = CompilerBackendLoweringSurface.Lane7SystemDeviceCommand,
                    State = CompilerBackendCapabilityState.ProductionExecutable,
                    AvailableRequirements = CompilerBackendLoweringContract.FutureL7RequiredRequirements,
                    UsesModelOrTestHelper = true
                });
        CompilerBackendLoweringDecision backendOnlyDecision =
            CompilerBackendLoweringContract.EvaluateProductionDscLowering(
                new CompilerBackendLoweringRequest
                {
                    Surface = CompilerBackendLoweringSurface.Lane6DmaStreamCompute,
                    State = CompilerBackendCapabilityState.ProductionExecutable,
                    AvailableRequirements = CompilerBackendLoweringRequirement.BackendAddressSpace
                });
        CompilerBackendLoweringDecision conflictCacheOnlyDecision =
            CompilerBackendLoweringContract.EvaluateProductionL7Lowering(
                new CompilerBackendLoweringRequest
                {
                    Surface = CompilerBackendLoweringSurface.Lane7SystemDeviceCommand,
                    State = CompilerBackendCapabilityState.ProductionExecutable,
                    AvailableRequirements = CompilerBackendLoweringRequirement.OrderCacheFaultContract
                });

        Assert.False(parserOnlyDecision.IsAllowed);
        Assert.Contains("descriptor or parser evidence", parserOnlyDecision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(descriptorOnlyDecision.IsAllowed);
        Assert.Contains("ProductionExecutable", descriptorOnlyDecision.Reason, StringComparison.Ordinal);
        Assert.False(l7ModelHelperDecision.IsAllowed);
        Assert.Contains("model or test helper", l7ModelHelperDecision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(backendOnlyDecision.IsAllowed);
        Assert.True((backendOnlyDecision.MissingRequirements & CompilerBackendLoweringRequirement.ExecutableCarrier) != 0);
        Assert.False(conflictCacheOnlyDecision.IsAllowed);
        Assert.True((conflictCacheOnlyDecision.MissingRequirements & CompilerBackendLoweringRequirement.ExecutableCarrier) != 0);
    }

    [Fact]
    public void Phase13_KeepsPhase12AsMigrationGateAndCompilerLoweringAsLastMile()
    {
        string phase12 = ReadRepoFile(
            "Documentation/Refactoring/Phases Ex1/12_Testing_Conformance_And_Documentation_Migration.md");
        string phase13 = ReadRepoFile(
            "Documentation/Refactoring/Phases Ex1/13_Dependency_Graph_And_Execution_Order.md");
        string adr13 = ReadRepoFile(
            "Documentation/Refactoring/Phases Ex1/ADR_13_Dependency_Graph_And_Execution_Order.md");
        string combined = phase13 + Environment.NewLine + adr13;

        Assert.Contains("| 13 |", phase12, StringComparison.Ordinal);
        Assert.Contains("Ex1Phase13DependencyOrderTests", phase12, StringComparison.Ordinal);
        AssertContainsAll(
            phase13,
            "EXD --> COMP",
            "L7 --> ACCCOMP",
            "P11[\"Phase 11 Compiler Contract\"] --> COMP",
            "P11 --> ACCCOMP",
            "P12[\"Phase 12 Tests and Doc Migration\"] --> COMP",
            "P12 --> ACCCOMP");
        AssertContainsAll(
            combined,
            "Use phase 12 as the gate",
            "Compiler/backend production lowering is last-mile work",
            "Enable compiler/backend production lowering only after conformance and documentation migration");
    }

    private static string ReadPhase13AndAdr13() =>
        ReadRepoFile("Documentation/Refactoring/Phases Ex1/13_Dependency_Graph_And_Execution_Order.md") +
        Environment.NewLine +
        ReadRepoFile("Documentation/Refactoring/Phases Ex1/ADR_13_Dependency_Graph_And_Execution_Order.md");

    private static void AssertContainsAll(string text, params string[] expectedValues)
    {
        foreach (string expectedValue in expectedValues)
        {
            Assert.Contains(expectedValue, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ReadRepoFile(string relativePath)
    {
        string fullPath = Path.Combine(
            CompatFreezeScanner.FindRepoRoot(),
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath), $"Missing repository file: {relativePath}");
        return File.ReadAllText(fullPath);
    }
}
