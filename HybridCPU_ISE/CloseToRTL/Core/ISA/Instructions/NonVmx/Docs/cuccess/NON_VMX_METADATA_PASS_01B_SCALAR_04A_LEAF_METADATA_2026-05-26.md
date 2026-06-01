# Non-VMX Metadata Pass 01B - Scalar 04A Leaf Metadata

Date: 2026-05-26

Scope: `HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx/`.

This pass expands the Iteration 04A scalar deferred leaf files into metadata-owner anchors. It does not add execution, compiler helpers, numeric opcodes, decoder acceptance, registry materializers, or runtime claims.

## Closed As Metadata-Only

- `CPOP`, `POPCNT`
- `ROLI`, `RORI`
- `ANDN`, `ORN`, `XNOR`
- `MIN`, `MAX`, `MINU`, `MAXU`
- `REV8`, `BREV8`
- `BSET`, `BCLR`, `BINV`, `BEXT`
- `BSETI`, `BCLRI`, `BINVI`, `BEXTI`

Each leaf now carries local constants for:

- `Mnemonic`
- `OperandShape`
- `ParameterDescriptor`
- `MicroOpShape`
- `ExecutionLaneBinding`
- `EvidenceBoundary`
- decoder/encoder, IR projection, registry/materializer, retire writeback, and replay evidence requirements
- VMX-neutral boundary markers
- row-specific ABI blockers such as canonical popcount mnemonic, rotate-immediate encoding, boolean-invert facade/hardware decision, signedness, byte/bit ordering, bitfield index masking, and canonical boolean extract results

## VMX Boundary

These rows are ordinary Non-VMX scalar metadata anchors. VMX is not a point of integration for this pass.

- `NoVmxFrontendIntegrationRequired = true`
- `RequiresVmxProjection = false`

If any row later becomes executable, VMX can observe it only through generic HybridCPU legality, execution domain, retire publication, projection, and evidence/migration policy. No VMX frontend, VMCS manager, VmxCaps, VM-exit, or VMX-specific handler was added.

## Still Open

- opcode allocation or facade-only decision
- decoder/encoder ABI compatibility
- `InstructionIR` projection
- registry/materializer
- typed MicroOp publication
- execute/capture semantics
- retire-owned writeback evidence
- replay/rollback/conformance tests
- golden artifacts

## Verification

Expected focused verification:

```powershell
dotnet build "HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore
dotnet test "HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~NonVmxIteration04AScalarDeferredTemplateTests|FullyQualifiedName~NonVmxIteration04BDeferredTemplateSurfaceTests|FullyQualifiedName~CompilerNoEmissionBoundaryTests|FullyQualifiedName~NonVmxIteration02CatalogStatusTests|FullyQualifiedName~NonVmxIteration03CRotateExecutableTests|FullyQualifiedName~OpcodeRegistryCoverageTests|FullyQualifiedName~OpcodeEnumValueParityTests"
git diff --check -- "HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx" "HybridCPU_ISE.Tests/tests/NonVmxIteration04AScalarDeferredTemplateTests.cs" "Documentation/InstructionsRefactor2/OpenTasks/NON_VMX_MISSING_INSTRUCTIONS_CURRENT_SHORTLIST_2026-05-25.md"
```
