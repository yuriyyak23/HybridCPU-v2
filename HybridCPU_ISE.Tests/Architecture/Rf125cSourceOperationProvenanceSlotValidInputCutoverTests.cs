using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.5c guards for the valid-input-only SourceOperationProvenance
/// source-slot cutover. Raw construction and the legacy numeric JSON property
/// remain compatibility boundaries with unchanged invalid-input behavior.
/// </summary>
public sealed class Rf125cSourceOperationProvenanceSlotValidInputCutoverTests
{
    [Fact]
    public void EveryValidSlotHasCheckedRawRecordAndJsonParity()
    {
        SemanticInstructionKey key = CreateKey();

        for (int raw = 0; raw < BundleMetadata.BundleSlotCount; raw++)
        {
            var checkedValue = new SourceOperationProvenance(
                key,
                2,
                17,
                SlotId.Create(raw),
                23);
            var compatibilityValue = new SourceOperationProvenance(
                key,
                2,
                17,
                raw,
                23);

            Assert.Equal(checkedValue, compatibilityValue);
            Assert.Equal(raw, checkedValue.SourceSlotIndex);
            Assert.Equal(SlotId.Create(raw), checkedValue.SourceSlotId);

            string checkedJson = JsonSerializer.Serialize(checkedValue);
            string compatibilityJson = JsonSerializer.Serialize(
                compatibilityValue);
            Assert.Equal(checkedJson, compatibilityJson);
            Assert.Contains($"\"SourceSlotIndex\":{raw}", checkedJson,
                StringComparison.Ordinal);
            Assert.DoesNotContain("\"SourceSlotId\"", checkedJson,
                StringComparison.Ordinal);

            SourceOperationProvenance? roundTrip =
                JsonSerializer.Deserialize<SourceOperationProvenance>(
                    checkedJson);
            Assert.NotNull(roundTrip);
            Assert.Equal(raw, roundTrip.SourceSlotIndex);
            Assert.Equal(SlotId.Create(raw), roundTrip.SourceSlotId);
            Assert.Equal(2, roundTrip.SourceVirtualThreadId);
            Assert.Equal(17UL, roundTrip.SourceBundleSerial);
            Assert.Equal(23UL, roundTrip.FetchEpoch);
        }
    }

    [Fact]
    public void RawCompatibilityBoundaryPreservesExactInvalidWinner()
    {
        ConstructorInfo[] constructors =
            typeof(SourceOperationProvenance).GetConstructors(
                BindingFlags.Public | BindingFlags.Instance);
        Assert.Equal(2, constructors.Length);

        ConstructorInfo rawConstructor = Assert.Single(
            constructors,
            candidate => candidate.GetParameters()[3].ParameterType ==
                         typeof(int));
        ConstructorInfo checkedConstructor = Assert.Single(
            constructors,
            candidate => candidate.GetParameters()[3].ParameterType ==
                         typeof(SlotId));
        Assert.NotNull(rawConstructor.GetCustomAttribute<JsonConstructorAttribute>());
        Assert.Null(checkedConstructor.GetCustomAttribute<JsonConstructorAttribute>());

        PropertyInfo rawProperty =
            typeof(SourceOperationProvenance).GetProperty(
                nameof(SourceOperationProvenance.SourceSlotIndex))!;
        PropertyInfo checkedProperty =
            typeof(SourceOperationProvenance).GetProperty(
                nameof(SourceOperationProvenance.SourceSlotId))!;
        Assert.Equal(typeof(int), rawProperty.PropertyType);
        Assert.Equal(typeof(SlotId), checkedProperty.PropertyType);
        Assert.NotNull(checkedProperty.GetCustomAttribute<JsonIgnoreAttribute>());

        SemanticInstructionKey key = CreateKey();
        foreach (int invalidSlot in new[]
                 {
                     -1,
                     BundleMetadata.BundleSlotCount,
                     int.MinValue,
                     int.MaxValue,
                 })
        {
            ArgumentOutOfRangeException exception = Assert.Throws<
                ArgumentOutOfRangeException>(() =>
                new SourceOperationProvenance(
                    key,
                    -1,
                    1,
                    invalidSlot,
                    1));
            Assert.Equal("sourceSlotIndex", exception.ParamName);
        }

        ArgumentOutOfRangeException invalidVt = Assert.Throws<
            ArgumentOutOfRangeException>(() =>
            new SourceOperationProvenance(key, -1, 1, 0, 1));
        Assert.Equal("sourceVirtualThreadId", invalidVt.ParamName);

        ArgumentOutOfRangeException checkedInvalidVt = Assert.Throws<
            ArgumentOutOfRangeException>(() =>
            new SourceOperationProvenance(key, -1, 1, SlotId.Zero, 1));
        Assert.Equal("sourceVirtualThreadId", checkedInvalidVt.ParamName);
    }

    [Fact]
    public void ProductionWriterAndSchedulerValidReadsUseCheckedSlotId()
    {
        string root = FindRepositoryRoot();
        string contracts = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Decoder", "Rf06ExecutionContracts.cs");
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Scheduling", "Fsp",
            "CPU_Core.PipelineExecution.Fsp.cs");
        string routing = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Scheduling", "Rf06ScalarSchedulerRouting.cs");

        Assert.Contains("[JsonConstructor]", contracts,
            StringComparison.Ordinal);
        Assert.Contains("ValidateRawSourceSlotIndex(sourceSlotIndex)",
            contracts, StringComparison.Ordinal);
        Assert.Contains("[JsonIgnore]\n    public SlotId SourceSlotId",
            contracts.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("public int SourceSlotIndex => SourceSlotId",
            contracts, StringComparison.Ordinal);
        Assert.Contains(
            "(uint)sourceSlotIndex >= BundleMetadata.BundleSlotCount",
            contracts, StringComparison.Ordinal);
        Assert.Contains(
            "throw new ArgumentOutOfRangeException(nameof(sourceSlotIndex))",
            contracts, StringComparison.Ordinal);

        Assert.Equal(1, Count(fsp, "Core.SlotId.Create(entry.SlotIndex),"));
        Assert.Equal(2, Count(routing, "provenance.SourceSlotId"));
        Assert.Contains(
            "(uint)scheduledOperation.Admission.SourceProvenance.SourceSlotIndex >= BundleMetadata.BundleSlotCount",
            Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
                "Architecture", "Registers", "Retire",
                "Rf08RetireEffectIdentityContracts.cs"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ValidTestCallersUseCheckedConstructionAndRawCallsAreInvalidProbes()
    {
        string root = FindRepositoryRoot();
        string testsRoot = Path.Combine(root, "HybridCPU_ISE.Tests");
        string[] sourceFiles = Directory.EnumerateFiles(
                testsRoot,
                "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !HasPathSegment(path, "bin") &&
                           !HasPathSegment(path, "obj") &&
                           !path.EndsWith(
                               nameof(Rf125cSourceOperationProvenanceSlotValidInputCutoverTests) +
                               ".cs",
                               StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains(
                "new SourceOperationProvenance(",
                StringComparison.Ordinal))
            .ToArray();

        foreach (string path in sourceFiles)
        {
            string source = File.ReadAllText(path);
            if (path.EndsWith(
                    "Rf125SourceOperationProvenanceSlotInventoryTests.cs",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Assert.Contains("SlotId.", source, StringComparison.Ordinal);
        }

        string allProduction = JoinSources(Path.Combine(root, "HybridCPU_ISE"));
        string compiler = JoinSources(Path.Combine(root, "HybridCPU_Compiler"));
        string bridge = JoinSources(Path.Combine(root, "CpuInterfaceBridge"));
        string assembler = JoinSources(
            Path.Combine(root, "TestAssemblerConsoleApps"));
        Assert.Equal(
            1,
            Count(
                allProduction,
                "SourceOperationProvenance provenance = new("));
        Assert.DoesNotContain("SourceOperationProvenance", compiler,
            StringComparison.Ordinal);
        Assert.DoesNotContain("SourceOperationProvenance", bridge,
            StringComparison.Ordinal);
        Assert.DoesNotContain("SourceOperationProvenance", assembler,
            StringComparison.Ordinal);
    }


    private static SemanticInstructionKey CreateKey() =>
        SemanticInstructionKey.Create(
            [1, 2, 3],
            "rf12.5c",
            CanonicalDecodeContext.Unbound);

    private static int Count(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(
                   value,
                   offset,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static string JoinSources(string root) =>
        Directory.Exists(root)
            ? string.Join(
                Environment.NewLine,
                Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                    .Where(path => !HasPathSegment(path, "bin") &&
                                   !HasPathSegment(path, "obj"))
                    .Select(File.ReadAllText))
            : string.Empty;

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
}
