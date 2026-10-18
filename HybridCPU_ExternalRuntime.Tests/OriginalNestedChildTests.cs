using System.Collections;
using System.Reflection;
using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime.Tests;

public sealed class OriginalNestedChildTests
{
    private static ExternalOperationIdentity Op(ulong n) => new(new(Guid.NewGuid()), new(n));
    [Fact]
    public void DuplicateNestedInventoryRefusesLookupAndNewEffectAndPreservesExactDescendantClose()
    {
        var runtime = new HybridCpuExternalRuntime();
        var root = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(71))).Receipt!;
        var parent = runtime.CreateOriginalChild(new(root, new(Guid.NewGuid(), ExternalChildAuthority.GuestMemory, 4096, Op(72))) { AllowNestedCreation = true })!.Raw.Receipt!;
        var request = new ExternalOriginalNestedChildRequest(parent, new(Guid.NewGuid(), ExternalChildAuthority.GuestMemory, 1024, Op(73)));
        var raw = runtime.CreateOriginalNestedChild(request)!;
        var children = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("childRegistry", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        var owner = children[parent.Lease.Handle]!;
        var rows = (IList)owner.GetType().GetProperty("OriginalNestedChildren")!.GetValue(owner)!;
        rows.Add(rows[0]); owner.GetType().GetProperty("NestedChildrenIssued")!.SetValue(owner, 2UL);
        Assert.Null(runtime.ResolveOriginalNestedChild(request));
        Assert.Null(runtime.CreateOriginalNestedChild(new(parent, new(Guid.NewGuid(), ExternalChildAuthority.GuestMemory, 512, Op(74)))));
        Assert.Equal(2, children.Count);
        Assert.True(runtime.CloseOriginalChild(new(raw.Raw.Receipt!, Op(75)))!.NoFurtherUse);
        Assert.True(runtime.CloseOriginalChild(new(parent, Op(76)))!.NoEffect);
    }
    [Fact]
    public void DuplicateRootFactoryInventoryRefusesExistingNestedObservationBeforeAnotherEffect()
    {
        var runtime = new HybridCpuExternalRuntime();
        var root = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(61))).Receipt!;
        var parentRequest = new ExternalOriginalChildCreateRequest(root, new(Guid.NewGuid(), ExternalChildAuthority.GuestMemory, 4096, Op(62))) { AllowNestedCreation = true };
        var parent = runtime.CreateOriginalChild(parentRequest)!.Raw.Receipt!;
        var request = new ExternalOriginalNestedChildRequest(parent, new(Guid.NewGuid(), ExternalChildAuthority.GuestMemory, 1024, Op(63)));
        var nested = runtime.CreateOriginalNestedChild(request)!;
        var roots = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("registry", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        var owner = roots[root.Lease.Handle]!;
        var rows = (IList)owner.GetType().GetProperty("OriginalChildCreates")!.GetValue(owner)!;
        rows.Add(rows[0]); owner.GetType().GetProperty("OriginalChildCreatesIssued")!.SetValue(owner, 2UL);
        Assert.Null(runtime.ResolveOriginalNestedChild(request));
        Assert.Null(runtime.CreateOriginalNestedChild(new(parent, new(Guid.NewGuid(), ExternalChildAuthority.GuestMemory, 512, Op(64)))));
        Assert.True(runtime.CloseOriginalChild(new(nested.Raw.Receipt!, Op(65)))!.NoFurtherUse);
    }
    [Theory]
    [InlineData("exact")] [InlineData("no-permission")] [InlineData("history-loss")]
    [InlineData("copy")] [InlineData("unknown-state")]
    public void OriginalNestedFactoryOwnsAdmissionAndBlocksParentUntilExactDescendantClosure(string mode)
    {
        var runtime = new HybridCpuExternalRuntime();
        var root = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(1))).Receipt!;
        var parentRequest = new ExternalOriginalChildCreateRequest(root, new(Guid.NewGuid(), ExternalChildAuthority.GuestMemory, 4096, Op(2)))
            { AllowNestedCreation = mode != "no-permission" };
        var parentRaw = runtime.CreateOriginalChild(parentRequest)!;
        var parent = parentRaw.Raw.Receipt!;
        Assert.Equal(mode != "no-permission", parentRaw.NestedCreationAllowed);
        var registry = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("childRegistry", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        var owner = registry[parent.Lease.Handle]!;
        if (mode == "unknown-state") owner.GetType().GetProperty("State")!.SetValue(owner, (ExternalChildDomainState)255);
        var request = new ExternalOriginalNestedChildRequest(parent, new(Guid.NewGuid(), ExternalChildAuthority.None, 1024, Op(3)), true);
        Assert.Null(runtime.CreateOriginalNestedChild(request with { ParentAdmission = parent with { } }));
        var raw = runtime.CreateOriginalNestedChild(request)!;
        Assert.Same(raw, runtime.ResolveOriginalNestedChild(request));
        Assert.Null(runtime.CreateOriginalNestedChild(request)); Assert.Null(runtime.CreateOriginalNestedChild(request with { }));
        Assert.Null(runtime.ResolveOriginalNestedChild(request with { }));
        if (mode is "no-permission" or "unknown-state")
        {
            Assert.True(raw.NoEffect); Assert.True(raw.NoFurtherAdmission); Assert.Null(raw.Raw.Receipt);
            owner.GetType().GetProperty("State")!.SetValue(owner, ExternalChildDomainState.Ready);
            Assert.Null(runtime.CreateOriginalNestedChild(request)); Assert.Same(raw, runtime.ResolveOriginalNestedChild(request));
            Assert.True(runtime.CloseOriginalChild(new(parent, Op(4)))!.NoFurtherUse);
            Assert.Same(raw, runtime.ResolveOriginalNestedChild(request));
            return;
        }
        Assert.False(raw.NoEffect); Assert.False(raw.NoFurtherAdmission);
        Assert.True(raw.NestedCreationAllowed);
        var child = raw.Raw.Receipt!;
        var refused = runtime.CloseOriginalChild(new(parent, Op(4)))!;
        Assert.True(refused.NoEffect); Assert.False(refused.NoFurtherUse);
        Assert.Null(runtime.CloseChildDomain(parent.Lease, Op(5)).Receipt);
        var history = (IList)owner.GetType().GetProperty("OriginalNestedChildren")!.GetValue(owner)!;
        if (mode == "history-loss") history.Clear();
        if (mode == "copy") history[0] = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(history[0], null)!;
        if (mode is "history-loss" or "copy")
        {
            Assert.Null(runtime.ResolveOriginalNestedChild(request));
            Assert.True(runtime.CloseOriginalChild(new(child, Op(6)))!.NoFurtherUse);
            Assert.True(runtime.CloseOriginalChild(new(parent, Op(7)))!.NoEffect);
            Assert.Null(runtime.CloseChildDomain(parent.Lease, Op(8)).Receipt);
            return;
        }
        var grandRequest = new ExternalOriginalNestedChildRequest(child, new(Guid.NewGuid(), ExternalChildAuthority.None, 512, Op(6)));
        var grand = runtime.CreateOriginalNestedChild(grandRequest)!.Raw.Receipt!;
        Assert.True(runtime.CloseOriginalChild(new(child, Op(7)))!.NoEffect);
        Assert.True(runtime.CloseOriginalChild(new(grand, Op(8)))!.NoFurtherUse);
        Assert.True(runtime.CloseOriginalChild(new(child, Op(9)))!.NoFurtherUse);
        Assert.True(runtime.CloseOriginalChild(new(parent, Op(10)))!.NoFurtherUse);
        Assert.Same(raw, runtime.ResolveOriginalNestedChild(request));
    }
}
