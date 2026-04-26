using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Diagnostics;

namespace HybridCPU_ISE.Tests;

public class LoopPhaseTelemetryTests
{
    [Fact]
    public void WhenLoopPhaseSamplingDisabledThenProfileRemainsBackwardCompatible()
    {
        var scheduler = new MicroOpScheduler
        {
            TypedSlotEnabled = true,
            EnableLoopPhaseSampling = false
        };

        RunLoopIterations(scheduler, loopPcAddress: 0x5200, iterations: 23, startEpochId: 1);

        TypedSlotTelemetryProfile profile = TelemetryExporter.BuildProfile(scheduler, "loop-phase-disabled");

        Assert.Null(profile.LoopPhaseProfiles);
    }

    [Fact]
    public void WhenKernelLoopRepeatsThenLoopPhaseProfileIsExportedWithBoundedSamples()
    {
        var scheduler = new MicroOpScheduler
        {
            TypedSlotEnabled = true,
            EnableLoopPhaseSampling = true
        };

        RunLoopIterations(scheduler, loopPcAddress: 0x6200, iterations: 23, startEpochId: 100);

        TypedSlotTelemetryProfile profile = TelemetryExporter.BuildProfile(scheduler, "loop-phase-kernel");

        Assert.NotNull(profile.LoopPhaseProfiles);
        Assert.Single(profile.LoopPhaseProfiles!);
        Assert.Equal(0x6200UL, profile.LoopPhaseProfiles[0].LoopPcAddress);
        Assert.Equal(16, profile.LoopPhaseProfiles[0].IterationsSampled);
        Assert.True(profile.LoopPhaseProfiles[0].OverallClassVariance > 0.0);
    }

    [Fact]
    public void WhenMoreThanEightHotLoopsObservedThenExporterKeepsTopEightProfiles()
    {
        var scheduler = new MicroOpScheduler
        {
            TypedSlotEnabled = true,
            EnableLoopPhaseSampling = true
        };

        ulong epochId = 500;
        for (int loopIndex = 0; loopIndex < 10; loopIndex++)
        {
            int iterations = 20 - loopIndex;
            RunLoopIterations(scheduler, 0x7000UL + (ulong)loopIndex, iterations, epochId);
            epochId += (ulong)iterations;
        }

        TypedSlotTelemetryProfile profile = TelemetryExporter.BuildProfile(scheduler, "loop-phase-top-k");

        Assert.NotNull(profile.LoopPhaseProfiles);
        Assert.Equal(8, profile.LoopPhaseProfiles!.Count);
        Assert.DoesNotContain(profile.LoopPhaseProfiles, profileEntry => profileEntry.LoopPcAddress == 0x7008UL);
        Assert.DoesNotContain(profile.LoopPhaseProfiles, profileEntry => profileEntry.LoopPcAddress == 0x7009UL);
    }

    private static void RunLoopIterations(MicroOpScheduler scheduler, ulong loopPcAddress, int iterations, ulong startEpochId)
    {
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            int registerOffset = iteration % 8;

            scheduler.SetReplayPhaseContext(new ReplayPhaseContext(
                isActive: true,
                epochId: startEpochId + (ulong)iteration,
                cachedPc: loopPcAddress,
                epochLength: 12,
                completedReplays: 4,
                validSlotCount: 5,
                stableDonorMask: 0xE0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None));

            MicroOp[] bundle = CreateLoopBundle(registerOffset);
            scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateScalarALU(1, (ushort)(48 + registerOffset), (ushort)(56 + registerOffset), (ushort)(64 + registerOffset)));
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);
        }
    }

    private static MicroOp[] CreateLoopBundle(int registerOffset)
    {
        var bundle = new MicroOp[8];
        bundle[0] = MicroOpTestHelper.CreateScalarALU(0, (ushort)(1 + registerOffset), (ushort)(9 + registerOffset), (ushort)(17 + registerOffset));
        bundle[1] = MicroOpTestHelper.CreateScalarALU(0, (ushort)(25 + registerOffset), (ushort)(33 + registerOffset), (ushort)(41 + registerOffset));

        if ((registerOffset & 1) == 0)
        {
            bundle[2] = MicroOpTestHelper.CreateLoad(0, (ushort)(72 + registerOffset), 0x100000UL + (ulong)(registerOffset * 64));
        }
        else
        {
            bundle[2] = MicroOpTestHelper.CreateLoad(0, (ushort)(72 + registerOffset), 0x100000UL + (ulong)(registerOffset * 64));
            bundle[3] = MicroOpTestHelper.CreateLoad(0, (ushort)(88 + registerOffset), 0x200000UL + (ulong)(registerOffset * 64));
        }

        return bundle;
    }
}
