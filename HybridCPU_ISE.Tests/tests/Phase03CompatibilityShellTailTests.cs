using System.Reflection;
using Xunit;

namespace HybridCPU_ISE.Tests.Phase03
{
    public sealed class CompatibilityShellTailTests
    {
        [Fact]
        public void DecodeCompatibilityShell_NoLongerPublishesBroadDecodedBundleDescriptorType()
        {
            Assembly assembly = typeof(YAKSys_Hybrid_CPU.Processor.CPU_Core).Assembly;

            Assert.Null(assembly.GetType("YAKSys_Hybrid_CPU.Core.DecodedBundleDescriptor"));
            Assert.Null(assembly.GetType("YAKSys_Hybrid_CPU.Core.DecodedBundleDescriptorInspectionBuilder"));
        }

        [Fact]
        public void CpuCore_NoLongerCarriesDecodedBundleField()
        {
            FieldInfo? field = typeof(YAKSys_Hybrid_CPU.Processor.CPU_Core).GetField(
                "decodedBundle",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.Null(field);
        }
    }
}
