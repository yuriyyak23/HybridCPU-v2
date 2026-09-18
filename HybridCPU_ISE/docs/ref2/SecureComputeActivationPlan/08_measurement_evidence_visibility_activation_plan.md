# Measurement Evidence Visibility Activation Plan

## Phase Metadata

- File name: `08_measurement_evidence_visibility_activation_plan.md`
- Phase goal: keep measurement, evidence, attestation facts and debug visibility separate from runtime authority.
- Status: partial measurement/evidence classification gate; no exclusive production evidence publisher is proven.
- Scope: measurement descriptors, evidence policy, evidence publication, debug classes and attestation visibility.
- No-goals: no evidence-as-activation, no VMREAD authority, no migration authority from evidence alone.

## Current Baseline

`DomainMeasurementDescriptor` is materialized by handle/provenance/epoch and is explicitly attestation-evidence-only. `SecureEvidencePolicy` controls guest-visible, migration-serializable, compatibility-alias, debug-only and host-owned quarantined evidence.

## Authority Owner

Neutral evidence policy owns visibility. It does not own backend execution, migration, VMREAD or publication.

## Implemented Baseline

- evidence classification matrix;
- guest-visible evidence tests;
- host-owned quarantine tests;
- recomputed-after-restore and stale-epoch tests;
- attestation/debug handling as visibility policy only.

## What Remains Denied/Future-Gated

- evidence as runtime authority;
- evidence as VMREAD value source;
- debug trace as checkpoint payload;
- attestation fact as production activation evidence;
- measurement descriptor as backend execution proof.

## Forbidden Shortcuts

- `GuestVisible` evidence means activation;
- debug-only evidence migrates as guest state;
- attestation facts bypass completion fence;
- measurement descriptor opens VMREAD;
- host-owned evidence enters checkpoint image.

## Required RFC/ADR

No RFC/ADR for classification. A separate debug/attestation API RFC is needed before any stable external API is exposed.

## Code Anchors

- `DomainMeasurementDescriptor.cs`
- `SecureEvidencePolicy.cs`
- `SecureEvidencePublicationPolicy.cs`
- `SecureHostInspectionPolicy.cs`
- `SecureCompletionPublicationFence.cs`

## Documentation Anchors

- `SecureComputerefactoringNew/11_measurement_and_evidence_visibility.md`
- `SecureComputerefactoringNew/12_debug_observability_and_attestation_boundary.md`
- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md`

## Required Tests

- host-owned evidence publication denied;
- recomputed-after-restore evidence cannot be guest-visible publication state;
- migration-serializable evidence is not guest-visible publication;
- stale measurement epoch denied;
- debug-only evidence cannot become migration payload or VMREAD authority;
- evidence policy cannot authorize completion or retire.

## Required Static/Source Scans

- `GuestVisible.*activation`
- `DebugOnly.*migration`
- `attestation.*runtime authority`
- `DomainMeasurementDescriptor.*backend`
- `HostOwnedQuarantined.*publish`

## Migration/Evidence Classification

Every evidence class must be one of guest-visible, migration-serializable, compatibility-alias, recomputed-after-restore, debug-only, host-owned quarantined or denied.

## Completion/Retire Implications

Evidence visibility never substitutes for completion fence or retire rule.

## SecureCompute Activation Implications

Limited activation may cite evidence policy only as a prerequisite, not as activation proof.

## Exit Criteria

- evidence matrix complete;
- tests prove host-only and recomputed classes cannot leak;
- docs avoid evidence-as-authority wording.

Exit status: open for production publication. Classifier tests establish intended non-authority, but closure requires a single reachable `SecureEvidencePublisher`, audience-specific schemas, no alternate descriptor/debug serialization path, retired/recomputed provenance and replay-safe signing after restore.

## Dependency

Previous: `07_secure_capability_grant_epoch_activation_plan.md`. Next: `09_privileged_execution_state_owner_rfc.md`.
