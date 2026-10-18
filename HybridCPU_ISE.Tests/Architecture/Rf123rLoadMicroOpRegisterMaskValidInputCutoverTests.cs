using System.Reflection;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123rLoadMicroOpRegisterMaskValidInputCutoverTests
{

    [Fact]
    public void EveryRepresentableRegisterPreservesCheckedHelperParity()
    {
        for (int raw = ArchRegId.MinValue; raw <= ArchRegId.MaxValue; raw++)
        {
            Assert.True(ArchRegId.TryCreate(raw, out ArchRegId checkedId));
            Assert.Equal(ResourceMaskBuilder.ForRegisterRead(raw),
                ResourceMaskBuilder.ForArchitecturalRegisterRead(checkedId));
            Assert.Equal(ResourceMaskBuilder.ForRegisterWrite(raw),
                ResourceMaskBuilder.ForArchitecturalRegisterWrite(checkedId));

            var operation = Create((ushort)raw, (ushort)raw, true);
            Assert.Equal(Expected((ushort)raw, (ushort)raw, true),
                operation.ResourceMask);
        }
    }

    [Fact]
    public void EveryUshortPreservesFreshOperationRawBehavior()
    {
        for (int raw = ushort.MinValue; raw <= ushort.MaxValue; raw++)
        {
            ushort value = (ushort)raw;
            var operation = Create(value, value, true);
            bool present = value != VLIW_Instruction.NoReg;

            Assert.Equal(present ? [(int)value] : Array.Empty<int>(),
                operation.ReadRegisters);
            Assert.Equal(present ? [(int)value] : Array.Empty<int>(),
                operation.WriteRegisters);
            Assert.Equal(Expected(value, value, true), operation.ResourceMask);
        }
    }

    [Fact]
    public void SentinelX0RawFallbackAndStatefulSeamsRemainFrozen()
    {
        foreach (ushort value in new ushort[] { 0, 31, 32, 255, 65534, 65535 })
        {
            var operation = Create(value, value, true);
            Assert.Equal(Expected(value, value, true), operation.ResourceMask);
        }

        var stateful = Create(7, 9, true);
        stateful.WritesRegister = false;
        stateful.BaseRegID = VLIW_Instruction.NoReg;
        stateful.Address = 0x1000;
        stateful.InitializeMetadata();
        Assert.Empty(stateful.ReadRegisters);
        Assert.Equal([9], stateful.WriteRegisters);
        Assert.False(stateful.IsStealable);
        Assert.Equal(Expected(VLIW_Instruction.NoReg, 9, false),
            stateful.ResourceMask);
    }

    [Fact]
    public void PublicShapeFactoryAndPublicationOrderRemainUnchanged()
    {
        ConstructorInfo constructor = Assert.Single(
            typeof(LoadMicroOp).GetConstructors(
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.DeclaredOnly));
        Assert.Empty(constructor.GetParameters());
        Assert.Equal(typeof(ushort),
            typeof(LoadMicroOp).GetProperty(nameof(LoadMicroOp.BaseRegID))!
                .PropertyType);
        Assert.Equal(typeof(ushort),
            typeof(LoadMicroOp).GetProperty(nameof(LoadMicroOp.DestRegID))!
                .PropertyType);

        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.LD,
            Reg1ID = 9,
            Reg2ID = 7,
            MemoryAddress = 0x1000,
            HasMemoryAddress = true,
        };
        var operation = Assert.IsType<LoadMicroOp>(
            InstructionRegistry.CreateMicroOp(context.OpCode, context));
        Assert.Equal([7], operation.ReadRegisters);
        Assert.Equal([9], operation.WriteRegisters);
        Assert.Equal(Expected(7, 9, true), operation.ResourceMask);
    }

    private static LoadMicroOp Create(
        ushort baseRegister,
        ushort destinationRegister,
        bool writesRegister)
    {
        var operation = new LoadMicroOp
        {
            BaseRegID = baseRegister,
            DestRegID = destinationRegister,
            WritesRegister = writesRegister,
            Address = 0xFFFF000000000000UL,
            Size = 8,
            OwnerThreadId = 0,
        };
        operation.InitializeMetadata();
        return operation;
    }

    private static ResourceBitset Expected(
        ushort baseRegister,
        ushort destinationRegister,
        bool writesRegister)
    {
        ResourceBitset result =
            ResourceMaskBuilder.ForLoad() |
            ResourceMaskBuilder.ForMemoryDomain(0);
        if (baseRegister != VLIW_Instruction.NoReg)
            result |= ResourceMaskBuilder.ForRegisterRead(baseRegister);
        if (writesRegister && destinationRegister != VLIW_Instruction.NoReg)
            result |= ResourceMaskBuilder.ForRegisterWrite(destinationRegister);
        return result;
    }

    private static string ExtractClass(string source, string signature) =>
        ExtractBalanced(source, signature);

    private static string ExtractMethod(string source, string signature) =>
        ExtractBalanced(source, signature);

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
