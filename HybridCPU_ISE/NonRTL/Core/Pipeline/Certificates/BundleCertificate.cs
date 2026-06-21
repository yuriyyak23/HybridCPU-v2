using System;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Bundle resource proof structure for Singularity/SIP-style formal verification.
    /// Provides a certificate that demonstrates resource isolation and safety guarantees.
    /// (Phase 2: Formal Resource Proofs - Verification Readiness)
    /// </summary>
    public struct BundleResourceProof
    {
        /// <summary>
        /// Cycle at which this proof was generated
        /// </summary>
        public long Cycle;

        /// <summary>
        /// Deterministic hash of the VLIW bundle
        /// Ensures proof corresponds to specific bundle state
        /// </summary>
        public uint BundleHash;

        /// <summary>
        /// Start address of allowed memory range for this bundle's thread
        /// </summary>
        public ulong AllowedMemoryRangeStart;

        /// <summary>
        /// End address of allowed memory range for this bundle's thread
        /// </summary>
        public ulong AllowedMemoryRangeEnd;

        /// <summary>
        /// Bitmask indicating which hardware threads are active in this bundle
        /// Bit i = 1 means thread i has operations in this bundle
        /// </summary>
        public uint ThreadMask;

        /// <summary>
        /// Simulated signature bytes for the ISE proof fixture.
        /// This is not a hardware root-of-trust claim.
        /// </summary>
        public byte[] Signature;

        /// <summary>
        /// Timestamp when proof was generated (for audit trail)
        /// </summary>
        public DateTime Timestamp;

        /// <summary>
        /// Whether this proof passed all verification checks
        /// </summary>
        public bool IsValid;

        /// <summary>
        /// Human-readable description of verification status
        /// </summary>
        public string VerificationStatus;

        /// <summary>
        /// Create a new bundle resource proof
        /// </summary>
        public BundleResourceProof()
        {
            Cycle = 0;
            BundleHash = 0;
            AllowedMemoryRangeStart = 0;
            AllowedMemoryRangeEnd = 0;
            ThreadMask = 0;
            Signature = Array.Empty<byte>();
            Timestamp = DateTime.UtcNow;
            IsValid = false;
            VerificationStatus = "Not verified";
        }

        /// <summary>
        /// Calculate deterministic hash of VLIW bundle
        /// Uses FNV-1a hash algorithm for determinism
        /// </summary>
        public static uint CalculateBundleHash(System.Collections.Generic.IReadOnlyList<MicroOp?> bundle)
        {
            if (bundle == null || bundle.Count == 0)
                return 0;

            // FNV-1a constants
            const uint FNV_OFFSET_BASIS = 2166136261;
            const uint FNV_PRIME = 16777619;

            uint hash = FNV_OFFSET_BASIS;

            foreach (MicroOp? op in bundle)
            {
                if (op == null)
                    continue;

                // Hash OpCode
                uint opCode = op.OpCode;
                hash ^= opCode & 0xFF;
                hash *= FNV_PRIME;
                hash ^= (opCode >> 8) & 0xFF;
                hash *= FNV_PRIME;
                hash ^= (opCode >> 16) & 0xFF;
                hash *= FNV_PRIME;
                hash ^= (opCode >> 24) & 0xFF;
                hash *= FNV_PRIME;

                // Hash OwnerThreadId
                hash ^= (uint)op.OwnerThreadId;
                hash *= FNV_PRIME;
            }

            return hash;
        }

        /// <summary>
        /// Verify proof-fixture signature bytes.
        /// This is simulated for ISE and is not a hardware root-of-trust claim.
        /// </summary>
        public bool VerifySignature()
        {
            // Simulated signature verification only.

            if (Signature == null || Signature.Length == 0)
                return false;

            // For ISE: simple length check indicates presence of signature
            return Signature.Length >= 32; // Minimum signature length
        }

        /// <summary>
        /// Get human-readable string representation
        /// </summary>
        public override string ToString()
        {
            return $"BundleProof[Cycle={Cycle}, Hash=0x{BundleHash:X8}, " +
                   $"MemRange=0x{AllowedMemoryRangeStart:X}-0x{AllowedMemoryRangeEnd:X}, " +
                   $"ThreadMask=0x{ThreadMask:X}, Valid={IsValid}]";
        }
    }
}
