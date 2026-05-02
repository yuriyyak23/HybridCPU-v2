# DmaStreamCompute Current Contract

Current implementation, model/runtime-side APIs, fail-closed carrier surfaces,
and future design are separate. Future architecture is not implemented behavior.

## Current Implemented Contract

- `DmaStreamComputeMicroOp` is a lane6 typed-slot descriptor/decode carrier.
- Phase 3 selects Option A: keep `DmaStreamComputeMicroOp` as a fail-closed
  carrier, not an executable DMA micro-op.
- Direct `DmaStreamComputeMicroOp.Execute(ref Processor.CPU_Core core)` is
  disabled and throws fail-closed.
- `DmaStreamComputeDescriptorParser.ExecutionEnabled` is `false`.
- `DmaStreamComputeRuntime` is an explicit runtime helper;
  `DmaStreamComputeRuntime.ExecuteToCommitPending(...)` is not canonical
  micro-op execution and is not called by `DmaStreamComputeMicroOp.Execute(...)`.
- Helper/runtime tokens, retire-style fault records, commit-pending observations,
  progress diagnostics, and parser/model APIs are not executable ISA evidence.
- Compiler/backend code may preserve and validate DSC1 descriptor sideband, but
  must not assume executable ISA semantics from the lane6 carrier.
- Production compiler/backend code must not lower memory compute to executable
  DSC in the current contract.
- `DmaStreamComputeMicroOp.WritesRegister` is `false`; the carrier has no
  architectural register read/write contract.

## Model-Only And Runtime-Side APIs

`DmaStreamComputeRuntime` can execute a guard-accepted descriptor in tests or
explicit runtime orchestration by:

1. creating a `DmaStreamComputeToken`;
2. reading source ranges through `DmaStreamAcceleratorBackend`;
3. computing copy/add/mul/fma/reduce into token-owned staged buffers;
4. moving the token to `CommitPending`;
5. publishing bytes only when `DmaStreamComputeToken.Commit(...)` is called.

This helper is intentionally outside the canonical micro-op `Execute` path.
It does not call `StreamEngine` and does not call `DMAController`.

## Unsupported / Fail-Closed Behavior

Current code does not implement:

- executable lane6 `DmaStreamComputeMicroOp.Execute(...)`;
- async DmaStreamCompute queue, scheduler, overlap, pause, resume, cancel,
  reset, or fence ISA controls;
- virtual/IOMMU-backed or cache-coherent DmaStreamCompute runtime memory;
- stride, tile, 2D, scatter-gather, or partial-success DSC1 execution modes;
- executable DSC2 token issue, runtime execution, memory publication, or
  production compiler/backend lowering;
- StreamEngine or DMAController fallback when DSC runtime/helper validation
  rejects.

Unsupported descriptor fields and raw VLIW carrier attempts fail closed instead
of being normalized into an executable operation.

## DSC1 Descriptor ABI

Code evidence:

- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeDescriptorParser.cs`
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeDescriptor.cs`

The implemented descriptor ABI is exactly:

- magic: `0x31435344`, `"DSC1"` as a little-endian scalar;
- ABI version: `1`;
- header size: `128`;
- range entry size: `16`;
- supported operations: `Copy`, `Add`, `Mul`, `Fma`, `Reduce`;
- supported element types: `UInt8`, `UInt16`, `UInt32`, `UInt64`,
  `Float32`, `Float64`;
- supported shapes: `Contiguous1D`; `FixedReduce` only for `Reduce`;
- range encoding: `InlineContiguous`;
- partial completion policy: `AllOrNone`;
- owner binding fields: `OwnerVirtualThreadId`, `OwnerContextId`,
  `OwnerCoreId`, `OwnerPodId`, `DeviceId`, `OwnerDomainTag`;
- required authority: `DmaStreamComputeOwnerGuardDecision` from the guard plane;
  replay/certificate identity cannot substitute for the guard decision;
- range entry layout: little-endian `{ ulong address, ulong length }`;
- range rules: non-zero length, element-size alignment, no
  `address + length` overflow.

Parser rejection surfaces include unsupported ABI/operation/type/shape,
non-zero reserved fields, unsupported range encoding, non-`AllOrNone` policy,
bad range-table alignment, zero length, overflow, missing guard decision, and
owner/guard mismatch.

## DSC2 Parser-Only Boundary

Ex1 Phase07 adds a DSC2 parser-only/capability foundation. Current DSC2 evidence
is limited to descriptor parsing, capability classification, and deterministic
exact or conservative footprint normalization. It is explicitly not current
lane6 execution.

Parser-only DSC2 descriptors, capability grants, address-space descriptors,
strided/tile/scatter-gather footprints, and normalized footprint summaries:

- do not allocate pipeline tokens;
- do not call `DmaStreamComputeRuntime`;
- do not publish memory;
- do not prove IOMMU-backed execution;
- do not authorize successful partial completion;
- do not authorize compiler/backend production lowering.

## Memory, Ordering, Commit, And Fault Semantics

Code evidence:

- `DmaStreamAcceleratorBackend.TryReadRange(...)`
- `DmaStreamComputeToken.Commit(...)`
- `Processor.MainMemoryArea.TryReadPhysicalRange(...)`
- `Processor.MainMemoryArea.TryWritePhysicalRange(...)`

Current runtime/helper memory is physical main memory:

- source reads use exact physical main memory ranges;
- destination bytes are staged in token-owned buffers;
- staged writes are not visible before token commit;
- commit writes physical main memory only from `CommitPending` after fresh
  owner/domain guard validation and exact staged coverage checks;
- all-or-none commit snapshots destination bytes and rolls back any partial
  write failure before reporting a fault;
- bounds failures become model/token faults and can produce retire-style
  exceptions only through explicit token commit result APIs.

`IBurstBackend`, `IOMMUBurstBackend`, no-fallback resolver tests, cache
flush/invalidate helpers, conflict/cache observers, and address-space models are
infrastructure or model evidence. The current DSC runtime/helper path does not
use them as executable DSC integration. There is no current virtual translation,
IOMMU-backed DSC runtime memory, cache coherency, global CPU load/store conflict
authority, or architectural async DMA overlap for this runtime/helper path. In
short: no current async DMA overlap.

Progress, poll, wait, fence, and retire-style diagnostics do not publish memory.
`AllOrNone` remains the only successful completion policy; successful partial
completion is future-gated and rejected for DSC1.

## Future Design

The following require explicit architecture approval before they can move into
the current contract:

- executable lane6 micro-op semantics;
- pipeline token allocation for lane6 DSC;
- production compiler/backend executable DSC lowering;
- async DmaStreamCompute scheduler/queue/completion;
- pause/resume/cancel/reset/fence ISA controls;
- virtual/IOMMU/cache-coherent runtime memory;
- stride/tile/2D/scatter-gather descriptor forms;
- partial completion as a successful architectural mode;
- integration with global fences or CPU load/store conflict management.

The Ex1 dependency order is:

1. Phase02 executable lane6 DSC ADR approval.
2. Phase03 token store and issue/admission allocation.
3. Phase04 precise retire fault publication.
4. Phase05 ordering/conflict service.
5. Phase06 explicit physical/IOMMU backend selection with no fallback.
6. Phase07 DSC2/capability executable-use gate for any new descriptor feature.
7. Phase08 all-or-none/progress and any later partial-success ADR.
8. Phase09 explicit non-coherent cache flush/invalidate protocol; coherent DMA
   still requires a separate ADR.
9. Phase11 compiler/backend lowering contract.
10. Phase12 conformance and documentation migration.

Phase13 records this order as a planning/documentation gate only. It must not be
used as implementation approval, and downstream parser/model/helper/cache/
backend/compiler evidence must not satisfy upstream execution gates.

## Code Evidence Links

- `HybridCPU_ISE/Core/Pipeline/MicroOps/DmaStreamComputeMicroOp.cs`
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeDescriptorParser.cs`
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs`
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamAcceleratorBackend.cs`
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeToken.cs`
- `HybridCPU_ISE/Processor/Memory/Processor.Memory.cs`
- `HybridCPU_ISE.Tests/tests/DmaStreamCompute*.cs`
- `HybridCPU_ISE.Tests/CompilerTests/DmaStreamComputeCompilerContractTests.cs`

## Glossary

- `DMA`: separate memory transfer controller/channel API; not DSC token
  lifecycle.
- `StreamEngine`: in-core stream/vector executor; not DSC descriptor executor.
- `DmaStreamCompute`: lane6 descriptor carrier plus explicit runtime helper and
  token model.
- `backend`: runtime/model executor used by explicit helper code.
- `token`: staged state and commit container; evidence, not authority by itself.
- `commit`: explicit publication operation after guard and coverage checks.
- `retire`: pipeline publication/exception boundary; not implied by helper
  execution.
- `fence`: future executable semantics only unless a model helper explicitly
  says otherwise.
- `queue`: future DSC feature; no current lane6 ISA queue exists.
