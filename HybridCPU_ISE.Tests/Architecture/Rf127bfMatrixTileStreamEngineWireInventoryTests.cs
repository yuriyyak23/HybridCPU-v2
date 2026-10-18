namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7bf MatrixTile raw stream-engine byte transport inventory.</summary>
public sealed class Rf127bfMatrixTileStreamEngineWireInventoryTests
{
    [Fact]
    public void ByteTransportIsPublicButItsTypedUseIsFixedZeroValidatedBeforeRetire()
    {
        string root = Root();
        string transfer = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "ISA", "Instructions",
            "NonVmx", "Lanes00_03Vector", "MatrixTile", "MatrixTileStreamTransferAbi.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "ISA", "Instructions",
            "NonVmx", "Lanes00_03Vector", "MatrixTile", "MatrixTileRetirePublicationAbi.cs");

        Assert.Contains("byte StreamEngineChannel", transfer, StringComparison.Ordinal);
        Assert.Contains("return new MatrixTileStreamTransferRecord(", transfer, StringComparison.Ordinal);
        Assert.Contains("MatrixTileResourceContour.StreamEngineChannel,", transfer, StringComparison.Ordinal);
        Assert.Contains("StreamEngineChannel == MatrixTileResourceContour.StreamEngineChannel", transfer,
            StringComparison.Ordinal);
        Assert.Contains("MatrixTileStreamTransferAbi.ValidateCapture(capture);", retire,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AbsenceAndFingerprintMarkersRemainOutsideTheRawEngineValue()
    {
        string root = Root();
        string transfer = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "ISA", "Instructions",
            "NonVmx", "Lanes00_03Vector", "MatrixTile", "MatrixTileStreamTransferAbi.cs");
        string replay = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "ISA", "Instructions",
            "NonVmx", "Lanes00_03Vector", "MatrixTile", "MatrixTileReplayRollbackAbi.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "ISA", "Instructions",
            "NonVmx", "Lanes00_03Vector", "MatrixTile", "MatrixTileRetirePublicationAbi.cs");

        Assert.Contains("ResourceClass == MatrixTileRuntimeResourceClass.None", transfer,
            StringComparison.Ordinal);
        Assert.Contains("? (byte)MatrixTileResourceContour.StreamEngineChannel", replay,
            StringComparison.Ordinal);
        Assert.Contains(": byte.MaxValue", replay, StringComparison.Ordinal);
        Assert.Contains("AddByte(ref hash, transfer.StreamEngineChannel);", retire,
            StringComparison.Ordinal);
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
