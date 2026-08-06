# Position And Authority

## Architectural Position

SecureCompute is intended as a neutral runtime-domain architecture. Current code contains descriptor-shaped policy and a generic runtime admission service; it does not yet prove CPU Stage-B integration. It is not a virtualization mode and is not an ISA capability extension.

The required architecture is a canonical decode/operation identity followed by SafetyVerifier issuance of an immutable certificate and a production issue carrier. That chain is absent. Existing generic admission accepts a caller-selected operation class and can obtain a full descriptor from either the runtime context or the request.

## Authority Hierarchy

Future authority may be accepted only from neutral runtime owners:

1. a canonical descriptor registry with opaque binding and one lifecycle owner;
2. SafetyVerifier-issued operation/domain/VT/slot/epoch/effects certificate;
3. operation-specific policy with exhaustive deny-by-default taxonomy;
4. a runtime-owned mint/revoke grant ledger;
5. a named production effect owner and typed result;
6. separately owned completion, retire, migration and evidence publication paths.

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

No current SecureCompute path establishes production backend execution. Policy classes, caller booleans, documentation and source-string checks are not authority or reachability proof.
