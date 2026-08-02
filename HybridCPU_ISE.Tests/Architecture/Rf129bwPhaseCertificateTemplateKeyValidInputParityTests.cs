namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129bwPhaseCertificateTemplateKeyValidInputParityTests
{
    [Fact]
    public void BuildersAndCachesPreserveEachLayoutSpecificValidTuple()
    {
        string source = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Certificates", "ReplayPhaseSubstrate.Implementations.cs");
        string ledger = Read("Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");

        Assert.Contains("new PhaseCertificateTemplateKey(", source, StringComparison.Ordinal);
        Assert.Contains("certificate.StructuralIdentity", source, StringComparison.Ordinal);
        Assert.Contains("new PhaseCertificateTemplateKey4Way(", source, StringComparison.Ordinal);
        Assert.Contains("SmtBundleMetadata4Way bundleMetadata", source, StringComparison.Ordinal);
        Assert.Contains("BoundaryGuardState boundaryGuard", source, StringComparison.Ordinal);
        Assert.Contains("if (_template.Matches(templateKey))", source, StringComparison.Ordinal);
        Assert.Contains("_template = new PhaseCertificateTemplate(templateKey, certificate)", source, StringComparison.Ordinal);
        Assert.Contains("if (!templateKey.IsValid || _template.Matches(templateKey))", source, StringComparison.Ordinal);
        Assert.Contains("_template = new PhaseCertificateTemplate4Way(templateKey, certificate)", source, StringComparison.Ordinal);
        Assert.Contains("RF-12.9bw | closed valid-input contract", ledger, StringComparison.Ordinal);
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
