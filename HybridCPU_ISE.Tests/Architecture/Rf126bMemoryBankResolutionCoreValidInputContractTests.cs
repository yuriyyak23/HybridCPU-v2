using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Core.Decoder;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6b additive zero-caller result contract. Existing raw geometry
/// resolvers, scheduler consumers and invalid-input behavior stay untouched.
/// </summary>
public sealed class Rf126bMemoryBankResolutionCoreValidInputContractTests
{
    [Fact]
    public void EveryRepresentableBankHasExactResolvedRoundTrip()
    {
        for (int raw = 0; raw < MemoryBankId.BankCount; raw++)
        {
            var bank = new MemoryBankId(raw);
            MemoryBankResolution result = MemoryBankResolution.Resolved(bank);

            Assert.Equal(MemoryBankResolutionKind.Resolved, result.Kind);
            Assert.True(result.IsResolved);
            Assert.Equal(bank, result.Bank);
            Assert.True(result.TryGetResolved(out MemoryBankId projected));
            Assert.Equal(bank, projected);
            Assert.Equal(raw, projected.Value);
            Assert.Equal($"Resolved({raw})", result.ToString());
        }
    }

    [Fact]
    public void BankZeroIsResolvedAndNeverAliasesEitherNonResolvedCase()
    {
        MemoryBankResolution resolvedZero =
            MemoryBankResolution.Resolved(new MemoryBankId(0));
        MemoryBankResolution unavailable =
            MemoryBankResolution.UnavailableTopology;
        MemoryBankResolution invalid = MemoryBankResolution.InvalidGeometry;

        Assert.True(resolvedZero.IsResolved);
        Assert.Equal(new MemoryBankId(0), resolvedZero.Bank);
        Assert.NotEqual(resolvedZero, unavailable);
        Assert.NotEqual(resolvedZero, invalid);
        Assert.NotEqual(unavailable, invalid);
    }

    [Fact]
    public void NonResolvedCasesCarryNoBankAndDefaultFailsClosed()
    {
        MemoryBankResolution unavailable =
            MemoryBankResolution.UnavailableTopology;
        MemoryBankResolution invalid = MemoryBankResolution.InvalidGeometry;

        Assert.Equal(default, unavailable);
        Assert.Equal(MemoryBankResolutionKind.UnavailableTopology,
            unavailable.Kind);
        Assert.False(unavailable.IsResolved);
        Assert.Null(unavailable.Bank);
        Assert.False(unavailable.TryGetResolved(out MemoryBankId unavailableBank));
        Assert.Equal(default, unavailableBank);
        Assert.Equal("UnavailableTopology", unavailable.ToString());

        Assert.Equal(MemoryBankResolutionKind.InvalidGeometry, invalid.Kind);
        Assert.False(invalid.IsResolved);
        Assert.Null(invalid.Bank);
        Assert.False(invalid.TryGetResolved(out MemoryBankId invalidBank));
        Assert.Equal(default, invalidBank);
        Assert.Equal("InvalidGeometry", invalid.ToString());
    }

    [Fact]
    public void PublicShapeCannotConstructAnIncoherentKindBankPair()
    {
        Assert.Empty(typeof(MemoryBankResolution).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance |
            BindingFlags.DeclaredOnly));
        Assert.Equal(typeof(MemoryBankResolutionKind),
            typeof(MemoryBankResolution).GetProperty(
                nameof(MemoryBankResolution.Kind))!.PropertyType);
        Assert.Equal(typeof(MemoryBankId?),
            typeof(MemoryBankResolution).GetProperty(
                nameof(MemoryBankResolution.Bank))!.PropertyType);

        Assert.Equal(
            [
                nameof(MemoryBankResolutionKind.UnavailableTopology),
                nameof(MemoryBankResolutionKind.Resolved),
                nameof(MemoryBankResolutionKind.InvalidGeometry)
            ],
            Enum.GetNames<MemoryBankResolutionKind>());
        Assert.Equal((byte)0,
            (byte)MemoryBankResolutionKind.UnavailableTopology);
        Assert.Equal((byte)1, (byte)MemoryBankResolutionKind.Resolved);
        Assert.Equal((byte)2,
            (byte)MemoryBankResolutionKind.InvalidGeometry);
    }

    [Fact]
    public void NewContractHasOnlyFourAuthorizedProjectionFactoryCallers()
    {
        string root = FindRepositoryRoot();
        string contractPath = Path.GetFullPath(Path.Combine(
            root, "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder",
            "MemoryBankResolution.cs"));
        string production = JoinSources(
            Path.Combine(root, "HybridCPU_ISE"), contractPath);
        string compiler = JoinSources(Path.Combine(root, "HybridCPU_Compiler"));
        string bridge = JoinSources(Path.Combine(root, "CpuInterfaceBridge"));
        string assembler = JoinSources(
            Path.Combine(root, "TestAssemblerConsoleApps"));
        const string callerPattern =
            @"\bMemoryBankResolution\.(?:Resolved|UnavailableTopology|InvalidGeometry)\b";

        Assert.Equal(
            [
                "MemoryBankResolution.InvalidGeometry",
                "MemoryBankResolution.InvalidGeometry",
                "MemoryBankResolution.Resolved",
                "MemoryBankResolution.Resolved",
                "MemoryBankResolution.Resolved",
                "MemoryBankResolution.UnavailableTopology",
                "MemoryBankResolution.UnavailableTopology"
            ],
            Regex.Matches(production, callerPattern).Cast<Match>()
                .Select(match => match.Value)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());
        Assert.DoesNotMatch(callerPattern, compiler);
        Assert.DoesNotMatch(callerPattern, bridge);
        Assert.DoesNotMatch(callerPattern, assembler);
        Assert.DoesNotContain("MemoryBankResolution", compiler,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankResolution", bridge,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankResolution", assembler,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingRawResolversAndInvalidBehaviorRemainUnchanged()
    {
        string root = FindRepositoryRoot();
        string routing = File.ReadAllText(Path.Combine(
            root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution",
            "Memory", "LoadStore", "MemoryBankRouting.cs"));

        Assert.Contains(
            "public static int ResolveSchedulerVisibleBankId(ulong address)",
            routing, StringComparison.Ordinal);
        Assert.Contains(
            "public static int ResolveBankId(ulong address, int bankWidthBytes, int numBanks)",
            routing, StringComparison.Ordinal);
        Assert.Contains(
            "internal static bool IsResolvedSchedulerVisibleBankId(int bankId) => bankId >= 0",
            routing, StringComparison.Ordinal);
        Assert.Contains(
            "int resolvedBankWidthBytes = bankWidthBytes > 0",
            routing, StringComparison.Ordinal);
        Assert.Contains(
            "int resolvedNumBanks = numBanks > 0",
            routing, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankResolution", routing,
            StringComparison.Ordinal);
    }


    private static string JoinSources(
        string sourceRoot,
        string? excludedPath = null)
    {
        string? normalizedExcluded = excludedPath is null
            ? null
            : Path.GetFullPath(excludedPath);
        return string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(sourceRoot, "*.cs",
                    SearchOption.AllDirectories)
                .Where(path =>
                    !IsBuildOutput(path) &&
                    (normalizedExcluded is null ||
                     !string.Equals(Path.GetFullPath(path), normalizedExcluded,
                         StringComparison.OrdinalIgnoreCase)))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static bool IsBuildOutput(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName,
                    "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName,
                    "ResearchPaper", "section", "md base")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Repository root was not found.");
    }
}
