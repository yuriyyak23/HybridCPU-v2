namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129bvPhaseCertificateTemplateKeyRepresentationDecisionTests
{
    [Fact]
    public void RetainsTwoInternalLayoutSpecificCompositeRepresentations()
    {
        string interCore = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Certificates", "ReplayPhaseSubstrate.cs");
        string smt = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Certificates", "ReplayPhaseSubstrate.Implementations.cs");
        string ledger = Read("Documentation", "Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");

        Assert.Contains("internal readonly struct PhaseCertificateTemplateKey", interCore, StringComparison.Ordinal);
        Assert.Contains("ReplayPhaseKey phaseKey", interCore, StringComparison.Ordinal);
        Assert.Contains("BundleResourceCertificateIdentity certificateIdentity", interCore, StringComparison.Ordinal);
        Assert.Contains("PhaseKey.IsValid && CertificateIdentity.IsValid", interCore, StringComparison.Ordinal);
        Assert.Contains("internal readonly struct PhaseCertificateTemplateKey4Way", smt, StringComparison.Ordinal);
        Assert.Contains("BundleResourceCertificateIdentity4Way certificateIdentity", smt, StringComparison.Ordinal);
        Assert.Contains("SmtBundleMetadata4Way bundleMetadata", smt, StringComparison.Ordinal);
        Assert.Contains("BoundaryGuardState boundaryGuard", smt, StringComparison.Ordinal);
        Assert.Contains("BoundaryGuard.IsValid", smt, StringComparison.Ordinal);
        Assert.DoesNotContain("public readonly struct PhaseCertificateTemplateKey", interCore, StringComparison.Ordinal);
        Assert.DoesNotContain("public readonly struct PhaseCertificateTemplateKey4Way", smt, StringComparison.Ordinal);
        Assert.Contains("RF-12.9bv | closed architecture decision", ledger, StringComparison.Ordinal);
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
