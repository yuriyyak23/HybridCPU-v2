using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.5a zero-caller core representation contract for canonical/working
/// bundle positions only. Physical lanes, pinning and existing raw callers are
/// deliberately outside this slice.
/// </summary>
public sealed class Rf125aBundleSlotIdCoreValidInputContractTests
{
    [Fact]
    public void EveryValidBundlePositionHasExactSignatureAndWireParity()
    {
        Assert.Equal(BundleMetadata.BundleSlotCount, SlotId.SlotCount);
        Assert.Equal(0, SlotId.MinValue);
        Assert.Equal(7, SlotId.MaxValue);
        Assert.Equal(SlotId.MinValue, SlotId.Zero.Value);
        Assert.Equal(default, SlotId.Zero);

        for (int rawValue = SlotId.MinValue;
             rawValue <= SlotId.MaxValue;
             rawValue++)
        {
            Assert.True(SlotId.IsRepresentable(rawValue));

            SlotId fromConstructor = new((byte)rawValue);
            SlotId fromCreate = SlotId.Create(rawValue);
            SlotId fromWire = SlotId.FromRawValue((byte)rawValue);
            SlotId fromCast = (SlotId)rawValue;

            Assert.True(SlotId.TryCreate(rawValue, out SlotId fromTryCreate));
            Assert.Equal(fromConstructor, fromCreate);
            Assert.Equal(fromConstructor, fromWire);
            Assert.Equal(fromConstructor, fromCast);
            Assert.Equal(fromConstructor, fromTryCreate);
            Assert.Equal((byte)rawValue, fromWire.ToRawValue());
            Assert.Equal(rawValue, (int)fromWire);
            Assert.Equal($"slot{rawValue}", fromWire.ToString());

            string json = JsonSerializer.Serialize(fromWire);
            Assert.Equal(fromWire, JsonSerializer.Deserialize<SlotId>(json));
        }
    }

    [Fact]
    public void ContractRejectsNonPositionsWithoutChangingExistingRawSurfaces()
    {
        Assert.False(SlotId.IsRepresentable(-1));
        Assert.False(SlotId.IsRepresentable(SlotId.SlotCount));
        Assert.False(SlotId.IsRepresentable(int.MinValue));
        Assert.False(SlotId.IsRepresentable(int.MaxValue));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SlotId((byte)SlotId.SlotCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => SlotId.Create(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SlotId.Create(SlotId.SlotCount));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SlotId.FromRawValue((byte)SlotId.SlotCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => (SlotId)(-1));

        Assert.False(SlotId.TryCreate(-1, out SlotId negativeResult));
        Assert.Equal(default, negativeResult);
        Assert.Equal(SlotId.MinValue, negativeResult.Value);
        Assert.False(SlotId.TryCreate(
            SlotId.SlotCount,
            out SlotId highResult));
        Assert.Equal(default, highResult);
        Assert.Equal(SlotId.MinValue, highResult.Value);

        Assert.Equal(typeof(int),
            typeof(SourceOperationProvenance).GetProperty(
                nameof(SourceOperationProvenance.SourceSlotIndex))!.PropertyType);
        Assert.Equal(typeof(int),
            typeof(VliwOperationId).GetProperty(
                nameof(VliwOperationId.WorkingSlotIndex))!.PropertyType);
        Assert.Equal(typeof(int),
            typeof(ScheduledOperation).GetProperty(
                nameof(ScheduledOperation.PhysicalLane))!.PropertyType);
        Assert.Equal(typeof(byte),
            typeof(SlotPlacementMetadata).GetField(
                nameof(SlotPlacementMetadata.PinnedLaneId))!.FieldType);
    }

    [Fact]
    public void NewSignaturesHaveOnlyRf125cProductionAndZeroExternalCallers()
    {
        string root = FindRepositoryRoot();
        string contractPath = Path.GetFullPath(Path.Combine(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Architecture",
            "BinaryFormat",
            "SlotEncoding",
            "SlotId.cs"));
        string production = JoinSources(
            Path.Combine(root, "HybridCPU_ISE"),
            contractPath);
        string compiler = JoinSources(Path.Combine(root, "HybridCPU_Compiler"));
        string testAssembler = JoinSources(
            Path.Combine(root, "TestAssemblerConsoleApps"));
        Assert.Equal(2, Regex.Matches(
            production,
            @"\bSlotId\.Create\s*\(").Count);
        Assert.Contains("ValidateRawSourceSlotIndex", production,
            StringComparison.Ordinal);
        Assert.Contains("Core.SlotId.Create(entry.SlotIndex)", production,
            StringComparison.Ordinal);
        Assert.DoesNotMatch(
            @"\bSlotId\.(?:TryCreate|FromRawValue|IsRepresentable)\s*\(",
            production);
        Assert.DoesNotMatch(@"\bnew\s+SlotId\s*\(", production);

        foreach (string external in new[] { compiler, testAssembler })
        {
            Assert.DoesNotMatch(
                @"\bSlotId\.(?:Create|TryCreate|FromRawValue|IsRepresentable)\s*\(",
                external);
            Assert.DoesNotMatch(@"\bnew\s+SlotId\s*\(", external);
            Assert.DoesNotMatch(
                @"\bSlotId\b\s+\w+\s*(?:[),;={]|=>)",
                external);
        }

        Assert.Equal(1, Regex.Matches(
            string.Join(Environment.NewLine, production, compiler, testAssembler),
            @"\b(?:record\s+struct|struct|class)\s+LaneId\b").Count);
    }

    [Fact]
    public void PublicShapeIsRepresentationOnlyAndCannotEncodeCursorOrAbsence()
    {
        ConstructorInfo constructor = Assert.Single(
            typeof(SlotId).GetConstructors(BindingFlags.Public |
                                           BindingFlags.Instance));
        ParameterInfo parameter = Assert.Single(constructor.GetParameters());
        Assert.Equal(typeof(byte), parameter.ParameterType);
        Assert.Equal(typeof(byte), typeof(SlotId).GetProperty(
            nameof(SlotId.Value))!.PropertyType);

        Assert.True(SlotId.IsRepresentable(0));
        Assert.True(SlotId.IsRepresentable(7));
        Assert.False(SlotId.IsRepresentable(8));

        string root = FindRepositoryRoot();
        string contract = File.ReadAllText(Path.Combine(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Architecture",
            "BinaryFormat",
            "SlotEncoding",
            "SlotId.cs"));
        Assert.Contains("This is not a legality, occupancy or placement check",
            contract, StringComparison.Ordinal);
        Assert.DoesNotContain("LaneId", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("Pinned", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("Cursor", contract, StringComparison.Ordinal);
    }


    private static string JoinSources(
        string sourceRoot,
        string? excludedPath = null)
    {
        string? normalizedExcluded = excludedPath is null
            ? null
            : Path.GetFullPath(excludedPath);
        return string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path =>
                    !IsGeneratedPath(path) &&
                    (normalizedExcluded is null ||
                     !string.Equals(
                         Path.GetFullPath(path),
                         normalizedExcluded,
                         StringComparison.OrdinalIgnoreCase)))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(File.ReadAllText));
    }

    private static bool IsGeneratedPath(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

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
}
