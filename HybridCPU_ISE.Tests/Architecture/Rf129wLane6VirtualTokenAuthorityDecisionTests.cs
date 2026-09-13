namespace HybridCPU_ISE.Tests.Architecture;
public sealed class Rf129wLane6VirtualTokenAuthorityDecisionTests
{
 [Fact] public void PaperKeepsVirtualNativeMappingOwnerLocalAndPredicateNonAuthoritative(){string p=File.ReadAllText(Path.Combine(Root(),"ResearchPaper","section","md base","3_Architectural_Overview_and_Frontend_Contract.md"));Assert.Contains("For a Lane-6 virtual token handle",p,StringComparison.Ordinal);Assert.Contains("Zero/default is unissued or absent",p,StringComparison.Ordinal);Assert.Contains("compatibility/evidence seam",p,StringComparison.Ordinal);Assert.Contains("requires a separate decision",p,StringComparison.Ordinal);}
 private static string Root(){DirectoryInfo? c=new(AppContext.BaseDirectory);while(c is not null){if(Directory.Exists(Path.Combine(c.FullName,"ResearchPaper")))return c.FullName;c=c.Parent;}throw new DirectoryNotFoundException();}
}
