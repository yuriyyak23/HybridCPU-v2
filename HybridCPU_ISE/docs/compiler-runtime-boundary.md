# Compiler/Runtime Boundary

## Scope

This document fixes the present compiler/frontend/runtime authority boundary for
the landed repository. It does not introduce a stricter contract than the live
code currently enforces.

## Fail-Closed Ingress

The handshake boundary is fail-closed.

- `CompilerContract.Version` is the canonical ingress version surface.
- Producers must call `DeclareCompilerContractVersion(...)` before typed-slot
  facts or instruction recording are accepted.
- `ProcessorCompilerBridge.EnsureCompilerContractHandshake(...)` rejects
  missing, duplicate, or mismatched declarations before the bridge treats
  compiler structure as usable input.

This fail-closed handshake is separate from typed-slot fact staging. The current
repository does not soften handshake enforcement merely because fact handling is
still staged.
This is the only fail-closed claim made for the current mainline
compiler/runtime boundary. It does not mean that missing typed-slot facts halt
canonical execution.

## Typed-Slot Fact Staging Versus Bridge Policy

Two related but distinct surfaces are active today.

- `TypedSlotFactStaging.CurrentMode == ValidationOnly` is the typed-slot staging
  surface inside `SafetyVerifier.TypedSlot.cs` and related compiler-emission
  helpers.
- `CompilerContract.CurrentTypedSlotPolicy.Mode == CompatibilityValidation` is
  the active bridge policy after a successful handshake.

That split has concrete consequences.

- Missing facts remain compatible with canonical execution.
- Present facts may be validated against the runtime bundle.
- Present facts act as structured handoff metadata plus validation/quarantine
  evidence unless a stricter policy is selected.
- Structural disagreement may be recorded and quarantine-logged as agreement
  evidence.
- `StrictVerification` is an explicit stronger verification mode, not the
  default.
- `RequiredForAdmission` remains a future seam rather than a selectable current
  mainline policy.

## What Compiler Evidence Does And Does Not Mean

Compiler-side admissibility evidence is not runtime legality authority.

- `HybridCpuBundleBuilder.Classify(...)` and emitted
  `TypedSlotBundleFacts` express compiler-side structural admissibility.
- `IrAdmissibilityAgreement` and
  `HybridCpuCompiledProgram.AdmissibilityAgreement` summarize agreement and
  staging status.
- These surfaces are build-time and ingress-time evidence. They are not the
  active runtime admission authority, and they do not override a later runtime
  rejection.

The compiler may therefore be fact-strict in preflight while runtime still
permits canonical execution without mandatory fact presence. Present facts
remain structured handoff, validation, and quarantine metadata unless a
stricter policy is explicitly selected.

## What Runtime Still Owns

Runtime remains authoritative for the live admission decision.

- `IRuntimeLegalityService` and `LegalityDecision` remain the legality
  authority seam.
- Runtime still owns owner/domain guards, replay-sensitive legality reuse,
  class admission, lane materialization, and dynamic reject surfaces.
- The compiler-side admissibility taxonomy and the runtime reject taxonomy are
  related surfaces, not one flattened shared enum.

The safe repository-facing statement is therefore narrow: compiler facts stage,
characterize, and validate candidate bundles, but runtime legality remains the
authority for actual admission.

In short, runtime legality remains the authority.

## Repository-Facing Non-Claims

This document does not claim that:

- `IrAdmissibilityAgreement` authorizes runtime admission;
- missing typed-slot facts currently fail the canonical runtime path;
- compiler-side structural agreement is the same thing as runtime legality;
- the future `RequiredForAdmission` seam is already active.

## Code And Proof Surfaces

Primary code authority:

- `HybridCPU_ISE/Core/Contracts/CompilerContract.cs`
- `HybridCPU_ISE/Processor/Core/Processor.CompilerBridge.cs`
- `HybridCPU_ISE/Core/Pipeline/Safety/SafetyVerifier.Types.cs`
- `HybridCPU_ISE/Core/Pipeline/Safety/SafetyVerifier.TypedSlot.cs`
- `HybridCPU_Compiler/Core/IR/Admission/HybridCpuBundleBuilder.cs`
- `HybridCPU_Compiler/Core/IR/Model/HybridCpuCompiledProgram.cs`
- `HybridCPU_Compiler/Core/IR/Model/IrAdmissibilityAgreement.cs`

Representative proof surfaces:

- `HybridCPU_ISE.Tests/tests/Phase12CompilerContractHandshakeTests.cs`
- `HybridCPU_ISE.Tests/PhasingAndExtensions/Stage7AgreementTests.cs`
- `HybridCPU_ISE.Tests/tests/Phase09TypedSlotFactStagingDocumentationTests.cs`
