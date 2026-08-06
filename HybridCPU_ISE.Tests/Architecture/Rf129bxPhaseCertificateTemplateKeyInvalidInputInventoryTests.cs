namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129bxPhaseCertificateTemplateKeyInvalidInputInventoryTests
{
    [Fact]
    public void InvalidKeysAreFrozenAtCacheSuppressionAndLiveLegalityConsumers()
    {
        string source = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Certificates", "ReplayPhaseSubstrate.Implementations.cs");
        string interCore = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Safety", "SafetyVerifier.RuntimeLegality.cs");
        string smt = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Safety", "SafetyVerifier.SmtLegality.cs");
        string ledger = Read("Documentation", "Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");

        Assert.Contains("if (!templateKey.IsValid)", source, StringComparison.Ordinal);
        Assert.Contains("if (templateKey.IsValid)", source, StringComparison.Ordinal);
        Assert.Contains("? new PhaseCertificateTemplate4Way(templateKey, certificate)\n                : default", source.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("bool wasInvalidated = Invalidate();", source, StringComparison.Ordinal);
        Assert.Contains("bool attemptedReplayReuse = liveTemplateKey.PhaseKey.IsValid", interCore, StringComparison.Ordinal);
        Assert.Contains("return EvaluateInterCoreCertificate(", interCore, StringComparison.Ordinal);
        Assert.Contains("return EvaluateInterCoreCompatibility(", interCore, StringComparison.Ordinal);
        Assert.Contains("LegalityDecision boundaryDecision = EvaluateSmtBoundaryGuard(liveTemplateKey);", smt, StringComparison.Ordinal);
        Assert.Contains("TryRejectSmtOwnerDomainGuard", smt, StringComparison.Ordinal);
        Assert.Contains("return EvaluateSmtCertificate(", smt, StringComparison.Ordinal);
        Assert.Contains("RF-12.9bx | closed inventory/freeze", ledger, StringComparison.Ordinal);
    }

    private static string Read(params string[] path) => File.ReadAllText(Path.Combine(new[] { FindRepositoryRoot() }.Concat(path).ToArray()));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE"))) return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException();
    }
}
