using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1139TestSupportStreamEngineRefThisHardeningTests
{
    [Fact]
    public void ThreeTestOnlyStreamCallsUseStableIdentityAdapters()
    {
        string support = TestSupport();
        Assert.Equal(4, Regex.Matches(support,
            @"CaptureTestStreamPublicationsWithStableCoreIdentity\(").Count);
        Assert.Equal(2, Regex.Matches(support,
            @"ExecuteTestStreamRequestWithStableCoreIdentity\(").Count);
        Assert.Equal(2, Regex.Matches(support,
            @"StreamEngine\.CaptureRetireWindowPublications\(\s*ref stableCoreIdentity,").Count);
        Assert.Single(Regex.Matches(support,
            @"StreamEngine\.Execute\(\s*ref stableCoreIdentity,"));
        Assert.DoesNotContain("StreamEngine.CaptureRetireWindowPublications(\n                        ref this", support);
        Assert.DoesNotContain("StreamEngine.Execute(\n                    ref this", support);
    }

    [Fact]
    public void DirectTestPublicationAndExecutionOrderingRemainFrozen()
    {
        string support = TestSupport();
        Order(support, "CaptureTestStreamPublicationsWithStableCoreIdentity(\n                        in request,",
            "ApplyCapturedRetireWindowBatch(");
        Assert.Contains("if (request.RequiresMemoryVisibleCarrier)", support);
        Assert.Contains("without an authoritative retire/apply contour", support);
        Assert.Contains("ExecuteTestStreamRequestWithStableCoreIdentity(\n                    in request,", support);

        string stream = Read(Root(), "HybridCPU_ISE", "CloseToHSL", "Core", "Execution",
            "StreamEngine", "Modes", "StreamEngine.cs");
        Assert.Equal(0, Regex.Matches(stream, @"\bcore\s*=(?!=)").Count);
        Order(stream, "CaptureRetireWindowPublicationsCore(",
            "ResolveExecutionVtId(ref core, ownerThreadId)");
    }

    [Fact]
    public void NoProductionSelfPassingRemains()
    {
        string root = Root();
        string production = Sources(Path.Combine(root, "HybridCPU_ISE"));
        string support = TestSupport();
        Assert.Empty(Regex.Matches(support, @"\bref\s+this\s*[,\)]"));
        Assert.Empty(Regex.Matches(support, @"atomicMicroOp\.Execute\(ref this\)"));
        Assert.Empty(Regex.Matches(production, @"\bref\s+this\s*[,\)]"));
    }

    [Fact]
    public void EvidenceClosesOnlyTestSupportStreamFamily()
    {
        string root = Root();
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "10_RF11",
            "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.39-testsupport-streamengine-ref-this-hardening.md");
        Assert.Contains("RF-11.39 TestSupport StreamEngine ref-this seam hardening", ledger);
        Assert.Contains("changes no production runtime path", evidence,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TestAssemblerConsoleApps was not run", evidence);
        Assert.Contains("RF-11.40", ledger);
    }

    private static string TestSupport() => Read(Root(), "HybridCPU_ISE", "CloseToHSL", "Core",
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
