# Closed: VmxCaps projection alias helper

Date: 2026-05-24

## Source rule

`VmxCaps` remains only an ABI CSR alias. The source of truth is `CapabilityDescriptorSet`, and writes to `VmxCaps` must not mutate capability state.

## Completed change

- Replaced the empty `VmxCapsProjection` placeholder with a generated-compatibility-style helper.
- Added read projection through `CapabilityPublicationPolicy` and `CapabilityDescriptorSet`.
- Added explicit CSR address binding to the frozen `CsrAddresses.VmxCaps` ABI address.
- Added write evaluation as rejected or compatibility-no-effect without changing descriptor state.

## Validation

- `dotnet build HybridCPU_ISE.csproj --no-restore`
- Result: passed with existing project warnings, `0 Error(s)`.
