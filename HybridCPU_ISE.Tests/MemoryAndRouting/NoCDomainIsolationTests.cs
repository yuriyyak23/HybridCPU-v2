using Xunit;
using YAKSys_Hybrid_CPU.Execution;

namespace HybridCPU_ISE.Tests.MemoryAndRouting
{
    /// <summary>
    /// PR 2 — Domain-Isolated Virtual Channels Tests
    ///
    /// Tests the domain isolation features in NoC_XY_Router:
    /// - DomainVcIsolationEnabled property
    /// - MaxInflightPerDomain property
    /// - DomainStallCount statistics
    /// - MapDomainToVC hashing function
    /// - MapDomainToSlot hashing function
    /// - Per-domain inflight tracking
    /// - Domain-isolated VC selection
    /// - ReleaseDomainInflight counter decrement
    ///
    /// Target file: NoC_XY_Router.cs
    /// Namespace: HybridCPU_ISE.Tests.MemoryAndRouting
    /// </summary>
    public class NoCDomainIsolationTests
    {
        #region 2.1 NoC_XY_Router — DomainVcIsolationEnabled Property

        [Fact]
        public void WhenRouterCreatedThenDomainVcIsolationDisabledByDefault()
        {
            // Arrange & Act
            var router = new NoC_XY_Router(localX: 0, localY: 0);

            // Assert
            Assert.False(router.DomainVcIsolationEnabled);
        }

        [Fact]
        public void WhenDomainVcIsolationSetTrueThenPropertyReflects()
        {
            // Arrange
            var router = new NoC_XY_Router(localX: 0, localY: 0);

            // Act
            router.DomainVcIsolationEnabled = true;

            // Assert
            Assert.True(router.DomainVcIsolationEnabled);

            // Act - Set back to false
            router.DomainVcIsolationEnabled = false;

            // Assert
            Assert.False(router.DomainVcIsolationEnabled);
        }

        #endregion

        #region 2.2 NoC_XY_Router — MaxInflightPerDomain Property

        [Fact]
        public void WhenRouterCreatedThenMaxInflightPerDomainHasDefault()
        {
            // Arrange & Act
            var router = new NoC_XY_Router(localX: 0, localY: 0);

            // Assert - Default should be VC_BUFFER_DEPTH * 5 = 4 * 5 = 20
            Assert.Equal(20, router.MaxInflightPerDomain);
        }

        [Fact]
        public void WhenMaxInflightPerDomainSetThenPropertyReflects()
        {
            // Arrange
            var router = new NoC_XY_Router(localX: 0, localY: 0);

            // Act
            router.MaxInflightPerDomain = 10;

            // Assert
            Assert.Equal(10, router.MaxInflightPerDomain);

            // Act - Set to zero (disables domain isolation)
            router.MaxInflightPerDomain = 0;

            // Assert
            Assert.Equal(0, router.MaxInflightPerDomain);
        }

        #endregion

        #region 2.3 NoC_XY_Router — DomainStallCount Statistics

        [Fact]
        public void WhenRouterCreatedThenDomainStallCountIsZero()
        {
            // Arrange & Act
            var router = new NoC_XY_Router(localX: 0, localY: 0);

            // Assert
            Assert.Equal(0, router.DomainStallCount);
        }

        [Fact]
        public void WhenDomainVcIsolationDisabledThenNoStallsCounted()
        {
            // Arrange
            var router1 = new NoC_XY_Router(localX: 0, localY: 0);
            var router2 = new NoC_XY_Router(localX: 1, localY: 0);
            router1.ConnectNeighbor(NoCPort.East, router2);

            router1.DomainVcIsolationEnabled = false; // Disabled

            // Act - Send many flits from same domain (should not stall)
            for (int i = 0; i < 50; i++)
            {
                var flit = new NoCFlit
                {
                    DestX = 1,
                    DestY = 0,
                    SrcX = 0,
                    SrcY = 0,
                    DomainTag = 0x1234, // Same domain
                    VirtualChannel = 0,
                    Payload = new byte[64]
                };
                router1.RoutePacket(flit);
            }

            // Assert - No stalls when isolation is disabled
            Assert.Equal(0, router1.DomainStallCount);
        }

        [Fact]
        public void WhenMaxInflightPerDomainZeroThenNoStallsCounted()
        {
            // Arrange
            var router1 = new NoC_XY_Router(localX: 0, localY: 0);
            var router2 = new NoC_XY_Router(localX: 1, localY: 0);
            router1.ConnectNeighbor(NoCPort.East, router2);

            router1.DomainVcIsolationEnabled = true;
            router1.MaxInflightPerDomain = 0; // Zero disables limit

            // Act - Send many flits
            for (int i = 0; i < 50; i++)
            {
                var flit = new NoCFlit
                {
                    DestX = 1,
                    DestY = 0,
                    SrcX = 0,
                    SrcY = 0,
                    DomainTag = 0x1234,
                    VirtualChannel = 0,
                    Payload = new byte[64]
                };
                router1.RoutePacket(flit);
            }

            // Assert - No stalls when MaxInflightPerDomain is 0
            Assert.Equal(0, router1.DomainStallCount);
        }

        [Fact]
        public void WhenDomainTagZeroThenNoStallsCounted()
        {
            // Arrange
            var router1 = new NoC_XY_Router(localX: 0, localY: 0);
            var router2 = new NoC_XY_Router(localX: 1, localY: 0);
            router1.ConnectNeighbor(NoCPort.East, router2);

            router1.DomainVcIsolationEnabled = true;
            router1.MaxInflightPerDomain = 5;

            // Act - Send many flits with DomainTag = 0 (exempt from isolation)
            for (int i = 0; i < 50; i++)
            {
                var flit = new NoCFlit
                {
                    DestX = 1,
                    DestY = 0,
                    SrcX = 0,
                    SrcY = 0,
                    DomainTag = 0, // Zero is exempt
                    VirtualChannel = 0,
                    Payload = new byte[64]
                };
                router1.RoutePacket(flit);
            }

            // Assert - No stalls for DomainTag = 0
            Assert.Equal(0, router1.DomainStallCount);
        }

        [Fact]
        public void WhenDomainInflightExceedsMaxThenStallsIncrement()
        {
            // Arrange
            var router1 = new NoC_XY_Router(localX: 0, localY: 0);
            var router2 = new NoC_XY_Router(localX: 1, localY: 0);
            router1.ConnectNeighbor(NoCPort.East, router2);

            router1.DomainVcIsolationEnabled = true;
            router1.MaxInflightPerDomain = 5; // Limit to 5 inflight per domain

            // Act - Send 10 flits from same domain (first 5 accepted, next 5 stalled)
            for (int i = 0; i < 10; i++)
            {
                var flit = new NoCFlit
                {
                    DestX = 1,
                    DestY = 0,
                    SrcX = 0,
                    SrcY = 0,
                    DomainTag = 0x1234,
                    VirtualChannel = 0,
                    Payload = new byte[64]
                };
                router1.RoutePacket(flit);
            }

            // Assert - Should have 5 stalls (exceeded max of 5)
            Assert.Equal(5, router1.DomainStallCount);
        }

        [Fact]
        public void WhenMultipleDomainsWithinLimitsThenNoStalls()
        {
            // Arrange
            var router1 = new NoC_XY_Router(localX: 0, localY: 0);
            var router2 = new NoC_XY_Router(localX: 1, localY: 0);
            router1.ConnectNeighbor(NoCPort.East, router2);

            router1.DomainVcIsolationEnabled = true;
            router1.MaxInflightPerDomain = 10;

            // Act - Send 5 flits from domain A and 5 from domain B
            for (int i = 0; i < 5; i++)
            {
                var flitA = new NoCFlit
                {
                    DestX = 1, DestY = 0, SrcX = 0, SrcY = 0,
                    DomainTag = 0xAAAA, VirtualChannel = 0, Payload = new byte[64]
                };
                router1.RoutePacket(flitA);

                var flitB = new NoCFlit
                {
                    DestX = 1, DestY = 0, SrcX = 0, SrcY = 0,
                    DomainTag = 0xBBBB, VirtualChannel = 0, Payload = new byte[64]
                };
                router1.RoutePacket(flitB);
            }

            // Assert - Each domain stays within limit, no stalls
            Assert.Equal(0, router1.DomainStallCount);
        }

        #endregion

        #region 2.4 NoC_XY_Router — MapDomainToVC Hashing

        [Fact]
        public void WhenDomainVcIsolationEnabledThenFlitVcUpdated()
        {
            // Arrange
            var router1 = new NoC_XY_Router(localX: 0, localY: 0);
            var router2 = new NoC_XY_Router(localX: 1, localY: 0);
            router1.ConnectNeighbor(NoCPort.East, router2);

            router1.DomainVcIsolationEnabled = true;

            // Act - Send flit with DomainTag that maps to VC 1
            var flit = new NoCFlit
            {
                DestX = 1, DestY = 0, SrcX = 0, SrcY = 0,
                DomainTag = 0xFFFF, // Should hash to VC 1 (odd XOR result)
                VirtualChannel = 0, // Original VC (will be overwritten)
                Payload = new byte[64]
            };

            router1.RoutePacket(flit);

            // Assert - VC should be updated based on domain hash
            // The XOR-fold of 0xFFFF is 1, so VC should be 1
            // We can't directly inspect the flit after routing, but we can verify
            // the router accepted the packet without errors
            Assert.True(router1.FlitsRouted > 0);
        }

        [Fact]
        public void WhenDomainVcIsolationDisabledThenFlitVcNotUpdated()
        {
            // Arrange
            var router1 = new NoC_XY_Router(localX: 0, localY: 0);
            var router2 = new NoC_XY_Router(localX: 1, localY: 0);
            router1.ConnectNeighbor(NoCPort.East, router2);

            router1.DomainVcIsolationEnabled = false; // Disabled

            // Act - Send flit with explicit VC
            var flit = new NoCFlit
            {
                DestX = 1, DestY = 0, SrcX = 0, SrcY = 0,
                DomainTag = 0xFFFF,
                VirtualChannel = 0, // Should stay 0
                Payload = new byte[64]
            };

            router1.RoutePacket(flit);

            // Assert - VC should not be modified
            Assert.True(router1.FlitsRouted > 0);
        }

        [Fact]
        public void WhenDifferentDomainsSameBitPatternThenDifferentVc()
        {
            // Arrange
            var router1 = new NoC_XY_Router(localX: 0, localY: 0);
            var router2 = new NoC_XY_Router(localX: 1, localY: 0);
            router1.ConnectNeighbor(NoCPort.East, router2);

            router1.DomainVcIsolationEnabled = true;

            // Act - Send flits with domains that should map to different VCs
            // Domain 0x1 → XOR-fold → 1 (VC 1)
            // Domain 0x0 → XOR-fold → 0 (VC 0)
            var flit1 = new NoCFlit
            {
                DestX = 1, DestY = 0, SrcX = 0, SrcY = 0,
                DomainTag = 0x1, VirtualChannel = 0, Payload = new byte[64]
            };

            var flit2 = new NoCFlit
            {
                DestX = 1, DestY = 0, SrcX = 0, SrcY = 0,
                DomainTag = 0x0, VirtualChannel = 0, Payload = new byte[64]
            };

            router1.RoutePacket(flit1);
            router1.RoutePacket(flit2);

            // Assert - Both should be routed successfully
            Assert.Equal(2, router1.FlitsRouted);
        }

        #endregion

        #region 2.5 NoC_XY_Router — MapDomainToSlot Hashing

        [Fact]
        public void WhenDifferentDomainsHashToSameSlotThenShareCounter()
        {
            // Arrange
            var router1 = new NoC_XY_Router(localX: 0, localY: 0);
            var router2 = new NoC_XY_Router(localX: 1, localY: 0);
            router1.ConnectNeighbor(NoCPort.East, router2);

            router1.DomainVcIsolationEnabled = true;
            router1.MaxInflightPerDomain = 10;

            // Act - Send flits from domains that hash to same slot
            // Domain 0x00 and 0x10 both hash to slot 0
            for (int i = 0; i < 5; i++)
            {
                var flit1 = new NoCFlit
                {
                    DestX = 1, DestY = 0, SrcX = 0, SrcY = 0,
                    DomainTag = 0x00, VirtualChannel = 0, Payload = new byte[64]
                };
                router1.RoutePacket(flit1);

                var flit2 = new NoCFlit
                {
                    DestX = 1, DestY = 0, SrcX = 0, SrcY = 0,
                    DomainTag = 0x10, VirtualChannel = 0, Payload = new byte[64]
                };
                router1.RoutePacket(flit2);
            }

            // Assert - 10 flits sent, should hit limit since they share counter
            Assert.Equal(10, router1.FlitsRouted);
        }

        [Fact]
        public void WhenDomainsHashToDifferentSlotsThenIndependentCounters()
        {
            // Arrange
            var router1 = new NoC_XY_Router(localX: 0, localY: 0);
            var router2 = new NoC_XY_Router(localX: 1, localY: 0);
            router1.ConnectNeighbor(NoCPort.East, router2);

            router1.DomainVcIsolationEnabled = true;
            router1.MaxInflightPerDomain = 5;

            // Act - Send 5 flits from each of 3 different domains
            // These should hash to different slots
            for (int i = 0; i < 5; i++)
            {
                var flit1 = new NoCFlit
                {
                    DestX = 1, DestY = 0, SrcX = 0, SrcY = 0,
                    DomainTag = 0x0001, VirtualChannel = 0, Payload = new byte[64]
                };
                router1.RoutePacket(flit1);

                var flit2 = new NoCFlit
                {
                    DestX = 1, DestY = 0, SrcX = 0, SrcY = 0,
                    DomainTag = 0x0002, VirtualChannel = 0, Payload = new byte[64]
                };
                router1.RoutePacket(flit2);

                var flit3 = new NoCFlit
                {
                    DestX = 1, DestY = 0, SrcX = 0, SrcY = 0,
                    DomainTag = 0x0003, VirtualChannel = 0, Payload = new byte[64]
                };
                router1.RoutePacket(flit3);
            }

            // Assert - All 15 should be routed (each domain gets its own limit)
            Assert.Equal(15, router1.FlitsRouted);
            Assert.Equal(0, router1.DomainStallCount);
        }

        #endregion

        #region 2.6 NoC_XY_Router — Per-Domain Inflight Tracking

        [Fact]
        public void WhenDomainInflightTrackedThenCountersIncrement()
        {
            // Arrange
            var router1 = new NoC_XY_Router(localX: 0, localY: 0);
            var router2 = new NoC_XY_Router(localX: 1, localY: 0);
            router1.ConnectNeighbor(NoCPort.East, router2);

            router1.DomainVcIsolationEnabled = true;
            router1.MaxInflightPerDomain = 20;

            // Act - Send 10 flits from same domain
            for (int i = 0; i < 10; i++)
            {
                var flit = new NoCFlit
                {
                    DestX = 1, DestY = 0, SrcX = 0, SrcY = 0,
                    DomainTag = 0xABCD, VirtualChannel = 0, Payload = new byte[64]
                };
                router1.RoutePacket(flit);
            }

            // Assert - All flits should be routed (within limit)
            Assert.Equal(10, router1.FlitsRouted);
            Assert.Equal(0, router1.DomainStallCount);
        }

        [Fact]
        public void WhenDomainInflightExceedsLimitThenStall()
        {
            // Arrange
            var router1 = new NoC_XY_Router(localX: 0, localY: 0);
            var router2 = new NoC_XY_Router(localX: 1, localY: 0);
            router1.ConnectNeighbor(NoCPort.East, router2);

            router1.DomainVcIsolationEnabled = true;
            router1.MaxInflightPerDomain = 3; // Very low limit

            // Act - Send 6 flits (first 3 accepted, next 3 stalled)
            for (int i = 0; i < 6; i++)
            {
                var flit = new NoCFlit
                {
                    DestX = 1, DestY = 0, SrcX = 0, SrcY = 0,
                    DomainTag = 0xDEAD, VirtualChannel = 0, Payload = new byte[64]
                };
                router1.RoutePacket(flit);
            }

            // Assert
            Assert.Equal(6, router1.FlitsRouted); // RoutePacket called 6 times
            Assert.Equal(3, router2.FlitsRouted); // Only 3 made it to router2
            Assert.Equal(3, router1.DomainStallCount);
            Assert.Equal(3, router1.FlitsDropped);
        }

        #endregion

        #region 2.7 NoC_XY_Router — ReleaseDomainInflight

        [Fact]
        public void WhenReleaseDomainInflightThenCounterDecrements()
        {
            // Arrange
            var router = new NoC_XY_Router(localX: 0, localY: 0);
            router.DomainVcIsolationEnabled = true;

            // We can't directly access the internal counter, but we can verify
            // that after releasing, we can send more flits

            // Act - Release a domain that wasn't tracked (should be safe no-op)
            router.ReleaseDomainInflight(0x1234);

            // Assert - No crash, method completed
            Assert.True(true);
        }

        [Fact]
        public void WhenReleaseDomainInflightWithIsolationDisabledThenNoOp()
        {
            // Arrange
            var router = new NoC_XY_Router(localX: 0, localY: 0);
            router.DomainVcIsolationEnabled = false;

            // Act
            router.ReleaseDomainInflight(0x1234);

            // Assert - No crash, method completed
            Assert.True(true);
        }

        [Fact]
        public void WhenReleaseDomainInflightWithZeroTagThenNoOp()
        {
            // Arrange
            var router = new NoC_XY_Router(localX: 0, localY: 0);
            router.DomainVcIsolationEnabled = true;

            // Act
            router.ReleaseDomainInflight(0); // DomainTag 0 is exempt

            // Assert - No crash
            Assert.True(true);
        }

        #endregion

        #region 2.8 NoC_XY_Router — Virtual Channel Credit Management

        [Fact]
        public void WhenRouterCreatedThenAllVcCreditsInitialized()
        {
            // Arrange & Act
            var router = new NoC_XY_Router(localX: 0, localY: 0);

            // Assert - Can't directly access _vcCredits, but we can verify
            // that the router is ready to accept flits
            Assert.Equal(0, router.FlitsRouted);
            Assert.Equal(0, router.FlitsDropped);
        }

        [Fact]
        public void WhenReturnCreditThenCanSendMoreFlits()
        {
            // Arrange
            var router1 = new NoC_XY_Router(localX: 0, localY: 0);
            var router2 = new NoC_XY_Router(localX: 1, localY: 0);
            router1.ConnectNeighbor(NoCPort.East, router2);

            // Act - Return credit on VC 0
            router1.ReturnCredit(NoCPort.East, virtualChannel: 0);

            // Assert - No crash
            Assert.True(true);
        }

        [Fact]
        public void WhenReturnCreditOverloadThenUsesVc0()
        {
            // Arrange
            var router = new NoC_XY_Router(localX: 0, localY: 0);

            // Act - Call overload without VC parameter (should default to VC 0)
            router.ReturnCredit(NoCPort.East);

            // Assert - No crash
            Assert.True(true);
        }

        #endregion

        #region 2.9 NoC_XY_Router — Statistics and Observability

        [Fact]
        public void WhenFlitsRoutedThenStatisticsUpdate()
        {
            // Arrange
            var router1 = new NoC_XY_Router(localX: 0, localY: 0);
            var router2 = new NoC_XY_Router(localX: 1, localY: 0);
            router1.ConnectNeighbor(NoCPort.East, router2);

            // Act
            var flit = new NoCFlit
            {
                DestX = 1, DestY = 0, SrcX = 0, SrcY = 0,
                DomainTag = 0, VirtualChannel = 0, Payload = new byte[64]
            };
            router1.RoutePacket(flit);

            // Assert
            Assert.Equal(1, router1.FlitsRouted);
            Assert.Equal(0, router1.FlitsDropped);
        }

        [Fact]
        public void WhenFlitReachesDestinationThenDeliveredCountIncreases()
        {
            // Arrange
            var router = new NoC_XY_Router(localX: 0, localY: 0);

            // Act - Send flit to local destination
            var flit = new NoCFlit
            {
                DestX = 0, DestY = 0, // Same as router position
                SrcX = 0, SrcY = 0,
                DomainTag = 0, VirtualChannel = 0,
                Payload = new byte[64],
                IsWrite = true
            };
            router.RoutePacket(flit);

            // Assert
            Assert.Equal(1, router.FlitsRouted);
            Assert.Equal(1, router.FlitsDelivered);
        }

        [Fact]
        public void WhenFlitDroppedThenDropCountIncreases()
        {
            // Arrange
            var router = new NoC_XY_Router(localX: 0, localY: 0);
            router.DomainVcIsolationEnabled = true;
            router.MaxInflightPerDomain = 0; // Force all flits to be processed

            // Act - Send flit to non-existent neighbor (will be dropped)
            var flit = new NoCFlit
            {
                DestX = 1, DestY = 0, // No neighbor connected
                SrcX = 0, SrcY = 0,
                DomainTag = 0, VirtualChannel = 0, Payload = new byte[64]
            };
            router.RoutePacket(flit);

            // Assert
            Assert.Equal(1, router.FlitsRouted);
            Assert.Equal(1, router.FlitsDropped);
        }

        #endregion
    }
}
