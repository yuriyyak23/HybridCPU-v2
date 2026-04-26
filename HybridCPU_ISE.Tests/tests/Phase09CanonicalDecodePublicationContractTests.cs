using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09CanonicalDecodePublicationContractTests
    {
        [Fact]
        public void T9_08a_AllConcreteMicroOpTypes_DeclareExplicitCanonicalDecodePublicationPolicy()
        {
            Type[] concreteMicroOpTypes = typeof(MicroOp).Assembly
                .GetTypes()
                .Where(type => !type.IsAbstract && typeof(MicroOp).IsAssignableFrom(type))
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();

            string[] unspecifiedPolicyTypes = concreteMicroOpTypes
                .Select(CreateMicroOp)
                .Where(microOp => microOp.CanonicalDecodePublication == CanonicalDecodePublicationMode.Unspecified)
                .Select(microOp => microOp.GetType().FullName ?? microOp.GetType().Name)
                .ToArray();

            Assert.True(
                unspecifiedPolicyTypes.Length == 0,
                "Concrete MicroOp types must declare an explicit canonical decode publication policy. Missing: " +
                string.Join(", ", unspecifiedPolicyTypes));
        }

        [Fact]
        public void T9_08b_CanonicalFamilies_DeclareSelfPublishedPolicy()
        {
            Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, new ScalarALUMicroOp().CanonicalDecodePublication);
            Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, new BranchMicroOp().CanonicalDecodePublication);
            Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, new CsrReadWriteMicroOp().CanonicalDecodePublication);
            Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, new LoadMicroOp().CanonicalDecodePublication);
            Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, new AtomicMicroOp().CanonicalDecodePublication);
            Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, new NopMicroOp().CanonicalDecodePublication);
            Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, new MoveMicroOp().CanonicalDecodePublication);
            Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, new SysEventMicroOp().CanonicalDecodePublication);
            Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, new StreamControlMicroOp().CanonicalDecodePublication);
            Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, new VectorBinaryOpMicroOp().CanonicalDecodePublication);
            Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, new VectorMaskPopCountMicroOp().CanonicalDecodePublication);
            Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, new VectorTransferMicroOp().CanonicalDecodePublication);
            Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, new VConfigMicroOp().CanonicalDecodePublication);
            Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, new VmxMicroOp().CanonicalDecodePublication);
        }

        [Fact]
        public void T9_08c_FallbackFamilies_DeclareProjectorOwnedPolicy()
        {
            Assert.Equal(CanonicalDecodePublicationMode.ProjectorPublishes, new VectorALUMicroOp().CanonicalDecodePublication);
            Assert.Equal(CanonicalDecodePublicationMode.ProjectorPublishes, new GenericMicroOp().CanonicalDecodePublication);
            Assert.Equal(CanonicalDecodePublicationMode.ProjectorPublishes, new PortIOMicroOp().CanonicalDecodePublication);
            Assert.Equal(CanonicalDecodePublicationMode.ProjectorPublishes, new TrapMicroOp().CanonicalDecodePublication);
            Assert.Equal(CanonicalDecodePublicationMode.ProjectorPublishes, new CustomAcceleratorMicroOp().CanonicalDecodePublication);
            Assert.Equal(CanonicalDecodePublicationMode.ProjectorPublishes, new HaltMicroOp().CanonicalDecodePublication);
            Assert.Equal(CanonicalDecodePublicationMode.ProjectorPublishes, new IncrDecrMicroOp().CanonicalDecodePublication);
        }

        private static MicroOp CreateMicroOp(Type type)
        {
            object? instance = type.GetConstructor(Type.EmptyTypes) is not null
                ? Activator.CreateInstance(type)
                : RuntimeHelpers.GetUninitializedObject(type);

            Assert.True(instance is not null, $"Could not instantiate MicroOp type '{type.FullName}'.");
            return Assert.IsAssignableFrom<MicroOp>(instance);
        }
    }
}
