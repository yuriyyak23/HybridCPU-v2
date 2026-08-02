using System.Reflection;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1110ArchitecturalStateExtractionTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [Fact]
    public void RuntimeContainsExactCommittedArchitecturalContour()
    {
        Type runtime = Required("YAKSys_Hybrid_CPU.Core.CoreRuntimeState");
        Type architectural = Required("YAKSys_Hybrid_CPU.Core.ArchitecturalState");
        Assert.Single(runtime.GetFields(Flags), field => field.FieldType == architectural);
        Assert.Equal(new[]
        {
            "CallContextStack", "Contexts", "CoreFlags", "Csr", "FlagsContextStack",
            "FloatingPointContexts", "InterruptContextStack", "MemoryDomainCertificate",
            "NocRouteConfiguration", "PodAffinityMask", "PodId", "PredicateRegister0",
            "PredicateRegister1", "PredicateRegister10", "PredicateRegister11",
            "PredicateRegister12", "PredicateRegister13", "PredicateRegister14",
            "PredicateRegister15", "PredicateRegister2", "PredicateRegister3",
            "PredicateRegister4", "PredicateRegister5", "PredicateRegister6",
            "PredicateRegister7", "PredicateRegister8", "PredicateRegister9",
            "SavedVectorContext", "Stack", "VectorConfig", "VectorExceptionStatus"
        }, architectural.GetFields(Flags).Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Empty(architectural.GetMethods(Flags).Where(method =>
            method.Name is "Commit" or "Rollback" or "Publish" or "Execute"));
    }

    [Fact]
    public void LegacyStorageIsRemovedAndCompatibilitySurfacesAreByRef()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in new[]
        {
            "ArchContexts", "Csr", "ThreadFPContexts", "CoreFlagsRegister",
            "Core_FlagsRegisters_Stack", "Call_Callback_Addresses",
            "Interrupt_Callback_Addresses", "Stack", "VectorConfig",
            "ExceptionStatus", "SavedVectorContext", "predReg0", "predReg15",
            "CsrPodId", "CsrPodAffinityMask", "CsrMemDomainCert", "CsrNocRouteCfg"
        })
        {
            Assert.Null(core.GetField(name, Flags));
            Assert.True((core.GetProperty(name, Flags) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        }
    }

    [Fact]
    public void TransitionalFacadeCopiesAliasOneArchitecturalOwner()
    {
#pragma warning disable CS0618
        var core = new Processor.CPU_Core(0);
#pragma warning restore CS0618
        Processor.CPU_Core copy = core;
        copy.VectorConfig.VL = 13;
        copy.SetPredicateRegister(3, 0x55UL);
        copy.Call_Callback_Addresses.Add(0x1234UL);

        Assert.Same(core.Runtime.Architectural, copy.Runtime.Architectural);
        Assert.Same(core.ArchContexts, copy.ArchContexts);
        Assert.Same(core.Csr, copy.Csr);
        Assert.Equal(13UL, core.VectorConfig.VL);
        Assert.Equal(0x55UL, core.GetPredicateRegister(3));
        Assert.Equal(0x1234UL, Assert.Single(core.Call_Callback_Addresses));
    }

    [Fact]
    public void ExistingInitializationResetAndPublicationSitesRemainOrdered()
    {
        string root = FindRoot();
        string stateData = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "State", "Architectural", "CPU_Core.StateData.cs");
        string vector = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "State", "Architectural", "CPU_Core.State.cs");
        AssertOrder(stateData, "this._runtime = new CoreRuntimeState();", "this.CoreFlagsRegister = new FlagsRegister", "InitializeVectorState();", "this.ThreadFPContexts =", "this.ArchContexts =", "this.RetireCoordinator = new RetireCoordinator");
        Assert.Contains("ArchContexts[vtId].CommittedRegs[archReg] = value;", stateData, StringComparison.Ordinal);
        Assert.Contains("ArchContexts[vtId].CommittedPc = pc;", stateData, StringComparison.Ordinal);
        Assert.Contains("RetireCoordinator.Retire(RetireRecord.RegisterWrite", stateData, StringComparison.Ordinal);
        Assert.Contains("VectorConfig.Reset();", vector, StringComparison.Ordinal);
        Assert.Contains("ExceptionStatus.Reset();", vector, StringComparison.Ordinal);
    }

    [Fact]
    public void BackendFrontendVmxPipelineAndExtensionsRemainOutsideArchitecturalState()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in new[] { "PhysicalRegisters", "ArchRenameMap", "ArchCommitMap", "PhysRegFreeList" })
            Assert.True((core.GetProperty(name, Flags) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        Assert.True((core.GetProperty("ulong_InstructionPointer", Flags) ?? throw new InvalidOperationException("ulong_InstructionPointer")).PropertyType.IsByRef);
        foreach (string name in new[] { "IsVMXRoot", "VirtualThreadPipelineStates" })
            Assert.True((core.GetProperty(name, Flags) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        foreach (string name in new[] { "_matrixTileRegisterFile", "_dmaStreamComputeTokenStore", "_externalAcceleratorRuntime" })
            Assert.True((core.GetProperty(name, Flags) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        Assert.True((core.GetProperty("pipeEX", Flags) ?? throw new InvalidOperationException("pipeEX")).PropertyType.IsByRef);
        Assert.True((core.GetProperty("pipeMEM", Flags) ?? throw new InvalidOperationException("pipeMEM")).PropertyType.IsByRef);
        Assert.True((core.GetProperty("pipeWB", Flags) ?? throw new InvalidOperationException("pipeWB")).PropertyType.IsByRef);
    }

    [Fact]
    public void LedgerAndEvidenceCloseOnlyArchitecturalState()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11", "rf11.10-architectural-state-extraction.md");
        Assert.Contains("RF-11.10 | closed ArchitecturalState", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-11.11 SchedulingState", ledger, StringComparison.Ordinal);
        Assert.Contains("27", evidence, StringComparison.Ordinal);
        Assert.Contains("publication authority", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("live frontend PC", evidence, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertOrder(string text, params string[] markers)
    {
        int previous = -1;
        foreach (string marker in markers)
        {
            int current = text.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(current > previous, $"Expected '{marker}' after prior marker.");
            previous = current;
        }
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
