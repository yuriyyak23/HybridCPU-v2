using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcDocumentationClaimSafetyTests
{
    private static readonly ForbiddenClaimPattern[] ForbiddenClaimPatterns =
    {
        new(
            "legacy custom accelerator active compute",
            new Regex(
                @"\blegacy custom accelerator\b.*\bactive\b.*\bcompute\b|\bactive\b.*\bcompute\b.*\blegacy custom accelerator\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        new(
            "MatMul fixture publishes memory",
            new Regex(
                @"\bmatmul fixture\b.*\bpublish(?:es)?\b.*\bmemory\b|\bmatmul\b.*\bfixture\b.*\barchitectural memory\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        new(
            "registry success grants execution",
            new Regex(
                @"\bregistry success\b.*\bgrant(?:s)?\b.*\bexecution\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        new(
            "BranchControl authorizes external accelerator commands",
            new Regex(
                @"\bbranchcontrol\b.*\bauthori[sz](?:e|es)\b.*\bexternal accelerator\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        new(
            "lane6 DmaStreamClass carries external accelerator commands",
            new Regex(
                @"\blane6\b.*\bdmastreamclass\b.*\bexternal accelerator command\b|\bdmastreamclass\b.*\bexternal accelerator command\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        new(
            "runtime silently falls back after accelerator rejection",
            new Regex(
                @"\bruntime\b.*\bsilent(?:ly)?\b.*\bfallback\b.*\baccelerator rejection\b|\baccelerator rejection\b.*\bsilent(?:ly)?\b.*\bfallback\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        new(
            "direct device write is architectural commit",
            new Regex(
                @"\bdirect device write\b.*\barchitectural commit\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        new(
            "token identity alone can commit",
            new Regex(
                @"\btoken identity alone\b.*\bcommit\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        new(
            "telemetry is authority",
            new Regex(
                @"\btelemetry\b.*\bis\b.*\bauthority\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
    };

    [Fact]
    public void L7SdcDocumentationClaimSafety_FinalDocsDoNotMakeForbiddenAffirmativeClaims()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string[] documentPaths = EnumerateClaimSafetyDocuments(repoRoot).ToArray();
        Assert.NotEmpty(documentPaths);

        var violations = new List<string>();
        foreach (string documentPath in documentPaths)
        {
            string relativePath = NormalizeRelativePath(Path.GetRelativePath(repoRoot, documentPath));
            string[] lines = File.ReadAllLines(documentPath);
            bool insideForbiddenList = false;

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex].Trim();
                if (line.StartsWith("Forbidden documentation claims:", StringComparison.OrdinalIgnoreCase))
                {
                    insideForbiddenList = true;
                    continue;
                }

                if (insideForbiddenList &&
                    (line.StartsWith("Tests to add:", StringComparison.OrdinalIgnoreCase) ||
                     line.StartsWith("Test cases:", StringComparison.OrdinalIgnoreCase) ||
                     line.StartsWith("Must not break:", StringComparison.OrdinalIgnoreCase)))
                {
                    insideForbiddenList = false;
                }

                if (insideForbiddenList || line.Length == 0 || line.StartsWith("```", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (ForbiddenClaimPattern claimPattern in ForbiddenClaimPatterns)
                {
                    if (claimPattern.Pattern.IsMatch(line) && !IsNegatedOrQuarantined(line))
                    {
                        violations.Add($"{relativePath}:{lineIndex + 1}: {claimPattern.Name}: {line}");
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "L7-SDC documentation contains affirmative unsafe claims:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void L7SdcDocumentationClaimSafety_PhaseDocsKeepDiagnosticsClosureGate()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string phaseRoot = Path.Combine(repoRoot, "Documentation", "CustomExternalAccelerator", "Phases");
        string[] phaseDocs = Directory.GetFiles(phaseRoot, "Phase_*.md", SearchOption.TopDirectoryOnly);

        string[] missingClosureGate = phaseDocs
            .Where(path =>
            {
                string text = File.ReadAllText(path);
                return !text.Contains("TestAssemblerConsoleApps", StringComparison.Ordinal) &&
                       !text.Contains("matrix-smoke", StringComparison.OrdinalIgnoreCase);
            })
            .Select(path => NormalizeRelativePath(Path.GetRelativePath(repoRoot, path)))
            .ToArray();

        Assert.Empty(missingClosureGate);
    }

    [Fact]
    public void L7SdcDocumentationClaimSafety_L7DocsNameHardPinnedSystemSingletonAndEvidenceBoundaries()
    {
        string text = ReadCombined(
            "Documentation/CustomExternalAccelerator/00_L7_SDC_Executive_Spec.md",
            "Documentation/CustomExternalAccelerator/01_L7_SDC_Migration_Phases.md",
            "Documentation/CustomExternalAccelerator/02_L7_SDC_Test_And_Rollback_Plan.md",
            "Documentation/CustomExternalAccelerator/03_L7_SDC_Phase_Code_Audit.md");

        Assert.Contains("SetHardPinnedPlacement(SlotClass.SystemSingleton, 7)", text, StringComparison.Ordinal);
        Assert.Contains("lane7", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("typed sideband", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("telemetry remains evidence only", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("after emitted `ACCEL_SUBMIT`", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("remains rejection", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void L7SdcDocumentationClaimSafety_LegacyMatMulAndContextSwitchClaimsStayGuarded()
    {
        string text = ReadCombined(
            "Documentation/CustomExternalAccelerator/00_L7_SDC_Executive_Spec.md",
            "Documentation/CustomExternalAccelerator/01_L7_SDC_Migration_Phases.md",
            "Documentation/CustomExternalAccelerator/02_L7_SDC_Test_And_Rollback_Plan.md",
            "Documentation/CustomExternalAccelerator/03_L7_SDC_Phase_Code_Audit.md",
            "Documentation/CustomExternalAccelerator/Phases/Phase_14_Documentation_Quarantine_And_Claim_Safety.md");

        Assert.Contains("No production L7-SDC path may call `ICustomAccelerator.Execute()`", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MatMul exists as metadata-only capability provider", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mapping epoch", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("detach", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("suspend", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IOMMU/domain epoch", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void L7SdcDocumentationClaimSafety_DmaStreamAndAssistDocsRemainSeparateFromL7SdcAuthority()
    {
        string dmaText = ReadCombined(
            "Documentation/Stream WhiteBook/DmaStreamCompute/00_README.md",
            "Documentation/Stream WhiteBook/DmaStreamCompute/01_Current_Contract.md",
            "Documentation/Stream WhiteBook/StreamEngine DmaStreamCompute/02_DmaStreamCompute.md");
        string assistText = ReadRepoFile("HybridCPU_ISE/docs/assist-semantics.md");

        Assert.Contains("lane6", dmaText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SlotClass.DmaStreamClass", dmaText, StringComparison.Ordinal);
        Assert.Contains("not a custom accelerator", dmaText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Execute(ref Processor.CPU_Core core)", dmaText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fail closed", dmaText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("explicit runtime helper", dmaText, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("architecturally invisible", assistText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("non-retiring", assistText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("replay-discardable", assistText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void L7SdcDocumentationClaimSafety_StreamWhiteBookUsesCurrentAnchorsOnly()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string whiteBookRoot = Path.Combine(repoRoot, "Documentation", "Stream WhiteBook");
        string[] documents = Directory.GetFiles(whiteBookRoot, "*.md", SearchOption.AllDirectories);
        var violations = new List<string>();

        foreach (string document in documents)
        {
            string[] lines = File.ReadAllLines(document);
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index];
                bool containsStaleAnchor =
                    line.Contains("Documentation/DmaStreamCompute", StringComparison.Ordinal) ||
                    line.Contains("Documentation/StreamEngine DmaStreamCompute and ExternalAccelerators", StringComparison.Ordinal);
                if (!containsStaleAnchor)
                {
                    continue;
                }

                string previous = index == 0 ? string.Empty : lines[index - 1];
                if (line.Contains("Stale only", StringComparison.OrdinalIgnoreCase) ||
                    previous.Contains("Stale only", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                violations.Add(
                    $"{NormalizeRelativePath(Path.GetRelativePath(repoRoot, document))}:{index + 1}: {line.Trim()}");
            }
        }

        string combined = ReadCombined(
            "Documentation/Stream WhiteBook/DmaStreamCompute/01_Current_Contract.md",
            "Documentation/Stream WhiteBook/ExternalAccelerators/00_README.md",
            "Documentation/Stream WhiteBook/ExternalAccelerators/05_Token_Lifecycle_And_Register_ABI.md",
            "Documentation/Stream WhiteBook/ExternalAccelerators/07_Memory_Conflict_Model.md",
            "Documentation/Stream WhiteBook/ExternalAccelerators/11_DmaStreamCompute_And_Assist_Separation.md");

        Assert.Empty(violations);
        Assert.Contains("Documentation/Stream WhiteBook/DmaStreamCompute/01_Current_Contract.md", combined, StringComparison.Ordinal);
        Assert.Contains("DmaStreamComputeRuntime` is an explicit runtime helper", combined, StringComparison.Ordinal);
        Assert.Contains("no current async DMA overlap", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ACCEL_* currently do not write architectural rd", combined, StringComparison.Ordinal);
        Assert.Contains("AcceleratorRegisterAbi is model-only", combined, StringComparison.Ordinal);
        Assert.Contains("AcceleratorFenceModel is model-only", combined, StringComparison.Ordinal);
        Assert.Contains("No executable ACCEL_FENCE", combined, StringComparison.Ordinal);
        Assert.Contains("No universal external accelerator command protocol", combined, StringComparison.Ordinal);
        Assert.Contains("model result property only", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("There is no global CPU load/store pipeline hook", combined, StringComparison.Ordinal);
    }

    private static IEnumerable<string> EnumerateClaimSafetyDocuments(string repoRoot)
    {
        string[] roots =
        {
            Path.Combine(repoRoot, "Documentation", "CustomExternalAccelerator"),
            Path.Combine(repoRoot, "Documentation", "Stream WhiteBook", "DmaStreamCompute"),
            Path.Combine(repoRoot, "Documentation", "Stream WhiteBook", "StreamEngine DmaStreamCompute"),
            Path.Combine(repoRoot, "Documentation", "Stream WhiteBook", "ExternalAccelerators")
        };

        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (string documentPath in Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories))
            {
                if (documentPath.Contains(
                    $"{Path.DirectorySeparatorChar}Ideas{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return documentPath;
            }
        }

        yield return Path.Combine(repoRoot, "HybridCPU_ISE", "docs", "assist-semantics.md");
    }

    private static bool IsNegatedOrQuarantined(string line)
    {
        string normalized = line.ToLowerInvariant();
        string[] guards =
        {
            "not ",
            "not-",
            " no ",
            "never",
            "cannot",
            "can't",
            "must not",
            "may not",
            "does not",
            "do not",
            "is not",
            "are not",
            "without",
            "forbid",
            "forbidden",
            "reject",
            "rejected",
            "fail-closed",
            "quarantine",
            "quarantined",
            "evidence only",
            "not authority"
        };

        return guards.Any(guard => normalized.Contains(guard, StringComparison.Ordinal));
    }

    private static string ReadCombined(params string[] relativePaths) =>
        string.Join(Environment.NewLine, relativePaths.Select(ReadRepoFile));

    private static string ReadRepoFile(string relativePath)
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string fullPath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath), $"Missing repository document: {relativePath}");
        return File.ReadAllText(fullPath);
    }

    private static string NormalizeRelativePath(string relativePath) =>
        relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

    private sealed record ForbiddenClaimPattern(string Name, Regex Pattern);
}
