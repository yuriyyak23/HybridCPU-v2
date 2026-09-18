# External SecureCompute Analysis Reconciliation And Recommended Decisions

Status date: 2026-08-07.

This document reconciles the two 2026-08-06 external analysis inputs with the current local source tree. It records plan-level recommended decisions, not implemented authority, executable reachability, an ADR approval or release evidence.

## Reviewed Inputs And Provenance

| Input | Reviewed fact | SHA-256 | Evidence treatment |
| --- | --- | --- | --- |
| `docs/ref2/1/SC/deep-research-report SC.md` | 207 lines; reports remote revision `d3814d1f332f083034d3b245f807a45f97792070` | `83f1717e2057b80f56463411a0c80ac20460acc00a2a43488f124f350685164c` | audit hypotheses and recommendations only; the reported revision is unavailable in the local object database |
| `docs/ref2/1/SC/Исследование-SecureCompute.md` | 702 lines; reviews the plan archive and refines dependency order and authority separation | `376c2d677c1ddc18fd916ce54d83c84328eb43cc2883e0b497b385432851042c` | architecture recommendation only until matched to source, ADR and executable tests |
| current local source | `b6d4871e0f06ebde07015e393c0d36af0362f506`; dirty worktree | n/a | current implementation fact, with dirty-tree disclosure; not immutable release evidence |

Evidence levels used in this reconciliation:

- E3: reachable production code plus executable positive and negative tests;
- E2: production source contract or executable negative gate without a positive effect path;
- E1: documentation, ADR/RFC proposal, static/source-string or document-string guard;
- E0: absent, unexecuted or unverifiable claim.

E1 cannot close an execution, authority or release blocker. E2 policy classes are not E3 effect-path enforcement.

## Revalidated Findings

The important audit findings remain confirmed on the current local source:

1. no canonical SecureCompute composition root connects decoded operation identity, SafetyVerifier, issue, execute, backend receipt, completion and retire;
2. `SecureComputeDomainDescriptor` has public constructors, no lifecycle registry and two full-descriptor carriers through `context.SecureCompute ?? request.SecureDescriptor`;
3. no SecureCompute-specific SafetyVerifier certificate exists; the untracked virtualization admission prototype is fault-only, zero-authority and is not SecureCompute implementation evidence;
4. `SecureGrantAuthorityPolicy` validates caller-provided handles, epochs, bounds and `runtimeOwnerMaterialized`; no mint/reserve/consume/revoke ledger exists;
5. caller-selected `SecureDomainOperationClass` defaults to `Ordinary`, and most non-ordinary classes reach a generic `AllowedSecureOperation`; this is neither canonical taxonomy nor exhaustive operation-specific admission;
6. secure memory, I/O, migration, evidence and completion/retire types are policy/classifier islands outside the real memory, DMA/IOMMU, backend and retire effect roots;
7. memory regions and shared buffers use ordered first-match lookup without canonical overlap/duplicate rejection;
8. `SecureCheckpointPayloadPolicy` has an unknown-value default-allow branch;
9. disabled behavior has admission tests, not paired whole-machine observational-equivalence traces;
10. Phase 22 consumes caller booleans and always denies; it is useful as a negative regression classifier but cannot become a release approver.

The compiler boundary also remains confirmed: `SecureComputePolicyAdmissionOnly` is excluded from production lowering and Phase 19 denies every SecureCompute emission request. The compiler already distinguishes test-only/model evidence from production authority, which permits a hermetic test-only carrier profile design without opening product emission.

## Compatibility With HybridCPU-v2 Architecture

| Recommended decision | Compatibility result | Local architecture anchor |
| --- | --- | --- |
| generated ISA registry owns static operation vocabulary | accepted | RF-02/RF-03 generated manifest and static-authority cutover |
| canonical decoder freezes decoded identity | accepted | RF-04 canonical decoded contracts and semantic identity |
| SafetyVerifier owns dynamic legality and issues an opaque admission witness | accepted | current checker-owned `LegalityDecision` and certificate semantics |
| compiler/test artifacts never outrank runtime legality | accepted | compiler authority taxonomy and no-emission whitebook |
| VMX/VMCS/`VmxCaps` remain projection-only and zero-authority | accepted | SecureCompute Phase 17 and virtualization boundaries |
| separate request, receipt, completion and retire authority | accepted | existing backend/retire ADR philosophy and SecureCompute Phase 14 split |
| generated exhaustive tables fail closed on unknown values | accepted | manifest parity, invalid-ID and no-fallback rules |
| sharded neutral runtime registry instead of guest-visible singleton state | accepted as recommended design | CPU/core ownership model; requires a SecureCompute owner ADR before implementation |

No recommendation justifies CHERI/tagged-memory semantics, a VMX SecureCompute mode, generic VMCALL authority, nested execution or product compiler emission.

## Accepted Recommended Authority Model

The following are accepted as the preferred plan decisions. They remain open implementation work until their owner RFC/ADR, production path and tests are complete.

### Canonical operation identity

- the generated ISA registry owns vocabulary and schema version;
- canonical decode materializes the frozen decoded identity;
- SafetyVerifier derives `SecureOperationKind`; callers, compiler hints, VMX fields and request DTOs cannot supply it;
- every authoritative operation enum reserves `Unknown = 0`; `Ordinary` is an explicit known non-secure operation, not the default for unknown values;
- a generated exhaustive coverage table makes a new operation fail CI until owner, policy and negative/positive tests are named;
- unknown, unmapped or identity-mismatched operations deny before issue.

Misleading positive-looking classifier names must be removed from authoritative paths. Results such as `AllowedSecureOperation` and `AllowedAdmittedDenied` should become explicit non-execution vocabulary such as `PolicyValidatedNoExecution` and `RecognizedButExecutionDenied`. Compatibility aliases, if temporarily retained, cannot cross SafetyVerifier or issue and require deletion tests.

`RuntimeBoundaryAdmissionService` is retained only as an internal policy helper under SafetyVerifier composition. Its generic result cannot cross issue and cannot be described as CPU Stage-B enforcement.

### Descriptor lifecycle and binding

`SecureDomainRegistry` is the recommended sole materialize/activate/quiesce/revoke/destroy owner. It owns immutable full descriptors and publishes only an opaque binding:

`SecureDomainBinding(RegistryId, DomainId, Generation, PolicyDigest, Seal)`.

The lifecycle is `Created -> Active -> Quiescing -> Revoked/Destroyed`. Policy mutation creates a new generation. Stale bindings, certificates, grants, maps, DMA intents and checkpoint payloads deny. `DomainTag`, registry seal or policy digest alone is identity, not capability.

### Admission, grants, effects and publication

The authority types are deliberately separate:

| Type | Sole owner | Meaning | Explicit non-meaning |
| --- | --- | --- | --- |
| `SecureAdmissionCertificate` | SafetyVerifier | exact attempt legality, identity and bounded effect envelope | not a grant, effect receipt, completion or retire authority |
| `SecureGrantLedger` entry/handle | neutral ledger owner | resource rights and revocation state | not operation legality or evidence of an effect |
| `SecureExecutionRequest` | issue/backend transport owner | one accepted attempt offered to one named backend | not proof that execution happened |
| `SecureExecutionReceipt` | named backend effect owner | exact bounded effect happened once for the accepted attempt | not completion or architectural visibility |
| `SecureCompletionRecord` | completion owner | one receipt was consumed exactly once | not retire publication |
| retire record/effect | architectural retire owner | in-order architectural visibility | not reusable execution authority |

The admission certificate binds the canonical operation, descriptor generation, domain/address space, VT/owner context, source/working/physical lane, bundle/FSP decision, replay/attempt epoch, referenced grant identities and the maximum effect envelope. It is opaque, issuer-bound and single-use through backend acceptance. Replay or restore never revives it.

The grant ledger lifecycle is `Mint -> Lookup/Reserve -> Consume -> Revoke`. Entries bind subject, resource, rights, descriptor generation, resource/ledger epochs, nonce, use count and state. Effect-bearing backend, DMA and hypercall grants are one-shot; any bounded read lease requires a separately named policy. Restore reissues fresh grants and permanently invalidates prior handles.

### Memory, I/O, migration and evidence

- memory and shared-buffer configuration is materialized through canonical sorted interval maps with checked arithmetic, stable IDs/digests and rejection of zero-length, overflow, overlap, duplicate or ambiguous entries;
- memory enforcement is two-phase: pre-translation intent admission and post-translation effect verification, including fetch, load, store, atomic, cache fill/writeback, prefetch, assists, FSP and DMA;
- the first shared-buffer release, when separately reached, uses bounded copy-in/copy-out staging rather than direct host/device shared mappings;
- device, IOMMU mapping, DMA submission and DMA completion have separate neutral owners and one-shot intent identity;
- checkpoint/restore is denied for the first register-local limited contour; a later protocol must use exhaustive payload taxonomy, sealed versioned manifests, anti-replay state, fresh registry generations/grants and atomic reopen;
- a single evidence publisher may project facts only after the owning effect/retire boundary; it does not own the measurement, backend effect, completion or retire decision.

## Corrected Dependency Order

The accepted order supersedes the former ordering that placed full disabled equivalence after registry/grant/map refactors or placed every controlled test carrier after limited release:

1. commit the plan and reproducible audit manifest to an immutable clean revision with exact SDK, generator commands, filters and hashes;
2. build the end-to-end disabled observational-equivalence harness and run it after every semantic PR;
3. establish canonical ISA operation identity and remove caller authority over the taxonomy;
4. implement the descriptor registry and opaque binding;
5. implement the grant ledger;
6. implement the SafetyVerifier-issued admission certificate;
7. carry the certificate through Stage B, SMT/FSP and issue without reconstruction;
8. add a hermetic test-only controlled-carrier profile;
9. prove public canonical decode -> certificate -> issue -> expected deny end to end;
10. add one named side-effect-free backend transport probe with no memory, I/O, VMX, nested path or product compiler emission;
11. separately implement one register-local deterministic positive operation and produce an execution receipt;
12. connect exact completion and retire owners;
13. connect one evidence publisher after retire;
14. implement an independent offline release-evidence verifier;
15. connect memory, IOMMU/DMA and hypercall effect domains in separate later changes;
16. keep checkpoint/restore denied for the first release and implement it only through its own later protocol;
17. propose product compiler emission only through a future RFC after named limited runtime release;
18. propose nested execution only through a separate future RFC.

Steps 8 and 9 do not open product compiler or assembler APIs. The test-only profile must be absent from production packages, require an explicit test build/profile and non-production signer, emit only a frozen conformance vector through the public canonical decoder, and remain runtime-denied by default. Handcrafted post-decode carriers do not count as end-to-end proof.

## First Positive Contour Recommendation

The preferred first effect path, after steps 1-10 close, is one deterministic register-local measured transform with:

- one secure VT and one named owner;
- no memory, I/O, DMA/IOMMU, hypercall, VMX, compatibility projection, nested execution or checkpoint/restore;
- FSP disabled initially;
- no product compiler emission;
- explicit register inputs and bounded register output;
- one certificate, one request, one receipt, one completion and exactly-once retire;
- fault/squash/replay paths that publish no partial output;
- a kill switch that stops admission, drains/discards in-flight attempts and revokes the domain generation.

This recommendation does not authorize implementation of a success stub. The transport-only neutral probe must close before the register-local effect, and the named production owner/path/reachability chain must be reviewable and executable.

C1 memory, IOMMU/DMA, hypercall and checkpoint protocols may remain hard-denied for this register-local contour. That permits only a claim scoped to the C0-closed local operation; it does not close or weaken any C1 blocker.

## Disabled Equivalence Gate

The differential harness compares architectural, not incidental host, observations across SecureCompute absent and explicitly Disabled configurations:

- architectural registers and memory;
- exceptions/faults and issue/retire ordering;
- VT scheduling for SMT widths 1, 2 and 4;
- FSP off/on nomination and replay behavior;
- memory and I/O contention outcomes;
- completion, public evidence/telemetry and replay digests.

Wall-clock time, GC timing, host thread IDs and unrelated logging order are excluded unless product semantics expose them. Any semantic PR that cannot reproduce equal architectural traces keeps activation hard-off.

## Permanent Phase 22 Split

`SecureComputePhase22LimitedReleaseGatePolicy` remains permanently deny-only. It must not be converted into approval by flipping the final branch or trusting its caller booleans.

A separate offline `SecureComputeReleaseEvidenceVerifier` is the recommended future release owner. It consumes an immutable evidence bundle containing exact SHA, clean-tree state, SDK/profile digest, generator and generated-output hashes, test filters/results, owner/path/reachability proof, bypass inventory, migration decision, rollback drill and independent signatures. Its output is a build-profile artifact bound to the reviewed SHA; it is not a runtime certificate, descriptor, grant or SafetyVerifier input.

Phase 22 remains the negative/future-gated runtime classifier even after such a verifier exists. Limited release can be claimed only from the independent artifact-backed review process for one named path.

## Phase Impact

| Phase | Reconciled status/decision |
| --- | --- |
| 04 | move complete disabled equivalence to the second implementation change and keep phase partial until paired machine traces pass |
| 05 | adopt registry-owned immutable descriptors and opaque generation binding |
| 06 | adopt decoder-derived taxonomy, SafetyVerifier certificate and internal-helper role for generic admission |
| 07 | adopt a stateful mint/reserve/consume/revoke ledger; handle validation remains partial |
| 11-12 | adopt canonical disjoint maps; keep memory and device effects separately blocked |
| 14 | adopt request -> receipt -> completion -> retire type and owner separation |
| 15-16 | deny first-contour checkpoint/restore; later protocol and one post-retire publisher remain separate |
| 18 | remains unconditionally future/design-fenced |
| 19 | retain product no-emission; add only the separately fenced early test-only carrier profile |
| 20 | remains a pre-activation evidence classifier, not execution authority |
| 21 | remains closed only as negative/future-gated conformance; test-only carrier tests do not prove release |
| 22 | remains permanently deny-only; future release verification is a separate offline owner |

## Current Claim Boundary

Positive runtime execution and limited/production release remain forbidden. The decisions above are recommended architecture for resolving blockers; none is implemented by this documentation change.

Phase 18 remains future/design-fenced, Phase 20 remains a pre-activation evidence gate, Phase 21 remains a negative/future-gated conformance matrix, and Phase 22 remains a fail-closed deny-only classifier.
