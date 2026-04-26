using System;
using System.IO;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09RollbackBoundariesDocumentationTests
    {
        [Fact]
        public void T9_09c_RollbackBoundariesDoc_StatesCapturedSurfacesAndFailClosedMemoryRules()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\rollback-boundaries.md");

            Assert.Contains("CaptureRegisterState", text, StringComparison.Ordinal);
            Assert.Contains("CaptureMemoryState", text, StringComparison.Ordinal);
            Assert.Contains("Rollback", text, StringComparison.Ordinal);
            Assert.Contains("CanSafelyRollback", text, StringComparison.Ordinal);
            Assert.Contains("fail closed", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("unbound", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("partial", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_09d_RollbackBoundariesDoc_DoesNotAdvertiseUniversalRecoveryTheorem()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\rollback-boundaries.md");

            Assert.Contains("does not claim universal recovery", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("RetireCoordinator", text, StringComparison.Ordinal);
            Assert.DoesNotContain("full precise exceptions theorem", text, StringComparison.OrdinalIgnoreCase);
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
