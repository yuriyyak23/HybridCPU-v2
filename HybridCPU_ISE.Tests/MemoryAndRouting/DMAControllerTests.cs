using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;
using System;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Unit tests for DMA Controller async functionality.
    /// Tests async completion, callbacks, events, and channel management.
    /// </summary>
    public class DMAControllerTests
    {
        private void InitializeTestEnvironment()
        {
            // Initialize IOMMU
            IOMMU.Initialize();
            IOMMU.RegisterDevice(0);
        }

        [Fact]
        public void Test_DMAController_Initialize()
        {
            // Arrange
            InitializeTestEnvironment();
            Processor proc = default;

            // Act
            var dma = new DMAController(ref proc);

            // Assert
            Assert.NotNull(dma);
            for (byte ch = 0; ch < 8; ch++)
            {
                Assert.Equal(DMAController.ChannelState.Idle, dma.GetChannelState(ch));
            }
        }

        [Fact]
        public void Test_ConfigureTransfer_ValidDescriptor_ReturnsTrue()
        {
            // Arrange
            InitializeTestEnvironment();
            Processor proc = default;
            var dma = new DMAController(ref proc);

            var descriptor = new DMAController.TransferDescriptor
            {
                SourceAddress = 0x1000,
                DestAddress = 0x2000,
                TransferSize = 1024,
                ElementSize = 1,
                ChannelID = 0,
                Priority = 128
            };

            // Act
            bool result = dma.ConfigureTransfer(descriptor);

            // Assert
            Assert.True(result);
            Assert.Equal(DMAController.ChannelState.Configured, dma.GetChannelState(0));
        }

        [Fact]
        public void Test_ConfigureTransfer_ChannelBusy_ReturnsFalse()
        {
            // Arrange
            InitializeTestEnvironment();
            Processor proc = default;
            var dma = new DMAController(ref proc);

            var descriptor = new DMAController.TransferDescriptor
            {
                SourceAddress = 0x1000,
                DestAddress = 0x2000,
                TransferSize = 1024,
                ElementSize = 1,
                ChannelID = 0,
                Priority = 128
            };

            // Configure first time
            dma.ConfigureTransfer(descriptor);
            dma.StartTransfer(0);

            // Act - try to configure again while active
            bool result = dma.ConfigureTransfer(descriptor);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Test_StartTransfer_ConfiguredChannel_ReturnsTrue()
        {
            // Arrange
            InitializeTestEnvironment();
            Processor proc = default;
            var dma = new DMAController(ref proc);

            var descriptor = new DMAController.TransferDescriptor
            {
                SourceAddress = 0x1000,
                DestAddress = 0x2000,
                TransferSize = 1024,
                ElementSize = 1,
                ChannelID = 0,
                Priority = 128
            };

            dma.ConfigureTransfer(descriptor);

            // Act
            bool result = dma.StartTransfer(0);

            // Assert
            Assert.True(result);
            Assert.Equal(DMAController.ChannelState.Active, dma.GetChannelState(0));
        }

        [Fact]
        public void Test_PauseTransfer_ActiveChannel_ReturnsTrue()
        {
            // Arrange
            InitializeTestEnvironment();
            Processor proc = default;
            var dma = new DMAController(ref proc);

            var descriptor = new DMAController.TransferDescriptor
            {
                SourceAddress = 0x1000,
                DestAddress = 0x2000,
                TransferSize = 4096, // Large transfer
                ElementSize = 1,
                ChannelID = 0,
                Priority = 128,
                UseIOMMU = false
            };

            dma.ConfigureTransfer(descriptor);
            dma.StartTransfer(0);

            // Act
            bool result = dma.PauseTransfer(0);

            // Assert
            Assert.True(result);
            Assert.Equal(DMAController.ChannelState.Paused, dma.GetChannelState(0));
        }

        [Fact]
        public void Test_ResumeTransfer_PausedChannel_ReturnsTrue()
        {
            // Arrange
            InitializeTestEnvironment();
            Processor proc = default;
            var dma = new DMAController(ref proc);

            var descriptor = new DMAController.TransferDescriptor
            {
                SourceAddress = 0x1000,
                DestAddress = 0x2000,
                TransferSize = 4096,
                ElementSize = 1,
                ChannelID = 0,
                Priority = 128,
                UseIOMMU = false
            };

            dma.ConfigureTransfer(descriptor);
            dma.StartTransfer(0);
            dma.PauseTransfer(0);

            // Act
            bool result = dma.ResumeTransfer(0);

            // Assert
            Assert.True(result);
            Assert.Equal(DMAController.ChannelState.Active, dma.GetChannelState(0));
        }

        [Fact]
        public void Test_CancelTransfer_ActiveChannel_ReturnsTrue()
        {
            // Arrange
            InitializeTestEnvironment();
            Processor proc = default;
            var dma = new DMAController(ref proc);

            var descriptor = new DMAController.TransferDescriptor
            {
                SourceAddress = 0x1000,
                DestAddress = 0x2000,
                TransferSize = 4096,
                ElementSize = 1,
                ChannelID = 0,
                Priority = 128,
                UseIOMMU = false
            };

            dma.ConfigureTransfer(descriptor);
            dma.StartTransfer(0);

            // Act
            bool result = dma.CancelTransfer(0);

            // Assert
            Assert.True(result);
            Assert.Equal(DMAController.ChannelState.Idle, dma.GetChannelState(0));
        }

        [Fact]
        public void Test_ResetChannel_ClearsState()
        {
            // Arrange
            InitializeTestEnvironment();
            Processor proc = default;
            var dma = new DMAController(ref proc);

            var descriptor = new DMAController.TransferDescriptor
            {
                SourceAddress = 0x1000,
                DestAddress = 0x2000,
                TransferSize = 256,
                ElementSize = 1,
                ChannelID = 0,
                Priority = 128
            };

            dma.ConfigureTransfer(descriptor);
            dma.StartTransfer(0);

            // Act
            dma.ResetChannel(0);

            // Assert
            Assert.Equal(DMAController.ChannelState.Idle, dma.GetChannelState(0));
            var (transferred, total) = dma.GetChannelProgress(0);
            Assert.Equal(0u, transferred);
            Assert.Equal(0u, total);
        }
    }
}
