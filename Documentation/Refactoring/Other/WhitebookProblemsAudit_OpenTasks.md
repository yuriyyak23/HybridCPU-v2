# WhiteBook Problems Audit - Open Task Consolidation

Status date: 2026-04-30.

Source audit files:

- `Documentation/Stream WhiteBook/WhitebookProblemsAudit.md`
- `Documentation/Stream WhiteBook/WhitebookProblemsAudit2.md`

Status source of truth:

- `Documentation/Refactoring/Phases/00_Index_And_Audit_Map.md`
- `Documentation/Refactoring/Phases/Phase_00_Baseline_Locking.md`
- `Documentation/Refactoring/Phases/Phase_01_Documentation_Truth_Alignment.md`
- `Documentation/Refactoring/Phases/Phase_02_Code_Contract_Cleanup.md`
- `Documentation/Refactoring/Phases/Phase_03_DMA_Execution_Model_Decision.md`
- `Documentation/Refactoring/Phases/Phase_04_StreamEngine_DMA_Separation.md`
- `Documentation/Refactoring/Phases/Phase_05_ExternalAccelerator_Contract.md`
- `Documentation/Refactoring/Phases/Phase_06_Future_Feature_Backlog.md`
- `Documentation/Refactoring/Phases/Phase_06_FEATURE_000_Executable_Lane6_DSC_Gate.md`

## Classification

There are no open current-contract cleanup tasks left from the two audit files.
Phase 0-5 close the implemented-contract documentation and code-contract
cleanup scope. Phase 3 selects Option A for lane6 `DmaStreamComputeMicroOp`, so
executable lane6 DSC is not approved.

The remaining open items are future architecture gates only. They must not be
implemented or moved into Current Implemented Contract text without explicit
architecture approval and a dedicated implementation phase.

## Open Future Tasks

| Open ID | Source audit items | Current status | Phase-plan anchor | Required next action |
|---|---|---|---|---|
| OPEN-FUTURE-001 | `WhitebookProblemsAudit.md` `AUDIT-002`; appended external-audit `AUDIT-006`; `WhitebookProblemsAudit2.md` `AUDIT-006` | Future only. Lane6 DSC remains fail-closed carrier. | Phase 3 Option A; Phase 6 `FEATURE-000`; `Phase_06_FEATURE_000_Executable_Lane6_DSC_Gate.md` | If executable lane6 DSC is desired, approve Option B and write the full implementation plan before code changes. |
| OPEN-FUTURE-002 | appended external-audit `AUDIT-002`; `WhitebookProblemsAudit2.md` `AUDIT-002` | Future descriptor/runtime extension. Current DSC1 stays `InlineContiguous`. | Phase 6 `FEATURE-001` | Define DSC ABI v2 or extension mechanism for stride/tile/2D/scatter-gather, parser/runtime design, and range-normalization tests. |
| OPEN-FUTURE-003 | appended external-audit `AUDIT-003`; `WhitebookProblemsAudit2.md` `AUDIT-003` | Future completion-mode extension. Current policy stays `AllOrNone`. | Phase 6 `FEATURE-002` | Define visibility, rollback, retry/replay, fault, and compiler-visible completion semantics before implementation. |
| OPEN-FUTURE-004 | `WhitebookProblemsAudit.md` `AUDIT-009` | Future DSC lifecycle ISA decision. Current ISE has no DSC pause/resume/cancel/reset/fence ISA surface. | Phase 0 `AUDIT-009`; Phase 6 prohibitions; related to `FEATURE-000`, `FEATURE-005`, and `FEATURE-008` | Decide whether DSC lifecycle controls are part of executable lane6 DSC, executable fence/order, interrupt/polling completion, or a new dedicated Phase 6 feature. |
| OPEN-FUTURE-005 | `WhitebookProblemsAudit.md` `AUDIT-006`; appended external-audit `AUDIT-005`; `WhitebookProblemsAudit2.md` `AUDIT-005` | Future scheduling/completion work. Current `StreamEngine.BurstIO` DMA use remains synchronous helper behavior. | Phase 6 `FEATURE-003` and `FEATURE-004`; Phase 4 future gate | Define queue capacity, arbitration, completion events, CPU/DMA overlap, ordering, and deterministic scheduler tests. |
| OPEN-FUTURE-006 | `WhitebookProblemsAudit.md` `AUDIT-001`, `AUDIT-003`, `AUDIT-008` | Future external accelerator command ISA. Current L7-SDC carriers remain fail-closed and model-only. | Phase 5 Future Device Protocol Gate; Phase 6 `FEATURE-006` | Define `rd` writeback, token allocation, backend dispatch, queue/backpressure, staged write publication, commit/retire, and fault/exception semantics. |
| OPEN-FUTURE-007 | `WhitebookProblemsAudit.md` `AUDIT-010` | Future executable fence/order semantics. Current `AcceleratorFenceModel` is model-only and executable `ACCEL_FENCE` is absent. | Phase 6 `FEATURE-005` | Define global memory/order model, token/DMA completion model, executable fence behavior, and fence conformance tests. |
| OPEN-FUTURE-008 | `WhitebookProblemsAudit.md` `AUDIT-004` | Future global conflict integration. Current conflict manager is explicit/optional, not a CPU load/store hook. | Phase 6 `FEATURE-009` | Define installation model, conflict response policy, load/store/atomic/fence interactions, and absent-versus-installed tests. |
| OPEN-FUTURE-009 | External accelerator summaries in both audit files | Future device protocol/completion surface. Current external accelerator protocol is model-only and has no universal executable command/interrupt path. | Phase 6 `FEATURE-007` and `FEATURE-008` | Define MMIO/register device protocol, address map, privilege/security, interrupt routing, polling/wait state machine, and tests. |
| OPEN-FUTURE-010 | `WhitebookProblemsAudit.md` `AUDIT-007`; memory/coherency notes in both audit files | Future memory-system decision. Current DSC runtime/helper uses physical main memory, not virtual/IOMMU/cache-coherent memory. | Phase 6 `FEATURE-010` | Define coherency policy, cache/SRF invalidation rules, virtual/IOMMU integration if any, fence visibility, and rollback tests. |

## Closed By Current Refactoring Plan

These audit findings are closed for the implemented contract and must not be
carried as current cleanup work:

| Source item | Closed/current status |
|---|---|
| `WhitebookProblemsAudit.md` `AUDIT-001` | L7 carriers do not write architectural registers; `AcceleratorRegisterAbi` is model-only. Future executable register writeback is tracked by `OPEN-FUTURE-006`. |
| `WhitebookProblemsAudit.md` `AUDIT-002` | Lane6 direct execution remains fail-closed; Phase 3 selected Option A. Future executable lane6 DSC is tracked by `OPEN-FUTURE-001`. |
| `WhitebookProblemsAudit.md` `AUDIT-003` | L7 model APIs are separated from instruction execution. Future executable external accelerator ISA is tracked by `OPEN-FUTURE-006`. |
| `WhitebookProblemsAudit.md` `AUDIT-004` | Conflict manager is explicit/optional for current contract. Future global hook is tracked by `OPEN-FUTURE-008`. |
| `WhitebookProblemsAudit.md` `AUDIT-005` | DSC runtime/helper is separate from `StreamEngine` and `DMAController`. |
| `WhitebookProblemsAudit.md` `AUDIT-006` | StreamEngine DMA helper is synchronous current behavior. Future queue/overlap is tracked by `OPEN-FUTURE-005`. |
| `WhitebookProblemsAudit.md` `AUDIT-007` | DSC runtime/helper physical memory model is documented. Future coherency/IOMMU work is tracked by `OPEN-FUTURE-010`. |
| `WhitebookProblemsAudit.md` `AUDIT-008` | L7 faults are model observations/results, not retire exceptions. Future executable fault publication is part of `OPEN-FUTURE-006`. |
| `WhitebookProblemsAudit.md` `AUDIT-010` | `AcceleratorFenceModel` is model-only; executable `ACCEL_FENCE` fails closed. Future executable fence semantics are tracked by `OPEN-FUTURE-007`. |
| `WhitebookProblemsAudit.md` `AUDIT-011` | Component ownership and lifecycle are documented as separate. |
| `WhitebookProblemsAudit.md` `AUDIT-012` | Current documentation roots are under `Documentation/Stream WhiteBook/...`; stale roots are not current anchors. |
| appended external-audit `AUDIT-002` and `WhitebookProblemsAudit2.md` `AUDIT-002` | Current DSC1 `InlineContiguous` ABI is documented. Future stride/tile/2D/scatter-gather is tracked by `OPEN-FUTURE-002`. |
| appended external-audit `AUDIT-003` and `WhitebookProblemsAudit2.md` `AUDIT-003` | Current DSC completion policy is `AllOrNone`. Future partial-success completion is tracked by `OPEN-FUTURE-003`. |
| appended external-audit `AUDIT-004` and `WhitebookProblemsAudit2.md` `AUDIT-004` | Owner/domain guard admission and commit checks are documented for current contract. |
| appended external-audit `AUDIT-005` and `WhitebookProblemsAudit2.md` `AUDIT-005` | StreamEngine/DSC/DMA separation is closed for current contract. Future shared scheduling is tracked by `OPEN-FUTURE-005` if approved. |
| appended external-audit `AUDIT-006` and `WhitebookProblemsAudit2.md` `AUDIT-006` | DMA/DSC retire-publication ambiguity is closed by Phase 3 Option A and Phase 4 separation. Future executable completion/retire behavior is tracked by `OPEN-FUTURE-001` and `OPEN-FUTURE-005`. |

## Residual Risk Boundary

The known full-suite residual risk remains separate from these audit tasks:
`5928` passed, `2` skipped, and `3` failed in independent
`Phase12VliwCompatFreezeTests` coverage for missing
`build\run-compat-freeze-gate.ps1` and allowlist hits in
`TestAssemblerConsoleApps\StreamVectorSpecSuite.cs`. This is not evidence of a
Stream/DMA/Accelerator refactor regression without a matching focused failure.
