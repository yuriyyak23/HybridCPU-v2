# L7-SDC Stream WhiteBook

This WhiteBook is the current compact architecture record for L7-SDC under
`Documentation/Stream WhiteBook/ExternalAccelerators`. It normalizes the closed
L7 model evidence and the Ex1 Phase00-Phase13 gates into a separate reading pack
and keeps every statement tied to live code, tests, or phase closure evidence.

Current implementation, model-only APIs, fail-closed surfaces, and future design
are separated. Future architecture is not implemented behavior.

## Reading order

1. `01_L7_SDC_Executive_Summary.md`
2. `02_Topology_And_ISA_Placement.md`
3. `03_Descriptor_ABI_And_Carrier_Cleanliness.md`
4. `04_Authority_Model.md`
5. `05_Token_Lifecycle_And_Register_ABI.md`
6. `06_Backend_Staging_Commit_And_Rollback.md`
7. `07_Memory_Conflict_Model.md`
8. `08_MatMul_Capability_Provider.md`
9. `09_Compiler_Emission_Path.md`
10. `10_Telemetry_And_Evidence.md`
11. `11_DmaStreamCompute_And_Assist_Separation.md`
12. `Diagrams/00_Diagram_Index.md`


## Boundaries not expanded

- Fixed `W=8` topology remains lanes 0-3 ALU, lanes 4-5 LSU, lane6 `DmaStreamClass`, lane7 `BranchControl`/`SystemSingleton` alias.
- L7-SDC uses lane7 `SystemSingleton` only.
- `DmaStreamCompute` remains lane6 CPU-native descriptor-backed stream compute.
- L7-SDC uses typed sideband descriptors; raw carrier bits are not ABI or authority.
- `SystemDeviceCommandMicroOp.Execute(...)` throws fail-closed for every
  `ACCEL_*` carrier.
- Current `ACCEL_*` carriers have `WritesRegister = false` and empty
  architectural write metadata.
- ACCEL_* currently do not write architectural rd.
- `AcceleratorRegisterAbi`, `AcceleratorFenceModel`, queue, backend, conflict,
  and commit helpers are explicit model/runtime-side APIs, not pipeline
  instruction execution.
- AcceleratorRegisterAbi is model-only.
- AcceleratorFenceModel is model-only.
- Owner/domain plus mapping/IOMMU epoch guard is the authority root.
- Mapping/IOMMU epoch validation is current model admission/commit authority
  evidence only; it is not current executable L7 IOMMU-backed memory execution.
- `DeviceComplete` is not commit.
- Staged writes publish only through `AcceleratorCommitCoordinator`.
- Direct backend writes cannot publish architectural memory.
- After an emitted `ACCEL_SUBMIT` is rejected, the L7-SDC path remains rejected.
- Production L7-SDC paths do not call `ICustomAccelerator.Execute()`.
- MatMul L7-SDC surfaces are metadata, descriptor, and staging contours only.
- Telemetry and evidence snapshots are observations and cannot authorize guard decisions.
- There is no executable `ACCEL_FENCE`, no universal external accelerator
  command protocol, no architectural `rd` writeback, and no global CPU
  load/store conflict manager hook in the current implementation.
- No executable ACCEL_FENCE.
- No universal external accelerator command protocol.

## Ex1 Gates

Current L7 remains fail-closed/model-only after Ex1:

- Ex1 Phase10 gates executable `ACCEL_*` ISA and any `rd`/CSR publication.
- Ex1 Phase11 keeps compiler/backend production lowering forbidden unless all
  executable feature requirements are present.
- Ex1 Phase12 is the conformance/documentation migration gate.
- Ex1 Phase13 is dependency-order planning only; it does not approve executable
  L7, lane6 DSC, DSC2 execution, IOMMU-backed execution, coherent DMA/cache,
  async overlap, successful partial completion, or production lowering.

Downstream L7 surfaces remain non-upstream evidence: fake/test backends,
capability registry, queue, fence, token store, register ABI, conflict manager,
commit coordinator, telemetry, and compiler sideband emission must remain
explicitly model-only/test-only/sideband-only until the relevant gates close.
