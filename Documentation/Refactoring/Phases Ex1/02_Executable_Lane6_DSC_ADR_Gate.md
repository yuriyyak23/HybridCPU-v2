# Phase 02 - Executable Lane6 DSC ADR Gate

Status:
Future gated / Design required. Implementation-ready only after ADR approval.

Scope:
Define the architecture approval gate for TASK-001: making lane6 `DmaStreamComputeMicroOp` executable. This phase does not authorize code changes.

Current code evidence:
- `DmaStreamComputeMicroOp.Execute(...)` is fail-closed.
- `DmaStreamComputeDescriptorParser.ExecutionEnabled` is false.
- `DmaStreamComputeRuntime.ExecuteToCommitPending(...)` can produce `CommitPending` through explicit helper/model orchestration, but it is not pipeline execution.
- `DmaStreamComputeToken` already models admitted/issued/read/compute/commit/fault/cancel states, but tokens are not allocated by normal pipeline issue.
- Phase 3 selected Option A: keep fail-closed carrier.
- Phase 6 FEATURE-000 remains open and not approved.

Architecture decision:
Future gated:
- Async token-based executable lane6 DSC is the target architecture if Option B is approved.
- Direct synchronous routing from `DmaStreamComputeMicroOp.Execute` to `DmaStreamComputeRuntime.ExecuteToCommitPending` is rejected / forbidden until ADR, and should remain rejected even after ADR unless the ADR explicitly proves pipeline, retire, replay, ordering, and exception safety.
- The executable design must treat `Execute` as admission/scheduling boundary or carrier dispatch metadata, not as a monolithic long-latency memory mutation function.

Non-goals:
- Do not implement executable DSC in this phase.
- Do not call runtime helper directly from carrier `Execute`.
- Do not allocate durable tokens at decode.
- Do not publish memory before retire/commit boundary is defined.
- Do not grant compiler/backend production lowering.

Required design gates:
The ADR must define:
- token scheduler and active token store;
- token allocation at issue/admission;
- runtime step decomposition instead of monolithic helper execution;
- completion model: poll, wait, fence, interrupt, retire observation, or explicit mix;
- commit/retire boundary;
- replay, squash, trap, and context-switch cancellation;
- precise fault priority and issuing metadata;
- memory ordering against CPU loads/stores/atomics/fences;
- explicit physical versus IOMMU-translated backend selection;
- cache flush/invalidate requirements;
- compiler/backend lowering contract and feature/capability guards;
- compatibility treatment for existing fail-closed tests.

Implementation plan:
Implementation-ready only after gate:
1. Create ADR with state machines and instruction semantics.
2. Split runtime helper into reusable validation/read/compute/stage/complete steps without making the current helper canonical ISA execution.
3. Add a token store and issue/admission API.
4. Add async engine or scheduler tick model.
5. Add retire publication and all-or-none commit path.
6. Add cancellation hooks for replay/squash/trap/context switch.
7. Wire ordering/conflict/cache/backend services behind feature gates.
8. Only then consider changing lane6 carrier behavior.

Affected files/classes/methods:
- `DmaStreamComputeMicroOp.Execute`
- `DmaStreamComputeDescriptorParser.ExecutionEnabled`
- `DmaStreamComputeRuntime.ExecuteToCommitPending`
- `DmaStreamComputeToken`
- Future `DmaStreamComputeTokenStore`
- CPU issue/admission and retire paths
- CPU replay/squash/trap/context-switch paths
- Memory ordering/conflict/cache observer services
- Compiler/backend lowering surfaces

Testing requirements:
- Existing negative direct-execute tests remain until the ADR explicitly changes compatibility mode.
- Future tests must cover positive copy/add/mul/fma/reduce execution, token issue/admission, async completion, cancellation, all-or-none commit, precise retire faults, ordering litmus cases, IOMMU/backend selection, cache flush/invalidate, and compiler/backend conformance.

Documentation updates:
Before implementation, update architecture docs with the ADR, state diagrams, fault priority, ordering rules, backend/addressing contract, cache protocol, and compiler contract. Do not migrate Future Design into Current Implemented Contract until code and tests land.

Compiler/backend impact:
Current compiler/backend remains prohibited from production lowering to executable lane6 DSC. Future lowering may be enabled only through capability-gated feature detection after executable semantics and conformance tests exist.

Compatibility risks:
Changing `Execute` is breaking relative to Phase 3 Option A. Risks include hidden synchronous side effects, imprecise exceptions, replay leakage, guard/domain misuse, IOMMU fallback, stale cache visibility, and compiler reliance on unimplemented overlap.

Exit criteria:
- ADR approved.
- All prerequisite designs recorded.
- Compatibility plan accepted.
- Test plan accepted.
- No code change is implied by this file alone.

Blocked by:
Phase 01 current contract lock remains authoritative until this ADR is approved.

Enables:
Phases 03 through 09 and phase 11 can become implementation workstreams for executable lane6 DSC after approval.

