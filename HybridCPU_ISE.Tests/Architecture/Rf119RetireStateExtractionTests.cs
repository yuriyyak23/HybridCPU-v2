using System.Reflection;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf119RetireStateExtractionTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [Fact]
    public void RuntimeContainsExactRetireAuthorityAndCertificates()
    {
        Type runtime = Required("YAKSys_Hybrid_CPU.Core.CoreRuntimeState");
        Type retire = Required("YAKSys_Hybrid_CPU.Core.RetireState");
        Assert.Single(runtime.GetFields(Flags), field => field.FieldType == retire);
        Assert.Equal(new[] { "Coordinator", "DecodePublicationCertificate", "ExecuteCompletionCertificate", "RetireVisibilityCertificate" },
            retire.GetFields(Flags).Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void LegacyFieldsAreRemovedAndForwardersAreByRef()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in new[] { "RetireCoordinator", "decodePublicationCertificate", "executeCompletionCertificate", "retireVisibilityCertificate" })
        {
            Assert.Null(core.GetField(name, Flags));
            Assert.True((core.GetProperty(name, Flags) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        }
    }

    [Fact]
    public void CopiesAliasTheSameFunctionalCoordinator()
    {
#pragma warning disable CS0618
        var core = new Processor.CPU_Core(0);
#pragma warning restore CS0618
        Processor.CPU_Core copy = core;
        Assert.Same(core.RetireCoordinator, copy.RetireCoordinator);
        Assert.Same(core.Runtime.Retire, copy.Runtime.Retire);
    }

    [Fact]
    public void SelectedPrefixPrevalidationAndPublicationCallsRemain()
    {
        string source = Read(FindRoot(), "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        Assert.Contains("RetireCoordinator.Prevalidate(retireBatch.RetireRecords);", source, StringComparison.Ordinal);
        Assert.Contains("RetireCoordinator.Retire(retireBatch.RetireRecords);", source, StringComparison.Ordinal);
        Assert.Contains("retireVisibilityCertificate", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WbBackendReplayAndExtensionJournalsRemainOutsideRetireState()
    {
        Type core = typeof(Processor.CPU_Core);
        Assert.True((core.GetProperty("pipeWB", Flags) ?? throw new InvalidOperationException("pipeWB")).PropertyType.IsByRef);
        foreach (string name in new[] { "PhysicalRegisters", "ArchRenameMap", "ArchCommitMap", "PhysRegFreeList" })
            Assert.True((core.GetProperty(name, Flags) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        Assert.True((core.GetProperty("_matrixTileRegisterFile", Flags) ?? throw new InvalidOperationException()).PropertyType.IsByRef);
        Assert.True((core.GetProperty("ArchContexts", Flags) ?? throw new InvalidOperationException("ArchContexts")).PropertyType.IsByRef);
        string[] names = Required("YAKSys_Hybrid_CPU.Core.RetireState").GetFields(Flags).Select(f => f.Name).ToArray();
        Assert.DoesNotContain(names, n => n.Contains("Journal", StringComparison.Ordinal));
        Assert.DoesNotContain(names, n => n.Contains("Queue", StringComparison.Ordinal));
    }

    [Fact]
    public void LedgerAndEvidenceCloseOnlyRetireState()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11", "rf11.9-retire-state-extraction.md");
        Assert.Contains("RF-11.9 | closed RetireState", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-11.10 ArchitecturalState", ledger, StringComparison.Ordinal);
        Assert.Contains("functional authority", evidence, StringComparison.OrdinalIgnoreCase);
    }

    private static Type Required(string name) => typeof(Processor.CPU_Core).Assembly.GetType(name) ?? throw new InvalidOperationException(name);
    private static string Read(string root, params string[] parts) => File.ReadAllText(parts.Aggregate(root, Path.Combine));
    private static string FindRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current != null) { if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx"))) return current; current = Directory.GetParent(current)?.FullName; }
        throw new DirectoryNotFoundException();
    }
}
