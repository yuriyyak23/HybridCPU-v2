namespace HybridCPU_ISE.Tests.Architecture;
public sealed class Rf1143CoreTableDefaultNullLifecycleDecisionTests
{
 [Fact] public void PaperDefinesLifecycleAuthority(){string p=Read("ResearchPaper","section","md base","3_Architectural_Overview_and_Frontend_Contract.md");Assert.Contains("### 3.6 Stable Core Identity and Platform Table Lifecycle",p);Assert.Contains("absent slot",p);Assert.Contains("ready platform exposes only populated slots",p);Assert.Contains("Whole-core replacement is an explicit platform lifecycle action",p);}
 [Fact] public void DecisionMovesNoProductionState(){string e=Read("Documentation", "Documentation", "ArchitectureAuthorityRefactor","Evidence","RF11","rf11.43-table-default-null-lifecycle-decision.md");Assert.Contains("changes no production declaration",e,StringComparison.OrdinalIgnoreCase);Assert.Contains("TestAssemblerConsoleApps was not run",e);}
 [Fact] public void LedgerNamesOneImplementationTask(){string l=Read("Documentation", "Documentation", "ArchitectureAuthorityRefactor","10_RF11","00_CURRENT_STATUS_AND_LEDGER.md");Assert.Contains("RF-11.43 table/default/null lifecycle architecture decision",l);Assert.Contains("RF-11.44 production core-table lifecycle implementation",l);}
 static string Read(params string[] p)=>File.ReadAllText(p.Aggregate(Root(),Path.Combine));static string Root(){string? c=AppContext.BaseDirectory;while(c!=null){if(File.Exists(Path.Combine(c,"HybridCPU v2.slnx")))return c;c=Directory.GetParent(c)?.FullName;}throw new DirectoryNotFoundException();}
}
