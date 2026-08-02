namespace HybridCPU_ISE.Tests.Architecture;
public sealed class Rf1146DefaultCoreTestSeamHardeningTests
{
 [Fact] public void MatrixTileCaptureUsesConstructedCore(){string m=Read("HybridCPU_ISE.Tests","tests","Phase09MatrixTileRuntimeIsaPackageContractTests.cs");Assert.Contains("Processor.CPU_Core core = new(0);",m);Assert.DoesNotContain("Processor.CPU_Core core = "+"default;",m);Assert.Contains("tileMicroOp.Execute(ref core)",m);}
 [Fact] public void DefaultExpectationCoversStructAndClassEras(){string t=Read("HybridCPU_ISE.Tests","Architecture","Rf113EmptyCoreRuntimeStateTests.cs");Assert.Contains("typeof(Processor.CPU_Core).IsValueType",t);Assert.Contains("Assert.Throws<InvalidOperationException>(() => absent.Runtime);",t);Assert.Contains("Assert.Null(default(Processor.CPU_Core));",t);}
 [Fact] public void EvidenceClosesOnlyDefaultTestSeam(){string e=Read("Documentation", "ArchitectureAuthorityRefactor","Evidence","RF11","rf11.46-default-core-test-seam-hardening.md");string l=Read("Documentation", "ArchitectureAuthorityRefactor","10_RF11","00_CURRENT_STATUS_AND_LEDGER.md");Assert.Contains("no production code",e,StringComparison.OrdinalIgnoreCase);Assert.Contains("TestAssemblerConsoleApps was not run",e);Assert.Contains("RF-11.47",l);}
 static string Read(params string[] p)=>File.ReadAllText(p.Aggregate(Root(),Path.Combine));static string Root(){string? c=AppContext.BaseDirectory;while(c!=null){if(File.Exists(Path.Combine(c,"HybridCPU v2.slnx")))return c;c=Directory.GetParent(c)?.FullName;}throw new DirectoryNotFoundException();}
}
