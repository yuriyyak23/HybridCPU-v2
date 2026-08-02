using HybridCPU_ISE.Tests.IsaV5;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf082RetireRecordBatchPrevalidationTests
{
    [Fact]
    public void ValidBatch_PreservesOrderBackendLinkageAndFourVtIsolation()
    {
        var (_, coordinator, contexts, physical, _, commit) =
            CommitUnitTests.BuildFourVtForUnifiedState();
        RetireRecord[] records =
        [
            RetireRecord.RegisterWrite(0, 5, 10),
            RetireRecord.RegisterWrite(3, 5, 30),
            RetireRecord.RegisterWrite(0, 5, 20),
            RetireRecord.PcWrite(2, 0x4000),
        ];

        coordinator.Retire(records);

        Assert.Equal(20UL, contexts[0].CommittedRegs[5]);
        Assert.Equal(0UL, contexts[1].CommittedRegs[5]);
        Assert.Equal(0UL, contexts[2].CommittedRegs[5]);
        Assert.Equal(30UL, contexts[3].CommittedRegs[5]);
        Assert.Equal(0x4000UL, contexts[2].CommittedPc);
        Assert.Equal(20UL, physical.Read(CommitUnitTests.GetDedicatedPhysRegIdForTest(0, 5)));
        Assert.Equal(30UL, physical.Read(CommitUnitTests.GetDedicatedPhysRegIdForTest(3, 5)));
        Assert.Equal(
            CommitUnitTests.GetDedicatedPhysRegIdForTest(0, 5),
            commit.Lookup(0, 5));
        Assert.Equal(
            CommitUnitTests.GetDedicatedPhysRegIdForTest(3, 5),
            commit.Lookup(3, 5));
    }

    [Fact]
    public void InvalidLaterVt_PrevalidationRejectsBeforeOlderRegisterOrPcMutation()
    {
        var (_, coordinator, contexts, physical, _, commit) =
            CommitUnitTests.BuildFourVtForUnifiedState();
        int phys = CommitUnitTests.GetDedicatedPhysRegIdForTest(0, 7);
        int committedPhys = commit.Lookup(0, 7);
        RetireRecord[] records =
        [
            RetireRecord.RegisterWrite(0, 7, 0xAA),
            RetireRecord.PcWrite(1, 0x8000),
            RetireRecord.RegisterWrite(SmtWays, 8, 0xBB),
        ];

        Assert.Throws<ArgumentOutOfRangeException>(() => coordinator.Retire(records));

        Assert.Equal(0UL, contexts[0].CommittedRegs[7]);
        Assert.Equal(0UL, contexts[1].CommittedPc);
        Assert.Equal(0UL, physical.Read(phys));
        Assert.Equal(committedPhys, commit.Lookup(0, 7));
    }

    [Fact]
    public void InvalidLaterRegister_PrevalidationRejectsBeforeAnyVtMutation()
    {
        var (_, coordinator, contexts, physical, _, _) =
            CommitUnitTests.BuildFourVtForUnifiedState();
        int phys = CommitUnitTests.GetDedicatedPhysRegIdForTest(2, 9);
        RetireRecord[] records =
        [
            RetireRecord.RegisterWrite(2, 9, 0x11),
            RetireRecord.RegisterWrite(2, RenameMap.ArchRegs, 0x22),
        ];

        Assert.Throws<ArgumentOutOfRangeException>(() => coordinator.Retire(records));

        Assert.Equal(0UL, contexts[2].CommittedRegs[9]);
        Assert.Equal(0UL, physical.Read(phys));
        Assert.All(
            Enumerable.Range(0, SmtWays).Where(vt => vt != 2),
            vt => Assert.Equal(0UL, contexts[vt].CommittedRegs[9]));
    }

    [Fact]
    public void X0NoOpAndSingleRecordSemanticsRemainUnchanged()
    {
        var (_, coordinator, contexts, physical, _, commit) =
            CommitUnitTests.BuildFourVtForUnifiedState();

        coordinator.Retire(
        [
            RetireRecord.RegisterWrite(1, 0, 999),
            RetireRecord.RegisterWrite(1, 4, 123),
        ]);

        Assert.Equal(0UL, contexts[1].CommittedRegs[0]);
        Assert.Equal(0UL, physical.Read(0));
        Assert.Equal(0, commit.Lookup(1, 0));
        Assert.Equal(123UL, contexts[1].CommittedRegs[4]);
        Assert.Equal(
            123UL,
            physical.Read(CommitUnitTests.GetDedicatedPhysRegIdForTest(1, 4)));
    }

    [Fact]
    public void SourceHasCompleteValidationPassBeforeFirstApplyAndKeepsOwners()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "Registers", "Retire",
            "RetireCoordinator.cs"));
        int batch = source.IndexOf(
            "public void Retire(ReadOnlySpan<RetireRecord> records)",
            StringComparison.Ordinal);
        int firstValidation = source.IndexOf("ValidateRecord(records[i])", batch, StringComparison.Ordinal);
        int firstApply = source.IndexOf("ApplyRetireRecord(records[i])", batch, StringComparison.Ordinal);

        Assert.True(batch >= 0 && firstValidation > batch && firstApply > firstValidation);
        Assert.DoesNotContain("ApplyRetireRecord", MemberBody(source, "private void ValidateRecord"), StringComparison.Ordinal);
        Assert.Contains("_archRenameMap.Lookup", source, StringComparison.Ordinal);
        Assert.Contains("_physicalRegisters.Write", source, StringComparison.Ordinal);
        Assert.Contains("_archCommitMap.Commit", source, StringComparison.Ordinal);
    }

    private static string MemberBody(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, signature);
        int open = source.IndexOf('{', start);
        int depth = 0;
        for (int i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            if (source[i] == '}' && --depth == 0) return source[open..(i + 1)];
        }

        throw new InvalidOperationException($"Unterminated member {signature}.");
    }

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

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
