# Non-VMX Metadata Pass 01A Scalar Leaf Metadata Snapshot

Date: 2026-05-26

Scope: `HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx/Lanes00_03Scalar/`.

## Closed Boundary

Metadata Pass 01A materializes scalar deferred metadata directly into the per-instruction leaf partial files that previously depended on `NonVmxScalarDeferredTemplates.cs`. This is not executable instruction closure.

Leaf templates materialized:

- `SEQZ`, `SNEZ`
- `CSEL`
- `CZERO.NEZ`
- `SH2ADD`, `SH3ADD`
- `ADD.UW`, `SH1ADD.UW`, `SH2ADD.UW`, `SH3ADD.UW`, `SLLI.UW`
- `CLMULH`, `CLMULR`
- `CRC32`, `CRC64`
- `ADC`, `SBC`, `ADDC`, `SUBC`

## Evidence Statement

Each leaf template now exposes local mnemonic, operand, parameter, MicroOp-shape, lane-binding, and evidence metadata:

- `OperandShape`
- `ParameterDescriptor`
- `MicroOpShape`
- `ExecutionLaneBinding = "Lanes00_03Scalar"`
- `RequiresDecoderEncoderAbi = true`
- `RequiresInstructionIrProjection = true`
- `RequiresRegistryMaterializer = true`
- `RequiresRetireRegisterWriteback = true`
- `RequiresReplayRollbackEvidence = true`
- `HasOpcodeAllocation = false`
- `IsExecutable = false`
- `CompilerHelperAllowed = false`

## VMX Boundary

Each row is VMX-neutral:

- `NoVmxFrontendIntegrationRequired = true`
- `RequiresVmxProjection = false`

Ordinary Non-VMX scalar rows integrate with generic ISA metadata, legality, lane binding, retire, replay, and evidence policy. VMX is not a manual integration point unless a future row explicitly crosses a virtualization boundary.

No Metadata Pass 01A row allocates:

- numeric opcode
- decoder/encoder acceptance
- `InstructionIR` projection
- registry/materializer entry
- typed MicroOp
- execute/capture semantics
- retire/writeback semantics
- compiler helper emission authority
- VMX frontend/VMCS/VmxCaps/VM-exit projection

## Verification

- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`
- targeted `dotnet test` for scalar/deferred Non-VMX metadata and no-emission boundaries.
