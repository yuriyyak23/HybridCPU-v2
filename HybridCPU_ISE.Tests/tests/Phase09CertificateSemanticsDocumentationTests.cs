using System;
using System.IO;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09CertificateSemanticsDocumentationTests
    {
        [Fact]
        public void T9_12a_CertificateSemanticsArtifact_States_Descriptor_Certificate_And_EvidenceDiagram()
        {
            string text = ReadRepoFile("Documentation\\WhiteBook\\23. certificate-semantics-and-legality-evidence.md");

            Assert.Contains("BundleLegalityDescriptor", text, StringComparison.Ordinal);
            Assert.Contains("BundleLegalityAnalyzer", text, StringComparison.Ordinal);
            Assert.Contains("BundleResourceCertificate4Way", text, StringComparison.Ordinal);
            Assert.Contains("StructuralIdentity", text, StringComparison.Ordinal);
            Assert.Contains("ClassOccupancy", text, StringComparison.Ordinal);
            Assert.Contains("LegalityDecision", text, StringComparison.Ordinal);
            Assert.Contains("IRuntimeLegalityService", text, StringComparison.Ordinal);
            Assert.Contains("PhaseCertificateTemplateKey4Way", text, StringComparison.Ordinal);
            Assert.Contains("ReplayPhaseKey", text, StringComparison.Ordinal);
            Assert.Contains("SmtBundleMetadata4Way", text, StringComparison.Ordinal);
            Assert.Contains("BoundaryGuardState", text, StringComparison.Ordinal);
            Assert.Contains("```mermaid", text, StringComparison.Ordinal);
            Assert.Contains("not raw mask layout", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("runtime legality remains the authority", text, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("Documentation\\WhiteBook\\0. chapter-index.md")]
        [InlineData("Documentation\\WhiteBook\\10. safety-isolation-and-legality.md")]
        [InlineData("Documentation\\WhiteBook\\19. references-and-reading-order.md")]
        [InlineData("Documentation\\WhiteBook\\20. legality-predicate.md")]
        public void T9_12b_WhiteBookEntryPoints_Expose_CertificateSemanticsArtifact(string relativePath)
        {
            string text = ReadRepoFile(relativePath);

            Assert.Contains("23. certificate-semantics-and-legality-evidence.md", text, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_12c_CodeSurfaces_Preserve_CertificateSemantics_Over_MaskLayout()
        {
            string descriptorText = ReadRepoFile("HybridCPU_ISE\\Core\\Legality\\BundleLegalityDescriptor.cs");
            Assert.Contains("Canonical Phase 03 legality-only descriptor", descriptorText, StringComparison.Ordinal);
            Assert.Contains("excludes cluster issue preparation", descriptorText, StringComparison.Ordinal);

            string certificateText = ReadRepoFile("HybridCPU_ISE\\Core\\Pipeline\\Certificates\\BundleResourceCertificate4Way.cs");
            Assert.Contains("structural witness", certificateText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ClassOccupancy", certificateText, StringComparison.Ordinal);
            Assert.Contains("StructuralIdentity", certificateText, StringComparison.Ordinal);
            Assert.Contains("rather than raw mask layout alone", certificateText, StringComparison.OrdinalIgnoreCase);

            string replayText = ReadRepoFile("HybridCPU_ISE\\Core\\Pipeline\\Certificates\\ReplayPhaseSubstrate.Implementations.cs");
            Assert.Contains("Replay epoch is only one part of the scope", replayText, StringComparison.Ordinal);
            Assert.Contains("PhaseCertificateTemplateKey4Way", replayText, StringComparison.Ordinal);
            Assert.Contains("SmtBundleMetadata4Way", replayText, StringComparison.Ordinal);
            Assert.Contains("BoundaryGuardState", replayText, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_12d_ValidationSummary_Cites_CertificateSemanticsProofSurface()
        {
            string text = ReadRepoFile("Documentation\\WhiteBook\\13. validation-status-and-test-evidence.md");

            Assert.Contains("Phase09CertificateSemanticsDocumentationTests.cs", text, StringComparison.Ordinal);
            Assert.Contains("certificate semantics", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("51", text, StringComparison.Ordinal);
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
