# Runtime Admission And Descriptors

## Root Descriptor

`SecureComputeDomainDescriptor` is the current root policy object. `SecureComputeSecurityLevel.None` normalizes to disabled behavior. It has public constructors and no registry-controlled lifecycle. A full descriptor may arrive from `DomainRuntimeContext.SecureCompute` or request-supplied `SecureDescriptor`; therefore descriptor identity and the sole materialization/revocation owner are open C0 blockers.

Descriptor presence is necessary for non-ordinary secure operations but is never sufficient for backend execution, publication or activation.

## Ordinary No-Effect

For `SecureDomainOperationClass.Ordinary`:

- absent descriptor remains allowed;
- disabled descriptor remains allowed;
- unmaterialized descriptor remains allowed;
- enabled descriptor does not over-deny the ordinary stream.

These admission-level tests do not prove observational equivalence of pipeline, SMT/FSP, memory, I/O, exception and retire traces.

## Non-Ordinary Fail-Closed Routing

For direct calls with a non-ordinary caller-selected operation class, `RuntimeBoundaryAdmissionService` routes through `SecureDomainAdmissionPolicy`.

- missing descriptor -> `SecureDomainBoundaryDenied`;
- disabled descriptor -> `SecureDomainBoundaryDenied`;
- unmaterialized descriptor -> `SecureDomainBoundaryDenied`;
- policy denial reason is retained by the generic service result.

This is policy behavior, not proof that the CPU decode/issue path supplies canonical operation identity or calls the service. Observed VMX production callers use the default ordinary operation class.

## Active Descriptor Checks

The intended order after registry lookup and certificate verification is:

1. neutral runtime domain binding;
2. secure descriptor materialization and required subpolicy checks;
3. secure memory domain/address-space binding;
4. operation-specific memory admission;
5. grant, capability and evidence checks;
6. owner-specific policy checks.

Admission success alone sets no backend, completion or retire authority. A logical `Validated`/`Authorized` flag is not a certificate.

## Operation Classes

The enum names secure domain entry, secure memory, evidence creation, completion publication intent, retire side-effect intent, secure I/O, secure hypercall, migration, nested intent and compatibility projection. Only secure I/O and secure hypercall have special dispatch in `SecureDomainAdmissionPolicy`; other non-ordinary values can reach a generic positive result. Exhaustive deny-by-default dispatch is therefore open.
