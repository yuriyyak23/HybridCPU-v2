using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1135CsrEffectRefThisHardeningTests
{
    [Fact]
    public void CsrEffectUsesOneStableIdentityAdapterAndNoDirectSelfPassing()
    {
        string retire = Retire();
        Assert.Single(Regex.Matches(retire, @"MaterializeCsrEffectWithStableCoreIdentity\(csrMicroOp\)"));
        Assert.Single(Regex.Matches(retire, @"csrMicroOp\.CreateRetireEffect\(ref stableCoreIdentity\)"));
        Assert.DoesNotContain("csrMicroOp.CreateRetireEffect(ref this)", retire, StringComparison.Ordinal);
    }

    [Fact]
    public void CsrSurfaceReadEffectAndPublicationTopologyRemainFrozen()
    {
        string root = FindRoot();
        string csr = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Control", "MicroOp.Control.cs");
        AssertOrder(csr, "ResolveStorageSurface(ref core, CSRAddress);", "ReadCsr(ref core, storageSurface, CSRAddress);",
            "ResolveWriteValue(ref core, priorValue)", "return CsrRetireEffect.Create(");
        Assert.Equal(0, Regex.Matches(csr, @"\bcore\s*=(?!=)").Count);
        string retire = Retire();
        AssertOrder(retire, "retireBatch.CaptureGeneratedCsrEffect(",
            "PrevalidateRetireWindowBatchForPublication(", "ApplyRetiredCsrEffect(retireEffect.CsrEffect);");
    }

    [Fact]
    public void ResidualSelfPassingIsOneAtomicTestSupportCall()
    {
        string root = FindRoot();
        string production = ReadSources(Path.Combine(root, "HybridCPU_ISE"));
        Assert.Empty(Regex.Matches(Retire(), @"\bref\s+this\s*[,\)]"));
        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Core", "CPU_Core.TestSupport.cs");
        Assert.Empty(Regex.Matches(testSupport, @"\bref\s+this\s*[,\)]"));
        Assert.Empty(Regex.Matches(production, @"\bref\s+this\s*[,\)]"));
    }

    [Fact]
    public void EvidenceClosesOnlyCsrEffectMaterialization()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11", "rf11.35-csr-effect-ref-this-hardening.md");
        Assert.Contains("RF-11.35 CSR-effect materialization ref-this seam hardening", ledger, StringComparison.Ordinal);
        Assert.Contains("no production state declaration", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--minimal-logs", evidence, StringComparison.Ordinal);
        Assert.Contains("RF-11.36", ledger, StringComparison.Ordinal);
    }

    private static string Retire() => Read(FindRoot(), "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
    private static void AssertOrder(string text, params string[] markers) { int prior = -1; foreach (string marker in markers) { int current = text.IndexOf(marker, prior + 1, StringComparison.Ordinal); Assert.True(current > prior, marker); prior = current; } }
    private static string ReadSources(string path) => string.Join('\n', Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)).Select(File.ReadAllText));
    private static string Read(string root, params string[] parts) => File.ReadAllText(parts.Aggregate(root, Path.Combine));
    private static string FindRoot() { string? current = AppContext.BaseDirectory; while (current != null) { if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx"))) return current; current = Directory.GetParent(current)?.FullName; } throw new DirectoryNotFoundException(); }
}
