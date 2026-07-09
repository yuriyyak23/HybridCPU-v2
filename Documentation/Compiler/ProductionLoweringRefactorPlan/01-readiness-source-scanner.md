# 01 — Production-lowering readiness source scanner

## Goal

Add CI-enforced source scanners before any production-lowering behavior changes. This phase prevents accidental authority strengthening while the production-provider model is introduced.

This phase is intentionally test/scanner-only. It must not change compiler behavior.

## Why this phase comes first

The current codebase already contains obsolete compatibility facades, legacy bool/Try surfaces, helper carrier emission, descriptor parsers, runtime guard observations, and contour shells. Those surfaces are safe only because they are classified as structural evidence, helper ABI, descriptor ABI, or no-emission.

Before production providers exist, CI must reject edits that silently promote those surfaces into production lowering.

## Scanner rules

### Legacy bool/Try/Success/Valid/Accepted surfaces

Reject new production-path reads of legacy names such as:

- `IsAllowed`
- `IsLegal`
- `LegalSlots`
- `TryRecoverFromInstruction`
- `Success`
- `Valid`
- `Accepted`
- raw `bool` public lowering boundaries

Allowed exceptions must be explicit adapter code that produces `CompilerLoweringDecision`, `LegacyApiTranslation`, or structural/evidence-only results.

### Parser/helper non-strengthening

Reject code that treats any of the following as production authority:

- descriptor parser success;
- `CompilerHelperRecoveryStatus.HelperAbiRecovered`;
- `CompilerPositiveEmissionResult<TPlan>` helper emission;
- MatrixTile projection/materialization helper success;
- DSC descriptor ABI acceptance;
- L7 descriptor guard acceptance;
- runtime token or virtual-handle evidence.

The scanner should require text like `HelperAbiOnly`, `ParserOnly`, `DescriptorAbiEvidence`, `CarrierIsNotPublication`, or `RuntimeAuthorityDependency` near adapter code that crosses public boundaries.

### No fallback

Reject new code patterns that imply hidden fallback:

- scalar fallback from vector/matrix/DSC/L7;
- stream fallback from MatrixTile;
- DSC/L7 interchange;
- descriptorless L7 submit;
- raw `CompileInstruction` direct emission of system-device, MatrixTile, VLOAD, or VSTORE opcodes.

### VMX and SecureCompute no-emission

Reject any normal production-lowering PR that introduces:

- VMX carrier emission;
- VMCS compiler-owned state;
- `VmxCaps` as compiler capability authority;
- SecureCompute secure backend carrier emission;
- SecureCompute policy/admission success as execution authority.

Any such change belongs only in a separate RFC branch.

## Files likely touched

- `HybridCPU_ISE.Tests/CompilerTests/*Readiness*Tests.cs`
- optional scanner helper under `HybridCPU_ISE.Tests/TestHelpers/`
- no compiler runtime code.

## Tests that must be green

- all existing Phase 09 negative matrix tests;
- all existing cleanup migration readiness tests;
- new scanner tests.

## Merge gate

- No product behavior changes.
- Scanner has explicit allowlist for current compatibility adapters.
- Scanner fails for representative synthetic bad patterns.

## Rollback

Delete the new scanner tests/helpers. Since this phase is behavior-free, rollback cannot affect runtime ABI.
