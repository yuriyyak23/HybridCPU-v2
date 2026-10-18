using System.Collections;
using System.Reflection;
using HybridCPU.ExternalRuntime.Contracts;
namespace HybridCPU.ExternalRuntime.Tests;
public sealed class OriginalParentDeviceCloseContinuationTests
{
    private static ExternalOperationIdentity Op(ulong n)=>new(new(Guid.NewGuid()),new(n));
    [Theory] [InlineData(false)] [InlineData(true)]
    public void RetainedOriginalDeviceCloseWaitsForIndependentUsersAndExactTransferSource(bool missing)
    {
        const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        var runtime=new HybridCpuExternalRuntime();
        var root=runtime.BindDomain(new(Guid.NewGuid(),ExternalDomainProfile.IsolatedDomain,Op(1))).Receipt!;
        var child=runtime.CreateOriginalChild(new(root,new(Guid.NewGuid(),ExternalChildAuthority.VirtualIo,4096,Op(2))))!.Raw.Receipt!;
        var peer=runtime.CreateOriginalChild(new(root,new(Guid.NewGuid(),ExternalChildAuthority.VirtualIo,4096,Op(3))))!.Raw.Receipt!;
        var device=runtime.BindOriginalParentDevice(new(root,Guid.NewGuid(),"device-close",ExternalChildVirtualIoRights.Read|ExternalChildVirtualIoRights.Write,512,Op(4)))!;
        var one=new ExternalOriginalVirtualIoRequest(child.Lease,device,Op(5));var two=new ExternalOriginalVirtualIoRequest(peer.Lease,device,Op(6));
        var first=runtime.BindOriginalVirtualIo(one)!;var second=runtime.BindOriginalVirtualIo(two)!;
        Assert.True(runtime.TransferOriginalVirtualIo(new(one,Op(7),0,true,1,[0x51]))!.Completed);
        var read=new ExternalOriginalVirtualIoTransferRequest(two,Op(8),0,false,1,null);var snapshot=runtime.TransferOriginalVirtualIo(read)!;
        var roots=(IDictionary)typeof(HybridCpuExternalRuntime).GetField("registry",hidden)!.GetValue(runtime)!;
        var parent=roots[root.Lease.Handle]!;var owner=Assert.Single(((IEnumerable)parent.GetType().GetProperty("OriginalParentDevices")!.GetValue(parent)!).Cast<object>());
        var backing=(byte[])owner.GetType().GetField("Backing")!.GetValue(owner)!;
        Assert.Null(runtime.ContinueOriginalParentDeviceClose(device));Assert.Null(runtime.CloseOriginalParentDevice(device with {}));
        Assert.Null(owner.GetType().GetField("CloseAdmission")!.GetValue(owner));
        Assert.Null(runtime.CloseOriginalParentDevice(device));Assert.Same(device,owner.GetType().GetField("CloseAdmission")!.GetValue(owner));
        Assert.Null(runtime.BindOriginalVirtualIo(new(peer.Lease,device,Op(9))));
        Assert.Same(second,runtime.ResolveOriginalVirtualIo(two));
        Assert.True(runtime.TransferOriginalVirtualIo(new(two,Op(10),0,true,1,[0x61]))!.Completed);
        Assert.True(runtime.CloseOriginalVirtualIo(new(one,first,Op(11)))!.NoFurtherUse);
        Assert.Null(runtime.ContinueOriginalParentDeviceClose(device));Assert.Equal(0x61,backing[0]);
        Assert.True(runtime.TransferOriginalVirtualIo(new(two,Op(12),0,true,1,[0x62]))!.Completed);
        Assert.Equal(new byte[]{0x51},snapshot.CopyReadData());
        Assert.True(runtime.CloseOriginalVirtualIo(new(two,second,Op(13)))!.NoFurtherUse);
        Assert.Throws<InvalidOperationException>(()=>snapshot.CopyReadData());Assert.Equal(0x62,backing[0]);
        var io=((IEnumerable)owner.GetType().GetProperty("Users")!.GetValue(owner)!).Cast<object>().Single(f=>ReferenceEquals(f.GetType().GetProperty("Request")!.GetValue(f),two));
        var fact=((IEnumerable)io.GetType().GetProperty("Transfers")!.GetValue(io)!).Cast<object>().Single(f=>ReferenceEquals(f.GetType().GetField("Request")!.GetValue(f),read));
        var raw=fact.GetType().GetField("Raw")!;raw.SetValue(fact,missing?null:typeof(object).GetMethod("MemberwiseClone",hidden)!.Invoke(snapshot,null));
        Assert.Null(runtime.ContinueOriginalParentDeviceClose(device));Assert.Null(runtime.ResolveOriginalParentDeviceClosure(device));Assert.Equal(0x62,backing[0]);
        raw.SetValue(fact,snapshot);Assert.Null(runtime.ResolveOriginalParentDeviceClosure(device)); // Query cannot clear backing.
        Assert.Null(runtime.ContinueOriginalParentDeviceClose(device with {}));Assert.Equal(0x62,backing[0]);
        var intent=owner.GetType().GetField("CloseAdmission")!;intent.SetValue(owner,device with {});
        Assert.Null(runtime.CloseOriginalParentDevice(device));Assert.Null(runtime.ContinueOriginalParentDeviceClose(device));Assert.Equal(0x62,backing[0]);
        intent.SetValue(owner,null);Assert.Null(runtime.ContinueOriginalParentDeviceClose(device));Assert.Equal(0x62,backing[0]);
        intent.SetValue(owner,device);
        var closed=runtime.ContinueOriginalParentDeviceClose(device)!;Assert.True(closed.NoFurtherUse);Assert.All(backing,b=>Assert.Equal(0,b));
        Assert.Same(closed,runtime.ResolveOriginalParentDeviceClosure(device));Assert.Same(closed,runtime.ContinueOriginalParentDeviceClose(device));
        var closure=owner.GetType().GetField("Closure")!;closure.SetValue(owner,closed with {});
        Assert.Null(runtime.ResolveOriginalParentDeviceClosure(device));Assert.Null(runtime.ContinueOriginalParentDeviceClose(device));
        closure.SetValue(owner,closed);Assert.Same(closed,runtime.ResolveOriginalParentDeviceClosure(device));
    }
}
