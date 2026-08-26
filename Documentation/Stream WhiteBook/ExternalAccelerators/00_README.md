# L7-SDC Stream WhiteBook

This WhiteBook is the current compact architecture record for L7-SDC under
`Documentation/Stream WhiteBook/ExternalAccelerators`. It normalizes the current
scoped executable command contour, adjacent fail-closed surfaces, and expansion
gates into a separate reading pack while keeping every statement tied to live
code, tests, or closure evidence.

Implemented behavior, helper/model-only APIs, fail-closed surfaces,
compatibility projections, conformance evidence, and future design are
separated. Future architecture is not implemented behavior.

Current instruction-side closure and risk evidence now live in
`Documentation/InstructionsRefactor/WhiteBook/`. Read that pack for the
current scalar, atomic, fence, vector, and risk-closure record; keep this pack
as the separate L7-SDC / stream-accelerator reference.

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
13. `Documentation/InstructionsRefactor/WhiteBook/00_README.md` - current instruction-side closure reference.


## Boundaries not expanded

- Fixed `W=8` topology remains lanes 0-3 ALU, lanes 4-5 LSU, lane6
  `DmaStreamClass` / `MatrixTileStreamClass` physical alias, and lane7
  `BranchControl` / `SystemSingleton` physical alias.
- L7-SDC uses lane7 `SystemSingleton` only.
- `DmaStreamCompute` remains lane6 CPU-native descriptor-backed stream compute.
- MatrixTile load/store uses a distinct lane6 tile-stream ABI. It does not use
  DSC descriptors, tokens, queues, or commit authority.
- L7-SDC uses typed sideband descriptors; raw carrier bits are not ABI or authority.
- `SystemDeviceCommandMicroOp.Execute(...)` dispatches the current scoped
  runtime contour for `ACCEL_QUERY_CAPS`, `ACCEL_SUBMIT`, `ACCEL_POLL`,
  `ACCEL_WAIT`, `ACCEL_CANCEL`, `ACCEL_FENCE`, and `ACCEL_STATUS`.
- Current carriers have `WritesRegister = DestinationRegister != 0`; retire
  writeback is emitted only when the runtime ABI result writes and the carrier
  has a destination register.
- ACCEL_* currently do not write architectural rd by opcode class alone; register
  writeback requires the scoped runtime ABI result and carrier destination.
- `ACCEL_SUBMIT` requires a typed, guard-accepted `AcceleratorCommandDescriptor`;
  descriptorless submit remains fail-closed.
- `AcceleratorRegisterAbi`, `AcceleratorFenceModel`, token store, backend,
  conflict, and commit helpers are runtime-side surfaces used by the current
  scoped contour. They are not a universal external accelerator protocol.
- AcceleratorRegisterAbi is model-only outside the scoped runtime command
  contour.
- AcceleratorFenceModel is model-only outside the scoped runtime fence contour.
- Owner/domain plus mapping/IOMMU epoch guard is the authority root.
- Mapping/IOMMU epoch validation is current model admission/commit authority
  evidence only; it is not current executable L7 IOMMU-backed memory execution.
- `DeviceComplete` is not commit.
- Staged writes publish only through `AcceleratorCommitCoordinator`.
- Direct backend writes cannot publish architectural memory.
- After an emitted `ACCEL_SUBMIT` is rejected, the L7-SDC path remains rejected.
- MatMul L7-SDC surfaces are metadata, descriptor, and staging contours only.
- Telemetry and evidence snapshots are observations and cannot authorize guard decisions.
- `ACCEL_FENCE` is executable only as the scoped runtime fence path: it observes
  guarded tokens, can call the commit coordinator under policy, and can write a
  packed status result to `rd` when the ABI says so.
- No executable ACCEL_FENCE exists outside that scoped runtime fence path.
- There is no universal external accelerator command protocol and no global CPU
  load/store conflict manager hook in the current implementation.
- No universal external accelerator command protocol.

## Expansion Gates

Current L7 after Ex1 is a scoped executable contour plus adjacent closed gates:

- Current code covers the listed SDC commands, staged
  backend completion, guarded observation/fence/commit, and register ABI
  writeback where tests prove it.
- The expansion gate still blocks any widening beyond that contour: universal command
  protocol, arbitrary external accelerator backends, coherent DMA/cache,
  IOMMU-backed memory execution, VMX guest binding, broad compiler/backend
  production lowering, and new `rd`/CSR publication forms.
- The compiler/backend gate keeps production lowering forbidden unless the
  relevant executable feature requirements are present.
- The conformance/documentation migration gate remains separate from runtime
  implementation.
- Dependency-order planning does not approve expansion beyond the current L7
  contour, lane6 DSC expansion beyond the current DSC1 materialized contour,
  DSC2 execution, IOMMU-backed execution, coherent DMA/cache, async overlap,
  successful partial completion, or production lowering.

Downstream L7 surfaces remain non-upstream evidence: fake/test backends,
capability registry, queue, fence, token store, register ABI, conflict manager,
commit coordinator, telemetry, and compiler sideband emission prove only the
specific current behaviors they exercise until the relevant expansion gates
close.
