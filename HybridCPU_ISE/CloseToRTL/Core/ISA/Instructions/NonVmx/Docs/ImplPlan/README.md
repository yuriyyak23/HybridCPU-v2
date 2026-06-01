# Non-VMX Implementation Refactor Plan Index

Date: 2026-05-27

Scope: phased production-slice playbook for Non-VMX CloseToRTL instructions,
contours, descriptor transports, and control-plane rows. A phase may move from
metadata to production when it closes the full evidence chain listed below.
Deferred text in older phase files is a guardrail against partial evidence, not
a standing blocker against implementation.

## Current Boundary

- Planning root: `CloseToRTL\Core\ISA\Instructions\NonVmx\Docs\ImplPlan`.
- Instruction root: `CloseToRTL\Core\ISA\Instructions\NonVmx`.
- Production implementation boundary: the full HybridCPU evidence chain, not
  only CloseToRTL metadata.
- A phase-local ABI/policy decision is allowed to allocate opcodes, descriptor
  op-types, decoder/encoder ABI, IR projection, materializer rows, typed
  MicroOps, execution, retire publication, replay/conformance, golden vectors,
  and no-emission/compiler tests.
- VMX is not an integration target for ordinary Non-VMX instructions. VMX sees
  them through shared legality, execution-domain, memory-domain,
  capability/completion, projection, and evidence/migration services.

## Phase Index

| Phase | File | Domain |
|---|---|---|
| 00 | `PHASE_00_CURRENT_STATE_AUDIT.md` | Inventory, status classification, aggregate metadata cleanup boundary |
| 01 | `PHASE_01_SCALAR_EXECUTION_PARTIALS.md` | Scalar executable-oriented ALU candidates |
| 01A | `PHASE_01A_CPOP_POPCNT_DECISION_GATE.md` | CPOP canonical scalar closure and POPCNT no-emission alias gate |
| 01B | `PHASE_01B_BOOLEAN_INVERT_DECISION_GATE.md` | ANDN/ORN/XNOR hardware-vs-facade and no-hidden-lowering gate |
| 01C | `PHASE_01C_SCALAR_MIN_MAX_DECISION_GATE.md` | MIN/MAX/MINU/MAXU scalar production closure and evidence boundary |
| 01D | `PHASE_01D_BYTE_BIT_REVERSE_DECISION_GATE.md` | REV8/BREV8 byte-order, bit-order, and scalar-vs-vector evidence gate |
| 01E | `PHASE_01E_SCALAR_SELECT_DECISION_GATE.md` | CSEL scalar select ABI gate and CZERO.NEZ scalar closure |
| 01F | `PHASE_01F_ZERO_COMPARE_FACADE_DECISION_GATE.md` | SEQZ/SNEZ facade-only no-emission closure |
| 02 | `PHASE_02_SCALAR_BITFIELD_AND_ROTATE_IMMEDIATE.md` | ROLI/RORI rotate-immediate closure plus register-indexed and immediate-indexed scalar bitfield closure |
| 03 | `PHASE_03_ADDRESS_GENERATION_UW.md` | Scalar address-generation `.UW` and shifted-add production closure |
| 04 | `PHASE_04_CARRYLESS_CRC_MULTIPRECISION.md` | Carry-less variants, CRC, multi-precision carry/borrow |
| 05 | `PHASE_05_VECTOR_PREDICATE_AND_SELECT.md` | VLM-gated predicate/select/scalar-result vectors |
| 06 | `PHASE_06_VECTOR_WIDEN_NARROW_CONVERT.md` | VLM-gated widen/narrow/convert vectors |
| 07 | `PHASE_07_VECTOR_SEGMENT_STRUCTURE_MEMORY.md` | VLM-gated structure movement, segment memory, 2D/indexed+2D memory |
| 08 | `PHASE_08_VECTOR_FIXED_POINT_SATURATING.md` | VLM-gated fixed-point, saturating, average, clip, prefix min/max |
| 09 | `PHASE_09_DOT_MATRIX_DEFERRAL_BOUNDARY.md` | Dot/matrix negative decision gate; advanced dot reserved and matrix/tile optional-disabled |
| 10 | `PHASE_10_LANE6_DESCRIPTOR_OPS.md` | Lane6 descriptor-owned op/shape negative decision gate; descriptor-only declared rows, no executable closure |
| 11 | `PHASE_11_LANE6_QUEUE_QUERY_DSC2.md` | Lane6 queue/query/DSC2 negative decision gate; reserved/parser-only rows, no executable closure |
| 12 | `PHASE_12_LANE7_COUNTER_HINTS.md` | Lane7 counters and hints |
| 13 | `PHASE_13_LANE7_CACHE_TLB_IOMMU.md` | Lane7 cache, translation fence, IOMMU maintenance negative decision gate; no executable closure |
| 14 | `PHASE_14_LANE7_ACCELERATOR_CONTROL.md` | Lane7 accelerator topology/lifecycle/queue controls negative decision gate; no executable closure |
| 15 | `PHASE_15_COMPATIBILITY_CONFORMANCE_SWEEP.md` | Compatibility/conformance sweep closed as audit package; no production surface opened |

## Execution Rules

Each phase must be implemented as a small PR package. Do not combine scalar execution, vector VLM opening, Lane6 descriptor authority, and Lane7 control-plane authority in one PR.

No phase may treat enum presence, mnemonic constants, parser acceptance, sideband descriptor metadata, or stub partial files as execution evidence. Execution closure requires the full chain: status/catalog, opcode or descriptor op-type, decoder/encoder ABI, `InstructionIR`/projection, registry/materializer, typed MicroOp and CloseToRTL object, execute/capture semantics, retire/writeback or side-effect publication, replay/rollback/conformance, and golden/no-emission regression where needed.

VMX rule: Non-VMX instructions integrate through the shared legality/execution/retire/projection model only. VMX-specific projection is a virtualization-boundary production gate only for privileged guest-visible effects, VMCS-visible state, VM-exit, `VmxCaps`, migration/checkpoint, host-owned evidence, DMA/Lane6/Lane7/external backend authority, or nested virtualization policy.

## Full Production Path Catalog

Every instruction file promoted to production must start with comments listing
the concrete production paths used by that row. Use the closest profile below
and add/remove phase-specific files only when the implementation truly needs
them.

### Scalar ALU / Bitmanip / Facade-Hardware Rows

- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\ISA\Instructions\NonVmx\...\<Instruction>.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\ISA\Instructions\NonVmx\...\<Instruction>.LocalSemantics.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Common\CPU_Core.Enums.cs`
- `HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\InstructionSupportStatus.cs`
- `HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\IsaV4Surface.cs`
- `HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\OpcodeInfo.Registry.Data.Scalar.cs`
- `HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\InstructionEncoder.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Decoder\VliwDecoderV4.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Pipeline\MicroOps\InstructionIR.cs`
- `HybridCPU ISE\HybridCPU_ISE\NonRTL\Core\Decoder\DecodedBundleTransportProjector.cs`
- `HybridCPU ISE\HybridCPU_ISE\NonRTL\Core\Diagnostics\InstructionRegistry.Helpers.Core.cs`
- `HybridCPU ISE\HybridCPU_ISE\NonRTL\Core\Diagnostics\InstructionRegistry.Initialize.Scalar.cs`
- `HybridCPU ISE\HybridCPU_ISE\NonRTL\Core\Pipeline\InternalOpBuilder.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Pipeline\MicroOps\InternalOp.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Pipeline\MicroOps\MicroOp.Compute.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\ALU\ScalarAluOps.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Execution\ExecutionDispatcherV4.Scalar.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Registers\Retire\RetireRecord.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Pipeline\Core\CPU_Core.PipelineExecution.Retire.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Pipeline\MicroOps\ReplayToken.cs`
- `HybridCPU ISE\HybridCPU_ISE.Tests\tests\NonVmxPhaseXX<Instruction>ExecutableTests.cs`
- `HybridCPU ISE\HybridCPU_ISE.Tests\CompilerTests\CompilerNoEmissionBoundaryTests.cs`
- Deferred guardrails in `HybridCPU ISE\HybridCPU_ISE.Tests\tests\NonVmxIteration*Deferred*Tests.cs` and adjacent fail-closed scalar tests must remove the promoted mnemonic from deferred lists.

### Vector / VLM / Vector Memory Rows

- All scalar/common catalog paths above where applicable.
- `HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\OpcodeInfo.Registry.Data.Vector.cs`
- `HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\VectorLegalityMatrix.cs`
- `HybridCPU ISE\HybridCPU_ISE\NonRTL\Core\Diagnostics\InstructionRegistry.Helpers.Vector.cs`
- `HybridCPU ISE\HybridCPU_ISE\NonRTL\Core\Diagnostics\InstructionRegistry.Initialize.Vector.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Pipeline\MicroOps\VectorMicroOps*.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Execution\ExecutionDispatcherV4*.cs`
- Vector legality, direct-factory, carrier projection, retire/replay, and golden tests under `HybridCPU ISE\HybridCPU_ISE.Tests\tests\Phase*Vector*Tests.cs`.

### Lane6 Descriptor / Queue / DSC Carrier Rows

- Phase 10 descriptor op-type and shape/range rows are currently a negative
  production decision gate. `DmaStreamCompute.SUB/MIN/MAX/ABSDIFF/CLAMP`,
  `CONVERT`, `COMPARE/SELECT`, explicit `REDUCE_*`, and `DSC_SHAPE_*` are
  descriptor-only declarations, not scalar opcodes and not executable Lane6
  runtime closure.
- Phase 11 queue lifecycle, capability query, and DSC2 carrier rows are
  currently a negative production decision gate. `DSC_POLL/WAIT/CANCEL/FENCE`
  and `DSC_COMMIT` plus `DSC_QUERY_BACKEND/SHAPE` remain reserved/no-allocation
  rows, while `DSC2` remains parser-only declared evidence; neither `DSC_STATUS`,
  `DSC_QUERY_CAPS`, generic `DmaStreamCompute`, nor Phase 10 descriptor-only
  declarations authorize execution.
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\ISA\Instructions\NonVmx\Lane06DmaStream\...`
- `HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\InstructionSupportStatus.cs`
- `HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\IsaV4Surface.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Decoder\VliwDecoderV4.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Pipeline\MicroOps\DmaStreamComputeMicroOp.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Pipeline\MicroOps\DmaStreamComputeStatusMicroOp.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Pipeline\MicroOps\DmaStreamComputeQueryCapsMicroOp.cs`
- `HybridCPU ISE\HybridCPU_ISE\NonRTL\Core\Execution\DmaStreamCompute\*.cs`
- `HybridCPU ISE\HybridCPU_ISE\NonRTL\Core\Diagnostics\InstructionRegistry.Accelerators.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Pipeline\Core\CPU_Core.PipelineExecution.Retire.cs`
- Lane6 queue/token/descriptor/conformance tests under `HybridCPU ISE\HybridCPU_ISE.Tests\tests\Phase09Lane6*Tests.cs`, `Phase09StreamEngine*Tests.cs`, and `L7Sdc*Tests.cs` when applicable.

### Lane7 System / Control-Plane Rows

- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\ISA\Instructions\NonVmx\Lane07SystemControl\...`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Common\CPU_Core.Enums.cs`
- `HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\InstructionSupportStatus.cs`
- `HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\IsaV4Surface.cs`
- `HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\OpcodeInfo.Registry.Data.System.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Decoder\VliwDecoderV4.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Execution\ExecutionDispatcherV4.System.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Execution\ExecutionDispatcherV4.CsrAndSmtVt.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Pipeline\MicroOps\MicroOp.System.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Pipeline\MicroOps\SystemDeviceCommandMicroOp.cs`
- `HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Runtime\Events\Traps\*.cs` when the row creates a typed trap/event surface.
- Lane7/counter/control-plane tests under `HybridCPU ISE\HybridCPU_ISE.Tests\tests\Lane7*Tests.cs`, `L7Sdc*Tests.cs`, and relevant conformance suites.

### Compiler And VMX Boundary

- Compiler helper emission is closed unless the phase explicitly opens helper
  authority. If opened, update compiler facade/helper ABI under
  `HybridCPU ISE\HybridCPU_Compiler\...` and
  replace no-emission assertions with explicit helper authority tests.
- Do not update VMX frontend, VMCS manager, `VmxCaps`, VM-exit, VMREAD/VMWRITE,
  or VMX-specific handlers for ordinary Non-VMX rows.
- Add VMX-compatible projection/evidence only when the row crosses a
  virtualization boundary: guest-visible privileged effect, VMCS-projected
  state, new VMX-visible capability, new VM-exit, migration/checkpoint effect,
  host-owned evidence, DMA/Lane6/Lane7/external backend authority, or nested
  virtualization policy.
