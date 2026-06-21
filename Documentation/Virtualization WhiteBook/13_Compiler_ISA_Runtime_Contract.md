# Compiler, ISA, And Runtime Contract

The compiler and ISA layers may expose VMX-compatible opcodes and metadata, but they do not authorize virtualization backend behavior.

## ISA Position

VMX opcodes remain part of the frozen compatibility ISA vocabulary. They can be classified, encoded, decoded, and tested as VMX-compatible instructions. This preserves ABI compatibility and instruction inventory stability.

However, a VMX opcode is not a runtime operation by itself. The opcode must cross:

- decode validation;
- compatibility projection validation;
- runtime boundary admission;
- neutral owner evaluation;
- completion and retire publication.

## Compiler Boundary

Relevant compiler-boundary paths:

- `CloseToRTL/Core/Virtualization/CompilerBoundary/IR/VirtualizationCompilerIntent.cs`
- `CloseToRTL/Core/Virtualization/CompilerBoundary/Lowering/VirtualizationLoweringBoundary.cs`
- `CloseToRTL/Core/Virtualization/CompilerBoundary/LaneBinding/VirtualizationLaneBindingPolicy.cs`
- `CloseToRTL/Core/Virtualization/CompilerBoundary/Scheduling/VirtualizationSystemSingletonSchedulingRule.cs`
- `CloseToRTL/Core/Virtualization/CompilerBoundary/Bundling/VirtualizationBundleLegalityRule.cs`
- `CloseToRTL/Core/Virtualization/CompilerBoundary/NoEmission/VirtualizationNoEmissionRegressionGate.cs`

These boundaries prevent the compiler from turning compatibility names into direct runtime mutations.

## Non-VMX Instructions

New non-VMX instructions should integrate with the general HybridCPU legality and execution chain:

```text
ISA metadata
  -> decoder
  -> internal op / micro-op materialization
  -> legality and typed-slot scheduling
  -> runtime owner
  -> retire and evidence
```

They should not be routed through VMX simply because they interact with virtualization-like domains. VMX matters only when a compatibility boundary is explicitly crossed.

## VMX Instructions

VMX-compatible instructions follow:

```text
ISA metadata / opcode classification
  -> VmxInstructionPayload
  -> VmxCompatDecodeBoundary
  -> VmxCompatProjectionService
  -> RuntimeBoundaryAdmissionService
  -> neutral owner
  -> denied or projected completion
```

The compiler can produce payloads and enforce no-emission gates. It cannot assert backend success.

## Retire ABI Compatibility

`VmxRetireEffect` and `VmxRetireOutcome` are retained compatibility retire vocabulary. They are useful for ABI stability and for fail-closed effect modelling. They are not proof that the runtime owner admitted a successful backend transition.

Production callers must not treat VMX success factories as runtime authority unless the operation explicitly passed neutral runtime ownership and publication fences.

## No-Emission Regression Rule

No-emission gates are part of the compiler/runtime contract. If a VMX-compatible operation is not admitted through neutral authority, the compiler must not quietly generate a backend execution path for it.

## Practical Guidance

When adding a virtualization-related instruction:

1. Define ISA metadata and decode behavior.
2. Identify the neutral runtime owner.
3. Define capability and evidence requirements.
4. Add runtime admission.
5. Add neutral completion/retire publication.
6. Add compatibility projection only after the neutral path exists.
7. Add conformance proving the frontend cannot bypass the neutral owner.
