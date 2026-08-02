using System.Text.RegularExpressions;
using HybridCPU_ISE.Tests.TestHelpers;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6l valid-input-only MemoryBankResolution selection in the RF-06
/// Assist capability projection.
/// </summary>
[Collection("Phase09 Memory Bank Routing Telemetry")]
public sealed class Rf126lAssistCapabilityBankResolutionValidInputCutoverTests
{
    private const string ThisFile =
        "Rf126lAssistCapabilityBankResolutionValidInputCutoverTests.cs";
    private const string ProjectionFault =
        "RF-06.5 assist projection requires one resolved read footprint and a non-retire-visible assist carrier.";

    [Fact]
    public void EveryResolvedBankProjectsExactCapabilityIncludingBankZero()
    {
        MemoryBankRouting.ResetTelemetryForTesting();
        try
        {
            ProcessorMemoryScope.WithProcessorMemory(
                ProcessorMemoryScope.CreateMemorySubsystem(
                    MemoryBankId.BankCount, 64),
                () =>
                {
                    for (int bank = 0; bank < MemoryBankId.BankCount; bank++)
                    {
                        ulong address = checked((ulong)bank * 64UL + 8UL);
                        AssistMicroOp assist = CreateAssist(address);
                        Rf06AssistCapability capability =
                            Rf06SpecializedCapabilityProjection.ProjectAssist(
                                assist, Binding());

                        MemoryBankId expected = new(bank);
                        Assert.Equal(expected, capability.Bank);
                        Assert.Equal(expected,
                            capability.Contract.Memory.Bank);
                        Assert.Equal(MemoryCapabilityKind.Load,
                            capability.Contract.Memory.Kind);
                        FrozenMemoryRange range = Assert.Single(
                            capability.Contract.Memory.Footprint);
                        Assert.Equal(address, range.Address);
                        Assert.Equal(32UL, range.Length);
                        Assert.Equal(range, capability.PrefetchFootprint);
                        Assert.Equal(assist.Kind, capability.Kind);
                        Assert.Equal(assist.ExecutionMode,
                            capability.ExecutionMode);
                        Assert.Equal(assist.CarrierKind,
                            capability.CarrierKind);
                        Assert.Equal(assist.CarrierVirtualThreadId,
                            capability.CarrierVirtualThreadId);
                        Assert.Equal(assist.DonorVirtualThreadId,
                            capability.DonorVirtualThreadId);
                        Assert.Equal(assist.TargetVirtualThreadId,
                            capability.TargetVirtualThreadId);
                    }
                });

            Assert.Equal(0UL,
                MemoryBankRouting.SchedulerVisibleUninitializedUseCount);
        }
        finally
        {
            MemoryBankRouting.ResetTelemetryForTesting();
        }
    }

    [Fact]
    public void ProjectionStillConsumesConstructorTimeSnapshot()
    {
        AssistMicroOp assist = ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(16, 64),
            () => CreateAssist(5UL * 64UL + 8UL));
        ulong resourceMask = assist.ResourceMask.High;
        ulong safetyMask = assist.SafetyMask.High;

        ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(16, 128),
            () =>
            {
                Rf06AssistCapability capability =
                    Rf06SpecializedCapabilityProjection.ProjectAssist(
                        assist, Binding());
                Assert.Equal(5, assist.MemoryBankId);
                Assert.Equal(new MemoryBankId(5), capability.Bank);
                Assert.Equal(resourceMask, assist.ResourceMask.High);
                Assert.Equal(safetyMask, assist.SafetyMask.High);
            });
    }

    [Fact]
    public void UnavailableAndIncompleteTopologyKeepExactProjectionFault()
    {
        AssertUnresolvedProjectionFault(memory: null);
        AssertUnresolvedProjectionFault(
            ProcessorMemoryScope.CreateMemorySubsystem(8, 0));
    }

    [Fact]
    public void WideGeometryKeepsLowAliasAndHighConstructorFault()
    {
        ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(32, 64),
            () =>
            {
                AssistMicroOp low = CreateAssist(0);
                Rf06AssistCapability capability =
                    Rf06SpecializedCapabilityProjection.ProjectAssist(
                        low, Binding());
                Assert.Equal(new MemoryBankId(0), capability.Bank);

                ArgumentOutOfRangeException fault =
                    Assert.Throws<ArgumentOutOfRangeException>(
                        () => CreateAssist(17UL * 64UL));
                Assert.Equal("bankId", fault.ParamName);
            });
    }

    [Fact]
    public void SourceOrderKeepsGateResultExtractionAndPublication()
    {
        string source = ProjectionSource();
        string method = Slice(source,
            "internal static Rf06AssistCapability ProjectAssist(",
            "internal static Rf06DmaCapability ProjectDma(");
        int gate = Index(method, "!carrier.HasResolvedMemoryBankId");
        int footprint = Index(method,
            "FrozenMemoryRange footprint = new(address, length)", gate);
        int result = Index(method,
            "MemoryBankResolution bankResolution =", footprint);
        int selection = Index(method,
            "MemoryBankResolution.Resolved(", result);
        int checkedBank = Index(method,
            "new MemoryBankId(carrier.MemoryBankId)", selection);
        int extraction = Index(method,
            "MemoryBankId bank = bankResolution.Bank!.Value", checkedBank);
        int contract = Index(method,
            "ExecutionContract contract = CreateContract(", extraction);
        int memory = Index(method,
            "MemoryCapability.Create(", contract);
        int capability = Index(method,
            "return new Rf06AssistCapability(", memory);

        Assert.True(gate >= 0);
        Assert.True(footprint > gate);
        Assert.True(result > footprint);
        Assert.True(selection > result);
        Assert.True(checkedBank > selection);
        Assert.True(extraction > checkedBank);
        Assert.True(contract > extraction);
        Assert.True(memory > contract);
        Assert.True(capability > memory);
        Assert.Equal(3, Regex.Matches(method, @"\bbank\b").Count);
    }

    [Fact]
    public void CallerAndAssistStorageTopologyRemainUnchanged()
    {
        string root = FindRepositoryRoot();
        string assist = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Assist", "AssistMicroOp.cs");
        Assert.Contains("public int MemoryBankId { get; }", assist,
            StringComparison.Ordinal);
        Assert.Contains(
            "MemoryBankId = ResolveMemoryBankId(BaseAddress)",
            assist, StringComparison.Ordinal);
        Assert.Contains(
            "public bool HasResolvedMemoryBankId =>",
            assist, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankResolution", assist,
            StringComparison.Ordinal);

        Assert.Equal(0, CountQualifiedProjectCalls(
            Path.Combine(root, "HybridCPU_ISE")));
        Assert.Equal(4, CountQualifiedProjectCalls(
            Path.Combine(root, "HybridCPU_ISE.Tests"), ThisFile));
        foreach (string externalRoot in new[]
                 {
                     "HybridCPU_Compiler",
                     "CpuInterfaceBridge",
                     "HybridCPU_RoslynBridge",
                     "TestAssemblerConsoleApps"
                 })
        {
            Assert.Equal(0, CountQualifiedProjectCalls(
                Path.Combine(root, externalRoot)));
        }

        string backpressure = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Core", "Pipeline", "Assist", "Runtime", "AssistRuntime.cs");
        Assert.Equal(2, Regex.Matches(backpressure,
            @"assistMicroOp\.MemoryBankId\b").Count);
    }

    [Fact]
    public void ResultFactoryAndExternalManifestIsExact()
    {
        string root = FindRepositoryRoot();
        string production = ReadSourceTree(Path.Combine(root,
            "HybridCPU_ISE"), excludedFileName: "MemoryBankResolution.cs");
        Assert.Equal(3, Regex.Matches(production,
            @"\bMemoryBankResolution\.Resolved\s*\(").Count);
        Assert.Equal(2, Regex.Matches(production,
            @"\bMemoryBankResolution\.UnavailableTopology\b").Count);
        Assert.Equal(2, Regex.Matches(production,
            @"\bMemoryBankResolution\.InvalidGeometry\b").Count);

        string[] resolvedSites = CaptureCallSites(root,
            Path.Combine(root, "HybridCPU_ISE"),
            @"\bMemoryBankResolution\.Resolved\s*\(",
            excludedFileName: "MemoryBankResolution.cs");
        Assert.Equal(
            [
                "HybridCPU_ISE/CloseToHSL/Core/Decoder/Rf06SpecializedCapabilityProjection.cs:: MemoryBankResolution.Resolved(new MemoryBankId(bank));",
                "HybridCPU_ISE/CloseToHSL/Core/Decoder/Rf06SpecializedCapabilityProjection.cs:MemoryBankResolution.Resolved(",
                "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Rf06MemoryShadowOracleDifferential.cs:MemoryBankResolution.Resolved(new MemoryBankId(bank));"
            ],
            resolvedSites);

        foreach (string externalRoot in new[]
                 {
                     "HybridCPU_Compiler",
                     "CpuInterfaceBridge",
                     "HybridCPU_RoslynBridge",
                     "TestAssemblerConsoleApps"
                 })
        {
            Assert.Empty(CaptureCallSites(root,
                Path.Combine(root, externalRoot),
                @"\bMemoryBankResolution\.(?:Resolved|UnavailableTopology|InvalidGeometry)\b"));
        }
    }


    private static void AssertUnresolvedProjectionFault(
        YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memory)
    {
        MemoryBankRouting.ResetTelemetryForTesting();
        try
        {
            ProcessorMemoryScope.WithProcessorMemory(memory, () =>
            {
                AssistMicroOp assist = CreateAssist(0x2000);
                InvalidOperationException fault =
                    Assert.Throws<InvalidOperationException>(() =>
                        Rf06SpecializedCapabilityProjection.ProjectAssist(
                            assist, Binding()));
                Assert.Equal(ProjectionFault, fault.Message);
                Assert.Equal(
                    MemoryBankRouting.UninitializedSchedulerVisibleBankId,
                    assist.MemoryBankId);
                Assert.False(assist.HasResolvedMemoryBankId);
                Assert.Equal(0UL, assist.ResourceMask.High);
                Assert.Equal(0UL, assist.SafetyMask.High);
            });
            Assert.Equal(1UL,
                MemoryBankRouting.SchedulerVisibleUninitializedUseCount);
        }
        finally
        {
            MemoryBankRouting.ResetTelemetryForTesting();
        }
    }

    private static AssistMicroOp CreateAssist(ulong baseAddress) =>
        new(
            AssistKind.Ldsa,
            AssistExecutionMode.CachePrefetch,
            AssistCarrierKind.LsuHosted,
            baseAddress,
            prefetchLength: 32,
            elementSize: 4,
            elementCount: 8,
            new AssistOwnerBinding(
                carrierVirtualThreadId: 0,
                donorVirtualThreadId: 0,
                targetVirtualThreadId: 0,
                ownerContextId: 7,
                domainTag: 9,
                replayEpochId: 11,
                assistEpochId: 13,
                LocalityHint.None));

    private static GeneratedStaticBinding Binding()
    {
        Assert.True(GeneratedStaticBinding.TryFromOpcode(
            Processor.CPU_Core.IsaOpcodeValues.ADD,
            out GeneratedStaticBinding binding));
        return binding;
    }

    private static int CountQualifiedProjectCalls(
        string root,
        string? excludedFileName = null)
    {
        var regex = new Regex(
            @"\bRf06SpecializedCapabilityProjection\.ProjectAssist\s*\(",
            RegexOptions.CultureInvariant);
        return EnumerateSources(root)
            .Where(path => excludedFileName is null ||
                           !path.EndsWith(excludedFileName,
                               StringComparison.OrdinalIgnoreCase))
            .Sum(path => regex.Matches(File.ReadAllText(path)).Count);
    }

    private static string[] CaptureCallSites(
        string repositoryRoot,
        string sourceRoot,
        string pattern,
        string? excludedFileName = null)
    {
        var regex = new Regex(pattern, RegexOptions.CultureInvariant);
        var entries = new List<string>();
        foreach (string path in EnumerateSources(sourceRoot)
                     .Where(path => excludedFileName is null ||
                                    !path.EndsWith(excludedFileName,
                                        StringComparison.OrdinalIgnoreCase))
                     .Where(path => !path.EndsWith(ThisFile,
                         StringComparison.OrdinalIgnoreCase)))
        {
            string relative = Path.GetRelativePath(repositoryRoot, path)
                .Replace('\\', '/');
            foreach (string line in File.ReadLines(path))
            {
                if (regex.IsMatch(line))
                    entries.Add($"{relative}:{line.Trim()}");
            }
        }
        entries.Sort(StringComparer.Ordinal);
        return entries.ToArray();
    }

    private static int Index(string source, string value, int start = 0)
    {
        int index = source.IndexOf(value, start, StringComparison.Ordinal);
        Assert.True(index >= 0, $"Missing source marker: {value}");
        return index;
    }

    private static string Slice(
        string source,
        string startMarker,
        string endMarker)
    {
        int start = Index(source, startMarker);
        int end = Index(source, endMarker, start);
        return source[start..end];
    }

    private static string ProjectionSource()
    {
        string root = FindRepositoryRoot();
        return Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder",
                "Rf06SpecializedCapabilityProjection.cs")
            .Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static string ReadSourceTree(
        string root,
        string? excludedFileName = null) =>
        string.Join("\n", EnumerateSources(root)
            .Where(path => excludedFileName is null ||
                           !path.EndsWith(excludedFileName,
                               StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.EndsWith(ThisFile,
                StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText));

    private static IEnumerable<string> EnumerateSources(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .OrderBy(path => path, StringComparer.Ordinal);

    private static bool IsBuildOutput(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/",
                   StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/obj/",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(parts.Aggregate(root, Path.Combine));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName,
                    "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName,
                    "ResearchPaper", "section", "md base")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException(
            "Repository root was not found.");
    }
}
