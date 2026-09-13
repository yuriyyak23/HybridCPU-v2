using System.Reflection;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1124MatrixTileStateExtractionTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly string[] LegacyNames =
    [
        "_matrixTileReplayInvalidationEpoch", "_matrixTileStreamInvalidationCount",
        "_nextMatrixTileCaptureOrdinal", "_nextMatrixTileReplayCheckpointOrdinal"
    ];

    [Fact]
    public void ExtensionContainsExactFourFieldMatrixTileLeaf()
    {
        Type extensions = Required("YAKSys_Hybrid_CPU.Core.ExtensionState");
        Type matrix = Required("YAKSys_Hybrid_CPU.Core.MatrixTileState");
        Assert.Single(extensions.GetFields(Flags), field => field.FieldType == matrix);
        Assert.Equal(new[]
        {
            "NextCaptureOrdinal", "NextReplayCheckpointOrdinal", "ReplayInvalidationEpoch",
            "StreamInvalidationCount"
        }, matrix.GetFields(Flags).Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(matrix.GetFields(Flags), field => Assert.Equal(typeof(ulong), field.FieldType));
        Assert.DoesNotContain(matrix.GetMethods(Flags), method => method.Name is
            "Execute" or "Commit" or "Rollback" or "Publish" or "Fallback");
    }

    [Fact]
    public void LegacyFieldsAreRemovedAndForwardByRef()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in LegacyNames)
        {
            Assert.Null(core.GetField(name, Flags));
            Assert.True((core.GetProperty(name, Flags) ??
                throw new InvalidOperationException(name)).PropertyType.IsByRef);
        }
    }

    [Fact]
    public void CopiesShareMatrixTileIdentityScalars()
    {
#pragma warning disable CS0618
        var core = new Processor.CPU_Core(0);
#pragma warning restore CS0618
        Processor.CPU_Core copy = core;
        Assert.Same(core.Runtime.Extensions.MatrixTile, copy.Runtime.Extensions.MatrixTile);
        core.Runtime.Extensions.MatrixTile.NextCaptureOrdinal = 41;
        Assert.Equal(42UL, copy.AllocateMatrixTileCaptureOrdinal());
    }

    [Fact]
    public void AllocationInvalidationAndReplayEpochProtocolsRemainAtExistingOwners()
    {
        string root = FindRoot();
        string matrix = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture",
            "State", "Architectural", "CPU_Core.MatrixTileState.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Retire", "Evidence", "CPU_Core.MatrixTileRetireState.cs");
        Assert.Contains("_matrixTileStreamInvalidationCount += checked", matrix, StringComparison.Ordinal);
        Assert.Contains("checked(++_nextMatrixTileCaptureOrdinal)", retire, StringComparison.Ordinal);
        Assert.Contains("checked(++_nextMatrixTileReplayCheckpointOrdinal)", retire, StringComparison.Ordinal);
        AssertOrder(retire, "if (_matrixTileReplayInvalidationEpoch == 0)",
            "_matrixTileReplayInvalidationEpoch = 1;", "return _matrixTileReplayInvalidationEpoch;");
        Assert.Contains("_matrixTileReplayInvalidationEpoch = checked(current + 1);", retire, StringComparison.Ordinal);
    }

    [Fact]
    public void ClosedWorldLegacyNameUseIsOnlyFacadeAndTwoOwnerFiles()
    {
        string root = FindRoot();
        string production = Path.Combine(root, "HybridCPU_ISE");
        string[] expected =
        [
            Path.Combine("CloseToHSL", "Core", "Architecture", "State", "Architectural", "CPU_Core.MatrixTileState.cs"),
            Path.Combine("CloseToHSL", "Core", "Pipeline", "Retire", "Evidence", "CPU_Core.MatrixTileRetireState.cs"),
            Path.Combine("CloseToHSL", "Core", "State", "CPU_Core.RuntimeState.cs")
        ];
        string[] actual = Directory.GetFiles(production, "*.cs", SearchOption.AllDirectories)
            .Where(file => LegacyNames.Any(name => File.ReadAllText(file).Contains(name, StringComparison.Ordinal)))
            .Select(file => Path.GetRelativePath(production, file))
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected.OrderBy(file => file, StringComparer.Ordinal), actual);
    }

    [Fact]
    public void MatrixTileDscAndL7OwnersRemainSeparate()
    {
        Type extensions = Required("YAKSys_Hybrid_CPU.Core.ExtensionState");
        Assert.Contains(extensions.GetFields(Flags), field => field.Name == "DmaStreamComputeTokenStore");
        Assert.Contains(extensions.GetFields(Flags), field => field.Name == "ExternalAcceleratorRuntime");
        Type matrix = Required("YAKSys_Hybrid_CPU.Core.MatrixTileState");
        Assert.DoesNotContain(matrix.GetFields(Flags), field =>
            field.FieldType.Name.Contains("Dma", StringComparison.Ordinal) ||
            field.FieldType.Name.Contains("Accelerator", StringComparison.Ordinal) ||
            field.FieldType.Name.Contains("Retire", StringComparison.Ordinal) ||
            field.FieldType.Name.Contains("ReplayJournal", StringComparison.Ordinal));
    }

    [Fact]
    public void LedgerAndEvidenceCloseOnlyMatrixTileScalarStorage()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.24-matrix-tile-state-extraction.md");
        Assert.Contains("RF-11.24 | closed MatrixTileState", ledger, StringComparison.Ordinal);
        Assert.Contains("exactly four", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RF-11.25 ResourceState", ledger, StringComparison.Ordinal);
    }

    private static Type Required(string name) => typeof(Processor.CPU_Core).Assembly.GetType(name) ??
        throw new InvalidOperationException(name);

    private static void AssertOrder(string text, params string[] markers)
    {
        int prior = -1;
        foreach (string marker in markers)
        {
            int current = text.IndexOf(marker, prior + 1, StringComparison.Ordinal);
            Assert.True(current > prior, $"Expected '{marker}' after prior marker.");
            prior = current;
        }
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(parts.Aggregate(root, Path.Combine));

    private static string FindRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException();
    }
}
