using System;
using System.IO;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09AssistVisibilityDocumentationTests
    {
        [Fact]
        public void T9_09i_AssistSemantics_StatesRetireAndFaultOrderingInteraction()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\assist-semantics.md");

            Assert.Contains("Phase 05 Retire And Fault Ordering Interaction", text, StringComparison.Ordinal);
            Assert.Contains("IsRetireVisible", text, StringComparison.Ordinal);
            Assert.Contains("SuppressesArchitecturalFaults", text, StringComparison.Ordinal);
            Assert.Contains("ResolveStableRetireOrder", text, StringComparison.Ordinal);
            Assert.Contains("CanRetireLanePrecisely", text, StringComparison.Ordinal);
            Assert.Contains("TryResolveExceptionDeliveryDecisionForRetireWindow", text, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_09j_AssistSemantics_PreservesBoundedVisibilityClaim()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\assist-semantics.md");

            Assert.Contains("not zero-observable", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("architecturally retire-invisible", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("does not create an architectural exception winner", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("does not publish retire-visible architectural state", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("full precise exceptions theorem", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("renaming-free", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_09k_AssistSemantics_StatesQuotaBackpressureAndAdmissionFailureSurfaces()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\assist-semantics.md");

            Assert.Contains("Admission, Quota, And Backpressure", text, StringComparison.Ordinal);
            Assert.Contains("quota rejects and backpressure rejects are admission failures", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("AssistBundleQuota = 1", text, StringComparison.Ordinal);
            Assert.Contains("AssistMemoryQuota", text, StringComparison.Ordinal);
            Assert.Contains("AssistQuotaRejectKind.IssueCredits", text, StringComparison.Ordinal);
            Assert.Contains("AssistQuotaRejectKind.LineCredits", text, StringComparison.Ordinal);
            Assert.Contains("AssistBackpressureRejectKind.SharedOuterCap", text, StringComparison.Ordinal);
            Assert.Contains("AssistBackpressureRejectKind.OutstandingMemory", text, StringComparison.Ordinal);
            Assert.Contains("AssistBackpressureRejectKind.DmaStreamRegisterFile", text, StringComparison.Ordinal);
            Assert.Contains("TypedSlotRejectReason.AssistQuotaReject", text, StringComparison.Ordinal);
            Assert.Contains("TypedSlotRejectReason.AssistBackpressureReject", text, StringComparison.Ordinal);
            Assert.Contains("AssistQuotaRejects", text, StringComparison.Ordinal);
            Assert.Contains("AssistBackpressureRejects", text, StringComparison.Ordinal);
            Assert.Contains("TypedSlotAssistQuotaRejects", text, StringComparison.Ordinal);
            Assert.Contains("TypedSlotAssistBackpressureRejects", text, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_09l_AssistVisibilityEnvelope_SeparatesArchitecturalRetireTraceAndTelemetrySurfaces()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\assist-semantics.md");
            string microOp = ReadRepoFile("HybridCPU_ISE\\Core\\Pipeline\\MicroOps\\MicroOp.cs");

            Assert.Contains("Visibility Envelope", text, StringComparison.Ordinal);
            Assert.Contains("Architectural state visibility", text, StringComparison.Ordinal);
            Assert.Contains("Cache/prefetch visibility", text, StringComparison.Ordinal);
            Assert.Contains("Replay trace visibility", text, StringComparison.Ordinal);
            Assert.Contains("Telemetry visibility", text, StringComparison.Ordinal);
            Assert.Contains("assists are architecturally invisible", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("assists are retire-invisible", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("assists are microarchitecturally observable", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("assists are replay-trace-visible", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("assists are telemetry-visible", text, StringComparison.OrdinalIgnoreCase);

            Assert.Contains("retire-invisible", microOp, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("carrier-memory effects", microOp, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("replay and telemetry evidence", microOp, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_09m_AssistEntryPoints_KeepBoundedVisibilityLanguage()
        {
            string readme = ReadRepoFile("README.md");
            string whiteBook = ReadRepoFile("Documentation\\WhiteBook\\9. assist-runtime-donor-taxonomy-and-carriers.md");

            Assert.Contains("retire-invisible", readme, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("replay", readme, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("telemetry", readme, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("boundedly observable", whiteBook, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("replay and telemetry evidence", whiteBook, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("HybridCPU_ISE/docs/assist-semantics.md", whiteBook, StringComparison.Ordinal);
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
