using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.5 decision-only closed-world inventory guards. Bundle-slot position,
/// post-Stage-B physical lane and the pinning discriminator are deliberately
/// frozen as separate raw contours. These guards authorize no migration or
/// invalid-input change.
/// </summary>
public sealed class Rf125SlotPhysicalLanePinningInventoryTests
{
    private const string ThisFile =
        "Rf125SlotPhysicalLanePinningInventoryTests.cs";
    private const string Rf125aGuardFile =
        "Rf125aBundleSlotIdCoreValidInputContractTests.cs";
    private const string Rf125bGuardFile =
        "Rf125bSourceOperationProvenanceSlotInventoryTests.cs";
    private const string Rf125cGuardFile =
        "Rf125cSourceOperationProvenanceSlotValidInputCutoverTests.cs";

    private const string FamilyPattern =
        @"\b(?:SlotId|SlotID|SlotIndex|slotIndex|WorkingSlotIndex|SourceSlotIndex|LaneId|LaneID|LaneIndex|laneIndex|PhysicalLane|physicalLane|PinnedLaneId|PinnedSlot|IssueSlot|OccupiedLaneIndex)\b";



    [Fact]
    public void SlotAndPhysicalLaneValidationOwnersRemainSeparate()
    {
        SemanticInstructionKey key = SemanticInstructionKey.Create(
            [1, 2, 5],
            "rf125",
            CanonicalDecodeContext.Unbound);
        var provenance = new SourceOperationProvenance(
            key,
            0,
            1,
            SlotId.Zero,
            1);
        Assert.Equal(0, provenance.SourceSlotIndex);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SourceOperationProvenance(key, 0, 1, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SourceOperationProvenance(key, 0, 1, 8, 1));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExecutionPlacement.Create(
                SlotClass.SystemSingleton,
                SlotPinningKind.HardPinned,
                8));

        string root = FindRepositoryRoot();
        string contracts = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Decoder", "Rf06ExecutionContracts.cs");
        Assert.Contains("(uint)workingSlotIndex >= BundleMetadata.BundleSlotCount",
            contracts, StringComparison.Ordinal);
        Assert.Contains("(uint)physicalLane >= BundleMetadata.BundleSlotCount",
            contracts, StringComparison.Ordinal);
        Assert.Contains("CreateAfterStageB(", contracts, StringComparison.Ordinal);
        Assert.Contains("VliwOperationId.Issue(", contracts, StringComparison.Ordinal);
    }

    [Fact]
    public void PinningDiscriminatorZeroAliasesAndUncheckedRawSeamsRemainFrozen()
    {
        ExecutionPlacement flexible = ExecutionPlacement.Create(
            SlotClass.AluClass,
            SlotPinningKind.ClassFlexible,
            byte.MaxValue);
        Assert.Equal(SlotPinningKind.ClassFlexible, flexible.PinningKind);
        Assert.Equal(0, flexible.PinnedLaneId);

        ExecutionPlacement pinnedZero = ExecutionPlacement.Create(
            SlotClass.AluClass,
            SlotPinningKind.HardPinned,
            0);
        Assert.Equal(SlotPinningKind.HardPinned, pinnedZero.PinningKind);
        Assert.Equal(0, pinnedZero.PinnedLaneId);

        var uncheckedRaw = new SlotPlacementMetadata
        {
            RequiredSlotClass = SlotClass.SystemSingleton,
            PinningKind = SlotPinningKind.HardPinned,
            PinnedLaneId = byte.MaxValue
        };
        Assert.Equal(byte.MaxValue, uncheckedRaw.PinnedLaneId);

        string root = FindRepositoryRoot();
        string placement = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Scheduling", "SlotPlacementMetadata.cs");
        string microOp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Types", "MicroOp.cs");
        string descriptor = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Decoder", "DecodedBundleDescriptor.cs");
        string custom = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Types", "MicroOp.Misc.cs");

        Assert.Contains("PinningKind       = SlotPinningKind.ClassFlexible",
            placement, StringComparison.Ordinal);
        Assert.Contains("PinnedLaneId      = 0", placement,
            StringComparison.Ordinal);
        Assert.Contains("protected void SetHardPinnedPlacement", microOp,
            StringComparison.Ordinal);
        Assert.DoesNotContain("pinnedLaneId >= BundleMetadata.BundleSlotCount",
            microOp, StringComparison.Ordinal);
        Assert.Contains("PinningKind = SlotPinningKind.HardPinned", descriptor,
            StringComparison.Ordinal);
        Assert.Contains("PinnedLaneId = 0", descriptor, StringComparison.Ordinal);
        Assert.Contains("SetPlacement(SlotClass.Unclassified, SlotPinningKind.HardPinned)",
            custom,
            StringComparison.Ordinal);
    }


    [Fact]
    public void CompilerWireReplayCertificateTelemetryAndDiagnosticSeamsRemainFrozen()
    {
        string root = FindRepositoryRoot();
        string irSlot = Read(root, "HybridCPU_Compiler", "Core", "IR", "Model",
            "IrMaterializedBundleSlot.cs");
        string lowerer = Read(root, "HybridCPU_Compiler", "Core", "IR",
            "Bundling", "HybridCpuBundleLowerer.cs");
        string contracts = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Decoder", "Rf06ExecutionContracts.cs");
        string retireIdentity = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Architecture", "Registers", "Retire",
            "Rf08RetireEffectIdentityContracts.cs");
        string certificate = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Certificates", "BundleResourceCertificate4Way.cs");
        string telemetry = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "TelemetryExporter.cs");
        string trace = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "TraceSink.cs");

        Assert.Contains("int SlotIndex", irSlot, StringComparison.Ordinal);
        Assert.Contains("IrIssueSlotMask PhysicalSlotMask", irSlot,
            StringComparison.Ordinal);
        Assert.Contains("slotMetadata[slot.SlotIndex]", lowerer,
            StringComparison.Ordinal);
        Assert.Contains("new SlotPlacementMetadata", lowerer,
            StringComparison.Ordinal);
        Assert.Contains("projection.Placement.PinnedLaneId", contracts,
            StringComparison.Ordinal);
        Assert.Contains("public int WorkingSlotIndex", retireIdentity,
            StringComparison.Ordinal);
        Assert.Contains("public int PhysicalLaneIndex", retireIdentity,
            StringComparison.Ordinal);
        Assert.Contains("public int SourceSlotIndex", retireIdentity,
            StringComparison.Ordinal);
        Assert.Contains("SlotClassLaneMap.GetLaneMask(slotClass)", certificate,
            StringComparison.Ordinal);
        Assert.Contains("new Dictionary<SlotClass, long>", telemetry,
            StringComparison.Ordinal);
        Assert.Contains("public SlotPinningKind PinningKind", trace,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReflectionDiagnosticsAndTestSupportMutationSeamsRemainVisible()
    {
        Assert.True(typeof(SlotPlacementMetadata).IsValueType);
        Assert.NotNull(typeof(MicroOp).GetProperty(nameof(MicroOp.Placement),
            BindingFlags.Public | BindingFlags.Instance));
        Assert.True(typeof(MicroOp).GetProperty(nameof(MicroOp.Placement))!.CanWrite);

        string root = FindRepositoryRoot();
        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Core", "CPU_Core.TestSupport.cs");
        string tests = JoinSources(Path.Combine(root, "HybridCPU_ISE.Tests"));

        Assert.Contains("pipeEX.SetLane(laneIndex, lane)", testSupport,
            StringComparison.Ordinal);
        Assert.Contains("pipeMEM.SetLane(laneIndex, lane)", testSupport,
            StringComparison.Ordinal);
        Assert.Contains("pipeWB.SetLane(laneIndex, lane)", testSupport,
            StringComparison.Ordinal);
        Assert.Contains("occupiedLaneMap[laneIndex]", testSupport,
            StringComparison.Ordinal);
        Assert.Contains("GetProperty(nameof(MicroOp.Placement))", tests,
            StringComparison.Ordinal);
        Assert.Contains("PinnedLaneId", tests, StringComparison.Ordinal);
    }


    private static InventoryFingerprint CaptureRoot(string root, string sourceRoot)
    {
        var regex = new Regex(FamilyPattern, RegexOptions.CultureInvariant);
        var entries = new List<string>();
        string absoluteRoot = Path.Combine(root, sourceRoot);
        foreach (string path in EnumerateSources(absoluteRoot)
                     .Where(path =>
                         !path.EndsWith(ThisFile,
                             StringComparison.OrdinalIgnoreCase) &&
                         !path.EndsWith(Rf125aGuardFile,
                             StringComparison.OrdinalIgnoreCase) &&
                         !path.EndsWith(Rf125bGuardFile,
                             StringComparison.OrdinalIgnoreCase) &&
                         !path.EndsWith(Rf125cGuardFile,
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
        string.Join("\n", roots.SelectMany(EnumerateSources).Select(File.ReadAllText));

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
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private readonly record struct InventoryFingerprint(int Count, string Sha256);
}
