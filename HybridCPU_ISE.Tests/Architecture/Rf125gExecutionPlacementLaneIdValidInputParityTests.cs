using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf125gExecutionPlacementLaneIdValidInputParityTests
{
    [Fact]
    public void EveryValidHardPinHasExactTypedFactoryAndRawFactoryParity()
    {
        for (int rawValue = LaneId.MinValue; rawValue <= LaneId.MaxValue; rawValue++)
        {
            LaneId laneId = LaneId.Create(rawValue);
            ExecutionPlacement typed = ExecutionPlacement.CreateHardPinned(
                SlotClass.AluClass, laneId, domainTag: 0x5A);
            ExecutionPlacement raw = ExecutionPlacement.Create(
                SlotClass.AluClass, SlotPinningKind.HardPinned, (byte)rawValue, domainTag: 0x5A);

            Assert.Equal(raw, typed);
            Assert.Equal((byte)rawValue, typed.PinnedLaneId);
            Assert.Equal(SlotPinningKind.HardPinned, typed.PinningKind);
            Assert.True(typed.TryGetHardPinnedLaneId(out LaneId projected));
            Assert.Equal(laneId, projected);
            Assert.Equal((byte)rawValue, projected.ToRawValue());
        }
    }

    [Fact]
    public void RawFactoryAndItsInvalidBehaviorRemainUnchanged()
    {
        ExecutionPlacement flexible = ExecutionPlacement.Create(
            SlotClass.AluClass, SlotPinningKind.ClassFlexible, byte.MaxValue);
        Assert.Equal(SlotPinningKind.ClassFlexible, flexible.PinningKind);
        Assert.Equal((byte)0, flexible.PinnedLaneId);
        Assert.False(flexible.TryGetHardPinnedLaneId(out LaneId noLane));
        Assert.Equal(default, noLane);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExecutionPlacement.Create(SlotClass.AluClass, SlotPinningKind.HardPinned, 8));
        Assert.Equal("pinnedLaneId", exception.ParamName);

        PropertyInfo rawStorage = typeof(ExecutionPlacement).GetProperty(
            nameof(ExecutionPlacement.PinnedLaneId))!;
        Assert.Equal(typeof(byte), rawStorage.PropertyType);
        MethodInfo rawFactory = typeof(ExecutionPlacement).GetMethod(
            nameof(ExecutionPlacement.Create),
            [typeof(SlotClass), typeof(SlotPinningKind), typeof(byte), typeof(ulong)])!;
        Assert.NotNull(rawFactory);
    }

    [Fact]
    public void TypedSignatureHasNoProductionCompilerOrTestAssemblerCaller()
    {
        string root = FindRepositoryRoot();
        string production = JoinSources(Path.Combine(root, "HybridCPU_ISE"));
        string compiler = JoinSources(Path.Combine(root, "HybridCPU_Compiler"));
        string testAssembler = JoinSources(Path.Combine(root, "TestAssemblerConsoleApps"));

        foreach (string external in new[] { production, compiler, testAssembler })
        {
            Assert.DoesNotMatch(@"\bExecutionPlacement\.CreateHardPinned\s*\(", external);
            Assert.DoesNotMatch(@"\b\.TryGetHardPinnedLaneId\s*\(", external);
        }
    }

    private static string JoinSources(string sourceRoot) => string.Join(
        Environment.NewLine,
        Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGenerated(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(File.ReadAllText));

    private static bool IsGenerated(string path) => path.Replace('\\', '/').Contains("/bin/", StringComparison.OrdinalIgnoreCase) || path.Replace('\\', '/').Contains("/obj/", StringComparison.OrdinalIgnoreCase);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE"))) return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException();
    }
}
