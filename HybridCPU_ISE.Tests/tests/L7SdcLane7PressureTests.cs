using System.Collections.Generic;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Fences;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcLane7PressureTests
{
    [Fact]
    public void L7SdcLane7Pressure_RepeatedSubmitPollStormTriggersBoundedThrottleEvidence()
    {
        var throttle = new AcceleratorLane7PressureThrottle(maxSubmitPollPerWindow: 3);

        AcceleratorLane7PressureResult submit0 =
            throttle.TryAdmit(SystemDeviceCommandKind.Submit);
        AcceleratorLane7PressureResult poll0 =
            throttle.TryAdmit(SystemDeviceCommandKind.Poll);
        AcceleratorLane7PressureResult submit1 =
            throttle.TryAdmit(SystemDeviceCommandKind.Submit);
        AcceleratorLane7PressureResult rejected =
            throttle.TryAdmit(SystemDeviceCommandKind.Poll);

        Assert.True(submit0.Accepted, submit0.Message);
        Assert.True(poll0.Accepted, poll0.Message);
        Assert.True(submit1.Accepted, submit1.Message);
        Assert.True(rejected.Rejected);
        Assert.Equal(AcceleratorTokenFaultCode.Lane7PressureRejected, rejected.FaultCode);
        Assert.Equal(3, rejected.SubmitPollCount);
        Assert.Equal(1, rejected.ThrottleRejectCount);
        Assert.True(rejected.BranchOrSystemProgressPreserved);

        throttle.ResetWindow();
        AcceleratorLane7PressureResult afterReset =
            throttle.TryAdmit(SystemDeviceCommandKind.Poll);

        Assert.True(afterReset.Accepted, afterReset.Message);
        Assert.Equal(1, afterReset.SubmitPollCount);
        Assert.Equal(1, afterReset.ThrottleRejectCount);
    }

    [Fact]
    public void L7SdcLane7Pressure_ThrottleDoesNotBlockBranchOrSystemProgressEvidence()
    {
        var throttle = new AcceleratorLane7PressureThrottle(maxSubmitPollPerWindow: 1);
        Assert.True(throttle.TryAdmit(SystemDeviceCommandKind.Submit).Accepted);
        Assert.True(throttle.TryAdmit(SystemDeviceCommandKind.Poll).Rejected);

        AcceleratorLane7PressureResult wait =
            throttle.TryAdmit(SystemDeviceCommandKind.Wait);
        AcceleratorLane7PressureResult fence =
            throttle.TryAdmit(SystemDeviceCommandKind.Fence);
        AcceleratorLane7PressureResult progress =
            throttle.RecordBranchOrSystemProgress();

        Assert.True(wait.Accepted, wait.Message);
        Assert.True(fence.Accepted, fence.Message);
        Assert.True(progress.Accepted, progress.Message);
        Assert.True(wait.BranchOrSystemProgressPreserved);
        Assert.True(fence.BranchOrSystemProgressPreserved);
        Assert.True(progress.BranchOrSystemProgressPreserved);
        Assert.Equal(1, throttle.SubmitPollCount);
        Assert.Equal(1, throttle.ThrottleRejectCount);
    }

    [Fact]
    public void L7SdcLane7Pressure_ControlMicroOpsRemainHardPinnedSystemSingleton()
    {
        var microOps = new List<SystemDeviceCommandMicroOp>
        {
            new AcceleratorSubmitMicroOp(),
            new AcceleratorPollMicroOp(),
            new AcceleratorWaitMicroOp(),
            new AcceleratorCancelMicroOp(),
            new AcceleratorFenceMicroOp()
        };

        foreach (SystemDeviceCommandMicroOp microOp in microOps)
        {
            Assert.Equal(InstructionClass.System, microOp.InstructionClass);
            Assert.Equal(SlotClass.SystemSingleton, microOp.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned, microOp.Placement.PinningKind);
            Assert.Equal((byte)7, microOp.Placement.PinnedLaneId);
            Assert.NotEqual(SlotClass.BranchControl, microOp.Placement.RequiredSlotClass);
            Assert.False(microOp.IsControlFlow);
        }
    }
}
