using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxD2V2GovernanceNegativeSubstrateTests
{
    [Fact]
    public void CanonicalEncoder_IsBinaryVersionedDeterministicAndSerializerIndependent()
    {
        Fixture fixture = Fixture.Create();
        ImmutableArray<byte> canonical =
            VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(fixture.Spec);
        string header = Encoding.ASCII.GetString(canonical.AsSpan(0, 8));

        Assert.Equal("HCPUVD2\0", header);
        Assert.Equal(1, canonical[8]);
        Assert.Equal(0, canonical[9]);
        Assert.Equal(VirtualizationDecisionCanonicalEncoderV2.EncodingVersion, canonical[10]);

        VirtualizationDecisionSpecV2 reordered = fixture.Spec with
        {
            OwnerMap = fixture.Spec.OwnerMap.Reverse().ToImmutableArray(),
        };
        Assert.Equal(
            fixture.Spec.SpecDigest,
            VirtualizationDecisionCanonicalEncoderV2.ComputeSpecDigest(reordered));
        Assert.True(canonical.AsSpan().SequenceEqual(
            VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(reordered).AsSpan()));

        byte[] serializerJson = JsonSerializer.SerializeToUtf8Bytes(fixture.Spec);
        byte[] serializerJsonWithWhitespace = Encoding.UTF8.GetBytes(
            " \r\n" + Encoding.UTF8.GetString(serializerJson) + "\r\n ");
        Assert.NotEqual(serializerJson, canonical.ToArray());
        Assert.NotEqual(serializerJsonWithWhitespace, canonical.ToArray());
        Assert.Equal(
            fixture.Spec.SpecDigest,
            VirtualizationDecisionCanonicalEncoderV2.ComputeSpecDigest(fixture.Spec));
    }

    [Fact]
    public void StructuralFixture_ReturnsImmutablePolicyObjectAndNoAuthority()
    {
        Fixture fixture = Fixture.Create();
        VirtualizationDecisionValidationResultV2 result = fixture.Validate();

        Assert.Equal(VirtualizationDecisionValidationDecisionV2.AcceptedPolicyObject, result.Decision);
        Assert.True(result.IsAcceptedPolicyObject);
        AcceptedVirtualizationDecision accepted = Assert.IsType<AcceptedVirtualizationDecision>(result.AcceptedDecision);
        Assert.Equal(VirtualizationDecisionValidatorV2.ExpectedDecisionId, accepted.DecisionId);
        Assert.Equal(VirtualizationDecisionValidatorV2.ExpectedOwnerId, accepted.OwnerId);
        Assert.False(result.RuntimeCapabilityGranted);
        Assert.False(result.BackendExecutionAuthorized);
        Assert.False(result.CompletionPublicationAuthorized);
        Assert.False(result.RetirePublicationAuthorized);
    }

    [Fact]
    public void Validator_DeniesWrongSpecDigest()
    {
        Fixture fixture = Fixture.Create();
        VirtualizationDecisionValidationResultV2 result = fixture.Validate(
            fixture.Spec with { SpecDigest = new string('f', 64) });
        Assert.Equal(VirtualizationDecisionValidationDecisionV2.DeniedSpecDigestMismatch, result.Decision);
    }

    [Fact]
    public void Validator_DeniesWrongAcceptanceDigest()
    {
        Fixture fixture = Fixture.Create();
        VirtualizationDecisionValidationResultV2 result = fixture.Validate(
            acceptance: fixture.Acceptance with { AcceptanceDigest = new string('e', 64) });
        Assert.Equal(VirtualizationDecisionValidationDecisionV2.DeniedAcceptanceDigestMismatch, result.Decision);
    }

    [Fact]
    public void Validator_DeniesMalformedDigestWithoutThrowing()
    {
        Fixture fixture = Fixture.Create();
        VirtualizationDecisionValidationResultV2 result = fixture.Validate(
            fixture.Spec with { SpecDigest = "not-sha256" });
        Assert.Equal(VirtualizationDecisionValidationDecisionV2.DeniedMalformedDigest, result.Decision);
    }

    [Fact]
    public void Validator_DeniesWrongResolvedSpecCommitSha()
    {
        Fixture fixture = Fixture.Create();
        string wrongSha = new('4', 40);
        Fixture changed = fixture.RebuildAcceptance(acceptance => acceptance with
        {
            SpecCommitSha = wrongSha,
            OwnerReviewEvidence = acceptance.OwnerReviewEvidence with { ReviewedSpecCommitSha = wrongSha },
            ArchitectureReviewEvidence = acceptance.ArchitectureReviewEvidence with { ReviewedSpecCommitSha = wrongSha },
        });

        Assert.Equal(
            VirtualizationDecisionValidationDecisionV2.DeniedSpecBytesAtCommitMismatch,
            changed.Validate().Decision);
    }

    [Fact]
    public void Validator_DeniesMalformedAndSelfReferentialCommitSha()
    {
        Fixture fixture = Fixture.Create();
        Assert.Equal(
            VirtualizationDecisionValidationDecisionV2.DeniedMalformedCommitSha,
            fixture.Validate(acceptance: fixture.Acceptance with { SpecCommitSha = "bad" }).Decision);

        string containingSha = fixture.Evidence.AcceptanceContainingCommitSha;
        Fixture selfReferential = fixture.RebuildAcceptance(acceptance => acceptance with
        {
            SpecCommitSha = containingSha,
            OwnerReviewEvidence = acceptance.OwnerReviewEvidence with { ReviewedSpecCommitSha = containingSha },
            ArchitectureReviewEvidence = acceptance.ArchitectureReviewEvidence with { ReviewedSpecCommitSha = containingSha },
        });
        selfReferential = selfReferential with
        {
            Evidence = selfReferential.Evidence with { ResolvedSpecCommitSha = containingSha },
        };
        Assert.Equal(
            VirtualizationDecisionValidationDecisionV2.DeniedSelfReferentialCommitSha,
            selfReferential.Validate().Decision);
    }

    [Fact]
    public void Validator_DeniesWrongDecisionIdAfterCanonicalRebuild()
    {
        Fixture changed = Fixture.Create().RebuildSpec(spec => spec with { DecisionId = "wrong" });
        Assert.Equal(
            VirtualizationDecisionValidationDecisionV2.DeniedDecisionIdentity,
            changed.Validate().Decision);
    }

    [Fact]
    public void Validator_DeniesNonCanonicalAndWrongCommitBytes()
    {
        Fixture fixture = Fixture.Create();
        ImmutableArray<byte> noncanonical = fixture.Evidence.SpecCanonicalBytes.Add(0);
        Assert.Equal(
            VirtualizationDecisionValidationDecisionV2.DeniedNonCanonicalSpecBytes,
            fixture.Validate(evidence: fixture.Evidence with { SpecCanonicalBytes = noncanonical }).Decision);

        ImmutableArray<byte> wrongAtCommit = fixture.Evidence.SpecBytesAtCommit.SetItem(5,
            (byte)(fixture.Evidence.SpecBytesAtCommit[5] ^ 0x01));
        Assert.Equal(
            VirtualizationDecisionValidationDecisionV2.DeniedSpecBytesAtCommitMismatch,
            fixture.Validate(evidence: fixture.Evidence with { SpecBytesAtCommit = wrongAtCommit }).Decision);
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    public void Validator_DeniesZeroOrWrongOwner(ulong ownerId)
    {
        Fixture changed = Fixture.Create().RebuildSpec(spec => spec with { OwnerId = ownerId });
        Assert.Equal(VirtualizationDecisionValidationDecisionV2.DeniedOwner, changed.Validate().Decision);
    }

    [Fact]
    public void Validator_DeniesZeroDuplicateAdjacentAndCrossNamespaceLeaves()
    {
        Fixture zero = Fixture.Create().RebuildSpec(spec => spec with { NumericLeaf = 0 });
        Assert.Equal(VirtualizationDecisionValidationDecisionV2.DeniedZeroLeaf, zero.Validate().Decision);

        Fixture fixture = Fixture.Create();
        VirtualizationNamespaceAllocationV2 duplicate = new(
            fixture.Spec.OperationNamespace,
            16,
            fixture.Spec.NumericLeaf,
            "another-decision",
            VirtualizationNamespaceClassV2.RuntimeAuthority);
        Assert.Equal(
            VirtualizationDecisionValidationDecisionV2.DeniedDuplicateLeaf,
            fixture.Validate(evidence: fixture.Evidence with
            {
                ExistingAllocations = fixture.Evidence.ExistingAllocations.Add(duplicate),
            }).Decision);

        VirtualizationNamespaceAllocationV2 adjacent = duplicate with
        {
            NumericLeaf = 2,
            DecisionId = "adjacent-decision",
        };
        Assert.Equal(
            VirtualizationDecisionValidationDecisionV2.DeniedAdjacentLeaf,
            fixture.Validate(evidence: fixture.Evidence with
            {
                ExistingAllocations = fixture.Evidence.ExistingAllocations.Add(adjacent),
            }).Decision);

        VirtualizationNamespaceAllocationV2 crossNamespace = duplicate with
        {
            OperationNamespace = "HybridCPU.VMCALL.OtherRuntime.v1",
            DecisionId = "cross-namespace-decision",
        };
        Assert.Equal(
            VirtualizationDecisionValidationDecisionV2.DeniedCrossNamespaceLeaf,
            fixture.Validate(evidence: fixture.Evidence with
            {
                ExistingAllocations = fixture.Evidence.ExistingAllocations.Add(crossNamespace),
            }).Decision);
    }

    [Fact]
    public void Validator_AllowsDistinctFrozenCompatibilityNamespaceButNotWidthOrAbiChanges()
    {
        Fixture fixture = Fixture.Create();
        Assert.Equal(VirtualizationDecisionValidationDecisionV2.AcceptedPolicyObject, fixture.Validate().Decision);

        Fixture width = fixture.RebuildSpec(spec => spec with { LeafWidth = 32 });
        Assert.Equal(VirtualizationDecisionValidationDecisionV2.DeniedNamespaceRule, width.Validate().Decision);

        Fixture abi = fixture.RebuildSpec(spec => spec with { Rs2Contract = "X1" });
        Assert.Equal(VirtualizationDecisionValidationDecisionV2.DeniedAbi, abi.Validate().Decision);
    }

    [Fact]
    public void Validator_DeniesMissingUnknownAndWrongPolicy()
    {
        Fixture missing = Fixture.Create().RebuildSpec(spec => spec with
        {
            CancellationPolicy = VirtualizationCancellationPolicyV2.Unspecified,
        });
        Assert.Equal(VirtualizationDecisionValidationDecisionV2.DeniedPolicyMissing, missing.Validate().Decision);

        Fixture unknown = Fixture.Create().RebuildSpec(spec => spec with
        {
            CancellationPolicy = (VirtualizationCancellationPolicyV2)255,
        });
        Assert.Equal(VirtualizationDecisionValidationDecisionV2.DeniedUnknownPolicy, unknown.Validate().Decision);

        Fixture wrong = Fixture.Create().RebuildSpec(spec => spec with { RequiresMemoryDomain = true });
        Assert.Equal(VirtualizationDecisionValidationDecisionV2.DeniedPolicyProfile, wrong.Validate().Decision);
    }

    [Fact]
    public void Validator_DeniesIncompleteAndCompatibilityOwnedMap()
    {
        Fixture fixture = Fixture.Create();
        Fixture incomplete = fixture.RebuildSpec(spec => spec with
        {
            OwnerMap = spec.OwnerMap.RemoveAt(0),
        });
        Assert.Equal(VirtualizationDecisionValidationDecisionV2.DeniedOwnerMapIncomplete, incomplete.Validate().Decision);

        Fixture compatibilityOwned = fixture.RebuildSpec(spec => spec with
        {
            OwnerMap = spec.OwnerMap.SetItem(0, spec.OwnerMap[0] with { Owner = "CompatibilityFrontend" }),
        });
        Assert.Equal(VirtualizationDecisionValidationDecisionV2.DeniedOwnerMapMismatch, compatibilityOwned.Validate().Decision);
    }

    [Fact]
    public void Validator_DeniesMissingCodeOwnersAndReviewerRoleMismatch()
    {
        Fixture fixture = Fixture.Create();
        Assert.Equal(
            VirtualizationDecisionValidationDecisionV2.DeniedCodeOwners,
            fixture.Validate(evidence: fixture.Evidence with
            {
                CodeOwners = fixture.Evidence.CodeOwners with { FilePresent = false },
            }).Decision);

        Fixture wrongRole = fixture.RebuildAcceptance(acceptance => acceptance with
        {
            OwnerReviewEvidence = acceptance.OwnerReviewEvidence with
            {
                Role = VirtualizationDecisionReviewRoleV2.ArchitectureReviewRole,
            },
        });
        Assert.Equal(VirtualizationDecisionValidationDecisionV2.DeniedReviewRole, wrongRole.Validate().Decision);
    }

    [Fact]
    public void Validator_DeniesCompatibilityOnlyReviewButAllowsSamePrincipalForLogicalRoles()
    {
        Fixture fixture = Fixture.Create();
        Assert.Equal(
            fixture.Acceptance.OwnerReviewEvidence.Principal,
            fixture.Acceptance.ArchitectureReviewEvidence.Principal);
        Assert.Equal(VirtualizationDecisionValidationDecisionV2.AcceptedPolicyObject, fixture.Validate().Decision);

        Fixture compatibilityOnly = fixture.RebuildAcceptance(acceptance => acceptance with
        {
            OwnerReviewEvidence = acceptance.OwnerReviewEvidence with
            {
                AuthorityPlane = VirtualizationDecisionReviewAuthorityPlaneV2.CompatibilityFrontend,
            },
        });
        Assert.Equal(
            VirtualizationDecisionValidationDecisionV2.DeniedCompatibilityReview,
            compatibilityOnly.Validate().Decision);
    }

    [Fact]
    public void Validator_DeniesInvalidOrInconsistentRepositoryPrincipalAttribution()
    {
        Fixture fixture = Fixture.Create();

        Fixture invalidPrincipal = fixture.RebuildAcceptance(acceptance => acceptance with
        {
            AcceptedBy = "repository-owner",
        });
        Assert.Equal(
            VirtualizationDecisionValidationDecisionV2.DeniedAcceptanceState,
            invalidPrincipal.Validate().Decision);

        Fixture mismatchedReview = fixture.RebuildAcceptance(acceptance => acceptance with
        {
            ArchitectureReviewEvidence = acceptance.ArchitectureReviewEvidence with
            {
                Principal = "@another-repository-owner",
            },
        });
        Assert.Equal(
            VirtualizationDecisionValidationDecisionV2.DeniedReviewMismatch,
            mismatchedReview.Validate().Decision);

        Fixture mismatchedCodeOwners = fixture with
        {
            Evidence = fixture.Evidence with
            {
                CodeOwners = fixture.Evidence.CodeOwners with
                {
                    Rules = fixture.Evidence.CodeOwners.Rules.SetItem(
                        0,
                        fixture.Evidence.CodeOwners.Rules[0] with
                        {
                            Principal = "@another-repository-owner",
                        }),
                },
            },
        };
        Assert.Equal(
            VirtualizationDecisionValidationDecisionV2.DeniedCodeOwners,
            mismatchedCodeOwners.Validate().Decision);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    public void Validator_DeniesInactiveAcceptanceRecordStates(int stateValue)
    {
        var state = (VirtualizationDecisionAcceptanceStateV2)stateValue;
        Fixture changed = Fixture.Create().RebuildAcceptance(acceptance => acceptance with
        {
            AcceptanceState = state,
        });
        Assert.Equal(VirtualizationDecisionValidationDecisionV2.DeniedAcceptanceState, changed.Validate().Decision);
    }

    [Fact]
    public void Validator_DeniesEffectiveImmutableRevocation()
    {
        Fixture fixture = Fixture.Create();
        VirtualizationDecisionRevocationRecordV2 record = new(
            2,
            "REV-0001",
            fixture.Acceptance.DecisionId,
            fixture.Acceptance.AcceptanceDigest,
            VirtualizationDecisionRevocationStateV2.Effective,
            Fixture.RepositoryPrincipal,
            "policy revoked",
            1,
            new string('0', 64));
        record = record with
        {
            RevocationDigest = VirtualizationDecisionCanonicalEncoderV2.ComputeRevocationDigest(record),
        };
        VirtualizationDecisionRevocationEvidenceV2 lineage = new(
            record,
            VirtualizationDecisionCanonicalEncoderV2.EncodeRevocation(record));

        Assert.Equal(
            VirtualizationDecisionValidationDecisionV2.DeniedRevoked,
            fixture.Validate(evidence: fixture.Evidence with { Revocations = [lineage] }).Decision);
    }

    [Fact]
    public void Validator_DeniesEffectiveImmutableSupersession()
    {
        Fixture fixture = Fixture.Create();
        VirtualizationDecisionSupersessionRecordV2 record = new(
            2,
            "SUP-0001",
            fixture.Acceptance.DecisionId,
            fixture.Acceptance.AcceptanceDigest,
            "D2-HV-FUTURE-DECISION",
            new string('a', 64),
            VirtualizationDecisionSupersessionStateV2.Effective,
            Fixture.RepositoryPrincipal,
            1,
            new string('0', 64));
        record = record with
        {
            SupersessionDigest = VirtualizationDecisionCanonicalEncoderV2.ComputeSupersessionDigest(record),
        };
        VirtualizationDecisionSupersessionEvidenceV2 lineage = new(
            record,
            VirtualizationDecisionCanonicalEncoderV2.EncodeSupersession(record));

        Assert.Equal(
            VirtualizationDecisionValidationDecisionV2.DeniedSuperseded,
            fixture.Validate(evidence: fixture.Evidence with { Supersessions = [lineage] }).Decision);
    }

    [Fact]
    public void Validator_DeniesInvalidLineage()
    {
        Fixture fixture = Fixture.Create();
        VirtualizationDecisionRevocationRecordV2 record = new(
            2,
            "REV-BAD",
            fixture.Acceptance.DecisionId,
            new string('b', 64),
            VirtualizationDecisionRevocationStateV2.Effective,
            Fixture.RepositoryPrincipal,
            "wrong acceptance lineage",
            1,
            new string('0', 64));
        record = record with
        {
            RevocationDigest = VirtualizationDecisionCanonicalEncoderV2.ComputeRevocationDigest(record),
        };
        VirtualizationDecisionRevocationEvidenceV2 lineage = new(
            record,
            VirtualizationDecisionCanonicalEncoderV2.EncodeRevocation(record));

        Assert.Equal(
            VirtualizationDecisionValidationDecisionV2.DeniedInvalidLineage,
            fixture.Validate(evidence: fixture.Evidence with { Revocations = [lineage] }).Decision);
    }

    [Fact]
    public void Validator_MalformedLineageFailsClosedWithoutThrowing()
    {
        Fixture fixture = Fixture.Create();
        var malformed = new VirtualizationDecisionRevocationRecordV2(
            2,
            "REV-MALFORMED",
            fixture.Acceptance.DecisionId,
            "not-a-digest",
            VirtualizationDecisionRevocationStateV2.Effective,
            Fixture.RepositoryPrincipal,
            "malformed fixture",
            1,
            new string('c', 64));
        VirtualizationDecisionValidationResultV2 result = fixture.Validate(
            evidence: fixture.Evidence with
            {
                Revocations = [new(malformed, [])],
            });

        Assert.Equal(VirtualizationDecisionValidationDecisionV2.DeniedInvalidLineage, result.Decision);
        Assert.Null(result.AcceptedDecision);
    }

    [Fact]
    public void GovernanceSourcesContainNoRuntimeAuthorityShortcut()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string governanceRoot = Path.Combine(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Runtime",
            "Governance",
            "Virtualization");
        string source = string.Concat(Directory.GetFiles(governanceRoot, "*.cs").Select(File.ReadAllText));

        Assert.DoesNotContain("HypercallBackendAdmissionDecision.Allowed", source);
        Assert.DoesNotContain("BackendExecutionAuthorized: true", source);
        Assert.DoesNotContain("InvokeHypercall", source);
        Assert.DoesNotContain("DomainHypercallRuntimeExecutor", source);
        Assert.DoesNotContain("new CompletionRecord", source);
        Assert.DoesNotContain("VmxRetireEffect.VmcsRead(", source);
        Assert.DoesNotContain("VirtualizationOperationOwnerSnapshot", source);
        Assert.DoesNotContain("CapabilityGrant(", source);
    }

    private sealed record Fixture(
        VirtualizationDecisionSpecV2 Spec,
        VirtualizationDecisionAcceptanceRecordV2 Acceptance,
        VirtualizationDecisionValidationEvidenceV2 Evidence)
    {
        internal const string RepositoryPrincipal = "@repository-owner";
        private const string SpecCommitSha = "1111111111111111111111111111111111111111";
        private const string ContainingCommitSha = "2222222222222222222222222222222222222222";
        private const string CodeOwnersBlobSha = "3333333333333333333333333333333333333333";

        internal static Fixture Create()
        {
            VirtualizationDecisionSpecV2 spec = new(
                2,
                VirtualizationDecisionValidatorV2.ExpectedDecisionId,
                VirtualizationDecisionValidatorV2.ExpectedOperationNamespace,
                16,
                0,
                1,
                VirtualizationDecisionValidatorV2.ExpectedOperationId,
                VirtualizationDecisionOwnerClassV2.NeutralRuntimeOwner,
                VirtualizationDecisionValidatorV2.ExpectedOwnerId,
                1,
                1,
                1,
                "ArchitecturalRegisterFullNumericLeafValue",
                "X0",
                "X0NoResult",
                VirtualizationDecisionResultAbiV2.NoPayload,
                VirtualizationDecisionEffectClassV2.NoStateNoPayload,
                VirtualizationCapabilityRequirementV2.DomainGrantedVmCallProbeNoStateV1,
                VirtualizationDecisionValidatorV2.ExpectedCapabilityMask,
                true,
                VirtualizationDelegationPolicyV2.NonDelegable,
                VirtualizationRevocationPolicyV2.RuntimeRevocable,
                VirtualizationCapabilityMigrationClassV2.DomainLocal,
                VirtualizationEvidenceVisibilityV2.HostOnly,
                VirtualizationProjectionPolicyV2.NeverProject,
                VirtualizationExecutionEvidenceRequirementV2.None,
                VirtualizationDomainRequirementV2.ExecutionDomainBound,
                true,
                false,
                false,
                VirtualizationAddressSpaceRequirementV2.None,
                VirtualizationSecureDomainPolicyV2.Deny,
                VirtualizationCancellationPolicyV2.DenyBeforeExecution,
                VirtualizationReplayPolicyV2.DenyAttemptReplay,
                VirtualizationOperationMigrationPolicyV2.DrainOnly,
                VirtualizationCompletionEvidenceClassV2.HostOwnedRuntimeEvidence,
                VirtualizationCompletionMigrationClassV2.HostOwnedNonMigratable,
                VirtualizationProjectionPolicyV2.NeverProject,
                VirtualizationCompletionPolicyV2.AtomicE3ToCompletionRecordAndE5,
                VirtualizationRetirePolicyV2.PreciseE5BoundNoStateRetire,
                VirtualizationAdjacentLeafPolicyV2.DenyAllExceptExact,
                VirtualizationCrossNamespacePolicyV2.AllowDistinctFrozenCompatibilityNamespaceOnly,
                OwnerMap(),
                new string('0', 64));
            spec = spec with { SpecDigest = VirtualizationDecisionCanonicalEncoderV2.ComputeSpecDigest(spec) };

            VirtualizationDecisionAcceptanceRecordV2 acceptance = CreateAcceptance(spec);
            var codeOwners = new VirtualizationCodeOwnersEvidenceV2(
                true,
                CodeOwnersBlobSha,
                CodeOwnersRules());
            var existingAllocations = ImmutableArray.Create(
                new VirtualizationNamespaceAllocationV2(
                    "HybridCPU.VMFUNC.FrozenAbi.v1",
                    16,
                    1,
                    "FROZEN-VMFUNC-CAPABILITY-QUERY",
                    VirtualizationNamespaceClassV2.FrozenCompatibility));
            var evidence = new VirtualizationDecisionValidationEvidenceV2(
                VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(spec),
                VirtualizationDecisionCanonicalEncoderV2.EncodeAcceptance(acceptance),
                VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(spec),
                SpecCommitSha,
                ContainingCommitSha,
                codeOwners,
                existingAllocations,
                [],
                []);
            return new(spec, acceptance, evidence);
        }

        internal VirtualizationDecisionValidationResultV2 Validate(
            VirtualizationDecisionSpecV2? spec = null,
            VirtualizationDecisionAcceptanceRecordV2? acceptance = null,
            VirtualizationDecisionValidationEvidenceV2? evidence = null) =>
            VirtualizationDecisionValidatorV2.Validate(
                spec ?? Spec,
                acceptance ?? Acceptance,
                evidence ?? Evidence);

        internal Fixture RebuildSpec(Func<VirtualizationDecisionSpecV2, VirtualizationDecisionSpecV2> update)
        {
            VirtualizationDecisionSpecV2 spec = update(Spec);
            spec = spec with { SpecDigest = VirtualizationDecisionCanonicalEncoderV2.ComputeSpecDigest(spec) };
            VirtualizationDecisionAcceptanceRecordV2 acceptance = CreateAcceptance(spec);
            return this with
            {
                Spec = spec,
                Acceptance = acceptance,
                Evidence = Evidence with
                {
                    SpecCanonicalBytes = VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(spec),
                    SpecBytesAtCommit = VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(spec),
                    AcceptanceCanonicalBytes = VirtualizationDecisionCanonicalEncoderV2.EncodeAcceptance(acceptance),
                },
            };
        }

        internal Fixture RebuildAcceptance(
            Func<VirtualizationDecisionAcceptanceRecordV2, VirtualizationDecisionAcceptanceRecordV2> update)
        {
            VirtualizationDecisionAcceptanceRecordV2 acceptance = update(Acceptance);
            acceptance = acceptance with
            {
                AcceptanceDigest = VirtualizationDecisionCanonicalEncoderV2.ComputeAcceptanceDigest(acceptance),
            };
            return this with
            {
                Acceptance = acceptance,
                Evidence = Evidence with
                {
                    AcceptanceCanonicalBytes = VirtualizationDecisionCanonicalEncoderV2.EncodeAcceptance(acceptance),
                },
            };
        }

        private static VirtualizationDecisionAcceptanceRecordV2 CreateAcceptance(
            VirtualizationDecisionSpecV2 spec)
        {
            VirtualizationDecisionReviewEvidenceV2 ownerReview = new(
                VirtualizationDecisionReviewRoleV2.OwnerReviewRole,
                VirtualizationDecisionReviewAuthorityPlaneV2.NeutralRuntimeOwner,
                VirtualizationDecisionReviewStateV2.Completed,
                RepositoryPrincipal,
                spec.DecisionId,
                spec.SpecDigest,
                SpecCommitSha,
                "fixture-owner-review");
            VirtualizationDecisionReviewEvidenceV2 architectureReview = ownerReview with
            {
                Role = VirtualizationDecisionReviewRoleV2.ArchitectureReviewRole,
                AuthorityPlane = VirtualizationDecisionReviewAuthorityPlaneV2.Architecture,
                EvidenceId = "fixture-architecture-review",
            };
            VirtualizationDecisionAcceptanceRecordV2 acceptance = new(
                2,
                spec.DecisionId,
                spec.SpecDigest,
                SpecCommitSha,
                VirtualizationDecisionAcceptanceStateV2.Accepted,
                RepositoryPrincipal,
                1,
                ownerReview,
                architectureReview,
                CodeOwnersBlobSha,
                null,
                null,
                new string('0', 64));
            return acceptance with
            {
                AcceptanceDigest = VirtualizationDecisionCanonicalEncoderV2.ComputeAcceptanceDigest(acceptance),
            };
        }

        private static ImmutableArray<VirtualizationDecisionOwnerMapEntryV2> OwnerMap()
        {
            (string Field, string Owner)[] entries =
            [
                ("Operation", "DomainHypercallRuntimeOwner"),
                ("OperandAbi", "CanonicalOperandSnapshotOwner"),
                ("CapabilityAdmission", "RuntimeCapabilityOwner"),
                ("DomainAdmission", "RuntimeDomainOwner"),
                ("ExecutionAdmission", "SafetyVerifier"),
                ("CancellationReplay", "DomainHypercallRuntimeOwner"),
                ("CompletionPublication", "NeutralCompletionOwner"),
                ("RetirePublication", "CanonicalRetireOwner"),
                ("MigrationRestore", "CheckpointRestoreOwner"),
                ("AdjacentLeafDenial", "VirtualizationDecisionValidatorV2"),
            ];
            return entries.Select(entry => new VirtualizationDecisionOwnerMapEntryV2(
                entry.Field,
                entry.Owner,
                "Phase38ExactValueSource",
                "ExactTypedGrantOrNone",
                "HostOnlyOrNone",
                "DrainOnlyOrHostOwnedNonMigratable",
                "DenyOnMissingOrMismatch"))
                .ToImmutableArray();
        }

        private static ImmutableArray<VirtualizationCodeOwnersRuleV2> CodeOwnersRules() =>
        [
            new("/HybridCPU_ISE/CloseToHSL/Core/Runtime/Events/Hypercalls/", RepositoryPrincipal),
            new("/HybridCPU_ISE/CloseToHSL/Core/Runtime/Capabilities/", RepositoryPrincipal),
            new("/HybridCPU_ISE/CloseToHSL/Core/Pipeline/Safety/", RepositoryPrincipal),
            new("/HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/", RepositoryPrincipal),
            new("/HybridCPU_ISE/CloseToHSL/Core/Pipeline/Retire/", RepositoryPrincipal),
            new("/HybridCPU_ISE/docs/ref2/VirtualizationActivationPlan/", RepositoryPrincipal),
        ];
    }
}
