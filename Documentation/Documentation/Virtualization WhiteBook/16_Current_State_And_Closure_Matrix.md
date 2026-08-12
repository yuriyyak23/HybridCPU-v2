# Current State And Closure Matrix

Compiler-boundary reconciliation (2026-08-11): exact default-off emission of `PROBE_NO_STATE_V1` is implemented and verified through `CompilerEmissionDecisionV1` and canonical compiler-only intent binding. It is not release-authorized compiler activation and creates no runtime authority. Runtime independently revalidates the serialized carrier. Adjacent VMCALL, VMREAD/VMWRITE, nested, SecureCompute and device/lane scopes remain compiler-denied. `GuestCr0`/`GuestCr4` VMREAD is machine-accepted governance-only; production implementation is not authorized.

PR-I closure (2026-08-10): exact `PROBE_NO_STATE_V1` E7 is closed as
DrainOnly/policy-identity-only. Live E2/E3/E5/E6 block checkpoint, cancellation
returns all issuing registries to zero, restore advances neutral generation and
duplicate/wrong-profile restore denies. This is not a general migration format,
compiler gate, release claim or broad virtualization activation.

PR-G closure (2026-08-10): exact completion publication is now implemented only
through the neutral completion owner as atomic `CompletionRecord` plus opaque E5.
This is host-owned/non-migratable completion evidence, not E6 or retire authority;
compatibility remains projection-only and VMX retire remains fault-only.

This chapter records the current virtualization status as rechecked through 2026-08-09.

## Current Facts

- VMX compatibility frontend freeze is declared.
- VMX is a frozen compatibility frontend, not the virtualization architecture.
- Physical legacy VMX backend authority is absent.
- `Legacy/VMX` has no active production C# authority, or the path is absent.
- `VmxExecutionUnit.cs` is absent.
- `VmcsManager.cs` is absent.
- `IVmcsManager.cs` is absent.
- VMCSv2 mutable helper authority has been removed or fenced.
- Generated/frontend projection inventory is compatibility artifact inventory.
- VMREAD has partial generated/read-only value projection through neutral owners after runtime admission.
- VMCALL trap projection is admitted-denied through neutral trap result, hypercall backend admission, route policy, and publication fences.
- Completion-only and retire-capable route flags are structurally separated, but neither positive descriptor is connected to VMX frontend.
- SecureCompute/VMX compatibility surfaces are projection and denial fences; VMX cannot activate, grant, or own SecureCompute.
- PR-A D2 v2 governance/negative substrate is implemented: immutable spec,
  acceptance and lineage records, a versioned canonical binary encoder,
  fail-closed exact-byte/SHA/policy/review validator and negative diagnostics.
- Phase 38 architecture/policy values are decided and the machine structure is
  validator-tested. Separately authorized PR-B now supplies committed exact
  SpecV2 bytes, repository CODEOWNERS, attributable owner/architecture review
  receipts, the AcceptanceRecordV2, stable owner/capability allocation metadata
  and the generated exact policy lookup. Machine D2 is accepted.
- Separately authorized PR-C implements the exact execution-only common-legality
  rule, an immutable O1 loaded only from accepted D2 and one-time full-value
  operand capture after live E1 at the canonical materialization seam. O1 is
  policy and the operand snapshot is identity; neither is a capability or E2.
- The repository owner subsequently authorized bounded PR-D through PR-I.
  PR-D is closed by commit `992c0cc2895b444ebc92c4b48d91175567f48076`:
  only SafetyVerifier can issue the
  private exact-operation E2 from live D2/O1/E1/operand, generation-bearing
  typed grant, runtime-root epoch and restore-generation owner. This is runtime
  admission, not backend authority.
- PR-E is closed in the current worktree as one isolated default-off exact
  executor. It atomically consumes one live E2 and emits one opaque restore-bound
  E3 with canonical non-zero no-effect/no-result digests. It has no production
  decode/dispatcher/compatibility composition and no completion or retire
  connection; production VMCALL remains fault-only.
- PR-F is closed in the current worktree as the exclusive canonical E4 path.
  A distinct neutral `InvokeHypercall` operation is admitted only after the
  existing lane-7 E1 and immutable operand seam; `VmxMicroOp.Execute` consumes
  the carrier-bound dispatch into E3. No default binding, compatibility caller,
  completion record or retire permission exists, and VMX retire still faults.

## Closure Matrix

| Area | Current Status | Authority Owner | VMX Role |
|---|---|---|---|
| VMX frontend freeze | Closed | Compatibility frontend contract | Frozen ABI/projection |
| Legacy VMX backend | Closed as absent | None | Forbidden return |
| VMCS manager/runtime manager | Closed as absent | None | Forbidden return |
| VMCS fields | Generated read-only/denied | Runtime descriptors / completion | Projection schema |
| VMREAD completion-owned fields | Projected read-only | `CompletionRecord` | Recomputed compatibility value |
| VMREAD memory-owned fields | Projected read-only for admitted slice | `MemoryDomainDescriptor` / translation view | Compatibility value projection |
| VMREAD execution-owned fields | Direct read-only projection for `GuestPc`/`GuestSp`/`GuestFlags` | `ExecutionDomainDescriptor` / read-only state view | Compatibility value projection; not architectural VMREAD |
| VMREAD `GuestCr0`/`GuestCr4` | Guarded direct read-only projection only | `PrivilegedExecutionStateDescriptor` plus field-specific policy | All gates required; no mutation/backend/completion/retire |
| Other VMREAD privileged/control/host fields | Explicitly denied | Missing neutral privileged/control/host owner | Fail-closed alias |
| VMWRITE | Denied/fail-closed | No admitted neutral owner | Denied alias |
| VMCALL trap projection | Admitted-denied | Neutral trap policy/result | VMX exit projection |
| VMCALL backend admission | Closed as fail-closed | `HypercallBackendAdmissionService`; no backend owner | Missing neutral owner |
| Trap completion route | Split route policy implemented; positive use future-gated | `TrapCompletionRouteService` | Projection-only denied route in VMX frontend |
| Trap result | Closed neutral split | `NeutralTrapResult` | Mapped later |
| Completion publication | Completion-only neutral fence scaffolding implemented; production VMCALL denied | `TrapCompletionPublicationFence` | Projected only after fence |
| Retire intercept exit | Fenced | Publication fence + retire model | Fail-closed VMX effect |
| Descriptor readiness | Closed fail-closed | Neutral materialized descriptors/checkpoints | Not derived from VMREAD |
| Migration/evidence for recomputed fields | Closed | Neutral migration/evidence policy | VMCS completion fields are not payload authority |
| Capabilities | Grant-first | `CapabilityDescriptorSet` | `VmxCaps` projection |
| Host evidence | Non-leak | Evidence policy / host evidence boundary | Compatibility projection only |
| SecureCompute compatibility | Projection/denial matrix | SecureCompute runtime descriptors/policies | VMX cannot activate or own |
| D2 v2 governance | PR-B attributable machine materialization closed | Neutral repository/runtime governance | Accepted policy metadata only; no VMX/runtime authority |
| O1 and canonical VMCALL operand identity | PR-C closed fault-only in current worktree | Neutral runtime policy/materialization owners | Immutable policy/identity only; no E2, backend, completion or retire |
| Exact VMCALL E2 admission | PR-D closed and committed | SafetyVerifier plus neutral live grant/root/restore owners | Private attempt-bound admission; compatibility cannot issue or consume |
| Exact VMCALL E3 executor/receipt | PR-E closed isolated/default-off | `DomainHypercallRuntimeExecutor` | No VMX call path, completion or retire authority |
| Exact VMCALL E4 composition | PR-F closed exclusive/fail-closed | Canonical scheduler/SafetyVerifier/runtime executor owners | Compatibility cannot invoke; completion/retire remain false |
| Nested composition | Neutral model | Nested descriptors/services | VMX-compatible bridge |
| Compiler emission | Exact `PROBE_NO_STATE_V1` implemented/verified, default-disabled; broad scope denied | `CompilerEmissionDecisionV1` owns carrier-production policy only | Frozen opcode plus exact ISA carrier; no runtime authority |

## Recent Closed Heavy Steps

Generated read-only VMREAD value projection:

- completion-owned fields project from `CompletionRecord`;
- memory-owned fields project from `MemoryDomainReadOnlyTranslationView`;
- `Vpid` is tied to neutral address-space tagging;
- `Cr3TargetCount` is tied to neutral address-space target count;
- execution-owned `GuestPc`, `GuestSp`, and `GuestFlags` project only from `ExecutionDomainReadOnlyStateView`;
- `GuestCr0`/`GuestCr4` project only through the guarded privileged-state contract; host execution aliases, `HostCr3`, compatibility-control fields, unknown fields, all writes, and any widening remain denied.

Neutral trap/completion/backend split:

- `NeutralTrapResult` is runtime vocabulary;
- `VmxTrapProjectionMapper` maps only after the neutral result exists;
- `TrapCompletionRouteService` exists as neutral route policy before any successful publication;
- `RuntimeOwnedCompletionPublication` separates the completion route flag from retire authorization;
- the neutral fence can return `CompletionPublishedRetireDenied` with a completion record and retire false;
- host-owned evidence and missing/unsafe migration classification cannot grant retire;
- production VMCALL uses `HypercallBackendAdmissionRequest.MissingNeutralOwner`;
- no successful VMCALL backend, compatibility-exit completion, or intercept retire publication is opened.

Descriptor readiness and migration/evidence proof:

- VMREAD projection values do not make migration or nested readiness successful;
- recomputed completion-owned compatibility fields are not checkpoint payload authority;
- compatibility projection metadata cannot become restore or evidence authority.

## What Is Still Not Implemented

- Successful VMX backend execution.
- Mutable VMCS field store.
- Active VMCS pointer state.
- Successful VMCALL backend hypercall path.
- Live capability grants or a loaded executable `HCOWNR` runtime service; the
  committed registries contain allocation metadata only.
- Production D2-bound E2 issuance and real checkpoint-restore advancement of the
  restore generation captured by the PR-C operand snapshot.
- Successful VMX intercept/exit retire publication without neutral owner.
- Feature-complete VMREAD backend execution.
- VMREAD values for privileged/control/host/nested fields without explicit neutral owner/value source.
- Architectural VMREAD integration/writeback for `GuestCr0`/`GuestCr4`; the direct guarded projection owner exists but is not a production instruction path.
- Neutral host-address-space owner for `HostCr3`.
- Neutral host-execution owner for host PC/SP/flags/control aliases.
- Control-bit VMREAD mapper over a materialized neutral compatibility-control value contract.

## Residual Interpretation Rule

If a compatibility object looks successful but the neutral owner, value source, backend owner, route, or publication fence is absent, classify it as projection-only or denied. Do not classify it as production backend success.
PR-H closure (2026-08-10): the exact Phase-38 probe alone can receive and consume
one opaque E6 in the canonical CPU WB retire contour, with zero architectural
effects. Compatibility remains fault-only. E7 drain/restore/determinism and
release are not closed by PR-H.
