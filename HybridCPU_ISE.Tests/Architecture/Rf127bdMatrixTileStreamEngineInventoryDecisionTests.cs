namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7bd MatrixTile StreamEngineId selector and ABI inventory decision.</summary>
public sealed class Rf127bdMatrixTileStreamEngineInventoryDecisionTests
{
    [Fact]
    public void FixedSelectorHasExactlyThreeResourceMaskConsumers()
    {
        string root = Root();
        string legality = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Legality",
            "BundleLegalityAnalyzer.cs");
        string microOps = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "MicroOps", "MatrixTile", "MatrixTileMicroOps.cs");

        Assert.Equal(1, Count(legality, "ForStreamEngine(StreamEngineId.Zero)"));
        Assert.Equal(2, Count(microOps, "ForStreamEngine(StreamEngineId.Zero)"));
        Assert.DoesNotContain("ForStreamEngine(MatrixTileResourceContour.StreamEngineChannel)", legality,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ForStreamEngine(MatrixTileResourceContour.StreamEngineChannel)", microOps,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ByteTransportIsValidatedAndRetainedInReplayAndRetireFingerprints()
    {
        string root = Root();
        string contour = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "ISA", "Instructions",
            "NonVmx", "Lanes00_03Vector", "MatrixTile", "MatrixTileResourceContour.cs");
        string transfer = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "ISA", "Instructions",
            "NonVmx", "Lanes00_03Vector", "MatrixTile", "MatrixTileStreamTransferAbi.cs");
        string replay = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "ISA", "Instructions",
            "NonVmx", "Lanes00_03Vector", "MatrixTile", "MatrixTileReplayRollbackAbi.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "ISA", "Instructions",
            "NonVmx", "Lanes00_03Vector", "MatrixTile", "MatrixTileRetirePublicationAbi.cs");

        Assert.Contains("public const int StreamEngineChannel = 0;", contour, StringComparison.Ordinal);
        Assert.Contains("byte StreamEngineChannel", transfer, StringComparison.Ordinal);
        Assert.Contains("StreamEngineChannel == MatrixTileResourceContour.StreamEngineChannel", transfer,
            StringComparison.Ordinal);
        Assert.Contains("? (byte)MatrixTileResourceContour.StreamEngineChannel", replay,
            StringComparison.Ordinal);
        Assert.Contains(": byte.MaxValue", replay, StringComparison.Ordinal);
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
