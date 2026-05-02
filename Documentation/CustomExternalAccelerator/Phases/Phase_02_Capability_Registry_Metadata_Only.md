# Phase 02 - Capability registry metadata-only

Status: closed.

Goal:

- Introduce an external accelerator capability registry that describes
  capabilities but cannot authorize decode, execution, command submission, or
  commit.

ISE files likely touched:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Capabilities/*`
- `HybridCPU_ISE/Core/Diagnostics/InstructionRegistry.Accelerators.cs`
- `HybridCPU_ISE/Core/Diagnostics/InstructionRegistry.Types.cs`
- `HybridCPU_ISE/Core/Accelerators/MatMulAccelerator.cs`

Compiler files likely touched:

- `HybridCPU_Compiler/Core/IR/Model/*`
- `HybridCPU_Compiler/Core/IR/Hazards/HybridCpuOpcodeSemantics.cs`
- `HybridCPU_Compiler/API/Threading/HybridCpuThreadCompilerContext.cs`

New ISE types:

- `AcceleratorCapabilityRegistry`
- `AcceleratorCapabilityDescriptor`
- `AcceleratorOperationCapability`
- `AcceleratorShapeCapability`
- `AcceleratorDatatypeCapability`
- `AcceleratorAlignmentCapability`
- `AcceleratorPartialPolicyCapability`
- `AcceleratorResourceModel`
- `IAcceleratorCapabilityProvider`
- `AcceleratorCapabilityQuery`
- `AcceleratorCapabilityQueryResult`

Methods to design:

- `AcceleratorCapabilityRegistry.RegisterProvider(IAcceleratorCapabilityProvider provider)`
- `AcceleratorCapabilityRegistry.TryGetDescriptor(...)`
- `AcceleratorCapabilityRegistry.Query(AcceleratorCapabilityQuery query)`
- `IAcceleratorCapabilityProvider.GetCapabilities()`
- `AcceleratorOperationCapability.SupportsDatatype(...)`
- `AcceleratorShapeCapability.SupportsShape(...)`
- `AcceleratorResourceModel.EstimateLatencyCycles(...)`
- `AcceleratorResourceModel.EstimateQueueOccupancy(...)`

Authority rules:

- registry query success is evidence only
- capability descriptor success is evidence only
- operation support is not descriptor acceptance
- shape support is not owner/domain acceptance
- resource model success is not queue admission
- no registry method returns an executable micro-op

Tests to add:

- `L7SdcCapabilityRegistryTests`
- `L7SdcCapabilityIsNotAuthorityTests`
- compiler capability-model tests if compile-time metadata is introduced

Test cases:

- capability registration cannot decode an opcode
- capability registration cannot bypass lane7 hard pin
- capability registration cannot accept a missing descriptor
- capability registration cannot submit without owner/domain guard
- capability registration cannot commit a token
- unknown accelerator id rejects despite registry presence for another id
- unknown capability/adoption mode rejects

Must not break:

- `InstructionRegistry.RegisterAccelerator` remains legacy diagnostic metadata
- L7-SDC capability registry does not replace native opcode registration
- compiler cannot emit L7-SDC solely because legacy custom registry has an entry

Phase closure validation:

- Run focused tests:

```powershell
dotnet test --filter "L7SdcCapabilityRegistry|L7SdcCapabilityIsNotAuthority"
```

- Run affected baseline filters:

```powershell
dotnet test --filter "Phase4Extensibility|CompilerV5ContractAlignment"
```

- Run diagnostics console:

```powershell
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

- Compare with `Documentation/AsmAppTestResults.md`; capability metadata must
  not change runtime diagnostics.

Definition of done:

- capability metadata exists and is queryable
- every test proves metadata alone cannot grant authority

Rollback rule:

- disable provider registration or return empty query results if any authority
  leak appears
