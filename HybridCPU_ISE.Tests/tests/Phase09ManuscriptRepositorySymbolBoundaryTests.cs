using System;
using System.IO;
using System.Linq;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09ManuscriptRepositorySymbolBoundaryTests
    {
        private static readonly string[] BannedRepositorySymbols =
        {
            "SlotClassLaneMap",
            "LegalityDecision",
            "PhaseCertificateTemplateKey4Way",
            "PackBundleIntraCoreSmt",
            "PipelineFspStage2_Intersect",
            "TryClassAdmission",
            "TryMaterializeLane",
            "ResolveNextInjectableSlot",
            "TryPassOuterCap",
            "TypedSlotEnabled",
            "RenameMap",
            "CommitMap",
            "FreeList",
            "SCHED1",
            "SCHED2"
        };

        [Fact]
        public void T9_17b_ManuscriptSections_DoNotNarrate_Through_RepositorySymbols()
        {
            string[] manuscriptPaths = EnumerateManuscriptFiles().ToArray();
            Assert.NotEmpty(manuscriptPaths);

            foreach (string manuscriptPath in manuscriptPaths)
            {
                string text = File.ReadAllText(manuscriptPath);

                foreach (string symbol in BannedRepositorySymbols)
                {
                    Assert.DoesNotContain(symbol, text, StringComparison.Ordinal);
                }
            }
        }

        [Fact]
        public void T9_17c_ManuscriptTables_DoNotExpose_CodeAnchorColumns()
        {
            string[] manuscriptPaths = EnumerateManuscriptFiles().ToArray();
            Assert.NotEmpty(manuscriptPaths);

            foreach (string manuscriptPath in manuscriptPaths)
            {
                string text = File.ReadAllText(manuscriptPath);
                Assert.DoesNotContain("Code anchors", text, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static string[] EnumerateManuscriptFiles()
        {
            string repoRoot = CompatFreezeScanner.FindRepoRoot();
            string mdBaseDir = Path.Combine(repoRoot, "ResearchPaper", "section", "md base");
            string latexSectionsDir = Path.Combine(repoRoot, "ResearchPaper", "section", "latex", "Sections");
            string[] mdFiles = Directory.EnumerateFiles(mdBaseDir, "*.md", SearchOption.TopDirectoryOnly).ToArray();

            if (mdFiles.Length > 0)
            {
                return mdFiles
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            return Directory
                .EnumerateFiles(latexSectionsDir, "*.tex", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
