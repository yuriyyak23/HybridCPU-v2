# Layer 1 Secure Domain Descriptor Plan

## Purpose

Документ проектирует `SecureComputeDomainDescriptor` как opt-in нейтральный descriptor для Layer 1. Он задает secure-domain policy, но не меняет ISA, Stage A legality, VMX frontend или существующее поведение ordinary domains.

## Scope

Layer 1 descriptor должен описывать secure-domain activation, default/disabled semantics, связь с `DomainRuntimeContext`, secure admission hook и no-effect behavior для обычных доменов.

## Non-goals

Не проектируются capability registers, tagged memory, secure VMCS, `VmxCaps.SecureCompute`, новые instruction encodings, новые operand forms, новые modes of addressing или обязательная secure semantics для existing non-VMX instructions.

## Architectural invariants

`SecureComputeDomainDescriptor` активен только при materialized enabled secure-domain policy. Отсутствие descriptor означает ordinary domain. `SecurityLevel = Disabled` или `None` эквивалентны отсутствию enforcement. Первая реализация должна зафиксировать единственный disabled-state: либо нормализовать `None -> Disabled` при materialization, либо исключить один из вариантов из публичной модели.

VMX не может активировать secure compute. `VmxCaps` не может выдавать secure compute. `VMCS` не может хранить secure state. VMREAD/VMWRITE остаются compatibility projection/write-denied boundary.

## Proposed descriptors / policies

Предлагаемая форма `SecureComputeDomainDescriptor`:

- `DomainTag` - neutral domain identity binding; must match current runtime domain tag and must not derive from active VMCS pointer.
- `SecurityLevel` - `Disabled`, `None`, `Measured`, `Private`, `RestrictedInspection`, `SealedRuntime` или более строгая future enum. Для Phase 1 достаточно disabled/enabled semantics; если `None` остается в enum, materialization must normalize `None -> Disabled`.
- `MeasurementRequired` - secure enter requires materialized `DomainMeasurementDescriptor`.
- `PrivateMemoryRequired` - secure enter requires materialized `SecureMemoryDomainDescriptor` with private memory class.
- `HostInspectionPolicy` - controls host/debug/scheduler visibility, never grants guest-visible state directly.
- `EvidenceVisibilityPolicy` - links to `SecureEvidencePolicy`.
- `MigrationPolicy` - links to `SecureMigrationDescriptor`.
- `IoPolicy` - links to `SecureIoDomainDescriptor`.
- `HypercallPolicy` - links to `SecureHypercallDescriptor`.
- `DebugPolicy` - defines debug allowed/denied/measured-debug class.
- `CompatibilityProjectionPolicy` - controls whether compatibility aliases may expose secure-relevant values.

Disabled/default semantics:

- descriptor absent -> ordinary domain, unchanged behavior;
- descriptor present with `SecurityLevel = Disabled` -> ordinary domain, unchanged behavior;
- descriptor present with `SecurityLevel = None` -> ordinary domain, unchanged behavior;
- descriptor enabled with missing required subpolicy -> denied only for secure-domain operations and secure enter, not for ordinary instruction decode or Stage A.

Shell-only risk: наличие пустых `partial` типов не является доказательством no-effect semantics. Первый implementation PR обязан добавить tests `absent descriptor -> unchanged`, `disabled descriptor -> unchanged`, `SecurityLevel.None -> unchanged` до любых positive secure paths.

## Integration points

`DomainRuntimeContext` should eventually expose an optional secure descriptor view. The view must be materialized by neutral runtime admission, not by VMX.

`RuntimeBoundaryAdmissionService` receives a secure admission hook after ordinary domain context is resolved. Secure checks must be enabled only when an enabled secure descriptor is materialized and only for secure-domain operation classes. Ordinary domains and ordinary operation classes must bypass secure enforcement to avoid over-deny.

Status 2026-05-30: Phase 2 baseline implemented. `DomainRuntimeContext` exposes an optional neutral `SecureCompute` descriptor view, and `RuntimeBoundaryAdmissionService` accepts an opt-in secure operation classification that defaults to `Ordinary`. The hook bypasses absent/disabled descriptors, denies enabled unmaterialized descriptors for secure-domain operation classes, and fails closed for missing required measurement/memory subpolicy. No Stage A, VMX, VMCS, `VmxCaps`, migration, evidence, I/O or hypercall positive path was opened.

Status 2026-05-30 update: Phase 2 binding gap closed. `DomainRuntimeContext` now carries neutral `DomainTag` / `AddressSpaceTag` binding metadata, and `RuntimeBoundaryAdmissionService` denies enabled secure-domain operation classes when the materialized secure descriptor does not match the neutral runtime domain tag. Secure memory descriptors must also match the secure domain tag before Stage B memory policy can admit secure memory access.

1. resolve neutral domain descriptors;
2. if no active secure descriptor, keep existing decision path;
3. if enabled secure descriptor exists, validate required subpolicies according to operation class;
4. deny only operations whose runtime class requires secure-domain enforcement.

Stage A may carry classification metadata, but existing instructions do not require secure policy. Stage B consumes classification and secure descriptor only when the runtime operation crosses memory, evidence, migration, I/O, hypercall, debug, nested or publication boundaries.

## No-regression requirements

No regression gates:

- ordinary domain decode unchanged;
- ordinary Stage A unchanged except optional metadata presence;
- ordinary Stage B unchanged when descriptor absent/disabled;
- non-VMX instruction legality unchanged;
- 2048-bit bundle, 256-byte VLIW carrier, typed slots, scheduling and lane binding unchanged for ordinary domains;
- compiler facade/helper ABI does not emit secure-specific instruction encodings;
- no VMX exposure in Phase 1.

## Tests and conformance

Required tests:

- absent descriptor -> unchanged ordinary domain decisions;
- disabled descriptor -> unchanged ordinary domain decisions;
- `SecurityLevel = None` -> unchanged ordinary domain decisions;
- `SecurityLevel.None` materializes as the same disabled-state as `Disabled`, or the model exposes only one disabled-state;
- enabled descriptor with missing measurement policy -> secure enter denied only when measurement is required;
- enabled descriptor with missing memory policy -> denied only for secure private-memory operation;
- enabled descriptor does not over-deny ordinary domain operation classes;
- VMX cannot activate secure compute;
- `VmxCaps` cannot grant secure compute;
- VMCS cannot store secure state;
- VMCS field/projection cannot become secure-domain identity;
- source-pattern guard rejects active VMCS pointer as secure-domain source.

## Closure criteria

The descriptor skeleton phase closes when a neutral optional descriptor can exist in design and tests without changing existing runtime behavior. Closure must prove no VMX projection surface is opened and no non-VMX instruction receives new mandatory secure checks. If the phase only contains shell types, closure wording must say: design baseline + shells only; not feature-complete SecureCompute.

## Forbidden shortcuts

- Do not put `SecureComputeDomainDescriptor` under VMX compatibility namespace.
- Do not add `SecureCompute` bit to `VmxCaps` as authority.
- Do not store descriptor fields in VMCS.
- Do not infer `DomainTag` from active VMCS pointer.
- Do not fail ordinary domains because secure subpolicies are absent.

## Open questions

- Resolved 2026-05-30: `SecurityLevel.None` remains an accepted compatibility spelling but normalizes to `Disabled`; the runtime model has one disabled/no-effect state.
- Resolved 2026-05-30: `requiresSecureDomainCheck` is a Stage B derived/explicit runtime operation classification. Stage A remains free of mandatory SecureCompute policy.
