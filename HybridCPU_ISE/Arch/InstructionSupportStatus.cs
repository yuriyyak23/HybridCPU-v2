using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Arch
{
    public enum IsaInstructionStatus : byte
    {
        Mandatory = 0,
        OptionalEnabled = 1,
        OptionalDisabled = 2,
        Reserved = 3,
        LegacyRetained = 4,
        ParserOnly = 5,
        DescriptorOnly = 6,
        Prohibited = 7,
        CarrierOnly = 8
    }

    public enum RuntimeInstructionEvidence : byte
    {
        None = 0,
        DeclaredOnly = 1,
        DecoderAccepted = 2,
        DescriptorProjected = 3,
        Materialized = 4,
        Executable = 5,
        RetireVisible = 6,
        ConformanceTested = 7
    }

    public readonly record struct InstructionSupportStatus
    {
        public InstructionSupportStatus(
            string mnemonic,
            IsaInstructionStatus status,
            RuntimeInstructionEvidence runtimeEvidence,
            string extensionName = "",
            bool hasNumericOpcode = false,
            bool hasRuntimeOpcodeMetadata = false,
            bool hasCanonicalDecoderAcceptance = false,
            bool hasRegistryFactory = false,
            bool hasExecutionSemantics = false)
        {
            Mnemonic = string.IsNullOrWhiteSpace(mnemonic)
                ? throw new ArgumentException("Instruction mnemonic must be non-empty.", nameof(mnemonic))
                : mnemonic;
            Status = status;
            RuntimeEvidence = runtimeEvidence;
            ExtensionName = extensionName ?? string.Empty;
            HasNumericOpcode = hasNumericOpcode;
            HasRuntimeOpcodeMetadata = hasRuntimeOpcodeMetadata;
            HasCanonicalDecoderAcceptance = hasCanonicalDecoderAcceptance;
            HasRegistryFactory = hasRegistryFactory;
            HasExecutionSemantics = hasExecutionSemantics;
        }

        public string Mnemonic { get; }

        public IsaInstructionStatus Status { get; }

        public RuntimeInstructionEvidence RuntimeEvidence { get; }

        public string ExtensionName { get; }

        public bool HasNumericOpcode { get; }

        public bool HasRuntimeOpcodeMetadata { get; }

        public bool HasCanonicalDecoderAcceptance { get; }

        public bool HasRegistryFactory { get; }

        public bool HasExecutionSemantics { get; }

        public bool IsExecutableClaim =>
            Status is IsaInstructionStatus.Mandatory or IsaInstructionStatus.OptionalEnabled &&
            RuntimeEvidence >= RuntimeInstructionEvidence.Executable &&
            HasExecutionSemantics;
    }

    /// <summary>
    /// Runtime-owned support terminology for the instruction refactor inventory.
    /// This catalog is a declaration/evidence surface only; it is not a legality
    /// service and does not participate in decoder, scheduler, or execution authority.
    /// </summary>
    public static class InstructionSupportStatusCatalog
    {
        private static readonly string[] s_mandatoryInteger64RepairMnemonics =
        {
            "SRA",
            "ADDIW",
            "ADDW",
            "SUBW",
            "SLLW",
            "SRLW",
            "SRAW",
            "SLLIW",
            "SRLIW",
            "SRAIW",
            "MULW",
            "DIVW",
            "DIVUW",
            "REMW",
            "REMUW",
            "SEXT.W",
            "ZEXT.W"
        };

        private static readonly InstructionSupportStatus[] s_explicitStatuses =
            BuildExplicitStatuses();

        private static readonly IReadOnlyDictionary<string, InstructionSupportStatus> s_byMnemonic =
            BuildMnemonicIndex(s_explicitStatuses);

        public static IReadOnlyList<string> MandatoryInteger64RepairMnemonics =>
            s_mandatoryInteger64RepairMnemonics;

        public static IReadOnlyList<InstructionSupportStatus> ExplicitStatuses =>
            s_explicitStatuses;

        public static bool TryGetExplicitStatus(
            string mnemonic,
            out InstructionSupportStatus status)
        {
            if (string.IsNullOrWhiteSpace(mnemonic))
            {
                status = default;
                return false;
            }

            return s_byMnemonic.TryGetValue(NormalizeMnemonic(mnemonic), out status);
        }

        public static InstructionSupportStatus GetStatus(string mnemonic)
        {
            if (TryGetExplicitStatus(mnemonic, out InstructionSupportStatus status))
            {
                return status;
            }

            string normalizedMnemonic = NormalizeMnemonic(mnemonic);
            if (IsaV4Surface.ProhibitedOpcodes.Contains(normalizedMnemonic))
            {
                return new InstructionSupportStatus(
                    normalizedMnemonic,
                    IsaInstructionStatus.Prohibited,
                    RuntimeInstructionEvidence.None);
            }

            if (IsaV4Surface.OptionalExtensions.Contains(normalizedMnemonic))
            {
                return new InstructionSupportStatus(
                    normalizedMnemonic,
                    IsaInstructionStatus.OptionalDisabled,
                    RuntimeInstructionEvidence.DeclaredOnly,
                    extensionName: "OptionalScalarExtension");
            }

            if (IsaV4Surface.MandatoryCoreOpcodes.Contains(normalizedMnemonic))
            {
                bool hasOpcodeMetadata = TryFindOpcodeInfoByMnemonic(
                    normalizedMnemonic,
                    out OpcodeInfo opcodeInfo);
                return new InstructionSupportStatus(
                    normalizedMnemonic,
                    IsaInstructionStatus.Mandatory,
                    hasOpcodeMetadata
                        ? RuntimeInstructionEvidence.DeclaredOnly
                        : RuntimeInstructionEvidence.None,
                    hasNumericOpcode: hasOpcodeMetadata,
                    hasRuntimeOpcodeMetadata: hasOpcodeMetadata,
                    hasCanonicalDecoderAcceptance: hasOpcodeMetadata,
                    hasRegistryFactory: false,
                    hasExecutionSemantics: false);
            }

            return new InstructionSupportStatus(
                normalizedMnemonic,
                IsaInstructionStatus.Reserved,
                RuntimeInstructionEvidence.None);
        }

        public static bool TryGetStatus(
            Processor.CPU_Core.InstructionsEnum opcode,
            out InstructionSupportStatus status)
        {
            string enumName = opcode.ToString();
            if (TryGetExplicitStatus(enumName, out status))
            {
                return true;
            }

            if (OpcodeRegistry.TryGetMnemonic((uint)opcode, out string mnemonic) &&
                TryGetExplicitStatus(mnemonic, out status))
            {
                return true;
            }

            status = GetStatus(
                OpcodeRegistry.TryGetMnemonic((uint)opcode, out mnemonic)
                    ? mnemonic
                    : enumName);
            return true;
        }

        private static InstructionSupportStatus[] BuildExplicitStatuses()
        {
            var rows = new List<InstructionSupportStatus>();

            rows.Add(new InstructionSupportStatus(
                "SRA",
                IsaInstructionStatus.Mandatory,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "Integer64Repair",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));

            rows.Add(new InstructionSupportStatus(
                "ADDIW",
                IsaInstructionStatus.Mandatory,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "Integer64Repair",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));

            rows.Add(new InstructionSupportStatus(
                "ADDW",
                IsaInstructionStatus.Mandatory,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "Integer64Repair",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));

            rows.Add(new InstructionSupportStatus(
                "SUBW",
                IsaInstructionStatus.Mandatory,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "Integer64Repair",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));

            rows.Add(new InstructionSupportStatus(
                "SLLW",
                IsaInstructionStatus.Mandatory,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "Integer64Repair",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));

            rows.Add(new InstructionSupportStatus(
                "SRLW",
                IsaInstructionStatus.Mandatory,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "Integer64Repair",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));

            rows.Add(new InstructionSupportStatus(
                "SRAW",
                IsaInstructionStatus.Mandatory,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "Integer64Repair",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));

            rows.Add(new InstructionSupportStatus(
                "SLLIW",
                IsaInstructionStatus.Mandatory,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "Integer64Repair",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));

            rows.Add(new InstructionSupportStatus(
                "SRLIW",
                IsaInstructionStatus.Mandatory,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "Integer64Repair",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));

            rows.Add(new InstructionSupportStatus(
                "SRAIW",
                IsaInstructionStatus.Mandatory,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "Integer64Repair",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));

            rows.Add(new InstructionSupportStatus(
                "MULW",
                IsaInstructionStatus.Mandatory,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "Integer64Repair",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));

            rows.Add(new InstructionSupportStatus(
                "DIVW",
                IsaInstructionStatus.Mandatory,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "Integer64Repair",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));

            rows.Add(new InstructionSupportStatus(
                "DIVUW",
                IsaInstructionStatus.Mandatory,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "Integer64Repair",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));

            rows.Add(new InstructionSupportStatus(
                "REMW",
                IsaInstructionStatus.Mandatory,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "Integer64Repair",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));

            rows.Add(new InstructionSupportStatus(
                "REMUW",
                IsaInstructionStatus.Mandatory,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "Integer64Repair",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));

            rows.Add(new InstructionSupportStatus(
                "SEXT.W",
                IsaInstructionStatus.Mandatory,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "Integer64Repair",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));

            rows.Add(new InstructionSupportStatus(
                "ZEXT.W",
                IsaInstructionStatus.Mandatory,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "Integer64Repair",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));

            foreach (string mnemonic in s_mandatoryInteger64RepairMnemonics)
            {
                if (string.Equals(mnemonic, "SRA", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(mnemonic, "ADDIW", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(mnemonic, "ADDW", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(mnemonic, "SUBW", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(mnemonic, "SLLW", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(mnemonic, "SRLW", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(mnemonic, "SRAW", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(mnemonic, "SLLIW", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(mnemonic, "SRLIW", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(mnemonic, "SRAIW", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(mnemonic, "MULW", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(mnemonic, "DIVW", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(mnemonic, "DIVUW", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(mnemonic, "REMW", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(mnemonic, "REMUW", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(mnemonic, "SEXT.W", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(mnemonic, "ZEXT.W", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                rows.Add(new InstructionSupportStatus(
                    mnemonic,
                    IsaInstructionStatus.Mandatory,
                    RuntimeInstructionEvidence.None,
                    extensionName: "Integer64Repair"));
            }

            rows.Add(new InstructionSupportStatus(
                "VGATHER",
                IsaInstructionStatus.DescriptorOnly,
                RuntimeInstructionEvidence.DecoderAccepted,
                extensionName: "VectorIndexedMemory",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true));
            rows.Add(new InstructionSupportStatus(
                "VSCATTER",
                IsaInstructionStatus.DescriptorOnly,
                RuntimeInstructionEvidence.DecoderAccepted,
                extensionName: "VectorIndexedMemory",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true));

            rows.Add(new InstructionSupportStatus(
                "DmaStreamCompute",
                IsaInstructionStatus.DescriptorOnly,
                RuntimeInstructionEvidence.DescriptorProjected,
                extensionName: "Lane6DSC",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true));
            rows.Add(new InstructionSupportStatus(
                "DSC2",
                IsaInstructionStatus.ParserOnly,
                RuntimeInstructionEvidence.DeclaredOnly,
                extensionName: "Lane6DSC"));

            rows.Add(new InstructionSupportStatus(
                "ACCEL_SUBMIT",
                IsaInstructionStatus.DescriptorOnly,
                RuntimeInstructionEvidence.DescriptorProjected,
                extensionName: "Lane7L7SDC",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true));

            AddCarrierOnlyL7Sdc(rows, "ACCEL_QUERY_CAPS");
            AddCarrierOnlyL7Sdc(rows, "ACCEL_POLL");
            AddCarrierOnlyL7Sdc(rows, "ACCEL_WAIT");
            AddCarrierOnlyL7Sdc(rows, "ACCEL_CANCEL");
            AddCarrierOnlyL7Sdc(rows, "ACCEL_FENCE");

            AddOptionalEnabledVectorConfigSystemSingleton(rows, "VSETVL");
            AddOptionalEnabledVectorConfigSystemSingleton(rows, "VSETVLI");
            AddOptionalEnabledVectorConfigSystemSingleton(rows, "VSETIVLI");

            AddOptionalEnabledVectorBinaryCompute(rows, "VADD");
            AddOptionalEnabledVectorBinaryCompute(rows, "VSUB");
            AddOptionalEnabledVectorBinaryCompute(rows, "VMUL");
            AddOptionalEnabledVectorBinaryCompute(rows, "VDIV");
            AddOptionalEnabledVectorUnaryCompute(rows, "VSQRT");
            AddOptionalEnabledVectorBinaryCompute(rows, "VMOD");

            AddOptionalEnabledVectorTransfer(rows, "VLOAD");
            AddOptionalEnabledVectorTransfer(rows, "VSTORE");

            AddOptionalEnabledVectorBinaryCompute(rows, "VXOR");
            AddOptionalEnabledVectorBinaryCompute(rows, "VOR");
            AddOptionalEnabledVectorBinaryCompute(rows, "VAND");
            AddOptionalEnabledVectorUnaryCompute(rows, "VNOT");
            AddOptionalEnabledVectorBinaryCompute(rows, "VSLL");
            AddOptionalEnabledVectorBinaryCompute(rows, "VSRL");
            AddOptionalEnabledVectorBinaryCompute(rows, "VSRA");
            AddOptionalEnabledVectorFma(rows, "VFMADD");
            AddOptionalEnabledVectorFma(rows, "VFMSUB");
            AddOptionalEnabledVectorFma(rows, "VFNMADD");
            AddOptionalEnabledVectorFma(rows, "VFNMSUB");
            AddOptionalEnabledVectorBinaryCompute(rows, "VMIN");
            AddOptionalEnabledVectorBinaryCompute(rows, "VMAX");
            AddOptionalEnabledVectorBinaryCompute(rows, "VMINU");
            AddOptionalEnabledVectorBinaryCompute(rows, "VMAXU");

            AddOptionalEnabledPredicateMaskPublication(rows, "VMAND");
            AddOptionalEnabledPredicateMaskPublication(rows, "VMOR");
            AddOptionalEnabledPredicateMaskPublication(rows, "VMXOR");
            AddOptionalEnabledPredicateMaskPublication(rows, "VMNOT");

            AddOptionalEnabledVectorComparisonPredicatePublication(rows, "VCMPEQ");
            AddOptionalEnabledVectorComparisonPredicatePublication(rows, "VCMPNE");
            AddOptionalEnabledVectorComparisonPredicatePublication(rows, "VCMPLT");
            AddOptionalEnabledVectorComparisonPredicatePublication(rows, "VCMPLE");
            AddOptionalEnabledVectorComparisonPredicatePublication(rows, "VCMPGT");
            AddOptionalEnabledVectorComparisonPredicatePublication(rows, "VCMPGE");

            rows.Add(new InstructionSupportStatus(
                "VPOPC",
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "VectorPredicateMaskScalarResult",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));

            AddOptionalEnabledVectorPredicativeMovement(rows, "VCOMPRESS");
            AddOptionalEnabledVectorPredicativeMovement(rows, "VEXPAND");
            AddOptionalEnabledVectorPermutation(rows, "VPERMUTE");
            AddOptionalEnabledVectorPermutation(rows, "VRGATHER");
            AddOptionalEnabledVectorSlide(rows, "VSLIDEUP");
            AddOptionalEnabledVectorSlide(rows, "VSLIDEDOWN");
            AddOptionalEnabledVectorUnaryCompute(rows, "VREVERSE");
            AddOptionalEnabledVectorUnaryCompute(rows, "VPOPCNT");
            AddOptionalEnabledVectorUnaryCompute(rows, "VCLZ");
            AddOptionalEnabledVectorUnaryCompute(rows, "VCTZ");
            AddOptionalEnabledVectorUnaryCompute(rows, "VBREV8");

            AddOptionalEnabledVectorReductionScalarFootprint(rows, "VREDSUM");
            AddOptionalEnabledVectorReductionScalarFootprint(rows, "VREDMAX");
            AddOptionalEnabledVectorReductionScalarFootprint(rows, "VREDMIN");
            AddOptionalEnabledVectorReductionScalarFootprint(rows, "VREDMAXU");
            AddOptionalEnabledVectorReductionScalarFootprint(rows, "VREDMINU");
            AddOptionalEnabledVectorReductionScalarFootprint(rows, "VREDAND");
            AddOptionalEnabledVectorReductionScalarFootprint(rows, "VREDOR");
            AddOptionalEnabledVectorReductionScalarFootprint(rows, "VREDXOR");
            AddOptionalEnabledVectorDotProductScalarFootprint(rows, "VDOT");
            AddOptionalEnabledVectorDotProductScalarFootprint(rows, "VDOTU");
            AddOptionalEnabledVectorDotProductScalarFootprint(rows, "VDOTF");
            AddOptionalEnabledVectorDotProductScalarFootprint(rows, "VDOT_FP8");

            AddOptionalDisabledMatrix(rows, "MTILE_LOAD");
            AddOptionalDisabledMatrix(rows, "MTILE_STORE");
            AddOptionalDisabledMatrix(rows, "MTILE_MACC");
            AddOptionalDisabledMatrix(rows, "MTRANSPOSE");

            AddReservedNoAllocation(rows, "SFENCE.VMA", "CacheTlbCoherency");
            AddReservedNoAllocation(rows, "DCACHE_CLEAN", "CacheTlbCoherency");
            AddReservedNoAllocation(rows, "DCACHE_INVAL", "CacheTlbCoherency");
            AddReservedNoAllocation(rows, "DCACHE_FLUSH", "CacheTlbCoherency");
            AddReservedNoAllocation(rows, "ICACHE_INVAL", "CacheTlbCoherency");

            return rows.ToArray();
        }

        private static void AddOptionalEnabledVectorConfigSystemSingleton(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "VectorConfigSystemSingleton",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
        }

        private static void AddOptionalEnabledVectorTransfer(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "VectorTransferCarrier",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
        }

        private static void AddOptionalEnabledVectorBinaryCompute(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "VectorBinaryComputeCarrier",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
        }

        private static void AddOptionalEnabledVectorUnaryCompute(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "VectorUnaryComputeCarrier",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
        }

        private static void AddOptionalEnabledVectorFma(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "VectorFmaDescriptorBacked",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
        }

        private static void AddOptionalEnabledVectorReductionScalarFootprint(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "VectorReductionScalarFootprintCarrier",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
        }

        private static void AddOptionalEnabledVectorDotProductScalarFootprint(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "VectorDotProductScalarFootprint",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
        }

        private static void AddOptionalEnabledPredicateMaskPublication(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "VectorPredicateMaskPublication",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
        }

        private static void AddOptionalEnabledVectorComparisonPredicatePublication(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "VectorComparisonPredicatePublication",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
        }

        private static void AddOptionalEnabledVectorPredicativeMovement(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "VectorPredicativeMovement",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
        }

        private static void AddOptionalEnabledVectorPermutation(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "VectorPermutation",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
        }

        private static void AddOptionalEnabledVectorSlide(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "VectorSlide",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
        }

        private static void AddOptionalDisabledMatrix(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalDisabled,
                RuntimeInstructionEvidence.DeclaredOnly,
                extensionName: "XMatrix",
                hasNumericOpcode: true));
        }

        private static void AddCarrierOnlyL7Sdc(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.CarrierOnly,
                RuntimeInstructionEvidence.Materialized,
                extensionName: "Lane7L7SDC",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: false));
        }

        private static void AddReservedNoAllocation(
            List<InstructionSupportStatus> rows,
            string mnemonic,
            string extensionName)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.Reserved,
                RuntimeInstructionEvidence.None,
                extensionName: extensionName));
        }

        private static IReadOnlyDictionary<string, InstructionSupportStatus> BuildMnemonicIndex(
            IEnumerable<InstructionSupportStatus> statuses)
        {
            var index = new Dictionary<string, InstructionSupportStatus>(
                StringComparer.OrdinalIgnoreCase);
            foreach (InstructionSupportStatus status in statuses)
            {
                index[NormalizeMnemonic(status.Mnemonic)] = status;
            }

            return index;
        }

        private static bool TryFindOpcodeInfoByMnemonic(
            string mnemonic,
            out OpcodeInfo opcodeInfo)
        {
            foreach (OpcodeInfo info in OpcodeRegistry.Opcodes)
            {
                if (string.Equals(
                        NormalizeMnemonic(info.Mnemonic),
                        NormalizeMnemonic(mnemonic),
                        StringComparison.OrdinalIgnoreCase))
                {
                    opcodeInfo = info;
                    return true;
                }
            }

            opcodeInfo = default;
            return false;
        }

        private static string NormalizeMnemonic(string mnemonic) =>
            mnemonic.Trim();
    }
}
