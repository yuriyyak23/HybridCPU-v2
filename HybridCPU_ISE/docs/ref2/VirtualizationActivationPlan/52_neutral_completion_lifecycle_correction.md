# Phase 52 — neutral completion lifecycle correction

Status: `CLOSED GREEN / SUBJECT AND LATER NON-SELF-REFERENTIAL EVIDENCE`.

## Authorization and bounded scope

The repository owner explicitly authorized correction of the existing neutral
completion lifecycle and, only after this boundary closes, one separately
audited neutral CPU instruction fetch/load/store translation-fault producer.
This authorization removes the Phase 51 producer-authorization blocker. It does
not authorize VMX/VMCS/VMREAD runtime authority, a completion VMREAD D2,
SpecV2, AcceptanceRecordV2, VMREAD composition, VMWRITE, broad activation,
nested/IOMMU/device authority, SecureCompute semantics, or compiler changes.

This phase changes only the existing neutral completion foundation:

- mandatory completion candidates are prevalidated before any retire-visible
  late effect;
- one owner-held canonical retire-window lease linearizes prevalidation, late
  effects, completion installation, receipt issuance, and final retire
  visibility;
- denial is fail-fast before architectural late effects, so a denied mandatory
  completion cannot accompany a published retire-visible effect;
- restore advances restore identity and atomically clears observations, live
  receipts, replay identities, and latest-receipt state;
- exact-scope clear/rebind revokes the matching observation, receipt, and replay
  identity while preserving other scopes;
- architectural state replacement revokes the old observation owner and all
  live receipt/replay state before fresh execution state is installed.

## 2026-06-11 Audit Contract

- File name: `52_neutral_completion_lifecycle_correction.md`.
- Purpose: Close the bounded lifecycle, commit-denial, stale-observation, stale-receipt, restore, rebind, and owner-replacement gaps identified by Phase 51.
- Status: Implemented and green in the worktree; subject and later non-self-referential evidence provenance pending.
- Scope: Existing neutral architectural completion commit and observation owners and their canonical retire/architectural-state lifecycle callers only.
- No-goals: No CPU translation-fault producer in this phase, VMX/VMCS authority, completion VMREAD D2/composition, VMWRITE, compiler, nested/IOMMU/device, or SecureCompute semantic change.
- Code anchors: `ArchitecturalCompletionCommitOwner.cs`, `DomainCompletionObservationOwner.cs`, `CPU_Core.PipelineExecution.Retire.cs`, `CPU_Core.StateData.cs`, and `CPU_Core.State.cs`.
- Authority owner: Existing `ArchitecturalCompletionCommitOwner`, `DomainCompletionObservationOwner`, canonical WB retire boundary, and architectural state lifecycle only.
- Required RFC/ADR: The repository-owner bounded authorization is recorded here; producer E0 remains separate and completion-backed VMREAD still requires later separate authorization.
- Acceptance criteria: Denial before retire-visible effects, atomic restore invalidation, exact-scope clear/rebind, old-owner revocation, production callers, repeated races, cross-matrices, Release, forbidden scans, and clean provenance.
- Tests/static scans: Phase 52 focused lifecycle/race tests, Phase 48/51 guards, full VMX and SecureCompute matrices, Release without test hooks, ignored/untracked C# scan, forbidden authority scan, and `git diff --check`.
- Risks: Holding a lease across late effects, accidental lifecycle interleaving, stale receipt/snapshot survival, mistaking compatibility vocabulary for authority, or opening producer/VMREAD work early.
- Next-gate dependency: Subject and later evidence provenance, then separate neutral CPU instruction translation-fault producer E0 only.

Owner map completeness remains unresolved at: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.

## Production reachability and linearization

The canonical retire caller is
`CPU_Core.PipelineExecution.Retire.cs::FinalizeWriteBackRetireWindow`:

1. retired WB lanes are cleared;
2. `PrepareCanonicalRetireWindow` validates every mandatory candidate;
3. late architectural effects and redirect are applied while the lease holds
   the completion owner lock;
4. the prepared candidates are committed;
5. only then is the retire-visibility contour certificate published.

The existing production lifecycle seams are:

- `PrepareExecutionStart -> ResetExecutionStartPcState ->
  ReplaceObservationOwnerAfterArchitecturalStateReplacement` for full fresh
  execution/owner replacement;
- `RestoreVectorContext -> InvalidateAfterRestore` for architectural vector
  context restore.

No separate domain-rebind manager was introduced. No suitable distinct
production domain-rebind owner exists in the scoped caller graph; exact-scope
`ClearObservation`/`RebindObservation` remain operations of the existing
neutral owner and are covered by focused isolation tests.

## Receipt, observation, replay, and race contract

The receipt remains issuer-sealed, live-registry backed, exact-owner/domain/
context/VT/attempt/event/class/digest/order/commit/restore bound, and exactly
once consumable. Observation remains neutral, read-only, exact-scope, and
`RecomputedCompletion`; snapshot, generation, receipt, and seals remain
non-serialized. Owner replacement makes the prior observation owner inactive.
Restore cannot interleave with a prepared retire window, and no post-restore
snapshot exists until a new completion commits.

Focused negatives cover rejected duplicate batches before publication,
absent-versus-committed publication, exact-scope isolation, replay identity
revocation, inactive replaced owners, stale receipts, and restore/commit races.

## Verification

- focused lifecycle/Phase 48/Phase 51/SecureCompute guard: `49/49`;
- documentation/status/lifecycle guard: `77/77`;
- lifecycle races: five runs of `2/2`;
- full VMX matrix: `499/499` in the current worktree and `481/481` on the exact clean staged tree;
- SecureCompute exact-name cross-matrix: `404/404` in the current worktree and `395/395` on the exact clean staged tree;
- Release without test hooks, established zero-warning gate profile: `0 errors / 0 warnings`;
- Release without warning suppression: `0 errors / 43 pre-existing out-of-pool warnings`;
- canonical ignored C#: `0`; canonical untracked C#: `0`;
- lifecycle diff forbidden-authority scan: no matches;
- `git diff --check`: passed.

## Verdict and next candidate

The neutral lifecycle correction is implemented, gate-green, and closed by
subject `2246049040fe61721a00e8535ed925c63cf587dc`, tree
`3a59c1b2c6eb838ca26d38c0fb52f15b562b0828`, and the later
non-self-referential Phase 52 evidence record. It grants no compatibility
authority.

The only next candidate is the separately authorized neutral CPU instruction
fetch/load/store translation-fault producer E0. E0 must first prove the existing
canonical CPU translation and precise-fault arbitration owner, production
reachability, deterministic older/younger and same-window selection, exact
domain/context/VT/attempt/event provenance, and whether the existing operation
identity is unique. Completion-backed VMREAD remains closed after producer
closure pending a separate exact field-coverage audit and authorization.
