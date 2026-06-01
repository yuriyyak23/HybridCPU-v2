# Phase 01F - Zero-Compare Facade Decision Gate

Date: 2026-05-26

## Slice

Sixth and final Phase 01 mini-group: `SEQZ` and `SNEZ`.

This package does not advance scalar select hardware rows, bitfield, compiler
helper, VMX-specific, vector, Lane6, or Lane7 work.

## Production Path Overlay

`SEQZ` and `SNEZ` are resolved as facade-only/no-emission rows in this package.
No hardware opcode, decoder row, `InstructionIR` projection, materializer,
typed MicroOp, compiler helper, or hidden lowering is opened. A future hardware
package would need the scalar full production path in `README.md`. VMX sees only
the generic runtime model; no VMX-specific handler is added.

## Current Status

`SEQZ` and `SNEZ` are CloseToRTL facade-only metadata rows. Their leaf metadata
records possible zero-compare hardware-candidate outcomes, but those constants
are not runtime evidence and do not authorize compiler emission.

The current rows remain `FacadeOnlyNoEmissionClosed` templates and have:

- no opcode allocation;
- no decoder or encoder ABI;
- no `InstructionIR` projection;
- no registry/materializer row;
- no typed scalar MicroOp publication;
- no dispatch/capture path;
- no retire writeback proof;
- no replay/rollback/conformance artifacts;
- no compiler helper emission authority.

`InstructionSupportStatusCatalog` keeps both mnemonics as `Reserved` with
`RuntimeInstructionEvidence.None`.

## Decision

This iteration chooses facade-only/no-emission for both mnemonics:

- no hidden compiler lowering is authorized;
- no public helper/facade method is authorized;
- hardware treatment remains possible only in a later full scalar production
  package;
- boolean result canonicalization remains local hardware-candidate metadata
  only;
- facade policy cannot be counted as runtime evidence for either mnemonic.

## Evidence Gate State

| Gate | State |
|---|---|
| status/catalog | Preserved as reserved/no-emission; no executable claim |
| opcode value or facade decision | Facade-only/no-emission selected; no opcode allocated |
| decoder/encoder ABI | No decoder/encoder row; future hardware form requires external production package |
| InstructionIR/projection | No IR projection; no hidden lowering projection |
| registry/materializer | No registry/materializer row |
| typed MicroOp / object | No runtime MicroOp; local object remains metadata-only |
| execute/capture semantics | Local hardware-candidate 0/1 helpers only; no runtime execute/capture |
| retire/writeback | No retire path; future hardware form would require x0 discard evidence |
| replay/rollback/conformance | No runtime replay; future hardware form needs zero/nonzero/x0 tests |
| golden/no-emission | Existing no-emission boundary remains authoritative and hidden lowering stays forbidden |

## Production Follow-On Rules

- Do not add `Opcode` or `Execute` to `SeqzInstruction` or `SnezInstruction`
  under this facade-only decision.
- Do not infer executable support from `ParameterDescriptor` or `MicroOpShape`
  metadata.
- Add compiler helper emission or lowering only by explicit helper authority.
- Do not treat facade/lowering policy as runtime evidence.
- Add VMX-compatible projection only if a virtualization boundary is crossed.

## Phase 01 Closure Note

With `PHASE_01A` through `PHASE_01F`, every Phase 01 candidate now has either
runtime closure or an explicit no-runtime-evidence decision gate. Hardware or
facade promotion is allowed later only through the full production path and
matching authority decision for opcode, decoder, materializer, runtime, retire,
replay, golden, and compiler-emission evidence.
