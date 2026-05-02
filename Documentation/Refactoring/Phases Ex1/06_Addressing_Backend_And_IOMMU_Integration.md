# Phase 06 - Addressing Backend And IOMMU Integration

Status:
Design required. Implementation-ready only after executable DSC/L7 gates and explicit AddressSpace contract.

Scope:
Cover TASK-005: explicit physical versus IOMMU-translated backend selection for future executable DSC and L7 memory access.

Current code evidence:
- `IBurstBackend.Read(ulong deviceID, ulong address, Span<byte> buffer)` and `Write(ulong deviceID, ulong address, ReadOnlySpan<byte> buffer)` exist.
- `IOMMUBurstBackend.Read` delegates to `Memory.IOMMU.ReadBurst(deviceID, address, buffer)`.
- `IOMMUBurstBackend.Write` delegates to `Memory.IOMMU.WriteBurst(deviceID, address, buffer)`.
- `IOMMUBurstBackend.RegisterAcceleratorDevice` and `InitiateAcceleratorDMA` are not proof of production accelerator protocol.
- Current DSC runtime/helper uses physical main memory through `DmaStreamAcceleratorBackend` and token commit.
- Current DSC descriptors do not provide an approved executable address-space contract.

Architecture decision:
Future gated:
- Existing IOMMU burst backend is infrastructure, not current executable DSC/L7 integration.
- Future executable memory paths must select an explicit backend/address-space mode.
- No silent fallback from IOMMU-translated to physical memory is allowed.
- Owner/domain/device binding must define translation authority before any virtual/IOMMU descriptor is accepted.

Non-goals:
- Do not change current DSC helper physical path in this documentation phase.
- Do not claim DSC/L7 IOMMU execution is implemented.
- Do not infer `deviceID` from thread/context without an approved binding rule.
- Do not reuse DSC1 reserved bits for address-space semantics.

Required design gates:
- `AddressSpace` model: `Physical`, `IOMMUTranslated`, and any future modes.
- Descriptor ABI location for address-space selection: DSC2 or capability-gated extension block.
- Device ID binding and owner/domain authority.
- Translation fault mapping into phase 04 precise fault records.
- Physical backend wrapper contract for current physical semantics.
- IOMMU backend contract for translated semantics.
- Mapping epoch and revocation behavior.
- Backend capability discovery and rejection behavior.

Implementation plan:
1. Define `AddressSpace` in a new ABI version or extension block.
2. Add a physical `IBurstBackend` wrapper for current physical main-memory helper semantics.
3. Add a backend resolver that selects physical or `IOMMUBurstBackend` explicitly.
4. Bind token owner/domain/device ID at issue/admission.
5. Route read/write faults into `DmaStreamComputeFaultRecord`.
6. Reject unsupported backend/address-space combinations before token issue or as precise admission faults.
7. Preserve current DSC1 physical helper behavior unless a gated executable mode says otherwise.

Affected files/classes/methods:
- `IBurstBackend`
- `IOMMUBurstBackend`
- `Memory.IOMMU.ReadBurst`
- `Memory.IOMMU.WriteBurst`
- `DmaStreamAcceleratorBackend`
- `DmaStreamComputeRuntime`
- `DmaStreamComputeToken`
- future backend resolver
- DSC2 parser and capability model
- L7 accelerator memory portal and commit model

Testing requirements:
- Physical descriptor uses physical backend only.
- IOMMU-translated descriptor calls `IOMMUBurstBackend` with approved `deviceID`.
- Unsupported address-space mode rejects with no fallback.
- Device ID mismatch rejects.
- Owner/domain guard mismatch rejects.
- Translation, permission, alignment, and bounds faults map to precise token faults.
- Mapping revocation during active token is canceled, faulted, or serialized per ADR.

Documentation updates:
Replace any "IOMMU absent" language with "IOMMU burst infrastructure exists, executable DSC/L7 integration is future gated." Document exact backend selection and no-fallback rule before implementation.

Compiler/backend impact:
Current compiler/backend must not assume IOMMU-translated DSC addresses. Future lowering may emit address-space-qualified descriptors only after DSC2/capability discovery and backend conformance tests exist.

Compatibility risks:
Silent fallback would violate isolation. Reusing DSC1 fields would break immutable ABI. Ambiguous device ID binding would make owner/domain guards meaningless.

Exit criteria:
- Address-space ABI approved.
- Physical and translated backend selection rules approved.
- Fault mapping approved.
- No-fallback tests specified.

Blocked by:
Phase 02 executable DSC ADR, phase 03 token binding, phase 04 fault publication, and phase 07 DSC2/capability ABI for new descriptor fields.

Enables:
Truthful executable DSC/L7 IOMMU integration, backend-selectable memory paths, and compiler lowering for virtual/IOMMU descriptors after all gates.

