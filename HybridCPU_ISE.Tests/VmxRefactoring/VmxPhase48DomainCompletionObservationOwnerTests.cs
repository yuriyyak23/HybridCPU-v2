using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase48DomainCompletionObservationOwnerTests
{
    [Fact]
    public void MachineStatus_ClosesOnlyP48BImplementationAndKeepsProjectionBlocked()
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
        Assert.Equal(
            "P48AAndP48BProvenanceClosedFoundationOnly",
            phase48.GetProperty("State").GetString());
        Assert.Equal(
            "DomainCompletionObservationOwnerNeutralReadOnlyDownstreamOfCommit",
            phase48.GetProperty("ObservationOwner").GetString());
        Assert.Equal(
            "RuntimeOwnedNonZeroMonotonicOnCommitClearRestoreRebindOwnerReplacement",
            phase48.GetProperty("CompletionGeneration").GetString());
        Assert.Equal("NotAuthorized",
            phase48.GetProperty("VmReadDecisionMaterialization").GetString());
        Assert.Equal("NotAuthorized",
            phase48.GetProperty("VmReadProductionComposition").GetString());

        JsonElement candidate = root.GetProperty(
            "VmReadCurrentCompletionScalarDeliveryCandidate");
        Assert.Equal("NotMaterialized", candidate.GetProperty("SpecV2").GetString());
        Assert.Equal("NotMaterialized",
            candidate.GetProperty("AcceptanceRecordV2").GetString());
        Assert.Equal("NotAuthorized",
            candidate.GetProperty("ProductionImplementation").GetString());
        Assert.Equal("None", candidate.GetProperty("RuntimeAuthority").GetString());
        Assert.Equal("NoneUntilSeparateNeutralCpuTranslationFaultCompletionProducerAuthorization",
            root.GetProperty("NextCandidatePool").GetString());
    }

    [Fact]
    public void P48BEvidence_IsLaterNonSelfReferentialAndHashesCleanSubjectBytes()
    {
        const string subject = "a751264e73ff73cf924b5559e72b7a1582c25cf9";
        const string tree = "988b58397195939118f4c55bc3de41e67e9bc742";
        string repositoryRoot =
            VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        using JsonDocument evidence = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repositoryRoot,
            "HybridCPU_ISE", "docs", "ref2", "VirtualizationActivationPlan",
            "evidence",
            "2026-08-12-phase48b-neutral-current-completion-observation-clean-evidence.json")));
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
        Assert.Contains("not yet authorized", root.GetProperty("next_pool").GetString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CommitAndObservationInstall_ShareTheReceiptPublicationOperation()
    {
        Fixture fixture = CreateFixture();
        ArchitecturalCompletionCommitResult committed = fixture.Owner
            .CommitAtCanonicalRetireBoundary(
                fixture.Producer,
                Candidate(domain: 17, context: 23, vt: 2, attempt: 11, eventId: 101));

        Assert.True(committed.IsCommitted);
        CompletionObservationResult observed = fixture.Observation.Observe(
            new CompletionObservationScope(17, 23, 2));
        Assert.True(observed.IsObserved);
        NeutralCompletionObservationSnapshot snapshot = observed.Snapshot!.Value;
        Assert.Equal(committed.Receipt!.Binding.CompletionIdentity, snapshot.CompletionIdentity);
        Assert.Equal(committed.Receipt.Binding.CommitSequence, snapshot.CommitSequence);
        Assert.Equal(committed.Receipt.Binding.CanonicalOrderSequence,
            snapshot.CanonicalOrderSequence);
        Assert.Equal(committed.Receipt.Binding.RestoreGeneration,
            snapshot.RestoreGeneration);
        Assert.NotEqual(0UL, snapshot.CompletionGeneration);
        Assert.Equal(CompletionObservationMigrationClass.RecomputedCompletion,
            fixture.Observation.MigrationClass);
    }

    [Fact]
    public void TwoDomainsRemainIndependentAndCrossScopeIsDenied()
    {
        Fixture fixture = CreateFixture();
        Assert.True(fixture.Owner.CommitAtCanonicalRetireBoundary(
            fixture.Producer,
            Candidate(17, 23, 1, 11, 101)).IsCommitted);
        Assert.True(fixture.Owner.CommitAtCanonicalRetireBoundary(
            fixture.Producer,
            Candidate(18, 24, 2, 12, 102)).IsCommitted);

        NeutralCompletionObservationSnapshot first = fixture.Observation.Observe(
            new CompletionObservationScope(17, 23, 1)).Snapshot!.Value;
        NeutralCompletionObservationSnapshot second = fixture.Observation.Observe(
            new CompletionObservationScope(18, 24, 2)).Snapshot!.Value;
        Assert.Equal(17UL, first.Scope.DomainId);
        Assert.Equal(18UL, second.Scope.DomainId);
        Assert.NotEqual(first.CompletionIdentity, second.CompletionIdentity);
        Assert.True(second.CompletionGeneration > first.CompletionGeneration);

        Assert.Equal(CompletionObservationDecision.DeniedAbsent,
            fixture.Observation.Observe(new CompletionObservationScope(17, 24, 1)).Decision);
        Assert.Equal(CompletionObservationDecision.DeniedAbsent,
            fixture.Observation.Observe(new CompletionObservationScope(17, 23, 2)).Decision);
        Assert.Equal(CompletionObservationDecision.DeniedAbsent,
            fixture.Observation.Observe(new CompletionObservationScope(18, 24, 0)).Decision);
        Assert.Equal(CompletionObservationDecision.DeniedInvalidScope,
            fixture.Observation.Observe(new CompletionObservationScope(0, 23, 1)).Decision);
    }

    [Fact]
    public void PresenceContract_DistinguishesLegalZeroFromAbsent()
    {
        Fixture fixture = CreateFixture();
        Assert.True(fixture.Owner.CommitAtCanonicalRetireBoundary(
            fixture.Producer,
            Candidate(17, 23, 2, 11, 101) with
            {
                Facts = TrapFacts(reason: 0, addressPresent: true, address: 0)
            }).IsCommitted);
        var scope = new CompletionObservationScope(17, 23, 2);

        NeutralCompletionFieldResult reason = fixture.Observation.ReadField(
            scope,
            NeutralCompletionObservationField.Reason);
        Assert.True(reason.IsPresent);
        Assert.Equal(0UL, reason.Value);

        NeutralCompletionFieldResult qualification = fixture.Observation.ReadField(
            scope,
            NeutralCompletionObservationField.Qualification);
        Assert.Equal(NeutralCompletionFieldDecision.DeniedAbsent, qualification.Decision);
        Assert.False(qualification.IsPresent);

        NeutralCompletionObservationSnapshot snapshot =
            fixture.Observation.Observe(scope).Snapshot!.Value;
        Assert.True(snapshot.Facts.Reason.IsPresent);
        Assert.Equal(0UL, snapshot.Facts.Reason.Value);
        Assert.False(snapshot.Facts.Qualification.IsPresent);
        Assert.True(snapshot.Facts.FaultAddress.IsPresent);
        Assert.Equal(0UL, snapshot.Facts.FaultAddress.Value);
    }

    [Fact]
    public void SemanticMismatchAndAbsentFacts_AreExplicitDenials()
    {
        Fixture fixture = CreateFixture();
        Assert.True(fixture.Owner.CommitAtCanonicalRetireBoundary(
            fixture.Producer,
            Candidate(17, 23, 2, 11, 101) with
            {
                Facts = TrapFacts(reason: 2, addressPresent: true, address: 0x1000)
            }).IsCommitted);
        var scope = new CompletionObservationScope(17, 23, 2);

        Assert.Equal(NeutralCompletionFieldDecision.DeniedSemanticMismatch,
            fixture.Observation.ReadField(
                scope,
                NeutralCompletionObservationField.GuestPhysicalAddress).Decision);
        Assert.Equal(NeutralCompletionFieldDecision.DeniedAbsent,
            fixture.Observation.ReadField(
                scope,
                NeutralCompletionObservationField.SecondStageTranslationViolationAuxiliary)
                .Decision);
        Assert.Equal(NeutralCompletionFieldDecision.DeniedAbsent,
            fixture.Observation.ReadField(
                new CompletionObservationScope(99, 23, 2),
                NeutralCompletionObservationField.Reason).Decision);
    }

    [Fact]
    public void ExactTranslationProducer_AllowsOnlyAdmittedGuestPhysicalAndSecondStageFacts()
    {
        Fixture fixture = CreateFixture(
            new ArchitecturalCompletionProducerPolicy(
                "CanonicalTranslationFaultProducer",
                NeutralArchitecturalCompletionClass.TranslationFault,
                RequiresReason: true,
                AllowsQualification: true,
                NeutralFaultAddressSemantic.GuestPhysicalAddress,
                NeutralFaultAuxiliarySemantic.SecondStageTranslationViolation));
        NeutralArchitecturalCompletionFacts facts = new(
            NeutralArchitecturalCompletionClass.TranslationFault,
            NeutralScalarFact.Present(0),
            NeutralScalarFact.Present(0),
            NeutralAddressFact.Present(0, NeutralFaultAddressSemantic.GuestPhysicalAddress),
            NeutralAuxiliaryFact.Present(
                0,
                NeutralFaultAuxiliarySemantic.SecondStageTranslationViolation));
        Assert.True(fixture.Owner.CommitAtCanonicalRetireBoundary(
            fixture.Producer,
            Candidate(31, 41, 0, 21, 201) with { Facts = facts }).IsCommitted);
        var scope = new CompletionObservationScope(31, 41, 0);

        Assert.All(new[]
        {
            NeutralCompletionObservationField.Reason,
            NeutralCompletionObservationField.Qualification,
            NeutralCompletionObservationField.GuestPhysicalAddress,
            NeutralCompletionObservationField.SecondStageTranslationViolationAuxiliary,
        }, field =>
        {
            NeutralCompletionFieldResult result = fixture.Observation.ReadField(scope, field);
            Assert.True(result.IsPresent);
            Assert.Equal(0UL, result.Value);
        });
    }

    [Fact]
    public void GenerationAdvancesOnCommitClearRestoreRebindAndOwnerReplacement()
    {
        Fixture fixture = CreateFixture();
        var scope = new CompletionObservationScope(17, 23, 2);
        ulong initial = fixture.Observation.CurrentGeneration;
        Assert.True(fixture.Owner.CommitAtCanonicalRetireBoundary(
            fixture.Producer,
            Candidate(17, 23, 2, 11, 101)).IsCommitted);
        ulong committed = fixture.Observation.CurrentGeneration;
        Assert.True(committed > initial);

        fixture.Owner.ClearObservation(scope);
        ulong cleared = fixture.Observation.CurrentGeneration;
        Assert.True(cleared > committed);
        Assert.Equal(CompletionObservationDecision.DeniedAbsent,
            fixture.Observation.Observe(scope).Decision);

        Assert.True(fixture.Owner.CommitAtCanonicalRetireBoundary(
            fixture.Producer,
            Candidate(17, 23, 2, 12, 102)).IsCommitted);
        fixture.Owner.RebindObservation(scope);
        ulong rebound = fixture.Observation.CurrentGeneration;
        Assert.True(rebound > cleared);
        Assert.Equal(CompletionObservationDecision.DeniedAbsent,
            fixture.Observation.Observe(scope).Decision);

        Assert.True(fixture.Owner.CommitAtCanonicalRetireBoundary(
            fixture.Producer,
            Candidate(17, 23, 2, 13, 103)).IsCommitted);
        fixture.Owner.InvalidateAfterRestore();
        ulong restored = fixture.Observation.CurrentGeneration;
        Assert.True(restored > rebound);
        Assert.Equal(CompletionObservationDecision.DeniedAbsent,
            fixture.Observation.Observe(scope).Decision);

        DomainCompletionObservationOwner old = fixture.Observation;
        DomainCompletionObservationOwner replacement = fixture.Owner.ReplaceObservationOwner();
        Assert.True(replacement.CurrentGeneration > restored);
        Assert.Equal(CompletionObservationDecision.DeniedInactiveOwner,
            old.Observe(scope).Decision);
        Assert.Equal(CompletionObservationDecision.DeniedAbsent,
            replacement.Observe(scope).Decision);
    }

    [Fact]
    public void CallerCannotForgeCommitInstallationCapability()
    {
        Fixture fixture = CreateFixture();
        ArchitecturalCompletionCommitResult committed = fixture.Owner
            .CommitAtCanonicalRetireBoundary(
                fixture.Producer,
                Candidate(17, 23, 2, 11, 101));
        var forged = new DomainCompletionObservationOwner.CommitInstaller(
            fixture.Observation,
            fixture.Owner);
        Assert.Throws<InvalidOperationException>(() =>
            fixture.Observation.InstallCommittedCompletion(
                forged,
                fixture.Owner,
                committed.Receipt!.Binding,
                TrapFacts()));
    }

    [Fact]
    public void CommitClearRestoreRebindAndReplacementRaces_AreLinearized()
    {
        for (int repetition = 0; repetition < 20; repetition++)
        {
            Fixture fixture = CreateFixture();
            var scope = new CompletionObservationScope(17, 23, 2);
            Parallel.Invoke(
                () => fixture.Owner.CommitAtCanonicalRetireBoundary(
                    fixture.Producer,
                    Candidate(17, 23, 2, 11, 101)),
                () => fixture.Owner.ClearObservation(scope));
            AssertSnapshotIsAbsentOrSelfConsistent(fixture.Owner.ObservationOwner, scope);

            Parallel.Invoke(
                () => fixture.Owner.CommitAtCanonicalRetireBoundary(
                    fixture.Producer,
                    Candidate(17, 23, 2, 12, 102)),
                fixture.Owner.InvalidateAfterRestore);
            AssertSnapshotIsAbsentOrSelfConsistent(fixture.Owner.ObservationOwner, scope);

            Parallel.Invoke(
                () => fixture.Owner.CommitAtCanonicalRetireBoundary(
                    fixture.Producer,
                    Candidate(17, 23, 2, 13, 103)),
                () => fixture.Owner.RebindObservation(scope));
            AssertSnapshotIsAbsentOrSelfConsistent(fixture.Owner.ObservationOwner, scope);

            DomainCompletionObservationOwner? replacement = null;
            Parallel.Invoke(
                () => fixture.Owner.CommitAtCanonicalRetireBoundary(
                    fixture.Producer,
                    Candidate(17, 23, 2, 14, 104)),
                () => replacement = fixture.Owner.ReplaceObservationOwner());
            Assert.NotNull(replacement);
            AssertSnapshotIsAbsentOrSelfConsistent(replacement!, scope);
        }
    }

    [Fact]
    public void ObservationStateGenerationReceiptsAndSeals_HaveNoSerializationContour()
    {
        string repositoryRoot =
            VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string observation = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Completion",
            "Observation", "DomainCompletionObservationOwner.cs"));
        Assert.Contains("RecomputedCompletion", observation);
        Assert.DoesNotContain("JsonSerializer", observation, StringComparison.Ordinal);
        Assert.DoesNotContain("BinaryWriter", observation, StringComparison.Ordinal);
        Assert.DoesNotContain("ISerializable", observation, StringComparison.Ordinal);
        Assert.DoesNotContain("VmExitReason", observation, StringComparison.Ordinal);
        Assert.DoesNotContain("VmxExitQualification", observation, StringComparison.Ordinal);
        Assert.DoesNotContain("VmcsField", observation, StringComparison.Ordinal);
        Assert.DoesNotContain("VmRead", observation, StringComparison.Ordinal);
        Assert.DoesNotContain("ArchitecturalCompletionCommitReceipt", observation,
            StringComparison.Ordinal);
    }

    private static void AssertSnapshotIsAbsentOrSelfConsistent(
        DomainCompletionObservationOwner owner,
        in CompletionObservationScope scope)
    {
        CompletionObservationResult result = owner.Observe(scope);
        if (!result.IsObserved)
        {
            Assert.Contains(result.Decision, new[]
            {
                CompletionObservationDecision.DeniedAbsent,
                CompletionObservationDecision.DeniedInactiveOwner,
            });
            return;
        }

        NeutralCompletionObservationSnapshot snapshot = result.Snapshot!.Value;
        Assert.Equal(scope, snapshot.Scope);
        Assert.NotEqual(0UL, snapshot.CompletionGeneration);
        Assert.NotEqual(0UL, snapshot.CompletionIdentity);
        Assert.NotEqual(0UL, snapshot.CommitSequence);
        Assert.NotEqual(0UL, snapshot.RestoreGeneration);
        Assert.True(snapshot.CompletionGeneration <= owner.CurrentGeneration);
    }

    private static Fixture CreateFixture(
        ArchitecturalCompletionProducerPolicy? policy = null)
    {
        var observation = new DomainCompletionObservationOwner(
            new CompletionGenerationAuthority());
        var owner = new ArchitecturalCompletionCommitOwner(
            new VirtualizationRestoreGenerationOwner(),
            observation);
        ArchitecturalCompletionCommitOwner.ProducerRegistration producer =
            owner.RegisterProducer(policy ?? new ArchitecturalCompletionProducerPolicy(
                "CanonicalPipelineTrapEntryProducer",
                NeutralArchitecturalCompletionClass.TrapEntry,
                RequiresReason: true,
                AllowsQualification: false,
                NeutralFaultAddressSemantic.VirtualAddress,
                NeutralFaultAuxiliarySemantic.None));
        return new(owner, owner.ObservationOwner, producer);
    }

    private static ArchitecturalCompletionCandidate Candidate(
        ulong domain,
        int context,
        int vt,
        ulong attempt,
        ulong eventId) =>
        new(domain, context, vt, attempt, eventId, TrapFacts());

    private static NeutralArchitecturalCompletionFacts TrapFacts(
        ulong reason = 2,
        bool addressPresent = false,
        ulong address = 0) =>
        new(
            NeutralArchitecturalCompletionClass.TrapEntry,
            NeutralScalarFact.Present(reason),
            NeutralScalarFact.Absent,
            addressPresent
                ? NeutralAddressFact.Present(
                    address,
                    NeutralFaultAddressSemantic.VirtualAddress)
                : NeutralAddressFact.Absent,
            NeutralAuxiliaryFact.Absent);

    private sealed record Fixture(
        ArchitecturalCompletionCommitOwner Owner,
        DomainCompletionObservationOwner Observation,
        ArchitecturalCompletionCommitOwner.ProducerRegistration Producer);

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
