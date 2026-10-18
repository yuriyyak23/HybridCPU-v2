using System.Collections;
using System.Reflection;
using HybridCPU.ExternalRuntime.Contracts;
namespace HybridCPU.ExternalRuntime.Tests;
public sealed class OriginalGuestMapTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static ExternalOperationIdentity Op(ulong n) => new(new(Guid.NewGuid()), new(n));
    [Theory]
    [InlineData("duplicate")]
    [InlineData("copy")]
    [InlineData("missing")]
    public void MalformedOriginalMapHistoryCannotAdmitAnotherEffectButExactUnmapPreservesForeignMapping(string fault)
    {
        var runtime = new HybridCpuExternalRuntime();
        var parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(31))).Receipt!;
        var child = runtime.CreateChildDomain(parent.Lease, new(Guid.NewGuid(), ExternalChildAuthority.GuestMemory, 16384, Op(32))).Receipt!;
        var request = new ExternalOriginalGuestMapRequest(child, new(0, 4096, Op(33)));
        var mapped = runtime.MapOriginalGuest(request)!;
        var foreign = runtime.MapChildGuestMemory(child.Lease, new(8192, 4096, Op(34))).Receipt!;
        var children = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("childRegistry", Private)!.GetValue(runtime)!;
        var owner = children[child.Lease.Handle]!;
        var rows = (IList)owner.GetType().GetProperty("OriginalGuestMaps")!.GetValue(owner)!;
        var original = rows[0]!;
        if (fault == "duplicate") rows.Add(original);
        if (fault == "copy") rows[0] = typeof(object).GetMethod("MemberwiseClone", Private)!.Invoke(original, null)!;
        if (fault == "missing") rows.Clear();
        Assert.Null(runtime.ResolveOriginalGuestMap(request));
        Assert.Null(runtime.MapOriginalGuest(new(child, new(4096, 4096, Op(35)))));
        Assert.Null(runtime.MapChildGuestMemory(child.Lease, new(4096, 4096, Op(36))).Receipt);
        var mappings = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("guestMappingRegistry", Private)!.GetValue(runtime)!;
        Assert.Equal(2, mappings.Count);
        Assert.True(runtime.UnmapOriginalGuest(new(mapped.Raw.Receipt!, Op(37)))!.NoFurtherUse);
        var foreignOwner = mappings[foreign.Mapping.Handle]!;
        Assert.False((bool)foreignOwner.GetType().GetProperty("Closed")!.GetValue(foreignOwner)!);
        Assert.True(runtime.CloseOriginalChild(new(child, Op(38)))!.NoEffect);
        Assert.True(runtime.UnmapOriginalGuest(new(foreign, Op(39)))!.NoFurtherUse);
        Assert.True(runtime.CloseOriginalChild(new(child, Op(40)))!.NoFurtherUse);
        Assert.True(runtime.CloseOriginalDomain(new(parent, Op(41)))!.NoFurtherUse);
    }
    [Theory]
    [InlineData("childRegistry")] [InlineData("registry")]
    public void OriginalMapRequiresOriginalAdmissionAndCanonicalProducingSources(string name)
    {
        var runtime = new HybridCpuExternalRuntime();
        var parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(1))).Receipt!;
        var child = runtime.CreateChildDomain(parent.Lease, new(Guid.NewGuid(), ExternalChildAuthority.GuestMemory, 4096, Op(2))).Receipt!;
        var request = new ExternalOriginalGuestMapRequest(child, new(0, 4096, Op(3)));
        Assert.Null(runtime.MapOriginalGuest(request with { Admission = child with { } }));
        var routes = (IDictionary)typeof(HybridCpuExternalRuntime).GetField(name, Private)!.GetValue(runtime)!;
        var key = Assert.Single(routes.Keys.Cast<object>()); var original = routes[key]!;
        routes[key] = typeof(object).GetMethod("MemberwiseClone", Private)!.Invoke(original, null)!;
        Assert.Null(runtime.MapOriginalGuest(request));
        routes[key] = original;
        var mapped = runtime.MapOriginalGuest(request)!;
        Assert.False(mapped.NoEffect); Assert.NotNull(mapped.Raw.Receipt);
        Assert.Same(mapped, runtime.ResolveOriginalGuestMap(request));
        Assert.Null(runtime.ResolveOriginalGuestMap(request with { }));
        Assert.Null(runtime.MapOriginalGuest(request));
        routes[key] = typeof(object).GetMethod("MemberwiseClone", Private)!.Invoke(original, null)!;
        Assert.Null(runtime.ResolveOriginalGuestMap(request));
        routes[key] = original;
        Assert.Same(mapped, runtime.ResolveOriginalGuestMap(request));
        Assert.True(runtime.UnmapOriginalGuest(new(mapped.Raw.Receipt!, Op(4)))!.NoFurtherUse);
        Assert.True(runtime.CloseOriginalChild(new(child, Op(5)))!.NoFurtherUse);
        Assert.True(runtime.CloseOriginalDomain(new(parent, Op(6)))!.NoFurtherUse);
    }
    [Fact]
    public void ClosedProjectionCannotEraseOverlapAndOriginalHistoryAbsenceCannotProveNoEffect()
    {
        var runtime = new HybridCpuExternalRuntime();
        var parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(10))).Receipt!;
        var child = runtime.CreateChildDomain(parent.Lease, new(Guid.NewGuid(), ExternalChildAuthority.GuestMemory, 4096, Op(11))).Receipt!;
        var first = new ExternalOriginalGuestMapRequest(child, new(0, 4096, Op(12)));
        var mapped = runtime.MapOriginalGuest(first)!;
        var mappings = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("guestMappingRegistry", Private)!.GetValue(runtime)!;
        var original = mappings[mapped.Raw.Receipt!.Mapping.Handle]!;
        original.GetType().GetProperty("Closed")!.SetValue(original, true);
        var blocked = runtime.MapOriginalGuest(new(child, new(0, 4096, Op(13))))!;
        Assert.True(blocked.NoEffect); Assert.Null(blocked.Raw.Receipt);
        Assert.Equal(ExternalRuntimeOutcome.Denied, runtime.MapChildGuestMemory(child.Lease, new(0, 4096, Op(14))).Outcome);
        var children = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("childRegistry", Private)!.GetValue(runtime)!;
        var source = children[child.Lease.Handle]!;
        var facts = (IList)source.GetType().GetProperty("OriginalGuestMaps")!.GetValue(source)!;
        facts.Clear();
        Assert.Null(runtime.ResolveOriginalGuestMap(first));
        Assert.True(runtime.CloseOriginalChild(new(child, Op(15)))!.NoEffect);
    }
}
