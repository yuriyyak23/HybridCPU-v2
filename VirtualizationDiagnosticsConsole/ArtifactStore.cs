using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal sealed class ArtifactStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public ArtifactStore(string directory) => DirectoryPath = Path.GetFullPath(directory);

    public string DirectoryPath { get; }

    public void EnsureDirectory() => Directory.CreateDirectory(DirectoryPath);

    public string PathFor(string fileName) => Path.Combine(DirectoryPath, fileName);

    public void WriteJson<T>(string fileName, T value)
    {
        EnsureDirectory();
        string destination = PathFor(fileName);
        string temporary = destination + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, JsonOptions), new UTF8Encoding(false));
        File.Move(temporary, destination, overwrite: true);
    }

    public T? ReadJson<T>(string fileName)
    {
        string path = PathFor(fileName);
        return File.Exists(path)
            ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)
            : default;
    }

    public static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}

internal static class RepositoryLocator
{
    public static string FindRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "HybridCPU v2.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("HybridCPU ISE repository root was not found.");
    }
}
