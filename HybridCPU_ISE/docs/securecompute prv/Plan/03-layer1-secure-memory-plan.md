# Layer 1 Secure Memory Plan

## Purpose

Документ проектирует `SecureMemoryDomainDescriptor` как neutral memory-domain policy for secure domains. Цель - описать private/shared/measured/runtime mutable memory без capability-aware pointer model, tagged memory или изменений ISA memory operands.

## Scope

План покрывает private memory, explicit shared memory, measured memory, runtime mutable secure memory, `DomainTag` / `AddressSpaceTag` / epoch binding, host inspection denial, DMA/I/O boundary assumptions и VMREAD compatibility constraints.

## Non-goals

Не вводятся tagged memory, capability-bearing operands, capability-aware `LOAD` / `STORE` / `FETCH`, новые memory operand encodings, CHERI-like pointer provenance на каждый указатель, новый decoder path или обязательная secure semantics для ordinary domains.

## Architectural invariants

Memory owner остается `MemoryDomainDescriptor` и associated neutral memory services. Secure memory policy усиливает Stage B/runtime boundary, но не становится VMX-owned EPT/VPID table.

`GuestCr3`, `EptPointer`, `Vpid`, `Cr3TargetCount` остаются VMREAD projection values только из neutral `MemoryDomainReadOnlyTranslationView` при существующих условиях. Secure memory descriptors не создают VMCS-backed memory authority.

## Implementation status

- [x] Phase 3 baseline closed on 2026-05-30: `SecureMemoryDomainDescriptor` now models private/shared/measured/runtime-mutable regions, address-space tag, policy epoch and DMA policy.
- [x] Stage B secure memory route: `SecureMemoryAdmissionPolicy` denies private host reads, denies private DMA including I/O/hypercall argument origins, requires explicit shared-buffer DMA policy plus typed grant, requires measured regions for measurement access and rejects stale/unmaterialized policy epochs.
- [x] Runtime boundary integration: secure memory access is routed only through opt-in secure-domain operation classification after enabled secure descriptor activation; ordinary operations remain bypass/no-effect.
- [x] Non-regression/source guards: ordinary `LOAD`/`STORE`/`FETCH` sources do not require SecureMemory policy; SecureMemory sources do not create VMCS-backed `GuestCr3`, `EptPointer`, `Vpid` or `HostCr3` authority.
- [x] Phase 3 tightening closed on 2026-05-30: secure memory domain/address-space binding is checked at Stage B, shared DMA requires matching secure I/O shared-buffer direction/owner/lifetime/evidence plus typed grant, runtime-mutable regions carry dirty and migration classification, and host-evidence non-leak shell is covered by SecureCompute conformance tests.
- [x] Phase 4 dependency closed on 2026-05-30: measured memory classification is bound to `DomainMeasurementDescriptor` memory digest checks before measured secure-domain admission.
- [x] Phase 5 dependency closed on 2026-05-30: secure migration policy now consumes measurement revalidation/reattest and sealed/encrypted private-memory payload contract rules.
- [x] Phase 6 dependency closed on 2026-05-30: secure I/O and hypercall admission now reuses explicit shared-buffer descriptors, typed grants, neutral I/O owner checks and completion/retire publication fences.
- [x] Phase 7 dependency closed on 2026-05-31: runtime descriptor/grant monotonicity discipline is covered as Layer 2 baseline, not CHERI ISA.
- [x] Phase 10 release gate closed on 2026-05-31: doc/source conformance and stale-doc cleanup prove Phase 3 remains no new ISA memory semantics.
- [x] Post-Phase10 owner/RFC proof gate closed on 2026-05-31: backend owner proof is policy evidence only; runtime execution remains tracked in `Plan2/14-securecompute-open-decision-backlog.md`.

## Proposed descriptors / policies

`SecureMemoryDomainDescriptor` should describe:

- `DomainTag` - secure domain binding;
- `AddressSpaceTag` - neutral address-space identity, not VMX VPID authority;
- `PolicyEpoch` - anti-stale and revocation boundary;
- `PrivateRegions` - host-inspection-denied regions;
- `SharedRegions` - explicit shared buffers with direction, owner, lifetime and evidence class;
- `MeasuredRegions` - memory ranges included in domain measurement;
- `RuntimeMutableRegions` - writable secure memory excluded or separately classified in measurement;
- `DmaPolicy` - default denied, explicit shared-buffer only;
- `HostInspectionPolicy` - denied by default for private memory;
- `MigrationPayloadClass` - serializable, recomputed, host-owned, secret-denied or non-migratable.

Memory classes:

- private - domain-only, host direct read denied, DMA denied unless explicitly mediated;
- shared - explicit, bounded, direction-limited, evidence-classified;
- measured - included in measurement hash/classification;
- runtime mutable - tracked by epoch/dirty policy and migration classification;
- compatibility projection - read-only derived fields, never authority.

## Integration points

`RuntimeBoundaryAdmissionService` should route secure-domain memory operations through memory policy only after descriptor activation. This route is Stage B/runtime policy only; it must not alter ordinary `LOAD` / `STORE` / `FETCH` decoding, operand formats, address modes, tagged memory behavior or capability-aware checks.

`LOAD` / `STORE` / `FETCH` behavior:

- ordinary domain unchanged;
- secure domain routed through Stage B/memory policy;
- missing secure memory policy -> denied only if secure domain requires private memory or operation touches secure private/shared/measured class;
- no new operand format or addressing mode;
- no tagged pointer check in Layer 1.

DMA behavior:

- default-deny for private secure memory;
- allowed only for explicit shared buffers with secure I/O/memory policy and typed grant;
- no DMA path may bypass `SecureMemoryDomainDescriptor` by treating a private guest address as an ordinary host-visible pointer.

Lane6 descriptor sideband and Lane7 control-plane runtime may transport descriptors/evidence only as neutral sideband envelopes with visibility policy. They must not become memory authority or leak private host evidence.

## No-regression requirements

- Existing memory translation tests remain unchanged when secure descriptor is absent/disabled.
- Existing VMREAD memory-owned fields keep current source requirements.
- `HostCr3` remains denied.
- No EPT/VPID/CR3 scalar cache is introduced.
- No DMA path may infer permission from VMX compatibility aliases.
- No ordinary `LOAD` / `STORE` / `FETCH` behavior changes outside an enabled, materialized secure-domain policy route.
- Scheduler, bundling and VLIW typed-slot legality do not change for ordinary domains.

## Tests and conformance

Required tests:

- ordinary domain memory behavior unchanged;
- secure domain with `PrivateMemoryRequired` and missing `SecureMemoryDomainDescriptor` -> secure enter/admission denied;
- private memory not host readable;
- shared memory explicit only;
- DMA to private memory denied;
- DMA to private memory remains denied even when the address is passed through I/O/hypercall argument paths;
- DMA to explicit shared buffers requires both policy and grant;
- measured memory required when `MeasurementRequired`;
- runtime mutable memory has epoch/dirty classification;
- host evidence not produced as guest-visible state;
- no VMCS-backed `GuestCr3`, `EptPointer`, `Vpid`;
- `HostCr3` remains denied.

## Closure criteria

Closure requires a documented memory policy shape, test matrix, and non-regression proof that secure memory is an optional Stage B/runtime policy. No ISA memory access semantics are rewritten in this layer.

## Forbidden shortcuts

- Do not implement private memory as a VMCS/EPT scalar cache.
- Do not allow host inspection because VMREAD exposes a memory alias.
- Do not treat `AddressSpaceTag` as a VMX-owned VPID.
- Do not serialize private host mapping evidence as guest state.
- Do not add tagged memory in Layer 1.
- Do not add capability-aware memory checks under the name of secure memory policy.

## Open questions

- Resolved 2026-05-30: Layer 1 treats `MeasuredRegions` as stable secure memory region descriptors. Phase 4 is responsible for binding those classifications to materialized measurement/evidence publication.
- Resolved 2026-05-31: Phase 3 binds shared-buffer lifetime to secure policy epoch. Completion fence and retire publication for I/O/hypercall paths are covered by Phase 6; explicit revocation/derivation monotonicity is covered by the Phase 7 runtime descriptor/grant baseline.
