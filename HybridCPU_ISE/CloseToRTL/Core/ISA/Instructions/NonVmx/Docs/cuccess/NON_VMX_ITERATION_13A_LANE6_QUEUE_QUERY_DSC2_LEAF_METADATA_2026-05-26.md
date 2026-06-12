# Non-VMX Iteration 13A - Lane6 Queue/Query/DSC2 Leaf Metadata

Date: 2026-05-26

Scope: `HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx/`.

This pass materializes Lane6 queue lifecycle, read-only query, and DSC2 parser-only carrier metadata into their per-instruction leaf partial files. It does not add execution, compiler helpers, numeric scalar opcodes, decoder acceptance, registry materializers, runtime admission, retire side effects, or golden execution claims.

## Closed As Metadata-Only

- `DSC_POLL`
- `DSC_WAIT`
- `DSC_CANCEL`
- `DSC_FENCE`
- `DSC_COMMIT`
- `DSC_QUERY_BACKEND`
- `DSC_QUERY_SHAPE`
- `DSC2`

Each leaf now carries local constants for:

- `Mnemonic`
- `OperandShape`
- `ParameterDescriptor`
- `MicroOpShape`
- `ExecutionLaneBinding`
- `EvidenceBoundary`
- Lane6 ownership markers
- decoder/encoder, IR projection, registry/materializer, retire publication or side-effect, replay, rollback, and bounded-result requirements
- scalar-opcode and compiler-helper exclusion
- host-evidence leak prevention
- future virtualization-boundary policy for Lane6/DMA/external-backend authority

## VMX Boundary

These rows are Non-VMX Lane6 metadata anchors. VMX is not a point of integration for this pass.

- `NoVmxFrontendIntegrationRequired = true`
- `RequiresImmediateVmxProjection = false`
- `RequiresFutureVirtualizationBoundaryPolicy = true`

If any Lane6 queue, query, or DSC2 row later becomes executable, it must close a generic HybridCPU legality/runtime/retire/replay policy first and then provide a VMX-compatible virtualization-boundary policy. This pass adds no VMX frontend, VMCS manager, VmxCaps, VM-exit, or VMX-specific handler.

## Still Open

- control opcode or descriptor-command allocation
- decoder/encoder ABI compatibility
- `InstructionIR` projection
- registry/materializer
- typed MicroOp publication and lane binding
- token/queue/capability authority
- execute/capture semantics
- retire-owned publication or side-effect commit
- replay/rollback/conformance tests
- DSC2 descriptor-v2 ADR and parser manifest
- golden artifacts

## Verification

Expected focused verification:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore
dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~NonVmxIteration04BDeferredTemplateSurfaceTests|FullyQualifiedName~CompilerNoEmissionBoundaryTests|FullyQualifiedName~DmaStreamComputeDsc2Phase07Tests|FullyQualifiedName~DmaStreamComputeStatusPhase07Tests|FullyQualifiedName~DmaStreamComputeQueryCapsPhase07ATests|FullyQualifiedName~Phase00InstructionInventoryTests"
git diff --check -- "HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx" "HybridCPU_ISE.Tests/tests/NonVmxIteration04BDeferredTemplateSurfaceTests.cs" "Documentation/InstructionsRefactor2/OpenTasks/NON_VMX_MISSING_INSTRUCTIONS_CURRENT_SHORTLIST_2026-05-25.md"
```
