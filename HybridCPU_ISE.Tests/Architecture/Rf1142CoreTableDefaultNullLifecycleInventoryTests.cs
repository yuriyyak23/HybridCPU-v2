using System.Text.RegularExpressions;
namespace HybridCPU_ISE.Tests.Architecture;
public sealed class Rf1142CoreTableDefaultNullLifecycleInventoryTests
{
    [Fact] public void StructEraTableAndLifecycleSurfaceIsFrozen()
    {
        string root=Root();string p=Read(root,"HybridCPU_ISE","NonRTL","Processor","Core","Processor.cs");string i=Read(root,"HybridCPU_ISE","NonRTL","Processor","Core","Processor.CoreIdentity.cs");string o=Read(root,"HybridCPU_ISE","Legacy","Obsolete","Processor.Initialization.Obsolete.cs");
        Assert.Contains("public static CPU_Core[] CPU_Cores { get; private set; } = Array.Empty<CPU_Core>();",p);Assert.Contains("BeginCoreTableConstruction(1024);",o);Assert.Contains("ValidateCoreTableConstructionComplete();",o);Assert.Contains("private static ref CPU_Core GetCoreSlotRef",i);Assert.Contains("_ = liveCore.Runtime;",i);Assert.Contains("_ = replacement.Runtime;",i);
    }
    [Fact] public void DefaultAndDirectTestTableMutationSeamsAreFrozen()
    {
        string root=Root();string tests=Sources(Path.Combine(root,"HybridCPU_ISE.Tests"));
        Assert.Contains("Assert.Null(default(Processor.CPU_Core));",tests);Assert.DoesNotContain("Processor.CPU_Core core = "+"default;",tests);
        Assert.Empty(Regex.Matches(tests,@"Processor\.CPU_Cores\s*="));
        Assert.Contains("Processor.RestoreCoreTableForTesting(originalCores);",tests);
    }
    [Fact] public void AuthorityDoesNotDefineClassArrayAbsentSlotPolicy()
    {
        string root=Root();string adr=Read(root,"Documentation", "Documentation", "ArchitectureAuthorityRefactor","02_Authority","ADR-010_CPU_Core_State_Ownership.md");string e=Read(root,"Documentation", "Documentation", "ArchitectureAuthorityRefactor","Evidence","RF11","rf11.42-core-table-default-null-lifecycle-inventory.md");
        Assert.Contains("sealed partial class CPU_Core",adr);Assert.Contains("do not define",e,StringComparison.OrdinalIgnoreCase);Assert.Contains("RF-11.43",e);
    }
    [Fact] public void InventoryMovesNoRuntimeState()
    {
        string root=Root();string l=Read(root,"Documentation", "Documentation", "ArchitectureAuthorityRefactor","10_RF11","00_CURRENT_STATUS_AND_LEDGER.md");string e=Read(root,"Documentation", "Documentation", "ArchitectureAuthorityRefactor","Evidence","RF11","rf11.42-core-table-default-null-lifecycle-inventory.md");
        Assert.Contains("RF-11.42 CPU_Core table/default/null lifecycle inventory/freeze",l);Assert.Contains("no production code",e,StringComparison.OrdinalIgnoreCase);Assert.Contains("TestAssemblerConsoleApps was not run",e);
    }
    static string Sources(string p)=>string.Join('\n',Directory.GetFiles(p,"*.cs",SearchOption.AllDirectories).Where(f=>!f.Contains("\\bin\\")&&!f.Contains("\\obj\\")).Select(File.ReadAllText));
    static string Read(string r,params string[] p)=>File.ReadAllText(p.Aggregate(r,Path.Combine));static string Root(){string? c=AppContext.BaseDirectory;while(c!=null){if(File.Exists(Path.Combine(c,"HybridCPU v2.slnx")))return c;c=Directory.GetParent(c)?.FullName;}throw new DirectoryNotFoundException();}
}
