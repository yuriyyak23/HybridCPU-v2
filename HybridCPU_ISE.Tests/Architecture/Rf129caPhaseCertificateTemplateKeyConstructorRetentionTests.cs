namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129caPhaseCertificateTemplateKeyConstructorRetentionTests
{
    [Fact]
    public void ActiveInternalConstructorsRemainNarrowLayoutSpecificSeams()
    {
        string interCore = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Certificates", "ReplayPhaseSubstrate.cs");
        string source = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Certificates", "ReplayPhaseSubstrate.Implementations.cs");
        string ledger = Read("Documentation", "Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");

        Assert.Contains("internal readonly struct PhaseCertificateTemplateKey", interCore, StringComparison.Ordinal);
        Assert.Contains("public PhaseCertificateTemplateKey(", interCore, StringComparison.Ordinal);
        Assert.Contains("internal readonly struct PhaseCertificateTemplateKey4Way", source, StringComparison.Ordinal);
        Assert.Contains("public PhaseCertificateTemplateKey4Way(", source, StringComparison.Ordinal);
        Assert.Contains("private static PhaseCertificateTemplateKey CreateInterCoreTemplateKey", source, StringComparison.Ordinal);
        Assert.Contains("private static PhaseCertificateTemplateKey4Way CreateSmtTemplateKey", source, StringComparison.Ordinal);
        Assert.Contains("RF-12.9ca | closed architecture decision", ledger, StringComparison.Ordinal);
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
