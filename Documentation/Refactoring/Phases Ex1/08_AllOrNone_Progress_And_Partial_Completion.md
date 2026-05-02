# Phase 08 - AllOrNone Progress And Partial Completion

Status:
Current contract plus Future gated. All-or-none is current successful completion; partial success requires separate ADR.

Scope:
Cover TASK-007: preserve `AllOrNone`, define safe progress diagnostics, and block successful partial completion until a future architecture decision.

Current code evidence:
- DSC1 parser accepts only `DmaStreamComputePartialCompletionPolicy.AllOrNone`.
- `DmaStreamComputeToken.StageDestinationWrite(...)` requires staged writes inside normalized write footprints.
- `DmaStreamComputeToken.MarkComputeComplete()` requires exact staged coverage before `CommitPending`.
- `DmaStreamComputeToken.TryCommitAllOrNone(...)` snapshots destination bytes and rolls back on write failure.
- Current helper/model path does not expose successful partial memory visibility.

Architecture decision:
Current contract:
- `AllOrNone` is the only successful completion policy.
- Staged bytes are not visible before commit.
- Commit failure must roll back any partial physical write.
- Progress may be diagnostic evidence only.

Future gated:
- Successful partial completion is rejected / forbidden until a separate ADR defines visibility, retry, rollback, fault priority, compiler contract, and ABI support.
- If ever approved, partial success belongs in DSC2 or a capability-gated extension, not DSC1.

Non-goals:
- Do not define partial success as current behavior.
- Do not let progress counters imply committed memory visibility.
- Do not allow helper/runtime partial writes to be reported as success.
- Do not make compiler scheduling depend on partial completion.

Required design gates:
- Progress record semantics: diagnostic, polling evidence, or architectural state.
- Exact staged coverage rules for all-or-none.
- Rollback guarantees and failure behavior.
- Future partial-success ADR if needed.
- ABI version/capability for partial success.
- Compiler-visible memory and retry semantics for partial success.

Implementation plan:
1. Preserve all-or-none commit and rollback behavior.
2. Add optional progress diagnostics only as non-authoritative token metadata.
3. Ensure progress diagnostics never alter `Succeeded`, `Committed`, or memory visibility.
4. Add tests that partial progress followed by cancel/fault leaves memory unchanged.
5. Reject any partial-success descriptor until the future ADR and ABI exist.

Affected files/classes/methods:
- `DmaStreamComputeDescriptorParser`
- `DmaStreamComputeDescriptor`
- `DmaStreamComputeToken.StageDestinationWrite`
- `DmaStreamComputeToken.MarkComputeComplete`
- `DmaStreamComputeToken.TryCommitAllOrNone`
- future progress telemetry structures
- future DSC2 partial-completion fields if approved

Testing requirements:
- Missing staged write coverage faults.
- Staged write outside normalized footprint faults.
- Commit write failure rolls back all bytes.
- Cancel before commit leaves memory unchanged.
- Progress counters can advance without committing memory.
- Non-`AllOrNone` DSC1 policy rejects.
- DSC2 partial-success descriptors reject until feature enabled.

Documentation updates:
Document `AllOrNone` as Current contract. Document progress diagnostics as non-authoritative. Document successful partial completion only under Future gated design after a separate ADR.

Compiler/backend impact:
Current compiler/backend may assume all-or-none for accepted current DSC helper/future v1 executable contracts. It must not rely on partial memory visibility or successful partial completion.

Compatibility risks:
Partial visibility would break retry, exception, and compiler assumptions. Progress diagnostics must be clearly separated from success/failure state.

Exit criteria:
- All-or-none contract is preserved.
- Progress diagnostics are specified as diagnostics only.
- Partial success is explicitly blocked pending ADR.

Blocked by:
No blocker for current all-or-none lock. Future partial success is blocked by phase 02 executable DSC, phase 03 token lifecycle, phase 04 faults, phase 05 ordering, phase 07 DSC2/capability ABI, phase 09 cache protocol, and phase 11 compiler contract.

Enables:
Safe current helper semantics, future poll/progress observability, and a clean gate for any later partial-success design.

