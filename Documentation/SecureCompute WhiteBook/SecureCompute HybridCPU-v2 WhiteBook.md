# SecureCompute HybridCPU-v2 WhiteBook

Status: audit WhiteBook based on repository code, plans, docs and conformance tests  
Date: 2026-05-31  
Scope: SecureCompute Layer 1/Layer 2 runtime model, VMX compatibility boundary, conformance gates and Plan open decisions

## 1. Executive Summary

SecureCompute in HybridCPU-v2 is a neutral runtime descriptor and admission discipline for secure domains. It is not a VMX mode, not a secure VMCS, not a `VmxCaps` authority bit, not CHERI ISA, not tagged memory and not production secure backend execution.

The problem space it addresses is architectural ownership. Secure-domain state, memory policy, evidence, migration policy, I/O, hypercall admission, nested intent and compatibility visibility must be owned by neutral HybridCPU runtime descriptors and policies. Compatibility frontends may name or project selected read-only values, but they do not own secure state and do not authorize secure execution.

The current repository implements a bounded SecureCompute baseline:

- a neutral `SecureComputeDomainDescriptor` with disabled/no-effect semantics and `SecurityLevel.None -> Disabled` normalization;
- optional `DomainRuntimeContext.SecureCompute` plus neutral `DomainTag` and `AddressSpaceTag` binding;
- `RuntimeBoundaryAdmissionService` hooks for opt-in secure operation classes;
- secure memory, measurement, evidence publication, migration, I/O and hypercall policy objects;
- Layer 2 runtime grant discipline with provenance, bounds, epochs, revocation and monotonic derivation;
- VMX compatibility deny/projection fences and conformance tests;
- nested secure-domain design-fence admission;
- Phase 10 release gates and Post-Phase10 owner/RFC proof gate.

What remains open is deliberately quarantined in Plan2. Positive secure backend runtime execution is still open, compatibility advertisement is open, secure visibility alias placement is open, and future capability-aware ISA/memory topics are open. Plan2 Phase 14 is backlog hardening, not an implementation phase.

This WhiteBook does not claim that SecureCompute is production ready, complete, able to execute secure backends, backed by secure VMCS state, owned by VMX or equivalent to CHERI-like ISA semantics.

## 2. Architectural Position

SecureCompute sits beside the existing HybridCPU domain runtime model. Its architectural center is the Stage B/runtime admission path, not the VMX frontend.

Stage A remains a structural and classification boundary. Existing instructions must not become Stage A illegal because no secure descriptor exists. Stage A may carry neutral classification facts such as "may touch memory" or "requires secure-domain check", but the secure decision belongs to Stage B/runtime admission.

Stage B and runtime admission own the secure decision. `RuntimeBoundaryAdmissionService` joins domain boundary, capability boundary, evidence boundary, frontend mutation guards, runtime authority and optional secure admission.

Activation is opt-in. SecureCompute becomes active only when a materialized enabled `SecureComputeDomainDescriptor` is present and the operation class is secure-domain-sensitive. Absence, `Disabled` and `SecurityLevel.None` remain no-effect for ordinary execution.

The architecture separates:

- descriptor: secure domain, memory, measurement, migration, I/O, hypercall, backend owner and nested intent records;
- policy: admission, evidence, migration, grant authority, I/O/hypercall and compatibility projection rules;
- evidence: host-owned, guest-visible, migration-serializable, compatibility alias, recomputed-after-restore and debug-only classes;
- migration: explicit payload classes and restore revalidation;
- VMX projection: read-only compatibility vocabulary with denial by default for secure-sensitive paths.

## 3. Non-Goals And Forbidden Interpretations

SecureCompute must not be interpreted as:

- a VMX mode;
- a secure VMCS;
- `VmxCaps` authority;
- VMREAD/VMWRITE backend execution;
- CHERI ISA;
- tagged memory;
- capability registers;
- capability-aware `LOAD`, `STORE` or `FETCH`;
- decoder, encoder, ABI, register or 2048-bit bundle format change;
- positive secure backend runtime execution.

The current Layer 1/Layer 2 implementation also must not introduce provisional future capability-aware types, tag/provenance checkpoint fields, capability operand metadata or hidden product execution paths.

## 4. Phase And Status Ledger

| Phase | Status | Closure class | Proven | Not opened | Key evidence |
|---|---:|---|---|---|---|
| Phase 0 | closed | docs-only plus negative conformance baseline | SecureCompute architectural invariants and VMX non-authority baseline | Runtime behavior | `Plan/00`, `Plan/01`, Phase 10 doc-lint |
| Phase 1 | closed | shell/no-effect policy admission | `SecureComputeDomainDescriptor`, disabled/no-effect, `None -> Disabled` | VMX exposure and secure execution | `SecureComputeDomainDescriptorNoEffectTests` |
| Phase 1.5 | closed | negative conformance | VMX cannot activate, `VmxCaps` cannot grant, VMCS cannot store secure state | Positive VMX/SecureCompute path | `SecureComputeVmxDenialGuardTests` |
| Phase 2 | closed | fail-closed policy admission | Runtime admission hook, secure operation class scoping, domain/address-space binding | Evidence, migration, I/O, hypercall execution | `SecureRuntimeBoundaryAdmissionHookTests` |
| Phase 3 | closed | fail-closed policy admission plus negative conformance | Private/shared/measured/runtime-mutable memory policy, private host/DMA denial, explicit shared DMA grants | Tagged memory and capability-aware memory | `SecureMemoryDomainPolicyTests` |
| Phase 4 | closed | positive policy admission for measurement prerequisites plus negative conformance | Measurement materialization, stale/revoked/pending denial, policy/memory digest binding | Production attestation | `SecureMeasurementEvidencePolicyTests` |
| Phase 4.5 / Plan 05 | closed | positive policy admission for fenced publication plus negative conformance | Evidence visibility, compatibility alias checks, completion vs retire fence, sideband visibility | Production evidence transport | `SecureEvidencePublicationPolicyTests` |
| Phase 5 | closed | fail-closed migration policy plus negative conformance | Checkpoint payload denial, restore revalidation/reattest, stale epoch/grant denial, sealed private payload contract | Live migration, key management, production sealing | `SecureMigrationPolicyTests` |
| Phase 6 | closed | positive policy admission for explicit shared-buffer I/O and admitted-denied hypercall plus negative conformance | Secure I/O owner, typed shared-buffer grants, raw private pointer denial, admitted-denied semantics | Positive secure backend execution | `SecureIoHypercallPolicyTests` |
| Phase 7 | closed | runtime descriptor/grant discipline plus negative conformance | Provenance, bounds, epoch/revocation, monotonic child derivation | CHERI ISA, tagged memory, capability registers | `SecureAuthorityDisciplineTests` |
| Phase 8 | closed | VMX deny/projection matrix plus negative conformance | Secure-sensitive VMREAD denial, VMWRITE no-effect, `VmxCaps` no materialization, VMCS checkpoint denial | Secure VMCS and VMX-owned SecureCompute | `SecureComputeVmxPhase8BoundaryMatrixTests` |
| Phase 9 | closed | design fence plus negative conformance | Child intent owner, parent/child monotonicity, nested projection bounds, Shadow VMCS/VMCS12/VMCS02 authority denial | Nested SecureCompute execution | `SecureNestedDomainDesignFenceTests`, `SecureComputeVmxPhase9NestedFenceTests` |
| Phase 10 | closed | conformance/doc/source release gate | Source guards, doc-lint, stale-doc cleanup, status-label audit, production-claim audit | Complete SecureCompute feature set | `SecureComputePhase10ReleaseGateTests`, `SecureComputeVmxPhase10ReleaseGateTests` |
| Post-Phase10 owner/RFC proof gate | closed | policy-evidence admission only | Neutral backend owner proof chain may reach `AllowedProofOnlyNoExecution` | Runtime execution, completion publication, retire effects | `SecureBackendOwnerRfcGateTests` |
| Phase 14 / Plan2 backlog hardening | closed as backlog hardening | open-decision quarantine | Open decisions moved and hardened | Implementation phase | `Plan2/14-securecompute-open-decision-backlog.md` |

## 5. Runtime Model

`SecureComputeDomainDescriptor` is the root secure-domain descriptor. It carries `DomainTag`, `SecurityLevel`, measurement and private-memory requirements, host inspection policy, evidence visibility policy, migration policy, I/O policy, hypercall policy, debug policy and compatibility projection policy.

`SecureComputeSecurityLevel.None` aliases and normalizes to `Disabled`. A descriptor is active only when it is enabled and materialized. Materialization currently means a non-zero `DomainTag`.

`DomainRuntimeContext` can carry an optional `SecureCompute` descriptor and neutral `DomainTag` / `AddressSpaceTag`. The context exposes binding checks. Secure runtime admission denies an enabled secure operation if the descriptor domain tag does not match the neutral runtime domain tag.

`RuntimeBoundaryAdmissionService` first validates ordinary runtime boundaries, capabilities and evidence. Only then, and only for non-ordinary `SecureDomainOperationClass` with an enabled descriptor, it invokes secure admission and secure memory admission. It also denies compatibility frontend mutations that attempt to mutate authoritative runtime state.

No-effect semantics are explicit:

- absent descriptor: ordinary behavior remains unchanged;
- disabled descriptor: ordinary behavior remains unchanged;
- `SecurityLevel.None`: normalized to disabled;
- unmaterialized enabled descriptor: denied for secure-domain operation classes;
- ordinary operations: allowed as no-effect and not over-denied by SecureCompute.

## 6. Secure Memory Model

`SecureMemoryDomainDescriptor` defines secure memory by domain tag, address-space tag, policy epoch, region descriptors and DMA policy. Regions are classified as private, shared, measured or runtime-mutable.

Private memory denies host reads and denies DMA. Shared memory is not enough by itself; DMA is allowed only for explicit shared buffers under `SecureIoDomainDescriptor`, with direction, owner domain, lifetime epoch, evidence class and typed grant. Measured regions support measurement admission. Runtime-mutable regions require dirty policy and migration classification.

The memory model is Stage B/runtime policy. It does not add tagged memory, capability-aware memory semantics, capability registers or capability-aware `LOAD` / `STORE` / `FETCH`. Ordinary memory instructions remain unchanged outside active secure-domain policy.

## 7. Measurement, Evidence And Publication

`DomainMeasurementDescriptor` records measurement handle, state, debug class, policy digest, memory digest, runtime digest, evidence class, creator domain tag, parent measurement ID and policy source hash. It is materialized only when the measurement state, handle, policy digest, creator domain and policy source are valid.

Measurement admission denies missing, pending, stale, revoked, unmaterialized, policy-digest mismatched, memory-digest mismatched and debug-class mismatched measurement. It binds measurement to the secure domain and, when secure memory is present, to the secure memory policy epoch and measured memory digest.

`SecureEvidencePolicy` narrows neutral `EvidencePolicyDescriptor`. Evidence classes are:

- `Denied`;
- `GuestVisible`;
- `MigrationSerializable`;
- `CompatibilityAlias`;
- `RecomputedAfterRestore`;
- `DebugOnly`;
- `HostOwnedQuarantined`.

Host-owned evidence is quarantined. Guest-visible publication requires both secure and neutral evidence policy approval. Compatibility alias evidence additionally requires explicit read-only compatibility projection policy. Recomputed-after-restore evidence cannot be reused as guest-visible publication state.

`SecureCompletionPublicationFence` separates completion publication from retire publication. Completion requires a completion fence. Retire publication requires explicit retire-fence state and rule. An admitted-denied path is not success and must not publish completion or retire effects as though backend execution had completed.

## 8. Secure Migration

`SecureMigrationDescriptor` carries migration mode, private-memory migration policy, policy epoch, guest-visible evidence allowance, compatibility projection metadata allowance, measurement restore policy and grant restore policy.

`SecureCheckpointPayloadPolicy` and `SecureMigrationAdmissionPolicy` deny host-owned evidence, scheduler evidence, backend binding evidence, native token evidence, debug traces as guest state, VMCS projection metadata, compatibility projection metadata, raw measurement secrets, active host pointers and raw sealing keys.

Private secure memory migration is denied unless an explicit sealed and encrypted payload contract is complete. Restore admission validates current policy epoch, measurement revalidation or reattestation, grant provenance, grant epoch and restored private-memory policy.

Current migration descriptors do not contain provisional tag/provenance checkpoint fields. Future tag/provenance migration is an open Plan2 decision requiring a separate format and proof chain.

## 9. Secure I/O And Hypercall

`SecureIoDomainDescriptor` supports denial by default and explicit shared-buffer-only DMA. A secure I/O operation requires a materialized neutral I/O owner. Shared buffers must be materialized, bounded, direction-checked, owner-bound and current for the policy epoch.

`SecureHypercallDescriptor` describes allowed hypercall IDs, required grant, argument descriptors, evidence requirement, completion fence requirement and retire publication rule requirement. Raw private pointer arguments are denied. Opaque handles require current epoch/provenance. Shared-buffer arguments require an explicit shared-buffer descriptor and current typed grant.

`SecureIoHypercallAdmissionPolicy` can allow I/O policy admission and can return `AllowedAdmittedDenied` for recognized hypercall admission while backend execution remains closed. Even when a hypercall policy asks for backend execution, current code returns `DeniedBackendSuccessClosed`. This is intentional: positive secure backend runtime execution is not open.

## 10. Layer 2 Authority Discipline

Layer 2 is a runtime descriptor/grant discipline inspired by monotonic authority rules. It is not CHERI ISA.

`SecureGrantHandle` has kind, local ID, provenance hash and epoch. Guest-visible scalar materialization always fails through `TryMaterializeFromGuestScalar`. A handle must have scalar shape, provenance and current epoch to be materialized.

`SecureGrantAuthorityPolicy` rejects:

- guest architectural scalar materialization;
- compatibility projection as authority;
- missing neutral runtime owner;
- missing provenance;
- revoked grants;
- stale epochs;
- requested bounds that exceed granted bounds;
- missing neutral typed grant when a typed grant scope is required.

`SecureAuthorityBounds` makes authority monotonic by subset checks. `SecurePolicyDerivationRecord` binds parent/child policy digest, provenance and epoch. Nested and child-domain flows may validate a child only when it stays within parent bounds.

Compatibility projection scope cannot satisfy `SecureGrantHandle` authority. VMX, VMCS, VMREAD, VMWRITE and `VmxCaps` are not grant sources.

## 11. VMX Compatibility Boundary

VMX is a frozen compatibility frontend. VMCS is a read-only compatibility projection surface, not a state owner. `VmxCaps` is a compatibility projection of typed grants or capabilities, not authority.

The compatibility boundary code enforces:

- `ActivateSecureCompute` through VMX is denied;
- `GrantSecureCompute` through `VmxCaps` is denied;
- storing secure state in VMCS is denied;
- compatibility writes cannot mutate SecureCompute state;
- read projection may be allowed only as projection-only vocabulary.

The Phase 8 matrix adds a stricter read/write boundary. Secure-sensitive VMREAD paths require neutral owner, read-only source, secure visibility, migration classification and conformance proof. Schema-owner mismatch is denied. VMWRITE is no-effect for SecureCompute state. `VmxCaps` cannot materialize secure descriptors. VMCS checkpoint authority is denied. Compatibility projection cannot become backend success.

Shadow VMCS, VMCS12 and VMCS02 remain compatibility structures. They are not nested secure authority and cannot be used as mutable secure runtime state.

## 12. Nested Secure Domain Design Fence

`SecureChildDomainIntentDescriptor` records parent and child domain tags, requested security level, requested authority bounds, derivation record and intent state.

`SecureNestedDomainAdmissionPolicy` is a design fence. It can allow no-effect or design-fence admission, but it does not authorize backend success or mutable nested secure state.

Nested secure admission requires:

- neutral child-intent owner;
- materialized enabled parent secure descriptor;
- materialized child intent;
- no parent-to-child or child-to-parent host-evidence leakage;
- nested projection within parent policy;
- monotonic child bounds;
- current parent/child epoch and derivation provenance.

VMCS12 and VMCS02 authority payloads are denied. Mutable Shadow VMCS authority is denied. A Shadow VMCS may only remain a compatibility bridge.

## 13. Conformance And Source Guards

SecureCompute conformance is split by ownership boundary:

- `HybridCPU_ISE.Tests/SecureComputeRefactoring` covers neutral SecureCompute runtime descriptors, policies, grants, migration, evidence, I/O/hypercall, nested design fence, release gates and Plan2 hardening.
- `HybridCPU_ISE.Tests/VmxRefactoring` covers VMX compatibility boundary, VMREAD/VMWRITE denial/projection, VMCS/VmxCaps non-authority and nested VMX fence tests.

Important guard classes include:

- no-effect tests for absent, disabled and `SecurityLevel.None`;
- runtime-boundary hook tests;
- memory private/shared/measured/runtime-mutable tests;
- measurement and evidence policy tests;
- evidence publication and sideband tests;
- migration payload and restore tests;
- I/O and hypercall admitted-denied tests;
- authority monotonicity and provenance tests;
- VMX Phase 8 boundary matrix tests;
- nested secure design fence tests;
- Phase 10 release-gate doc/source tests;
- Post-Phase10 backend owner/RFC proof-gate tests.

Source guards reject or scan for forbidden authority regressions: VMX-owned SecureCompute, VMCS state ownership, `VmxCaps` grant/activation, VMREAD/VMWRITE backend mutation, decoder/encoder/ISA/capability-aware imports, VMCS checkpoint authority, host evidence serialization, production-ready claims and feature-complete claims.

## 14. Plan2 Open Decisions

Plan2 is an open-decision backlog. It is not an implementation phase. Every item below remains `open` unless explicitly marked otherwise by a future approved RFC/ADR and phase plan.

| Item | Status | Why open | Required RFC/ADR | Required negative tests | Required proof chain | Forbidden shortcuts | Current safe default |
|---|---|---|---|---|---|---|---|
| Positive secure backend runtime execution owner/RFC | `open` | Current gate accepts proof only and denies execution | RFC for neutral runtime owner and ADR for completion/retire publication | Missing owner, VMX/VMCS/VmxCaps owner, missing grant, stale epoch, raw private pointer, host evidence publication, no completion fence, no retire rule | enabled descriptor, neutral owner, typed grant, policy, argument/shared-buffer classification, evidence, completion fence, retire rule, current epoch, negative coverage | Treat VMCALL admission, VMREAD, VMCS, `VmxCaps` or compatibility projection as execution authority | `AllowedProofOnlyNoExecution` maximum |
| SecureCompute compatibility advertisement policy | `open` | Current default is zero VMX exposure for SecureCompute authority | RFC deciding permanent zero exposure or read-only advertisement | Advertisement grants authority, activates descriptor, changes execution, serializes authority | neutral evidence owner, read-only projection, migration class, conformance proof | `VmxCapsProjection` as grant/activation | no advertisement |
| Secure visibility alias placement | `open` | Alias may belong in neutral debug/attestation API rather than VMX schema | ADR for alias API and owner | VMX alias by default, alias without neutral evidence, alias without migration classification | neutral evidence admission, read-only projection, publication fence | VMCS field as secure state | separate neutral API preferred |
| Future capability-aware ISA profile shape | `open` | True capability ISA changes decoder, operands, memory and ABI | Repository-level ISA RFC | Layer 1/2 product imports future capability types, old pointers become capabilities, Stage A rejects old code | ISA, ABI, decoder, encoder, memory, migration, compiler, conformance plan | Placeholder product types under current SecureCompute namespaces | future quarantine |
| Future 2048-bit bundle legality for capability operands | `open` | Capability operands could affect typed slots and VLIW/EPIC legality | ISA/bundle ADR | Provisional capability metadata in current slots, ordinary bundle behavior changes | golden artifacts, typed-slot legality, compiler ABI proof | Hidden 2048-bit bundle changes | no current metadata |
| Future tag/provenance migration format | `open` | Tag/provenance migration needs a separate format and restore model | Migration/tag RFC | Tag/provenance fields in current `SecureMigrationDescriptor`, host evidence as guest state | format, restore validation, host-evidence separation | Provisional checkpoint fields in current descriptors | no tag/provenance checkpoint fields |

## 15. RFC Guidance Summary

`SecureCompute RFC HybridCPU-v2.md` is advisory input only. It does not authorize product code, backend execution, completion publication, retire effects, VMX/VMCS/`VmxCaps` authority, decoder/encoder changes or capability-aware ISA work.

The guidance says any future positive backend execution path must first establish:

- neutral runtime backend owner;
- enabled secure domain descriptor;
- typed capability grant;
- secure operation policy;
- argument and shared-buffer classification;
- evidence approval;
- completion fence;
- retire publication rule;
- current epoch and provenance validation;
- negative conformance coverage.

Until a separate implementation phase exists, `AllowedProofOnlyNoExecution` remains the maximum safe result.

## 16. Traceability Matrix

| WhiteBook section | Source plan/doc/test/code area | Status | Risk if misread |
|---|---|---:|---|
| Executive Summary | `Plan/12`, `Plan2/14`, Phase 10 tests | closed baseline plus open backlog | Overclaiming production readiness |
| Architectural Position | `Plan/00`, `Docs/Decoder update`, `RuntimeBoundaryAdmissionService` | closed | Moving secure decisions into Stage A or VMX |
| Non-Goals | `Plan/00`, `Plan/13`, Phase 10 tests | closed guard | CHERI/tagged-memory or VMX authority drift |
| Phase Ledger | `Plan/12`, `Plan/11`, Plan2 | closed ledger | Treating closure as feature-complete implementation |
| Runtime Model | `SecureComputeDomainDescriptor`, `DomainRuntimeContext`, `SecureDomainAdmissionPolicy` | implemented baseline | Over-denying ordinary operations |
| Secure Memory | `SecureMemoryDomainDescriptor`, `SecureMemoryAdmissionPolicy` | implemented policy baseline | Mistaking policy regions for tagged memory |
| Measurement/Evidence | `DomainMeasurementDescriptor`, `SecureEvidencePolicy`, `SecureEvidencePublicationPolicy` | implemented policy baseline | Publishing host-owned evidence or treating completion as retire |
| Migration | `SecureMigrationDescriptor`, `SecureCheckpointPayloadPolicy`, migration tests | implemented policy baseline | Restoring VMCS/compat metadata as authority |
| I/O/Hypercall | `SecureIoDomainDescriptor`, `SecureHypercallDescriptor`, `SecureIoHypercallAdmissionPolicy` | implemented admitted-denied baseline | Treating admission as backend success |
| Layer 2 Authority | `SecureGrantHandle`, `SecureGrantAuthorityPolicy`, authority tests | implemented runtime discipline | Calling it CHERI ISA or scalar authority |
| VMX Boundary | `Core/Virtualization/SecureCompute`, VMX refactoring tests | closed deny/projection matrix | Treating VMCS/VmxCaps/VMREAD as authority |
| Nested Fence | `SecureChildDomainIntentDescriptor`, `SecureNestedDomainAdmissionPolicy` | design fence closed | Claiming nested SecureCompute execution |
| Conformance | `SecureComputeRefactoring`, `VmxRefactoring` tests | closed release gates | Removing source/doc guards as cosmetic |
| Plan2 | `Plan2/14`, RFC guidance | open decisions quarantined | Treating backlog as implementation authorization |
| RFC Guidance | `Plan2/SecureCompute RFC HybridCPU-v2.md` | advisory only | Opening backend execution without phase plan |

## 17. Final Verdict

The current SecureCompute state is a neutral, opt-in runtime descriptor/admission baseline with fail-closed policies, positive policy admission in narrow non-execution cases, negative conformance coverage, VMX deny/projection boundaries, nested design fences and release-gate source/doc guards.

It is safe to claim:

- SecureCompute descriptors and policy-admission foundations exist;
- inactive SecureCompute remains no-effect;
- secure operation classes fail closed without required owners/subpolicies;
- Layer 2 grant/provenance/epoch discipline exists at runtime descriptor level;
- VMX, VMCS and `VmxCaps` are non-authoritative compatibility surfaces;
- Post-Phase10 owner/RFC proof gate can accept proof evidence only.

It is not safe to claim:

- SecureCompute is production ready;
- SecureCompute is feature complete;
- positive secure backend runtime execution;
- secure VMCS;
- VMX activation of SecureCompute;
- `VmxCaps` authority;
- CHERI ISA, tagged memory, capability registers or capability-aware memory execution.

The next architectural decision boundary is Plan2: whether and how to open positive secure backend runtime execution through a separate approved RFC/ADR, phase plan, neutral runtime owner implementation, proof chain and new negative conformance tests. Until then, backend execution stays closed.
