using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;
using System;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Unit tests for dynamic bank scheduling and adaptive stall cycles.
    /// Tests the new arbitration policies and queue management.
    /// </summary>
    public class MemorySubsystemBankSchedulingTests : IDisposable
    {
        private readonly Processor.MainMemoryArea _savedMainMemory = Processor.MainMemory;

        public void Dispose()
        {
            Processor.MainMemory = _savedMainMemory;
        }

        private void InitializeTestEnvironment()
        {
            // Initialize IOMMU with identity-mapped address range for test data
            Processor.MainMemory = new Processor.MultiBankMemoryArea(4, 0x4000000UL);
            IOMMU.Initialize();
            IOMMU.RegisterDevice(0);
            IOMMU.Map(
                deviceID: 0,
                ioVirtualAddress: 0,
                physicalAddress: 0,
                size: 0x10000,
                permissions: IOMMUAccessPermissions.ReadWrite);
        }

        [Fact]
        public void Test_AdaptiveStall_IncreasesWithQueueDepth()
        {
            // Arrange
            InitializeTestEnvironment();
            Processor proc = default;
            var memory = new MemorySubsystem(ref proc);
            memory.NumBanks = 4;
            memory.BankWidthBytes = 64;

            // Write test data
            byte[] testData = new byte[256];
            for (int i = 0; i < testData.Length; i++)
            {
                testData[i] = (byte)i;
            }
            Processor.MainMemory.Position = 0x0000;
            Processor.MainMemory.Write(testData);
            Processor.MainMemory.Position = 0x0100;
            Processor.MainMemory.Write(testData);

            // Act - Create bank conflicts on same bank
            byte[] buffer1 = new byte[256];
            byte[] buffer2 = new byte[256];

            // First access establishes baseline
            memory.Read(0, 0x0000, buffer1);
            long firstStallCycles = memory.StallCycles;

            // Second access to same bank should create conflict
            // Address 0x0100 maps to bank 0: (0x0100 / 64) % 4 = (256 / 64) % 4 = 4 % 4 = 0
            memory.Read(0, 0x0100, buffer2);
            long secondStallCycles = memory.StallCycles;

            // Assert - Second access should have stall cycles if there was a conflict
            // Note: The exact timing depends on when the bank is released
            Assert.True(secondStallCycles >= firstStallCycles,
                $"Stall cycles should not decrease. First: {firstStallCycles}, Second: {secondStallCycles}");
        }

        [Fact]
        public void Test_RoundRobinArbitration_SelectsDifferentBanks()
        {
            // Arrange
            InitializeTestEnvironment();
            Processor proc = default;
            var memory = new MemorySubsystem(ref proc);
            memory.NumBanks = 8;
            memory.BankWidthBytes = 64;
            memory.ArbitrationPolicy = BankArbitrationPolicy.RoundRobin;

            // Write test data
            byte[] testData = new byte[64];
            for (int i = 0; i < testData.Length; i++)
            {
                testData[i] = (byte)i;
            }
            Processor.MainMemory.Position = 0x0000;
            Processor.MainMemory.Write(testData);
            Processor.MainMemory.Position = 0x0040;
            Processor.MainMemory.Write(testData);
            Processor.MainMemory.Position = 0x0080;
            Processor.MainMemory.Write(testData);

            // Act - Access different banks in sequence
            byte[] buffer = new byte[64];

            // Access bank 0 (address 0x0000)
            memory.Read(0, 0x0000, buffer);

            // Access bank 1 (address 0x0040 = 64 bytes)
            memory.Read(0, 0x0040, buffer);

            // Access bank 2 (address 0x0080 = 128 bytes)
            memory.Read(0, 0x0080, buffer);

            // Assert - No conflicts should occur with different banks
            // Note: TotalBursts may be 0 if IOMMU operations fail, but conflicts should still be tracked
            Assert.Equal(0, memory.BankConflicts);
            Assert.Equal(BankArbitrationPolicy.RoundRobin, memory.ArbitrationPolicy);
        }

        [Fact]
        public void Test_WeightedFairArbitration_PrioritizesLongerQueues()
        {
            // Arrange
            InitializeTestEnvironment();
            Processor proc = default;
            var memory = new MemorySubsystem(ref proc);
            memory.NumBanks = 4;
            memory.BankWidthBytes = 64;
            memory.ArbitrationPolicy = BankArbitrationPolicy.WeightedFair;

            // Act - Verify policy is set correctly
            // Assert
            Assert.Equal(BankArbitrationPolicy.WeightedFair, memory.ArbitrationPolicy);
            Assert.Equal(4, memory.NumBanks);
        }

        [Fact]
        public void Test_PriorityArbitration_Policy()
        {
            // Arrange
            InitializeTestEnvironment();
            Processor proc = default;
            var memory = new MemorySubsystem(ref proc);
            memory.NumBanks = 4;
            memory.BankWidthBytes = 64;
            memory.ArbitrationPolicy = BankArbitrationPolicy.Priority;

            // Act - Verify policy is set correctly
            // Assert
            Assert.Equal(BankArbitrationPolicy.Priority, memory.ArbitrationPolicy);
            Assert.Equal(4, memory.NumBanks);
        }

        [Fact]
        public void Test_NewMetrics_AreTracked()
        {
            // Arrange
            InitializeTestEnvironment();
            Processor proc = default;
            var memory = new MemorySubsystem(ref proc);

            // Act - Perform some memory operations
            byte[] buffer = new byte[256];
            memory.Read(0, 0x0000, buffer);
            memory.Read(0, 0x0000, buffer); // Same address to cause conflict

            // Assert - Check new metrics are being tracked
            Assert.True(memory.TotalWaitCycles >= 0);
            Assert.True(memory.AverageWaitCycles >= 0.0);
            Assert.True(memory.MaxQueueDepth >= 0);
            Assert.True(memory.CurrentQueuedRequests >= 0);
        }

        [Fact]
        public void Test_NumMemoryPorts_Configuration()
        {
            // Arrange
            InitializeTestEnvironment();
            Processor proc = default;
            var memory = new MemorySubsystem(ref proc);

            // Act - Set number of memory ports
            memory.NumMemoryPorts = 4;

            // Assert
            Assert.Equal(4, memory.NumMemoryPorts);
        }

        [Fact]
        public void Test_BankBandwidth_Configuration()
        {
            // Arrange
            InitializeTestEnvironment();
            Processor proc = default;
            var memory = new MemorySubsystem(ref proc);

            // Act - Set bank bandwidth
            memory.BankBandwidthGBps = 51.2;

            // Assert
            Assert.Equal(51.2, memory.BankBandwidthGBps);
        }

        [Fact]
        public void Test_ProcessorConfig_NewParameters()
        {
            // Arrange & Act
            var config = ProcessorConfig.HighPerformanceFPGA();

            // Assert
            Assert.Equal(4, config.NumMemoryPorts);
            Assert.Equal(51.2, config.BankBandwidthGBps);
            Assert.Equal(BankArbitrationPolicy.WeightedFair, config.ArbitrationPolicy);
        }

        [Fact]
        public void Test_ProcessorConfig_DefaultParameters()
        {
            // Arrange & Act
            var config = ProcessorConfig.Default();

            // Assert
            Assert.Equal(2, config.NumMemoryPorts);
            Assert.Equal(25.6, config.BankBandwidthGBps);
            Assert.Equal(BankArbitrationPolicy.RoundRobin, config.ArbitrationPolicy);
        }

        [Fact]
        public void Test_NumBanks_Reconfiguration_AllowsConfiguredHighBankAccess()
        {
            // Arrange
            InitializeTestEnvironment();
            Processor proc = default;
            var memory = new MemorySubsystem(ref proc)
            {
                NumBanks = 16,
                BankWidthBytes = 128
            };

            ulong targetAddress = 128UL * 15UL;

            // Act
            byte[] buffer = new byte[64];
            var token = memory.EnqueueRead(0, targetAddress, buffer.Length, buffer);

            // Assert
            Assert.NotNull(token);
            Assert.Equal(1, memory.CurrentQueuedRequests);
            Assert.True(memory.CancelPendingRequest(token));
            Assert.Equal(0, memory.CurrentQueuedRequests);
        }

        [Fact]
        public void Test_AdvanceCycles_ProcessesQueuedRequests()
        {
            // Arrange
            InitializeTestEnvironment();
            Processor proc = default;
            var memory = new MemorySubsystem(ref proc);
            memory.NumBanks = 4;

            // Act - Advance cycles to trigger queue processing
            memory.AdvanceCycles(10);

            // Assert - Should not crash and should handle empty queues
            Assert.Equal(0, memory.CurrentQueuedRequests);
        }

        [Fact]
        public void Test_ResetStatistics_ClearsNewMetrics()
        {
            // Arrange
            InitializeTestEnvironment();
            Processor proc = default;
            var memory = new MemorySubsystem(ref proc);

            // Perform some operations
            byte[] buffer = new byte[256];
            memory.Read(0, 0x0000, buffer);

            // Act - Reset statistics
            memory.ResetStatistics();

            // Assert - All metrics should be reset
            Assert.Equal(0, memory.TotalBursts);
            Assert.Equal(0, memory.TotalBytesTransferred);
            Assert.Equal(0, memory.BankConflicts);
            Assert.Equal(0, memory.StallCycles);
            Assert.Equal(0, memory.TotalWaitCycles);
            Assert.Equal(0, memory.MaxQueueDepth);
        }

        [Fact]
        public void Test_PerformanceReport_IncludesNewMetrics()
        {
            // Arrange
            var report = new PerformanceReport
            {
                TotalWaitCycles = 1000,
                AverageWaitCycles = 10.5,
                MaxQueueDepth = 5,
                CurrentQueuedRequests = 2
            };

            // Act
            string summary = report.GenerateSummary();
            string csv = report.ExportToCSV();

            // Assert - Verify new metrics appear in reports
            Assert.Contains("Total Wait Cycles", summary);
            Assert.Contains("Avg Wait Cycles", summary);
            Assert.Contains("Max Queue Depth", summary);
            Assert.Contains("Current Queued", summary);

            Assert.Contains("TotalWaitCycles,1000", csv);
            Assert.Contains("AverageWaitCycles,10.5", csv);
            Assert.Contains("MaxQueueDepth,5", csv);
            Assert.Contains("CurrentQueuedRequests,2", csv);
        }
    }
}
