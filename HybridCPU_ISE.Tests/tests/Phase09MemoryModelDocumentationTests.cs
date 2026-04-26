using System;
using System.IO;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09MemoryModelDocumentationTests
    {
        [Fact]
        public void T9_09a_MemoryModel_ExternalizesAtomicRetireResolutionAndApplyBoundary()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\memory-model.md");

            Assert.Contains("AtomicRetireEffect", text, StringComparison.Ordinal);
            Assert.Contains("ResolveRetireEffect", text, StringComparison.Ordinal);
            Assert.Contains("ApplyRetireEffect", text, StringComparison.Ordinal);
            Assert.Contains("AtomicRetireOutcome", text, StringComparison.Ordinal);
            Assert.Contains("memory visibility", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("register writeback", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("RetireCoordinator", text, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_09b_MemoryModel_StatesReservationAndClaimBoundaries()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\memory-model.md");

            Assert.Contains("reservation", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("SC", text, StringComparison.Ordinal);
            Assert.Contains("LR", text, StringComparison.Ordinal);
            Assert.Contains("speculative", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("does not prove a global memory-order theorem", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("does not upgrade", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("complete precise-exception theorem", text, StringComparison.OrdinalIgnoreCase);
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
