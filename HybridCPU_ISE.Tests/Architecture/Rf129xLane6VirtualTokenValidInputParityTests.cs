namespace HybridCPU_ISE.Tests.Architecture;
public sealed class Rf129xLane6VirtualTokenValidInputParityTests
{
 [Fact] public void ValidMappingRetainsTypedVirtualAndNativeSignatures(){string s=Read("HybridCPU_ISE","CloseToHSL","Core","Runtime","Lanes","Lane6","Lane6StateBlock.cs");string e=Read("HybridCPU_ISE","CloseToHSL","Core","Runtime","Lanes","Lane6","HostOwnedEvidence","Lane6HostOwnedEvidenceStore.cs");Assert.Contains("DmaStreamComputeTokenHandle hostHandle",s,StringComparison.Ordinal);Assert.Contains("out Lane6VirtualToken virtualToken",s,StringComparison.Ordinal);Assert.Contains("HostEvidence.TryResolve(virtualToken, out hostHandle)",s,StringComparison.Ordinal);Assert.Contains("TryBind(",e,StringComparison.Ordinal);Assert.Contains("TryResolve(",e,StringComparison.Ordinal);}
 private static string Read(params string[] p)=>File.ReadAllText(Path.Combine(new[]{Root()}.Concat(p).ToArray())); private static string Root(){DirectoryInfo? c=new(AppContext.BaseDirectory);while(c is not null){if(Directory.Exists(Path.Combine(c.FullName,"HybridCPU_ISE")))return c.FullName;c=c.Parent;}throw new DirectoryNotFoundException();}
}
