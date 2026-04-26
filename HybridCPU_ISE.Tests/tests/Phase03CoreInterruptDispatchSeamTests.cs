using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Execution;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03CoreInterruptDispatchSeamTests
{
    [Fact]
    public void VectorDivideByZeroTrap_WhenCoreInterruptDispatcherIsInjected_UsesCoreOwnedDispatchSeam()
    {
        ProcessorMode originalMode = Processor.CurrentProcessorMode;
        _ = new Processor(ProcessorMode.Emulation);
        ref Processor.CPU_Core core = ref Processor.CPU_Cores[0];

        bool dispatchCalled = false;
        Processor.DeviceType capturedDeviceType = default;
        ushort capturedInterruptId = 0;
        ulong capturedCoreId = ulong.MaxValue;

        try
        {
            core.WriteVirtualThreadPipelineState(0, PipelineState.Task);
            core.WriteActiveLivePc(0x440);
            core.ExceptionStatus.Reset();
            Assert.True(core.ExceptionStatus.SetExceptionMode(1));

            core.TestSetInterruptDispatcher((deviceType, interruptId, coreId) =>
            {
                dispatchCalled = true;
                capturedDeviceType = deviceType;
                capturedInterruptId = interruptId;
                capturedCoreId = coreId;
                return 1;
            });

            byte[] left = BitConverter.GetBytes(42.0d);
            byte[] right = BitConverter.GetBytes(0.0d);
            byte[] destination = new byte[sizeof(double)];

            VectorALU.ApplyBinary(
                (uint)Processor.CPU_Core.InstructionsEnum.VDIV,
                DataTypeEnum.FLOAT64,
                left,
                right,
                destination,
                elemSize: sizeof(double),
                vl: 1,
                predIndex: 0,
                tailAgnostic: false,
                maskAgnostic: false,
                ref core);

            Assert.True(dispatchCalled);
            Assert.Equal(Processor.DeviceType.VectorUnit, capturedDeviceType);
            Assert.Equal(
                (ushort)(0x80 + (byte)Processor.CPU_Core.VectorException.DivByZero),
                capturedInterruptId);
            Assert.Equal((ulong)core.CoreID, capturedCoreId);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(0));
            Assert.True(core.SavedVectorContext.Valid);
            Assert.Equal(0x440UL, core.SavedVectorContext.FaultingPC);
            Assert.Equal(
                (uint)Processor.CPU_Core.VectorException.DivByZero,
                core.SavedVectorContext.FaultingOpCode);
        }
        finally
        {
            core.TestResetInterruptDispatcher();
            Processor.CurrentProcessorMode = originalMode;
        }
    }
}

