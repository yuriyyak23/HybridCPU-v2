# Phase 18 - Release Gate For Limited Runtime Virtualization

Status: `PR-J CLOSED FOR DEVELOPMENT-LOCAL EXACT PROFILE / DEFAULT-DISABLED UNTIL EXPLICIT PROFILE ACTIVATION`. No activation is approved by this document alone; no broad activation is approved by this document.

Current-state authority: `VirtualizationActivationStatusV1.json` is normative. Historical closure language below defines claim boundaries but does not override current stage/gate status.

PR-I closure is not a release gate: exact no-state E6 plus E7
drain/restore/determinism do not themselves authorize compiler emission or a
limited release claim. This phase remains `NO-GO` until separately authorized,
SHA-bound release review and rollback evidence are completed.

## 2026-06-11 Audit Contract

- File name: `18_release_gate_for_limited_runtime_virtualization.md`.
- Purpose: define what may be claimed only after exactly one owner-specific path is implemented and verified.
- Status: release gate definition only; no activation is approved by this document.
- Scope: owner RFC/ADR, exact scope, admission, backend execution, completion route, publication fence, retire rule, capability/evidence, migration, rollback, adjacent denials, claim boundary.
- No-goals: no all-VMX support, no feature completeness, no nested, no SecureCompute via VMX, no VMWRITE, no compiler emission, no lane passthrough, no host aliases/controls.
- Code anchors: path-specific; first candidate anchors are `Runtime/Events/Hypercalls/**`, `VmxCompatibilityAdmissionService.Traps.cs`, `TrapCompletionRoutePolicy.cs`, `TrapCompletionPublicationFence.cs`.
- Authority owner: approved neutral owner for the exact path; release review records evidence but is not runtime authority.
- Required RFC/ADR: mandatory with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: admission -> neutral backend execution -> route/fence policy -> atomic completion plus E5 -> explicit retire rule -> E7 evidence; any missing dependency means `must remain denied`.
- Tests/static scans: all Phase 16 negative tests, owner positive tests, adjacent denials, migration/evidence non-leak, completion/retire separation, static scans.
- Risks: claiming production activation from readiness corpus, proof-only/admitted-denied result, backend success without publication, or completion without retire.
- Next-gate dependency: no automatic next phase; new work starts as owner-specific RFC/ADR.

## Phase Goal

Define what may be claimed after one limited owner-specific path is fully implemented and verified.

## Current Baseline

D2, O1 and E2-E7 are closed for exactly `HybridCPU.VMCALL.Runtime.v1` leaf `0x0001` / `PROBE_NO_STATE_V1`; E1 remains closed fault-only and compatibility VMX remains fault-only outside the exact neutral composition. PR-I is contained by `46917937d58fd22b2c0b9ee9308c5ead6e8af11f` with tree `1150abad86d260b63cc17a076cb23af32f074c1f`.

PR-J development-local closure is bound to subject `bcd2d7f4654d4dab17c7a6705cb885fdd572510d` / tree `2ae54ab2ba4f0da1e9d95fc95dfb1ad83b080e33` and a later evidence record. Per-domain transition-in-flight accounting closes the E2->E3, E3->E5 and E5->E6 gaps, the required race matrix passes, and the exact profile plus ordered kill switch remain default-disabled until explicitly activated. Compiler Gate is `ClosedByDefault` and is not a runtime-correctness dependency.

Phase 42 independently closes exact `GuestCr0`/`GuestCr4` read-only VMREAD production scalar delivery under its separate accepted D2, clean subject and later evidence. Its profile also defaults disabled; it does not widen the VMCALL release claim or authorize broad VMREAD, VMWRITE, backend/trap completion, VMX retire effects or compiler emission.

## PR-J Required Closures

1. Record dependency provenance for PR-I containing commit `46917937d58fd22b2c0b9ee9308c5ead6e8af11f` and tree `1150abad86d260b63cc17a076cb23af32f074c1f`. If PR-J changes code, that subject remains PR-I provenance rather than the release candidate: designate a new immutable release-candidate `SubjectCommitSha`/`SubjectTreeSha`, then reference it from a later non-self-referential evidence record.
2. Prove cross-registry E7 quiescence in code using a shared per-domain lifecycle epoch/gate or transition-in-flight counter included in the quiescence predicate. Required races are drain vs E2->E3, drain vs E3->E5, drain vs E5->E6, restore vs new E2, cancel vs E3 publication, cancel vs E5->E6, and two independent domains draining concurrently.
3. Define exact activation and reverse transition without a broad `enableVmcall` boolean: default disabled, explicit exact runtime profile and domain capability provisioning, then kill switch `close new E2 -> drain transition-in-flight and E2/E3/E5/E6 to zero -> revoke exact binding/grant -> deterministic fault-only fallback`.
4. Emit an immutable release record whose claim is exactly `limited scoped activation of PROBE_NO_STATE_V1 only`. It records subject commit/tree, DecisionId, SpecDigest, OwnerId/policy version/epoch, source hashes, commands/results, diagnostic artifact hashes, toolchain, clean status and activation/kill-switch/rollback state. It is release evidence, never runtime authority.

Current closure state: PR-I containing provenance is fixed. PR-J concurrency/quiescence, exact activation/rollback and the later evidence record are closed for development-local exact-profile activation. Broad VMX activation, compiler emission and all deferred surfaces remain denied.

## ISE-RELEASE-GATE-18 - Closure Record

Closure date: 2026-06-18.

State: closed `READINESS/FINAL-CLAIM GATE / NO-BROAD-RELEASE-CLAIM / NO-IMPLEMENTATION-PERMISSION`.

This closes the release gate as a final-claim/readiness boundary only. It does not accept an owner RFC/ADR. It does not allocate an exact VMCALL leaf. It does not approve a broad virtualization release claim. It does not turn closed audits, green tests, static scans, golden artifacts, PR order, rollout sequencing, backlog rows, handoffs, response audits, conformance fixtures, or documentation closure into runtime implementation permission.

The release gate is a claim-boundary record, not an implementation-authority source.

Release claim matrix:

| Evidence | Release-gate use | Required authority before any limited claim | Default result |
| --- | --- | --- | --- |
| closed audit records and backlog rows | readiness history only | accepted owner-specific RFC/ADR plus exact implementation evidence | no release claim |
| green negative/static tests and scans | regression evidence only | exact path positive tests plus adjacent denials and source scans | no activation claim |
| rollout/PR order | sequencing evidence only | runtime owner acceptance before production implementation | no implementation permission |
| exact owner-specific RFC/ADR | required input, not enough by itself | admission, backend execution, route, publication fence, retire, migration, rollback evidence | remain gated until chain is complete |
| backend success evidence | one arrow in a larger chain | completion route and publication fence from neutral owners | not completion publication |
| completion publication evidence | completion-only evidence | explicit retire rule and retire owner authorization | not retire publication |
| release notes or marketing wording | claim boundary only | release reviewer cites exact path, exclusions, migration/evidence class, and rollback state | no broad VMX/SecureCompute/compiler/lane/stream claim |

Release invariants:

- Release review records whether the exact path met the gate; it is not a runtime owner.
- A final claim can only name one exact accepted path and its excluded surfaces.
- "Limited scoped activation for exact path X" requires the full dependency set: accepted D2 and loaded O1, E1-bound one-time operand snapshot, runtime admission/neutral trap, live E2, E3 receipt, E4 proof of exclusive canonical composition to E3, atomic completion plus E5, E6 retire, E7 drain/restore/determinism, capability/evidence policy, rollback and adjacent denials.
- Any missing arrow means the release result remains gated.
- Closed audits, Phase 16 conformance closure, Phase 17 rollout order, and this Phase 18 closure remain non-authority evidence.
- Broad claims for VMX, SecureCompute through VMX, nested virtualization, VMWRITE, compiler emission, lane/stream passthrough, migration payloads, host aliases, or compatibility controls remain denied/future-gated unless a future owner-specific RFC/ADR and implementation chain proves that exact path.

## Owner Of Authority

The approved neutral owner for the single active path. Release review only records whether the path met the gate.

## What Can Be Implemented

Release gate checklist:

- owner-specific RFC/ADR accepted;
- exact path scope and exact leaf/field identifier named;
- operation-specific SafetyVerifier certificate bound to the accepted manifest and live runtime operand value;
- neutral owner implemented;
- runtime admission required;
- backend execution separated from admission;
- opaque backend execution receipt separated from completion authority;
- completion route authorized by neutral route policy;
- publication fence required;
- retire rule explicit;
- capability and evidence policy enforced;
- migration/checkpoint class explicit;
- rollback/no-host-evidence checks pass;
- adjacent denied states remain denied;
- static scans clean or allowlisted with denial context;
- documentation claim boundary updated.
- proof-only and admitted-denied semantics remain evidence-only and are not activation approval.
- SecureCompute `AllowedSecureOperation`, `AllowedProofOnlyNoExecution`, descriptor materialization, publication-fence shape, and privileged read-only projection do not satisfy any backend-execution arrow.

## What Remains Denied/Future-Gated

The release gate does not imply:

- all VMX support;
- full virtualization feature set;
- nested support;
- SecureCompute active through VMX;
- VMWRITE support;
- compiler virtualization emission;
- Lane6/Lane7/Stream passthrough;
- host alias support;
- compatibility control value projection.
- `AllowedProofOnlyNoExecution` or `AllowedAdmittedDenied` as activation approval.
- guarded `GuestCr0`/`GuestCr4` read-only projection as broad VMCS, SecureCompute, or runtime activation.

## Forbidden Shortcuts

- Calling the whole virtualization subsystem complete.
- Treating one active path as VMX backend restoration.
- Treating a candidate leaf class as the exact accepted leaf ID.
- Reusing SecureCompute admission/proof/projection results to fill the neutral virtualization backend-execution arrow.
- Expanding scope after tests pass.
- Omitting denied adjacent states from release notes.

## Required RFC/ADR

Required for the single approved path. For the recommended first path, the release gate should cite Phase 06 RFC/ADR and implementation evidence from Phases 07-09 and 15-16.

## Code Anchors

Path-specific. For recommended first path:

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Events/Hypercalls/**`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Routing/TrapCompletionRoutePolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Records/TrapCompletionPublicationFence.cs`

## Documentation Anchors

- This directory.
- `Documentation/Virtualization WhiteBook/16_Current_State_And_Closure_Matrix.md`
- `Documentation/Virtualization WhiteBook/17_Roadmap_And_Residual_Risk.md`
- `Documentation/Virtualization WhiteBook/19_Source_References_And_Check_Commands.md`

## Required Tests

- All Phase 16 negative tests green.
- Owner-specific positive tests green.
- Adjacent denied states green.
- Migration/evidence non-leak green.
- Completion/retire separation tests green.
- Static scans clean.

## Required Static/Source Scans

Run Phase 16 scans and any path-specific scans from the owner RFC/ADR. Release cannot proceed with unexplained matches.

## Migration/Evidence Classification

The release record must state the migration/evidence class and excluded evidence classes. If any class is unknown, release remains blocked.

## Completion/Retire Implications

The exact-scope Release Gate can open only after the approved path proves:

```text
D2 accepted
  -> O1 loaded
  -> E1-bound one-time operand snapshot
  -> RuntimeBoundaryAdmission / neutral trap
  -> E2
  -> E3
  -> E4 proof of the exclusive canonical composition to E3
  -> atomic CompletionRecord + E5
  -> E6
  -> E7
  -> exact-scope Release Gate
```

Any missing arrow blocks release.

## Exit Criteria

- Release review says either "remain gated" or "limited scoped activation for exact path X".
- All no-goal areas remain explicitly denied/future-gated.
- No forbidden regression exists.

## Dependency On Previous/Next Phase

Depends on Phases 01-17. There is no next phase; new work starts as a new owner-specific RFC/ADR.
