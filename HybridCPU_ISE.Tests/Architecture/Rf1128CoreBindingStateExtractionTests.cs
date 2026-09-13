using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1128CoreBindingStateExtractionTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    private static readonly string[] Names =
        ["CoreID", "_platformContext", "_executionMode", "_interruptDispatcher"];

    [Fact]
    public void RuntimeContainsExactStorageOnlyCoreBindingState()
    {
        Type runtime = Required("YAKSys_Hybrid_CPU.Core.CoreRuntimeState");
        Type binding = Required("YAKSys_Hybrid_CPU.Core.CoreBindingState");
        Assert.Single(runtime.GetFields(Flags), field => field.FieldType == binding);
        Assert.Equal(
            new[] { "CoreId", "PlatformContext", "ExecutionMode", "InterruptDispatcher" },
            binding.GetFields(Flags).Select(field => field.Name));
        Assert.Equal(typeof(uint), binding.GetField("CoreId", Flags)?.FieldType);
        Assert.Equal(typeof(CpuCorePlatformContext), binding.GetField("PlatformContext", Flags)?.FieldType);
        Assert.Equal(typeof(ProcessorMode), binding.GetField("ExecutionMode", Flags)?.FieldType);
        Assert.Equal(
            typeof(Func<Processor.DeviceType, ushort, ulong, byte>),
            binding.GetField("InterruptDispatcher", Flags)?.FieldType);
        Assert.DoesNotContain(binding.GetMethods(Flags), method => method.Name is
            "AdvanceCycle" or "Execute" or "Commit" or "Rollback" or "Publish" or
            "Replace" or "Migrate");
    }

    [Fact]
    public void DirectFieldsAreRemovedAndCompatibilityNamesForwardByRef()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in Names)
        {
            Assert.Null(core.GetField(name, Flags));
            Assert.True((core.GetProperty(name, Flags) ??
                throw new InvalidOperationException(name)).PropertyType.IsByRef);
        }
    }

    [Fact]
    public void ConstructionAndTransitionalCopiesPreserveOneBindingIdentity()
    {
        var memory = new Processor.MainMemoryArea();
        CpuCorePlatformContext context = CpuCorePlatformContext.CreateFixed(memory, ProcessorMode.Compiler);
        var core = new Processor.CPU_Core(37, context);
        Processor.CPU_Core copy = core;

        Assert.Same(core.Runtime.Binding, copy.Runtime.Binding);
        Assert.Equal(37U, core.CoreID);
        Assert.Equal(context, core.Runtime.Binding.PlatformContext);
        Assert.Equal(ProcessorMode.Compiler, core.Runtime.Binding.ExecutionMode);
        Assert.NotNull(core.Runtime.Binding.InterruptDispatcher);

        copy.CoreID = 38;
        Assert.Equal(38U, core.CoreID);
        core.SynchronizeExecutionMode();
        Assert.Equal(ProcessorMode.Compiler, copy.Runtime.Binding.ExecutionMode);
    }

    [Fact]
    public void InterruptDispatcherRetainsCoreIdentityAndTestSupportLifecycle()
    {
        var memory = new Processor.MainMemoryArea();
        var core = new Processor.CPU_Core(
            19,
            CpuCorePlatformContext.CreateFixed(memory, ProcessorMode.Emulation));
        ulong observedCoreId = ulong.MaxValue;
        core.TestSetInterruptDispatcher((_, _, coreId) =>
        {
            observedCoreId = coreId;
            return 7;
        });

        try
        {
            Assert.Equal((byte)7, core.DispatchInterrupt(default, 11));
            Assert.Equal(19UL, observedCoreId);
        }
        finally
        {
            core.TestResetInterruptDispatcher();
        }

        Assert.NotNull(core.Runtime.Binding.InterruptDispatcher);
    }

    [Fact]
    public void LifecycleAndFrozenCycleCallSitesRemainInPlace()
    {
        string root = FindRoot();
        string state = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "State",
            "Architectural", "CPU_Core.StateData.cs");
        AssertOrder(state, "this._runtime = new CoreRuntimeState();", "this.CoreID = CoreID;",
            "this._platformContext = platformContext;", "this._executionMode = platformContext.ResolveExecutionMode();",
            "this._interruptDispatcher = DefaultInterruptDispatcher;");

        string mode = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "State",
            "Architectural", "CPU_Core.ExecutionMode.cs");
        Assert.Contains("internal void SynchronizeExecutionMode()", mode, StringComparison.Ordinal);
        AssertOrder(mode, "if (_platformContext.IsConfigured)",
            "_executionMode = _platformContext.ResolveExecutionMode();", "_executionMode = Processor.CurrentProcessorMode;");

        string interrupt = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture",
            "ExceptionsAndTraps", "CPU_Core.InterruptDispatch.cs");
        AssertOrder(interrupt, "_interruptDispatcher ?? DefaultInterruptDispatcher;",
            "return interruptDispatcher(deviceType, interruptId, CoreID);");

        string identity = Read(root, "HybridCPU_ISE", "NonRTL", "Processor", "Core", "Processor.CoreIdentity.cs");
        Assert.Contains("public static CPU_Core GetCoreRef", identity, StringComparison.Ordinal);
        Assert.Contains("public static void ReplaceCore", identity, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplaceCore(", ReadSources(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL")), StringComparison.Ordinal);
    }

    [Fact]
    public void CopyReflectionSerializationAndCrossStageSeamsRemainExplicit()
    {
        string root = FindRoot();
        string production = ReadSources(Path.Combine(root, "HybridCPU_ISE"));
        string tests = string.Join('\n', SourceFiles(Path.Combine(root, "HybridCPU_ISE.Tests"))
            .Where(file => !file.EndsWith(nameof(Rf1128CoreBindingStateExtractionTests) + ".cs", StringComparison.Ordinal))
            .Where(file => !file.EndsWith("Rf1116ResidualOwnerAndFacadeReadinessTests.cs", StringComparison.Ordinal))
            .Select(File.ReadAllText));

        Assert.DoesNotContain("SetValueDirect", production, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Serialize(core.Runtime.Binding", production, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Deserialize<CoreBindingState", production, StringComparison.Ordinal);
        Assert.Empty(SourceFiles(Path.Combine(root, "HybridCPU_ISE.Tests")).Where(file =>
        {
            string text = File.ReadAllText(file);
            return Names.Any(name => Regex.IsMatch(text, $@"\b{Regex.Escape(name)}\b")) &&
                text.Contains("SetValueDirect", StringComparison.Ordinal) &&
                !file.EndsWith(nameof(Rf1128CoreBindingStateExtractionTests) + ".cs", StringComparison.Ordinal) &&
                !file.EndsWith("Rf110StateOwnerEntryInventoryFreezeTests.cs", StringComparison.Ordinal) &&
                !file.EndsWith("Rf1116ResidualOwnerAndFacadeReadinessTests.cs", StringComparison.Ordinal);
        }));
        string directCoreTableIndex = "CPU_" + "Cores[";
        Assert.DoesNotContain(directCoreTableIndex, production, StringComparison.Ordinal);
        Assert.Contains(directCoreTableIndex, tests, StringComparison.Ordinal);

        string identity = Read(root, "HybridCPU_ISE", "NonRTL", "Processor", "Core", "Processor.CoreIdentity.cs");
        Assert.Contains("CpuCoreDiagnosticSnapshot GetCoreSnapshot(int coreId)", identity, StringComparison.Ordinal);
    }

    [Fact]
    public void LedgerAndEvidenceCloseOnlyCoreBindingAndDeferSnapshotHardening()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.28-core-binding-state-extraction.md");
        Assert.Contains("RF-11.28 | closed CoreBindingState", ledger, StringComparison.Ordinal);
        Assert.Contains("exactly four", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RF-11.29 diagnostic snapshot hardening", ledger, StringComparison.Ordinal);
        Assert.Contains("shallow", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not hardened", evidence, StringComparison.OrdinalIgnoreCase);
    }

    private static Type Required(string name) => typeof(Processor.CPU_Core).Assembly.GetType(name) ??
        throw new InvalidOperationException(name);
    private static void AssertOrder(string text, params string[] markers)
    {
        int prior = -1;
        foreach (string marker in markers)
        {
            int current = text.IndexOf(marker, prior + 1, StringComparison.Ordinal);
            Assert.True(current > prior, $"Expected '{marker}' after prior marker.");
            prior = current;
        }
    }
    private static string ReadSources(string path) => string.Join('\n', SourceFiles(path).Select(File.ReadAllText));
    private static IEnumerable<string> SourceFiles(string path) => Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
        .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    private static string Read(string root, params string[] parts) => File.ReadAllText(parts.Aggregate(root, Path.Combine));
    private static string FindRoot()
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
