using System.Text.RegularExpressions;
namespace HybridCPU_ISE.Tests.Architecture;
public sealed class Rf1136RetiredCsrWriteRefThisHardeningTests
{
    [Fact] public void RetiredCsrWriteUsesStableIdentityAdapter()
    {
        string r = Retire();
        Assert.Equal(2, Regex.Matches(r, @"WriteRetiredCsrWithStableCoreIdentity\(").Count);
        Assert.Single(Regex.Matches(r, @"Core\.CSRMicroOp\.WriteCsr\(ref stableCoreIdentity,"));
        Assert.DoesNotContain("Core.CSRMicroOp.WriteCsr(\n                        ref this", r, StringComparison.Ordinal);
    }
    [Fact] public void SelectedRetireAndStorageRoutingRemainFrozen()
    {
        string root = Root(); string r = Retire();
        Order(r, "PrevalidateRetireWindowBatchForPublication(", "ApplyRetireBatchImmediateEffects(", "ApplyRetiredCsrEffect(retireEffect.CsrEffect);", "WriteRetiredCsrWithStableCoreIdentity(");
        string c = Read(root,"HybridCPU_ISE","CloseToHSL","Core","Pipeline","MicroOps","Control","MicroOp.Control.cs");
        Assert.Contains("case CsrStorageSurface.VectorPodPlane:", c); Assert.Contains("case CsrStorageSurface.WiredCsrFile:", c);
        Assert.Equal(0, Regex.Matches(c, @"\bcore\s*=(?!=)").Count);
    }
    [Fact] public void ResidualIsOneAtomicTestSupportCall()
    {
        string root=Root(); string prod=All(Path.Combine(root,"HybridCPU_ISE"));
        Assert.Empty(Regex.Matches(Retire(),@"\bref\s+this\s*[,\)]"));
        Assert.Empty(Regex.Matches(Read(root,"HybridCPU_ISE","CloseToHSL","Core","Pipeline","Core","CPU_Core.TestSupport.cs"),@"\bref\s+this\s*[,\)]"));
        Assert.Empty(Regex.Matches(prod,@"\bref\s+this\s*[,\)]"));
    }
    [Fact] public void EvidenceClosesOnlyRetiredCsrWrite()
    {
        string root=Root(); string l=Read(root,"Documentation", "ArchitectureAuthorityRefactor","10_RF11","00_CURRENT_STATUS_AND_LEDGER.md"); string e=Read(root,"Documentation", "ArchitectureAuthorityRefactor","Evidence","RF11","rf11.36-retired-csr-write-ref-this-hardening.md");
        Assert.Contains("RF-11.36 retired CSR-write ref-this seam hardening",l); Assert.Contains("no production state declaration",e,StringComparison.OrdinalIgnoreCase); Assert.Contains("--minimal-logs",e); Assert.Contains("RF-11.37",l);
    }
    static string Retire()=>Read(Root(),"HybridCPU_ISE","CloseToHSL","Core","Pipeline","Retire","Evidence","CPU_Core.PipelineExecution.Retire.cs");
    static void Order(string s,params string[] m){int p=-1;foreach(var x in m){int n=s.IndexOf(x,p+1,StringComparison.Ordinal);Assert.True(n>p,x);p=n;}}
    static string All(string p)=>string.Join('\n',Directory.GetFiles(p,"*.cs",SearchOption.AllDirectories).Where(f=>!f.Contains("\\bin\\")&&!f.Contains("\\obj\\")).Select(File.ReadAllText));
    static string Read(string r,params string[] p)=>File.ReadAllText(p.Aggregate(r,Path.Combine));
    static string Root(){string? c=AppContext.BaseDirectory;while(c!=null){if(File.Exists(Path.Combine(c,"HybridCPU v2.slnx")))return c;c=Directory.GetParent(c)?.FullName;}throw new DirectoryNotFoundException();}
}
