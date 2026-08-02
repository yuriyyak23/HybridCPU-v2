using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123akArchitecturalRegisterResourceMaskCallerClosureAuditTests
{
    private const string ThisFile =
        "Rf123akArchitecturalRegisterResourceMaskCallerClosureAuditTests.cs";

    [Fact]
    public void PublicRegisterMaskSurfaceIsExactlyTheFrozenTenMethodContour()
    {
        Type type = typeof(ResourceMaskBuilder);
        AssertSurface(type, "ForRegisterRead", typeof(ResourceBitset),
            typeof(int));
        AssertSurface(type, "ForRegisterRead", typeof(ResourceBitset),
            typeof(int), typeof(int));
        AssertSurface(type, "ForRegisterWrite", typeof(ResourceBitset),
            typeof(int));
        AssertSurface(type, "ForRegisterWrite", typeof(ResourceBitset),
            typeof(int), typeof(int));
        AssertSurface(type, "ForArchitecturalRegisterRead",
            typeof(ResourceBitset), typeof(ArchRegId));
        AssertSurface(type, "ForArchitecturalRegisterRead",
            typeof(ResourceBitset), typeof(ArchRegId), typeof(int));
        AssertSurface(type, "ForArchitecturalRegisterWrite",
            typeof(ResourceBitset), typeof(ArchRegId));
        AssertSurface(type, "ForArchitecturalRegisterWrite",
            typeof(ResourceBitset), typeof(ArchRegId), typeof(int));
        AssertSurface(type, "ForRegisterRead128", typeof(SafetyMask128),
            typeof(int));
        AssertSurface(type, "ForRegisterWrite128", typeof(SafetyMask128),
            typeof(int));
        AssertSurface(type, "ForArchitecturalRegisterRead128",
            typeof(SafetyMask128), typeof(ArchRegId));
        AssertSurface(type, "ForArchitecturalRegisterWrite128",
            typeof(SafetyMask128), typeof(ArchRegId));

        MethodInfo[] registerMaskMethods = type.GetMethods(
                BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name.Contains("Register",
                StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(12, registerMaskMethods.Length);
        Assert.DoesNotContain(registerMaskMethods,
            method => method.IsGenericMethod);
    }


    [Fact]
    public void EveryRawProductionCallIsAnExactCheckedSelectionFallback()
    {
        string root = FindRepositoryRoot();
        string production = string.Join("\n",
            EnumerateSourceFiles(Path.Combine(root, "HybridCPU_ISE"))
                .Where(path => !HasPathSegment(path, "Legacy"))
                .Select(File.ReadAllText));

        Assert.Equal(24, Regex.Matches(production,
            @"(?s)\?\s*ResourceMaskBuilder\.ForArchitecturalRegisterRead\(.*?\)\s*:\s*ResourceMaskBuilder\.ForRegisterRead\(").Count);
        Assert.Equal(13, Regex.Matches(production,
            @"(?s)\?\s*ResourceMaskBuilder\.ForArchitecturalRegisterWrite\(.*?\)\s*:\s*ResourceMaskBuilder\.ForRegisterWrite\(").Count);
        Assert.Equal(24, CountCalls(production, "ForRegisterRead"));
        Assert.Equal(13, CountCalls(production, "ForRegisterWrite"));
        Assert.Equal(0, CountCalls(production, "ForRegisterRead128"));
        Assert.Equal(0, CountCalls(production, "ForRegisterWrite128"));

        Assert.DoesNotContain(
            "ResourceMaskBuilder.ForRegisterRead(regId,",
            production, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ResourceMaskBuilder.ForRegisterWrite(regId,",
            production, StringComparison.Ordinal);
        Assert.DoesNotContain("Math.Clamp", ExtractBuilder(production),
            StringComparison.Ordinal);
        Assert.DoesNotContain("%", ExtractBuilder(production),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutableTestAndTestSupportCallersRemainExplicitlyCounted()
    {
        string root = FindRepositoryRoot();
        Dictionary<string, CallCounts> tests = ScanFiles(
            Path.Combine(root, "HybridCPU_ISE.Tests"),
            path => !path.EndsWith(ThisFile, StringComparison.Ordinal));

        Assert.Equal(new CallCounts(108, 81, 17, 15, 10, 10, 1, 1),
            Sum(tests.Values));
        Assert.Equal(34, tests.Count(pair => pair.Value.RawRead != 0));
        Assert.Equal(30, tests.Count(pair => pair.Value.RawWrite != 0));

        Assert.Equal(new CallCounts(4, 3, 0, 0, 4, 3, 0, 0),
            tests["TestHelpers/MicroOpTestHelper.cs"]);

        Assert.Equal(
            new[]
            {
                "Architecture/Rf120ResourceIdIngressGuardTests.cs",
                "Miscellaneous/MaskBitPositionsArticleQ1Tests.cs",
                "SafetyAndVerification/SafetyMask128Tests.cs",
                "SafetyAndVerification/SafetyMaskTests.cs",
                "TestHelpers/MicroOpTestHelper.cs"
            },
            tests.Where(pair => pair.Value.RawRead128 != 0)
                .Select(pair => pair.Key).Order().ToArray());
        Assert.Equal(
            new[]
            {
                "Architecture/Rf120ResourceIdIngressGuardTests.cs"
            },
            tests.Where(pair => pair.Value.CheckedRead128 != 0)
                .Select(pair => pair.Key).Order().ToArray());
        Assert.Equal(
            new[]
            {
                "Architecture/Rf120ResourceIdIngressGuardTests.cs"
            },
            tests.Where(pair => pair.Value.CheckedWrite128 != 0)
                .Select(pair => pair.Key).Order().ToArray());
    }

    [Fact]
    public void RawInvalidAliasesAndCheckedZeroSemanticsRemainFrozen()
    {
        Func<int, ResourceBitset> rawRead =
            RequiredMethod("ForRegisterRead", typeof(int))
                .CreateDelegate<Func<int, ResourceBitset>>();
        Func<int, ResourceBitset> rawWrite =
            RequiredMethod("ForRegisterWrite", typeof(int))
                .CreateDelegate<Func<int, ResourceBitset>>();
        Func<int, int, ResourceBitset> rawVtRead =
            RequiredMethod("ForRegisterRead", typeof(int), typeof(int))
                .CreateDelegate<Func<int, int, ResourceBitset>>();
        Func<int, int, ResourceBitset> rawVtWrite =
            RequiredMethod("ForRegisterWrite", typeof(int), typeof(int))
                .CreateDelegate<Func<int, int, ResourceBitset>>();
        Func<ArchRegId, ResourceBitset> checkedRead =
            RequiredMethod("ForArchitecturalRegisterRead",
                    typeof(ArchRegId))
                .CreateDelegate<Func<ArchRegId, ResourceBitset>>();
        Func<ArchRegId, ResourceBitset> checkedWrite =
            RequiredMethod("ForArchitecturalRegisterWrite",
                    typeof(ArchRegId))
                .CreateDelegate<Func<ArchRegId, ResourceBitset>>();

        Assert.Equal(rawRead(0), rawRead(-1));
        Assert.Equal(rawWrite(0), rawWrite(-1));
        Assert.Equal(rawVtRead(0, 0), rawVtRead(-1, 0));
        Assert.Equal(rawVtWrite(0, 0), rawVtWrite(-1, 0));
        Assert.Equal(1UL << 63, rawRead(-4).Low);
        Assert.Equal(1UL << 15, rawWrite(-4).Low);
        Assert.Equal(1UL << 15, rawRead(int.MaxValue).Low);
        Assert.Equal(1UL << 31, rawWrite(int.MaxValue).Low);

        Assert.Equal(rawRead(0), checkedRead(ArchRegId.Zero));
        Assert.Equal(rawWrite(0), checkedWrite(default));
        Assert.Throws<ArgumentOutOfRangeException>(() => rawVtRead(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => rawVtWrite(
            0, Processor.CPU_Core.SmtWays));
    }

    private static Dictionary<string, CallCounts> ScanFiles(
        string root,
        Func<string, bool> include)
    {
        var result = new Dictionary<string, CallCounts>(
            StringComparer.Ordinal);
        foreach (string path in EnumerateSourceFiles(root).Where(include))
        {
            string source = StripCommentsAndStrings(File.ReadAllText(path));
            CallCounts counts = new(
                CountCalls(source, "ForRegisterRead"),
                CountCalls(source, "ForRegisterWrite"),
                CountCalls(source, "ForArchitecturalRegisterRead"),
                CountCalls(source, "ForArchitecturalRegisterWrite"),
                CountCalls(source, "ForRegisterRead128"),
                CountCalls(source, "ForRegisterWrite128"),
                CountCalls(source, "ForArchitecturalRegisterRead128"),
                CountCalls(source, "ForArchitecturalRegisterWrite128"));
            if (counts != default)
            {
                result[Path.GetRelativePath(root, path)
                    .Replace('\\', '/')] = counts;
            }
        }
        return result;
    }

    private static int CountCalls(string source, string method) =>
        Regex.Matches(source,
            $@"ResourceMaskBuilder\.{Regex.Escape(method)}(?![A-Za-z0-9_])\s*\(")
            .Count;

    private static string StripCommentsAndStrings(string source)
    {
        var result = new StringBuilder(source.Length);
        bool lineComment = false;
        bool blockComment = false;
        bool quoted = false;
        bool verbatim = false;
        bool character = false;

        for (int index = 0; index < source.Length; index++)
        {
            char current = source[index];
            char next = index + 1 < source.Length ? source[index + 1] : '\0';
            if (lineComment)
            {
                if (current == '\n')
                {
                    lineComment = false;
                    result.Append(current);
                }
                else
                {
                    result.Append(' ');
                }
                continue;
            }
            if (blockComment)
            {
                if (current == '*' && next == '/')
                {
                    result.Append("  ");
                    index++;
                    blockComment = false;
                }
                else
                {
                    result.Append(current == '\n' ? '\n' : ' ');
                }
                continue;
            }
            if (quoted)
            {
                if (!verbatim && current == '\\')
                {
                    result.Append("  ");
                    index++;
                    continue;
                }
                if (verbatim && current == '"' && next == '"')
                {
                    result.Append("  ");
                    index++;
                    continue;
                }
                if (current == '"')
                {
                    quoted = false;
                    verbatim = false;
                }
                result.Append(current == '\n' ? '\n' : ' ');
                continue;
            }
            if (character)
            {
                if (current == '\\')
                {
                    result.Append("  ");
                    index++;
                    continue;
                }
                if (current == '\'')
                {
                    character = false;
                }
                result.Append(' ');
                continue;
            }
            if (current == '/' && next == '/')
            {
                result.Append("  ");
                index++;
                lineComment = true;
            }
            else if (current == '/' && next == '*')
            {
                result.Append("  ");
                index++;
                blockComment = true;
            }
            else if (current == '@' && next == '"')
            {
                result.Append("  ");
                index++;
                quoted = true;
                verbatim = true;
            }
            else if (current == '"')
            {
                result.Append(' ');
                quoted = true;
            }
            else if (current == '\'')
            {
                result.Append(' ');
                character = true;
            }
            else
            {
                result.Append(current);
            }
        }
        return result.ToString();
    }

    private static string ExtractBuilder(string allProduction)
    {
        const string marker = "public static class ResourceMaskBuilder";
        int start = allProduction.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        int nextType = allProduction.IndexOf("\n    public ",
            start + marker.Length, StringComparison.Ordinal);
        return nextType < 0
            ? allProduction[start..]
            : allProduction[start..nextType];
    }

    private static MethodInfo RequiredMethod(
        string name,
        params Type[] parameterTypes) =>
        typeof(ResourceMaskBuilder).GetMethod(name,
            BindingFlags.Public | BindingFlags.Static, parameterTypes)
        ?? throw new MissingMethodException(
            typeof(ResourceMaskBuilder).FullName, name);

    private static void AssertSurface(
        Type type,
        string name,
        Type returnType,
        params Type[] parameterTypes)
    {
        MethodInfo method = type.GetMethod(name,
            BindingFlags.Public | BindingFlags.Static, parameterTypes)
            ?? throw new MissingMethodException(type.FullName, name);
        Assert.Equal(returnType, method.ReturnType);
    }

    private static CallCounts Sum(IEnumerable<CallCounts> counts) =>
        counts.Aggregate(default(CallCounts),
            (sum, value) => sum + value);

    private static IEnumerable<string> EnumerateSourceFiles(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !HasPathSegment(path, "bin") &&
                           !HasPathSegment(path, "obj"));

    private static bool HasPathSegment(string path, string segment) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Contains(segment, StringComparer.OrdinalIgnoreCase);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName,
                    "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(directory.FullName,
                    "Documentation")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private readonly record struct CallCounts(
        int RawRead,
        int RawWrite,
        int CheckedRead,
        int CheckedWrite,
        int RawRead128,
        int RawWrite128,
        int CheckedRead128,
        int CheckedWrite128)
    {
        public static CallCounts operator +(CallCounts left, CallCounts right) =>
            new(
                left.RawRead + right.RawRead,
                left.RawWrite + right.RawWrite,
                left.CheckedRead + right.CheckedRead,
                left.CheckedWrite + right.CheckedWrite,
                left.RawRead128 + right.RawRead128,
                left.RawWrite128 + right.RawWrite128,
                left.CheckedRead128 + right.CheckedRead128,
                left.CheckedWrite128 + right.CheckedWrite128);
    }
}
