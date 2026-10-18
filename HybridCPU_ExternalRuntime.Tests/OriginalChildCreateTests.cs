using System.Collections;
using System.Reflection;
using HybridCPU.ExternalRuntime.Contracts;
namespace HybridCPU.ExternalRuntime.Tests;
public sealed class OriginalChildCreateTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static ExternalOperationIdentity Op(ulong n) => new(new(Guid.NewGuid()), new(n));
    [Theory]
    [InlineData("duplicate")]
    [InlineData("copy")]
    [InlineData("missing")]
    public void MalformedParentCreateInventoryRefusesAnotherChildEffect(string fault)
    {
        var runtime = new HybridCpuExternalRuntime();
        var parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(61))).Receipt!;
        var request = new ExternalOriginalChildCreateRequest(parent, new(Guid.NewGuid(), ExternalChildAuthority.GuestMemory, 4096, Op(62)));
        var raw = runtime.CreateOriginalChild(request)!;
        var roots = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("registry", Private)!.GetValue(runtime)!;
        var owner = roots[parent.Lease.Handle]!;
        var rows = (IList)owner.GetType().GetProperty("OriginalChildCreates")!.GetValue(owner)!;
        var fact = rows[0]!;
        if (fault == "duplicate") rows.Add(fact);
        if (fault == "copy") rows[0] = typeof(object).GetMethod("MemberwiseClone", Private)!.Invoke(fact, null)!;
        if (fault == "missing") rows.Clear();
        Assert.Null(runtime.ResolveOriginalChildCreate(request));
        Assert.Null(runtime.CreateOriginalChild(new(parent, new(Guid.NewGuid(), ExternalChildAuthority.GuestMemory, 4096, Op(63)))));
        Assert.Null(runtime.CreateChildDomain(parent.Lease, new(Guid.NewGuid(), ExternalChildAuthority.GuestMemory, 4096, Op(64))).Receipt);
        var children = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("childRegistry", Private)!.GetValue(runtime)!;
        Assert.Single(children.Values.Cast<object>());
        Assert.True(runtime.CloseOriginalChild(new(raw.Raw.Receipt!, Op(65)))!.NoFurtherUse);
    }
    [Theory]
    [InlineData("registry")] [InlineData("childRegistry")]
    public void OriginalCreateRequiresCanonicalParentAdmissionAndReturnsOnlySameOriginalFact(string routeName)
    {
        var runtime = new HybridCpuExternalRuntime();
        var parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(1))).Receipt!;
        var request = new ExternalOriginalChildCreateRequest(parent, new(Guid.NewGuid(), ExternalChildAuthority.GuestMemory, 4096, Op(2)));
        Assert.Null(runtime.CreateOriginalChild(request with { Admission = parent with { } }));
        var result = runtime.CreateOriginalChild(request)!;
        Assert.False(result.NoEffect); var child = result.Raw.Receipt!;
        Assert.Same(result, runtime.ResolveOriginalChildCreate(request));
        Assert.Null(runtime.ResolveOriginalChildCreate(request with { }));
        Assert.Null(runtime.CreateOriginalChild(request));
        var routes = (IDictionary)typeof(HybridCpuExternalRuntime).GetField(routeName, Private)!.GetValue(runtime)!;
        var key = Assert.Single(routes.Keys.Cast<object>()); var original = routes[key]!;
        routes[key] = typeof(object).GetMethod("MemberwiseClone", Private)!.Invoke(original, null)!;
        Assert.Null(runtime.ResolveOriginalChildCreate(request));
        if (routeName == "registry") Assert.Null(runtime.CreateOriginalChild(new(parent, new(Guid.NewGuid(), ExternalChildAuthority.GuestMemory, 4096, Op(3)))));
        routes[key] = original;
        Assert.Same(result, runtime.ResolveOriginalChildCreate(request));
        Assert.True(runtime.CloseOriginalChild(new(child, Op(4)))!.NoFurtherUse);
        Assert.True(runtime.CloseOriginalDomain(new(parent, Op(5)))!.NoFurtherUse);
    }
    [Fact]
    public void OriginalCreateNoEffectAndUnknownIntentRemainDistinctFromChildAndParentClosure()
    {
        var runtime = new HybridCpuExternalRuntime();
        var parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(10))).Receipt!;
        var refusal = new ExternalOriginalChildCreateRequest(parent, new(Guid.NewGuid(), ExternalChildAuthority.GuestMemory, 0, Op(11)));
        var noEffect = runtime.CreateOriginalChild(refusal)!;
        Assert.True(noEffect.NoEffect); Assert.Null(noEffect.Raw.Receipt); Assert.Same(noEffect, runtime.ResolveOriginalChildCreate(refusal));
        var request = new ExternalOriginalChildCreateRequest(parent, new(Guid.NewGuid(), ExternalChildAuthority.GuestMemory, 4096, Op(12)));
        Assert.NotNull(runtime.CreateOriginalChild(request)!.Raw.Receipt);
        var roots = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("registry", Private)!.GetValue(runtime)!;
        var owner = Assert.Single(roots.Values.Cast<object>());
        var facts = (IList)owner.GetType().GetProperty("OriginalChildCreates")!.GetValue(owner)!;
        var fact = facts.Cast<object>().Single(f => ReferenceEquals(f.GetType().GetProperty("Request")!.GetValue(f), request));
        fact.GetType().GetProperty("Observation")!.SetValue(fact, null);
        ((IDictionary)owner.GetType().GetProperty("Children")!.GetValue(owner)!).Clear();
        Assert.Null(runtime.ResolveOriginalChildCreate(request));
        Assert.Equal(ExternalRuntimeOutcome.Denied, runtime.CreateChildDomain(parent.Lease, new(Guid.NewGuid(), ExternalChildAuthority.GuestMemory, 4096, Op(13))).Outcome);
        Assert.True(runtime.CloseOriginalDomain(new(parent, Op(14)))!.NoEffect);
    }
}
