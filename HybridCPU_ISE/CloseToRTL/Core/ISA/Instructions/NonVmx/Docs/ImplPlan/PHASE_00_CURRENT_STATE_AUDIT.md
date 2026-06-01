# Phase 00 - Current State Audit

## Goal

Maintain the implementation map as production slices close. This phase
classifies all `NonVmx` partial instruction/contour classes by evidence status
and records which production path profile each remaining row must use.

## Production Path Overlay

Phase 00 no longer blocks executable work. It is the routing ledger: every row
must point at one of the full production path profiles in `README.md` before it
is promoted. Ordinary scalar/vector Non-VMX rows use generic legality,
execution, retire, replay, projection, and no-emission evidence; VMX-specific
paths are required only for rows that cross an explicit virtualization boundary.

## Current Classification

| Group | Instructions / contours | Current status |
|---|---|---|
| Executable CloseToRTL scalar objects | `CTZ`; `SEXT.B`, `SEXT.H`, `ZEXT.H`; `ROL`, `ROR`, `ROLI`, `RORI`; `BSET`, `BCLR`, `BINV`, `BEXT`; `BSETI`, `BCLRI`, `BINVI`, `BEXTI`; `ANDN`, `ORN`, `XNOR`; `MIN`, `MAX`, `MINU`, `MAXU`; `REV8`, `BREV8`; `CZERO.NEZ`; `CPOP`; `SH2ADD`, `SH3ADD`, `ADD.UW`, `SH1ADD.UW`, `SH2ADD.UW`, `SH3ADD.UW`, `SLLI.UW`; `CLMULH`, `CLMULR` | Have opcode references, typed CloseToRTL objects, `Execute` methods, and prior conformance evidence. Do not duplicate. |
| Scalar metadata-only / no-emission | `POPCNT`, `SEQZ`/`SNEZ`, `CSEL`, `CRC32`/`CRC64`, `ADC`/`SBC`/`ADDC`/`SUBC` | Leaf metadata exists; no opcode/materializer/runtime authority. |
| Reserved scalar rows | CRC, multi-precision, and selected facade/system anchors | Explicit reserved/no-emission rows that still need full production evidence before execution. |
| Descriptor-only Lane6 | `DmaStreamCompute.SUB/MIN/MAX/ABSDIFF/CLAMP/CONVERT/COMPARE/SELECT/REDUCE_*`; `DSC_SHAPE_*` contours | Descriptor-owned; not scalar opcodes. |
| Lane6 queue/query/carrier | `DSC_POLL`, `DSC_WAIT`, `DSC_CANCEL`, `DSC_FENCE`, `DSC_COMMIT`, `DSC_QUERY_BACKEND`, `DSC_QUERY_SHAPE`, `DSC2` | No execution; queue/token/capability/DSC2 ABI still external. |
| Vector VLM-gated contours | Predicate/select, widen/narrow/convert, structure movement, segment memory, 2D/indexed+2D memory, fixed-point/saturating, dot/matrix | Fail-closed until VLM and full execution evidence close. |
| Lane7 counter/hint/control-plane | `RDTIME`, `RDINSTRET`, `PAUSE`, `SFENCE.VMA`, cache maintenance, IOMMU maintenance, accelerator control | Privilege, host-evidence, replay, and virtualization boundaries remain open. |
| Aggregate partial metadata | `NonVmxVectorDeferredTemplates.cs`, `NonVmxVectorMemoryDeferredTemplates.cs`, `NonVmxLane07DeferredTemplates.cs` | Compatibility/template metadata may overlap leaf files; audit before removal or split. |

## Existing Partial Files

- Scalar: `Lanes00_03Scalar\...`.
- Vector: `Lanes00_03Vector\...`.
- Memory vector contours: `Lanes04_05Memory\...`.
- Lane6: `Lane06DmaStream\...`.
- Lane7: `Lane07SystemControl\...`.
- Existing docs: `Docs\NON_VMX_CLOSE_TO_RTL_IMPLEMENTATION_PLAN.md` and iteration snapshots.

## New Partial Files Allowed Later

- Production slices may add local partials such as `*.LocalSemantics.cs`,
  `*.Legality.cs`, `*.Capture.cs`, or `*.RetireContract.cs` next to the
  existing leaf classes.
- If a row moves beyond metadata, it must also update the full runtime/test
  path profile named in `README.md`.

## Local CloseToRTL Work

- Verify class/file inventory and duplicate aggregate-vs-leaf metadata.
- Add documentation, local metadata cross-checks, and future local partial naming conventions.
- Identify pure scalar semantics that can be represented locally without changing decoder/runtime yet.

## Production Evidence Gates

Opcode allocation, descriptor op-type allocation, decoder/encoder ABI, `InstructionIR` projection, materializer registration, typed MicroOp publication, runtime dispatcher/capture, retire engine changes, replay/rollback tests, golden artifacts, and compiler no-emission tests outside Core.

## Metadata Constants

Existing constants such as `Mnemonic`, `EvidenceBoundary`, `HasOpcodeAllocation=false`, `HasScalarOpcodeAllocation=false`, `IsExecutable=false`, `CompilerHelperAllowed=false`, `RequiresVectorLegalityMatrixClosure`, `NoBaseOpcodeDuplication`, `NoGuestVisibleHostEvidence`, and VMX-neutral markers are ABI/evidence markers. Do not change them without an explicit status/catalog decision.

## Evidence Chain Stories

- Decoder/encoder ABI: audit current coverage and point each open row at the production package that will add decoder/encoder evidence.
- InstructionIR/projection: audit current coverage and record the production package that will add missing projections.
- Typed MicroOp/materializer: audit current coverage and record the production package that will add missing materializers.
- Execute/capture semantics: identify the production package that owns execution/capture closure.
- Retire/writeback/side effects: identify register writeback, staged publication, staged commit, or owned side-effect needs.
- Replay/rollback/conformance: list required evidence per phase; no tests are added here.

## Special Boundaries

- Vector VLM gate: every vector row remains fail-closed unless a future phase explicitly closes VLM evidence.
- Lane6 sideband: descriptor op-types and shapes stay descriptor-owned and cannot become scalar ISA opcodes.
- Lane7 boundary: host evidence, privilege, cache/TLB/IOMMU, accelerator, and counter sources cannot become guest architectural state without policy.
- No-emission: metadata-only and reserved rows remain closed to compiler helper emission.

## Risks

- Aggregate partial files can mask leaf metadata or create duplicate constants.
- Mnemonic or enum presence can be mistaken for execution evidence.
- Closed scalar rows can be accidentally reimplemented while opening adjacent reserved rows.

## Closure Criteria

- Inventory groups above are accepted as the starting map.
- Each phase points to exact leaf files, production path profile, and evidence gates.
- Phase 00 itself is a routing ledger; implementation happens in the phase package that closes the full path.

## Prohibited Actions

- Add opcode values, decoder paths, materializers, compiler helpers, runtime execution, VMX-compatible projection, or VLM openings only from the phase package that closes the matching full production path.
- Do not mark metadata-only, reserved, parser-only, descriptor-only, or sideband-only rows as implemented.
