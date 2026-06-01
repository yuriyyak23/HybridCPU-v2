# Non-VMX Iteration 08A Vector Widen/Narrow/Convert Leaf Templates Snapshot

Date: 2026-05-26

Scope: `HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx/Lanes00_03Vector/{Widening,Narrowing,Conversion}/`.

## Closed Boundary

Iteration 08A materializes the vector widen/narrow/convert no-emission template metadata directly in the per-instruction leaf partial files. This is not an executable instruction closure.

Leaf templates materialized:

- `VWADD`, `VWADDU`, `VWSUB`, `VWSUBU`, `VWMUL`, `VWMULU`, `VWMACC`
- `VNSRL`, `VNSRA`
- `VSEXT`, `VCVT.I`, `VCVT.U`, `VCVT.F`

## Evidence Statement

Each leaf template exposes mnemonic/operand/evidence metadata and sets:

- `RequiresVectorLegalityMatrixClosure = true`
- `RequiresRetireStagedPublication = true`
- `IsExecutable = false`
- `CompilerHelperAllowed = false`

Widening and `VSEXT` rows carry source/destination width ABI markers. Widening rows also carry signedness ABI markers; `VWMACC` additionally carries accumulator ABI metadata. Narrowing rows carry narrowing and rounding/saturation/trap policy markers. Conversion rows carry conversion and rounding/saturation/trap policy markers.

No Iteration 08A row allocates:

- numeric opcode or descriptor op-type
- decoder/encoder path
- `InstructionIR` projection
- registry/materializer entry
- typed MicroOp
- executable `VectorLegalityMatrix` contour
- execute/capture semantics
- retire/writeback or vector publication semantics
- compiler helper emission authority

## ABI Blockers

- Widening rows: source/destination width, signedness, accumulator footprint, overflow behavior, and staged publication remain open.
- Narrowing rows: truncation/rounding/saturation/trap policy and mask/tail behavior remain open.
- `VSEXT`: source width and signedness remain open and separate from closed `VZEXT`.
- `VCVT.I`/`VCVT.U`/`VCVT.F`: NaN, rounding, saturation/trap policy, and result footprint remain open.

## Verification

- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`
- targeted `dotnet test` for `NonVmxIteration04BDeferredTemplateSurfaceTests` plus related Non-VMX catalog/no-emission parity tests.
