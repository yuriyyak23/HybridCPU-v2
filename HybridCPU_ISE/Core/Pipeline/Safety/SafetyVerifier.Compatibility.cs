using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core
{
    public partial class SafetyVerifier
    {
        /// <summary>
        /// Fast path verification using producer-side admission metadata.
        /// Checks shared structural overlap separately from register hazard legality so the
        /// compatibility helper no longer treats the raw safety mask as the correctness authority.
        /// </summary>
        /// <param name="bundle">Current VLIW bundle (8 slots)</param>
        /// <param name="candidate">Candidate micro-operation to inject</param>
        /// <param name="globalHardwareMask">Global hardware state (e.g., MSHR/Memory channels load)</param>
        /// <returns>True if injection is safe (no mask conflicts), false otherwise</returns>
        private bool VerifyInjectionFast128(
            IReadOnlyList<MicroOp?> bundle,
            MicroOp candidate,
            SafetyMask128 globalHardwareMask = default)
        {
            MicroOpAdmissionMetadata candidateAdmission = candidate.AdmissionMetadata;
            SafetyMask128 candidateSharedMask = candidateAdmission.SharedStructuralMask;
            uint candidateRegisterMask = candidateAdmission.RegisterHazardMask;

            if (candidateSharedMask.IsZero && candidateRegisterMask == 0)
                return false;

            SafetyMask128 bundleSharedMask = globalHardwareMask;
            uint bundleRegisterMask = 0;
            for (int i = 0; i < bundle.Count; i++)
            {
                var op = bundle[i];
                if (op != null)
                {
                    MicroOpAdmissionMetadata admission = op.AdmissionMetadata;
                    bundleSharedMask |= admission.SharedStructuralMask;
                    bundleRegisterMask |= admission.RegisterHazardMask;
                }
            }

            if (candidateSharedMask.IsNonZero && bundleSharedMask.ConflictsWith(candidateSharedMask))
                return false;

            return !HasRegisterConflict(bundleRegisterMask, candidateRegisterMask);
        }

        private LegalityDecision EvaluateInterCoreCompatibility(
            IReadOnlyList<MicroOp?> bundle,
            MicroOp candidate,
            int bundleOwnerThreadId,
            SafetyMask128 globalHardwareMask)
        {
            if (HasStructuralAdmissionMask(candidate) && BundleHasAllStructuralAdmissionMasks(bundle))
            {
                const LegalityAuthoritySource structuralAuthoritySource =
                    LegalityAuthoritySource.AdmissionMetadataStructuralCheck;

                return VerifyInjectionFast128(bundle, candidate, globalHardwareMask)
                    ? LegalityDecision.Allow(structuralAuthoritySource, attemptedReplayCertificateReuse: false)
                    : LegalityDecision.Reject(
                        RejectKind.CrossLaneConflict,
                        CertificateRejectDetail.SharedResourceConflict,
                        structuralAuthoritySource,
                        attemptedReplayCertificateReuse: false);
            }

            const LegalityAuthoritySource authoritySource =
                LegalityAuthoritySource.DetailedCompatibilityCheck;

            if (!CheckRegisterDependencies(
                    bundle,
                    candidate,
                    bundleOwnerThreadId,
                    candidate.OwnerThreadId))
            {
                return LegalityDecision.Reject(
                    RejectKind.CrossLaneConflict,
                    CertificateRejectDetail.RegisterGroupConflict,
                    authoritySource,
                    attemptedReplayCertificateReuse: false);
            }

            if (!CheckMemoryDependencies(
                    bundle,
                    candidate,
                    bundleOwnerThreadId,
                    candidate.OwnerThreadId))
            {
                return LegalityDecision.Reject(
                    RejectKind.CrossLaneConflict,
                    CertificateRejectDetail.SharedResourceConflict,
                    authoritySource,
                    attemptedReplayCertificateReuse: false);
            }

            return LegalityDecision.Allow(authoritySource, attemptedReplayCertificateReuse: false);
        }

        private static SafetyMask128 GetStructuralAdmissionMask(MicroOp microOp)
        {
            return microOp?.AdmissionMetadata.StructuralSafetyMask ?? SafetyMask128.Zero;
        }

        private static bool HasStructuralAdmissionMask(MicroOp microOp)
        {
            return microOp != null && microOp.AdmissionMetadata.HasStructuralSafetyMask;
        }

        private static bool BundleHasAllStructuralAdmissionMasks(IReadOnlyList<MicroOp?> bundle)
        {
            foreach (MicroOp? op in bundle)
            {
                if (op != null && !HasStructuralAdmissionMask(op))
                    return false;
            }

            return true;
        }

        private static bool HasRegisterConflict(uint bundleRegisterMask, uint candidateRegisterMask)
        {
            ushort candidateRead = (ushort)(candidateRegisterMask & 0xFFFF);
            ushort candidateWrite = (ushort)(candidateRegisterMask >> 16);
            ushort bundleRead = (ushort)(bundleRegisterMask & 0xFFFF);
            ushort bundleWrite = (ushort)(bundleRegisterMask >> 16);

            return ((candidateRead & bundleWrite)
                  | (candidateWrite & bundleRead)
                  | (candidateWrite & bundleWrite)) != 0;
        }

        /// <summary>
        /// Check for register dependencies (RAW/WAW/WAR hazards) between candidate and bundle operations.
        ///
        /// While threads have logically separate register files in the ISA, when operations from
        /// different threads are packed into the same VLIW bundle, they may share physical register
        /// resources during execution. FSP verification must check for register conflicts across
        /// ALL operations in the bundle, regardless of thread ownership.
        ///
        /// This ensures:
        /// - No RAW/WAW/WAR hazards between any operations executing in the same cycle
        /// - Safe sharing of physical register file during VLIW bundle execution
        /// </summary>
        private bool CheckRegisterDependencies(
            IReadOnlyList<MicroOp?> bundle,
            MicroOp candidate,
            int bundleOwnerThreadId,
            int candidateOwnerThreadId)
        {
            var candidateReads = new HashSet<int>(candidate.ReadRegisters);
            var candidateWrites = new HashSet<int>(candidate.WriteRegisters);

            foreach (var op in bundle)
            {
                if (op == null)
                    continue;

                var opReads = new HashSet<int>(op.ReadRegisters);
                var opWrites = new HashSet<int>(op.WriteRegisters);

                if (candidateWrites.Overlaps(opReads))
                    return false;

                if (candidateReads.Overlaps(opWrites))
                    return false;

                if (candidateWrites.Overlaps(opWrites))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Check for memory dependencies (memory range conflicts) between candidate and bundle operations.
        ///
        /// Memory conflicts occur when:
        /// - Two operations access overlapping memory ranges
        /// - At least one is a write
        ///
        /// Memory domains (IOMMU) provide isolation between threads, but we still need to check
        /// for conflicts within operations that might be accessing shared memory regions.
        ///
        /// Conservative approach: If any overlap exists with a write, reject injection.
        /// </summary>
        private bool CheckMemoryDependencies(
            IReadOnlyList<MicroOp?> bundle,
            MicroOp candidate,
            int bundleOwnerThreadId,
            int candidateOwnerThreadId)
        {
            IReadOnlyList<(ulong Address, ulong Length)> candidateReadRanges =
                GetSafetyVisibleReadRanges(candidate);
            IReadOnlyList<(ulong Address, ulong Length)> candidateWriteRanges =
                candidate.WriteMemoryRanges ?? Array.Empty<(ulong Address, ulong Length)>();

            if (candidateReadRanges.Count == 0 &&
                candidateWriteRanges.Count == 0)
            {
                return true;
            }

            foreach (var op in bundle)
            {
                if (op == null)
                    continue;

                IReadOnlyList<(ulong Address, ulong Length)> opReadRanges =
                    GetSafetyVisibleReadRanges(op);
                IReadOnlyList<(ulong Address, ulong Length)> opWriteRanges =
                    op.WriteMemoryRanges ?? Array.Empty<(ulong Address, ulong Length)>();

                if (candidateWriteRanges.Count != 0 && opReadRanges.Count != 0)
                {
                    if (HasRangeOverlap(candidateWriteRanges, opReadRanges))
                        return false;
                }

                if (candidateReadRanges.Count != 0 && opWriteRanges.Count != 0)
                {
                    if (HasRangeOverlap(candidateReadRanges, opWriteRanges))
                        return false;
                }

                if (candidateWriteRanges.Count != 0 && opWriteRanges.Count != 0)
                {
                    if (HasRangeOverlap(candidateWriteRanges, opWriteRanges))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Check if two sets of memory ranges overlap.
        /// </summary>
        private bool HasRangeOverlap(
            IReadOnlyList<(ulong Address, ulong Length)> ranges1,
            IReadOnlyList<(ulong Address, ulong Length)> ranges2)
        {
            foreach (var range1 in ranges1)
            {
                ulong start1 = range1.Address;
                ulong end1 = range1.Address + range1.Length;

                foreach (var range2 in ranges2)
                {
                    ulong start2 = range2.Address;
                    ulong end2 = range2.Address + range2.Length;

                    if (start1 < end2 && start2 < end1)
                        return true;
                }
            }

            return false;
        }
    }
}
