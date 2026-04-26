using System;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03DirectFactoryVectorNonRepresentableAddressingRejectTests
{
    [Theory]
    [MemberData(
        nameof(VectorNonRepresentableAddressingTestHelper.RepresentativeContours),
        MemberType = typeof(VectorNonRepresentableAddressingTestHelper))]
    public void DirectFactory_WhenVectorFamilyUsesNonRepresentableIndexedOr2DContour_ThenFailsClosedBeforeManualPublication(
        VectorNonRepresentableFamily family,
        InstructionsEnum opcode,
        bool is2D,
        string addressingContour)
    {
        var instruction = VectorNonRepresentableAddressingTestHelper.CreateInstruction(family, opcode, is2D);
        DecoderContext context =
            VectorNonRepresentableAddressingTestHelper.CreateDecoderContext(in instruction);

        InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)opcode, context));

        Assert.Contains(addressingContour, exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            VectorNonRepresentableAddressingTestHelper.GetFactoryAddressingLabel(family),
            exception.Message,
            StringComparison.Ordinal);
        Assert.Contains("fail closed", exception.Message, StringComparison.Ordinal);
        Assert.Contains("non-representable compat surface", exception.Message, StringComparison.Ordinal);
    }
}
