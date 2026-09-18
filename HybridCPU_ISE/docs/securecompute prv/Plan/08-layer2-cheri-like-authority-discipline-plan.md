# Layer 2 CHERI-Like Authority Discipline Plan

## Purpose

Документ проектирует CHERI-inspired authority discipline for SecureCompute Layer 2 без ISA expansion. Цель - получить monotonic, bounded, provenance-aware runtime authority на уровне descriptors, typed grants and opaque handles, не вводя capability-aware ISA.

Correct phase formula:

- Layer 2 introduces CHERI-like authority discipline only at runtime descriptor, typed grant, monotonic policy derivation, provenance, revocation and epoch level.
- Layer 2 does not introduce CHERI-like ISA.
- Layer 2 does not introduce capability-aware memory.
- Layer 2 does not change `LOAD` / `STORE` / `FETCH`.
- Layer 2 does not change decoder or encoder.
- Layer 2 does not change ABI.
- Layer 2 does not change VLIW/EPIC operand metadata.

## Scope

Layer 2 покрывает unforgeable typed secure grants at runtime level, monotonic derivation of policies, parent/child restrictions, bounded authority, sealing-like handles, explicit provenance, revocation and epoch model.

## Non-goals

Не вводятся capability registers, tagged memory, capability-bearing operands, capability-aware `LOAD` / `STORE` / `FETCH`, pointer provenance per pointer, sealed ISA capabilities, new ABI operands or VLIW/EPIC operand metadata changes.

Layer 2 also explicitly forbids decoder/encoder changes, Stage A metadata changes and VLIW/EPIC operand metadata changes.

## Architectural invariants

Layer 2 remains descriptor/grant discipline. It strengthens `RuntimeBoundaryAdmissionService`, secure descriptors and typed grants. It does not alter decoder, encoder, instruction encodings, Stage A legality for existing instructions or memory operand semantics.

Child secure domain cannot exceed parent authority. Derived policy cannot widen host inspection, migration payloads, compatibility projection or debug visibility.

Layer 2 must not require new Stage A metadata. It may consume existing operation classification only at Stage B/runtime admission.

`SecureGrantHandle` must never be accepted directly from guest architectural state. A guest may reference an opaque identifier only through validated runtime lookup; the scalar identifier itself is not authority and cannot materialize a secure grant.

`CompatibilityProjection` scope cannot satisfy `SecureGrantHandle` authority. Compatibility projection vocabulary remains read-only compatibility evidence/projection only and cannot be upgraded into secure authority.

Host/runtime confused-deputy protection is mandatory: valid host/runtime authority plus a guest-forged scalar handle must still deny secure I/O, secure hypercall or secure migration authority.

## Implementation status

- [x] Phase 7 first safe pool closed on 2026-05-31: `SecureGrantAuthorityPolicy` validates materialization source, provenance, authority bounds, combined domain/memory/grant/provenance epochs and neutral typed-grant scope.
- [x] `SecureGrantHandle` remains a runtime descriptor handle: guest-visible scalar materialization is fail-closed and `CompatibilityProjection` scope cannot satisfy secure authority.
- [x] `SecureChildDomainIntentDescriptor` now validates monotonic parent/child derivation and denies child I/O, hypercall or debug policy widening.
- [x] Secure migration restore rejects grant handles without provenance, stale grant epochs and restored handles that were not rederived/revalidated.
- [x] Source guards prove Phase 7 authority sources did not introduce VMCS, `VmxCaps`, VMREAD or VMWRITE authority.
- [x] Phase 9 nested secure domain design fence closed on 2026-05-31: parent/child monotonic derivation is enforced before nested secure design admission, while Shadow VMCS remains compatibility vocabulary only.
- [x] Post-Phase10 owner/RFC proof gate closed on 2026-05-31: concrete owner integration is covered as neutral policy-evidence admission only; positive secure backend execution remains closed.
- [x] Future runtime-execution decision transferred to `Plan2/14-securecompute-open-decision-backlog.md`: concrete backend execution requires a separate implementation phase and decision record.

## Proposed descriptors / policies

Layer 2 principles:

- typed secure grants at runtime level that are not forgeable from guest-visible scalar values;
- monotonic derivation of policies;
- child secure domain cannot exceed parent authority;
- derived secure policy cannot expand host inspection;
- derived migration policy cannot allow more payload/evidence than source;
- derived compatibility projection policy cannot expose more fields;
- bounded authority for memory regions, I/O, hypercalls, debug and migration;
- sealing-like opaque handles for secure domain identity and measurement handles;
- explicit provenance for grants, descriptors and checkpoint payloads;
- revocation/epoch model for secure grants and secure memory policy.

Planned policy vocabulary:

- `SecureGrantHandle` - opaque runtime handle, not forgeable from guest-visible scalar values and never accepted directly from guest architectural state.
- `SecurePolicyDerivationRecord` - parent policy digest, child digest, derivation rule, epoch.
- `SecureAuthorityBounds` - memory/I/O/hypercall/debug/migration/projection limits.
- `SecureRevocationEpoch` - admission rejects stale grant/policy/memory epochs.
- `SealedDomainIdentityHandle` - runtime-sealed descriptor handle; not a sealed ISA capability, pointer capability, tagged-memory token or active VMCS pointer.
- `MeasurementHandle` - opaque measurement identity, no raw secret payload.

`SecureGrantHandle` non-forgeability is narrowly defined for Layer 2 only:

- not forgeable from guest-visible scalar values;
- requires provenance validation;
- requires authority bounds validation;
- requires current epoch validation.

Do not use `unforgeable` as if the handle were a hardware CHERI capability. `SecureGrantHandle` is not an ISA capability, not a pointer capability and not a tagged-memory token.

Grant scope mapping:

- `HardwareAvailable` may prove that hardware support exists.
- `RuntimeEnabled` may prove that the runtime enabled a feature class.
- `DomainGranted` may prove that the current domain received ordinary typed authority.
- `SecureDerived` must prove bounded derivation from a parent secure policy.
- `MigrationRestored` must prove restore-time rederivation or revalidation.
- `DebugOnly` must be visible only under explicit measured-debug policy.
- `CompatibilityProjection` cannot satisfy secure authority and cannot be mixed with `SecureDerived`, `MigrationRestored` or `DebugOnly`.

Revocation baseline:

- per-domain policy epoch;
- per-memory-policy epoch;
- per-grant-collection epoch;
- per-handle provenance epoch.

Any mismatch denies admission. A current domain epoch alone is not enough to revive stale memory policy, stale grant collection or stale handle authority.

Migration serialization baseline:

- sealed-like handles are never serialized as authority by default;
- migration may serialize only a provenance record or rederive token;
- restore must revalidate epoch and provenance before admission;
- restore must reject missing provenance;
- restore must reject stale epoch;
- compatibility metadata and VMCS-shaped metadata cannot restore secure handle authority.

## Integration points

`CapabilityDescriptorSet` remains typed grant owner. Layer 2 adds secure grant classes or derivation rules under neutral capability infrastructure, not under `VmxCaps`.

Secure grant checks may consume `CapabilityGrantScope.HardwareAvailable`, `RuntimeEnabled` and `DomainGranted` as input evidence. Future secure scopes such as `SecureDerived`, `MigrationRestored` and `DebugOnly` must be modeled as secure runtime derivation classes, not as compatibility projection. `CapabilityGrantScope.CompatibilityProjection` is not a secure authority source.

`NestedDomainDescriptor` and future child-intent descriptors consume monotonic parent/child policies. Secure nested admission requires proof that child policy is subset of parent authority.

`SecureMigrationDescriptor` must carry provenance and epoch validation. Restore rejects derived policy that expands payload/evidence/projection over source.

Restore must not accept a serialized handle as authority. It must rederive or revalidate the handle using migration provenance and current epoch before any secure-domain admission.

## No-regression requirements

- `VmxCaps` remains compatibility projection, not authority.
- Existing non-VMX instructions do not become capability-aware.
- Ordinary memory operations do not require capability operands.
- No tagged memory appears in Layer 2.
- VLIW/EPIC typed-slot legality remains unchanged except optional metadata that does not affect ordinary legality.
- Compiler helper ABI does not emit new capability-bearing operands.
- No new Stage A metadata is required for Layer 2.
- `CompatibilityProjection` grants cannot satisfy secure authority checks.
- Guest-visible scalar values cannot materialize `SecureGrantHandle`.

## Tests and conformance

Required tests:

- child cannot exceed parent;
- derived policy cannot expand host inspection;
- derived migration policy cannot serialize more evidence than source;
- derived compatibility projection cannot open more fields;
- debug policy cannot be widened by child;
- revoked secure grant fails admission;
- stale epoch fails admission;
- forged scalar handle rejected;
- handle with missing provenance rejected;
- handle with valid scalar shape but stale epoch rejected;
- valid scalar shape but missing provenance -> denied;
- valid scalar shape but stale epoch -> denied;
- guest-visible scalar cannot materialize `SecureGrantHandle`;
- `CompatibilityProjection` scope cannot satisfy `SecureGrantHandle` authority;
- valid host/runtime authority + guest-forged scalar handle -> denied;
- checkpoint payload with missing provenance rejected;
- migration restore handle without provenance rejected;
- migration restore handle with stale epoch rejected;
- valid parent policy + child request widening I/O policy -> denied;
- valid parent policy + child request widening hypercall policy -> denied;
- valid parent policy + child request widening debug policy -> denied;
- `VmxCaps` projection cannot create secure grant;
- ordinary instruction stream unaffected.

## Closure criteria

Layer 2 closes when secure authority derivation is monotonic, bounded and epoch-checked at runtime descriptor/grant level, with no ISA-visible capability model.

## Forbidden shortcuts

- Do not encode secure grants as VMCS fields.
- Do not treat scalar handles as forgeable guest capabilities.
- Do not add capability registers.
- Do not add tagged memory.
- Do not rewrite `LOAD` / `STORE` / `FETCH`.
- Do not let child policy widen parent policy.
- Do not accept `SecureGrantHandle` directly from guest architectural state.
- Do not let `CompatibilityProjection` satisfy secure grant authority.
- Do not serialize sealed-like handles as authority by default.
- Do not treat `SealedDomainIdentityHandle` as sealed ISA capability.
- Do not add Layer 2 Stage A metadata.

## Open questions

- Resolved 2026-05-31: revocation is combined for the current baseline: per-domain policy epoch, per-memory-policy epoch, per-grant-collection epoch and per-handle provenance epoch must all be current where applicable.
- Resolved 2026-05-31: sealed-like handles are not serialized as authority by default. Migration may serialize only provenance records or rederive tokens; restore must rederive or revalidate provenance and epoch.
