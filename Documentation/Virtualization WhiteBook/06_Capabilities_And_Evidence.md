# Capabilities And Evidence

Capabilities and evidence keep virtualization authority grant-first and visibility-aware. VMX capability bits are projected from typed runtime grants; they do not create grants.

## Capability Ownership

The capability substrate is under:

- `CloseToRTL/Core/Runtime/Capabilities/Descriptors/CapabilityDescriptorSet.cs`
- `CloseToRTL/Core/Runtime/Capabilities/Grants/CapabilityGrant.cs`
- `CloseToRTL/Core/Runtime/Capabilities/Negotiation/CapabilityNegotiationService.cs`
- `CloseToRTL/Core/Runtime/Capabilities/Publication/CapabilityPublicationPolicy.cs`
- `CloseToRTL/Core/Runtime/Capabilities/CapabilityBoundaryRequirement.cs`

The authority contract is:

```text
hardware support
  intersect runtime policy
  intersect domain grant
  intersect security policy
  intersect migration compatibility
  intersect evidence policy
  -> guest-visible compatibility projection
```

VMX capability projections live under `CloseToRTL/Core/Virtualization/Compatibility/Generated/CapabilityProjection`. The generated schema can expose compatibility bits such as VMX instruction capability names, but the bits are aliases over a neutral `CapabilityDescriptorSet`.

## Evidence Ownership

Host evidence is not guest ABI. It is not migration payload. It is not VMREAD-visible by default.

Relevant paths:

- `CloseToRTL/Core/Runtime/Evidence/**`
- `CloseToRTL/Core/Runtime/Evidence/HostOwned/HostOwnedEvidenceBoundary.cs`
- `CloseToRTL/Core/Runtime/Evidence/GuestVisible/GuestVisibleEvidenceProjection.cs`
- `CloseToRTL/Core/Runtime/Evidence/DebugTrace/DebugTraceExportPolicy.cs`
- `CloseToRTL/Core/Virtualization/Sideband/EvidenceTransport/EvidenceSidebandEnvelope.cs`

Evidence policy decides whether a fact is:

- host-owned only;
- guest architectural state;
- compatibility alias;
- migration-visible;
- debug/diagnostic only.

## Compatibility Alias Evidence

Compatibility alias projection requires evidence approval. For example, `VmxCompatibilityAdmissionService` uses `EvidenceBoundaryRequirement.GuestVisible(EvidenceVisibilityClass.CompatibilityAlias)` when admitting VMREAD projection and VMCALL trap projection.

This protects against a common failure mode: a VMX-compatible name looks familiar, so a caller treats it as automatically guest-visible. In HybridCPU, visibility is explicit policy, not implied by naming.

## Host Evidence Non-Leak Rule

Host-owned evidence must not leak into:

- VMCS projection fields;
- VMREAD scalar values;
- migration descriptors;
- guest-visible debug trace;
- VMX capability bits;
- trap qualifications.

If a compatibility projection needs a fact, the runtime owner must publish a guest-visible projection of that fact under an evidence policy.

## Conformance Anchors

The conformance tree includes host-evidence non-leak contracts, capability authority contracts, and VMX frontend exposure checks. These contracts are part of the architecture because they keep future refactors from turning evidence into authority.
