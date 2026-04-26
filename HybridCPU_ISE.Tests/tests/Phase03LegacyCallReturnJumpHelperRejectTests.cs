using System;
using System.Globalization;
using HybridCPU.Compiler.Core.API.Facade;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03LegacyCallReturnJumpHelperRejectTests
{
    private const string ProhibitedFragment = "rejects raw CALL/RET/JMP wrappers as prohibited opcodes";

    private static void AssertHardwareOpcodeIsNotProhibited(uint opCode)
    {
        Assert.DoesNotContain(opCode.ToString(CultureInfo.InvariantCulture), IsaV4Surface.ProhibitedOpcodes);

        OpcodeInfo? info = OpcodeRegistry.GetInfo(opCode);
        Assert.True(info.HasValue, $"Expected opcode 0x{opCode:X} to remain published through canonical registry.");
        Assert.DoesNotContain(info.Value.Mnemonic, IsaV4Surface.ProhibitedOpcodes);
    }
    /*
        [Fact]
        public void CallArchReg_HelperRejectsInCompilerMode()
        {
            _ = new Processor(ProcessorMode.Compiler);
            Processor.EntryPoint entryPoint = default;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => Processor.CPU_Cores[0].Call(
                    ref entryPoint,
                    ArchRegId.Create(1),
                    entryPointMemoryAddress: 0x180));

            Assert.Contains("CALL wrapper contour is unsupported", exception.Message);
            Assert.Contains(ProhibitedFragment, exception.Message);
        }

        [Fact]
        public void ReturnArchReg_HelperRejectsInEmulationMode()
        {
            _ = new Processor(ProcessorMode.Emulation);
            Processor.EntryPoint entryPoint = default;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => Processor.CPU_Cores[0].Return(
                    ref entryPoint,
                    ArchRegId.Create(2)));

            Assert.Contains("RET wrapper contour is unsupported", exception.Message);
            Assert.Contains(ProhibitedFragment, exception.Message);
        }

        [Fact]
        public void JumpArchReg_HelperRejectsInCompilerMode()
        {
            _ = new Processor(ProcessorMode.Compiler);
            Processor.EntryPoint entryPoint = default;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => Processor.CPU_Cores[0].Jump(
                    ref entryPoint,
                    ArchRegId.Create(3),
                    memoryAddress: 0x1C0));

            Assert.Contains("JMP wrapper contour is unsupported", exception.Message);
            Assert.Contains(ProhibitedFragment, exception.Message);
        }

        [Fact]
        public void CallIntRegister_HelperRejectsInEmulationMode()
        {
            _ = new Processor(ProcessorMode.Emulation);
            Processor.EntryPoint entryPoint = default;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => Processor.CPU_Cores[0].Call(
                    ref entryPoint,
                    new Processor.CPU_Core.IntRegister(1, 0),
                    EntryPoint_MemoryAddress: 0x200));

            Assert.Contains("CALL wrapper contour is unsupported", exception.Message);
            Assert.Contains(ProhibitedFragment, exception.Message);
        }

        [Fact]
        public void ReturnIntRegister_HelperRejectsInCompilerMode()
        {
            _ = new Processor(ProcessorMode.Compiler);
            Processor.EntryPoint entryPoint = default;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => Processor.CPU_Cores[0].Return(
                    ref entryPoint,
                    new Processor.CPU_Core.IntRegister(2, 0)));

            Assert.Contains("RET wrapper contour is unsupported", exception.Message);
            Assert.Contains(ProhibitedFragment, exception.Message);
        }

        [Fact]
        public void JumpIntRegister_HelperRejectsInEmulationMode()
        {
            _ = new Processor(ProcessorMode.Emulation);
            Processor.EntryPoint entryPoint = default;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => Processor.CPU_Cores[0].Jump(
                    ref entryPoint,
                    new Processor.CPU_Core.IntRegister(3, 0),
                    MemoryAddress: 0x240));

            Assert.Contains("JMP wrapper contour is unsupported", exception.Message);
            Assert.Contains(ProhibitedFragment, exception.Message);
        }*/

    [Fact]
    public void JumpWrapper_IsNotPublishedInCanonicalOpcodeRegistry()
    {
        Assert.Contains("JMP", IsaV4Surface.ProhibitedOpcodes);
        Assert.Contains("18", IsaV4Surface.ProhibitedOpcodes);
        Assert.Null(OpcodeRegistry.GetInfo(18u));
        Assert.False(OpcodeRegistry.IsControlFlowOp(18u));
        Assert.DoesNotContain(OpcodeRegistry.Opcodes, info => info.OpCode == 18u);
        Assert.DoesNotContain(OpcodeRegistry.Opcodes, info => string.Equals(info.Mnemonic, "JMP", StringComparison.Ordinal));
    }

    [Fact]
    public void ProhibitedOpcodes_HaveNoIntersectionWithLiveOpcodeRegistryPublication()
    {
        foreach (OpcodeInfo info in OpcodeRegistry.Opcodes)
        {
            Assert.DoesNotContain(info.Mnemonic, IsaV4Surface.ProhibitedOpcodes);
            Assert.DoesNotContain(
                info.OpCode.ToString(CultureInfo.InvariantCulture),
                IsaV4Surface.ProhibitedOpcodes);
        }
    }

    [Fact]
    public void InstructionEncoder_CsrHelpers_EmitOnlyNonProhibitedHardwareOpcodes()
    {
        VLIW_Instruction[] emittedInstructions =
        [
            InstructionEncoder.EncodeCSRRead(CsrAddresses.Mstatus, destReg: 6),
            InstructionEncoder.EncodeCSRWrite(CsrAddresses.Mstatus, srcReg: 5),
            InstructionEncoder.EncodeClearExceptionCounters(),
        ];

        foreach (VLIW_Instruction instruction in emittedInstructions)
        {
            AssertHardwareOpcodeIsNotProhibited(instruction.OpCode);
        }
    }

    [Fact]
    public void PlatformAsmFacade_CsrHelpers_EmitOnlyNonProhibitedHardwareOpcodes()
    {
        var context = new HybridCpuThreadCompilerContext(0);
        var facade = new PlatformAsmFacade(0, context);

        facade.CsrRead(new AsmRegister(6), CsrAddresses.Mstatus);
        facade.CsrWrite(CsrAddresses.Mstatus, new AsmRegister(5));
        facade.CsrClear();

        Assert.Equal(3, context.InstructionCount);

        foreach (VLIW_Instruction instruction in context.GetCompiledInstructions())
        {
            AssertHardwareOpcodeIsNotProhibited(instruction.OpCode);
        }
    }
}

