# Phase 00 - Index And Architecture Baseline

Status:
Current contract lock plus future design index. Documentation only. This phase set is not code implementation approval.

Scope:
This folder, `Documentation/Refactoring/Phases Ex1`, is an extended phase plan derived from TASK-001 through TASK-010 and the corrected final recommendations in `Documentation/Stream WhiteBook/ExtentionsAnalytic_Audit.md`.

It exists separately from the earlier phase set because the earlier documents lock the current fail-closed/model-only baseline, while this set decomposes the future implementation gates needed before executable DSC, executable L7, async overlap, IOMMU-backed execution, cache visibility, coherency, or compiler lowering claims can become true.

Current code evidence:
- `Documentation/Stream WhiteBook/ExtentionsAnalytic_Audit.md` states the corrected final recommendations and explicitly separates implemented behavior from future architecture.
- `Documentation/Stream WhiteBook/DmaStreamCompute/01_Current_Contract.md` locks lane6 DSC as fail-closed and model/helper-only.
- `Documentation/Refactoring/Phases Base Plan/Phase_03_DMA_Execution_Model_Decision.md` selects Option A: keep lane6 DSC fail-closed.
- `Documentation/Refactoring/Phases Base Plan/Phase_04_StreamEngine_DMA_Separation.md` separates StreamEngine, DMAController, DSC, and L7-SDC.
- `Documentation/Refactoring/Phases Base Plan/Phase_05_ExternalAccelerator_Contract.md` locks L7-SDC as model-only.
- `Documentation/Refactoring/Phases Base Plan/Phase_06_FEATURE_000_Executable_Lane6_DSC_Gate.md` keeps executable lane6 DSC open and not approved.
- `Documentation/Refactoring/Phases Base Plan/Phase_06_Future_Feature_Backlog.md` records future features without implementation approval.

Architecture decision:
Current Implemented Contract:

| Area | Current contract |
|---|---|
| Lane6 DSC | `DmaStreamComputeMicroOp` is a typed-slot descriptor carrier. `Execute` remains fail-closed. |
| DSC runtime | `DmaStreamComputeRuntime` is explicit model/runtime helper code, not ISA execution. |
| DSC1 ABI | Immutable v1 ABI: `InlineContiguous`, `AllOrNone`, reserved fields rejected. |
| DSC memory | Current helper path uses physical main memory. |
| IOMMU | `IBurstBackend` and `IOMMUBurstBackend` exist, but do not prove executable DSC/L7 integration. |
| Cache/prefetch | L1/L2 data, VLIW fetch, assist-resident prefetch, and domain flush surfaces exist, but not coherent DMA/cache hierarchy. |
| L7-SDC | `ACCEL_*` carriers fail closed, `WritesRegister = false`; queue/fence/register/backend/commit APIs are model-only. |
| Compiler/backend | Production lowering to executable DSC or executable `ACCEL_*` is forbidden. |

Future Architecture:

| Area | Future gated target |
|---|---|
| Lane6 DSC | Async token-based executable model after ADR approval. |
| Token lifecycle | Token allocation at issue/admission, active token store, cancellation, completion, retire observation. |
| Faults | Precise retire fault publication with priority and issuing metadata. |
| Ordering | Explicit wait/poll/fence semantics and mandatory conflict service for executable overlap. |
| Addressing | Explicit physical versus IOMMU-translated backend selection with no silent fallback. |
| Descriptor ABI | DSC2 or capability-gated extension blocks for stride/tile/scatter-gather/address-space extensions. |
| Progress | Progress diagnostics now; successful partial completion only after separate future ADR. |
| L7 | Executable external accelerator ISA only after ADR, starting with read-only tiers if approved. |
| Cache | Non-coherent explicit flush/invalidate first; coherent DMA only after separate ADR. |

Phase table:

| Phase | File | Primary gate |
|---|---|---|
| 00 | `00_Index_And_Architecture_Baseline.md` | Baseline and dependency index. |
| 01 | `01_Current_Contract_Lock.md` | Lock current fail-closed/model-only behavior. |
| 02 | `02_Executable_Lane6_DSC_ADR_Gate.md` | ADR before executable lane6 DSC. |
| 03 | `03_DSC_Token_Lifecycle_And_Issue_Admission.md` | Token allocation and lifetime. |
| 04 | `04_DSC_Precise_Faults_And_Retire_Publication.md` | Precise fault publication. |
| 05 | `05_Memory_Ordering_And_Global_Conflict_Service.md` | Ordering, fences, conflict service. |
| 06 | `06_Addressing_Backend_And_IOMMU_Integration.md` | Address spaces and backend selection. |
| 07 | `07_DSC2_Descriptor_ABI_And_Capabilities.md` | DSC2/capability ABI. |
| 08 | `08_AllOrNone_Progress_And_Partial_Completion.md` | All-or-none lock and progress diagnostics. |
| 09 | `09_Cache_Prefetch_And_NonCoherent_Protocol.md` | Explicit non-coherent protocol. |
| 10 | `10_External_Accelerator_L7_SDC_Gate.md` | L7 executable ISA gate. |
| 11 | `11_Compiler_Backend_Lowering_Contract.md` | Compiler/backend prohibitions and future contract. |
| 12 | `12_Testing_Conformance_And_Documentation_Migration.md` | Test strategy and doc migration rule. |
| 13 | `13_Dependency_Graph_And_Execution_Order.md` | Final dependency graph and execution order. |

Non-goals:
- Do not change CPU/ISE code.
- Do not edit the existing phase documents.
- Do not move Future Design claims into Current Implemented Contract.
- Do not approve executable DSC, executable L7, coherent DMA, partial success, or production compiler lowering.

Required design gates:
- ADR for executable lane6 DSC before changing `DmaStreamComputeMicroOp.Execute`.
- ADR or equivalent decision record for L7 executable ISA before changing `SystemDeviceCommandMicroOp.Execute` or `WritesRegister`.
- ADR for coherent DMA/cache before claiming coherence.
- Implementation, tests, and architecture approval before any Future Design migrates into Current Implemented Contract.

Implementation plan:
This phase plan is documentation/design basis only. Implementation-ready work starts only after the relevant gate file is satisfied and a separate code implementation phase is approved.

Dependency summary:
- Executable lane6 DSC is blocked by phases 02, 03, 04, 05, 06, 08, 09, 11, and 12.
- Async DMA overlap is blocked by token scheduler, completion model, ordering/conflict service, cancellation, and fence/wait/poll semantics.
- Executable L7 ISA is blocked by phase 10 plus token, backend, commit, ordering, cache, and compiler contracts.
- Cache/coherency claims are blocked by phase 09 and a future coherent-DMA ADR.
- Compiler/backend production lowering is blocked until executable semantics, tests, and documentation migration are complete.

Forbidden Current Contract transfers:
- Do not state that lane6 DSC is executable.
- Do not state that async DMA overlap is implemented.
- Do not state that IOMMU is integrated with executable DSC.
- Do not state that L7-SDC model APIs are executable ISA.
- Do not state that cache/prefetch surfaces are coherent hierarchy.
- Do not state that compiler/backend may production-lower to future semantics.
- Do not state that successful partial completion exists.
- Do not treat fake/test backends as production device protocol.

Affected files/classes/methods:
Documentation only in this phase. Future design references include:
- `HybridCPU_ISE/Core/Pipeline/MicroOps/DmaStreamComputeMicroOp.cs`
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/*`
- `HybridCPU_ISE/Core/Execution/BurstIO/IBurstBackend.cs`
- `HybridCPU_ISE/Core/Execution/BurstIO/IOMMUBurstBackend.cs`
- `HybridCPU_ISE/Core/Cache/CPU_Core.Cache.cs`
- `HybridCPU_ISE/Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/*`
- `HybridCPU_Compiler/*`

Testing requirements:
This index requires documentation verification only: files exist, phase sequence is complete, required sections are present, and no future-gated claim is presented as current behavior.

Documentation updates:
Future documentation may cross-link this plan from WhiteBook and phase docs, but only after review. This task intentionally does not modify existing documents.

Compiler/backend impact:
Current contract remains restrictive. Compiler/backend may preserve, validate, and test descriptors, but must not produce production code that depends on executable DSC/L7, async overlap, IOMMU-translated DSC addresses, coherent DMA/cache, partial success, or DSC1 stride/tile/scatter semantics.

Compatibility risks:
The main risk is wording drift: a reader may treat target architecture as implemented behavior. This plan marks such items as `Future gated`, `Design required`, or `Implementation-ready only after gate`.

Exit criteria:
- All Ex1 phase files are present and sequential.
- Each file has the required sections.
- The plan preserves Phase 3 Option A and Phase 5 model-only L7.
- Future-gated claims are not written as current behavior.

Blocked by:
No code gate. This documentation phase depends only on the audit and existing phase documents.

Enables:
Architecturally safe planning for future executable lane6 DSC, L7, IOMMU/backend integration, explicit non-coherent cache protocol, compiler/backend lowering contracts, and conformance testing.

