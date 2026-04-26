using System;
using System.IO;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09BackendStateTruthfulnessDocumentationTests
    {
        [Fact]
        public void T9_09k_BackendStateTruthfulness_DocumentsLiveRenameCommitSubstrate()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\backend-state-truthfulness.md");

            Assert.Contains("PhysicalRegisterFile", text, StringComparison.Ordinal);
            Assert.Contains("RenameMap", text, StringComparison.Ordinal);
            Assert.Contains("CommitMap", text, StringComparison.Ordinal);
            Assert.Contains("FreeList", text, StringComparison.Ordinal);
            Assert.Contains("RetireCoordinator", text, StringComparison.Ordinal);
            Assert.Contains("backend truthfulness", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_09l_BackendStateTruthfulness_DoesNotLetLegalityReplaceBackendSubstrate()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\backend-state-truthfulness.md");

            Assert.Contains("legality", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("certificate", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("typed-slot", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("does not replace the rename/commit/free-list substrate", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("does not remove the backend substrate", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_09m_BackendStateTruthfulness_AnchorsMemoryExceptionAndRollbackBoundaries()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\backend-state-truthfulness.md");

            Assert.Contains("memory visibility", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("exception ordering", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("rollback", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("architectural publication", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("bounded", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_09n_BackendStateTruthfulness_NarrowsForbiddenRepositoryClaims()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\backend-state-truthfulness.md");

            Assert.DoesNotContain("renaming-free", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("full precise exceptions theorem", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("legality replaces rename", text, StringComparison.OrdinalIgnoreCase);
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
