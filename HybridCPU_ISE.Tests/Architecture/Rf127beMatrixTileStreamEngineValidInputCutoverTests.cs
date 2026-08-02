namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7be MatrixTile StreamEngineId valid-input resource-mask cutover.</summary>
public sealed class Rf127beMatrixTileStreamEngineValidInputCutoverTests
{
    [Fact]
    public void CheckedZeroPreservesTheThreeMatrixTileResourceMaskContours()
    {
        string root = Root();
        string legality = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Legality",
            "BundleLegalityAnalyzer.cs");
        string microOps = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "MicroOps", "MatrixTile", "MatrixTileMicroOps.cs");

        Assert.Equal(1, Count(legality, "ForStreamEngine(StreamEngineId.Zero)"));
        Assert.Contains("ForMemoryDomain(slot.OwnerThreadId)", legality, StringComparison.Ordinal);
        Assert.Contains("ForMatrixTileStreamWindow()", legality, StringComparison.Ordinal);
        Assert.Equal(2, Count(microOps, "ForStreamEngine(StreamEngineId.Zero)"));
        Assert.Contains("ForLoad()", microOps, StringComparison.Ordinal);
        Assert.Contains("ForStore()", microOps, StringComparison.Ordinal);
        Assert.Contains("ForMatrixTileIngress()", microOps, StringComparison.Ordinal);
        Assert.Contains("ForMatrixTileEgress()", microOps, StringComparison.Ordinal);
    }

    [Fact]
    public void ByteTransportAndFingerprintFormsRemainUntouched()
    {
        string root = Root();
        string transfer = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "ISA", "Instructions",
            "NonVmx", "Lanes00_03Vector", "MatrixTile", "MatrixTileStreamTransferAbi.cs");
        string replay = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "ISA", "Instructions",
            "NonVmx", "Lanes00_03Vector", "MatrixTile", "MatrixTileReplayRollbackAbi.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "ISA", "Instructions",
            "NonVmx", "Lanes00_03Vector", "MatrixTile", "MatrixTileRetirePublicationAbi.cs");

        Assert.Contains("byte StreamEngineChannel", transfer, StringComparison.Ordinal);
        Assert.Contains("StreamEngineChannel == MatrixTileResourceContour.StreamEngineChannel", transfer,
            StringComparison.Ordinal);
        Assert.Contains("? (byte)MatrixTileResourceContour.StreamEngineChannel", replay,
            StringComparison.Ordinal);
        Assert.Contains("AddByte(ref hash, transfer.StreamEngineChannel);", retire,
            StringComparison.Ordinal);
    }

    private static int Count(string text, string value) => text.Split(value, StringSplitOptions.None).Length - 1;

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
