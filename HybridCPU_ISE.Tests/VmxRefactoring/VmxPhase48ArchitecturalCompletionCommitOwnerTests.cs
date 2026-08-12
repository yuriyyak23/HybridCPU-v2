using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase48ArchitecturalCompletionCommitOwnerTests
{
    [Fact]
    public void MachineStatus_KeepsP48AProvenanceClosedWhileP48BDoesNotOpenVmRead()
    {
        string repositoryRoot =
            VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        using JsonDocument status = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repositoryRoot,
            "HybridCPU_ISE", "docs", "ref2", "VirtualizationActivationPlan",
            "VirtualizationActivationStatusV1.json")));
        JsonElement root = status.RootElement;
        JsonElement phase48 = root.GetProperty(
            "Phase48NeutralArchitecturalCompletionFoundation");
        Assert.Equal("P48AAndP48BProvenanceClosedFoundationOnly",
            phase48.GetProperty("State").GetString());
        Assert.Equal("ArchitecturalCompletionCommitOwner",
            phase48.GetProperty("CommitOwner").GetString());
        Assert.Equal("ClosedGreenSubjectAndLaterNonSelfReferentialEvidence",
            phase48.GetProperty("P48A").GetString());
        Assert.Equal("NotAuthorized",
            phase48.GetProperty("VmReadDecisionMaterialization").GetString());
        Assert.Equal("NotAuthorized",
            phase48.GetProperty("VmReadProductionComposition").GetString());
        Assert.Equal("ClosedGreenSubjectAndLaterNonSelfReferentialEvidence",
            phase48.GetProperty("P48B").GetString());

        JsonElement completionCandidate = root.GetProperty(
            "VmReadCurrentCompletionScalarDeliveryCandidate");
        Assert.Equal("NotMaterialized",
            completionCandidate.GetProperty("SpecV2").GetString());
        Assert.Equal("NotMaterialized",
            completionCandidate.GetProperty("AcceptanceRecordV2").GetString());
        Assert.Equal("NotAuthorized",
            completionCandidate.GetProperty("ProductionImplementation").GetString());
    }

    [Fact]
    public void P48AEvidence_IsLaterNonSelfReferentialAndHashesCleanSubjectBytes()
    {
        const string subject = "03ecefb3c155c161b9b7516bfa2fe2609628693c";
        const string tree = "029b74560a014bc50b460e920444eeb6cbc4601d";
        string repositoryRoot =
            VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        using JsonDocument evidence = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repositoryRoot,
            "HybridCPU_ISE", "docs", "ref2", "VirtualizationActivationPlan",
            "evidence",
            "2026-08-12-phase48a-neutral-architectural-completion-commit-clean-evidence.json")));
        JsonElement root = evidence.RootElement;
        Assert.True(root.GetProperty("non_self_referential").GetBoolean());
        Assert.False(root.GetProperty("runtime_authority").GetBoolean());
        Assert.Equal(subject,
            root.GetProperty("implementation_subject").GetProperty("commit_sha").GetString());
        Assert.Equal(tree,
            root.GetProperty("implementation_subject").GetProperty("tree_sha").GetString());
        Assert.Equal(tree, GitText(repositoryRoot, "rev-parse", $"{subject}^{{tree}}"));

        foreach (JsonProperty source in root
            .GetProperty("source_hashes_sha256_clean_worktree_bytes")
            .EnumerateObject())
        {
            byte[] bytes = GitBytes(
                repositoryRoot,
                "cat-file",
                "--filters",
                $"--path={source.Name}",
                $"{subject}:{source.Name}");
            string actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            Assert.Equal(source.Value.GetString(), actual);
        }

        Assert.False(root.GetProperty("next_field_group_opened").GetBoolean());
        Assert.True(root.GetProperty("p48b")
            .GetProperty("authorized_after_this_provenance_closure").GetBoolean());
        Assert.False(root.GetProperty("p48b").GetProperty("implemented").GetBoolean());
    }

    [Fact]
    public void Receipt_BindsExactIdentityDigestOrderCommitAndRestoreGeneration()
    {
        (ArchitecturalCompletionCommitOwner owner,
            ArchitecturalCompletionCommitOwner.ProducerRegistration producer) = CreateOwner();

        ArchitecturalCompletionCommitResult first = owner.CommitAtCanonicalRetireBoundary(
            producer,
            Candidate(attempt: 11, eventId: 101));
        ArchitecturalCompletionCommitResult second = owner.CommitAtCanonicalRetireBoundary(
            producer,
            Candidate(attempt: 12, eventId: 102));

        Assert.True(first.IsCommitted);
        Assert.True(second.IsCommitted);
        ArchitecturalCompletionReceiptBinding a = first.Receipt!.Binding;
        ArchitecturalCompletionReceiptBinding b = second.Receipt!.Binding;
        Assert.NotEqual(0UL, a.CompletionIdentity);
        Assert.Equal(producer.OwnerIdentity, a.ProducerOwnerIdentity);
        Assert.Equal(producer.OwnerEpoch, a.ProducerOwnerEpoch);
        Assert.Equal(17UL, a.DomainId);
        Assert.Equal(23, a.ContextId);
        Assert.Equal(2, a.VirtualThreadId);
        Assert.Equal(11UL, a.AttemptId);
        Assert.Equal(101UL, a.EventId);
        Assert.Equal(NeutralArchitecturalCompletionClass.TrapEntry, a.CompletionClass);
        Assert.Equal(64, a.CompletionDigest.Length);
        Assert.NotEqual(0UL, a.RestoreGeneration);
        Assert.True(b.CanonicalOrderSequence > a.CanonicalOrderSequence);
        Assert.True(b.CommitSequence > a.CommitSequence);
        Assert.True(owner.ValidateLiveReceipt(first.Receipt, a));
    }

    [Fact]
    public void ForgedUnregisteredStaleAndCrossOwnerEvidence_IsDenied()
    {
        (ArchitecturalCompletionCommitOwner owner,
            ArchitecturalCompletionCommitOwner.ProducerRegistration producer) = CreateOwner();
        var fakeRegistration = new ArchitecturalCompletionCommitOwner.ProducerRegistration(
            owner,
            producer.OwnerIdentity,
            producer.OwnerEpoch,
            producer.Policy);
        ArchitecturalCompletionCommitResult unregistered =
            owner.CommitAtCanonicalRetireBoundary(fakeRegistration, Candidate());
        Assert.Equal(
            ArchitecturalCompletionCommitDecision.DeniedUnregisteredProducer,
            unregistered.Decision);

        ArchitecturalCompletionCommitResult committed =
            owner.CommitAtCanonicalRetireBoundary(producer, Candidate());
        Assert.True(committed.IsCommitted);
        ArchitecturalCompletionReceiptBinding binding = committed.Receipt!.Binding;
        var forged = new ArchitecturalCompletionCommitOwner.ArchitecturalCompletionCommitReceipt(
            owner,
            binding,
            new string('0', 64));
        Assert.False(owner.ValidateLiveReceipt(forged, binding));
        var malformed = new ArchitecturalCompletionCommitOwner.ArchitecturalCompletionCommitReceipt(
            owner,
            binding,
            "not-a-seal");
        Assert.False(owner.ValidateLiveReceipt(malformed, binding));

        (ArchitecturalCompletionCommitOwner other, _) = CreateOwner();
        Assert.False(other.ValidateLiveReceipt(committed.Receipt, binding));

        owner.InvalidateAfterRestore();
        Assert.False(owner.ValidateLiveReceipt(committed.Receipt, binding));
        Assert.True(owner.CurrentRestoreGeneration > binding.RestoreGeneration);
    }

    [Fact]
    public void Receipt_RejectsEveryCrossBindingAndConsumesExactlyOnce()
    {
        (ArchitecturalCompletionCommitOwner owner,
            ArchitecturalCompletionCommitOwner.ProducerRegistration producer) = CreateOwner();
        ArchitecturalCompletionCommitResult committed =
            owner.CommitAtCanonicalRetireBoundary(producer, Candidate());
        ArchitecturalCompletionCommitOwner.ArchitecturalCompletionCommitReceipt receipt =
            committed.Receipt!;
        ArchitecturalCompletionReceiptBinding binding = receipt.Binding;

        ArchitecturalCompletionReceiptBinding[] mismatches =
        [
            binding with { CompletionIdentity = binding.CompletionIdentity + 1 },
            binding with { ProducerOwnerIdentity = binding.ProducerOwnerIdentity + 1 },
            binding with { ProducerOwnerEpoch = binding.ProducerOwnerEpoch + 1 },
            binding with { DomainId = binding.DomainId + 1 },
            binding with { ContextId = binding.ContextId + 1 },
            binding with { VirtualThreadId = binding.VirtualThreadId + 1 },
            binding with { AttemptId = binding.AttemptId + 1 },
            binding with { EventId = binding.EventId + 1 },
            binding with { CompletionClass = NeutralArchitecturalCompletionClass.SystemEvent },
            binding with { CompletionDigest = new string('a', 64) },
            binding with { CanonicalOrderSequence = binding.CanonicalOrderSequence + 1 },
            binding with { CommitSequence = binding.CommitSequence + 1 },
            binding with { RestoreGeneration = binding.RestoreGeneration + 1 },
        ];
        Assert.All(mismatches, mismatch =>
            Assert.False(owner.ValidateLiveReceipt(receipt, mismatch)));

        Assert.True(owner.TryConsumeReceipt(receipt, binding));
        Assert.False(owner.TryConsumeReceipt(receipt, binding));
        Assert.False(owner.ValidateLiveReceipt(receipt, binding));
    }

    [Fact]
    public void DuplicateReplayAndBroadClassOrSemanticAcceptance_IsDenied()
    {
        (ArchitecturalCompletionCommitOwner owner,
            ArchitecturalCompletionCommitOwner.ProducerRegistration producer) = CreateOwner();
        ArchitecturalCompletionCandidate exact = Candidate();
        Assert.True(owner.CommitAtCanonicalRetireBoundary(producer, exact).IsCommitted);
        Assert.Equal(
            ArchitecturalCompletionCommitDecision.DeniedDuplicateOrReplay,
            owner.CommitAtCanonicalRetireBoundary(producer, exact).Decision);
        Assert.Equal(
            ArchitecturalCompletionCommitDecision.DeniedDuplicateOrReplay,
            owner.CommitAtCanonicalRetireBoundary(
                producer,
                exact with
                {
                    Facts = Facts() with { Reason = NeutralScalarFact.Present(99) }
                }).Decision);

        Assert.Equal(
            ArchitecturalCompletionCommitDecision.DeniedProducerPolicy,
            owner.CommitAtCanonicalRetireBoundary(
                producer,
                Candidate(attempt: 12, eventId: 102) with
                {
                    Facts = Facts() with
                    {
                        CompletionClass = NeutralArchitecturalCompletionClass.SystemEvent
                    }
                }).Decision);
        Assert.Equal(
            ArchitecturalCompletionCommitDecision.DeniedProducerPolicy,
            owner.CommitAtCanonicalRetireBoundary(
                producer,
                Candidate(attempt: 13, eventId: 103) with
                {
                    Facts = Facts() with
                    {
                        Qualification = NeutralScalarFact.Present(0)
                    }
                }).Decision);
        Assert.Equal(
            ArchitecturalCompletionCommitDecision.DeniedProducerPolicy,
            owner.CommitAtCanonicalRetireBoundary(
                producer,
                Candidate(attempt: 14, eventId: 104) with
                {
                    Facts = Facts() with
                    {
                        FaultAddress = NeutralAddressFact.Present(
                            0,
                            NeutralFaultAddressSemantic.GuestPhysicalAddress)
                    }
                }).Decision);
        Assert.Equal(
            ArchitecturalCompletionCommitDecision.DeniedProducerPolicy,
            owner.CommitAtCanonicalRetireBoundary(
                producer,
                Candidate(attempt: 15, eventId: 105) with
                {
                    Facts = Facts() with
                    {
                        FaultAuxiliary = NeutralAuxiliaryFact.Present(
                            0,
                            NeutralFaultAuxiliarySemantic.SecondStageTranslationViolation)
                    }
                }).Decision);

        ArchitecturalCompletionCommitResult legalZero = owner.CommitAtCanonicalRetireBoundary(
            producer,
            Candidate(attempt: 16, eventId: 106) with
            {
                Facts = Facts() with
                {
                    FaultAddress = NeutralAddressFact.Present(
                        0,
                        NeutralFaultAddressSemantic.VirtualAddress)
                }
            });
        Assert.True(legalZero.IsCommitted);
    }

    [Fact]
    public void MissingDomainContextVtAttemptOrEventIdentity_IsDenied()
    {
        (ArchitecturalCompletionCommitOwner owner,
            ArchitecturalCompletionCommitOwner.ProducerRegistration producer) = CreateOwner();
        ArchitecturalCompletionCandidate baseline = Candidate();
        ArchitecturalCompletionCandidate[] invalid =
        [
            baseline with { DomainId = 0 },
            baseline with { ContextId = 0 },
            baseline with { VirtualThreadId = -1 },
            baseline with { VirtualThreadId = Processor.CPU_Core.SmtWays },
            baseline with { AttemptId = 0 },
            baseline with { EventId = 0 },
        ];

        Assert.All(invalid, candidate => Assert.Equal(
            ArchitecturalCompletionCommitDecision.DeniedMissingIdentity,
            owner.CommitAtCanonicalRetireBoundary(producer, candidate).Decision));
    }

    [Fact]
    public void DuplicateCommitAndRestoreRaces_AreLinearized()
    {
        for (int repetition = 0; repetition < 20; repetition++)
        {
            (ArchitecturalCompletionCommitOwner owner,
                ArchitecturalCompletionCommitOwner.ProducerRegistration producer) = CreateOwner();
            ArchitecturalCompletionCandidate duplicate = Candidate();
            int committed = 0;
            Parallel.For(0, 64, _ =>
            {
                if (owner.CommitAtCanonicalRetireBoundary(producer, duplicate).IsCommitted)
                    Interlocked.Increment(ref committed);
            });
            Assert.Equal(1, committed);

            ArchitecturalCompletionCommitResult raced = default;
            Parallel.Invoke(
                () => raced = owner.CommitAtCanonicalRetireBoundary(
                    producer,
                    Candidate(attempt: 20, eventId: 200)),
                owner.InvalidateAfterRestore);
            if (raced.IsCommitted)
            {
                ArchitecturalCompletionReceiptBinding binding = raced.Receipt!.Binding;
                bool live = owner.ValidateLiveReceipt(raced.Receipt, binding);
                Assert.True(!live || binding.RestoreGeneration == owner.CurrentRestoreGeneration);
            }
        }
    }

    [Fact]
    public void ProductionSeam_IsAfterLateEffectsAndBeforeVisibilityCertificate()
    {
        string repositoryRoot =
            VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string retire = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs"));
        int finalize = retire.IndexOf(
            "private void FinalizeWriteBackRetireWindow(",
            StringComparison.Ordinal);
        int late = retire.IndexOf(
            "ApplyRetireBatchLateEffectsAndRedirect(",
            finalize,
            StringComparison.Ordinal);
        int commit = retire.IndexOf(
            "CommitArchitecturalCompletionAtCanonicalRetireBoundary(ref retireBatch);",
            late,
            StringComparison.Ordinal);
        int certificate = retire.IndexOf(
            "PublishRetireVisibilityContourCertificate(",
            commit,
            StringComparison.Ordinal);
        Assert.True(finalize >= 0 && finalize < late && late < commit && commit < certificate);

        string coordinator = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "Registers", "Retire",
            "RetireCoordinator.cs"));
        Assert.DoesNotContain("Completion", coordinator, StringComparison.Ordinal);

        string owner = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Completion", "Commit",
            "ArchitecturalCompletionCommitOwner.cs"));
        Assert.DoesNotContain("CompletionRecord", owner, StringComparison.Ordinal);
        Assert.DoesNotContain("TrapCompletionPublicationFence", owner, StringComparison.Ordinal);
        Assert.DoesNotContain("DomainHypercallCompletionOwner", owner, StringComparison.Ordinal);
        Assert.DoesNotContain("VmReadScalar", owner, StringComparison.Ordinal);
    }

    private static (
        ArchitecturalCompletionCommitOwner Owner,
        ArchitecturalCompletionCommitOwner.ProducerRegistration Producer) CreateOwner()
    {
        var owner = new ArchitecturalCompletionCommitOwner(
            new VirtualizationRestoreGenerationOwner(),
            new DomainCompletionObservationOwner(
                new CompletionGenerationAuthority()));
        ArchitecturalCompletionCommitOwner.ProducerRegistration producer =
            owner.RegisterProducer(new ArchitecturalCompletionProducerPolicy(
                "CanonicalPipelineTrapEntryProducer",
                NeutralArchitecturalCompletionClass.TrapEntry,
                RequiresReason: true,
                AllowsQualification: false,
                NeutralFaultAddressSemantic.VirtualAddress,
                NeutralFaultAuxiliarySemantic.None));
        return (owner, producer);
    }

    private static ArchitecturalCompletionCandidate Candidate(
        ulong attempt = 11,
        ulong eventId = 101) =>
        new(17, 23, 2, attempt, eventId, Facts());

    private static NeutralArchitecturalCompletionFacts Facts() =>
        new(
            NeutralArchitecturalCompletionClass.TrapEntry,
            NeutralScalarFact.Present(2),
            NeutralScalarFact.Absent,
            NeutralAddressFact.Absent,
            NeutralAuxiliaryFact.Absent);

    private static string GitText(string workingDirectory, params string[] arguments) =>
        System.Text.Encoding.UTF8.GetString(GitBytes(workingDirectory, arguments)).TrimEnd();

    private static byte[] GitBytes(string workingDirectory, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        foreach (string argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start());
        using var output = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(output);
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
        return output.ToArray();
    }
}
