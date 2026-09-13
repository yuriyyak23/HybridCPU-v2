namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1210BridgeInventoryTests
{
    [Fact]
    public void RetainedBridgesAreFamilySpecificAndInternalTemplateKeysHaveNone()
    {
        string guard = Read("HybridCPU_ISE.Tests", "Architecture", "Rf120ResourceIdIngressGuardTests.cs");
        string replay = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Replay", "ReplayToken.cs");
        string key = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Certificates", "ReplayPhaseSubstrate.cs");
        string ledger = Read("Documentation", "Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");

        Assert.Contains("JsonSerializer.Deserialize<VtId>(json)", guard, StringComparison.Ordinal);
        Assert.Contains("JsonSerializer.Deserialize<ReplayToken>(json)", replay, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer", key, StringComparison.Ordinal);
        Assert.DoesNotContain("BinaryWriter", key, StringComparison.Ordinal);
        Assert.Contains("RF-12.10 | closed inventory/freeze", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.11 | closed retention audit", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12 overall | closed", ledger, StringComparison.Ordinal);
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
