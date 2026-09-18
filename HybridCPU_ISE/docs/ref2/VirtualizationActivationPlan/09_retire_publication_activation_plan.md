# Phase 09 - Future-Gated Retire Publication Plan

Status: PR-H closed for the exact Phase-38 no-state leaf only. Every path without live E6 remains fault-only; E7 and release remain gated.

## 2026-08-10 PR-H E6 Closure Addendum

`DomainHypercallRetireOwner` is a per-CPU neutral owner. The existing stable WB
retire-order selector and fault truncation choose the candidate; after complete
batch prevalidation the owner consumes one live owner-bound E5 into one opaque
`VirtualizationRetireGrant` E6 bound to E1 attempt, VT/domain, source/working
slot, post-Stage-B operation, physical lane, retire-window identity, order epoch
and restore generation. The ordinary WB finalizer consumes E6 exactly once.
The exact operation publishes no register, memory, VM-state or redirect effect.
Without E6 the pre-existing `VmxRetireEffect` remains
`SecurityPolicyViolation`; compatibility never issues or consumes E6.

PR-H does not provide drain/checkpoint/restore closure, migration or determinism
evidence required by E7 and therefore does not satisfy the release gate.

## 2026-06-11 Audit Contract

- File name: `09_retire_publication_activation_plan.md`.
- Purpose: define the final retire publication gate for a future backend-executed and completion-published path.
- Status: `future-gated`; current VMX frontend paths do not retire as backend success.
- Scope: neutral retire rule, commit/rollback, ordering/fence semantics, failure behavior, downstream compatibility projection.
- No-goals: no retire from completion alone, no VMX frontend retire authority, no retire for VMWRITE/nested/SecureCompute/lane/compiler paths.
- Code anchors: `TrapCompletionPublicationFence.cs`, `TrapCompletionRoutePolicy.cs`, VMX retire/projection helpers covered by `VmxTrapProjectionPublicationFenceTests` and `VmxTrapCompletionRouteRetirePublicationHardeningTests`.
- Authority owner: neutral retire policy plus publication fence result; compatibility retire vocabulary is downstream only.
- Required RFC/ADR: owner RFC/ADR must define retire behavior with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: completion publication != retire publication; retire allowed only when explicit rule, rollback, evidence, and migration checks pass.
- Tests/static scans: completion-allowed/retire-denied cases, rollback denial, host-evidence denial, migration-class denial, exact-leaf positive only after RFC.
- Risks: treating completion success or `VmExitReason` as final architectural success.
- Next-gate dependency: Phase 18 release gate after Phases 06-09 and 15-16 are complete.

## Phase Goal

Define the explicit retire publication rule required before any successful VMCALL path can become architecturally visible as a retired runtime virtualization effect.

## Historical Baseline (2026-06-11)

Current VMCALL admitted-denied paths carry `TrapCompletionPublicationFenceResult` with both publication flags false because backend execution is missing. Separately, future-gated neutral fence scaffolding can return `CompletionPublishedRetireDenied`: completion true, retire false. This does not open production VMCALL or Phase 09 retire publication.

## ISE-HV-RETIRE-PUBLICATION-GATE-09 - Closure Record

Closure date: 2026-06-18.

State: closed `FUTURE-GATED RETIRE GATE / COMPLETION-NOT-RETIRE / NO-RETIRE-PUBLICATION / NO-PRODUCTION-CHANGE`.

Closure result: retire publication remains a separate future-gated owner rule; current VMCALL paths do not retire as backend success, and completion-only scaffolding does not open retire.

This closure does not accept an RFC/ADR, authorize backend execution, publish completion, publish retire, treat completion publication as retire publication, create VMX frontend retire authority, consume `CompletionPublishedRetireDenied` as success, or grant authority to VMX, VMCS, `VmxCaps`, SecureCompute, compiler, migration, lane, stream, tests, docs, scans, `VmExitReason`, completion kind, route descriptor flags, or fence shape.

Closure invariants:

- Completion publication is not retire publication.
- `RetirePublicationAllowed == false` remains the default for completion-only scaffolding.
- Current VMCALL admitted-denied paths carry both completion and retire publication flags false.
- VMX frontend cannot publish retire effect without neutral permission.
- SecureCompute `AllowedProofOnlyNoExecution` and VMX `AllowedAdmittedDenied` remain non-retire evidence.
- Phase 18 release wording cannot consume Phase 09 until a future accepted owner rule exists.

## Owner Of Authority

Neutral retire policy and the publication fence result. VMX compatibility retire vocabulary is downstream representation, not permission.

## What Can Be Implemented

After Phases 06-08:

- a neutral retire rule for the approved leaf;
- explicit commit/rollback behavior;
- ordering/fence semantics;
- denial behavior when completion succeeded but retire permission fails;
- projection of a VMX-compatible retire/completion result only after retire rule permits it.
- a completion success may still be denied retire and must remain a blocked, explicitly classified outcome.

## What Remains Denied/Future-Gated

- Retire for any non-RFC leaf.
- Retire when backend result is denied.
- Retire when completion fence denies.
- Retire for VMWRITE, nested, SecureCompute, lanes, or compiler-generated backend paths.

## Forbidden Shortcuts

- Treating completion publication as retire publication.
- Retiring from VMX frontend result state.
- Publishing guest-visible success before rollback/no-host-evidence checks.
- Encoding `VmExitReason` as the retire authority.
- Treating `AllowedProofOnlyNoExecution` or `AllowedAdmittedDenied` as retire permission.

## Required RFC/ADR

The owner RFC/ADR must define retire behavior. If the first leaf is no-state, the retire rule must still explicitly state what architectural state, if any, changes.

Phase 38 accepts `RetirePolicy = PreciseE5BoundNoStateRetire` for `PROBE_NO_STATE_V1`. The retire owner may issue one opaque E6 only for the canonical retire head with matching live E5, attempt/VT/effect, retire-window identity, ordering epoch and restore generation, and only if the attempt is neither squashed nor already retired. The operation has zero register, memory and VM-state writes and no explicit redirect; consuming E6 permits normal precise instruction retirement, not a fabricated x0 write or compatibility effect.

The positive path must be integrated into the existing retire-window/head/order contour. It must not reuse `VmxRetireEffect.VmCall`, `VmxRetireEffect.InterceptExit` or an equivalent compatibility success factory: current `ApplyRemovedFrontendFailClosedEffect` deliberately faults every valid VMX compatibility effect and remains unchanged for all paths without E6.

## Code Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Records/TrapCompletionPublicationFence.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Routing/TrapCompletionRoutePolicy.cs`
- VMX retire/projection helpers referenced by `VmxTrapProjectionPublicationFenceTests` and `VmxTrapCompletionRouteRetirePublicationHardeningTests`

## Documentation Anchors

- `HybridCPU_ISE/docs/ref2/Old/VirtualiztionRefactoringNew/08_trap_completion_route_and_retire_publication.md`
- `Documentation/Virtualization WhiteBook/12_Trap_Intercept_Completion_Retire.md`
- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md` for completion/retire separation discipline.

## Required Tests

- Completion allowed but retire denied does not publish retire effect.
- Retire allowed requires completion allowed.
- Retire denied on rollback failure.
- Retire denied if host-owned evidence would become guest-visible.
- Retire denied if migration class is absent.
- Positive retire only for the approved leaf.

## Required Static/Source Scans

```powershell
rg -n "RetirePublicationAllowed|RetirePublicationAuthorized|PublishRetire|retire publication|CompatibilityExit" HybridCPU_ISE/CloseToHSL/Core
```

Matches must show explicit fence/retire rule, not frontend-only publication.

## Migration/Evidence Classification

Retire publication may expose guest-visible status only through the approved evidence class. Host-owned evidence, backend handles, debug traces, scheduler evidence, and native tokens remain host-owned and non-migratable.

`VmxRetireEffect` is compatibility data and never the E6 authority, even when a positive factory creates a non-faulted shape. Production retire requires a separate opaque consume-once `VirtualizationRetireGrant` issued by the canonical retire owner from one live E5 token and bound to ROB/retire slot, VT/domain, attempt and ordering/restore epochs. Squash, exception, duplicate, wrong slot/VT/domain or stale generation must deny before architectural state changes.

## Completion/Retire Implications

This is the final gate before a scoped path can be called limited active runtime virtualization. Without it, the path may at most be backend-executed with unpublished or non-retired result. Completion is not retire.

## Exit Criteria

- Retire rule exists and is tested.
- Completion and retire remain separately denied by default.
- VMX frontend cannot publish retire effect without neutral permission.

## Dependency On Previous/Next Phase

Depends on Phase 08. Phase 18 uses this as a release-gate prerequisite.
