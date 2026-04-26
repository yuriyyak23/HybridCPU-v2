using System;
using System.Linq;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03LegacyMoveDt5HelperRejectTests
{
    [Fact]
    public void TripleDestinationArchRegMove_HelperSurfaceIsRemoved()
    {
        Assert.False(HasMoveOverload(
            typeof(ArchRegId),
            typeof(ArchRegId),
            typeof(ArchRegId),
            typeof(ulong),
            typeof(ulong),
            typeof(ulong)));
    }

    [Fact]
    public void TripleDestinationIntRegisterMove_HelperSurfaceIsRemoved()
    {
        Assert.False(HasMoveOverload(
            typeof(Processor.CPU_Core.IntRegister),
            typeof(Processor.CPU_Core.IntRegister),
            typeof(Processor.CPU_Core.IntRegister),
            typeof(ulong),
            typeof(ulong),
            typeof(ulong)));
    }

    private static bool HasMoveOverload(params Type[] parameterTypes)
    {
        return typeof(Processor.CPU_Core)
            .GetMethods()
            .Any(method =>
                string.Equals(method.Name, "Move", StringComparison.Ordinal) &&
                method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(parameterTypes));
    }
}
