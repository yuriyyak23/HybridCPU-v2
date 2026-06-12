# Position And Authority

## Architectural Position

SecureCompute is a neutral runtime-domain architecture. Its center is descriptor-owned policy and Stage B runtime admission. It is not a virtualization mode and is not an ISA capability extension.

Stage A may classify an operation, but it does not require a secure descriptor for ordinary execution and does not own the secure decision. Stage B joins the neutral runtime context, operation class, descriptor state, domain/address-space binding, grants, evidence and operation-specific policies.

## Authority Hierarchy

Authority is accepted only from neutral runtime owners:

1. runtime domain context;
2. root SecureCompute descriptor;
3. operation-specific descriptor and policy;
4. current typed grants and epochs;
5. bounded evidence or visibility policy;
6. an owner-specific RFC/ADR implementation for any future positive effect.

Compatibility frontends, VMX decode, VMCS fields, `VmxCaps`, VMCALL recognition, trap decisions, telemetry, documentation and tests do not own runtime authority.

## Result Classes

The current implementation deliberately separates:

- no-effect ordinary behavior;
- fail-closed admission denial;
- policy admission with no execution;
- owner proof with no execution;
- read-only compatibility projection;
- future backend execution;
- future completion publication;
- future retire publication;
- production activation.

Passing one class does not imply the next class.

## Current Highest Positive Surfaces

- `GuestCr0` / `GuestCr4`: gated read-only compatibility projection only.
- secure memory: descriptor-owned policy admission only.
- secure I/O/shared buffer: descriptor-owned policy admission only.
- backend owner: `AllowedProofOnlyNoExecution`.
- secure hypercall: `AllowedAdmittedDenied` or fail-closed denial; no backend success.

No current SecureCompute path establishes production backend execution.
