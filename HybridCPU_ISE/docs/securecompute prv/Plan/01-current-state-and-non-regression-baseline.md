# Current State And Non-Regression Baseline

## Purpose

Документ фиксирует baseline текущей архитектуры HybridCPU-v2 перед SecureCompute refactoring. Его задача - защитить уже закрытые VMX/runtime инварианты от регрессии и задать список владельцев, которым SecureCompute обязан подчиняться.

## Scope

Baseline охватывает runtime domain ownership, VMX compatibility boundary, VMREAD projection policy, migration/checkpoint rules, completion/retire publication, evidence non-leakage, nested direction и no-emission compiler boundary.

Учитываются документы `audit3.md`, `audit5.md`, `ОСНОВЫ и ПРАВИЛА VMX.md`, `SuccessClosed/` после closure 240, `schemas/`, а также текущие документы `CloseToHSL/Core/Runtime/Domains/SecureCompute/Docs/`.

## Non-goals

Документ не предлагает новые типы, не открывает VMREAD поля, не описывает реализацию secure memory, не меняет VMX frontend и не вводит capability-aware ISA.

## Architectural invariants

Текущая модель:

- HybridCPU architecture axis = `Legality A|B`, not VMX;
- virtualization = domain-level extension of generic legality/runtime model;
- VMX = frozen compatibility frontend;
- VMCS = generated/read-only projection surface;
- `VmxCaps` = projection of typed grants, not authority;
- runtime-owned legality;
- capability-centric authority через typed grants;
- evidence-centric visibility;
- retire-owned publication;
- host-owned evidence must not leak into guest/domain architectural state;
- checkpoint/migration image contains guest-visible state/policy, not host-owned evidence;
- non-VMX ISA growth must not require manual VMX integration except explicit guest-visible compatibility projection.

## Proposed descriptors / policies

Current owners, которые нельзя подменять SecureCompute-специфичной VMX логикой:

- `ExecutionDomainDescriptor` - neutral execution state owner; only `GuestPc`, `GuestSp`, `GuestFlags` currently have safe read-only VMREAD projection through `ExecutionDomainReadOnlyStateView`.
- `MemoryDomainDescriptor` - neutral memory translation owner; `GuestCr3`, `EptPointer`, `Vpid`, `Cr3TargetCount` may be projected only from read-only translation view under current rules.
- `CapabilityDescriptorSet` - typed grant-backed capability publication owner; `VmxCaps` is only compatibility projection.
- `EvidencePolicyDescriptor` - evidence visibility owner; host-owned evidence is quarantined.
- `MigrationValidationPolicy` - migration admissibility owner.
- `DomainCheckpointImage` - neutral checkpoint payload owner, not VMCS image.
- `RuntimeBoundaryAdmissionService` - Stage B/runtime boundary admission owner.
- `TrapCompletionRouteDescriptor` / `TrapCompletionRouteService` - neutral trap completion route owner.
- `HypercallBackendAdmissionService` - neutral hypercall backend admission owner; missing backend remains admitted-denied.
- `VmcsReadOnlyValueProjectionService` - generated/read-only VMREAD projection service, not mutable VMCS backend.

## Integration points

SecureCompute должен встраиваться поверх этих owners:

- `DomainRuntimeContext` may carry an optional materialized secure descriptor view.
- `RuntimeBoundaryAdmissionService` may consume the optional secure descriptor only after ordinary domain admission context exists.
- Memory, evidence, migration, completion, I/O, hypercall and nested services remain neutral owners.
- VMX frontend may only ask for compatibility projection, then receive denied/projected result according to neutral policy.

## No-regression requirements

Forbidden regression symbols/patterns:

- `VmxExecutionUnit` as runtime owner;
- `VmcsManager`;
- `IVmcsManager`;
- active VMCS pointer;
- mutable VMCS field store;
- scalar cache;
- `VmxCaps` authority;
- VMREAD/VMWRITE scalar backend;
- VMCS checkpoint authority;
- host evidence serialization;
- scheduler evidence serialization;
- backend binding evidence serialization;
- native token evidence serialization;
- debug trace serialization as guest state;
- VMX runtime manager;
- legacy helper authority;
- compatibility projection metadata as source of migration truth.

Current VMREAD/security-sensitive status remains:

- `GuestPc`, `GuestSp`, `GuestFlags` may be projected only from `ExecutionDomainReadOnlyStateView`.
- `GuestCr0`, `GuestCr4` remain denied until neutral privileged execution-state semantics exist.
- `GuestCr3`, `EptPointer`, `Vpid`, `Cr3TargetCount` may be projected only from neutral memory-domain read-only translation view.
- `HostCr3` remains denied until neutral host-address-space owner exists.
- compatibility-control VMREAD values remain denied unless explicit neutral control-bit value contract exists.
- host execution aliases remain denied unless separate neutral host-execution owner exists.

## Tests and conformance

Baseline conformance must include:

- absence of `VmxExecutionUnit`, `VmcsManager`, `IVmcsManager` outside retired or explicitly disabled tests;
- VMREAD deny decisions for `GuestCr0`, `GuestCr4`, `HostCr3`, host execution aliases and compatibility-control fields;
- `VmxCaps` write no-emission/rejected/no-effect behavior;
- migration checkpoint rejection of VMCS projection authority;
- host-owned evidence non-leakage;
- scheduler evidence, backend binding evidence, native token evidence and debug traces rejected from guest-visible/checkpoint state;
- admitted-denied VMCALL path with no backend execution, no completion publication and no retire publication.

## Closure criteria

Phase 0 baseline is closed when existing VMXRefactoring tests pass unchanged and new SecureCompute planning docs do not require any source behavior change.

## Forbidden shortcuts

Do not treat any compatibility name as a neutral owner. In particular, `VMCS`, `VmxCaps`, VMREAD field names, VMX opcodes and VMCS projection schema are vocabulary, not authority.

## Open questions

- Resolved 2026-05-30: SecureCompute-specific conformance now keeps local guards for Stage A/no-emission, VMCS-backed authority denial, secure memory source patterns and host-evidence non-leak shells. VMXRefactoring remains the frozen compatibility regression suite.
- Resolved 2026-05-30: no-emission compiler boundary covers generated ISA/VMX surfaces and Stage A sources. Secure descriptor schema files are allowed as neutral descriptor vocabulary only; they must not appear in instruction encodings, operand formats, VMX activation, VMCS state stores or `VmxCaps` authority.
