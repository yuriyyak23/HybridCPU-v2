# Secure Debug Attestation API ADR/Plan

## Phase Metadata

- File name: `16_secure_debug_attestation_api_plan.md`
- Phase goal: close a neutral debug/attestation visibility API gate without turning visibility into runtime authority.
- Status: partial visibility classifier; exclusive production API/publisher reachability is open.
- Scope: debug trace, attestation report, telemetry snapshot, host-inspection metadata and compatibility-alias evidence visibility.
- No-goals: no debug trace migration, no compatibility-read authority, no activation evidence, no backend owner proof, no completion publication and no retire publication.

## ADR-SC-DEBUG-ATTESTATION-VISIBILITY

Phase 16 adds `SecureDebugAttestationVisibilityPolicy` as a descriptor-owned, fail-closed visibility classifier. The policy consumes already materialized descriptor/evidence facts and returns visibility-only results. It does not create runtime authority, compatibility-read authority, migration authority, activation evidence, backend-owner proof, completion publication or retire publication.

## Current Baseline

Debug and attestation facts are visibility surfaces. Host-owned evidence is quarantined. Debug traces cannot become guest state, checkpoint authority, backend proof, completion publication or retire publication.

## Authority Owner

The owner is the neutral evidence/debug/attestation visibility policy. Debuggers, telemetry collectors, host inspection, compatibility projection code and test evidence do not own SecureCompute runtime authority.

Accepted recommended refinement: a future single `SecureEvidencePublisher` is the only production projection path and runs after the owning effect/retire or restore-recomputation boundary. It owns audience filtering and publication only; it never owns measurement creation, backend execution, completion, retire or release approval, and it cannot publish internal certificate, grant, receipt, completion, host-pointer or backend-binding objects.

## Request/Result Vocabulary

`SecureDebugAttestationVisibilityRequest` classifies:

- `DebugTrace`
- `AttestationReport`
- `TelemetrySnapshot`
- `HostInspectionMetadata`
- `CompatibilityAliasEvidence`

The request has explicit denied shortcut flags for migration payload, compatibility-read value source, activation evidence, backend owner proof, private-memory inspection, completion publication and retire publication.

`SecureDebugAttestationVisibilityResult` publishes only classification fields:

- visibility class;
- guest-visible, host-only, debug-only and attestation-only bits;
- authority bits that are always false in Phase 16: `CreatesRuntimeAuthority`, `CreatesVmreadAuthority`, `CreatesMigrationAuthority`, `CreatesActivationEvidence`, `CreatesBackendOwnerProof`, `CompletionPublicationAuthorized` and `RetirePublicationAuthorized`.

## Preconditions For Allowed Visibility

Debug trace visibility requires all of:

- materialized secure descriptor;
- explicit debug policy;
- explicit debug-evidence policy;
- materialized, current, non-stale measurement.

Attestation report visibility requires all of:

- materialized secure descriptor;
- materialized, current, non-stale measurement;
- attestation evidence-only measurement;
- guest-visible evidence policy;
- non-host-owned evidence class.

Telemetry snapshot visibility is host-owned quarantined evidence only.

Host-inspection metadata visibility is host-only metadata and cannot request private-memory inspection.

Compatibility-alias evidence visibility requires an explicit read-only compatibility projection policy and remains visibility only.

## Denied Shortcuts

The policy denies:

- debug trace as migration payload;
- attestation report as activation evidence;
- telemetry as backend owner proof;
- host inspection as private-memory authority;
- visibility as compatibility-read value source;
- debug or attestation output as completion publication;
- debug or attestation output as retire publication.

## Migration/Evidence Classification

Debug traces are `DebugOnly`. Telemetry snapshots and host inspection metadata are host-owned/host-only. Attestation reports can be guest-visible only under evidence policy and cannot become migration authority. Compatibility-alias evidence is visibility only and cannot carry VMCS projection metadata as migration or checkpoint authority.

## VMX And Compatibility Implications

Phase 16 does not add VMX authority and does not open a compatibility-read value source. Compatibility-alias evidence can be classified only after projection policy says the alias is read-only visibility. The actual Phase 17 VMX/VMCS/`VmxCaps` zero-authority proof remains the next sequential gate.

## Compiler Implications

Phase 16 does not add compiler secure emission. `SecureComputeNoEmissionContract` remains the boundary until Phase 19.

## Code Anchors

- `SecureDebugAttestationVisibilityPolicy.cs`
- `SecureHostInspectionPolicy.cs`
- `DomainMeasurementDescriptor.cs`
- `SecureEvidencePolicy.cs`
- `SecureEvidencePublicationPolicy.cs`

## Required Tests

- debug-only evidence cannot expose host-owned facts;
- debug trace cannot serialize as guest state;
- debug trace cannot request completion or retire publication;
- attestation output cannot authorize compatibility-read values or activation evidence;
- host inspection cannot bypass private-memory policy;
- telemetry cannot satisfy backend owner proof;
- compatibility-alias evidence requires projection policy and creates no read authority;
- source scans prove no VMX, VMCS, backend execution, publication or compiler emission shortcut in the Phase 16 policy.

## Required Static/Source Scans

Use owner/path/reachability-scoped scans over `SecureDebugAttestationVisibilityPolicy.cs` and its immediate evidence/debug descriptor inputs:

- no VMCS manager, VMX execution unit or field-store dependency;
- no backend execution request/result dependency;
- no `BackendExecutionAuthorized: true`;
- no `CompletionPublicationAuthorized: true`;
- no `RetirePublicationAuthorized: true`;
- no compiler controlled-emission dependency.

Repository-wide scans for words such as completion, retire or compatibility-read are informational only unless owner/path/reachability shows SecureCompute authority.

## Bounded Rollback Procedure

Rollback Phase 16 by removing `SecureDebugAttestationVisibilityPolicy.cs`, removing `SecureDebugAttestationVisibilityPhase16Tests.cs`, reverting this file and restoring Phase 16 to future visibility API status. Do not reset unrelated Phase 13, Phase 14 or Phase 15 changes. Re-run the evidence/debug/attestation source guard and release gate after rollback.

## SecureCompute Activation Implications

Phase 16 is an observability/visibility gate only. It cannot be used as production activation evidence and cannot satisfy Phase 20 runtime activation evidence.

## Exit Criteria

- visibility API vocabulary is typed and fail-closed;
- authority shortcut requests are denied;
- host/debug/telemetry evidence cannot migrate or publish;
- source scans prove no VMX/backend/publication/compiler shortcut;
- docs state visibility only.

Exit status: open for production visibility publication. Focused policy tests do not prove that all debug, telemetry and attestation exporters pass through one publisher. Closure requires one `SecureEvidencePublisher`, disjoint audience schemas and no direct descriptor serialization path.

## Dependency

Previous: `15_secure_migration_checkpoint_restore_plan.md`. Next: `17_secure_vmx_boundary_zero_authority_plan.md`.
