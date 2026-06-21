using Xunit;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Unit tests for Exception Tracking functionality.
    /// Tests exception counters, exception modes, rounding modes, and exception status management.
    /// </summary>
    public class ExceptionTrackingTests
    {
        private void InitializeTestCore(ulong coreID = 0)
        {
            if (Processor.CPU_Cores == null || Processor.CPU_Cores.Length == 0)
            {
                Processor.CPU_Cores = new Processor.CPU_Core[1024];
            }

            // Reset exception status to defaults
            Processor.CPU_Cores[coreID].ExceptionStatus.Reset();
        }

        [Fact]
        public void Test_ExceptionStatus_Reset_ClearsAllCounters()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;

            // Set some values
            status.OverflowCount = 10;
            status.UnderflowCount = 5;
            status.DivByZeroCount = 3;
            status.InvalidOpCount = 7;
            status.InexactCount = 2;
            status.ExceptionMode = 2;
            status.RoundingMode = 4;

            // Act
            status.Reset();

            // Assert
            Assert.Equal(0u, status.OverflowCount);
            Assert.Equal(0u, status.UnderflowCount);
            Assert.Equal(0u, status.DivByZeroCount);
            Assert.Equal(0u, status.InvalidOpCount);
            Assert.Equal(0u, status.InexactCount);
            Assert.Equal((byte)0, status.ExceptionMode); // Accumulate mode
            Assert.Equal((byte)0, status.RoundingMode); // RNE
            Assert.Equal((byte)1, status.VectorEnabled); // Should be enabled
        }

        [Fact]
        public void Test_ExceptionStatus_ClearCounters_PreservesModesAndFlags()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;

            // Set up state
            status.OverflowCount = 10;
            status.UnderflowCount = 5;
            status.ExceptionMode = 2;
            status.RoundingMode = 3;
            status.VectorEnabled = 1;
            status.VectorDirty = 1;

            // Act
            status.ClearCounters();

            // Assert - Counters cleared
            Assert.Equal(0u, status.OverflowCount);
            Assert.Equal(0u, status.UnderflowCount);
            Assert.Equal(0u, status.DivByZeroCount);
            Assert.Equal(0u, status.InvalidOpCount);
            Assert.Equal(0u, status.InexactCount);

            // Assert - Modes and flags preserved
            Assert.Equal((byte)2, status.ExceptionMode);
            Assert.Equal((byte)3, status.RoundingMode);
            Assert.Equal((byte)1, status.VectorEnabled);
            Assert.Equal((byte)1, status.VectorDirty);
        }

        [Fact]
        public void Test_ExceptionStatus_HasExceptions_DetectsAnyException()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;

            // Initially no exceptions
            Assert.False(status.HasExceptions());

            // Test each exception type
            status.OverflowCount = 1;
            Assert.True(status.HasExceptions());
            status.OverflowCount = 0;

            status.UnderflowCount = 1;
            Assert.True(status.HasExceptions());
            status.UnderflowCount = 0;

            status.DivByZeroCount = 1;
            Assert.True(status.HasExceptions());
            status.DivByZeroCount = 0;

            status.InvalidOpCount = 1;
            Assert.True(status.HasExceptions());
            status.InvalidOpCount = 0;

            status.InexactCount = 1;
            Assert.True(status.HasExceptions());
        }

        [Fact]
        public void Test_SetExceptionMode_ValidModes_ReturnsTrue()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;

            // Test all valid modes
            for (byte mode = 0; mode <= 2; mode++)
            {
                // Act
                bool result = status.SetExceptionMode(mode);

                // Assert
                Assert.True(result);
                Assert.Equal(mode, status.ExceptionMode);
            }
        }

        [Fact]
        public void Test_SetExceptionMode_InvalidMode_ReturnsFalse()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;
            status.ExceptionMode = 1; // Set to valid mode

            // Act
            bool result = status.SetExceptionMode(3); // Invalid mode

            // Assert
            Assert.False(result);
            Assert.Equal((byte)1, status.ExceptionMode); // Should not change
        }

        [Fact]
        public void Test_SetRoundingMode_ValidModes_ReturnsTrue()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;

            // Test all valid rounding modes
            for (byte mode = 0; mode <= 4; mode++)
            {
                // Act
                bool result = status.SetRoundingMode(mode);

                // Assert
                Assert.True(result);
                Assert.Equal(mode, status.RoundingMode);
            }
        }

        [Fact]
        public void Test_SetRoundingMode_InvalidMode_ReturnsFalse()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;
            status.RoundingMode = 2; // Set to valid mode

            // Act
            bool result = status.SetRoundingMode(5); // Invalid mode

            // Assert
            Assert.False(result);
            Assert.Equal((byte)2, status.RoundingMode); // Should not change
        }

        [Fact]
        public void Test_ExceptionMode_Accumulate_DefaultMode()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;

            // Assert - Default should be accumulate mode (0)
            Assert.Equal((byte)0, status.ExceptionMode);
        }

        [Fact]
        public void Test_RoundingMode_RNE_DefaultMode()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;

            // Assert - Default should be RNE (0) per IEEE 754
            Assert.Equal((byte)0, status.RoundingMode);
        }

        [Fact]
        public void Test_OverflowCount_Increment()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;

            // Act
            status.OverflowCount++;
            status.OverflowCount++;
            status.OverflowCount++;

            // Assert
            Assert.Equal(3u, status.OverflowCount);
        }

        [Fact]
        public void Test_UnderflowCount_Increment()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;

            // Act
            status.UnderflowCount += 5;

            // Assert
            Assert.Equal(5u, status.UnderflowCount);
        }

        [Fact]
        public void Test_DivByZeroCount_Increment()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;

            // Act
            status.DivByZeroCount++;

            // Assert
            Assert.Equal(1u, status.DivByZeroCount);
        }

        [Fact]
        public void Test_InvalidOpCount_Increment()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;

            // Act
            status.InvalidOpCount += 7;

            // Assert
            Assert.Equal(7u, status.InvalidOpCount);
        }

        [Fact]
        public void Test_InexactCount_Increment()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;

            // Act
            status.InexactCount += 100;

            // Assert
            Assert.Equal(100u, status.InexactCount);
        }

        [Fact]
        public void Test_MultipleExceptions_IndependentCounters()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;

            // Act - Simulate multiple different exceptions
            status.OverflowCount += 3;
            status.UnderflowCount += 2;
            status.DivByZeroCount += 1;
            status.InvalidOpCount += 4;
            status.InexactCount += 5;

            // Assert - Each counter is independent
            Assert.Equal(3u, status.OverflowCount);
            Assert.Equal(2u, status.UnderflowCount);
            Assert.Equal(1u, status.DivByZeroCount);
            Assert.Equal(4u, status.InvalidOpCount);
            Assert.Equal(5u, status.InexactCount);
            Assert.Equal(15u, status.TotalExceptions());
        }

        [Fact]
        public void Test_VectorDirty_Flag()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;

            // Initially clean after reset
            Assert.Equal((byte)0, status.VectorDirty);

            // Act - Mark as dirty
            status.VectorDirty = 1;

            // Assert
            Assert.Equal((byte)1, status.VectorDirty);

            // Clear
            status.VectorDirty = 0;
            Assert.Equal((byte)0, status.VectorDirty);
        }

        [Fact]
        public void Test_VectorEnabled_Flag()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;

            // Default is enabled
            Assert.Equal((byte)1, status.VectorEnabled);

            // Act - Disable
            status.VectorEnabled = 0;

            // Assert
            Assert.Equal((byte)0, status.VectorEnabled);
        }

        [Fact]
        public void Test_ExceptionMode_Persistence()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;

            // Act - Set mode and accumulate exceptions
            status.SetExceptionMode(1); // Trap on first
            status.OverflowCount += 5;
            status.UnderflowCount += 3;

            // Assert - Mode persists even with exceptions
            Assert.Equal((byte)1, status.ExceptionMode);
            Assert.Equal(5u, status.OverflowCount);
        }

        [Fact]
        public void Test_RoundingMode_Persistence()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;

            // Act - Set rounding mode and accumulate exceptions
            status.SetRoundingMode(3); // RUP
            status.InexactCount += 10;

            // Assert - Rounding mode persists
            Assert.Equal((byte)3, status.RoundingMode);
            Assert.Equal(10u, status.InexactCount);
        }

        [Fact]
        public void Test_MultiCore_IndependentExceptionStatus()
        {
            // Arrange
            InitializeTestCore(0);
            InitializeTestCore(1);
            InitializeTestCore(2);

            // Act - Set different states for each core
            Processor.CPU_Cores[0].ExceptionStatus.ExceptionMode = 0;
            Processor.CPU_Cores[0].ExceptionStatus.OverflowCount = 10;

            Processor.CPU_Cores[1].ExceptionStatus.ExceptionMode = 1;
            Processor.CPU_Cores[1].ExceptionStatus.UnderflowCount = 5;

            Processor.CPU_Cores[2].ExceptionStatus.ExceptionMode = 2;
            Processor.CPU_Cores[2].ExceptionStatus.DivByZeroCount = 3;

            // Assert - Each core has independent state
            Assert.Equal((byte)0, Processor.CPU_Cores[0].ExceptionStatus.ExceptionMode);
            Assert.Equal(10u, Processor.CPU_Cores[0].ExceptionStatus.OverflowCount);
            Assert.Equal(0u, Processor.CPU_Cores[0].ExceptionStatus.UnderflowCount);

            Assert.Equal((byte)1, Processor.CPU_Cores[1].ExceptionStatus.ExceptionMode);
            Assert.Equal(0u, Processor.CPU_Cores[1].ExceptionStatus.OverflowCount);
            Assert.Equal(5u, Processor.CPU_Cores[1].ExceptionStatus.UnderflowCount);

            Assert.Equal((byte)2, Processor.CPU_Cores[2].ExceptionStatus.ExceptionMode);
            Assert.Equal(3u, Processor.CPU_Cores[2].ExceptionStatus.DivByZeroCount);
        }

        [Fact]
        public void Test_ExceptionCounters_MaxValue()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;

            // Act - Set to maximum uint value
            status.OverflowCount = uint.MaxValue;
            status.UnderflowCount = uint.MaxValue;
            status.DivByZeroCount = uint.MaxValue;
            status.InvalidOpCount = uint.MaxValue;
            status.InexactCount = uint.MaxValue;

            // Assert - Values should hold maximum
            Assert.Equal(uint.MaxValue, status.OverflowCount);
            Assert.Equal(uint.MaxValue, status.UnderflowCount);
            Assert.Equal(uint.MaxValue, status.DivByZeroCount);
            Assert.Equal(uint.MaxValue, status.InvalidOpCount);
            Assert.Equal(uint.MaxValue, status.InexactCount);
        }

        [Fact]
        public void Test_ClearExceptionCounters_Alias()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;

            status.OverflowCount = 10;
            status.UnderflowCount = 5;

            // Act - Test alias method
            status.ClearExceptionCounters();

            // Assert
            Assert.Equal(0u, status.OverflowCount);
            Assert.Equal(0u, status.UnderflowCount);
        }

        [Fact]
        public void Test_ExceptionStatus_ContextSwitch_Simulation()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;

            // Task A: set up state
            status.SetExceptionMode(1);
            status.SetRoundingMode(2);
            status.OverflowCount = 5;
            status.VectorDirty = 1;

            // Save Task A state
            byte savedMode = status.ExceptionMode;
            byte savedRounding = status.RoundingMode;
            uint savedOverflow = status.OverflowCount;

            // Task B: change state
            status.SetExceptionMode(2);
            status.SetRoundingMode(3);
            status.ClearCounters();
            status.UnderflowCount = 10;

            // Restore Task A state
            status.SetExceptionMode(savedMode);
            status.SetRoundingMode(savedRounding);
            status.ClearCounters();
            status.OverflowCount = savedOverflow;

            // Assert - Task A state restored
            Assert.Equal((byte)1, status.ExceptionMode);
            Assert.Equal((byte)2, status.RoundingMode);
            Assert.Equal(5u, status.OverflowCount);
        }

        [Fact]
        public void Test_RoundingMode_AllModes()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;

            // Test each rounding mode
            var modes = new (byte mode, string name)[]
            {
                (0, "RNE"), // Round to Nearest, ties to Even
                (1, "RTZ"), // Round Toward Zero
                (2, "RDN"), // Round Down
                (3, "RUP"), // Round Up
                (4, "RMM")  // Round to Nearest, ties to Max Magnitude
            };

            foreach (var (mode, name) in modes)
            {
                // Act
                bool result = status.SetRoundingMode(mode);

                // Assert
                Assert.True(result, $"Failed to set rounding mode {name} ({mode})");
                Assert.Equal(mode, status.RoundingMode);
            }
        }

        [Fact]
        public void Test_ExceptionMode_AllModes()
        {
            // Arrange
            InitializeTestCore();
            ref var status = ref Processor.CPU_Cores[0].ExceptionStatus;

            // Test each exception mode
            var modes = new (byte mode, string name)[]
            {
                (0, "Accumulate"),
                (1, "Trap on First"),
                (2, "Trap on Any")
            };

            foreach (var (mode, name) in modes)
            {
                // Act
                bool result = status.SetExceptionMode(mode);

                // Assert
                Assert.True(result, $"Failed to set exception mode {name} ({mode})");
                Assert.Equal(mode, status.ExceptionMode);
            }
        }
    }
}
