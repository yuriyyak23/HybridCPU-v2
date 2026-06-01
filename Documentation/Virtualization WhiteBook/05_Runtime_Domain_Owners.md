# Runtime Domain Owners

The runtime domain layer is the virtualization substrate. VMX compatibility code can project from it, but the ownership lives here.

## Execution Domain

Execution domain descriptors own guest-visible execution state, scheduling policy, execution extension descriptors, bundle legality, event queue linkage, evidence policy, and completion route metadata.

Relevant paths:

- `CloseToRTL/Core/Runtime/Domains/Descriptors/ExecutionDomain/**`
- `CloseToRTL/Core/Runtime/Domains/Admission/Execution/ExecutionDomainRuntime.cs`
- `CloseToRTL/Core/Runtime/Domains/Legality/DomainLegalityService.cs`
- `CloseToRTL/Core/Runtime/Domains/Scheduling/DomainSchedulingAdmission.cs`

VMX may project `GuestPc`, `GuestSp`, and `GuestFlags` only from a materialized `ExecutionDomainReadOnlyStateView`. Control-like fields such as `GuestCr0` and `GuestCr4` remain denied until a neutral privileged execution-state owner defines real semantics. Host execution aliases remain denied until a separate neutral host-execution owner exists.

## Memory Domain

Memory domain descriptors and services own address spaces, translation, nested page walking, dirty tracking, and invalidation. VMX names such as EPT, VPID, INVEPT, and INVVPID are compatibility aliases over neutral memory-domain behavior.

Relevant paths:

- `CloseToRTL/Core/Runtime/Memory/AddressSpaces/**`
- `CloseToRTL/Core/Runtime/Memory/Translation/**`
- `CloseToRTL/Core/Runtime/Memory/Invalidation/**`
- `CloseToRTL/Core/Runtime/Memory/DirtyTracking/**`
- `CloseToRTL/Core/Runtime/Domains/Admission/Memory/MemoryDomainRuntime.cs`

The VMCS projection schema maps fields such as `GuestCr3`, `EptPointer`, `Vpid`, `Cr3TargetCount`, and translation fault fields to descriptor or completion owners. The mapping is projection metadata, not a memory owner.

`HostCr3` is not sourced from the guest/domain translation view. It remains denied until a neutral host-address-space owner exposes a read-only host root.

## I/O Domain

I/O and DMA authority is neutral. IOMMU and DMA descriptors control windows, bindings, invalidation, and authority checks. VMX-compatible I/O names can be aliases only after this owner has admitted the operation.

Relevant paths:

- `CloseToRTL/Core/Runtime/IO/Dma/**`
- `CloseToRTL/Core/Runtime/IO/Iotlb/**`
- `CloseToRTL/Core/Runtime/Memory/Iommu/**`
- `CloseToRTL/Core/Runtime/Domains/Admission/IO/IoDomainRuntime.cs`

## Event, Trap, And Completion Owners

Trap and completion ownership is split from VMX exit vocabulary:

- trap requests are neutral `TrapRequest`;
- trap policy is neutral `TrapPolicyDescriptor` plus `TrapPolicyBitmap`;
- trap result is neutral `NeutralTrapResult`;
- completion publication is neutral `TrapCompletionPublicationFence`;
- route publication is neutral `TrapCompletionRouteService`;
- hypercall backend admission is neutral `HypercallBackendAdmissionService`;
- VMX exit vocabulary is a later projection.

Relevant paths:

- `CloseToRTL/Core/Runtime/Events/Traps/**`
- `CloseToRTL/Core/Runtime/Completion/**`

## Runtime Context

`DomainRuntimeContext` is the admission context. A compatibility frontend without a domain runtime context cannot cross the boundary. This is why compatibility artifacts cannot stand alone as runtime authority.

## Owner Rule

For every virtualization feature, ask: what neutral runtime descriptor owns the fact? If the only available answer is a VMX field, VMCS block, VMX CSR, or VMX opcode, the feature is still a projection or denied alias, not production authority.
