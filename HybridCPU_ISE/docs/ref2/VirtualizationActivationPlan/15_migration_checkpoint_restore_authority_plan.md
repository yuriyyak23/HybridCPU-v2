# Phase 15 - Migration Checkpoint Restore Authority Plan

Status: migration/evidence authority gate. No migration format is opened by this document.

## 2026-06-11 Audit Contract

- File name: `15_migration_checkpoint_restore_authority_plan.md`.
- Purpose: prevent migration/checkpoint/restore artifacts from becoming authority source.
- Status: migration/evidence gate; no migration format is opened by this document.
- Scope: migration classes, host-owned evidence exclusion, restore validation, rollback/no-state proof, completion/VMCS projection non-authority.
- No-goals: no VMCS projection metadata authority, no completion projection serialization as authority, no host evidence/native token/backend handle/debug trace migration.
- Code anchors: neutral checkpoint/restore anchors from closure corpus, `VmcsFieldProjectionSchema.cs`, `TrapCompletionPublicationFence.cs`, SecureCompute migration policy anchors.
- Authority owner: neutral migration/checkpoint descriptors, domain checkpoint images, restore validation services, and evidence policies; VMREAD output is not authority.
- Required RFC/ADR: every positive path must name migration class with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: unknown migration class blocks release; checkpoint-as-authority is `не доказано` and `должно оставаться denied`.
- Tests/static scans: VMCS projection checkpoint denial, completion recompute tests, lane token/telemetry migration denial, SecureCompute projection metadata denial, restore owner/evidence classification denial.
- Risks: serializing backend handles, native tokens, or compatibility projection metadata as guest state.
- Next-gate dependency: Phase 16 tests and Phase 18 release gate for any positive path.

## Phase Goal

Ensure that any limited runtime virtualization path has explicit migration/checkpoint/restore classification and cannot serialize host-owned evidence or compatibility projection as authority.

## Historical Baseline (2026-06-11)

The current corpus denies VMCS projection as migration authority. Completion-owned VMREAD fields are recomputed projection values. SecureCompute migration policies deny host-owned evidence, scheduler evidence, backend handles, native tokens, debug traces, VMCS projection metadata, compatibility projection metadata, raw secrets, active host pointers, and raw sealing keys.

The privileged execution-state descriptor classifies guarded `GuestCr0`/`GuestCr4` projection as `RevalidatedAfterRestore`. This means the neutral owner must be revalidated after restore; VMREAD output is not serialized authority and cannot reconstruct the owner.

## ISE-MIGRATION-PAYLOAD-AUTHORITY-15 - Closure Record

Closure date: 2026-06-18.

State: closed `DENIED/FUTURE-GATED BASELINE / NO-MIGRATION-PAYLOAD-AUTHORITY`.

This closure records the current migration/checkpoint/restore authority boundary only. It does not approve a migration format, does not classify an active VMCALL payload, does not make VMCS projection metadata, compiler artifacts, SecureCompute proof-only admissions, or lane/stream evidence checkpoint authority, and does not open restore, completion publication, or retire publication.

| Surface | Current result | Owner/value source | Evidence class | Migration class | Denial or boundary reason |
| --- | --- | --- | --- | --- | --- |
| VMCS projection metadata | denied as payload authority | none from compatibility projection | projection-denial evidence | `Denied` | `DeniedVmcsProjectionAuthority` / `DeniedCompatibilityProjectionAuthority`; projection metadata is not checkpoint state |
| compiler artifacts and generated examples | metadata/readiness only | compiler no-emission and controlled-emission gates | no-emission evidence | not migration authority | compiler output cannot be runtime, checkpoint, restore, completion, or retire authority |
| SecureCompute proof-only backend owner admission | proof-only/no-execution evidence | secure backend owner RFC gate | proof-only evidence | not checkpoint payload authority | `AllowedProofOnlyNoExecution` cannot become runtime execution, completion, retire, or migration payload authority |
| lane/stream evidence | denied as host-owned/native evidence | lane/stream runtime owners only | token/telemetry/replay evidence | `HostOwnedNonMigratable` or `Denied` | scheduler evidence, backend binding evidence, native token evidence, debug traces, telemetry, and replay evidence cannot become guest checkpoint state |
| completion-owned VMREAD projection | recomputed projection only | neutral completion owner when present | recomputed projection evidence | `RecomputedAfterRestore` only with proof | internal completion records are manifest coverage only, not checkpoint or restore authority |
| guarded `GuestCr0`/`GuestCr4` projection | owner revalidated after restore | privileged execution-state owner | read-only compatibility projection evidence | `RevalidatedAfterRestore` | VMREAD output is not serialized authority and cannot reconstruct the owner |
| SecureCompute output manifest entries | manifest/classification coverage only | secure runtime manifest policy | classification evidence | entry-specific, restore-validated | internal backend result and internal completion record are not checkpoint authority; recomputed-after-restore requires restore validation proof |

Closure invariants:

- VMCS projection metadata and compatibility projection metadata cannot be migration/checkpoint/restore authority.
- Compiler metadata, compiler artifacts, examples, goldens, and generated compatibility output are not payload authority.
- SecureCompute `AllowedProofOnlyNoExecution` and proof-only RFC states cannot become migration payloads.
- Lane6/Lane7/Stream tokens, backend bindings, telemetry, scheduler evidence, replay evidence, and debug traces are host-owned or recomputed evidence, not guest checkpoint state.
- Completion publication, if ever allowed by a neutral owner, still does not imply checkpoint payload authority or retire publication.
- `GuestCr0`/`GuestCr4` remain guarded read-only projection with `RevalidatedAfterRestore`; the projection value is not the restore source.
- Any future positive path must name exactly one migration class, excluded evidence classes, restore validation proof, owner/value source, capability policy, completion policy, retire policy, and denial reasons in an owner-specific RFC/ADR.

## Owner Of Authority

Neutral migration/checkpoint descriptors, domain checkpoint images, restore validation services, and evidence policies. VMCS projection and VMREAD output are not authority.

## What Can Be Implemented

- Migration class for the chosen VMCALL path.
- Tests that no host-owned evidence leaks into guest-visible or migration-visible state.
- Restore validation for any descriptor-owned state.
- Rollback/no-state proof for the recommended first leaf.
- Static scans for backend handles and tokens in checkpoint models.

## What Remains Denied/Future-Gated

- Migration of VMCS projection metadata as authority.
- Migration of completion projection values.
- Migration of `GuestCr0`/`GuestCr4` VMREAD projection output as authority; only the neutral descriptor's migration contract may participate, followed by restore revalidation.
- Migration of native backend handles, lane tokens, scheduler evidence, debug traces, host-owned evidence.
- Migration of SecureCompute private state without secure migration RFC.
- Nested migration through Shadow VMCS/VMCS12/VMCS02.

## Forbidden Shortcuts

- Treating checkpoint image as authority source.
- Restoring compatibility projection metadata into runtime state.
- Serializing backend handles or native tokens as guest state.
- Using VMREAD to reconstruct authoritative runtime state.

## Required RFC/ADR

Every positive activation path must name migration class. For a no-state VMCALL leaf, the RFC may choose `NoPayload`, but tests must prove it.

## Code Anchors

- Neutral checkpoint/restore anchors named in VMX refactoring closure corpus.
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Records/TrapCompletionPublicationFence.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/**` migration policy anchors.

## Documentation Anchors

- `HybridCPU_ISE/docs/ref2/Old/VirtualiztionRefactoringNew/14_documentation_migration_and_claim_hygiene.md`
- `Documentation/Virtualization WhiteBook/06_Capabilities_And_Evidence.md`
- `Documentation/Virtualization WhiteBook/16_Current_State_And_Closure_Matrix.md`
- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md`

## Required Tests

- VMCS projection cannot be checkpoint authority.
- Completion-owned fields are recomputed, not serialized.
- Privileged CR0/CR4 projection is revalidated after restore and VMREAD output is never the restore source.
- Lane6/Lane7 tokens and telemetry cannot migrate as guest state.
- SecureCompute VMX projection metadata cannot migrate as secure authority.
- First VMCALL leaf has `NoPayload` or explicit neutral migration payload.
- Restore denies stale or missing owner/evidence classification.

## Required Static/Source Scans

```powershell
rg -n "Checkpoint|Migration|Restore|Vmcs|CompletionRecord|VmxCompletionProjection|NativeToken|SchedulerEvidence|DebugTrace|Telemetry" HybridCPU_ISE/CloseToHSL/Core HybridCPU_ISE/NonRTL/Core
```

Matches in serialization code require classification and tests.

## Migration/Evidence Classification

Allowed classification values:

- `NoPayload`;
- `DescriptorOwned`;
- `RecomputedAfterRestore`;
- `MigrationSerializableGuestState`;
- `HostOwnedNonMigratable`;
- `Denied`.

The chosen path must define exactly one primary class and list excluded evidence classes.

## Completion/Retire Implications

Published completion or retire does not automatically become checkpoint state. If a retire effect changes persistent state, that state must be classified separately by the neutral owner.

For `PROBE_NO_STATE_V1`, Phase 38 fixes `OperationMigrationPolicy=DrainOnly` and `CompletionMigrationClass=HostOwnedNonMigratable`. A future domain-scoped drain gate closes new E2 issuance, drains/cancels work and proves live E2/E3/E5 plus pending E6 counts are zero before checkpoint. Live registries owned by the corresponding neutral services are authoritative; a lifecycle ledger is bookkeeping only.

Restore advances a neutral domain restore generation and invalidates E1 issuer generations, E2, E3, E5 and E6. O1 is reloaded from local accepted D2 rather than checkpoint; a SpecDigest/profile mismatch keeps virtualization denied. E1, operand snapshot, E2/E3/E5/E6, capability handles, owner seals and backend receipts are not serialized. This is consistent with current `DomainCheckpointImage`, which rejects host-owned runtime, scheduler, backend-binding and native-token evidence.

## Exit Criteria

- Migration class is explicit for every positive path.
- No host-owned evidence is serialized as guest state.
- Restore validation denies projection-as-authority.

## 2026-08-10 PR-I E7 Closure

The exact `PROBE_NO_STATE_V1` slice now implements `DrainOnly` through
`DomainHypercallDrainLifecycleOwner`. New E2 issuance closes before checkpoint;
the SafetyVerifier, executor, completion owner and retire owner registries must
each report zero live E2/E3/E5/E6. Cancellation removes those authorities from
their issuing registries. The checkpoint contains only DecisionId, SpecDigest,
domain, epoch and restore-generation identity under the versioned `HCPUE7V1`
digest; it contains no E1/O1 operand, token, receipt, seal, capability handle,
host evidence or compatibility projection.

Restore accepts that exact policy identity once, invalidates E1 admissions,
advances the neutral restore generation and reopens composition only after the
local Phase-38 SpecDigest is revalidated. Wrong profile, stale generation,
in-flight authority and duplicate restore deny. This closes E7 for the exact
slice only and creates no general migration format or checkpoint authority.

## Dependency On Previous/Next Phase

Depends on Phases 06-09 for the first active path and on Phase 12/13 if lanes or SecureCompute are involved. Phase 16 converts this into tests.
