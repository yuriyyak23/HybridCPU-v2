using System;
using System.IO;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09ExceptionModelDocumentationTests
    {
        [Fact]
        public void T9_09e_ExceptionModel_NamesBoundedRetireAndFaultOrderingModel()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\exception-model.md");

            Assert.Contains("bounded stage-aware retire/exception model", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("eligibility", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("authority", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("order", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("fault precedence", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_09f_ExceptionModel_AnchorsRetireHelpersAndStageAwareFaultWinner()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\exception-model.md");

            Assert.Contains("ResolveRetireEligibleWriteBackLanes", text, StringComparison.Ordinal);
            Assert.Contains("ResolveStableRetireOrder", text, StringComparison.Ordinal);
            Assert.Contains("CanRetireLanePrecisely", text, StringComparison.Ordinal);
            Assert.Contains("ResolveWriteBackFaultMask", text, StringComparison.Ordinal);
            Assert.Contains("ResolveMemoryFaultMask", text, StringComparison.Ordinal);
            Assert.Contains("ResolveExecuteFaultMask", text, StringComparison.Ordinal);
            Assert.Contains("TryResolveStageAwareExceptionWinner", text, StringComparison.Ordinal);
            Assert.Contains("TryResolveExceptionDeliveryDecisionForRetireWindow", text, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_09g_ExceptionModel_StatesAssistAndBackendTruthBoundaries()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\exception-model.md");

            Assert.Contains("IsRetireVisible", text, StringComparison.Ordinal);
            Assert.Contains("SuppressesArchitecturalFaults", text, StringComparison.Ordinal);
            Assert.Contains("non-retiring assist", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("RetireCoordinator", text, StringComparison.Ordinal);
            Assert.Contains("PhysicalRegisterFile", text, StringComparison.Ordinal);
            Assert.Contains("RenameMap", text, StringComparison.Ordinal);
            Assert.Contains("CommitMap", text, StringComparison.Ordinal);
            Assert.Contains("FreeList", text, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_09h_ExceptionModel_NarrowsClaimsWithoutRenamingFreeRhetoric()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\exception-model.md");

            Assert.Contains("does not claim a complete precise-exception theorem", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("does not claim universal rollback", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("full precise exceptions theorem", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("renaming-free", text, StringComparison.OrdinalIgnoreCase);
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
