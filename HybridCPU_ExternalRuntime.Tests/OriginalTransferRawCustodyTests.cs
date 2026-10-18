using System.Collections;
using System.Reflection;
using HybridCPU.ExternalRuntime.Contracts;
namespace HybridCPU.ExternalRuntime.Tests;
public sealed class OriginalTransferRawCustodyTests
{
    private static ExternalOperationIdentity Op(ulong n)=>new(new(Guid.NewGuid()),new(n));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LostReplyCopiedRawCannotCloseOriginalSnapshot(bool closeIo)
    {
        const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        var runtime=new HybridCpuExternalRuntime();
        var root=runtime.BindDomain(new(Guid.NewGuid(),ExternalDomainProfile.IsolatedDomain,Op(1))).Receipt!;
        var child=runtime.CreateOriginalChild(new(root,new(Guid.NewGuid(),ExternalChildAuthority.VirtualIo,4096,Op(2))))!.Raw.Receipt!;
        var device=runtime.BindOriginalParentDevice(new(root,Guid.NewGuid(),"raw-custody",ExternalChildVirtualIoRights.Read|ExternalChildVirtualIoRights.Write,512,Op(3)))!;
        var request=new ExternalOriginalVirtualIoRequest(child.Lease,device,Op(4));
        var binding=runtime.BindOriginalVirtualIo(request)!;
        Assert.True(runtime.TransferOriginalVirtualIo(new(request,Op(5),0,true,1,[0x71]))!.Completed);
        var read=new ExternalOriginalVirtualIoTransferRequest(request,Op(6),0,false,1,null);
        var produced=runtime.TransferOriginalVirtualIo(read)!; // Producer-side witness; initial reply is not consumed.
        var owners=(IDictionary)typeof(HybridCpuExternalRuntime).GetField("childRegistry",hidden)!.GetValue(runtime)!;
        var owner=owners[child.Lease.Handle]!;
        var io=Assert.Single(((IEnumerable)owner.GetType().GetProperty("OriginalVirtualIo")!.GetValue(owner)!).Cast<object>());
        var facts=((IEnumerable)io.GetType().GetProperty("Transfers")!.GetValue(io)!).Cast<object>();
        var fact=facts.Single(f=>ReferenceEquals(f.GetType().GetField("Request")!.GetValue(f),read));
        var raw=fact.GetType().GetField("Raw")!;
        var copy=(ExternalOriginalVirtualIoTransferReceipt)typeof(object).GetMethod("MemberwiseClone",hidden)!.Invoke(produced,null)!;
        raw.SetValue(fact,copy);
        var close=new ExternalOriginalVirtualIoCloseRequest(request,binding,Op(7));
        Assert.Null(runtime.ContinueOriginalVirtualIoClose(close));
        var closure=closeIo ? runtime.CloseOriginalVirtualIo(close)?.NoFurtherUse==true : runtime.ReleaseOriginalVirtualIoSnapshot(new(copy))?.NoFurtherSnapshotRead==true;
        Assert.Equal((false,(byte)0x71),(closure,produced.CopyReadData()[0]));
        Assert.Null(runtime.ResolveOriginalVirtualIoTransfer(read));
        Assert.Null(runtime.TransferOriginalVirtualIo(read));
        Assert.Null(runtime.TransferOriginalVirtualIo(new(request,Op(8),0,true,1,[0xEE])));
        Assert.Equal(2UL,io.GetType().GetField("TransfersIssued")!.GetValue(io));
        Assert.Null(runtime.ResolveOriginalVirtualIoClosure(close));
        raw.SetValue(fact,produced);
        Assert.Null(runtime.ResolveOriginalVirtualIoClosure(close));
        Assert.Null(runtime.ContinueOriginalVirtualIoClose(close with {}));
        Assert.Same(produced,runtime.ResolveOriginalVirtualIoTransfer(read));
        if(closeIo)
        {
            Assert.Null(runtime.TransferOriginalVirtualIo(new(request,Op(9),0,true,1,[0xEE])));
            Assert.False(runtime.TransferOriginalVirtualIo(request,0,[0xEE],true));
        }
        Assert.True(runtime.ReleaseOriginalVirtualIoSnapshot(new(produced))!.NoFurtherSnapshotRead);
        Assert.Throws<InvalidOperationException>(()=>produced.CopyReadData());
        Assert.Null(runtime.ContinueOriginalVirtualIoClose(close with {}));
        var closed=closeIo?runtime.ContinueOriginalVirtualIoClose(close):runtime.CloseOriginalVirtualIo(close);
        Assert.True(closed!.NoFurtherUse);Assert.Same(closed,runtime.ResolveOriginalVirtualIoClosure(close));
        Assert.Same(closed,runtime.ContinueOriginalVirtualIoClose(close));
        var closureField=io.GetType().GetField("Closure")!;closureField.SetValue(io,closed with {});
        Assert.Null(runtime.ResolveOriginalVirtualIoClosure(close));Assert.Null(runtime.ContinueOriginalVirtualIoClose(close));
        closureField.SetValue(io,closed);Assert.Same(closed,runtime.ResolveOriginalVirtualIoClosure(close));
        Assert.True(runtime.CloseOriginalParentDevice(device)!.NoFurtherUse);
    }
}
