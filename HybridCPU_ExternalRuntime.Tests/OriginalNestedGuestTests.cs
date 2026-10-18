using System.Collections;
using System.Reflection;
using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime.Tests;

public sealed class OriginalNestedGuestTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static ExternalOperationIdentity Op(ulong n) => new(new(Guid.NewGuid()), new(n));
    [Theory]
    [InlineData("exact")] [InlineData("history-before")] [InlineData("history-after")]
    [InlineData("no-effect")] [InlineData("parent-copy")]
    public void NestedGuestMapEffectAndPureCleanupRequireOriginalAncestorFactories(string mode)
    {
        var runtime = new HybridCpuExternalRuntime();
        var root = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(1))).Receipt!;
        var parent = runtime.CreateOriginalChild(new(root, new(Guid.NewGuid(), ExternalChildAuthority.GuestMemory, 4096, Op(2)))
            { AllowNestedCreation = true })!.Raw.Receipt!;
        var child = runtime.CreateOriginalNestedChild(new(parent, new(Guid.NewGuid(), ExternalChildAuthority.GuestMemory, 1024, Op(3))))!.Raw.Receipt!;
        var registry = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("childRegistry", Private)!.GetValue(runtime)!;
        var owner = registry[parent.Lease.Handle]!;
        var childOwner = registry[child.Lease.Handle]!;
        var history = (IList)owner.GetType().GetProperty("OriginalNestedChildren")!.GetValue(owner)!;
        var request = new ExternalOriginalGuestMapRequest(child, new(0, 512, Op(4)));
        if (mode == "history-before") history.Clear();
        if (mode == "parent-copy") registry[parent.Lease.Handle] = typeof(object).GetMethod("MemberwiseClone", Private)!.Invoke(owner, null)!;
        if (mode == "no-effect") owner.GetType().GetProperty("State")!.SetValue(owner, (ExternalChildDomainState)255);
        var raw = runtime.MapOriginalGuest(request);
        var mappings = (IDictionary)childOwner.GetType().GetProperty("Mappings")!.GetValue(childOwner)!;
        if (mode is "history-before" or "parent-copy")
        {
            Assert.Null(raw); Assert.Empty(mappings); Assert.Null(runtime.ResolveOriginalGuestMap(request)); return;
        }
        Assert.NotNull(raw);
        Assert.Null(runtime.MapOriginalGuest(request)); Assert.Null(runtime.ResolveOriginalGuestMap(request with { }));
        if (mode == "no-effect")
        {
            Assert.True(raw.NoEffect); Assert.Empty(mappings);
            owner.GetType().GetProperty("State")!.SetValue(owner, ExternalChildDomainState.Ready);
            Assert.Same(raw, runtime.ResolveOriginalGuestMap(request));
            Assert.Null(runtime.MapOriginalGuest(request));
            Assert.True(runtime.CloseOriginalChild(new(child, Op(5)))!.NoFurtherUse);
            Assert.True(runtime.CloseOriginalChild(new(parent, Op(6)))!.NoFurtherUse);
            Assert.Same(raw, runtime.ResolveOriginalGuestMap(request)); return;
        }
        Assert.False(raw.NoEffect); Assert.Single(mappings);
        if (mode == "history-after")
        {
            history.Clear(); Assert.Null(runtime.ResolveOriginalGuestMap(request));
            Assert.Null(runtime.UnmapOriginalGuest(new(raw.Raw.Receipt!, Op(5))));
            Assert.Single(mappings); Assert.True(runtime.CloseOriginalChild(new(child, Op(6)))!.NoEffect); return;
        }
        Assert.Same(raw, runtime.ResolveOriginalGuestMap(request));
        var unmap = new ExternalOriginalGuestUnmapRequest(raw.Raw.Receipt!, Op(5));
        var closure = runtime.UnmapOriginalGuest(unmap)!;
        Assert.True(closure.NoFurtherUse); Assert.False(closure.NoEffect);
        Assert.Same(closure, runtime.ResolveOriginalGuestUnmap(unmap));
        Assert.Null(runtime.UnmapOriginalGuest(unmap));
        Assert.True(runtime.CloseOriginalChild(new(child, Op(6)))!.NoFurtherUse);
        Assert.True(runtime.CloseOriginalChild(new(parent, Op(7)))!.NoFurtherUse);
        Assert.Same(closure, runtime.ResolveOriginalGuestUnmap(unmap));
    }
}
