# Phase 1 - Documentation Truth Alignment

Status date: 2026-04-29.

Status: closed for the approved refactoring scope.

Goal: make Stream WhiteBook match current CPU ISE behavior and stop promising
future behavior as implemented behavior.

## Updated Documentation Roots

- `Documentation/Stream WhiteBook/DmaStreamCompute`
- `Documentation/Stream WhiteBook/StreamEngine DmaStreamCompute`
- `Documentation/Stream WhiteBook/ExternalAccelerators`

Current docs must use these roots as current anchors. Legacy roots may appear
only as explicitly labeled stale examples or test fixtures; they must not be
used as current source links.

## Required Structure Now Present

Affected WhiteBook sections now separate:

1. current implemented contract;
2. model-only/runtime-side APIs;
3. unsupported/fail-closed behavior;
4. descriptor ABI;
5. memory, ordering, commit, and fault semantics;
6. future design;
7. code evidence links;
8. glossary/ownership notes.

## DmaStreamCompute Documentation Result

Closed files:

- `00_README.md`
- `01_Current_Contract.md`
- `02_Phase_Evidence_Ledger.md`
- `03_Validation_And_Rollback.md`
- `04_Continuation_Prompt_Phase12.md`

Current documented contract:

- `DmaStreamComputeMicroOp` is a lane6 descriptor/decode carrier.
- Direct `DmaStreamComputeMicroOp.Execute(ref Processor.CPU_Core core)` throws
  fail-closed.
- `DmaStreamComputeDescriptorParser.ExecutionEnabled` is `false`.
- `DmaStreamComputeRuntime` is an explicit runtime helper and is not canonical
  micro-op execution.
- Compiler/backend code must not infer executable ISA semantics from the
  descriptor carrier.
- DSC1 ABI is fixed to v1, 128-byte header, 16-byte range entries,
  little-endian scalar fields, `InlineContiguous`, and `AllOrNone`.
- Runtime/helper memory is physical main memory through
  `TryReadPhysicalRange` and `TryWritePhysicalRange`.
- No current async DMA overlap, virtual/IOMMU/cache-coherent runtime memory, or
  lane6 ISA pause/resume/cancel/reset/fence surface is claimed.

## StreamEngine / DSC Documentation Result

Closed files:

- `00_README.md`
- `01_StreamEngine_SFR_SRF_VectorALU.md`
- `02_DmaStreamCompute.md`
- `03_VDSA_Assist_Warming_Prefetch_SRF_DataIngress.md`

Current documented contract:

- `StreamEngine` is an in-core stream/vector execution module.
- `StreamEngine.Execute(...)` is not a DSC descriptor executor.
- `StreamEngine.CaptureRetireWindowPublications(...)` is not DSC token commit.
- `BurstIO` DMA paths drive `DMAController.ExecuteCycle()` synchronously in
  helper code.
- `BurstWriteViaDMA(...)` writes the destination surface before DMA
  bookkeeping; docs must not describe this as async architectural DMA commit.
- `DMAController` descriptor chaining remains placeholder/currently not a DSC
  execution model.

## ExternalAccelerators Documentation Result

Closed files:

- `00_README.md`
- `01_L7_SDC_Executive_Summary.md`
- `02_Topology_And_ISA_Placement.md`
- `03_Descriptor_ABI_And_Carrier_Cleanliness.md`
- `04_Authority_Model.md`
- `05_Token_Lifecycle_And_Register_ABI.md`
- `06_Backend_Staging_Commit_And_Rollback.md`
- `07_Memory_Conflict_Model.md`
- `08_MatMul_Capability_Provider.md`
- `09_Compiler_Emission_Path.md`
- `10_Telemetry_And_Evidence.md`
- `11_DmaStreamCompute_And_Assist_Separation.md`
- `Diagrams/*.md`

Current documented contract:

- `SystemDeviceCommandMicroOp` carriers hard-pin to lane7
  `SlotClass.SystemSingleton`.
- `SystemDeviceCommandMicroOp.Execute(...)` throws fail-closed.
- `ACCEL_*` carriers have `WritesRegister = false` and no architectural
  `rd` writeback.
- `AcceleratorRegisterAbi` is model-only.
- `AcceleratorFenceModel` is model-only.
- SDC1 descriptor ABI is documented as the current descriptor model.
- There is no executable `ACCEL_FENCE`.
- There is no universal external accelerator command protocol.
- There is no global CPU load/store conflict-manager hook.
- L7 faults/status are model observations/results, not current retire
  exceptions.

## Acceptance Result

- AUDIT-001 through AUDIT-012 are reflected in documentation, tests, or future
  gates.
- Mandatory WhiteBook claims are machine-checkable by
  `L7SdcDocumentationClaimSafetyTests`.
- No current compiler-facing section claims `ACCEL_* rd` writeback or
  production executable `ACCEL_SUBMIT`.
- No current section claims DSC runtime uses `StreamEngine`/`DMAController`.
- No current section claims StreamEngine DMA helper provides architectural
  async completion.
