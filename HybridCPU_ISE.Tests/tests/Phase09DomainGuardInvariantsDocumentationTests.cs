using System;
using System.IO;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09DomainGuardInvariantsDocumentationTests
    {
        [Fact]
        public void T9_15a_DomainGuardArtifact_StatesGuardBeforeReuseAndAuthorityScope()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\domain-guard-invariants.md");

            Assert.Contains("guard-before-reuse", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("domain and owner guards precede replay reuse", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("LegalityAuthoritySource.GuardPlane", text, StringComparison.Ordinal);
            Assert.Contains("attemptedReplayCertificateReuse: false", text, StringComparison.Ordinal);
            Assert.Contains("not a hardware root-of-trust", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("EvaluateDomainIsolationProbe", text, StringComparison.Ordinal);
            Assert.Contains("EvaluateInterCoreDomainGuard", text, StringComparison.Ordinal);
            Assert.Contains("EvaluateSmtBoundaryGuard", text, StringComparison.Ordinal);
            Assert.Contains("TryRejectSmtOwnerDomainGuard", text, StringComparison.Ordinal);
            Assert.Contains("TryValidateAssistMicroOp", text, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_15b_DomainGuardArtifact_NamesRejectAndTelemetrySurfaces()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\domain-guard-invariants.md");

            Assert.Contains("RejectKind.OwnerMismatch", text, StringComparison.Ordinal);
            Assert.Contains("RejectKind.DomainMismatch", text, StringComparison.Ordinal);
            Assert.Contains("RejectKind.Boundary", text, StringComparison.Ordinal);
            Assert.Contains("AssistInvalidationReason.OwnerInvalidation", text, StringComparison.Ordinal);
            Assert.Contains("AssistInvalidationReason.InterCoreOwnerDrift", text, StringComparison.Ordinal);
            Assert.Contains("AssistInvalidationReason.InterCoreBoundaryDrift", text, StringComparison.Ordinal);
            Assert.Contains("SmtOwnerContextGuardRejects", text, StringComparison.Ordinal);
            Assert.Contains("SmtDomainGuardRejects", text, StringComparison.Ordinal);
            Assert.Contains("SmtBoundaryGuardRejects", text, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_15c_CanonicalGuardComments_DoNotExplainGuardPlaneThroughLegacyFspInjection()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\Core\\Pipeline\\Safety\\SafetyVerifier.Guards.cs");

            Assert.Contains("before scheduler admission, replay reuse, or certificate acceptance", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("guard plane", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("before FSP injection", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("stolen slot", text, StringComparison.OrdinalIgnoreCase);
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
