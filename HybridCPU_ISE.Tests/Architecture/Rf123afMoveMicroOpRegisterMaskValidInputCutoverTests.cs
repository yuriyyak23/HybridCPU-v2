using System.Reflection;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123afMoveMicroOpRegisterMaskValidInputCutoverTests
{
    [Fact]
    public void PaperAuthorizesOnlyTheTwoCheckedSelectionsWithRawFallbacks()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains("A later valid-input-only cutover may branch independently at the one read-loop\nfold and the one write-loop fold",
            paper, StringComparison.Ordinal);
        Assert.Contains("Every already-participating x0..x31 value\nmay use the distinctly named checked `ArchRegId` read or write mask entry\npoint",
            paper, StringComparison.Ordinal);
        Assert.Contains("every other participating `ushort` must retain the exact raw helper",
            paper, StringComparison.Ordinal);
        Assert.Contains("Invalid-input behavior, signature migration, constructor\nhardening, reachability changes and raw API removal require separate decisions",
            paper, StringComparison.Ordinal);
    }

    [Fact]
    public void ExactlyTwoFoldsSelectCheckedPathsAndRetainRawFallbacks()
    {
        string carrier = ExtractBalanced(Read(FindRepositoryRoot(),
                "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps",
                "Types", "MicroOp.Misc.cs"),
            "public class MoveMicroOp");
        string body = ExtractBalanced(carrier,
            "private void InitializeMetadata()");

        Assert.Equal(2, Count(body, "ArchRegId.TryCreate("));
        Assert.Equal(1, Count(body,
            "ResourceMaskBuilder.ForArchitecturalRegisterRead("));
        Assert.Equal(1, Count(body,
            "ResourceMaskBuilder.ForArchitecturalRegisterWrite("));
        Assert.Equal(1, Count(body, "ResourceMaskBuilder.ForRegisterRead("));
        Assert.Equal(1, Count(body, "ResourceMaskBuilder.ForRegisterWrite("));
        Assert.Equal(1, Count(body, "ResourceMaskBuilder.ForStore("));
        Assert.Equal(1, Count(body, "ResourceMaskBuilder.ForLoad("));

        AssertOrdered(body,
            "ReadRegisters = readRegs;",
            "WriteRegisters =",
            "ResourceMask = ResourceBitset.Zero;",
            "for (int i = 0; i < readRegs.Count; i++)",
            "ArchRegId.TryCreate(readRegs[i]",
            "ResourceMaskBuilder.ForArchitecturalRegisterRead(readRegister)",
            "ResourceMaskBuilder.ForRegisterRead(readRegs[i])",
            "for (int i = 0; i < writeRegs.Count; i++)",
            "ArchRegId.TryCreate(writeRegs[i]",
            "ResourceMaskBuilder.ForArchitecturalRegisterWrite(writeRegister)",
            "ResourceMaskBuilder.ForRegisterWrite(writeRegs[i])",
            "if (dataType == 2)",
            "ResourceMaskBuilder.ForStore()",
            "else if (dataType == 3)",
            "ResourceMaskBuilder.ForLoad()");
    }

    [Fact]
    public void EveryRepresentableRegisterMatchesFormerRawMasksInEveryMoveRole()
    {
        for (int raw = ArchRegId.MinValue; raw <= ArchRegId.MaxValue; raw++)
        {
            Assert.True(ArchRegId.TryCreate(raw, out ArchRegId checkedId));
            Assert.Equal(ResourceMaskBuilder.ForRegisterRead(raw),
                ResourceMaskBuilder.ForArchitecturalRegisterRead(checkedId));
            Assert.Equal(ResourceMaskBuilder.ForRegisterWrite(raw),
                ResourceMaskBuilder.ForArchitecturalRegisterWrite(checkedId));

            MoveMicroOp move = Create(0, (ushort)raw, (ushort)raw);
            move.RefreshWriteMetadata();
            Assert.Equal([raw], move.ReadRegisters);
            Assert.Equal([raw], move.WriteRegisters);
            Assert.Equal(
                ResourceMaskBuilder.ForRegisterRead(raw) |
                ResourceMaskBuilder.ForRegisterWrite(raw),
                move.ResourceMask);

            MoveMicroOp store = Create(2, (ushort)raw, 19);
            store.RefreshWriteMetadata();
            Assert.Equal([raw], store.ReadRegisters);
            Assert.Empty(store.WriteRegisters);
            Assert.Equal(
                ResourceMaskBuilder.ForRegisterRead(raw) |
                ResourceMaskBuilder.ForStore(),
                store.ResourceMask);

            MoveMicroOp load = Create(3, (ushort)raw, 23);
            load.RefreshWriteMetadata();
            Assert.Empty(load.ReadRegisters);
            Assert.Equal([raw], load.WriteRegisters);
            Assert.Equal(
                ResourceMaskBuilder.ForRegisterWrite(raw) |
                ResourceMaskBuilder.ForLoad(),
                load.ResourceMask);
        }
    }

    [Fact]
    public void EveryParticipatingNonrepresentableUshortKeepsExactRawFallback()
    {
        for (int raw = ArchRegId.MaxValue + 1;
             raw < VLIW_Instruction.NoReg;
             raw++)
        {
            Assert.False(ArchRegId.TryCreate(raw, out _));
            MoveMicroOp move = Create(0, (ushort)raw, (ushort)raw);
            move.RefreshWriteMetadata();

            Assert.Equal([raw], move.ReadRegisters);
            Assert.Equal([raw], move.WriteRegisters);
            Assert.Equal(
                ResourceMaskBuilder.ForRegisterRead(raw) |
                ResourceMaskBuilder.ForRegisterWrite(raw),
                move.ResourceMask);
        }
    }

    [Fact]
    public void NoRegPredicatesDefaultX0FailureWinnersAndMemoryFoldsStayExact()
    {
        MoveMicroOp absent = Create(0, VLIW_Instruction.NoReg,
            VLIW_Instruction.NoReg);
        absent.RefreshWriteMetadata();
        Assert.Empty(absent.ReadRegisters);
        Assert.Empty(absent.WriteRegisters);
        Assert.Equal(ResourceBitset.Zero, absent.ResourceMask);

        var fresh = new MoveMicroOp();
        fresh.RefreshWriteMetadata();
        Assert.Equal([0], fresh.ReadRegisters);
        Assert.Equal([0], fresh.WriteRegisters);
        Assert.Equal(
            ResourceMaskBuilder.ForRegisterRead(0) |
            ResourceMaskBuilder.ForRegisterWrite(0),
            fresh.ResourceMask);

        MoveMicroOp carrier = Create(0, 7, 8);
        carrier.RefreshWriteMetadata();
        IReadOnlyList<int> reads = carrier.ReadRegisters;
        IReadOnlyList<int> writes = carrier.WriteRegisters;
        ResourceBitset mask = carrier.ResourceMask;

        VLIW_Instruction instruction = carrier.Instruction;
        instruction.DataType = 4;
        carrier.Instruction = instruction;
        Assert.Throws<InvalidOperationException>(
            carrier.RefreshWriteMetadata);
        Assert.Same(reads, carrier.ReadRegisters);
        Assert.Same(writes, carrier.WriteRegisters);
        Assert.Equal(mask, carrier.ResourceMask);

        instruction.DataType = 6;
        carrier.Instruction = instruction;
        Assert.Throws<InvalidOperationException>(
            carrier.RefreshWriteMetadata);
        Assert.Same(reads, carrier.ReadRegisters);
        Assert.Same(writes, carrier.WriteRegisters);
        Assert.Equal(mask, carrier.ResourceMask);

        MoveMicroOp storeWithoutRegister =
            Create(2, VLIW_Instruction.NoReg, 9);
        storeWithoutRegister.RefreshWriteMetadata();
        Assert.Equal(ResourceMaskBuilder.ForStore(),
            storeWithoutRegister.ResourceMask);

        MoveMicroOp loadWithoutRegister =
            Create(3, VLIW_Instruction.NoReg, 9);
        loadWithoutRegister.RefreshWriteMetadata();
        Assert.Equal(ResourceMaskBuilder.ForLoad(),
            loadWithoutRegister.ResourceMask);
    }

    [Fact]
    public void SignaturesPlacementExecutionAliasesAndOtherFamiliesRemainFrozen()
    {
        Type type = typeof(MoveMicroOp);
        ConstructorInfo constructor = Assert.Single(type.GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(constructor.GetParameters());
        MethodInfo initialize = type.GetMethod("InitializeMetadata",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.True(initialize.IsPrivate);
        Assert.Empty(initialize.GetParameters());

        MoveMicroOp invalidSource = Create(0, 32, 1);
        invalidSource.RefreshWriteMetadata();
        var core = new Processor.CPU_Core(0);
        Assert.True(invalidSource.Execute(ref core));
        Assert.True(invalidSource.TryGetPrimaryWriteBackResult(
            out ulong value));
        Assert.Equal(0UL, value);
        Assert.Equal(SlotPinningKind.ClassFlexible,
            invalidSource.Placement.PinningKind);
        Assert.Equal(SlotClass.AluClass,
            invalidSource.Placement.RequiredSlotClass);

        MoveMicroOp store = Create(2, 1, 2);
        store.RefreshWriteMetadata();
        Assert.Throws<InvalidOperationException>(
            () => store.Execute(ref core));
        MoveMicroOp load = Create(3, 1, 2);
        load.RefreshWriteMetadata();
        Assert.Throws<InvalidOperationException>(
            () => load.Execute(ref core));

        string carrier = ExtractBalanced(Read(FindRepositoryRoot(),
                "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps",
                "Types", "MicroOp.Misc.cs"),
            "public class MoveMicroOp");
        foreach (string unrelated in new[]
                 {
                     "MemoryBankId", "AcceleratorTokenHandle", "ChannelId",
                     "DomainId", "TokenId", "SlotId"
                 })
        {
            Assert.DoesNotContain(unrelated, carrier,
                StringComparison.Ordinal);
        }
    }

    private static MoveMicroOp Create(
        byte dataType,
        ushort reg1Id,
        ushort reg2Id) =>
        new()
        {
            Instruction = new VLIW_Instruction
            {
                DataType = dataType,
                Word1 = (ulong)reg1Id | ((ulong)reg2Id << 16),
                Src2Pointer = 0x1234
            }
        };

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
                $"'{marker}' was not found after offset {previous}.");
            previous = index;
        }
    }

    private static int Count(string source, string token)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(token, offset,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += token.Length;
        }
        return count;
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine([root, .. parts]));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory =
            new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
