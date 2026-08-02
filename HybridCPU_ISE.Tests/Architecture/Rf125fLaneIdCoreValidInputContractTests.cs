using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf125fLaneIdCoreValidInputContractTests
{
    [Fact]
    public void EveryValidPhysicalLaneHasExactConstructionProjectionAndWireParity()
    {
        Assert.Equal(8, LaneId.LaneCount);
        Assert.Equal(0, LaneId.MinValue);
        Assert.Equal(7, LaneId.MaxValue);
        Assert.Equal(LaneId.MinValue, LaneId.Zero.Value);
        Assert.Equal(default, LaneId.Zero);

        for (int rawValue = LaneId.MinValue; rawValue <= LaneId.MaxValue; rawValue++)
        {
            Assert.True(LaneId.IsRepresentable(rawValue));

            LaneId fromConstructor = new((byte)rawValue);
            LaneId fromCreate = LaneId.Create(rawValue);
            LaneId fromWire = LaneId.FromRawValue((byte)rawValue);
            LaneId fromCast = (LaneId)rawValue;

            Assert.True(LaneId.TryCreate(rawValue, out LaneId fromTryCreate));
            Assert.Equal(fromConstructor, fromCreate);
            Assert.Equal(fromConstructor, fromWire);
            Assert.Equal(fromConstructor, fromCast);
            Assert.Equal(fromConstructor, fromTryCreate);
            Assert.Equal((byte)rawValue, fromWire.ToRawValue());
            Assert.Equal(rawValue, (int)fromWire);
            Assert.Equal($"lane{rawValue}", fromWire.ToString());
            Assert.Equal(fromWire, JsonSerializer.Deserialize<LaneId>(JsonSerializer.Serialize(fromWire)));
        }
    }

    [Fact]
    public void NewContractRejectsNonLanesWithoutChangingRawPlacementOrSchedulerBehavior()
    {
        Assert.False(LaneId.IsRepresentable(-1));
        Assert.False(LaneId.IsRepresentable(LaneId.LaneCount));
        Assert.False(LaneId.IsRepresentable(int.MinValue));
        Assert.False(LaneId.IsRepresentable(int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LaneId((byte)LaneId.LaneCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => LaneId.Create(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => LaneId.FromRawValue((byte)LaneId.LaneCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => (LaneId)(-1));

        Assert.False(LaneId.TryCreate(-1, out LaneId negative));
        Assert.Equal(default, negative);
        Assert.Equal(LaneId.MinValue, negative.Value);
        Assert.False(LaneId.TryCreate(LaneId.LaneCount, out LaneId high));
        Assert.Equal(default, high);
        Assert.Equal(LaneId.MinValue, high.Value);

        Assert.Equal(typeof(byte), typeof(SlotPlacementMetadata).GetField(
            nameof(SlotPlacementMetadata.PinnedLaneId))!.FieldType);
        string root = FindRepositoryRoot();
        string scheduler = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Admission", "MicroOpScheduler.Admission.cs"));
        Assert.Contains("LaneId.TryCreate(candidate.Placement.PinnedLaneId", scheduler, StringComparison.Ordinal);
        Assert.Contains("int lane = laneId", scheduler, StringComparison.Ordinal);
        Assert.Contains("1 << lane", scheduler, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicShapeHasOnlyTheApprovedExecutionPlacementProjectionConsumer()
    {
        ConstructorInfo constructor = Assert.Single(typeof(LaneId).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(typeof(byte), Assert.Single(constructor.GetParameters()).ParameterType);

        string root = FindRepositoryRoot();
        string contractPath = Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "LaneId.cs"));
        string production = JoinSources(Path.Combine(root, "HybridCPU_ISE"), contractPath);
        string compiler = JoinSources(Path.Combine(root, "HybridCPU_Compiler"));
        string testAssembler = JoinSources(Path.Combine(root, "TestAssemblerConsoleApps"));

        Assert.Equal(1, Regex.Matches(production,
            @"\bLaneId\.TryCreate\s*\(\s*PinnedLaneId\s*,\s*out\s+laneId\s*\)").Count);
        Assert.Equal(1, Regex.Matches(production,
            @"\blaneId\.ToRawValue\s*\(").Count);

        foreach (string external in new[] { compiler, testAssembler })
        {
            Assert.DoesNotMatch(@"\bLaneId\.(?:Create|TryCreate|FromRawValue|IsRepresentable)\s*\(", external);
            Assert.DoesNotMatch(@"\bnew\s+LaneId\s*\(", external);
            Assert.DoesNotMatch(@"\bLaneId\b\s+\w+\s*(?:[),;={]|=>)", external);
        }

        string contract = File.ReadAllText(contractPath);
        Assert.Contains("This is not a pinning, legality, occupancy or placement check", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotId", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("PinnedLaneId", contract, StringComparison.Ordinal);
    }

    private static string JoinSources(string sourceRoot, string? excludedPath = null)
    {
        string? normalizedExcluded = excludedPath is null ? null : Path.GetFullPath(excludedPath);
        return string.Join(Environment.NewLine, Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGenerated(path) && (normalizedExcluded is null || !string.Equals(Path.GetFullPath(path), normalizedExcluded, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).Select(File.ReadAllText));
    }

    private static bool IsGenerated(string path) => path.Replace('\\', '/').Contains("/bin/", StringComparison.OrdinalIgnoreCase) || path.Replace('\\', '/').Contains("/obj/", StringComparison.OrdinalIgnoreCase);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE"))) return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException();
    }
}
