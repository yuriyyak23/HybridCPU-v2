# Non-VMX CloseToRTL implementation plan

Date: 2026-05-27

Scope: `HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx/`.

This is the production-slice status ledger for the Non-VMX CloseToRTL work. The plan text alone does not close any instruction, descriptor op, contour, helper ABI, or runtime evidence claim; closure is recorded here only after source, catalog, ABI, runtime, tests, and golden/status artifacts are present.

VMX is not the integration axis for ordinary Non-VMX rows. New instructions integrate with the shared HybridCPU legality, execution, memory-domain, completion, projection, and evidence/migration model. VMX-specific work appears here only when a row crosses a virtualization boundary.

## VMX boundary rule for Non-VMX metadata

Ordinary Non-VMX instruction metadata integrates with the generic HybridCPU legality and execution substrate, not with the VMX frontend. VMX observes a Non-VMX row only through generic execution domains, memory domains, capability publication, retire/completion publication, projection services, and evidence/migration policy.

Add VMX frontend, VMCS manager, `VmxCaps`, VM-exit, VMREAD/VMWRITE, or VMX-specific handler changes for a new Non-VMX instruction only when the row explicitly crosses a virtualization boundary: guest-visible privileged effects, VMCS-projected state, new VMX-visible capability, new VM-exit type, migration/checkpoint effect, host-owned evidence, Lane6/Lane7/external backend authority, DMA/IOMMU authority, or nested-virtualization policy.

## Standard Production Path

Every promoted instruction file must start with comments listing the concrete
production paths used by that instruction. The authoritative path catalog lives
in `ImplPlan/README.md`; this top-level plan uses the same profiles.

Baseline scalar production rows update:

- `HybridCPU_ISE/Core/Common/CPU_Core.Enums.cs`;
- `HybridCPU_ISE/NonRTL/Arch/InstructionSupportStatus.cs`;
- `HybridCPU_ISE/NonRTL/Arch/IsaV4Surface.cs`;
- `HybridCPU_ISE/NonRTL/Arch/OpcodeInfo.Registry.Data.Scalar.cs`;
- `HybridCPU_ISE/NonRTL/Arch/InstructionEncoder.cs` when a new canonical encoder shape is needed;
- `HybridCPU_ISE/Core/Decoder/VliwDecoderV4.cs`;
- `HybridCPU_ISE/Core/Pipeline/MicroOps/InstructionIR.cs` and `HybridCPU_ISE/NonRTL/Core/Decoder/DecodedBundleTransportProjector.cs` when projection grows;
- `HybridCPU_ISE/NonRTL/Core/Diagnostics/InstructionRegistry.Helpers.Core.cs`;
- `HybridCPU_ISE/NonRTL/Core/Diagnostics/InstructionRegistry.Initialize.Scalar.cs`;
- `HybridCPU_ISE/NonRTL/Core/Pipeline/InternalOpBuilder.cs`;
- `HybridCPU_ISE/Core/Pipeline/MicroOps/InternalOp.cs`;
- `HybridCPU_ISE/Core/Pipeline/MicroOps/MicroOp.Compute.cs` when typed publication changes;
- `HybridCPU_ISE/Core/ALU/ScalarAluOps.cs`;
- `HybridCPU_ISE/Core/Execution/ExecutionDispatcherV4.Scalar.cs`;
- retire/replay paths such as `HybridCPU_ISE/Core/Pipeline/Core/CPU_Core.PipelineExecution.Retire.cs` and `HybridCPU_ISE/Core/Pipeline/MicroOps/ReplayToken.cs`;
- one focused executable/conformance test under `HybridCPU_ISE.Tests/tests`;
- compiler no-emission or explicit helper-authority tests under `HybridCPU_ISE.Tests/CompilerTests`.

Vector rows add `OpcodeInfo.Registry.Data.Vector.cs`,
`VectorLegalityMatrix.cs`, vector registry helpers/initializers, typed
`VectorMicroOps*.cs`, vector execution/retire paths, and VLM/carrier/golden
tests. Lane6 rows add descriptor/queue/carrier runtime, token, owner/domain,
and backend-retire evidence. Lane7 rows add system/control-plane runtime,
capability/privilege/admission, side-effect publication, replay, and
virtualization-boundary evidence when guest-visible.

## Inputs inspected

- `HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx/`: 146 `.cs` files: 141 per-instruction/contour partial files plus 5 aggregate deferred-template partial files. Initial state was 139 anchor-only `public sealed partial class` files; `ROLI`/`RORI` template anchors were added in Iteration 04A by explicit user request; vector/Lane6/Lane7 aggregate templates were added in Iteration 04B; scalar aggregate templates for the remaining anchors were added in Iteration 04C; vector predicate/select leaf metadata was materialized in Iteration 07A; vector widen/narrow/convert leaf metadata was materialized in Iteration 08A; vector segment/structure memory leaf metadata was materialized in Iteration 09A; vector fixed-point/saturating and prefix-scan leaf metadata was materialized in Iteration 10A; dot/matrix deferral leaf metadata was materialized in Iteration 11A; Lane6 descriptor op/shape leaf metadata was materialized in Iteration 12A; Lane6 queue/query/DSC2 leaf metadata was materialized in Iteration 13A; Lane7 counter/hint leaf metadata was materialized in Iteration 14A; scalar deferred leaf metadata and VMX-neutral markers were materialized in Metadata Pass 01A; scalar Iteration 04A bitcount/boolean/minmax/reverse/bitfield/rotate-immediate leaf metadata was expanded in Metadata Pass 01B; current status is tracked below.
- `HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx/NON_VMX_MISSING_INSTRUCTIONS_CURRENT_SHORTLIST_2026-05-25.md`: current authoritative Non-VMX report point.
- `Documentation/InstructionsRefactor2/OpenTasks/NON_VMX_MISSING_INSTRUCTIONS_AUDIT_2026-05-23.md`: intentionally removed as obsolete; do not treat its absence as an input blocker.
- `HybridCPU_ISE/NonRTL/Arch/InstructionSupportStatus.cs`: status/evidence surface; many shortlist rows are reserved/no allocation, optional-disabled, parser-only, or absent.
- `HybridCPU_ISE/NonRTL/Arch/IsaV4Surface.cs`: frozen ISA v4 surface, prohibited opcode policy, optional/parser-only/reserved sets.
- `HybridCPU_ISE/NonRTL/Arch/VectorLegalityMatrix.cs`: current runtime-owned vector contour gate. It explicitly keeps several related contours fail-closed even when a base vector opcode is executable.
- `HybridCPU_ISE.Tests/CompilerTests/CompilerNoEmissionBoundaryTests.cs`: no-emission guard for closed helpers, runtime-only contours, Lane6/Lane7 control-plane, cache/TLB/matrix, and VMX.
- Existing tests in `HybridCPU_ISE.Tests/tests`: Phase00 inventory, scalar optional closures, vector contour closure tests, DSC2 parser-only tests, Lane7 topology/queue taxonomy tests, stream parity tests.

## Evidence closure rule

An instruction is not implemented because it has an enum value, mnemonic, parser acceptance, opcode metadata, descriptor sideband, or a CloseToRTL stub file. Closure requires this complete chain:

```text
status/catalog
  -> opcode value or descriptor op-type
  -> decoder/encoder ABI
  -> InstructionIR/projection
  -> registry/materializer
  -> typed MicroOp / CloseToRTL instruction object
  -> execute/capture semantics
  -> retire/writeback/side-effect publication
  -> replay/rollback/conformance tests
  -> golden artifacts and no-emission regression where needed
```

## Dependency legend used in inventory

- `CAT`: `InstructionSupportStatusCatalog` and, where needed, `IsaV4Surface`.
- `OP`: numeric opcode or non-numeric descriptor op-type allocation.
- `DEC`: classifier, decoder, encoder, and ABI compatibility story.
- `IR`: `InstructionIR` / projection.
- `MAT`: registry/factory/materializer.
- `OBJ`: CloseToRTL typed instruction object beyond the anchor.
- `UOP`: typed MicroOp publication and lane binding.
- `EXE`: execute/capture semantics.
- `RET`: retire/writeback or retire-owned side-effect publication.
- `RPL`: replay/rollback determinism.
- `TST`: conformance and regression tests.
- `GLD`: golden artifacts.
- `NOE`: no-emission / compiler-boundary tests.
- `VLM`: `VectorLegalityMatrix` row or contour update.
- `DSC`: Lane6 descriptor/runtime/token/queue/commit surface.
- `L7`: Lane7 system/control-plane runtime.

## Inventory status summary

- Non-VMX instruction/contour partial classes inspected: 141.
- Missing stubs against current shortlist: 0.
- Extra Non-VMX instruction stubs against the 2026-05-25 shortlist: 2 user-requested rotate-immediate anchors, `ROLI` and `RORI`; both are now Phase 02 executable scalar rows with opcodes `335..336`. Phase 02 also promotes shortlist bitfield rows `BSET`/`BCLR`/`BINV`/`BEXT` with opcodes `337..340` and `BSETI`/`BCLRI`/`BINVI`/`BEXTI` with opcodes `341..344`.
- Current status: 0 instruction/contour partial types remain without either runtime closure or metadata-only boundary; per-instruction leaf files may still be anchor-only where an aggregate partial carries the boundary metadata. Deferred/no-emission template partial classes are closed as Iteration 04A/04B/04C/07A/08A/09A/10A/11A/12A/13A/14A plus Metadata Pass 01A/01B metadata-only boundaries. `CTZ` is closed as Iteration 03A; `SEXT.B`, `SEXT.H`, `ZEXT.H` are closed as Iteration 03B; `ROL`, `ROR` are closed as Iteration 03C; `ANDN`, `ORN`, `XNOR` are closed as Phase 01B; `MIN`, `MAX`, `MINU`, `MAXU` are closed as Phase 01C; `REV8`, `BREV8` are closed as Phase 01D; `CZERO.NEZ` is closed as a Phase 01E typed scalar ALU instruction object; `CPOP` is closed as a Phase 01A canonical typed scalar ALU instruction object; `SEQZ`/`SNEZ` plus `POPCNT` are closed as no-emission facade/alias decisions; `ROLI`/`RORI` are closed as Phase 02 rotate-immediate typed scalar ALU rows; and `BSET`/`BCLR`/`BINV`/`BEXT` plus `BSETI`/`BCLRI`/`BINVI`/`BEXTI` are closed as Phase 02 scalar bitfield typed scalar ALU rows.
- Main conflict class: several anchors represent descriptor-owned or deferred contours. Those must not become ordinary scalar opcodes.

## Inventory: scalar lanes 0-3

Iteration 04C added aggregate partial templates for the remaining scalar rows in this section. Metadata Pass 01A materialized those remaining scalar aggregate boundaries directly into the 19 per-instruction leaf files with operand, parameter, MicroOp-shape, generic lane-binding, retire/replay, and VMX-neutral markers. Metadata Pass 01B expanded the 21 Iteration 04A scalar bitcount, boolean-invert, min/max, byte/bit reverse, bitfield, and rotate-immediate leaf files with the same metadata-owner model. Each remaining deferred partial type remains non-executable until ABI/opcode/runtime evidence closes.

| File | Instruction/family | Current status | Classification | Needed dependencies |
|---|---|---|---|---|
| `Lanes00_03Scalar/FacadeCandidates/ZeroCompare/SeqzInstruction.cs` | `SEQZ` | Closed as Phase 01F facade-only/no-emission | facade-only zero-compare; no hidden lowering | closed no-emission decision: `CAT, OBJ template, NOE`; no `OP, DEC, IR, MAT, UOP, EXE, RET, RPL` unless a future hardware package opens |
| `Lanes00_03Scalar/FacadeCandidates/ZeroCompare/SnezInstruction.cs` | `SNEZ` | Closed as Phase 01F facade-only/no-emission | facade-only zero-compare; no hidden lowering | closed no-emission decision: `CAT, OBJ template, NOE`; no `OP, DEC, IR, MAT, UOP, EXE, RET, RPL` unless a future hardware package opens |
| `Lanes00_03Scalar/ConditionalSelect/CselInstruction.cs` | `CSEL` | Closed as Phase 01E negative carrier decision; not executable | scalar hardware candidate, but current ABI has no approved 4-source carrier | closed no-emission carrier gate: `CAT, OBJ template, NOE`; no `OP, DEC, IR, MAT, UOP, EXE, RET, RPL` unless a future scalar-select production package approves a carrier; VMX only through generic projection if needed |
| `Lanes00_03Scalar/ZeroingSelect/CzeroNezInstruction.cs` | `CZERO.NEZ` | Closed in Phase 01E | scalar hardware; pure XLEN=64 binary zeroing-select ALU, no side effects | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; opcode `333`; VMX generic only |
| `Lanes00_03Scalar/BitManipulation/BitCount/CtzInstruction.cs` | `CTZ` | Closed in Iteration 03A | scalar hardware; pure XLEN=64 unary ALU, no side effects | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE` |
| `Lanes00_03Scalar/BitManipulation/BitCount/CpopInstruction.cs` | `CPOP` | Closed in Phase 01A closure | canonical scalar hardware popcount; pure XLEN=64 unary ALU, no side effects | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; opcode `334`; VMX generic only |
| `Lanes00_03Scalar/BitManipulation/BitCount/PopcntInstruction.cs` | `POPCNT` | Closed as Phase 01A no-emission alias boundary | alias/facade only; no runtime opcode | closed no-emission alias: `CAT, OBJ template, NOE`; no parser/runtime/helper authority |
| `Lanes00_03Scalar/BitManipulation/Rotates/RolInstruction.cs` | `ROL` | Closed in Iteration 03C | scalar hardware; pure XLEN=64 binary rotate, no side effects | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE` |
| `Lanes00_03Scalar/BitManipulation/Rotates/RorInstruction.cs` | `ROR` | Closed in Iteration 03C | scalar hardware; pure XLEN=64 binary rotate, no side effects | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE` |
| `Lanes00_03Scalar/BitManipulation/Rotates/RoliInstruction.cs` | `ROLI` | Closed in Phase 02 | scalar hardware; pure XLEN=64 rotate-left-immediate, unsigned imm6, no side effects | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; opcode `335`; VMX generic only |
| `Lanes00_03Scalar/BitManipulation/Rotates/RoriInstruction.cs` | `RORI` | Closed in Phase 02 | scalar hardware; pure XLEN=64 rotate-right-immediate, unsigned imm6, no side effects | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; opcode `336`; VMX generic only |
| `Lanes00_03Scalar/BitManipulation/BooleanInvert/AndnInstruction.cs` | `ANDN` | Closed in Phase 01B | scalar hardware; pure XLEN=64 binary boolean-invert, no side effects | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; VMX generic only |
| `Lanes00_03Scalar/BitManipulation/BooleanInvert/OrnInstruction.cs` | `ORN` | Closed in Phase 01B | scalar hardware; pure XLEN=64 binary boolean-invert, no side effects | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; VMX generic only |
| `Lanes00_03Scalar/BitManipulation/BooleanInvert/XnorInstruction.cs` | `XNOR` | Closed in Phase 01B | scalar hardware; pure XLEN=64 binary boolean-invert, no side effects | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; VMX generic only |
| `Lanes00_03Scalar/ScalarMinMax/MinInstruction.cs` | `MIN` | Closed in Phase 01C | scalar hardware; signed XLEN=64 min, separate from vector/AMO/Lane6 min | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; VMX generic only |
| `Lanes00_03Scalar/ScalarMinMax/MaxInstruction.cs` | `MAX` | Closed in Phase 01C | scalar hardware; signed XLEN=64 max, separate from vector/AMO/Lane6 max | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; VMX generic only |
| `Lanes00_03Scalar/ScalarMinMax/MinuInstruction.cs` | `MINU` | Closed in Phase 01C | scalar hardware; unsigned XLEN=64 min, separate from vector/AMO/Lane6 min | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; VMX generic only |
| `Lanes00_03Scalar/ScalarMinMax/MaxuInstruction.cs` | `MAXU` | Closed in Phase 01C | scalar hardware; unsigned XLEN=64 max, separate from vector/AMO/Lane6 max | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; VMX generic only |
| `Lanes00_03Scalar/BitManipulation/ByteBitReverse/Rev8Instruction.cs` | `REV8` | Closed in Phase 01D | scalar hardware; unary XLEN=64 byte-order reverse, separate from vector `VBREV8` evidence | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; VMX generic only |
| `Lanes00_03Scalar/BitManipulation/ByteBitReverse/Brev8Instruction.cs` | `BREV8` | Closed in Phase 01D | scalar hardware; unary XLEN=64 bit reverse within each byte, separate from vector `VBREV8` evidence | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; VMX generic only |
| `Lanes00_03Scalar/Extension/SextBInstruction.cs` | `SEXT.B` | Closed in Iteration 03B | scalar hardware; pure XLEN=64 unary sign-extension, no side effects | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE` |
| `Lanes00_03Scalar/Extension/SextHInstruction.cs` | `SEXT.H` | Closed in Iteration 03B | scalar hardware; pure XLEN=64 unary sign-extension, no side effects | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE` |
| `Lanes00_03Scalar/Extension/ZextHInstruction.cs` | `ZEXT.H` | Closed in Iteration 03B | scalar hardware; pure XLEN=64 unary zero-extension, no side effects | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE` |
| `Lanes00_03Scalar/BitManipulation/BitSetClearInvert/BsetInstruction.cs` | `BSET` | Closed in Phase 02 | scalar hardware; register-indexed bit set with `rs2 & 0x3F`, no side effects | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; opcode `337`; VMX generic only |
| `Lanes00_03Scalar/BitManipulation/BitSetClearInvert/BclrInstruction.cs` | `BCLR` | Closed in Phase 02 | scalar hardware; register-indexed bit clear with `rs2 & 0x3F`, no side effects | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; opcode `338`; VMX generic only |
| `Lanes00_03Scalar/BitManipulation/BitSetClearInvert/BinvInstruction.cs` | `BINV` | Closed in Phase 02 | scalar hardware; register-indexed bit invert with `rs2 & 0x3F`, no side effects | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; opcode `339`; VMX generic only |
| `Lanes00_03Scalar/BitManipulation/BitExtract/BextInstruction.cs` | `BEXT` | Closed in Phase 02 | scalar hardware; register-indexed bit extract with canonical 0/1 result, no side effects | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; opcode `340`; VMX generic only |
| `Lanes00_03Scalar/BitManipulation/BitSetClearInvert/BsetiInstruction.cs` | `BSETI` | Closed in Phase 02 | scalar hardware; immediate-indexed bit set with unsigned `imm6`, no side effects | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; opcode `341`; VMX generic only |
| `Lanes00_03Scalar/BitManipulation/BitSetClearInvert/BclriInstruction.cs` | `BCLRI` | Closed in Phase 02 | scalar hardware; immediate-indexed bit clear with unsigned `imm6`, no side effects | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; opcode `342`; VMX generic only |
| `Lanes00_03Scalar/BitManipulation/BitSetClearInvert/BinviInstruction.cs` | `BINVI` | Closed in Phase 02 | scalar hardware; immediate-indexed bit invert with unsigned `imm6`, no side effects | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; opcode `343`; VMX generic only |
| `Lanes00_03Scalar/BitManipulation/BitExtract/BextiInstruction.cs` | `BEXTI` | Closed in Phase 02 | scalar hardware; immediate-indexed bit extract with canonical 0/1 result, no side effects | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; opcode `344`; VMX generic only |
| `Lanes00_03Scalar/AddressGeneration/Sh2addInstruction.cs` | `SH2ADD` | Closed in Phase 03 | scalar address-generation ALU; `(rs1 << 2) + rs2`, no LSU bypass | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; opcode `345`; VMX generic only |
| `Lanes00_03Scalar/AddressGeneration/Sh3addInstruction.cs` | `SH3ADD` | Closed in Phase 03 | scalar address-generation ALU; `(rs1 << 3) + rs2`, no LSU bypass | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; opcode `346`; VMX generic only |
| `Lanes00_03Scalar/AddressGeneration/AddUwInstruction.cs` | `ADD.UW` | Closed in Phase 03 | `.UW` source-width ABI: zero-extend low 32 bits of `rs1`, add full-width `rs2`; no LSU bypass | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; opcode `347`; VMX generic only |
| `Lanes00_03Scalar/AddressGeneration/Sh1addUwInstruction.cs` | `SH1ADD.UW` | Closed in Phase 03 | `.UW` source-width ABI: `(zext32(rs1) << 1) + rs2`; distinct from closed full-width `SH1ADD`; no LSU bypass | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; opcode `348`; VMX generic only |
| `Lanes00_03Scalar/AddressGeneration/Sh2addUwInstruction.cs` | `SH2ADD.UW` | Closed in Phase 03 | `.UW` source-width ABI: `(zext32(rs1) << 2) + rs2`; no LSU bypass | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; opcode `349`; VMX generic only |
| `Lanes00_03Scalar/AddressGeneration/Sh3addUwInstruction.cs` | `SH3ADD.UW` | Closed in Phase 03 | `.UW` source-width ABI: `(zext32(rs1) << 3) + rs2`; no LSU bypass | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; opcode `350`; VMX generic only |
| `Lanes00_03Scalar/AddressGeneration/SlliUwInstruction.cs` | `SLLI.UW` | Closed in Phase 03 | `.UW` source-width ABI: `zext32(rs1) << imm6`; imm6 range rejection; no LSU bypass | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; opcode `351`; VMX generic only |
| `Lanes00_03Scalar/CarrylessMultiply/ClmulhInstruction.cs` | `CLMULH` | Closed in Phase 04 | scalar carry-less high window; LSB-first GF(2) product bits `[127:64]`; no side effects | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; opcode `352`; VMX generic only |
| `Lanes00_03Scalar/CarrylessMultiply/ClmulrInstruction.cs` | `CLMULR` | Closed in Phase 04 | scalar carry-less reversed/high window; LSB-first GF(2) product bits `[126:63]`; no side effects | closed: `CAT, OP, DEC, IR, MAT, OBJ, UOP, EXE, RET, RPL, TST, GLD, NOE`; opcode `353`; VMX generic only |
| `Lanes00_03Scalar/CRC/Crc32Instruction.cs` | `CRC32` | ABI gate closed negative in Phase 04; not executable | scalar CRC candidate; no unique polynomial/reflection/seed/final-xor/endian policy selected | reserved/no-allocation: `CAT, OBJ template, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/GLD`; VMX generic only |
| `Lanes00_03Scalar/CRC/Crc64Instruction.cs` | `CRC64` | ABI gate closed negative in Phase 04; not executable | scalar CRC candidate; no unique polynomial/reflection/seed/final-xor/endian policy selected | reserved/no-allocation: `CAT, OBJ template, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/GLD`; VMX generic only |
| `Lanes00_03Scalar/MultiPrecision/AdcInstruction.cs` | `ADC` | ABI gate closed negative in Phase 04; not executable | needs explicit carry-in/out carrier and retire-owned carry-out publication; no implicit flags | reserved/no-allocation: `CAT, OBJ template, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/GLD`; VMX generic only |
| `Lanes00_03Scalar/MultiPrecision/SbcInstruction.cs` | `SBC` | ABI gate closed negative in Phase 04; not executable | needs explicit borrow-in/out carrier and retire-owned borrow-out publication; no implicit flags | reserved/no-allocation: `CAT, OBJ template, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/GLD`; VMX generic only |
| `Lanes00_03Scalar/MultiPrecision/AddcInstruction.cs` | `ADDC` | ABI gate closed negative in Phase 04; not executable | needs explicit retire-owned carry-out publication; no implicit flags | reserved/no-allocation: `CAT, OBJ template, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/GLD`; VMX generic only |
| `Lanes00_03Scalar/MultiPrecision/SubcInstruction.cs` | `SUBC` | ABI gate closed negative in Phase 04; not executable | needs explicit retire-owned borrow-out publication; no implicit flags | reserved/no-allocation: `CAT, OBJ template, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/GLD`; VMX generic only |

## Inventory: vector lanes 0-3 and 4-5

Iteration 04B added aggregate partial templates for every vector/memory row in this section. Iteration 07A materialized the predicate/select subset directly in its per-instruction leaf files. Iteration 08A materialized the widen/narrow/convert subset directly in its per-instruction leaf files. Iteration 09A materialized the segment/structure memory subset directly in its per-instruction leaf files. Iteration 10A materialized the fixed-point/saturating and prefix-scan subset directly in its per-instruction leaf files. Iteration 11A materialized the dot/matrix deferral subset directly in its per-instruction leaf files. Other per-instruction leaf files may still be anchor-only, but each partial type carries no-emission metadata and remains non-executable until the exact `VectorLegalityMatrix` contour and runtime evidence chain close.

| File | Instruction/family | Current status | Classification | Needed dependencies |
|---|---|---|---|---|
| `Lanes00_03Vector/PredicateMask/VmergeInstruction.cs` | `VMERGE` | Phase 05A negative decision gate; not executable | vector predicate/select; distinct mnemonic until ABI proves aliasing; polarity, mask/tail, explicit predicate sideband, element-width/LMUL/VL, and staged publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor/StreamEngine/DMA fallback, compiler helper, or VMX-specific path |
| `Lanes00_03Vector/PredicateMask/VselectInstruction.cs` | `VSELECT` | Phase 05A negative decision gate; not executable | vector predicate/select; distinct mnemonic until ABI proves aliasing; polarity, mask/tail, explicit predicate sideband, element-width/LMUL/VL, and staged publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor/StreamEngine/DMA fallback, compiler helper, or VMX-specific path |
| `Lanes00_03Vector/PredicateMask/VfirstInstruction.cs` | `VFIRST` | Phase 05B negative decision gate; not executable | vector predicate scalar-result; scalar `rd`, empty-mask sentinel, first-index width/sign, active VL/tail, and retire/replay scalar-result publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor/StreamEngine/DMA fallback, compiler helper, or VMX-specific path |
| `Lanes00_03Vector/PredicateMask/VanyInstruction.cs` | `VANY` | Phase 05B negative decision gate; not executable | vector predicate scalar-result; scalar `rd`, empty-mask policy, boolean result encoding, active VL/tail, and retire/replay scalar-result publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor/StreamEngine/DMA fallback, compiler helper, or VMX-specific path |
| `Lanes00_03Vector/PredicateMask/VallInstruction.cs` | `VALL` | Phase 05B negative decision gate; not executable | vector predicate scalar-result; scalar `rd`, empty-mask policy, boolean result encoding, active VL/tail, and retire/replay scalar-result publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor/StreamEngine/DMA fallback, compiler helper, or VMX-specific path |
| `Lanes00_03Vector/PredicateMask/VmsifInstruction.cs` | `VMSIF` | Phase 05C negative decision gate; not executable | predicate-only publication; including-first semantics, predicate-only destination representation, tail/mask policy, and staged rollback remain open; separate from closed `VMSBF` | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no vector RF exposure, descriptor/StreamEngine/DMA fallback, compiler helper, or VMX-specific path |
| `Lanes00_03Vector/PredicateMask/VmsofInstruction.cs` | `VMSOF` | Phase 05C negative decision gate; not executable | predicate-only publication; only-first semantics, predicate-only destination representation, tail/mask policy, and staged rollback remain open; separate from closed `VMSBF` | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no vector RF exposure, descriptor/StreamEngine/DMA fallback, compiler helper, or VMX-specific path |
| `Lanes00_03Vector/Widening/VwaddInstruction.cs` | `VWADD` | Phase 06A negative decision gate; not executable | widening vector add; source/destination width, signedness, LMUL/VL, overflow/result footprint, mask/tail, and staged publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/Widening/VwadduInstruction.cs` | `VWADDU` | Phase 06A negative decision gate; not executable | unsigned widening vector add; source/destination width, signedness, LMUL/VL, overflow/result footprint, mask/tail, and staged publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/Widening/VwsubInstruction.cs` | `VWSUB` | Phase 06A negative decision gate; not executable | widening vector subtract; source/destination width, signedness, LMUL/VL, overflow/result footprint, mask/tail, and staged publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/Widening/VwsubuInstruction.cs` | `VWSUBU` | Phase 06A negative decision gate; not executable | unsigned widening vector subtract; source/destination width, signedness, LMUL/VL, overflow/result footprint, mask/tail, and staged publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/Widening/VwmulInstruction.cs` | `VWMUL` | Phase 06A negative decision gate; not executable | widening vector multiply; source/destination width, signedness, LMUL/VL, product/result footprint, mask/tail, and staged publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/Widening/VwmuluInstruction.cs` | `VWMULU` | Phase 06A negative decision gate; not executable | unsigned widening vector multiply; source/destination width, signedness, LMUL/VL, product/result footprint, mask/tail, and staged publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/Widening/VwmaccInstruction.cs` | `VWMACC` | Phase 06A negative decision gate; not executable | widening vector multiply-accumulate; source/destination width, signedness, accumulator precision/footprint, LMUL/VL, mask/tail, and staged publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/Narrowing/VnsrlInstruction.cs` | `VNSRL` | Phase 06B negative decision gate; not executable | narrowing logical shift; source/destination width, shift source/immediate ABI, truncation/rounding/saturation/trap policy, mask/tail, and staged publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/Narrowing/VnsraInstruction.cs` | `VNSRA` | Phase 06B negative decision gate; not executable | narrowing arithmetic shift; source/destination width, shift source/immediate ABI, truncation/rounding/saturation/trap policy, mask/tail, and staged publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/Conversion/VsextInstruction.cs` | `VSEXT` | Phase 06C negative decision gate; not executable | vector sign-extension; separate from closed `VZEXT`; source/destination width, signedness, LMUL/VL, mask/tail, and staged publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/Conversion/VcvtIInstruction.cs` | `VCVT.I` | Phase 06C negative decision gate; not executable | conversion to signed integer; conversion policy, result footprint, NaN/invalid conversion, rounding/saturation/trap, mask/tail, and staged publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/Conversion/VcvtUInstruction.cs` | `VCVT.U` | Phase 06C negative decision gate; not executable | conversion to unsigned integer; conversion policy, result footprint, NaN/invalid conversion, rounding/saturation/trap, mask/tail, and staged publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/Conversion/VcvtFInstruction.cs` | `VCVT.F` | Phase 06C negative decision gate; not executable | conversion to float; conversion policy, floating-point result footprint, NaN/invalid conversion/FP exception, rounding/saturation/trap, mask/tail, and staged publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/PrefixScan/VscanMinInstruction.cs` | `VSCAN.MIN` | Phase 08C negative decision gate; not executable | prefix min scan; separate from closed `VSCAN.SUM`; inclusive/exclusive, element order, signedness/type, tail/mask, replay, and staged publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor fallback, helper, scalar/multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/PrefixScan/VscanMaxInstruction.cs` | `VSCAN.MAX` | Phase 08C negative decision gate; not executable | prefix max scan; separate from closed `VSCAN.SUM`; inclusive/exclusive, element order, signedness/type, tail/mask, replay, and staged publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor fallback, helper, scalar/multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/StructureMovement/VzipInstruction.cs` | `VZIP` | Phase 07A negative decision gate; not executable | structure movement; shape ABI, element order, aliasing, mask/tail, staged publication, replay, and no hidden fallback remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor/StreamEngine/DMA fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/StructureMovement/VunzipInstruction.cs` | `VUNZIP` | Phase 07A negative decision gate; not executable | structure movement; shape ABI, element order, aliasing, mask/tail, staged publication, replay, and no hidden fallback remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor/StreamEngine/DMA fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/StructureMovement/VinterleaveInstruction.cs` | `VINTERLEAVE` | Phase 07A negative decision gate; not executable | structure movement; shape ABI, element order, aliasing, mask/tail, staged publication, replay, and no hidden fallback remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor/StreamEngine/DMA fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/StructureMovement/VdeinterleaveInstruction.cs` | `VDEINTERLEAVE` | Phase 07A negative decision gate; not executable | structure movement; shape ABI, element order, aliasing, mask/tail, staged publication, replay, and no hidden fallback remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor/StreamEngine/DMA fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/SaturatingFixedPoint/VsubSatInstruction.cs` | `VSUB.SAT` | Phase 08A negative decision gate; not executable | saturating subtract; separate from closed `VADD.SAT`; signedness, width, clamp, overflow, mask/tail, and staged publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor fallback, helper, scalar/multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/SaturatingFixedPoint/VmulSatInstruction.cs` | `VMUL.SAT` | Phase 08A negative decision gate; not executable | saturating multiply; separate from closed `VADD.SAT`; product precision, signedness, width, clamp, overflow, mask/tail, and staged publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor fallback, helper, scalar/multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/SaturatingFixedPoint/VsllSatInstruction.cs` | `VSLL.SAT` | Phase 08A negative decision gate; not executable | saturating left shift; shift operand ABI and saturation meaningfulness remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor fallback, helper, scalar/multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/SaturatingFixedPoint/VsrlSatInstruction.cs` | `VSRL.SAT` | Phase 08A negative decision gate; not executable | saturating logical right shift; may remain reserved if non-meaningful; shift operand ABI and saturation policy remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor fallback, helper, scalar/multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/SaturatingFixedPoint/VsraSatInstruction.cs` | `VSRA.SAT` | Phase 08A negative decision gate; not executable | saturating arithmetic right shift; may remain reserved if non-meaningful; shift operand ABI and saturation policy remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor fallback, helper, scalar/multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/SaturatingFixedPoint/VavgInstruction.cs` | `VAVG` | Phase 08B negative decision gate; not executable | fixed-point average; signedness, width, truncation/rounding, overflow, mask/tail, and staged publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor fallback, helper, scalar/multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/SaturatingFixedPoint/VavgRInstruction.cs` | `VAVG.R` | Phase 08B negative decision gate; not executable | rounded fixed-point average; rounding mode, tie policy, signedness, width, overflow, mask/tail, and staged publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor fallback, helper, scalar/multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/SaturatingFixedPoint/VclipInstruction.cs` | `VCLIP` | Phase 08B negative decision gate; not executable | fixed-point clip/narrow; bounds encoding, result width, signedness, narrowing/truncation, mask/tail, and staged publication remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor fallback, helper, scalar/multi-op lowering, or VMX-specific path |
| `Lanes00_03Vector/DotMixedPrecision/VdotBlockscaleInstruction.cs` | `VDOT.BLOCKSCALE` | Phase 09A negative decision gate; not executable | deferred advanced dot contour; scale metadata, accumulator precision/result footprint, VLM, retire/replay, and golden evidence remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/SCH/EXE/RET/RPL/TST/GLD`; no descriptor fallback, hidden scalar/vector lowering, multi-op emission, host-owned evidence, or VMX-specific path |
| `Lanes00_03Vector/DotMixedPrecision/VdotAccumInstruction.cs` | `VDOT.ACCUM` | Phase 09A negative decision gate; not executable | deferred advanced dot contour; accumulator precision/result footprint, separate result surface, VLM, retire/replay, and golden evidence remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/SCH/EXE/RET/RPL/TST/GLD`; no descriptor fallback, hidden scalar/vector lowering, multi-op emission, host-owned evidence, or VMX-specific path |
| `Lanes00_03Vector/DotMixedPrecision/VdotWideI16Instruction.cs` | `VDOT.WIDE.I16` | Phase 09A negative decision gate; not executable | deferred beyond current scoped `VDOT.WIDE`; wider-integer contour ABI open and no name-only extension | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/SCH/EXE/RET/RPL/TST/GLD`; no descriptor fallback, hidden scalar/vector lowering, multi-op emission, host-owned evidence, or VMX-specific path |
| `Lanes00_03Vector/DotMixedPrecision/VdotWideI32Instruction.cs` | `VDOT.WIDE.I32` | Phase 09A negative decision gate; not executable | deferred beyond current scoped `VDOT.WIDE`; wider-integer contour ABI open and no name-only extension | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/SCH/EXE/RET/RPL/TST/GLD`; no descriptor fallback, hidden scalar/vector lowering, multi-op emission, host-owned evidence, or VMX-specific path |
| `Lanes00_03Vector/MatrixTile/MtileLoadInstruction.cs` | `MTILE_LOAD` | Phase 09B negative decision gate; optional-disabled, not executable | tile memory contour; descriptor ABI, memory-shape/fault model, generic runtime ownership, staged publication, and replay remain open | optional-disabled declared-only: `CAT, OBJ template, VLM, NOE`; existing enum is not execution evidence; blocked before `DEC/IR/MAT/UOP/SCH/EXE/RET/RPL/TST/GLD`; no VMX/Lane6/Lane7/external backend fallback |
| `Lanes00_03Vector/MatrixTile/MtileStoreInstruction.cs` | `MTILE_STORE` | Phase 09B negative decision gate; optional-disabled, not executable | tile memory contour; descriptor ABI, memory-shape/fault model, generic runtime ownership, staged commit, and replay remain open | optional-disabled declared-only: `CAT, OBJ template, VLM, NOE`; existing enum is not execution evidence; blocked before `DEC/IR/MAT/UOP/SCH/EXE/RET/RPL/TST/GLD`; no VMX/Lane6/Lane7/external backend fallback |
| `Lanes00_03Vector/MatrixTile/MtileMaccInstruction.cs` | `MTILE_MACC` | Phase 09B negative decision gate; optional-disabled, not executable | tile MACC contour; tile descriptor, accumulator tile/result footprint, execution model, VLM, retire/replay, and golden evidence remain open | optional-disabled declared-only: `CAT, OBJ template, VLM, NOE`; existing enum is not execution evidence; blocked before `DEC/IR/MAT/UOP/SCH/EXE/RET/RPL/TST/GLD`; no VMX/Lane6/Lane7/external backend fallback |
| `Lanes00_03Vector/MatrixTile/MtransposeInstruction.cs` | `MTRANSPOSE` | Phase 09B negative decision gate; optional-disabled, not executable | matrix transpose contour; tile descriptor, transpose policy, execution model, staged publication, and replay remain open | optional-disabled declared-only: `CAT, OBJ template, VLM, NOE`; existing enum is not execution evidence; blocked before `DEC/IR/MAT/UOP/SCH/EXE/RET/RPL/TST/GLD`; no VMX/Lane6/Lane7/external backend fallback |
| `Lanes04_05Memory/Segments/Vldseg2Instruction.cs` | `VLDSEG2` | Phase 07B negative decision gate; not executable | vector segment load; segment count, stride, alignment, byte order, mask/tail, fault granularity, staged publication, and replay remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor/StreamEngine/DMA fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes04_05Memory/Segments/Vldseg4Instruction.cs` | `VLDSEG4` | Phase 07B negative decision gate; not executable | vector segment load; segment count, stride, alignment, byte order, mask/tail, fault granularity, staged publication, and replay remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor/StreamEngine/DMA fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes04_05Memory/Segments/Vldseg8Instruction.cs` | `VLDSEG8` | Phase 07B negative decision gate; not executable | vector segment load; segment count, stride, alignment, byte order, mask/tail, fault granularity, staged publication, and replay remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor/StreamEngine/DMA fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes04_05Memory/Segments/Vstseg2Instruction.cs` | `VSTSEG2` | Phase 07B negative decision gate; not executable | vector segment store; segment count, stride, alignment, byte order, mask/tail, fault granularity, staged byte commit, and rollback remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor/StreamEngine/DMA fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes04_05Memory/Segments/Vstseg4Instruction.cs` | `VSTSEG4` | Phase 07B negative decision gate; not executable | vector segment store; segment count, stride, alignment, byte order, mask/tail, fault granularity, staged byte commit, and rollback remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor/StreamEngine/DMA fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes04_05Memory/Segments/Vstseg8Instruction.cs` | `VSTSEG8` | Phase 07B negative decision gate; not executable | vector segment store; segment count, stride, alignment, byte order, mask/tail, fault granularity, staged byte commit, and rollback remain open | reserved/no-allocation: `CAT, OBJ template, VLM, NOE`; blocked before `OP/DEC/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no descriptor/StreamEngine/DMA fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes04_05Memory/Shapes2D/Vload2DContour.cs` | `VLOAD` 2D contour | Phase 07C negative decision gate; not executable | contour evolution only; 2D rows/columns/stride/bounds/fault sideband open; closed 1D `VLOAD` is not evidence | contour-only template: `OBJ template, VLM, NOE`; blocked before `DEC sideband/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no base-opcode duplication, descriptor/StreamEngine/DMA fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes04_05Memory/Shapes2D/Vstore2DContour.cs` | `VSTORE` 2D contour | Phase 07C negative decision gate; not executable | contour evolution only; 2D rows/columns/stride/bounds/fault sideband open; closed 1D `VSTORE` is not evidence | contour-only template: `OBJ template, VLM, NOE`; blocked before `DEC sideband/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no base-opcode duplication, descriptor/StreamEngine/DMA fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes04_05Memory/Indexed2D/VgatherIndexed2DContour.cs` | `VGATHER` indexed+2D contour | Phase 07C negative decision gate; not executable | contour evolution only; index surface, 2D shape, bounds, descriptor transport, and fault order open; closed 1D indexed `VGATHER` is not evidence | contour-only template: `OBJ template, VLM, NOE`; blocked before `DEC sideband/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no base-opcode duplication, descriptor/StreamEngine/DMA fallback, helper, multi-op lowering, or VMX-specific path |
| `Lanes04_05Memory/Indexed2D/VscatterIndexed2DContour.cs` | `VSCATTER` indexed+2D contour | Phase 07C negative decision gate; not executable | contour evolution only; index surface, 2D shape, bounds, descriptor transport, and fault order open; closed 1D indexed `VSCATTER` is not evidence | contour-only template: `OBJ template, VLM, NOE`; blocked before `DEC sideband/IR/MAT/UOP/EXE/RET/RPL/TST/GLD`; no base-opcode duplication, descriptor/StreamEngine/DMA fallback, helper, multi-op lowering, or VMX-specific path |

## Inventory: Lane6 descriptor-owned expansion

Iteration 04B added aggregate partial templates for every Lane6 row in this section. Iteration 12A materialized the descriptor op-type and shape/range subset directly in the leaf partial files. Phase 10 closes that same subset as an explicit negative production decision gate: rows are descriptor-only declared metadata, not executable closure, and scoped `DmaStreamCompute` DSC1 evidence is not evidence for these op-type or shape expansions. Iteration 13A materialized the queue lifecycle, read-only query, and DSC2 parser-only carrier subset directly in the leaf partial files. These templates explicitly mark descriptor/queue/carrier ownership, no scalar opcode allocation, no execution, no compiler helper authority, no host-evidence leak, and future virtualization-boundary policy for Lane6 authority.

| File | Instruction/family | Current status | Classification | Needed dependencies |
|---|---|---|---|---|
| `Lane06DmaStream/DescriptorOps/Arithmetic/DscSubDescriptorOp.cs` | `DmaStreamCompute{op=SUB}` | Phase 10 negative decision gate; descriptor-only declared; not executable | descriptor op-type, not scalar opcode | fail-closed: `OBJ marker, CAT descriptor-only, DSC boundary, NOE`; executable still needs `DSC op enum, DEC descriptor, IR, MAT, typed UOP, lane binding, EXE, RET, RPL, TST, GLD`; no generic `DmaStreamCompute`, StreamEngine, DMAController, Lane7, external backend, helper, multi-op, or VMX-specific fallback |
| `Lane06DmaStream/DescriptorOps/Arithmetic/DscMinDescriptorOp.cs` | `DmaStreamCompute{op=MIN}` | Phase 10 negative decision gate; descriptor-only declared; not executable | descriptor op-type, signedness ABI | fail-closed: `OBJ marker, CAT descriptor-only, DSC boundary, NOE`; executable still needs `DSC op enum, DEC descriptor, IR, MAT, typed UOP, lane binding, EXE, RET, RPL, TST, GLD`; generic arithmetic evidence is insufficient |
| `Lane06DmaStream/DescriptorOps/Arithmetic/DscMaxDescriptorOp.cs` | `DmaStreamCompute{op=MAX}` | Phase 10 negative decision gate; descriptor-only declared; not executable | descriptor op-type, signedness ABI | fail-closed: `OBJ marker, CAT descriptor-only, DSC boundary, NOE`; executable still needs `DSC op enum, DEC descriptor, IR, MAT, typed UOP, lane binding, EXE, RET, RPL, TST, GLD`; generic arithmetic evidence is insufficient |
| `Lane06DmaStream/DescriptorOps/Arithmetic/DscAbsDiffDescriptorOp.cs` | `DmaStreamCompute{op=ABSDIFF}` | Phase 10 negative decision gate; descriptor-only declared; not executable | descriptor op-type, overflow policy ABI | fail-closed: `OBJ marker, CAT descriptor-only, DSC boundary, NOE`; executable still needs `DSC op enum, DEC descriptor, IR, MAT, typed UOP, lane binding, EXE, RET, RPL, TST, GLD`; generic arithmetic evidence is insufficient |
| `Lane06DmaStream/DescriptorOps/Arithmetic/DscClampDescriptorOp.cs` | `DmaStreamCompute{op=CLAMP}` | Phase 10 negative decision gate; descriptor-only declared; not executable | descriptor op-type, bounds ABI | fail-closed: `OBJ marker, CAT descriptor-only, DSC boundary, NOE`; executable still needs `DSC op enum, DEC descriptor, IR, MAT, typed UOP, lane binding, EXE, RET, RPL, TST, GLD`; no scalar/vector lowering |
| `Lane06DmaStream/DescriptorOps/TypeConversion/DscConvertDescriptorOp.cs` | `DmaStreamCompute{op=CONVERT}` | Phase 10 negative decision gate; descriptor-only declared; not executable | descriptor op-type, conversion ABI | fail-closed: `OBJ marker, CAT descriptor-only, DSC boundary, NOE`; executable still needs conversion policy ABI, `DSC op enum, DEC descriptor, IR, MAT, typed UOP, EXE, RET, RPL, TST, GLD`; no helper emission |
| `Lane06DmaStream/DescriptorOps/Predicate/DscCompareDescriptorOp.cs` | `DmaStreamCompute{op=COMPARE}` | Phase 10 negative decision gate; descriptor-only declared; not executable | descriptor op-type, predicate footprint ABI | fail-closed: `OBJ marker, CAT descriptor-only, DSC boundary, NOE`; executable still needs predicate/result footprint ABI, `DSC op enum, DEC descriptor, IR, MAT, typed UOP, EXE, RET, RPL, TST, GLD` |
| `Lane06DmaStream/DescriptorOps/Predicate/DscSelectDescriptorOp.cs` | `DmaStreamCompute{op=SELECT}` | Phase 10 negative decision gate; descriptor-only declared; not executable | descriptor op-type, predicate/select ABI | fail-closed: `OBJ marker, CAT descriptor-only, DSC boundary, NOE`; executable still needs select result footprint ABI, `DSC op enum, DEC descriptor, IR, MAT, typed UOP, EXE, RET, RPL, TST, GLD` |
| `Lane06DmaStream/DescriptorOps/Reduction/DscReduceSumDescriptorOp.cs` | `REDUCE_SUM` | Phase 10 negative decision gate; descriptor-only declared; not executable | explicit descriptor reduction | fail-closed: `OBJ marker, CAT descriptor-only, DSC boundary, NOE`; current generic `Reduce` evidence is not evidence for named reductions; executable still needs explicit op enum, result footprint, retire publication, replay, golden |
| `Lane06DmaStream/DescriptorOps/Reduction/DscReduceMinDescriptorOp.cs` | `REDUCE_MIN` | Phase 10 negative decision gate; descriptor-only declared; not executable | explicit descriptor reduction | fail-closed: `OBJ marker, CAT descriptor-only, DSC boundary, NOE`; current generic `Reduce` evidence is not evidence for named reductions; executable still needs explicit op enum, result footprint, retire publication, replay, golden |
| `Lane06DmaStream/DescriptorOps/Reduction/DscReduceMaxDescriptorOp.cs` | `REDUCE_MAX` | Phase 10 negative decision gate; descriptor-only declared; not executable | explicit descriptor reduction | fail-closed: `OBJ marker, CAT descriptor-only, DSC boundary, NOE`; current generic `Reduce` evidence is not evidence for named reductions; executable still needs explicit op enum, result footprint, retire publication, replay, golden |
| `Lane06DmaStream/DescriptorOps/Reduction/DscReduceAndDescriptorOp.cs` | `REDUCE_AND` | Phase 10 negative decision gate; descriptor-only declared; not executable | explicit descriptor reduction | fail-closed: `OBJ marker, CAT descriptor-only, DSC boundary, NOE`; current generic `Reduce` evidence is not evidence for named reductions; executable still needs explicit op enum, result footprint, retire publication, replay, golden |
| `Lane06DmaStream/DescriptorOps/Reduction/DscReduceOrDescriptorOp.cs` | `REDUCE_OR` | Phase 10 negative decision gate; descriptor-only declared; not executable | explicit descriptor reduction | fail-closed: `OBJ marker, CAT descriptor-only, DSC boundary, NOE`; current generic `Reduce` evidence is not evidence for named reductions; executable still needs explicit op enum, result footprint, retire publication, replay, golden |
| `Lane06DmaStream/DescriptorOps/Reduction/DscReduceXorDescriptorOp.cs` | `REDUCE_XOR` | Phase 10 negative decision gate; descriptor-only declared; not executable | explicit descriptor reduction | fail-closed: `OBJ marker, CAT descriptor-only, DSC boundary, NOE`; current generic `Reduce` evidence is not evidence for named reductions; executable still needs explicit op enum, result footprint, retire publication, replay, golden |
| `Lane06DmaStream/DescriptorOps/ShapeRange/DscStridedShapeContour.cs` | strided descriptor shape | Phase 10 negative decision gate; descriptor-only declared; not executable | descriptor shape evolution | fail-closed: `OBJ marker, CAT descriptor-only, DSC shape boundary, NOE`; executable still needs shape enum, parser manifest, normalized footprint, fault/alias policy, IR, MAT, typed UOP, EXE, RET, RPL, TST, GLD; no `DSC2` or backend fallback |
| `Lane06DmaStream/DescriptorOps/ShapeRange/DscTiledShapeContour.cs` | tiled descriptor shape | Phase 10 negative decision gate; descriptor-only declared; not executable | descriptor shape evolution | fail-closed: `OBJ marker, CAT descriptor-only, DSC shape boundary, NOE`; executable still needs shape enum, parser manifest, normalized footprint, fault/alias policy, IR, MAT, typed UOP, EXE, RET, RPL, TST, GLD; no `DSC2` or backend fallback |
| `Lane06DmaStream/DescriptorOps/ShapeRange/DscScatterGatherShapeContour.cs` | scatter/gather descriptor shape | Phase 10 negative decision gate; descriptor-only declared; not executable | descriptor shape evolution | fail-closed: `OBJ marker, CAT descriptor-only, DSC shape boundary, NOE`; executable still needs shape enum, parser manifest, normalized footprint, fault/alias policy, IR, MAT, typed UOP, EXE, RET, RPL, TST, GLD; no `DSC2` or backend fallback |
| `Lane06DmaStream/DescriptorOps/ShapeRange/Dsc2DShapeContour.cs` | 2D descriptor shape | Phase 10 negative decision gate; descriptor-only declared; not executable | descriptor shape evolution | fail-closed: `OBJ marker, CAT descriptor-only, DSC shape boundary, NOE`; executable still needs shape enum, parser manifest, normalized footprint, fault/alias policy, IR, MAT, typed UOP, EXE, RET, RPL, TST, GLD; no `DSC2` or backend fallback |
| `Lane06DmaStream/DescriptorOps/ShapeRange/DscMultiRangeShapeContour.cs` | multi-range descriptor shape | Phase 10 negative decision gate; descriptor-only declared; not executable | descriptor shape evolution | fail-closed: `OBJ marker, CAT descriptor-only, DSC shape boundary, NOE`; executable still needs shape enum, parser manifest, normalized footprint, fault/alias policy, IR, MAT, typed UOP, EXE, RET, RPL, TST, GLD; no `DSC2` or backend fallback |
| `Lane06DmaStream/QueueLifecycle/DscPollInstruction.cs` | `DSC_POLL` | Phase 11 negative decision gate; reserved/no-allocation; not executable | queue/control command; not scalar ALU | fail-closed: `OBJ marker, CAT reserved, DSC queue boundary, NOE`; scoped `DSC_STATUS`, `DSC_QUERY_CAPS`, generic `DmaStreamCompute`, Phase 10 descriptor-only, and `DSC2` parser evidence are insufficient; executable still needs queue command ABI, token/handle authority, DEC, IR, MAT, typed queue UOP, scheduler lane binding, EXE, RET, RPL, TST, GLD; no helper, multi-op, Lane7/external backend, or VMX-specific fallback |
| `Lane06DmaStream/QueueLifecycle/DscWaitInstruction.cs` | `DSC_WAIT` | Phase 11 negative decision gate; reserved/no-allocation; not executable | queue/control command; not scalar ALU | fail-closed: `OBJ marker, CAT reserved, DSC queue boundary, NOE`; scoped `DSC_STATUS`, `DSC_QUERY_CAPS`, generic `DmaStreamCompute`, Phase 10 descriptor-only, and `DSC2` parser evidence are insufficient; executable still needs queue command ABI, wait scope, token/handle authority, DEC, IR, MAT, typed queue UOP, scheduler lane binding, EXE, RET, RPL, TST, GLD; no helper, multi-op, Lane7/external backend, or VMX-specific fallback |
| `Lane06DmaStream/QueueLifecycle/DscCancelInstruction.cs` | `DSC_CANCEL` | Phase 11 negative decision gate; reserved/no-allocation; not executable | queue/control command; not scalar ALU | fail-closed: `OBJ marker, CAT reserved, DSC queue boundary, NOE`; scoped `DSC_STATUS`, `DSC_QUERY_CAPS`, generic `DmaStreamCompute`, Phase 10 descriptor-only, and `DSC2` parser evidence are insufficient; executable still needs queue command ABI, cancel scope, rollback journal, DEC, IR, MAT, typed queue UOP, scheduler lane binding, EXE, retire-owned side effects, RPL, TST, GLD; no helper, multi-op, Lane7/external backend, or VMX-specific fallback |
| `Lane06DmaStream/QueueLifecycle/DscFenceInstruction.cs` | `DSC_FENCE` | Phase 11 negative decision gate; reserved/no-allocation; not executable | queue/control command; not scalar ALU | fail-closed: `OBJ marker, CAT reserved, DSC queue boundary, NOE`; scoped `DSC_STATUS`, `DSC_QUERY_CAPS`, generic `DmaStreamCompute`, Phase 10 descriptor-only, and `DSC2` parser evidence are insufficient; executable still needs queue command ABI, ordering/fence scope, DEC, IR, MAT, typed queue UOP, scheduler lane binding, EXE, retire-owned side effects, RPL, TST, GLD; no helper, multi-op, Lane7/external backend, or VMX-specific fallback |
| `Lane06DmaStream/QueueLifecycle/DscCommitInstruction.cs` | `DSC_COMMIT` | Phase 11 negative decision gate; reserved/no-allocation; not executable | queue/control command; not scalar ALU | fail-closed: `OBJ marker, CAT reserved, DSC queue boundary, NOE`; scoped `DSC_STATUS`, `DSC_QUERY_CAPS`, generic `DmaStreamCompute`, Phase 10 descriptor-only, and `DSC2` parser evidence are insufficient; executable still needs queue command ABI, staged commit authority, DEC, IR, MAT, typed queue UOP, scheduler lane binding, EXE, retire-owned side effects, RPL, TST, GLD; no helper, multi-op, Lane7/external backend, or VMX-specific fallback |
| `Lane06DmaStream/Queries/DscQueryBackendInstruction.cs` | `DSC_QUERY_BACKEND` | Phase 11 negative decision gate; reserved/no-allocation; not executable | read-only capability query; no host-evidence leak | fail-closed: `OBJ marker, CAT reserved, DSC query boundary, NOE`; scoped `DSC_QUERY_CAPS` evidence is insufficient for backend query publication; executable still needs query selector ABI, bounded/scrubbed result ABI, DEC, IR, MAT, typed query UOP, scheduler lane binding, EXE, RET, RPL, TST, GLD; no host-owned evidence publication, helper, multi-op, Lane7/external backend, or VMX-specific fallback |
| `Lane06DmaStream/Queries/DscQueryShapeInstruction.cs` | `DSC_QUERY_SHAPE` | Phase 11 negative decision gate; reserved/no-allocation; not executable | read-only capability query; no host-evidence leak | fail-closed: `OBJ marker, CAT reserved, DSC query boundary, NOE`; scoped `DSC_QUERY_CAPS` evidence is insufficient for shape query publication; executable still needs shape query selector ABI, bounded/scrubbed result ABI, DEC, IR, MAT, typed query UOP, scheduler lane binding, EXE, RET, RPL, TST, GLD; no host-owned evidence publication, helper, multi-op, Lane7/external backend, or VMX-specific fallback |
| `Lane06DmaStream/CarrierV2/Dsc2DescriptorCarrier.cs` | `DSC2` | Phase 11 negative decision gate; parser-only/declared-only; not executable | parser-only/deferred carrier; do not execute | fail-closed: `OBJ marker, CAT parser-only, DSC parser-only boundary, NOE`; parser acceptance is not execution evidence; execution only after separate descriptor-v2 ADR, parser manifest, runtime admission, typed projection/materializer, retire/replay policy, golden artifacts, and generic runtime ownership; no queue runtime fallback, helper, hidden lowering, multi-op, Lane7/external backend, or VMX-specific path |

## Inventory: Lane7 Non-VMX system/control-plane

Iteration 04B added aggregate partial templates for every Lane7 row in this section. Iteration 14A materialized the counter/hint subset directly in leaf partial files. Phase 13A materialized the cache/TLB/IOMMU maintenance subset through maintenance, replay, and virtualization-boundary marker partials. Phase 14A materialized the accelerator-control subset through capability, authority, and lifecycle marker partials. These templates keep counter, hint, maintenance, IOMMU, and accelerator-control rows non-executable until retire-owned publication, replay, capability authority, and any required virtualization-boundary policy are closed.

| File | Instruction/family | Current status | Classification | Needed dependencies |
|---|---|---|---|---|
| `Lane07SystemControl/Counters/RdtimeInstruction.cs` | `RDTIME` | Leaf metadata materialized in Iteration 14A; not executable | system counter; deterministic replay blocker | closed template: `OBJ template, L7 counter boundary, NOE`; executable still needs `CAT, OP, DEC, IR, MAT, UOP, L7, RET, RPL, TST, GLD`; future virtualization-boundary policy required before execution |
| `Lane07SystemControl/Counters/RdinstretInstruction.cs` | `RDINSTRET` | Leaf metadata materialized in Iteration 14A; not executable | system counter; retire accounting blocker | closed template: `OBJ template, L7 counter boundary, NOE`; executable still needs `CAT, OP, DEC, IR, MAT, UOP, L7, RET, RPL, TST, GLD`; future virtualization-boundary policy required before execution |
| `Lane07SystemControl/Hints/PauseInstruction.cs` | `PAUSE` | Leaf metadata materialized in Iteration 14A; not executable | scheduling hint; no architectural progress guarantee | closed template: `OBJ template, L7 hint boundary, NOE`; executable still needs `CAT, OP or hint encoding, DEC, IR, MAT, UOP, L7, RET, RPL, TST, GLD`; no architectural state leakage or progress guarantee |
| `Lane07SystemControl/TranslationFences/SfenceVmaInstruction.cs` | `SFENCE.VMA` | Phase 13 negative decision gate; not executable | reserved/deferred translation fence | closed marker boundary: `OBJ template, L7 translation-fence boundary, NOE`; executable still needs `CAT, OP, DEC, IR, MAT, UOP, L7, RET, RPL, TST, GLD`; no generic fence fallback and no VMX/EPT/VPID/NPT alias |
| `Lane07SystemControl/CacheMaintenance/IcacheInvalInstruction.cs` | `ICACHE_INVAL` | Phase 13 negative decision gate; not executable | reserved/deferred cache maintenance | closed marker boundary: `OBJ template, L7 cache-maintenance boundary, NOE`; executable still needs `CAT, OP, DEC, IR, MAT, UOP, L7, RET, RPL, TST, GLD`; `FENCE_I` is not evidence |
| `Lane07SystemControl/CacheMaintenance/DcacheCleanInstruction.cs` | `DCACHE_CLEAN` | Phase 13 negative decision gate; not executable | reserved/deferred cache maintenance | closed marker boundary: `OBJ template, L7 cache-maintenance boundary, NOE`; executable still needs `CAT, OP, DEC, IR, MAT, UOP, L7, RET, RPL, TST, GLD`; no generic fence fallback |
| `Lane07SystemControl/CacheMaintenance/DcacheInvalInstruction.cs` | `DCACHE_INVAL` | Phase 13 negative decision gate; not executable | reserved/deferred cache maintenance | closed marker boundary: `OBJ template, L7 cache-maintenance boundary, NOE`; executable still needs `CAT, OP, DEC, IR, MAT, UOP, L7, RET, RPL, TST, GLD`; no generic fence fallback |
| `Lane07SystemControl/CacheMaintenance/DcacheFlushInstruction.cs` | `DCACHE_FLUSH` | Phase 13 negative decision gate; not executable | reserved/deferred cache maintenance | closed marker boundary: `OBJ template, L7 cache-maintenance boundary, NOE`; executable still needs `CAT, OP, DEC, IR, MAT, UOP, L7, RET, RPL, TST, GLD`; no generic fence fallback |
| `Lane07SystemControl/Iommu/IotlbInvInstruction.cs` | `IOTLB_INV` | Phase 13 negative decision gate; not executable | catalog row absent/implicit; non-VMX DMA-domain invalidation | closed marker boundary: `OBJ template, L7 IOMMU-maintenance boundary, NOE`; executable still needs explicit `CAT/OP/DEC/IR/MAT/UOP/L7/RET/RPL/TST/GLD`; no VMX/EPT/VPID/NPT alias |
| `Lane07SystemControl/Iommu/IommuFenceInstruction.cs` | `IOMMU_FENCE` | Phase 13 negative decision gate; not executable | catalog row absent/implicit; non-VMX DMA-domain fence | closed marker boundary: `OBJ template, L7 IOMMU-maintenance boundary, NOE`; executable still needs explicit `CAT/OP/DEC/IR/MAT/UOP/L7/RET/RPL/TST/GLD`; no VMX/EPT/VPID/NPT alias |
| `Lane07SystemControl/AcceleratorControl/Topology/AccelQueryAbiInstruction.cs` | `ACCEL_QUERY_ABI` | Phase 14 negative decision gate; not executable | topology/capability command; no compiler helper | closed marker boundary: `OBJ template, L7 accelerator-control boundary, NOE`; executable still needs explicit `CAT/OP or descriptor command/DEC/IR/MAT/UOP/L7/RET/RPL/TST/GLD`; no host-evidence leak, backend fallback, or VMX-specific path |
| `Lane07SystemControl/AcceleratorControl/Topology/AccelQueryTopologyInstruction.cs` | `ACCEL_QUERY_TOPOLOGY` | Phase 14 negative decision gate; not executable | topology/capability command; no compiler helper | closed marker boundary: `OBJ template, L7 accelerator-control boundary, NOE`; executable still needs explicit `CAT/OP or descriptor command/DEC/IR/MAT/UOP/L7/RET/RPL/TST/GLD`; no host-evidence leak, backend fallback, or VMX-specific path |
| `Lane07SystemControl/AcceleratorControl/Lifecycle/AccelOpenInstruction.cs` | `ACCEL_OPEN` | Phase 14 negative decision gate; not executable | accelerator lifecycle; capability/queue authority blocker | closed marker boundary: `OBJ template, L7 accelerator-control boundary, NOE`; executable still needs explicit `CAT/OP or descriptor command/DEC/IR/MAT/UOP/L7/RET/RPL/TST/GLD`; no lifecycle state publication before retire |
| `Lane07SystemControl/AcceleratorControl/Lifecycle/AccelCloseInstruction.cs` | `ACCEL_CLOSE` | Phase 14 negative decision gate; not executable | accelerator lifecycle; capability/queue authority blocker | closed marker boundary: `OBJ template, L7 accelerator-control boundary, NOE`; executable still needs explicit `CAT/OP or descriptor command/DEC/IR/MAT/UOP/L7/RET/RPL/TST/GLD`; no lifecycle state publication before retire |
| `Lane07SystemControl/AcceleratorControl/QueueBinding/AccelBindQueueInstruction.cs` | `ACCEL_BIND_QUEUE` | Phase 14 negative decision gate; not executable | queue binding; token/queue authority blocker | closed marker boundary: `OBJ template, L7 accelerator-control boundary, NOE`; executable still needs explicit `CAT/OP or descriptor command/DEC/IR/MAT/UOP/L7/RET/RPL/TST/GLD`; no queue binding before token/queue authority |
| `Lane07SystemControl/AcceleratorControl/QueueBinding/AccelUnbindQueueInstruction.cs` | `ACCEL_UNBIND_QUEUE` | Phase 14 negative decision gate; not executable | queue unbinding; token/queue authority blocker | closed marker boundary: `OBJ template, L7 accelerator-control boundary, NOE`; executable still needs explicit `CAT/OP or descriptor command/DEC/IR/MAT/UOP/L7/RET/RPL/TST/GLD`; no queue binding before token/queue authority |

## Reconciliation with current shortlist

- All current shortlist rows have corresponding anchors under `NonVmx/`.
- `ROLI` and `RORI` are user-requested rotate-immediate anchors added after the 2026-05-25 shortlist; Phase 02 promotes them as explicit imm6 runtime rows without duplicating the closed register-register `ROL`/`ROR` evidence.
- No remaining anchor-only or template-only `NonVmx/` file duplicates closed current runtime-owned contours such as `CZERO.EQZ`, `CZERO.NEZ`, `CLZ`, `CTZ`, `SEXT.B`, `SEXT.H`, `ZEXT.H`, `ROL`, `ROR`, `SH1ADD`, `RDCYCLE`, `CLMUL`, current base vector operations, `VGATHER`/`VSCATTER` 1D indexed contours, current DSC1 `DmaStreamCompute`, `DSC_STATUS`, `DSC_QUERY_CAPS`, or current Lane7 `ACCEL_QUERY_CAPS/SUBMIT/POLL/WAIT/CANCEL/FENCE/STATUS`.
- `VLOAD`/`VSTORE`, `VGATHER`/`VSCATTER`, and `VDOT.WIDE` anchors are contour anchors, not duplicate base-opcode anchors.
- `DSC2` remains parser-only until a descriptor-v2 execution chain closes.
- Matrix/tile anchors remain optional-disabled/deferred.

## Implementation classification

### Real implementation candidates

- Simple scalar ALU and bitmanip after opcode allocation: the remaining CRC/checksum and multiprecision rows. `CSEL` is closed as a negative Phase 01E carrier decision because the current scalar ABI has no approved four-register carrier. `ANDN`, `ORN`, `XNOR`, `MIN`, `MAX`, `MINU`, `MAXU`, `REV8`, `BREV8`, `CZERO.NEZ`, canonical `CPOP`, `ROLI`/`RORI`, register-indexed `BSET`/`BCLR`/`BINV`/`BEXT`, immediate-indexed `BSETI`/`BCLRI`/`BINVI`/`BEXTI`, Phase 03 address-generation rows, and Phase 04 `CLMULH`/`CLMULR` are already closed production rows.
- Scalar arithmetic extensions after ABI decisions: `CRC32`, `CRC64`, `ADC`, `SBC`, `ADDC`, `SUBC`.
- Vector predicate/select and vector compute/memory contours only after `VectorLegalityMatrix` is updated from fail-closed to executable for the exact contour.
- Lane7 counters/hints only with retire-owned deterministic semantics.

### Facade-only / assembler-lowering candidates

- `SEQZ`, `SNEZ`: closed as facade-only/no-emission rows; no hidden lowering or helper authority.
- `POPCNT`: closed as a no-emission alias boundary for canonical runtime `CPOP`; no parser/runtime/helper authority.
- `ANDN`, `ORN`, `XNOR`: closed as hardware rows; do not reopen them as facade-only aliases.

### Descriptor-only or control-plane contours

- Lane6 descriptor op-types and descriptor shapes are descriptor-owned. They must not become ordinary scalar instructions.
- `DSC_POLL`, `DSC_WAIT`, `DSC_CANCEL`, `DSC_FENCE`, `DSC_COMMIT`, `DSC_QUERY_BACKEND`, and `DSC_QUERY_SHAPE` require token/queue/commit authority and must not expose compiler helpers prematurely.
- Lane7 accelerator topology/lifecycle/queue commands require capability, queue, token, execution, and commit authority before execution.

### Items not to implement as ordinary opcodes

- Lane6 descriptor op-types: `SUB`, `MIN`, `MAX`, `ABSDIFF`, `CLAMP`, `CONVERT`, `COMPARE`, `SELECT`, explicit reductions, and descriptor shape expansion.
- `DSC2`: parser-only descriptor carrier until a separate descriptor-v2 execution contract closes.
- `VLOAD`/`VSTORE` 2D and indexed+2D `VGATHER`/`VSCATTER`: contour evolution, not duplicate base opcodes.
- Matrix/tile: optional-disabled until tile memory, fault, retire, and compiler no-emission model exists.

### Already closed contours not to duplicate

- Scalar: `CZERO.EQZ`, `CZERO.NEZ`, `CLZ`, `CTZ`, `CPOP`, `SEXT.B`, `SEXT.H`, `ZEXT.H`, `ROL`, `ROR`, `ROLI`, `RORI`, `BSET`, `BCLR`, `BINV`, `BEXT`, `BSETI`, `BCLRI`, `BINVI`, `BEXTI`, `ANDN`, `ORN`, `XNOR`, `MIN`, `MAX`, `MINU`, `MAXU`, `REV8`, `BREV8`, `SH1ADD`, `SH2ADD`, `SH3ADD`, `ADD.UW`, `SH1ADD.UW`, `SH2ADD.UW`, `SH3ADD.UW`, `SLLI.UW`, `RDCYCLE`, `CLMUL`, `CLMULH`, `CLMULR`, and mandatory/core repaired scalar rows.
- Vector: current executable base vector config/compute/memory/mask/reduction/movement rows, plus current advanced closures listed in the 2026-05-25 shortlist.
- Lane6: current DSC1 `DmaStreamCompute` closed subset, `DSC_STATUS`, `DSC_QUERY_CAPS`.
- Lane7: current `ACCEL_QUERY_CAPS`, `ACCEL_SUBMIT`, `ACCEL_POLL`, `ACCEL_WAIT`, `ACCEL_CANCEL`, `ACCEL_FENCE`, `ACCEL_STATUS`.

## Iteration 01: inventory and path routing

1. Goal: keep this inventory and PR sequence aligned with production implementation.
2. Files in `NonVmx/`: this Markdown plan only.
3. External dependencies: none changed; inspected `InstructionSupportStatusCatalog`, `IsaV4Surface`, `VectorLegalityMatrix`, no-emission tests, and available OpenTasks shortlist.
4. Instructions: all 139 anchors.
5. ABI / parameters: document-only unless a later phase production package closes the relevant ABI.
6. Algorithm: none.
7. Retire effects: none.
8. Replay / rollback: none.
9. Conformance tests: plan must cite future tests; no test code in this iteration.
10. Golden artifacts: none.
11. No-emission tests: unchanged; treated as planning constraints.
12. Definition of Done: plan exists, all anchors inventoried, obsolete 2026-05-23 audit absence recorded as non-blocking.
13. Do not do: do not treat this inventory alone as evidence; implementation belongs in the phase production path.

## Iteration 02: catalog/status alignment for missing or implicit rows

Status: partially closed. Iteration 02A closed catalog/status alignment for scalar bitfield `B*` rows and non-VMX `IOTLB_INV`/`IOMMU_FENCE`. Those rows may now move to production through their phase-specific full path when opcode/descriptor/control ABI is explicit.

1. Goal: align explicit status rows before executable work.
2. Files in `NonVmx/`: `BsetInstruction.cs`, `BclrInstruction.cs`, `BinvInstruction.cs`, `BextInstruction.cs`, `BsetiInstruction.cs`, `BclriInstruction.cs`, `BinviInstruction.cs`, `BextiInstruction.cs`, `IotlbInvInstruction.cs`, `IommuFenceInstruction.cs`, all Lane6 descriptor op anchors, all Lane7 accelerator topology/queue anchors. Add production path comments to any row promoted beyond metadata.
3. External dependencies: `InstructionSupportStatusCatalog`, `IsaV4Surface`, opcode registry policy, no-emission tests.
4. Instructions: bitfield `B*`, non-VMX `IOTLB_INV`/`IOMMU_FENCE`, Lane6 descriptor op-types, Lane7 topology/queue commands.
5. ABI / parameters: status-only for rows that remain deferred; production rows must close the runtime ABI in their phase package.
6. Algorithm: none.
7. Retire effects: none.
8. Replay / rollback: none.
9. Conformance tests: inventory tests must prove these rows are `Reserved`, `DescriptorOnly`, `ParserOnly`, or `OptionalDisabled` until execution work starts.
10. Golden artifacts: status snapshot showing no executable claim.
11. No-emission tests: extend `CompilerNoEmissionBoundaryTests` fragments for all new closed helper names.
12. Definition of Done: Iteration 02A is closed for `BSET`, `BCLR`, `BINV`, `BEXT`, `BSETI`, `BCLRI`, `BINVI`, `BEXTI`, `IOTLB_INV`, and `IOMMU_FENCE`; catalog can answer these rows explicitly. Phase 02 later promotes all scalar bitfield rows; maintenance rows remain non-executable until their full path closes. Wider descriptor/topology alignment remains open where rows are not yet covered by this subset.
13. Do not do: do not allocate partial opcode/descriptor authority without the matching decoder, IR, materializer, execution, retire, replay, and test evidence.

## Iteration 03: simple scalar ALU without side effects

Status: partially closed. Iteration 03A closed `CTZ`; Iteration 03B closed `SEXT.B`, `SEXT.H`, and `ZEXT.H`; Iteration 03C closed `ROL` and `ROR`; Phase 01B closed `ANDN`/`ORN`/`XNOR`; Phase 01C closed scalar `MIN`/`MAX`/`MINU`/`MAXU`; Phase 01D closed scalar `REV8`/`BREV8`; Phase 01E closed scalar `CZERO.NEZ` and closed `CSEL` as a negative carrier decision; Phase 01A closure closed canonical scalar `CPOP`; Phase 01F closed `SEQZ`/`SNEZ` as no-emission facade rows; Phase 02 closed `ROLI`/`RORI` with imm6 ABI, register-indexed `BSET`/`BCLR`/`BINV`/`BEXT` with masked index ABI, and immediate-indexed `BSETI`/`BCLRI`/`BINVI`/`BEXTI` with imm6 ABI; Phase 03 closed `SH2ADD`, `SH3ADD`, `ADD.UW`, `SH1ADD.UW`, `SH2ADD.UW`, `SH3ADD.UW`, and `SLLI.UW`; `POPCNT` is a no-emission alias boundary.

1. Goal: close a first scalar ALU batch with no memory, system, descriptor, or hidden state side effects.
2. Files in `NonVmx/`: `CselInstruction.cs`, `CzeroNezInstruction.cs`, `CtzInstruction.cs`, canonical `CpopInstruction.cs` or `PopcntInstruction.cs`, `RolInstruction.cs`, `RorInstruction.cs`, `RoliInstruction.cs`, `RoriInstruction.cs`, `AndnInstruction.cs`, `OrnInstruction.cs`, `XnorInstruction.cs`, `MinInstruction.cs`, `MaxInstruction.cs`, `MinuInstruction.cs`, `MaxuInstruction.cs`, `Rev8Instruction.cs`, `Brev8Instruction.cs`, `SextBInstruction.cs`, `SextHInstruction.cs`, `ZextHInstruction.cs`.
3. External dependencies: `InstructionSupportStatusCatalog`, `IsaV4Surface`, opcode registry, decoder/encoder, `InstructionIR`, scalar MicroOp/materializer, retire/writeback, scalar replay tests.
4. Instructions: no remaining local Phase 01 runtime candidate; `CSEL` carrier gate is closed negative. Closed references: `CTZ`; `CPOP`; `SEXT.B`, `SEXT.H`, `ZEXT.H`; `ROL`, `ROR`; `ROLI`, `RORI`; `BSET`, `BCLR`, `BINV`, `BEXT`; `BSETI`, `BCLRI`, `BINVI`, `BEXTI`; `ANDN`, `ORN`, `XNOR`; `MIN`, `MAX`, `MINU`, `MAXU`; `REV8`, `BREV8`; `CZERO.NEZ`; no-emission `SEQZ`/`SNEZ`/`POPCNT`/`CSEL`.
5. ABI / parameters: `rd, rs1` for unary, including closed `CPOP`; `rd, rs1, rs2` for binary, including closed `CZERO.NEZ`; `CSEL` must use explicit `rd, rs_true, rs_false, rs_cond` through an approved four-register carrier/sideband ABI; x0 discard must be explicit.
6. Algorithm: XLEN=64 semantics; shift counts masked to low 6 bits; CTZ zero returns 64; scalar min/max signedness explicit; `BREV8` bit ordering documented.
7. Retire effects: write `rd` only, discard x0, no branch/system side effects.
8. Replay / rollback: deterministic pure ALU result; replay must reproduce result and x0 discard.
9. Conformance tests: decode/encode, IR projection, materializer, execution edge cases, x0, signedness, zero-input, rotate wrap, reverse boundary.
10. Golden artifacts: scalar opcode/encoding manifest and execution vector cases.
11. No-emission tests: keep non-canonical aliases closed where applicable.
12. Definition of Done: closed scalar rows have `ConformanceTested` runtime evidence, numeric opcode, canonical decoder/projection, registry materializer, typed `InternalOpKind`, CloseToRTL typed object with production path comments, execute/capture semantics, retire-owned writeback, x0 discard, replay/rollback tests, golden/status artifact, and compiler no-emission regression. Remaining selected rows must individually reach that executable evidence level before being marked runtime closed.
13. Production rule: `SEQZ`/`SNEZ` are currently facade-only/no-emission; implement hardware only through a future scalar full path, and keep compiler facade lowering out of runtime evidence.

## Iteration 04: scalar bitfield register and immediate forms

Status: closed for scalar bitfield rows. Register-indexed `BSET`/`BCLR`/`BINV`/`BEXT` are closed in Phase 02 as typed scalar ALU rows with opcodes `337..340`, `rd, rs1, rs2`, `Immediate=0`, index masking, retire/replay/golden/no-emission evidence. Immediate-indexed `BSETI`/`BCLRI`/`BINVI`/`BEXTI` are closed in Phase 02 as typed scalar ALU rows with opcodes `341..344`, `rd, rs1, imm6`, `Word1=(rd, rs1, x0)`, imm6 range rejection, retire/replay/golden/no-emission evidence.

1. Goal: close bitfield register-indexed and immediate-indexed forms.
2. Files in `NonVmx/`: `BsetInstruction.cs`, `BclrInstruction.cs`, `BinvInstruction.cs`, `BextInstruction.cs`, `BsetiInstruction.cs`, `BclriInstruction.cs`, `BinviInstruction.cs`, `BextiInstruction.cs`.
3. External dependencies: explicit catalog rows, opcode registry, immediate encoding, decoder rejection tests, `InstructionIR`, bitfield MicroOps, retire/writeback.
4. Instructions: `BSET`, `BCLR`, `BINV`, `BEXT`, `BSETI`, `BCLRI`, `BINVI`, `BEXTI`.
5. ABI / parameters: register forms `rd, rs1, rs2`; immediate forms `rd, rs1, imm6`.
6. Algorithm: index masked to XLEN width for register forms; immediate must be 0..63; `BEXT` result is canonical 0 or 1.
7. Retire effects: write `rd` only, x0 discard.
8. Replay / rollback: pure deterministic ALU; no hidden flags.
9. Conformance tests: register forms cover bit positions 0, 1, 63, register index overflow, x0, replay/rollback, and `BEXT` result canonicalization; immediate forms cover imm6 edges, rs2 alias rejection, out-of-range rejection, x0, replay/rollback, and `BEXTI` result canonicalization.
10. Golden artifacts: register and immediate bitfield result tables are closed.
11. No-emission tests: no public helper for runtime-only register or immediate bitfield rows.
12. Definition of Done: register-indexed and immediate-indexed rows are executable after decoder, materializer, execution, retire, replay, and tests close.
13. Do not do: do not infer bitfield support from vector bitmanip or generic shifts.

## Iteration 05: address-generation `.UW`

Status: closed in Phase 03. `SH2ADD`, `SH3ADD`, `ADD.UW`, `SH1ADD.UW`, `SH2ADD.UW`, `SH3ADD.UW`, and `SLLI.UW` now have explicit opcodes `345..351`, runtime status/catalog evidence, decoder/encoder ABI, `InstructionIR` projection, scalar materializers, typed `InternalOpKind`s, CloseToRTL executable objects, scalar ALU/dispatcher execution, retire/writeback with x0 discard, replay/rollback, golden vectors, compiler no-emission guardrails, and generic VMX-only boundary.

1. Goal: close scalar address-generation arithmetic without granting LSU bypass.
2. Files in `NonVmx/`: `Sh2addInstruction.cs`, `Sh3addInstruction.cs`, `AddUwInstruction.cs`, `Sh1addUwInstruction.cs`, `Sh2addUwInstruction.cs`, `Sh3addUwInstruction.cs`, `SlliUwInstruction.cs`.
3. External dependencies: catalog/status, opcode allocation, decoder/encoder, IR, scalar ALU materializer, scheduler typed-slot legality, tests proving no LSU side effects.
4. Instructions: `SH2ADD`, `SH3ADD`, `ADD.UW`, `SH1ADD.UW`, `SH2ADD.UW`, `SH3ADD.UW`, `SLLI.UW`.
5. ABI / parameters: register forms `rd, rs1, rs2`, canonical `Immediate=0`; immediate `SLLI.UW rd, rs1, imm6`, canonical `Word1=(rd, rs1, x0)`, imm6 range rejection.
6. Algorithm: `SH2ADD/SH3ADD` compute `(rs1 << 2 or 3) + rs2`; `.UW` forms zero-extend low 32 bits of `rs1` before shift/add; XLEN=64 wrap.
7. Retire effects: scalar `rd` write only, x0 discard, no LSU or memory side effects.
8. Replay / rollback: deterministic pure ALU, rollback restores architectural truth.
9. Conformance tests: low/high 32-bit boundary, wrap, shift amount, x0, no memory access, lane class remains ALU, decoder alias rejection, `SLLI.UW` imm6 rejection.
10. Golden artifacts: ABI and result tables are closed in Phase 03 tests and CloseToRTL local vectors.
11. No-emission tests: compiler helpers and hidden `ZEXT.W + SH*ADD` lowering remain absent.
12. Definition of Done: closed; no legality path treats these as LSU authorization.
13. Do not do: do not reuse `ZEXT.W + SH*ADD` as hidden multi-op runtime execution unless facade-only mode is explicitly chosen.

## Iteration 06: carry-less, CRC, and multi-precision

Status: `CLMULH`/`CLMULR` are closed in Phase 04 as scalar carry-less register-register ALU rows with opcodes `352/353`, typed materialization, dispatcher capture, retire x0/writeback, replay/rollback, and golden vectors. `CRC32`/`CRC64` and `ADC`/`SBC`/`ADDC`/`SUBC` have Phase 04 negative ABI decisions and remain reserved/no-allocation/no-emission until polynomial/reflection/seed/final-xor/endian and carry/borrow publication ABIs close.

1. Goal: close scalar arithmetic extensions that need stronger ABI decisions.
2. Files in `NonVmx/`: `ClmulhInstruction.cs`, `ClmulrInstruction.cs`, `Crc32Instruction.cs`, `Crc64Instruction.cs`, `AdcInstruction.cs`, `SbcInstruction.cs`, `AddcInstruction.cs`, `SubcInstruction.cs`.
3. External dependencies: catalog/status, opcode allocation, decoder/encoder, IR, scalar MicroOps, retire/writeback, golden vectors.
4. Instructions: `CLMULH`, `CLMULR`, `CRC32`, `CRC64`, `ADC`, `SBC`, `ADDC`, `SUBC`.
5. ABI / parameters: closed `CLMULH/CLMULR rd, rs1, rs2`, `Immediate=0`; CRC proposed `rd, rs_seed, rs_data` remains non-executable because polynomial/reflection/seed/final-xor/endian policy is undecided; multi-precision remains non-executable because explicit carry/borrow input and retire-visible output publication ABI is undecided and no implicit architectural flags are allowed.
6. Algorithm: closed XLEN=64 LSB-first GF(2) multiplication high/reversed windows for `CLMULH/CLMULR`; CRC polynomial/reflection/seed/final-xor/endian behavior remains a future ABI decision; carry/borrow result convention remains a future ABI decision.
7. Retire effects: `CLMULH/CLMULR` scalar destination only with x0 discard; any explicit carry/borrow destination for future rows only; no implicit flags.
8. Replay / rollback: `CLMULH/CLMULR` deterministic arithmetic and scalar register rollback; future CRC/carry rows must not use hidden state.
9. Conformance tests: known CLMUL high/reversed vectors, CRC known-answer tests, carry/borrow edge cases.
10. Golden artifacts: closed bit-order manifest via `CLMULH/CLMULR` local vectors; future polynomial and carry ABI manifests.
11. No-emission tests: no `CLMULH`/`CLMULR` compiler helpers; no CRC or multi-precision helpers until ABI is approved.
12. Definition of Done: ABI decisions are documented before opcode execution is enabled.
13. Do not do: do not expose implicit condition flags or host helper shortcuts.

## Iteration 07: vector predicate/select

Status: leaf template boundary materialized in Iteration 07A; executable vector predicate/select work remains open and VLM-gated. Iteration 07A only moved no-emission metadata into the seven predicate/select leaf partial files and did not allocate opcodes, decoder paths, materializers, MicroOps, VLM executable contours, retire publication, or compiler helpers.

1. Goal: close predicate-mask and scalar predicate-summary vector contours.
2. Files in `NonVmx/`: `VmergeInstruction.cs`, `VselectInstruction.cs`, `VfirstInstruction.cs`, `VanyInstruction.cs`, `VallInstruction.cs`, `VmsifInstruction.cs`, `VmsofInstruction.cs`.
3. External dependencies: catalog/status, opcode allocation, decoder/encoder sideband, `VectorLegalityMatrix`, IR, vector MicroOps, predicate publication, retire, no-emission tests.
4. Instructions: `VMERGE`, `VSELECT`, `VFIRST`, `VANY`, `VALL`, `VMSIF`, `VMSOF`.
5. ABI / parameters: vector surface pointers plus predicate-mask sideband; scalar `rd` for `VFIRST/VANY/VALL`; sentinel for no active bit.
6. Algorithm: element select/merge under active mask and VL; `VFIRST` first active index; `VANY`/`VALL` boolean scalar; `VMSIF/VMSOF` predicate prefix publication.
7. Retire effects: staged predicate/vector publication or scalar result; no vector RF exposure for predicate-only ops.
8. Replay / rollback: staged publication rollback, mask/tail determinism.
9. Conformance tests: mask/tail policy, empty mask, full mask, partial VL, replay after fault.
10. Golden artifacts: predicate contour matrix and mask result vectors.
11. No-emission tests: compiler facade remains raw transport/no helper until approved.
12. Definition of Done: `VectorLegalityMatrix` marks only exact closed contours executable.
13. Do not do: do not infer support from closed `VMSBF`, comparisons, `VCOMPRESS`, or `VEXPAND`.

## Iteration 08: vector widen, narrow, and convert

Status: leaf template boundary materialized in Iteration 08A; executable width-changing/conversion work remains open and VLM-gated. Iteration 08A only moved no-emission metadata into the 13 widen/narrow/convert leaf partial files and did not allocate opcodes, decoder paths, materializers, MicroOps, VLM executable contours, retire publication, or compiler helpers.

1. Goal: close vector width-changing and conversion contours.
2. Files in `NonVmx/`: `VwaddInstruction.cs`, `VwadduInstruction.cs`, `VwsubInstruction.cs`, `VwsubuInstruction.cs`, `VwmulInstruction.cs`, `VwmuluInstruction.cs`, `VwmaccInstruction.cs`, `VnsrlInstruction.cs`, `VnsraInstruction.cs`, `VsextInstruction.cs`, `VcvtIInstruction.cs`, `VcvtUInstruction.cs`, `VcvtFInstruction.cs`.
3. External dependencies: catalog/status, opcode/sideband ABI, decoder/encoder, `VectorLegalityMatrix`, vector type/element codec, IR, vector MicroOps, staged memory publication, retire, tests.
4. Instructions: widening add/sub/mul/MACC, narrowing shifts, sign-extension, int/uint/float conversions.
5. ABI / parameters: source/destination width sideband; optional accumulator surface; rounding/saturation/trap policy sideband for narrowing/conversion.
6. Algorithm: defined source/destination element widths, signedness, accumulator precision, truncation/rounding, NaN and saturation/trap behavior.
7. Retire effects: staged vector memory or vector surface publication; no partial visible writes outside retire policy.
8. Replay / rollback: exact staged-write rollback; deterministic type conversion.
9. Conformance tests: width matrix, signed/unsigned boundaries, NaN/overflow, masks/tails, fault replay.
10. Golden artifacts: width/rounding/conversion matrix.
11. No-emission tests: no high-level helpers while contours are runtime-only.
12. Definition of Done: VLM contour and evidence status match exactly; no base-opcode shortcut.
13. Do not do: do not treat closed `VZEXT` as evidence for `VSEXT` or conversions.

## Iteration 09: vector segment and structure memory

Status: Phase 07A/07B/07C negative decision gates recorded on top of the Iteration 09A leaf template boundary. Executable segment, structure, 2D, and indexed+2D memory work remains open and VLM-gated. This closure only strengthens no-emission/evidence markers in the 14 segment/structure memory leaf partial files and does not allocate opcodes, decoder paths, materializers, MicroOps, VLM executable contours, retire publication/commit semantics, or compiler helpers.

1. Goal: close vector segment load/store and structure movement without hidden StreamEngine fallback.
2. Files in `NonVmx/`: `Vldseg2Instruction.cs`, `Vldseg4Instruction.cs`, `Vldseg8Instruction.cs`, `Vstseg2Instruction.cs`, `Vstseg4Instruction.cs`, `Vstseg8Instruction.cs`, `Vload2DContour.cs`, `Vstore2DContour.cs`, `VgatherIndexed2DContour.cs`, `VscatterIndexed2DContour.cs`, `VzipInstruction.cs`, `VunzipInstruction.cs`, `VinterleaveInstruction.cs`, `VdeinterleaveInstruction.cs`.
3. External dependencies: memory shape ABI, decoder/encoder sideband, `VectorLegalityMatrix`, StreamEngine legality only where explicit, memory fault model, retire staged commit.
4. Instructions: segment loads/stores, 2D load/store contours, indexed+2D gather/scatter contours, zip/unzip/interleave/deinterleave.
5. ABI / parameters: base pointer, destination/source surfaces, segment count, stride/shape sideband, index surface for indexed+2D.
6. Algorithm: byte ordering, address calculation, segment deinterleaving, 2D row/column/stride traversal, structure movement shape.
7. Retire effects: loads publish staged vector surfaces; stores commit exact byte order at retire; faults do not partially publish outside policy.
8. Replay / rollback: fault ordering, staged-store rollback, deterministic address replay.
9. Conformance tests: segment counts 2/4/8, alignment, faults, byte order, indexed+2D bounds, no hidden fallback.
10. Golden artifacts: memory shape and byte-order vectors.
11. No-emission tests: no compiler helper for fail-closed contours.
12. Definition of Done: only explicitly closed contours become executable in VLM.
13. Do not do: do not duplicate base `VLOAD/VSTORE/VGATHER/VSCATTER` closure or bypass VLM.

## Iteration 10: vector fixed-point and saturating

Status: Phase 08A/08B/08C negative decision gates recorded on top of the Iteration 10A leaf template boundary. Executable fixed-point/saturating and prefix-scan work remains open and VLM-gated. This closure only strengthens no-emission/evidence markers in the 10 fixed-point/saturating and prefix-scan leaf partial files and did not allocate opcodes, decoder paths, materializers, MicroOps, VLM executable contours, retire publication semantics, or compiler helpers.

1. Goal: close fixed-point arithmetic after policy decisions.
2. Files in `NonVmx/`: `VsubSatInstruction.cs`, `VmulSatInstruction.cs`, `VsllSatInstruction.cs`, `VsrlSatInstruction.cs`, `VsraSatInstruction.cs`, `VavgInstruction.cs`, `VavgRInstruction.cs`, `VclipInstruction.cs`, `VscanMinInstruction.cs`, `VscanMaxInstruction.cs`.
3. External dependencies: policy ADR for signedness/rounding/clamp, catalog/status, decoder sideband, `VectorLegalityMatrix`, vector MicroOps, retire, tests.
4. Instructions: `VSUB.SAT`, `VMUL.SAT`, `VSLL.SAT`, `VSRL.SAT`, `VSRA.SAT`, `VAVG`, `VAVG.R`, `VCLIP`, `VSCAN.MIN`, `VSCAN.MAX`.
5. ABI / parameters: element width, signedness, rounding mode, clamp bounds, inclusive/exclusive scan decision.
6. Algorithm: saturating clamp, average truncation/rounding, clip/narrow, prefix min/max.
7. Retire effects: staged vector publication with mask/tail policy.
8. Replay / rollback: deterministic clamp and rounding; rollback staged writes.
9. Conformance tests: signed/unsigned min/max, overflow, underflow, odd averages, scan boundaries.
10. Golden artifacts: saturating arithmetic and scan matrix.
11. No-emission tests: no helper exposure for reserved right-shift saturation if those stay reserved.
12. Definition of Done: any non-meaningful saturating shift remains reserved with tests proving no execution.
13. Do not do: do not infer support from closed `VADD.SAT` or `VSCAN.SUM`.

## Iteration 11: dot/matrix deferral boundary

Status: Phase 09 negative decision gate recorded on top of the Iteration 11A leaf template boundary. Advanced dot/mixed-precision rows remain reserved/no-allocation; matrix/tile rows remain optional-disabled declared-only. This closure only strengthens fail-closed status, no-emission/evidence markers, VLM separation, and scoped-`VDOT.WIDE` guardrails. It did not allocate advanced dot opcodes, did not open decoder/encoder ABI, did not add `InstructionIR` projection, did not publish registry/materializer rows, typed vector/tile MicroOps, scheduler/lane binding, execution/capture, retire/writeback, replay/rollback, compiler helpers, or VMX-specific paths.

1. Goal: preserve future dot and matrix anchors without execution claims.
2. Files in `NonVmx/`: `VdotBlockscaleInstruction.cs`, `VdotAccumInstruction.cs`, `VdotWideI16Instruction.cs`, `VdotWideI32Instruction.cs`, `MtileLoadInstruction.cs`, `MtileStoreInstruction.cs`, `MtileMaccInstruction.cs`, `MtransposeInstruction.cs`.
3. External dependencies: `InstructionSupportStatusCatalog`, `IsaV4Surface.OptionalDisabledOpcodes`, `VectorLegalityMatrix`, no-emission tests, scoped `VDOT.WIDE` executable guardrails.
4. Instructions: advanced dot variants and matrix/tile set.
5. ABI / parameters: not allocated for advanced dot and optional-disabled only for matrix; document required future scale metadata, accumulator precision/result footprint, wider integer contours, tile descriptor, memory/fault model, tile execution, and transpose policy.
6. Algorithm: none executable in this iteration.
7. Retire effects: none.
8. Replay / rollback: no execution; tests prove fail-closed.
9. Conformance tests: no-emission, reserved/optional-disabled status, marker ABI constness, optional-disabled/parser rejection, VLM fail-closed, scoped `VDOT.WIDE` non-authority for advanced dot names, and no VMX/Lane6/Lane7/external backend fallback.
10. Golden artifacts: deferral manifest only.
11. No-emission tests: explicitly forbid matrix/tile and advanced dot helpers.
12. Definition of Done: no runtime or compiler path can claim execution for deferred dot/matrix; Phase 09 is a negative decision gate, not executable closure.
13. Do not do: do not extend current scoped `VDOT.WIDE` by name alone and do not treat existing dot/vector arithmetic evidence as evidence for blockscale, accumulator variants, tile memory, tile MACC, or matrix transpose.

## Iteration 12: Lane6 descriptor op-type expansion

Status: Phase 10 explicit negative decision gate. The rows are descriptor-only declared metadata, not executable closure; descriptor op-type and shape/range execution remains open, descriptor-owned, and no-scalar-opcode.

1. Goal: add descriptor-owned arithmetic/predicate/conversion/reduction op-types without scalar opcode growth.
2. Files in `NonVmx/`: all files under `Lane06DmaStream/DescriptorOps/Arithmetic`, `Predicate`, `TypeConversion`, `Reduction`, and `ShapeRange`.
3. External dependencies: descriptor op enum/allocation, descriptor parser validation, DSC runtime admission, token service, memory owner/domain guard, staged commit, replay, tests.
4. Instructions: `SUB`, `MIN`, `MAX`, `ABSDIFF`, `CLAMP`, `CONVERT`, `COMPARE`, `SELECT`, explicit reductions, and strided/tiled/scatter-gather/2D/multi-range descriptor shapes.
5. ABI / parameters: descriptor src/dst ranges, type, shape, owner/domain, signedness, bounds, predicate/result footprint, scalar or surface reduction result.
6. Algorithm: memory-memory compute under descriptor authority only.
7. Retire effects: staged destination writes or scalar/surface reduction result at retire; no guest-visible host evidence.
8. Replay / rollback: token replay, descriptor structural identity, staged-write rollback, all-or-none or explicit partial policy.
9. Conformance tests: current Phase 10 regressions prove descriptor-only status, const marker ABI, no enum/opcode/registry publication, no VLM executable contour, generic `DmaStreamCompute` non-authority, no compiler helper, and no VMX/Lane7/external backend fallback.
10. Golden artifacts: none for execution; future production requires descriptor op-type ABI, shape enum ABI, normalized footprint hashes, replay/golden manifests, and retire publication artifacts.
11. No-emission tests: compiler must not publish per-op helpers, hidden scalar/vector lowering, or multi-op emission.
12. Definition of Done: Phase 10 is done only as a negative gate; every listed op-type/shape remains fail-closed until the full descriptor evidence chain closes.
13. Do not do: do not create ordinary scalar opcodes, descriptor parser/runtime fallbacks, generic `DmaStreamCompute` expansions, `DSC2` fallbacks, StreamEngine/DMAController fallbacks, Lane7/external backend paths, or VMX-specific paths for these op-types.

## Iteration 13: Lane6 queue/control commands and DSC2

Status: Phase 11 negative decision gate closed; queue/query rows remain reserved/no-allocation, `DSC2` remains parser-only/declared-only, and no executable closure or production publication is opened.

1. Goal: plan and close queue lifecycle commands separately from descriptor compute; keep `DSC2` gated.
2. Files in `NonVmx/`: all files under `Lane06DmaStream/QueueLifecycle`, `Lane06DmaStream/Queries`, and `Lane06DmaStream/CarrierV2/Dsc2DescriptorCarrier.cs`.
3. External dependencies: queue handle ABI, token namespace, fence/cancel scope, capability query ABI, DSC2 descriptor-v2 ADR, parser, runtime admission, retire side effects.
4. Instructions: `DSC_POLL`, `DSC_WAIT`, `DSC_CANCEL`, `DSC_FENCE`, `DSC_COMMIT`, `DSC_QUERY_BACKEND`, `DSC_QUERY_SHAPE`, `DSC2`.
5. ABI / parameters: token/queue handle, scope, capability query sideband, descriptor-v2 header and extension table.
6. Algorithm: queue state observation/control; DSC2 remains parser-only until execution ADR closes.
7. Retire effects: poll/query scalar/status result; wait/fence/cancel/commit side effects retire-owned; DSC2 no execution while parser-only.
8. Replay / rollback: deterministic queue state evidence, rollback of queued side effects, descriptor identity hash.
9. Conformance tests: Phase 11 regressions prove reserved/parser-only status, const marker ABI, no enum/opcode/registry publication, no VLM executable contour, scoped `DSC_STATUS`/`DSC_QUERY_CAPS` non-authority, no compiler helper, and no VMX/Lane7/external backend fallback.
10. Golden artifacts: none for execution; future production requires queue command ABI, capability query ABI, DSC2 parser manifest, replay/golden manifests, and retire publication artifacts.
11. No-emission tests: no queue/control/query/DSC2 helpers, hidden scalar/vector lowering, or multi-op emission.
12. Definition of Done: Phase 11 is done only as a negative gate; queue/query commands fail closed without token/queue authority and `DSC2` execution remains disabled unless a separate descriptor-v2 ADR lands.
13. Do not do: do not hide Lane6 queue commands behind scalar ALU or general system opcodes; do not add DmaStreamCompute, DSC status/query, DSC2, Lane7, external backend, or VMX-specific fallbacks.

## Iteration 14: Lane7 counters and hints

Status: leaf metadata boundary closed in Iteration 14A; counter/hint execution remains open and retire/replay-gated.

1. Goal: close deterministic non-VMX counters and scheduling hint semantics.
2. Files in `NonVmx/`: `RdtimeInstruction.cs`, `RdinstretInstruction.cs`, `PauseInstruction.cs`.
3. External dependencies: catalog/status, opcode allocation, decoder/encoder, system-singleton lane binding, retire accounting, replay clock model, no-emission tests.
4. Instructions: `RDTIME`, `RDINSTRET`, `PAUSE`.
5. ABI / parameters: `RDTIME rd`; `RDINSTRET rd`; `PAUSE` no operands or approved hint immediate.
6. Algorithm: deterministic time source or virtualized/replay-stable counter; retire-count read; pause is scheduling hint only.
7. Retire effects: counter reads write `rd` at retire; `PAUSE` has no architectural state or progress guarantee.
8. Replay / rollback: replay must reproduce counter observations; retire-count rollback must be explicit.
9. Conformance tests: deterministic replay, privilege policy, x0 discard, no hidden progress guarantee.
10. Golden artifacts: counter replay traces.
11. No-emission tests: compiler helpers remain absent unless explicitly approved.
12. Definition of Done: counter reads are retire-owned and replay-stable.
13. Do not do: do not infer `RDTIME/RDINSTRET` from closed `RDCYCLE`.

## Iteration 15: Lane7 cache/TLB/IOMMU maintenance

Status: Phase 13 negative decision gate closed. Maintenance execution remains open and retire/coherency-authority gated; the closure only adds maintenance/replay/virtualization-boundary marker partials and tests. It does not allocate opcodes, open decoder/encoder ABI, add `InstructionIR`, materializers, typed MicroOps, scheduler binding, execution/capture, retire side effects, replay/rollback engine, golden artifacts, compiler helpers, or VMX-specific paths.

1. Goal: close non-VMX maintenance only after memory/coherency authority exists.
2. Files in `NonVmx/`: `SfenceVmaInstruction.cs`, `IcacheInvalInstruction.cs`, `DcacheCleanInstruction.cs`, `DcacheInvalInstruction.cs`, `DcacheFlushInstruction.cs`, `IotlbInvInstruction.cs`, `IommuFenceInstruction.cs`.
3. External dependencies: MMU/TLB/cache/coherency model, IOMMU domain ownership, decoder/encoder, IR, Lane7 materializer, retire side effects, replay invalidation model.
4. Instructions: `SFENCE.VMA`, `ICACHE_INVAL`, `DCACHE_CLEAN`, `DCACHE_INVAL`, `DCACHE_FLUSH`, `IOTLB_INV`, `IOMMU_FENCE`.
5. ABI / parameters: address/range or descriptor sideband; ASID/domain/IOVA/fence scope.
6. Algorithm: invalidate/clean/fence exact domain and range; order effects through retire.
7. Retire effects: retire-owned maintenance publication; no speculative visible side effects.
8. Replay / rollback: invalidation epoch replay, rollback of not-yet-retired maintenance.
9. Conformance tests: Phase 13 fail-closed regressions prove deferred status, const marker ABI, no enum/opcode/registry publication, no VLM executable contour, scoped `FENCE`/`FENCE_I`/atomic/Lane6/Lane7 non-authority, no compiler helper, and no VMX/EPT/VPID/NPT fallback.
10. Golden artifacts: none for execution; future production requires maintenance ABI and invalidation trace artifacts.
11. No-emission tests: keep helpers blocked until memory model is closed.
12. Definition of Done: Phase 13 is done only as a negative gate; maintenance cannot execute without domain authority, coherency model, replay, and retire publication.
13. Do not do: do not borrow VMX/EPT/NPT/VPID semantics, infer from `FENCE`/`FENCE_I` or atomics, or publish host-owned evidence.

## Iteration 16: Lane7 topology/queue accelerator commands

Status: Phase 14 negative decision gate closed. Topology/lifecycle/queue execution remains open and capability/queue/token-authority gated; the closure only adds capability, authority, and lifecycle marker partials and tests. It does not allocate opcodes, open decoder/encoder ABI, add `InstructionIR`, materializers, typed MicroOps, scheduler binding, execution/capture, retire side effects, replay/rollback engine, golden artifacts, compiler helpers, external backend integration, or VMX-specific paths.

1. Goal: close non-VMX accelerator topology/lifecycle/queue commands under Lane7 runtime authority.
2. Files in `NonVmx/`: `AccelQueryAbiInstruction.cs`, `AccelQueryTopologyInstruction.cs`, `AccelOpenInstruction.cs`, `AccelCloseInstruction.cs`, `AccelBindQueueInstruction.cs`, `AccelUnbindQueueInstruction.cs`.
3. External dependencies: Lane7 accelerator runtime, capability authority, queue authority, token authority, command queue semantics, decoder/encoder, IR, materializer, retire effects, no-emission tests.
4. Instructions: `ACCEL_QUERY_ABI`, `ACCEL_QUERY_TOPOLOGY`, `ACCEL_OPEN`, `ACCEL_CLOSE`, `ACCEL_BIND_QUEUE`, `ACCEL_UNBIND_QUEUE`.
5. ABI / parameters: accelerator class/device/queue descriptor sideband, handle/token/queue identifiers, capability result footprint.
6. Algorithm: read-only topology queries, handle lifecycle, queue bind/unbind with authority checks.
7. Retire effects: lifecycle and queue state changes publish only at retire; query results guest-visible only if capability policy allows.
8. Replay / rollback: handle/token namespace replay, queue binding rollback, no host evidence leak.
9. Conformance tests: Phase 14 fail-closed regressions prove reserved status, const marker ABI, no enum/opcode/registry publication, no VLM executable contour, scoped `ACCEL_QUERY_CAPS/SUBMIT/POLL/WAIT/CANCEL/FENCE/STATUS` and topology taxonomy non-authority, no compiler helper, and no VMX/Lane6/Lane7 submit/external backend fallback.
10. Golden artifacts: none for execution; future production requires topology/queue command ABI, denial matrix, replay/migration, and retire publication artifacts.
11. No-emission tests: extend existing Lane7 no-emission guards.
12. Definition of Done: Phase 14 is done only as a negative gate; execution authority, commit authority, and capability authority must all be explicit before any production row opens.
13. Do not do: do not duplicate current closed `ACCEL_QUERY_CAPS/SUBMIT/POLL/WAIT/CANCEL/FENCE/STATUS`, infer from topology taxonomy, or expose backend helpers.

## Iteration 17: final compatibility and conformance sweep

Status: Phase 15 compatibility/conformance sweep closed as an audit package.
The closure adds the local sweep contract and focused regression tests that
prove executable rows still carry the full catalog/opcode/decoder/IR/
materializer/MicroOp/execution/retire/replay/golden/no-emission evidence chain,
while deferred rows from Phases 05-14 remain fail-closed. It does not allocate
opcodes, open decoder/encoder ABI, add `InstructionIR`, materializers, typed
MicroOps, scheduler binding, execution/capture, retire side effects, replay
engines, golden execution artifacts, compiler helpers, external backends, or
VMX-specific paths.

1. Goal: prove the completed Non-VMX CloseToRTL surface is ABI-compatible, replay-stable, and compiler-safe.
2. Files in `NonVmx/`: all 139 anchors plus this plan if updated with final status.
3. External dependencies: all catalog/status rows, opcode/descriptor registries, decoder/encoder ABI, IR, materializers, MicroOps, retire, vector legality, Lane6/Lane7 runtimes, test manifests.
4. Instructions: all implemented rows; all deferred rows; all closed-current rows that must not be duplicated.
5. ABI / parameters: freeze manifest for every newly executable row and explicit no-emission manifest for deferred/reserved rows.
6. Algorithm: cross-check implementation algorithms against golden vectors and runtime-owned evidence.
7. Retire effects: every side effect must be retire-owned and replay-described.
8. Replay / rollback: run replay/rollback suites for scalar, vector, Lane6, Lane7, maintenance, and sideband descriptors.
9. Conformance tests: full test sweep plus targeted no-emission and no-duplication tests.
10. Golden artifacts: final opcode/descriptor ABI, vector contour matrix, replay traces, no-emission snapshot.
11. No-emission tests: all runtime-only/reserved/facade-only rows remain compiler-safe.
12. Definition of Done: Phase 15 is done as a sweep-only closure; no row claims execution without full evidence chain; no VMX leakage; no frozen frontend breakage.
13. Do not do: do not treat CloseToRTL object presence as runtime closure by itself.

## Cross-cutting production decisions and risks

- The 2026-05-23 audit file referenced by the task is missing from `OpenTasks`; resolve before final backlog closure.
- ISA v4 is frozen. New numeric opcode allocation needs a formal evolution story and decoder/encoder compatibility story.
- `VectorLegalityMatrix` is runtime authority for vector contours; base opcode closure is not enough.
- Existing no-emission tests intentionally forbid many helper names. Any helper decision must update tests and policy explicitly.
- Lane6 descriptor op-types require descriptor authority, token admission, staged writes, and retire commit. They must not become scalar opcodes.
- `DSC2` has parser-only evidence in the current runtime; execution must remain blocked until descriptor-v2 closure.
- Lane7 cache/TLB/IOMMU maintenance lacks a complete non-VMX coherency/rollback model in the current evidence surface.
- Lane7 accelerator topology/queue commands require capability, queue, token, execution, and commit authority before execution.
- Host-owned evidence must never become guest architectural state.

## Plan-level Definition of Done

- Every existing stub is mapped to an instruction, family, or contour.
- Every implementation iteration lists files, dependencies, ABI, algorithm, retire effects, replay/rollback, tests, golden artifacts, no-emission boundaries, DoD, and non-goals.
- Descriptor-only, facade-only, deferred, and already-closed contours are separated from ordinary opcode implementation work.
- No instruction implementation code is introduced by this plan.
