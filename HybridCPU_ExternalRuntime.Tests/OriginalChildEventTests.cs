using System.Collections;
using System.Reflection;
using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime.Tests;

public sealed class OriginalChildEventTests
{
    private static ExternalOperationIdentity Op(ulong n) => new(new(Guid.NewGuid()), new(n));

    [Theory]
    [InlineData("lost-factory")]
    [InlineData("copied-factory")]
    [InlineData("unknown-ancestor")]
    public void NestedEventAdmissionRevalidatesOriginalAncestorsBeforeMailboxEffect(string mode)
    {
        var runtime = new HybridCpuExternalRuntime();
        var root = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(101))).Receipt!;
        var parent = runtime.CreateOriginalChild(new(root,
            new(Guid.NewGuid(), ExternalChildAuthority.EventInjection, 4096, Op(102)))
            { AllowNestedCreation = true })!.Raw.Receipt!;
        var child = runtime.CreateOriginalNestedChild(new(parent,
            new(Guid.NewGuid(), ExternalChildAuthority.EventInjection, 1024, Op(103))))!.Raw.Receipt!;
        var owners = (IDictionary)typeof(HybridCpuExternalRuntime)
            .GetField("childRegistry", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        var parentOwner = owners[parent.Lease.Handle]!;
        var childOwner = owners[child.Lease.Handle]!;
        var history = (IList)parentOwner.GetType().GetProperty("OriginalNestedChildren")!.GetValue(parentOwner)!;
        if (mode == "lost-factory") history.Clear();
        else if (mode == "copied-factory")
            history[0] = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(history[0], null)!;
        else parentOwner.GetType().GetProperty("State")!.SetValue(parentOwner, (ExternalChildDomainState)255);

        var request = new ExternalOriginalChildEventRequest(child, "nested:admission", 1, Op(104));
        var raw = runtime.InjectOriginalChildEvent(request);
        var events = (IList)childOwner.GetType().GetProperty("OriginalEvents")!.GetValue(childOwner)!;
        if (mode == "unknown-ancestor")
        {
            Assert.NotNull(raw);
            Assert.True(raw.NoEffect);
            Assert.True(raw.NoFurtherDelivery);
            Assert.Null(raw.Receipt);
            var fact = Assert.Single(events.Cast<object>());
            Assert.Null(fact.GetType().GetField("Payload")!.GetValue(fact));
            Assert.Same(raw, runtime.ResolveOriginalChildEvent(request));
            Assert.Null(runtime.InjectOriginalChildEvent(request));
        }
        else
        {
            Assert.Null(raw);
            Assert.Empty(events);
        }
        Assert.Equal(0UL, childOwner.GetType().GetProperty("LastEventSequence")!.GetValue(childOwner));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedEventContainmentClosesOnlyItsOriginalMailboxAfterAncestorChange(bool loseFactory)
    {
        var runtime = new HybridCpuExternalRuntime();
        var root = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(111))).Receipt!;
        var parent = runtime.CreateOriginalChild(new(root,
            new(Guid.NewGuid(), ExternalChildAuthority.EventInjection, 4096, Op(112)))
            { AllowNestedCreation = true })!.Raw.Receipt!;
        var child = runtime.CreateOriginalNestedChild(new(parent,
            new(Guid.NewGuid(), ExternalChildAuthority.EventInjection, 1024, Op(113))))!.Raw.Receipt!;
        var foreign = runtime.CreateChildDomain(root.Lease,
            new(Guid.NewGuid(), ExternalChildAuthority.EventInjection, 1024, Op(114))).Receipt!;
        var foreignRequest = new ExternalOriginalChildEventRequest(foreign, "foreign:mailbox", 1, Op(115));
        var foreignRaw = runtime.InjectOriginalChildEvent(foreignRequest)!;
        var request = new ExternalOriginalChildEventRequest(child, "nested:mailbox", 1, Op(116));
        var accepted = runtime.InjectOriginalChildEvent(request)!;
        Assert.False(accepted.NoEffect);
        Assert.False(accepted.NoFurtherDelivery);
        Assert.Null(runtime.CloseChildDomain(child.Lease, Op(117)).Receipt);

        var owners = (IDictionary)typeof(HybridCpuExternalRuntime)
            .GetField("childRegistry", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        var parentOwner = owners[parent.Lease.Handle]!;
        if (loseFactory)
            ((IList)parentOwner.GetType().GetProperty("OriginalNestedChildren")!.GetValue(parentOwner)!).Clear();
        else parentOwner.GetType().GetProperty("State")!.SetValue(parentOwner, (ExternalChildDomainState)255);

        Assert.Null(runtime.ContainOriginalChildEvent(request with { }));
        var closed = runtime.ContainOriginalChildEvent(request)!;
        Assert.True(closed.NoFurtherDelivery);
        Assert.False(closed.NoEffect);
        Assert.Same(accepted.Receipt, closed.Receipt);
        Assert.Same(closed, runtime.ResolveOriginalChildEvent(request));
        Assert.Same(closed, runtime.ContainOriginalChildEvent(request));
        Assert.Null(runtime.InjectOriginalChildEvent(request));
        Assert.True(runtime.CloseOriginalChild(new(child, Op(118)))!.NoFurtherUse);
        Assert.Same(foreignRaw, runtime.ResolveOriginalChildEvent(foreignRequest));
        Assert.False(foreignRaw.NoFurtherDelivery);
        Assert.Null(runtime.CloseChildDomain(foreign.Lease, Op(119)).Receipt);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateOriginalHistoryRefusesQueriesSettlementAndRepeatWithoutClearingMailbox(bool forgedCount)
    {
        var runtime = new HybridCpuExternalRuntime();
        var parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(71))).Receipt!;
        var child = runtime.CreateChildDomain(parent.Lease, new(Guid.NewGuid(), ExternalChildAuthority.EventInjection, 4096, Op(72))).Receipt!;
        var request = new ExternalOriginalChildEventRequest(child, "duplicate-original", 1, Op(73));
        var raw = runtime.InjectOriginalChildEvent(request)!;
        Assert.False(raw.NoEffect);
        var children = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("childRegistry", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        var owner = children[child.Lease.Handle]!;
        var history = (IList)owner.GetType().GetProperty("OriginalEvents")!.GetValue(owner)!;
        var fact = Assert.Single(history.Cast<object>());
        history.Add(fact);
        if (forgedCount) owner.GetType().GetProperty("OriginalEventsIssued")!.SetValue(owner, 2UL);
        Assert.Null(runtime.ResolveOriginalChildEvent(request));
        Assert.Null(runtime.ConsumeOriginalChildEvent(request));
        Assert.Null(runtime.ContainOriginalChildEvent(request));
        Assert.Null(runtime.InjectOriginalChildEvent(request));
        Assert.Null(runtime.InjectOriginalChildEvent(new(child, "second", 2, Op(74))));
        Assert.Same(request.SourceResourceId, fact.GetType().GetField("Payload")!.GetValue(fact));
        Assert.Same(raw, fact.GetType().GetField("Observation")!.GetValue(fact));
        Assert.Null(runtime.CloseChildDomain(child.Lease, Op(75)).Receipt);
        Assert.Equal(2, history.Count);
    }
    [Fact]
    public void UnknownNativeChildStateRefusesBeforeMailboxEffect()
    {
        var runtime = new HybridCpuExternalRuntime();
        var parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(51))).Receipt!;
        var child = runtime.CreateChildDomain(parent.Lease, new(Guid.NewGuid(), ExternalChildAuthority.EventInjection, 4096, Op(52))).Receipt!;
        var children = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("childRegistry", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        var owner = children[child.Lease.Handle]!;
        owner.GetType().GetProperty("State")!.SetValue(owner, (ExternalChildDomainState)255);
        var request = new ExternalOriginalChildEventRequest(child, "unknown-state", 1, Op(53));
        var denied = runtime.InjectOriginalChildEvent(request)!;
        Assert.True(denied.NoEffect); Assert.True(denied.NoFurtherDelivery); Assert.Null(denied.Receipt);
        var fact = Assert.Single(((IEnumerable)owner.GetType().GetProperty("OriginalEvents")!.GetValue(owner)!).Cast<object>());
        Assert.Null(fact.GetType().GetField("Payload")!.GetValue(fact));
        owner.GetType().GetProperty("State")!.SetValue(owner, ExternalChildDomainState.Ready);
        Assert.Null(runtime.InjectOriginalChildEvent(request)); Assert.Same(denied, runtime.ResolveOriginalChildEvent(request));
    }
    [Theory]
    [InlineData("consume")]
    [InlineData("contain")]
    [InlineData("refused")]
    [InlineData("history-loss")]
    public void OriginalMailboxPinsChildUntilExactSettlementAndCannotRepeat(string mode)
    {
        var runtime = new HybridCpuExternalRuntime();
        var parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(1))).Receipt!;
        var child = runtime.CreateChildDomain(parent.Lease, new(Guid.NewGuid(), ExternalChildAuthority.EventInjection | ExternalChildAuthority.Execute, 4096, Op(2))).Receipt!;
        if (mode != "refused") Assert.NotNull(runtime.TransitionChildDomain(child.Lease, ExternalChildDomainTransition.Start, Op(3)).Receipt);
        var domains = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("registry", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        var rootOwner = domains[parent.Lease.Handle]!;
        if (mode == "refused") rootOwner.GetType().GetProperty("InFlight")!.SetValue(rootOwner, Op(30));
        var request = new ExternalOriginalChildEventRequest(child, "virtual:event:original", 1, Op(4));
        var raw = runtime.InjectOriginalChildEvent(request)!;
        Assert.Same(raw, runtime.ResolveOriginalChildEvent(request));
        Assert.Null(runtime.InjectOriginalChildEvent(request)); Assert.Null(runtime.InjectOriginalChildEvent(request with { }));
        Assert.Null(runtime.ResolveOriginalChildEvent(request with { }));
        Assert.Null(runtime.ContainOriginalChildEvent(request with { }));
        var copiedAdmission = new ExternalOriginalChildEventRequest(child with { }, "virtual:event:foreign-copy", 2, Op(40));
        Assert.Null(runtime.InjectOriginalChildEvent(copiedAdmission));
        Assert.Null(runtime.ResolveOriginalChildEvent(copiedAdmission));
        var children = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("childRegistry", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        var owner = children[child.Lease.Handle]!;
        var history = (IList)owner.GetType().GetProperty("OriginalEvents")!.GetValue(owner)!;
        var fact = Assert.Single(history.Cast<object>());
        var payload = fact.GetType().GetField("Payload")!;
        if (mode == "refused")
        {
            Assert.True(raw.NoEffect); Assert.True(raw.NoFurtherDelivery); Assert.Null(payload.GetValue(fact));
            rootOwner.GetType().GetProperty("InFlight")!.SetValue(rootOwner, null);
            Assert.NotNull(runtime.TransitionChildDomain(child.Lease, ExternalChildDomainTransition.Start, Op(5)).Receipt);
            Assert.Null(runtime.InjectOriginalChildEvent(request));
            Assert.Same(raw, runtime.ConsumeOriginalChildEvent(request));
        }
        else
        {
            Assert.False(raw.NoEffect); Assert.False(raw.NoFurtherDelivery);
            Assert.Same(request.SourceResourceId, payload.GetValue(fact));
            Assert.Null(runtime.CloseChildDomain(child.Lease, Op(6)).Receipt);
            if (mode == "history-loss")
            {
                history.Clear(); Assert.Null(runtime.ResolveOriginalChildEvent(request));
                Assert.Null(runtime.ContainOriginalChildEvent(request)); Assert.Null(runtime.CloseChildDomain(child.Lease, Op(7)).Receipt);
                Assert.NotNull(payload.GetValue(fact)); return;
            }
            var completed = mode == "consume" ? runtime.ConsumeOriginalChildEvent(request)! : runtime.ContainOriginalChildEvent(request)!;
            Assert.True(completed.NoFurtherDelivery); Assert.False(completed.NoEffect); Assert.Same(raw.Receipt, completed.Receipt);
            Assert.Null(payload.GetValue(fact)); Assert.Same(completed, runtime.ResolveOriginalChildEvent(request));
            Assert.Same(completed, runtime.ConsumeOriginalChildEvent(request)); Assert.Same(completed, runtime.ContainOriginalChildEvent(request));
        }
        Assert.NotNull(runtime.CloseChildDomain(child.Lease, Op(8)).Receipt);
        Assert.True(runtime.CloseOriginalDomain(new(parent, Op(9)))!.NoFurtherUse);
    }
}
