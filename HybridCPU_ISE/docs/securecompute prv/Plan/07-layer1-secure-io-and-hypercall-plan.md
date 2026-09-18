# Layer 1 Secure I/O And Hypercall Plan

## Purpose

Документ проектирует `SecureIoDomainDescriptor` and `SecureHypercallDescriptor` for secure domains. Цель - ввести I/O mediation and hypercall backend admission fences without turning VMCALL into backend success by default.

## Scope

План покрывает secure I/O mediation, explicit shared buffers, DMA denial, hypercall argument classification, neutral backend ownership, completion fence and retire publication rules.

## Non-goals

Не проектируются concrete device model, driver ABI, hypercall ABI encoding, new ISA instruction, VMX-only backend, direct DMA bypass or capability-aware pointer ABI.

## Architectural invariants

I/O owner remains neutral runtime I/O/memory/IOMMU descriptors. Hypercall owner remains `HypercallBackendAdmissionService` and neutral backend descriptors. VMX/VMCALL may be compatibility vocabulary, but cannot create backend authority.

Admitted-denied semantics remain mandatory: decode/admission of VMCALL or trap projection is not success. Success requires neutral backend owner, typed capability grant, secure hypercall policy, argument memory classification, evidence approval, completion fence and retire publication rule.

The first secure hypercall implementation phase should be denial-only. No positive secure hypercall backend path may be added until the neutral backend owner, typed grant, secure policy, argument/shared-buffer classification, evidence approval, completion fence and retire publication rule all exist with tests.

## Implementation status

- [x] Phase 6 baseline closed on 2026-05-30: secure I/O and secure hypercall admission fences are implemented as fail-closed policy shells.
- [x] Secure I/O owner guard exists: secure I/O operation classes require a neutral I/O owner policy, and secure DMA is admitted only through explicit shared-buffer descriptors plus typed grants.
- [x] Secure hypercall admission exists: allowed hypercall id, neutral backend owner, typed grant, argument classification, evidence policy, completion fence and retire fence are checked before any visible result.
- [x] Raw private pointer and forged opaque/scalar handle arguments are denied.
- [x] Admitted-denied semantics are preserved: secure hypercall admission can be recognized without backend execution, and even a descriptor requesting backend execution is denied in this phase.
- [x] VMX/VMCALL remains compatibility vocabulary only; source guards prove no VMCS/VMREAD/VMWRITE/`VmxCaps` authority was introduced.
- [x] Phase 7 first safe pool closed on 2026-05-31: secure grant admission baseline now rejects missing provenance, stale/revoked epochs, guest-scalar materialization and CompatibilityProjection authority.
- [x] Phase 8 VMX compatibility boundary closed on 2026-05-31: compatibility projection remains read-only/no-backend-success, and VMWRITE cannot mutate SecureCompute state.
- [x] Phase 9 nested secure domain design fence closed on 2026-05-31: nested child I/O, hypercall, debug, migration and compatibility projection authority cannot exceed the parent runtime descriptor bounds.
- [x] Next dependent work closed: Phase 10 release gate closed on 2026-05-31 with doc/source conformance hardening, status-label audit and production-claim audit.
- [x] Post-Phase10 owner/RFC proof gate closed on 2026-05-31: neutral backend owner proof, approved RFC/ADR requirement and negative tests are covered without opening runtime execution.
- [x] Future runtime-execution decision transferred to `Plan2/14-securecompute-open-decision-backlog.md`: positive secure I/O/hypercall backend execution remains unopened pending a separate implementation phase and decision record.

## Phase 7 handoff requirements

Phase 7 must keep I/O and hypercall authority at runtime descriptor/grant level. It must not introduce hardware CHERI capabilities, ISA-visible capability registers, pointer capabilities, tagged memory tokens, new operand formats or new Stage A metadata.

When Phase 7 uses the word `unforgeable`, it means only:

- not forgeable from guest-visible scalar values;
- requires provenance validation;
- requires authority bounds validation;
- requires current epoch validation;
- not an ISA capability;
- not a pointer capability;
- not a tagged-memory token.

`SecureGrantHandle` must never be accepted directly from guest architectural state. A scalar value with a valid-looking shape is not a grant. Materialization requires a neutral runtime owner to resolve provenance, authority bounds and current epoch before Stage B/runtime admission.

`SealedDomainIdentityHandle` is a runtime-sealed descriptor handle, not a sealed ISA capability. It must not be confused with a hardware sealed capability, a pointer token, a VMCS pointer or a VMX identity.

Layer 2 must not require new Stage A metadata. It may consume the existing operation classification only at Stage B/runtime admission.

Revocation must be modeled as combined state unless Phase 7 narrows it further:

- per-domain policy epoch;
- per-memory-policy epoch;
- per-grant-collection epoch;
- per-handle provenance epoch.

Any mismatch is fail-closed. A current domain epoch alone is not enough to revive a stale memory policy, stale grant collection or stale handle.

Secure grants must be related to typed grant scopes without letting compatibility vocabulary become secure authority:

- `HardwareAvailable` may prove that hardware support exists.
- `RuntimeEnabled` may prove that the runtime enabled a feature class.
- `DomainGranted` may prove that the current domain received ordinary typed authority.
- `SecureDerived` must prove bounded derivation from a parent secure policy.
- `MigrationRestored` must prove restore-time rederivation or revalidation.
- `DebugOnly` must be visible only under explicit measured-debug policy.
- `CompatibilityProjection` cannot satisfy `SecureGrantHandle` authority and cannot be upgraded into `SecureDerived`, `MigrationRestored` or `DebugOnly`.

Migration rules for sealed-like handles:

- sealed-like handles are not serialized as authority by default;
- migration payloads may carry only non-authoritative descriptors or evidence required for rederivation/revalidation;
- restore must rederive or revalidate handle provenance and epoch before admission;
- restore must reject handles without provenance;
- restore must reject handles with stale epoch;
- compatibility metadata or VMCS-shaped metadata cannot restore secure handle authority.

Host/runtime confused-deputy protection is mandatory: neutral host or runtime authority must not perform secure I/O, DMA or hypercall backend work because a guest supplied a forged scalar handle. Backend action requires a materialized secure grant from the neutral owner plus operation-specific policy, evidence and publication fences.

## Proposed descriptors / policies

`SecureIoDomainDescriptor` should describe:

- `AllowedDevices` or neutral device classes;
- `SharedBufferPolicy`;
- `DmaPolicy` - denied by default, explicit shared buffer only;
- `MmioPolicy`;
- `InterruptInjectionPolicy`;
- `EvidenceClass`;
- `CompletionFenceRequired`;
- `MigrationPayloadClass`.

`SecureHypercallDescriptor` should describe:

- `BackendOwner` - neutral runtime owner identity, not VMX;
- `AllowedHypercallIds`;
- `ArgumentClassification` - immediate, guest-visible shared memory, private pointer denied, measured buffer, opaque handle;
- `RequiredCapabilityGrant`;
- `EvidenceApprovalPolicy`;
- `CompletionFencePolicy`;
- `RetirePublicationPolicy`;
- `MigrationClassification`;
- `DebugVisibility`.

Argument/shared-buffer classification:

- raw private guest pointers are always denied;
- shared buffers must be explicit descriptors with direction, bounds, owner, lifetime, grant and evidence class;
- immediate/scalar arguments remain ordinary values only if they do not imply private memory access;
- opaque handles must be validated by provenance and epoch, not by scalar value alone.

## Integration points

Secure I/O admission:

1. ordinary I/O path unchanged if secure descriptor absent/disabled;
2. enabled secure descriptor requires I/O policy for I/O-sensitive operations;
3. DMA to private memory denied unless explicit mediated shared-buffer policy exists;
4. host inspection of private buffers denied;
5. completion and retire publication require fence and policy.

Secure hypercall admission:

1. decode/trap/VMCALL compatibility admission may succeed as projection-only;
2. backend admission requires neutral owner;
3. typed grant and secure hypercall descriptor must allow the hypercall;
4. memory arguments must be classified;
5. evidence approval and completion fence are required before any visible success.
6. retire publication requires a separate retire rule after completion fence.

## No-regression requirements

- Existing VMCALL remains admitted-denied without neutral backend owner.
- No backend success from compatibility projection alone.
- No private pointer argument allowed by raw address.
- No DMA to private memory by default.
- DMA is allowed only for explicit shared buffers with policy and grant.
- Completion/retire publication denied without secure fence.
- Ordinary non-secure I/O behavior unchanged.

## Tests and conformance

Required tests:

- missing secure I/O owner -> denied for secure I/O operation;
- ordinary domain I/O unchanged;
- DMA to private memory denied;
- DMA to explicit shared buffer allowed only with policy and grant;
- hypercall raw private pointer denied;
- hypercall opaque/scalar handle forged from guest-visible scalar denied;
- missing neutral backend owner -> denied;
- missing typed grant -> denied;
- missing secure hypercall policy -> denied;
- admitted-denied != success;
- completion/retire publication denied without fence;
- completion fence alone does not imply retire publication;
- VMCALL projection cannot become backend success.

Phase 7 handoff negative tests:

- valid scalar shape but missing provenance -> denied;
- valid scalar shape but stale epoch -> denied;
- `CompatibilityProjection` grant cannot satisfy secure authority;
- guest-visible scalar cannot materialize `SecureGrantHandle`;
- child widens I/O policy -> denied;
- child widens hypercall policy -> denied;
- child widens debug policy -> denied;
- migration restore handle without provenance -> denied;
- migration restore handle with stale epoch -> denied;
- host/runtime confused-deputy attempt with forged guest handle -> denied.

## Closure criteria

The phase closes when secure I/O and hypercall operations have neutral owner requirements, argument memory classification and publication fences, while existing VMX admitted-denied behavior remains intact.

## Forbidden shortcuts

- Do not make VMCALL success because VMX compatibility decode succeeded.
- Do not use VMCS fields as hypercall backend state.
- Do not allow raw private guest pointer as shared buffer.
- Do not add positive secure hypercall success in the first secure hypercall phase.
- Do not bypass evidence policy for device completion.
- Do not use `VmxCaps` as hypercall capability authority.
- Do not let `CompatibilityProjection` satisfy secure grant authority.
- Do not accept `SecureGrantHandle` from guest-visible scalar state.
- Do not treat `SealedDomainIdentityHandle` as sealed ISA capability.
- Do not add new Stage A metadata for Layer 2 grant discipline.

## Open questions

- Resolved 2026-05-31: Phase 6 models the neutral backend owner as an explicit materialized owner flag/fence only. The Post-Phase10 owner/RFC proof gate introduces neutral owner proof as policy evidence only; positive backend success remains closed until a later runtime-execution phase is approved.
- Resolved 2026-05-30: shared-buffer descriptors live under secure I/O policy and secure memory admission references them by address/bounds/direction/owner/lifetime/evidence plus typed grant.
