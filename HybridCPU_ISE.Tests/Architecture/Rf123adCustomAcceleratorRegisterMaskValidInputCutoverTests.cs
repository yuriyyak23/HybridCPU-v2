using System.Reflection;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123adCustomAcceleratorRegisterMaskValidInputCutoverTests
{
    [Fact]
    public void PaperAuthorizesOnlyTwoCheckedRegisterBranchesAndRawFallbacks()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains("A later valid-input-only cutover may branch independently for each element at\nthe one existing read fold and at the one conditional destination fold",
            paper, StringComparison.Ordinal);
        Assert.Contains("Already-participating x0..x31 values may use the distinctly named checked\n`ArchRegId` read or write mask entry point",
            paper, StringComparison.Ordinal);
        Assert.Contains("every other `int` must retain the\nexact raw helper",
            paper, StringComparison.Ordinal);
        Assert.Contains("`acceleratorId` and `ForAccelerator` must remain raw and\nunchanged",
            paper, StringComparison.Ordinal);
    }

    [Fact]
    public void ExactlyTwoRegisterFoldsSelectCheckedPathsAndKeepAcceleratorRaw()
    {
        string source = Read(FindRepositoryRoot(), "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "MicroOps", "Types",
            "MicroOp.Misc.cs");
        string carrier = ExtractBalanced(source,
            "public class CustomAcceleratorMicroOp");
        string body = ExtractBalanced(carrier,
            "public void InitializeMetadata(int acceleratorId, int[] inputRegIds, int outputRegId)");

        Assert.Equal(2, Count(body, "ArchRegId.TryCreate("));
        Assert.Equal(1, Count(body,
            "ResourceMaskBuilder.ForArchitecturalRegisterRead("));
        Assert.Equal(1, Count(body,
            "ResourceMaskBuilder.ForArchitecturalRegisterWrite("));
        Assert.Equal(1, Count(body, "ResourceMaskBuilder.ForRegisterRead("));
        Assert.Equal(1, Count(body, "ResourceMaskBuilder.ForRegisterWrite("));
        Assert.Equal(1, Count(body, "ResourceMaskBuilder.ForAccelerator("));
        Assert.DoesNotContain("AcceleratorId", body,
            StringComparison.Ordinal);
        Assert.DoesNotContain("TryCreate(acceleratorId", body,
            StringComparison.Ordinal);

        AssertOrdered(body,
            "ReadRegisters = inputRegIds;",
            "if (WritesRegister)",
            "ResourceMask = ResourceBitset.Zero;",
            "ResourceMaskBuilder.ForAccelerator(acceleratorId)",
            "foreach (int regId in inputRegIds)",
            "ArchRegId.TryCreate(regId",
            "ResourceMaskBuilder.ForArchitecturalRegisterRead(inputRegister)",
            "ResourceMaskBuilder.ForRegisterRead(regId)",
            "if (WritesRegister)",
            "ArchRegId.TryCreate(outputRegId",
            "ResourceMaskBuilder.ForArchitecturalRegisterWrite(outputRegister)",
            "ResourceMaskBuilder.ForRegisterWrite(outputRegId)",
            "PublishExplicitStructuralSafetyMask();",
            "RefreshAdmissionMetadata(this);");
    }

    [Fact]
    public void EveryRepresentableRegisterMatchesTheFormerRawReadAndWriteMasks()
    {
        for (int raw = ArchRegId.MinValue; raw <= ArchRegId.MaxValue; raw++)
        {
            Assert.True(ArchRegId.TryCreate(raw, out ArchRegId checkedId));
            Assert.Equal(ResourceMaskBuilder.ForRegisterRead(raw),
                ResourceMaskBuilder.ForArchitecturalRegisterRead(checkedId));
            Assert.Equal(ResourceMaskBuilder.ForRegisterWrite(raw),
                ResourceMaskBuilder.ForArchitecturalRegisterWrite(checkedId));

            var carrier = new CustomAcceleratorMicroOp
            {
                WritesRegister = true
            };
            carrier.InitializeMetadata(2, [raw], raw);
            Assert.Equal([raw], carrier.ReadRegisters);
            Assert.Equal([raw], carrier.WriteRegisters);
            Assert.Equal(
                ResourceMaskBuilder.ForAccelerator(2) |
                ResourceMaskBuilder.ForRegisterRead(raw) |
                ResourceMaskBuilder.ForRegisterWrite(raw),
                carrier.ResourceMask);
        }
    }

    [Fact]
    public void BoundaryRawValuesOrderingDuplicatesAndX0RetainExactBehavior()
    {
        foreach (int raw in BoundaryValues())
        {
            var carrier = new CustomAcceleratorMicroOp
            {
                WritesRegister = true,
                DestRegID = 29
            };
            int[] reads = [raw, 0, raw];
            carrier.InitializeMetadata(3, reads, raw);

            Assert.Same(reads, carrier.ReadRegisters);
            Assert.Equal([raw], carrier.WriteRegisters);
            Assert.Equal(29, carrier.DestRegID);
            Assert.Equal(
                ResourceMaskBuilder.ForAccelerator(3) |
                ResourceMaskBuilder.ForRegisterRead(raw) |
                ResourceMaskBuilder.ForRegisterRead(0) |
                ResourceMaskBuilder.ForRegisterWrite(raw),
                carrier.ResourceMask);
        }
    }

    [Fact]
    public void InvalidAcceleratorNullArrayAndStatefulWriteListRemainUnchanged()
    {
        var invalidAccelerator = new CustomAcceleratorMicroOp
        {
            WritesRegister = true
        };
        int[] reads = [7];
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                invalidAccelerator.InitializeMetadata(4, reads, 8));
        Assert.Equal("accelId", exception.ParamName);
        Assert.Same(reads, invalidAccelerator.ReadRegisters);
        Assert.Equal([8], invalidAccelerator.WriteRegisters);
        Assert.Equal(ResourceBitset.Zero, invalidAccelerator.ResourceMask);

        var nullReads = new CustomAcceleratorMicroOp
        {
            WritesRegister = true
        };
        Assert.Throws<NullReferenceException>(() =>
            nullReads.InitializeMetadata(1, null!, 11));
        Assert.Null(nullReads.ReadRegisters);
        Assert.Equal([11], nullReads.WriteRegisters);
        Assert.Equal(ResourceMaskBuilder.ForAccelerator(1),
            nullReads.ResourceMask);

        var staleWrite = new CustomAcceleratorMicroOp
        {
            WritesRegister = true
        };
        staleWrite.InitializeMetadata(0, [1], 4);
        staleWrite.WritesRegister = false;
        staleWrite.InitializeMetadata(0, [2], 9);
        Assert.Equal([4], staleWrite.WriteRegisters);
        Assert.False(staleWrite.AdmissionMetadata.WritesRegister);
        Assert.Equal(
            ResourceMaskBuilder.ForAccelerator(0) |
            ResourceMaskBuilder.ForRegisterRead(2),
            staleWrite.ResourceMask);
    }

    [Fact]
    public void PlacementFailClosedExecutionGenericWriteBackAndUnrelatedFamiliesStayFrozen()
    {
        var carrier = new CustomAcceleratorMicroOp
        {
            OpCode = 0xFFFF,
            WritesRegister = true,
            DestRegID = 7,
            OwnerThreadId = 0
        };
        Assert.Equal(SlotClass.Unclassified,
            carrier.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned,
            carrier.Placement.PinningKind);
        Assert.Equal(0, carrier.Placement.PinnedLaneId);

        YAKSys_Hybrid_CPU.Processor.CPU_Core core = new(0);
        Assert.Throws<InvalidOpcodeException>(() => carrier.Execute(ref core));
        carrier.CapturePrimaryWriteBackResult(0xA5UL);

        Span<RetireRecord> records = stackalloc RetireRecord[1];
        int count = 0;
        carrier.EmitWriteBackRetireRecords(ref core, records, ref count);
        Assert.Equal(1, count);
        Assert.Equal(7, records[0].ArchReg);
        Assert.Equal(0xA5UL, records[0].Value);

        Type type = typeof(CustomAcceleratorMicroOp);
        MethodInfo initialize = type.GetMethod(
            nameof(CustomAcceleratorMicroOp.InitializeMetadata)) ??
            throw new MissingMethodException();
        Assert.Equal(
            [typeof(int), typeof(int[]), typeof(int)],
            initialize.GetParameters().Select(parameter =>
                parameter.ParameterType).ToArray());

        string root = FindRepositoryRoot();
        string runtime = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "InstructionRegistry.Runtime.cs");
        Assert.Contains("throw CreateUnsupportedCustomAcceleratorException(opCode)",
            runtime, StringComparison.Ordinal);

        string carrierSource = ExtractBalanced(Read(root, "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "MicroOps", "Types",
            "MicroOp.Misc.cs"), "public class CustomAcceleratorMicroOp");
        foreach (string unrelated in new[]
                 {
                     "MemoryBankId", "AcceleratorTokenHandle", "ChannelId",
                     "DomainId", "TokenId", "SlotId"
                 })
        {
            Assert.DoesNotContain(unrelated, carrierSource,
                StringComparison.Ordinal);
        }
    }

    private static IEnumerable<int> BoundaryValues()
    {
        yield return int.MinValue;
        yield return -1_000_000;
        yield return -65;
        yield return -64;
        yield return -63;
        yield return -4;
        yield return -3;
        yield return -2;
        yield return -1;
        for (int value = 0; value <= 40; value++)
            yield return value;
        yield return 63;
        yield return 64;
        yield return 255;
        yield return 65_535;
        yield return int.MaxValue;
    }

    private static string ExtractBalanced(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{signature}' was not found.");
        int brace = source.IndexOf('{', start);
        int depth = 0;
        for (int index = brace; index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            else if (source[index] == '}' && --depth == 0)
                return source[start..(index + 1)];
        }
        throw new InvalidOperationException($"'{signature}' was not closed.");
    }

    private static void AssertOrdered(string source, params string[] markers)
    {
        int previous = -1;
        foreach (string marker in markers)
        {
            int index = source.IndexOf(marker, previous + 1,
                StringComparison.Ordinal);
            Assert.True(index > previous,
                $"Marker '{marker}' was missing or out of order.");
            previous = index;
        }
    }

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

    private static int Count(string text, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = text.IndexOf(value, offset,
            StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException(
            "HybridCPU repository root was not found.");
    }
}
