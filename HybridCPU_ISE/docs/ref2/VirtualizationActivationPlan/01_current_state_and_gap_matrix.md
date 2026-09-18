# Phase 01 - Current State And Gap Matrix

Status: docs/planning phase. No runtime activation.

## 2026-06-11 Audit Contract

- File name: `01_current_state_and_gap_matrix.md`.
- Purpose: classify current code/tests/docs state and the gap to any limited runtime virtualization path.
- Status: readiness-only inventory; no activation approval.
- Scope: VMREAD partial projection, VMCALL admitted-denied path, VMCS projection vocabulary, SecureCompute/nested/lane/compiler/migration boundaries.
- No-goals: no backend execution, no VMWRITE, no nested execution, no SecureCompute activation through VMX, no lane/stream authority, no compiler emission, no migration authority.
- Code anchors: `RuntimeBoundaryAdmissionService.cs`, `VmxCompatibilityAdmissionService.cs`, `VmxCompatibilityAdmissionService.Traps.cs`, `VmcsReadOnlyValueProjectionService.cs`, `VmcsFieldProjectionSchema.cs`, `HypercallBackendAdmissionPolicy.cs`, `TrapCompletionRoutePolicy.cs`, `TrapCompletionPublicationFence.cs`.
- Authority owner: neutral runtime descriptors/services only; this gap matrix is not runtime authority.
- Required RFC/ADR: none for classification; any positive path requires owner-specific RFC/ADR with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: every surface is implemented/projection-only/denied/model-helper/future-gated/forbidden; unproven items are `не доказано` and `должно оставаться denied`.
- Tests/static scans: baseline plan-doc guards, VMREAD denial tests, VMCALL missing-owner tests, VMCS/active-pointer absence scans.
- Risks: treating readiness corpus, documentation, or test coverage as activation evidence.
- Next-gate dependency: Phase 02 static gates.

## Phase Goal

Record the exact baseline inherited from `VirtualiztionRefactoringNew` and classify the gap from readiness closure to limited active runtime virtualization.

## 2026-08-06 Evidence Scope

This matrix was rechecked against the current working copy, not copied from the external archive. The evidence manifest is `evidence/2026-08-06-current-worktree-evidence.json`.

- Current local `HEAD`: `ddfffa2d7b86fb3ece21f8d21c6447ae47ee3868`; the worktree is dirty, so observations that include Phase 36/37 are not clean-SHA evidence.
- External-audit SHA: `f794ea378779fe153a206af5fe287571884b1bc4`; not present locally, ancestry to current `HEAD` not proven.
- New VRT reports use remote SHA `d3814d1f332f083034d3b245f807a45f97792070`; it is not the active local subject and its plan-absence claim cannot describe current `HEAD`.
- Configured local origin: `yaksysdev/HybridCPU-v2`, which differs from the remote named by the audit.
- The plan and its guard test are tracked at current `HEAD`; the audit's plan-absent conclusion does not describe current `HEAD`.
- `CloseToHSL` is canonical and has 933 tracked files at current `HEAD`; obsolete `CloseToRTL` has zero tracked files. Clean ancestor `55807df77978a960382fa913dda4e7ace0093a6b` contains E1, and clean ancestor `a594d10abcbe8593d23fed16310af30706893452` contains the Phase 34 fail-closed substrate; neither contains current P1/P2.

E0 clean-subject provenance is closed. E1 typed SafetyVerifier admission is implemented for fault-only canonical transport. Owner acceptance, exact leaf, backend, completion, retire and release remain independently blocked.

The 2026-08-08 `deep-research-report 2.md` recheck adds four current facts: Phase 37 P2 is closed only under `TESTING`; VMCALL decode qualification stores `rs1`/`rs2` selectors rather than runtime values; the v1 D2 `AcceptedCommitSha` single-object model is insufficient as final non-circular acceptance provenance; and positive completion/retire factories remain constructible compatibility shapes, not E3/E5/E6 authority.

## Verified Operational Classification

| Class | Current surface | Verified result | What it does not prove |
| --- | --- | --- | --- |
| pipeline-executed | frozen VMX decode, `VmxMicroOp`, typed SafetyVerifier E1 admission, dispatcher execute, retire boundary | VMX is recognized, fully serializing/system-singleton; canonical issue-packet lane 7 can carry a live issuer-bound E1 certificate, then execution deterministically becomes `SecurityPolicyViolation`; retire converts every valid compatibility effect to fail-closed fault | no VMX operation success, no VMREAD writeback, no VMCALL backend, no completion, no successful VMX retire |
| direct projection API | `AdmitVmReadProjection`; guarded `GuestCr0`/`GuestCr4`; direct VMCALL trap admission | explicitly invoked callers/tests can obtain read-only projection or admitted-denied DTOs after their local gates | not canonical ISA execution, not architectural publication, not a SafetyVerifier-issued authorization |
| model/scaffolding | generated VMCS schema, `VmxRetireEffect` positive-shaped factories, positive route descriptors, publication fence, nested/SecureCompute/lane/compiler/migration policies | vocabulary and policy shapes exist and can be unit-tested directly | object construction or positive-looking result does not prove a production caller or authority chain |
| unreachable/isolated | compatibility services from production dispatcher; positive completion/retire descriptors from VMX frontend; hypercall backend executor | caller search finds service/tests/showcase only for VMREAD and service/tests only for VMCALL; dispatcher creates a fault directly | no production composition and no architectural success path |

`VmxRetireEffect` is a compatibility carrier, not evidence of execution. Its positive-shaped factories are inert in the current canonical retire path because `ApplyRemovedFrontendFailClosedEffect` maps every valid effect to a fault. Tests or direct callers that construct such an effect prove only the object's shape.

`CompletionRecordCompatibilityProjection.cs` also contains public compatibility factories that construct a `CompletionRecord` after receiving a positive-looking fence DTO. Current caller search finds only tests, and no admission handler, dispatcher, pipeline or retire caller reaches the factory. This is isolated compatibility scaffolding, not proof of publication. E5 remains blocked until record creation is owner-bound and located behind a neutral completion token; adding any production caller to this factory is a forbidden shortcut.

## External Audit Verdicts On The Current Working Copy

| Audit conclusion | Verdict | Current evidence |
| --- | --- | --- |
| No production-connected successful VMX virtualization path | подтверждено / confirmed | `ExecutionDispatcherV4.VmxCompatibility.cs` returns `VmxFault`; `MicroOp.IO.cs` creates only `VmxRetireEffect.Fault`; retire maps every valid VMX effect to fault |
| Frozen VMX decode and scheduling metadata are live | подтверждено / confirmed | `VmxMicroOp` is `VmxSerial`, hard-pinned to system singleton lane 7, and carries register/resource metadata consumed by ordinary legality paths |
| Direct VMREAD projection exists but is not architectural VMREAD | подтверждено / confirmed | production caller search finds no dispatcher/retire consumer; direct callers are tests and `SimpleAsmApp.Showcase`; no projection-result `RetireRecord` path exists |
| `GuestCr0`/`GuestCr4` remain wholly denied | опровергнуто / refuted | current worktree has guarded read-only projection after descriptor/domain/address-space/epoch/visibility/`RevalidatedAfterRestore`/conformance gates; mutation, backend, completion, retire and wider fields remain denied |
| VMCALL is admitted-denied and lacks backend execution | подтверждено / confirmed | handler uses `MissingNeutralOwner`, `ProjectionOnlyDenied`; admission enum has no allowed backend decision and service ends in `DeniedNeutralBackendOwnerRfcAdr` |
| An accepted owner-specific VMCALL RFC/ADR and exact numeric leaf exist | опровергнуто / refuted | only draft identifier/descriptor skeleton exists; no accepted state, numeric leaf allocation, allowed admission result, or executor exists |
| SafetyVerifier issues a typed virtualization admission certificate | подтверждено для E1 / confirmed for E1 | current worktree has exclusive live issuance/validation bound to opcode/operation, VT/domain, owner context, source/working slot, bundle, attempt and replay epoch; exact leaf and later owner identities are explicitly absent, so the certificate remains fault-only |
| Completion and retire authority are separated in shape | подтверждено частично / partially confirmed | completion-only and coupled descriptors/fence results exist, but public booleans/descriptors are scaffolding and no owner-bound backend-result token or retire grant connects them to production |
| A `CompletionRecord` may be created only by an owner-bound production completion path | опровергнуто / refuted | the neutral fence can construct a record from boolean/enum inputs in direct calls; current frontend does not reach the positive branch, so this is a design gap, not active publication |
| VMREAD is an architecturally executable instruction | не доказано / not proven | decode and hazard metadata exist, but canonical execute/retire faults and there is no writeback from the projection result |
| VMWRITE, nested execution, SecureCompute-through-VMX, lane/stream passthrough and compiler VMX emission are active | опровергнуто / refuted | current VMX production chain contains no such authority or backend composition; related objects remain denial/projection/model surfaces |
| The plan is absent from the repository | опровергнуто for current HEAD; historically confirmed only for the audit's reported snapshot | clean subject `HEAD` tracks the 32-file baseline and guard; the evidence update adds Phase 32 without changing production code; this does not retroactively validate the audit archive on `f794...` |
| Evidence is reproducible from the clean local subject SHA | подтверждено / confirmed | `b6d4871...` was clean, `CloseToHSL` is tracked, all 13 anchors match, lineage regeneration matches, and VMX-refactoring tests pass 137/137 without external files |
| A green readiness/source-scan suite proves activation | опровергнуто / refuted | focused tests prove denied/projection contracts only; no positive canonical backend/completion/retire integration test exists |
| Neutral owner must be organizationally or repository-external | опровергнуто / refuted | WhiteBook requires independence from VMX/VMCS authority; an in-repository runtime-domain owner is valid if governance attribution/review and non-compatibility ownership are explicit |
| A monolithic `VirtualizationRuntimeContext` is required | подтверждено частично / partially confirmed | stable attempt/domain/epoch bindings are required, but current `DomainRuntimeContext` and separate domain owners already exist; any aggregate must be a non-owning view accepted by ADR, not a duplicate authority store |
| VMCALL currently captures the runtime numeric leaf value | confirmed only at the PR-C E1-bound canonical seam | `VirtualizationOperandSnapshot` captures the full `Rs1` value once, binds attempt/domain/slot/replay/restore identities and fixes `Rs2`/`Rd` to x0; compatibility qualification remains non-authoritative |
| Repository CI and projection-generation drift checks are absent | опровергнуто частично / partially refuted | `.github/workflows/ci.yml` and `VerifyProjectionLineage.ps1` exist; a clean containing SHA for the current E1 worktree and future positive slices is still required |

### Audit Blocker Register B0-B13

| Audit blocker | Verdict on current worktree | Disposition |
| --- | --- | --- |
| B0 plan/evidence provenance | закрыто для clean subject / closed for clean subject | local-only clean manifest binds `b6d4871...`; `CloseToHSL` is tracked and `CloseToRTL` is obsolete with zero tracked files; closure grants no implementation permission |
| B1 unconditional production fault | подтверждено / confirmed | keep fail-closed until E0-E4 are completed by authorized owners |
| B2 forgeable boolean admission/no typed certificate | опровергнуто / refuted for E1 | typed issuer-live certificate is implemented; compatibility booleans remain non-authoritative; this does not open E2/backend work |
| B3 no canonical virtualization state owner | подтверждено / confirmed | no VMCS-like store or active pointer may be inferred; first candidate remains no-state only unless a separate state-owner RFC is accepted |
| B4 no machine-materialized owner, exact-leaf registry or executor | closed through isolated E3 only | attributable D2, exact lookup, immutable O1, PR-D E2 and default-off exact PR-E executor/E3 exist; no canonical production composition, completion or retire exists |
| B5 completion descriptors/factories are constructible scaffolding, not owner tokens | подтверждено / confirmed | `CompletionRecordCompatibilityProjection` is test-called only; E5 requires an attempt-bound backend-result token and neutral completion owner before any production record construction |
| B6 no owner-bound retire grant | подтверждено / confirmed | E6 must bind live post-Stage-B/ROB identity; bools/enums/`VmxRetireEffect` are insufficient |
| B7 VMREAD lacks architectural writeback | подтверждено / confirmed | claim only direct compatibility projection; canonical VMREAD remains not proven |
| B8 VMWRITE remains denied | подтверждено / confirmed | keep all fields read-only/denied; first slice excludes VMWRITE |
| B9 nested is non-production model/projection | подтверждено частично / partially confirmed | helpers/models exist, but no production VMX composition caller or child-intent authority was found; separate track only |
| B10 memory/I/O/lane/stream cannot derive VMX authority | подтверждено / confirmed | neutral subsystem runtimes may be active for their own purposes, but no VMX passthrough authority or frontend caller was found |
| B11 compiler no-emission/authority gap | подтверждено / confirmed | compiler-facing validation/scaffolding is non-authoritative; the optional Compiler Gate follows runtime E7 evidence and is not an authority stage |
| B12 active-path migration payload/determinism gap | подтверждено частично / partially confirmed | denial/recompute policies and tests exist, but there is no active VMCALL attempt/completion/retire chain to migrate or prove deterministic |
| B13 positive integration/CI evidence gap | подтверждено частично / partially confirmed | focused local denied/projection tests now run green, refuting only the audit's local-not-run limitation; clean-SHA CI, positive canonical integration and determinism evidence remain absent |

### Audit-2 Blocker Extensions

| Blocker | Verified state | Required disposition |
| --- | --- | --- |
| B14 D2 acceptance provenance | confirmed | v1 same-object `AcceptedCommitSha` is structural/negative substrate only; machine D2 requires immutable `VirtualizationDecisionSpecV2` plus later `VirtualizationDecisionAcceptanceRecordV2` over spec SHA+digest and matched review |
| B15 canonical operand value | PR-C closed/fault-only | the canonical issue/materialization seam captures selector/full-value and live E1 identities once; E2/E3 must consume the immutable snapshot and may not re-read registers |
| B16 publication non-forgeability | confirmed | positive completion/fence/retire shapes are constructible but isolated; production requires separate opaque consume-once E3 receipt, E5 token and E6 grant |
| B17 P2/prototype boundary | closed TESTING-only | P2 is default-off model evidence at the canonical seam; it carries no leaf/value and authorizes no P3 or production E2/E3/E4 |
| B18 local release provenance | blocked for current worktree | current P1/P2 is not in a clean containing SHA; local clean evidence is required before release, but remote Git access is outside this task |

## Documentation Reconciliation

- The WhiteBook statements that `GuestCr0`/`GuestCr4` are wholly denied are stale. The accurate claim is guarded read-only direct projection only, with all other effects denied.
- The internal v8 Phase 07 text that calls VMX/VMCS `runtime-complete` and names `VmxExecutionUnit`/`VmcsManager` is a historical pre-freeze status record, not current architecture.
- The plan's closed readiness gates remain evidence about denials and ordering. They are not permission to implement any positive arrow.

## Historical Baseline (2026-06-11)

- Phase 16 closed with GO for documentation/readiness/test/static-gate maintenance and NO-GO for active runtime virtualization.
- The current code has partial VMREAD value projection through neutral owners, including guarded read-only `GuestCr0`/`GuestCr4` projection through a neutral privileged execution-state descriptor.
- VMCALL is admitted-denied: trap projection is evaluated, backend admission is evaluated, but no neutral backend owner is materialized.
- Completion route and publication fence exist, but VMX frontend paths use `ProjectionOnlyDenied`.
- VMCS/VMCSv2 is generated projection vocabulary, not mutable state.
- SecureCompute, nested, Lane6/Lane7/Stream, compiler emission, migration/checkpoint remain bounded by denial or future gates for virtualization activation.

## Owner Of Authority

Authority remains with neutral runtime descriptors and services:

- execution domain descriptors for materialized guest PC/SP/flags;
- memory domain descriptors for address-space roots, second-stage roots, VPID tag, and CR3 target count;
- completion records for recomputed completion projection;
- trap policy, hypercall backend admission, route policy, and publication fence for VMCALL-adjacent paths;
- typed capability grants and evidence policy descriptors;
- migration/checkpoint descriptors and restore validation;
- SecureCompute descriptors only for SecureCompute runtime policy, not VMX authority.
- privileged execution-state descriptor/policy plus a separate field-specific projection service for `GuestCr0`/`GuestCr4`; owner acceptance does not itself authorize projection or any mutation/backend/publication effect.

## What Can Be Implemented

- A complete gap matrix over all VMX-facing surfaces.
- Static source scans that distinguish compatibility vocabulary from authority code.
- Owner-specific RFC/ADR templates for future positive paths.
- Additional negative tests proving current denied states remain denied.
- The Phase 36 TESTING-only no-state/no-payload research probe and its diagnostics are implemented as a separate prototype lane; this is service-level research execution, not a production VMX backend or architectural instruction path.

## What Remains Denied/Future-Gated

- Any backend execution path.
- VMWRITE.
- New VMREAD fields without owner/value/evidence/migration/test package.
- Any widening of `GuestCr0`/`GuestCr4` beyond the implemented read-only field-local projection.
- Runtime-owned completion/retire publication from VMX frontend.
- Nested execution.
- SecureCompute activation through VMX/VMCS/`VmxCaps`.
- Lane6/Lane7/Stream passthrough as virtualization authority.

## Forbidden Shortcuts

- Treating a generated schema entry as availability.
- Treating admission as backend execution.
- Treating completion route classes as publication permission.
- Treating migration/checkpoint image contents as authority.
- Reusing guest execution or memory read-only views for host aliases.

## Required RFC/ADR

No RFC/ADR is required to classify current state. Any positive implementation after this phase requires a new owner-specific RFC/ADR.

## Code Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Events/Hypercalls/HypercallBackendAdmissionPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Routing/TrapCompletionRoutePolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Records/TrapCompletionPublicationFence.cs`

## Documentation Anchors

- `HybridCPU_ISE/docs/ref2/Old/VirtualiztionRefactoringNew/00_refactoring_plan_index.md`
- `HybridCPU_ISE/docs/ref2/Old/VirtualiztionRefactoringNew/15_final_readiness_review_and_next_work_order.md`
- `HybridCPU_ISE/docs/ref2/Old/VirtualiztionRefactoringNew/16_external_audit_activation_readiness_addendum.md`
- `Documentation/Virtualization WhiteBook/16_Current_State_And_Closure_Matrix.md`
- `Documentation/Virtualization WhiteBook/17_Roadmap_And_Residual_Risk.md`
- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md`

## Required Tests

- Existing: `VmxDocumentationFinalClosureTests`, `VmxGeneratedReadOnlyVmReadValueProjectionTests`, `VmxMemoryOwnedVmReadValueProjectionTests`, `VmxExecutionOwnedVmReadValueProjectionTests`, `GuestCr0Cr4ReadOnlyProjectionTests`, `PrivilegedExecutionStateOwnerPolicyTests`, `VmxControlLikeVmReadDenialTests`, `VmxAdmittedDeniedVmCallTrapPathTests`, `VmxHypercallBackendAdmissionPolicyTests`, `VmxTrapCompletionRouteOwnerTests`.
- Add: a new activation-plan baseline test that asserts this file and `00_virtualization_activation_refactoring_index.md` classify the current state as readiness-only.

## Required Static/Source Scans

- Scan for `VmcsManager`, `IVmcsManager`, `VmxExecutionUnit`, active VMCS pointer, mutable VMCS field store, raw `TryReadScalarField` fallback, direct `WriteFieldValue`, and direct VMX authority imports.
- Scan docs for claims that imply broad runtime support, VMX authority, VMWRITE support, SecureCompute through VMX, or nested support.

## Migration/Evidence Classification

This phase publishes classification only. It must not add migration payloads. VMREAD values remain projection output; completion-owned values remain recomputed; host-owned evidence, scheduler evidence, backend handles, native tokens, debug traces, and compatibility projection metadata remain excluded from checkpoint authority.

## Completion/Retire Implications

No completion or retire path is opened. VMCALL remains admitted-denied and publication fences remain closed for VMX frontend paths.

## Exit Criteria

- Every current surface is classified as implemented, projection-only, denied, model/helper-only, future-gated, or forbidden.
- Every positive gap points to an owner-specific RFC/ADR.
- No item is classified as active runtime virtualization.

## Dependency On Previous/Next Phase

Depends on the closed readiness corpus. Phase 02 turns this classification into non-regression gates.
