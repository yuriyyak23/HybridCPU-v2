namespace HybridCPU_ISE.Tests.Architecture;
public sealed class Rf129acLane7VirtualTokenInventoryTests
{
 [Fact] public void Lane7VirtualTokenHasSeparateStoreAndHostBinding(){string s=Read("HybridCPU_ISE","CloseToHSL","Core","Runtime","Lanes","Lane7","Lane7StateBlock.cs");string e=Read("HybridCPU_ISE","CloseToHSL","Core","Runtime","Lanes","Lane7","HostOwnedEvidence","Lane7HostOwnedEvidenceStore.cs");Assert.Contains("record struct Lane7VirtualToken(",s,StringComparison.Ordinal);Assert.Contains("virtualToken = new Lane7VirtualToken(",s,StringComparison.Ordinal);Assert.Contains("Dictionary<ulong, Lane7VirtualToken>",s,StringComparison.Ordinal);Assert.Contains("Dictionary<ulong, AcceleratorTokenHandle>",e,StringComparison.Ordinal);Assert.Contains("TryBindToken(virtualToken, token.Handle)",s,StringComparison.Ordinal);}
 private static string Read(params string[] p)=>File.ReadAllText(Path.Combine(new[]{Root()}.Concat(p).ToArray()));private static string Root(){DirectoryInfo? c=new(AppContext.BaseDirectory);while(c is not null){if(Directory.Exists(Path.Combine(c.FullName,"HybridCPU_ISE")))return c.FullName;c=c.Parent;}throw new DirectoryNotFoundException();}
}
