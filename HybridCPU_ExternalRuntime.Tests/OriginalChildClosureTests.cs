using System.Collections;
using System.Reflection;
using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime.Tests;

public sealed class OriginalChildClosureTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static ExternalOperationIdentity Op(ulong n) => new(new(Guid.NewGuid()), new(n));

    [Theory]
    [InlineData("childRegistry")] [InlineData("registry")]
    public void OriginalChildCloseRejectsCopiedSourcesAndRetainsOnlyCanonicalOutcome(string registryName)
    {
        var runtime = new HybridCpuExternalRuntime();
        var parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(1))).Receipt!;
        var admission = runtime.CreateChildDomain(parent.Lease, new(Guid.NewGuid(), ExternalChildAuthority.Execute, 4096, Op(2))).Receipt!;
        var request = new ExternalOriginalChildCloseRequest(admission, Op(3));
        Assert.Null(runtime.CloseOriginalChild(request with { Admission = admission with { } }));
        var routes = (IDictionary)typeof(HybridCpuExternalRuntime).GetField(registryName, Private)!.GetValue(runtime)!;
        var key = Assert.Single(routes.Keys.Cast<object>());
        var original = routes[key]!;
        routes[key] = typeof(object).GetMethod("MemberwiseClone", Private)!.Invoke(original, null)!;
        Assert.Null(runtime.CloseOriginalChild(request));
        routes[key] = original;
        var closed = runtime.CloseOriginalChild(request)!;
        Assert.True(closed.NoFurtherUse); Assert.False(closed.NoEffect);
        Assert.Same(closed, runtime.ResolveOriginalChildClose(request));
        Assert.Null(runtime.ResolveOriginalChildClose(request with { }));
        Assert.Null(runtime.CloseOriginalChild(request));
        Assert.Null(runtime.CloseOriginalChild(new(admission, Op(4))));
        Assert.Equal(ExternalRuntimeOutcome.Stale, runtime.CloseChildDomain(admission.Lease, Op(5)).Outcome);
        routes[key] = typeof(object).GetMethod("MemberwiseClone", Private)!.Invoke(original, null)!;
        Assert.Null(runtime.ResolveOriginalChildClose(request));
        routes[key] = original;
        Assert.Same(closed, runtime.ResolveOriginalChildClose(request));
        Assert.True(runtime.CloseOriginalDomain(new(parent, Op(6)))!.NoFurtherUse);
    }

    [Fact]
    public void LiveMappingNoEffectIsSeparateFromOriginalClosureAndHistoryAbsence()
    {
        var runtime = new HybridCpuExternalRuntime();
        var parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(10))).Receipt!;
        var admission = runtime.CreateChildDomain(parent.Lease, new(Guid.NewGuid(), ExternalChildAuthority.GuestMemory, 4096, Op(11))).Receipt!;
        var mapping = runtime.MapChildGuestMemory(admission.Lease, new(0, 4096, Op(12))).Receipt!.Mapping;
        var request = new ExternalOriginalChildCloseRequest(admission, Op(13));
        var refused = runtime.CloseOriginalChild(request)!;
        Assert.True(refused.NoEffect); Assert.False(refused.NoFurtherUse); Assert.Null(refused.Raw.Receipt);
        Assert.Same(refused, runtime.ResolveOriginalChildClose(request));
        Assert.Null(runtime.CloseOriginalChild(request));
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.UnmapChildGuestMemory(mapping, Op(14)).Outcome);
        var fresh = new ExternalOriginalChildCloseRequest(admission, Op(15));
        var closed = runtime.CloseOriginalChild(fresh)!;
        Assert.True(closed.NoFurtherUse);
        var children = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("childRegistry", Private)!.GetValue(runtime)!;
        var child = children[admission.Lease.Handle]!;
        var originalFact = child.GetType().GetProperty("OriginalClose")!.GetValue(child)!;
        child.GetType().GetProperty("OriginalClose")!.SetValue(child, typeof(object).GetMethod("MemberwiseClone", Private)!.Invoke(originalFact, null)!);
        Assert.Null(runtime.ResolveOriginalChildClose(fresh));
        child.GetType().GetProperty("OriginalClose")!.SetValue(child, originalFact);
        Assert.Same(closed, runtime.ResolveOriginalChildClose(fresh));
        child.GetType().GetProperty("OriginalClose")!.SetValue(child, null);
        Assert.Null(runtime.ResolveOriginalChildClose(fresh));
        Assert.Null(runtime.CloseOriginalChild(new(admission, Op(16)))!.Raw.Receipt); // Closed projection cannot produce another lifetime.
    }
}
