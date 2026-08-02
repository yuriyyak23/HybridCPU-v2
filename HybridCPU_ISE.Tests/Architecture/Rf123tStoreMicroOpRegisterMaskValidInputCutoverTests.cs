using System.Reflection;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123tStoreMicroOpRegisterMaskValidInputCutoverTests
{
    [Fact]
    public void StoreUsesTwoIndependentCheckedPathsWithExactRawFallbacks()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Memory", "MicroOp.LoadStore.cs"));
        string store = ExtractBalanced(source, "public class StoreMicroOp");
        string initialize = ExtractBalanced(
            store, "public void InitializeMetadata()");

        Assert.Equal(2, Count(initialize, "ArchRegId.TryCreate("));
        Assert.Equal(1, Count(initialize,
            "ArchRegId.TryCreate(SrcRegID, out ArchRegId sourceRegister)"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForArchitecturalRegisterRead(sourceRegister)"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterRead(SrcRegID)"));
        Assert.Equal(1, Count(initialize,
            "ArchRegId.TryCreate(BaseRegID, out ArchRegId baseRegister)"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForArchitecturalRegisterRead(baseRegister)"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterRead(BaseRegID)"));
        Assert.DoesNotContain("ArchRegId.Create(", initialize,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Math.Clamp", initialize,
            StringComparison.Ordinal);
        Assert.DoesNotContain("%", initialize, StringComparison.Ordinal);

        AssertOrdered(initialize,
            "readRegs.Add(SrcRegID)",
            "readRegs.Add(BaseRegID)",
            "ResourceMask = ResourceBitset.Zero;",
            "ArchRegId.TryCreate(SrcRegID, out ArchRegId sourceRegister)",
            "ResourceMaskBuilder.ForRegisterRead(SrcRegID)",
            "ArchRegId.TryCreate(BaseRegID, out ArchRegId baseRegister)",
            "ResourceMaskBuilder.ForRegisterRead(BaseRegID)",
            "ResourceMaskBuilder.ForStore()",
            "ResourceMaskBuilder.ForMemoryDomain(OwnerThreadId)",
            "PublishExplicitStructuralSafetyMask();",
            "RefreshAdmissionMetadata(this);");
    }

    [Fact]
    public void EveryRepresentableRegisterPreservesCheckedHelperParity()
    {
        for (int raw = ArchRegId.MinValue; raw <= ArchRegId.MaxValue; raw++)
        {
            Assert.True(ArchRegId.TryCreate(raw, out ArchRegId checkedId));
            Assert.Equal(ResourceMaskBuilder.ForRegisterRead(raw),
                ResourceMaskBuilder.ForArchitecturalRegisterRead(checkedId));

            StoreMicroOp sourceOnly = Create(
                (ushort)raw, VLIW_Instruction.NoReg);
            StoreMicroOp baseOnly = Create(
                VLIW_Instruction.NoReg, (ushort)raw);
            Assert.Equal(Expected((ushort)raw, VLIW_Instruction.NoReg),
                sourceOnly.ResourceMask);
            Assert.Equal(Expected(VLIW_Instruction.NoReg, (ushort)raw),
                baseOnly.ResourceMask);
        }
    }

    [Fact]
    public void EveryUshortPreservesIndependentFreshOperationBehavior()
    {
        for (int raw = ushort.MinValue; raw <= ushort.MaxValue; raw++)
        {
            ushort value = (ushort)raw;
            bool present = value != VLIW_Instruction.NoReg;

            StoreMicroOp sourceOnly = Create(
                value, VLIW_Instruction.NoReg);
            Assert.Equal(present ? [(int)value] : Array.Empty<int>(),
                sourceOnly.ReadRegisters);
            Assert.Equal(Expected(value, VLIW_Instruction.NoReg),
                sourceOnly.ResourceMask);

            StoreMicroOp baseOnly = Create(
                VLIW_Instruction.NoReg, value);
            Assert.Equal(present ? [(int)value] : Array.Empty<int>(),
                baseOnly.ReadRegisters);
            Assert.Equal(Expected(VLIW_Instruction.NoReg, value),
                baseOnly.ResourceMask);
        }
    }

    [Fact]
    public void SentinelDuplicateRawFallbackAndStatefulSeamsRemainFrozen()
    {
        foreach (ushort value in new ushort[] { 0, 31, 32, 255, 65534, 65535 })
        {
            StoreMicroOp operation = Create(value, value);
            bool present = value != VLIW_Instruction.NoReg;
            Assert.Equal(present ? [(int)value, (int)value] : Array.Empty<int>(),
                operation.ReadRegisters);
            Assert.Equal(Expected(value, value), operation.ResourceMask);
        }

        StoreMicroOp stateful = Create(7, 9);
        stateful.SrcRegID = VLIW_Instruction.NoReg;
        stateful.BaseRegID = VLIW_Instruction.NoReg;
        stateful.Address = 0x1000;
        stateful.InitializeMetadata();
        Assert.Empty(stateful.ReadRegisters);
        Assert.Empty(stateful.WriteRegisters);
        Assert.False(stateful.IsStealable);
        Assert.Equal(Expected(
            VLIW_Instruction.NoReg, VLIW_Instruction.NoReg),
            stateful.ResourceMask);
    }

    [Fact]
    public void PublicShapeTypedFactoriesAndExecutionBoundaryRemainUnchanged()
    {
        ConstructorInfo constructor = Assert.Single(
            typeof(StoreMicroOp).GetConstructors(
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.DeclaredOnly));
        Assert.Empty(constructor.GetParameters());
        Assert.Equal(typeof(ushort),
            typeof(StoreMicroOp).GetProperty(nameof(StoreMicroOp.SrcRegID))!
                .PropertyType);
        Assert.Equal(typeof(ushort),
            typeof(StoreMicroOp).GetProperty(nameof(StoreMicroOp.BaseRegID))!
                .PropertyType);

        foreach ((InstructionsEnum opcode, byte size) in new[]
                 {
                     (InstructionsEnum.SB, (byte)1),
                     (InstructionsEnum.SH, (byte)2),
                     (InstructionsEnum.SW, (byte)4),
                     (InstructionsEnum.SD, (byte)8),
                 })
        {
            var context = new DecoderContext
            {
                OpCode = (uint)opcode,
                Reg2ID = 7,
                Reg3ID = 9,
                MemoryAddress = 0x1000,
                HasMemoryAddress = true,
            };
            StoreMicroOp operation = Assert.IsType<StoreMicroOp>(
                InstructionRegistry.CreateMicroOp(context.OpCode, context));
            Assert.Equal(9, operation.SrcRegID);
            Assert.Equal(7, operation.BaseRegID);
            Assert.Equal(size, operation.Size);
            Assert.Equal([9, 7], operation.ReadRegisters);
            Assert.Equal(Expected(9, 7), operation.ResourceMask);
        }

        string pipeline = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Stages", "Memory",
            "CPU_Core.PipelineExecution.Memory.cs"));
        Assert.Contains(
            "GetRegisterValueWithForwarding(consumerThreadId, storeOp.SrcRegID)",
            pipeline, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "GetRegisterValueWithForwarding(consumerThreadId, storeOp.BaseRegID)",
            pipeline, StringComparison.Ordinal);
    }

    private static StoreMicroOp Create(
        ushort sourceRegister,
        ushort baseRegister)
    {
        var operation = new StoreMicroOp
        {
            SrcRegID = sourceRegister,
            BaseRegID = baseRegister,
            Address = 0xFFFF000000000000UL,
            Size = 8,
            OwnerThreadId = 0,
        };
        operation.InitializeMetadata();
        return operation;
    }

    private static ResourceBitset Expected(
        ushort sourceRegister,
        ushort baseRegister)
    {
        ResourceBitset result =
            ResourceMaskBuilder.ForStore() |
            ResourceMaskBuilder.ForMemoryDomain(0);
        if (sourceRegister != VLIW_Instruction.NoReg)
            result |= ResourceMaskBuilder.ForRegisterRead(sourceRegister);
        if (baseRegister != VLIW_Instruction.NoReg)
            result |= ResourceMaskBuilder.ForRegisterRead(baseRegister);
        return result;
    }

    private static string ExtractBalanced(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{signature}' was not found.");
        int brace = source.IndexOf('{', start);
        Assert.True(brace > start);
        int depth = 0;
        for (int index = brace; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}' && --depth == 0)
                return source[start..(index + 1)];
        }

        throw new InvalidOperationException($"'{signature}' was not closed.");
    }

    private static void AssertOrdered(string text, params string[] values)
    {
        int previous = -1;
        foreach (string value in values)
        {
            int current = text.IndexOf(
                value, previous + 1, StringComparison.Ordinal);
            Assert.True(current > previous,
                $"'{value}' must occur after offset {previous}.");
            previous = current;
        }
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset =
            text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
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
