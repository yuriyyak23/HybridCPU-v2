using System;
using System.Collections.Generic;
using System.IO;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09ExceptionClaimNarrowingDocumentationTests
    {
        private static readonly string[] Phase05Docs =
        {
            "HybridCPU_ISE\\docs\\memory-model.md",
            "HybridCPU_ISE\\docs\\exception-model.md",
            "HybridCPU_ISE\\docs\\rollback-boundaries.md",
            "HybridCPU_ISE\\docs\\assist-semantics.md",
            "HybridCPU_ISE\\docs\\backend-state-truthfulness.md"
        };

        [Fact]
        public void T9_09o_Phase05Docs_DoNotReintroduceBlanketPreciseExceptionClaims()
        {
            foreach (string relativePath in Phase05Docs)
            {
                string text = ReadRepoFile(relativePath);

                Assert.DoesNotContain("full precise exceptions", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("fully precise exceptions", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("precise exceptions theorem", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("all exceptions are precise", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("renaming-free", text, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void T9_09p_Phase05Docs_KeepStrongClaimsTiedToBoundedEvidence()
        {
            var expectations = new Dictionary<string, string[]>
            {
                ["HybridCPU_ISE\\docs\\memory-model.md"] = new[]
                {
                    "retire-local visibility model",
                    "does not prove a global memory-order theorem",
                    "does not upgrade the current exception evidence"
                },
                ["HybridCPU_ISE\\docs\\exception-model.md"] = new[]
                {
                    "bounded stage-aware retire/exception model",
                    "does not claim a complete precise-exception theorem",
                    "does not claim universal rollback"
                },
                ["HybridCPU_ISE\\docs\\rollback-boundaries.md"] = new[]
                {
                    "fail closed",
                    "does not claim universal rollback"
                },
                ["HybridCPU_ISE\\docs\\assist-semantics.md"] = new[]
                {
                    "architecturally retire-invisible",
                    "bounded invisibility claim"
                },
                ["HybridCPU_ISE\\docs\\backend-state-truthfulness.md"] = new[]
                {
                    "backend truthfulness",
                    "does not claim a complete precise-exception theorem",
                    "does not remove the backend substrate"
                }
            };

            foreach (KeyValuePair<string, string[]> expectation in expectations)
            {
                string text = ReadRepoFile(expectation.Key);
                foreach (string requiredPhrase in expectation.Value)
                {
                    Assert.Contains(requiredPhrase, text, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        [Fact]
        public void T9_09q_TestPlanningSurface_DoesNotDescribeBackendOracleAsRenamingFree()
        {
            string text = ReadRepoFile("HybridCPU_ISE.Tests\\TODO_TESTS.md");

            Assert.DoesNotContain("renaming-free oracle", text, StringComparison.OrdinalIgnoreCase);
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
