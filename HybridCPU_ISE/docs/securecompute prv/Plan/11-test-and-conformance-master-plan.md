# Test And Conformance Master Plan

## Purpose

Документ задает общий test matrix для SecureCompute refactoring. Он объединяет negative tests, positive tests, conformance guards, source-pattern guards, build-time schema guards, migration/evidence guards and no-regression gates.

## Scope

План покрывает Layer 1, Layer 2, VMX compatibility boundary, nested future-safe direction, compiler no-emission boundary, replay/rollback and stale-doc cleanup.

## Non-goals

Документ не реализует тесты и не меняет product code. Он описывает обязательные классы проверок для будущих PR/closures.

## Architectural invariants

Testing must prove the negative boundary first. SecureCompute is not considered architecturally integrated until tests prove that absent/disabled descriptor leaves existing behavior unchanged and enabled secure descriptor fails closed without neutral owners/subpolicies. Shell types alone are not evidence of no-effect semantics.

Generated schema owner mismatch must deny. Missing owner must deny. Inactive secure compute must keep existing tests unchanged.

## Implementation status

Coverage closed does not imply production-ready SecureCompute. A checked coverage item means that the named negative boundary, policy-admission rule, source guard or design fence has tests; it does not claim positive secure backend execution unless explicitly labeled that way.

- [x] Phase 1 no-effect coverage: absent descriptor, disabled descriptor, `SecurityLevel.None`, enabled ordinary no-over-deny, and no-emission guard.
- [x] Phase 1.5 VMX denial-only coverage: VMX cannot activate SecureCompute, `VmxCaps` cannot grant it, VMCS cannot store secure state, secure-sensitive VMREAD/VMWRITE remain denied unless the full neutral proof chain exists.
- [x] Phase 2 runtime-boundary coverage: secure admission hook is not evaluated for absent/disabled descriptors, does not over-deny ordinary operations, and denies active enabled secure-domain operations when required measurement or memory subpolicy is missing.
- [x] Phase 3 secure memory coverage: private/shared/measured/runtime-mutable memory policy, private host-read denial, private DMA denial, explicit shared DMA typed-grant requirement, measured-region requirement, ordinary `LOAD`/`STORE`/`FETCH` no-regression and VMCS-backed memory-authority source guards.
- [x] Phase 4 measurement/evidence coverage: materialized measurement, stale/revoked/pending denial, policy/memory digest binding, evidence visibility categories, publication fence, raw measurement checkpoint denial and attestation not via VMREAD/VMCS/`VmxCaps`.
- [x] Phase 4.5 / Plan 05 evidence publication coverage: compatibility alias evidence, completion-vs-retire separation, admitted-denied VMCALL publication, Lane6/Lane7 sideband visibility and stale evidence replay/rollback.
- [x] Phase 5 migration coverage: restore revalidation/reattest admission, migration payload class denial, VMCS/compat metadata rejection, stale grant/measurement epoch denial and private-memory sealed/encrypted contract.
- [x] Phase 6 secure I/O/hypercall coverage: secure I/O owner, typed shared-buffer grants, raw private pointer denial, forged opaque handle denial, neutral hypercall backend owner, admitted-denied semantics and completion/retire publication fences.
- [x] Phase 7 descriptor/grant monotonicity coverage: child cannot exceed parent, stale/revoked grants denied, scalar forged handles rejected and grant provenance/epoch validation.
- [x] Phase 8 VMX compatibility boundary coverage: secure-sensitive VMREAD fields, schema owner mismatch, VMWRITE no-effect, `VmxCaps` descriptor materialization, VMCS checkpoint authority and projection backend success are denied.
- [x] Phase 9 nested secure domain design fence coverage: neutral child-intent owner, active parent secure descriptor, monotonic parent/child bounds, host-evidence non-leakage, nested projection bounds, stale parent/child epochs and Shadow VMCS/VMCS12/VMCS02 authority denial are covered.
- [x] Phase 10 conformance hardening and stale-doc cleanup closed on 2026-05-31: release-gate doc-lint, source guards, status-label audit and production-claim audit are covered by `SecureComputePhase10ReleaseGateTests` and `SecureComputeVmxPhase10ReleaseGateTests`.
- [x] Post-Phase10 secure backend owner/RFC proof gate closed on 2026-05-31: `SecureBackendOwnerRfcGateTests` cover neutral owner, approved RFC/ADR, proof-chain, stale-epoch, negative-test and backend-execution-closed decisions.
- [x] Future coverage pool transferred to `Plan2/14-securecompute-open-decision-backlog.md`: production secure backend runtime execution must add new denied tests, neutral owner implementation proof and production-claim audit before any execution claim.

## Proposed descriptors / policies

Testable policy groups:

- `SecureComputeDomainDescriptor` activation and disabled/no-effect semantics;
- `SecureMemoryDomainDescriptor` private/shared/measured policy;
- `DomainMeasurementDescriptor` materialization and stale epoch;
- `SecureEvidencePolicy` visibility and publication fence;
- `SecureMigrationDescriptor` checkpoint payload classes;
- `SecureIoDomainDescriptor` and `SecureHypercallDescriptor` admission;
- Layer 2 monotonic descriptor/grant derivation;
- `CompatibilityProjectionPolicy` deny/projection matrix;
- nested child-intent monotonicity and design-fence-only nested admission.

## Integration points

Test locations should prefer:

- `HybridCPU_ISE.Tests/SecureComputeRefactoring` for secure-domain neutral tests;
- `HybridCPU_ISE.Tests/VmxRefactoring` only for VMX-boundary and compatibility-denial tests;
- existing conformance namespaces under `CloseToHSL/Core/Virtualization/Conformance` only for compatibility boundary guards;
- runtime conformance under neutral runtime folders for descriptor/migration/evidence/memory tests.

Build-time/source-pattern guards should scan for forbidden authority regressions:

- `VmxExecutionUnit`;
- `VmcsManager`;
- `IVmcsManager`;
- active VMCS pointer as identity;
- mutable VMCS field store;
- scalar cache;
- `VmxCaps` secure authority;
- VMREAD/VMWRITE backend mutation;
- VMCS checkpoint authority;
- host evidence serialization.
- scheduler evidence serialization;
- backend binding evidence serialization;
- native token evidence serialization;
- debug trace guest-state serialization.

## No-regression requirements

No-regression matrix:

- absent descriptor -> unchanged existing ISA/runtime/VMX frontend;
- disabled descriptor -> unchanged;
- `SecurityLevel = None` -> unchanged;
- `SecurityLevel.None` normalizes to the same disabled-state as `Disabled`, or the public model exposes only one disabled-state;
- Stage A metadata only -> no existing instruction denial;
- Stage B secure checks only when a materialized enabled descriptor exists and the operation class is secure-domain-sensitive;
- VMX cannot activate secure compute;
- VmxCaps cannot grant secure compute;
- VMCS cannot store secure state;
- no new instruction encodings;
- no changes to 2048-bit bundle/256-byte VLIW carrier;
- no EPIC/VLIW typed-slot legality change for ordinary domains.

## Tests and conformance

Negative tests:

- missing secure descriptor with ordinary domain -> allowed as before;
- absent descriptor -> unchanged;
- disabled descriptor -> unchanged;
- `SecurityLevel.None` -> unchanged;
- enabled secure descriptor with missing required subpolicy -> denied for secure operation;
- enabled secure descriptor does not over-deny ordinary operation classes;
- missing neutral owner -> denied;
- generated schema owner mismatch -> denied;
- host evidence exposure -> denied;
- VMCS projection checkpoint -> denied;
- compatibility metadata authority -> denied;
- stale epoch -> denied;
- revoked grant -> denied;
- raw private hypercall pointer -> denied;
- DMA private memory -> denied.
- private secure memory migration without sealed/encrypted payload contract -> denied;
- secure hypercall positive path before neutral backend owner -> denied;
- VMREAD secure-sensitive field with vocabulary only -> denied;
- `GuestCr0` / `GuestCr4` / `HostCr3` / host execution aliases remain denied;
- host evidence, scheduler evidence, backend binding evidence, native token evidence and debug traces rejected from guest-visible/checkpoint state.

Positive policy-admission tests:

- disabled/no-effect descriptor preserves existing decisions;
- explicit shared buffer is admitted by policy with secure I/O policy and typed grant;
- secure measurement materialized allows measured-enter admission policy;
- allowed guest-visible evidence published only after fence;
- migration payload is admitted by policy only for explicit serializable class;
- child policy subset of parent may validate, but backend execution remains denied/design-fenced;
- existing VMREAD completion/memory/execution slices still work under their current neutral owner rules when security policy allows non-secure context.

Positive runtime-execution tests:

- not opened for secure backend execution in the current Layer 1/Layer 2 baseline;
- any future positive secure I/O, hypercall, migration, nested or VMX-adjacent runtime execution path must add a matching denied test, neutral owner proof and production-claim audit before it is marked complete.

Conformance guards:

- source-pattern authority guards;
- no-emission compiler boundary guards;
- generated schema parity guards;
- VMREAD deny matrix;
- VMWRITE deny/no-effect matrix;
- early VMX denial-only guard for Phase 1.5;
- migration/evidence payload-class guards;
- replay/rollback epoch guards;
- stale-doc cleanup guard that flags references to VMX-owned secure compute.
- Phase 10 doc-lint must reject forbidden production/authority claims such as `VmxCaps grants SecureCompute`, `VMCS owns secure state`, and `VMX activates SecureCompute`.

## Closure criteria

Each PR phase must add at least one negative conformance test before opening any positive path. The master plan closes when every descriptor/policy has absent, disabled, missing-owner, missing-policy, stale-epoch and forbidden-authority coverage. Shell-only or documentation-only completion must be labeled design baseline + shells only; not feature-complete SecureCompute.

## Forbidden shortcuts

- Do not open a positive secure path without a corresponding denied test.
- Do not rely on manual review for VMCS/VmxCaps authority regressions when a source-pattern guard can detect them.
- Do not make generated schema changes without parity/golden-artifact tests.
- Do not mark SecureCompute complete without code proof.

## Open questions

- Resolved 2026-05-31: neutral runtime tests use `HybridCPU_ISE.Tests/SecureComputeRefactoring`; VMX boundary tests use `HybridCPU_ISE.Tests/VmxRefactoring`.
- Resolved 2026-05-31: markdown planning files must be covered by Phase 10 doc-lint for forbidden phrases such as `VmxCaps grants SecureCompute`, `VMCS owns secure state`, and `VMX activates SecureCompute`.
- Transferred 2026-05-31 to `Plan2/14-securecompute-open-decision-backlog.md`: future production runtime-execution coverage remains open until a separate implementation phase is approved.
