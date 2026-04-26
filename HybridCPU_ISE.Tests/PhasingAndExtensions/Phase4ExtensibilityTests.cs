using System;
using System;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Accelerators;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Tests for Phase 4: Extensibility - HLS Accelerator Interface & Pluggable FSP Strategies
    /// </summary>
    public class Phase4ExtensibilityTests
    {
        #region Custom Accelerator Tests

        [Fact]
        public void Test_4_1_RegisterCustomAccelerator()
        {
            // Arrange
            InstructionRegistry.Clear();
            InstructionRegistry.Initialize();
            var matMul = new MatMulAccelerator();

            // Act
            InstructionRegistry.RegisterAccelerator(matMul);

            // Assert
            var retrieved = InstructionRegistry.GetAccelerator("MatMul");
            Assert.NotNull(retrieved);
            Assert.Equal("MatMul", retrieved.Name);
        }

        [Fact]
        public void Test_4_2_ExecuteAcceleratorOperation()
        {
            // Arrange
            var matMul = new MatMulAccelerator();
            ulong[] operands = new ulong[]
            {
                0x1000,  // matA_addr
                0x2000,  // matB_addr
                0x3000,  // matC_addr
                4,       // M
                4,       // N
                4        // K
            };
            byte[] config = Array.Empty<byte>();

            // Act
            ulong[] results = matMul.Execute(0xC000, operands, config);

            // Assert
            Assert.NotNull(results);
            Assert.Single(results);
            Assert.Equal(0x3000UL, results[0]); // Returns result address
        }

        [Fact]
        public void Test_4_3_DataDependentLatency()
        {
            // Arrange
            var matMul = new MatMulAccelerator();
            ulong[] smallOperands = new ulong[] { 0x1000, 0x2000, 0x3000, 2, 2, 2 }; // 2x2x2
            ulong[] largeOperands = new ulong[] { 0x1000, 0x2000, 0x3000, 8, 8, 8 }; // 8x8x8

            // Act
            int smallLatency = matMul.GetLatency(0xC000, smallOperands);
            int largeLatency = matMul.GetLatency(0xC000, largeOperands);

            // Assert
            Assert.True(smallLatency > 0);
            Assert.True(largeLatency > smallLatency); // Larger matrices take longer
        }

        [Fact]
        public void Test_4_4_AcceleratorResourceFootprint()
        {
            // Arrange
            var matMul = new MatMulAccelerator();

            // Act
            var footprint = matMul.GetResourceFootprint(0xC000);

            // Assert
            Assert.True(footprint.MemoryBandwidthMBps > 0);
            Assert.True(footprint.RequiresExclusiveAccess); // MatMul requires exclusive access
        }

        [Fact]
        public void Test_4_5_AcceleratorIsPipelined()
        {
            // Arrange
            var matMul = new MatMulAccelerator();

            // Act
            bool isPipelined = matMul.IsPipelined(0xC000);

            // Assert
            Assert.True(isPipelined); // MatMul is pipelined
        }

        [Fact]
        public void Test_4_6_AcceleratorReset()
        {
            // Arrange
            var matMul = new MatMulAccelerator();

            // Act - should not throw
            matMul.Reset();

            // Assert - verify accelerator can still execute after reset
            ulong[] operands = new ulong[] { 0x1000, 0x2000, 0x3000, 2, 2, 2 };
            ulong[] results = matMul.Execute(0xC000, operands, Array.Empty<byte>());
            Assert.NotNull(results);
        }

        [Fact]
        public void Test_4_7_CustomAcceleratorRuntimeSurface_FailsClosedUntilTruthfulCarrierExists()
        {
            // Arrange
            InstructionRegistry.Clear();
            InstructionRegistry.Initialize();
            var matMul = new MatMulAccelerator();
            InstructionRegistry.RegisterAccelerator(matMul);

            var context = new DecoderContext
            {
                OpCode = 0xC000,
                Reg1ID = 1,
                Reg2ID = 2,
                Reg3ID = 3,
                AuxData = 0
            };

            // Act / Assert
            Assert.True(InstructionRegistry.IsCustomAcceleratorOpcode(0xC000));

            InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
                () => InstructionRegistry.CreateMicroOp(0xC000, context));

            Assert.Contains("fail closed", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("truthful canonical publication", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Nomination-Based Scheduling Tests

        [Fact]
        public void Test_4_8_Nominate_StalledCore_ShouldBeIgnored()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.SetCoreStalled(1, true);
            var op = new NopMicroOp { OwnerThreadId = 1 };

            // Act
            scheduler.Nominate(1, op);

            // Assert - stalled core's nomination should be annulled
            var stolen = scheduler.TryStealSlot(0, 0);
            Assert.Null(stolen);
        }

        [Fact]
        public void Test_4_9_Nominate_ControlFlowOp_ShouldBeIgnored()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var cfOp = new BranchMicroOp { OwnerThreadId = 1 };

            // Act - control flow ops should not be nominated
            scheduler.Nominate(1, cfOp);

            // Assert
            var stolen = scheduler.TryStealSlot(0, 0);
            Assert.Null(stolen);
        }

        [Fact]
        public void Test_4_10_TryStealSlot_PriorityEncoder_ShouldReturnLowestIndex()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var op5 = new NopMicroOp { OwnerThreadId = 5 };
            var op2 = new NopMicroOp { OwnerThreadId = 2 };

            scheduler.Nominate(5, op5);
            scheduler.Nominate(2, op2);

            // Act - priority encoder scans 0→15
            var stolen = scheduler.TryStealSlot(0, 0);

            // Assert - should return core 2's nomination (lower index)
            Assert.NotNull(stolen);
            Assert.Same(op2, stolen);
        }

        [Fact]
        public void Test_4_11_PackBundle_WithNominations_ShouldFillSlots()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var microOp1 = new NopMicroOp { OwnerThreadId = 1 };
            var microOp2 = new NopMicroOp { OwnerThreadId = 2 };
            scheduler.Nominate(1, microOp1);
            scheduler.Nominate(2, microOp2);

            var originalBundle = new MicroOp[8]; // Empty bundle (null slots)

            // Act
            var packedBundle = scheduler.PackBundle(
                originalBundle,
                currentThreadId: 0,
                stealEnabled: true,
                stealMask: 0xFF
            );

            // Assert
            Assert.NotNull(packedBundle);
        }

        [Fact]
        public void Test_4_12_ClearNominationPorts_ShouldResetAllPorts()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.Nominate(0, new NopMicroOp { OwnerThreadId = 0 });
            scheduler.Nominate(5, new NopMicroOp { OwnerThreadId = 5 });
            scheduler.Nominate(15, new NopMicroOp { OwnerThreadId = 15 });

            // Act
            scheduler.ClearNominationPorts();

            // Assert
            var stolen = scheduler.TryStealSlot(0, 0);
            Assert.Null(stolen);
        }

        [Fact]
        public void Test_4_15_MultipleAccelerators()
        {
            // Arrange
            InstructionRegistry.Clear();
            InstructionRegistry.Initialize();
            var matMul = new MatMulAccelerator();
            InstructionRegistry.RegisterAccelerator(matMul);

            // Act
            var allAccelerators = InstructionRegistry.GetAllAccelerators();

            // Assert
            Assert.NotEmpty(allAccelerators);
            Assert.True(allAccelerators.ContainsKey("MatMul"));
        }

        #endregion
    }
}
