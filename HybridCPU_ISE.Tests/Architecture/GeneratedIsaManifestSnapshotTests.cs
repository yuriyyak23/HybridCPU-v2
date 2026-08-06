using System.Text.Json;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class GeneratedIsaManifestSnapshotTests
{
    [Fact]
    public void ManifestInput_IsVersionedAndExplicitlyGeneratorInput()
    {
        string root = FindRepositoryRoot();
        string manifestPath = Path.Combine(root, "isa", "hybridcpu-isa.manifest.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement rootElement = document.RootElement;

        Assert.Equal(2, rootElement.GetProperty("manifestSchemaVersion").GetInt32());
        Assert.Equal("declared-static-isa-manifest", rootElement.GetProperty("sourceOfTruth").GetString());
        Assert.True(rootElement.GetProperty("isGeneratorInput").GetBoolean());
        Assert.Equal(250, rootElement.GetProperty("instructionCount").GetInt32());
        Assert.Equal(rootElement.GetProperty("instructionCount").GetInt32(), rootElement.GetProperty("instructions").GetArrayLength());
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")) &&
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the HybridCPU repository root.");
    }
}
