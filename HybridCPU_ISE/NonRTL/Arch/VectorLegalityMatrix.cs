using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.Arch
{
    public enum VectorContourKind : byte
    {
        OneDimensional = 0,
        IndexedAddressing = 1,
        TwoDimensionalAddressing = 2,
        Masked = 3,
        TailMaskPolicy = 4,
        Reduction = 5,
        DescriptorBacked = 6
    }

    public enum VectorContourLegalityStatus : byte
    {
        NotApplicable = 0,
        Executable = 1,
        FailClosed = 2,
        DescriptorOnly = 3
    }

    public sealed class VectorLegalityMatrixRow
    {
        public VectorLegalityMatrixRow(
            string familyName,
            IReadOnlyList<InstructionsEnum> opcodes,
            VectorContourLegalityStatus oneDimensional,
            VectorContourLegalityStatus indexedAddressing,
            VectorContourLegalityStatus twoDimensionalAddressing,
            VectorContourLegalityStatus masked,
            VectorContourLegalityStatus tailMaskPolicy,
            VectorContourLegalityStatus reduction,
            VectorContourLegalityStatus descriptorBacked,
            string runtimeEvidenceNote)
        {
            FamilyName = string.IsNullOrWhiteSpace(familyName)
                ? throw new ArgumentException("Vector family name must be non-empty.", nameof(familyName))
                : familyName;
            Opcodes = opcodes ?? throw new ArgumentNullException(nameof(opcodes));
            OneDimensional = oneDimensional;
            IndexedAddressing = indexedAddressing;
            TwoDimensionalAddressing = twoDimensionalAddressing;
            Masked = masked;
            TailMaskPolicy = tailMaskPolicy;
            Reduction = reduction;
            DescriptorBacked = descriptorBacked;
            RuntimeEvidenceNote = runtimeEvidenceNote ?? string.Empty;
        }

        public string FamilyName { get; }

        public IReadOnlyList<InstructionsEnum> Opcodes { get; }

        public VectorContourLegalityStatus OneDimensional { get; }

        public VectorContourLegalityStatus IndexedAddressing { get; }

        public VectorContourLegalityStatus TwoDimensionalAddressing { get; }

        public VectorContourLegalityStatus Masked { get; }

        public VectorContourLegalityStatus TailMaskPolicy { get; }

        public VectorContourLegalityStatus Reduction { get; }

        public VectorContourLegalityStatus DescriptorBacked { get; }

        public string RuntimeEvidenceNote { get; }

        public VectorContourLegalityStatus GetContourStatus(VectorContourKind contour) =>
            contour switch
            {
                VectorContourKind.OneDimensional => OneDimensional,
                VectorContourKind.IndexedAddressing => IndexedAddressing,
                VectorContourKind.TwoDimensionalAddressing => TwoDimensionalAddressing,
                VectorContourKind.Masked => Masked,
                VectorContourKind.TailMaskPolicy => TailMaskPolicy,
                VectorContourKind.Reduction => Reduction,
                VectorContourKind.DescriptorBacked => DescriptorBacked,
                _ => throw new ArgumentOutOfRangeException(nameof(contour), contour, null)
            };
    }

    /// <summary>
    /// Runtime-owned vector contour evidence matrix. This table is declaration/evidence
    /// for current runtime legality; opcode metadata, parser acceptance, and sideband
    /// descriptors do not override it.
    /// </summary>
    public static class VectorLegalityMatrix
    {
        private static readonly VectorLegalityMatrixRow[] s_rows = BuildRows();
        private static readonly IReadOnlyDictionary<InstructionsEnum, VectorLegalityMatrixRow> s_byOpcode =
            BuildOpcodeIndex(s_rows);

        public static IReadOnlyList<VectorLegalityMatrixRow> Rows => s_rows;

        public static bool TryGetRow(
            InstructionsEnum opcode,
            [NotNullWhen(true)]
            out VectorLegalityMatrixRow? row)
        {
            return s_byOpcode.TryGetValue(opcode, out row);
        }

        public static VectorLegalityMatrixRow GetRow(InstructionsEnum opcode)
        {
            if (TryGetRow(opcode, out VectorLegalityMatrixRow? row))
            {
                return row;
            }

            throw new InvalidOperationException(
                $"Vector opcode {opcode} has no runtime-owned vector legality matrix row.");
        }

        public static bool TryGetAddressingStatus(
            InstructionsEnum opcode,
            bool indexed,
            bool is2D,
            out VectorContourLegalityStatus status)
        {
            if (!TryGetRow(opcode, out VectorLegalityMatrixRow? row))
            {
                status = VectorContourLegalityStatus.FailClosed;
                return false;
            }

            status = ResolveAddressingStatus(row, indexed, is2D);
            return true;
        }

        public static VectorContourLegalityStatus GetAddressingStatus(
            InstructionsEnum opcode,
            bool indexed,
            bool is2D)
        {
            VectorLegalityMatrixRow row = GetRow(opcode);
            return ResolveAddressingStatus(row, indexed, is2D);
        }

        public static bool AllowsAddressingExecution(
            InstructionsEnum opcode,
            bool indexed,
            bool is2D)
        {
            return GetAddressingStatus(opcode, indexed, is2D) ==
                   VectorContourLegalityStatus.Executable;
        }

        private static VectorContourLegalityStatus ResolveAddressingStatus(
            VectorLegalityMatrixRow row,
            bool indexed,
            bool is2D)
        {
            if (indexed && is2D)
            {
                return CombineAddressingStatuses(
                    row.IndexedAddressing,
                    row.TwoDimensionalAddressing);
            }

            if (indexed)
            {
                return row.IndexedAddressing;
            }

            return is2D
                ? row.TwoDimensionalAddressing
                : row.OneDimensional;
        }

        private static VectorContourLegalityStatus CombineAddressingStatuses(
            VectorContourLegalityStatus first,
            VectorContourLegalityStatus second)
        {
            if (first == VectorContourLegalityStatus.Executable &&
                second == VectorContourLegalityStatus.Executable)
            {
                return VectorContourLegalityStatus.Executable;
            }

            if (first == VectorContourLegalityStatus.FailClosed ||
                second == VectorContourLegalityStatus.FailClosed)
            {
                return VectorContourLegalityStatus.FailClosed;
            }

            if (first == VectorContourLegalityStatus.DescriptorOnly ||
                second == VectorContourLegalityStatus.DescriptorOnly)
            {
                return VectorContourLegalityStatus.DescriptorOnly;
            }

            return VectorContourLegalityStatus.NotApplicable;
        }

        private static IReadOnlyDictionary<InstructionsEnum, VectorLegalityMatrixRow> BuildOpcodeIndex(
            IReadOnlyList<VectorLegalityMatrixRow> rows)
        {
            var byOpcode = new Dictionary<InstructionsEnum, VectorLegalityMatrixRow>();
            foreach (VectorLegalityMatrixRow row in rows)
            {
                foreach (InstructionsEnum opcode in row.Opcodes)
                {
                    byOpcode[opcode] = row;
                }
            }

            return byOpcode;
        }

        private static VectorLegalityMatrixRow[] BuildRows()
        {
            const VectorContourLegalityStatus na = VectorContourLegalityStatus.NotApplicable;
            const VectorContourLegalityStatus executable = VectorContourLegalityStatus.Executable;
            const VectorContourLegalityStatus failClosed = VectorContourLegalityStatus.FailClosed;
            const VectorContourLegalityStatus descriptorOnly = VectorContourLegalityStatus.DescriptorOnly;

            return
            [
                Row(
                    "VectorConfigSystemSingleton",
                    [InstructionsEnum.VSETVL, InstructionsEnum.VSETVLI, InstructionsEnum.VSETIVLI],
                    na,
                    failClosed,
                    failClosed,
                    na,
                    executable,
                    na,
                    na,
                    "Vector configuration is a lane7 system-singleton contour; indexed/2D vector payloads are not execution authority."),

                Row(
                    "VectorBinaryComputeCarrier",
                    [InstructionsEnum.VADD, InstructionsEnum.VSUB, InstructionsEnum.VMUL, InstructionsEnum.VDIV,
                     InstructionsEnum.VMOD, InstructionsEnum.VXOR, InstructionsEnum.VOR, InstructionsEnum.VAND,
                     InstructionsEnum.VSLL, InstructionsEnum.VSRL, InstructionsEnum.VSRA, InstructionsEnum.VMIN,
                     InstructionsEnum.VMAX, InstructionsEnum.VMINU, InstructionsEnum.VMAXU],
                    executable,
                    failClosed,
                    failClosed,
                    executable,
                    executable,
                    na,
                    na,
                    "Only the 1D in-place memory contour has MicroOp materialization and execution evidence."),

                Row(
                    "VectorUnaryComputeCarrier",
                    [InstructionsEnum.VSQRT, InstructionsEnum.VNOT, InstructionsEnum.VREVERSE, InstructionsEnum.VPOPCNT,
                     InstructionsEnum.VCLZ, InstructionsEnum.VCTZ, InstructionsEnum.VBREV8],
                    executable,
                    failClosed,
                    failClosed,
                    executable,
                    executable,
                    na,
                    na,
                    "Only the 1D unary vector contour has MicroOp materialization and execution evidence."),

                Row(
                    "VectorReductionScalarFootprintCarrier",
                    [InstructionsEnum.VREDSUM, InstructionsEnum.VREDMAX, InstructionsEnum.VREDMIN,
                     InstructionsEnum.VREDMAXU, InstructionsEnum.VREDMINU, InstructionsEnum.VREDAND,
                     InstructionsEnum.VREDOR, InstructionsEnum.VREDXOR],
                    executable,
                    failClosed,
                    failClosed,
                    executable,
                    executable,
                    executable,
                    na,
                    "Reduction is executable only on the 1D scalar-footprint contour."),

                Row(
                    "VectorFmaDescriptorBacked",
                    [InstructionsEnum.VFMADD, InstructionsEnum.VFMSUB, InstructionsEnum.VFNMADD, InstructionsEnum.VFNMSUB],
                    executable,
                    failClosed,
                    failClosed,
                    executable,
                    executable,
                    na,
                    executable,
                    "FMA execution requires the descriptor-backed 1D contour; descriptor-less, indexed, and 2D contours fail closed."),

                Row(
                    "VectorDotProductScalarFootprint",
                    [InstructionsEnum.VDOT, InstructionsEnum.VDOTU, InstructionsEnum.VDOTF, InstructionsEnum.VDOT_FP8],
                    executable,
                    failClosed,
                    failClosed,
                    executable,
                    na,
                    executable,
                    failClosed,
                    "Dot-product execution is limited to the current 1D scalar-footprint ABI; indexed/2D and descriptor-backed ABI variants fail closed."),

                Row(
                    "VectorDotProductWideScalarFootprint",
                    [InstructionsEnum.VDOT_WIDE],
                    executable,
                    failClosed,
                    failClosed,
                    executable,
                    na,
                    executable,
                    failClosed,
                    "VDOT.WIDE is executable only for the packed 1D FP16/BF16/FP8-to-FP32 and INT8/UINT8-to-32-bit scalar-footprint contours; indexed/2D, descriptor-backed, wider integer, block-scaled, and separate-destination variants fail closed."),

                Row(
                    "VectorTransferCarrier",
                    [InstructionsEnum.VLOAD, InstructionsEnum.VSTORE],
                    executable,
                    failClosed,
                    failClosed,
                    na,
                    na,
                    na,
                    na,
                    "VLOAD/VSTORE publish only the 1D transfer-shape carrier; indexed/2D transfer contours fail closed."),

                Row(
                    "VectorPredicateMaskPublication",
                    [InstructionsEnum.VMAND, InstructionsEnum.VMOR, InstructionsEnum.VMXOR, InstructionsEnum.VMNOT],
                    executable,
                    failClosed,
                    failClosed,
                    na,
                    na,
                    na,
                    na,
                    "Predicate-mask logical publication has no addressed vector-surface contour."),

                Row(
                    "VectorMaskPrefixPublication",
                    [InstructionsEnum.VMSBF],
                    executable,
                    failClosed,
                    failClosed,
                    na,
                    executable,
                    na,
                    na,
                    "VMSBF is executable only as a 1D predicate-mask prefix publication contour; VMSIF/VMSOF and vector select/merge remain closed."),

                Row(
                    "VectorZeroExtendPublication",
                    [InstructionsEnum.VZEXT],
                    executable,
                    failClosed,
                    failClosed,
                    executable,
                    executable,
                    na,
                    na,
                    "VZEXT is executable only as a packed 1D unsigned zero-extension memory-to-memory contour; other widen/narrow/convert forms remain closed."),

                Row(
                    "VectorScanPrefixPublication",
                    [InstructionsEnum.VSCAN_SUM],
                    executable,
                    failClosed,
                    failClosed,
                    executable,
                    executable,
                    na,
                    na,
                    "VSCAN.SUM is executable only as a packed 1D integer inclusive-prefix sum contour; VSCAN.MIN/MAX, segment movement, indexed/2D, and matrix/tile remain closed."),

                Row(
                    "VectorPredicateMaskScalarResult",
                    [InstructionsEnum.VPOPC],
                    executable,
                    failClosed,
                    failClosed,
                    na,
                    na,
                    executable,
                    na,
                    "VPOPC is executable only as a predicate-mask scalar-result contour."),

                Row(
                    "VectorComparisonPredicatePublication",
                    [InstructionsEnum.VCMPEQ, InstructionsEnum.VCMPNE, InstructionsEnum.VCMPLT,
                     InstructionsEnum.VCMPLE, InstructionsEnum.VCMPGT, InstructionsEnum.VCMPGE],
                    executable,
                    failClosed,
                    failClosed,
                    executable,
                    executable,
                    na,
                    na,
                    "Comparison publication is executable only for the 1D predicate-publication contour."),

                Row(
                    "VectorPredicativeMovement",
                    [InstructionsEnum.VCOMPRESS, InstructionsEnum.VEXPAND],
                    executable,
                    failClosed,
                    failClosed,
                    executable,
                    executable,
                    na,
                    na,
                    "Predicative movement is executable only for the 1D single-surface contour."),

                Row(
                    "VectorPermutation",
                    [InstructionsEnum.VPERMUTE, InstructionsEnum.VRGATHER],
                    executable,
                    failClosed,
                    failClosed,
                    executable,
                    executable,
                    na,
                    na,
                    "Permutation consumes an index vector on the 1D two-surface contour; it is not vector indexed-memory addressing."),

                Row(
                    "VectorPermute2Publication",
                    [InstructionsEnum.VPERM2],
                    executable,
                    failClosed,
                    failClosed,
                    executable,
                    executable,
                    na,
                    na,
                    "VPERM2 is executable only for a packed 1D two-source two-lane immediate-controlled contour."),

                Row(
                    "VectorTransposePublication",
                    [InstructionsEnum.VTRANSPOSE],
                    executable,
                    failClosed,
                    failClosed,
                    executable,
                    executable,
                    na,
                    failClosed,
                    "VTRANSPOSE is executable only for a packed 1D single-surface fixed 2x2 in-place transpose contour."),

                Row(
                    "VectorSlide",
                    [InstructionsEnum.VSLIDEUP, InstructionsEnum.VSLIDEDOWN],
                    executable,
                    failClosed,
                    failClosed,
                    executable,
                    executable,
                    na,
                    na,
                    "Slide is executable only for the 1D single-surface contour."),

                Row(
                    "VectorSlideOnePublication",
                    [InstructionsEnum.VSLIDE1UP, InstructionsEnum.VSLIDE1DOWN],
                    executable,
                    failClosed,
                    failClosed,
                    executable,
                    executable,
                    na,
                    na,
                    "VSLIDE1UP and VSLIDE1DOWN are executable only for packed 1D single-surface fixed-one-lane contours."),

                Row(
                    "VectorIndexedGatherMemory",
                    [InstructionsEnum.VGATHER],
                    failClosed,
                    executable,
                    failClosed,
                    executable,
                    executable,
                    na,
                    executable,
                    "VGATHER publishes one executable 1D indexed-read carrier backed by Indexed2SrcDesc; VSCATTER, 2D, and indexed+2D remain closed."),

                Row(
                    "VectorIndexedScatterMemory",
                    [InstructionsEnum.VSCATTER],
                    failClosed,
                    executable,
                    failClosed,
                    executable,
                    executable,
                    na,
                    executable,
                    "VSCATTER publishes one executable 1D indexed-write carrier backed by Indexed2SrcDesc; vector 2D and indexed+2D scatter remain closed.")
                ,

                Row(
                    "XMatrix",
                    [InstructionsEnum.MTILE_LOAD, InstructionsEnum.MTILE_STORE, InstructionsEnum.MTILE_MACC, InstructionsEnum.MTRANSPOSE],
                    failClosed,
                    failClosed,
                    failClosed,
                    failClosed,
                    failClosed,
                    failClosed,
                    descriptorOnly,
                    "XMatrix Phase05 publishes runtime-owned descriptor-only legality rows for MTILE_LOAD, MTILE_STORE, MTILE_MACC, and MTRANSPOSE; descriptor-backed legality references the matrix/tile descriptor, memory/fault, accumulator, and transpose ABI, while decoder, IR, MicroOp, execution, compiler emission, and fallback paths remain closed.")
            ];
        }

        private static VectorLegalityMatrixRow Row(
            string familyName,
            IReadOnlyList<InstructionsEnum> opcodes,
            VectorContourLegalityStatus oneDimensional,
            VectorContourLegalityStatus indexedAddressing,
            VectorContourLegalityStatus twoDimensionalAddressing,
            VectorContourLegalityStatus masked,
            VectorContourLegalityStatus tailMaskPolicy,
            VectorContourLegalityStatus reduction,
            VectorContourLegalityStatus descriptorBacked,
            string runtimeEvidenceNote)
        {
            return new VectorLegalityMatrixRow(
                familyName,
                opcodes,
                oneDimensional,
                indexedAddressing,
                twoDimensionalAddressing,
                masked,
                tailMaskPolicy,
                reduction,
                descriptorBacked,
                runtimeEvidenceNote);
        }
    }
}
