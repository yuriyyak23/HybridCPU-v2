using Xunit;
using YAKSys_Hybrid_CPU.Arch;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Unit tests for <see cref="IsaV4Surface"/> — canonical ISA v4 surface declaration.
    /// Validates internal consistency of the surface definition created in Phase 01.
    /// </summary>
    public class IsaV4SurfaceTests
    {
        // ─── Set Disjointness ──────────────────────────────────────────────────────

        /// <summary>
        /// ProhibitedOpcodes and MandatoryCoreOpcodes must be disjoint.
        /// An opcode cannot be both prohibited and mandatory.
        /// </summary>
        [Fact]
        public void ProhibitedOpcodes_AndMandatoryCore_AreDisjoint()
        {
            var intersection = new System.Collections.Generic.HashSet<string>(
                IsaV4Surface.ProhibitedOpcodes);
            intersection.IntersectWith(IsaV4Surface.MandatoryCoreOpcodes);

            Assert.Empty(intersection);
        }

        /// <summary>
        /// OptionalExtensions and MandatoryCoreOpcodes must be disjoint.
        /// Optional extensions are not part of the mandatory core by definition.
        /// </summary>
        [Fact]
        public void OptionalExtensions_AndMandatoryCore_AreDisjoint()
        {
            var intersection = new System.Collections.Generic.HashSet<string>(
                IsaV4Surface.OptionalExtensions);
            intersection.IntersectWith(IsaV4Surface.MandatoryCoreOpcodes);

            Assert.Empty(intersection);
        }

        /// <summary>
        /// OptionalExtensions and ProhibitedOpcodes must be disjoint.
        /// An opcode cannot be both optional and prohibited.
        /// </summary>
        [Fact]
        public void OptionalExtensions_AndProhibited_AreDisjoint()
        {
            var intersection = new System.Collections.Generic.HashSet<string>(
                IsaV4Surface.OptionalExtensions);
            intersection.IntersectWith(IsaV4Surface.ProhibitedOpcodes);

            Assert.Empty(intersection);
        }

        // ─── PipelineClassMap Coverage ────────────────────────────────────────────

        /// <summary>
        /// Every opcode in MandatoryCoreOpcodes must have a pipeline class mapping.
        /// The pipeline class map must cover the complete mandatory core surface.
        /// </summary>
        [Fact]
        public void PipelineClassMap_CoversAllMandatoryCoreOpcodes()
        {
            var unmapped = new System.Collections.Generic.List<string>();
            foreach (string opcode in IsaV4Surface.MandatoryCoreOpcodes)
            {
                if (!IsaV4Surface.PipelineClassMap.ContainsKey(opcode))
                    unmapped.Add(opcode);
            }

            Assert.Empty(unmapped);
        }

        /// <summary>
        /// All pipeline class values in PipelineClassMap must be one of the seven
        /// canonical pipeline classes defined in the ISA v4 master plan.
        /// </summary>
        [Fact]
        public void PipelineClassMap_ContainsOnlyCanonicalPipelineClasses()
        {
            var canonicalClasses = new System.Collections.Generic.HashSet<string>
            {
                "ALU", "LSU", "BR", "ATOM",
                "SYS_SERIAL", "CSR_SERIAL", "VMX_SERIAL", "DMA_STREAM",
            };

            foreach (var (opcode, pipelineClass) in IsaV4Surface.PipelineClassMap)
            {
                Assert.True(
                    canonicalClasses.Contains(pipelineClass),
                    $"Opcode '{opcode}' has unknown pipeline class '{pipelineClass}'.");
            }
        }

        // ─── Opcode Count Validation ───────────────────────────────────────────────

        /// <summary>
        /// The mandatory core must contain exactly <see cref="IsaV4Surface.IsaMandatoryOpcodeCount"/>
        /// canonical v4 opcodes, including the lane6 descriptor-backed DmaStreamCompute
        /// opcode and excluding optional extensions.
        /// </summary>
        [Fact]
        public void MandatoryCoreOpcodes_ContainsExactly97Opcodes()
        {
            Assert.Equal(IsaV4Surface.IsaMandatoryOpcodeCount, IsaV4Surface.MandatoryCoreOpcodes.Count);
            Assert.Equal(97, IsaV4Surface.IsaMandatoryOpcodeCount);
        }

        /// <summary>
        /// ProhibitedOpcodes set must not be empty — the prohibited list is a
        /// required architectural constraint, not an optional annotation.
        /// </summary>
        [Fact]
        public void ProhibitedOpcodes_IsNotEmpty()
        {
            Assert.NotEmpty(IsaV4Surface.ProhibitedOpcodes);
        }

        // ─── Specific Opcode Membership ───────────────────────────────────────────

        /// <summary>
        /// Hint opcodes must be in ProhibitedOpcodes, never in the mandatory core.
        /// Scheduling policy belongs in SlotMetadata, not the opcode space.
        /// </summary>
        [Theory]
        [InlineData("HINT_LIKELY")]
        [InlineData("HINT_UNLIKELY")]
        [InlineData("HINT_HOT")]
        [InlineData("HINT_COLD")]
        [InlineData("HINT_STREAM")]
        [InlineData("HINT_REUSE")]
        [InlineData("HINT_STEALABLE")]
        [InlineData("HINT_NOSTEAL")]
        public void HintOpcodes_AreInProhibited_NotInMandatoryCore(string opcode)
        {
            Assert.Contains(opcode, IsaV4Surface.ProhibitedOpcodes);
            Assert.DoesNotContain(opcode, IsaV4Surface.MandatoryCoreOpcodes);
        }

        /// <summary>
        /// VT-identity and FSP policy opcodes must be prohibited.
        /// RDVTID/RDVTMASK: VT identity is accessed via the CSR plane.
        /// FSP_FENCE: FSP policy boundaries belong in SlotMetadata.
        /// </summary>
        [Theory]
        [InlineData("RDVTID")]
        [InlineData("RDVTMASK")]
        [InlineData("FSP_FENCE")]
        public void VtIdentityAndFspPolicyOpcodes_AreProhibited(string opcode)
        {
            Assert.Contains(opcode, IsaV4Surface.ProhibitedOpcodes);
            Assert.DoesNotContain(opcode, IsaV4Surface.MandatoryCoreOpcodes);
        }

        /// <summary>
        /// Pseudo-op assembler mnemonics must be in ProhibitedOpcodes.
        /// NOP/LI/MV/CALL/RET/JMP are assembler expansions, not hardware instructions.
        /// </summary>
        [Theory]
        [InlineData("NOP")]
        [InlineData("LI")]
        [InlineData("MV")]
        [InlineData("CALL")]
        [InlineData("RET")]
        [InlineData("JMP")]
        public void PseudoOps_AreProhibited_NotInMandatoryCore(string opcode)
        {
            Assert.Contains(opcode, IsaV4Surface.ProhibitedOpcodes);
            Assert.DoesNotContain(opcode, IsaV4Surface.MandatoryCoreOpcodes);
        }

        /// <summary>
        /// CSR_READ and CSR_WRITE are compiler/emulator wrappers, not hardware instructions.
        /// They must be prohibited from the hardware ISA opcode space.
        /// </summary>
        [Theory]
        [InlineData("CSR_READ")]
        [InlineData("CSR_WRITE")]
        public void CsrWrappers_AreProhibited_NotInMandatoryCore(string opcode)
        {
            Assert.Contains(opcode, IsaV4Surface.ProhibitedOpcodes);
            Assert.DoesNotContain(opcode, IsaV4Surface.MandatoryCoreOpcodes);
        }

        /// <summary>
        /// Optional extensions must not appear in the mandatory core or prohibited sets.
        /// They form their own independent extension registration category.
        /// </summary>
        [Theory]
        [InlineData("NOT")]
        [InlineData("XSQRT")]
        [InlineData("XFMAC")]
        public void OptionalExtensionOpcodes_AreInOptionalSet_NotInMandatoryOrProhibited(string opcode)
        {
            Assert.Contains(opcode, IsaV4Surface.OptionalExtensions);
            Assert.DoesNotContain(opcode, IsaV4Surface.MandatoryCoreOpcodes);
            Assert.DoesNotContain(opcode, IsaV4Surface.ProhibitedOpcodes);
        }

        /// <summary>
        /// VMX instruction plane must be fully present in the mandatory core.
        /// These are first-class ISA instructions, not CSR wrappers.
        /// </summary>
        [Theory]
        [InlineData("VMXON")]
        [InlineData("VMXOFF")]
        [InlineData("VMLAUNCH")]
        [InlineData("VMRESUME")]
        [InlineData("VMREAD")]
        [InlineData("VMWRITE")]
        [InlineData("VMCLEAR")]
        [InlineData("VMPTRLD")]
        public void VmxOpcodes_AreInMandatoryCore(string opcode)
        {
            Assert.Contains(opcode, IsaV4Surface.MandatoryCoreOpcodes);
            Assert.DoesNotContain(opcode, IsaV4Surface.ProhibitedOpcodes);
        }

        /// <summary>
        /// Full 64-bit atomic plane (AMO doubleword) must be present in the mandatory core.
        /// P6 (AMO_D incompleteness) is addressed by including the complete AMO_D family.
        /// </summary>
        [Theory]
        [InlineData("AMOSWAP_D")]
        [InlineData("AMOADD_D")]
        [InlineData("AMOXOR_D")]
        [InlineData("AMOAND_D")]
        [InlineData("AMOOR_D")]
        [InlineData("AMOMIN_D")]
        [InlineData("AMOMAX_D")]
        [InlineData("AMOMINU_D")]
        [InlineData("AMOMAXU_D")]
        public void AmoDoublewordOpcodes_AreInMandatoryCore(string opcode)
        {
            Assert.Contains(opcode, IsaV4Surface.MandatoryCoreOpcodes);
        }

        /// <summary>
        /// SMT/VT synchronization opcodes must be in the mandatory core.
        /// YIELD, WFE, SEV, POD_BARRIER, VT_BARRIER are first-class ISA instructions.
        /// </summary>
        [Theory]
        [InlineData("YIELD")]
        [InlineData("WFE")]
        [InlineData("SEV")]
        [InlineData("POD_BARRIER")]
        [InlineData("VT_BARRIER")]
        public void SmtVtOpcodes_AreInMandatoryCore(string opcode)
        {
            Assert.Contains(opcode, IsaV4Surface.MandatoryCoreOpcodes);
        }

        // ─── Mandatory Core Classes ────────────────────────────────────────────────

        /// <summary>
        /// MandatoryCoreClasses must contain all 8 canonical ISA v4 instruction classes.
        /// </summary>
        [Fact]
        public void MandatoryCoreClasses_ContainsAllEightCanonicalClasses()
        {
            Assert.Equal(8, IsaV4Surface.MandatoryCoreClasses.Count);
            Assert.Contains("ScalarAlu", IsaV4Surface.MandatoryCoreClasses);
            Assert.Contains("Memory", IsaV4Surface.MandatoryCoreClasses);
            Assert.Contains("ControlFlow", IsaV4Surface.MandatoryCoreClasses);
            Assert.Contains("Atomic", IsaV4Surface.MandatoryCoreClasses);
            Assert.Contains("System", IsaV4Surface.MandatoryCoreClasses);
            Assert.Contains("Csr", IsaV4Surface.MandatoryCoreClasses);
            Assert.Contains("SmtVt", IsaV4Surface.MandatoryCoreClasses);
            Assert.Contains("Vmx", IsaV4Surface.MandatoryCoreClasses);
        }
    }
}
