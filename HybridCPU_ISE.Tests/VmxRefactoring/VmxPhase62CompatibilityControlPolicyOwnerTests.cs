using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase62CompatibilityControlPolicyOwnerTests
{
    private const CompatibilityEventRoutingPolicy FullPolicy =
        CompatibilityEventRoutingPolicy.RuntimeTrapPolicyRequired |
        CompatibilityEventRoutingPolicy.NeutralTrapResultRequired |
        CompatibilityEventRoutingPolicy.PublicationFenceRequired;

    [Fact]
    public void ExplicitProductionConstruction_IssuesExactIdentityAndNonZeroFreshness()
    {
        Processor.CPU_Core core = CreateCore(41, FullPolicy);
        CompatibilityControlPolicyOwner owner = Assert.IsType<CompatibilityControlPolicyOwner>(
            core.CompatibilityControlPolicyOwner);
        CompatibilityControlPolicySnapshot snapshot = Assert.IsType<CompatibilityControlPolicySnapshot>(
            core.CaptureCompatibilityControlPolicy());

        Assert.True(owner.IsCurrent(snapshot));
        Assert.True(snapshot.IsMaterialized);
        Assert.Equal(CompatibilityControlPolicyOwner.CanonicalOwnerIdentity,
            snapshot.Identity.OwnerIdentity);
        Assert.Equal(1UL, snapshot.Identity.OwnerEpoch);
        Assert.Equal(41UL, snapshot.Identity.DomainIdentity);
        Assert.Equal(1UL, snapshot.Identity.PolicyGeneration);
        Assert.Equal(FullPolicy, snapshot.EventRoutingPolicy);
        Assert.False(snapshot.RuntimeAuthorityGranted);
        Assert.False(snapshot.IsCompatibilityValue);
    }

    [Fact]
    public void DefaultConstruction_DoesNotInventPolicyOrChangeInterruptBehavior()
    {
        var core = new Processor.CPU_Core(0, CpuCorePlatformContext.CreateFixed(
            new Processor.MainMemoryArea(), ProcessorMode.Emulation));
        byte calls = 0;
        core.TestSetInterruptDispatcher((_, _, _) => { calls++; return 7; });

        Assert.Null(core.CompatibilityControlPolicyOwner);
        Assert.Null(core.CaptureCompatibilityControlPolicy());
        Assert.Equal((byte)7, core.DispatchInterrupt(default, 1));
        Assert.Equal((byte)1, calls);
    }

    [Fact]
    public void ProductionInterruptDispatch_ConsumesCurrentNeutralPolicy()
    {
        Processor.CPU_Core core = CreateCore(42, FullPolicy);
        byte calls = 0;
        core.TestSetInterruptDispatcher((_, _, _) => { calls++; return 9; });

        Assert.Equal((byte)9, core.DispatchInterrupt(default, 2));
        Assert.Equal((byte)1, calls);

        core.ReplaceCompatibilityControlPolicy(
            CompatibilityEventRoutingPolicy.RuntimeTrapPolicyRequired);
        Assert.Throws<InvalidOperationException>(() => core.DispatchInterrupt(default, 3));
        Assert.Equal((byte)1, calls);
    }

    [Fact]
    public void ReplaceRebindRestoreAndOwnerReplacement_InvalidateOldSnapshots()
    {
        Processor.CPU_Core core = CreateCore(43, FullPolicy);
        CompatibilityControlPolicyOwner firstOwner = core.CompatibilityControlPolicyOwner!;
        CompatibilityControlPolicySnapshot first = firstOwner.CaptureCurrent();

        CompatibilityControlPolicySnapshot replaced =
            core.ReplaceCompatibilityControlPolicy(FullPolicy);
        Assert.False(firstOwner.IsCurrent(first));
        Assert.True(firstOwner.IsCurrent(replaced));
        Assert.True(replaced.Identity.OwnerEpoch > first.Identity.OwnerEpoch);
        Assert.True(replaced.Identity.PolicyGeneration > first.Identity.PolicyGeneration);

        CompatibilityControlPolicySnapshot rebound =
            core.RebindCompatibilityControlPolicy(44);
        Assert.False(firstOwner.IsCurrent(replaced));
        Assert.Equal(44UL, rebound.Identity.DomainIdentity);

        Processor.CPU_Core.VectorContext saved = core.SaveVectorContext();
        core.RestoreVectorContext(saved);
        CompatibilityControlPolicySnapshot restored = firstOwner.CaptureCurrent();
        Assert.False(firstOwner.IsCurrent(rebound));
        Assert.Equal(rebound.Identity.OwnerEpoch, restored.Identity.OwnerEpoch);
        Assert.True(restored.Identity.PolicyGeneration > rebound.Identity.PolicyGeneration);

        core.ResetExecutionStartPcState(0);
        CompatibilityControlPolicySnapshot ownerReplaced = firstOwner.CaptureCurrent();
        Assert.False(firstOwner.IsCurrent(restored));
        Assert.True(ownerReplaced.Identity.OwnerEpoch > restored.Identity.OwnerEpoch);
    }

    [Fact]
    public async Task CaptureVersusReplaceRebindAndRestore_NeverAuthenticatesStaleSnapshot()
    {
        Processor.CPU_Core core = CreateCore(45, FullPolicy);
        CompatibilityControlPolicyOwner owner = core.CompatibilityControlPolicyOwner!;
        for (int iteration = 0; iteration < 100; iteration++)
        {
            CompatibilityControlPolicySnapshot before = owner.CaptureCurrent();
            Task mutate = Task.Run(() =>
            {
                switch (iteration % 3)
                {
                    case 0: core.ReplaceCompatibilityControlPolicy(FullPolicy); break;
                    case 1: core.RebindCompatibilityControlPolicy((ulong)(100 + iteration)); break;
                    default: core.RestoreVectorContext(core.SaveVectorContext()); break;
                }
            });
            await mutate;
            Assert.False(owner.IsCurrent(before));
            Assert.True(owner.IsCurrent(owner.CaptureCurrent()));
        }
    }

    private static Processor.CPU_Core CreateCore(
        ulong domainIdentity,
        CompatibilityEventRoutingPolicy policy) =>
        new(
            0,
            CpuCorePlatformContext.CreateFixed(
                new Processor.MainMemoryArea(),
                ProcessorMode.Emulation,
                compatibilityControlPolicyProfile:
                    new CompatibilityControlPolicyConstructionProfile(
                        domainIdentity,
                        policy)));
}
