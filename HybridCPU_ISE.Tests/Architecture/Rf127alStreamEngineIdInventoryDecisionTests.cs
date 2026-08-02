using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7al StreamEngineId closed-world inventory and authority decision.</summary>
public sealed class Rf127alStreamEngineIdInventoryDecisionTests
{
    [Fact]
    public void PaperDefinesStreamEngineAsSeparateClosedZeroBasedSelector()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md"));

        Assert.Contains("`StreamEngineId` is local to stream-resource selection and has values `0..3`.", paper,
            StringComparison.Ordinal);
        Assert.Contains("It is not a DMA channel.", paper, StringComparison.Ordinal);
        Assert.Contains("Zero is valid engine 0. Absence is an outer result.", paper, StringComparison.Ordinal);
    }


    [Fact]
    public void MatrixTileWireRetainsIndependentByteStreamSelectorAndReplayPublicationConsumers()
    {
        string transfer = Read("HybridCPU_ISE", "CloseToHSL", "Core", "ISA", "Instructions", "NonVmx",
            "Lanes00_03Vector", "MatrixTile", "MatrixTileStreamTransferAbi.cs");
        string replay = Read("HybridCPU_ISE", "CloseToHSL", "Core", "ISA", "Instructions", "NonVmx",
            "Lanes00_03Vector", "MatrixTile", "MatrixTileReplayRollbackAbi.cs");
        string retire = Read("HybridCPU_ISE", "CloseToHSL", "Core", "ISA", "Instructions", "NonVmx",
            "Lanes00_03Vector", "MatrixTile", "MatrixTileRetirePublicationAbi.cs");

        Assert.Contains("byte StreamEngineChannel", transfer, StringComparison.Ordinal);
        Assert.Contains("StreamEngineChannel == MatrixTileResourceContour.StreamEngineChannel", transfer,
            StringComparison.Ordinal);
        Assert.Contains("? (byte)MatrixTileResourceContour.StreamEngineChannel", replay,
            StringComparison.Ordinal);
        Assert.Contains("AddByte(ref hash, transfer.StreamEngineChannel);", retire,
            StringComparison.Ordinal);
    }


    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(
        new[] { Root() }.Concat(parts).ToArray()));

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
