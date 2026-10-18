namespace HybridCPU_ISE.Tests.Architecture;
public sealed class Rf129zLane6VirtualTokenInvalidBehaviorDecisionTests
{
 [Fact] public void PaperKeepsInvalidMappingAndRestoreOwnerLocal(){string p=File.ReadAllText(Path.Combine(Root(),"ResearchPaper","section","md base","3_Architectural_Overview_and_Frontend_Contract.md"));Assert.Contains("Default, malformed or unmapped Lane-6 virtual token inputs",p,StringComparison.Ordinal);Assert.Contains("fails before a virtual/native binding",p,StringComparison.Ordinal);Assert.Contains("host-owned rebuild",p,StringComparison.Ordinal);Assert.Contains("No shared invalid result",p,StringComparison.Ordinal);}
 private static string Root(){DirectoryInfo? c=new(AppContext.BaseDirectory);while(c is not null){if(Directory.Exists(Path.Combine(c.FullName,"ResearchPaper")))return c.FullName;c=c.Parent;}throw new DirectoryNotFoundException();}
}
