using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using HybridCPU_ISE.Tests.TestHelpers;
using System;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Unit tests for MicroOpTestHelper factory methods.
    /// Verifies that helper methods create valid micro-ops with correct resource and safety masks.
    ///
    /// Created: 2026-03-02
    /// For: testPerfPlan.md Iteration 2 - Test infrastructure validation
    /// </summary>
    public class MicroOpTestHelperTests
    {
        #region Scalar ALU Tests

        [Fact]
        public void CreateScalarALU_ShouldCreateValidOp()
        {
            // Act
            var op = MicroOpTestHelper.CreateScalarALU(
                virtualThreadId: 0,
                destReg: 10,
                src1Reg: 11,
                src2Reg: 12);

            // Assert
            Assert.NotNull(op);
            Assert.Equal(0, op.VirtualThreadId);
            Assert.Equal(10, op.DestRegID);
            Assert.Equal(11, op.Src1RegID);
            Assert.Equal(12, op.Src2RegID);
            Assert.True(op.WritesRegister);
            Assert.NotNull(op.ResourceMask);
            Assert.True(op.SafetyMask.IsNonZero);
        }

        [Fact]
        public void CreateScalarALU_WithCustomOpCode_ShouldUseProvidedOpCode()
        {
            // Arrange
            uint customOpCode = 42;

            // Act
            var op = MicroOpTestHelper.CreateScalarALU(
                virtualThreadId: 1,
                destReg: 1,
                src1Reg: 2,
                src2Reg: 3,
                opCode: customOpCode);

            // Assert
            Assert.Equal(customOpCode, op.OpCode);
        }

        [Fact]
        public void CreateScalarALU_WithDefaultOpCode_ShouldUseAddition()
        {
            // Act
            var op = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);

            // Assert
            Assert.Equal((uint)Processor.CPU_Core.InstructionsEnum.Addition, op.OpCode);
        }

        [Fact]
        public void CreateScalarALU_ShouldHaveValidResourceMask()
        {
            // Act
            var op = MicroOpTestHelper.CreateScalarALU(0, 10, 11, 12);

            // Assert - Resource mask should be non-zero (includes read/write masks)
            Assert.NotNull(op.ResourceMask);
            // Mask should have bits set for register reads and write
            // Exact validation depends on ResourceMaskBuilder implementation
        }

        [Fact]
        public void CreateScalarALU_ShouldHaveValidSafetyMask()
        {
            // Act
            var op = MicroOpTestHelper.CreateScalarALU(0, 10, 11, 12);

            // Assert
            Assert.True(op.SafetyMask.IsNonZero, "Safety mask should be initialized");
        }

        [Fact]
        public void CreateScalarALUImmediate_ShouldCreateValidOp()
        {
            // Act
            var op = MicroOpTestHelper.CreateScalarALUImmediate(
                virtualThreadId: 1,
                destReg: 5,
                srcReg: 6,
                immediate: 100);

            // Assert
            Assert.NotNull(op);
            Assert.Equal(1, op.VirtualThreadId);
            Assert.Equal(5, op.DestRegID);
            Assert.Equal(6, op.Src1RegID);
            Assert.True(op.WritesRegister);
            Assert.True(op.UsesImmediate);
            Assert.True(op.SafetyMask.IsNonZero);
        }

        [Fact]
        public void CreateScalarALUImmediate_ShouldStoreImmediateValue()
        {
            // Act
            var op = MicroOpTestHelper.CreateScalarALUImmediate(0, 1, 2, 0x1234);

            // Assert
            // Immediate value stored in Src2RegID for simple ALU ops
            Assert.Equal(0x1234, op.Src2RegID);
        }

        #endregion

        #region Memory Micro-Op Tests

        [Fact]
        public void CreateLoad_ShouldCreateValidOp()
        {
            // Act
            var op = MicroOpTestHelper.CreateLoad(
                virtualThreadId: 0,
                destReg: 20,
                address: 0x100000,
                domainTag: 0);

            // Assert
            Assert.NotNull(op);
            Assert.Equal(0, op.VirtualThreadId);
            Assert.Equal((ushort)20, op.DestRegID);
            Assert.Equal(0x100000UL, op.Address);
            Assert.Equal((byte)0, op.Placement.DomainTag);
            Assert.True(op.WritesRegister);
            Assert.NotNull(op.ResourceMask);
            Assert.True(op.SafetyMask.IsNonZero);
        }

        [Fact]
        public void CreateLoad_WithDomainTag_ShouldSetCorrectDomain()
        {
            // Act
            var op = MicroOpTestHelper.CreateLoad(1, 10, 0x200000UL, domainTag: 3);

            // Assert
            Assert.Equal((byte)3, op.Placement.DomainTag);
        }

        [Fact]
        public void CreateStore_ShouldCreateValidOp()
        {
            // Act
            var op = MicroOpTestHelper.CreateStore(
                virtualThreadId: 2,
                srcReg: 15,
                address: 0x300000,
                domainTag: 1);

            // Assert
            Assert.NotNull(op);
            Assert.Equal(2, op.VirtualThreadId);
            Assert.Equal((ushort)15, op.SrcRegID);
            Assert.Equal(0x300000UL, op.Address);
            Assert.Equal((byte)1, op.Placement.DomainTag);
            Assert.Equal((byte)sizeof(ulong), op.Size);
            Assert.NotNull(op.ResourceMask);
            Assert.True(op.SafetyMask.IsNonZero);
        }

        [Fact]
        public void CreateStore_WithDomainTag_ShouldSetCorrectDomain()
        {
            // Act
            var op = MicroOpTestHelper.CreateStore(0, 5, 0x400000UL, domainTag: 2);

            // Assert
            Assert.Equal((byte)2, op.Placement.DomainTag);
        }

        [Fact]
        public void CreateStore_ShouldPublishMemoryFactsAndBankIntent()
        {
            ProcessorMemoryScope.WithProcessorMemory(
                ProcessorMemoryScope.CreateMemorySubsystem(numBanks: 16, bankWidthBytes: 4096),
                () =>
            {
                // Act
                var op = MicroOpTestHelper.CreateStore(
                    virtualThreadId: 2,
                    srcReg: 15,
                    address: 4096UL * 7UL,
                    domainTag: 1);

                // Assert
                Assert.True(op.AdmissionMetadata.IsMemoryOp);
                Assert.Equal(7, op.MemoryBankId);

                var writeRange = Assert.Single(op.AdmissionMetadata.WriteMemoryRanges);
                Assert.Equal(4096UL * 7UL, writeRange.Address);
                Assert.Equal((ulong)sizeof(ulong), writeRange.Length);
            });
        }

        [Fact]
        public void CreateLoadStore_WithExplicitBankId_RequiresInitializedRuntimeMemoryGeometry()
        {
            ProcessorMemoryScope.WithProcessorMemory(memory: null, () =>
            {
                InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                    () => MicroOpTestHelper.CreateLoadStore(
                        virtualThreadId: 1,
                        address: 0x1000UL,
                        destReg: 6,
                        isLoad: true,
                        memoryBankId: 3));

                Assert.Contains("requires an initialized Processor.Memory geometry", ex.Message, StringComparison.Ordinal);
            });
        }

        #endregion

        #region Utility Micro-Op Tests

        [Fact]
        public void CreateNop_ShouldCreateValidOp()
        {
            // Act
            var op = MicroOpTestHelper.CreateNop(virtualThreadId: 3);

            // Assert
            Assert.NotNull(op);
            Assert.Equal(3, op.VirtualThreadId);
            Assert.Equal(0u, op.OpCode);
            Assert.NotNull(op.ResourceMask);
            Assert.True(op.SafetyMask.IsZero); // NOP should have zero safety mask
        }

        [Fact]
        public void CreateNop_WithDefaultVirtualThreadId_ShouldUseZero()
        {
            // Act
            var op = MicroOpTestHelper.CreateNop();

            // Assert
            Assert.Equal(0, op.VirtualThreadId);
        }

        #endregion

        #region Test Scenario Helpers - Orthogonal Set

        [Fact]
        public void CreateOrthogonalSet_Count1_ShouldReturn1Op()
        {
            // Act
            var ops = MicroOpTestHelper.CreateOrthogonalSet(1);

            // Assert
            Assert.NotNull(ops);
            Assert.Equal(1, ops.Count);
            Assert.IsType<ScalarALUMicroOp>(ops[0]);
        }

        [Fact]
        public void CreateOrthogonalSet_Count4_ShouldReturn4NonConflictingOps()
        {
            // Act
            var ops = MicroOpTestHelper.CreateOrthogonalSet(4);

            // Assert
            Assert.NotNull(ops);
            Assert.Equal(4, ops.Count);

            // Should include scalar ALU ops for VT0 and VT1
            Assert.IsType<ScalarALUMicroOp>(ops[0]);
            Assert.IsType<ScalarALUMicroOp>(ops[1]);

            // Should include load ops for VT2 and VT3
            Assert.IsType<LoadMicroOp>(ops[2]);
            Assert.IsType<LoadMicroOp>(ops[3]);
        }

        [Fact]
        public void CreateOrthogonalSet_InvalidCount_ShouldThrowException()
        {
            // Act & Assert - Count 0 is invalid
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                MicroOpTestHelper.CreateOrthogonalSet(0));

            // Count 5 is invalid (max is 4)
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                MicroOpTestHelper.CreateOrthogonalSet(5));
        }

        [Fact]
        public void CreateOrthogonalSet_ShouldUseNonOverlappingRegisters()
        {
            // Act
            var ops = MicroOpTestHelper.CreateOrthogonalSet(2);

            // Assert - Verify ops use different register groups
            var op0 = (ScalarALUMicroOp)ops[0];
            var op1 = (ScalarALUMicroOp)ops[1];

            // VT0 uses R0-R7, VT1 uses R8-R15 - no overlap
            Assert.True(op0.DestRegID < 8);
            Assert.True(op1.DestRegID >= 8 && op1.DestRegID < 16);
        }

        #endregion

        #region Test Scenario Helpers - Conflicting Set

        [Fact]
        public void CreateConflictingSet_ShouldReturn2Ops()
        {
            // Act
            var ops = MicroOpTestHelper.CreateConflictingSet();

            // Assert
            Assert.NotNull(ops);
            Assert.Equal(2, ops.Count);
        }

        [Fact]
        public void CreateConflictingSet_ShouldHaveWAWConflict()
        {
            // Act
            var ops = MicroOpTestHelper.CreateConflictingSet();

            // Assert - Both ops write to R1 (WAW conflict)
            var op0 = (ScalarALUMicroOp)ops[0];
            var op1 = (ScalarALUMicroOp)ops[1];

            Assert.Equal(1, op0.DestRegID);
            Assert.Equal(1, op1.DestRegID);
        }

        [Fact]
        public void CreateConflictingSet_ShouldHaveDifferentVirtualThreads()
        {
            // Act
            var ops = MicroOpTestHelper.CreateConflictingSet();

            // Assert
            Assert.NotEqual(ops[0].VirtualThreadId, ops[1].VirtualThreadId);
        }

        #endregion

        #region Test Scenario Helpers - RAW Dependent Set

        [Fact]
        public void CreateRAWDependentSet_ShouldReturn2Ops()
        {
            // Act
            var ops = MicroOpTestHelper.CreateRAWDependentSet();

            // Assert
            Assert.NotNull(ops);
            Assert.Equal(2, ops.Count);
        }

        [Fact]
        public void CreateRAWDependentSet_ShouldHaveRAWDependency()
        {
            // Act
            var ops = MicroOpTestHelper.CreateRAWDependentSet();

            // Assert - First op writes R10, second op reads R10
            var op0 = (ScalarALUMicroOp)ops[0];
            var op1 = (ScalarALUMicroOp)ops[1];

            Assert.Equal(10, op0.DestRegID);
            Assert.True(op1.Src1RegID == 10 || op1.Src2RegID == 10);
        }

        #endregion

        #region Test Scenario Helpers - Memory Conflict Set

        [Fact]
        public void CreateMemoryConflictSet_ShouldReturn2Ops()
        {
            // Act
            var ops = MicroOpTestHelper.CreateMemoryConflictSet();

            // Assert
            Assert.NotNull(ops);
            Assert.Equal(2, ops.Count);
        }

        [Fact]
        public void CreateMemoryConflictSet_ShouldAccessSameDomain()
        {
            // Act
            var ops = MicroOpTestHelper.CreateMemoryConflictSet();

            // Assert - Both ops access domain 0
            var load = (LoadMicroOp)ops[0];
            var store = (StoreMicroOp)ops[1];

            Assert.Equal((byte)0, load.Placement.DomainTag);
            Assert.Equal((byte)0, store.Placement.DomainTag);
        }

        [Fact]
        public void CreateMemoryConflictSet_ShouldHaveLoadAndStore()
        {
            // Act
            var ops = MicroOpTestHelper.CreateMemoryConflictSet();

            // Assert
            Assert.IsType<LoadMicroOp>(ops[0]);
            Assert.IsType<StoreMicroOp>(ops[1]);
        }

        #endregion

        #region Test Scenario Helpers - Diverse Set

        [Fact]
        public void CreateDiverseSet_ShouldReturn5Ops()
        {
            // Act
            var ops = MicroOpTestHelper.CreateDiverseSet();

            // Assert
            Assert.NotNull(ops);
            Assert.Equal(5, ops.Count);
        }

        [Fact]
        public void CreateDiverseSet_ShouldIncludeAllOpTypes()
        {
            // Act
            var ops = MicroOpTestHelper.CreateDiverseSet();

            // Assert - Should have ALU, Load, Store, NOP
            Assert.Contains(ops, op => op is ScalarALUMicroOp && !((ScalarALUMicroOp)op).UsesImmediate);
            Assert.Contains(ops, op => op is ScalarALUMicroOp && ((ScalarALUMicroOp)op).UsesImmediate);
            Assert.Contains(ops, op => op is LoadMicroOp);
            Assert.Contains(ops, op => op is StoreMicroOp);
            Assert.Contains(ops, op => op is NopMicroOp);
        }

        #endregion

        #region Safety Mask Validation Tests

        [Fact]
        public void CreateScalarALU_TwoNonConflictingOps_ShouldHaveNoSafetyMaskOverlap()
        {
            // Act
            var op1 = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);
            var op2 = MicroOpTestHelper.CreateScalarALU(1, destReg: 10, src1Reg: 11, src2Reg: 12);

            // Assert - Different register groups should not conflict
            Assert.False(op1.SafetyMask.ConflictsWith(op2.SafetyMask),
                "Ops using different register groups should not have safety mask conflicts");
        }

        [Fact]
        public void CreateConflictingSet_ShouldHaveSafetyMaskOverlap()
        {
            // Act
            var ops = MicroOpTestHelper.CreateConflictingSet();
            var op0 = ops[0];
            var op1 = ops[1];

            // Assert - Both write R1, should have safety mask conflict
            Assert.True(op0.SafetyMask.ConflictsWith(op1.SafetyMask),
                "Ops writing to same register should have safety mask conflicts");
        }

        [Fact]
        public void CreateMemoryConflictSet_ShouldHaveSafetyMaskOverlap()
        {
            // Act
            var ops = MicroOpTestHelper.CreateMemoryConflictSet();
            var op0 = ops[0];
            var op1 = ops[1];

            // Assert - Both access same memory domain, should conflict
            Assert.True(op0.SafetyMask.ConflictsWith(op1.SafetyMask),
                "Ops accessing same memory domain should have safety mask conflicts");
        }

        #endregion
    }
}
