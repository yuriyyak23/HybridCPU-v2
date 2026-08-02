using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace YAKSys_Hybrid_CPU.Core.Legality
{
    /// <summary>
    /// Default <see cref="ILegalityChecker"/> implementation for HybridCPU ISA v4.
    ///
    /// Evaluation order (first failing check wins):
    /// <list type="number">
    ///   <item>Privilege check: M-mode-only instruction in a lower privilege mode -&gt; <see cref="LegalityResult.PrivilegeFault"/>.</item>
    ///   <item>Data-hazard check: Rs1 or Rs2 has a RAW hazard -&gt; <see cref="LegalityResult.Stall"/>.</item>
    ///   <item>Structural-resource check: no execution unit available for the instruction class -&gt; <see cref="LegalityResult.Stall"/>.</item>
    ///   <item>Otherwise: <see cref="LegalityResult.Legal"/>.</item>
    /// </list>
    ///
    /// ARCHITECTURE RULE: This class is a pipeline-stage service.
    /// It must NOT be called from the decoder.
    /// </summary>
    public sealed class LegalityChecker : ILegalityChecker
    {
        /// <inheritdoc />
        public LegalityResult Check(
            InstructionIR instruction,
            IHazardState hazards,
            IResourceState resources,
            PrivilegeLevel currentPrivilege)
        {
            ushort opcode = instruction.CanonicalOpcode.Value;

            if (RequiresMachineMode(opcode) &&
                currentPrivilege < PrivilegeLevel.Machine)
            {
                return LegalityResult.PrivilegeFault;
            }

            if (instruction.Rs1 > 0 && hazards.HasRawHazard(instruction.Rs1))
            {
                return LegalityResult.Stall;
            }

            if (instruction.Rs2 > 0 && hazards.HasRawHazard(instruction.Rs2))
            {
                return LegalityResult.Stall;
            }

            if (!resources.IsAvailable(instruction.Class))
            {
                return LegalityResult.Stall;
            }

            return LegalityResult.Legal;
        }

        private static bool RequiresMachineMode(ushort opCode)
        {
            OpcodeInfo? info = OpcodeRegistry.GetInfo(opCode);
            return info is not null &&
                   (info.Value.Flags & InstructionFlags.Privileged) != 0;
        }
    }
}
