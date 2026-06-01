# Current Implementation Map

The current codebase uses `CloseToRTL/Core/Runtime` and `CloseToRTL/Core/Virtualization` as the live implementation surface. Older documents may refer to logical `Core/Runtime` and `Core/VMX`; in this checkout, the active surfaces are the `CloseToRTL` paths.

## Runtime Authority Surface

- `CloseToRTL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs`
- `CloseToRTL/Core/Runtime/Domains/**`
- `CloseToRTL/Core/Runtime/Capabilities/**`
- `CloseToRTL/Core/Runtime/Memory/**`
- `CloseToRTL/Core/Runtime/IO/**`
- `CloseToRTL/Core/Runtime/Lanes/**`
- `CloseToRTL/Core/Runtime/Nested/**`
- `CloseToRTL/Core/Runtime/Events/Traps/**`
- `CloseToRTL/Core/Runtime/Completion/**`
- `CloseToRTL/Core/Runtime/Evidence/**`
- `CloseToRTL/Core/Runtime/Migration/**`
- `CloseToRTL/Core/Runtime/Domains/SecureCompute/**`

These files own neutral authority. They may expose facts to compatibility layers, but they must not depend on VMX vocabulary as source of truth.

## VMX Compatibility Surface

- `CloseToRTL/Core/Virtualization/Compatibility/FrozenAbi/**`
- `CloseToRTL/Core/Virtualization/Compatibility/Frontend/Decode/**`
- `CloseToRTL/Core/Virtualization/Compatibility/Frontend/Handlers/**`
- `CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/**`
- `CloseToRTL/Core/Virtualization/Compatibility/Frontend/Retire/**`
- `CloseToRTL/Core/Virtualization/Compatibility/Generated/**`
- `CloseToRTL/Core/Virtualization/SecureCompute/**`

These files own compatibility names, frozen alias maps, generated schemas, and projection contracts. They may call neutral admission services. They may not own production runtime authority.

The SecureCompute virtualization surface is also compatibility/projection/fence code. It must not grant, activate, checkpoint, or own SecureCompute runtime authority.

## Compiler Boundary Surface

- `CloseToRTL/Core/Virtualization/CompilerBoundary/**`
- `NonRTL/Arch/OpcodeInfo.Registry.Data.System.cs`
- `NonRTL/Arch/InstructionClassifier.cs`
- `NonRTL/Core/Diagnostics/InstructionRegistry.*`
- `NonRTL/Core/Pipeline/InternalOpBuilder.cs`

The compiler and diagnostic surfaces may classify VMX opcodes and produce compatibility payloads. Classification is not backend authorization.

## Conformance Surface

- `CloseToRTL/Core/Virtualization/Conformance/**`
- `HybridCPU_ISE.Tests/VmxRefactoring/**`

The conformance tree contains static contracts, no-emission fences, generated parity checks, authority-boundary checks, and the current VMX refactoring tests. Some conformance files intentionally mention forbidden manager names as strings to prevent their return; those mentions are evidence, not implementation.

## Important Path Translation

When a document says `Core/Runtime/Domains`, read it as the logical runtime owner. In this checkout, the concrete implementation lives under `CloseToRTL/Core/Runtime/Domains`. When a document says `Core/VMX/Compatibility`, read it as the logical VMX compatibility frontend. In this checkout, the concrete implementation lives under `CloseToRTL/Core/Virtualization/Compatibility`.

## Current Empty / Removed Surfaces

The physical legacy VMX backend surface is absent from the active model. The expected absence set is:

- `Legacy/VMX` has no production C# authority, or the path is absent.
- `VmxExecutionUnit.cs` is absent.
- `VmcsManager.cs` is absent.
- `IVmcsManager.cs` is absent.
- VMCS runtime manager names appear only in quarantine/conformance text when they are used as forbidden string probes.
