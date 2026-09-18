# Layer 1 Secure Evidence And Publication Plan

## Purpose

Документ проектирует `SecureEvidencePolicy` и правила publication для SecureCompute Layer 1. Цель - сохранить evidence-centric visibility, отделить completion от retire publication и предотвратить leakage host-owned evidence.

## Scope

План покрывает host-owned evidence quarantine, guest-visible evidence, migration-serializable evidence, compatibility alias evidence, secure completion fence, retire publication rule и VMREAD visibility constraints.

## Non-goals

Документ не открывает новые VMREAD fields, не делает VMREAD backend, не проектирует debug trace serialization и не меняет существующую retire model для ordinary domains.

## Architectural invariants

Evidence owner остается `EvidencePolicyDescriptor` and neutral evidence services. `SecureEvidencePolicy` is a narrowing layer over `EvidencePolicyDescriptor`: it may further restrict visibility for secure domains, but it must not grant visibility denied by the base evidence policy and must not become a second independent evidence authority.

Completion publication and retire publication remain separate. A secure operation may have an internal completion record, but guest-visible publication requires fence and policy. Retire side effects require retire-owned publication rule and cannot be inferred from compatibility projection.

## Implementation status

- [x] Phase 4 handoff closed on 2026-05-30: attestation evidence publication now requires `SecureEvidencePolicy`, neutral `EvidencePolicyDescriptor` and `SecureCompletionPublicationFence`; host-owned/recomputed measurement evidence is denied from guest-visible publication.
- [x] Checkpoint/evidence guard baseline exists: host-owned evidence and raw measurement secrets are denied checkpoint payload classes.
- [x] Phase 4.5 closed on 2026-05-30: compatibility alias evidence, completion-vs-retire separation, admitted-denied VMCALL publication, Lane6/Lane7 sideband visibility and stale evidence replay/rollback are covered by `SecureEvidencePublicationPolicy` and targeted conformance tests.
- [x] Phase 5 dependency closed on 2026-05-30: restore revalidation/reattest admission and sealed/encrypted private-memory payload contract rules are wired as migration policy guards.
- [x] Phase 6 dependency closed on 2026-05-30: secure I/O and hypercall publication now requires neutral evidence approval plus completion and retire publication fences; admitted-denied hypercalls still cannot publish backend success.
- [x] Phase 7 first safe pool closed on 2026-05-31: runtime descriptor/grant monotonicity baseline now validates provenance, authority bounds and current epochs without CHERI-like ISA work.
- [x] Phase 8 VMX compatibility boundary closed on 2026-05-31: secure-sensitive VMX projection paths are denied by default unless neutral owner, read-only source, secure visibility, migration classification and conformance proof all exist.
- [x] Phase 9 nested secure domain design fence closed on 2026-05-31: nested parent/child evidence remains separated, host-owned evidence leakage is denied and Shadow VMCS/VMCS12/VMCS02 cannot satisfy nested secure authority.
- [x] Next dependent work closed: Phase 10 release gate closed on 2026-05-31 with doc/source conformance hardening and production-claim audit.
- [x] Post-Phase10 owner/RFC proof gate closed on 2026-05-31: neutral backend owner proof, approved RFC/ADR requirement and negative tests are covered without opening runtime execution.
- [x] Future runtime-execution decision transferred to `Plan2/14-securecompute-open-decision-backlog.md`: production secure backend execution remains unopened pending a separate implementation phase and decision record.

## Proposed descriptors / policies

`SecureEvidencePolicy` should classify evidence:

- `HostOwned` - visible only to host/runtime audit, never guest architectural state, never checkpoint payload as authority.
- `GuestVisible` - explicitly allowed to be observed by the domain.
- `MigrationSerializable` - allowed checkpoint payload class, still subject to migration policy.
- `CompatibilityAlias` - may be projected through VMX/VMCS-like vocabulary only under explicit compatibility projection policy.
- `RecomputedAfterRestore` - not serialized; rebuilt from neutral owner after restore.
- `DebugOnly` - visible only under measured-debug/debug policy.
- `Denied` - must not be published.

Policy fields:

- `DefaultEvidenceVisibility`;
- `CompatibilityAliasVisibility`;
- `HostEvidenceQuarantineRequired`;
- `CompletionFenceRequired`;
- `RetirePublicationRequired`;
- `MigrationEvidenceClass`;
- `DebugEvidenceClass`;
- `ReplayConformanceClass`.

Composition rule:

- base `EvidencePolicyDescriptor` denial wins over `SecureEvidencePolicy`;
- `SecureEvidencePolicy` may only narrow host-owned, guest-visible, migration-serializable, compatibility-alias and debug evidence classes;
- if the two policies disagree about visibility, migration or compatibility projection, admission must fail closed.

## Integration points

`RuntimeBoundaryAdmissionService` should require evidence policy for secure-domain operations that create evidence, expose evidence, publish completion, publish retire effects or project compatibility aliases.

`TrapCompletionRouteService` and completion routing must use secure completion fence for secure-domain trap/hypercall/IO paths. A completion record is not sufficient for retire side effects. Secure completion fence and retire publication rule are separate gates; passing one must not imply the other.

`VmcsReadOnlyValueProjectionService` must remain denied for secure-sensitive fields unless:

1. generated schema owner exists;
2. neutral owner provides read-only value source;
3. runtime admission allows read compatibility projection;
4. `SecureEvidencePolicy` allows compatibility alias visibility;
5. migration classification is defined;
6. conformance tests prove no authority leak.

## No-regression requirements

- Host-owned evidence must not leak into guest/domain architectural state.
- Compatibility alias evidence remains denied unless explicit policy exists.
- Completion publication before fence is denied.
- Retire publication remains separate from completion and compatibility projection.
- Completion success cannot be treated as retire publication.
- `GuestCr0`, `GuestCr4`, `HostCr3`, host execution aliases and compatibility-control fields remain denied.
- Existing VMREAD admitted slices do not gain secure evidence semantics by accident.

## Tests and conformance

Required tests:

- host-owned evidence denied for guest visibility;
- host-owned evidence rejected from checkpoint payload;
- compatibility alias denied unless explicit policy;
- completion publication denied before secure fence;
- retire publication denied without retire rule;
- completion fence pass does not imply retire side-effect publication;
- VMREAD secure-sensitive field denied without secure visibility contract;
- admitted-denied VMCALL cannot publish backend completion;
- Lane6/Lane7 sideband evidence envelopes respect visibility class;
- replay/rollback does not reuse stale evidence epoch.

## Closure criteria

The phase closes when secure evidence categories and publication fences are documented, with tests proving that evidence visibility, completion publication and retire side effects are separately admitted.

## Forbidden shortcuts

- Do not convert host-owned evidence into guest-visible state.
- Do not serialize scheduler, backend binding or native token evidence as checkpoint authority.
- Do not treat completion record as retire permission.
- Do not use VMREAD projection as evidence backend.
- Do not allow compatibility alias by default.

## Open questions

- Resolved 2026-05-30: `CompatibilityAlias` remains an independent class requiring secure evidence permission, neutral compatibility-alias evidence permission and explicit read-only compatibility projection policy.
- Resolved 2026-05-30: secure completion fence is evaluated at publication admission; completion publication and retire side-effect publication are separate gates, and completion success never implies retire permission.
