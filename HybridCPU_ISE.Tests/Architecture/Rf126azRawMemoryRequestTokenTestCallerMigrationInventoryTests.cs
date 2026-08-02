namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6az closed-world inventory for the retained public raw
/// MemoryRequestToken constructor's test and reflection compatibility seams.
/// This decision guard authorizes no production or test migration.
/// </summary>
public sealed class Rf126azRawMemoryRequestTokenTestCallerMigrationInventoryTests
{
    private const string ThisFile =
        "Rf126azRawMemoryRequestTokenTestCallerMigrationInventoryTests.cs";

    [Fact]
    public void DirectConstructorCallerInventoryIsZero()
    {
        string root = Root();
        string tests = Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture");
        string[] callers = MatchingFiles(tests,
            "new MemorySubsystem.MemoryRequestToken(",
            "PhysicalMemoryBankBinding")
            .Where(file => !string.Equals(file,
                "Rf126ayRawMemoryRequestTokenConstructorEligibilityDecisionTests.cs",
                StringComparison.Ordinal))
            .Where(file => !string.Equals(file,
                "Rf126baAcceptedBindingCaptureTestMigrationInventoryTests.cs",
                StringComparison.Ordinal))
            .Where(file => !string.Equals(file,
                "Rf126bcStoredBindingConsumerTestMigrationInventoryTests.cs",
                StringComparison.Ordinal))
            .Where(file => !string.Equals(file,
                "Rf126beLegacyCancellationForgedTokenMigrationInventoryTests.cs",
                StringComparison.Ordinal))
            .Where(file => !string.Equals(file,
                "Rf126bgInvalidCancellationForgedTokenMigrationInventoryTests.cs",
                StringComparison.Ordinal))
            .Where(file => !string.Equals(file,
                "Rf126biRawTokenReflectionSignatureMigrationInventoryTests.cs",
                StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(callers);
    }


    [Fact]
    public void ExternalTreesHaveNoConstructorOrReflectionCompatibilityCaller()
    {
        string root = Root();
        foreach (string relative in new[]
                 {
                     "HybridCPU_Compiler", "HybridCPU_RoslynBridge",
                     "CpuInterfaceBridge", "TestAssemblerConsoleApps"
                 })
        {
            string text = Tree(Path.Combine(root, relative));
            Assert.Equal(0, Count(text, "new MemoryRequestToken("));
            Assert.False(text.Contains("MemoryRequestToken") &&
                text.Contains("Get" + "Constructors("));
        }
    }

    [Fact]
    public void PaperMakesTheseSeamsRemovalBlockersNotTokenAuthority()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper",
            "section", "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("reflection/signature consumer,\ntest and", paper,
            StringComparison.Ordinal);
        Assert.Contains("never pending-map, location, admission, completion or\npublication authority", paper,
            StringComparison.Ordinal);
    }

    private static string[] MatchingFiles(string directory, params string[] markers) =>
        Directory.EnumerateFiles(directory, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(path => !path.EndsWith(ThisFile, StringComparison.OrdinalIgnoreCase))
            .Where(path => markers.All(marker => File.ReadAllText(path).Contains(marker,
                StringComparison.Ordinal)))
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray()!;

    private static int Count(string text, string marker) => text.Split(marker).Length - 1;
    private static string Slice(string text, string start, string end)
    {
        int first = text.IndexOf(start, StringComparison.Ordinal);
        int last = text.IndexOf(end, first, StringComparison.Ordinal);
        Assert.True(first >= 0 && last > first);
        return text[first..last];
    }
    private static string Tree(string root) => string.Join("\n",
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Replace('\\', '/').Contains("/bin/", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Replace('\\', '/').Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal).Select(File.ReadAllText));

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
