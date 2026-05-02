# StreamEngine, SFR/SRF, And VectorALU

## Scope

This file summarizes the live raw stream/vector execution stack:

- `StreamExecutionRequest` ingress projection.
- `StreamEngine` dispatch, memory movement, and retire publication helpers.
- `BurstIO`, address generation, stream descriptors, and explicit memory
  subsystem binding.
- `StreamRegisterFile` (`SRF`; external notes may say `SFR`) warm, bypass,
  assist-owned allocation, invalidation, and telemetry.
- `VectorALU` typed element compute.

This is not the `DmaStreamCompute` runtime/helper path and is not a
`DmaStreamCompute` descriptor executor. StreamEngine/SRF/VectorALU can share
lane6-adjacent memory concepts, but descriptor-backed lane6 `DmaStreamCompute`
must not silently degrade into this raw stream/vector surface.

## Primary Code Surfaces

- `HybridCPU_ISE/Core/Execution/StreamEngine/StreamExecutionRequest.cs`
- `HybridCPU_ISE/Core/Execution/StreamEngine/StreamEngine.cs`
- `HybridCPU_ISE/Core/Execution/StreamEngine/StreamEngine.Execute1D.cs`
- `HybridCPU_ISE/Core/Execution/StreamEngine/StreamEngine.ExecuteModes.cs`
- `HybridCPU_ISE/Core/Execution/StreamEngine/StreamEngine.BurstIO.cs`
- `HybridCPU_ISE/Core/Execution/StreamEngine/StreamEngine.Prefetch.cs`
- `HybridCPU_ISE/Core/Execution/StreamEngine/StreamEngine.AddressGen.cs`
- `HybridCPU_ISE/Core/Execution/StreamEngine/StreamDescriptors.cs`
- `HybridCPU_ISE/Core/Execution/Compute/VectorALU*.cs`
- `HybridCPU_ISE/Memory/Registers/StreamRegisterFile*.cs`

Primary proof surfaces:

- `HybridCPU_ISE.Tests/tests/Phase09StreamIngressWarmupTests.cs`
- `HybridCPU_ISE.Tests/tests/Phase09StreamEngineMemorySubsystemBindingSeamTests.cs`
- `HybridCPU_ISE.Tests/tests/Phase09VectorNonRepresentableAddressingClosureTests.cs`
- `HybridCPU_ISE.Tests/EvaluationAndMetrics/StreamRegisterFileBypassTests.cs`

## Ingress Projection

`StreamExecutionRequest` is the validated ingress projection for live
StreamEngine execution. It carries only the runtime facts needed by StreamEngine:

- opcode and datatype
- predicate mask
- destination/source pointers
- immediate
- stream length
- stride and row stride
- indexed and 2D mode bits
- tail-agnostic and mask-agnostic policy bits

`CreateValidatedCompatIngress(...)` calls
`VLIW_Instruction.ValidateWord3ForProductionIngress(...)`. That means:

- `word3[50]`, the retired policy gap, must be zero.
- `word3[49:48]`, the raw `VirtualThreadId` hint, is ignored by live
  execution.
- Raw VT is never owner/domain authority.
- Compat-only transport fields do not survive as execution legality facts.

The helper classifies important visibility contours:

- `IsScalar` is true when `StreamLength <= 1`.
- scalar-retire-visible carriers include scalar length-1 operations and `VPOPC`.
- predicate-state carriers include mask/comparison surfaces.
- memory-visible carriers exclude scalar and predicate-only surfaces.
- unsupported scalarized vector helper, zero-length helper, and control helper
  surfaces are explicit rejection surfaces.

## StreamEngine Dispatch

`StreamEngine.Execute(...)` has two ingress overloads:

- raw `VLIW_Instruction` -> validated `StreamExecutionRequest`
- already-projected `StreamExecutionRequest`

The execution owner is resolved from the explicit owner thread when valid, else
from the active virtual thread. The raw VT hint from `word3` is not consulted as
authority.

The dispatch order is:

1. Reject zero-length stream execution unless a narrow retire-helper contour
   explicitly supports it.
2. Reject unsupported raw transfer vector contours.
3. Reject indexed/2D addressing for vector helpers that cannot represent those
   modes safely.
4. Reject raw descriptor-less FMA contours that would otherwise smuggle a hidden
   operand through `Immediate`.
5. Route length-1 scalar register operations through the scalar register helper
   only after opcode support checks.
6. Route mask manipulation through predicate state helpers.
7. Resolve element size and memory-visible vector path.
8. Dispatch indexed, 2D, predicative movement, double-buffered 1D, or normal 1D.

Unsupported cases fail closed instead of publishing partial scalar state, hidden
success, stale scratch state, or silent no-op success.

`StreamEngine.Execute(...)` executes stream/vector operations only. It does not
parse DSC1 descriptors and does not allocate or commit `DmaStreamComputeToken`
state. `CaptureRetireWindowPublications(...)` publishes narrow stream/vector
retire facts; it is not a DmaStreamCompute token commit surface.

## Addressing And Descriptor Shapes

The StreamEngine surface supports these raw vector/memory shapes:

- 1D contiguous or strided transfer.
- 2D transfer with row stride and row length.
- indexed transfer through `Indexed2SrcDesc`.
- tri-operand FMA through `TriOpDesc` when the raw contour has an explicit
  descriptor.

`Indexed2SrcDesc` carries:

- `Src2Base`
- `IndexBase`
- `IndexStride`
- `IndexType` where `0` means `uint32` and `1` means `uint64`
- `IndexIsByteOffset` where `0` means element index and `1` means byte offset

`TriOpDesc` carries:

- `SrcA`
- `SrcB`
- `StrideA`
- `StrideB`

These are StreamEngine descriptors. They are not the `DmaStreamCompute`
descriptor ABI.

## BurstIO And Memory Binding

`BurstIO` is the memory movement substrate used by StreamEngine:

- It prefers an explicit `MemorySubsystem` argument when present.
- It can fall back to the active/global memory subsystem for legacy StreamEngine
  entry points.
- It respects AXI4-style constraints such as maximum burst length and 4 KB
  boundaries.
- Large contiguous reads/writes may use DMA controller paths.
- Contiguous reads first try an exact SRF prefetched chunk hit.
- Strided reads/writes run element-by-element.
- 2D paths are planned as row/segment operations.
- indexed gather/scatter paths use descriptor-provided index windows.

Completion is all-or-fault at the StreamEngine level:

- incomplete burst reads throw instead of returning stale or partial data
- incomplete burst writes throw instead of reporting hidden success
- zero-row-length non-empty 2D transfers fail closed
- successful writes invalidate overlapping SRF windows

The DMA route is synchronous helper behavior in the current ISE:

- `BurstReadViaDMA(...)` and `BurstWriteViaDMA(...)` configure a DMA channel,
  start it, and then loop `DMAController.ExecuteCycle()` until completion or a
  safety limit.
- `BurstWriteViaDMA(...)` writes the source span to the destination memory
  surface before DMA bookkeeping and therefore must not be described as async
  DMA commit.
- `DMAController.NextDescriptor` chaining is placeholder behavior in current
  code.
- These helper paths do not prove architectural CPU/DMA overlap, async DMA
  completion, global ordering, or DmaStreamCompute token commit semantics.

The memory subsystem binding seam is tested so that explicit `memSub` does not
accidentally read from `Processor.Memory`.

These helper paths are independent StreamEngine behavior. They do not prove
lane6 DSC execution, architectural async DMA overlap, IOMMU-backed DSC/L7
execution, a global CPU load/store conflict authority, coherent DMA/cache, or
compiler/backend production lowering.

## Stream Register File

`StreamRegisterFile` is a small SRF used for stream ingress warming and bypass:

- default register count: 8
- maximum vector length model: 32 elements
- maximum element size: 8 bytes
- register payload: 256 bytes
- states: `Invalid`, `Loading`, `Valid`, `Dirty`

Each SRF entry tracks:

- byte payload
- state
- source address
- valid byte count
- last access time
- element size
- element count
- assist-owned flag

Foreground allocation:

- exact valid source/element/byte coverage is a hit
- loading entries are not foreground hits
- misses pick an LRU victim
- newly allocated foreground entries clear the assist-owned flag

SRF reads require:

- `Valid` state
- matching source address
- matching element size
- enough valid bytes for the requested element count

`TryReadPrefetchedChunk(...)` is intentionally narrow: it is for packed,
contiguous, exact-source chunks. It is not a general cache lookup.

Writes through StreamEngine invalidate overlapping SRF ranges so later reads
cannot consume stale warmed data.

## Assist-Owned SRF Partition

Assist-owned SRF allocation is stricter than foreground allocation:

- an existing compatible valid/loading assist entry can be reused
- an empty slot can be used only within the resident budget
- loading entries count against the loading budget
- assist allocation cannot evict foreground-owned entries
- if no assist-owned victim exists, allocation rejects

Reject taxonomy:

- `ResidentBudget`
- `LoadingBudget`
- `NoAssistVictim`

The default assist SRF partition policy is two resident registers and one loading
register.

## Warming And Prefetch

`StreamEngine.Prefetch.cs` exposes both foreground and assist warming:

- foreground `PrefetchToStreamRegister(...)`
- assist `TryPrefetchToAssistStreamRegister(...)`
- public lane6 assist seam `ScheduleLane6AssistPrefetch(...)`

The maximum SRF resident chunk is 256 bytes, capped by element size and requested
element count.

The warm path:

1. records warm attempt telemetry
2. reuses an already-valid register when possible
3. marks the register loading
4. asks IOMMU for a read translation warm over the assist range
5. reads through the active backend
6. loads SRF bytes and marks the register valid
7. records success or reject telemetry

Translation or backend failure invalidates the in-progress register and returns
failure. It does not publish warm success.

This IOMMU warm path is an assist/StreamEngine ingress helper. It is not
executable DSC/L7 IOMMU integration evidence and does not weaken the Phase06
no-fallback gate for future executable memory paths.

Foreground 1D/2D/indexed execution can plan the next chunk and warm ahead when
the shape is contiguous and bounded by SRF capacity. Indexed warming can warm the
destination and packed index stream.

## SRF Telemetry

`StreamIngressWarmTelemetry` reports:

- foreground warm attempts, successes, reuse hits, and bypass hits
- assist warm attempts, successes, reuse hits, and bypass hits
- translation rejects
- backend rejects
- assist resident-budget rejects
- assist loading-budget rejects
- assist no-victim rejects

This telemetry is evidence only. It is not authority for owner, domain, replay,
execution, or commit.

## VectorALU

`VectorALU` performs typed element compute over spans supplied by StreamEngine.
It is not a lane6 descriptor executor.

Implemented compute families include:

- binary vector operations
- binary-immediate operations
- unary operations
- comparisons that produce predicate masks
- mask operations and `VPOPC`
- FMA variants
- reductions
- saturating arithmetic
- bit manipulation helpers
- predicative movement
- gather/scatter helpers
- dot-product and widening dot-product contours
- permutation and slide contours

Execution is datatype-aware:

- floating point uses `ElementCodec.LoadF` and floating compute helpers
- signed integer uses `LoadI`
- unsigned integer uses `LoadU`
- raw bitwise operations on floating data use raw element bits where required

Predication and tail/mask behavior:

- inactive lanes are skipped unless mask-agnostic policy allows overwrite
- tail-agnostic and mask-agnostic flags are passed from
  `StreamExecutionRequest`
- comparison is limited by predicate register width

Exception tracking:

- vector dirty state is marked on vector operations
- divide-by-zero, invalid operation, overflow, underflow, and inexact counters
  are updated
- exception mode can accumulate, trap, interrupt, or save vector context

## Retire Publication

`CaptureRetireWindowPublications(...)` is narrow by design. It publishes only
retire-visible scalar or predicate outcomes that can be represented safely:

- scalar register stream operations after opcode checks
- `VPOPC` scalar result
- predicate-mask state where the helper surface is predicate-visible

Unsupported indexed/2D/direct-retire vector addressing is rejected before retire
publication. Memory-visible vector effects are not silently converted into
scalar retire records.

## Fail-Closed Boundaries

These patterns remain invalid:

- raw `VLOAD`/`VSTORE` through unsupported legacy raw stream contour
- non-representable indexed/2D addressing for helpers that cannot safely express
  it
- raw descriptor-less FMA
- uninitialized raw scratch success
- zero-length hidden no-op success
- stale SRF reads after writes
- partial BurstIO transfer success
- using StreamEngine or VectorALU as fallback for `DmaStreamCompute`

## Operational Summary

StreamEngine is the raw stream/vector executor. SRF is a bounded ingress-warm and
bypass structure. VectorALU is a typed compute helper. Together they provide
stream/vector execution, but they do not grant descriptor-backed lane6 authority.
When lane6 descriptor-backed compute is requested, the `DmaStreamCompute` typed
sideband carrier plus explicit runtime/helper path is the only implemented
model contour, and direct micro-op execution remains fail-closed.

Ex1 Phase13 treats StreamEngine, SRF, assist, cache/prefetch, IOMMU warm, and
DMA helper evidence as downstream or adjacent evidence only. None of these
surfaces can close upstream executable DSC/L7/DSC2, async overlap, coherent
DMA/cache, or production compiler/backend lowering gates.
