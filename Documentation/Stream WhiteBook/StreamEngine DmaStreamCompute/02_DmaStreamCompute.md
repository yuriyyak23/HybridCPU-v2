# DmaStreamCompute

## Scope

`DmaStreamCompute` is the lane6 descriptor/decode carrier path plus explicit
runtime/helper token model for descriptor-backed memory compute. Direct
`DmaStreamComputeMicroOp.Execute(...)` is disabled and must fail closed.

This summary covers:

- ISA and compiler carrier surface
- typed sideband descriptor transport
- native VLIW carrier validation
- descriptor ABI validation
- owner/domain guard authority
- decode and projector behavior
- micro-op placement and fail-closed execution boundary
- explicit runtime helper, token, commit, replay, and telemetry contours
- rejected fallback patterns

## Primary Code Surfaces

ISA and classification:

- `HybridCPU_ISE/Core/Common/CPU_Core.Enums.cs`
- `HybridCPU_ISE/Arch/OpcodeInfo.Registry.Data.MemoryControl.cs`
- `HybridCPU_ISE/Arch/InstructionClassifier.cs`
- `HybridCPU_ISE/Arch/IsaV4Surface.cs`
- `HybridCPU_ISE/Core/Diagnostics/InstructionRegistry.Initialize.Base.cs`

Compiler and IR:

- `HybridCPU_Compiler/API/Threading/HybridCpuThreadCompilerContext.cs`
- `HybridCPU_Compiler/Core/IR/Model/IrSlotMetadata.cs`
- `HybridCPU_Compiler/Core/IR/Construction/HybridCpuIrBuilder.cs`
- `HybridCPU_Compiler/Core/IR/Bundling/HybridCpuBundleLowerer.cs`
- `HybridCPU_Compiler/Core/IR/Hazards/HybridCpuHazardModel.cs`
- `HybridCPU_Compiler/Core/IR/Hazards/HybridCpuOpcodeSemantics.cs`

Transport, decode, and projection:

- `HybridCPU_ISE/Core/Contracts/CompilerTransport/InstructionSlotMetadata.cs`
- `HybridCPU_ISE/Core/Decoder/VliwDecoderV4.cs`
- `HybridCPU_ISE/Core/Pipeline/MicroOps/InstructionIR.cs`
- `HybridCPU_ISE/Core/Decoder/DecodedBundleTransportProjector.cs`

Descriptor/runtime:

- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeDescriptor.cs`
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeDescriptorParser.cs`
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeValidationResult.cs`
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeTelemetry.cs`
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeReplayEvidence.cs`
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeToken.cs`
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs`
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamAcceleratorBackend.cs`
- `HybridCPU_ISE/Core/Pipeline/MicroOps/DmaStreamComputeMicroOp.cs`

Primary proof surfaces:

- `HybridCPU_ISE.Tests/CompilerTests/DmaStreamComputeCompilerContractTests.cs`
- `HybridCPU_ISE.Tests/tests/DmaStreamCompute*.cs`
- `HybridCPU_ISE.Tests/tests/Phase12CompilerContractHandshakeTests.cs`
- `HybridCPU_ISE.Tests/tests/Phase12VliwCompatFreezeTests.cs`
- `HybridCPU_ISE.Tests/tests/CompilerParallelDecompositionCanonicalContourTests.cs`
- `HybridCPU_ISE.Tests/tests/Phase09NativeVliwBoundaryDocumentationTests.cs`

## Current Implemented Boundary

Current code implements descriptor preservation, typed-slot placement, owner
guard validation, parser rejection surfaces, runtime helper APIs, token staging,
and token commit helpers. It does not implement executable lane6 DMA in the
canonical micro-op path.

`DmaStreamComputeDescriptorParser.ExecutionEnabled` is `false`.
`DmaStreamComputeRuntime.ExecuteToCommitPending(...)` is a runtime-side helper,
not `MicroOp.Execute`. Compiler/backend code must not lower production compute
assuming executable lane6 DSC semantics.

Ex1 Phase12/Phase13 add two global guards:

- documentation migration requires architecture approval, code evidence,
  positive/negative tests, compiler/backend conformance, and claim-safety;
- the dependency graph is planning-only and cannot approve executable DSC/L7,
  DSC2 execution, async DMA overlap, IOMMU-backed execution, coherent DMA/cache,
  successful partial completion, or production compiler/backend lowering.

## ISA Surface

`DmaStreamCompute` is registered as an ISA v4 memory operation:

- mnemonic: `DmaStreamCompute`
- pipeline class: `DMA_STREAM`
- instruction class: `Memory`
- serialization class: `MemoryOrdered`
- latency model: 8
- memory footprint class: 3
- memory read/write flags set
- register arity in the opcode registry is zero

It is mandatory in the ISA v4 surface and is not a pseudo-op, hint opcode, or
custom accelerator opcode.

## Compiler Contract

The compiler emits a native `DmaStreamCompute` VLIW instruction with all raw
payload fields cleared:

- `DataType = 0`
- `PredicateMask = 0`
- `Immediate = 0`
- `DestSrc1Pointer = 0`
- `Src2Pointer = 0`
- `StreamLength = 0`
- `Stride = 0`
- `VirtualThreadId = 0`

The actual descriptor travels through typed sideband metadata:

```text
compiler descriptor validation
-> InstructionSlotMetadata.DmaStreamComputeDescriptor
-> IR slot metadata
-> lowered bundle annotations
-> VliwDecoderV4
-> InstructionIR
-> DecodedBundleTransportProjector
-> DmaStreamComputeMicroOp
```

The compiler accepts only explicit adoption modes:

- `Compatibility`
- `Strict`
- `Future`

Any unknown mode throws `ArgumentOutOfRangeException`. Modes do not weaken
owner/domain, descriptor, lane6, or footprint authority.

Compiler emission requires:

- supported descriptor ABI and header
- descriptor reference covering the accepted payload
- descriptor identity hash matching the payload when present
- accepted owner/domain guard decision
- guard owner binding matching descriptor owner binding
- canonical lane6 device id
- supported operation, element type, shape, range encoding, and partial policy
- non-empty read/write footprints and normalized footprints
- non-zero normalized footprint hash

The compiler hazard model classifies the opcode as `IrResourceClass.DmaStream`,
not load/store, vector ALU, scalar ALU, or generic.

## Descriptor ABI

The live v1 descriptor parser uses:

- magic: `0x31435344` (`DSC1` little-endian scalar)
- ABI version: `1`
- fixed header size: `128`
- range entry size: `16`
- range encoding: `InlineContiguous`
- partial completion policy: `AllOrNone`
- canonical device id: `6`

Supported operation kinds:

- `Copy`
- `Add`
- `Mul`
- `Fma`
- `Reduce`

Supported element types:

- `UInt8`
- `UInt16`
- `UInt32`
- `UInt64`
- `Float32`
- `Float64`

Supported shapes:

- `Contiguous1D`
- `FixedReduce` only when operation is `Reduce`

Expected source range counts:

- `Copy`: 1
- `Add`: 2
- `Mul`: 2
- `Fma`: 3
- `Reduce`: 1

Destination range count must be non-zero.

The parser rejects:

- short descriptors
- wrong magic
- unsupported ABI version
- wrong header size
- total size outside the supplied buffer
- descriptor reference that does not cover the payload
- descriptor reference identity mismatch
- dirty flags or reserved fields
- unsupported operation, element type, shape, or range encoding
- non-`AllOrNone` partial completion policy
- wrong range counts
- unaligned range table offsets
- range table overflow
- zero-length memory ranges
- address plus length overflow
- address or length not aligned to element size
- internally overlapping destination ranges
- source/destination overlap except exact in-place snapshot ranges

Accepted descriptor output includes:

- descriptor reference
- ABI/header/total size
- descriptor identity hash
- certificate input hash
- operation, element type, shape, range encoding, partial policy
- owner binding and guard decision
- raw and normalized read/write ranges
- alias policy
- normalized footprint hash

## DSC2 Parser-Only Contour

DSC2 descriptors and capability extensions are current only as parser-only and
model-only surfaces. They can validate extension blocks and produce exact or
conservative normalized footprints for diagnostics/planning, including
address-space, strided, tile/2D, scatter-gather, capability profile, and
footprint-summary forms.

Parser-only DSC2 evidence does not:

- allocate lane6 pipeline tokens;
- invoke `DmaStreamComputeRuntime`;
- publish memory;
- enable IOMMU-backed execution;
- enable successful partial completion;
- prove cache coherency;
- authorize compiler/backend production lowering.

## Owner And Domain Authority

Owner/domain authority is explicit and guard-plane sourced.

Descriptor structural read can locate owner fields but cannot make the
descriptor executable. `Parse(descriptorBytes)` without a guard decision always
fails with owner/domain fault.

Guarded parse requires:

- guard decision is allowed
- guard decision authority source is `GuardPlane`
- guard decision did not attempt replay/certificate reuse
- guard decision descriptor owner binding is present
- guard decision descriptor owner binding equals the structurally-read owner
  fields

Owner binding includes:

- owner virtual thread id
- owner context id
- owner core id
- owner pod id
- owner domain tag
- device id

Runtime owner guard context includes the same owner/core/pod/domain/device
identity plus active domain certificate.

Raw `VirtualThreadId` in VLIW word3 is only a transport hint. It is rejected for
`DmaStreamCompute` native carrier validation when non-zero and never grants
owner/domain authority.

## Native VLIW Carrier Validation

`VliwDecoderV4` validates the carrier before producing canonical `InstructionIR`.

Rules:

- descriptor sideband can only accompany the native `DmaStreamCompute` opcode
- native `DmaStreamCompute` requires typed descriptor sideband
- `word3[50]` retired policy gap must be zero
- `word0[47:40]` reserved bits must be zero
- raw `VirtualThreadId` must be zero
- slot index must be 6
- descriptor sideband must have accepted owner/domain guard decision
- descriptor reference sideband must match descriptor payload

Non-lane6 placement, missing descriptor sideband, dirty reserved bits, raw VT
authority, or reference/payload mismatch fail closed during decode.

## Projection And Micro-Op

`DecodedBundleTransportProjector` creates a `DmaStreamComputeMicroOp` only when
`InstructionIR` carries the guard-accepted descriptor payload.

If only a descriptor reference reaches the projector without payload, the
projector creates a canonical trap micro-op instead of guessing or falling back.

`DmaStreamComputeMicroOp`:

- is the canonical lane6 typed-slot carrier
- requires an accepted owner/domain guard decision
- requires mandatory read and write footprints
- has `MicroOpClass.Dma`
- has `InstructionClass.Memory`
- has `SerializationClass.MemoryOrdered`
- sets `IsMemoryOp = true`
- sets `HasSideEffects = true`
- sets `WritesRegister = false`
- has no architectural read/write registers
- uses `SlotClass.DmaStreamClass`
- carries owner VT/context/domain from descriptor owner binding
- publishes normalized read/write memory ranges
- builds a resource mask over DMA channel, StreamEngine, load/store, and memory
  domain bucket
- self-publishes canonical decode metadata

Current direct `Execute(...)` is disabled and throws:

```text
DmaStreamComputeMicroOp execution is disabled and must fail closed.
The lane6 typed-slot surface preserves descriptor and footprint evidence only.
```

`DmaStreamComputeDescriptorParser.ExecutionEnabled` is also `false`.

This is intentional. It preserves the native ISA/compiler/decode path while
avoiding an unguarded active execution claim.

## Runtime Helper And Token Model

`DmaStreamComputeRuntime` models descriptor execution to commit-pending through
a backend and token helper. This is not the same as enabling direct canonical
micro-op execution.

Runtime helper flow:

1. create `DmaStreamComputeToken`
2. mark issued
3. validate descriptor runtime constraints
4. read operands through `DmaStreamAcceleratorBackend`
5. mark reads complete
6. compute output bytes
7. stage destination writes into token
8. validate exact staged write coverage
9. mark compute complete and transition to `CommitPending`

Supported runtime operations:

- copy exact source footprint to destination footprint
- element-wise add
- element-wise multiply
- element-wise FMA
- reduce to one scalar destination element

Integer operations use unsigned element loading for the supported integer
element types. Floating operations support `Float32` and `Float64`.

The backend:

- reads exact physical ranges from main memory
- rejects out-of-range source reads
- tracks read bursts, bytes read, staged writes, bytes staged, modeled latency,
  and element operation counts
- reports `UsedLane6Backend = true`
- reports `AluLaneOccupancyDelta = 0`
- reports `DirectDestinationWriteCount = 0`

No direct destination write is commit authority.

## Token Commit And Fault Publication

`DmaStreamComputeToken` state machine:

- `Admitted`
- `Issued`
- `ReadsComplete`
- `ComputeComplete`
- `CommitPending`
- `Committed`
- `Faulted`
- `Canceled`

Destination writes are staged in token-owned buffers. A staged write is accepted
only if it is inside the normalized write footprint.

Commit requires:

- token state is `CommitPending`
- fresh owner/domain guard validation succeeds
- guard authority is still guard-plane sourced
- no replay/certificate authority substitution
- owner/context/core/pod/device/domain did not drift
- active domain certificate satisfies the current domain coverage predicate
- staged writes exactly cover normalized write ranges
- all-or-none physical write commit succeeds

The current domain coverage predicate treats domain `0` as covered only by
certificate `0`; non-zero domains accept certificate `0` or an overlapping
domain bit. This is a live-code fact, not a statement that certificates are
authority by themselves.

If a physical write fails after partial writes, prior writes are rolled back
from snapshots. Failure is not reported as visible success.

Faults can produce retire exceptions:

- domain and owner context violations map to domain faults
- translation, permission, alignment, device, partial completion, replay
  invalidation, and memory faults map to page-fault style publication
- descriptor/unsupported/execution-disabled surfaces publish ordinary invalid
  operation exceptions

Tokens are evidence and commit containers. A token alone does not grant
authority.

Commit-pending, progress counters, poll/wait/fence diagnostics, and retire-style
fault helpers do not publish memory. `AllOrNone` remains the only successful
completion policy in the current contract.

## Replay And Evidence

Replay evidence contains:

- descriptor reference
- descriptor ABI/header/size/identity
- certificate input hash
- operation, element type, shape, range encoding, partial policy, alias policy
- carrier evidence
- footprint evidence
- owner/domain evidence
- token lifecycle evidence
- lane placement evidence
- envelope hash

Replay reuse requires complete evidence and exact match across descriptor shape,
carrier, certificate input, footprint, owner/domain, token lifecycle, lane
placement, and envelope hash.

Replay misses are classified with explicit invalidation reasons such as:

- descriptor payload lost
- carrier mismatch
- incomplete evidence
- descriptor mismatch
- certificate input mismatch
- footprint mismatch
- owner/domain mismatch
- token evidence mismatch
- lane placement mismatch

Custom registry carriers and missing descriptor payloads are not replay-reusable
canonical carriers.

## Pressure And Telemetry

Admission pressure surfaces:

- lane6 unavailable
- DMA credits
- SRF credits
- memory subsystem pressure
- outstanding token cap

Pressure rejects are telemetry/admission rejects before token creation. They do
not grant fallback execution.

Telemetry counters track:

- descriptor parse attempts, accepts, rejects
- token accepted, active, staged, committed, canceled, faulted
- descriptor, owner/domain, device, token, quota, and backpressure fault families
- lane6, DMA credit, SRF credit, memory pressure, and token cap rejects
- unsupported carrier rejects
- replay reuse hits and rejects
- bytes read, bytes staged, and operation counts
- last validation fault, token fault, pressure reject, and replay mismatch field

Telemetry is evidence only.

## Legacy Quarantine

The following remain fail-closed or negative-control seams:

- `CustomAcceleratorMicroOp`
- custom accelerator registry paths
- MatMul accelerator fixtures
- accelerator DMA seams
- raw registry factory for `DmaStreamCompute`

They do not become canonical `DmaStreamCompute` success paths.

## Rejected Patterns

Invalid patterns:

- lane6 as a fifth ALU
- `DmaStreamCompute` outside slot 6
- missing typed descriptor sideband
- descriptor reference without payload
- dirty reserved bits or retired policy-gap bit as ABI
- raw VT hint as authority
- scalar/vector/ALU/`GenericMicroOp` fallback
- StreamEngine/VectorALU fallback for descriptor-backed lane6 compute
- direct DMA destination write as commit
- telemetry, replay evidence, certificate identity, or token identity as
  authority
- unknown compatibility mode accepted by default
- custom accelerator registry as canonical execution

## Operational Summary

`DmaStreamCompute` is a native lane6 typed-sideband descriptor path. Its current
canonical decode surface can carry and verify descriptor, footprint, owner/domain,
lane placement, replay, token, and telemetry evidence. Direct micro-op execution
is intentionally disabled and fail-closed. Runtime helper/token tests model the
commit contour, but they do not weaken the canonical lane6 boundary.

Phase13 downstream evidence non-inversion applies here: parser-only descriptors,
model tokens, helper runtime paths, backend/addressing infrastructure,
conflict/cache observers, and compiler sideband transport are useful evidence
only when labeled as such. They do not satisfy upstream execution gates.
