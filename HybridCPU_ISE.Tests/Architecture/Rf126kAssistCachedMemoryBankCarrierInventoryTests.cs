using System.Text.RegularExpressions;
using HybridCPU_ISE.Tests.TestHelpers;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6k decision-only closed-world inventory of the cached Assist bank
/// snapshot, its masks, capability projection and scheduler backpressure uses.
/// </summary>
[Collection("Phase09 Memory Bank Routing Telemetry")]
public sealed class Rf126kAssistCachedMemoryBankCarrierInventoryTests
{
    private const string ThisFile =
        "Rf126kAssistCachedMemoryBankCarrierInventoryTests.cs";

    [Fact]
    public void PaperSeparatesResolvedBankIdentityFromAssistAuthority()
    {
        string root = FindRepositoryRoot();
        string bankAuthority = Read(root, "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");
        string assistAuthority = Read(root, "ResearchPaper", "section",
            "md base",
            "6_Assist_Coupled_Data_Movement_and_Donor_Semantics.md");

        Assert.Contains(
            "Existing `MemoryBankId` denotes a resolved scheduler-visible bank `0..15`",
            bankAuthority, StringComparison.Ordinal);
        Assert.Contains("Bank zero is valid", bankAuthority,
            StringComparison.Ordinal);
        Assert.Contains("Resolution does not grant memory admission",
            bankAuthority, StringComparison.Ordinal);
        Assert.Contains(
            "Assist bypasses the ordinary non-assist dynamic outer-cap gate",
            assistAuthority, StringComparison.Ordinal);
        Assert.Contains(
            "it still faces assist-specific backpressure and quota",
            assistAuthority, StringComparison.Ordinal);
        Assert.Contains("An assist is non-retiring", assistAuthority,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ConstructorSnapshotsEveryValidBankAndProjectsExactCapability()
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
                        AssistMicroOp assist =
                            CreateAssist(checked((ulong)bank * 64UL + 8UL));
                        Assert.Equal(bank, assist.MemoryBankId);
                        Assert.True(assist.HasResolvedMemoryBankId);
                        ulong bankBit = 1UL << (48 + bank);
                        Assert.Equal(bankBit, assist.ResourceMask.High);
                        Assert.Equal(bankBit, assist.SafetyMask.High);
                        Assert.Equal(assist.ResourceMask,
                            assist.OriginalResourceMask);

                        Rf06AssistCapability capability =
                            Rf06SpecializedCapabilityProjection.ProjectAssist(
                                assist, BindingFor(Processor.CPU_Core
                                    .IsaOpcodeValues.ADD));
                        Assert.Equal(new MemoryBankId(bank), capability.Bank);
                        Assert.Equal(new MemoryBankId(bank),
                            capability.Contract.Memory.Bank);
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
    public void CachedBankMasksAndCapabilityDoNotFollowLaterTopologyMutation()
    {
        AssistMicroOp assist = ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(16, 64),
            () => CreateAssist(5UL * 64UL + 8UL));
        Assert.Equal(5, assist.MemoryBankId);
        ulong originalBankBit = assist.ResourceMask.High;

        ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(16, 128),
            () =>
            {
                Assert.Equal(5, assist.MemoryBankId);
                Assert.Equal(originalBankBit, assist.ResourceMask.High);
                Assert.Equal(originalBankBit, assist.SafetyMask.High);
                Rf06AssistCapability capability =
                    Rf06SpecializedCapabilityProjection.ProjectAssist(
                        assist,
                        BindingFor(Processor.CPU_Core.IsaOpcodeValues.ADD));
                Assert.Equal(new MemoryBankId(5), capability.Bank);
            });
    }

    [Fact]
    public void UnavailableAndWideGeometryKeepExactLegacyOutcomes()
    {
        AssertUnresolvedSnapshot(memory: null);
        AssertUnresolvedSnapshot(
            ProcessorMemoryScope.CreateMemorySubsystem(8, 0));

        ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(32, 64),
            () =>
            {
                AssistMicroOp wideLow = CreateAssist(0);
                Assert.Equal(0, wideLow.MemoryBankId);
                Assert.True(wideLow.HasResolvedMemoryBankId);
                Assert.Equal(1UL << 48, wideLow.ResourceMask.High);

                ArgumentOutOfRangeException fault =
                    Assert.Throws<ArgumentOutOfRangeException>(
                        () => CreateAssist(17UL * 64UL));
                Assert.Equal("bankId", fault.ParamName);
            });
    }

    [Fact]
    public void SourceOrderFreezesSnapshotMaskPublicationAndProjectionGate()
    {
        string root = FindRepositoryRoot();
        string assist = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
                "Pipeline", "MicroOps", "Assist", "AssistMicroOp.cs")
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        string constructor = Slice(assist,
            "public AssistMicroOp(",
            "public AssistKind Kind");
        int snapshot = Index(constructor,
            "MemoryBankId = ResolveMemoryBankId(BaseAddress)");
        int ranges = Index(constructor,
            "ReadMemoryRanges = new[] { (BaseAddress, PrefetchLength) }",
            snapshot);
        int resolvedGate = Index(constructor,
            "MemoryBankRouting.IsResolvedSchedulerVisibleBankId(MemoryBankId)",
            ranges);
        int resourceMask = Index(constructor,
            "ResourceMaskBuilder.ForMemoryBank(MemoryBankId)", resolvedGate);
        int safetyMask = Index(constructor,
            "ResourceMaskBuilder.ForMemoryBank128(MemoryBankId)", resourceMask);
        int original = Index(constructor,
            "OriginalResourceMask = ResourceMask", safetyMask);
        int admission = Index(constructor,
            "RefreshAdmissionMetadata()", original);

        Assert.True(snapshot >= 0);
        Assert.True(ranges > snapshot);
        Assert.True(resolvedGate > ranges);
        Assert.True(resourceMask > resolvedGate);
        Assert.True(safetyMask > resourceMask);
        Assert.True(original > safetyMask);
        Assert.True(admission > original);

        string projection = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
                "Decoder", "Rf06SpecializedCapabilityProjection.cs")
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        string method = Slice(projection,
            "internal static Rf06AssistCapability ProjectAssist(",
            "internal static Rf06DmaCapability ProjectDma(");
        int hasResolved = Index(method,
            "!carrier.HasResolvedMemoryBankId");
        int result = Index(method,
            "MemoryBankResolution.Resolved(", hasResolved);
        int checkedBank = Index(method,
            "new MemoryBankId(carrier.MemoryBankId)", result);
        int extractedBank = Index(method,
            "MemoryBankId bank = bankResolution.Bank!.Value", checkedBank);
        int capability = Index(method,
            "MemoryCapability.Create(", extractedBank);
        Assert.True(result > hasResolved);
        Assert.True(checkedBank > hasResolved);
        Assert.True(extractedBank > checkedBank);
        Assert.True(capability > extractedBank);
    }

    [Fact]
    public void ClosedWorldProducerReaderAndConstructionManifestsAreExact()
    {
        string root = FindRepositoryRoot();
        string assist = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Assist", "AssistMicroOp.cs");
        Assert.Equal(1, Regex.Matches(assist,
            @"MemoryBankId\s*=\s*ResolveMemoryBankId\(BaseAddress\)").Count);
        Assert.Equal(2, Regex.Matches(assist,
            @"MemoryBankRouting\.IsResolvedSchedulerVisibleBankId\(MemoryBankId\)").Count);
        Assert.Equal(1, Regex.Matches(assist,
            @"ResourceMaskBuilder\.ForMemoryBank\(MemoryBankId\)").Count);
        Assert.Equal(1, Regex.Matches(assist,
            @"ResourceMaskBuilder\.ForMemoryBank128\(MemoryBankId\)").Count);

        string projection = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Decoder", "Rf06SpecializedCapabilityProjection.cs");
        Assert.Equal(1, Regex.Matches(projection,
            @"carrier\.HasResolvedMemoryBankId\b").Count);
        Assert.Equal(1, Regex.Matches(projection,
            @"carrier\.MemoryBankId\b").Count);

        string backpressure = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Assist", "Runtime", "AssistRuntime.cs");
        Assert.Equal(2, Regex.Matches(backpressure,
            @"assistMicroOp\.MemoryBankId\b").Count);
        Assert.Contains("if ((uint)memoryBankId >= 16)",
            backpressure, StringComparison.Ordinal);
        Assert.Contains(
            "IncrementPackedConsumedHardwareBudget(_consumedSharedReadBudgetByBank, memoryBankId)",
            backpressure, StringComparison.Ordinal);

        Assert.Equal(2, CountMatches(root, "HybridCPU_ISE",
            @"new\s+AssistMicroOp\s*\("));
        Assert.Equal(1, CountMatches(root, "TestAssemblerConsoleApps",
            @"new\s+AssistMicroOp\s*\("));
        Assert.Equal(8, CountMatches(root, "HybridCPU_ISE.Tests",
            @"new\s+AssistMicroOp\s*\(", ThisFile));
        Assert.Equal(0, CountMatches(root, "HybridCPU_Compiler",
            @"new\s+AssistMicroOp\s*\("));
        Assert.Equal(0, CountMatches(root, "CpuInterfaceBridge",
            @"new\s+AssistMicroOp\s*\("));
        Assert.Equal(0, CountMatches(root, "HybridCPU_RoslynBridge",
            @"new\s+AssistMicroOp\s*\("));
    }

    [Fact]
    public void RawBypassWireReplayReflectionAndTestSupportSeamsStayExplicit()
    {
        string root = FindRepositoryRoot();
        string backpressure = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Assist", "Runtime", "AssistRuntime.cs");
        Assert.Contains(
            "if ((uint)memoryBankId >= 16)\n            {\n                return true;",
            backpressure.Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal);
        Assert.Contains(
            "if ((uint)bankId >= 16)\n            {\n                return consumedPackedBudgetByBank;",
            backpressure.Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal);

        string certificate = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Certificates", "BundleResourceCertificate.cs");
        Assert.Contains(
            "SafetyMask128 opMask = admission.CertificateMask",
            certificate, StringComparison.Ordinal);
        string replayKey = Slice(
            certificate.Replace("\r\n", "\n", StringComparison.Ordinal),
            "private static void AppendAssistReplayKey(",
            "private static void AppendDmaStreamComputeReplayKey(");
        Assert.DoesNotContain("MemoryBankId", replayKey,
            StringComparison.Ordinal);

        foreach (string externalRoot in new[]
                 {
                     "HybridCPU_Compiler",
                     "CpuInterfaceBridge",
                     "HybridCPU_RoslynBridge"
                 })
        {
            string text = ReadSourceTree(Path.Combine(root, externalRoot));
            Assert.DoesNotContain("AssistMicroOp", text,
                StringComparison.Ordinal);
            Assert.DoesNotContain("HasResolvedMemoryBankId", text,
                StringComparison.Ordinal);
        }

        string tests = ReadSourceTree(Path.Combine(root,
            "HybridCPU_ISE.Tests"));
        Assert.DoesNotContain("<MemoryBankId>k__BackingField", tests,
            StringComparison.Ordinal);
        string scope = Read(root, "HybridCPU_ISE.Tests", "TestHelpers",
            "ProcessorMemoryScope.cs");
        Assert.Contains("Processor.Memory = memory", scope,
            StringComparison.Ordinal);
        Assert.DoesNotContain("SetValue(", scope, StringComparison.Ordinal);
    }


    private static void AssertUnresolvedSnapshot(
        YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memory)
    {
        MemoryBankRouting.ResetTelemetryForTesting();
        try
        {
            ProcessorMemoryScope.WithProcessorMemory(memory, () =>
            {
                AssistMicroOp assist = CreateAssist(0x2000);
                Assert.Equal(
                    MemoryBankRouting.UninitializedSchedulerVisibleBankId,
                    assist.MemoryBankId);
                Assert.False(assist.HasResolvedMemoryBankId);
                Assert.Equal(0UL, assist.ResourceMask.High);
                Assert.Equal(0UL, assist.SafetyMask.High);
                Assert.Throws<InvalidOperationException>(() =>
                    Rf06SpecializedCapabilityProjection.ProjectAssist(
                        assist,
                        BindingFor(Processor.CPU_Core.IsaOpcodeValues.ADD)));
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

    private static GeneratedStaticBinding BindingFor(ushort opcode)
    {
        Assert.True(GeneratedStaticBinding.TryFromOpcode(
            opcode, out GeneratedStaticBinding binding));
        return binding;
    }

    private static int CountMatches(
        string repositoryRoot,
        string relativeRoot,
        string pattern,
        string? excludedFileName = null)
    {
        var regex = new Regex(pattern, RegexOptions.CultureInvariant);
        return EnumerateSources(Path.Combine(repositoryRoot, relativeRoot))
            .Where(path => excludedFileName is null ||
                           !path.EndsWith(excludedFileName,
                               StringComparison.OrdinalIgnoreCase))
            .Sum(path => regex.Matches(File.ReadAllText(path)).Count);
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

    private static string ReadSourceTree(string root) =>
        string.Join("\n", EnumerateSources(root)
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
