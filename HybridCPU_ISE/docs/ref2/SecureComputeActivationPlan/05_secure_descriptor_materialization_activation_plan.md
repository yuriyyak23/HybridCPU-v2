# Secure Descriptor Materialization Activation Plan

## Phase Metadata

- File name: `05_secure_descriptor_materialization_activation_plan.md`
- Phase goal: define activation-grade descriptor materialization and subdescriptor completeness.
- Status: open lifecycle-owner gate; structural DTO/completeness checks exist, but canonical materialization and revocation do not.
- Scope: root descriptor, memory, measurement, evidence, migration, I/O, hypercall, backend owner and nested intent descriptors.
- No-goals: no backend execution and no compatibility projection by descriptor presence alone.

## Current Baseline

The root descriptor is active only when enabled and materialized. Many subdescriptors exist as neutral policy surfaces. Missing or incomplete policy remains fail-closed.

## Authority Owner

No neutral descriptor lifecycle owner is implemented. Callers construct full descriptors, `DomainRuntimeContext.WithSecureCompute` replaces them, and the generic request can carry a second full descriptor. The required owner is a single future `SecureDomainRegistry` that returns only opaque generation-bound bindings.

Accepted recommended design: the neutral runtime may shard the registry by CPU/node/runtime instance, but each descriptor identity has exactly one authoritative registry owner. The registry owns immutable full descriptors and the lifecycle `Created -> Active -> Quiescing -> Revoked/Destroyed`. Runtime carriers contain only `SecureDomainBinding(RegistryId, DomainId, Generation, PolicyDigest, Seal)`. Any policy mutation creates a new generation and invalidates stale certificates, grants, maps, DMA intents and restore payloads. `DomainTag`, `PolicyDigest` and `Seal` are identity/binding material, never capability by themselves.

## Implemented Baseline

- descriptor and operation-specific completeness checks;
- construction defaults that remain disabled/fail-closed;
- tests for missing or unmaterialized required subdescriptor denial;
- documentation that materialization is necessary but not sufficient for activation.

## What Remains Denied/Future-Gated

- backend execution from materialization alone;
- VMREAD projection from materialization alone;
- migration authority from materialization alone;
- completion/retire publication from materialization alone;
- nested execution from child intent materialization.

## Forbidden Shortcuts

- descriptor exists means secure backend exists;
- materialized owner means publication;
- materialized memory descriptor means hardware tag;
- materialized hypercall descriptor means VMCALL/backend success;
- materialized compatibility projection policy means VMREAD readable value.

## Required RFC/ADR

No RFC/ADR for completeness hardening. A new owner RFC/ADR is required when materialization enables any positive runtime effect.

## Code Anchors

- `SecureComputeDomainDescriptor.cs`
- `SecureMemoryDomainDescriptor.cs`
- `DomainMeasurementDescriptor.cs`
- `SecureMigrationDescriptor.cs`
- `SecureIoDomainDescriptor.cs`
- `SecureHypercallDescriptor.cs`
- `SecureBackendOwnerDescriptor.cs`
- `SecureChildDomainIntentDescriptor.cs`

## Documentation Anchors

- `SecureComputerefactoringNew/07_domain_descriptor_materialization.md`
- `SecureComputerefactoringNew/08_subdescriptor_materialization_and_completeness.md`
- `SecureComputerefactoringNew/20_positive_runtime_execution_rfc_gate.md`

## Required Tests

- incomplete secure descriptor is denied for secure operations;
- missing measurement when required is denied;
- missing secure memory policy when required is denied;
- missing migration policy denies migration;
- missing I/O owner denies I/O;
- missing hypercall policy denies hypercall;
- backend owner descriptor materialization remains proof-only;
- child intent materialization remains design-fence.

## Required Static/Source Scans

- `IsMaterialized.*BackendExecutionAuthorized`
- `SecureBackendOwnerDescriptor.*activation`
- `SecureHypercallDescriptor.*backend success`
- `CompatibilityProjectionPolicy.*VMREAD.*allowed`

## Migration/Evidence Classification

Each descriptor field must be classified as policy metadata, guest-visible state, migration-serializable policy, recomputed evidence, host-owned denied evidence or compatibility projection metadata denied as authority.

## Completion/Retire Implications

Materialization creates no completion record and no retire publication rule. Those require Phase 14 plus a positive owner path.

## SecureCompute Activation Implications

Activation may require materialization, but materialization is not activation.

## Exit Criteria

- descriptor completeness matrix exists;
- lifecycle state machine and sole registry owner are fixed by an owner ADR;
- request/context full-descriptor carriers are removed from authoritative paths;
- activation, quiesce, revoke, generation replacement and stale-binding behavior are atomic and tested;
- missing subdescriptor tests deny;
- every subdescriptor has owner and migration/evidence class;
- no positive execution claim is made.

Exit status: open. DTO construction and `IsMaterialized` checks are confirmed, but completion requires one materialize/revoke owner, removal of request-supplied full descriptors, opaque bindings, generation/policy digest identity, atomic epoch change and stale/swap/replay tests.

## Dependency

Previous: `04_no_effect_disabled_baseline_revalidation.md`. Next: `06_stage_b_secure_admission_activation_plan.md`.
