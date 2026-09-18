# Phase 48 — Neutral Architectural Completion Commit And Observation Foundation

Status: `P48-A AND P48-B PROVENANCE CLOSED / FOUNDATION ONLY / NO VMREAD D2 / NO PRODUCTION VMREAD`.

Phase 48 is the only currently authorized candidate pool after the blocked
Phase 47 E0. Its purpose is to establish neutral architectural-completion
commit evidence and an explicit per-field presence/semantic-validity substrate,
but only where the canonical runtime architecture supports them. It does not
materialize or accept
`D2-HV-VMREAD-SCALAR-DELIVERY-V1-CURRENT-COMPLETION-0004` and does not authorize
production VMREAD.

## P48-A — Canonical Completion Commit Evidence

Before implementation, E0 must identify the actual canonical retire/order
linearization boundary, its production callers, the completion producers that
reach it, and the existing domain/context/VT, attempt/event, owner-epoch, order,
restore and replay bindings. A test-only or VMX-specific seam is not sufficient.

An `ArchitecturalCompletionCommitOwner`, or an equivalent neutral owner, may be
introduced only at that proven boundary. It is not a VMX, VMREAD, trap, backend,
VMCALL or VMCS owner. `RetireCoordinator` must not be broadened into a generic
completion store merely to support a future VMREAD projection; completion
commit integrates with canonical retire-window ordering as a separate neutral
authority.

Only the canonical commit owner may issue an opaque, nonforgeable
`ArchitecturalCompletionCommitReceipt`. The receipt must be issuer-sealed,
live-registry-backed, single-use where consumption is required, and bound to:

- exact completion identity;
- producer owner identity and owner epoch;
- domain, virtualization context and VT;
- attempt and event identity;
- completion class and completion digest;
- canonical order sequence and commit sequence;
- restore generation.

Route/fence booleans, caller-provided `CompletionRecord`, compatibility-created
`CompatibilityExit`, VMCALL E5/E6, and test-created objects are not commit
evidence. `CompletionRecordClass` alone proves neither origin nor eligibility.
Every eligible producer must be explicitly registered by exact owner identity
and policy. No producer or class is opened automatically.

P48-A closes only after forged, stale, replayed, duplicate, cross-owner,
cross-domain, cross-context, cross-VT, cross-attempt/event, cross-class/digest,
cross-order/commit and pre-restore receipts are denied; ineligible and
unregistered producers are denied; canonical order/commit linearization and
restore invalidation are race-tested; and compatibility, trap-fence and VMCALL
shortcuts remain unreachable as authority.

If no canonical architectural completion commit point can be proven without
VMX-specific or test-only state, P48-A closes `BLOCKED` without a surrogate
owner, registry or receipt, and P48-B does not open.

## P48-B — Neutral Current-Completion Observation

P48-B opens only after a green, provenance-closed P48-A. A
`DomainCompletionObservationOwner`, or equivalent neutral owner, may then be
introduced solely as a downstream read-only observation owner for an already
architecturally committed neutral completion.

The current observation is scoped by exact domain plus virtualization
context/VT. Architectural commit and installation of the new observation
snapshot must share one linearization point: there must be no interval in which
the new completion is committed while observation still exposes the preceding
snapshot.

The owner maintains a runtime-owned, non-zero, monotonic
`CompletionGeneration`. It advances on every newly committed completion,
explicit clear/invalidation, restore, domain/context rebind, and observation
owner replacement. A caller cannot supply either the current generation or the
current completion.

The snapshot stores neutral facts, never `VmExitReason`,
`VmxExitQualification`, VMCS fields or precomputed VMREAD values. It has an
explicit presence contract that distinguishes absence from a legal scalar zero
for:

- reason;
- qualification;
- fault address;
- fault auxiliary data.

Address and auxiliary facts also carry neutral semantic classifications.
Future VMX mapping may use `ExitReason` only from a present admitted reason,
`ExitQualification` only from a present qualification valid for the exact
producer/class, `GuestPhysicalAddress` only from a present neutral
guest-physical address, and `EptViolationQualification` only from present
auxiliary data classified as a second-stage translation violation. Absence or a
semantic mismatch is an explicit denial; successful-zero fallback is forbidden.

Observation migration is `RecomputedCompletion`. The snapshot,
`CompletionGeneration` as authority, commit receipts, seals and any future
VMREAD receipts are not serialized. After restore, current observation is
absent until a new canonical completion is committed.

P48-B closes only after atomic commit/install races, generation replacement,
clear, restore, rebind and owner-replacement races, two-domain and cross-VT
isolation, absent-versus-zero cases, semantic-class mismatch cases, and
serialization exclusions pass. No future VMREAD projection may reread or infer
facts outside the neutral snapshot.

## Forbidden Scope

Phase 48 must not:

- create a VMREAD-specific current-completion registry;
- use `CompletionRecord.FromCompatibilityExit` as a canonical source;
- turn `TrapCompletionPublicationFenceResult` into nonforgeable authority;
- reuse VMCALL E5/E6 or their owner/receipt semantics;
- accept any `CompletionRecordClass` implicitly;
- create VMREAD SpecV2, AcceptanceRecordV2, scalar-result receipts,
  PRF/rename/writeback delivery or production VMREAD composition;
- open adjacent VMREAD fields, VMWRITE, broad VMX activation, compiler changes,
  nested virtualization, SecureCompute, memory/IOMMU/I/O/device effects, or
  lane/stream expansion.

## Iteration, Verification And Provenance

P48-A and P48-B are separate sequential boundaries. Before each boundary,
recheck HEAD/tree/status, dirty and ignored/untracked dependencies, canonical
production callers and reachability, authority/value/admission ownership,
restore and replay semantics, tests, guards and machine-readable status. Make
the smallest change, run focused positive/negative/race tests and forbidden
scans, then run the full VMX and SecureCompute matrices and a Release build
without test hooks. Do not open P48-B if P48-A exit criteria fail.

Subject and later non-self-referential evidence commits are permitted separately
for P48-A and P48-B only after their gates are green. Evidence must identify the
already existing subject SHA/tree, source hashes, exact commands/results,
toolchain, activation/rollback state and forbidden scans. Existing unrelated
worktree changes must not be staged or committed. Push is forbidden.

After successful P48-A and P48-B, the completion-owned field group remains
closed. A new, separate bounded E0/D2 authorization is required before any
SpecV2 or AcceptanceRecordV2 for exact `ExitReason + ExitQualification +
GuestPhysicalAddress + EptViolationQualification` may be created.

## 2026-06-11 Audit Contract

- File name: `48_neutral_architectural_completion_commit_and_observation_foundation.md`.
- Purpose: Authorize only a neutral architectural-completion commit-evidence foundation and its downstream read-only observation substrate.
- Status: Authorized bounded, not started; P48-B remains closed until a green provenance-closed P48-A.
- Scope: Canonical completion commit evidence, exact producer registration, explicit field presence and neutral semantic classification only.
- No-goals: No VMREAD SpecV2, AcceptanceRecordV2, scalar-result receipt, production VMREAD, VMWRITE, compiler, nested, SecureCompute, memory/IOMMU/I/O/device/lane/stream expansion.
- Code anchors: Canonical retire/window ordering, `RetireCoordinator`, completion records and producers, domain/context/VT lifecycle, restore and replay contours.
- Authority owner: A neutral canonical completion commit owner may be created only at a proven production architectural-order boundary; VMX, VMREAD, VMCS, trap, backend and VMCALL are prohibited authorities.
- Required RFC/ADR: P48-A E0 must prove the canonical production commit point and eligible producer identities before implementation; a separate later E0/D2 decision is required for any VMREAD projection.
- Acceptance criteria: Issuer-sealed live-registry-backed exact-bound receipts, restore/replay invalidation, exact-once semantics, and only after P48-A green an atomic domain/context/VT observation with non-zero monotonic generation and explicit presence/semantic validity.
- Tests/static scans: Focused owner/receipt/observation/restore/race tests, VMX and SecureCompute matrices, Release build without test hooks, caller/reachability review, dirty-source dependency check and forbidden-authority scans.
- Risks: A caller record, fence boolean, compatibility factory, VMCALL receipt, test object or successful-zero fallback becoming surrogate completion authority.
- Next-gate dependency: Separate bounded E0/D2 authorization after successful P48-A and P48-B; no completion-owned VMREAD field group opens automatically.

Owner map completeness remains unresolved at: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.

## P48-A E0 And Implementation Closure

P48-A E0 proved the production linearization point in
`PipelineStage_WriteBack`: the selected older prefix is fully prevalidated,
immediate effects are applied, `FinalizeWriteBackRetireWindow` applies late
effects and redirects, and only then may the neutral completion owner commit;
the existing retire-visibility contour certificate is published afterwards.
The production caller is
`CommitArchitecturalCompletionAtCanonicalRetireBoundary`, reached from that
finalizer. `RetireCoordinator` remains unchanged and owns only its existing
register/PC retire records.

The only registered P48-A producer is the exact
`CanonicalPipelineTrapEntryProducer`. Its candidate is captured from a live
`PostStageBIssuedAttempt` and matching `TrapEntryEvent`; domain, context, VT,
physical lane, working bundle/slot and operation attempt must agree before the
candidate reaches the commit boundary. A completion class by itself is never
eligible.

`ArchitecturalCompletionCommitOwner` issues an opaque
`ArchitecturalCompletionCommitReceipt` only after exact live producer-policy
validation. The receipt is HMAC sealed, live-registry backed and bound to the
completion identity, producer owner identity/epoch, domain/context/VT,
attempt/event, neutral class/digest, canonical order, commit sequence and the
shared runtime restore generation. Duplicate identity is rejected independent
of caller-supplied facts; exact consumption is single-use; restore invalidates
the registry and advances generation.

Focused positive, negative and race tests passed 7/7; the restore/duplicate
race test passed in ten repeated process runs. The full VMX refactoring matrix
passed 462/462, SecureCompute passed 395/395, and the Release production build
without test support or internal hooks passed with zero errors. Forbidden scans
found no compatibility factory, trap-fence, VMCALL E5/E6, completion-record,
VMREAD/VMCS, backend, VMWRITE or adjacent-subsystem authority in the new owner;
`RetireCoordinator` is unchanged.

This is an implementation closure only until the later non-self-referential
evidence commit records the existing subject SHA/tree and source hashes. P48-B
remains closed until that provenance closure. No observation owner,
`CompletionGeneration`, VMREAD decision/spec/acceptance/receipt or production
VMREAD composition exists at this boundary.

## P48-A Provenance Closure

The existing P48-A implementation subject is
`03ecefb3c155c161b9b7516bfa2fe2609628693c`, tree
`029b74560a014bc50b460e920444eeb6cbc4601d`, parent
`8c1df83d39d151cf67fe161c895639b9e7c5b5a0`. A detached clean worktree at
that exact subject passed VMX 450/450, SecureCompute 395/395, Release/no-hooks,
ten independent race-test process runs, `git diff --check`, caller review and
forbidden scans. It contained zero ignored or untracked C# dependencies under
`CloseToHSL`.

The later evidence record is
`evidence/2026-08-12-phase48a-neutral-architectural-completion-commit-clean-evidence.json`.
It is non-self-referential and records the already existing subject SHA/tree,
clean-worktree source hashes, exact commands/results, toolchain, rollback and
migration state, and forbidden authority denials.

P48-B is therefore authorized as the next bounded subphase. It is not yet
implemented at this provenance boundary. No observation owner or
`CompletionGeneration` exists here, and no VMREAD D2 field group is opened.

## P48-B Implementation Closure

`DomainCompletionObservationOwner` is the neutral downstream read-only owner.
It is constructed with a private runtime `CompletionGenerationAuthority` and
is registered with exactly one `ArchitecturalCompletionCommitOwner`. The
commit owner alone holds the exact live installer capability. During the
canonical commit operation it creates the receipt binding, installs the
observation snapshot, and only then publishes the live receipt; no externally
visible committed receipt can coexist with the preceding observation.

Snapshots are keyed by exact domain, context and VT. They store only neutral
completion identity/provenance/order facts and `NeutralArchitecturalCompletionFacts`.
They contain no VMX exit enum, VMCS field, VMREAD value, receipt or seal. The
runtime-owned non-zero generation advances on every commit, clear, restore,
rebind and observation-owner replacement. Callers cannot provide a current
completion or current generation.

Reason, qualification, fault address and fault auxiliary data each carry
explicit presence independent of the scalar value. Legal zero remains present.
Address and auxiliary facts retain their neutral semantics. Neutral field
reads deny absent qualification/auxiliary data and deny a virtual-address fact
when a guest-physical address is requested; only an exact registered
translation-fault producer policy may install guest-physical and second-stage
translation-violation facts.

Restore clears every snapshot before any later observation and advances both
restore and observation generations. Clear/rebind affect the exact scope;
owner replacement invalidates the old owner, starts the replacement empty and
advances the shared generation. Migration classification is
`RecomputedCompletion`; snapshots, generation authority, receipts and seals
have no serialization contour.

Focused P48-A/P48-B tests passed 18/18 before status closure. The full VMX
matrix passed 473/473, SecureCompute passed 395/395, Release/no-hooks passed
with zero errors, and the combined commit/clear/restore/rebind/replacement race
test passed ten independent process runs. Forbidden scans found no observation
dependency from VMREAD or the compatibility frontend, no projection/backing
store vocabulary in the owner, no serialization path and no adjacent
subsystem expansion.

P48-B remains provenance-pending until a separate subject commit and later
non-self-referential evidence commit are created and verified from clean
source. No SpecV2, AcceptanceRecordV2, scalar-result receipt, PRF delivery,
production completion-owned VMREAD or VMWRITE is authorized.

## P48-B Provenance Closure And Stop Boundary

The existing P48-B subject is
`a751264e73ff73cf924b5559e72b7a1582c25cf9`, tree
`988b58397195939118f4c55bc3de41e67e9bc742`, parent
`63246d03ef1100fec5333c6e1035dcf4660d08c5`. Its clean detached verification
passed VMX 461/461, SecureCompute 395/395, Release/no-hooks, ten independent
race-test runs, `git diff --check`, serialization scans, production
reachability review and forbidden scans. It contained zero ignored or
untracked canonical C# dependencies.

The later non-self-referential record is
`evidence/2026-08-12-phase48b-neutral-current-completion-observation-clean-evidence.json`.
It pins the already existing subject SHA/tree, source hashes, commands/results,
toolchain, migration/rollback state and all forbidden authority denials.

Phase 48 stops here. The only next admissible pool is a new separate bounded
E0/D2 authorization decision for the exact `ExitReason + ExitQualification +
GuestPhysicalAddress + EptViolationQualification` group. That pool is required
but is not authorized by Phase 48. No production VMREAD, SpecV2,
AcceptanceRecordV2, scalar-result receipt, PRF delivery, VMWRITE or adjacent
activation may be inferred from this closure.
