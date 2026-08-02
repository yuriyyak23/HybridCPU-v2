using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6ax production guard for the RF-12.6aw raw-ID cancellation decision.
/// </summary>
public sealed class Rf126axLegacyCancellationStoredBindingInvalidBehaviorTests
{
    [Fact]
    public void RawIdCancellationValidatesBindingBeforeRemovingTheOwnerEntry()
    {
        string cancellation = Cancellation(Root());

        Order(cancellation,
            "pendingRequests.TryGetValue(requestID, out token)",
            "token.GetPhysicalBankBindingForOwner();",
            "!physicalBankBinding.IsWellFormed",
            "physicalBankBinding.Generation != geometry.Generation",
            "physicalBankBinding.BankIndex.Value >= geometry.BankCount",
            "return false;",
            "pendingRequests.Remove(requestID);",
            "RemoveQueuedBankRequest(",
            "physicalBankBinding.BankIndex.Value");
        Assert.Equal(0, Regex.Matches(cancellation,
            @"ComputeBankId\(token\.Address\)").Count);
        Assert.Equal(1, Regex.Matches(cancellation,
            @"token\.GetPhysicalBankBindingForOwner\(\)").Count);
    }

    [Fact]
    public void WidthRemapUsesTheCapturedQueueIndexAndRemovesTheEntry()
    {
        MemorySubsystem memory = CreateMemory();
        MemorySubsystem.MemoryRequestToken token = memory.EnqueueRead(
            0, 64, 1, new byte[1]);

        memory.BankWidthBytes = 128;

        Assert.True(memory.CancelPendingRequest(token.RequestID));
        Assert.Equal(0, memory.CurrentQueuedRequests);
        Assert.False(memory.CancelPendingRequest(token.RequestID));
    }

    [Fact]
    public void ShrunkAwayCapturedQueueStillConsumesTheValidPendingIdentity()
    {
        MemorySubsystem memory = CreateMemory();
        MemorySubsystem.MemoryRequestToken token = memory.EnqueueRead(
            0, 3UL * 64UL, 1, new byte[1]);

        memory.NumBanks = 2;

        Assert.Equal(0, memory.CurrentQueuedRequests);
        Assert.False(memory.CancelPendingRequest(token.RequestID));
        Assert.False(memory.CancelPendingRequest(token.RequestID));
    }

    [Fact]
    public void MalformedStaleAndNonMemberBindingsFailClosedWithoutMapMutation()
    {
        AssertBindingFailurePreservesAcceptedRequest(default);

        MemorySubsystem staleMemory = CreateMemory();
        MemorySubsystem.MemoryRequestToken staleToken = staleMemory.EnqueueRead(
            0, 64, 1, new byte[1]);
        PhysicalMemoryBankBinding staleOriginal = Binding(staleToken);
        AssertBindingFailurePreservesAcceptedRequest(staleMemory, staleToken,
            PhysicalMemoryBankBinding.Create(staleOriginal.BankIndex,
                MemoryBankGeometryGeneration.Create(staleOriginal.Generation.Value + 1)));

        MemorySubsystem memberMemory = CreateMemory();
        MemorySubsystem.MemoryRequestToken memberToken = memberMemory.EnqueueRead(
            0, 64, 1, new byte[1]);
        PhysicalMemoryBankBinding memberOriginal = Binding(memberToken);
        AssertBindingFailurePreservesAcceptedRequest(memberMemory, memberToken,
            PhysicalMemoryBankBinding.Create(
                PhysicalMemoryBankIndex.Create(memberMemory.PublishedPhysicalBankGeometry.BankCount),
                memberOriginal.Generation));
    }

    [Fact]
    public void PublicTokenCannotInjectAReplacementBinding()
    {
        string subsystem = Read(Root(), "HybridCPU_ISE", "CloseToHSL",
            "Memory", "Subsystem", "MemorySubsystem.cs");
        string tokenSource = Slice(subsystem, "public class MemoryRequestToken",
            "public void ThrowIfFailed(");
        Assert.Equal(0, Regex.Matches(tokenSource,
            @"public\s+MemoryRequestToken\s*\(").Count);
        Assert.Equal(1, Regex.Matches(tokenSource,
            @"internal\s+MemoryRequestToken\s*\(").Count);
        Assert.Contains("PhysicalMemoryBankBinding physicalBankBinding", tokenSource,
            StringComparison.Ordinal);

        MemorySubsystem memory = CreateMemory();
        MemorySubsystem.MemoryRequestToken accepted = memory.EnqueueRead(
            0, 64, 1, new byte[1]);
        Assert.Contains(
            "return token != null && CancelPendingRequest(token.RequestID);",
            Read(Root(), "HybridCPU_ISE", "CloseToHSL", "Memory",
                "Subsystem", "MemorySubsystem.Operations.cs"),
            StringComparison.Ordinal);
        Assert.True(memory.CancelPendingRequest(accepted));
    }

    [Fact]
    public void ServiceCompletionWiresAndTestSupportRemainOutsideThisCutover()
    {
        string root = Root();
        string helpers = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
            "Subsystem", "MemorySubsystem.Helpers.cs");
        string external = ReadTrees(root, "HybridCPU_Compiler", "CpuInterfaceBridge",
            "HybridCPU_RoslynBridge", "TestAssemblerConsoleApps");
        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Core", "CPU_Core.TestSupport.cs");

        Assert.DoesNotContain("request.PhysicalBankBinding", helpers,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryRequestToken", external,
            StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalMemoryBankBinding", external,
            StringComparison.Ordinal);
        Assert.DoesNotContain("physicalBankBinding", testSupport,
            StringComparison.Ordinal);
    }


    private static void AssertBindingFailurePreservesAcceptedRequest(
        PhysicalMemoryBankBinding replacement)
    {
        MemorySubsystem memory = CreateMemory();
        MemorySubsystem.MemoryRequestToken token = memory.EnqueueRead(
            0, 64, 1, new byte[1]);
        AssertBindingFailurePreservesAcceptedRequest(memory, token, replacement);
    }

    private static void AssertBindingFailurePreservesAcceptedRequest(
        MemorySubsystem memory,
        MemorySubsystem.MemoryRequestToken token,
        PhysicalMemoryBankBinding replacement)
    {
        PhysicalMemoryBankBinding original = Binding(token);
        SetBinding(token, replacement);
        Assert.False(memory.CancelPendingRequest(token.RequestID));
        Assert.Equal(1, memory.CurrentQueuedRequests);

        SetBinding(token, original);
        Assert.True(memory.CancelPendingRequest(token.RequestID));
        Assert.Equal(0, memory.CurrentQueuedRequests);
    }

    private static MemorySubsystem CreateMemory()
    {
        Processor processor = default;
        return new MemorySubsystem(ref processor);
    }

    private static PhysicalMemoryBankBinding Binding(
        MemorySubsystem.MemoryRequestToken token) =>
        (PhysicalMemoryBankBinding)BindingField().GetValue(token)!;

    private static void SetBinding(MemorySubsystem.MemoryRequestToken token,
        PhysicalMemoryBankBinding binding) => BindingField().SetValue(token, binding);

    private static FieldInfo BindingField() =>
        typeof(MemorySubsystem.MemoryRequestToken).GetField("physicalBankBinding",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static string Cancellation(string root) => Slice(Read(root,
        "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
        "MemorySubsystem.Operations.cs"),
        "public bool CancelPendingRequest(ulong requestID)", "#endregion");

    private static void Order(string text, params string[] markers)
    {
        int cursor = -1;
        foreach (string marker in markers)
        {
            int next = text.IndexOf(marker, cursor + 1, StringComparison.Ordinal);
            Assert.True(next > cursor, $"Expected marker after {cursor}: {marker}");
            cursor = next;
        }
    }

    private static string Slice(string text, string start, string end)
    {
        int first = text.IndexOf(start, StringComparison.Ordinal);
        int last = text.IndexOf(end, first, StringComparison.Ordinal);
        Assert.True(first >= 0 && last > first);
        return text[first..last];
    }

    private static string ReadTrees(string root, params string[] roots) =>
        string.Join("\n", roots.Select(relative => string.Join("\n",
            Directory.EnumerateFiles(Path.Combine(root, relative), "*.cs",
                SearchOption.AllDirectories)
                .Where(path => !path.Replace('\\', '/').Contains("/bin/",
                    StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.Replace('\\', '/').Contains("/obj/",
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText))));

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(parts.Aggregate(root, Path.Combine));

    private static string Root()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "ResearchPaper")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
