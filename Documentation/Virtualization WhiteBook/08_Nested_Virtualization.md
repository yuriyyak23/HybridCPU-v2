# Nested Virtualization

Nested virtualization is represented by neutral nested domain descriptors and projection services. Nested VMX vocabulary is compatibility vocabulary, not the public nested model.

## Neutral Nested Owners

Relevant paths:

- `CloseToRTL/Core/Runtime/Nested/Descriptors/NestedDomainDescriptor.cs`
- `CloseToRTL/Core/Runtime/Nested/Projection/NestedProjectionService.cs`
- `CloseToRTL/Core/Runtime/Nested/Projection/INestedProjectionService.cs`
- `CloseToRTL/Core/Runtime/Nested/MemoryComposition/**`
- `CloseToRTL/Core/Runtime/Nested/CapabilityFilter/**`
- `CloseToRTL/Core/Runtime/Nested/Policies/NestedEvidencePolicy.cs`
- `CloseToRTL/Core/Runtime/Nested/NestedDomainProjectionCheckpointService.cs`

These files own nested descriptors, nested memory composition, capability filtering, nested evidence policy, and checkpoint projection.

## Compatibility Nested Projection

Relevant VMX-compatible projection paths:

- `CloseToRTL/Core/Virtualization/Compatibility/Generated/VmcsProjection/ShadowVmcsNestedProjectionService.cs`
- `CloseToRTL/Core/Virtualization/Compatibility/Generated/VmcsProjection/NestedDomainControllerCompatibilityProjection.cs`
- `CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/Nested/**`

These files can map neutral nested facts into VMX-shaped exit mappings and shadow-VMCS bridge vocabulary. They must remain projection layers.

## Nested Intercepts

Nested intercept translation uses neutral trap requests and policy bitmaps before projecting a VMX-compatible exit. The projection layer may use `VmExitReason` and `TrapDecision` to describe the compatibility-facing result, but the decision source remains `NeutralTrapResult`.

The correct ordering is:

```text
TrapRequest
  -> L0 mandatory neutral policy
  -> L1 requested neutral policy
  -> NeutralTrapResult
  -> VmxTrapProjectionMapper / nested mapper
  -> VMX-compatible exit mapping
```

## Shadow VMCS Rule

Shadow VMCS support is a compatibility bridge. It must not become a second runtime state store. If a nested feature needs mutable state, that state belongs under neutral nested descriptors or runtime domain services.

## Future Nested Work

The next acceptable nested expansion is a neutral child-intent owner for nested domain actions. It should be admitted and validated before any VMX-compatible nested projection can expose it.
