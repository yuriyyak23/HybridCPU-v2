namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7bg Paper authority for MatrixTile fixed stream transport.</summary>
public sealed class Rf127bgMatrixTileStreamEngineAuthorityDecisionTests
{
    [Fact]
    public void PaperDefinesFixedZeroNonzeroRejectionAndOuterAbsence()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md"));

        Assert.Contains("#### MatrixTile fixed stream-transport byte boundary", paper,
            StringComparison.Ordinal);
        Assert.Contains("exactly byte `0`", paper, StringComparison.Ordinal);
        Assert.Contains("Bytes `1..3`", paper, StringComparison.Ordinal);
        Assert.Contains("bytes `4..255`", paper, StringComparison.Ordinal);
        Assert.Contains("Absence is not encoded in `StreamEngineChannel`", paper,
            StringComparison.Ordinal);
        Assert.Contains("No versioned wire bridge is introduced", paper, StringComparison.Ordinal);
        Assert.Contains("public primary constructor and `init` transport shape remain retained", paper,
            StringComparison.Ordinal);
        Assert.Contains("RF-12 does not authorize their", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingValidationAndFingerprintOwnersMatchThePaperDecision()
    {
        string root = Root();
        string transfer = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "ISA", "Instructions",
            "NonVmx", "Lanes00_03Vector", "MatrixTile", "MatrixTileStreamTransferAbi.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "ISA", "Instructions",
            "NonVmx", "Lanes00_03Vector", "MatrixTile", "MatrixTileRetirePublicationAbi.cs");

        Assert.Contains("StreamEngineChannel == MatrixTileResourceContour.StreamEngineChannel", transfer,
            StringComparison.Ordinal);
        Assert.Contains("MatrixTileStreamTransferAbi.ValidateCapture(capture);", retire,
            StringComparison.Ordinal);
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
