using System.Text.RegularExpressions;
namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1141ReadonlyStructMemberRefThisHardeningTests
{
    [Fact]
    public void ExceptionStatusAliasesNoLongerUseRefThis()
    {
        string source = VectorConfig();
        Assert.Equal(2, Regex.Matches(source, @"ref VectorExceptionStatus status = ref ExceptionStatus;").Count);
        Assert.DoesNotContain("ref this.ExceptionStatus", source);
    }
    [Fact]
    public void MaskAndPriorityMutationOrderRemainsFrozen()
    {
        string s = VectorConfig();
        Order(s, "byte mask = (byte)(rs1Value & 0x1F);", "ref VectorExceptionStatus status = ref ExceptionStatus;", "status.SetMask(mask);");
        int p = s.IndexOf("private void Exec_VSETVEXCPPRI", StringComparison.Ordinal);
        string pri = s[p..]; Order(pri, "ref VectorExceptionStatus status = ref ExceptionStatus;", "for (int i = 0; i < 5; i++)", "status.SetPriority(i, pri);");
    }
    [Fact]
    public void ProductionHasNoRefThisSurface()
    {
        string prod = Sources(Path.Combine(Root(), "HybridCPU_ISE"));
        Assert.Empty(Regex.Matches(prod, @"\bref\s+this(?:\s*[,\)]|\.)"));
        Assert.Equal(67, Regex.Matches(prod, @"partial\s+class\s+CPU_Core").Count);
    }
    [Fact]
    public void EvidenceClosesOnlyReadonlyMemberAliases()
    {
        string root = Root(); string l = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md"); string e = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11", "rf11.41-readonly-struct-member-ref-this-hardening.md");
        Assert.Contains("RF-11.41 readonly struct-member ref-this hardening", l); Assert.Contains("no state declaration", e, StringComparison.OrdinalIgnoreCase); Assert.Contains("--minimal-logs", e); Assert.Contains("RF-11.42", l);
    }
    static string VectorConfig() => Read(Root(), "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "State", "Architectural", "CPU_Core.VectorConfig.cs");
    static void Order(string s, params string[] m) { int p = -1; foreach (string x in m) { int n = s.IndexOf(x, p + 1, StringComparison.Ordinal); Assert.True(n > p, x); p = n; } }
    static string Sources(string p) => string.Join('\n', Directory.GetFiles(p, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\")).Select(File.ReadAllText));
    static string Read(string r, params string[] p) => File.ReadAllText(p.Aggregate(r, Path.Combine));
    static string Root() { string? c = AppContext.BaseDirectory; while (c != null) { if (File.Exists(Path.Combine(c, "HybridCPU v2.slnx"))) return c; c = Directory.GetParent(c)?.FullName; } throw new DirectoryNotFoundException(); }
}
