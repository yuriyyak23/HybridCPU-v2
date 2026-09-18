# Current Audit Revalidation And Dependency Order

Status date: 2026-08-07.

This document is the current audit reconciliation and dependency ledger. It is not runtime authority, an execution certificate or release evidence.

## Reproducibility Baseline

| Field | Current local fact |
| --- | --- |
| external-analysis revision | `d3814d1f332f083034d3b245f807a45f97792070` reported by `deep-research-report SC.md`; the object is not present in the local Git object database, so ancestry against the current branch cannot be proven locally |
| current local `HEAD` | `b6d4871e0f06ebde07015e393c0d36af0362f506` on `refactor/compiler-core-authority-boundaries` |
| worktree | dirty: the initial `git status --short` contained 68 entries (16 modified, 46 deleted and 6 untracked); unrelated pipeline, VMX and documentation work is not part of this plan update |
| active solution | `HybridCPU v2.slnx`; includes `HybridCPU_ISE`, `HybridCPU_ISE.Tests`, `HybridCPU_Compiler`, generator and documentation projects |
| requested SDK | `global.json` specifies `10.0.201` |
| actual SDK in this review | `10.0.204`, MSBuild `18.3.3`; this mismatch blocks a clean reproducibility claim |
| current production root used for review | tracked `HybridCPU_ISE/CloseToHSL/` plus `HybridCPU_Compiler/`; untracked VMX admission prototype files were inspected only as dirty-worktree context and are not baseline evidence |
| external analyses | `docs/ref2/1/SC/deep-research-report SC.md` and `docs/ref2/1/SC/Исследование-SecureCompute.md`; input hypotheses/recommendations only |

Key SHA-256 inputs captured before this plan update:

| Input | SHA-256 |
| --- | --- |
| `global.json` | `f368191a0d8b7510fcb3fc6a0bbadbd728b50fbefa0315e62bb87c59cb72dc58` |
| `HybridCPU v2.slnx` | `2682ea9ba14fe2480f9f0a5541012be5decb3f0e8df15102302077b7032dcc98` |
| `deep-research-report SC.md` | `83f1717e2057b80f56463411a0c80ac20460acc00a2a43488f124f350685164c` |
| `Исследование-SecureCompute.md` | `376c2d677c1ddc18fd916ce54d83c84328eb43cc2883e0b497b385432851042c` |
| `HybridCPU_ISE/HybridCPU_ISE.csproj` | `4974eaf298bbfffe2ffbb31a784bf3ed9c8802e74882e428f5b1ab3f4d11fa55` |
| `HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj` | `4fe1bd337192b0e8bc5626cf9e6b98c7c0d4bb6140ea80cbc08879476855655c` |
| `RuntimeBoundaryAdmissionService.cs` | `fc4f3247f3d804be4afed0f489fbfbddcd966c0586f779ab1d3d3728bbbd871a` |
| `SecureDomainAdmissionPolicy.cs` | `f272a4ae1b38404a78b8eac3a65e8274f713982e5d90b636d9926af414a52224` |
| `SecurePositiveRuntimeExecutionActivationPolicy.cs` | `ad8bcbb73cfa2e170c04d3a2376310f2f3e568ee60281a010ed2d4e8395e2466` |
| `SecureComputePhase22LimitedReleaseGatePolicy.cs` | `df6b06a51f9edb81fe16c4a1f5ff6aaa60a37c5b886f3765decfe491fbcf41f6` |
| `HybridCPU_Compiler/Core/IR/Authority/CompilerProductionLoweringGate.cs` | `65fdd23ac9e540b591a8008f77072940f1ce182eead019404cd384a53d4e6a6a` |
| `tools/HybridCPU.IsaGen/Program.cs` | `172280b0f490b95324a297af1c282d63c8db5203fe4a75844b50c26d2ad3547e` |
| `isa/hybridcpu-isa.manifest.json` | `39b7fc5d1622e6d11725c005306560dff6b9095df6d6830a7748aeb2a6a0b29c` |
| `isa/hybridcpu-isa.csharp-compatibility.json` | `9add283266da5eb9cb3955366978913d29ae9a8bbb43be3bfc2af302e30e9cbd` |
| `GeneratedIsaOpcodeValues.g.cs` | `4ad1d049514cbb2f95290c7aeb16466d99fec5d7fa3950cf38f8322783185fa6` |

These hashes identify reviewed inputs; they do not prove a clean checkout, generator determinism or CI provenance. A release evidence run must repeat them from an immutable clean revision and record exact filters, counts, configuration and artifacts.

## Revalidated Audit Findings

| Question | Current fact | Classification |
| --- | --- | --- |
| canonical decode-to-retire SecureCompute path | no SecureCompute reference exists in `SafetyVerifier`; the only production constructors/callers of `RuntimeBoundaryAdmissionService` found are VMX compatibility VMREAD/VMCALL projection paths, whose `SecureOperationClass` stays at the default `Ordinary`; no SecureCompute issue/execute/result/completion/retire chain exists | open C0 |
| descriptor materialization/revocation owner | no registry or lifecycle owner exists; constructors are public DTO construction and `DomainRuntimeContext.WithSecureCompute` replaces the full descriptor reference | open C0 |
| descriptor carrier sources | `context.SecureCompute ?? request.SecureDescriptor` accepts two full-descriptor carrier sources | confirmed defect, C0 |
| SafetyVerifier certificate | no `SecureAdmissionCertificate` exists; `SecureDomainAdmissionResult` contains only decision and reason and binds no operation/domain/VT/source slot/epoch/effects identity. An untracked fault-only virtualization certificate prototype is zero-authority and is neither SecureCompute code nor immutable baseline evidence | open C0 |
| grant mint/revoke owner | `SecureGrantAuthorityPolicy` validates caller-supplied handles, epochs, bounds and booleans; no authoritative ledger/mint/revoke collection exists, and the hypercall helper passes `runtimeOwnerMaterialized: true` | open C0 |
| operation-specific exhaustive policies | only `SecureIo` and `SecureHypercall` receive special dispatch; other non-ordinary classes fall through to generic `AllowedSecureOperation`; unknown migration/checkpoint payloads fall through to allowed defaults | open C0/C1 |
| effect-path integration | secure memory, I/O, migration, evidence and completion/retire policies have no callers in the production memory/DMA/IOMMU/execute/retire roots; they are policy/classifier islands | open C0/C1 |
| disabled observational equivalence | tests prove admission-level ordinary no-effect and non-ordinary denial only; no dual-run pipeline/SMT/FSP/memory/I/O/exception/retire trace comparison exists | open C2 |
| conformance proof quality | executable policy tests exist, but source-string and plan-string checks are also counted in the current matrix; documentation assertions are documentation guards, not execution or authority proof | partial/C2 |
| reproducibility | current review is on a dirty worktree, audit SHA is unavailable locally, requested and actual SDK patch versions differ, and no immutable test/artifact attestation exists | open C2 |

## Phase Classification

| Phase | Current classification | Maximum supported statement |
| --- | --- | --- |
| 00-03 | confirmed governance vocabulary, updated baseline | documentation/process only |
| 04 | partial | admission-level ordinary no-effect; end-to-end disabled equivalence unproven |
| 05 | overestimated/open | descriptor DTO/completeness checks exist; lifecycle owner, opaque binding and revocation do not |
| 06 | overestimated/open | generic boundary policy route exists; CPU Stage-B/SafetyVerifier enforcement and production reachability do not |
| 07 | partial | handle/bounds/epoch validation exists; mint/revoke ledger does not |
| 08 | partial | measurement/evidence classifiers exist; one production evidence publisher does not |
| 09-10 | confirmed narrowly | neutral `GuestCr0`/`GuestCr4` read-only projection only; zero SecureCompute authority |
| 11-12 | partial policy classes | memory/I/O/shared-buffer admission logic exists; maps are ambiguous and memory/DMA/IOMMU/device effect paths are not connected |
| 13 | confirmed proof-only | identifier and request vocabulary only; no backend execution owner/path |
| 14 | overestimated as enforcement | publication policy model exists; no backend-result/completion/retire production owner chain |
| 15 | partial classifier | payload/output classification exists; default-allow branches, serializer/sealer/key owner and atomic restore protocol remain open |
| 16 | partial classifier | visibility policy exists; no exclusive production evidence publisher/API route |
| 17 | confirmed narrowly | read-only VMX compatibility projection remains zero-authority; it is not SecureCompute execution evidence |
| 18 | future/design-fenced unconditionally | no nested execution, mutable nested state, Shadow VMCS authority or nested publication |
| 19 | confirmed negative compiler decision; artifact proof open | compiler contour/lowering rejects SecureCompute emission; clean generated-artifact hash/no-emission proof is still required |
| 20 | pre-activation evidence classifier | caller booleans classify missing proofs; no executable positive path |
| 21 | negative/future-gated conformance matrix | executable negative policy tests plus documentation/source guards; no positive conformance or release authority |
| 22 | permanent hard-denied classifier | even a fully populated request returns `DeniedPhase22ManualApprovalNotImplemented`; future approval belongs to a separate offline evidence verifier, never to a flipped classifier branch |
| 23 | open backlog | no authority |
| 24-25 | current audit and recommendation ledgers | documentation-only reconciliation; no authority |

## Ranked Blocker Registry

### C0 — blocks every positive runtime path

1. No canonical operation taxonomy owned by decode/SafetyVerifier; caller controls `SecureDomainOperationClass`.
2. No production composition root from decode identity through SafetyVerifier, issue, execute, backend result, completion and retire.
3. No single descriptor lifecycle owner; two full-descriptor carriers and replacement without generation/revocation remain.
4. No immutable SafetyVerifier-issued certificate binding operation, descriptor, domain/address-space, VT, source/working/physical slot, bundle/FSP/replay identity, grants and bounded effects.
5. No grant ledger with one mint/revoke/reissue owner; caller handles and booleans remain inputs.
6. No exhaustive operation-specific admission; generic positive/default-allow results remain.
7. No backend-result, completion and retire owner chain reachable from a secure admitted attempt.

### C1 — blocks the named effect domain

1. Memory regions and shared buffers are first-match collections without overlap/duplicate canonicalization.
2. Private memory is a policy label; no pre-translation load/store/cache/prefetch/FSP enforcement path exists.
3. No device/PASID/IOMMU mapping owner, one-shot DMA intent or reset/rebind invalidation protocol.
4. Hypercall policy is admitted-denied and uses owner/provenance booleans; no typed backend execution owner exists.
5. Migration has default-allow payload classification and caller-supplied validation booleans; no serializer, key owner, anti-replay counter or atomic reopen protocol exists.
6. No exclusive evidence/debug/attestation publisher is connected to production outputs.

### C2 — blocks conformance and release

1. No end-to-end disabled observational equivalence for pipeline, SMT, FSP, memory, I/O, exceptions, completion and retire.
2. No clean generated-artifact no-emission proof with generator and output hashes.
3. No clean-checkout CI reproduction at an immutable SHA with exact SDK, configuration, filters, counts and artifact hashes.
4. No independent offline named-path release-evidence verifier or signed artifact; documentation/source-string checks are not part of the execution-proof numerator.

## Dependency-Ordered Small Changes

The order below incorporates the reconciled recommendations in Phase 25. Backend effects do not begin until the transport-only probe closes. Disabled equivalence is deliberately early, and the test-only carrier profile is deliberately earlier than the probe while remaining unavailable to product compiler/assembler APIs.

### Ownership, types, reachability and dependencies

| Step | Exact authority owner | Input -> output | Required production reachability | Dependencies |
| --- | --- | --- | --- | --- |
| 1 audit baseline freeze | release-evidence tooling; zero runtime authority | clean revision, exact SDK/generators/filters -> immutable manifest | clean-checkout CI only | none |
| 2 disabled equivalence harness | conformance harness; zero runtime authority | paired absent/Disabled workloads -> architectural trace hashes | real pipeline, SMT/FSP, memory/I/O, exception, completion and retire paths | 1 |
| 3 canonical operation taxonomy | generated ISA registry + canonical decoder + SafetyVerifier legality owner | frozen decode identity -> exhaustive `SecureOperationKind` with `Unknown = 0` | public canonical decode -> SafetyVerifier | 1-2 |
| 4 descriptor registry and opaque binding | `SecureDomainRegistry` as sole lifecycle owner | configuration DTO -> `SecureDomainBinding(RegistryId,DomainId,Generation,PolicyDigest,Seal)` | create/activate/quiesce/revoke -> runtime binding lookup | 3 |
| 5 grant ledger | `SecureGrantLedger` as sole mint/reserve/consume/revoke owner | issuer request/bounds -> opaque handle plus ledger entry/epochs/nonce/use state | registry/restore -> effect-path lookup | 4 |
| 6 SafetyVerifier certificate | SafetyVerifier as sole issuer | decoded identity, binding, VT/context, lanes, bundle/FSP/replay, grants, effect envelope -> opaque `SecureAdmissionCertificate` | SafetyVerifier -> issue carrier | 3-5 |
| 7 production admission carrier | issue owner carries, never reconstructs, the certificate | certificate -> source/working/physical attempt carrier | SafetyVerifier -> SMT/FSP nomination -> Stage B issue | 2,6 |
| 8 hermetic test-only carrier profile | conformance tooling; zero product/runtime authority | frozen test vector -> carrier bytes/sideband accepted only by test profile | test build -> public canonical decoder only | 3,6-7 |
| 9 decode-to-deny end-to-end slice | conformance harness; zero authority | test-only carrier -> certificate/issue attempt -> expected named denial | public decode -> SafetyVerifier -> issue -> deny | 7-8 |
| 10 named neutral transport probe | one named neutral transport owner | accepted attempt -> one-shot transport receipt with no effect | issued attempt -> exactly one owner; no memory/I/O/VMX/nested/product compiler path | 9 |
| 11 register-local deterministic effect | one named register-local backend owner | `SecureExecutionRequest` -> one-shot `SecureExecutionReceipt` | issued attempt -> named owner -> bounded register staging | 10 |
| 12 completion and retire | separate completion owner + architectural retire owner | receipt -> `SecureCompletionRecord` -> exactly-once retire effect | backend -> completion queue -> in-order retire | 11 |
| 13 evidence publisher | single `SecureEvidencePublisher`; zero effect authority | retired fact + audience policy -> bounded output | retire -> publisher only | 12 |
| 14 offline release verifier | independent release evidence owner; zero runtime authority | immutable proof bundle/signatures -> SHA-bound build-profile artifact | clean CI artifact -> independent release review | 1-13 |
| 15 canonical memory/shared-buffer maps | memory-domain registry owner | region/buffer DTOs -> checked sorted disjoint maps with stable IDs/digests | descriptor materialization only | 4-5 |
| 16 memory integration | canonical memory-attempt/effect owners | certificate + region ID + access -> pre-translation intent + post-translation verification | issue -> translation/cache/backing/fault path | 7,11-12,15 |
| 17 IOMMU/shared-buffer integration | device-context, mapping, submission and completion owners | certificate + device/PASID/VT/region/direction/sequence -> one-shot DMA intent/receipt | submit -> IOMMU -> device -> DMA completion | 5,7,15-16 |
| 18 hypercall internal backend | exact service/backend owner from a new execution RFC | certificate + typed args/grants -> request/receipt only | canonical service decode -> named owner; generic VMCALL excluded | 7,11-12,15-17 |
| 19 checkpoint/restore protocol | neutral migration, key and anti-replay owners | canonical state -> sealed manifest -> fresh binding/grants | separate future quiesce/checkpoint/restore/atomic-reopen path; denied for first contour | 4-5,12-18 |
| 20 product compiler emission RFC | compiler owner, future only | released runtime ABI -> proposed encoding/lowering contract | compiler pipeline only after a named limited runtime release | 14 plus named release |
| 21 nested execution RFC | explicit parent/child delegation owners, future only | parent certificate/grants -> child delegation/certificate model | separate nested composition root | separate future release gate |

### Bypasses, completion proof, replay and rollback

| Step | Forbidden bypass | Completion criteria and tests | Migration/replay requirement | Bounded rollback |
| --- | --- | --- | --- | --- |
| 1 | plan text, dirty `HEAD` or unavailable remote SHA as immutable evidence | clean build/test; exact SDK/config/filter/count; generator and artifact hashes | manifest versioned and immutable | remove manifest publication only |
| 2 | policy-return comparison only | paired SMT 1/2/4, FSP on/off, faults, branches, memory/I/O contention; equal architectural traces | deterministic replay digests | feature remains hard-off |
| 3 | caller enum, compiler hint, VMX bit or unknown default allow | exhaustive generated mapping; unknown/mismatch negatives | taxonomy/schema version recorded for future payloads | map every secure kind to deny |
| 4 | caller full descriptor, `context ?? request` or tag-only identity | one creator; duplicate/revoke/swap/stale tests | restore creates a fresh generation | revoke all bindings and return Disabled |
| 5 | scalar handle or owner boolean as authority | atomic mint/reserve/consume/revoke; cross-domain/nonce/stale tests | restore reissues; old handles stay invalid | bump ledger epoch and deny lookups |
| 6 | `...Validated` boolean, admission result or grant as certificate | forgery/VT/lane/bundle/replay/grant/effect-envelope tests | certificate never survives epoch/restore | stop issuer and drain attempts |
| 7 | scheduler reconstruction or descriptor copy | squash/FSP/SMT/source-working-physical identity tests | stale carrier denied after epoch change | remove edge and invalidate issuer generation |
| 8 | production API, shipped signer, runtime authority or handcrafted post-decode carrier | build/profile isolation, artifact inventory and public-decoder parity tests | fixture versioned; never restored | remove test profile/artifacts |
| 9 | direct SafetyVerifier/issue fixture bypass | real decode positive to expected deny; mutation/unknown/alternate-carrier negatives | replay produces same denial and fresh attempt ID | disable test profile only |
| 10 | success stub, test-only owner or effectful backend | one-shot transport receipt plus direct-call/forgery/duplicate/fault negatives; no state delta | receipt non-restorable | kill switch, drain/discard, epoch revoke |
| 11 | generic backend, memory/I/O or hidden compiler path | exact register result, fault/squash/no-partial-output and duplicate tests | pending attempts never restored | stop admission, drain/discard staged output |
| 12 | completion flag/fence without receipt or generic trap publication | no-receipt, duplicate-consume, order, fault and exactly-once retire tests | retired state only; pending completion not revived | discard pending before epoch bump |
| 13 | telemetry, descriptor JSON or compatibility alias as evidence | audience schemas and host-ID/pointer/backend leak negatives | recompute/sign after restore; no replay as authority | disable publisher without runtime change |
| 14 | Phase 22 booleans, self-certifying tests or generic wording | independent clean named-path bundle, signatures and rollback drill | artifact binds exact SHA/schema/profile | disable named profile, revoke epoch, drain/discard |
| 15 | first-match/most-specific implicit policy | overlap, duplicate, zero, overflow and permutation tests | canonical serialization stable | reject invalid maps at materialization |
| 16 | post-access check or compiler secure-load hint | fetch/load/store/atomic/cache/prefetch/fault/FSP cross-domain tests | pending attempts invalid after restore | deny new attempts and discard pending |
| 17 | buffer ID, host pointer, direct shared mapping or fence as authority | copy-in/out plus wrong device/PASID/VT/direction/epoch/replay/reset tests | reset/rebind invalidates sequences | revoke mappings and drain DMA |
| 18 | generic VMCALL/trap flags or raw private pointers | unknown ID/args/grant/replay/backend-fault tests | request/receipt one-shot; in-flight initially non-migratable | remove service registry edge |
| 19 | default allow, caller booleans, old handles or raw descriptor serialization | unknown/duplicate/omitted payload, interruption and rollback tests | monotonic anti-replay; atomic reopen; fresh grants | interrupted restore remains Disabled |
| 20 | product emission before named limited runtime release | future generated ISA diff, old-binary and no-hidden-authority tests | compiler artifacts versioned; runtime re-admits | restore product no-emission |
| 21 | child intent, VMCS12/02 or Shadow VMCS as authority | future depth/revocation-cascade/child-retire tests | child state independently versioned/revocable | no composition before its own release gate |

## Verification Record

Executed on 2026-08-06 with SDK `10.0.204`, configuration `Debug`:

| Slice | Exact filter | Result | Evidence class |
| --- | --- | --- | --- |
| SecureCompute policy/conformance/release corpus | `FullyQualifiedName~SecureComputeRefactoring` | 309 passed, 0 failed | direct policy tests plus separately identified source/doc guards; not production execution proof |
| SafetyVerifier and pipeline identity | `SafetyVerifierTests` or `Rf080RetireEffectIdentityFreezeTests` or `Rf124VtPipelineScoreboardIdentityInventoryTests` or `Rf112CoreIdentityAndCopySeamHardeningTests` | 46 passed, 0 failed | executable ordinary-pipeline identity regression; no SecureCompute certificate exists |
| VMX zero authority | `SecureComputeVmxPhase17NamedPositivePathZeroAuthorityTests` or `GuestCr0Cr4ReadOnlyProjectionTests` or `SecureComputeVmxDenialGuardTests` | 45 passed, 0 failed | executable compatibility denial/projection slice |
| compiler no emission | `SecureComputeCompilerPhase19ControlledEmissionGateTests` or `CompilerNoEmissionBoundaryTests` or `CompilerPhase09VmxSecureComputeNegativeMatrixTests` or `VmxCompilerIsaRuntimeNoEmissionContractTests` | 43 passed, 0 failed | executable negative compiler boundary; not a clean artifact-hash proof |

Scoped owner/path scans confirmed:

- the only production `_runtimeAdmission.Validate(...)` calls are the VMX compatibility VMREAD and trap projection handlers, and neither supplies a non-ordinary SecureCompute operation class;
- SafetyVerifier contains no `SecureCompute` or `SecureAdmissionCertificate` reference;
- completion/retire, migration/output, evidence and debug policies have declaration-only occurrences in production SecureCompute sources and no effect-root callers found in pipeline, memory or completion roots;
- `context.SecureCompute ?? request.SecureDescriptor`, generic `AllowedSecureOperation`, migration/checkpoint `_ => ...Allowed`, and hard-coded `runtimeOwnerMaterialized: true` remain present;
- no positive SecureCompute runtime/completion/retire/release authority literal was found in the scoped SecureCompute source tree.

Scoped `git diff --check` passed for the changed ActivationPlan and test files, and no trailing whitespace was found in the new/untracked plan and WhiteBook documents. Repository-wide `git diff --check` remains nonzero because unrelated pre-existing `Documentation/DocsRefactoring/audit.md` lines contain trailing whitespace. A full repository test run was intentionally not used as SecureCompute evidence in this heavily dirty cross-subsystem worktree.

## 2026-08-07 Verification Update

The current documentation update was checked with SDK `10.0.204`/MSBuild `18.3.3` while `global.json` requests `10.0.201`; results are therefore local regression evidence, not clean immutable release evidence.

| Slice | Result | Interpretation |
| --- | --- | --- |
| four directly affected plan/Phase 22 documentation guards | 4 passed | updated Phase 12/15/16 backlog wording and the new Phase 25 authority/order guard are consistent |
| Phase 20/21/22 classifier and release tests | 22 passed | pre-activation, negative-matrix and permanent deny behavior remain fail closed |
| ActivationPlan `Records*` documentation guards | 7 passed | existing plan status/gap assertions remain compatible with the reconciliation |
| VMX zero-authority plus compiler no-emission filters | 88 passed | compatibility remains zero-authority and product SecureCompute emission remains denied |
| SafetyVerifier/pipeline identity plus dirty VMX certificate prototype | 56 passed, 1 failed | `Rf124VtPipelineScoreboardIdentityInventoryTests` observed the user's untracked `SafetyVerifier.VirtualizationAdmission.cs`; this is dirty-worktree drift, not a SecureCompute certificate or a failure introduced by the plan edit |
| ISA generator `--check` | passed; 250 rows; digest `416607735e354770e0ec2707fe9b81ac3c3a7fccd344435e6dd8ccae6ebebdb0` | generated catalog is current in this worktree; clean-checkout determinism remains C2-open |

The broad `FullyQualifiedName~SecureComputeRefactoring` attempt built successfully but reported 295 passed and 15 failed. Eleven failures are `DirectoryNotFoundException` from the user's pre-existing deletion of `docs/ref2/SecureComputerefactoringNew`; the other four were affected documentation expectations and passed after scoped synchronization. The deleted unrelated corpus was not restored.

Current scoped reachability scans still find:

- no production definitions of `SecureAdmissionCertificate`, `SecureDomainRegistry`, `SecureGrantLedger`, `SecureExecutionRequest`, `SecureExecutionReceipt`, `SecureCompletionRecord` or `SecureComputeReleaseEvidenceVerifier`;
- production `RuntimeBoundaryAdmissionService.Validate` callers only in VMX compatibility handlers;
- no secure effect-policy caller in memory/DMA/IOMMU/backend/retire roots beyond the generic policy service;
- the dual full-descriptor carrier, hard-coded `runtimeOwnerMaterialized: true`, generic `AllowedSecureOperation` tail and checkpoint unknown-value default allow;
- Phase 22's final unconditional denial and compiler exclusion of `SecureComputePolicyAdmissionOnly` from production lowering.

The final Phase 22 source message still describes future release implementation inside the classifier. The reconciled plan treats that wording as an open guard-hardening item: future release approval must be a separate offline verifier, while the runtime classifier remains deny-only.

Final repository `git diff --check` and the scoped ActivationPlan/test check both returned zero. Git reported line-ending normalization warnings only; the new Phase 25 file has no trailing whitespace.

## Current Claim Boundary

Allowed now:

- Disabled baseline at admission level;
- descriptor/policy validation only as non-executing and non-publishing;
- read-only VMX compatibility projection with zero SecureCompute authority;
- compiler SecureCompute no-emission decision.

Not allowed now:

- positive backend execution;
- SecureCompute completion or retire publication;
- secure memory, DMA/IOMMU or hypercall effect claims;
- live checkpoint/restore or authoritative attestation claims;
- compiler secure emission;
- nested execution;
- limited or production release.

Therefore the maximum current statement is:

> SecureCompute Activation Plan актуализирован по текущему локальному коду и аудиту. Positive runtime execution и limited/production release остаются запрещёнными. Phase 18 остаётся future/design-fenced, Phase 20 — pre-activation evidence gate, Phase 21 — negative/future-gated conformance matrix, Phase 22 — fail-closed release gate.
