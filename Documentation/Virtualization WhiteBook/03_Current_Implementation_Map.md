# Current Implementation Map

The current codebase uses `CloseToHSL/Core/Runtime` and `CloseToHSL/Core/Virtualization` as the live implementation surface. Older documents may refer to logical `Core/Runtime` and `Core/VMX`; in this checkout, the active surfaces are the `CloseToHSL` paths.

Development sequencing and current/future classification are owned by `HybridCPU_ISE/docs/ref2/VirtualizationActivationPlan/`. This implementation map must stay consistent with that plan.

## Runtime Authority Surface

- `CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs`
- `CloseToHSL/Core/Runtime/Domains/**`
- `CloseToHSL/Core/Runtime/Capabilities/**`
- `CloseToHSL/Core/Runtime/Memory/**`
- `CloseToHSL/Core/Runtime/IO/**`
- `CloseToHSL/Core/Runtime/Lanes/**`
- `CloseToHSL/Core/Runtime/Nested/**`
- `CloseToHSL/Core/Runtime/Events/Traps/**`
- `CloseToHSL/Core/Runtime/Completion/**`
- `CloseToHSL/Core/Runtime/Evidence/**`
- `CloseToHSL/Core/Runtime/Migration/**`
- `CloseToHSL/Core/Runtime/Domains/SecureCompute/**`

These files own neutral authority. They may expose facts to compatibility layers, but they must not depend on VMX vocabulary as source of truth.

## VMX Compatibility Surface

- `CloseToHSL/Core/Virtualization/Compatibility/FrozenAbi/**`
- `CloseToHSL/Core/Virtualization/Compatibility/Frontend/Decode/**`
- `CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/**`
- `CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/**`
- `CloseToHSL/Core/Virtualization/Compatibility/Frontend/Retire/**`
- `CloseToHSL/Core/Virtualization/Compatibility/Generated/**`
- `CloseToHSL/Core/Virtualization/SecureCompute/**`

These files own compatibility names, frozen alias maps, generated schemas, and projection contracts. They may call neutral admission services. They may not own production runtime authority.

The SecureCompute virtualization surface is also compatibility/projection/fence code. It must not grant, activate, checkpoint, or own SecureCompute runtime authority.

## Compiler Boundary Surface

- `CloseToHSL/Core/Virtualization/CompilerBoundary/**`
- `NonRTL/Arch/OpcodeInfo.Registry.Data.System.cs`
- `NonRTL/Arch/InstructionClassifier.cs`
- `NonRTL/Core/Diagnostics/InstructionRegistry.*`
- `NonRTL/Core/Pipeline/InternalOpBuilder.cs`

The compiler and diagnostic surfaces may classify VMX opcodes and produce compatibility payloads. Classification is not backend authorization.

## Conformance Surface

- `CloseToHSL/Core/Virtualization/Conformance/**`
- `HybridCPU_ISE.Tests/VmxRefactoring/**`

The conformance tree contains static contracts, no-emission fences, generated parity checks, authority-boundary checks, and the current VMX refactoring tests. Some conformance files intentionally mention forbidden manager names as strings to prevent their return; those mentions are evidence, not implementation.

## Important Path Translation

When a document says `Core/Runtime/Domains`, read it as the logical runtime owner. In this checkout, the concrete implementation lives under `CloseToHSL/Core/Runtime/Domains`. When a document says `Core/VMX/Compatibility`, read it as the logical VMX compatibility frontend. In this checkout, the concrete implementation lives under `CloseToHSL/Core/Virtualization/Compatibility`.

## Current Empty / Removed Surfaces

The physical legacy VMX backend surface is absent from the active model. The expected absence set is:

- `Legacy/VMX` has no production C# authority, or the path is absent.
- `VmxExecutionUnit.cs` is absent.
- `VmcsManager.cs` is absent.
- `IVmcsManager.cs` is absent.
- VMCS runtime manager names appear only in quarantine/conformance text when they are used as forbidden string probes.
