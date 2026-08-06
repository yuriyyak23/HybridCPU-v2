using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1151ClassEraByRefAbiClosedWorldReauditTests
{
    private const string ExactCoreType = @"(?:Processor\.)?CPU_Core(?!\.)";

    [Fact]
    public void ProductionByRefParameterInventoryIsExactAndClosed()
    {
        string production = Sources(Path.Combine(Root(), "HybridCPU_ISE"));
        Assert.Equal(264, ParameterCount(production, "ref"));
        Assert.Equal(4, ParameterCount(production, "in"));
        Assert.Equal(0, ParameterCount(production, "out"));

        MatchCollection parameters = Regex.Matches(production,
            @"\b(?:ref|in|out)\s+" + ExactCoreType + @"\s+([A-Za-z_]\w*)\s*(?=[,)])");
        Assert.Equal(268, parameters.Count);
        Assert.All(parameters.Cast<Match>(), match => Assert.Equal("core", match.Groups[1].Value));
        Assert.Equal(37, Files(Path.Combine(Root(), "HybridCPU_ISE")).Count(file =>
        {
            string source = File.ReadAllText(file);
            return ParameterCount(source, "ref") + ParameterCount(source, "in") + ParameterCount(source, "out") > 0;
        }));
    }

    [Fact]
    public void LegacyParametersCannotRebindFacadeIdentityTransitively()
    {
        string production = Sources(Path.Combine(Root(), "HybridCPU_ISE"));
        Assert.Empty(Regex.Matches(production, @"(?m)^\s*core\s*=(?!=)"));
        Assert.DoesNotContain("Unsafe.As<", production, StringComparison.Ordinal);
        Assert.DoesNotContain("SetValueDirect", production, StringComparison.Ordinal);
    }

    [Fact]
    public void TableSlotRebindingRemainsOnePrivateLifecycleSeam()
    {
        string identity = Read("HybridCPU_ISE", "NonRTL", "Processor", "Core", "Processor.CoreIdentity.cs");
        Assert.Equal(1, Regex.Matches(identity, @"private\s+static\s+ref\s+CPU_Core\s+GetCoreSlotRef\s*\(").Count);
        Assert.Equal(1, Regex.Matches(identity, @"(?m)^\s*liveCore\s*=\s*replacement;").Count);
        Assert.Contains("public static CPU_Core GetCoreRef(int coreId)", identity, StringComparison.Ordinal);
        Assert.Contains("public static void ReplaceCore(int coreId, CPU_Core replacement)", identity, StringComparison.Ordinal);
    }

    [Fact]
    public void ReflectionSerializationAndTestMutationSeamsAreSeparated()
    {
        string production = Sources(Path.Combine(Root(), "HybridCPU_ISE"));
        string tests = Sources(Path.Combine(Root(), "HybridCPU_ISE.Tests"));
        Assert.DoesNotContain("JsonSerializer.Deserialize<Processor.CPU_Core", production, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Serialize(core", production, StringComparison.Ordinal);
        Assert.DoesNotContain("SetValueDirect(__makeref(core), value);", production, StringComparison.Ordinal);
        Assert.Contains("field.SetValue(core.Runtime.Scratch, value);", tests, StringComparison.Ordinal);
        Assert.Contains("TEST-ONLY REFLECTION MUTATION ADAPTER", tests, StringComparison.Ordinal);
    }

    [Fact]
    public void TestByRefParametersAreFrozenAsNonProductionAdapters()
    {
        string tests = Sources(Path.Combine(Root(), "HybridCPU_ISE.Tests"));
        Assert.Equal(117, ParameterCount(tests, "ref"));
        Assert.Equal(0, ParameterCount(tests, "in"));
        Assert.Equal(0, ParameterCount(tests, "out"));
    }

    [Fact]
    public void EvidenceNamesOnlyExitAuditNext()
    {
        string evidence = Read("Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11", "rf11.51-class-era-by-ref-abi-closed-world-reaudit.md");
        string ledger = Read("Documentation", "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        Assert.Contains("not an identity-replacement path", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RF-11.52", ledger, StringComparison.Ordinal);
    }

    private static int ParameterCount(string source, string modifier) => Regex.Matches(source,
        @"\b" + modifier + @"\s+" + ExactCoreType + @"\s+[A-Za-z_]\w*\s*(?=[,)])").Count;
    private static IEnumerable<string> Files(string path) => Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
        .Where(file => !file.Contains("\\bin\\") && !file.Contains("\\obj\\"));
    private static string Sources(string path) => string.Join('\n', Files(path).Select(File.ReadAllText));
    private static string Read(params string[] parts) => File.ReadAllText(parts.Aggregate(Root(), Path.Combine));
    private static string Root()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx"))) return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException();
    }
}
