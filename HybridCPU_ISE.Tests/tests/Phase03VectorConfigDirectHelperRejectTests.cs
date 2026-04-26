using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03VectorConfigDirectHelperRejectTests
{
    [Theory]
    [InlineData(InstructionsEnum.VSETVL)]
    [InlineData(InstructionsEnum.VSETVLI)]
    [InlineData(InstructionsEnum.VSETIVLI)]
    public void DirectVectorConfigHelpers_WhenRetainedHelperSurfaceIsInvoked_ThenRejectWithoutVectorConfigOrRdMutation(
        InstructionsEnum opcode)
    {
        const int vtId = 0;
        const int rd = 4;
        const ulong originalRdValue = 0xAA55UL;

        var core = new Processor.CPU_Core(0);
        core.VectorConfig.VL = 9;
        core.VectorConfig.VTYPE = 0x47;
        core.VectorConfig.TailAgnostic = 1;
        core.VectorConfig.MaskAgnostic = 1;
        core.WriteCommittedArch(vtId, rd, originalRdValue);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => InvokeVectorConfigHelper(ref core, opcode, rd));

        Assert.Contains(opcode.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains("authoritative lane-7 retire/apply carrier", exception.Message, StringComparison.Ordinal);
        Assert.Equal(9UL, core.VectorConfig.VL);
        Assert.Equal(0x47UL, core.VectorConfig.VTYPE);
        Assert.Equal((byte)1, core.VectorConfig.TailAgnostic);
        Assert.Equal((byte)1, core.VectorConfig.MaskAgnostic);
        Assert.Equal(originalRdValue, core.ReadArch(vtId, rd));
    }

    [Fact]
    public void DirectCsrReadHelper_WhenRetainedHelperSurfaceIsInvoked_ThenRejectsWithoutZeroDefaultReadback()
    {
        const int vtId = 0;
        const int destinationRegister = 5;
        const ushort unsupportedCsr = 0x8FF;
        const ulong originalDestinationValue = 0xDEAD_BEEFUL;

        var core = new Processor.CPU_Core(0);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => core.ExecuteCSRRead(Register(destinationRegister), unsupportedCsr));

        Assert.Contains("ExecuteCSRRead", exception.Message, StringComparison.Ordinal);
        Assert.Contains("CSR 0x8FF", exception.Message, StringComparison.Ordinal);
        Assert.Contains("zero-default readback", exception.Message, StringComparison.Ordinal);
        Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));
    }

    [Fact]
    public void DirectCsrWriteHelper_WhenWritableSurfaceWouldMutateState_ThenRejectsWithoutEagerCsrMutation()
    {
        const int vtId = 0;
        const int sourceRegister = 6;
        const ushort writableCsr = 0xA00;

        var core = new Processor.CPU_Core(0);
        core.VectorConfig.FSP_Enabled = 0;
        core.WriteCommittedArch(vtId, sourceRegister, 1UL);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => core.ExecuteCSRWrite(Register(sourceRegister), writableCsr));

        Assert.Contains("ExecuteCSRWrite", exception.Message, StringComparison.Ordinal);
        Assert.Contains("CSR 0xA00", exception.Message, StringComparison.Ordinal);
        Assert.Contains("eager helper-side read/write mutation", exception.Message, StringComparison.Ordinal);
        Assert.Equal((byte)0, core.VectorConfig.FSP_Enabled);
    }

    [Fact]
    public void DirectCsrWriteHelper_WhenReadOnlySurfaceWouldBeSilentlyIgnored_ThenRejectsFailClosed()
    {
        const int vtId = 0;
        const int sourceRegister = 7;
        const ushort readOnlyCsr = 0xA10;

        var core = new Processor.CPU_Core(0);
        core.VectorConfig.FSP_InjectionCount = 12;
        core.WriteCommittedArch(vtId, sourceRegister, 0x1234UL);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => core.ExecuteCSRWrite(Register(sourceRegister), readOnlyCsr));

        Assert.Contains("ExecuteCSRWrite", exception.Message, StringComparison.Ordinal);
        Assert.Contains("CSR 0xA10", exception.Message, StringComparison.Ordinal);
        Assert.Contains("silent write-ignore", exception.Message, StringComparison.Ordinal);
        Assert.Equal(12UL, core.VectorConfig.FSP_InjectionCount);
    }

    private static void InvokeVectorConfigHelper(
        ref Processor.CPU_Core core,
        InstructionsEnum opcode,
        int rd)
    {
        switch (opcode)
        {
            case InstructionsEnum.VSETVL:
                core.ExecuteVSETVL(Register(rd), Register(5), Register(6));
                break;

            case InstructionsEnum.VSETVLI:
                core.ExecuteVSETVLI(Register(rd), Register(5), vtypeImm: 0x47);
                break;

            case InstructionsEnum.VSETIVLI:
                core.ExecuteVSETIVLI(Register(rd), avlImm: 13, vtypeImm: 0x47);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null);
        }
    }

    private static Processor.CPU_Core.IntRegister Register(int archRegisterId) =>
        new((ushort)archRegisterId, init_CoreOwnerID: 0);
}
