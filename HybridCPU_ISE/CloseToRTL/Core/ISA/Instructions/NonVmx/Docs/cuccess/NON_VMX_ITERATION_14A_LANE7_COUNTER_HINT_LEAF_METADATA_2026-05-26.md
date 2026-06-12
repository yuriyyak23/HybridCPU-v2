# Non-VMX Iteration 14A - Lane7 Counter/Hint Leaf Metadata

Date: 2026-05-26

Scope: `HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx/`.

This pass materializes Lane7 counter and scheduling-hint metadata into per-instruction leaf partial files. It does not add execution, compiler helpers, numeric opcodes, decoder acceptance, registry materializers, runtime counter reads, retire writeback, or replay/golden execution claims.

## Closed As Metadata-Only

- `RDTIME`
- `RDINSTRET`
- `PAUSE`

Each leaf now carries local constants for:

- `Mnemonic`
- `OperandShape`
- `ParameterDescriptor`
- `MicroOpShape`
- `ExecutionLaneBinding`
- `EvidenceBoundary`
- decoder/encoder, IR projection, registry/materializer, retire/replay requirements
- opcode and compiler-helper exclusion
- VMX-neutral integration markers
- counter-specific replay/retire/privilege blockers
- hint-specific no-state-leak and no-progress-guarantee blockers

## VMX Boundary

These rows are ordinary Non-VMX Lane7 metadata anchors. VMX is not a point of integration for this pass.

- `NoVmxFrontendIntegrationRequired = true`
- `RequiresImmediateVmxProjection = false`

`RDTIME` and `RDINSTRET` also carry `RequiresFutureVirtualizationBoundaryPolicy = true` because executable counter reads would cross replay/privilege/virtualization policy. `PAUSE` remains a scheduling hint with no architectural state or progress guarantee.

## Still Open

- opcode or hint encoding allocation
- decoder/encoder ABI compatibility
- `InstructionIR` projection
- registry/materializer
- typed MicroOp publication and Lane7 binding
- deterministic counter source and retire accounting model
- privilege and virtualization-boundary policy
- execute/capture semantics
- retire-owned publication
- replay/rollback/conformance tests
- golden artifacts

## Verification

Expected focused verification:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore
dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~NonVmxIteration04BDeferredTemplateSurfaceTests|FullyQualifiedName~CompilerNoEmissionBoundaryTests|FullyQualifiedName~Phase03ScalarSystemCounterRdcycleExecutableTests|FullyQualifiedName~OpcodeRegistryCoverageTests|FullyQualifiedName~OpcodeEnumValueParityTests"
git diff --check -- "HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx" "HybridCPU_ISE.Tests/tests/NonVmxIteration04BDeferredTemplateSurfaceTests.cs" "Documentation/InstructionsRefactor2/OpenTasks/NON_VMX_MISSING_INSTRUCTIONS_CURRENT_SHORTLIST_2026-05-25.md"
```
