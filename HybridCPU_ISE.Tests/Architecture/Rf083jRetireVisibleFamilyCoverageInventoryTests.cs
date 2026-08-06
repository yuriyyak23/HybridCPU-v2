using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.3j closes the vocabulary/owner ledger gap without pretending that an
/// identity label has become a live per-family publication transport.
/// </summary>
public sealed class Rf083jRetireVisibleFamilyCoverageInventoryTests
{
    [Fact]
    public void ClosedVocabularyCoversTheAdrMinimumAndAllCurrentTypedWindowFamilies()
    {
        RetireVisibleEffectKind[] expected =
        [
            RetireVisibleEffectKind.RegisterWrite,
            RetireVisibleEffectKind.PcWrite,
            RetireVisibleEffectKind.CsrWrite,
            RetireVisibleEffectKind.VectorConfigWrite,
            RetireVisibleEffectKind.DeferredStoreCommit,
            RetireVisibleEffectKind.ScalarMemoryStoreCommit,
            RetireVisibleEffectKind.AtomicCommit,
            RetireVisibleEffectKind.SystemCommit,
            RetireVisibleEffectKind.VmxCommit,
            RetireVisibleEffectKind.TrapCommit,
            RetireVisibleEffectKind.PipelineEventPublication,
            RetireVisibleEffectKind.PredicateStateWrite,
            RetireVisibleEffectKind.VectorStreamDirty,
            RetireVisibleEffectKind.MatrixTileCommit,
            RetireVisibleEffectKind.AcceleratorCommit,
        ];

        Assert.Equal(expected, Enum.GetValues<RetireVisibleEffectKind>());

        string root = FindRepositoryRoot();
        string windowTypes = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.Types.cs");
        foreach (string windowKind in new[]
                 {
                     "DeferredStoreCommit", "Csr", "VectorConfig", "Atomic", "System", "Vmx",
                     "SerializingBoundary", "PipelineEvent", "ScalarMemoryStore", "PredicateState",
                     "VectorStreamDirty",
                 })
        {
            Assert.Contains($"{windowKind} =", windowTypes, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void OnlyApprovedScalarAluRegisterWriteHasALiveExactIdentityBridge()
    {
        string root = FindRepositoryRoot();
        string productionRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] identityFreezers = Directory.GetFiles(productionRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(
                "RetireVisibleEffectIdentity.Freeze(", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(productionRoot, path).Replace('\\', '/'))
            .ToArray();

        Assert.Equal(
            ["Pipeline/Scheduling/PostStageBIssuedAttempt.cs"],
            identityFreezers);

        string carrier = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "PostStageBIssuedAttempt.cs");
        Assert.Contains("candidate is not Core.ScalarALUMicroOp", Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs"), StringComparison.Ordinal);
        Assert.Contains("RetireVisibleEffectKind.RegisterWrite", carrier, StringComparison.Ordinal);
        Assert.DoesNotContain("RetireVisibleEffectKind.PcWrite", carrier, StringComparison.Ordinal);
        Assert.DoesNotContain("RetireVisibleEffectKind.DeferredStoreCommit", carrier, StringComparison.Ordinal);
        Assert.DoesNotContain("RetireVisibleEffectKind.MatrixTileCommit", carrier, StringComparison.Ordinal);
        Assert.DoesNotContain("RetireVisibleEffectKind.AcceleratorCommit", carrier, StringComparison.Ordinal);
    }

    [Fact]
    public void RetainedFamiliesRemainAtExistingSelectedRetireOwnersWithoutIdentityReconstruction()
    {
        string root = FindRepositoryRoot();
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        string matrixTile = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "MatrixTile", "MatrixTileMicroOps.cs");
        string accelerator = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "ExternalAccelerators", "Tokens", "AcceleratorTokenStore.cs");

        Assert.Contains("RetireCoordinator.Retire(retireBatch.RetireRecords)", retire, StringComparison.Ordinal);
        foreach (string windowKind in new[]
                 {
                     "DeferredStoreCommit", "Csr", "VectorConfig", "Atomic", "System", "PipelineEvent",
                     "Vmx", "ScalarMemoryStore", "PredicateState", "VectorStreamDirty",
                 })
        {
            Assert.Contains($"case RetireWindowEffectKind.{windowKind}", retire, StringComparison.Ordinal);
        }

        Assert.Contains("RetireCapturedResult", matrixTile, StringComparison.Ordinal);
        Assert.Contains("TryCommitPublication", accelerator, StringComparison.Ordinal);
        Assert.DoesNotContain("RetireVisibleEffectIdentity.Freeze(", retire, StringComparison.Ordinal);
        Assert.DoesNotContain("RetireVisibleEffectIdentity.Freeze(", matrixTile, StringComparison.Ordinal);
        Assert.DoesNotContain("RetireVisibleEffectIdentity.Freeze(", accelerator, StringComparison.Ordinal);
    }

    [Fact]
    public void PaperAndAdrDistinguishTemporaryRowsFromApprovedResidualExclusions()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base", "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");
        string adr = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "02_Authority", "ADR-009_VLIW_Retirement.md");

        Assert.Contains("RF-08.3j retire-visible-family exclusion ledger", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.3o scalar-load row instead has its explicit", paper, StringComparison.Ordinal);
        Assert.Contains("admissible at RF-08 exit", paper, StringComparison.Ordinal);
        Assert.Contains("must not claim complete", paper, StringComparison.Ordinal);
        Assert.Contains("scalar-load, `PcWrite` or `CsrWrite`", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.4d approved `PcWrite` C-C residual exclusion", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.4f CSR-A approved `CsrWrite` residual exclusion", paper, StringComparison.Ordinal);
        Assert.Contains("MatrixTileCommit", paper, StringComparison.Ordinal);
        Assert.Contains("AcceleratorCommit", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.3j family-exclusion ledger", adr, StringComparison.Ordinal);
        Assert.Contains("These approved residual exclusions are", adr, StringComparison.Ordinal);
        Assert.Contains("RF-08.4f `CsrWrite` CSR-A row", adr, StringComparison.Ordinal);
        Assert.Contains("No exclusion authorizes a synthetic identity", adr, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(segments).ToArray()));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
