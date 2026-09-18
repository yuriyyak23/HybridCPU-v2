# Secure Capability Grant Epoch Activation Plan

## Phase Metadata

- File name: `07_secure_capability_grant_epoch_activation_plan.md`
- Phase goal: preserve Layer 2 as descriptor/grant authority discipline and prepare activation-grade grants.
- Status: partial handle-validation gate; grant mint/revoke/reissue ownership is open.
- Scope: typed grants, authority bounds, monotonic derivation, provenance metadata, epochs and revocation.
- No-goals: no CHERI ISA, no capability registers, no tagged memory and no capability-aware LOAD/STORE/FETCH.

## Current Baseline

`SecureGrantHandle`, `SecureGrantAuthorityPolicy`, `SecureAuthorityBounds`, `SecurePolicyDerivationRecord` and `SecureRevocationEpoch` implement descriptor-level checks. Compatibility projection and guest scalar materialization are denied as grant authority.

## Authority Owner

`SecureGrantAuthorityPolicy` validates caller-supplied facts but does not own a grant collection. The required authority owner is a future `SecureGrantLedger` with one mint/revoke/reissue path, monotonically increasing collection epoch, domain/effect bounds and non-replay nonce.

Accepted recommended lifecycle: `Mint -> Lookup/Reserve -> Consume -> Revoke`. Each ledger entry binds subject, resource, rights, descriptor generation, domain/resource/ledger epochs, nonce, use count and state. Backend, DMA and hypercall effect grants are one-shot. Any reusable read-like lease must be bounded and separately named. The ledger, not a caller boolean, decides materialization, liveness and consumption. Restore creates fresh entries and permanently invalidates old handles.

## Implemented Baseline

- typed grant validation by requested operation scope;
- stale epoch and revoked grant tests;
- monotonic child derivation tests;
- explicit descriptor-level grant inputs to secure policy admission.

## What Remains Denied/Future-Gated

- granting through `VmxCaps`;
- granting through VMREAD projection;
- materializing secure grants from guest scalar state;
- CHERI-like pointer authority;
- migration of tag/provenance memory format.

## Forbidden Shortcuts

- `SecureGrantHandle` as CPU capability register;
- provenance hash as pointer provenance;
- epoch as hardware tag;
- compatibility projection as grant source;
- grant presence as backend execution.

## Required RFC/ADR

No RFC/ADR for grant hardening. A positive backend owner RFC must reference this grant policy.

## Code Anchors

- `SecureGrantHandle.cs`
- `SecureGrantAuthorityPolicy.cs`
- `SecureAuthorityBounds.cs`
- `SecurePolicyDerivationRecord.cs`
- `SecureRevocationEpoch.cs`
- `CapabilityDescriptorSet`

## Documentation Anchors

- `SecureComputerefactoringNew/10_capability_grant_monotonicity.md`
- `SecureCompute Plan/08-layer2-cheri-like-authority-discipline-plan.md`
- `SecureCompute Docs/Сравнение-SecureCompute-и-CHERI.md`

## Required Tests

- missing grant denied;
- stale epoch denied;
- revoked grant denied;
- non-monotonic derivation denied;
- child authority expansion denied;
- compatibility projection materialization denied;
- guest scalar materialization denied;
- typed grant accepted only for the requested scope.
- duplicate reservation/consumption denied;
- cross-domain, cross-resource, wrong-subject and nonce replay denied;
- domain generation, resource generation or ledger epoch change invalidates lookup atomically;
- restore reissue never preserves the old handle/nonce.

## Required Static/Source Scans

- `VmxCaps.*SecureGrant`
- `VMREAD.*SecureGrant`
- `TryMaterializeFromGuestScalar.*Allowed`
- `capability-aware LOAD`
- `capability-aware STORE`
- `capability-aware FETCH`
- `tagged memory`

## Migration/Evidence Classification

Grant handles may be restored only when migration policy permits and provenance/epoch validation succeeds. Raw provenance secrets and host grant stores are denied.

## Completion/Retire Implications

Grant admission is not completion publication, retire publication or backend success.

## SecureCompute Activation Implications

Any future positive path must name the required grant scope and epoch policy.

## Exit Criteria

- grants are mapped to operation classes;
- negative grant tests cover stale/missing/non-monotonic/revoked cases;
- no ISA-level capability semantics are introduced.

Exit status: open for authority. Validation tests are retained, but completion requires one ledger issuer, atomic revoke, removal of `runtimeOwnerMaterialized` booleans and hard-coded `true`, restore reissue, cross-domain/stale/nonce replay tests and ledger lookup on every effect path.

## Dependency

Previous: `06_stage_b_secure_admission_activation_plan.md`. Next: `08_measurement_evidence_visibility_activation_plan.md`.
