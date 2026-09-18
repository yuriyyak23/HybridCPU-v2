# Layer 1 Measurement And Attestation Plan

## Purpose

Документ проектирует `DomainMeasurementDescriptor` и attestation flow для SecureCompute Layer 1. Измерение является evidence-producing neutral runtime operation, а не VMX field, VMCS state или VmxCaps feature.

## Scope

План покрывает measurement materialization, `MeasurementRequired` policy, attestation as evidence, leakage controls, checkpoint restrictions и binding measurement to secure policies.

## Non-goals

Не проектируются конкретные криптографические алгоритмы, remote attestation protocol, key management implementation, sealed ISA capabilities или raw secret serialization. Не открываются VMREAD fields для measurement.

## Architectural invariants

Measurement owner must be neutral runtime/evidence infrastructure. Attestation is an evidence operation: it is not a VMREAD field, not VMCS state and not a `VmxCaps` feature. VMX может только получить compatibility projection, если существует explicit `CompatibilityProjectionPolicy`, neutral value source, evidence visibility policy, migration classification и tests.

Measurement missing must deny secure domain enter only when secure descriptor requires measurement. Ordinary domains remain unaffected.

## Implementation status

- [x] Phase 4 baseline closed on 2026-05-30: `DomainMeasurementDescriptor` now carries an opaque handle, measurement epoch, policy/memory/runtime digests, debug class, attestation evidence class and provenance binding metadata.
- [x] Measurement admission: `SecureMeasurementAdmissionPolicy` validates materialized measurement, stale/revoked/pending states, policy digest, measured-memory digest, epoch binding and debug measurement class before measured secure-domain entry.
- [x] Evidence publication guard: attestation publication requires secure evidence policy, neutral evidence policy and completion publication fence; host-owned/recomputed evidence remains non-guest-visible.
- [x] Checkpoint guard: raw measurement secrets and host-owned evidence are denied checkpoint payload classes.
- [x] VMX boundary guard: measurement/attestation are not VMREAD fields, VMCS state or `VmxCaps` advertised features.
- [x] Phase 5 dependency closed on 2026-05-30: revalidation/reattest requirements now feed secure migration restore admission and stale measurement epochs are denied.
- [x] Phase 6 dependency closed on 2026-05-30: I/O and hypercall policies that influence boundary effects are bound into the secure policy digest and publication is admitted only through evidence/completion/retire fences.
- [x] Phase 7 first safe pool closed on 2026-05-31: runtime descriptor/grant monotonicity baseline now validates provenance, authority bounds and current epochs without CHERI-like ISA work.
- [x] Phase 8 VMX compatibility boundary closed on 2026-05-31: secure-sensitive VMX projection paths are denied by default unless neutral owner, read-only source, secure visibility, migration classification and conformance proof all exist.
- [x] Phase 9 nested secure domain design fence closed on 2026-05-31: nested admission now requires neutral child intent, active parent secure descriptor, monotonic provenance/epoch derivation and denies host-evidence leakage, nested projection expansion and VMCS12/VMCS02 authority.
- [x] Next dependent work closed: Phase 10 release gate closed on 2026-05-31 with doc/source conformance hardening and production-claim audit.
- [x] Post-Phase10 owner/RFC proof gate closed on 2026-05-31: neutral backend owner proof, approved RFC/ADR requirement and negative tests are covered without opening runtime execution.
- [x] Future runtime-execution decision transferred to `Plan2/14-securecompute-open-decision-backlog.md`: production secure backend execution remains unopened pending a separate implementation phase and decision record.

## Proposed descriptors / policies

`DomainMeasurementDescriptor` should describe:

- `MeasurementId` - opaque handle, not raw secret;
- `MeasurementEpoch` - anti-stale value;
- `MeasuredPolicyDigest` - binding to secure domain policy;
- `MeasuredMemoryDigest` - binding to measured memory class;
- `MeasuredRuntimeDigest` - optional binding to selected runtime descriptors;
- `DebugClass` - production, measured-debug, debug-denied, or debug-unmeasured-denied;
- `AttestationEvidenceClass` - guest-visible, host-owned, migration-serializable, or compatibility-alias-denied;
- `MaterializationState` - absent, pending, materialized, stale, revoked;
- `Provenance` - creator, parent handle, policy source and epoch.

Measurement must bind to:

- domain policy;
- memory policy;
- migration policy;
- debug policy;
- compatibility projection policy;
- evidence visibility policy;
- I/O and hypercall policy if they influence allowed boundary effects.

## Integration points

`RuntimeBoundaryAdmissionService` validates measurement before secure enter when `MeasurementRequired = true`.

`SecureEvidencePolicy` classifies attestation result visibility. Attestation is evidence, not architectural guest state by default and not a compatibility field by default.

`SecureMigrationDescriptor` classifies whether measurement handles may be serialized, recomputed, re-attested or rejected on restore. Raw host evidence and raw secrets are never checkpoint payload.

Debug policy can alter measurement class. A debug-enabled secure domain is not equivalent to production secure domain unless descriptor explicitly allows measured-debug class.

## No-regression requirements

- Ordinary domain admission unaffected when measurement descriptor absent.
- No measurement VMCS field store.
- No attestation VMREAD field by default.
- No attestation `VmxCaps` feature bit or VMCS state owner.
- No raw host evidence leakage through VMREAD.
- No secret or sealing key in checkpoint.
- No compatibility projection without explicit visibility policy.
- Existing completion-owned, memory-owned and execution-owned VMREAD slices remain unchanged.

## Tests and conformance

Required tests:

- measurement missing -> secure domain enter denied when `MeasurementRequired`;
- measurement missing -> ordinary domain unaffected;
- stale measurement epoch -> secure enter denied;
- debug domain changes measurement class;
- debug-denied policy rejects debug exposure;
- attestation cannot expose host-owned evidence;
- attestation handle cannot be VMREAD as secure state without explicit visibility contract;
- attestation cannot be advertised or activated through `VmxCaps`;
- attestation cannot be checkpointed as raw host evidence or raw secret material;
- checkpoint rejects raw measurement secrets;
- restore revalidates or re-attests when policy says so.

## Closure criteria

The phase closes when measurement is specified as neutral evidence-bound descriptor materialization, with explicit deny behavior for missing/stale measurement and no compatibility/state-owner shortcut. Documentation/shell-only completion must be named design baseline only, not implemented attestation.

## Forbidden shortcuts

- Do not store measurement in VMCS.
- Do not publish measurement through `VmxCaps`.
- Do not model attestation as VMREAD backend data.
- Do not make attestation a backend success result without evidence policy.
- Do not serialize raw host-owned evidence or secrets.
- Do not treat debug trace as guest-visible measurement proof.

## Open questions

- Resolved 2026-05-30: `MeasurementId` is an opaque runtime handle. Restore must revalidate or re-attest when migration policy says `ReattestRequired` or evidence class is `RecomputedAfterRestore`; stable cross-host identity is not assumed in Phase 4.
- Resolved 2026-05-30: measured-debug domains are not treated as production-equivalent. Debug policy must explicitly allow measured-debug class, and production-only migration remains a future Phase 5 denial/default-no path.
