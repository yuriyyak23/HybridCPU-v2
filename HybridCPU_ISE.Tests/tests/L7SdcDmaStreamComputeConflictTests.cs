using System.Collections.Generic;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Conflicts;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcDmaStreamComputeConflictTests
{
    [Fact]
    public void L7SdcDmaStreamComputeConflict_AdmissionOverlapWithAcceleratorWriteRejectsLane6Side()
    {
        var manager = new ExternalAcceleratorConflictManager();
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedTokenForDescriptor(
                destinationRanges: new[] { new AcceleratorMemoryRange(0x9000, 0x40) });
        AcceleratorConflictDecision reservation =
            manager.TryReserveOnSubmit(fixture.Token, fixture.Evidence);
        Assert.True(reservation.IsAccepted, reservation.Message);

        AcceleratorConflictDecision dmaAdmission =
            manager.NotifyDmaStreamComputeAdmission(
                readRanges: new[] { new AcceleratorMemoryRange(0x9100, 0x10) },
                writeRanges: new[] { new AcceleratorMemoryRange(0x9010, 0x10) },
                fixture.Evidence);

        Assert.True(dmaAdmission.IsRejected);
        Assert.Equal(
            AcceleratorConflictClass.DmaStreamComputeOverlapsAcceleratorWrite,
            dmaAdmission.ConflictClass);
        Assert.Equal(AcceleratorTokenFaultCode.ConflictRejected, dmaAdmission.TokenFaultCode);
        Assert.Equal(1, manager.ActiveReservationCount);
        Assert.Equal(AcceleratorTokenState.Created, fixture.Token.State);
    }

    [Fact]
    public void L7SdcDmaStreamComputeConflict_NonOverlappingLane6DescriptorRemainsSeparateContour()
    {
        var manager = new ExternalAcceleratorConflictManager();
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedTokenForDescriptor(
                destinationRanges: new[] { new AcceleratorMemoryRange(0xA000, 0x40) });
        Assert.True(manager.TryReserveOnSubmit(fixture.Token, fixture.Evidence).IsAccepted);
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();

        AcceleratorConflictDecision dmaAdmission =
            manager.NotifyDmaStreamComputeAdmission(
                ConvertRanges(descriptor.NormalizedReadMemoryRanges),
                ConvertRanges(descriptor.NormalizedWriteMemoryRanges),
                fixture.Evidence);

        Assert.True(dmaAdmission.IsAccepted, dmaAdmission.Message);
        Assert.False(dmaAdmission.CanPublishArchitecturalMemory);
        Assert.Equal(1, manager.ActiveReservationCount);
        Assert.Equal(AcceleratorTokenState.Created, fixture.Token.State);
    }

    [Fact]
    public void L7SdcDmaStreamComputeConflict_IncompleteLane6FootprintRejectsConservatively()
    {
        var manager = new ExternalAcceleratorConflictManager();
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        Assert.True(manager.TryReserveOnSubmit(fixture.Token, fixture.Evidence).IsAccepted);

        AcceleratorConflictDecision missingWriteFootprint =
            manager.NotifyDmaStreamComputeAdmission(
                readRanges: new[] { new AcceleratorMemoryRange(0x1000, 0x10) },
                writeRanges: System.Array.Empty<AcceleratorMemoryRange>(),
                fixture.Evidence);
        AcceleratorConflictDecision overflowingRange =
            manager.NotifyDmaStreamComputeAdmission(
                readRanges: new[] { new AcceleratorMemoryRange(ulong.MaxValue, 0x10) },
                writeRanges: new[] { new AcceleratorMemoryRange(0xA000, 0x10) },
                fixture.Evidence);

        Assert.True(missingWriteFootprint.IsRejected);
        Assert.True(overflowingRange.IsRejected);
        Assert.Equal(AcceleratorConflictClass.IncompleteFootprintTruth, missingWriteFootprint.ConflictClass);
        Assert.Equal(AcceleratorConflictClass.IncompleteFootprintTruth, overflowingRange.ConflictClass);
        Assert.Equal(AcceleratorTokenFaultCode.ConflictRejected, missingWriteFootprint.TokenFaultCode);
        Assert.Equal(AcceleratorTokenFaultCode.ConflictRejected, overflowingRange.TokenFaultCode);
    }

    private static IReadOnlyList<AcceleratorMemoryRange> ConvertRanges(
        IReadOnlyList<DmaStreamComputeMemoryRange> ranges)
    {
        var converted = new AcceleratorMemoryRange[ranges.Count];
        for (int index = 0; index < ranges.Count; index++)
        {
            converted[index] = new AcceleratorMemoryRange(
                ranges[index].Address,
                ranges[index].Length);
        }

        return converted;
    }
}
