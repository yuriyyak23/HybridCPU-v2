using System;
using System.IO;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09ClaimSafetyDocumentationTests
    {
        [Theory]
        [InlineData("README.md")]
        [InlineData("Documentation\\WhiteBook\\1. introduction.md")]
        [InlineData("Documentation\\WhiteBook\\18. conclusion.md")]
        public void T9_08n_PrimaryArchitectureEntryPoints_BoundDeterminismClaimsToReplayEvidenceEnvelope(string relativePath)
        {
            string text = ReadRepoFile(relativePath);

            Assert.Contains("replay-stable", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("deterministic SMT-VLIW", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_08o_EvaluationEntryPoint_Explains_CurrentDotnetTestBaselinePath()
        {
            string text = ReadRepoFile("HybridCPU_ISE.Tests\\EVALUATION_TESTS_README.md");

            Assert.Contains("validation-baseline.md", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("dotnet test", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("typed-slot reject taxonomy", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("AssistQuotaReject", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("AssistBackpressureReject", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("replay invalidation", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("runner mismatch", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("3.5x", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("1.0 -> 3.5", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("1-slot + 4-slot peaks", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_08p_IsaStatusEntryPoint_IsMarkedHistorical_NotCurrentBaselineAuthority()
        {
            string text = ReadRepoFile("HybridCPU_ISE.Tests\\ISA_MODEL_TEST_STATUS.md");

            Assert.Contains("historical status snapshot", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not authoritative", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Documentation/validation-baseline.md", text, StringComparison.Ordinal);
            Assert.Contains("Documentation/evidence-matrix.md", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Current Status", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("48 / ~150", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("32% Complete", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Last Updated:", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Author:", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_08q_ReadmeEntryPoint_PointsToCurrentAuthority_AndNarrowsTypedSlotFactClaims()
        {
            string text = ReadRepoFile("README.md");
            string layoutText = ReadRepoFile("Documentation\\WhiteBook\\14. repository-layout-and-code-map.md");
            string referencesText = ReadRepoFile("Documentation\\WhiteBook\\19. references-and-reading-order.md");

            Assert.Contains("Documentation/operational-semantics.md", text, StringComparison.Ordinal);
            Assert.Contains("Documentation/WhiteBook/", text, StringComparison.Ordinal);
            Assert.True(
                text.Contains("Documentation/WhiteBook/22. typed-slot-contract-staging.md", StringComparison.Ordinal) ||
                text.Contains("typed-slot", StringComparison.OrdinalIgnoreCase),
                "README should keep a typed-slot authority anchor."
            );
            Assert.Contains("Documentation/validation-baseline.md", text, StringComparison.Ordinal);
            Assert.Contains("ValidationOnly", text, StringComparison.Ordinal);
            Assert.True(
                text.Contains("not yet a mandatory correctness substrate", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("not as a historical roadmap", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("runtime authority", StringComparison.OrdinalIgnoreCase),
                "README should keep a bounded-claim/runtime-authority qualifier for typed-slot facts."
            );
            Assert.DoesNotContain("fully implemented and closed", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("typed-slot pipeline is complete", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("The compiler is expected to guarantee", text, StringComparison.OrdinalIgnoreCase);

            foreach (string whiteBookEntryPoint in new[] { layoutText, referencesText })
            {
                Assert.Contains("README.md", whiteBookEntryPoint, StringComparison.Ordinal);
                Assert.DoesNotContain("HybridCPU_ISE/Docs/v2", whiteBookEntryPoint, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("HybridCPU_ISE/Docs/NextMicroArch", whiteBookEntryPoint, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("HybridCPU_Compiler/Docs", whiteBookEntryPoint, StringComparison.OrdinalIgnoreCase);
            }
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
