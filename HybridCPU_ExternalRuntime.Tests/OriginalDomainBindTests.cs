using System.Collections;
using System.Reflection;
using HybridCPU.ExternalRuntime.Contracts;
namespace HybridCPU.ExternalRuntime.Tests;
public sealed class OriginalDomainBindTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static ExternalOperationIdentity Op(ulong n) => new(new(Guid.NewGuid()), new(n));
    [Fact]
    public void PreparedOriginalSourceMustBeFencedAtTheActualBindProducer()
    {
        var original = new HybridCpuExternalRuntime(); var foreign = new HybridCpuExternalRuntime();
        var live = foreign.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(1))).Receipt!;
        var request = new ExternalDomainBindRequest(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(2));
        var prepared = original.PrepareOriginalDomainBind(request)!;
        Assert.Null(foreign.BindOriginalDomain(prepared)); Assert.Null(foreign.ResolveOriginalDomainBind(prepared));
        var foreignRoots = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("registry", Private)!.GetValue(foreign)!;
        Assert.Single(foreignRoots.Keys.Cast<object>());
        Assert.Null(original.ResolveOriginalDomainBind(prepared)); // No response is not no-effect/closure.
        var copy = (ExternalOriginalDomainBindRequest)typeof(object).GetMethod("MemberwiseClone", Private)!.Invoke(prepared, null)!;
        Assert.Null(original.BindOriginalDomain(copy)); Assert.Null(original.ResolveOriginalDomainBind(copy));
        var accepted = original.BindOriginalDomain(prepared)!;
        Assert.False(accepted.NoEffect); Assert.Equal(ExternalRuntimeOutcome.Bound, accepted.Raw.Outcome);
        Assert.Same(accepted, original.ResolveOriginalDomainBind(prepared)); Assert.Null(original.BindOriginalDomain(prepared));
        Assert.True(original.CloseOriginalDomain(new(accepted.Raw.Receipt!, Op(3)))!.NoFurtherUse);
        Assert.True(foreign.CloseOriginalDomain(new(live, Op(4)))!.NoFurtherUse);
    }
    [Fact]
    public void RootCopiesAndLostPreparedHistoryCannotBecomeOriginalBindOutcome()
    {
        var runtime = new HybridCpuExternalRuntime();
        var prepared = runtime.PrepareOriginalDomainBind(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(10)))!;
        var accepted = runtime.BindOriginalDomain(prepared)!;
        var roots = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("registry", Private)!.GetValue(runtime)!;
        var key = Assert.Single(roots.Keys.Cast<object>()); var owner = roots[key]!;
        roots[key] = typeof(object).GetMethod("MemberwiseClone", Private)!.Invoke(owner, null)!;
        Assert.Null(runtime.ResolveOriginalDomainBind(prepared)); roots[key] = owner;
        Assert.Same(accepted, runtime.ResolveOriginalDomainBind(prepared));
        ((IDictionary)typeof(HybridCpuExternalRuntime).GetField("originalDomainBinds", Private)!.GetValue(runtime)!).Clear();
        Assert.Null(runtime.ResolveOriginalDomainBind(prepared)); Assert.Null(runtime.BindOriginalDomain(prepared));
        Assert.Null(runtime.PrepareOriginalDomainBind(prepared.Request));
        Assert.Null(runtime.RetireOriginalDomainBind(prepared));
        Assert.True(runtime.CloseOriginalDomain(new(accepted.Raw.Receipt!, Op(11)))!.NoFurtherUse);
    }
    [Fact]
    public void ExactProducingNoEffectDoesNotCloseAnIndependentOriginalRoot()
    {
        var runtime = new HybridCpuExternalRuntime(); var operation = Op(20);
        var live = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, operation)).Receipt!;
        var prepared = runtime.PrepareOriginalDomainBind(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, operation))!;
        var noEffect = runtime.BindOriginalDomain(prepared)!;
        Assert.True(noEffect.NoEffect); Assert.Null(noEffect.Raw.Receipt); Assert.Same(noEffect, runtime.ResolveOriginalDomainBind(prepared));
        var roots = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("registry", Private)!.GetValue(runtime)!;
        Assert.Single(roots.Keys.Cast<object>());
        Assert.True(runtime.CloseOriginalDomain(new(live, Op(21)))!.NoFurtherUse);
    }

    [Fact]
    public void RetirementSealsTheOriginalPreparedProducerAndLegacyOperationWithoutCreatingAParent()
    {
        var factories = 0;
        var runtime = new HybridCpuExternalRuntime(() => { factories++; return new AdmissionDomainLifetimeOwner(); });
        var prepared = runtime.PrepareOriginalDomainBind(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(30)))!;
        var copy = (ExternalOriginalDomainBindRequest)typeof(object).GetMethod("MemberwiseClone", Private)!.Invoke(prepared, null)!;
        Assert.Null(runtime.RetireOriginalDomainBind(copy));
        Assert.Null(new HybridCpuExternalRuntime().RetireOriginalDomainBind(prepared));
        var retired = runtime.RetireOriginalDomainBind(prepared)!;
        Assert.True(retired.NoEffect); Assert.Null(retired.Raw.Receipt);
        Assert.Same(retired, runtime.RetireOriginalDomainBind(prepared));
        Assert.Same(retired, runtime.ResolveOriginalDomainBind(prepared));
        Assert.Null(runtime.BindOriginalDomain(prepared));
        Assert.Null(runtime.BindDomain(prepared.Request).Receipt);
        Assert.Null(runtime.BindDomain(prepared.Request with { }).Receipt);
        Assert.Null(runtime.BindDomain(prepared.Request with { RequestId = Guid.NewGuid() }).Receipt);
        Assert.Equal(0, factories);
        Assert.Empty(((IDictionary)typeof(HybridCpuExternalRuntime).GetField("registry", Private)!.GetValue(runtime)!).Keys.Cast<object>());
    }

    [Fact]
    public async Task RetirementCannotTurnAnInFlightActualFactoryIntoNoEffectOrRepeatItsInvocation()
    {
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var factories = 0;
        var runtime = new HybridCpuExternalRuntime(() =>
        { Interlocked.Increment(ref factories); entered.Set(); if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException(); return new AdmissionDomainLifetimeOwner(); });
        var prepared = runtime.PrepareOriginalDomainBind(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(40)))!;
        var binding = Task.Run(() => runtime.BindOriginalDomain(prepared));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.Null(runtime.RetireOriginalDomainBind(prepared)); Assert.Null(runtime.ResolveOriginalDomainBind(prepared));
            Assert.Null(runtime.BindOriginalDomain(prepared)); Assert.Null(runtime.BindDomain(prepared.Request).Receipt);
        }
        finally { release.Set(); }
        var known = (await binding)!;
        Assert.False(known.NoEffect); Assert.NotNull(known.Raw.Receipt);
        Assert.Same(known, runtime.RetireOriginalDomainBind(prepared)); Assert.Equal(1, factories);
        var child = runtime.CreateChildDomain(known.Raw.Receipt!.Lease,
            new(Guid.NewGuid(), ExternalChildAuthority.Execute, 4096, Op(41))).Receipt!;
        var refused = runtime.CloseOriginalDomain(new(known.Raw.Receipt, Op(42)))!;
        Assert.True(refused.NoEffect); Assert.False(refused.NoFurtherUse);
        Assert.True(runtime.CloseOriginalChild(new(child, Op(43)))!.NoFurtherUse);
        Assert.True(runtime.CloseOriginalDomain(new(known.Raw.Receipt, Op(44)))!.NoFurtherUse);
        Assert.Null(runtime.BindOriginalDomain(prepared)); Assert.Equal(1, factories);
    }
}
