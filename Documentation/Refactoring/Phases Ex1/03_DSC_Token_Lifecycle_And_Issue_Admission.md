# Phase 03 - DSC Token Lifecycle And Issue Admission

Status:
Design required. Implementation-ready only after phase 02 ADR.

Scope:
Define TASK-002 token allocation and lifecycle rules for future executable lane6 DSC.

Current code evidence:
- `DmaStreamComputeTokenState` includes `Admitted`, `Issued`, `ReadsComplete`, `ComputeComplete`, `CommitPending`, `Committed`, `Faulted`, and `Canceled`.
- `DmaStreamComputeToken.TryAdmit(...)` creates/admission-gates model tokens from validation result.
- `DmaStreamComputeToken.MarkIssued()`, `StageDestinationWrite(...)`, `MarkComputeComplete()`, `Cancel(...)`, and commit/fault methods already model lifecycle pieces.
- `DmaStreamComputeRuntime.ExecuteToCommitPending(...)` creates tokens in explicit helper/model flow, not normal pipeline issue.
- `DmaStreamComputeMicroOp` carries owner/thread/context and footprint evidence but does not allocate pipeline tokens.

Architecture decision:
Future gated:
- Token allocation point must be issue/admission.
- Token allocation must not occur at decode, because decode can be speculative and cannot allocate durable DMA state.
- Token allocation must not be deferred to retire, because backpressure, resource masks, ordering, and cancellation need an active object earlier.
- Memory-stage allocation is not the preferred point unless ADR proves it is the first non-speculative admission boundary with all typed-slot, guard, footprint, and quota checks.

Non-goals:
- Do not treat current helper-created tokens as pipeline tokens.
- Do not allocate tokens for squashed decode-only bundles.
- Do not let tokens outlive owner/domain authority.
- Do not publish memory from admitted/issued/read/compute states.

Required design gates:
- Issue/admission API and ownership contract.
- Token ID namespace: per core, per pod, per domain, or global.
- Token store lookup, capacity, quota, and backpressure behavior.
- Owner/domain/device/context binding.
- Cancellation rules for replay, squash, trap, context switch, reset, and guard revocation.
- Interaction with pipeline retirement and completion observation.
- Progress and diagnostic fields that do not imply partial success.

Implementation plan:
1. Add `DmaStreamComputeTokenStore` as the owner of active executable tokens.
2. Allocate token only after typed slot, descriptor ABI, owner/domain guard, normalized footprints, resource masks, quota, and conflict admission are accepted.
3. Bind token to issuing PC, bundle ID, lane/slot, virtual thread, context, core, pod, domain tag, and device ID.
4. Enforce token cap and per-domain quotas before issuing work.
5. Move token through explicit states:
   - `Admitted`
   - `Issued`
   - `ReadsComplete`
   - `ComputeComplete`
   - `CommitPending`
   - terminal `Committed`, `Faulted`, or `Canceled`
6. Add cancellation fan-out from replay/squash/trap/context-switch paths.
7. Retire observes terminal or `CommitPending` state according to phase 04.

Affected files/classes/methods:
- `DmaStreamComputeToken`
- future `DmaStreamComputeTokenStore`
- `DmaStreamComputeMicroOp`
- `DmaStreamComputeRuntime` step APIs
- CPU pipeline issue/admission stage
- CPU replay/squash/trap/context-switch control paths
- telemetry and diagnostics

Testing requirements:
- No token allocation on decode-only or squashed bundles.
- Token allocation occurs exactly once at issue/admission.
- Token IDs are unique within the approved namespace.
- Owner/domain mismatch rejects admission.
- Quota/backpressure/token-cap rejection is deterministic.
- Cancel before commit prevents memory mutation.
- Faulted token cannot commit.
- Committed token cannot be canceled.
- Context switch cancels or preserves tokens only according to ADR.

Documentation updates:
Document the token state machine, token metadata, token store ownership, cancellation causes, and the distinction between model helper tokens and future executable pipeline tokens.

Compiler/backend impact:
Compiler/backend may assume descriptor identity and normalized footprints are preserved only after feature approval. It must not assume decode has allocated a token, and must emit explicit wait/poll/fence sequences only after their semantics exist.

Compatibility risks:
Premature token allocation can leak side effects across replay/squash. Overly late allocation can hide backpressure and violate scheduling/resource modeling.

Exit criteria:
- Token allocation point is fixed as issue/admission.
- Token store, state machine, cancellation, quota, and metadata contracts are specified.
- Tests are defined before executable implementation starts.

Blocked by:
Phase 02 ADR gate.

Enables:
Precise faults, async scheduling, ordering/fence semantics, progress diagnostics, executable lane6 DSC, and future L7 token reuse patterns.

