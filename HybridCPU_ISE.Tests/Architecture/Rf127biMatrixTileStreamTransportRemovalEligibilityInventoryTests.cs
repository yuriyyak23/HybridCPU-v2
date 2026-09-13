using System.Reflection;
using YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7bi MatrixTile raw transport removal-eligibility inventory.</summary>
public sealed class Rf127biMatrixTileStreamTransportRemovalEligibilityInventoryTests
{
    [Fact]
    public void PublicRecordConstructorAndInitByteShapeRemainExplicit()
    {
        ConstructorInfo[] constructors = typeof(MatrixTileStreamTransferRecord).GetConstructors();
        Assert.Single(constructors);
        Assert.Contains(constructors[0].GetParameters(), parameter =>
            parameter.ParameterType == typeof(byte) && parameter.Name == "StreamEngineChannel");

        PropertyInfo property = typeof(MatrixTileStreamTransferRecord).GetProperty(
            nameof(MatrixTileStreamTransferRecord.StreamEngineChannel))!;
        Assert.Equal(typeof(byte), property.PropertyType);
        Assert.NotNull(property.SetMethod);
    }

    [Fact]
    public void OnlyCompleteConstructsProductionTransportAndNoDirectReflectionOrSerializationSeamExists()
    {
        string root = Root();
        string production = string.Join("\n", Directory.EnumerateFiles(
            Path.Combine(root, "HybridCPU_ISE"), "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText));
        string tests = string.Join("\n", Directory.EnumerateFiles(
            Path.Combine(root, "HybridCPU_ISE.Tests"), "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText));

        Assert.Equal(1, Count(production, "new MatrixTileStreamTransferRecord("));
        Assert.Contains("StreamTransfer = StreamTransfer.DeepClone()", production, StringComparison.Ordinal);
        // One reflective inspection and this guard's literal are the only occurrences.
        Assert.Equal(2, Count(tests, "MatrixTileStreamTransferRecord).GetConstructor"));
        Assert.DoesNotContain("MatrixTileStreamTransferRecord>(", production,
            StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Serialize<MatrixTileStreamTransferRecord>", production,
            StringComparison.Ordinal);
    }

    private static int Count(string text, string value) => text.Split(value, StringSplitOptions.None).Length - 1;

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
