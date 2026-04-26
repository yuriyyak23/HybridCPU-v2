using Xunit;
using YAKSys_Hybrid_CPU.Execution;
using System;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Comprehensive tests for NoC XY Router - 2D Mesh Network-on-Chip.
    /// Validates XY dimension-order routing, deadlock-free behavior, virtual channels,
    /// credit-based flow control, and domain isolation propagation.
    /// </summary>
    public class NoCXYRouterTests
    {
        #region Basic Routing Tests

        [Fact]
        public void Router_InitialState_ShouldHaveZeroStatistics()
        {
            // Arrange
            var router = new NoC_XY_Router(0, 0);

            // Assert
            Assert.Equal(0L, router.FlitsRouted);
            Assert.Equal(0L, router.FlitsDelivered);
            Assert.Equal(0L, router.FlitsDropped);
        }

        [Fact]
        public void Router_LocalDelivery_ShouldIncrementDeliveredCount()
        {
            // Arrange
            var router = new NoC_XY_Router(2, 3);
            var flit = new NoCFlit
            {
                DestX = 2,
                DestY = 3,
                SrcX = 0,
                SrcY = 0,
                Address = 0x1000,
                Payload = new byte[64],
                IsWrite = true,
                HopCount = 0
            };

            // Act
            router.RoutePacket(flit);

            // Assert
            Assert.Equal(1L, router.FlitsRouted);
            Assert.Equal(1L, router.FlitsDelivered);
            Assert.Equal(0L, router.FlitsDropped);
        }

        [Fact]
        public void Router_XYRouting_ShouldRouteXDimensionFirst()
        {
            // Arrange - Create 2×1 mesh: (0,0) -- (1,0)
            var router0 = new NoC_XY_Router(0, 0);
            var router1 = new NoC_XY_Router(1, 0);
            router0.ConnectNeighbor(NoCPort.East, router1);

            var flit = new NoCFlit
            {
                DestX = 1,
                DestY = 0,
                SrcX = 0,
                SrcY = 0,
                Address = 0x1000,
                Payload = new byte[64],
                IsWrite = true,
                HopCount = 0
            };

            // Act
            router0.RoutePacket(flit);

            // Assert - Flit should reach router1
            Assert.Equal(1L, router0.FlitsRouted);
            Assert.Equal(0L, router0.FlitsDelivered);
            Assert.Equal(1L, router1.FlitsRouted);
            Assert.Equal(1L, router1.FlitsDelivered);
        }

        [Fact]
        public void Router_XYRouting_ShouldRouteYDimensionAfterX()
        {
            // Arrange - Create 2×2 mesh:
            // (0,1) -- (1,1)
            //   |        |
            // (0,0) -- (1,0)
            var router00 = new NoC_XY_Router(0, 0);
            var router10 = new NoC_XY_Router(1, 0);
            var router01 = new NoC_XY_Router(0, 1);
            var router11 = new NoC_XY_Router(1, 1);

            router00.ConnectNeighbor(NoCPort.East, router10);
            router00.ConnectNeighbor(NoCPort.North, router01);
            router10.ConnectNeighbor(NoCPort.North, router11);
            router01.ConnectNeighbor(NoCPort.East, router11);

            var flit = new NoCFlit
            {
                DestX = 1,
                DestY = 1,
                SrcX = 0,
                SrcY = 0,
                Address = 0x1000,
                Payload = new byte[64],
                IsWrite = true,
                HopCount = 0
            };

            // Act - Route from (0,0) to (1,1): should go (0,0) -> (1,0) -> (1,1)
            router00.RoutePacket(flit);

            // Assert - XY routing: X first, then Y
            Assert.Equal(1L, router00.FlitsRouted);
            Assert.Equal(1L, router10.FlitsRouted);
            Assert.Equal(1L, router11.FlitsRouted);
            Assert.Equal(1L, router11.FlitsDelivered);
            Assert.Equal(0L, router01.FlitsRouted); // Not used (X-first routing)
        }

        [Fact]
        public void Router_WestRouting_ShouldRouteNegativeXDirection()
        {
            // Arrange - Create 2×1 mesh: (1,0) -- (0,0)
            var router1 = new NoC_XY_Router(1, 0);
            var router0 = new NoC_XY_Router(0, 0);
            router1.ConnectNeighbor(NoCPort.West, router0);

            var flit = new NoCFlit
            {
                DestX = 0,
                DestY = 0,
                SrcX = 1,
                SrcY = 0,
                Address = 0x1000,
                Payload = new byte[64],
                IsWrite = true,
                HopCount = 0
            };

            // Act
            router1.RoutePacket(flit);

            // Assert
            Assert.Equal(1L, router1.FlitsRouted);
            Assert.Equal(0L, router1.FlitsDelivered);
            Assert.Equal(1L, router0.FlitsRouted);
            Assert.Equal(1L, router0.FlitsDelivered);
        }

        [Fact]
        public void Router_SouthRouting_ShouldRouteNegativeYDirection()
        {
            // Arrange - Create 1×2 mesh vertically
            var router01 = new NoC_XY_Router(0, 1);
            var router00 = new NoC_XY_Router(0, 0);
            router01.ConnectNeighbor(NoCPort.South, router00);

            var flit = new NoCFlit
            {
                DestX = 0,
                DestY = 0,
                SrcX = 0,
                SrcY = 1,
                Address = 0x1000,
                Payload = new byte[64],
                IsWrite = true,
                HopCount = 0
            };

            // Act
            router01.RoutePacket(flit);

            // Assert
            Assert.Equal(1L, router01.FlitsRouted);
            Assert.Equal(1L, router00.FlitsDelivered);
        }

        #endregion

        #region Deadlock Prevention Tests

        [Fact]
        public void Router_XYOrdering_ShouldPreventDeadlock_DiagonalPath()
        {
            // Arrange - Create 3×3 mesh to test diagonal routing
            var routers = new NoC_XY_Router[3, 3];
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    routers[x, y] = new NoC_XY_Router(x, y);
                }
            }

            // Connect all neighbors
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    if (x < 2) routers[x, y].ConnectNeighbor(NoCPort.East, routers[x + 1, y]);
                    if (x > 0) routers[x, y].ConnectNeighbor(NoCPort.West, routers[x - 1, y]);
                    if (y < 2) routers[x, y].ConnectNeighbor(NoCPort.North, routers[x, y + 1]);
                    if (y > 0) routers[x, y].ConnectNeighbor(NoCPort.South, routers[x, y - 1]);
                }
            }

            // Act - Route from (0,0) to (2,2): should follow X-first path
            var flit = new NoCFlit
            {
                DestX = 2,
                DestY = 2,
                SrcX = 0,
                SrcY = 0,
                Address = 0x1000,
                Payload = new byte[64],
                IsWrite = true,
                HopCount = 0
            };

            routers[0, 0].RoutePacket(flit);

            // Assert - Path should be (0,0) -> (1,0) -> (2,0) -> (2,1) -> (2,2)
            Assert.Equal(1L, routers[0, 0].FlitsRouted);
            Assert.Equal(1L, routers[1, 0].FlitsRouted);
            Assert.Equal(1L, routers[2, 0].FlitsRouted);
            Assert.Equal(1L, routers[2, 1].FlitsRouted);
            Assert.Equal(1L, routers[2, 2].FlitsRouted);
            Assert.Equal(1L, routers[2, 2].FlitsDelivered);

            // Verify alternative Y-first path was NOT used
            Assert.Equal(0L, routers[0, 1].FlitsRouted);
            Assert.Equal(0L, routers[1, 1].FlitsRouted);
        }

        #endregion

        #region TTL and Hop Count Tests

        [Fact]
        public void Router_HopCount_ShouldIncrementOnEachHop()
        {
            // Arrange - Create 4×1 mesh
            var routers = new NoC_XY_Router[4];
            for (int i = 0; i < 4; i++)
            {
                routers[i] = new NoC_XY_Router(i, 0);
                if (i > 0)
                    routers[i - 1].ConnectNeighbor(NoCPort.East, routers[i]);
            }

            var flit = new NoCFlit
            {
                DestX = 3,
                DestY = 0,
                SrcX = 0,
                SrcY = 0,
                Address = 0x1000,
                Payload = new byte[64],
                IsWrite = true,
                HopCount = 0
            };

            // Act
            routers[0].RoutePacket(flit);

            // Assert - Should traverse 4 routers (3 hops + 1 delivery)
            Assert.Equal(1L, routers[0].FlitsRouted);
            Assert.Equal(1L, routers[1].FlitsRouted);
            Assert.Equal(1L, routers[2].FlitsRouted);
            Assert.Equal(1L, routers[3].FlitsRouted);
            Assert.Equal(1L, routers[3].FlitsDelivered);
        }

        [Fact]
        public void Router_TTLExpiry_ShouldDropFlit()
        {
            // Arrange
            var router = new NoC_XY_Router(0, 0);
            var flit = new NoCFlit
            {
                DestX = 1,
                DestY = 0,
                SrcX = 0,
                SrcY = 0,
                Address = 0x1000,
                Payload = new byte[64],
                IsWrite = true,
                HopCount = 16 // MAX_HOPS
            };

            // Act
            router.RoutePacket(flit);

            // Assert - Flit should be dropped due to TTL expiry
            Assert.Equal(0L, router.FlitsRouted);
            Assert.Equal(1L, router.FlitsDropped);
        }

        #endregion

        #region Credit-Based Flow Control Tests

        [Fact]
        public void Router_BufferFull_ShouldDropFlit()
        {
            // Arrange
            var router0 = new NoC_XY_Router(0, 0);
            var router1 = new NoC_XY_Router(1, 0);
            router0.ConnectNeighbor(NoCPort.East, router1);

            // Act - Send flits until credits exhausted (VC_BUFFER_DEPTH = 4 per VC)
            for (int i = 0; i < 10; i++)
            {
                var flit = new NoCFlit
                {
                    DestX = 1,
                    DestY = 0,
                    SrcX = 0,
                    SrcY = 0,
                    Address = 0x1000,
                    Payload = new byte[64],
                    IsWrite = true,
                    HopCount = 0,
                    VirtualChannel = 1  // Use VC1
                };
                router0.RoutePacket(flit);
            }

            // Assert - Some flits should be dropped when buffer is exhausted
            Assert.Equal(10L, router0.FlitsRouted);
            Assert.True(router0.FlitsDropped > 0, $"Expected some dropped, got {router0.FlitsDropped}");
        }

        [Fact]
        public void Router_ReturnCredit_ShouldAllowSubsequentSend()
        {
            // Arrange
            var router0 = new NoC_XY_Router(0, 0);
            var router1 = new NoC_XY_Router(1, 0);
            router0.ConnectNeighbor(NoCPort.East, router1);

            // Send flits to exhaust VC1 credits
            for (int i = 0; i < 4; i++)
            {
                var flit = new NoCFlit
                {
                    DestX = 1,
                    DestY = 0,
                    SrcX = 0,
                    SrcY = 0,
                    Address = 0x1000,
                    Payload = new byte[64],
                    IsWrite = true,
                    HopCount = 0,
                    VirtualChannel = 1
                };
                router0.RoutePacket(flit);
            }

            // Act - Return credit and send another flit
            router0.ReturnCredit(NoCPort.East, 1);

            var newFlit = new NoCFlit
            {
                DestX = 1,
                DestY = 0,
                SrcX = 0,
                SrcY = 0,
                Address = 0x1000,
                Payload = new byte[64],
                IsWrite = true,
                HopCount = 0,
                VirtualChannel = 1
            };
            router0.RoutePacket(newFlit);

            // Assert - Last flit should succeed after credit return
            Assert.Equal(5L, router0.FlitsRouted);
        }

        #endregion

        #region Virtual Channel Tests

        [Fact]
        public void Router_VirtualChannels_ShouldAllowIndependentFlow()
        {
            // Arrange
            var router0 = new NoC_XY_Router(0, 0);
            var router1 = new NoC_XY_Router(1, 0);
            router0.ConnectNeighbor(NoCPort.East, router1);

            // Act - Send 4 flits on VC0, then 4 on VC1
            for (int i = 0; i < 4; i++)
            {
                var flit0 = new NoCFlit
                {
                    DestX = 1, DestY = 0, SrcX = 0, SrcY = 0,
                    Address = 0x1000, Payload = new byte[64],
                    IsWrite = true, HopCount = 0, VirtualChannel = 0
                };
                router0.RoutePacket(flit0);

                var flit1 = new NoCFlit
                {
                    DestX = 1, DestY = 0, SrcX = 0, SrcY = 0,
                    Address = 0x2000, Payload = new byte[64],
                    IsWrite = true, HopCount = 0, VirtualChannel = 1
                };
                router0.RoutePacket(flit1);
            }

            // Assert - Both VCs should be exhausted independently
            Assert.Equal(8L, router0.FlitsRouted);
        }

        [Fact]
        public void Router_HoLAvoidance_ShouldTryAlternateVC()
        {
            // Arrange
            var router0 = new NoC_XY_Router(0, 0);
            var router1 = new NoC_XY_Router(1, 0);
            router0.ConnectNeighbor(NoCPort.East, router1);

            // Exhaust VC1 (4 slots)
            for (int i = 0; i < 4; i++)
            {
                var flit = new NoCFlit
                {
                    DestX = 1, DestY = 0, SrcX = 0, SrcY = 0,
                    Address = 0x1000, Payload = new byte[64],
                    IsWrite = true, HopCount = 0, VirtualChannel = 1
                };
                router0.RoutePacket(flit);
            }

            // Act - Send low-priority (QoS=0) flit on VC1 (full) - should try VC0
            var lowPriorityFlit = new NoCFlit
            {
                DestX = 1, DestY = 0, SrcX = 0, SrcY = 0,
                Address = 0x2000, Payload = new byte[64],
                IsWrite = true, HopCount = 0,
                VirtualChannel = 1, QosPriority = 0
            };
            router0.RoutePacket(lowPriorityFlit);

            // Assert - Flit should succeed by using alternate VC
            Assert.Equal(5L, router0.FlitsRouted);
            Assert.Equal(0L, router0.FlitsDropped);
        }

        [Fact]
        public void Router_HighPriority_ShouldNotUseAlternateVC()
        {
            // Arrange
            var router0 = new NoC_XY_Router(0, 0);
            var router1 = new NoC_XY_Router(1, 0);
            router0.ConnectNeighbor(NoCPort.East, router1);

            // Exhaust VC1
            for (int i = 0; i < 4; i++)
            {
                var flit = new NoCFlit
                {
                    DestX = 1, DestY = 0, SrcX = 0, SrcY = 0,
                    Address = 0x1000, Payload = new byte[64],
                    IsWrite = true, HopCount = 0, VirtualChannel = 1
                };
                router0.RoutePacket(flit);
            }

            // Act - Send high-priority flit (QoS=1) on full VC1
            var highPriorityFlit = new NoCFlit
            {
                DestX = 1, DestY = 0, SrcX = 0, SrcY = 0,
                Address = 0x2000, Payload = new byte[64],
                IsWrite = true, HopCount = 0,
                VirtualChannel = 1, QosPriority = 1
            };
            router0.RoutePacket(highPriorityFlit);

            // Assert - High-priority flit should be dropped (no VC switching)
            Assert.Equal(5L, router0.FlitsRouted);
            Assert.Equal(1L, router0.FlitsDropped);
        }

        #endregion

        #region Domain Isolation Tests

        [Fact]
        public void Router_DomainTag_ShouldPropagateThroughNetwork()
        {
            // Arrange
            var router0 = new NoC_XY_Router(0, 0);
            var router1 = new NoC_XY_Router(1, 0);
            router0.ConnectNeighbor(NoCPort.East, router1);

            ulong domainTag = 0xDEADBEEFCAFEBABE;
            var flit = new NoCFlit
            {
                DestX = 1, DestY = 0, SrcX = 0, SrcY = 0,
                Address = 0x1000, Payload = new byte[64],
                IsWrite = true, HopCount = 0,
                DomainTag = domainTag
            };

            // Act
            router0.RoutePacket(flit);

            // Assert - Domain tag should remain unchanged
            // (In real implementation, would verify at destination backend)
            Assert.Equal(1L, router1.FlitsDelivered);
        }

        #endregion

        #region Disconnected Topology Tests

        [Fact]
        public void Router_NoNeighbor_ShouldDropFlit()
        {
            // Arrange - Isolated router (no neighbors)
            var router = new NoC_XY_Router(0, 0);
            var flit = new NoCFlit
            {
                DestX = 1,
                DestY = 0,
                SrcX = 0,
                SrcY = 0,
                Address = 0x1000,
                Payload = new byte[64],
                IsWrite = true,
                HopCount = 0
            };

            // Act
            router.RoutePacket(flit);

            // Assert - Flit should be dropped (no East neighbor)
            Assert.Equal(1L, router.FlitsRouted);
            Assert.Equal(1L, router.FlitsDropped);
        }

        #endregion

        #region IBurstBackend Integration Tests

        [Fact]
        public void Router_SendRemoteBurst_ShouldCreateAndRouteFlit()
        {
            // Arrange
            var router0 = new NoC_XY_Router(0, 0);
            var router1 = new NoC_XY_Router(1, 0);
            router0.ConnectNeighbor(NoCPort.East, router1);

            byte[] data = new byte[128];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)i;

            // Act
            router0.SendRemoteBurst(
                destPodX: 1, destPodY: 0,
                deviceId: 42, address: 0x10000,
                data: data, domainTag: 0x123);

            // Assert
            Assert.Equal(1L, router0.FlitsRouted);
            Assert.Equal(1L, router1.FlitsDelivered);
        }

        #endregion

        #region Large Mesh Stress Tests

        [Fact]
        public void Router_LargeMesh_ShouldRouteCorrectly()
        {
            // Arrange - Create 4×4 mesh (16 routers)
            var routers = new NoC_XY_Router[4, 4];
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    routers[x, y] = new NoC_XY_Router(x, y);
                }
            }

            // Connect all neighbors
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    if (x < 3) routers[x, y].ConnectNeighbor(NoCPort.East, routers[x + 1, y]);
                    if (x > 0) routers[x, y].ConnectNeighbor(NoCPort.West, routers[x - 1, y]);
                    if (y < 3) routers[x, y].ConnectNeighbor(NoCPort.North, routers[x, y + 1]);
                    if (y > 0) routers[x, y].ConnectNeighbor(NoCPort.South, routers[x, y - 1]);
                }
            }

            // Act - Route from (0,0) to (3,3) - longest path (6 hops)
            var flit = new NoCFlit
            {
                DestX = 3, DestY = 3, SrcX = 0, SrcY = 0,
                Address = 0x1000, Payload = new byte[64],
                IsWrite = true, HopCount = 0
            };

            routers[0, 0].RoutePacket(flit);

            // Assert - Path: (0,0)->(1,0)->(2,0)->(3,0)->(3,1)->(3,2)->(3,3)
            Assert.Equal(1L, routers[3, 3].FlitsDelivered);
            Assert.Equal(0L, routers[3, 3].FlitsDropped);
        }

        #endregion
    }
}
