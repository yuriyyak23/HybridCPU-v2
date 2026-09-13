using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests
{
    public class DomainIsolationStressTests
    {
        [Fact]
        public void WhenCrossDomainPressureAppliedThenIsolationBlocksRemainExplicit()
        {
            var scheduler = new MicroOpScheduler();

            for (int cycle = 0; cycle < 4; cycle++)
            {
                var bundle = new MicroOp[8];
                bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);

                scheduler.Nominate(1, MicroOpTestHelper.CreateLoad(1, 8, 0x1000 + (ulong)(cycle * 64), domainTag: 0x04));
                scheduler.Nominate(2, MicroOpTestHelper.CreateLoad(1, 9, 0x2000 + (ulong)(cycle * 64), domainTag: 0x00));
                scheduler.Nominate(3, MicroOpTestHelper.CreateLoad(1, 10, 0x3000 + (ulong)(cycle * 64), domainTag: 0x02));

                scheduler.PackBundle(
                    bundle,
                    currentThreadId: 0,
                    stealEnabled: true,
                    stealMask: 0xFF,
                    domainCertificate: 0x02);
            }

            SchedulerPhaseMetrics metrics = scheduler.GetPhaseMetrics();

            Assert.True(metrics.DomainIsolationProbeAttempts >= 12);
            Assert.True(metrics.DomainIsolationBlockedAttempts >= 8);
            Assert.True(metrics.DomainIsolationCrossDomainBlocks >= 4);
            Assert.True(metrics.DomainIsolationKernelToUserBlocks >= 4);
        }

        [Fact]
        public void WhenProbeClassificationRequestedThenVerifierSeparatesCrossDomainFromKernelBlocks()
        {
            var verifier = new SafetyVerifier();
            DomainIsolationProbeResult crossDomain = verifier.EvaluateDomainIsolationProbe(
                MicroOpTestHelper.CreateLoad(1, 8, 0x1000, domainTag: 0x04),
                podDomainCert: 0x02);
            DomainIsolationProbeResult kernelToUser = verifier.EvaluateDomainIsolationProbe(
                MicroOpTestHelper.CreateLoad(1, 9, 0x2000, domainTag: 0x00),
                podDomainCert: 0x02);

            Assert.False(crossDomain.IsAllowed);
            Assert.True(crossDomain.IsCrossDomainBlock);
            Assert.False(crossDomain.IsKernelToUserBlock);
            Assert.False(kernelToUser.IsAllowed);
            Assert.False(kernelToUser.IsCrossDomainBlock);
            Assert.True(kernelToUser.IsKernelToUserBlock);
        }
    }
}
