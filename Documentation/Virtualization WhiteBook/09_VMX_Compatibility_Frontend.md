# VMX Compatibility Frontend

The VMX compatibility frontend preserves the frozen VMX ABI surface while refusing to become the virtualization architecture.

## Frontend Layers

The current frontend is split into:

- frozen ABI names under `Compatibility/FrozenAbi`;
- decode boundary under `Compatibility/Frontend/Decode`;
- admission handlers under `Compatibility/Frontend/Handlers`;
- projection services under `Compatibility/Frontend/Projection`;
- retire vocabulary under `Compatibility/Frontend/Retire`;
- generated alias and projection schemas under `Compatibility/Generated`.

The frontend can decode and classify VMX-compatible operations. It cannot directly perform backend virtualization state transitions.

## Frozen ABI Vocabulary

Frozen ABI vocabulary includes:

- VMX opcode aliases;
- VMX operation kinds;
- VMCS field aliases;
- VMX CSR aliases;
- VMX function leaves;
- invalidation scopes;
- `VmExitReason`;
- `VmxExitQualification`.

This vocabulary is stable because external compatibility may depend on the names and numeric values. Stability does not imply authority.

## Decode Boundary

`VmxCompatDecodeBoundary` admits or denies a compatibility payload using descriptor, capability, scheduling, and no-emission validation flags. A successful decode only means the operation is a valid compatibility request shape.

Decode does not mean:

- runtime admission;
- domain authorization;
- backend execution;
- completion publication;
- retire publication.

## Projection Boundary

`VmxCompatProjectionService` validates whether a frozen alias can be projected. It uses generated alias metadata such as `CompatAliasMap`.

Projection validation is separate from runtime admission. This allows the frontend to say "this is a known VMX compatibility alias" without saying "this operation may mutate runtime state."

## Admitted Projection Paths

The current frontend has admitted projection paths:

- VMREAD projection can pass decode, alias projection, runtime admission, generated schema owner lookup, alias/evidence checks, and neutral owner value projection for the currently admitted field slices.
- VMCALL trap projection can pass decode, alias projection, runtime admission, neutral trap policy, backend admission evaluation, and route evaluation, but backend execution, completion publication, and retire publication remain denied in production. The frontend continues to use `ProjectionOnlyDenied`; it does not use either runtime-owned positive route descriptor.

This is intentional. It gives the project evidence that the frontend can cross the runtime boundary correctly without silently enabling a backend. VMREAD success means read-only compatibility projection from a neutral value source; it does not mean VMCS backend execution.

## Explicit Denials

Current explicit denials include:

- `GuestCr0` and `GuestCr4`: no neutral privileged execution-state semantics;
- host execution aliases: no neutral host-execution owner;
- `HostCr3`: no neutral host-address-space owner;
- compatibility-control fields: no admitted neutral control-bit value contract;
- VMWRITE: no neutral write owner;
- VMCALL backend success: no neutral hypercall backend owner.

## Frontend Result Mapper

VMX-facing result mappers translate neutral or compatibility admission outcomes into ABI-visible results. They are presentation layers. They must not be used by production callers as proof of runtime success unless a neutral runtime owner and publication fence have authorized success.

## Retained Surface

Retained VMX frontend surface is compatibility-only:

- `VmxInstructionPayload`
- `VmxRetireEffect`
- `VmxRetireOutcome`
- `VmxOperationKind`
- VMCS field schemas and aliases
- VMX capability projection
- shadow VMCS compatibility bridge
- VMX invalidation aliases

Any future expansion must keep this retained surface from becoming runtime authority.
