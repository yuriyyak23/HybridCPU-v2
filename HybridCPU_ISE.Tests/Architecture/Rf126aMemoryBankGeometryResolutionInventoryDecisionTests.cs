using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6a decision-only closed-world inventory of resolved scheduler-visible
/// memory-bank identity and the existing unavailable/invalid geometry seams.
/// These guards authorize no production migration or behavior change.
/// </summary>
public sealed class Rf126aMemoryBankGeometryResolutionInventoryDecisionTests
{
    private const string ThisFile =
        "Rf126aMemoryBankGeometryResolutionInventoryDecisionTests.cs";
    private const string Rf126bGuard =
        "Rf126bMemoryBankResolutionCoreValidInputContractTests.cs";
    private const string Rf126cGuard =
        "Rf126cSchedulerVisibleBankResolverProducerInventoryTests.cs";
    private const string Rf126dGuard =
        "Rf126dSpecializedCapabilityBankResolutionValidInputCutoverTests.cs";
    private const string Rf126eGuard =
        "Rf126eSpecializedCapabilityNonResolvedOutcomeInventoryTests.cs";
    private const string Rf126fGuard =
        "Rf126fSpecializedCapabilityNonResolvedResultCutoverTests.cs";
    private const string Rf126gGuard =
        "Rf126gLoadStoreComputedBankCarrierInventoryTests.cs";
    private const string Rf126hGuard =
        "Rf126hShadowLegacyCarrierBankResolutionValidInputCutoverTests.cs";
    private const string Rf126iGuard =
        "Rf126iShadowLegacyCarrierNonResolvedOutcomeInventoryTests.cs";
    private const string Rf126jGuard =
        "Rf126jShadowLegacyCarrierNonResolvedResultCutoverTests.cs";
    private const string Rf126kGuard =
        "Rf126kAssistCachedMemoryBankCarrierInventoryTests.cs";
    private const string Rf126lGuard =
        "Rf126lAssistCapabilityBankResolutionValidInputCutoverTests.cs";
    private const string Rf126mGuard =
        "Rf126mPhysicalMemoryBankQueueIndexInventoryDecisionTests.cs";
    private const string Rf126nGuard =
        "Rf126nPhysicalMemoryBankGeometryLifetimeArchitectureDecisionTests.cs";
    private const string Rf126oGuard =
        "Rf126oPhysicalMemoryBankIndexCoreValidInputContractTests.cs";
    private const string Rf126pGuard =
        "Rf126pPhysicalMemoryBankIndexProducerConsumerRevalidationTests.cs";
    private const string Rf126qGuard =
        "Rf126qPhysicalMemoryBankIndexProducerValidInputCutoverTests.cs";
    private const string Rf126rGuard =
        "Rf126rPhysicalMemoryBankInvalidGeometryFallbackInventoryDecisionTests.cs";
    private const string Rf126sGuard =
        "Rf126sPhysicalMemoryBankRejectionCarrierArchitectureDecisionTests.cs";
    private const string Rf126tGuard =
        "Rf126tMemoryBankGeometryGenerationRepresentationArchitectureDecisionTests.cs";
    private const string Rf126uGuard =
        "Rf126uMemoryBankGeometryGenerationCoreValidInputContractTests.cs";
    private const string Rf126vGuard =
        "Rf126vPhysicalMemoryBankGeometrySnapshotRevalidationTests.cs";
    private const string Rf126wGuard =
        "Rf126wPhysicalMemoryBankGeometryCoreValidInputContractTests.cs";
    private const string Rf126xGuard =
        "Rf126xPhysicalMemoryBankBindingCoreValidInputContractTests.cs";
    private const string Rf126yGuard =
        "Rf126yPhysicalMemoryBankResolutionCoreValidInputContractTests.cs";
    private const string Rf126zGuard =
        "Rf126zMemoryBankGeometryUpdateResultCoreValidInputContractTests.cs";
    private const string Rf126aaGuard =
        "Rf126aaMemoryBankGeometryPublicationProducerConsumerInventoryTests.cs";
    private const string Rf126abGuard =
        "Rf126abMemoryBankGeometryLifecycleQuiescenceArchitectureDecisionTests.cs";
    private const string Rf126acGuard =
        "Rf126acMemoryBankGeometryLifecycleSerializationFoundationTests.cs";
    private const string Rf126adGuard =
        "Rf126adMemoryBankGeometryAuthoritativeReplacementValidInputCutoverTests.cs";
    private const string Rf126aeGuard =
        "Rf126aeAcceptedRequestPhysicalBankBindingInventoryTests.cs";
    private const string Rf126afGuard =
        "Rf126afControllerNativeAcceptedRequestBindingStorageTests.cs";
    private const string Rf126agGuard =
        "Rf126agControllerStoredBindingConsumerRevalidationTests.cs";
    private const string Rf126ahGuard =
        "Rf126ahControllerOrdinaryReadStoredBindingValidInputCutoverTests.cs";
    private const string Rf126aiGuard =
        "Rf126aiCanonicalVectorPhysicalBankEnvelopeArchitectureDecisionTests.cs";
    private const string Rf126ajGuard =
        "Rf126ajCanonicalVectorPhysicalBankEnvelopeCoreValidInputContractTests.cs";
    private const string Rf126akGuard =
        "Rf126akCanonicalEnvelopeAdmissionStorageServiceRevalidationTests.cs";
    private const string Rf126alGuard =
        "Rf126alCanonicalEnvelopeCaptureAndPrivateStorageValidInputCutoverTests.cs";
    private const string Rf126amGuard =
        "Rf126amCanonicalStoredEnvelopeServiceConsumptionValidInputCutoverTests.cs";
    private const string Rf126anGuard =
        "Rf126anCanonicalEnvelopeMismatchInvalidBehaviorArchitectureDecisionTests.cs";
    private const string Rf126aoGuard =
        "Rf126aoCanonicalSourceBaseBindingRemovalEligibilityDecisionTests.cs";
    private const string Rf126apGuard =
        "Rf126apCanonicalSourceBaseBindingCompatibilityRemovalTests.cs";
    private const string Rf126asGuard =
        "Rf126asLegacyAsyncCancellationBindingCarrierDecisionTests.cs";
    private const string Rf126auGuard =
        "Rf126auLegacyStoredBindingConsumerRevalidationDecisionTests.cs";
    private const string Rf126awGuard =
        "Rf126awLegacyCancellationMismatchArchitectureDecisionTests.cs";
    private const string Rf126axGuard =
        "Rf126axLegacyCancellationStoredBindingInvalidBehaviorTests.cs";
    private const string FamilyPattern =
        @"\b(?:MemoryBankId|BankId|BankID|bankId|BankIndex|bankIndex|ResolveSchedulerVisibleBankId|IsResolvedSchedulerVisibleBankId|ResolveBankId|UninitializedSchedulerVisibleBankId)\b";


    [Fact]
    public void CheckedIdentityRepresentsOnlyResolvedZeroThroughFifteen()
    {
        Assert.Equal(16, MemoryBankId.BankCount);
        Assert.Equal(0, new MemoryBankId(0).Value);
        Assert.Equal(15, new MemoryBankId(15).Value);
        Assert.Equal(new MemoryBankId(0), default(MemoryBankId));
        Assert.Equal("0", new MemoryBankId(0).ToString());

        foreach (int invalid in new[] { -1, 16, int.MinValue, int.MaxValue })
        {
            ArgumentOutOfRangeException exception = Assert.Throws<
                ArgumentOutOfRangeException>(() => new MemoryBankId(invalid));
            Assert.Equal("value", exception.ParamName);
        }

        Assert.Null(MemoryCapability.None.Bank);
        Assert.Null(MemoryCapability.None.BankId);
        Assert.Null(typeof(MemoryBankId).Assembly.GetType(
            "YAKSys_Hybrid_CPU.Core.Memory.MemoryBankResolution"));
        Assert.Equal(typeof(MemoryBankResolution),
            typeof(MemoryBankId).Assembly.GetType(
                "YAKSys_Hybrid_CPU.Core.Decoder.MemoryBankResolution"));
    }

    [Fact]
    public void LegacyPureResolverFallbackAndWidePositiveGeometryStayObservable()
    {
        Assert.Equal(0, MemoryBankRouting.ResolveBankId(0, 64, 16));
        Assert.Equal(1, MemoryBankRouting.ResolveBankId(64, 64, 16));
        Assert.Equal(15, MemoryBankRouting.ResolveBankId(15 * 64UL, 64, 16));
        Assert.Equal(0, MemoryBankRouting.ResolveBankId(16 * 64UL, 64, 16));

        Assert.Equal(1, MemoryBankRouting.ResolveBankId(4096, 0, 16));
        Assert.Equal(1, MemoryBankRouting.ResolveBankId(4096, -1, 16));
        Assert.Equal(1, MemoryBankRouting.ResolveBankId(4096, 4096, 0));
        Assert.Equal(1, MemoryBankRouting.ResolveBankId(4096, 4096, -1));
        Assert.Equal(1, MemoryBankRouting.ResolveBankId(4096, 0, 0));

        int nonRepresentableResolved =
            MemoryBankRouting.ResolveBankId(17 * 64UL, 64, 32);
        Assert.Equal(17, nonRepresentableResolved);
        Assert.True(MemoryBankRouting.IsResolvedSchedulerVisibleBankId(
            nonRepresentableResolved));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MemoryBankId(nonRepresentableResolved));
    }

    [Fact]
    public void SchedulerVisibleUnavailableAndGeometryClassificationRemainRaw()
    {
        Assert.False(MemoryBankRouting.IsResolvedSchedulerVisibleBankId(
            MemoryBankRouting.UninitializedSchedulerVisibleBankId));
        Assert.True(MemoryBankRouting.IsResolvedSchedulerVisibleBankId(0));
        Assert.True(MemoryBankRouting.IsResolvedSchedulerVisibleBankId(15));
        Assert.True(MemoryBankRouting.IsResolvedSchedulerVisibleBankId(16));

        string root = FindRepositoryRoot();
        string routing = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Memory", "LoadStore", "MemoryBankRouting.cs");
        Assert.Contains(
            "internal const int UninitializedSchedulerVisibleBankId = -1",
            routing, StringComparison.Ordinal);
        Assert.Contains(
            "internal static bool IsResolvedSchedulerVisibleBankId(int bankId) => bankId >= 0",
            routing, StringComparison.Ordinal);
        Assert.Contains(
            "Processor.Memory is { NumBanks: > 0, BankWidthBytes: > 0 } memory",
            routing, StringComparison.Ordinal);
        Assert.Contains("return UninitializedSchedulerVisibleBankId", routing,
            StringComparison.Ordinal);
        Assert.Contains("private const int DefaultNumBanks = 16", routing,
            StringComparison.Ordinal);
        Assert.Contains("private const int DefaultBankWidthBytes = 4096",
            routing, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankResolution", routing,
            StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidGeometry", routing,
            StringComparison.Ordinal);
        Assert.DoesNotContain("UnavailableTopology", routing,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ResolverWritersConsumersIndexesMasksAndAuthorityOwnersStayFrozen()
    {
        string root = FindRepositoryRoot();
        string loadStore = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Memory", "MicroOp.LoadStore.cs");
        string assist = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Assist", "AssistMicroOp.cs");
        string projection = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Decoder", "Rf06SpecializedCapabilityProjection.cs");
        string memoryHelpers = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Memory", "Subsystem", "MemorySubsystem.Helpers.cs");
        string admission = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Scheduling", "Admission",
            "MicroOpScheduler.Admission.cs");
        string scoreboard = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Scheduling", "Scoreboard",
            "MicroOpScheduler.Scoreboard.cs");
        string oracle = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Scheduling", "MicroOpScheduler.ShadowOracle.cs");

        Assert.Contains(
            "public int MemoryBankId => Core.Memory.MemoryBankRouting.ResolveSchedulerVisibleBankId(MemoryAddress)",
            loadStore, StringComparison.Ordinal);
        Assert.Contains(
            "MemoryBankId = ResolveMemoryBankId(BaseAddress)",
            assist, StringComparison.Ordinal);
        Assert.Contains(
            "MemoryBankRouting.IsResolvedSchedulerVisibleBankId(MemoryBankId)",
            assist, StringComparison.Ordinal);
        Assert.Contains("ResourceMaskBuilder.ForMemoryBank(MemoryBankId)",
            assist, StringComparison.Ordinal);

        Assert.Contains(
            "int bank = MemoryBankRouting.ResolveSchedulerVisibleBankId(footprint[0].Address)",
            projection, StringComparison.Ordinal);
        Assert.Contains(
            "!MemoryBankRouting.IsResolvedSchedulerVisibleBankId(bank)",
            projection, StringComparison.Ordinal);
        Assert.Contains("new MemoryBankId(bank)", projection,
            StringComparison.Ordinal);

        Assert.Contains("PhysicalMemoryBankIndex.FromRawValue(",
            memoryHelpers, StringComparison.Ordinal);
        Assert.Contains("Core.Memory.MemoryBankRouting.ResolveBankId(",
            memoryHelpers, StringComparison.Ordinal);
        Assert.Contains("bankQueues[bankId]", memoryHelpers,
            StringComparison.Ordinal);
        Assert.Contains("(uint)bankId >= (uint)NumBanks", memoryHelpers,
            StringComparison.Ordinal);

        Assert.Contains("(uint)bankId < 16", admission,
            StringComparison.Ordinal);
        Assert.Contains("(1 << bankId)", admission,
            StringComparison.Ordinal);
        Assert.Contains("if ((uint)bankId >= 16)", admission,
            StringComparison.Ordinal);
        Assert.Contains("_smtScoreboardBankId[virtualThreadId, i] == bankId",
            scoreboard, StringComparison.Ordinal);
        Assert.Contains("candidateBankId >= 0", oracle,
            StringComparison.Ordinal);
        Assert.Contains("candidateBankId < 16", oracle,
            StringComparison.Ordinal);
        Assert.Contains("new byte[MemoryBankId.BankCount]", oracle,
            StringComparison.Ordinal);
    }

    [Fact]
    public void WireTelemetryTestSupportAndCrossFamilySeamsRemainExplicit()
    {
        string root = FindRepositoryRoot();
        string testHelper = Read(root, "HybridCPU_ISE.Tests", "TestHelpers",
            "MicroOpTestHelper.cs");
        string telemetry = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "TypedSlotTelemetryProfile.cs");
        string exporter = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "TelemetryExporter.cs");
        string compilerReader = Read(root, "HybridCPU_Compiler", "Core", "IR",
            "Telemetry", "TelemetryProfileReader.cs");
        string assembler = JoinSources(
            Path.Combine(root, "TestAssemblerConsoleApps"));
        string bridge = JoinSources(Path.Combine(root, "CpuInterfaceBridge"));
        string replay = JoinSources(
            Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core",
                "Pipeline", "MicroOps", "Replay"),
            Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core",
                "Pipeline", "Certificates"),
            Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core",
                "Diagnostics", "ReplayEngine.cs"));

        Assert.Contains(
            "((memoryBankId % runtimeNumBanks) + runtimeNumBanks) % runtimeNumBanks",
            testHelper, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyDictionary<int, long>?", telemetry,
            StringComparison.Ordinal);
        Assert.Contains("new Dictionary<int, long>(16)", exporter,
            StringComparison.Ordinal);
        Assert.Contains("GetBankPendingRejectCount(int bankId)",
            compilerReader, StringComparison.Ordinal);
        Assert.Contains("rejectsPerBank.TryGetValue(bankId", compilerReader,
            StringComparison.Ordinal);

        Assert.Contains("(bankId & 0x7)", assembler,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankId", assembler,
            StringComparison.Ordinal);
        Assert.DoesNotMatch(FamilyPattern, bridge);
        Assert.DoesNotMatch(FamilyPattern, replay);
    }


    private static InventoryFingerprint CaptureRoot(
        string root,
        string sourceRoot)
    {
        var regex = new Regex(FamilyPattern, RegexOptions.CultureInvariant);
        var entries = new List<string>();
        string absoluteRoot = Path.Combine(root, sourceRoot);
        foreach (string path in EnumerateSources(absoluteRoot)
                     .Where(path =>
                         !path.EndsWith(ThisFile,
                             StringComparison.OrdinalIgnoreCase) &&
                         !path.EndsWith(Rf126bGuard,
                             StringComparison.OrdinalIgnoreCase) &&
                         !path.EndsWith(Rf126cGuard,
                             StringComparison.OrdinalIgnoreCase) &&
                         !path.EndsWith(Rf126dGuard,
                             StringComparison.OrdinalIgnoreCase) &&
                         !path.EndsWith(Rf126eGuard,
                             StringComparison.OrdinalIgnoreCase) &&
                         !path.EndsWith(Rf126fGuard,
                             StringComparison.OrdinalIgnoreCase) &&
                         !path.EndsWith(Rf126gGuard,
                             StringComparison.OrdinalIgnoreCase) &&
                         !path.EndsWith(Rf126hGuard,
                             StringComparison.OrdinalIgnoreCase) &&
                         !path.EndsWith(Rf126iGuard,
                             StringComparison.OrdinalIgnoreCase) &&
                         !path.EndsWith(Rf126jGuard,
                             StringComparison.OrdinalIgnoreCase) &&
                         !path.EndsWith(Rf126kGuard,
                             StringComparison.OrdinalIgnoreCase) &&
                         !path.EndsWith(Rf126lGuard,
                             StringComparison.OrdinalIgnoreCase) &&
                         !path.EndsWith(Rf126mGuard,
                             StringComparison.OrdinalIgnoreCase) &&
                         !path.EndsWith(Rf126nGuard,
                             StringComparison.OrdinalIgnoreCase) &&
                         !path.EndsWith(Rf126oGuard,
                             StringComparison.OrdinalIgnoreCase) &&
                         !path.EndsWith(Rf126pGuard,
                             StringComparison.OrdinalIgnoreCase) &&
                         !path.EndsWith(Rf126qGuard,
                             StringComparison.OrdinalIgnoreCase) &&
                         !path.EndsWith(Rf126rGuard,
                             StringComparison.OrdinalIgnoreCase) &&
                         !path.EndsWith(Rf126sGuard,
                             StringComparison.OrdinalIgnoreCase) &&
                         !path.EndsWith(Rf126tGuard,
                             StringComparison.OrdinalIgnoreCase) &&
                            !path.EndsWith(Rf126uGuard,
                                StringComparison.OrdinalIgnoreCase) &&
                            !path.EndsWith(Rf126vGuard,
                                StringComparison.OrdinalIgnoreCase) &&
                            !path.EndsWith(Rf126wGuard,
                                StringComparison.OrdinalIgnoreCase) &&
                            !path.EndsWith(Rf126xGuard,
                                StringComparison.OrdinalIgnoreCase) &&
                            !path.EndsWith(Rf126yGuard,
                                StringComparison.OrdinalIgnoreCase) &&
                            !path.EndsWith(Rf126zGuard,
                                StringComparison.OrdinalIgnoreCase) &&
                            !path.EndsWith(Rf126aaGuard,
                                StringComparison.OrdinalIgnoreCase) &&
                             !path.EndsWith(Rf126abGuard,
                                 StringComparison.OrdinalIgnoreCase) &&
                             !path.EndsWith(Rf126acGuard,
                                 StringComparison.OrdinalIgnoreCase) &&
                             !path.EndsWith(Rf126adGuard,
                                 StringComparison.OrdinalIgnoreCase) &&
                             !path.EndsWith(Rf126aeGuard,
                                 StringComparison.OrdinalIgnoreCase) &&
                             !path.EndsWith(Rf126afGuard,
                                 StringComparison.OrdinalIgnoreCase) &&
                             !path.EndsWith(Rf126agGuard,
                                 StringComparison.OrdinalIgnoreCase) &&
                             !path.EndsWith(Rf126ahGuard,
                                 StringComparison.OrdinalIgnoreCase) &&
                             !path.EndsWith(Rf126aiGuard,
                                 StringComparison.OrdinalIgnoreCase) &&
                             !path.EndsWith(Rf126ajGuard,
                                 StringComparison.OrdinalIgnoreCase) &&
                             !path.EndsWith(Rf126akGuard,
                                 StringComparison.OrdinalIgnoreCase) &&
                             !path.EndsWith(Rf126alGuard,
                                 StringComparison.OrdinalIgnoreCase) &&
                             !path.EndsWith(Rf126amGuard,
                                 StringComparison.OrdinalIgnoreCase) &&
                             !path.EndsWith(Rf126anGuard,
                                 StringComparison.OrdinalIgnoreCase) &&
                             !path.EndsWith(Rf126aoGuard,
                                 StringComparison.OrdinalIgnoreCase) &&
                             !path.EndsWith(Rf126apGuard,
                                 StringComparison.OrdinalIgnoreCase) &&
                             !path.EndsWith(Rf126asGuard,
                                 StringComparison.OrdinalIgnoreCase) &&
                             !path.EndsWith(Rf126auGuard,
                                 StringComparison.OrdinalIgnoreCase) &&
                             !path.EndsWith(Rf126awGuard,
                                 StringComparison.OrdinalIgnoreCase) &&
                             !path.EndsWith(Rf126axGuard,
                                 StringComparison.OrdinalIgnoreCase)))
        {
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            foreach (string line in File.ReadLines(path))
            {
                int count = regex.Matches(line).Count;
                for (int occurrence = 0; occurrence < count; occurrence++)
                {
                    entries.Add($"{relative}:{line.Trim()}");
                }
            }
        }

        entries.Sort(StringComparer.Ordinal);
        string joined = string.Join("\n", entries);
        string sha256 = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(joined)))
            .ToLowerInvariant();
        return new InventoryFingerprint(entries.Count, sha256);
    }

    private static string JoinSources(params string[] roots) =>
        string.Join(
            Environment.NewLine,
            roots.SelectMany(path =>
                Directory.Exists(path)
                    ? EnumerateSources(path)
                    : File.Exists(path)
                        ? [path]
                        : [])
                .Select(File.ReadAllText));

    private static IEnumerable<string> EnumerateSources(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !HasPathSegment(path, "bin") &&
                           !HasPathSegment(path, "obj"))
            .OrderBy(path => path, StringComparer.Ordinal);

    private static bool HasPathSegment(string path, string segment) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Contains(segment, StringComparer.OrdinalIgnoreCase);

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(parts.Aggregate(root, Path.Combine));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(
                    current.FullName,
                    "ResearchPaper",
                    "section",
                    "md base")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private readonly record struct InventoryFingerprint(
        int Count,
        string Sha256);
}
