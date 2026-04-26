using System;
using System.IO;
using System.Linq;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09ReviewerRebuttalClaimBoundaryTests
    {
        private static readonly string[] BannedManuscriptPhrases =
        {
            "certificate-governed",
            "structural certificate",
            "resource certificate",
            "guard plane",
            "exception-backed stub",
            "fail-on-disable",
            "missing typed-slot facts halt execution",
            "OoO-free",
            "Hybrid CPU Model",
            "native VLIW",
            "native-VLIW"
        };

        [Fact]
        public void T9_18a_ManuscriptSections_DoNot_Reintroduce_RebuttalBoundaryOverclaims()
        {
            string[] manuscriptPaths = EnumerateManuscriptFiles().ToArray();
            Assert.NotEmpty(manuscriptPaths);

            foreach (string manuscriptPath in manuscriptPaths)
            {
                string text = File.ReadAllText(manuscriptPath);

                foreach (string phrase in BannedManuscriptPhrases)
                {
                    Assert.DoesNotContain(phrase, text, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        [Fact]
        public void T9_18b_ManuscriptFailClosedWording_Remains_HandshakeScoped()
        {
            string[] manuscriptPaths = EnumerateManuscriptFiles().ToArray();
            Assert.NotEmpty(manuscriptPaths);

            foreach (string manuscriptPath in manuscriptPaths)
            {
                string[] lines = File.ReadAllLines(manuscriptPath);

                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].IndexOf("fail-closed", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    string context = string.Join(
                        " ",
                        i > 0 ? lines[i - 1] : string.Empty,
                        lines[i],
                        i + 1 < lines.Length ? lines[i + 1] : string.Empty);

                    Assert.True(
                        context.IndexOf("handshake", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        context.IndexOf("ingress", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        context.IndexOf("version", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        context.IndexOf("rollback", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        context.IndexOf("guard", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        context.IndexOf("negative-control", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        context.IndexOf("admission", StringComparison.OrdinalIgnoreCase) >= 0,
                        $"Fail-closed wording in {manuscriptPath} must remain scoped to the ingress/version handshake. Offending line: {lines[i]}");
                }
            }
        }

        [Fact]
        public void T9_18c_SchedulerContourArtifact_States_OptInToggle_And_ExactSlotFallback()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\scheduler-contours-and-boundaries.md");

            Assert.Contains("TypedSlotEnabled", text, StringComparison.Ordinal);
            Assert.Contains("opt-in", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("A-B comparison", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Default `false`", text, StringComparison.Ordinal);
            Assert.Contains("does not throw or halt execution", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("exact-slot compatibility path", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("falls back to compatibility", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("failing closed", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("LateBindingConflict", text, StringComparison.Ordinal);
            Assert.Contains("not generic port arbitration", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_18d_BoundaryDocs_State_HandshakeScopedFailClosed_And_HardwareNonClaim()
        {
            string boundaryText = ReadRepoFile("HybridCPU_ISE\\docs\\compiler-runtime-boundary.md");
            string mapText = ReadRepoFile("Documentation\\paper-claim-evidence-map.md");

            Assert.Contains("only fail-closed claim", boundaryText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("missing typed-slot facts halt", boundaryText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("canonical execution", boundaryText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("structured handoff", boundaryText, StringComparison.OrdinalIgnoreCase);

            Assert.Contains("Hardware-cost critique is handled as an explicit non-claim", mapText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("timing/area/PPA", mapText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("outside the present evidence envelope", mapText, StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadRepoFile(string relativePath)
        {
            string repoRoot = CompatFreezeScanner.FindRepoRoot();
            string fullPath = Path.Combine(repoRoot, relativePath);
            Assert.True(File.Exists(fullPath), $"Missing repository document: {relativePath}");
            return File.ReadAllText(fullPath);
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
