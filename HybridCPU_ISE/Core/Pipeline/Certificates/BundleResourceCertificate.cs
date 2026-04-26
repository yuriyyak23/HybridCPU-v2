using System;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Structural identity of an inter-core bundle resource certificate.
    /// Excludes temporal fields like cycle number and keeps replay/template matching
    /// scoped to structural proof context instead of raw certificate object equality.
    /// </summary>
    public readonly struct BundleResourceCertificateIdentity : IEquatable<BundleResourceCertificateIdentity>
    {
        public BundleResourceCertificateIdentity(
            ulong combinedMaskLow,
            ulong combinedMaskHigh,
            int ownerThreadId,
            int operationCount,
            ulong bundleHash,
            ulong assistStructuralKey)
        {
            CombinedMaskLow = combinedMaskLow;
            CombinedMaskHigh = combinedMaskHigh;
            OwnerThreadId = ownerThreadId;
            OperationCount = operationCount;
            BundleHash = bundleHash;
            AssistStructuralKey = assistStructuralKey;
        }

        public ulong CombinedMaskLow { get; }

        public ulong CombinedMaskHigh { get; }

        public int OwnerThreadId { get; }

        public int OperationCount { get; }

        public ulong BundleHash { get; }

        public ulong AssistStructuralKey { get; }

        public bool IsValid =>
            (CombinedMaskLow != 0 || CombinedMaskHigh != 0) &&
            OperationCount > 0 &&
            OwnerThreadId >= 0 &&
            OwnerThreadId < 16;

        public bool Equals(BundleResourceCertificateIdentity other)
        {
            return CombinedMaskLow == other.CombinedMaskLow &&
                   CombinedMaskHigh == other.CombinedMaskHigh &&
                   OwnerThreadId == other.OwnerThreadId &&
                   OperationCount == other.OperationCount &&
                   BundleHash == other.BundleHash &&
                   AssistStructuralKey == other.AssistStructuralKey;
        }

        public override bool Equals(object? obj)
        {
            return obj is BundleResourceCertificateIdentity other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                CombinedMaskLow,
                CombinedMaskHigh,
                OwnerThreadId,
                OperationCount,
                BundleHash,
                AssistStructuralKey);
        }
    }

    /// <summary>
    /// Bundle resource certificate for verifiable non-interference (Phase: Safety Tags & Certificates).
    ///
    /// Purpose: Provides a formal proof that a bundle of micro-operations does not conflict
    /// with other operations based on resource usage. This certificate can be used by the
    /// Singularity-style OS to verify isolation guarantees at the hardware level.
    ///
    /// Design goals:
    /// - Hardware-agnostic: suitable for HLS synthesis
    /// - Verifiable: can be checked without re-analyzing operations
    /// - Efficient: minimal overhead for certificate generation and validation
    /// - Formal: based on safety mask bit-level analysis
    /// </summary>
    public class BundleResourceCertificate
    {
        private const ulong RegisterBitsMaskLow32 = 0xFFFF_FFFFUL;
        private const ulong AssistStructuralKeyOffsetBasis = 14695981039346656037UL;
        private const ulong AssistStructuralKeyPrime = 1099511628211UL;

        /// <summary>
        /// Combined safety mask for all operations in the bundle.
        /// Represents the union of all resource requirements.
        /// </summary>
        public SafetyMask128 CombinedMask { get; set; }

        /// <summary>
        /// Thread ID that owns this bundle.
        /// Used for thread isolation verification.
        /// </summary>
        public int OwnerThreadId { get; set; }

        /// <summary>
        /// Cycle number when this certificate was created.
        /// Used for temporal ordering and debugging.
        /// </summary>
        public ulong CycleNumber { get; set; }

        /// <summary>
        /// Number of operations in the bundle.
        /// Used for verification and statistics.
        /// </summary>
        public int OperationCount { get; set; }

        /// <summary>
        /// Indicates whether this certificate represents a speculative execution.
        /// Speculative operations may be rolled back without committing.
        /// </summary>
        public bool IsSpeculative { get; set; }

        /// <summary>
        /// Hash of the bundle for integrity verification.
        /// Computed from operation opcodes and resource masks.
        /// </summary>
        public ulong BundleHash { get; set; }

        /// <summary>
        /// Assist-owned structural discriminator that keeps replay/template reuse
        /// aware of widened assist owner tuples without reopening retire-visible
        /// bundle semantics.
        /// </summary>
        public ulong AssistStructuralKey { get; set; }

        /// <summary>
        /// Structural identity used by replay/template caches.
        /// Keeps certificate reuse scoped to proof context rather than object equality.
        /// </summary>
        public BundleResourceCertificateIdentity StructuralIdentity =>
            new BundleResourceCertificateIdentity(
                CombinedMask.Low,
                CombinedMask.High,
                OwnerThreadId,
                OperationCount,
                BundleHash,
                AssistStructuralKey);

        /// <summary>
        /// Create a resource certificate for a bundle of micro-operations.
        /// </summary>
        public static BundleResourceCertificate Create(
            System.Collections.Generic.IReadOnlyList<MicroOp?> bundle,
            int ownerThreadId,
            ulong cycleNumber)
        {
            if (bundle == null || bundle.Count == 0)
            {
                throw new ArgumentException("Bundle cannot be null or empty", nameof(bundle));
            }

            SafetyMask128 combinedMask = SafetyMask128.Zero;
            int operationCount = 0;
            bool isSpeculative = false;
            ulong assistStructuralKey = 0;

            // SipHash-2-4: collision-resistant integrity hash (HLS: 4-cycle latency)
            var hasher = new HardwareHash();
            hasher.Initialize();

            foreach (MicroOp? op in bundle)
            {
                if (op == null) continue;

                MicroOpAdmissionMetadata admission = op.AdmissionMetadata;
                SafetyMask128 opMask = admission.CertificateMask;
                combinedMask = combinedMask | opMask;

                operationCount++;
                isSpeculative |= op.IsSpeculative;
                assistStructuralKey = AccumulateAssistStructuralKey(assistStructuralKey, op);

                hasher.Compress((ulong)op.OpCode);
                hasher.Compress(opMask.Low);
                hasher.Compress(opMask.High);
                AppendAssistReplayKey(hasher, op);
            }

            ulong bundleHash = hasher.Finalize();

            return new BundleResourceCertificate
            {
                CombinedMask = combinedMask,
                OwnerThreadId = ownerThreadId,
                CycleNumber = cycleNumber,
                OperationCount = operationCount,
                IsSpeculative = isSpeculative,
                BundleHash = bundleHash,
                AssistStructuralKey = assistStructuralKey
            };
        }

        /// <summary>
        /// Verify that this certificate does not conflict with another certificate.
        /// Returns true if the two bundles can execute in parallel without conflicts.
        /// </summary>
        public bool NoConflictsWith(BundleResourceCertificate other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            // Check for resource conflicts using 128-bit mask
            return CombinedMask.NoConflictsWith(other.CombinedMask);
        }

        /// <summary>
        /// Verify that this certificate does not conflict with a candidate operation.
        /// Returns true if the candidate can be injected into the bundle without conflicts.
        /// </summary>
        public bool CanInject(MicroOp candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            MicroOpAdmissionMetadata admission = candidate.AdmissionMetadata;
            if ((GetSharedMask(CombinedMask) & admission.SharedStructuralMask).IsNonZero)
                return false;

            return !HasRegisterConflict((uint)(CombinedMask.Low & RegisterBitsMaskLow32), admission.RegisterHazardMask);
        }

        /// <summary>
        /// Verify candidate injection while preserving explicit conflict detail for
        /// checker-owned legality decisions.
        /// </summary>
        public bool CanInjectWithReason(MicroOp candidate, out CertificateRejectDetail detail)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            MicroOpAdmissionMetadata admission = candidate.AdmissionMetadata;
            if ((GetSharedMask(CombinedMask) & admission.SharedStructuralMask).IsNonZero)
            {
                detail = CertificateRejectDetail.SharedResourceConflict;
                return false;
            }

            if (HasRegisterConflict((uint)(CombinedMask.Low & RegisterBitsMaskLow32), admission.RegisterHazardMask))
            {
                detail = CertificateRejectDetail.RegisterGroupConflict;
                return false;
            }

            detail = CertificateRejectDetail.None;
            return true;
        }

        /// <summary>
        /// Create an extended certificate that includes an additional operation.
        /// Returns a new certificate representing the bundle with the operation added.
        /// </summary>
        public BundleResourceCertificate WithOperation(MicroOp op)
        {
            if (op == null)
            {
                throw new ArgumentNullException(nameof(op));
            }

            MicroOpAdmissionMetadata admission = op.AdmissionMetadata;
            SafetyMask128 opMask = admission.CertificateMask;

            // Chain-hash: compress previous digest with new op data
            var hasher = new HardwareHash();
            hasher.Initialize();
            hasher.Compress(BundleHash);
            hasher.Compress((ulong)op.OpCode);
            hasher.Compress(opMask.Low);
            hasher.Compress(opMask.High);
            AppendAssistReplayKey(hasher, op);

            return new BundleResourceCertificate
            {
                CombinedMask = CombinedMask | opMask,
                OwnerThreadId = OwnerThreadId,
                CycleNumber = CycleNumber,
                OperationCount = OperationCount + 1,
                IsSpeculative = IsSpeculative || op.IsSpeculative,
                BundleHash = hasher.Finalize(),
                AssistStructuralKey = AccumulateAssistStructuralKey(AssistStructuralKey, op)
            };
        }

        private static SafetyMask128 GetSharedMask(SafetyMask128 combinedMask)
        {
            return new SafetyMask128(combinedMask.Low & ~RegisterBitsMaskLow32, combinedMask.High);
        }

        private static void AppendAssistReplayKey(HardwareHash hasher, MicroOp op)
        {
            if (op is not AssistMicroOp assistMicroOp)
            {
                return;
            }

            // Keep the stable ownership signature in the template hash, but also
            // materialize the concrete assist tuple so same-VT and donor-VT
            // widened owners cannot collapse into one certificate identity.
            hasher.Compress(AssistOwnershipFingerprint.Compute(assistMicroOp));
            hasher.Compress((ulong)assistMicroOp.Kind);
            hasher.Compress((ulong)assistMicroOp.ExecutionMode);
            hasher.Compress((ulong)assistMicroOp.CarrierKind);
            hasher.Compress((ulong)assistMicroOp.CarrierSlotClass);
            hasher.Compress((ulong)assistMicroOp.DonorSource.Kind);
            hasher.Compress(PackIntPair(
                assistMicroOp.CarrierVirtualThreadId,
                assistMicroOp.DonorVirtualThreadId));
            hasher.Compress(PackIntPair(
                assistMicroOp.TargetVirtualThreadId,
                assistMicroOp.OwnerContextId));
            hasher.Compress(assistMicroOp.Placement.DomainTag);
            hasher.Compress(PackIntPair(
                assistMicroOp.DonorSource.SourceCoreId,
                assistMicroOp.DonorSource.OwnerContextId));
            hasher.Compress(
                ((ulong)assistMicroOp.DonorSource.SourcePodId << 48) |
                ((ulong)(ushort)assistMicroOp.PodId << 32) |
                (uint)assistMicroOp.TargetCoreId);
        }

        private static ulong PackIntPair(int low, int high)
        {
            unchecked
            {
                return (uint)low | ((ulong)(uint)high << 32);
            }
        }

        private static ulong AccumulateAssistStructuralKey(ulong currentKey, MicroOp op)
        {
            if (op is not AssistMicroOp assistMicroOp)
            {
                return currentKey;
            }

            ulong hash = currentKey == 0 ? AssistStructuralKeyOffsetBasis : currentKey;
            hash = MixAssistStructuralKey(hash, AssistOwnershipFingerprint.Compute(assistMicroOp));
            hash = MixAssistStructuralKey(hash, PackIntPair(
                (int)assistMicroOp.Kind,
                (int)assistMicroOp.DonorSource.Kind));
            hash = MixAssistStructuralKey(hash, PackIntPair(
                assistMicroOp.DonorVirtualThreadId,
                assistMicroOp.TargetVirtualThreadId));
            hash = MixAssistStructuralKey(hash, PackIntPair(
                assistMicroOp.CarrierCoreId,
                assistMicroOp.TargetCoreId));
            hash = MixAssistStructuralKey(hash, assistMicroOp.Placement.DomainTag);
            hash = MixAssistStructuralKey(hash, PackIntPair(
                assistMicroOp.DonorSource.SourceCoreId,
                assistMicroOp.OwnerContextId));
            hash = MixAssistStructuralKey(
                hash,
                ((ulong)assistMicroOp.DonorSource.SourcePodId << 48) |
                ((ulong)(ushort)assistMicroOp.PodId << 32) |
                (uint)assistMicroOp.DonorSource.OwnerContextId);
            return hash;
        }

        private static ulong MixAssistStructuralKey(ulong hash, ulong value)
        {
            for (int shift = 0; shift < 64; shift += 8)
            {
                hash ^= (byte)((value >> shift) & 0xFF);
                hash *= AssistStructuralKeyPrime;
            }

            return hash;
        }

        private static bool HasRegisterConflict(uint bundleRegMask, uint candidateRegMask)
        {
            ushort candRead = (ushort)(candidateRegMask & 0xFFFF);
            ushort candWrite = (ushort)(candidateRegMask >> 16);
            ushort bundleRead = (ushort)(bundleRegMask & 0xFFFF);
            ushort bundleWrite = (ushort)(bundleRegMask >> 16);

            return ((candRead & bundleWrite)
                  | (candWrite & bundleRead)
                  | (candWrite & bundleWrite)) != 0;
        }

        /// <summary>
        /// Validate the integrity of this certificate.
        /// Returns true if the certificate is well-formed and valid.
        /// </summary>
        public bool IsValid()
        {
            // Certificate is valid if:
            // 1. Combined mask is non-zero (at least one resource used)
            // 2. Operation count is positive
            // 3. Owner thread ID is in valid range [0, 15]
            return CombinedMask.IsNonZero &&
                   OperationCount > 0 &&
                   OwnerThreadId >= 0 &&
                   OwnerThreadId < 16;
        }

        public override string ToString()
        {
            return $"BundleResourceCertificate(Thread={OwnerThreadId}, Ops={OperationCount}, " +
                   $"Mask={CombinedMask}, Cycle={CycleNumber}, Hash=0x{BundleHash:X}, Speculative={IsSpeculative})";
        }

        /// <summary>
        /// Empty certificate (no resources, no operations).
        /// Used as a starting point for certificate construction.
        /// </summary>
        public static readonly BundleResourceCertificate Empty = new BundleResourceCertificate
        {
            CombinedMask = SafetyMask128.Zero,
            OwnerThreadId = -1,
            CycleNumber = 0,
            OperationCount = 0,
            IsSpeculative = false,
            BundleHash = 0,
            AssistStructuralKey = 0
        };
    }
}
