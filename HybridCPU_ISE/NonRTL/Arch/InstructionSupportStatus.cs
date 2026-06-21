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
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "VectorIndexedGatherMemory",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
            rows.Add(new InstructionSupportStatus(
                "VSCATTER",
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "VectorIndexedScatterMemory",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));

            rows.Add(new InstructionSupportStatus(
                "CZERO.EQZ",
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "ScalarSelectCzero",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
            AddReservedNoAllocation(rows, "SEQZ", "ScalarSelectCzero");
            AddReservedNoAllocation(rows, "SNEZ", "ScalarSelectCzero");
            AddReservedNoAllocation(rows, "CSEL", "ScalarSelectCzero");
            AddOptionalEnabledScalarSelectCzero(rows, "CZERO.NEZ");

            rows.Add(new InstructionSupportStatus(
                "CLZ",
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "ScalarBitmanipCore",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
            rows.Add(new InstructionSupportStatus(
                "CTZ",
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "ScalarBitmanipCore",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
            AddOptionalEnabledScalarBitmanipCore(rows, "CPOP");
            AddReservedNoAllocation(rows, "POPCNT", "ScalarBitmanipCore");
            AddOptionalEnabledScalarBitmanipCore(rows, "ROL");
            AddOptionalEnabledScalarBitmanipCore(rows, "ROR");
            AddOptionalEnabledScalarBitmanipCore(rows, "ROLI");
            AddOptionalEnabledScalarBitmanipCore(rows, "RORI");
            AddOptionalEnabledScalarBitmanipCore(rows, "ANDN");
            AddOptionalEnabledScalarBitmanipCore(rows, "ORN");
            AddOptionalEnabledScalarBitmanipCore(rows, "XNOR");
            AddOptionalEnabledScalarBitmanipCore(rows, "MIN");
            AddOptionalEnabledScalarBitmanipCore(rows, "MAX");
            AddOptionalEnabledScalarBitmanipCore(rows, "MINU");
            AddOptionalEnabledScalarBitmanipCore(rows, "MAXU");
            AddOptionalEnabledScalarBitmanipCore(rows, "REV8");
            AddOptionalEnabledScalarBitmanipCore(rows, "BREV8");
            AddOptionalEnabledScalarBitmanipCore(rows, "SEXT.B");
            AddOptionalEnabledScalarBitmanipCore(rows, "SEXT.H");
            AddOptionalEnabledScalarBitmanipCore(rows, "ZEXT.H");
            AddOptionalEnabledScalarBitfield(rows, "BSET");
            AddOptionalEnabledScalarBitfield(rows, "BCLR");
            AddOptionalEnabledScalarBitfield(rows, "BINV");
            AddOptionalEnabledScalarBitfield(rows, "BEXT");
            AddOptionalEnabledScalarBitfield(rows, "BSETI");
            AddOptionalEnabledScalarBitfield(rows, "BCLRI");
            AddOptionalEnabledScalarBitfield(rows, "BINVI");
            AddOptionalEnabledScalarBitfield(rows, "BEXTI");

            AddOptionalEnabledScalarAddressGeneration(rows, "SH1ADD");
            AddOptionalEnabledScalarAddressGeneration(rows, "SH2ADD");
            AddOptionalEnabledScalarAddressGeneration(rows, "SH3ADD");
            AddOptionalEnabledScalarAddressGeneration(rows, "ADD.UW");
            AddOptionalEnabledScalarAddressGeneration(rows, "SH1ADD.UW");
            AddOptionalEnabledScalarAddressGeneration(rows, "SH2ADD.UW");
            AddOptionalEnabledScalarAddressGeneration(rows, "SH3ADD.UW");
            AddOptionalEnabledScalarAddressGeneration(rows, "SLLI.UW");

            rows.Add(new InstructionSupportStatus(
                "RDCYCLE",
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "ScalarSystemCounter",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
            AddReservedNoAllocation(rows, "RDTIME", "ScalarSystemCounter");
            AddReservedNoAllocation(rows, "RDINSTRET", "ScalarSystemCounter");
            AddReservedNoAllocation(rows, "PAUSE", "ScalarSystemCounter");

            AddOptionalEnabledScalarCarryLessChecksum(rows, "CLMUL");
            AddOptionalEnabledScalarCarryLessChecksum(rows, "CLMULH");
            AddOptionalEnabledScalarCarryLessChecksum(rows, "CLMULR");
            AddReservedNoAllocation(rows, "CRC32", "ScalarCrcChecksum");
            AddReservedNoAllocation(rows, "CRC64", "ScalarCrcChecksum");
            AddReservedNoAllocation(rows, "ADC", "ScalarMultiPrecision");
            AddReservedNoAllocation(rows, "SBC", "ScalarMultiPrecision");
            AddReservedNoAllocation(rows, "ADDC", "ScalarMultiPrecision");
            AddReservedNoAllocation(rows, "SUBC", "ScalarMultiPrecision");

            rows.Add(new InstructionSupportStatus(
                "DmaStreamCompute",
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "Lane6DSC",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
            AddDescriptorOnlyNoAllocation(rows, "DmaStreamCompute.SUB", "Lane6DescriptorOp");
            AddDescriptorOnlyNoAllocation(rows, "DmaStreamCompute.MIN", "Lane6DescriptorOp");
            AddDescriptorOnlyNoAllocation(rows, "DmaStreamCompute.MAX", "Lane6DescriptorOp");
            AddDescriptorOnlyNoAllocation(rows, "DmaStreamCompute.ABSDIFF", "Lane6DescriptorOp");
            AddDescriptorOnlyNoAllocation(rows, "DmaStreamCompute.CLAMP", "Lane6DescriptorOp");
            AddDescriptorOnlyNoAllocation(rows, "DmaStreamCompute.CONVERT", "Lane6DescriptorOp");
            AddDescriptorOnlyNoAllocation(rows, "DmaStreamCompute.COMPARE", "Lane6DescriptorOp");
            AddDescriptorOnlyNoAllocation(rows, "DmaStreamCompute.SELECT", "Lane6DescriptorOp");
            AddDescriptorOnlyNoAllocation(rows, "DmaStreamCompute.REDUCE_SUM", "Lane6DescriptorOp");
            AddDescriptorOnlyNoAllocation(rows, "DmaStreamCompute.REDUCE_MIN", "Lane6DescriptorOp");
            AddDescriptorOnlyNoAllocation(rows, "DmaStreamCompute.REDUCE_MAX", "Lane6DescriptorOp");
            AddDescriptorOnlyNoAllocation(rows, "DmaStreamCompute.REDUCE_AND", "Lane6DescriptorOp");
            AddDescriptorOnlyNoAllocation(rows, "DmaStreamCompute.REDUCE_OR", "Lane6DescriptorOp");
            AddDescriptorOnlyNoAllocation(rows, "DmaStreamCompute.REDUCE_XOR", "Lane6DescriptorOp");
            AddDescriptorOnlyNoAllocation(rows, "DSC_SHAPE_STRIDED", "Lane6DescriptorShape");
            AddDescriptorOnlyNoAllocation(rows, "DSC_SHAPE_TILED", "Lane6DescriptorShape");
            AddDescriptorOnlyNoAllocation(rows, "DSC_SHAPE_SCATTER_GATHER", "Lane6DescriptorShape");
            AddDescriptorOnlyNoAllocation(rows, "DSC_SHAPE_2D", "Lane6DescriptorShape");
            AddDescriptorOnlyNoAllocation(rows, "DSC_SHAPE_MULTI_RANGE", "Lane6DescriptorShape");
            rows.Add(new InstructionSupportStatus(
                "DSC_STATUS",
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "Lane6QueueControl",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
            rows.Add(new InstructionSupportStatus(
                "DSC_QUERY_CAPS",
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "Lane6DscQuery",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
            AddReservedNoAllocation(rows, "DSC_POLL", "Lane6QueueControl");
            AddReservedNoAllocation(rows, "DSC_WAIT", "Lane6QueueControl");
            AddReservedNoAllocation(rows, "DSC_CANCEL", "Lane6QueueControl");
            AddReservedNoAllocation(rows, "DSC_FENCE", "Lane6QueueControl");
            AddReservedNoAllocation(rows, "DSC_COMMIT", "Lane6QueueControl");
            AddReservedNoAllocation(rows, "DSC_QUERY_BACKEND", "Lane6DscQuery");
            AddReservedNoAllocation(rows, "DSC_QUERY_SHAPE", "Lane6DscQuery");
            rows.Add(new InstructionSupportStatus(
                "DSC2",
                IsaInstructionStatus.ParserOnly,
                RuntimeInstructionEvidence.DeclaredOnly,
                extensionName: "Lane6DSC"));

            AddOptionalEnabledL7SdcProduction(rows, "ACCEL_QUERY_CAPS");
            AddOptionalEnabledL7SdcProduction(rows, "ACCEL_SUBMIT");
            AddOptionalEnabledL7SdcProduction(rows, "ACCEL_POLL");
            AddOptionalEnabledL7SdcProduction(rows, "ACCEL_WAIT");
            AddOptionalEnabledL7SdcProduction(rows, "ACCEL_CANCEL");
            AddOptionalEnabledL7SdcProduction(rows, "ACCEL_FENCE");
            AddOptionalEnabledL7SdcProduction(rows, "ACCEL_STATUS");
            AddReservedNoAllocation(rows, "ACCEL_QUERY_ABI", "Lane7TopologyQueue");
            AddReservedNoAllocation(rows, "ACCEL_QUERY_TOPOLOGY", "Lane7TopologyQueue");
            AddReservedNoAllocation(rows, "ACCEL_OPEN", "Lane7TopologyQueue");
            AddReservedNoAllocation(rows, "ACCEL_CLOSE", "Lane7TopologyQueue");
            AddReservedNoAllocation(rows, "ACCEL_BIND_QUEUE", "Lane7TopologyQueue");
            AddReservedNoAllocation(rows, "ACCEL_UNBIND_QUEUE", "Lane7TopologyQueue");

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
            AddOptionalEnabledVectorMaskPrefixPublication(rows, "VMSBF");
            AddOptionalEnabledVectorZeroExtendPublication(rows, "VZEXT");
            AddOptionalEnabledVectorScanPrefixPublication(rows, "VSCAN.SUM");
            AddOptionalEnabledVectorSaturatingAddPolicy(rows, "VADD.SAT");
            AddReservedNoAllocation(rows, "VSUB.SAT", "VectorSaturatingFixedPoint");
            AddReservedNoAllocation(rows, "VMUL.SAT", "VectorSaturatingFixedPoint");
            AddReservedNoAllocation(rows, "VSLL.SAT", "VectorSaturatingFixedPoint");
            AddReservedNoAllocation(rows, "VSRL.SAT", "VectorSaturatingFixedPoint");
            AddReservedNoAllocation(rows, "VSRA.SAT", "VectorSaturatingFixedPoint");
            AddReservedNoAllocation(rows, "VAVG", "VectorSaturatingFixedPoint");
            AddReservedNoAllocation(rows, "VAVG.R", "VectorSaturatingFixedPoint");
            AddReservedNoAllocation(rows, "VCLIP", "VectorSaturatingFixedPoint");
            AddReservedNoAllocation(rows, "VSCAN.MIN", "VectorScanSegmentMovement");
            AddReservedNoAllocation(rows, "VSCAN.MAX", "VectorScanSegmentMovement");
            AddReservedNoAllocation(rows, "VLDSEG2", "VectorScanSegmentMovement");
            AddReservedNoAllocation(rows, "VLDSEG4", "VectorScanSegmentMovement");
            AddReservedNoAllocation(rows, "VLDSEG8", "VectorScanSegmentMovement");
            AddReservedNoAllocation(rows, "VSTSEG2", "VectorScanSegmentMovement");
            AddReservedNoAllocation(rows, "VSTSEG4", "VectorScanSegmentMovement");
            AddReservedNoAllocation(rows, "VSTSEG8", "VectorScanSegmentMovement");
            AddReservedNoAllocation(rows, "VZIP", "VectorScanSegmentMovement");
            AddReservedNoAllocation(rows, "VUNZIP", "VectorScanSegmentMovement");
            AddReservedNoAllocation(rows, "VINTERLEAVE", "VectorScanSegmentMovement");
            AddReservedNoAllocation(rows, "VDEINTERLEAVE", "VectorScanSegmentMovement");
            AddReservedNoAllocation(rows, "VMERGE", "VectorMaskSelect");
            AddReservedNoAllocation(rows, "VSELECT", "VectorMaskSelect");
            AddReservedNoAllocation(rows, "VFIRST", "VectorMaskSelect");
            AddReservedNoAllocation(rows, "VANY", "VectorMaskSelect");
            AddReservedNoAllocation(rows, "VALL", "VectorMaskSelect");
            AddReservedNoAllocation(rows, "VMSIF", "VectorMaskSelect");
            AddReservedNoAllocation(rows, "VMSOF", "VectorMaskSelect");
            AddReservedNoAllocation(rows, "VWADD", "VectorWidenNarrowConvert");
            AddReservedNoAllocation(rows, "VWADDU", "VectorWidenNarrowConvert");
            AddReservedNoAllocation(rows, "VWSUB", "VectorWidenNarrowConvert");
            AddReservedNoAllocation(rows, "VWSUBU", "VectorWidenNarrowConvert");
            AddReservedNoAllocation(rows, "VWMUL", "VectorWidenNarrowConvert");
            AddReservedNoAllocation(rows, "VWMULU", "VectorWidenNarrowConvert");
            AddReservedNoAllocation(rows, "VWMACC", "VectorWidenNarrowConvert");
            AddReservedNoAllocation(rows, "VNSRL", "VectorWidenNarrowConvert");
            AddReservedNoAllocation(rows, "VNSRA", "VectorWidenNarrowConvert");
            AddReservedNoAllocation(rows, "VSEXT", "VectorWidenNarrowConvert");
            AddReservedNoAllocation(rows, "VCVT.I", "VectorWidenNarrowConvert");
            AddReservedNoAllocation(rows, "VCVT.U", "VectorWidenNarrowConvert");
            AddReservedNoAllocation(rows, "VCVT.F", "VectorWidenNarrowConvert");
            AddReservedNoAllocation(rows, "VDOT.BLOCKSCALE", "VectorDotMatrixDeferred");
            AddReservedNoAllocation(rows, "VDOT.ACCUM", "VectorDotMatrixDeferred");
            AddReservedNoAllocation(rows, "VDOT.WIDE.I16", "VectorDotMatrixDeferred");
            AddReservedNoAllocation(rows, "VDOT.WIDE.I32", "VectorDotMatrixDeferred");

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
            AddOptionalEnabledVectorPerm2(rows, "VPERM2");
            AddOptionalEnabledVectorTranspose(rows, "VTRANSPOSE");
            AddOptionalEnabledVectorSlide(rows, "VSLIDEUP");
            AddOptionalEnabledVectorSlide(rows, "VSLIDEDOWN");
            AddOptionalEnabledVectorSlideOne(rows, "VSLIDE1UP");
            AddOptionalEnabledVectorSlideOne(rows, "VSLIDE1DOWN");
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
            AddOptionalEnabledVectorDotProductWideScalarFootprint(rows, "VDOT.WIDE");

            AddOptionalEnabledMatrixProduction(rows, "MTILE_LOAD");
            AddOptionalEnabledMatrixProduction(rows, "MTILE_STORE");
            AddOptionalEnabledMatrixProduction(rows, "MTILE_MACC");
            AddOptionalEnabledMatrixProduction(rows, "MTRANSPOSE");

            AddReservedNoAllocation(rows, "SFENCE.VMA", "CacheTlbCoherency");
            AddReservedNoAllocation(rows, "DCACHE_CLEAN", "CacheTlbCoherency");
            AddReservedNoAllocation(rows, "DCACHE_INVAL", "CacheTlbCoherency");
            AddReservedNoAllocation(rows, "DCACHE_FLUSH", "CacheTlbCoherency");
            AddReservedNoAllocation(rows, "ICACHE_INVAL", "CacheTlbCoherency");
            AddReservedNoAllocation(rows, "IOTLB_INV", "CacheTlbCoherency");
            AddReservedNoAllocation(rows, "IOMMU_FENCE", "CacheTlbCoherency");

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

        private static void AddOptionalEnabledVectorSaturatingAddPolicy(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "VectorSaturatingAddPolicy",
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

        private static void AddOptionalEnabledVectorDotProductWideScalarFootprint(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "VectorDotProductWideScalarFootprint",
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

        private static void AddOptionalEnabledVectorMaskPrefixPublication(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "VectorMaskPrefixPublication",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
        }

        private static void AddOptionalEnabledVectorZeroExtendPublication(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "VectorZeroExtendPublication",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
        }

        private static void AddOptionalEnabledVectorScanPrefixPublication(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "VectorScanPrefixPublication",
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

        private static void AddOptionalEnabledVectorPerm2(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "VectorPermute2Publication",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
        }

        private static void AddOptionalEnabledVectorTranspose(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "VectorTransposePublication",
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

        private static void AddOptionalEnabledVectorSlideOne(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "VectorSlideOnePublication",
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

        private static void AddOptionalEnabledMatrixProduction(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "XMatrix",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
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

        private static void AddOptionalEnabledL7SdcProduction(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "Lane7L7SDC",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
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

        private static void AddDescriptorOnlyNoAllocation(
            List<InstructionSupportStatus> rows,
            string mnemonic,
            string extensionName)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.DescriptorOnly,
                RuntimeInstructionEvidence.DeclaredOnly,
                extensionName: extensionName));
        }

        private static void AddOptionalEnabledScalarBitmanipCore(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "ScalarBitmanipCore",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
        }

        private static void AddOptionalEnabledScalarBitfield(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "ScalarBitfield",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
        }

        private static void AddOptionalEnabledScalarAddressGeneration(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "ScalarAddressGeneration",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
        }

        private static void AddOptionalEnabledScalarCarryLessChecksum(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "ScalarCarryLessChecksum",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
        }

        private static void AddOptionalEnabledScalarSelectCzero(
            List<InstructionSupportStatus> rows,
            string mnemonic)
        {
            rows.Add(new InstructionSupportStatus(
                mnemonic,
                IsaInstructionStatus.OptionalEnabled,
                RuntimeInstructionEvidence.ConformanceTested,
                extensionName: "ScalarSelectCzero",
                hasNumericOpcode: true,
                hasRuntimeOpcodeMetadata: true,
                hasCanonicalDecoderAcceptance: true,
                hasRegistryFactory: true,
                hasExecutionSemantics: true));
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
