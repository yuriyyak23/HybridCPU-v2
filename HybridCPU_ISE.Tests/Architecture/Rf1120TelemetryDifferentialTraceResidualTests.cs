using System.Reflection;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1120TelemetryDifferentialTraceResidualTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [Fact]
    public void TelemetryOwnsSingleNullableDifferentialTraceReference()
    {
        Type telemetry = Required("YAKSys_Hybrid_CPU.Core.TelemetryState");
        FieldInfo field = telemetry.GetField("DifferentialTraceCapture", Flags) ?? throw new InvalidOperationException();
        Assert.Equal(typeof(DifferentialTraceCapture), field.FieldType);
        Assert.DoesNotContain(telemetry.GetMethods(Flags), method =>
            method.Name is "Execute" or "Commit" or "Rollback" or "Publish" or "Migrate");
    }

    [Fact]
    public void LegacyReferenceIsRemovedAndForwardsByRef()
    {
        Type core = typeof(Processor.CPU_Core);
        Assert.Null(core.GetField("differentialTraceCapture", Flags));
        PropertyInfo property = core.GetProperty("differentialTraceCapture", Flags) ?? throw new InvalidOperationException();
        Assert.True(property.PropertyType.IsByRef);
        Assert.True(property.GetMethod!.ReturnParameter.ParameterType.IsByRef);
    }

    [Fact]
    public void TransitionalCopiesAliasSameOptionalTraceCapture()
    {
#pragma warning disable CS0618
        var core = new Processor.CPU_Core(0);
#pragma warning restore CS0618
        Processor.CPU_Core copy = core;
        var capture = new DifferentialTraceCapture();
        core.Runtime.Telemetry.DifferentialTraceCapture = capture;
        Assert.Same(capture, copy.Runtime.Telemetry.DifferentialTraceCapture);
    }

    [Fact]
    public void ProductionAppendGateAndDisabledDefaultRemainUnchanged()
    {
        string root = FindRoot();
        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.StageFlow.cs");
        AssertOrder(stageFlow, "if (differentialTraceCapture == null)", "return;", "differentialTraceCapture.AddEntry(", "DifferentialTraceEntry.FromAdvisoryChain");
        string production = ReadSources(Path.Combine(root, "HybridCPU_ISE"));
        Assert.DoesNotContain("new DifferentialTraceCapture", production, StringComparison.Ordinal);
    }

    [Fact]
    public void TraceTypeRemainsAppendReadClearDiagnosticOnly()
    {
        Type trace = typeof(DifferentialTraceCapture);
        Assert.NotNull(trace.GetMethod("AddEntry", Flags));
        Assert.NotNull(trace.GetMethod("GetEntries", Flags));
        Assert.NotNull(trace.GetMethod("Clear", Flags));
        Assert.DoesNotContain(trace.GetMethods(Flags), method =>
            method.Name.Contains("Execute", StringComparison.Ordinal) ||
            method.Name.Contains("Commit", StringComparison.Ordinal) ||
            method.Name.Contains("Publish", StringComparison.Ordinal));
    }

    [Fact]
    public void LedgerAndEvidenceCloseOnlyDifferentialTraceReference()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11", "rf11.20-telemetry-differential-trace-residual.md");
        Assert.Contains("RF-11.20 | closed TelemetryState differential-trace residual", ledger, StringComparison.Ordinal);
        Assert.Contains("exactly one", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RF-11.21 AssistState", ledger, StringComparison.Ordinal);
    }

    private static Type Required(string name) => typeof(Processor.CPU_Core).Assembly.GetType(name) ?? throw new InvalidOperationException(name);
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
    private static string ReadSources(string path) => string.Join("\n", Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories)
        .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .OrderBy(file => file, StringComparer.Ordinal).Select(File.ReadAllText));
    private static string Read(string root, params string[] parts) => File.ReadAllText(parts.Aggregate(root, Path.Combine));
    private static string FindRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current != null) { if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx"))) return current; current = Directory.GetParent(current)?.FullName; }
        throw new DirectoryNotFoundException();
    }
}
