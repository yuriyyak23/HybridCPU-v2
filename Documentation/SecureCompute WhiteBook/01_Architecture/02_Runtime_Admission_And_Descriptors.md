# Runtime Admission And Descriptors

## Root Descriptor

`SecureComputeDomainDescriptor` is the neutral root policy object. `SecureComputeSecurityLevel.None` normalizes to disabled behavior. Root materialization requires a nonzero domain tag, while positive operation admission may require additional subdescriptors.

Descriptor presence is necessary for non-ordinary secure operations but is never sufficient for backend execution, publication or activation.

## Ordinary No-Effect

For `SecureDomainOperationClass.Ordinary`:

- absent descriptor remains allowed;
- disabled descriptor remains allowed;
- unmaterialized descriptor remains allowed;
- enabled descriptor does not over-deny the ordinary stream.

This theorem preserves existing instruction behavior and the compiler no-emission boundary.

## Non-Ordinary Fail-Closed Routing

For every non-ordinary secure operation class, `RuntimeBoundaryAdmissionService` routes through `SecureDomainAdmissionPolicy`.

- missing descriptor -> `SecureDomainBoundaryDenied`;
- disabled descriptor -> `SecureDomainBoundaryDenied`;
- unmaterialized descriptor -> `SecureDomainBoundaryDenied`;
- policy denial reason is retained through the Stage B hook.

The former enabled-descriptor routing guard is absent from the runtime path and retained only as a regression scan pattern.

## Active Descriptor Checks

After root admission, the existing order remains:

1. neutral runtime domain binding;
2. secure descriptor materialization and required subpolicy checks;
3. secure memory domain/address-space binding;
4. operation-specific memory admission;
5. grant, capability and evidence checks;
6. owner-specific policy checks.

Admission success alone sets no backend, completion or retire authority.

## Operation Classes

The fail-closed taxonomy covers secure domain entry, secure memory, evidence creation, completion publication intent, retire side-effect intent, secure I/O, secure hypercall, migration, nested intent and compatibility projection.
