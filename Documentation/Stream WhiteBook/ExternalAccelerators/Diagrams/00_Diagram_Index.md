# Diagram Index

Each diagram is a Mermaid diagram with code anchors under the diagram. The
diagrams describe current fail-closed carriers, explicit model/helper surfaces,
or future-gated planning evidence; none of them is implementation approval for
executable L7, executable lane6 DSC, DSC2 execution, async overlap,
IOMMU-backed execution, coherent DMA/cache, or production compiler/backend
lowering.

| Diagram | Mechanism |
| --- | --- |
| `01_Lane_Placement_And_Carrier_Flow.md` | Compiler/decoder/projector/lane7 carrier flow. |
| `02_Descriptor_Authority_Admission.md` | Descriptor parse, capability acceptance, guard admission. |
| `03_Token_Lifecycle.md` | Token states and coordinator-owned commit transitions. |
| `04_Backend_Staging_Commit.md` | Backend read, staging, commit, rollback, invalidation. |
| `05_Conflict_Manager.md` | Active footprint reservation and overlap decisions. |
| `06_Compiler_To_ISE_Transport.md` | Compiler intent to ISE sideband transport. |
| `07_Telemetry_Evidence_Not_Authority.md` | Authority plane and evidence plane separation. |

Global code anchors:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/*`
- `HybridCPU_ISE/Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs`
- `HybridCPU_ISE/Core/Decoder/VliwDecoderV4.cs`
- `HybridCPU_Compiler/Core/IR/Model/IrAcceleratorModels.cs`
- `HybridCPU_Compiler/API/Threading/HybridCpuThreadCompilerContext.cs`
- `HybridCPU_ISE.Tests/tests/L7Sdc*.cs`

Ex1 anchors:

- `Documentation/Refactoring/Phases Ex1/10_External_Accelerator_L7_SDC_Gate.md`
- `Documentation/Refactoring/Phases Ex1/12_Testing_Conformance_And_Documentation_Migration.md`
- `Documentation/Refactoring/Phases Ex1/13_Dependency_Graph_And_Execution_Order.md`
