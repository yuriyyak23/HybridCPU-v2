# DmaStreamCompute And Assist Separation

## Lane6 DmaStreamCompute

`DmaStreamCompute` is the lane6 descriptor carrier plus explicit runtime/helper
token model for descriptor-backed stream compute. It has its own DSC1 descriptor
ABI, guard, replay evidence, token, commit, and telemetry contour. Its micro-op
uses `SlotClass.DmaStreamClass` and lane6 class placement. Current direct
micro-op execution is open only for the Phase 06 DSC1 production contour through
`DmaStreamComputeRuntime.ExecuteMaterializedMicroOpToCommitPending(...)`; DSC2,
queue/async, broad lowering, and StreamEngine/DMAController fallback remain
fail-closed.

Code anchors:

- `HybridCPU_ISE/CloseToRTL/Core/Pipeline/MicroOps/Lane6DmaStream/DmaStreamComputeMicroOp.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/DmaStreamCompute/*`
- `HybridCPU_Compiler/API/Threading/HybridCpuThreadCompilerContext.cs`
- `HybridCPU_ISE.Tests/tests/DmaStreamCompute*.cs`
- `HybridCPU_ISE.Tests/CompilerTests/DmaStreamComputeCompilerContractTests.cs`

Documentation anchors:

- `Documentation/InstructionsRefactor/WhiteBook/02_Runtime_Surface_Closure.md`
- `Documentation/InstructionsRefactor/WhiteBook/04_Memory_Atomic_Fence_Model.md`
- `Documentation/InstructionsRefactor/WhiteBook/06_Verification_And_Risk_Closure.md`
- `Documentation/Stream WhiteBook/DmaStreamCompute/01_Current_Contract.md`
- `Documentation/Stream WhiteBook/StreamEngine DmaStreamCompute/02_DmaStreamCompute.md`
- `Documentation/Refactoring/Phases Ex1/02_Executable_Lane6_DSC_ADR_Gate.md`
- `Documentation/Refactoring/Phases Ex1/13_Dependency_Graph_And_Execution_Order.md`

## StreamEngine, SRF, and VectorALU helpers

StreamEngine, SRF, and VectorALU are older raw stream/vector execution and ingress
surfaces. They can share memory and SRF concepts with lane6 work, but they do not replace
the native descriptor-backed lane6 path and do not become L7-SDC command carriers.

Code anchors:

- `HybridCPU_ISE/Core/Execution/StreamEngine/*`
- `HybridCPU_ISE/Core/Execution/Compute/VectorALU*.cs`
- `Documentation/Stream WhiteBook/StreamEngine DmaStreamCompute/01_StreamEngine_SFR_SRF_VectorALU.md`

## VDSA assist boundary

Assist is architecturally invisible, non-retiring, replay-discardable, and boundedly
observable through cache/SRF warming, replay evidence, and telemetry. Lane6 assist
carriers may use DMA/SRF resources for ingress warming, but that is not VectorALU
execution, not `DmaStreamCompute` descriptor acceptance, and not L7-SDC lane7 command
authority.

Code anchors:

- `HybridCPU_ISE/docs/assist-semantics.md`
- `HybridCPU_ISE/Core/Pipeline/Assist/AssistRuntime.cs`
- `HybridCPU_ISE/Core/Pipeline/Assist/AssistMicroOp.cs`
- `HybridCPU_ISE/Core/Pipeline/Scheduling/MicroOpScheduler.Assist.cs`
- `HybridCPU_ISE/Core/Pipeline/Scheduling/MicroOpScheduler.AssistBackpressure.cs`
- `Documentation/Stream WhiteBook/StreamEngine DmaStreamCompute/03_VDSA_Assist_Warming_Prefetch_SRF_DataIngress.md`

Conflict evidence:

- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Conflicts/ExternalAcceleratorConflictManager.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcDmaStreamComputeConflictTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcSrfAssistConflictTests.cs`

These conflict/cache/assist observations are model evidence only. They do not
install a global CPU load/store authority and do not prove executable DSC/L7
overlap, coherent DMA/cache, or production compiler/backend lowering.

Stale anchors may appear only as stale examples, not current roots:

- Stale only: `Documentation/DmaStreamCompute/...`
- Stale only: `Documentation/StreamEngine DmaStreamCompute and ExternalAccelerators/...`

## Ownership glossary

- `DMA`: separate memory transfer controller and channel API.
- `StreamEngine`: in-core stream/vector execution module.
- `DmaStreamCompute`: lane6 descriptor carrier plus scoped Phase 06 runtime and
  token/commit contour.
- `ExternalAccelerator`: lane7 L7-SDC scoped command runtime subsystem.
- `backend`: runtime executor behind guarded contours, not automatic fallback
  execution.
- `token`: model state and commit container; not authority by itself.
- `commit`: explicit publication operation; not always retire.
- `retire`: pipeline publication/exception boundary.
- `fence`: scoped L7 runtime command for current tokens; broader/global fence
  semantics remain gated.
- `queue`: model queue unless a pipeline/device protocol is approved and
  implemented.

## Ex1 Non-Inversion Rule

The following remain downstream evidence only:

- lane6 DSC parser/helper/token/retire observations outside Phase 06 DSC1;
- L7 fake backend, capability registry, queue, fence, token, register ABI, and
  commit APIs outside the current scoped command contour;
- IOMMU backend infrastructure and no-fallback resolver decisions;
- conflict/cache observers and explicit non-coherent invalidation fan-out;
- compiler sideband emission, descriptor preservation, and carrier projection.

They cannot close upstream gates for expansion beyond current executable lane6
DSC1 Phase 06 or current L7 Phase 08 / Phase 08A commands, DSC2 execution,
async overlap, IOMMU-backed execution, coherent DMA/cache, successful partial
completion, or production compiler/backend lowering.
