using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch.Generated;

namespace YAKSys_Hybrid_CPU.Arch
{
    /// <summary>
    /// Compatibility façade for the HybridCPU ISA v4 static policy contour.
    /// The committed generated C# catalog is projected from typed registry rows;
    /// this type preserves its established consumer-facing API until the RF-13
    /// legacy surface removal gate closes. Runtime support remains evidence owned by
    /// <see cref="InstructionSupportStatusCatalog"/>, never by this declaration.
    /// </summary>
    public static class IsaV4Surface
    {
        public static readonly IReadOnlySet<string> MandatoryCoreClasses = Policy(nameof(MandatoryCoreClasses));
        public static readonly IReadOnlySet<string> MandatoryCoreOpcodes = Policy(nameof(MandatoryCoreOpcodes));
        public static readonly IReadOnlySet<string> SystemDeviceCommandOpcodes = Policy(nameof(SystemDeviceCommandOpcodes));
        public static readonly IReadOnlySet<string> CarrierOnlyOpcodes = Policy(nameof(CarrierOnlyOpcodes));
        public static readonly IReadOnlySet<string> MandatoryInteger64RepairOpcodes = Policy(nameof(MandatoryInteger64RepairOpcodes));
        public static readonly IReadOnlySet<string> DescriptorOnlyOpcodes = Policy(nameof(DescriptorOnlyOpcodes));
        public static readonly IReadOnlySet<string> ParserOnlyOpcodes = Policy(nameof(ParserOnlyOpcodes));
        public static readonly IReadOnlySet<string> OptionalEnabledOpcodes = Policy(nameof(OptionalEnabledOpcodes));
        public static readonly IReadOnlySet<string> OptionalDisabledOpcodes = Policy(nameof(OptionalDisabledOpcodes));
        public static readonly IReadOnlySet<string> ReservedOpcodes = Policy(nameof(ReservedOpcodes));
        public static readonly IReadOnlySet<string> ProhibitedOpcodes = Policy(nameof(ProhibitedOpcodes));
        public static readonly IReadOnlySet<string> OptionalExtensions = Policy(nameof(OptionalExtensions));
        public static readonly IReadOnlyDictionary<string, string> PipelineClassMap = GeneratedIsaCatalog.PipelineClassMap;

        /// <summary>HybridCPU ISA v4 is frozen; evolution requires a new catalog version.</summary>
        public const int IsaVersion = 4;

        /// <summary>Frozen v4 mandatory hardware-opcode count.</summary>
        public const int IsaMandatoryOpcodeCount = 111;

        /// <summary>Date on which the ISA v4 surface was formally frozen.</summary>
        public static readonly DateOnly FrozenDate = new(2026, 3, 14);

        private static IReadOnlySet<string> Policy(string policyId) => GeneratedIsaCatalog.GetStaticPolicy(policyId);
    }
}
