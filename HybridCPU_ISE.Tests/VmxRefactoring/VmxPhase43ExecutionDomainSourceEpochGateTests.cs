using System.Collections.Concurrent;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase43ExecutionDomainSourceEpochGateTests
{
    private const ulong Domain = 0x43;
    private const ulong AddressSpace = 0x4300;

    [Fact]
    public void Bind_IssuesNonZeroRuntimeEpochAndDoesNotTrustCallerEpoch()
    {
        var runtime = new ExecutionDomainRuntime();
        ExecutionDomainDescriptor supplied = Descriptor(1, 2, 3, callerEpoch: 0xdead);

        ExecutionDomainSourceBindResult result =
            runtime.BindAuthoritativeReadOnlyState(supplied, AddressSpace);

        Assert.True(result.IsBound);
        Assert.NotNull(result.Descriptor);
        Assert.NotSame(supplied, result.Descriptor);
        Assert.NotEqual(0UL, result.Epoch.Value);
        Assert.NotEqual(0xdeadUL, result.Epoch.Value);
        Assert.Equal(result.Epoch, runtime.CurrentSourceEpoch);
        Assert.Equal(result.Epoch.Value, result.Descriptor!.ReadOnlyState.StateEpoch);
        Assert.Equal(0xdeadUL, supplied.ReadOnlyState.StateEpoch);
        Assert.Empty(typeof(ExecutionDomainStateEpoch).GetConstructors());
        Assert.Null(typeof(ExecutionDomainStateEpoch).GetProperty(nameof(ExecutionDomainStateEpoch.Value))!.SetMethod);
    }

    [Fact]
    public void Bind_DeniesMissingIdentityDisabledProjectionAndIncompleteSource()
    {
        var runtime = new ExecutionDomainRuntime();
        Assert.Equal(ExecutionDomainSourceBindDecision.MissingDescriptor,
            runtime.BindAuthoritativeReadOnlyState(null, AddressSpace).Decision);
        Assert.Equal(ExecutionDomainSourceBindDecision.MissingDomainIdentity,
            runtime.BindAuthoritativeReadOnlyState(Descriptor(1, 2, 3, domain: 0), AddressSpace).Decision);
        Assert.Equal(ExecutionDomainSourceBindDecision.MissingAddressSpaceIdentity,
            runtime.BindAuthoritativeReadOnlyState(Descriptor(1, 2, 3), 0).Decision);
        Assert.Equal(ExecutionDomainSourceBindDecision.CompatibilityProjectionDenied,
            runtime.BindAuthoritativeReadOnlyState(Descriptor(1, 2, 3, enabled: false), AddressSpace).Decision);

        var incomplete = new ExecutionDomainDescriptor(
            Domain, null, null, null, true,
            new ExecutionDomainReadOnlyStateView(1, 2, 3, true, false, true));
        Assert.Equal(ExecutionDomainSourceBindDecision.IncompleteSourceState,
            runtime.BindAuthoritativeReadOnlyState(incomplete, AddressSpace).Decision);
        Assert.Null(runtime.CurrentSourceDescriptor);
        Assert.False(runtime.CurrentSourceEpoch.IsMaterialized);
    }

    [Fact]
    public void Capture_AtomicallyReturnsExactOwnerEpochAndFieldValuesOnly()
    {
        var runtime = new ExecutionDomainRuntime();
        ExecutionDomainSourceBindResult bound =
            runtime.BindAuthoritativeReadOnlyState(Descriptor(0x11, 0x22, 0x33), AddressSpace);

        AssertCapture(runtime, bound, VmcsField.GuestPc, 0x11);
        AssertCapture(runtime, bound, VmcsField.GuestSp, 0x22);
        AssertCapture(runtime, bound, VmcsField.GuestFlags, 0x33);

        foreach (VmcsField denied in new[]
                 {
                     VmcsField.GuestCr0, VmcsField.GuestCr3, VmcsField.GuestCr4,
                     VmcsField.HostCr0, VmcsField.HostCr3,
                 })
        {
            ExecutionDomainSourceCaptureResult result = runtime.CaptureVmReadScalarSource(
                bound.Descriptor, Domain, AddressSpace, denied);
            Assert.Equal(ExecutionDomainSourceCaptureDecision.FieldDenied, result.Decision);
            Assert.Null(result.Capture);
        }
    }

    [Fact]
    public void Capture_DeniesForgedCrossDomainCrossAddressAndCrossRuntimeSources()
    {
        var runtime = new ExecutionDomainRuntime();
        ExecutionDomainSourceBindResult bound =
            runtime.BindAuthoritativeReadOnlyState(Descriptor(1, 2, 3), AddressSpace);
        Assert.Equal(ExecutionDomainSourceCaptureDecision.StaleOrForeignDescriptor,
            runtime.CaptureVmReadScalarSource(Descriptor(1, 2, 3), Domain, AddressSpace, VmcsField.GuestPc).Decision);
        Assert.Equal(ExecutionDomainSourceCaptureDecision.DomainMismatch,
            runtime.CaptureVmReadScalarSource(bound.Descriptor, Domain + 1, AddressSpace, VmcsField.GuestPc).Decision);
        Assert.Equal(ExecutionDomainSourceCaptureDecision.AddressSpaceMismatch,
            runtime.CaptureVmReadScalarSource(bound.Descriptor, Domain, AddressSpace + 1, VmcsField.GuestPc).Decision);

        var otherRuntime = new ExecutionDomainRuntime();
        ExecutionDomainSourceBindResult other =
            otherRuntime.BindAuthoritativeReadOnlyState(Descriptor(4, 5, 6), AddressSpace);
        Assert.Equal(ExecutionDomainSourceCaptureDecision.StaleOrForeignDescriptor,
            runtime.CaptureVmReadScalarSource(other.Descriptor, Domain, AddressSpace, VmcsField.GuestPc).Decision);
        ExecutionDomainRuntime.SourceCapture capture = Assert.IsType<ExecutionDomainRuntime.SourceCapture>(
            runtime.CaptureVmReadScalarSource(bound.Descriptor, Domain, AddressSpace, VmcsField.GuestPc).Capture);
        Assert.True(runtime.IsAuthenticCapture(capture));
        Assert.False(otherRuntime.IsAuthenticCapture(capture));
        Assert.False(capture.RuntimeAuthorityGranted);
        Assert.False(capture.IsCapability);
        Assert.False(capture.IsReceipt);
    }

    [Fact]
    public void ReplaceRestoreRebindAndUnbind_InvalidateStaleDescriptorAndAdvanceEpoch()
    {
        var runtime = new ExecutionDomainRuntime();
        ExecutionDomainSourceBindResult first =
            runtime.BindAuthoritativeReadOnlyState(Descriptor(1, 2, 3), AddressSpace);
        ExecutionDomainRuntime.SourceCapture captured = Assert.IsType<ExecutionDomainRuntime.SourceCapture>(
            runtime.CaptureVmReadScalarSource(first.Descriptor, Domain, AddressSpace, VmcsField.GuestPc).Capture);

        ExecutionDomainSourceBindResult second =
            runtime.ReplaceAuthoritativeReadOnlyState(Descriptor(10, 20, 30), AddressSpace);
        Assert.NotEqual(first.Epoch, second.Epoch);
        Assert.Equal(ExecutionDomainSourceCaptureDecision.StaleOrForeignDescriptor,
            runtime.CaptureVmReadScalarSource(first.Descriptor, Domain, AddressSpace, VmcsField.GuestPc).Decision);
        AssertCapture(runtime, second, VmcsField.GuestPc, 10);

        // A completed source capture is a value freshness proof, not a continuing source lease.
        Assert.True(runtime.IsAuthenticCapture(captured));
        Assert.Equal(1UL, captured.Value);
        Assert.Equal(first.Epoch, captured.SourceEpoch);

        ExecutionDomainSourceBindResult restored =
            runtime.RebindAuthoritativeReadOnlyStateAfterRestore(Descriptor(100, 200, 300), AddressSpace);
        Assert.NotEqual(second.Epoch, restored.Epoch);
        runtime.UnbindAuthoritativeReadOnlyState();
        Assert.False(runtime.CurrentSourceEpoch.IsMaterialized);
        Assert.Null(runtime.CurrentSourceDescriptor);
        Assert.Equal(ExecutionDomainSourceCaptureDecision.SourceUnbound,
            runtime.CaptureVmReadScalarSource(restored.Descriptor, Domain, AddressSpace, VmcsField.GuestPc).Decision);

        ExecutionDomainSourceBindResult rebound =
            runtime.BindAuthoritativeReadOnlyState(Descriptor(7, 8, 9), AddressSpace);
        Assert.True(rebound.Epoch.Value > restored.Epoch.Value);
    }

    [Fact]
    public async Task CaptureVersusReplacement_NeverProducesMixedOwnerEpochOrValue()
    {
        var runtime = new ExecutionDomainRuntime();
        ExecutionDomainSourceBindResult initial =
            runtime.BindAuthoritativeReadOnlyState(Descriptor(1, 2, 3), AddressSpace);
        var failures = new ConcurrentQueue<string>();

        Task replacer = Task.Run(() =>
        {
            for (int index = 1; index <= 500; index++)
            {
                ulong basis = (ulong)index * 100;
                runtime.ReplaceAuthoritativeReadOnlyState(
                    Descriptor(basis + 1, basis + 2, basis + 3), AddressSpace);
            }
        });
        Task reader = Task.Run(() =>
        {
            for (int index = 0; index < 2000; index++)
            {
                ExecutionDomainDescriptor? presented = runtime.CurrentSourceDescriptor;
                ExecutionDomainSourceCaptureResult result = runtime.CaptureVmReadScalarSource(
                    presented, Domain, AddressSpace, VmcsField.GuestFlags);
                if (!result.IsCaptured)
                    continue;
                ExecutionDomainRuntime.SourceCapture capture = result.Capture!;
                if (!ReferenceEquals(capture.SourceOwner, presented) ||
                    capture.SourceOwner.ReadOnlyState.StateEpoch != capture.SourceEpoch.Value ||
                    capture.SourceOwner.ReadOnlyState.GuestFlags != capture.Value ||
                    capture.DomainTag != Domain || capture.AddressSpaceTag != AddressSpace)
                {
                    failures.Enqueue("mixed source snapshot");
                }
            }
        });

        await Task.WhenAll(replacer, reader);
        Assert.Empty(failures);
        Assert.True(initial.IsBound);
    }

    private static void AssertCapture(
        ExecutionDomainRuntime runtime,
        ExecutionDomainSourceBindResult bound,
        VmcsField field,
        ulong expectedValue)
    {
        ExecutionDomainSourceCaptureResult result = runtime.CaptureVmReadScalarSource(
            bound.Descriptor, Domain, AddressSpace, field);
        Assert.True(result.IsCaptured, result.Reason);
        ExecutionDomainRuntime.SourceCapture capture = result.Capture!;
        Assert.Same(bound.Descriptor, capture.SourceOwner);
        Assert.Equal(bound.Epoch, capture.SourceEpoch);
        Assert.Equal(field, capture.Field);
        Assert.Equal(expectedValue, capture.Value);
        Assert.Equal(Domain, capture.DomainTag);
        Assert.Equal(AddressSpace, capture.AddressSpaceTag);
        Assert.True(runtime.IsAuthenticCapture(capture));
    }

    private static ExecutionDomainDescriptor Descriptor(
        ulong pc,
        ulong sp,
        ulong flags,
        ulong callerEpoch = 0,
        ulong domain = Domain,
        bool enabled = true) =>
        new(domain, null, null, null, enabled,
            ExecutionDomainReadOnlyStateView.FromGuestPcSpFlags(pc, sp, flags, callerEpoch));
}
