using System;
using System.IO;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09LegacyTerminologyQuarantineDocumentationTests
    {
        [Theory]
        [InlineData("README.md")]
        [InlineData("Documentation\\WhiteBook\\8. smt-vliw-packing-and-densification.md")]
        public void T9_08k_PrimaryArchitectureSummaries_QuarantineFspAsHistoricalAlias(string relativePath)
        {
            string text = ReadRepoFile(relativePath);

            Assert.Contains("historical", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("FSP", text, StringComparison.Ordinal);
            Assert.Contains("densification", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_08l_EvaluationSummary_DropsLegacyFourSlotFraming_And_ExplainsRetainedCounterNames()
        {
            string text = ReadRepoFile("HybridCPU_ISE.Tests\\EVALUATION_TESTS_README.md");
            string quickStart = ReadRepoFile("HybridCPU_ISE.Tests\\QUICKSTART.md");
            string todoTests = ReadRepoFile("HybridCPU_ISE.Tests\\TODO_TESTS.md");

            Assert.DoesNotContain("4-slot", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("legacy counter names", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("bundle-compositional SMT densification", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("historical and non-authoritative", quickStart, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("historical and non-authoritative", todoTests, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("3.5x", quickStart, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("1-slot + 4-slot", quickStart, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("renaming-free oracle", todoTests, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_08m_IsaStatusSummary_DropsMaskFirstAndFspCategoryHeadlines()
        {
            string text = ReadRepoFile("HybridCPU_ISE.Tests\\ISA_MODEL_TEST_STATUS.md");

            Assert.DoesNotContain("Mask-based Hazard Detection", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("FSP Invariant Proofs", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("register-group/structural conflict screening", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("intra-core SMT densification", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_08n_ReplaySurfaces_DoNotExplainReplayThroughLegacyFspVocabulary()
        {
            string loopBuffer = ReadRepoFile("HybridCPU_ISE\\Core\\Pipeline\\Components\\LoopBuffer.cs");
            string replayToken = ReadRepoFile("HybridCPU_ISE\\Core\\Pipeline\\MicroOps\\ReplayToken.cs");
            string traceSink = ReadRepoFile("HybridCPU_ISE\\Core\\Diagnostics\\TraceSink.cs");

            foreach (string replaySurface in new[] { loopBuffer, replayToken, traceSink })
            {
                Assert.DoesNotContain("FSP integration", replaySurface, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("FSP donation", replaySurface, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("FSP stealing", replaySurface, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("FSP rollback support", replaySurface, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Active pilfering strategy", replaySurface, StringComparison.OrdinalIgnoreCase);
            }

            Assert.Contains("replay-stable donor", loopBuffer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("legacy trace field", traceSink, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("replay-aware bundle densification policy", traceSink, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_08o_LegacyFspCompatibilityTypes_AreQuarantinedAsSlotAdmissionCompatibilitySurfaces()
        {
            string admissionPolicy = ReadRepoFile("HybridCPU_ISE\\Core\\Pipeline\\Scheduling\\FspAdmissionPolicy.cs");
            string processor = ReadRepoFile("HybridCPU_ISE\\Core\\Pipeline\\Scheduling\\FspProcessor.cs");
            string slotAssignment = ReadRepoFile("HybridCPU_ISE\\Core\\Pipeline\\Scheduling\\SlotAssignment.cs");

            foreach (string text in new[] { admissionPolicy, processor, slotAssignment })
            {
                Assert.Contains("retained compatibility", text, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("bundle densification", text, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("slot admission", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("FSP pilfering", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("slot pilfering", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Free Slot Processing", text, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void T9_08p_LegacyIsFspInjectedFlag_IsQuarantinedAsBundleDensificationCompatibility()
        {
            string microOp = ReadRepoFile("HybridCPU_ISE\\Core\\Pipeline\\MicroOps\\MicroOp.cs");

            Assert.Contains("retained compatibility", microOp, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("bundle densification", microOp, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("slot admission", microOp, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("FSP slot pilfering", microOp, StringComparison.OrdinalIgnoreCase);

            Assert.Contains("silent speculative squash", microOp, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_08q_PerformanceTypedSlotTelemetry_DropsLegacyFspExhaustionVocabulary()
        {
            string performanceReport = ReadRepoFile("HybridCPU_ISE\\Processor\\Performance\\PerformanceReport.TypedSlot.cs");
            string scheduler = ReadRepoFile("HybridCPU_ISE\\Core\\Pipeline\\Scheduling\\MicroOpScheduler.cs");
            string rejectTypes = ReadRepoFile("HybridCPU_ISE\\Core\\Pipeline\\Safety\\SafetyVerifier.Types.cs");

            Assert.Contains("typed-slot densification exhaustion", performanceReport, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("FSP exhaustion", performanceReport, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("typed-slot densification exhaustion", scheduler, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("FSP exhaustion", scheduler, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("typed-slot densification exhaustion", rejectTypes, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("dynamic FSP exhaustion", rejectTypes, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_08r_LegacyCompatibilitySeams_UseTypedSlotAndDonorVocabulary()
        {
            string powerController = ReadRepoFile("HybridCPU_ISE\\Core\\Pipeline\\Components\\FspPowerController.cs");
            string microOp = ReadRepoFile("HybridCPU_ISE\\Core\\Pipeline\\MicroOps\\MicroOp.cs");
            string internalOp = ReadRepoFile("HybridCPU_ISE\\Core\\Pipeline\\MicroOps\\InternalOp.cs");

            Assert.Contains("retained compatibility", powerController, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("donor nomination", powerController, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("FSP donation patterns", powerController, StringComparison.OrdinalIgnoreCase);

            Assert.Contains("legacy fsp compatibility metadata", microOp, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("typed-slot bundle densification", microOp, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("slot stealing in VLIW bundles", microOp, StringComparison.OrdinalIgnoreCase);

            Assert.Contains("retained compatibility alias", internalOp, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("bundle densification", internalOp, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Fine-grained Slot Pilfering", internalOp, StringComparison.OrdinalIgnoreCase);
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
