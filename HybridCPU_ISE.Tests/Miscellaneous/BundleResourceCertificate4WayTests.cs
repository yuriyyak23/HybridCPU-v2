using Xunit;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Unit tests for BundleResourceCertificate4Way:
    /// - Two-stage conflict detection (SharedMask + per-VT register masks)
    /// - Cross-VT register independence (no false conflicts)
    /// - Shared resource blocking (DMA, LSU, memory domains)
    /// </summary>
    public class BundleResourceCertificate4WayTests
    {
        /// <summary>
        /// Helper: create a ScalarALUMicroOp with specified registers and VirtualThreadId.
        /// </summary>
        private ScalarALUMicroOp CreateAluOp(
            int vt,
            ushort dest,
            ushort src1,
            ushort src2,
            bool writesRegister = true,
            bool usesImmediate = false,
            ulong structuralLowMask = 0)
        {
            var op = new ScalarALUMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Addition,
                OwnerThreadId = 0,
                VirtualThreadId = vt,
                DestRegID = dest,
                Src1RegID = src1,
                Src2RegID = src2,
                UsesImmediate = usesImmediate,
                WritesRegister = writesRegister,
                SafetyMask = new SafetyMask128(structuralLowMask, 0)
            };
            op.InitializeMetadata();
            return op;
        }

        private ScalarALUMicroOp CreateReadOnlyAluOp(int vt, ushort src)
        {
            return CreateAluOp(
                vt,
                VLIW_Instruction.NoReg,
                src,
                VLIW_Instruction.NoReg,
                writesRegister: false,
                usesImmediate: true);
        }

        private ScalarALUMicroOp CreateWriteOnlyAluOp(int vt, ushort dest)
        {
            return CreateAluOp(
                vt,
                dest,
                VLIW_Instruction.NoReg,
                VLIW_Instruction.NoReg,
                writesRegister: true,
                usesImmediate: true);
        }

        /// <summary>
        /// Helper: create a MicroOp with explicit SafetyMask and VirtualThreadId.
        /// </summary>
        private MicroOp CreateOpWithMask(int vt, ulong low, ulong high = 0)
        {
            var op = new NopMicroOp
            {
                OpCode = 0,
                OwnerThreadId = 0,
                VirtualThreadId = vt,
                SafetyMask = new SafetyMask128(low, high)
            };
            return op;
        }

        [Fact]
        public void WhenEmptyCertThenAnyOperationCanBeInjected()
        {
            // Arrange
            var cert = BundleResourceCertificate4Way.Empty;
            var op = CreateAluOp(vt: 0, dest: 4, src1: 0, src2: 1);

            // Act & Assert
            Assert.True(cert.CanInject(op));
        }

        [Fact]
        public void WhenSameVtSameRegGroupThenConflictDetected()
        {
            // Arrange: both ops on VT0 use register group 0 (regs 0–3)
            var cert = BundleResourceCertificate4Way.Empty;
            var op1 = CreateAluOp(vt: 0, dest: 0, src1: 1, src2: 2);
            var op2 = CreateAluOp(vt: 0, dest: 0, src1: 1, src2: 3);

            cert.AddOperation(op1);

            // Act & Assert: same VT, same register group → conflict
            Assert.False(cert.CanInject(op2));
        }

        [Fact]
        public void WhenLiveSafetyMasksAreClearedThenAdmissionMetadataStillRejectsSameVtConflict()
        {
            var cert = BundleResourceCertificate4Way.Empty;
            var op1 = CreateAluOp(vt: 0, dest: 0, src1: 1, src2: 2);
            var op2 = CreateAluOp(vt: 0, dest: 0, src1: 1, src2: 3);

            op1.SafetyMask = SafetyMask128.Zero;
            op2.SafetyMask = SafetyMask128.Zero;

            cert.AddOperation(op1);

            Assert.False(cert.CanInject(op2));
        }

        [Fact]
        public void WhenDifferentVtSameRegGroupThenNoConflict()
        {
            // Arrange: VT0 and VT1 use the same register group 0
            var cert = BundleResourceCertificate4Way.Empty;
            var op1 = CreateAluOp(vt: 0, dest: 0, src1: 1, src2: 2);
            var op2 = CreateAluOp(vt: 1, dest: 0, src1: 1, src2: 3);

            cert.AddOperation(op1);

            // Act & Assert: different VTs have independent register banks → no conflict
            Assert.True(cert.CanInject(op2));
        }

        [Fact]
        public void WhenSharedResourceConflictThenRejectedRegardlessOfVt()
        {
            // Arrange: both ops claim the same memory domain (bit 32)
            var cert = BundleResourceCertificate4Way.Empty;
            var op1 = CreateOpWithMask(vt: 0, low: 1UL << 32);
            var op2 = CreateOpWithMask(vt: 1, low: 1UL << 32);

            cert.AddOperation(op1);

            // Act & Assert: shared resource conflict (memory domain)
            Assert.False(cert.CanInject(op2));
        }

        [Fact]
        public void WhenDifferentSharedResourcesThenNoConflict()
        {
            // Arrange: different memory domains (bit 32 vs bit 33)
            var cert = BundleResourceCertificate4Way.Empty;
            var op1 = CreateOpWithMask(vt: 0, low: 1UL << 32);
            var op2 = CreateOpWithMask(vt: 1, low: 1UL << 33);

            cert.AddOperation(op1);

            // Act & Assert: no shared resource conflict
            Assert.True(cert.CanInject(op2));
        }

        [Fact]
        public void WhenAllFourVtsThenPackedCorrectly()
        {
            // Arrange: 4 ops from 4 different VTs, each using different register groups
            var cert = BundleResourceCertificate4Way.Empty;

            var op0 = CreateAluOp(vt: 0, dest: 0, src1: 1, src2: 2);   // group 0
            var op1 = CreateAluOp(vt: 1, dest: 4, src1: 5, src2: 6);   // group 1
            var op2 = CreateAluOp(vt: 2, dest: 8, src1: 9, src2: 10);  // group 2
            var op3 = CreateAluOp(vt: 3, dest: 12, src1: 13, src2: 14);// group 3

            // Act
            Assert.True(cert.CanInject(op0));
            cert.AddOperation(op0);

            Assert.True(cert.CanInject(op1));
            cert.AddOperation(op1);

            Assert.True(cert.CanInject(op2));
            cert.AddOperation(op2);

            Assert.True(cert.CanInject(op3));
            cert.AddOperation(op3);

            // Assert
            Assert.Equal(4, cert.OperationCount);
        }

        [Fact]
        public void WhenHighBitResourceConflictThenRejected()
        {
            // Arrange: both ops claim same extended GRLB channel (High bit 0)
            var cert = BundleResourceCertificate4Way.Empty;
            var op1 = CreateOpWithMask(vt: 0, low: 0, high: 1UL);
            var op2 = CreateOpWithMask(vt: 2, low: 0, high: 1UL);

            cert.AddOperation(op1);

            // Act & Assert: High-bit shared resource conflict
            Assert.False(cert.CanInject(op2));
        }

        [Fact]
        public void WhenLsuBitsConflictThenRejected()
        {
            // Arrange: both ops claim LSU load channel (bit 48)
            var cert = BundleResourceCertificate4Way.Empty;
            var op1 = CreateOpWithMask(vt: 0, low: 1UL << 48);
            var op2 = CreateOpWithMask(vt: 3, low: 1UL << 48);

            cert.AddOperation(op1);

            // Act & Assert: LSU conflict is a shared resource, not per-VT
            Assert.False(cert.CanInject(op2));
        }

        [Fact]
        public void WhenDmaChannelConflictThenRejected()
        {
            // Arrange: both ops claim DMA channel 0 (bit 51)
            var cert = BundleResourceCertificate4Way.Empty;
            var op1 = CreateOpWithMask(vt: 1, low: 1UL << 51);
            var op2 = CreateOpWithMask(vt: 2, low: 1UL << 51);

            cert.AddOperation(op1);

            Assert.False(cert.CanInject(op2));
        }

        [Fact]
        public void GetRegMaskReturnsCorrectPerVtMask()
        {
            var cert = BundleResourceCertificate4Way.Empty;
            // reg group 0 (bits 0–3) for VT0, reg group 4 (bit 4 in read groups) for VT2
            var op0 = CreateAluOp(vt: 0, dest: 0, src1: 0, src2: VLIW_Instruction.NoReg, usesImmediate: true);
            var op2 = CreateAluOp(vt: 2, dest: 16, src1: 16, src2: VLIW_Instruction.NoReg, usesImmediate: true);

            cert.AddOperation(op0);
            cert.AddOperation(op2);

            Assert.Equal(0x0001_0001U, cert.GetRegMask(0));
            Assert.Equal(0U, cert.GetRegMask(1));
            Assert.Equal(0x0010_0010U, cert.GetRegMask(2));
            Assert.Equal(0U, cert.GetRegMask(3));
        }

        [Fact]
        public void AddOperationIncrementsOperationCount()
        {
            var cert = BundleResourceCertificate4Way.Empty;
            Assert.Equal(0, cert.OperationCount);

            cert.AddOperation(CreateOpWithMask(vt: 0, low: 1));
            Assert.Equal(1, cert.OperationCount);

            cert.AddOperation(CreateOpWithMask(vt: 1, low: 2));
            Assert.Equal(2, cert.OperationCount);
        }

        [Fact]
        public void ToStringIncludesAllFields()
        {
            var cert = BundleResourceCertificate4Way.Empty;
            cert.AddOperation(CreateOpWithMask(vt: 0, low: 0x0001));

            string result = cert.ToString();

            Assert.Contains("Cert4Way", result);
            Assert.Contains("Ops=1", result);
            Assert.Contains("VT0=", result);
        }

        #region RAR-Aware Check (§3b audit fix)

        /// <summary>
        /// Two ops from the same VT that only READ the same register group must not conflict.
        /// RAR is not a true hazard; the RAR-aware CanInject should allow both to coexist.
        /// Mask layout: bits 0–15 = read groups, bits 16–31 = write groups.
        /// </summary>
        [Fact]
        public void WhenSameVtBothOpsOnlyReadSameGroupThenNoFalseConflict()
        {
            // Arrange: two ops on VT0, both set only read-group bit 0 (bit 0, no write bits)
            var cert = BundleResourceCertificate4Way.Empty;
            var op1 = CreateReadOnlyAluOp(vt: 0, src: 0);
            var op2 = CreateReadOnlyAluOp(vt: 0, src: 1);

            cert.AddOperation(op1);

            // After RAR fix: two reads of the same group from the same VT must not conflict
            Assert.True(cert.CanInject(op2),
                "RAR: two reads of same register group from same VT must not produce a false conflict");
        }

        /// <summary>
        /// A candidate that writes a register group already being read by the bundle must conflict.
        /// WAR is a true hazard and must be blocked.
        /// </summary>
        [Fact]
        public void WhenSameVtCandidateWritesGroupAlreadyReadThenConflictDetected()
        {
            // Arrange: op1 reads group 0 (bit 0); op2 writes group 0 (bit 16)
            var cert = BundleResourceCertificate4Way.Empty;
            var readOp = CreateReadOnlyAluOp(vt: 0, src: 0);
            var writeOp = CreateWriteOnlyAluOp(vt: 0, dest: 1);

            cert.AddOperation(readOp);

            // WAR: candidate writes what bundle already reads → true conflict
            Assert.False(cert.CanInject(writeOp),
                "WAR: candidate writing a group already read by the bundle must be rejected");
        }

        #endregion
    }
}
