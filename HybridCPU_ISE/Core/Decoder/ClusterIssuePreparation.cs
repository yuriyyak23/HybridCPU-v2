using System;
using System.Numerics;

namespace YAKSys_Hybrid_CPU.Core
{
    public enum DecodeMode : byte
    {
        ReferenceSequentialMode = 0,
        ClusterPreparedMode = 1
    }

    public enum AuxiliaryClusterKind : byte
    {
        Vector = 0,
        Memory = 1,
        DmaStream = 2,
        Control = 3,
        System = 4,
        Other = 5
    }

    public readonly struct ScalarClusterIssueEntry
    {
        public ScalarClusterIssueEntry(DecodedBundleSlotDescriptor slotDescriptor)
            : this(
                slotDescriptor.SlotIndex,
                slotDescriptor.VirtualThreadId,
                slotDescriptor.OwnerThreadId,
                slotDescriptor.OpCode,
                slotDescriptor.MicroOp)
        {
        }

        internal ScalarClusterIssueEntry(
            byte slotIndex,
            int virtualThreadId,
            int ownerThreadId,
            uint opCode,
            MicroOp microOp)
        {
            SlotIndex = slotIndex;
            VirtualThreadId = virtualThreadId;
            OwnerThreadId = ownerThreadId;
            OpCode = opCode;
            MicroOp = microOp;
        }

        public MicroOp MicroOp { get; }
        public DecodedBundleSlotDescriptor SlotDescriptor
        {
            get
            {
                DecodedBundleSlotDescriptor slotDescriptor =
                    DecodedBundleSlotDescriptor.Create(SlotIndex, MicroOp);
                return new DecodedBundleSlotDescriptor(
                    slotDescriptor.MicroOp,
                    slotDescriptor.SlotIndex,
                    VirtualThreadId,
                    OwnerThreadId,
                    OpCode,
                    slotDescriptor.ReadRegisters,
                    slotDescriptor.WriteRegisters,
                    slotDescriptor.WritesRegister,
                    slotDescriptor.IsMemoryOp,
                    slotDescriptor.IsControlFlow,
                    slotDescriptor.Placement,
                    slotDescriptor.MemoryBankIntent,
                    slotDescriptor.IsFspInjected,
                    slotDescriptor.IsEmptyOrNop,
                    slotDescriptor.IsVectorOp);
            }
        }
        public byte SlotIndex { get; }
        public int VirtualThreadId { get; }
        public int OwnerThreadId { get; }
        public uint OpCode { get; }
    }

    public sealed class ScalarClusterIssueGroup
    {
        public ScalarClusterIssueGroup(byte candidateMask, byte preparedMask, ScalarClusterIssueEntry[] entries)
        {
            CandidateMask = candidateMask;
            PreparedMask = preparedMask;
            Entries = entries;
        }

        public byte CandidateMask { get; }
        public byte PreparedMask { get; }
        public ScalarClusterIssueEntry[] Entries { get; }
        public int Count => Entries.Length;

        /// <summary>
        /// Build per-VT scalar candidate summary from the prepared entries.
        /// VT indices outside [0..3] are clamped to avoid out-of-range access.
        /// </summary>
        public VtScalarCandidateSummary BuildVtSummary()
        {
            int vt0 = 0, vt1 = 0, vt2 = 0, vt3 = 0;
            for (int i = 0; i < Entries.Length; i++)
            {
                switch (Entries[i].VirtualThreadId)
                {
                    case 0: vt0++; break;
                    case 1: vt1++; break;
                    case 2: vt2++; break;
                    case 3: vt3++; break;
                }
            }

            return new VtScalarCandidateSummary(vt0, vt1, vt2, vt3);
        }
    }

    /// <summary>
    /// Per-VT scalar candidate counts derived from the prepared scalar issue group.
    /// Makes VT-aware ownership explicit in the scalar targeting contract.
    /// VT0..VT3 correspond to the 4-way SMT virtual thread indices.
    /// </summary>
    public readonly struct VtScalarCandidateSummary
    {
        public VtScalarCandidateSummary(int vt0Count, int vt1Count, int vt2Count, int vt3Count)
        {
            Vt0Count = vt0Count;
            Vt1Count = vt1Count;
            Vt2Count = vt2Count;
            Vt3Count = vt3Count;
        }

        /// <summary>Prepared scalar candidates owned by VT0.</summary>
        public int Vt0Count { get; }

        /// <summary>Prepared scalar candidates owned by VT1.</summary>
        public int Vt1Count { get; }

        /// <summary>Prepared scalar candidates owned by VT2.</summary>
        public int Vt2Count { get; }

        /// <summary>Prepared scalar candidates owned by VT3.</summary>
        public int Vt3Count { get; }

        /// <summary>Total prepared scalar candidates across all VTs.</summary>
        public int TotalCount => Vt0Count + Vt1Count + Vt2Count + Vt3Count;

        /// <summary>Number of distinct VTs that contributed at least one prepared scalar candidate.</summary>
        public int ActiveVtCount =>
            (Vt0Count > 0 ? 1 : 0)
            + (Vt1Count > 0 ? 1 : 0)
            + (Vt2Count > 0 ? 1 : 0)
            + (Vt3Count > 0 ? 1 : 0);

        /// <summary>
        /// Get the candidate count for a specific VT index (0..3).
        /// Returns 0 for out-of-range indices.
        /// </summary>
        public int GetCountForVt(int vtIndex) => vtIndex switch
        {
            0 => Vt0Count,
            1 => Vt1Count,
            2 => Vt2Count,
            3 => Vt3Count,
            _ => 0
        };
    }

    public readonly struct AuxiliaryClusterReservation
    {
        public AuxiliaryClusterReservation(AuxiliaryClusterKind kind, byte slotMask)
        {
            Kind = kind;
            SlotMask = slotMask;
        }

        public AuxiliaryClusterKind Kind { get; }
        public byte SlotMask { get; }
        public int Count => BitOperations.PopCount((uint)SlotMask);
    }

    public sealed class ClusterIssuePreparation
    {
        private const int BundleWidth = 8;

        private ClusterIssuePreparation(
            ulong pc,
            DecodeMode decodeMode,
            ScalarClusterIssueGroup scalarClusterGroup,
            AuxiliaryClusterReservation[] auxiliaryReservations,
            byte fallbackDiagnosticsMask,
            DecodedBundleAdmissionPrep admissionPrep,
            DecodedBundleDependencySummary? dependencySummary)
        {
            PC = pc;
            DecodeMode = decodeMode;
            ScalarClusterGroup = scalarClusterGroup;
            AuxiliaryReservations = auxiliaryReservations;
            FallbackDiagnosticsMask = fallbackDiagnosticsMask;
            AdmissionPrep = admissionPrep;
            DependencySummary = dependencySummary;
        }

        public ulong PC { get; }
        public DecodeMode DecodeMode { get; }
        public ScalarClusterIssueGroup ScalarClusterGroup { get; }
        public AuxiliaryClusterReservation[] AuxiliaryReservations { get; }

        /// <summary>
        /// Decode-side residual mask of non-empty slots that were not prepared for cluster issue.
        /// Diagnostics-only contour for the narrow reference path; not a runtime semantic owner for
        /// system/CSR/privileged behavior.
        /// </summary>
        public byte FallbackDiagnosticsMask { get; }

        public DecodedBundleAdmissionPrep AdmissionPrep { get; }
        public DecodedBundleDependencySummary? DependencySummary { get; }
        public bool HasScalarClusterCandidate => AdmissionPrep.ScalarCandidateMask != 0;

        /// <summary>
        /// Advisory decode hint that the narrow reference path remains relevant for some slots.
        /// Diagnostics-only and intentionally separate from runtime legality/FSM authority.
        /// </summary>
        public bool SuggestFallbackDiagnostics => AdmissionPrep.SuggestNarrowFallback;

        /// <summary>
        /// Create an empty handoff object for cases where no bundle has been decoded yet.
        /// </summary>
        public static ClusterIssuePreparation CreateEmpty(ulong pc = 0)
        {
            return new ClusterIssuePreparation(
                pc,
                DecodeMode.ReferenceSequentialMode,
                new ScalarClusterIssueGroup(0, 0, []),
                [],
                fallbackDiagnosticsMask: 0,
                admissionPrep: default,
                dependencySummary: null);
        }

        internal static ClusterIssuePreparation Create(
            ulong pc,
            IReadOnlyList<DecodedBundleSlotDescriptor> slots,
            DecodedBundleAdmissionPrep admissionPrep,
            DecodedBundleDependencySummary? dependencySummary)
        {
            ArgumentNullException.ThrowIfNull(slots);

            byte preparedScalarMask = ResolvePreparedScalarMask(
                admissionPrep,
                dependencySummary);
            Legality.ClusterIssueIntent clusterIssueIntent =
                Legality.ClusterIssueIntentBuilder.Build(slots, preparedScalarMask);
            ScalarClusterIssueGroup scalarClusterGroup = clusterIssueIntent.ToScalarClusterIssueGroup();
            AuxiliaryClusterReservation[] auxiliaryReservations =
                BuildAuxiliaryReservations(slots, admissionPrep.AuxiliaryOpMask);

            return CreateCore(
                pc,
                scalarClusterGroup,
                auxiliaryReservations,
                BuildFallbackDiagnosticsMask(slots, preparedScalarMask),
                admissionPrep,
                dependencySummary);
        }

        /// <summary>
        /// Create the advisory cluster handoff object from decoder-prepared metadata.
        /// Final acceptance remains on the runtime side.
        /// </summary>
        private static ClusterIssuePreparation CreateCore(
            ulong pc,
            ScalarClusterIssueGroup scalarClusterGroup,
            AuxiliaryClusterReservation[] auxiliaryReservations,
            byte fallbackDiagnosticsMask,
            DecodedBundleAdmissionPrep admissionPrep,
            DecodedBundleDependencySummary? dependencySummary)
        {
            DecodeMode decodeMode = admissionPrep.ScalarCandidateMask != 0
                ? DecodeMode.ClusterPreparedMode
                : DecodeMode.ReferenceSequentialMode;

            return new ClusterIssuePreparation(
                pc,
                decodeMode,
                scalarClusterGroup,
                auxiliaryReservations,
                fallbackDiagnosticsMask,
                admissionPrep,
                dependencySummary);
        }

        private static byte ResolvePreparedScalarMask(
            DecodedBundleAdmissionPrep admissionPrep,
            DecodedBundleDependencySummary? dependencySummary)
        {
            byte preparedScalarMask = admissionPrep.WideReadyScalarMask;
            if (!dependencySummary.HasValue)
            {
                return preparedScalarMask;
            }

            byte refinedPreparedScalarMask =
                dependencySummary.Value.ComputeRefinedWideReadyScalarMask(admissionPrep.ScalarCandidateMask);
            return BitOperations.PopCount((uint)refinedPreparedScalarMask) > BitOperations.PopCount((uint)preparedScalarMask)
                ? refinedPreparedScalarMask
                : preparedScalarMask;
        }

        private static byte BuildFallbackDiagnosticsMask(
            IReadOnlyList<DecodedBundleSlotDescriptor> slots,
            byte preparedScalarMask)
        {
            byte nonEmptyMask = 0;
            for (int slotIndex = 0; slotIndex < BundleWidth; slotIndex++)
            {
                if (slots[slotIndex].GetRuntimeExecutionIsEmptyOrNop())
                    continue;

                nonEmptyMask |= (byte)(1 << slotIndex);
            }

            return (byte)(nonEmptyMask & ~preparedScalarMask);
        }

        private static AuxiliaryClusterReservation[] BuildAuxiliaryReservations(
            IReadOnlyList<DecodedBundleSlotDescriptor> slots,
            byte auxiliaryOpMask)
        {
            byte vectorMask = 0;
            byte memoryMask = 0;
            byte dmaStreamMask = 0;
            byte controlMask = 0;
            byte systemMask = 0;
            byte otherMask = 0;

            for (int slotIndex = 0; slotIndex < BundleWidth; slotIndex++)
            {
                byte slotBit = (byte)(1 << slotIndex);
                if ((auxiliaryOpMask & slotBit) == 0)
                    continue;

                DecodedBundleSlotDescriptor slot = slots[slotIndex];
                switch (ClassifyAuxiliaryReservation(slot))
                {
                    case AuxiliaryClusterKind.Vector:
                        vectorMask |= slotBit;
                        break;
                    case AuxiliaryClusterKind.Memory:
                        memoryMask |= slotBit;
                        break;
                    case AuxiliaryClusterKind.DmaStream:
                        dmaStreamMask |= slotBit;
                        break;
                    case AuxiliaryClusterKind.Control:
                        controlMask |= slotBit;
                        break;
                    case AuxiliaryClusterKind.System:
                        systemMask |= slotBit;
                        break;
                    default:
                        otherMask |= slotBit;
                        break;
                }
            }

            AuxiliaryClusterReservation[] reservations = new AuxiliaryClusterReservation[6];
            int count = 0;
            AppendReservation(ref reservations, ref count, AuxiliaryClusterKind.Vector, vectorMask);
            AppendReservation(ref reservations, ref count, AuxiliaryClusterKind.Memory, memoryMask);
            AppendReservation(ref reservations, ref count, AuxiliaryClusterKind.DmaStream, dmaStreamMask);
            AppendReservation(ref reservations, ref count, AuxiliaryClusterKind.Control, controlMask);
            AppendReservation(ref reservations, ref count, AuxiliaryClusterKind.System, systemMask);
            AppendReservation(ref reservations, ref count, AuxiliaryClusterKind.Other, otherMask);

            AuxiliaryClusterReservation[] result = new AuxiliaryClusterReservation[count];
            Array.Copy(reservations, result, count);
            return result;
        }

        private static AuxiliaryClusterKind ClassifyAuxiliaryReservation(DecodedBundleSlotDescriptor slot)
        {
            SlotPlacementMetadata placement = slot.GetRuntimeAdmissionPlacement();
            bool isMemoryOp = slot.GetRuntimeAdmissionIsMemoryOp();
            bool isControlFlow = slot.GetRuntimeAdmissionIsControlFlow();

            if (slot.IsVectorOp)
                return AuxiliaryClusterKind.Vector;
            if (isMemoryOp)
                return AuxiliaryClusterKind.Memory;
            if (placement.RequiredSlotClass == SlotClass.DmaStreamClass)
                return AuxiliaryClusterKind.DmaStream;
            if (isControlFlow || placement.RequiredSlotClass == SlotClass.BranchControl)
                return AuxiliaryClusterKind.Control;
            if (placement.RequiredSlotClass == SlotClass.SystemSingleton)
                return AuxiliaryClusterKind.System;

            return AuxiliaryClusterKind.Other;
        }

        private static void AppendReservation(
            ref AuxiliaryClusterReservation[] reservations,
            ref int count,
            AuxiliaryClusterKind kind,
            byte slotMask)
        {
            if (slotMask == 0)
                return;

            reservations[count] = new AuxiliaryClusterReservation(kind, slotMask);
            count++;
        }
    }
}
