using System.Reflection;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1118ArchitecturalCsrAliasResidualCompletionTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [Fact]
    public void ArchitecturalStateContainsFourPodDomainRoutingValues()
    {
        Type architectural = Required("YAKSys_Hybrid_CPU.Core.ArchitecturalState");
        foreach (string name in new[] { "PodId", "PodAffinityMask", "MemoryDomainCertificate", "NocRouteConfiguration" })
            Assert.Equal(typeof(ulong), (architectural.GetField(name, Flags) ?? throw new InvalidOperationException(name)).FieldType);
        Assert.Equal(31, architectural.GetFields(Flags).Length);
        Assert.DoesNotContain(architectural.GetMethods(Flags), method =>
            method.Name is "Commit" or "Rollback" or "Publish" or "Execute");
    }

    [Fact]
    public void FourPublicLegacyFieldsAreRemovedAndForwardByRef()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in new[] { "CsrPodId", "CsrPodAffinityMask", "CsrMemDomainCert", "CsrNocRouteCfg" })
        {
            Assert.Null(core.GetField(name, Flags));
            PropertyInfo property = core.GetProperty(name, Flags) ?? throw new InvalidOperationException(name);
            Assert.True(property.PropertyType.IsByRef);
            Assert.True(property.GetMethod!.IsPublic);
        }
    }

    [Fact]
    public void TransitionalCopiesShareAllFourCsrValues()
    {
#pragma warning disable CS0618
        var core = new Processor.CPU_Core(0);
#pragma warning restore CS0618
        Processor.CPU_Core copy = core;
        copy.CsrPodId = 0x12;
        copy.CsrPodAffinityMask = 0x34;
        copy.CsrMemDomainCert = 0x56;
        copy.CsrNocRouteCfg = 0x78;
        Assert.Equal(0x12UL, core.CsrPodId);
        Assert.Equal(0x34UL, core.CsrPodAffinityMask);
        Assert.Equal(0x56UL, core.CsrMemDomainCert);
        Assert.Equal(0x78UL, core.CsrNocRouteCfg);
    }

    [Fact]
    public void ReadWriteAndRuntimeReadOnlyPodIdSemanticsRemainInPlace()
    {
        string root = FindRoot();
        string registers = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "Registers", "Architectural", "CPU_Core.Registers.cs");
        Assert.Contains("CSR_POD_ID => CsrPodId", registers, StringComparison.Ordinal);
        AssertOrder(registers, "case CSR_POD_ID:", "break;", "case CSR_POD_AFFINITY_MASK:", "CsrPodAffinityMask = value;", "case CSR_MEM_DOMAIN_CERT:", "CsrMemDomainCert = value;", "case CSR_NOC_ROUTE_CFG:", "CsrNocRouteCfg = value;");
        string processor = Read(root, "HybridCPU_ISE", "NonRTL", "Processor", "Core", "Processor.cs");
        AssertOrder(processor, "CPU_Core core = GetCoreRef(globalId);", "core.CsrPodId = podId;", "core.CsrPodAffinityMask = 0xFFFF;");
    }

    [Fact]
    public void DomainFilteringReplayAndCsrPublicationCallersRemainUnchanged()
    {
        string root = FindRoot();
        string memory = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Stages", "Memory", "CPU_Core.PipelineExecution.Memory.cs");
        string faults = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "Faults", "CPU_Core.PipelineExecution.Exceptions.cs");
        string replay = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Frontend", "Decode", "CPU_Core.ReplayDecodeContext.cs");
        string control = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Control", "MicroOp.Control.cs");
        Assert.Contains("(pipeEX.DomainTag & CsrMemDomainCert) != 0", memory, StringComparison.Ordinal);
        Assert.Contains("(domainTag & CsrMemDomainCert) != 0", faults, StringComparison.Ordinal);
        Assert.Contains("pod={CsrPodId:X16};mem-cert={CsrMemDomainCert:X16}", replay, StringComparison.Ordinal);
        Assert.Contains("ResolveStorageSurface(ref Processor.CPU_Core core, ulong addr)", control, StringComparison.Ordinal);
    }

    [Fact]
    public void LedgerAndEvidenceCloseOnlyFourCsrAliases()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11", "rf11.18-architectural-csr-alias-residual-completion.md");
        Assert.Contains("RF-11.18 | closed ArchitecturalState CSR-alias completion", ledger, StringComparison.Ordinal);
        Assert.Contains("exactly four", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RF-11.19", ledger, StringComparison.Ordinal);
    }

    private static Type Required(string name) => typeof(Processor.CPU_Core).Assembly.GetType(name) ?? throw new InvalidOperationException(name);
    private static void AssertOrder(string text, params string[] markers)
    {
        int prior = -1;
        foreach (string marker in markers)
        {
            int current = text.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(current > prior, $"Expected '{marker}' after prior marker.");
            prior = current;
        }
    }
    private static string Read(string root, params string[] parts) => File.ReadAllText(parts.Aggregate(root, Path.Combine));
    private static string FindRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current != null) { if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx"))) return current; current = Directory.GetParent(current)?.FullName; }
        throw new DirectoryNotFoundException();
    }
}
