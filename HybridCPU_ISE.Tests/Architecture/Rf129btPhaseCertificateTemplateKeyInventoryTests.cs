namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129btPhaseCertificateTemplateKeyInventoryTests
{
    [Fact]
    public void InterCoreAndFourWayTemplateKeysRemainSeparateCertificateCarriers()
    {
        string baseSubstrate = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Certificates", "ReplayPhaseSubstrate.cs");
        string implementations = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Certificates", "ReplayPhaseSubstrate.Implementations.cs");
        string schedulerSafety = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Safety", "SafetyVerifier.SmtLegality.cs");
        string ledger = Read("Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");
        Assert.Contains("internal readonly struct PhaseCertificateTemplateKey", baseSubstrate, StringComparison.Ordinal);
        Assert.Contains("ReplayPhaseKey phaseKey", baseSubstrate, StringComparison.Ordinal);
        Assert.Contains("BundleResourceCertificateIdentity certificateIdentity", baseSubstrate, StringComparison.Ordinal);
        Assert.Contains("public bool IsValid => PhaseKey.IsValid && CertificateIdentity.IsValid", baseSubstrate, StringComparison.Ordinal);
        Assert.Contains("internal readonly struct PhaseCertificateTemplateKey4Way", implementations, StringComparison.Ordinal);
        Assert.Contains("SmtBundleMetadata4Way bundleMetadata", implementations, StringComparison.Ordinal);
        Assert.Contains("BoundaryGuardState boundaryGuard", implementations, StringComparison.Ordinal);
        Assert.Contains("EvaluateSmtLegality(", schedulerSafety, StringComparison.Ordinal);
        Assert.DoesNotContain("struct CertificateTemplateKey", baseSubstrate, StringComparison.Ordinal);
        Assert.Contains("RF-12.9bt | closed inventory/freeze", ledger, StringComparison.Ordinal);
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
