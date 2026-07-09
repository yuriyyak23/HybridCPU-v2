# 05 — Compiler-to-ISE artifact parity harness

## Goal

Prove that each future production-lowering package is compatible with `HybridCPU_ISE` before callers migrate to it.

This phase establishes decode/encode/lane/slot parity. It should still not introduce production behavior by itself.

## Required parity dimensions

### Opcode identity

The package must prove that the compiler-selected opcode exactly matches the ISE opcode identity. No alias promotion, helper alias, parser alias, or compatibility factory can count as production identity.

### Carrier encoding

The carrier words/bytes emitted by the compiler must match the ISE decode/encode contract.

Required tests:

- compiler carrier -> ISE decode;
- ISE encode -> compiler expected carrier snapshot;
- round-trip where supported;
- byte/word-level golden snapshot.

### Lane ownership

The package must match code-confirmed lane ownership:

- lane 0-3: ALU/vector class-flexible execution;
- lane 4-5: LSU/load-store class-flexible execution;
- lane 6: DSC hard-pinned and MatrixTileStream carrier class;
- lane 7: branch/control, system singleton, VMX projection opcodes, and L7 accelerator opcodes.

Lane ownership is not authority. It is structural compatibility evidence.

### Slot and pinning parity

Tests must verify:

- compiler typed-slot facts remain structural evidence;
- slot/pinning facts match ISE execution profile;
- no compiler slot fact grants runtime legality;
- lane6 DSC and lane7 L7 stay distinct even if both use descriptor-like sideband.

### Runtime bridge parity

Runtime bridge handoff tests must assert that accepted compiler packages still require:

- runtime Legality A;
- runtime Legality B;
- runtime execution;
- runtime publication;
- runtime commit;
- runtime retire.

## Initial parity order

1. Scalar carrier parity.
2. Load/store carrier parity.
3. Branch/control parity.
4. VLOAD/VSTORE helper parity, still helper-only until scoped gate passes.
5. DSC lane6 descriptor-backed carrier parity.
6. L7 ACCEL_SUBMIT descriptor-backed carrier parity.

MatrixTile helper parity can remain as helper ABI parity unless an RFC creates a production candidate.

## Failure policy

Any parity mismatch must fail closed:

- no fallback to another contour;
- no scalar/vector/stream substitute;
- no descriptor-only success;
- no helper success promotion;
- no runtime authority claim.

## Files likely touched

- `HybridCPU_ISE.Tests/CompilerTests/*Parity*Tests.cs`
- `HybridCPU_ISE.Tests/TestHelpers/*Parity*`
- golden artifact test data from phase 02

## Merge gate

- Parity harness exists before production providers.
- All tested packages report runtime authority pending.
- No production caller migration occurs in this phase.

## Rollback

Remove parity tests/harness. Since no production provider consumes the harness yet, rollback is behavior-free.
