# Non-VMX Iteration 07A Vector Predicate Leaf Templates Snapshot

Date: 2026-05-26

Scope: `HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx/Lanes00_03Vector/PredicateMask/`.

## Closed Boundary

Iteration 07A materializes the vector predicate/select no-emission template metadata directly in the per-instruction leaf partial files. This is not an executable instruction closure.

Leaf templates materialized:

- `VMERGE`
- `VSELECT`
- `VFIRST`
- `VANY`
- `VALL`
- `VMSIF`
- `VMSOF`

## Evidence Statement

Each leaf template exposes mnemonic/operand/evidence metadata and sets:

- `RequiresPredicateMaskSideband = true`
- `RequiresVectorLegalityMatrixClosure = true`
- `RequiresRetireStagedPublication = true`
- `IsExecutable = false`
- `CompilerHelperAllowed = false`

Scalar-result predicate summaries additionally carry `RequiresScalarResultAbi = true`.
Predicate-only forms additionally carry `RequiresPredicateOnlyPublication = true`.

No Iteration 07A row allocates:

- numeric opcode or descriptor op-type
- decoder/encoder path
- `InstructionIR` projection
- registry/materializer entry
- typed MicroOp
- executable `VectorLegalityMatrix` contour
- execute/capture semantics
- retire/writeback or predicate publication semantics
- compiler helper emission authority

## ABI Blockers

- `VMERGE`/`VSELECT`: predicate-mask sideband, mask/tail policy, and staged publication remain open.
- `VFIRST`/`VANY`/`VALL`: scalar result footprint, no-active-bit sentinel, and active VL/tail semantics remain open.
- `VMSIF`/`VMSOF`: predicate-only publication remains open and separate from closed `VMSBF`.

## Verification

- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`
- targeted `dotnet test` for `NonVmxIteration04BDeferredTemplateSurfaceTests` plus related Non-VMX catalog/no-emission parity tests.
