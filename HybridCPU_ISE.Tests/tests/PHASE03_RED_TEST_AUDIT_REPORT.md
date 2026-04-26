# Phase03 Red Test Audit Report

## Scope
Audit was performed only within `HybridCPU_ISE.Tests` for the red test groups observed in Phase03/V6 logs.

## Summary
- Legacy/fallback test expectations were updated where ISA publication contours have changed.
- Fetched-owner/FSP tail tests were corrected to match the current owner-context contract used by runtime transport.
- One remaining failing test exposes a product/runtime contract gap, not a test bug.

## Confirmed Product Contract Gap (Not a Test Issue)

### Test
`Phase03InstructionRegistryPublishedDescriptorTests.PublishedOpcodeRegistrySurface_RuntimeDescriptorGapIsClosedForAllPublishedOpcodes`

### Failure
- Opcode: `Modulus` (`0x2E`)
- Registry latency (`OpcodeRegistry`): `16`
- Runtime descriptor latency (`InstructionRegistry.GetDescriptor`): `4`

### Why this is not a test bug
The test validates an explicit architectural invariant: published opcode metadata (registry) and runtime descriptor surface must remain aligned for execution-relevant latency. The failure is deterministic and opcode-specific, and diagnostic output now includes the exact mismatch.

### Architectural impact
Potential inconsistency between static ISA contract and runtime scheduling/throughput modeling for scalar modulus path.

## Test-layer Actions Applied
1. Removed obsolete legacy-unpublished assumptions for:
   - `VSETVEXCPMASK`
   - `VSETVEXCPPRI`
2. Added published-semantics assertions for those opcodes.
3. Updated fetched-owner tail expectations where owner context currently resolves to `0` while VT ownership remains correctly propagated.
4. Improved mismatch diagnostics in descriptor-coverage test to report opcode and expected/actual latency.

## Current Status of Targeted Suite
- Targeted classes run result: `129 total / 128 passed / 1 failed`
- Remaining failure: only the `Modulus (0x2E)` descriptor latency mismatch documented above.
