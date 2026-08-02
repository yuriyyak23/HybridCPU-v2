using System.Reflection;
using System.Text.Json;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf11PostClosureCodeAndDocumentationReconciliationTests
{
    private const BindingFlags InstanceFields =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Fact]
    public void ClosedFacadeAndContainmentRootStillHaveOneLiveIdentity()
    {
        Type core = typeof(Processor.CPU_Core);
        Assert.True(core.IsClass);
        Assert.True(core.IsSealed);

        FieldInfo runtime = Assert.Single(core.GetFields(InstanceFields));
        Assert.Equal("_runtime", runtime.Name);
        Assert.True(runtime.IsInitOnly);

        Type root = runtime.FieldType;
        PropertyInfo[] domains = root
            .GetProperties(InstanceFields)
            .Where(property => property.DeclaringType == root)
            .ToArray();
        Assert.Equal(19, domains.Length);

        string[] forbiddenUniversalAuthorities =
        [
            "Commit", "Publish", "Rollback", "Execute", "Fallback",
            "Checkpoint", "Migrate"
        ];
        Assert.DoesNotContain(
            root.GetMethods(InstanceFields),
            method => forbiddenUniversalAuthorities.Contains(method.Name, StringComparer.Ordinal));
    }

    [Fact]
    public void FrozenReplaySidebandRoundTripsNonZeroVirtualThreadIdentity()
    {
        VtId expected = new(3);
        string json = JsonSerializer.Serialize(expected);
        VtId actual = JsonSerializer.Deserialize<VtId>(json);

        Assert.Equal(expected, actual);
        Assert.Equal(3, actual.Value);
    }

    [Fact]
    public void ReconciledRuntimeSeamsRemainFailClosedAndOwnerSpecific()
    {
        string root = FindRepositoryRoot();
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        string microOp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "MicroOps", "Types", "MicroOp.cs");
        string registerIdentity = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Architecture", "Registers", "Architectural", "RegisterIdentity.cs");

        Assert.Contains("effect.ClearsArchitecturalExceptionState ||", retire, StringComparison.Ordinal);
        Assert.Contains("effect.HasCsrWrite ||", retire, StringComparison.Ordinal);
        Assert.Contains("effect.HasRegisterWriteback", retire, StringComparison.Ordinal);
        Assert.Contains("if (core is null ||", microOp, StringComparison.Ordinal);
        Assert.Contains("[System.Text.Json.Serialization.JsonConstructor]", registerIdentity, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentRf11DocumentsContainNoTransitionalFacadeClaims()
    {
        string root = FindRepositoryRoot();
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "10_RF11",
            "00_CURRENT_STATUS_AND_LEDGER.md");
        string currentIndexes = string.Join('\n',
            Read(root, "Documentation", "ArchitectureAuthorityRefactor", "00_Overview", "00_README.md"),
            Read(root, "Documentation", "ArchitectureAuthorityRefactor", "05_Governance",
                "05_Invariants_Dependency_Risks_DoD.md"),
            Read(root, "Documentation", "ArchitectureAuthorityRefactor", "08_RF08_RF09",
                "00_CURRENT_STATUS_AND_READING_ORDER.md"),
            Read(root, "Documentation", "ArchitectureAuthorityRefactor", "09_RF10",
                "00_CURRENT_STATUS_AND_LEDGER.md"),
            Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "README.md"));

        Assert.Contains("RF-11 overall | closed", ledger, StringComparison.Ordinal);
        Assert.Contains("rf11-post-closure-code-and-documentation-reconciliation.md", ledger,
            StringComparison.Ordinal);
        Assert.DoesNotContain("is still a mutable value type", ledger,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CPU_Core remains a struct", ledger,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("conversion is not yet behavior-preserving", ledger,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("66 partial struct declarations", ledger,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RF-11 is not opened", currentIndexes,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HistoricalPromptAndNewEvidenceCannotMasqueradeAsCurrentAuthority()
    {
        string root = FindRepositoryRoot();
        string prompt = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "09_RF10",
            "01_RF11_CONTINUATION_PROMPT.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11-post-closure-code-and-documentation-reconciliation.md");

        Assert.StartsWith("# Superseded RF-11 entry prompt", prompt, StringComparison.Ordinal);
        Assert.Contains("does not create RF-11.53", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("historical per-slice evidence was not rewritten", evidence,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no full statistics", evidence, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("HybridCPU repository root was not found.");
    }
}
