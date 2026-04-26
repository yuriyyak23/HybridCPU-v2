using System;
using System.IO;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09ReplayEnvelopeDocumentationTests
    {
        [Fact]
        public void T9_14a_ReplayEnvelopeArtifact_BoundsReuseInvalidationAndDeterminismClaims()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\replay-envelope.md");

            Assert.Contains("evidence-bounded", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not a global determinism theorem", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("replay reuse is valid only when", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("guard-before-reuse", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ReplayPhaseContext", text, StringComparison.Ordinal);
            Assert.Contains("ReplayPhaseInvalidationReason", text, StringComparison.Ordinal);
            Assert.Contains("AssistInvalidationReason", text, StringComparison.Ordinal);
            Assert.Contains("DeterministicLaneChooser.SelectWithReplayHint", text, StringComparison.Ordinal);
            Assert.Contains("TraceSink", text, StringComparison.Ordinal);
            Assert.Contains("ReplayEngine.CompareRepeatedRunsWithinEnvelope", text, StringComparison.Ordinal);
            Assert.DoesNotContain("guarantees global determinism", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("replay reuse is unconditional", text, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("None")]
        [InlineData("Completed")]
        [InlineData("PcMismatch")]
        [InlineData("Manual")]
        [InlineData("CertificateMutation")]
        [InlineData("PhaseMismatch")]
        [InlineData("InactivePhase")]
        [InlineData("DomainBoundary")]
        [InlineData("ClassCapacityMismatch")]
        [InlineData("ClassTemplateExpired")]
        [InlineData("SerializingEvent")]
        public void T9_14b_ReplayEnvelopeArtifact_NamesEveryReplayPhaseInvalidationReason(string reason)
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\replay-envelope.md");

            Assert.Contains(reason, text, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("None")]
        [InlineData("Replay")]
        [InlineData("Trap")]
        [InlineData("Fence")]
        [InlineData("VmTransition")]
        [InlineData("SerializingBoundary")]
        [InlineData("OwnerInvalidation")]
        [InlineData("Manual")]
        [InlineData("PipelineFlush")]
        [InlineData("InterCoreOwnerDrift")]
        [InlineData("InterCoreBoundaryDrift")]
        public void T9_14c_ReplayEnvelopeArtifact_NamesEveryAssistInvalidationReason(string reason)
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\replay-envelope.md");

            Assert.Contains(reason, text, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_14d_DeterministicLaneChooserComment_BoundsPlacementClaimToReplayEvidenceEnvelope()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\Core\\Pipeline\\Scheduling\\DeterministicLaneChooser.cs");

            Assert.Contains("replay/evidence envelope", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("supplied free-lane mask", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("guarantees global determinism", text, StringComparison.OrdinalIgnoreCase);
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
