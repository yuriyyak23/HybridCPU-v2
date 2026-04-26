using System;
using System.IO;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09LegalityPredicateDocumentationTests
    {
        [Fact]
        public void T9_09a_LegalityPredicateArtifact_StatesCanonicalAuthority_And_StageSplit()
        {
            string text = ReadRepoFile("Documentation\\WhiteBook\\20. legality-predicate.md");

            Assert.Contains("LegalityDecision", text, StringComparison.Ordinal);
            Assert.Contains("IRuntimeLegalityService", text, StringComparison.Ordinal);
            Assert.Contains("Stage A", text, StringComparison.Ordinal);
            Assert.Contains("Stage B", text, StringComparison.Ordinal);
            Assert.Contains("guard plane", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("StructuralIdentity", text, StringComparison.Ordinal);
            Assert.Contains("ReplayPhaseCertificate", text, StringComparison.Ordinal);
            Assert.Contains("StructuralCertificate", text, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("Documentation\\WhiteBook\\0. chapter-index.md")]
        [InlineData("Documentation\\WhiteBook\\10. safety-isolation-and-legality.md")]
        [InlineData("Documentation\\WhiteBook\\19. references-and-reading-order.md")]
        public void T9_09b_WhiteBookEntryPoints_ExposeLegalityPredicateArtifact(string relativePath)
        {
            string text = ReadRepoFile(relativePath);

            Assert.Contains("20. legality-predicate.md", text, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_09c_TypedSlotChapter_Keeps_StageA_StageB_NonCollapsed()
        {
            string text = ReadRepoFile("Documentation\\WhiteBook\\6. typed-slot-scheduling.md");

            Assert.Contains("Stage A of the typed-slot pipeline is `TryClassAdmission(...)`.", text, StringComparison.Ordinal);
            Assert.Contains("Stage B is `TryMaterializeLane(...)`:", text, StringComparison.Ordinal);
            Assert.Contains("Stage B does not reopen legality.", text, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_09d_ValidationSummary_Cites_LegalityPredicate_And_RuntimeProofSuites()
        {
            string text = ReadRepoFile("Documentation\\WhiteBook\\13. validation-status-and-test-evidence.md");

            Assert.Contains("Phase09LegalityPredicateDocumentationTests.cs", text, StringComparison.Ordinal);
            Assert.Contains("Phase09RuntimeLegalityServiceReachabilityProofTests.cs", text, StringComparison.Ordinal);
            Assert.Contains("Phase09SafetyVerifierGuardMatrixProofTests.cs", text, StringComparison.Ordinal);
            Assert.Contains("Phase09ReplayCertificateCoordinatorProofTests.cs", text, StringComparison.Ordinal);
            Assert.Contains("validation-baseline.md", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("dotnet test", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("dotnet vstest", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_09e_CanonicalCodeComments_Keep_StageBoundary_And_Avoid_ProofOverclaim()
        {
            string schedulerText = ReadRepoFile("HybridCPU_ISE\\Core\\Pipeline\\Scheduling\\MicroOpScheduler.Admission.cs");
            Assert.Contains("Stage A:", schedulerText, StringComparison.Ordinal);
            Assert.Contains("guard-plane first", schedulerText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("A1. Class-capacity", schedulerText, StringComparison.Ordinal);
            Assert.Contains("A2. Legality service decision", schedulerText, StringComparison.Ordinal);
            Assert.Contains("A3. IsOuterCapBlocking", schedulerText, StringComparison.Ordinal);
            Assert.DoesNotContain("A4. IsOuterCapBlocking", schedulerText, StringComparison.Ordinal);
            Assert.Contains("Stage B:", schedulerText, StringComparison.Ordinal);
            Assert.Contains("does not widen legality", schedulerText, StringComparison.OrdinalIgnoreCase);

            string runtimeLegalityText = ReadRepoFile("HybridCPU_ISE\\Core\\Pipeline\\Safety\\SafetyVerifier.RuntimeLegality.cs");
            Assert.Contains("structural conflict summary", runtimeLegalityText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("formal proof", runtimeLegalityText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Singularity-style verification", runtimeLegalityText, StringComparison.OrdinalIgnoreCase);
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
