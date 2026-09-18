# Layer 1 Secure Migration Plan

## Purpose

Документ проектирует `SecureMigrationDescriptor` для checkpoint/restore secure domains. Цель - сохранить правило: checkpoint/migration переносит guest-visible state/policy, но не VMCS projection authority, host-owned evidence, raw secrets или active host pointers.

## Scope

План покрывает checkpoint payload classes, anti-rollback, epoch model, restore validation, host evidence recomputation, guest-visible state/policy preservation and rejection of compatibility projection metadata as authority.

## Non-goals

Не проектируются binary format, cryptographic sealing implementation, transport protocol or live migration implementation. Не добавляется VMCS image authority и не сериализуются raw sealing keys.

## Architectural invariants

Migration owner remains `MigrationValidationPolicy`, `DomainCheckpointImage` and `RestoreValidationService`. `SecureMigrationDescriptor` refines allowed payloads for secure domains.

VMCS projection, VMREAD values and compatibility metadata are not authoritative migration state. Completion-owned compatibility fields remain recomputed from neutral `CompletionRecord` projection only and must not become checkpoint payload class.

## Implementation status

- [x] Phase 5 baseline closed on 2026-05-30: `SecureMigrationDescriptor` now carries measurement/grant restore policy classes in addition to mode, policy epoch and private-memory migration policy.
- [x] Secure restore admission exists: `SecureMigrationAdmissionPolicy` rejects missing/disabled policy, policy epoch rollback, stale measurement epoch, stale grant epoch, wrong restore policy and missing revalidation/re-attestation.
- [x] Secure checkpoint payload classes are explicit: host-owned evidence, VMCS projection metadata, compatibility metadata as authority, debug traces, active host pointers, raw measurement secrets and raw sealing keys are denied.
- [x] Private secure memory payload requires an explicit sealed/encrypted payload contract descriptor with neutral key owner, evidence policy and restore validation proof; no cryptographic sealing implementation or live migration transport is opened.
- [x] VMCS/VMREAD/compatibility metadata authority source guards exist in SecureCompute refactoring tests.
- [x] Phase 6 dependency closed on 2026-05-30: secure I/O/hypercall policy changes participate in measurement policy digest, and backend binding/compat metadata remain denied as authority.
- [x] Phase 7 first safe pool closed on 2026-05-31: migration restore now rejects grant handles without provenance, stale grant epochs and restored handles that were not rederived/revalidated.
- [x] Phase 8 VMX compatibility boundary closed on 2026-05-31: VMCS checkpoint metadata, `VmxCaps` descriptor materialization and projection-to-backend-success shortcuts remain denied.
- [x] Phase 9 nested secure domain design fence closed on 2026-05-31: nested child migration payloads cannot exceed parent policy, stale parent/child epochs fail admission and nested checkpoints reject VMCS12/VMCS02 authority.
- [x] Next dependent work closed: Phase 10 release gate closed on 2026-05-31 with doc/source conformance hardening and production-claim audit.
- [x] Post-Phase10 owner/RFC proof gate closed on 2026-05-31: neutral backend owner proof, approved RFC/ADR requirement and negative tests are covered without opening runtime execution.
- [x] Future runtime-execution decision transferred to `Plan2/14-securecompute-open-decision-backlog.md`: production secure backend execution remains unopened pending a separate implementation phase and decision record.

## Proposed descriptors / policies

`SecureMigrationDescriptor` should describe:

- `MigrationMode` - disabled, local-only, reattest-required, policy-compatible, non-migratable;
- `PolicyEpoch` - anti-rollback and stale-policy guard;
- `SecureMemoryPayloadClass` - private sealed, shared explicit, measured recompute, runtime mutable dirty-tracked, denied;
- `EvidencePayloadClass` - guest-visible serializable, recompute, host-owned denied, debug denied;
- `CompatibilityProjectionClass` - recomputed only, never authority;
- `MeasurementRestorePolicy` - reuse handle, revalidate, remeasure, reattest, denied;
- `GrantRestorePolicy` - preserve typed grants, rederive, deny stale epoch;
- `RollbackWindow` - default none for secure domains;
- `Provenance` - source domain tag, policy digest, measurement epoch, memory epoch, grant epoch.

Payload classes:

- guest-visible architectural state;
- secure domain policy descriptors;
- allowed secure memory payloads;
- migration-serializable evidence;
- recomputed host-owned evidence markers;
- denied classes: VMCS projection, compatibility metadata as authority, host evidence, scheduler evidence, backend binding evidence, native token evidence, debug traces as guest state, active pointers, raw secrets/sealing keys.

Private memory migration default:

- private secure memory migration is denied by default;
- opening private memory migration requires a separate sealed/encrypted payload contract, key/sealing ownership model, evidence policy and restore validation tests;
- until that contract exists, private memory is either non-migratable or must be rebuilt/reinitialized according to explicit policy.

## Integration points

Restore must rebuild/recompute host-owned evidence after neutral policy validation. It must not restore host-owned evidence as if it were guest state.

`RuntimeBoundaryAdmissionService` should use migration policy for domain enter after restore, nested restore and any operation depending on restored secure grants.

`SecureEvidencePolicy` and `DomainMeasurementDescriptor` must agree with migration classification. If measurement policy requires re-attestation, restore cannot enter secure domain until revalidation succeeds.

## No-regression requirements

- `DomainCheckpointImage` remains neutral checkpoint owner.
- VMCS-shaped checkpoint image must not reappear.
- Compatibility projection metadata remains rejected as authority.
- Host evidence is not serialized as guest/domain architectural state.
- Scheduler evidence, backend binding evidence, native token evidence and debug traces are rejected as checkpoint payload unless a future neutral owner classifies them explicitly outside guest state.
- Active host pointers and raw sealing keys are never payload.
- Existing migration/evidence proofs for recomputed compatibility fields remain valid.

## Tests and conformance

Required tests:

- host evidence rejected from secure checkpoint;
- VMCS projection rejected;
- compatibility metadata rejected as authority;
- wrong restore policy rejected;
- secure epoch rollback rejected;
- stale grant epoch rejected;
- restore requires revalidation/re-attestation if policy says so;
- completion-owned compatibility fields recomputed, not restored;
- private memory payload denied unless migration policy explicitly allows sealed class;
- private memory payload denied until sealed/encrypted payload semantics exist;
- debug traces rejected as guest state.

## Closure criteria

The phase closes when secure migration classes are explicit, restore validation is fail-closed for missing/stale policy and tests prove that VMCS/compatibility artifacts cannot become source of truth.

## Forbidden shortcuts

- Do not serialize VMCS projection state.
- Do not treat `VmcsReadOnlyValueProjectionService` output as checkpoint payload.
- Do not store raw sealing keys.
- Do not open private secure memory migration as plain serialized memory.
- Do not restore host evidence as guest-visible state.
- Do not ignore epoch rollback for convenience.

## Open questions

- Resolved 2026-05-30: Phase 5 admits only a descriptor-level sealed/encrypted private-memory payload contract with sealed payload, encrypted payload, neutral key owner, evidence policy and restore validation proof. This is a baseline contract, not production cryptographic sealing.
- Resolved 2026-05-30: migration `PolicyEpoch` is the restore admission epoch for Phase 5. Measurement and grant epochs must match it; divergence is fail-closed as stale measurement or stale grant.
