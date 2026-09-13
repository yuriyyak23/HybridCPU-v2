namespace HybridCPU_ISE.Tests.Architecture;
public sealed class Rf129aaLane6VirtualTokenConstructorEligibilityTests
{
 [Fact] public void ConstructorHasProductionAllocatorAndDirectTestWitness(){string r=Read("HybridCPU_ISE","CloseToHSL","Core","Runtime","Lanes","Lane6","Lane6QueueRuntime.cs");string t=Read("HybridCPU_ISE.Tests","VmxRefactoring","VmxProjectionSchemaAndQuarantineTests.cs");Assert.Contains("new Lane6VirtualToken(",r,StringComparison.Ordinal);Assert.Contains("new Lane6VirtualToken(",t,StringComparison.Ordinal);}
 private static string Read(params string[] p)=>File.ReadAllText(Path.Combine(new[]{Root()}.Concat(p).ToArray()));private static string Root(){DirectoryInfo? c=new(AppContext.BaseDirectory);while(c is not null){if(Directory.Exists(Path.Combine(c.FullName,"HybridCPU_ISE")))return c.FullName;c=c.Parent;}throw new DirectoryNotFoundException();}
}
