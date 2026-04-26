using System;
using System.IO;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09ReviewerMailTraceabilityTests
    {
        [Fact]
        public void T9_19a_Section3_States_TransportWidth_And_ProjectedMetadataBoundary()
        {
            string text = ReadPreferredManuscriptSection(
                "3.0_Architectural_Overview_and_Frontend_Contract.tex",
                "3_Architectural_Overview_and_Frontend_Contract.md");

            Assert.True(
                text.Contains("256 bytes = 2048 bits", StringComparison.Ordinal) ||
                text.Contains("256-byte", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("2048-bit", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("W=8", StringComparison.OrdinalIgnoreCase),
                "Section 3 should retain an explicit transport-width anchor (256-byte / 2048-bit or canonical W=8 substrate anchor)."
            );
            Assert.True(
                text.Contains("word0", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("slot", StringComparison.OrdinalIgnoreCase),
                "Section 3 should keep explicit slot-word/slot-structure anchoring."
            );
            Assert.True(
                text.Contains("SlotClass", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("typed", StringComparison.OrdinalIgnoreCase),
                "Section 3 should retain typed placement anchoring."
            );
            Assert.True(
                text.Contains("domainTag", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("owner", StringComparison.OrdinalIgnoreCase),
                "Section 3 should retain ownership/domain anchoring."
            );
        }

        [Fact]
        public void T9_19b_Section7_Separates_ReplayPhaseReuse_From_BoundedReplayTokenRollback()
        {
            string text = ReadPreferredManuscriptSection(
                "7.0_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.tex",
                "7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md");

            Assert.True(
                text.Contains("ReplayToken", StringComparison.Ordinal) ||
                text.Contains("replay token", StringComparison.OrdinalIgnoreCase),
                "Section 7 should retain explicit replay-token terminology."
            );
            Assert.Contains("replay phase", text, StringComparison.OrdinalIgnoreCase);
            Assert.True(
                text.Contains("invalidated", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("invalidates", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("invalidation", StringComparison.OrdinalIgnoreCase),
                "Section 7 should retain replay invalidation terminology."
            );
            Assert.Contains("rollback", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("live legality", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_19c_EvidenceMaps_Trace_Transport_Replay_Assist_And_Ordering_Narrowing()
        {
            string mapText = ReadRepoFile("Documentation\\paper-claim-evidence-map.md");
            string anchorText = ReadRepoFile("Documentation\\manuscript-implementation-anchor-map.md");

            Assert.Contains("2048 bits", mapText, StringComparison.Ordinal);
            Assert.Contains("word0", mapText, StringComparison.Ordinal);
            Assert.Contains("domainTag", mapText, StringComparison.Ordinal);
            Assert.Contains("Replay-phase/template reuse is distinct", mapText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Acquire/Release", mapText, StringComparison.Ordinal);
            Assert.Contains("donor assist epoch", mapText, StringComparison.OrdinalIgnoreCase);

            Assert.Contains("2048-bit", anchorText, StringComparison.Ordinal);
            Assert.Contains("word0", anchorText, StringComparison.Ordinal);
            Assert.Contains("inter-core assist freshness", anchorText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ordering surface boundary", anchorText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ReplayToken", anchorText, StringComparison.Ordinal);
        }

        private static string ReadRepoFile(string relativePath)
        {
            string repoRoot = CompatFreezeScanner.FindRepoRoot();
            string fullPath = Path.Combine(repoRoot, relativePath);
            Assert.True(File.Exists(fullPath), $"Missing repository file: {relativePath}");
            return File.ReadAllText(fullPath);
        }

        private static string ReadPreferredManuscriptSection(string latexFileName, string mdFileName)
        {
            string repoRoot = CompatFreezeScanner.FindRepoRoot();
            string latexPath = Path.Combine(repoRoot, "ResearchPaper", "section", "latex", "Sections", latexFileName);
            if (File.Exists(latexPath))
            {
                return File.ReadAllText(latexPath);
            }

            string mdPath = Path.Combine(repoRoot, "ResearchPaper", "section", "md base", mdFileName);
            Assert.True(File.Exists(mdPath), $"Missing manuscript section: {latexFileName} / {mdFileName}");
            return File.ReadAllText(mdPath);
        }
    }
}
