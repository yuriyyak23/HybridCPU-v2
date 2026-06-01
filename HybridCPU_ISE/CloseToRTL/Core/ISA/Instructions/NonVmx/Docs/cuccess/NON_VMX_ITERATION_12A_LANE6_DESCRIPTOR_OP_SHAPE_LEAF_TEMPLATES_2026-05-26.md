# Non-VMX Iteration 12A Lane6 Descriptor Op/Shape Leaf Templates Snapshot

Date: 2026-05-26

Scope: `HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx/Lane06DmaStream/DescriptorOps/`.

## Closed Boundary

Iteration 12A materializes Lane6 descriptor op-type and shape/range no-emission template metadata directly in the per-instruction leaf partial files. This is not an executable descriptor-runtime closure.

Leaf templates materialized:

- `DmaStreamCompute.SUB`, `DmaStreamCompute.MIN`, `DmaStreamCompute.MAX`
- `DmaStreamCompute.ABSDIFF`, `DmaStreamCompute.CLAMP`
- `DmaStreamCompute.CONVERT`
- `DmaStreamCompute.COMPARE`, `DmaStreamCompute.SELECT`
- `DmaStreamCompute.REDUCE_SUM`, `DmaStreamCompute.REDUCE_MIN`, `DmaStreamCompute.REDUCE_MAX`
- `DmaStreamCompute.REDUCE_AND`, `DmaStreamCompute.REDUCE_OR`, `DmaStreamCompute.REDUCE_XOR`
- `DSC_SHAPE_STRIDED`, `DSC_SHAPE_TILED`, `DSC_SHAPE_SCATTER_GATHER`, `DSC_SHAPE_2D`, `DSC_SHAPE_MULTI_RANGE`

## Evidence Statement

Each descriptor op leaf template exposes mnemonic/operand/evidence metadata and sets:

- `IsDescriptorOwned = true`
- `HasScalarOpcodeAllocation = false`
- `RequiresDescriptorOpTypeAllocation = true`
- `RequiresDescriptorParserValidation = true`
- `RequiresOwnerDomainGuard = true`
- `RequiresTokenAdmission = true`
- `RequiresStagedCommit = true`
- `RequiresRetireCommitAuthority = true`
- `RequiresReplayDeterminism = true`
- `NoGuestVisibleHostEvidence = true`
- `IsExecutable = false`
- `CompilerHelperAllowed = false`

Descriptor op families additionally set the future policy ABI marker needed by the row:

- arithmetic/subtract policy for `SUB`
- signedness/type policy for `MIN` and `MAX`
- overflow policy for `ABSDIFF`
- bounds policy for `CLAMP`
- conversion plus rounding/saturation/trap policy for `CONVERT`
- predicate footprint policy for `COMPARE`
- predicate and result footprint policy for `SELECT`
- reduction result and scalar-or-surface result policy for explicit reductions

Each descriptor shape leaf template sets:

- `RequiresShapeEnumAllocation = true`
- `RequiresDescriptorParserValidation = true`
- `RequiresOwnerDomainGuard = true`
- `RequiresTokenAdmission = true`
- `RequiresNormalizedFootprintAbi = true`
- `RequiresPartialCompletionPolicy = true`
- `RequiresStagedCommit = true`
- `RequiresRetireCommitAuthority = true`
- `RequiresReplayDeterminism = true`
- `NoGuestVisibleHostEvidence = true`
- one shape-specific ABI marker for stride, tile shape, index surface, 2D shape, or multi-range shape

No Iteration 12A row allocates:

- scalar numeric opcode
- descriptor op enum value or descriptor shape enum value
- decoder/encoder path
- `InstructionIR` projection
- registry/materializer entry
- typed MicroOp
- execute/capture semantics
- retire/writeback or descriptor commit semantics
- compiler helper emission authority

## ABI Blockers

- Descriptor op enum allocation and descriptor parser validation remain open.
- Descriptor source/destination range, type, shape, owner/domain, signedness, bounds, predicate/result footprint, scalar/surface reduction result, and normalized shape ABI remain open.
- Token admission, staged commit, retire-owned publication, replay/rollback, and all-or-none or explicit partial-completion policy remain open.
- No host-owned evidence may be published as guest architectural state.
- Lane6 queue lifecycle, query commands, and `DSC2` remain separate deferred rows for a later pool.

## Verification

- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`
- targeted `dotnet test` for `NonVmxIteration04BDeferredTemplateSurfaceTests` plus related Non-VMX catalog/no-emission parity tests.
