using System.Collections;
using System.Reflection;
using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime.Tests;

public sealed partial class CpuBackedChildExecutionTests
{
    [Theory]
    [InlineData(false,false)] [InlineData(true,false)] [InlineData(false,true)] [InlineData(true,true)]
    public void NativeArtifactReleaseHistoryControlsNewIoBytesButNotOriginalCleanup(bool legacy,bool descendant)
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var runtime=new HybridCpuExternalRuntime();
        var root=runtime.BindDomain(new(Guid.NewGuid(),ExternalDomainProfile.IsolatedDomain,Op(910))).Receipt!;
        var child=runtime.CreateOriginalChild(new(root,new(Guid.NewGuid(),ExternalChildAuthority.Execute|ExternalChildAuthority.GuestMemory|ExternalChildAuthority.VirtualIo,16*1024*1024,Op(911))){AllowNestedCreation=true})!.Raw.Receipt!;
        var mapping=runtime.MapChildGuestMemory(child.Lease,new(0,16*1024*1024,Op(912))).Receipt!.Mapping;
        var artifact=runtime.BindChildExecutableArtifact(child.Lease,new(mapping,Compile(nameof(LongRun)),3,Op(913))).Receipt!;
        var target=descendant?runtime.CreateOriginalNestedChild(new(child,new(Guid.NewGuid(),ExternalChildAuthority.VirtualIo,4096,Op(914))))!.Raw.Receipt!:child;
        var device=runtime.BindOriginalParentDevice(new(root,Guid.NewGuid(),"artifact-io",ExternalChildVirtualIoRights.Read|ExternalChildVirtualIoRights.Write,512,Op(915)))!;
        var admission=new ExternalOriginalVirtualIoRequest(target.Lease,device,Op(916));
        var bound=runtime.BindOriginalVirtualIo(admission)!;Assert.NotNull(bound);
        var write=new ExternalOriginalVirtualIoTransferRequest(admission,Op(917),0,true,1,[0x51]);
        var written=runtime.TransferOriginalVirtualIo(write)!;Assert.True(written.Completed);
        var read=new ExternalOriginalVirtualIoTransferRequest(admission,Op(918),0,false,1,null);
        var snapshot=runtime.TransferOriginalVirtualIo(read)!;
        var releaseRequest=new ExternalArtifactReleaseRequest(child.Lease,artifact,Op(919));
        var released=runtime.ReleaseChildExecutableArtifact(releaseRequest).Receipt!;Assert.True(released.NoFurtherUse);
        var owners=(IDictionary)typeof(HybridCpuExternalRuntime).GetField("childRegistry",flags)!.GetValue(runtime)!;
        var owner=owners[child.Lease.Handle]!;var property=owner.GetType().GetProperty("ArtifactRelease")!;
        var fact=property.GetValue(owner)!;var observation=fact.GetType().GetProperty("Observation")!;
        var roots=(IDictionary)typeof(HybridCpuExternalRuntime).GetField("registry",flags)!.GetValue(runtime)!;
        var devices=(IList)roots[root.Lease.Handle]!.GetType().GetProperty("OriginalParentDevices")!.GetValue(roots[root.Lease.Handle])!;
        var deviceOwner=Assert.Single(devices.Cast<object>());var backing=(byte[])deviceOwner.GetType().GetField("Backing")!.GetValue(deviceOwner)!;
        bool Transfer(byte value)=>legacy?runtime.TransferOriginalVirtualIo(admission,0,[value],true):runtime.TransferOriginalVirtualIo(new(admission,Op(920),0,true,1,[value]))?.Completed==true;
        void Denied(){var admitted=Transfer(0xEE);Assert.Equal((false,(byte)0x51),(admitted,backing[0]));}
        observation.SetValue(fact,null);Denied();
        Assert.Null(runtime.ResolveChildExecutableArtifactRelease(releaseRequest));
        Assert.Same(written,runtime.ResolveOriginalVirtualIoTransfer(write));
        Assert.Equal(new byte[]{0x51},snapshot.CopyReadData());
        Assert.True(runtime.ReleaseOriginalVirtualIoSnapshot(new(snapshot))!.NoFurtherSnapshotRead);
        observation.SetValue(fact,released with { });Denied();
        Assert.Null(runtime.ResolveChildExecutableArtifactRelease(releaseRequest));
        observation.SetValue(fact,released);
        property.SetValue(owner,null);Denied();
        property.SetValue(owner,typeof(object).GetMethod("MemberwiseClone",flags)!.Invoke(fact,null));Denied();
        property.SetValue(owner,fact);
        Assert.Same(released,runtime.ResolveChildExecutableArtifactRelease(releaseRequest));
        Assert.True(Transfer(0x61));Assert.Equal(0x61,backing[0]);
        Assert.True(runtime.CloseOriginalVirtualIo(new(admission,bound,Op(921)))!.NoFurtherUse);
        Assert.True(runtime.CloseOriginalParentDevice(device)!.NoFurtherUse);Assert.All(backing,value=>Assert.Equal(0,value));
        Assert.Same(released,runtime.ResolveChildExecutableArtifactRelease(releaseRequest));
    }
}
