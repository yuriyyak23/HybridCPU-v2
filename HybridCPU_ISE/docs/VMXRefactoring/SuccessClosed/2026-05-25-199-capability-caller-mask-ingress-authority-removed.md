# 2026-05-25-199 Capability Caller Mask Ingress Authority Removed

Date: 2026-05-25

Status: closed

## Rule / Basis

VMX is a frozen compatibility frontend, not the virtualization architecture. Capability authority belongs to neutral typed capability grants and runtime/domain descriptors, not VMX CSR words, compatibility masks, nested published capability words, or VMCS-shaped helper state.

This closure follows the `audit2.md` blocker that active production callers must not treat `FromCompatibilityMasks(...)`, `PublishedVmxCaps`, `PublishedCapabilityWord`, or non-typed `HasEffectiveCapability(...)` fallback as security/admission authority.

## Closed Slice

- Removed the non-typed admission fallback in `CapabilityBoundaryRequirement`: nonzero requirements with `RequiresTypedGrant: false` now fail closed.
- Routed `DomainRuntimeAuthority`, `DomainValidationResult`, `DomainLegalityService`, and `RuntimeBoundaryAdmissionService` through full typed `CapabilityBoundaryRequirement` values instead of loose capability masks.
- Changed `CapabilityPublicationPolicy` so VmxCaps compatibility publication checks `CapabilityGrantScope.CompatibilityProjection` typed grants and `IsPublishableCompatibilityGrant`.
- Removed nested caller-pressure names `PublishedVmxCaps` and `PublishedCapabilityWord` from production nested policy.
- Replaced nested `NestedEnablementProof.FromCompatibilityMasks(...)` with typed descriptor proof materialization.
- Made compatibility alias publication through nested capability words denied/read-only projection, while typed nested publication is derived from a typed grant.

## Intentionally Not Moved

No `VmcsManager`, `VmxExecutionUnit`, `VmcsManagerAdapter`, VMX capability manager, VMCS field store, VMX CSR owner, or renamed runtime owner was introduced.

`CapabilityGrantCollection.FromCompatibilityMasks(...)` remains only as explicit compatibility seed ingress that immediately materializes typed grants. It is not a runtime source of truth.

## Remaining Vocabulary

VMX/VmxCaps and nested compatibility names remain only as frozen compatibility/projection/conformance vocabulary. `HasEffectiveCapability(...)` remains descriptor-local projection/conformance vocabulary backed by typed grants, not an admission fallback.

## Live Generic Responsibilities

- Typed capability grants: `CapabilityGrantCollection`.
- Capability descriptor projection: `CapabilityDescriptorSet`.
- Runtime admission: `RuntimeBoundaryAdmissionService`.
- Runtime/root authority: `DomainRuntimeAuthority` and `RootAuthorityDescriptor`.
- Evidence policy: `EvidenceBoundaryRequirement` / `EvidencePolicyDescriptor`.

## Conformance

Added `CapabilityCallerMaskIngressAuthorityRemovalContract`, with tests proving:

- non-typed nonzero capability requirements fail closed;
- active admission/authority sources do not call `HasEffectiveCapability(...)`;
- VmxCaps publication checks typed publishable projection grants;
- nested `PublishedVmxCaps`, `PublishedCapabilityWord`, and `FromCompatibilityMasks(...)` are absent from production nested policy;
- compatibility alias publication is denied as authority;
- guest-visible capability projection does not expose host-only evidence.

## Verification

- `dotnet build HybridCPU_ISE.csproj --no-restore`: passed with existing warnings.
- `dotnet build HybridCPU_ISE.Tests.csproj --no-restore`: passed with existing warnings.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: passed.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~RuntimeBoundaryAdmissionTests"`: passed.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxCapsProjectionBoundaryTests"`: passed.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~CapabilityCallerMaskIngress"`: passed.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"`: passed.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~RemovedLegacyVmxExecutionUnit"`: passed.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~LegacyVmcsManager"`: passed.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmcsV2"`: passed.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Capability"`: passed.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Nested"`: passed.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxCaps"`: passed.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~DmaStreamCompute"`: failed on known unrelated repository-shape/native NonRTL checks.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Retire"`: failed on known unrelated documentation/stream/compat/native-DMA boundary checks.

## Residual Risk

Architectural freeze is still blocked by generic substrate placement, memory translation identity cleanup, Lane6 host-token evidence/migration proof, and generic nested-domain projection/checkpoint ownership.

## Next Heavy Step

Move active memory translation and nested-domain identity callers toward neutral `SecondStage*`, `AddressSpace*`, and generic nested-domain descriptors while preserving frozen compatibility projections.
