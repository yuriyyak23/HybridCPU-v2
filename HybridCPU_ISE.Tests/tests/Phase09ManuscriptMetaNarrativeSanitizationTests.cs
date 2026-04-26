using System;
using System.IO;
using System.Linq;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09ManuscriptMetaNarrativeSanitizationTests
    {
        private static readonly string[] BannedEditorialPhrases =
        {
            "should be read as",
            "Section 9 is allowed",
            "can now be stated directly",
            "Every empirical statement below should therefore be read"
        };

        [Fact]
        public void T9_17a_ManuscriptSections_DoNotContain_EditorialResidue()
        {
            string[] manuscriptPaths = EnumerateManuscriptFiles().ToArray();
            Assert.NotEmpty(manuscriptPaths);

            foreach (string manuscriptPath in manuscriptPaths)
            {
                string text = File.ReadAllText(manuscriptPath);

                foreach (string phrase in BannedEditorialPhrases)
                {
                    Assert.DoesNotContain(phrase, text, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        private static string[] EnumerateManuscriptFiles()
        {
            string repoRoot = CompatFreezeScanner.FindRepoRoot();
            string mdBaseDir = Path.Combine(repoRoot, "ResearchPaper", "section", "md base");
            string latexSectionsDir = Path.Combine(repoRoot, "ResearchPaper", "section", "latex", "Sections");
            string[] latexFiles = Directory.EnumerateFiles(latexSectionsDir, "*.tex", SearchOption.TopDirectoryOnly).ToArray();

            if (latexFiles.Length > 0)
            {
                return latexFiles
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            return Directory
                .EnumerateFiles(mdBaseDir, "*.md", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
