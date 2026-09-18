# CPU Stage B Secure Admission Activation Plan

## Phase Metadata

- File name: `06_stage_b_secure_admission_activation_plan.md`
- Phase goal: make SafetyVerifier-issued, identity-bound admission the only CPU Stage-B SecureCompute crossing point.
- Status: open. A generic boundary policy route exists, but CPU Stage-B/SafetyVerifier production reachability and certificate transport are not implemented.
- Scope: `RuntimeBoundaryAdmissionService`, secure operation classes, domain/address-space binding, secure memory access and authority ordering.
- No-goals: no backend execution service and no VMX frontend shortcut.

## Generic-Service Baseline

When called directly, `RuntimeBoundaryAdmissionService` leaves `SecureDomainOperationClass.Ordinary` as no-effect for absent, disabled and unmaterialized secure descriptors. For a caller-selected non-ordinary value, it routes the selected descriptor through `_secureAdmission.Admit(...)` before additional policy checks.

External audit finding, 2026-06-11: the previous generic hook bypassed `_secureAdmission.Admit(...)` when the secure descriptor was absent or disabled. Direct service tests now cover denial for non-ordinary inputs, but this does not close CPU Stage B: no production decode/SafetyVerifier/issue call graph supplies canonical operation identity.

Current proof split: ordinary no-effect and non-ordinary fail-closed semantics are direct generic-service tests. They do not prove CPU-path reachability, immutable identity transport or absence of alternate callers.

## Authority Owner

No CPU Stage-B SecureCompute authority owner is currently proven. `RuntimeBoundaryAdmissionService` is a generic policy service; the only production callers found are VMX compatibility paths using the default `Ordinary` secure operation class. The future sole issuer is SafetyVerifier, via an immutable certificate carried by issue.

Accepted recommended split:

- generated ISA registry owns the versioned operation vocabulary;
- canonical decoder freezes decoded identity;
- SafetyVerifier derives exhaustive `SecureOperationKind` with `Unknown = 0` and owns the legality verdict/certificate;
- callers, compiler metadata, VMX/trap values and request DTOs cannot supply or override the secure operation kind;
- `RuntimeBoundaryAdmissionService` becomes an internal policy helper under SafetyVerifier composition; its generic result cannot cross issue.

`SecureAdmissionCertificate` binds legality and a maximum effect envelope only. It is not a capability grant, execution receipt, completion record or retire authority. It must bind operation/descriptor generation/domain/address space/VT/owner context/source-working-physical lanes/bundle-FSP-replay identity/grant references and be opaque, issuer-bound and single-use through backend acceptance.

## What Can Be Implemented

- an admission matrix by `SecureDomainOperationClass`;
- tests for ordinary operation no-effect;
- negative tests for missing domain context, tag mismatch and memory binding mismatch;
- maintain the Stage B routing fix so absent, disabled and unmaterialized descriptors for non-ordinary secure operation classes reach fail-closed secure admission;
- source guard against alternate secure admission entry points.
- generated exhaustive operation coverage that fails when a new enum/manifest row lacks a named handler and tests;
- immutable certificate issue/validate/consume vocabulary with no public constructor or caller-set proof fields.

## What Remains Denied/Future-Gated

- backend execution without typed backend owner;
- hypercall backend success;
- publication without backend result and fences;
- VMREAD projection without neutral owner/value source;
- migration restore without validation and evidence rebuild.
- any compatibility path that treats absence, disabled state or unmaterialized state as permission for a non-ordinary secure operation.

## Forbidden Shortcuts

- calling secure backend code before Stage B;
- treating frontend decode as admission;
- treating VM exit reason or trap decision as runtime authority;
- bypassing domain/address-space binding.
- allowing `Unknown`, missing mapping or caller/decoder taxonomy mismatch to become `Ordinary`;
- treating the admission certificate as resource authority, backend result, completion or retire permission;
- preserving or reintroducing tests that allow `EnterSecureDomain` or equivalent non-ordinary secure classes with absent/disabled/unmaterialized descriptors.

## Required RFC/ADR

No RFC/ADR for Stage B hardening. Any new positive operation class needs owner-specific RFC/ADR.

## Code Anchors

- `RuntimeBoundaryAdmissionService.cs`
- `SecureDomainAdmissionService.cs`
- `SecureDomainAdmissionPolicy.cs`
- `SecureMemoryAdmissionPolicy.cs`
- `DomainRuntimeContext.cs`

## Documentation Anchors

- `SecureComputerefactoringNew/09_runtime_admission_boundary.md`
- `VirtualiztionRefactoringNew/03_runtime_boundary_admission_consolidation.md`
- `Documentation/Virtualization WhiteBook/11_Admission_Boundaries.md`

## Required Tests

- absent secure descriptor leaves ordinary operation allowed;
- disabled secure descriptor leaves ordinary operation allowed;
- unmaterialized secure descriptor leaves ordinary operation allowed;
- non-ordinary secure operation with missing descriptor fails closed;
- non-ordinary secure operation with disabled descriptor fails closed;
- non-ordinary secure operation with unmaterialized descriptor fails closed;
- enabled descriptor domain tag mismatch denied;
- secure memory domain/address-space mismatch denied;
- Stage B denial prevents backend, completion, retire and projection.
- old Stage B bypass tests are removed or inverted so that they assert secure-domain boundary denial for missing, disabled and unmaterialized descriptors across the non-ordinary taxonomy.
- unknown/missing/mismatched operation identity denies before issue;
- certificate forgery, issuer, generation, VT/context, lane, bundle/FSP/replay, grant and effect-envelope mutation deny;
- replay/restore/squash cannot reuse or revive a certificate.

## Required Static/Source Scans

- scan for secure backend calls outside Stage B;
- scan for VMX/VMCALL/trap code invoking SecureCompute authority directly;
- scan for `SecureOperationClass.Ordinary` invoking secure policy.
- scan for `secureDescriptor is { IsEnabled: true }` guards in Stage B paths that skip secure admission for non-ordinary operations;
- scan for `RuntimeBoundaryAdmission_NoEnabledDescriptorGuardBypassContractTests`;
- scan for test names or assertions preserving `DoesNotEvaluateSecureChecksWhenDescriptorAbsent` / `DoesNotEvaluateSecureChecksWhenDescriptorDisabled` semantics.
- scan for `CurrentReadinessBypass` or `Current Stage B bypass evidence only` tests and require their removal or inversion before activation.

## Migration/Evidence Classification

Admission decisions are not migration payloads and not evidence publication. Logs remain host/debug evidence unless explicitly classified.

## Completion/Retire Implications

Stage B admission never publishes completion or retire by itself.

## SecureCompute Activation Implications

Every future limited activation path must pass Stage B and prove no alternate path exists.

The missing/disabled/unmaterialized descriptor bypass is closed only inside the directly exercised generic service. CPU Stage-B enforcement remains open and no backend execution, completion publication, retire publication or production activation follows.

## Exit Criteria

- admission matrix complete;
- negative tests cover missing/disabled/mismatched states;
- missing, disabled and unmaterialized descriptor denials are observed through the Stage B hook across every non-ordinary secure operation class;
- source scan finds no alternate secure authority entry point.

Exit status: open. Direct generic-service routing tests remain useful negative regression evidence, but closure requires canonical decode taxonomy, a SafetyVerifier-issued certificate, one production carrier through source/working/physical identity and a call graph into the real issue path with no alternate caller-selected authority.

## Dependency

Previous: `05_secure_descriptor_materialization_activation_plan.md`. Next: `07_secure_capability_grant_epoch_activation_plan.md`.
