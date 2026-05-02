# DmaStreamCompute And Assist Separation

## Lane6 DmaStreamCompute

`DmaStreamCompute` is the lane6 descriptor carrier plus explicit runtime/helper
token model for descriptor-backed stream compute. It has its own DSC1 descriptor
ABI, guard, replay evidence, token, commit, and telemetry contour. Its micro-op
uses `SlotClass.DmaStreamClass` and lane6 class placement; direct micro-op
execution remains fail-closed.

Code anchors:

- `HybridCPU_ISE/Core/Pipeline/MicroOps/DmaStreamComputeMicroOp.cs`
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/*`
- `HybridCPU_Compiler/API/Threading/HybridCpuThreadCompilerContext.cs`
- `HybridCPU_ISE.Tests/tests/DmaStreamCompute*.cs`
- `HybridCPU_ISE.Tests/CompilerTests/DmaStreamComputeCompilerContractTests.cs`

Documentation anchors:

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

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Conflicts/ExternalAcceleratorConflictManager.cs`
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
- `DmaStreamCompute`: lane6 descriptor carrier plus explicit runtime helper and
  token model.
- `ExternalAccelerator`: lane7 L7-SDC carrier/model subsystem.
- `backend`: runtime/model executor, not automatic pipeline execution.
- `token`: model state and commit container; not authority by itself.
- `commit`: explicit publication operation; not always retire.
- `retire`: pipeline publication/exception boundary.
- `fence`: model API unless an executable instruction path is approved and
  implemented.
- `queue`: model queue unless a pipeline/device protocol is approved and
  implemented.

## Ex1 Non-Inversion Rule

The following remain downstream evidence only:

- lane6 DSC parser/helper/token/retire observations;
- L7 fake backend, capability registry, queue, fence, token, register ABI, and
  commit model APIs;
- IOMMU backend infrastructure and no-fallback resolver decisions;
- conflict/cache observers and explicit non-coherent invalidation fan-out;
- compiler sideband emission, descriptor preservation, and carrier projection.

They cannot close upstream gates for executable lane6 DSC, executable L7, DSC2
execution, async overlap, IOMMU-backed execution, coherent DMA/cache, successful
partial completion, or production compiler/backend lowering.
