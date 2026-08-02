using System.Reflection;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf117AdmissionStateExtractionTests
{
    private const BindingFlags InstanceDeclared =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.DeclaredOnly;

    private static readonly string[] Forwarders =
    [
        "pipeIDAdmissionCandidateView",
        "pipeIDAdmissionDecisionDraft",
        "pipeIDAdmissionHandoff",
        "pipeIDAdmissionPreparation"
    ];

    [Fact]
    public void RuntimeContainsOneExactFourFieldAdmissionDomain()
    {
        Type runtime = RequiredType("YAKSys_Hybrid_CPU.Core.CoreRuntimeState");
        Type admission = RequiredType("YAKSys_Hybrid_CPU.Core.AdmissionState");
        Assert.Single(runtime.GetFields(InstanceDeclared), field => field.FieldType == admission);
        Assert.Equal(
            new[] { "CandidateView", "DecisionDraft", "Handoff", "Preparation" },
            admission.GetFields(InstanceDeclared).Select(field => field.Name)
                .OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void LegacyAdmissionFieldsAreRemovedAndForwardersAreByRef()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in Forwarders)
        {
            Assert.Null(core.GetField(name, InstanceDeclared));
            PropertyInfo property = core.GetProperty(name, InstanceDeclared) ??
                throw new InvalidOperationException($"Admission ref-forwarder '{name}' is missing.");
            Assert.True(property.PropertyType.IsByRef);
            Assert.True(property.GetMethod!.ReturnParameter.ParameterType.IsByRef);
        }
    }

    [Fact]
    public void TransitionalCoreCopiesAliasOneAdmissionOwner()
    {
#pragma warning disable CS0618
        var core = new Processor.CPU_Core(0);
#pragma warning restore CS0618
        Processor.CPU_Core copy = core;

        YAKSys_Hybrid_CPU.Core.RuntimeClusterAdmissionDecisionDraft decision =
            YAKSys_Hybrid_CPU.Core.RuntimeClusterAdmissionDecisionDraft.CreateEmpty(0x7700);
        core.Runtime.Admission.DecisionDraft = decision;

        Assert.Equal(0x7700UL, copy.Runtime.Admission.DecisionDraft.PC);
    }

    [Fact]
    public void CombinedDecodeAdmissionConstructionAndConsumeSitesRemainInPlace()
    {
        string root = FindRepositoryRoot();
        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.StageFlow.cs");
        string pipeline = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Stages", "Issue", "CPU_Core.Pipeline.cs");

        Assert.Contains("pipeIDAdmissionPreparation = Core.RuntimeClusterAdmissionPreparation.Create", stageFlow, StringComparison.Ordinal);
        Assert.Contains("pipeIDAdmissionCandidateView =", stageFlow, StringComparison.Ordinal);
        Assert.Contains("pipeIDAdmissionDecisionDraft =", stageFlow, StringComparison.Ordinal);
        Assert.Contains("pipeIDAdmissionHandoff =", stageFlow, StringComparison.Ordinal);
        Assert.Contains("pipeID.AdmissionExecutionMode = pipeIDAdmissionDecisionDraft.ExecutionMode;", stageFlow, StringComparison.Ordinal);
        Assert.Contains("pipeIDAdmissionPreparation = Core.RuntimeClusterAdmissionPreparation.CreateEmpty();", pipeline, StringComparison.Ordinal);
    }

    [Fact]
    public void SchedulerDecodeExecutionAndRetireAuthoritiesRemainOutsideAdmissionState()
    {
        Type core = typeof(Processor.CPU_Core);
        Type admission = RequiredType("YAKSys_Hybrid_CPU.Core.AdmissionState");
        string[] fields = admission.GetFields(InstanceDeclared).Select(field => field.Name).ToArray();

        Assert.True((core.GetProperty("_fspScheduler", InstanceDeclared) ??
            throw new InvalidOperationException("_fspScheduler")).PropertyType.IsByRef);
        Assert.NotNull(core.GetProperty("rf08OperationAttemptIssuer", InstanceDeclared));
        Assert.True((core.GetProperty("pipeEX", InstanceDeclared) ??
            throw new InvalidOperationException("pipeEX")).PropertyType.IsByRef);

        Assert.DoesNotContain(fields, name => name.Contains("Scheduler", StringComparison.Ordinal));
        Assert.DoesNotContain(fields, name => name.Contains("Execute", StringComparison.Ordinal));
        Assert.DoesNotContain(fields, name => name.Contains("Retire", StringComparison.Ordinal));
    }

    [Fact]
    public void ObservationAndTestSupportRemainSnapshotAdapters()
    {
        string root = FindRepositoryRoot();
        string observation = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Core", "CPU_Core.Pipeline.Helpers.PipelineObservation.cs");
        string support = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Core", "CPU_Core.TestSupport.cs");

        Assert.Contains("admissionPreparation: pipeIDAdmissionPreparation", observation, StringComparison.Ordinal);
        Assert.Contains("admissionHandoff: pipeIDAdmissionHandoff", observation, StringComparison.Ordinal);
        Assert.Contains("return pipeIDAdmissionHandoff;", support, StringComparison.Ordinal);
    }

    [Fact]
    public void LedgerAndEvidenceCloseOnlyAdmissionState()
    {
        string root = FindRepositoryRoot();
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "10_RF11",
            "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.7-admission-state-extraction.md");

        Assert.Contains("RF-11.7 | closed AdmissionState", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-11.8 ReplayState", ledger, StringComparison.Ordinal);
        Assert.Contains("four", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("combined Decode/admission", evidence, StringComparison.Ordinal);
    }

    private static Type RequiredType(string name) =>
        typeof(Processor.CPU_Core).Assembly.GetType(name) ??
        throw new InvalidOperationException($"Required type '{name}' was not found.");

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
