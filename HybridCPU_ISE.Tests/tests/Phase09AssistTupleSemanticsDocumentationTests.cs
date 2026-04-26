using System;
using System.IO;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09AssistTupleSemanticsDocumentationTests
    {
        [Fact]
        public void T9_09r_AssistSemantics_StatesCanonicalFourTupleClosure()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\assist-semantics.md");

            Assert.Contains("Carrier Tuple Matrix", text, StringComparison.Ordinal);
            Assert.Contains("Donor-Source Binding Invariants", text, StringComparison.Ordinal);
            Assert.Contains("Four-Tuple Legality Matrix", text, StringComparison.Ordinal);
            Assert.Contains("AssistKind", text, StringComparison.Ordinal);
            Assert.Contains("AssistExecutionMode", text, StringComparison.Ordinal);
            Assert.Contains("AssistCarrierKind", text, StringComparison.Ordinal);
            Assert.Contains("AssistDonorSourceKind", text, StringComparison.Ordinal);
            Assert.Contains("Every four-tuple not named in this table is illegal", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_09s_AssistSemantics_KeepsCarrierDonorAndDensificationAxesSeparate()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\assist-semantics.md");

            Assert.Contains("bundle densification", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("donor provenance", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("carrier semantics", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("A donor kind does not implicitly pick a carrier", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("a carrier choice does not rewrite donor provenance", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("SameThreadSeed", text, StringComparison.Ordinal);
            Assert.Contains("IntraCoreVtDonorVector", text, StringComparison.Ordinal);
            Assert.Contains("InterCoreSameVtSeed", text, StringComparison.Ordinal);
            Assert.Contains("InterCoreVtDonorSeed", text, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_09t_AssistRuntimeComments_DescribeHowTheFourTupleCloses()
        {
            string runtime = ReadRepoFile("HybridCPU_ISE\\Core\\Pipeline\\Assist\\AssistRuntime.cs");

            Assert.Contains("first three tuple axes", runtime, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("donor-source axis", runtime, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("kind/execution/carrier contour", runtime, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("does not replace slot admission authority", runtime, StringComparison.OrdinalIgnoreCase);
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
