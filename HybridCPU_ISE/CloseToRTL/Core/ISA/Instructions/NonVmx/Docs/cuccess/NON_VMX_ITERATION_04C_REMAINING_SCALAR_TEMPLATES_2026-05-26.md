# Non-VMX Iteration 04C Remaining Scalar Templates Snapshot

Date: 2026-05-26

Scope: `HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx/Lanes00_03Scalar/NonVmxScalarDeferredTemplates.cs`.

## Closed Boundary

Iteration 04C closes the remaining scalar anchor-only CloseToRTL instruction classes as metadata-only no-emission templates. This is not an executable instruction closure.

Closed template rows:

- `SEQZ`, `SNEZ`
- `CSEL`
- `CZERO.NEZ`
- `SH2ADD`, `SH3ADD`
- `ADD.UW`, `SH1ADD.UW`, `SH2ADD.UW`, `SH3ADD.UW`, `SLLI.UW`
- `CLMULH`, `CLMULR`
- `CRC32`, `CRC64`
- `ADC`, `SBC`, `ADDC`, `SUBC`

## Evidence Statement

Each template exposes mnemonic/operand/evidence metadata and sets:

- `IsExecutable = false`
- `CompilerHelperAllowed = false`

No Iteration 04C row allocates:

- numeric opcode or descriptor op-type
- decoder/encoder path
- `InstructionIR` projection
- registry/materializer entry
- typed MicroOp
- execute/capture semantics
- retire/writeback side effect
- compiler helper emission authority

## Superseded Phase 01 Status

- `SEQZ`/`SNEZ`: superseded by Phase 01F facade-only/no-emission closure.
- `CSEL`: superseded by Phase 01E negative carrier decision;
  `Phase01ECarrierGateClosedNoApprovedCarrier`, no approved four-register
  carrier in Phase 01.
- `CZERO.NEZ`: superseded by Phase 01E scalar execution closure with opcode
  `333`.

The remaining rows below still keep their original no-execution blockers:

- Address-generation rows: no LSU-bypass authority; `.UW` source-width ABI remains open.
- `CLMULH`/`CLMULR`: GF(2) bit-order/high/reversed semantics remain open.
- `CRC32`/`CRC64`: polynomial/reflection/seed/final-xor/endian ABI remains open.
- `ADC`/`SBC`/`ADDC`/`SUBC`: explicit carry/borrow in/out ABI remains open; no implicit flags are exposed.

## Verification

- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~NonVmxIteration04BDeferredTemplateSurfaceTests|FullyQualifiedName~NonVmxIteration04AScalarDeferredTemplateTests|FullyQualifiedName~NonVmxIteration02CatalogStatusTests|FullyQualifiedName~NonVmxIteration03CRotateExecutableTests|FullyQualifiedName~CompilerNoEmissionBoundaryTests|FullyQualifiedName~OpcodeRegistryCoverageTests|FullyQualifiedName~OpcodeEnumValueParityTests"`

The targeted test set verifies the aggregate deferred template surface now has 135 metadata-only template partial types and that all scalar Iteration 04C templates remain non-executable with no public `Opcode` property or `Execute` method.
