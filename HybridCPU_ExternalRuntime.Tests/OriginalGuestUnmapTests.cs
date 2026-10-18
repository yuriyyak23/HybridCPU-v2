using System.Collections;
using System.Reflection;
using HybridCPU.ExternalRuntime.Contracts;
namespace HybridCPU.ExternalRuntime.Tests;
public sealed class OriginalGuestUnmapTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static ExternalOperationIdentity Op(ulong n) => new(new(Guid.NewGuid()), new(n));
    [Theory]
    [InlineData("guestMappingRegistry")] [InlineData("childRegistry")] [InlineData("registry")]
    public void OriginalUnmapFencesCanonicalSourcesAndRetainsExactOutcome(string name)
    {
        var runtime = new HybridCpuExternalRuntime();
        var parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(1))).Receipt!;
        var child = runtime.CreateChildDomain(parent.Lease, new(Guid.NewGuid(), ExternalChildAuthority.GuestMemory, 4096, Op(2))).Receipt!;
        var admission = runtime.MapChildGuestMemory(child.Lease, new(0, 4096, Op(3))).Receipt!;
        var request = new ExternalOriginalGuestUnmapRequest(admission, Op(4));
        Assert.Null(runtime.UnmapOriginalGuest(request with { Admission = admission with { } }));
        var routes = (IDictionary)typeof(HybridCpuExternalRuntime).GetField(name, Private)!.GetValue(runtime)!;
        var key = Assert.Single(routes.Keys.Cast<object>()); var original = routes[key]!;
        routes[key] = typeof(object).GetMethod("MemberwiseClone", Private)!.Invoke(original, null)!;
        Assert.Null(runtime.UnmapOriginalGuest(request));
        routes[key] = original;
        var closed = runtime.UnmapOriginalGuest(request)!;
        Assert.True(closed.NoFurtherUse); Assert.False(closed.NoEffect);
        Assert.Same(closed, runtime.ResolveOriginalGuestUnmap(request));
        Assert.Null(runtime.ResolveOriginalGuestUnmap(request with { }));
        Assert.Null(runtime.UnmapOriginalGuest(request));
        routes[key] = typeof(object).GetMethod("MemberwiseClone", Private)!.Invoke(original, null)!;
        Assert.Null(runtime.ResolveOriginalGuestUnmap(request));
        routes[key] = original;
        Assert.Same(closed, runtime.ResolveOriginalGuestUnmap(request));
        Assert.True(runtime.CloseOriginalChild(new(child, Op(5)))!.NoFurtherUse);
        Assert.True(runtime.CloseOriginalDomain(new(parent, Op(6)))!.NoFurtherUse);
    }
    [Fact]
    public void ClosedProjectionAndMissingHistoryCannotEraseOriginalGuestUsers()
    {
        var runtime = new HybridCpuExternalRuntime();
        var parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(10))).Receipt!;
        var child = runtime.CreateChildDomain(parent.Lease, new(Guid.NewGuid(), ExternalChildAuthority.GuestMemory, 4096, Op(11))).Receipt!;
        var admission = runtime.MapChildGuestMemory(child.Lease, new(0, 4096, Op(12))).Receipt!;
        var routes = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("guestMappingRegistry", Private)!.GetValue(runtime)!;
        var mapping = routes[admission.Mapping.Handle]!;
        mapping.GetType().GetProperty("Closed")!.SetValue(mapping, true);
        Assert.True(runtime.CloseOriginalChild(new(child, Op(13)))!.NoEffect);
        var request = new ExternalOriginalGuestUnmapRequest(admission, Op(14));
        Assert.Null(runtime.ResolveOriginalGuestUnmap(request));
        var noEffect = runtime.UnmapOriginalGuest(request)!;
        Assert.True(noEffect.NoEffect); Assert.False(noEffect.NoFurtherUse); Assert.Null(noEffect.Raw.Receipt);
        mapping.GetType().GetProperty("Closed")!.SetValue(mapping, false);
        var actual = new ExternalOriginalGuestUnmapRequest(admission, Op(15));
        Assert.True(runtime.UnmapOriginalGuest(actual)!.NoFurtherUse);
        mapping.GetType().GetProperty("OriginalUnmap")!.SetValue(mapping, null);
        Assert.Null(runtime.ResolveOriginalGuestUnmap(actual));
        mapping.GetType().GetProperty("OriginalClosure")!.SetValue(mapping, null);
        Assert.True(runtime.CloseOriginalChild(new(child, Op(16)))!.NoEffect);
        Assert.Equal(ExternalRuntimeOutcome.Stale, runtime.UnmapChildGuestMemory(admission.Mapping, Op(17)).Outcome);
    }
}
