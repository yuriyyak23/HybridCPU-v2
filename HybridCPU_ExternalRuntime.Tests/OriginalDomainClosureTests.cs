using System.Collections;
using System.Reflection;
using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime.Tests;

public sealed class OriginalDomainClosureTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static ExternalOperationIdentity Op(ulong n) => new(new(Guid.NewGuid()), new(n));

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalEffectIsNeverRepeatedAfterLateRoutingLossOrUnknownBackend(bool unknown)
    {
        var backend = new CountingOwner { ThrowClose = unknown };
        var diagnostics = new DetachCallback();
        var runtime = new HybridCpuExternalRuntime(() => backend, diagnostics);
        var admission = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(20))).Receipt!;
        var parents = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("registry", Private)!.GetValue(runtime)!;
        var key = Assert.Single(parents.Keys.Cast<object>());
        var original = parents[key]!;
        diagnostics.Callback = () =>
        {
            Assert.False(Monitor.IsEntered(original.GetType().GetProperty("Sync")!.GetValue(original)!));
            parents[key] = typeof(object).GetMethod("MemberwiseClone", Private)!.Invoke(original, null)!;
        };
        var request = new ExternalOriginalDomainCloseRequest(admission, Op(21));
        Assert.Null(runtime.CloseOriginalDomain(request));
        Assert.Equal(1, backend.CloseCalls);
        parents[key] = original;
        var saved = runtime.ResolveOriginalDomainClose(request);
        Assert.Equal(!unknown, saved is { NoFurtherUse: true });
        Assert.Null(runtime.CloseOriginalDomain(request));
        Assert.Null(runtime.CloseOriginalDomain(new(admission, Op(22))));
        Assert.Null(runtime.CloseDomain(admission.Lease, Op(23)).Receipt);
        Assert.Equal(1, backend.CloseCalls);
        Assert.Null(diagnostics.Failure);
    }

    private sealed class CountingOwner : IExternalDomainExecutionOwner
    {
        private readonly AdmissionDomainLifetimeOwner inner = new();
        public bool ThrowClose; public int CloseCalls;
        public BackendBindResult Open(ExternalDomainLease lease, ExternalOperationIdentity operation, HybridCpuExternalContractVersion version, ulong generation) => inner.Open(lease, operation, version, generation);
        public BackendTransitionResult Transition(ExternalDomainLease lease, ExternalDomainTransition transition, ExternalOperationIdentity operation, HybridCpuExternalContractVersion version, ulong generation) => inner.Transition(lease, transition, operation, version, generation);
        public BackendCloseResult Close(ExternalDomainLease lease, ExternalOperationIdentity operation, HybridCpuExternalContractVersion version, ulong generation)
        { CloseCalls++; if (ThrowClose) throw new IOException("original effect outcome lost"); return inner.Close(lease, operation, version, generation); }
    }
    private sealed class DetachCallback : IExternalDomainDiagnostics
    {
        public Action? Callback; public Exception? Failure;
        public void Attach(ExternalDomainLease lease, ulong generation) { }
        public void Detach(ExternalDomainLease lease, ulong generation)
        { try { Callback?.Invoke(); } catch (Exception exception) { Failure = exception; throw; } }
    }

    [Theory]
    [InlineData("missing")] [InlineData("copy")] [InlineData("projection")]
    public void OriginalDependentInventoryCannotBeErasedByRoutingOrClosedProjection(string fault)
    {
        var runtime = new HybridCpuExternalRuntime();
        var admission = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(1))).Receipt!;
        var child = runtime.CreateChildDomain(admission.Lease, new(Guid.NewGuid(), ExternalChildAuthority.Execute, 4096, Op(2))).Receipt!.Lease;
        var children = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("childRegistry", Private)!.GetValue(runtime)!;
        var original = children[child.Handle]!;
        if (fault == "missing") children.Remove(child.Handle);
        if (fault == "copy") children[child.Handle] = typeof(object).GetMethod("MemberwiseClone", Private)!.Invoke(original, null)!;
        var state = original.GetType().GetProperty("State")!;
        if (fault == "projection") state.SetValue(original, ExternalChildDomainState.Closed);
        var request = new ExternalOriginalDomainCloseRequest(admission, Op(3));
        var refused = runtime.CloseOriginalDomain(request);
        Assert.NotNull(refused); Assert.True(refused.NoEffect); Assert.False(refused.NoFurtherUse);
        Assert.Same(refused, runtime.ResolveOriginalDomainClose(request));
        Assert.Null(runtime.ResolveOriginalDomainClose(request with { }));
        Assert.Null(runtime.CloseOriginalDomain(request));
        children[child.Handle] = original;
        if (fault == "projection") state.SetValue(original, ExternalChildDomainState.Ready);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.CloseChildDomain(child, Op(4)).Outcome);
        var fresh = new ExternalOriginalDomainCloseRequest(admission, Op(5));
        var closed = runtime.CloseOriginalDomain(fresh);
        Assert.NotNull(closed); Assert.True(closed.NoFurtherUse); Assert.False(closed.NoEffect);
        Assert.Same(closed, runtime.ResolveOriginalDomainClose(fresh));
        Assert.Null(runtime.CloseOriginalDomain(fresh));
        Assert.Null(runtime.CreateChildDomain(admission.Lease, new(Guid.NewGuid(), ExternalChildAuthority.Execute, 4096, Op(6))).Receipt);
    }

    [Fact]
    public void EqualAdmissionOrOwnerCopyCannotCloseOriginalParent()
    {
        var runtime = new HybridCpuExternalRuntime();
        var admission = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(10))).Receipt!;
        Assert.Null(runtime.CloseOriginalDomain(new(admission with { }, Op(11))));
        var parents = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("registry", Private)!.GetValue(runtime)!;
        var key = Assert.Single(parents.Keys.Cast<object>());
        var original = parents[key]!;
        parents[key] = typeof(object).GetMethod("MemberwiseClone", Private)!.Invoke(original, null)!;
        Assert.Null(runtime.CloseOriginalDomain(new(admission, Op(12))));
        Assert.Equal("Ready", original.GetType().GetProperty("Lifecycle")!.GetValue(original)!.ToString());
        parents[key] = original;
        var request = new ExternalOriginalDomainCloseRequest(admission, Op(13));
        var closed = runtime.CloseOriginalDomain(request)!;
        Assert.True(closed.NoFurtherUse);
        foreach (var property in new[] { "Context", "RootAuthority", "Binding" })
            Assert.Null(original.GetType().GetProperty(property)!.GetValue(original));
        Assert.Same(closed, runtime.ResolveOriginalDomainClose(request));
    }
}
