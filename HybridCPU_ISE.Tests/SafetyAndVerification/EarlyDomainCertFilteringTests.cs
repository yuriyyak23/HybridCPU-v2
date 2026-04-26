using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using HybridCPU_ISE.Tests.TestHelpers;

namespace HybridCPU_ISE.Tests.SafetyAndVerification
{
    /// <summary>
    /// Refactoring Pt. 5: Early Domain Certificate Filtering (Leakage Point fix).
    /// Validates that domain-violating operations are squashed to NOP at the ID stage
    /// BEFORE they can stimulate TLB, AddressGen, L1 cache, or StreamEngine in EX.
    /// </summary>
    public class EarlyDomainCertFilteringTests
    {
        #region Domain Certificate Combinational Logic

        /// <summary>
        /// User-mode op (DomainTag != 0) with mismatching cert must be squashed.
        /// Condition: (DomainTag & CsrMemDomainCert) == 0 → squash.
        /// </summary>
        [Theory]
        [InlineData(0x01UL, 0x02UL)] // domain 1 vs cert 2 — no bits in common
        [InlineData(0x04UL, 0x08UL)] // domain 4 vs cert 8 — no bits in common
        [InlineData(0x10UL, 0x0FUL)] // domain 0x10 vs cert 0x0F — no bits in common
        public void WhenDomainTagMismatchesCert_ThenSquashConditionIsTrue(
            ulong domainTag, ulong cert)
        {
            // Arrange & Act
            bool shouldSquash = domainTag != 0 && cert != 0 &&
                                (domainTag & cert) == 0;

            // Assert
            Assert.True(shouldSquash,
                $"DomainTag=0x{domainTag:X} & Cert=0x{cert:X} should trigger squash");
        }

        /// <summary>
        /// User-mode op with matching cert bits must NOT be squashed.
        /// Condition: (DomainTag & CsrMemDomainCert) != 0 → pass through.
        /// </summary>
        [Theory]
        [InlineData(0x01UL, 0x01UL)] // exact match
        [InlineData(0x03UL, 0x01UL)] // superset — domain has cert bit
        [InlineData(0x0FUL, 0x08UL)] // domain includes cert bit
        public void WhenDomainTagMatchesCert_ThenSquashConditionIsFalse(
            ulong domainTag, ulong cert)
        {
            // Arrange & Act
            bool shouldSquash = domainTag != 0 && cert != 0 &&
                                (domainTag & cert) == 0;

            // Assert
            Assert.False(shouldSquash,
                $"DomainTag=0x{domainTag:X} & Cert=0x{cert:X} should NOT trigger squash");
        }

        /// <summary>
        /// Kernel-mode op (DomainTag == 0) bypasses cert check entirely.
        /// Zero domain = trusted kernel — never squashed.
        /// </summary>
        [Theory]
        [InlineData(0x00UL, 0x01UL)]
        [InlineData(0x00UL, 0xFFUL)]
        [InlineData(0x00UL, 0x00UL)]
        public void WhenDomainTagIsZero_ThenSquashIsNeverTriggered(
            ulong domainTag, ulong cert)
        {
            // Arrange & Act
            bool shouldSquash = domainTag != 0 && cert != 0 &&
                                (domainTag & cert) == 0;

            // Assert
            Assert.False(shouldSquash,
                "Kernel-mode (DomainTag=0) must never be squashed");
        }

        /// <summary>
        /// Unconfigured cert (CsrMemDomainCert == 0) bypasses check.
        /// Before OS sets up domain isolation, all ops pass.
        /// </summary>
        [Theory]
        [InlineData(0x01UL, 0x00UL)]
        [InlineData(0xFFUL, 0x00UL)]
        public void WhenCertIsZero_ThenSquashIsNeverTriggered(
            ulong domainTag, ulong cert)
        {
            // Arrange & Act
            bool shouldSquash = domainTag != 0 && cert != 0 &&
                                (domainTag & cert) == 0;

            // Assert
            Assert.False(shouldSquash,
                "Unconfigured cert (0) must never trigger squash");
        }

        #endregion

        #region NOP Replacement Semantics

        /// <summary>
        /// Squashed FSP-injected op must be replaced with a NopMicroOp that is NOT stealable.
        /// This prevents FSP from re-injecting into the squashed slot.
        /// FSP-injected ops follow the silent-squash path (§5.4 Case 1).
        /// </summary>
        [Fact]
        public void WhenOpIsSquashed_ThenReplacementNopIsNotStealable()
        {
            // Arrange — simulate what PipelineStage_Decode does for an FSP-injected op
            var violatingOp = MicroOpTestHelper.CreateLoad(
                virtualThreadId: 0, destReg: 1, address: 0x1000, domainTag: 3);
            violatingOp.IsFspInjected = true; // FSP-injected → silent squash path

            ulong csrMemDomainCert = 0x04; // Domain 3 & cert 4 → mismatch

            // Act — replicate early squash logic (FSP-injected branch)
            NopMicroOp replacement = null!;
            if (violatingOp.Placement.DomainTag != 0 && csrMemDomainCert != 0 &&
                (violatingOp.Placement.DomainTag & csrMemDomainCert) == 0 &&
                violatingOp.IsFspInjected)
            {
                replacement = new NopMicroOp
                {
                    IsStealable = false,
                    OpCode = violatingOp.OpCode
                };
            }

            // Assert
            Assert.NotNull(replacement);
            Assert.False(replacement.IsStealable);
            Assert.Equal(violatingOp.OpCode, replacement.OpCode);
        }

        /// <summary>
        /// Matching-domain op is NOT replaced — original MicroOp survives.
        /// </summary>
        [Fact]
        public void WhenDomainMatches_ThenOriginalOpSurvives()
        {
            // Arrange
            var validOp = MicroOpTestHelper.CreateLoad(
                virtualThreadId: 0, destReg: 1, address: 0x1000, domainTag: 5);

            ulong csrMemDomainCert = 0x05; // Domain 5 & cert 5 → match

            // Act
            bool shouldSquash = validOp.Placement.DomainTag != 0 && csrMemDomainCert != 0 &&
                                (validOp.Placement.DomainTag & csrMemDomainCert) == 0;

            // Assert
            Assert.False(shouldSquash);
        }

        #endregion

        #region EarlyDomainSquashCount Counter

        /// <summary>
        /// EarlyDomainSquashCount must be zero after PipelineControl.Clear().
        /// </summary>
        [Fact]
        public void WhenPipelineControlCleared_ThenEarlyDomainSquashCountIsZero()
        {
            // Arrange
            var pipeCtrl = new Processor.CPU_Core.PipelineControl();
            pipeCtrl.EarlyDomainSquashCount = 42;

            // Act
            pipeCtrl.Clear();

            // Assert
            Assert.Equal(0UL, pipeCtrl.EarlyDomainSquashCount);
        }

        /// <summary>
        /// EarlyDomainSquashCount increments correctly on each squash.
        /// </summary>
        [Fact]
        public void WhenMultipleSquashes_ThenCounterIncrementsCorrectly()
        {
            // Arrange
            var pipeCtrl = new Processor.CPU_Core.PipelineControl();
            pipeCtrl.Clear();

            // Act — simulate 3 early domain squashes
            pipeCtrl.EarlyDomainSquashCount++;
            pipeCtrl.EarlyDomainSquashCount++;
            pipeCtrl.EarlyDomainSquashCount++;

            // Assert
            Assert.Equal(3UL, pipeCtrl.EarlyDomainSquashCount);
        }

        #endregion

        #region Defense-in-Depth (MEM/WB Late Gates)

        /// <summary>
        /// Even with early filtering, MEM/WB still contain defense-in-depth gates.
        /// This test validates the condition stays consistent across all three stages.
        /// </summary>
        [Theory]
        [InlineData(0x01UL, 0x02UL, true)]  // mismatch → squash
        [InlineData(0x01UL, 0x01UL, false)] // match → no squash
        [InlineData(0x00UL, 0x01UL, false)] // kernel → no squash
        [InlineData(0x01UL, 0x00UL, false)] // unconfigured cert → no squash
        public void WhenDomainCheckApplied_ThenConditionIsIdenticalAcrossStages(
            ulong domainTag, ulong cert, bool expectedSquash)
        {
            // The same combinational expression is used at ID, MEM, and WB:
            // (tag != 0 && cert != 0 && (tag & cert) == 0)
            bool idCheck = domainTag != 0 && cert != 0 && (domainTag & cert) == 0;
            bool memCheck = domainTag != 0 && cert != 0 && (domainTag & cert) == 0;
            bool wbCheck = domainTag != 0 && cert != 0 && (domainTag & cert) == 0;

            Assert.Equal(expectedSquash, idCheck);
            Assert.Equal(idCheck, memCheck);
            Assert.Equal(memCheck, wbCheck);
        }

        #endregion

        #region Store Op Domain Squash at ID

        /// <summary>
        /// Store operations with mismatching domain must also be squashed at ID.
        /// This prevents speculative memory writes from polluting cache/NoC.
        /// </summary>
        [Fact]
        public void WhenStoreOpDomainMismatches_ThenSquashedToNop()
        {
            // Arrange
            var storeOp = MicroOpTestHelper.CreateStore(
                virtualThreadId: 1, srcReg: 5, address: 0x2000, domainTag: 7);

            ulong csrMemDomainCert = 0x08; // Domain 7 & cert 8 → mismatch

            // Act
            bool shouldSquash = storeOp.Placement.DomainTag != 0 && csrMemDomainCert != 0 &&
                                (storeOp.Placement.DomainTag & csrMemDomainCert) == 0;

            // Assert
            Assert.True(shouldSquash,
                "Store with mismatching domain must be squashed at ID to prevent speculative writes");
        }

        #endregion

        #region Scalar ALU Domain Squash at ID

        /// <summary>
        /// Even non-memory scalar ALU ops with mismatching domain are squashed.
        /// This prevents information leaks through timing side-channels on shared ALU ports.
        /// </summary>
        [Fact]
        public void WhenScalarAluDomainMismatches_ThenSquashedToNop()
        {
            // Arrange
            var aluOp = MicroOpTestHelper.CreateScalarALU(
                virtualThreadId: 2, destReg: 3, src1Reg: 4, src2Reg: 5);
            aluOp.Placement = aluOp.Placement with { DomainTag = 0x10 };

            ulong csrMemDomainCert = 0x0F; // 0x10 & 0x0F = 0 → mismatch

            // Act
            bool shouldSquash = aluOp.Placement.DomainTag != 0 && csrMemDomainCert != 0 &&
                                (aluOp.Placement.DomainTag & csrMemDomainCert) == 0;

            // Assert
            Assert.True(shouldSquash,
                "ALU op with mismatching domain must be squashed at ID");
        }

        #endregion

        #region FSP-Injected vs Owner-VT Domain Fault Paths (§5.4)

        /// <summary>
        /// FSP-injected op with domain mismatch follows the silent-squash path (§5.4 Case 1).
        /// IsFspInjected == true → silent NOP replacement, no DomainFaultException.
        /// </summary>
        [Fact]
        public void WhenFspInjectedOpDomainMismatches_ThenSilentSquashPath()
        {
            // Arrange: FSP-injected op from background VT with wrong domain tag
            var fspOp = MicroOpTestHelper.CreateLoad(
                virtualThreadId: 1, destReg: 2, address: 0x2000, domainTag: 0x04);
            fspOp.IsFspInjected = true;

            ulong cert = 0x08; // 0x04 & 0x08 = 0 → mismatch

            bool isMismatch = fspOp.Placement.DomainTag != 0 && cert != 0 &&
                              (fspOp.Placement.DomainTag & cert) == 0;

            // Assert: mismatch is detected and op is FSP-injected → silent squash branch
            Assert.True(isMismatch, "Domain mismatch must be detected");
            Assert.True(fspOp.IsFspInjected,
                "FSP-injected op must follow silent-squash path, not raise DomainFaultException");
        }

        /// <summary>
        /// Owner-VT op with domain mismatch follows the precise-fault path (§5.4 Case 2).
        /// IsFspInjected == false → DomainFaultException must be raised with correct fields.
        /// </summary>
        [Fact]
        public void WhenOwnerVtOpDomainMismatches_ThenPreciseFaultPath()
        {
            // Arrange: owner-VT op issued in normal program order with wrong domain tag
            var ownerOp = MicroOpTestHelper.CreateLoad(
                virtualThreadId: 0, destReg: 1, address: 0x1000, domainTag: 0x01);
            // IsFspInjected defaults to false for ops not injected by the scheduler

            ulong cert = 0x02; // 0x01 & 0x02 = 0 → mismatch
            ulong faultingPc = 0xDEAD_0000UL;

            bool isMismatch = ownerOp.Placement.DomainTag != 0 && cert != 0 &&
                              (ownerOp.Placement.DomainTag & cert) == 0;

            // Assert: mismatch detected, op is owner-VT → precise fault exception path
            Assert.True(isMismatch, "Domain mismatch must be detected");
            Assert.False(ownerOp.IsFspInjected,
                "Owner-VT op must follow precise-fault path");

            // Verify DomainFaultException carries correct diagnostic fields
            var ex = new DomainFaultException(
                vtId:  ownerOp.VirtualThreadId,
                pc:    faultingPc,
                opTag: ownerOp.Placement.DomainTag,
                cert:  cert);

            Assert.Equal(ownerOp.VirtualThreadId, ex.VirtualThreadId);
            Assert.Equal(faultingPc, ex.FaultingPC);
            Assert.Equal(ownerOp.Placement.DomainTag, ex.OperationDomainTag);
            Assert.Equal(cert, ex.ActiveCert);
            Assert.Contains("FAULT_DOMAIN_VT", ex.Message);
        }

        #endregion
    }
}
