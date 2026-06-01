# Non-VMX Iteration 09A Vector Segment/Structure Memory Leaf Templates Snapshot

Date: 2026-05-26

Scope: `HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx/Lanes00_03Vector/StructureMovement/` and `HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx/Lanes04_05Memory/{Segments,Shapes2D,Indexed2D}/`.

## Closed Boundary

Iteration 09A materializes the vector segment/structure memory no-emission template metadata directly in the per-instruction leaf partial files. This is not an executable instruction closure.

Leaf templates materialized:

- `VZIP`, `VUNZIP`, `VINTERLEAVE`, `VDEINTERLEAVE`
- `VLDSEG2`, `VLDSEG4`, `VLDSEG8`
- `VSTSEG2`, `VSTSEG4`, `VSTSEG8`
- 2D `VLOAD`, 2D `VSTORE`
- indexed+2D `VGATHER`, indexed+2D `VSCATTER`

## Evidence Statement

Each leaf template exposes mnemonic/operand/evidence metadata and sets:

- `RequiresVectorLegalityMatrixClosure = true`
- `IsExecutable = false`
- `CompilerHelperAllowed = false`

Structure movement rows additionally set:

- `RequiresStructureShapeAbi = true`
- `NoHiddenStreamEngineFallback = true`
- `RequiresRetireStagedPublication = true`

Segment load/store rows additionally set:

- `RequiresMemoryShapeAbi = true`
- `RequiresFaultReplayPolicy = true`
- `SegmentCount = 2`, `4`, or `8`
- `IsSegmentLoad = true` with `RequiresRetireStagedPublication = true`
- `IsSegmentStore = true` with `RequiresRetireStagedCommit = true`

2D and indexed+2D contour rows additionally set:

- `RequiresMemoryShapeAbi = true`
- `RequiresFaultReplayPolicy = true`
- `NoBaseOpcodeDuplication = true`
- `Requires2DShapeSideband = true` or `RequiresIndexed2DShapeSideband = true`
- load-like contours use `RequiresRetireStagedPublication = true`
- store-like contours use `RequiresRetireStagedCommit = true`

No Iteration 09A row allocates:

- numeric opcode or descriptor op-type
- decoder/encoder path
- `InstructionIR` projection
- registry/materializer entry
- typed MicroOp
- executable `VectorLegalityMatrix` contour
- execute/capture semantics
- retire/writeback or memory publication/commit semantics
- compiler helper emission authority

## ABI Blockers

- Structure movement rows: shape sideband ABI, staged movement publication, and hidden StreamEngine fallback prohibition remain open.
- Segment loads: memory shape ABI, byte ordering, partial fault publication, and replay policy remain open.
- Segment stores: exact byte commit order, fault/replay policy, and retire-owned commit semantics remain open.
- 2D `VLOAD`/`VSTORE`: rectangular shape descriptor, partial fault model, and conformance matrix remain open.
- indexed+2D `VGATHER`/`VSCATTER`: index surface semantics, 2D bounds/fault ordering, and descriptor-backed evidence remain open.

## Verification

- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`
- targeted `dotnet test` for `NonVmxIteration04BDeferredTemplateSurfaceTests` plus related Non-VMX catalog/no-emission parity tests.
