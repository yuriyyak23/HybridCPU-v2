# Non-VMX Missing Instructions Current Shortlist — 2026-05-25

Scope: `Documentation/InstructionsRefactor2/OpenTasks`, current HybridCPU-v2 ISE model on `master` as checked on 2026-05-25.

Companion to `NON_VMX_MISSING_INSTRUCTIONS_AUDIT_2026-05-23.md`. This file is intentionally shorter and actionable: it lists the still-open Non-VMX instruction/descriptor/control-plane gaps by lane class, with the required instruction shape, parameters, and evidence gate.

VMX remains excluded: no VMCS, VM-entry, VM-exit, VPID, EPT/NPT, INVEPT/INVVPID, VMFUNC, VMREAD/VMWRITE, VMLAUNCH/VMRESUME, migration, dirty-log, or VMCSv2 execution tasks are tracked here.

## 0. Derivation algorithm

Treat runtime-owned evidence as the only executable authority.

A mnemonic or descriptor op is considered **still missing** when at least one of these is true:

1. `InstructionSupportStatusCatalog.GetStatus(x).IsExecutableClaim == false`.
2. The status is `Reserved`, `OptionalDisabled`, `ParserOnly`, `DescriptorOnly`, `CarrierOnly`, or `Prohibited`.
3. Runtime evidence is below `Executable`, or execution semantics are not present.
4. For vector operations, `VectorLegalityMatrix` marks the requested contour as `FailClosed`, `DescriptorOnly`, or `NotApplicable`, even if the base opcode is executable on another contour.
5. Compiler facade/helper availability is absent or intentionally no-emission. Compiler transport is not execution authority.
6. For Lane6/Lane7, the item is a descriptor op-type or control-plane command unless a runtime-owned opcode/descriptor chain explicitly closes it.

Required closure chain for any row below:

```text
status/catalog entry
  -> opcode value or descriptor op-type allocation
  -> classifier/decoder ABI and encoder compatibility
  -> InstructionIR/projection
  -> registry/materializer
  -> typed MicroOp publication and lane binding
  -> execute/capture
  -> retire/writeback/side-effect publication
  -> replay/rollback/conformance tests
  -> golden artifacts and no-emission regression where compiler must stay frozen
```

## 1. Do not re-plan these closed Non-VMX contours

The current ISE model already marks these as executable/conformance-tested or otherwise intentionally closed. Do not copy them into a new missing-instruction queue without a fresh code-level gap audit.

- Scalar mandatory/core or repaired: current base scalar ALU, W-forms, load/store, branch, CSR, trap, AMO, `LUI`, `AUIPC`, `SLT`, `SLTU`, `SLTI`, `SLTIU`, `MULH`, `MULHU`, `MULHSU`, `DIVU`, `REM`, `REMU`, `SRA`, `ADDIW`, `ADDW`, `SUBW`, `SLLW`, `SRLW`, `SRAW`, `SLLIW`, `SRLIW`, `SRAIW`, `MULW`, `DIVW`, `DIVUW`, `REMW`, `REMUW`, `SEXT.W`, `ZEXT.W`.
- Scalar optional closures: `CZERO.EQZ`, `CLZ`, `CTZ`, `SEXT.B`, `SEXT.H`, `ZEXT.H`, `ROL`, `ROR`, `SH1ADD`, `RDCYCLE`, `CLMUL`.
- Vector config/compute/memory/mask/reduction/movement closures: `VSETVL`, `VSETVLI`, `VSETIVLI`, `VADD`, `VSUB`, `VMUL`, `VDIV`, `VSQRT`, `VMOD`, `VLOAD`, `VSTORE`, `VXOR`, `VOR`, `VAND`, `VNOT`, `VSLL`, `VSRL`, `VSRA`, `VFMADD`, `VFMSUB`, `VFNMADD`, `VFNMSUB`, `VMIN`, `VMAX`, `VMINU`, `VMAXU`, `VMAND`, `VMOR`, `VMXOR`, `VMNOT`, `VCMPEQ`, `VCMPNE`, `VCMPLT`, `VCMPLE`, `VCMPGT`, `VCMPGE`, `VPOPC`, `VCOMPRESS`, `VEXPAND`, `VPERMUTE`, `VRGATHER`, `VREDSUM`, `VREDMAX`, `VREDMIN`, `VREDMAXU`, `VREDMINU`, `VREDAND`, `VREDOR`, `VREDXOR`, `VDOT`, `VDOTU`, `VDOTF`, `VDOT_FP8`.
- Advanced vector closures from the current refactor: `VGATHER`, `VSCATTER`, `VMSBF`, `VZEXT`, `VSCAN.SUM`, `VADD.SAT`, `VSLIDEUP`, `VSLIDEDOWN`, `VSLIDE1UP`, `VSLIDE1DOWN`, `VPERM2`, `VTRANSPOSE`, `VREVERSE`, `VPOPCNT`, `VCLZ`, `VCTZ`, `VBREV8`, current `VDOT.WIDE` scalar-footprint contours.
- Lane6 current DSC1 closure: `DmaStreamCompute` for `Copy`, `Add`, `Mul`, `Fma`, `Reduce` with `UInt8/16/32/64`, `Float32/64`, `Contiguous1D` or `FixedReduce`, `InlineContiguous`, `AllOrNone`; plus `DSC_STATUS`, `DSC_QUERY_CAPS`.
- Lane7 current L7-SDC closure: `ACCEL_QUERY_CAPS`, `ACCEL_SUBMIT`, `ACCEL_POLL`, `ACCEL_WAIT`, `ACCEL_CANCEL`, `ACCEL_FENCE`, `ACCEL_STATUS`.

## 2. Lane map for rows below

| Lanes | Slot class | Meaning for this shortlist |
|---:|---|---|
| 0-3 | `AluClass` | Scalar ALU, vector compute, vector predicate/mask publication, dot/permute/fixed-point compute. |
| 4-5 | `LsuClass` | Scalar/vector memory transfer, segment load/store, future 2D/indexed transfer contours. |
| 6 | `DmaStreamClass` | `DmaStreamCompute` descriptor op-types, token admission, queue/fence/completion surface. |
| 7 | `BranchControl` / `SystemSingleton` | Branch/system/CSR/hints, memory-maintenance, non-VMX IOMMU/TLB control, Lane7 accelerator control-plane. |

---

# 3. Current missing shortlist

## 3.1 Lanes 0-3: scalar ALU / facade candidates

| Required instruction(s) | Parameters / ABI | Algorithm / meaning | Current model status | Required evidence |
|---|---|---|---|---|
| `SEQZ`, `SNEZ` | `rd, rs1` | `SEQZ`: `rd = rs1 == 0 ? 1 : 0`; `SNEZ`: `rd = rs1 != 0 ? 1 : 0`. | CLOSED as Phase 01F facade-only/no-emission rows; no hardware opcode, decoder, materializer, helper, or hidden lowering authority. | Future hardware requires a new full scalar path; current facade policy is not runtime evidence. |
| `CSEL` | `rd, rs_true, rs_false, rs_cond` or sideband-defined four-register ABI | Branchless scalar select. | CLOSED template boundary in Iteration 04C; CLOSED leaf metadata in Metadata Pass 01A; current Phase 01 carrier decision approves no 4-source ABI. | Explicit external 4-source carrier/sideband ABI, no hidden predicate register, ALU materializer, retire/replay. |
| `CZERO.NEZ` | `rd, rs1, rs2` | Counterpart to closed `CZERO.EQZ`; zeros when `rs2 != 0`, otherwise returns `rs1`. | CLOSED as Phase 01E scalar execution row with opcode `333`, canonical binary decoder/projection, scalar ALU materialization, retire/replay, golden vectors, and no compiler helper authority. | Keep separate from `CZERO.EQZ`; do not reopen as facade/helper lowering without explicit helper authority. |
| `CTZ` | `rd, rs1` | Count trailing zeros over XLEN=64; zero input returns XLEN. | Closed in Iteration 03A as `OptionalEnabled` / `ConformanceTested`, opcode 59. | Evidence includes canonical unary decoder/projection, scalar materializer, `InternalOpKind.Ctz`, CloseToRTL typed object, retire-owned writeback, x0 discard, replay/rollback tests, and compiler no-emission boundary. |
| `CPOP` / `POPCNT` | `rd, rs1` | Population count. | CLOSED with `CPOP` as Phase 01A canonical scalar execution row, opcode `334`, unary decoder/projection, scalar ALU materialization, retire/replay, golden vectors, and no compiler helper authority; `POPCNT` remains a reserved/no-emission alias boundary. | Do not allocate a second `POPCNT` opcode or helper without explicit parser/compiler alias authority. |
| `ROL`, `ROR` | `rd, rs1, rs2`; immediate forms are separate opcodes | Rotate left/right by masked low shift bits. | Closed in Iteration 03C as `OptionalEnabled` / `ConformanceTested`, opcodes 63/64. | Evidence includes canonical binary decoder/projection, scalar materializer, immediate-alias rejection, `InternalOpKind.Rol/Ror`, CloseToRTL typed objects, retire-owned writeback, x0 discard, replay/rollback tests, and compiler no-emission boundary. |
| `ROLI`, `RORI` | `rd, rs1, imm6`; canonical `Word1=(rd, rs1, x0)` | Rotate left/right by unsigned imm6, separate from closed register-register `ROL`/`ROR`. | CLOSED as local Phase 02 scalar rotate-immediate rows with opcodes `335..336`, `OptionalEnabled` / `ConformanceTested`. | Evidence includes opcode/status/catalog, decoder/encoder imm6 range rejection, `InstructionIR` projection, scalar materializer with `UsesImmediate`, `InternalOpKind.RolI/RorI`, CloseToRTL typed objects, retire-owned writeback, x0 discard, replay/rollback tests, golden vectors, compiler no-emission boundary, and VMX generic-only boundary. |
| `ANDN`, `ORN`, `XNOR` | `rd, rs1, rs2` | `rs1 & ~rs2`, `rs1 | ~rs2`, `~(rs1 ^ rs2)`. | Reserved/no execution. Template boundary closed in Iteration 04A; CLOSED leaf metadata in Metadata Pass 01B; OPEN opcode/decoder/materializer/runtime execution. | One MicroOp each or facade-only; no hidden multi-op emission unless facade explicitly owns it. |
| `MIN`, `MAX`, `MINU`, `MAXU` | `rd, rs1, rs2` | Scalar signed/unsigned min/max. | Reserved/no scalar execution. Template boundary closed in Iteration 04A; CLOSED leaf metadata in Metadata Pass 01B; OPEN opcode/decoder/materializer/runtime execution. | Keep distinct from vector `VMIN*` and AMO min/max; signedness tests. |
| `REV8`, `BREV8` | `rd, rs1` | Byte-order reverse and bit-reverse-in-byte / selected byte-bit ordering. | CLOSED as Phase 01D scalar execution rows with opcodes `331..332`, typed scalar ALU materialization, retire/replay, golden vectors, and no compiler helper authority. | Keep distinct from vector `VBREV8`. |
| `SEXT.B`, `SEXT.H`, `ZEXT.H` | `rd, rs1` | Sign/zero extend low 8/16-bit field to XLEN=64. | Closed in Iteration 03B as `OptionalEnabled` / `ConformanceTested`, opcodes 60/61/62. | Evidence includes canonical unary decoder/projection, scalar materializer, `InternalOpKind.SextB/SextH/ZextH`, byte/half data widths, CloseToRTL typed objects, retire-owned writeback, x0 discard, replay/rollback tests, and compiler no-emission boundary. |
| `BSET`, `BCLR`, `BINV`, `BEXT` | `rd, rs1, rs2`; canonical `Immediate=0` | Register-indexed bit set/clear/invert/extract with `rs2 & 0x3F`; `BEXT` returns canonical 0/1. | CLOSED as local Phase 02 scalar bitfield rows with opcodes `337..340`, `OptionalEnabled` / `ConformanceTested`. | Evidence includes opcode/status/catalog, decoder/encoder immediate-alias rejection, `InstructionIR` projection, scalar materializer, `InternalOpKind.Bset/Bclr/Binv/Bext`, CloseToRTL typed objects, retire-owned writeback, x0 discard, replay/rollback tests, golden vectors, compiler no-emission boundary, and VMX generic-only boundary. |
| `BSETI`, `BCLRI`, `BINVI`, `BEXTI` | `rd, rs1, imm6`; canonical `Word1=(rd, rs1, x0)` | Immediate-indexed bit set/clear/invert/extract; `BEXTI` returns canonical 0/1. | CLOSED as local Phase 02 scalar bitfield-immediate rows with opcodes `341..344`, `OptionalEnabled` / `ConformanceTested`. | Evidence includes opcode/status/catalog, decoder/encoder imm6 range rejection and rs2-alias rejection, `InstructionIR` projection, scalar materializer with `UsesImmediate`, `InternalOpKind.BsetI/BclrI/BinvI/BextI`, CloseToRTL typed objects, retire-owned writeback, x0 discard, replay/rollback tests, golden vectors, compiler no-emission boundary, and VMX generic-only boundary. |
| `SH2ADD`, `SH3ADD` | `rd, rs1, rs2` | Address-generation ALU: `(rs1 << 2|3) + rs2`. | CLOSED as Phase 03 scalar address-generation rows with opcodes `345..346`, `OptionalEnabled` / `ConformanceTested`. | Evidence includes opcode/status/catalog, decoder/encoder `Immediate=0` alias rejection, `InstructionIR` projection, scalar materializer, `InternalOpKind.Sh2Add/Sh3Add`, CloseToRTL typed objects, retire-owned writeback, x0 discard, replay/rollback tests, golden vectors, compiler no-emission boundary, and VMX generic-only boundary. |
| `ADD.UW`, `SH1ADD.UW`, `SH2ADD.UW`, `SH3ADD.UW`, `SLLI.UW` | `rd, rs1, rs2/imm6` | Zero-extend low 32 bits of `rs1`, then add/shift. | CLOSED as Phase 03 scalar `.UW` address-generation rows with opcodes `347..351`, `OptionalEnabled` / `ConformanceTested`. | Evidence includes explicit `.UW` source-width ABI, `SLLI.UW` `Word1=(rd, rs1, x0)` imm6 range rejection, no LSU bypass authority, scalar materializer/InternalOpKind, retire/replay/golden vectors, compiler no-emission boundary, and VMX generic-only boundary. |
| `CLMULH`, `CLMULR` | `rd, rs1, rs2`; canonical `Immediate=0` | Carry-less multiply high/reversed forms: LSB-first GF(2) product bits `[127:64]` and `[126:63]`. | CLOSED as Phase 04 scalar carry-less variant rows with opcodes `352..353`, `OptionalEnabled` / `ConformanceTested`. | Evidence includes opcode/status/catalog, decoder/encoder immediate-alias rejection, `InstructionIR` projection, scalar materializer, `InternalOpKind.ClMulH/ClMulR`, CloseToRTL typed objects, retire-owned writeback, x0 discard, replay/rollback tests, golden vectors, compiler no-emission boundary, and VMX generic-only boundary. |
| `CRC32`, `CRC64` | Proposed `rd, rs_seed, rs_data` remains non-executable | CRC update helper. | ABI gate CLOSED negative in Phase 04; reserved/no-allocation/no decoder/materializer/runtime execution. | Future package must choose polynomial, reflection, seed/final-xor, and endian ingestion policy before opcode allocation. |
| `ADC`, `SBC`, `ADDC`, `SUBC` | Explicit carry/borrow operand/result ABI TBD; no implicit flags | Multi-precision add/sub helpers. | ABI gate CLOSED negative in Phase 04; reserved/no-allocation/no decoder/materializer/runtime execution. | Future package must define visible carry/borrow input and retire-owned output publication convention. |

## 3.2 Lanes 0-3: vector compute / predicate / fixed-point gaps

| Required instruction(s) | Parameters / ABI | Algorithm / meaning | Current model status | Required evidence |
|---|---|---|---|---|
| `VMERGE`, `VSELECT` | `DestSrc1Pointer`, `Src2Pointer`, predicate-mask sideband | Merge/select vector elements under predicate. | Reserved/no execution. Leaf template boundary materialized in Iteration 07A with no opcode/decoder/materializer/VLM-executable/runtime claim. | Tail/mask policy, staged publication, replay. Decide alias vs separate hardware contour. |
| `VFIRST`, `VANY`, `VALL` | Scalar `rd` plus predicate-mask sideband | Scalar predicate summary: first active index, any active, all active. | Reserved/no execution. Leaf template boundary materialized in Iteration 07A with no opcode/decoder/materializer/VLM-executable/runtime claim. | Scalar result footprint, sentinel for no active bit, active VL/tail semantics, retire publication. |
| `VMSIF`, `VMSOF` | predicate destination/source sideband | Mask set-including-first and set-only-first. | Reserved/no execution; `VMSBF` only is closed. Leaf template boundary materialized in Iteration 07A with no opcode/decoder/materializer/VLM-executable/runtime claim. | Predicate-only publication tests and no vector RF exposure. |
| `VWADD`, `VWADDU`, `VWSUB`, `VWSUBU`, `VWMUL`, `VWMULU`, `VWMACC` | `DestSrc1Pointer`, `Src2Pointer`, optional accumulator/source-width sideband | Widening add/sub/mul/MACC. | Reserved/no execution. Leaf template boundary materialized in Iteration 08A with no opcode/decoder/materializer/VLM-executable/runtime claim. | Source/destination width ABI, overflow/saturation policy, accumulator/result footprint. |
| `VNSRL`, `VNSRA` | `DestSrc1Pointer`, shift source/immediate sideband | Narrowing logical/arithmetic right shift. | Reserved/no execution. Leaf template boundary materialized in Iteration 08A with no opcode/decoder/materializer/VLM-executable/runtime claim. | Truncation, rounding, saturation, mask/tail behavior. |
| `VSEXT` | `DestSrc1Pointer`, source-width sideband | Signed vector sign-extension. | Reserved/no execution; `VZEXT` only is closed. Leaf template boundary materialized in Iteration 08A with no opcode/decoder/materializer/VLM-executable/runtime claim. | Source widths, signedness, staged memory publication. |
| `VCVT.I`, `VCVT.U`, `VCVT.F` | `DestSrc1Pointer`, type/rounding sideband | Vector convert to signed int, unsigned int, or float. | Reserved/no execution. Leaf template boundary materialized in Iteration 08A with no opcode/decoder/materializer/VLM-executable/runtime claim. | NaN, rounding, saturation/trap policy, publication footprint. |
| `VSCAN.MIN`, `VSCAN.MAX` | `DestSrc1Pointer`, element-type sideband | Prefix min/max. | Reserved/no execution; `VSCAN.SUM` only is closed. Leaf template boundary materialized in Iteration 10A with no opcode/decoder/materializer/VLM-executable/runtime claim. | Inclusive/exclusive decision, tail policy, replay. |
| `VZIP`, `VUNZIP`, `VINTERLEAVE`, `VDEINTERLEAVE` | `DestSrc1Pointer`, `Src2Pointer`, shape sideband | 1D/2D structure movement. | Reserved/no execution. Leaf template boundary materialized in Iteration 09A with no opcode/decoder/materializer/VLM-executable/runtime claim. | Shape ABI, staged movement, no hidden StreamEngine fallback. |
| `VSUB.SAT`, `VMUL.SAT`, `VSLL.SAT`, `VSRL.SAT`, `VSRA.SAT` | `DestSrc1Pointer`, source/shift, saturating policy sideband | Saturating subtract/mul/shift family. | Reserved/no execution; `VADD.SAT` only is closed. Leaf template boundary materialized in Iteration 10A with no opcode/decoder/materializer/VLM-executable/runtime claim. | Signedness, element width, clamp behavior. For right shifts, decide whether saturation is meaningful or reserve. |
| `VAVG`, `VAVG.R`, `VCLIP` | `DestSrc1Pointer`, `Src2Pointer` or clip-bounds sideband | Average, rounded average, fixed-point clip/narrow. | Reserved/no execution. Leaf template boundary materialized in Iteration 10A with no opcode/decoder/materializer/VLM-executable/runtime claim. | Rounding/truncation, bounds encoding, signedness, result width. |
| `VDOT.BLOCKSCALE`, `VDOT.ACCUM`, wider `VDOT.WIDE.I16/I32` variants | Dot/mixed-precision sideband; optional accumulator/result separation | Advanced dot beyond current scalar-footprint ABI. | Future/backlog; current `VDOT`, `VDOTU`, `VDOTF`, `VDOT_FP8`, and scoped `VDOT.WIDE` are closed. Leaf template boundary materialized in Iteration 11A with no opcode/decoder/materializer/VLM-executable/runtime claim and no name-only scoped-`VDOT.WIDE` extension. | Scale metadata, accumulator precision, separate result surface rules, no hidden host evidence as guest architectural state. |
| `MTILE_LOAD`, `MTILE_STORE`, `MTILE_MACC`, `MTRANSPOSE` | Tile descriptor/base pointer/accumulator sideband | Matrix/tile extension. | `OptionalDisabled`, declared only. Leaf template boundary materialized in Iteration 11A with no opcode/decoder/materializer/VLM-executable/runtime claim. | Defer until tile execution model, tile memory-shape/fault model, and compiler no-emission gates are defined. |

## 3.3 Lanes 4-5: vector memory and LSU shape gaps

| Required instruction(s) / contour | Parameters / ABI | Algorithm / meaning | Current model status | Required evidence |
|---|---|---|---|---|
| `VLDSEG2`, `VLDSEG4`, `VLDSEG8` | Destination surfaces, base pointer, stride/shape sideband | Segment/interleaved vector loads. | Reserved/no execution. Leaf template boundary materialized in Iteration 09A with no opcode/decoder/materializer/VLM-executable/runtime claim. | Memory-shape ABI, fault/partial-publication rules, byte ordering, replay. |
| `VSTSEG2`, `VSTSEG4`, `VSTSEG8` | Source surfaces, base pointer, stride/shape sideband | Segment/interleaved vector stores. | Reserved/no execution. Leaf template boundary materialized in Iteration 09A with no opcode/decoder/materializer/VLM-executable/runtime claim. | Exact byte commit ordering, fault/replay, staged retire. |
| 2D `VLOAD`/`VSTORE` contour | base + row/column/stride shape sideband | Vector rectangular transfer. | Base `VLOAD`/`VSTORE` are executable only on current 1D transfer carrier; 2D contour fails closed. Leaf template boundary materialized in Iteration 09A with no base-opcode duplication and no opcode/decoder/materializer/VLM-executable/runtime claim. | Shape descriptor, partial fault model, conformance matrix. |
| indexed+2D `VGATHER`/`VSCATTER` contour | index surface + 2D shape sideband | 2D indexed memory gather/scatter. | 1D indexed `VGATHER`/`VSCATTER` are closed; indexed+2D remains fail-closed. Leaf template boundary materialized in Iteration 09A with no base-opcode duplication and no opcode/decoder/materializer/VLM-executable/runtime claim. | Address calculation, bounds/fault ordering, descriptor-backed evidence. |

## 3.4 Lane6: `DmaStreamCompute` descriptor op-types and queue surface

Lane6 expansion must remain descriptor-owned. Do not add these as ordinary scalar opcodes unless the ISA evolution process explicitly creates a new carrier. The current executable DSC1 carrier supports only `Copy`, `Add`, `Mul`, `Fma`, `Reduce` over the closed shape/type/range/policy subset.

| Required descriptor op / command | Parameters / ABI | Algorithm / meaning | Current model status | Required evidence |
|---|---|---|---|---|
| `DmaStreamCompute{op=SUB}` | src0/src1/dst ranges, type, shape, owner/domain | Memory-memory subtract. | CLOSED template boundary in Iteration 12A; OPEN descriptor op enum/runtime execution. | Add descriptor op-type, parser validation, token admission, staged commit, replay. |
| `DmaStreamCompute{op=MIN}`, `{op=MAX}` | same plus signedness/type policy | Elementwise min/max. | CLOSED template boundary in Iteration 12A; OPEN descriptor op enum/runtime execution. | Signedness/type behavior, fault/replay tests. |
| `DmaStreamCompute{op=ABSDIFF}`, `{op=CLAMP}` | src/dst plus bounds or type policy | Absolute difference and clamp. | CLOSED template boundary in Iteration 12A; OPEN descriptor op enum/runtime execution. | Overflow, bounds encoding, type checks. |
| `DmaStreamCompute{op=CONVERT}` | source/destination type sideband | Bulk type conversion. | CLOSED template boundary in Iteration 12A; OPEN descriptor op enum/runtime execution. | Rounding, NaN, saturation/trap behavior. |
| `DmaStreamCompute{op=COMPARE}`, `{op=SELECT}` | compare operands or predicate + true/false surfaces | Bulk compare and select. | CLOSED template boundary in Iteration 12A; OPEN descriptor op enum/runtime execution. | Predicate/result representation and commit ordering. |
| Explicit reductions: `{op=REDUCE_SUM}`, `{op=REDUCE_MIN}`, `{op=REDUCE_MAX}`, `{op=REDUCE_AND}`, `{op=REDUCE_OR}`, `{op=REDUCE_XOR}` | reduction descriptor; scalar or surface result footprint | Named reductions beyond generic closed `Reduce`. | CLOSED template boundary in Iteration 12A; OPEN explicit op enum/runtime execution. | Result footprint, retire publication, replay/golden tests. |
| Shape/range expansion | strided, tiled, scatter/gather, 2D, multi-range descriptor shape | Non-contiguous or higher-dimensional DSC. | CLOSED template boundary in Iteration 12A; OPEN shape enum/runtime execution. Current executable contour remains `Contiguous1D` or `FixedReduce`, `InlineContiguous`, `AllOrNone`. | New shape enum values, normalized footprints, alias policy, partial completion model. |
| `DSC_POLL`, `DSC_WAIT`, `DSC_CANCEL`, `DSC_FENCE`, `DSC_COMMIT` | token/queue handle plus fence/cancel scope | Queue lifecycle commands. | Reserved/no opcode allocation. CLOSED leaf metadata in Iteration 13A; OPEN token/queue/runtime execution. | Token lifecycle ABI, queue visibility, retire effects, rollback behavior, and future VMX-compatible policy before execution. |
| `DSC_QUERY_BACKEND`, `DSC_QUERY_SHAPE` | query descriptor/capability sideband | Capability/shape query. | Reserved/no opcode allocation. CLOSED leaf metadata in Iteration 13A; OPEN bounded capability publication/runtime execution. | Read-only capability ABI with no hidden host evidence, replay-stable result, and future VMX-compatible policy before execution. |
| `DSC2` | descriptor-v2 carrier | Future Lane6 descriptor-v2 transport. | ParserOnly/DeclaredOnly. CLOSED leaf metadata in Iteration 13A; OPEN descriptor-v2 ADR/runtime execution. | Descriptor v2 contract, backward-compatible decoder, no execution until runtime chain closes, and future VMX-compatible policy before execution. |

## 3.5 Lane7: system, maintenance, and accelerator control gaps

| Required instruction / command | Parameters / ABI | Algorithm / meaning | Current model status | Required evidence |
|---|---|---|---|---|
| `RDTIME` | `rd` | Read time counter. | Reserved/no execution; `RDCYCLE` only is closed. CLOSED leaf metadata in Iteration 14A; OPEN opcode/decoder/runtime execution. | Replay determinism, privilege/virtualization boundary, retire publication. |
| `RDINSTRET` | `rd` | Read retired-instruction counter. | Reserved/no execution. CLOSED leaf metadata in Iteration 14A; OPEN opcode/decoder/runtime execution. | Retire accounting and replay behavior. |
| `PAUSE` | no operands or hint immediate TBD | Spin-wait/scheduling hint. | Reserved/no execution. CLOSED leaf metadata in Iteration 14A; OPEN hint encoding/runtime policy. | No architectural state leakage and no hidden progress guarantee. |
| `SFENCE.VMA` | `rs1(addr), rs2(asid)` or canonical zero-payload subset | Non-VMX address-translation fence. | Reserved/deferred. | MMU/TLB/coherency/rollback architecture before execution. |
| `ICACHE_INVAL` | `rs1(addr), rs2(length)` or descriptor sideband | Instruction-cache invalidation. | Reserved/deferred. | Cache hierarchy, ordering, replay model. |
| `DCACHE_CLEAN`, `DCACHE_INVAL`, `DCACHE_FLUSH` | `rs1(addr), rs2(length)` or descriptor sideband | Data-cache clean/invalidate/clean+invalidate. | Reserved/deferred. | Coherency and memory-ordering model. |
| `IOTLB_INV`, `IOMMU_FENCE` | domain, IOVA range, fence scope via descriptor/control sideband | Non-VMX DMA-domain invalidation/fence. | Explicit `Reserved` / `RuntimeEvidence.None`; no numeric opcode allocation. | Bind to IOMMU domain ownership and Lane6 tokens/fences before any execution claim; avoid VPID/EPT/NPT semantics. |
| `ACCEL_QUERY_ABI`, `ACCEL_QUERY_TOPOLOGY`, `ACCEL_OPEN`, `ACCEL_CLOSE`, `ACCEL_BIND_QUEUE`, `ACCEL_UNBIND_QUEUE` | accelerator class/device/queue descriptor sideband | Lane7 topology/queue lifecycle commands. | Reserved, no numeric opcode, parser rejected, no compiler emission. | Capability authority, queue lifecycle model, token authority, command queue semantics, no-emission tests. |

## 4. Priority notes

1. Iteration 02A aligned explicit catalog rows for bitfield `B*` instructions and non-VMX `IOTLB_INV`/`IOMMU_FENCE` as `Reserved` / `RuntimeEvidence.None`; executable work still requires opcode/ABI/decoder/runtime evidence.
2. Iteration 03A closed `CTZ`.
3. Iteration 03B closed `SEXT.B`, `SEXT.H`, and `ZEXT.H`.
4. Iteration 03C closed register-register `ROL` and `ROR`.
5. Iteration 04A closed template/no-emission boundaries for `CPOP`/`POPCNT`, `ANDN`/`ORN`/`XNOR`, scalar `MIN`/`MAX`/`MINU`/`MAXU`, `REV8`/`BREV8`, bitfield `B*`, and user-requested rotate-immediate `ROLI`/`RORI`. Later production slices promoted `ANDN`/`ORN`/`XNOR`, `MIN`/`MAX`/`MINU`/`MAXU`, `REV8`/`BREV8`, canonical `CPOP`, Phase 02 `ROLI`/`RORI`, Phase 02 register-indexed `BSET`/`BCLR`/`BINV`/`BEXT`, and Phase 02 immediate-indexed `BSETI`/`BCLRI`/`BINVI`/`BEXTI`; `POPCNT` remains no-emission. The remaining template rows still have no opcode/decoder/materializer/compiler helper authority.
6. Iteration 04B closed aggregate CloseToRTL template/no-emission boundaries for vector, Lane6, and Lane7 deferred contours. These are metadata-only partials: VLM, descriptor authority, token/queue authority, retire publication, and compiler helper boundaries remain closed.
7. Iteration 04C closed remaining scalar anchor-only template/no-emission boundaries for `SEQZ`/`SNEZ`, `CSEL`, `CZERO.NEZ`, address-generation `.UW`, `CLMULH`/`CLMULR`, `CRC32`/`CRC64`, and `ADC`/`SBC`/`ADDC`/`SUBC`; Phase 01E later promoted `CZERO.NEZ` and closed `CSEL` as a negative no-carrier decision, Phase 01F closed `SEQZ`/`SNEZ` as facade-only/no-emission, Phase 03 promoted the address-generation `.UW` pool, Phase 04 promoted `CLMULH`/`CLMULR`, and Phase 04 also closed CRC/multiprecision ABI gates as explicit negative no-allocation decisions. The remaining CRC/checksum/multiprecision rows stay reserved/no-execution and have no opcode/decoder/materializer/compiler helper authority.
8. Iteration 07A materialized vector predicate/select leaf template/no-emission boundaries for `VMERGE`, `VSELECT`, `VFIRST`, `VANY`, `VALL`, `VMSIF`, and `VMSOF`; these remain VLM-gated reserved/no-execution rows with no opcode/decoder/materializer/compiler helper authority.
9. Iteration 08A materialized vector widen/narrow/convert leaf template/no-emission boundaries for `VWADD`/`VWADDU`/`VWSUB`/`VWSUBU`/`VWMUL`/`VWMULU`/`VWMACC`, `VNSRL`/`VNSRA`, `VSEXT`, and `VCVT.I`/`VCVT.U`/`VCVT.F`; these remain VLM-gated reserved/no-execution rows with no opcode/decoder/materializer/compiler helper authority.
10. Iteration 09A materialized vector segment/structure memory leaf template/no-emission boundaries for `VZIP`/`VUNZIP`/`VINTERLEAVE`/`VDEINTERLEAVE`, `VLDSEG2`/`VLDSEG4`/`VLDSEG8`, `VSTSEG2`/`VSTSEG4`/`VSTSEG8`, 2D `VLOAD`/`VSTORE`, and indexed+2D `VGATHER`/`VSCATTER`; these remain VLM-gated reserved/no-execution rows with no opcode/decoder/materializer/compiler helper authority and no base-opcode duplication.
11. Iteration 10A materialized vector fixed-point/saturating and prefix-scan leaf template/no-emission boundaries for `VSCAN.MIN`/`VSCAN.MAX`, `VSUB.SAT`/`VMUL.SAT`/`VSLL.SAT`/`VSRL.SAT`/`VSRA.SAT`, and `VAVG`/`VAVG.R`/`VCLIP`; these remain VLM-gated reserved/no-execution rows with no opcode/decoder/materializer/compiler helper authority.
12. Iteration 11A materialized dot/matrix deferral leaf template/no-emission boundaries for `VDOT.BLOCKSCALE`, `VDOT.ACCUM`, `VDOT.WIDE.I16`, `VDOT.WIDE.I32`, `MTILE_LOAD`, `MTILE_STORE`, `MTILE_MACC`, and `MTRANSPOSE`; these remain VLM-gated deferred/no-execution rows with no opcode/decoder/materializer/compiler helper authority and no name-only extension of scoped `VDOT.WIDE`.
13. Iteration 12A materialized Lane6 descriptor op-type and shape/range leaf template/no-emission boundaries for `SUB`/`MIN`/`MAX`/`ABSDIFF`/`CLAMP`, `CONVERT`, `COMPARE`/`SELECT`, explicit reductions, and strided/tiled/scatter-gather/2D/multi-range shapes; these remain descriptor-owned no-execution rows with no scalar opcode/compiler helper authority.
14. For vector, do not add a mnemonic just because a related base opcode is closed. `VectorLegalityMatrix` contour status is the gate.
15. For Lane6, prefer descriptor op-type/shape evolution over opcode growth. Every new op-type must prove owner/domain guard, token admission, staged writes, retire commit, replay, and fail-closed behavior outside the selected contour.
16. For Lane7, topology/queue expansion must stay metadata/control-plane owned until capability authority, queue authority, token authority, execution authority, and commit authority are all explicit.
17. Compiler facade remains frozen/no-emission for runtime-only and reserved contours. Add regression tests before exposing helper methods.
18. Metadata Pass 01A localized scalar deferred leaf metadata for `SEQZ`/`SNEZ`, `CSEL`, `CZERO.NEZ`, address-generation `.UW`, `CLMULH`/`CLMULR`, `CRC32`/`CRC64`, and `ADC`/`SBC`/`ADDC`/`SUBC`; Phase 01E later promoted `CZERO.NEZ`, Phase 01F closed `SEQZ`/`SNEZ` as no-emission facade rows, Phase 03 promoted the address-generation `.UW` pool, Phase 04 promoted `CLMULH`/`CLMULR`, Phase 04 closed CRC/multiprecision ABI gates negative, and `CSEL` remains no-execution because no 4-source carrier is approved.
19. Metadata Pass 01B expanded scalar Iteration 04A leaf metadata for `CPOP`/`POPCNT`, `ANDN`/`ORN`/`XNOR`, scalar `MIN`/`MAX`/`MINU`/`MAXU`, `REV8`/`BREV8`, bitfield `B*`, and `ROLI`/`RORI`; subsequent Phase 01B/01C/01D slices promoted the boolean-invert, scalar min/max, and byte/bit-reverse rows. Phase 01E separately promoted `CZERO.NEZ`; Phase 01A closure promoted canonical `CPOP` while leaving `POPCNT` no-emission; local Phase 02 promoted `ROLI`/`RORI`, register-indexed `BSET`/`BCLR`/`BINV`/`BEXT`, and immediate-indexed `BSETI`/`BCLRI`/`BINVI`/`BEXTI` while keeping compiler helper authority closed. The remaining no-execution rows still have no opcode/compiler helper authority and VMX-neutral generic projection only.
20. Iteration 13A materialized Lane6 queue lifecycle, read-only query, and DSC2 parser-only carrier leaf metadata; these remain no-execution rows with no scalar opcode/compiler helper authority, no host-evidence leak, and future virtualization-boundary policy required before any execution claim.
21. Iteration 14A materialized Lane7 counter/hint leaf metadata for `RDTIME`, `RDINSTRET`, and `PAUSE`; these remain no-execution rows with no opcode/compiler helper authority, replay/retire policy still open, and VMX visible only through generic counter/projection policy if execution is later approved.
