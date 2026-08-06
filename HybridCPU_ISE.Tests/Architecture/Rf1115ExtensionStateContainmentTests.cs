using System.Reflection;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1115ExtensionStateContainmentTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [Fact]
    public void RuntimeContainsExactNamespaceOnlyExtensionReferences()
    {
        Type runtime = Required("YAKSys_Hybrid_CPU.Core.CoreRuntimeState");
        Type extensions = Required("YAKSys_Hybrid_CPU.Core.ExtensionState");
        Assert.Single(runtime.GetFields(Flags), field => field.FieldType == extensions);
        Assert.Equal(new[] { "DmaStreamComputeTokenStore", "ExternalAcceleratorRuntime", "MatrixTile", "MatrixTileRegisterFile", "MatrixTileReplayJournals", "MatrixTileStreamRegisterFile" },
            extensions.GetFields(Flags).Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(extensions.GetFields(Flags), field => Assert.False(field.FieldType.IsValueType));
        Assert.Empty(extensions.GetMethods(Flags).Where(method =>
            method.Name is "Execute" or "Commit" or "Fallback" or "Rollback" or "Publish"));
    }

    [Fact]
    public void FiveLegacyReferencesAreRemovedAndForwardByRef()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in new[] { "_matrixTileRegisterFile", "_matrixTileStreamRegisterFile", "_matrixTileReplayJournals", "_dmaStreamComputeTokenStore", "_externalAcceleratorRuntime" })
        {
            Assert.Null(core.GetField(name, Flags));
            Assert.True((core.GetProperty(name, Flags) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        }
    }

    [Fact]
    public void FacadeCopiesAliasExtensionContainerAndDistinctOwnerReferences()
    {
#pragma warning disable CS0618
        var core = new Processor.CPU_Core(0);
#pragma warning restore CS0618
        Processor.CPU_Core copy = core;
        Assert.Same(core.Runtime.Extensions, copy.Runtime.Extensions);
        Assert.Same(core.GetMatrixTileRegisterFile(), copy.GetMatrixTileRegisterFile());
        Assert.Same(core.GetMatrixTileStreamRegisterFile(), copy.GetMatrixTileStreamRegisterFile());
        Assert.Same(core.GetDmaStreamComputeTokenStore(), copy.GetDmaStreamComputeTokenStore());
        Assert.Same(core.GetExternalAcceleratorRuntime(), copy.GetExternalAcceleratorRuntime());
    }

    [Fact]
    public void MatrixDscAndExternalOwnersKeepSeparateCallSurfaces()
    {
        string root = FindRoot();
        string matrix = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "State", "Architectural", "CPU_Core.MatrixTileState.cs");
        string matrixRetire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence", "CPU_Core.MatrixTileRetireState.cs");
        string state = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "State", "Architectural", "CPU_Core.StateData.cs");
        Assert.Contains("GetMatrixTileRegisterFile()", matrix, StringComparison.Ordinal);
        Assert.Contains("RegisterMatrixTileReplayJournal(", matrixRetire, StringComparison.Ordinal);
        Assert.Contains("GetDmaStreamComputeTokenStore()", state, StringComparison.Ordinal);
        Assert.Contains("GetExternalAcceleratorRuntime()", state, StringComparison.Ordinal);
        Assert.DoesNotContain("ExtensionState.Execute", matrix + matrixRetire + state, StringComparison.Ordinal);
        Assert.DoesNotContain("ExtensionState.Commit", matrix + matrixRetire + state, StringComparison.Ordinal);
        Assert.DoesNotContain("ExtensionState.Fallback", matrix + matrixRetire + state, StringComparison.Ordinal);
    }

    [Fact]
    public void MatrixScalarLeafIsNestedWhileVmxVirtualizationAndSecureContoursRemainOutside()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in new[] { "_matrixTileStreamInvalidationCount", "_nextMatrixTileCaptureOrdinal", "_nextMatrixTileReplayCheckpointOrdinal", "_matrixTileReplayInvalidationEpoch" })
        {
            Assert.Null(core.GetField(name, Flags));
            Assert.True((core.GetProperty(name, Flags) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        }
        foreach (string name in new[] { "IsVMXRoot", "VirtualThreadPipelineStates", "_vmxExecutionPlaneWired" })
            Assert.True((core.GetProperty(name, Flags) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);

        Type extensions = Required("YAKSys_Hybrid_CPU.Core.ExtensionState");
        Assert.Single(extensions.GetFields(Flags), field => field.FieldType.Name == "MatrixTileState");
        Assert.DoesNotContain(extensions.GetFields(Flags), field =>
            field.Name.Contains("Vmx", StringComparison.OrdinalIgnoreCase) ||
            field.Name.Contains("Secure", StringComparison.OrdinalIgnoreCase) ||
            field.Name.Contains("Virtualization", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LedgerAndEvidenceCloseOnlyExtensionReferenceContainment()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11", "rf11.15-extension-state-containment-references.md");
        Assert.Contains("RF-11.15 | closed ExtensionState references", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-11.16 residual", ledger, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no common accelerator", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no per-core SecureCompute", evidence, StringComparison.OrdinalIgnoreCase);
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
