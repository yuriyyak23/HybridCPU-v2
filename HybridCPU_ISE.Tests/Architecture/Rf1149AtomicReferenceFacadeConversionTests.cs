using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU;
namespace HybridCPU_ISE.Tests.Architecture;
public sealed class Rf1149AtomicReferenceFacadeConversionTests
{
 [Fact] public void CpuCoreIsOneSealedReferenceFacade(){Type t=typeof(Processor.CPU_Core);Assert.False(t.IsValueType);Assert.True(t.IsClass);Assert.True(t.IsSealed);FieldInfo f=Assert.Single(t.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly));Assert.Equal("_runtime",f.Name);Assert.True(f.IsInitOnly);Assert.Equal("CoreRuntimeState",f.FieldType.Name);}
 [Fact] public void AllPartialDeclarationsConvertedAtomically(){string p=Sources(Path.Combine(Root(),"HybridCPU_ISE"));Assert.Empty(Regex.Matches(p,@"partial\s+struct\s+CPU_Core"));Assert.Equal(66,Regex.Matches(p,@"sealed\s+partial\s+class\s+CPU_Core").Count);Assert.Empty(Regex.Matches(p,@"\bref\s+this(?:\s*[,\)]|\.)"));Assert.DoesNotContain("private readonly ref",p);}
 [Fact]
 public void FacadeAliasesPreserveRuntimeAndObjectIdentity()
 {
#pragma warning disable CS0618
  Processor.CPU_Core core = new(0);
#pragma warning restore CS0618
  Processor.CPU_Core alias = core;
  Assert.Same(core, alias);
  Assert.Same(core.Runtime, alias.Runtime);
 }
 [Fact] public void ClassEraLifecycleFailsClosed(){string i=Read("HybridCPU_ISE","NonRTL","Processor","Core","Processor.CoreIdentity.cs");Assert.Contains("if (liveCore is null)",i);Assert.Contains("ArgumentNullException.ThrowIfNull(replacement);",i);Assert.Contains("if (cores[coreId] is null)",i);}
 [Fact] public void FrozenCycleAndAuthorityBoundaryRemain(){string s=Read("HybridCPU_ISE","CloseToHSL","Core","Pipeline","ExecutionFlow","StageFlow","CPU_Core.PipelineExecution.StageFlow.cs");Order(s,"MemoryCyclePlatformOrchestrator.AdvanceCoreObservedPlatformEdge(","RefreshInFlightExplicitMemoryProgress();","PipelineStage_WriteBack();","PipelineStage_Memory();","PipelineStage_Execute();","PipelineStage_Decode();","PipelineStage_Fetch();");string r=Read("HybridCPU_ISE","CloseToHSL","Core","Pipeline","Retire","Evidence","CPU_Core.PipelineExecution.Retire.cs");Assert.Contains("RetireCoordinator.Prevalidate(retireBatch.RetireRecords);",r);Assert.Contains("RetireCoordinator.Retire(retireBatch.RetireRecords);",r);}
 [Fact] public void EvidenceNamesOnlyAccessorCutoverNext(){string e=Read("Documentation", "ArchitectureAuthorityRefactor","Evidence","RF11","rf11.49-atomic-reference-facade-conversion.md");string l=Read("Documentation", "ArchitectureAuthorityRefactor","10_RF11","00_CURRENT_STATUS_AND_LEDGER.md");Assert.Contains("storage location is unchanged",e,StringComparison.OrdinalIgnoreCase);Assert.Contains("--minimal-logs",e);Assert.Contains("RF-11.50",l);}
 static void Order(string s,params string[] m){int p=-1;foreach(string x in m){int n=s.IndexOf(x,p+1,StringComparison.Ordinal);Assert.True(n>p,x);p=n;}}static IEnumerable<string> Files(string p)=>Directory.GetFiles(p,"*.cs",SearchOption.AllDirectories).Where(f=>!f.Contains("\\bin\\")&&!f.Contains("\\obj\\"));static string Sources(string p)=>string.Join('\n',Files(p).Select(File.ReadAllText));static string Read(params string[] p)=>File.ReadAllText(p.Aggregate(Root(),Path.Combine));static string Root(){string? c=AppContext.BaseDirectory;while(c!=null){if(File.Exists(Path.Combine(c,"HybridCPU v2.slnx")))return c;c=Directory.GetParent(c)?.FullName;}throw new DirectoryNotFoundException();}
}
