using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1140TestSupportAtomicRefThisHardeningTests
{
    [Fact]
    public void AtomicTestHelperUsesStableIdentityAdapter()
    {
        string support = Support();
        Assert.Equal(2, Regex.Matches(support,
            @"ExecuteTestAtomicWithStableCoreIdentity\(").Count);
        Assert.Single(Regex.Matches(support,
            @"atomicMicroOp\.Execute\(ref stableCoreIdentity\)"));
        Assert.DoesNotContain("atomicMicroOp.Execute(ref this)", support);
    }

    [Fact]
    public void AtomicResolveAndRetireCarrierRemainFrozen()
    {
        string root = Root();
        string atomic = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "MicroOps", "Types", "MicroOp.Misc.cs");
        Order(atomic, "ReadUnifiedScalarSourceOperand(ref core, vtId, BaseRegID);",
            "core.AtomicMemoryUnit.ResolveRetireEffect(", "return true;",
            "public AtomicRetireEffect CreateRetireEffect() => _resolvedRetireEffect;");
        Assert.Equal(0, Regex.Matches(atomic, @"\bcore\s*=(?!=)").Count);

        string support = Support();
        Order(support, "ExecuteTestAtomicWithStableCoreIdentity(atomicMicroOp)",
            "AtomicRetireEffect atomicEffect = atomicMicroOp.CreateRetireEffect();",
            "lane.GeneratedAtomicEffect = atomicEffect;");
    }

    [Fact]
    public void ProductionHasNoSelfPassingFacadeCalls()
    {
        string production = Sources(Path.Combine(Root(), "HybridCPU_ISE"));
        Assert.Empty(Regex.Matches(production, @"\bref\s+this\s*[,\)]"));
        Assert.Empty(Regex.Matches(production, @"\bref\s+this\."));
    }

    [Fact]
    public void EvidenceClosesOnlyAtomicTestSupportSeam()
    {
        string root = Root();
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "10_RF11",
            "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.40-testsupport-atomic-ref-this-hardening.md");
        Assert.Contains("RF-11.40 TestSupport Atomic ref-this seam hardening", ledger);
        Assert.Contains("changes no production runtime path", evidence,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TestAssemblerConsoleApps was not run", evidence);
        Assert.Contains("RF-11.41", ledger);
    }

    private static string Support() => Read(Root(), "HybridCPU_ISE", "CloseToHSL", "Core",
        "Pipeline", "Core", "CPU_Core.TestSupport.cs");
    private static void Order(string text, params string[] markers)
    {
        int prior = -1;
        foreach (string marker in markers)
        {
            int current = text.IndexOf(marker, prior + 1, StringComparison.Ordinal);
            Assert.True(current > prior, marker);
            prior = current;
        }
    }
    private static string Sources(string path) => string.Join('\n',
        Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains("\\bin\\") && !file.Contains("\\obj\\"))
            .Select(File.ReadAllText));
    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(parts.Aggregate(root, Path.Combine));
    private static string Root()
    {
        string? current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx"))) return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException();
    }
}
