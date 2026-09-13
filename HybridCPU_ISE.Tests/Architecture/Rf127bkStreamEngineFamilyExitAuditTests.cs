using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7bl final StreamEngineId family closed-world exit audit.</summary>
public sealed class Rf127bkStreamEngineFamilyExitAuditTests
{
    [Fact]
    public void CheckedIdentityHasOnlyTheSelectedProductionResourceMaskOwners()
    {
        string root = Root();
        string production = Path.Combine(root, "HybridCPU_ISE");
        string[] users = Directory.EnumerateFiles(production, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("StreamEngineId", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray()!;

        Assert.Equal([
            "AssistMicroOp.cs",
            "BundleLegalityAnalyzer.cs",
            "DmaStreamComputeMicroOp.cs",
            "DmaStreamComputeQueryCapsMicroOp.cs",
            "DmaStreamComputeStatusMicroOp.cs",
            "MatrixTileMicroOps.cs",
            "MicroOp.Compute.cs",
            "MicroOp.cs",
            "StreamEngineId.cs",
            "VectorMicroOps.cs"
        ], users);

        string allProduction = string.Join("\n", Directory.EnumerateFiles(production, "*.cs",
            SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.DoesNotMatch(new Regex(@"ResourceMaskBuilder\.ForStreamEngine(?:128)?\(0\)"), allProduction);
        Assert.DoesNotContain("ForStreamEngine(MatrixTileResourceContour.StreamEngineChannel)", allProduction,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CheckedRangeAndInvalidBehaviorAreExplicitAndDoNotAliasZero()
    {
        Assert.Equal((byte)0, StreamEngineId.MinValue);
        Assert.Equal((byte)3, StreamEngineId.MaxValue);
        Assert.Equal((byte)0, StreamEngineId.Zero.Value);

        for (int raw = 0; raw <= 3; raw++)
        {
            StreamEngineId checkedId = StreamEngineId.Create(raw);
            Assert.Equal((byte)raw, checkedId.ToRawValue());
            Assert.Equal(checkedId, StreamEngineId.FromRawValue((byte)raw));
        }

        foreach (int invalid in new[] { -1, 4, int.MinValue, int.MaxValue })
        {
            Assert.False(StreamEngineId.TryCreate(invalid, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => StreamEngineId.Create(invalid));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => StreamEngineId.FromRawValue(4));
    }

    [Fact]
    public void RawBuildersAndMatrixTileTransportRemainSeparateRetainedCompatibilityBoundaries()
    {
        string root = Root();
        string builders = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps",
            "Types", "MicroOp.cs");
        string transfer = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "ISA", "Instructions",
            "NonVmx", "Lanes00_03Vector", "MatrixTile", "MatrixTileStreamTransferAbi.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "ISA", "Instructions",
            "NonVmx", "Lanes00_03Vector", "MatrixTile", "MatrixTileRetirePublicationAbi.cs");

        Assert.Contains("ForStreamEngine(int engineId)", builders, StringComparison.Ordinal);
        Assert.Contains("ForStreamEngine128(int engineId)", builders, StringComparison.Ordinal);
        Assert.Contains("ForStreamEngine(StreamEngineId engineId)", builders, StringComparison.Ordinal);
        Assert.Contains("ForStreamEngine128(StreamEngineId engineId)", builders, StringComparison.Ordinal);
        Assert.Contains("RequireResourceId(engineId, 4", builders, StringComparison.Ordinal);
        Assert.Contains("byte StreamEngineChannel,", transfer, StringComparison.Ordinal);
        Assert.Contains("StreamEngineChannel == MatrixTileResourceContour.StreamEngineChannel", transfer,
            StringComparison.Ordinal);
        Assert.Contains("MatrixTileStreamTransferAbi.ValidateCapture(capture);", retire,
            StringComparison.Ordinal);
        Assert.Contains("AddByte(ref hash, transfer.StreamEngineChannel);", retire,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PaperDefinesOwnershipAbsenceAndNoCrossFamilyOrReflectionBypass()
    {
        string root = Root();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");
        string type = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "Common",
            "StreamEngineId.cs");

        Assert.Contains("`StreamEngineId` is local to stream-resource selection", paper,
            StringComparison.Ordinal);
        Assert.Contains("It is not a DMA channel", paper, StringComparison.Ordinal);
        Assert.Contains("Zero is valid engine 0. Absence is an outer result", paper,
            StringComparison.Ordinal);
        Assert.Contains("MatrixTile fixed stream-transport byte boundary", paper,
            StringComparison.Ordinal);
        Assert.Contains("public primary constructor and `init` transport shape remain retained", paper,
            StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"\b(?:DmaChannelId|DeviceId|QueueId|TokenId|DomainId|LaneId|SlotId)\b"), type);
        Assert.DoesNotContain("BindingFlags", type, StringComparison.Ordinal);
        Assert.DoesNotContain("TestSupport", type, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine([root, .. parts]));

    private static string Root()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "ResearchPaper"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
