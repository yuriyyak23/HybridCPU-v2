# Non-VMX Iteration 10A Vector Fixed-Point/Saturating Leaf Templates Snapshot

Date: 2026-05-26

Scope: `HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx/Lanes00_03Vector/{PrefixScan,SaturatingFixedPoint}/`.

## Closed Boundary

Iteration 10A materializes the vector fixed-point/saturating and prefix-scan no-emission template metadata directly in the per-instruction leaf partial files. This is not an executable instruction closure.

Leaf templates materialized:

- `VSCAN.MIN`, `VSCAN.MAX`
- `VSUB.SAT`, `VMUL.SAT`, `VSLL.SAT`, `VSRL.SAT`, `VSRA.SAT`
- `VAVG`, `VAVG.R`, `VCLIP`

## Evidence Statement

Each leaf template exposes mnemonic/operand/evidence metadata and sets:

- `RequiresVectorLegalityMatrixClosure = true`
- `RequiresRetireStagedPublication = true`
- `IsExecutable = false`
- `CompilerHelperAllowed = false`

Prefix-scan rows additionally set:

- `RequiresPrefixScanPolicyAbi = true`
- `RequiresElementTypeSideband = true`
- `RequiresTailPolicyAbi = true`
- `SeparateFromClosedVscanSum = true`
- `RequiresReplayDeterminism = true`

Saturating subtract/multiply/shift rows additionally set:

- `RequiresSaturatingPolicyAbi = true`
- `RequiresElementWidthAbi = true`
- `RequiresSignednessAbi = true`
- `RequiresClampPolicyAbi = true`
- `RequiresSaturatingShiftMeaningDecision = true` for shift forms
- `MayRemainReservedIfNonMeaningful = true` for right-shift saturating forms

Average and clip rows additionally set:

- `RequiresAveragePolicyAbi = true` for `VAVG` and `VAVG.R`
- `RequiresRoundingTruncationPolicyAbi = true` for average rows
- `RequiresRoundingPolicyAbi = true` for `VAVG.R`
- `RequiresClipBoundsAbi = true`, `RequiresNarrowingPolicyAbi = true`, and `RequiresResultWidthAbi = true` for `VCLIP`

No Iteration 10A row allocates:

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

- `VSCAN.MIN`/`VSCAN.MAX`: inclusive/exclusive prefix policy, element type, tail policy, and replay determinism remain open and separate from closed `VSCAN.SUM`.
- `VSUB.SAT`/`VMUL.SAT`: signedness, element width, clamp behavior, and staged publication remain open; `VSUB.SAT` remains separate from closed `VADD.SAT`.
- `VSLL.SAT`/`VSRL.SAT`/`VSRA.SAT`: saturating-shift meaning remains open; right-shift forms may remain reserved if the policy is non-meaningful.
- `VAVG`/`VAVG.R`: rounding/truncation and signedness policy remain open.
- `VCLIP`: clip bounds encoding, signedness, result width, and narrowing policy remain open.

## Verification

- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`
- targeted `dotnet test` for `NonVmxIteration04BDeferredTemplateSurfaceTests` plus related Non-VMX catalog/no-emission parity tests.
