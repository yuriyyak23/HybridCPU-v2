using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09ValidationEvidenceCountDocumentationTests
    {
        private static readonly Regex TestDeclarationPattern = new(@"^\s*\[(Fact|Theory)\b", RegexOptions.CultureInvariant);

        [Fact]
        public void T9_08q_ValidationEvidenceDocuments_PublishLiveCountsAndRecountAutomation()
        {
            string repoRoot = CompatFreezeScanner.FindRepoRoot();
            EvidenceCounts counts = CollectCounts(repoRoot);

            string whiteBook = ReadRepoFile(repoRoot, "Documentation\\WhiteBook\\13. validation-status-and-test-evidence.md");
            string baseline = ReadRepoFile(repoRoot, "Documentation\\validation-baseline.md");
            string matrix = ReadRepoFile(repoRoot, "Documentation\\evidence-matrix.md");
            string? recountScript = TryReadRepoFile(repoRoot, "build\\recount-validation-evidence.ps1");

            Assert.Contains("`.cs` source files under `HybridCPU_ISE/`", whiteBook, StringComparison.Ordinal);
            Assert.Contains("`.cs` source files under `HybridCPU_Compiler/`", whiteBook, StringComparison.Ordinal);
            Assert.Contains("`.cs` source files under `HybridCPU_ISE.Tests/tests/`", whiteBook, StringComparison.Ordinal);
            Assert.Contains($"`{FactAttribute()}` / `{TheoryAttribute()}` declarations under `HybridCPU_ISE.Tests/tests/`", whiteBook, StringComparison.Ordinal);
            Assert.Contains("`.cs` source files across the full `HybridCPU_ISE.Tests/` tree", whiteBook, StringComparison.Ordinal);
            Assert.Contains($"`{FactAttribute()}` / `{TheoryAttribute()}` declarations across the full `HybridCPU_ISE.Tests/` tree", whiteBook, StringComparison.Ordinal);

            if (recountScript is not null)
            {
                Assert.Contains("build/recount-validation-evidence.ps1", whiteBook, StringComparison.Ordinal);
                Assert.Contains("run-validation-baseline.ps1", recountScript, StringComparison.Ordinal);
            }

            Assert.Contains("Documentation/evidence-matrix.md", whiteBook, StringComparison.Ordinal);
            Assert.Contains("Passed: `51`", baseline, StringComparison.Ordinal);
            Assert.Contains("Passed: `51`", matrix, StringComparison.Ordinal);
            Assert.Contains("Phase09ValidationEvidenceCountDocumentationTests", matrix, StringComparison.Ordinal);

            Assert.True(counts.IseSourceFiles > 0, "Expected non-zero ISE source file count.");
            Assert.True(counts.CompilerSourceFiles > 0, "Expected non-zero compiler source file count.");
            Assert.True(counts.TestsDirectorySourceFiles > 0, "Expected non-zero tests/ source file count.");
            Assert.True(counts.TestsDirectoryDeclarations > 0, "Expected non-zero tests/ declaration count.");
            Assert.True(counts.TestsAllSourceFiles > 0, "Expected non-zero full test tree source file count.");
            Assert.True(counts.TestsAllDeclarations > 0, "Expected non-zero full test tree declaration count.");
        }

        private static EvidenceCounts CollectCounts(string repoRoot)
        {
            string iseRoot = Path.Combine(repoRoot, "HybridCPU_ISE");
            string compilerRoot = Path.Combine(repoRoot, "HybridCPU_Compiler");
            string testsRoot = Path.Combine(repoRoot, "HybridCPU_ISE.Tests");
            string testsDirectoryRoot = Path.Combine(testsRoot, "tests");

            string[] testsDirectoryFiles = EnumerateSourceFiles(testsDirectoryRoot);
            string[] testsAllFiles = EnumerateSourceFiles(testsRoot);

            return new EvidenceCounts(
                EnumerateSourceFiles(iseRoot).Length,
                EnumerateSourceFiles(compilerRoot).Length,
                testsDirectoryFiles.Length,
                CountTestDeclarations(testsDirectoryFiles),
                testsAllFiles.Length,
                CountTestDeclarations(testsAllFiles));
        }

        private static string[] EnumerateSourceFiles(string root)
        {
            if (!Directory.Exists(root))
            {
                return Array.Empty<string>();
            }

            return Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(filePath => !CompatFreezeScanner.IsGeneratedPath(filePath))
                .ToArray();
        }

        private static int CountTestDeclarations(string[] filePaths) =>
            filePaths
                .SelectMany(File.ReadLines)
                .Sum(line => TestDeclarationPattern.Matches(line).Count);

        private static string FactAttribute() => "[" + "Fact]";

        private static string TheoryAttribute() => "[" + "Theory]";

        private static string ReadRepoFile(string repoRoot, string relativePath)
        {
            string fullPath = Path.Combine(repoRoot, relativePath);
            Assert.True(File.Exists(fullPath), $"Missing repository evidence surface: {relativePath}");
            return File.ReadAllText(fullPath);
        }

        private static string? TryReadRepoFile(string repoRoot, string relativePath)
        {
            string fullPath = Path.Combine(repoRoot, relativePath);
            return File.Exists(fullPath) ? File.ReadAllText(fullPath) : null;
        }

        private sealed record EvidenceCounts(
            int IseSourceFiles,
            int CompilerSourceFiles,
            int TestsDirectorySourceFiles,
            int TestsDirectoryDeclarations,
            int TestsAllSourceFiles,
            int TestsAllDeclarations);
    }
}
