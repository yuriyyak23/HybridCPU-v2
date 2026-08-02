using System;
using System.IO;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.DocumentationContracts
{
    public sealed class NativeVliwBoundaryDocumentationTests
    {
        [Theory]
        [InlineData("Documentation\\Documentation\\WhiteBook\\3. system-overview.md")]
        [InlineData("Documentation\\Documentation\\WhiteBook\\15. architectural-boundaries-and-non-goals.md")]
        [InlineData("Documentation\\Documentation\\WhiteBook\\17. current-state-and-modernization-tracks.md")]
        public void T9_08i_PrimaryWhiteBookBoundarySurfaces_StateActiveFrontendIsNativeVliwOnly(string relativePath)
        {
            string text = ReadRepoFile(relativePath);

            Assert.Contains("The active frontend is native VLIW only.", text, StringComparison.Ordinal);
            Assert.Contains("Compatibility ingress remains quarantined", text, StringComparison.Ordinal);
            Assert.Contains("DBT or scalar-generalized decode are not active proof surfaces", text, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_08j_ArchitectureAuthorityRefactorSummary_PreservesPublicFrontendAndFaultEvidence()
        {
            string decoderClosure = ReadRepoFile(
                "Documentation\\ArchitectureAuthorityRefactor\\Evidence\\RF06\\rf00-rf06-documentation-reconciliation.md");
            string audit = ReadRepoFile("Documentation\\ArchitectureAuthorityRefactor\\Evidence\\RF07\\rf07.exit-final-closed-world-audit.md");

            Assert.Contains("VliwDecoderV4 : IDecoderFrontend", decoderClosure, StringComparison.Ordinal);
            Assert.Contains("FaultInjection", audit, StringComparison.Ordinal);
            Assert.Contains("unknown exceptions", audit, StringComparison.OrdinalIgnoreCase);
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
