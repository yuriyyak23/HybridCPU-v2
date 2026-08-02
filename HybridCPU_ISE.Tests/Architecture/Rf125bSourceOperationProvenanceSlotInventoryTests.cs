using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.5b decision-only closed-world inventory of the immutable
/// SourceOperationProvenance source/canonical bundle-slot role. These guards
/// authorize no production migration or invalid-input change.
/// </summary>
public sealed class Rf125bSourceOperationProvenanceSlotInventoryTests
{
    private const string ThisFile =
        "Rf125bSourceOperationProvenanceSlotInventoryTests.cs";
    private const string Rf125cGuardFile =
        "Rf125cSourceOperationProvenanceSlotValidInputCutoverTests.cs";
    private const string FamilyPattern =
        @"\b(?:SourceOperationProvenance|SourceSlotIndex|sourceSlotIndex)\b";


    [Fact]
    public void PublicRecordHasOneCheckedRawConstructorAndNoSlotAbsenceAlias()
    {
        ConstructorInfo[] constructors =
            typeof(SourceOperationProvenance).GetConstructors(
                BindingFlags.Public | BindingFlags.Instance);
        Assert.Equal(2, constructors.Length);
        ConstructorInfo constructor = Assert.Single(
            constructors,
            candidate => candidate.GetParameters()[3].ParameterType ==
                         typeof(int));
        ParameterInfo sourceSlotParameter = Assert.Single(
            constructor.GetParameters(),
            parameter => parameter.Name == "sourceSlotIndex");
        Assert.Equal(typeof(int), sourceSlotParameter.ParameterType);

        PropertyInfo property = typeof(SourceOperationProvenance).GetProperty(
            nameof(SourceOperationProvenance.SourceSlotIndex))!;
        Assert.Equal(typeof(int), property.PropertyType);
        Assert.False(property.CanWrite);
        PropertyInfo checkedProperty =
            typeof(SourceOperationProvenance).GetProperty(
                nameof(SourceOperationProvenance.SourceSlotId))!;
        Assert.Equal(typeof(SlotId), checkedProperty.PropertyType);
        Assert.False(checkedProperty.CanWrite);

        SemanticInstructionKey key = SemanticInstructionKey.Create(
            [1, 2, 3],
            "rf12.5b",
            CanonicalDecodeContext.Unbound);
        var zero = new SourceOperationProvenance(
            key,
            0,
            1,
            SlotId.Zero,
            1);
        var maximum = new SourceOperationProvenance(
            key,
            0,
            1,
            SlotId.Create(7),
            1);
        Assert.Equal(0, zero.SourceSlotIndex);
        Assert.Equal(7, maximum.SourceSlotIndex);
        Assert.Equal(SlotId.Zero, zero.SourceSlotId);
        Assert.Equal(SlotId.Create(7), maximum.SourceSlotId);

        ArgumentOutOfRangeException negative = Assert.Throws<
            ArgumentOutOfRangeException>(() =>
            new SourceOperationProvenance(key, -1, 1, -1, 1));
        Assert.Equal("sourceSlotIndex", negative.ParamName);
        ArgumentOutOfRangeException high = Assert.Throws<
            ArgumentOutOfRangeException>(() =>
            new SourceOperationProvenance(key, -1, 1, 8, 1));
        Assert.Equal("sourceSlotIndex", high.ParamName);
        ArgumentOutOfRangeException invalidVt = Assert.Throws<
            ArgumentOutOfRangeException>(() =>
            new SourceOperationProvenance(key, -1, 1, 0, 1));
        Assert.Equal("sourceVirtualThreadId", invalidVt.ParamName);
    }

    [Fact]
    public void SoleWriterAndReaderProjectionContoursRemainClosed()
    {
        string root = FindRepositoryRoot();
        string productionRoot = Path.Combine(root, "HybridCPU_ISE");
        string[] directFiles = EnumerateSources(productionRoot)
            .Where(path =>
            {
                string source = File.ReadAllText(path);
                return source.Contains("SourceOperationProvenance",
                           StringComparison.Ordinal) ||
                       source.Contains("SourceSlotIndex",
                           StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .ToArray();

        Assert.Equal(
            [
                "HybridCPU_ISE/CloseToHSL/Core/Architecture/Registers/Retire/Rf08RetireEffectIdentityContracts.cs",
                "HybridCPU_ISE/CloseToHSL/Core/Decoder/Rf06ExecutionContracts.cs",
                "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Fsp/CPU_Core.PipelineExecution.Fsp.cs",
                "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Rf06ScalarSchedulerRouting.cs",
            ],
            directFiles);

        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Scheduling", "Fsp",
            "CPU_Core.PipelineExecution.Fsp.cs");
        string routing = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Scheduling", "Rf06ScalarSchedulerRouting.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Architecture", "Registers", "Retire",
            "Rf08RetireEffectIdentityContracts.cs");

        Assert.Equal(1, Regex.Matches(
            fsp,
            @"\bSourceOperationProvenance\s+provenance\s*=\s*new\s*\(").Count);
        Assert.Contains("canonical = canonicalBundle.GetSlot(entry.SlotIndex)",
            fsp, StringComparison.Ordinal);
        Assert.Contains("Core.SlotId.Create(entry.SlotIndex),", fsp,
            StringComparison.Ordinal);
        Assert.Contains("catch (InvalidOperationException)", fsp,
            StringComparison.Ordinal);

        Assert.Contains(
            "canonical.SlotIndex != provenance.SourceSlotId",
            routing, StringComparison.Ordinal);
        Assert.Contains("provenance.SourceSlotId,", routing,
            StringComparison.Ordinal);
        Assert.Contains("(uint)workingSlotIndex >= BundleMetadata.BundleSlotCount",
            routing, StringComparison.Ordinal);

        Assert.Contains(
            "public int SourceSlotIndex => SourceProvenance.SourceSlotIndex",
            retire, StringComparison.Ordinal);
        Assert.Contains(
            "(uint)scheduledOperation.Admission.SourceProvenance.SourceSlotIndex >= BundleMetadata.BundleSlotCount",
            retire, StringComparison.Ordinal);
    }

    [Fact]
    public void PairMaskLocalsAndPhysicalLaneRolesDoNotConflateWithProvenance()
    {
        string root = FindRepositoryRoot();
        string descriptor = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Decoder", "DecodedBundleDescriptor.cs");
        string legality = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Legality", "BundleLegalityAnalyzer.cs");
        string candidateView = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Core", "Pipeline", "Core",
            "RuntimeClusterAdmissionPreparation.CandidateView.cs");
        string contracts = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Decoder", "Rf06ExecutionContracts.cs");

        foreach (string pairSource in new[]
                 {
                     descriptor,
                     legality,
                     candidateView,
                 })
        {
            Assert.Contains("sourceSlotIndex", pairSource,
                StringComparison.Ordinal);
            Assert.DoesNotContain("SourceOperationProvenance", pairSource,
                StringComparison.Ordinal);
            Assert.DoesNotContain("SourceSlotIndex", pairSource,
                StringComparison.Ordinal);
        }

        string provenanceRecord = Slice(
            contracts,
            "public sealed record SourceOperationProvenance",
            "public sealed record AdmissionRecord");
        Assert.DoesNotContain("PhysicalLane", provenanceRecord,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Pinned", provenanceRecord,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBank", provenanceRecord,
            StringComparison.Ordinal);
        Assert.DoesNotContain("LaneId", provenanceRecord,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NoIndexShiftKeyClampModuloSerializationOrMutationBypassAppears()
    {
        string root = FindRepositoryRoot();
        string production = JoinSources(Path.Combine(root, "HybridCPU_ISE"));
        string direct = string.Join(
            Environment.NewLine,
            EnumerateSources(Path.Combine(root, "HybridCPU_ISE"))
                .Where(path =>
                {
                    string source = File.ReadAllText(path);
                    return source.Contains("SourceOperationProvenance",
                               StringComparison.Ordinal) ||
                           source.Contains("SourceSlotIndex",
                               StringComparison.Ordinal);
                })
                .Select(File.ReadAllText));

        Assert.DoesNotMatch(@"\[[^\]\r\n]*SourceSlotIndex", direct);
        Assert.DoesNotMatch(@"(?:<<|>>)[^\r\n]*SourceSlotIndex", direct);
        Assert.DoesNotMatch(@"SourceSlotIndex[^\r\n]*%", direct);
        Assert.DoesNotMatch(@"Math\.Clamp[^\r\n]*SourceSlotIndex", direct);
        Assert.DoesNotMatch(@"Dictionary<[^\r\n]*SourceSlotIndex", direct);
        Assert.DoesNotMatch(@"SourceSlotIndex\s*(?:==|!=)\s*0", direct);
        Assert.DoesNotMatch(@"SourceSlotIndex\s*\?\?\s*0", direct);
        Assert.DoesNotMatch(
            @"JsonSerializer\.(?:Serialize|Deserialize)[^\r\n]*(?:SourceOperationProvenance|SourceSlotIndex)",
            production);
        Assert.DoesNotMatch(
            @"(?:GetField|GetProperty|SetValue)[^\r\n]*SourceSlotIndex",
            production);

        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Core", "CPU_Core.TestSupport.cs");
        Assert.DoesNotContain("SourceOperationProvenance", testSupport,
            StringComparison.Ordinal);
        Assert.DoesNotContain("SourceSlotIndex", testSupport,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReplayCertificateTelemetryAndExternalWireCallersRemainAbsent()
    {
        string root = FindRepositoryRoot();
        string replay = JoinSources(
            Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core",
                "Pipeline", "MicroOps", "Replay"),
            Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core",
                "Diagnostics", "ReplayEngine.cs"));
        string certificates = JoinSources(Path.Combine(
            root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Certificates"));
        string telemetry = JoinSources(Path.Combine(
            root, "HybridCPU_ISE", "CloseToHSL", "Core", "Diagnostics"));

        foreach (string source in new[] { replay, certificates, telemetry })
        {
            Assert.DoesNotContain("SourceOperationProvenance", source,
                StringComparison.Ordinal);
            Assert.DoesNotContain("SourceSlotIndex", source,
                StringComparison.Ordinal);
        }

        foreach (string sourceRoot in new[]
                 {
                     "HybridCPU_Compiler",
                     "CpuInterfaceBridge",
                     "TestAssemblerConsoleApps",
                 })
        {
            string source = JoinSources(Path.Combine(root, sourceRoot));
            Assert.DoesNotMatch(FamilyPattern, source);
        }
    }


    private static InventoryFingerprint CaptureRoot(
        string root,
        string sourceRoot)
    {
        var regex = new Regex(FamilyPattern, RegexOptions.CultureInvariant);
        var entries = new List<string>();
        string absoluteRoot = Path.Combine(root, sourceRoot);
        foreach (string path in EnumerateSources(absoluteRoot)
                     .Where(path => !path.EndsWith(
                         ThisFile,
                         StringComparison.OrdinalIgnoreCase) &&
                                    !path.EndsWith(
                         Rf125cGuardFile,
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

    private static string Slice(string source, string start, string end)
    {
        int startIndex = source.IndexOf(start, StringComparison.Ordinal);
        int endIndex = source.IndexOf(end, startIndex, StringComparison.Ordinal);
        Assert.True(startIndex >= 0 && endIndex > startIndex);
        return source[startIndex..endIndex];
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
