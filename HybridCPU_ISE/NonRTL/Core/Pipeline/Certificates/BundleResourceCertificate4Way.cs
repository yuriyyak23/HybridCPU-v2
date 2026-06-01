using System;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Fine-grained rejection details from certificate check.
    /// Distinguishes shared resource vs register group conflict.
    /// <para>
    /// HLS design note: 2-bit reason MUX — no additional timing cost
    /// beyond the existing <see cref="BundleResourceCertificate4Way.CanInject"/> logic.
    /// </para>
    /// </summary>
    public enum CertificateRejectDetail : byte
    {
        /// <summary>No rejection — candidate was accepted.</summary>
        None = 0,

        /// <summary>Shared resource conflict (bits 32–127): memory domains, DMA, LSU, Accel.</summary>
        SharedResourceConflict = 1,

        /// <summary>Per-VT register group conflict (bits 0–31): RAW, WAR, or WAW hazard.</summary>
        RegisterGroupConflict = 2
    }

    /// <summary>
    /// Structural replay/template identity for <see cref="BundleResourceCertificate4Way"/>.
    /// This is the explicit compatibility seam used for replay reuse instead of comparing
    /// raw shared/register mask snapshots in scheduler code.
    /// </summary>
    public readonly struct BundleResourceCertificateIdentity4Way : IEquatable<BundleResourceCertificateIdentity4Way>
    {
        private const int OCCUPANCY_BITS_PER_CLASS = 4;
        private static readonly ushort CurrentLaneTopologySignature = ComputeLaneTopologySignature();

        public BundleResourceCertificateIdentity4Way(
            ulong bundleShapeId,
            uint classVector,
            ushort laneTopologySignature,
            byte resourceProfile,
            int operationCount,
            byte dmaStreamComputeIdentityCount = 0,
            ulong dmaStreamComputeDescriptorIdentityHash = 0,
            ulong dmaStreamComputeNormalizedFootprintHash = 0,
            ulong dmaStreamComputeReplayEnvelopeHash = 0)
        {
            BundleShapeId = bundleShapeId;
            ClassVector = classVector;
            LaneTopologySignature = laneTopologySignature;
            ResourceProfile = resourceProfile;
            OperationCount = operationCount;
            DmaStreamComputeIdentityCount = dmaStreamComputeIdentityCount;
            DmaStreamComputeDescriptorIdentityHash = dmaStreamComputeDescriptorIdentityHash;
            DmaStreamComputeNormalizedFootprintHash = dmaStreamComputeNormalizedFootprintHash;
            DmaStreamComputeReplayEnvelopeHash = dmaStreamComputeReplayEnvelopeHash;
        }

        /// <summary>
        /// Structural digest over the 4-way certificate's legality-relevant resource state.
        /// </summary>
        public ulong BundleShapeId { get; }

        /// <summary>
        /// Packed class-occupancy vector (5 classes × 4 bits).
        /// </summary>
        public uint ClassVector { get; }

        /// <summary>
        /// Structural signature of the current lane topology and class aliases.
        /// </summary>
        public ushort LaneTopologySignature { get; }

        /// <summary>
        /// Compact profile of which structural resource buckets are populated.
        /// </summary>
        public byte ResourceProfile { get; }

        /// <summary>
        /// Number of operations covered by the certificate.
        /// </summary>
        public int OperationCount { get; }

        /// <summary>
        /// Number of lane6 descriptor-backed compute identities carried by the certificate.
        /// Phase 03 expects this to be 0 or 1 because DmaStreamClass capacity is singleton.
        /// </summary>
        public byte DmaStreamComputeIdentityCount { get; }

        /// <summary>
        /// Descriptor identity hash for a descriptor-backed lane6 compute op, when present.
        /// </summary>
        public ulong DmaStreamComputeDescriptorIdentityHash { get; }

        /// <summary>
        /// Normalized memory-footprint identity hash for a descriptor-backed lane6 compute op, when present.
        /// </summary>
        public ulong DmaStreamComputeNormalizedFootprintHash { get; }

        /// <summary>
        /// Full replay evidence envelope hash for descriptor-backed lane6 compute.
        /// This is evidence only; it is not execution or commit authority.
        /// </summary>
        public ulong DmaStreamComputeReplayEnvelopeHash { get; }

        public bool IsValid => LaneTopologySignature != 0 && OperationCount >= 0;

        public static BundleResourceCertificateIdentity4Way Create(in BundleResourceCertificate4Way certificate)
        {
            uint classVector = EncodeClassVector(certificate.ClassOccupancy);
            byte resourceProfile = ComputeResourceProfile(certificate);

            var hasher = new HardwareHash();
            hasher.Initialize();
            hasher.Compress(certificate.SharedMask.Low);
            hasher.Compress(certificate.SharedMask.High);
            hasher.Compress(((ulong)certificate.RegMaskVT1 << 32) | certificate.RegMaskVT0);
            hasher.Compress(((ulong)certificate.RegMaskVT3 << 32) | certificate.RegMaskVT2);
            hasher.Compress(classVector);
            hasher.Compress(((ulong)CurrentLaneTopologySignature << 32) | resourceProfile);
            hasher.Compress((ulong)certificate.OperationCount);
            if (certificate.DmaStreamComputeIdentityCount != 0)
            {
                hasher.Compress(certificate.DmaStreamComputeIdentityCount);
                hasher.Compress(certificate.DmaStreamComputeDescriptorIdentityHash);
                hasher.Compress(certificate.DmaStreamComputeNormalizedFootprintHash);
                hasher.Compress(certificate.DmaStreamComputeReplayEnvelopeHash);
            }

            return new BundleResourceCertificateIdentity4Way(
                hasher.Finalize(),
                classVector,
                CurrentLaneTopologySignature,
                resourceProfile,
                certificate.OperationCount,
                certificate.DmaStreamComputeIdentityCount,
                certificate.DmaStreamComputeDescriptorIdentityHash,
                certificate.DmaStreamComputeNormalizedFootprintHash,
                certificate.DmaStreamComputeReplayEnvelopeHash);
        }

        public bool Equals(BundleResourceCertificateIdentity4Way other)
        {
            return BundleShapeId == other.BundleShapeId &&
                   ClassVector == other.ClassVector &&
                   LaneTopologySignature == other.LaneTopologySignature &&
                   ResourceProfile == other.ResourceProfile &&
                   OperationCount == other.OperationCount &&
                   DmaStreamComputeIdentityCount == other.DmaStreamComputeIdentityCount &&
                   DmaStreamComputeDescriptorIdentityHash == other.DmaStreamComputeDescriptorIdentityHash &&
                   DmaStreamComputeNormalizedFootprintHash == other.DmaStreamComputeNormalizedFootprintHash &&
                   DmaStreamComputeReplayEnvelopeHash == other.DmaStreamComputeReplayEnvelopeHash;
        }

        public override bool Equals(object? obj)
        {
            return obj is BundleResourceCertificateIdentity4Way other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)0x5B4D8A13;
                hash = (hash * 397) ^ BundleShapeId.GetHashCode();
                hash = (hash * 397) ^ (int)ClassVector;
                hash = (hash * 397) ^ LaneTopologySignature;
                hash = (hash * 397) ^ ResourceProfile;
                hash = (hash * 397) ^ OperationCount;
                hash = (hash * 397) ^ DmaStreamComputeIdentityCount.GetHashCode();
                hash = (hash * 397) ^ DmaStreamComputeDescriptorIdentityHash.GetHashCode();
                hash = (hash * 397) ^ DmaStreamComputeNormalizedFootprintHash.GetHashCode();
                hash = (hash * 397) ^ DmaStreamComputeReplayEnvelopeHash.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            string dmaStreamSuffix = DmaStreamComputeIdentityCount == 0
                ? string.Empty
                : $", DmaDesc=0x{DmaStreamComputeDescriptorIdentityHash:X16}, DmaFootprint=0x{DmaStreamComputeNormalizedFootprintHash:X16}, DmaIds={DmaStreamComputeIdentityCount}";

            return $"Cert4WayIdentity(Shape=0x{BundleShapeId:X16}, Class=0x{ClassVector:X5}, " +
                   $"Topo=0x{LaneTopologySignature:X4}, Profile=0x{ResourceProfile:X2}, Ops={OperationCount}{dmaStreamSuffix})";
        }

        private static uint EncodeClassVector(SlotClassCapacityState state)
        {
            return (uint)state.AluOccupied
                 | ((uint)state.LsuOccupied << OCCUPANCY_BITS_PER_CLASS)
                 | ((uint)state.DmaStreamOccupied << (OCCUPANCY_BITS_PER_CLASS * 2))
                 | ((uint)state.BranchControlOccupied << (OCCUPANCY_BITS_PER_CLASS * 3))
                 | ((uint)state.SystemSingletonOccupied << (OCCUPANCY_BITS_PER_CLASS * 4));
        }

        private static byte ComputeResourceProfile(in BundleResourceCertificate4Way certificate)
        {
            byte profile = 0;
            if (certificate.SharedMask.IsNonZero) profile |= 1 << 0;
            if (certificate.RegMaskVT0 != 0) profile |= 1 << 1;
            if (certificate.RegMaskVT1 != 0) profile |= 1 << 2;
            if (certificate.RegMaskVT2 != 0) profile |= 1 << 3;
            if (certificate.RegMaskVT3 != 0) profile |= 1 << 4;
            if (certificate.DmaStreamComputeIdentityCount != 0) profile |= 1 << 5;
            return profile;
        }

        private static ushort ComputeLaneTopologySignature()
        {
            var hasher = new HardwareHash();
            hasher.Initialize();
            CompressClass(ref hasher, SlotClass.AluClass);
            CompressClass(ref hasher, SlotClass.LsuClass);
            CompressClass(ref hasher, SlotClass.DmaStreamClass);
            CompressClass(ref hasher, SlotClass.BranchControl);
            CompressClass(ref hasher, SlotClass.SystemSingleton);
            return (ushort)(hasher.Finalize() & 0xFFFF);
        }

        private static void CompressClass(ref HardwareHash hasher, SlotClass slotClass)
        {
            hasher.Compress((ulong)(byte)slotClass);
            hasher.Compress((ulong)SlotClassLaneMap.GetLaneMask(slotClass));
            hasher.Compress((ulong)SlotClassLaneMap.GetClassCapacity(slotClass));
        }
    }

    /// <summary>
    /// Intra-core SMT legality certificate and structural witness for the active typed-slot path.
    ///
    /// Purpose: carry shared-resource state, per-VT register-group state, and typed-slot
    /// occupancy for the current bundle while exposing <see cref="StructuralIdentity"/> as
    /// the replay/template compatibility seam.
    ///
    /// Design:
    /// - SharedMask (SafetyMask128): OR of all operation masks with bits 0–31 cleared.
    ///   Tracks shared resources (memory domains, DMA, stream engines, accelerators, LSU).
    /// - RegMaskVT0..VT3 (uint): Per-thread register group masks (bits 0–31 only).
    ///   Isolates RAW/WAW/WAR checks to the owning virtual thread.
    ///
    /// Conflict resolution is two-stage:
    /// 1. Shared resource check: (SharedMask &amp; candidateShared).IsNonZero → reject.
    /// 2. Register group check: (RegMaskVT[vt] &amp; candidateReg) != 0 → reject.
    ///
    /// Repository-facing architectural surfaces should describe legality in terms of the
    /// certificate's semantics and structural identity rather than raw mask layout alone.
    ///
    /// HLS synthesis notes (KiWi):
    /// - Clearing bits 0–31 maps to wire truncation (zero gate delay).
    /// - The VirtualThreadId switch compiles to a parallel 4-to-1 MUX (single LUT6 layer).
    /// - No heap allocation; all fields are value types.
    /// </summary>
    public partial struct BundleResourceCertificate4Way
    {
        /// <summary>
        /// Bitmask constant for isolating register group bits (Low bits 0–31).
        /// </summary>
        private const ulong REG_BITS_MASK_LOW32 = 0xFFFF_FFFFUL;

        /// <summary>
        /// Maximum number of virtual threads per physical core.
        /// </summary>
        public const int SMT_WAYS = 4;

        /// <summary>
        /// Combined safety mask for shared (non-register) resources.
        /// Bits 0–31 are always zero; only bits 32–127 are significant.
        /// Represents the union of memory domains, LSU channels, DMA, stream engines,
        /// accelerators, and extended GRLB resources across all operations in the bundle.
        /// </summary>
        public SafetyMask128 SharedMask;

        /// <summary>
        /// Register group mask for virtual thread 0 (bits 0–31 of SafetyMask128.Low).
        /// Tracks both read groups (bits 0–15) and write groups (bits 16–31).
        /// </summary>
        public uint RegMaskVT0;

        /// <summary>
        /// Register group mask for virtual thread 1.
        /// </summary>
        public uint RegMaskVT1;

        /// <summary>
        /// Register group mask for virtual thread 2.
        /// </summary>
        public uint RegMaskVT2;

        /// <summary>
        /// Register group mask for virtual thread 3.
        /// </summary>
        public uint RegMaskVT3;

        /// <summary>
        /// Number of operations packed into this bundle certificate.
        /// </summary>
        public int OperationCount;

        /// <summary>
        /// Per-class occupancy tracked alongside the certificate.
        /// Updated each time <see cref="AddOperation"/> is called.
        /// <para>
        /// HLS: 5 × 3-bit counters = 15 flip-flops.
        /// </para>
        /// </summary>
        public SlotClassCapacityState ClassOccupancy;

        /// <summary>
        /// Count of descriptor-backed lane6 compute identities included in this certificate.
        /// Normal Phase 03 admission keeps this at 0 or 1 through DmaStreamClass capacity.
        /// </summary>
        public byte DmaStreamComputeIdentityCount;

        /// <summary>
        /// Descriptor identity hash for descriptor-backed lane6 compute evidence.
        /// </summary>
        public ulong DmaStreamComputeDescriptorIdentityHash;

        /// <summary>
        /// Normalized memory-footprint identity hash for descriptor-backed lane6 compute evidence.
        /// </summary>
        public ulong DmaStreamComputeNormalizedFootprintHash;

        /// <summary>
        /// Full DmaStreamCompute replay evidence envelope hash.
        /// Replay/certificate identity is evidence only and cannot authorize execution or commit.
        /// </summary>
        public ulong DmaStreamComputeReplayEnvelopeHash;

        /// <summary>
        /// Opaque structural identity used for replay/template matching.
        /// The certificate keeps raw masks as a local structural primitive, but callers
        /// should compare this identity rather than peeking at mask fields directly.
        /// </summary>
        public readonly BundleResourceCertificateIdentity4Way StructuralIdentity => BundleResourceCertificateIdentity4Way.Create(this);

        /// <summary>
        /// Check whether a candidate micro-operation can be injected into the bundle
        /// without resource conflicts. Two-stage O(1) check:
        /// 1. Shared resources (memory domains, DMA, LSU, Accel) — full 128-bit AND.
        /// 2. Register groups — RAR-aware 32-bit check scoped to the candidate's VirtualThreadId.
        ///    Read-After-Read is permitted (reads are non-destructive); only RAW, WAR, and WAW
        ///    are true hazards. This eliminates conservative false conflicts from §3b audit fix.
        ///    Note: AddOperation() still OR-accumulates both read and write bits into RegMaskVT*
        ///    (safe because BundleResourceCertificate4Way is a per-cycle ephemeral object rebuilt
        ///    from Empty on each PackBundleIntraCoreSmt call — no AND-NOT retirement concern).
        /// </summary>
        /// <param name="candidate">Candidate micro-operation to inject</param>
        /// <returns>True if injection is safe (no conflicts)</returns>
        public bool CanInject(MicroOp candidate)
        {
            ArgumentNullException.ThrowIfNull(candidate);

            MicroOpAdmissionMetadata admission = candidate.AdmissionMetadata;

            // Stage 1: Shared resource check (bits 32–127).
            SafetyMask128 candidateShared = admission.SharedStructuralMask;

            if ((SharedMask & candidateShared).IsNonZero)
                return false;

            // Stage 2: Per-thread register group check (bits 0–31) — RAR-aware.
            uint candidateReg = admission.RegisterHazardMask;

            // HLS: 4-to-1 MUX keyed by VirtualThreadId — single LUT6 layer.
            uint bundleReg = candidate.VirtualThreadId switch
            {
                0 => RegMaskVT0,
                1 => RegMaskVT1,
                2 => RegMaskVT2,
                _ => RegMaskVT3
            };

            // Split 32-bit mask into read-half (bits 0–15) and write-half (bits 16–31).
            // Conflict only on true hazards: RAW, WAR, WAW. RAR is not a hazard.
            ushort candRead  = (ushort)(candidateReg & 0xFFFF);
            ushort candWrite = (ushort)(candidateReg >> 16);
            ushort bndRead   = (ushort)(bundleReg & 0xFFFF);
            ushort bndWrite  = (ushort)(bundleReg >> 16);

            return ((candRead  & bndWrite) |   // RAW: candidate reads, bundle already writes
                    (candWrite & bndRead)  |   // WAR: candidate writes, bundle already reads
                    (candWrite & bndWrite)) == 0; // WAW: both write same group
        }

        /// <summary>
        /// Record an operation into this certificate, updating both shared and
        /// per-thread register masks. Called after a successful CanInject check.
        ///
        /// HLS: OR-accumulate into SharedMask + indexed RegMaskVT, single cycle.
        /// </summary>
        /// <param name="op">Micro-operation to add to the bundle certificate</param>
        public void AddOperation(MicroOp op)
        {
            ArgumentNullException.ThrowIfNull(op);

            MicroOpAdmissionMetadata admission = op.AdmissionMetadata;

            // Accumulate shared resources (bits 32–127).
            SafetyMask128 opShared = admission.SharedStructuralMask;
            SharedMask |= opShared;

            // Accumulate per-thread register groups (bits 0–31).
            uint opReg = admission.RegisterHazardMask;
            switch (op.VirtualThreadId)
            {
                case 0: RegMaskVT0 |= opReg; break;
                case 1: RegMaskVT1 |= opReg; break;
                case 2: RegMaskVT2 |= opReg; break;
                default: RegMaskVT3 |= opReg; break;
            }

            // Phase 05: update class occupancy
            ClassOccupancy.IncrementOccupancy(op.Placement.RequiredSlotClass);
            AddDmaStreamComputeIdentityIfNeeded(op);

            OperationCount++;
        }

        /// <summary>
        /// Return the per-thread register mask for the specified virtual thread.
        /// </summary>
        public uint GetRegMask(int virtualThreadId)
        {
            return virtualThreadId switch
            {
                0 => RegMaskVT0,
                1 => RegMaskVT1,
                2 => RegMaskVT2,
                _ => RegMaskVT3
            };
        }

        /// <summary>
        /// Extended CanInject that returns a structured reject reason.
        /// Same algorithm as <see cref="CanInject"/>, but annotates the rejection with
        /// <see cref="CertificateRejectDetail"/> for diagnostics/telemetry.
        /// <para>
        /// HLS: identical gate cost to <see cref="CanInject"/> + 2-bit reason MUX.
        /// </para>
        /// </summary>
        /// <param name="candidate">Candidate micro-operation to inject.</param>
        /// <param name="detail">On rejection: which stage caused the conflict.</param>
        /// <returns>True if injection is safe (no conflicts).</returns>
        public bool CanInjectWithReason(MicroOp candidate, out CertificateRejectDetail detail)
        {
            ArgumentNullException.ThrowIfNull(candidate);

            MicroOpAdmissionMetadata admission = candidate.AdmissionMetadata;

            detail = CertificateRejectDetail.None;

            // Stage 1: Shared resource check (bits 32–127).
            SafetyMask128 candidateShared = admission.SharedStructuralMask;

            if ((SharedMask & candidateShared).IsNonZero)
            {
                detail = CertificateRejectDetail.SharedResourceConflict;
                return false;
            }

            // Stage 2: Per-thread register group check (bits 0–31) — RAR-aware.
            uint candidateReg = admission.RegisterHazardMask;

            uint bundleReg = candidate.VirtualThreadId switch
            {
                0 => RegMaskVT0,
                1 => RegMaskVT1,
                2 => RegMaskVT2,
                _ => RegMaskVT3
            };

            ushort candRead  = (ushort)(candidateReg & 0xFFFF);
            ushort candWrite = (ushort)(candidateReg >> 16);
            ushort bndRead   = (ushort)(bundleReg & 0xFFFF);
            ushort bndWrite  = (ushort)(bundleReg >> 16);

            if (((candRead  & bndWrite) |
                 (candWrite & bndRead)  |
                 (candWrite & bndWrite)) != 0)
            {
                detail = CertificateRejectDetail.RegisterGroupConflict;
                return false;
            }

            return true;
        }

        private void AddDmaStreamComputeIdentityIfNeeded(MicroOp op)
        {
            if (op is not DmaStreamComputeMicroOp dmaStreamCompute)
            {
                return;
            }

            if (DmaStreamComputeIdentityCount == 0)
            {
                DmaStreamComputeIdentityCount = 1;
                DmaStreamComputeDescriptorIdentityHash = dmaStreamCompute.DescriptorIdentityHash;
                DmaStreamComputeNormalizedFootprintHash = dmaStreamCompute.NormalizedFootprintHash;
                DmaStreamComputeReplayEnvelopeHash = dmaStreamCompute.ReplayEvidence.EnvelopeHash;
                return;
            }

            var hasher = new HardwareHash();
            hasher.Initialize();
            hasher.Compress(DmaStreamComputeDescriptorIdentityHash);
            hasher.Compress(DmaStreamComputeNormalizedFootprintHash);
            hasher.Compress(DmaStreamComputeReplayEnvelopeHash);
            hasher.Compress(dmaStreamCompute.DescriptorIdentityHash);
            hasher.Compress(dmaStreamCompute.NormalizedFootprintHash);
            hasher.Compress(dmaStreamCompute.ReplayEvidence.EnvelopeHash);

            DmaStreamComputeIdentityCount = DmaStreamComputeIdentityCount == byte.MaxValue
                ? byte.MaxValue
                : (byte)(DmaStreamComputeIdentityCount + 1);
            DmaStreamComputeDescriptorIdentityHash = hasher.Finalize();
            DmaStreamComputeNormalizedFootprintHash ^= dmaStreamCompute.NormalizedFootprintHash;
            DmaStreamComputeReplayEnvelopeHash ^= dmaStreamCompute.ReplayEvidence.EnvelopeHash;
        }

        /// <summary>
        /// Empty certificate with no resources claimed.
        /// </summary>
        public static readonly BundleResourceCertificate4Way Empty = default;

        public override string ToString()
        {
            return $"Cert4Way(Ops={OperationCount}, Shared={SharedMask}, " +
                   $"VT0=0x{RegMaskVT0:X8}, VT1=0x{RegMaskVT1:X8}, " +
                   $"VT2=0x{RegMaskVT2:X8}, VT3=0x{RegMaskVT3:X8})";
        }
    }
}
