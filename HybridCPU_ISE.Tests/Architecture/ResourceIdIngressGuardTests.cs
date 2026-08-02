using System;
using System.Reflection;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-01 narrow ingress guards. Full checked-ID type migration remains RF-12.
/// </summary>
public sealed class ResourceIdIngressGuardTests
{
    [Fact]
    public void ExecutionOwnerVirtualThreadId_InvalidValue_ThrowsInsteadOfAliasingVtZero()
    {
        var microOp = new VirtualThreadGuardProbeMicroOp();

        Assert.Equal(0, microOp.RequireExecutionVirtualThreadId(0));
        Assert.Equal(Processor.CPU_Core.SmtWays - 1,
            microOp.RequireExecutionVirtualThreadId(Processor.CPU_Core.SmtWays - 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => microOp.RequireExecutionVirtualThreadId(-1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => microOp.RequireExecutionVirtualThreadId(Processor.CPU_Core.SmtWays));
    }

    [Fact]
    public void PipelineStateVirtualThreadId_InvalidValue_ThrowsInsteadOfFallingBackToActiveVt()
    {
        MethodInfo method = typeof(Processor.CPU_Core).GetMethod(
            "NormalizePipelineStateVtId",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Pipeline VT ingress guard is missing.");
        var core = new Processor.CPU_Core(0);

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => method.Invoke(core, new object[] { -1 }));

        Assert.IsType<ArgumentOutOfRangeException>(exception.InnerException);
    }

    [Fact]
    public void ResourceMaskBuilder_InvalidResourceIds_ThrowInsteadOfAliasingResourceZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceMaskBuilder.ForMemoryBank(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceMaskBuilder.ForMemoryBank(16));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceMaskBuilder.ForRegisterRead(1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ResourceMaskBuilder.ForRegisterWrite(1, Processor.CPU_Core.SmtWays));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceMaskBuilder.ForMemoryDomain(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceMaskBuilder.ForDMAChannel(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceMaskBuilder.ForStreamEngine(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceMaskBuilder.ForAccelerator(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceMaskBuilder.ForExtendedGRLBChannel(32));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceMaskBuilder.ForExtendedMemoryDomain(-1));
    }

    private sealed class VirtualThreadGuardProbeMicroOp : MicroOp
    {
        public int RequireExecutionVirtualThreadId(int ownerThreadId) =>
            NormalizeExecutionVtId(ownerThreadId);

        public override bool Execute(ref Processor.CPU_Core core) => true;

        public override string GetDescription() => "RF-01 virtual-thread ingress guard probe";
    }
}
