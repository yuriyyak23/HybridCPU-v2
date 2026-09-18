# Secure Migration Checkpoint Restore Output Manifest Classification ADR

## Phase Metadata

- File name: `15_secure_migration_checkpoint_restore_plan.md`
- ADR ID: `ADR-SC-MIGRATION-OUTPUT-MANIFEST`
- Phase goal: close the secure migration/checkpoint/restore classification gate for future completion/retire records and output manifests.
- Status: partial classifier; checkpoint serializer, key owner, anti-replay and atomic restore protocol are open.
- Scope: checkpoint payload classes, restore validation, output-manifest entry vocabulary, completion/retire record classification and release evidence prerequisites.
- No-goals: no backend executor, no success stub, no production SecureCompute execution, no compiler secure emission and no live migration format.

## Current Baseline

Migration and checkpoint policies deny host-owned evidence, scheduler evidence, backend binding evidence, native tokens, debug traces, VMCS/compat metadata, raw measurement secrets, raw sealing keys and active host pointers. Restore validates policy epoch plus measurement/grant rules. Phase 15 adds `SecureOutputManifestClassificationPolicy`, which classifies output-manifest entries for future positive paths without creating runtime, completion or retire publication authority.

## Authority Owner

Migration authority is owned by neutral migration policy and restore admission:

- `SecureCheckpointPayloadPolicy` owns payload-class classification;
- `SecureMigrationAdmissionPolicy` owns checkpoint payload admission and restore validation;
- `SecureOutputManifestClassificationPolicy` owns output-manifest coverage/classification;
- `SecureCompletionRetirePublicationAuthorityPolicy` remains the only Phase 14 owner for completion/retire publication decisions.

Checkpoint payloads and output manifests are evidence classification surfaces only. They do not create backend execution authority, completion publication authority, retire publication authority or release authority.

Accepted release-scope decision: checkpoint/restore is denied for the first register-local limited contour. A later owner-specific protocol must remove the unknown-value default allow, use exhaustive versioned payload taxonomy, quiesce the domain, seal a canonical manifest under a named key owner, validate monotonic anti-replay state, create fresh descriptor generation/grants and reopen atomically. Interrupted or partially validated restore leaves the domain Disabled.

## Output Manifest Vocabulary

`SecureOutputManifestEntryKind` names the required one-path manifest coverage:

- `RequestState`;
- `InternalBackendResult`;
- `InternalCompletionRecord`;
- `GuestVisibleOutput`;
- `RetireVisibleState`;
- `RecomputedAfterRestoreState`.

`SecureOutputManifestClassificationResult` carries the decision, entry kind, payload decision, manifest-classified bit, checkpoint-payload inclusion bit, restore-revalidation requirement and explicit `CreatesRuntimeAuthority`, `CreatesCompletionPublicationAuthority` and `CreatesRetirePublicationAuthority` false bits.

## Classification Rules

Required classification:

- request state is descriptor or guest-visible state;
- internal backend result is manifest coverage only and is not checkpoint authority;
- internal completion record is manifest coverage only and is not checkpoint or restore authority;
- guest-visible output is guest-visible or shared state only;
- retire-visible state is guest-visible architectural state only;
- recomputed-after-restore state requires restore validation proof and is rebuild-only manifest coverage.

Complete manifest coverage requires all six entry kinds. Missing coverage blocks Phase 20 positive execution evidence and any future independent offline release evidence for the named path; it does not create a Phase 22 allow path.

## Denied Payload Classes

The following classes remain denied in checkpoint payloads and output manifests:

- host-owned evidence;
- scheduler evidence;
- backend binding evidence;
- native token evidence;
- debug traces as guest state;
- raw measurement secrets;
- raw sealing keys;
- active host pointers;
- VMCS projection metadata;
- compatibility projection metadata.

Private memory classification still requires a complete sealed/encrypted payload contract with neutral key owner, evidence policy and restore validation proof, and it carries no raw sealing key.

## Completion And Retire Record Paths

Phase 15 does not publish completion or retire effects.

- Internal completion records are required manifest entries for future positive-path evidence.
- Internal completion records are not checkpoint payloads and are not restore authority.
- Retire-visible state must be guest-visible architectural state before it can be classified in a manifest.
- Recomputed-after-restore state is rebuilt after restore and requires restore validation proof.
- Completion and retire publication still require Phase 14 publication authority and later Phase 20/22 positive-path evidence.

The current classifier is not exhaustive: `SecureMigrationAdmissionPolicy.AdmitCheckpointPayload` and `SecureCheckpointPayloadPolicy.Classify` use default allowed branches. Unknown/future payload classes must be denied by an explicit registry before this phase can close.

## Backend Result Boundary

The backend-result owner boundary remains separate from migration classification. `InternalBackendResult` in a manifest is coverage only; it does not prove backend success and does not unlock completion/retire publication.

## Restore Preconditions

Restore remains fail-closed unless the current migration policy proves:

- materialized/current policy epoch;
- measurement revalidation or re-attestation where policy requires it;
- current measurement epoch when revalidation/reattestation is required;
- current grant epoch and scalar provenance;
- restore-time grant provenance revalidation or rederivation;
- private-memory policy plus complete sealed/encrypted payload contract when private memory is present.

## Failure Taxonomy

Phase 15 names these failure classes:

- missing required manifest entry;
- owner/path/reachability unclassified entry;
- wrong payload class for an entry kind;
- private-memory contract missing or incomplete;
- forbidden host-owned evidence/native-token/backend-binding class;
- debug trace as guest state;
- VMCS projection metadata;
- compatibility projection metadata;
- raw secret;
- active host pointer;
- recomputed-after-restore state without restore validation proof.

## Owner/Path/Reachability Scoped Source-Scan Policy

Source scans must be scoped to SecureCompute owner/path/reachability files. Repository-wide hits for neutral trap routing or generic completion infrastructure are not sufficient evidence of SecureCompute authority. Phase 15 scans must include:

- `SecureOutputManifestClassificationPolicy.cs`;
- `SecureCheckpointPayloadPolicy.cs`;
- `SecureMigrationAdmissionPolicy.cs`;
- Phase 15 focused tests.

The scan must prove no Phase 15 source dependency on VMX execution units, VMCS managers, VMREAD/VMWRITE helpers, `VmxCaps`, trap-route services, backend execution request/result types, publication true flags or compiler controlled-emission shortcuts.

## VMX Boundary

VMX compatibility projection remains zero-authority. VMCS and compatibility metadata are denied as migration/output manifest authority. A future path may project compatibility state only after a separately authorized neutral result and the Phase 17/20/22 gates; Phase 15 does not create that projection authority.

## Compiler Boundary

Compiler secure emission remains closed. Phase 15 adds no instruction encoding, operand format, capability-aware load/store/fetch or VMX secure-mode emission.

## Bounded Rollback Procedure

To roll back Phase 15 classification without a destructive repository-wide reset:

1. remove `SecureOutputManifestClassificationPolicy.cs`;
2. remove `SecureMigrationCheckpointRestoreOutputManifestPhase15Tests.cs`;
3. restore this ADR, index, gap matrix, conformance matrix, release gate, backlog and WhiteBook wording to Phase 14-next status;
4. rerun focused migration, release/conformance, VMX boundary and compiler no-emission slices;
5. rerun scoped scans proving forbidden payloads, VMX metadata, backend execution and publication shortcuts remain denied.

## Code Anchors

- `SecureMigrationDescriptor.cs`
- `SecureMigrationAdmissionPolicy.cs`
- `SecureCheckpointPayloadPolicy.cs`
- `SecureOutputManifestClassificationPolicy.cs`
- `SecureCompletionRetirePublicationAuthorityPolicy.cs`
- `DomainMeasurementDescriptor.cs`
- `SecureGrantAuthorityPolicy.cs`

## Documentation Anchors

- `00_securecompute_activation_refactoring_index.md`
- `01_current_state_and_gap_matrix.md`
- `21_conformance_negative_positive_test_matrix.md`
- `22_limited_securecompute_release_gate.md`
- `23_open_decision_backlog.md`
- `Documentation/SecureCompute WhiteBook/`

## Required Tests

- host-owned evidence rejected;
- scheduler evidence rejected;
- backend binding evidence rejected;
- native token evidence rejected;
- raw measurement secret rejected;
- raw sealing key rejected;
- active host pointer rejected;
- VMCS/compat metadata rejected;
- completion record is manifest-only and not checkpoint/restore authority;
- internal backend result is manifest-only and not backend success evidence;
- missing output-manifest entry blocks future positive-path evidence;
- complete manifest classification creates no runtime, completion publication or retire publication authority;
- recomputed-after-restore state requires restore validation proof;
- owner/path/reachability classification is required before an entry is accepted;
- restore validates policy epoch, grant epoch/provenance and measurement requirements;
- private memory requires complete sealed/encrypted contract;
- compiler no-emission remains closed.

## Required Static/Source Scans

- `HostOwnedEvidence.*Denied`
- `BackendBindingEvidence.*Denied`
- `NativeTokenEvidence.*Denied`
- `RawMeasurementSecret.*Denied`
- `RawSealingKey.*Denied`
- `ActiveHostPointer.*Denied`
- `VmcsProjectionMetadata.*Denied`
- `CompatibilityProjectionMetadata.*Denied`
- `InternalCompletionRecord`
- `RecomputedAfterRestoreState`
- `CreatesRuntimeAuthority: false`
- `CreatesCompletionPublicationAuthority: false`
- `CreatesRetirePublicationAuthority: false`

## Migration/Evidence Classification

Allowed manifest payload classes are neutral policy descriptors, guest-visible state, secure shared memory and private memory only with a complete sealed/encrypted contract. Internal backend result and internal completion record are manifest coverage only. Denied classes remain host-owned, native-token, raw-secret, active-pointer or compatibility-only.

## Completion/Retire Implications

Migration and restore validation do not publish completion, retire or activation evidence. Completion/retire publication remains future-gated behind Phase 14 authority plus a later positive execution path and release evidence.

## SecureCompute Activation Implications

Phase 15 provides partial output/restore classification for future paths. Unknown payload default-allow and the missing checkpoint protocol keep it open; it is not production SecureCompute activation.

## Exit Criteria

- payload matrix complete;
- output-manifest vocabulary implemented;
- six required output entries classified;
- denied payload tests pass;
- restore validation requirements explicit;
- no host evidence, native token, raw secret, active pointer, VMCS metadata or compatibility metadata becomes authority;
- one-path output manifest coverage is defined before positive execution or release approval;
- backend execution and compiler secure emission remain closed.

Exit status: open for checkpoint/restore. Classification tests are retained, but completion requires exhaustive deny-by-default payload registration, a versioned canonical serializer, neutral sealing/key owner, monotonic anti-replay counter, fresh binding/grant issuance and atomic reopen with interruption rollback.

## Dependency

Previous: `14_secure_completion_retire_publication_plan.md`. Next: `16_secure_debug_attestation_api_plan.md`.
