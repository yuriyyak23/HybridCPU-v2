using System.Reflection;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf113EmptyCoreRuntimeStateTests
{
    private const BindingFlags InstanceDeclared =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.DeclaredOnly;

    [Fact]
    public void ConstructedCoreOwnsOneStableReferenceIdentityAndCopiesAliasIt()
    {
#pragma warning disable CS0618
        var core = new Processor.CPU_Core(0);
        var otherCore = new Processor.CPU_Core(0);
#pragma warning restore CS0618
        Processor.CPU_Core transitionalCopy = core;

        Assert.Same(core.Runtime, transitionalCopy.Runtime);
        Assert.NotSame(core.Runtime, otherCore.Runtime);
        if (typeof(Processor.CPU_Core).IsValueType)
        {
            Processor.CPU_Core absent = default;
            Assert.Throws<InvalidOperationException>(() => absent.Runtime);
        }
        else
        {
            Assert.Null(default(Processor.CPU_Core));
        }
    }

    [Fact]
    public void RuntimeReferenceIsReadonlyAndCannotBeReplacedAfterConstruction()
    {
        FieldInfo field = typeof(Processor.CPU_Core).GetField(
            "_runtime",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException("CPU_Core._runtime was not found.");

        Assert.True(field.IsInitOnly);
        Assert.Equal("CoreRuntimeState", field.FieldType.Name);
        Assert.NotNull(typeof(Processor.CPU_Core).GetProperty(
            "Runtime",
            BindingFlags.Instance | BindingFlags.NonPublic));
    }

    [Fact]
    public void RuntimeRootContainsOnlyTheFirstExtractedDomainAndNoUniversalAuthority()
    {
        Type root = typeof(Processor.CPU_Core).Assembly.GetType(
            "YAKSys_Hybrid_CPU.Core.CoreRuntimeState") ??
            throw new InvalidOperationException("CoreRuntimeState was not found.");

        Assert.True(root.IsClass);
        Assert.True(root.IsSealed);
        Assert.Equal(
            new[] { "AdmissionState", "ArchitecturalState", "AssistState", "BackendState", "CacheState", "CoreBindingState", "DecodeState", "ExecutionState", "ExtensionState", "FrontendState", "LegacyCompatibilityState", "MemoryPipelineState", "ReplayState", "ResourceState", "RetireState", "SchedulingState", "ScratchState", "TelemetryState", "VirtualThreadControlState" },
            root.GetFields(InstanceDeclared)
                .Select(field => field.FieldType.Name)
                .OrderBy(name => name, StringComparer.Ordinal));
        string[] forbidden = ["Commit", "Rollback", "Publish", "Execute", "Migrate"];
        Assert.DoesNotContain(root.GetMethods(InstanceDeclared),
            method => forbidden.Any(name => method.Name.Contains(name, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void ConstructorAndPlatformAccessorsEnforceExplicitLifecycle()
    {
        string root = FindRepositoryRoot();
        string constructor = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Architecture", "State", "Architectural", "CPU_Core.StateData.cs");
        string accessors = Read(root, "HybridCPU_ISE", "NonRTL", "Processor", "Core",
            "Processor.CoreIdentity.cs");

        Assert.Equal(1, Count(constructor, "new CoreRuntimeState()"));
        Assert.Contains("_ = liveCore.Runtime;", accessors, StringComparison.Ordinal);
        Assert.Contains("_ = replacement.Runtime;", accessors, StringComparison.Ordinal);
        Assert.Contains("GetCoreSlotRef", accessors, StringComparison.Ordinal);
    }

    [Fact]
    public void LedgerAndEvidenceCloseOnlyTheEmptyRootSlice()
    {
        string root = FindRepositoryRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "10_RF11",
            "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.3-empty-core-runtime-state.md");

        Assert.Contains("RF-11.3 | closed empty containment root", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-11.4 TelemetryState", ledger, StringComparison.Ordinal);
        Assert.Contains("moves no semantic state", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("containment is not authority", evidence, StringComparison.OrdinalIgnoreCase);
    }

    private static int Count(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("HybridCPU repository root was not found.");
    }
}
