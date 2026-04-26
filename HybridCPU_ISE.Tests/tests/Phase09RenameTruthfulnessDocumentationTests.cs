using System;
using System.IO;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09RenameTruthfulnessDocumentationTests
    {
        [Theory]
        [InlineData("Documentation\\WhiteBook\\17. current-state-and-modernization-tracks.md")]
        [InlineData("HybridCPU_ISE.Tests\\EVALUATION_TESTS_README.md")]
        [InlineData("HybridCPU_ISE.Tests\\ISA_MODEL_TEST_STATUS.md")]
        public void T9_08g_PrimaryRepositorySummaries_DoNotDescribeLiveImplementationAsRenamingFree(string relativePath)
        {
            string text = ReadRepoFile(relativePath);
            Assert.DoesNotContain("renaming-free", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_08h_CurrentStateSummary_StatesExplicitRenameCommitBackendSubstrate()
        {
            string text = ReadRepoFile("Documentation\\WhiteBook\\17. current-state-and-modernization-tracks.md");

            Assert.Contains("PhysicalRegisterFile", text, StringComparison.Ordinal);
            Assert.Contains("RenameMap", text, StringComparison.Ordinal);
            Assert.Contains("CommitMap", text, StringComparison.Ordinal);
            Assert.Contains("FreeList", text, StringComparison.Ordinal);
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
