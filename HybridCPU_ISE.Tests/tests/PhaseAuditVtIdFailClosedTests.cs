using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.PhaseAudit;

public sealed class PhaseAuditVtIdFailClosedTests
{
    [Fact]
    public void CpuCoreState_WhenVtIdIsInvalid_ThenFailsClosedWithoutVt0Alias()
    {
        const int invalidVtId = 4;
        const int registerId = 5;
        const ulong vt0RegisterValue = 0x1111UL;
        const ulong vt0Pc = 0x4000UL;

        var core = new Processor.CPU_Core(0);
        core.WriteCommittedArch(0, registerId, vt0RegisterValue);
        core.WriteCommittedPc(0, vt0Pc);

        InvalidVirtualThreadException readFault =
            Assert.Throws<InvalidVirtualThreadException>(() => core.ReadArch(invalidVtId, registerId));
        AssertInvalidVtFault(readFault, nameof(core.ReadArch), invalidVtId);

        InvalidVirtualThreadException registerWriteFault =
            Assert.Throws<InvalidVirtualThreadException>(
                () => core.WriteCommittedArch(invalidVtId, registerId, 0x2222UL));
        AssertInvalidVtFault(registerWriteFault, nameof(core.WriteCommittedArch), invalidVtId);

        InvalidVirtualThreadException pcReadFault =
            Assert.Throws<InvalidVirtualThreadException>(() => core.ReadCommittedPc(invalidVtId));
        AssertInvalidVtFault(pcReadFault, nameof(core.ReadCommittedPc), invalidVtId);

        InvalidVirtualThreadException pcWriteFault =
            Assert.Throws<InvalidVirtualThreadException>(() => core.WriteCommittedPc(invalidVtId, 0x5000UL));
        AssertInvalidVtFault(pcWriteFault, "PublishVirtualThreadPcOwnership", invalidVtId);

        Assert.Equal(vt0RegisterValue, core.ReadArch(0, registerId));
        Assert.Equal(vt0Pc, core.ReadCommittedPc(0));
    }

    [Fact]
    public void LiveCpuStateAdapter_WhenVtIdIsInvalid_ThenFailsClosedWithoutVt0Alias()
    {
        const byte invalidVtId = 4;
        const int registerId = 6;
        const ulong vt0RegisterValue = 0x3333UL;
        const ulong vt0Pc = 0x6000UL;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(vt0Pc, activeVtId: 0);
        core.WriteCommittedArch(0, registerId, vt0RegisterValue);
        core.WriteCommittedPc(0, vt0Pc);

        var state = core.CreateLiveCpuStateAdapter(0);

        InvalidVirtualThreadException readFault =
            Assert.Throws<InvalidVirtualThreadException>(() => state.ReadRegister(invalidVtId, registerId));
        AssertInvalidVtFault(readFault, nameof(state.ReadRegister), invalidVtId);

        InvalidVirtualThreadException registerWriteFault =
            Assert.Throws<InvalidVirtualThreadException>(
                () => state.WriteRegister(invalidVtId, registerId, 0x4444UL));
        AssertInvalidVtFault(registerWriteFault, nameof(state.WriteRegister), invalidVtId);

        InvalidVirtualThreadException pcReadFault =
            Assert.Throws<InvalidVirtualThreadException>(() => state.ReadPc(invalidVtId));
        AssertInvalidVtFault(pcReadFault, nameof(state.ReadPc), invalidVtId);

        InvalidVirtualThreadException pcWriteFault =
            Assert.Throws<InvalidVirtualThreadException>(() => state.WritePc(invalidVtId, 0x7000UL));
        AssertInvalidVtFault(pcWriteFault, nameof(state.WritePc), invalidVtId);

        state.ApplyTo(ref core);

        Assert.Equal(vt0RegisterValue, core.ReadArch(0, registerId));
        Assert.Equal(vt0Pc, core.ReadCommittedPc(0));
        Assert.Equal(vt0Pc, core.ReadActiveLivePc());
    }

    [Fact]
    public void CpuCoreState_WhenVtIdIsNegative_ThenFailsClosedWithoutVt0Alias()
    {
        const int invalidVtId = -1;
        const int registerId = 7;
        const ulong vt0RegisterValue = 0x5555UL;

        var core = new Processor.CPU_Core(0);
        core.WriteCommittedArch(0, registerId, vt0RegisterValue);

        InvalidVirtualThreadException readFault =
            Assert.Throws<InvalidVirtualThreadException>(() => core.ReadArch(invalidVtId, registerId));
        AssertInvalidVtFault(readFault, nameof(core.ReadArch), invalidVtId);

        Assert.Equal(vt0RegisterValue, core.ReadArch(0, registerId));
    }

    private static void AssertInvalidVtFault(
        InvalidVirtualThreadException fault,
        string expectedOperation,
        int expectedVtId)
    {
        Assert.Equal(expectedOperation, fault.Operation);
        Assert.Equal(expectedVtId, fault.VtId);
        Assert.Equal(Processor.CPU_Core.SmtWays, fault.VtCount);
        Assert.Equal(ExecutionFaultCategory.InvalidVirtualThread, fault.Category);
        Assert.Equal(
            ExecutionFaultCategory.InvalidVirtualThread,
            ExecutionFaultContract.GetCategory(fault));
    }
}
