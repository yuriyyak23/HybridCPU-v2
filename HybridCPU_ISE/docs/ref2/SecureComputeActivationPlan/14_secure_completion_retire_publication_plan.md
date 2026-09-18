# ADR-SC-COMP-RETIRE-PUBLICATION

## Decision Status

- Phase: `14`.
- ADR identifier: `ADR-SC-COMP-RETIRE-PUBLICATION`.
- Status: partial fail-closed policy model; production backend-result, completion and retire owner reachability are open.
- Date: 2026-06-17.
- Maximum current SecureCompute result: no SecureCompute completion publication and no SecureCompute retire publication on proof-only, admitted-denied or registry-backed Phase 13 paths.
- Explicitly closed: backend execution, production SecureCompute execution and compiler secure emission.

Phase 14 defines the authority boundary between a backend result, an internal completion record, completion publication and retire publication. It does not implement a backend executor, backend success stub, production SecureCompute execution path or compiler secure emission.

Accepted recommended refinement: use distinct immutable types and one-shot links: `SecureExecutionRequest` is an offered attempt, `SecureExecutionReceipt` is the named backend owner's proof of the exact bounded effect, `SecureCompletionRecord` is the completion owner's exactly-once consumption of that receipt, and the retire record/effect is the architectural retire owner's visibility decision. `SecureAdmissionCertificate` is legality only and cannot substitute for any of these types. Existing Phase 14 vocabulary remains a classifier model until these production owners and links exist.

## Publication Authority Owners

SecureCompute publication is split into typed owners:

| Concept | Current owner decision |
| --- | --- |
| backend-result owner | future neutral backend-result owner only; current proof-only/admitted-denied paths do not produce a backend result |
| completion owner | separate neutral completion owner field in `SecureCompletionPublicationAuthorityRequest`; current paths fail closed before this owner can publish |
| retire owner | separate neutral retire owner field in `SecureRetirePublicationAuthorityRequest`; retire is not derived from completion fence presence |
| VMX compatibility projection | zero-authority projection vocabulary only |
| compiler | no-emission remains closed; compiler output is not publication evidence |

Completion owner and retire owner may later be implemented by the same neutral runtime service, but Phase 14 treats them as separate typed authority concepts. A completion owner does not imply retire owner authority.

## Backend-Result Owner Boundary

The backend-result owner is below the publication owners and above admission. The boundary is:

1. Phase 13 registry-backed owner/service admission can accept proof only.
2. Admission does not create `SecurePublicationBackendResultState.InternalNeutralResult`.
3. A future backend-result owner must be neutral, materialized, current-epoch and owner/path reachable.
4. Only after that future result exists can a completion owner evaluate an internal completion record.

`SecureCompletionRetirePublicationAuthorityPolicy` encodes this boundary. Current inputs from `SecureBackendOwnerAdmissionResult`, `SecureIoHypercallAdmissionResult` and `SecureHypercallBackendContractAdmissionResult` return denied Phase 14 publication results.

## Publication State Ladder

The Phase 14 ladder is:

1. `NoBackendResult`
2. `InternalBackendResult`
3. `InternalCompletionRecord`
4. `CompletionPublication`
5. `RetirePublication`

No step implies a later step. A completion fence can be necessary for step 4, but it is not backend-result evidence and is not retire owner evidence. Retire publication requires an already published completion plus a separate retire owner, explicit retire fence, safe evidence class and safe migration class.

## Request And Result Vocabulary

Production code now has neutral Phase 14 vocabulary:

- `SecurePublicationPathKind`
- `SecurePublicationBackendResultState`
- `SecurePublicationLadderStep`
- `SecureCompletionRetirePublicationDecision`
- `SecureCompletionPublicationAuthorityRequest`
- `SecureRetirePublicationAuthorityRequest`
- `SecureCompletionRetirePublicationResult`
- `SecureCompletionRetirePublicationAuthorityPolicy`

The vocabulary is policy-only. It is not a backend execution request/result model, not a production SecureCompute execution contract and not a compiler emission contract.

## Exact Preconditions For Completion Publication

Completion publication requires all of the following:

1. `SecurePublicationPathKind.NeutralRuntimeBackendResult`.
2. `SecurePublicationBackendResultState.InternalNeutralResult`.
3. A current, materialized, neutral backend-result owner.
4. A materialized internal completion record after the backend result.
5. A current, materialized, neutral completion owner.
6. Completion owner proof that includes completion-fence validation.
7. `SecureCompletionPublicationFence.CanPublishCompletion == true`.
8. Owner/path reachability from the secure backend result to the secure completion owner.
9. No VMX, VMCS, `VmxCaps`, trap projection, compatibility projection, migration metadata or compiler sideband as authority.

Current Phase 12/13 paths intentionally fail before item 1 or item 2.

## Exact Preconditions For Retire Publication

Retire publication requires all completion publication preconditions plus all of the following:

1. A prior Phase 14 completion publication result.
2. A current, materialized, neutral retire owner.
3. Retire owner proof that includes retire-fence validation.
4. `SecureCompletionPublicationFence.CanPublishRetire == true`.
5. Retire evidence class is `GuestArchitecturalState` or `CompatibilityAlias`.
6. Retire migration class is `RecomputedAfterRestore` or `GuestArchitecturalState`.
7. Owner/path reachability from the secure completion result to the secure retire owner.

Completion fence alone, route flags alone, a completion record alone or Phase 13 owner acceptance cannot satisfy these retire preconditions.

## Current Prohibitions

The following are denied by code/tests:

- proof-only backend owner admission cannot publish completion;
- proof-only backend owner admission cannot publish retire;
- admitted-denied secure hypercall recognition cannot publish completion;
- admitted-denied secure hypercall recognition cannot publish retire;
- registry-backed Phase 13 owner/service proof cannot publish completion;
- registry-backed Phase 13 owner/service proof cannot publish retire;
- Secure I/O policy admission cannot publish completion or retire;
- completion fence alone cannot cross the backend-result owner boundary;
- generic `TrapCompletionRoutePolicy` flags are not SecureCompute authority without secure owner/path reachability;
- VMX compatibility projection is zero-authority for SecureCompute publication;
- migration/checkpoint payloads exclude host evidence, native tokens, raw secrets, active pointers and compatibility metadata;
- compiler no-emission remains closed.

## Failure Taxonomy

Phase 14 fail-closed decisions:

- `DeniedNoBackendResult`
- `DeniedProofOnlyAdmission`
- `DeniedAdmittedDeniedAdmission`
- `DeniedRegistryBackedProofOnlyAdmission`
- `DeniedPolicyAdmissionOnly`
- `DeniedGenericTrapRouteNotSecureAuthority`
- `DeniedVmxProjectionOnly`
- `DeniedBackendResultOwner`
- `DeniedCompletionRecordMissing`
- `DeniedCompletionOwnerMissing`
- `DeniedCompletionOwnerSource`
- `DeniedCompletionOwnerEpoch`
- `DeniedCompletionOwnerProof`
- `DeniedCompletionFence`
- `DeniedCompletionPublicationRequired`
- `DeniedRetireOwnerMissing`
- `DeniedRetireOwnerSource`
- `DeniedRetireOwnerEpoch`
- `DeniedRetireOwnerProof`
- `DeniedRetireFence`
- `DeniedRetireEvidence`
- `DeniedRetireMigrationClass`

The enum also contains future positive decision names for the policy ladder, but no current production path reaches them from Phase 12 or Phase 13 admission.

## Owner/Path/Reachability Scoped Source-Scan Policy

Phase 14 scans must be scoped to SecureCompute owner/path reachability. Repository-wide scans for every `CompletionPublicationAuthorized: true` or `RetirePublicationAuthorized: true` are not valid evidence because neutral trap routing has legitimate generic route flags.

Required scoped scans:

- SecureCompute admission/result files must keep `BackendExecutionAuthorized`, `CompletionPublicationAuthorized` and `RetirePublicationAuthorized` false for Phase 12/13 paths.
- `SecureCompletionRetirePublicationAuthorityPolicy.cs` must deny proof-only, admitted-denied, registry-backed Phase 13, generic trap-route flag and VMX projection-only inputs.
- SecureCompute publication policy must not call `TrapCompletionRouteService`, `TrapCompletionRouteDescriptor` or `TrapCompletionPublicationFence`.
- VMX `VMCALL` frontend must remain projection-only and must not construct a SecureCompute completion record.
- Migration/checkpoint policy must continue denying host evidence, native tokens, raw secrets, active pointers, VMCS projection metadata and compatibility projection metadata.
- Compiler no-emission scans must remain closed for secure backend emission shortcuts.

## Migration Classification

Completion/retire records are not checkpoint authority in Phase 14. Current classification:

| Class | Phase 14 migration decision |
| --- | --- |
| internal backend result | future path must classify; no current payload |
| internal completion record | not checkpoint authority; future positive path must classify |
| completion publication output | not migration authority unless future RFC classifies guest-visible state |
| retire publication output | requires safe evidence and migration classes before retire |
| host evidence / scheduler evidence / backend binding evidence | denied |
| native tokens | denied |
| raw measurement secret / raw sealing key | denied |
| active host pointer | denied |
| VMCS projection metadata / compatibility projection metadata | denied |

Restore must revalidate or recompute any future allowed class. Compatibility projection metadata cannot be restored as SecureCompute authority.

## VMX Compatibility Projection

VMX projection may happen only after a neutral result and publication owners complete their gates. Phase 14 does not grant VMX any SecureCompute authority.

For the current `VMCALL` path:

- opcode `259` is transport vocabulary;
- `VmExitReason.VmCall == 18` is trap vocabulary;
- decoded leaf, service ID and owner ID are Phase 13 registry concepts;
- trap route flags and compatibility completion projection do not become SecureCompute publication authority.

## Compiler Decision

No compiler change is approved. SecureCompute compiler no-emission remains closed. Controlled emission remains Phase 19 work and still requires a separate RFC/ADR after a positive neutral runtime owner exists.

## Bounded Rollback

If a Phase 14 regression is found:

1. Treat every Phase 14 publication result as `DeniedNoBackendResult`.
2. Remove the affected caller path to `SecureCompletionRetirePublicationAuthorityPolicy`.
3. Keep Phase 13 `SecureHypercallBackendOwnerAbiRegistry` identifiers intact unless the regression is identifier-specific.
4. Keep `SecureIoHypercallAdmissionPolicy` as policy-only/admitted-denied.
5. Keep VMX `VMCALL` projection using projection-only route/fence denial.
6. Keep compiler no-emission closed.
7. Re-run Phase 14 focused tests, SecureCompute release/conformance tests, VMX boundary tests, migration tests, compiler no-emission tests and scoped source scans.
8. Update release wording to state Phase 14 is open until the regression is fixed.

Rollback must not use repository-wide destructive reset and must not disturb unrelated runtime owners.

## Code And Test Anchors

- `SecureCompletionPublicationFence.cs`
- `SecureCompletionRetirePublicationAuthorityPolicy.cs`
- `TrapCompletionRoutePolicy.cs`
- `TrapCompletionPublicationFence.cs`
- `SecureBackendOwnerAdmissionPolicy.cs`
- `SecureIoHypercallAdmissionPolicy.cs`
- `SecureHypercallBackendContractAdmissionPolicy.cs`
- `SecureHypercallBackendOwnerAbiRegistry.cs`
- `SecureCheckpointPayloadPolicy.cs`
- `SecureMigrationAdmissionPolicy.cs`
- `SecureComputeNoEmissionContract.cs`
- `SecureCompletionRetirePublicationAuthorityPhase14Tests.cs`
- `SecureHypercallBackendOwnerPhase13Tests.cs`
- `SecureIoHypercallPolicyTests.cs`
- `VmxTrapCompletionRouteRetirePublicationHardeningTests.cs`
- `VmxHypercallBackendOwnerDecisionReadinessTests.cs`

## Exit Criteria

Implemented and tested:

- typed publication ladder and request/result vocabulary;
- distinct backend-result, completion and retire owner concepts;
- proof-only, admitted-denied and registry-backed Phase 13 publication prohibition;
- completion fence alone denial across backend-result boundary;
- retire owner and explicit retire fence separation;
- owner/path/reachability source-scan policy;
- VMX zero-authority publication decision;
- migration and compiler boundaries retained.

The deny behavior and conceptual ladder are confirmed as policy tests. Phase 14 is not production enforcement: no SecureCompute backend-result producer or CPU completion/retire caller reaches this policy. Closure requires opaque owner-created results, exactly-once completion consumption and in-order retire integration with exact attempt identity.

## Dependency

Previous: `13_secure_hypercall_backend_owner_rfc.md`. Next: `15_secure_migration_checkpoint_restore_plan.md`.
