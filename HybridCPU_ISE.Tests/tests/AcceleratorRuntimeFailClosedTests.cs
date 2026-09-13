using System;
using Xunit;
using YAKSys_Hybrid_CPU.Execution;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class AcceleratorRuntimeFailClosedTests
{
    [Fact]
    public void AcceleratorRuntimeFailClosed_RejectsUnavailableOperations()
    {
        NotSupportedException registrationEx = Assert.Throws<NotSupportedException>(
            AcceleratorRuntimeFailClosed.ThrowRegistrationNotSupported);
        Assert.Contains("fail closed", registrationEx.Message, StringComparison.OrdinalIgnoreCase);

        NotSupportedException transferEx = Assert.Throws<NotSupportedException>(
            () => AcceleratorRuntimeFailClosed.ThrowTransferNotSupported());
        Assert.Contains("fail closed", transferEx.Message, StringComparison.OrdinalIgnoreCase);
    }

}
