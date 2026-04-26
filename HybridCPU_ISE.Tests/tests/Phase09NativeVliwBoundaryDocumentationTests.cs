using System;
using System.IO;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09NativeVliwBoundaryDocumentationTests
    {
        [Theory]
        [InlineData("Documentation\\WhiteBook\\3. system-overview.md")]
        [InlineData("Documentation\\WhiteBook\\15. architectural-boundaries-and-non-goals.md")]
        [InlineData("Documentation\\WhiteBook\\17. current-state-and-modernization-tracks.md")]
        public void T9_08i_PrimaryWhiteBookBoundarySurfaces_StateActiveFrontendIsNativeVliwOnly(string relativePath)
        {
            string text = ReadRepoFile(relativePath);

            Assert.Contains("The active frontend is native VLIW only.", text, StringComparison.Ordinal);
            Assert.Contains("Compatibility ingress remains quarantined", text, StringComparison.Ordinal);
            Assert.Contains("DBT or scalar-generalized decode are not active proof surfaces", text, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_08j_ValidationSummary_CitesNativeVliwBoundaryFreezeSuites()
        {
            string text = ReadRepoFile("Documentation\\WhiteBook\\13. validation-status-and-test-evidence.md");

            Assert.Contains("native-VLIW-only active frontend remains frozen by boundary suites", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Phase11NativeVliwDiagnosticDecodeTests.cs", text, StringComparison.Ordinal);
            Assert.Contains("Phase12VliwCompatFreezeTests.cs", text, StringComparison.Ordinal);
            Assert.Contains("Phase12RetiredCompatPolicyBitBoundaryTests.cs", text, StringComparison.Ordinal);
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
