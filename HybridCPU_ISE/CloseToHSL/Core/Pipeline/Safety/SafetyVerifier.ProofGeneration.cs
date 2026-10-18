using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core
{
    public partial class SafetyVerifier
    {
        // Phase 2: Current cycle counter for proof generation
        private long _currentCycle = 0;

        /// <summary>
        /// Set current cycle for proof generation (Phase 2)
        /// </summary>
        public void SetCurrentCycle(long cycle)
        {
            _currentCycle = cycle;
        }

        /// <summary>
        /// Generate a bundle resource proof certificate.
        /// This proof demonstrates that the bundle respects all safety invariants
        /// and can be verified by external monitors (OS, hypervisor, HRoT).
        /// (Phase 2: Formal Resource Proofs - Verification Readiness)
        /// </summary>
        public BundleResourceProof GenerateProof(
            IReadOnlyList<MicroOp?> bundle,
            SecurityContext context)
        {
            if (bundle == null || bundle.Count != 8)
            {
                return new BundleResourceProof
                {
                    IsValid = false,
                    VerificationStatus = "Invalid bundle (null or wrong size)"
                };
            }

            try
            {
                VerifyMemoryIsolation(bundle, context);
            }
            catch (Exception ex)
            {
                return new BundleResourceProof
                {
                    IsValid = false,
                    VerificationStatus = $"Memory isolation violation: {ex.Message}"
                };
            }

            if (!VerifyBundle(bundle))
            {
                return new BundleResourceProof
                {
                    IsValid = false,
                    VerificationStatus = "Register hazard or isolation violation detected"
                };
            }

            uint bundleHash = BundleResourceProof.CalculateBundleHash(bundle);

            uint threadMask = 0;
            foreach (var op in bundle)
            {
                if (op != null && op.OwnerThreadId >= 0 && op.OwnerThreadId < 32)
                {
                    threadMask |= (uint)(1 << op.OwnerThreadId);
                }
            }

            var proof = new BundleResourceProof
            {
                Cycle = _currentCycle,
                BundleHash = bundleHash,
                AllowedMemoryRangeStart = context.MinAddr,
                AllowedMemoryRangeEnd = context.MaxAddr,
                ThreadMask = threadMask,
                Timestamp = DateTime.UtcNow,
                IsValid = true,
                VerificationStatus = "All invariants verified"
            };

            proof = SignProof(proof);
            return proof;
        }

        /// <summary>
        /// Verify memory isolation for bundle operations.
        /// Ensures all memory accesses are within allowed bounds.
        /// (Phase 2: Internal verification helper)
        /// </summary>
        private void VerifyMemoryIsolation(
            IReadOnlyList<MicroOp?> bundle,
            SecurityContext context)
        {
            foreach (var op in bundle)
            {
                if (op == null)
                    continue;

                IReadOnlyList<(ulong Address, ulong Length)> readRanges = GetSafetyVisibleReadRanges(op);
                if (readRanges.Count != 0)
                {
                    foreach (var range in readRanges)
                    {
                        if (range.Address < context.MinAddr ||
                            (range.Address + range.Length) > context.MaxAddr)
                        {
                            throw new InvalidOperationException(
                                $"Memory read at 0x{range.Address:X} violates bounds " +
                                $"[0x{context.MinAddr:X}, 0x{context.MaxAddr:X})");
                        }
                    }
                }

                if (op.WriteMemoryRanges != null)
                {
                    foreach (var range in op.WriteMemoryRanges)
                    {
                        if (range.Address < context.MinAddr ||
                            (range.Address + range.Length) > context.MaxAddr)
                        {
                            throw new InvalidOperationException(
                                $"Memory write at 0x{range.Address:X} violates bounds " +
                                $"[0x{context.MinAddr:X}, 0x{context.MaxAddr:X})");
                        }
                    }
                }
            }
        }
    }
}
