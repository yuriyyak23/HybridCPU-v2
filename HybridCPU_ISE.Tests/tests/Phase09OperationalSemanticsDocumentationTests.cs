using System;
using System.IO;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09OperationalSemanticsDocumentationTests
    {
        [Fact]
        public void T9_15a_OperationalSemanticsArtifact_StatesMachineTuple_And_StepRelation()
        {
            string text = ReadRepoFile("Documentation\\operational-semantics.md");

            Assert.Contains("MachineState =", text, StringComparison.Ordinal);
            Assert.Contains("ArchitecturalState", text, StringComparison.Ordinal);
            Assert.Contains("FrontendState", text, StringComparison.Ordinal);
            Assert.Contains("SchedulerState", text, StringComparison.Ordinal);
            Assert.Contains("PipelineState", text, StringComparison.Ordinal);
            Assert.Contains("ReplayState", text, StringComparison.Ordinal);
            Assert.Contains("BackendState", text, StringComparison.Ordinal);
            Assert.Contains("EvidenceState", text, StringComparison.Ordinal);
            Assert.Contains("Step(MachineState_t, Inputs_t) -> MachineState_t+1", text, StringComparison.Ordinal);
            Assert.Contains("ExecutePipelineCycle()", text, StringComparison.Ordinal);
            Assert.Contains("WB -> MEM -> EX -> ID -> IF", text, StringComparison.Ordinal);
            Assert.Contains("STALL step", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("CYCLE step", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_15b_OperationalSemanticsArtifact_AnchorsDecodeLegalityReplayAndRetireBoundaries()
        {
            string text = ReadRepoFile("Documentation\\operational-semantics.md");

            Assert.Contains("DecodedBundleRuntimeState", text, StringComparison.Ordinal);
            Assert.Contains("BundleLegalityDescriptor", text, StringComparison.Ordinal);
            Assert.Contains("LegalityDecision", text, StringComparison.Ordinal);
            Assert.Contains("TryClassAdmission(...)", text, StringComparison.Ordinal);
            Assert.Contains("TryMaterializeLane(...)", text, StringComparison.Ordinal);
            Assert.Contains("ReplayPhaseContext", text, StringComparison.Ordinal);
            Assert.Contains("RetireCoordinator", text, StringComparison.Ordinal);
            Assert.Contains("ApplyRetireEffect(...)", text, StringComparison.Ordinal);
            Assert.Contains("TryResolveExceptionDeliveryDecisionForRetireWindow(...)", text, StringComparison.Ordinal);
            Assert.Contains("ReplayToken", text, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_15c_OperationalSemanticsArtifact_LinksSupportingBoundedSpecs_And_NonClaims()
        {
            string text = ReadRepoFile("Documentation\\operational-semantics.md");

            Assert.Contains("HybridCPU_ISE/docs/replay-envelope.md", text, StringComparison.Ordinal);
            Assert.Contains("HybridCPU_ISE/docs/memory-model.md", text, StringComparison.Ordinal);
            Assert.Contains("HybridCPU_ISE/docs/exception-model.md", text, StringComparison.Ordinal);
            Assert.Contains("HybridCPU_ISE/docs/rollback-boundaries.md", text, StringComparison.Ordinal);
            Assert.Contains("HybridCPU_ISE/docs/backend-state-truthfulness.md", text, StringComparison.Ordinal);
            Assert.Contains("native VLIW only", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not renaming-free", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ValidationOnly", text, StringComparison.Ordinal);
            Assert.Contains("not a global memory-order theorem", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not a complete precise-exception theorem", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("replay/evidence envelope", text, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("Documentation\\WhiteBook\\0. chapter-index.md")]
        [InlineData("Documentation\\WhiteBook\\5. execution-model.md")]
        [InlineData("Documentation\\WhiteBook\\19. references-and-reading-order.md")]
        [InlineData("Documentation\\WhiteBook\\20. legality-predicate.md")]
        public void T9_15d_WhiteBookEntryPoints_Expose_OperationalSemanticsArtifact(string relativePath)
        {
            string text = ReadRepoFile(relativePath);

            Assert.Contains("Documentation/operational-semantics.md", text, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_15e_ValidationSummary_Cites_OperationalSemanticsProofSurface()
        {
            string text = ReadRepoFile("Documentation\\WhiteBook\\13. validation-status-and-test-evidence.md");

            Assert.Contains("Phase09OperationalSemanticsDocumentationTests.cs", text, StringComparison.Ordinal);
            Assert.Contains("Phase09RetireContractClosureTests.cs", text, StringComparison.Ordinal);
            Assert.Contains("Phase09WriteBackFaultOrderingProofTests.cs", text, StringComparison.Ordinal);
            Assert.Contains("Phase09MemoryModelDocumentationTests.cs", text, StringComparison.Ordinal);
            Assert.Contains("Phase09ExceptionModelDocumentationTests.cs", text, StringComparison.Ordinal);
        }

        private static string ReadRepoFile(string relativePath)
        {
            string repoRoot = CompatFreezeScanner.FindRepoRoot();
            string fullPath = Path.Combine(repoRoot, relativePath);
            Assert.True(File.Exists(fullPath), $"Missing repository document: {relativePath}");
            return File.ReadAllText(fullPath);
        }
    }
}
