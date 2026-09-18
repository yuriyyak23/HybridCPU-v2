# VMX Compatibility Boundary Plan

## Purpose

Документ определяет, как VMX compatibility frontend видит SecureCompute. Главный принцип: VMX может только отображать разрешенную нейтральную проекцию; VMX не активирует SecureCompute, не хранит secure state и не выдает authority.

## Scope

План покрывает `VmxCaps`, VMCS, VMREAD/VMWRITE, compatibility projection policy, secure-sensitive deny matrix and current VMREAD policy.

## Non-goals

Не добавляется VMX mode, secure VMCS, `VmxCaps.SecureCompute`, VMREAD backend, VMWRITE backend mutation, active VMCS pointer identity or compatibility checkpoint authority.

## Architectural invariants

VMX remains frozen compatibility frontend / ABI vocabulary / generated projection surface. VMCS remains read-only compatibility projection, not state owner. `VmxCaps` remains projection of typed grants, not capability authority.

SecureCompute activation requires materialized neutral secure-domain policy. VMX cannot create it.

This boundary must be guarded early as a denial-only Phase 1.5, before any positive secure-domain behavior is opened: VMX cannot activate SecureCompute, `VmxCaps` cannot grant SecureCompute, and VMCS cannot store secure state.

## Implementation status

- [x] Phase 8 boundary matrix closed on 2026-05-31: `SecureComputeCompatibilityBoundaryMatrixPolicy` denies secure-sensitive VMREAD fields unless the neutral proof chain is complete.
- [x] `GuestCr0`, `GuestCr4`, `HostCr3`, host execution aliases and compatibility-control fields remain denied by explicit matrix decisions.
- [x] Generated/schema owner mismatch is denied before any value projection.
- [x] VMWRITE to SecureCompute projection is denied/no-effect, `VmxCaps` cannot materialize a secure descriptor, VMCS checkpoint metadata cannot become authority and projection cannot become backend success.
- [x] Source guards prove the Phase 8 SecureCompute/VMX compatibility sources do not introduce `VmcsManager`, `VmxExecutionUnit`, VMCS scalar read/write backend or `VmxCaps.Secure*` authority.
- [x] Phase 9 nested secure domain design fence closed on 2026-05-31: Shadow VMCS remains a compatibility bridge only, VMCS12/VMCS02 are rejected as nested secure authority and nested projection cannot open more than the parent policy permits.
- [x] Next dependent work closed: Phase 10 release gate closed on 2026-05-31 with VMX no-authority doc/source conformance and production-claim audit.
- [x] Post-Phase10 owner/RFC proof gate closed on 2026-05-31: backend owner proof is neutral runtime evidence, not VMX, VMCS or `VmxCaps` authority.
- [x] Future runtime-execution and compatibility-advertisement decisions transferred to `Plan2/14-securecompute-open-decision-backlog.md`; production secure backend execution remains unopened and non-VMX.

## Proposed descriptors / policies

`CompatibilityProjectionPolicy` inside `SecureComputeDomainDescriptor` should define:

- `DefaultSecureProjectionVisibility` - denied by default;
- `AllowedCompatibilityAliases` - explicit field/alias list, if any;
- `RequiredEvidenceClass` - secure visibility class;
- `MigrationClassification` - recomputed, serializable, denied;
- `SecureSensitiveFieldPolicy` - denied unless neutral owner and tests exist;
- `WritePolicy` - denied for secure fields and all VMCS-like state mutation;
- `NoAuthorityProofRequired` - generated/schema owner and source-pattern conformance.

Secure-sensitive deny matrix:

- `GuestCr0` - denied; neutral privileged execution-state owner missing.
- `GuestCr4` - denied; neutral privileged execution-state owner missing.
- `HostCr3` - denied; neutral host-address-space owner missing.
- host execution aliases - denied; neutral host-execution owner missing.
- compatibility-control fields - denied unless explicit neutral control-bit value contract exists.
- secure measurement/evidence/debug/migration fields - denied unless future explicit neutral owner, evidence policy, migration classification and tests exist.
- VMWRITE to secure field - denied/no effect; no backend mutation.

General secure-sensitive VMREAD rule: a field is denied unless all of the following exist: neutral owner, read-only value source, secure visibility policy, migration classification and conformance tests. Field vocabulary alone is never sufficient.

Current allowed VMREAD slices remain narrow:

- completion-owned fields from neutral `CompletionRecord`;
- memory-owned `GuestCr3`, `EptPointer`, `Vpid`, `Cr3TargetCount` only from `MemoryDomainReadOnlyTranslationView`;
- execution-owned `GuestPc`, `GuestSp`, `GuestFlags` only from `ExecutionDomainReadOnlyStateView`.

## Integration points

`VmxCompatibilityAdmissionService` remains projection-only frontend. It may pass secure-aware projection request into runtime admission only when neutral secure policy exists, but the default answer for secure-sensitive fields is denied.

`VmcsReadOnlyValueProjectionService` must not read secure descriptors directly as backend state. It may receive already-admitted neutral read-only views and evidence decisions.

`VmxCapsProjection` must not grow an authoritative secure bit. If a future compatibility alias advertises secure-domain support, it must be a read-only projection of typed publication grants and must not activate enforcement.

## No-regression requirements

- VMX cannot activate secure compute.
- `VmxCaps` cannot grant secure compute.
- VMCS cannot store secure state.
- Phase 1.5 denial-only guard must exist before VMX-related positive projection work.
- VMREAD remains generated/read-only/denied-by-default.
- VMWRITE remains write-denied/no-emission for projection-only fields.
- Compatibility projection cannot become backend success.
- Existing closure 240-255 deny decisions remain.

## Tests and conformance

Required tests:

- VMREAD without secure visibility -> denied;
- VMREAD with field vocabulary but missing neutral owner -> denied;
- VMREAD with neutral owner but missing secure visibility or migration class -> denied;
- VMWRITE secure field -> denied;
- `VmxCaps` secure bit write -> rejected/no effect;
- `VmxCaps` cannot materialize secure descriptor;
- VMCS checkpoint with secure metadata -> rejected;
- projection cannot become backend success;
- `GuestCr0` / `GuestCr4` remain denied;
- `HostCr3` and host execution aliases remain denied;
- compatibility-control fields remain denied;
- generated schema owner mismatch -> denied.

## Closure criteria

The VMX boundary closes when every SecureCompute-visible compatibility path is either denied by default or explicitly tied to neutral owner, read-only value source, evidence visibility, migration classification and conformance.

## Forbidden shortcuts

- Do not add `VmxCaps.SecureCompute` as authority.
- Do not add secure VMCS fields as state.
- Do not add VMREAD scalar backend.
- Do not allow VMWRITE into secure state.
- Do not use active VMCS pointer as secure-domain identity.
- Do not serialize VMCS projection metadata as secure checkpoint state.

## Open questions

- Transferred 2026-05-31 to `Plan2/14-securecompute-open-decision-backlog.md`: SecureCompute compatibility advertisement policy remains open; default remains zero VMX authority and no `VmxCaps` grant.
- Transferred 2026-05-31 to `Plan2/14-securecompute-open-decision-backlog.md`: any future secure visibility alias placement remains open; default remains separate neutral evidence/debug admission, not VMX state ownership.
