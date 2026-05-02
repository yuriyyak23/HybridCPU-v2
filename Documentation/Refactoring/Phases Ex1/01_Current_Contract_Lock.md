# Phase 01 - Current Contract Lock

Status:
Current contract. Documentation lock. No code implementation approval.

Scope:
Lock the current implemented behavior after TASK-001 through TASK-010 audit corrections. This phase is the reference boundary for all later future-gated phases.

Current code evidence:
- `DmaStreamComputeMicroOp.Execute(ref Processor.CPU_Core core)` throws fail-closed.
- `DmaStreamComputeMicroOp.WritesRegister = false`.
- `DmaStreamComputeMicroOp.SerializationClass = MemoryOrdered`.
- `DmaStreamComputeDescriptorParser.ExecutionEnabled => false`.
- `DmaStreamComputeDescriptorParser` accepts DSC1 `InlineContiguous` and `AllOrNone`, rejects reserved fields, and requires guard-backed owner/domain evidence.
- `DmaStreamComputeRuntime.ExecuteToCommitPending(...)` exists as helper/model code and is not called by `DmaStreamComputeMicroOp.Execute`.
- `DmaStreamComputeToken` stages writes and commits through explicit all-or-none commit APIs.
- `DmaStreamAcceleratorBackend` and token commit use physical main-memory helpers in the current DSC runtime/helper path.
- `IBurstBackend` and `IOMMUBurstBackend` exist, but current DSC runtime/helper does not use them.
- `SystemDeviceCommandMicroOp.Execute(...)` throws fail-closed for `ACCEL_*`.
- `SystemDeviceCommandMicroOp.WritesRegister = false`.
- `AcceleratorRegisterAbi`, queues, fences, fake backends, conflict manager, and commit coordinator are model-only relative to ISA execution.
- `CPU_Core.Cache.cs` exposes L1/L2 data, VLIW, assist, and domain flush surfaces, but no full coherent DMA/cache model.

Architecture decision:
Current contract:
- Lane6 DSC is a fail-closed typed-slot carrier.
- DSC runtime/helper is model-only relative to ISA execution.
- DSC1 is immutable: `InlineContiguous`, `AllOrNone`, fixed v1 operation/type/shape/range rules.
- L7-SDC is model-only and fail-closed.
- `IBurstBackend`/`IOMMUBurstBackend` are available infrastructure only, not executable DSC/L7 proof.
- Cache/prefetch surfaces exist, but coherent hierarchy is not implemented.
- StreamEngine, DMAController, DSC, and ExternalAccelerator remain architecturally separated.
- Production compiler/backend lowering to executable DSC/L7 is forbidden.

Non-goals:
- Do not convert runtime/helper APIs into instruction execution.
- Do not infer executable semantics from token, queue, fence, backend, or commit model surfaces.
- Do not retrofit DSC1 reserved fields with future behavior.
- Do not claim cache coherency, async overlap, partial success, or IOMMU-translated DSC addresses.

Required design gates:
- Future executable lane6 DSC must pass phase 02.
- Future L7 executable ISA must pass phase 10.
- Future DSC2 ABI must pass phase 07.
- Future cache/coherency claims must pass phase 09 and, for coherent DMA, a separate ADR.
- Future compiler/backend production lowering must pass phase 11 and phase 12.

Implementation plan:
No implementation. Preserve negative tests and model/helper tests as contract evidence. Treat current model/helper APIs as useful design surfaces, not architecture publication surfaces.

Affected files/classes/methods:
- `HybridCPU_ISE/Core/Pipeline/MicroOps/DmaStreamComputeMicroOp.cs`: `Execute`, `WritesRegister`, `SerializationClass`, owner/domain and footprint checks.
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeDescriptorParser.cs`: `ExecutionEnabled`, DSC1 parser, guard, reserved fields, `AllOrNone`.
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs`: `ExecuteToCommitPending`.
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeToken.cs`: staging, fault, commit, cancel.
- `HybridCPU_ISE/Core/Execution/BurstIO/IBurstBackend.cs`.
- `HybridCPU_ISE/Core/Execution/BurstIO/IOMMUBurstBackend.cs`.
- `HybridCPU_ISE/Core/Cache/CPU_Core.Cache.cs`.
- `HybridCPU_ISE/Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs`.
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/*`.

Testing requirements:
- Direct lane6 DSC `Execute` remains fail-closed.
- `DmaStreamComputeDescriptorParser.ExecutionEnabled` remains false.
- DSC1 rejects unsupported ABI, reserved fields, unsupported range encoding, non-`AllOrNone`, and missing/mismatched owner guard.
- Runtime/helper tests remain explicit and do not become pipeline tests.
- Direct `ACCEL_*` execution remains fail-closed.
- L7 model-only tests do not assert architectural register writeback or backend dispatch.
- Cache tests must not assert coherent DMA.

Documentation updates:
Existing docs should continue to call this Current Implemented Contract. Any text describing executable lane6 DSC, executable L7, async overlap, IOMMU-translated DSC, coherent DMA/cache, or partial success must be under Future Design until code and tests land.

Compiler/backend impact:
Forbidden under current contract:
- production lowering to executable lane6 DSC;
- production lowering to executable `ACCEL_*`;
- assuming async DMA overlap;
- assuming IOMMU-translated DSC addresses;
- assuming coherent DMA/cache;
- assuming successful partial completion;
- assuming DSC1 stride/tile/scatter-gather.

Allowed under current contract:
- descriptor preservation;
- descriptor validation;
- fail-closed conformance tests;
- explicit model/test helper usage.

Compatibility risks:
The current contract is compatible because it preserves fail-closed behavior. The risk is premature documentation or compiler movement into future semantics.

Exit criteria:
- Current contract bullets remain true.
- Existing fail-closed and model-only tests remain authoritative.
- No Ex1 phase weakens Phase 3 Option A or Phase 5 L7 model-only contract.

Blocked by:
No blocker. This is the baseline for all later phases.

Enables:
A stable baseline for ADR gates, token design, precise faults, ordering, IOMMU/backend selection, DSC2 ABI, cache protocol, L7 gating, compiler restrictions, and test migration.

