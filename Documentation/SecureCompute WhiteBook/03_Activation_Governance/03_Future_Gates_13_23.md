# Future Gates

## Exact Next Gate

`secure_hypercall_backend_owner_rfc.md` is closed as a bounded
owner-contract/identifier gate. `secure_completion_retire_publication_plan.md`
is closed as a fail-closed completion/retire publication authority gate.
`secure_migration_checkpoint_restore_plan.md` is closed as a fail-closed
migration/output-manifest classification gate.
`secure_debug_attestation_api_plan.md` is closed as a fail-closed
debug/attestation visibility gate. `secure_vmx_boundary_zero_authority_plan.md`
is closed as a fail-closed named-path VMX zero-authority gate. Nested child
intent remains future/design-fenced and does not authorize nested execution,
mutable nested state, Shadow VMCS authority or nested publication.
`compiler_no_emission_to_controlled_emission_gate.md` is closed as an explicit
no-compiler-change decision gate. Positive runtime execution activation is the
current production-oriented planning gate and remains future-gated because no
named runtime owner/path/reachability chain is locally proven. The conformance
matrix is closed only for current negative/future-gated evidence. The
limited-release classifier is fail-closed, and production release remains
denied.

`ADR-SC-HYP-BACKEND-OWNER` defines typed transport, decoded-leaf, service, owner, request/result, argument, replay, cancellation and migration vocabulary. The implemented admission result is proof-only and cannot open execution or publication.

`SecureHypercallBackendOwnerAbiRegistry` assigns the exact decoded leaf,
SecureCompute service ID and backend owner ID. `VMCALL` opcode `259`, trap
reason `18` and fixture `0x10` are not allocations and do not own authority.

`ADR-SC-COMP-RETIRE-PUBLICATION` defines the backend-result, completion-owner
and retire-owner ladder. `SecureCompletionRetirePublicationAuthorityPolicy`
denies publication from proof-only backend-owner admission, admitted-denied
secure hypercall recognition, registry-backed admission, generic trap-route
flags and VMX projection-only paths. Backend execution and compiler secure
emission remain closed.

`ADR-SC-MIGRATION-OUTPUT-MANIFEST` defines request state, internal backend
result, internal completion record, guest-visible output, retire-visible state
and recomputed-after-restore state classification. `SecureOutputManifestClassificationPolicy` denies host-owned evidence, scheduler evidence, backend binding evidence, native tokens, raw secrets, raw sealing keys, active host pointers, VMCS metadata and compatibility metadata. Internal backend results and completion records remain manifest-only.

`ADR-SC-DEBUG-ATTESTATION-VISIBILITY` defines debug trace, attestation report,
telemetry snapshot, host-inspection metadata and compatibility-alias evidence
visibility classification. `SecureDebugAttestationVisibilityPolicy` denies visibility as migration payload, compatibility-read value source, activation evidence, backend owner proof, private-memory inspection, completion publication or retire publication.

`ADR-SC-VMX-NAMED-PATH-ZERO-AUTHORITY` defines VMX zero-authority
classification for positive-looking compatibility, backend-owner, publication,
output-manifest, visibility, and future activation paths.
`SecureComputeNamedPathVmxZeroAuthorityPolicy` denies VMX activation, `VmxCaps`
grant, VMCS state store, active pointer identity, compatibility read/write
authority, VMCS checkpoint authority, completion publication and retire publication.

`ADR-SC-COMPILER-NOEMISSION-CONTROLLED-GATE` defines no-compiler-change and
future controlled-emission classification. `SecureComputeControlledEmissionGatePolicy` preserves no-emission as the only allowed current compiler decision and denies secure backend helper, secure hypercall helper, sideband metadata and future controlled-emission requests.

`ADR-SC-POSITIVE-RUNTIME-EXECUTION-ACTIVATION` defines positive runtime
activation classification. `SecurePositiveRuntimeExecutionActivationPolicy`
records that proof-only admission, admitted-denied recognition, publication
vocabulary, manifest-only evidence, debug/attestation visibility, VMX
projection, nested child intent and compiler no-emission are not a locally
proven runtime owner/path.

`ADR-SC-CONFORMANCE-NEGATIVE-POSITIVE-MATRIX` defines conformance evidence
classification. `SecureComputePhase21ConformanceEvidencePolicy` packages current negative/future-gated evidence for release-gate consumption only and creates no runtime, publication, VMX, compiler, nested or release authority.

`ADR-SC-LIMITED-SECURECOMPUTE-RELEASE-GATE` defines fail-closed release
classification. `SecureComputePhase22LimitedReleaseGatePolicy` rejects current release approval until a named positive runtime path and complete release evidence exist.

## Remaining Sequence

| Gate | Future responsibility |
| --- | --- |
| 13 | typed proof-only owner/service ADR implemented; exact production identifier allocation closed |
| 14 | fail-closed completion and retire publication separation gate closed |
| 15 | fail-closed migration/output-manifest classification gate closed |
| 16 | fail-closed debug and attestation visibility gate closed |
| 17 | fail-closed named-path VMX zero-authority gate closed |
| 18 | nested child-intent owner RFC remains future/design-fenced; nested execution, mutable nested state, Shadow VMCS authority and nested publication remain denied |
| 19 | fail-closed no-compiler-change decision gate closed |
| 20 | positive secure runtime execution activation remains future-gated until one owner/path/reachability chain is proven |
| 21 | conformance matrix closed only for current negative/future-gated evidence |
| 22 | limited release gate remains fail-closed until named positive path evidence exists |
| 23 | open-decision quarantine |

## Conditions Before Positive Backend Work

A future backend path requires:

- named neutral owner;
- accepted owner-specific RFC/ADR;
- typed request/result contract;
- capability and evidence gates;
- migration classification;
- negative tests for every denied shortcut;
- explicit completion and retire policy;
- VMX zero-authority proof;
- compiler boundary decision;
- release-gate approval for one named path.

None of these requirements can be replaced by WhiteBook wording.
