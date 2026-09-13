using System.Collections.Immutable;
using HybridCPU.Compiler.Core.IR.Artifacts;
using HybridCPU_ISE.Tests.CompilerTests;
using HybridCPU_ISE.Tests.TestHelpers;
using YAKSys_Hybrid_CPU.Arch.Generated;
using YAKSys_Hybrid_CPU.Core.Decoder;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf066CompilerParityTests
{
    [Fact]
    public void CompilerEmittedCorpus_FreezesCanonicalDecoderAndExactGeneratedBindingParity()
    {
        CompilerEmissionPackage[] corpus =
        [
            CompilerPhase05CompilerToIseParityHarnessTests.CreateScalarPackage(),
            CompilerPhase05CompilerToIseParityHarnessTests.CreateLoadStorePackage(),
            CompilerPhase05CompilerToIseParityHarnessTests.CreateVectorPackage(),
            CompilerPhase05CompilerToIseParityHarnessTests.CreateMatrixTilePackage(),
            CompilerPhase05CompilerToIseParityHarnessTests.CreateDscPackage(),
            CompilerPhase05CompilerToIseParityHarnessTests.CreateL7Package()
        ];

        foreach (CompilerEmissionPackage package in corpus)
        {
            Rf066CompilerParityProjection projection = Rf066CompilerParityProjector.Capture(package);
            Assert.Equal(Rf066CompilerParityProjector.ProjectionSchema, projection.ProjectionSchema);
            Assert.NotEmpty(projection.Slots);
            Assert.All(projection.Slots, slot =>
            {
                Assert.Equal(slot.Opcode, slot.StaticBinding.Opcode);
                Assert.Equal(slot.DescriptorFingerprint, slot.StaticBinding.DescriptorFingerprint);
                Assert.Equal(slot.LatencyModelId, slot.StaticBinding.LatencyModelId.Value);
                Assert.Equal(projection.CatalogVersion, slot.StaticBinding.CatalogVersion);
                Assert.Equal(projection.CatalogSha256, slot.StaticBinding.CatalogSha256);
            });
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("inactive")]
    [InlineData("unreferenced")]
    [InlineData("schema")]
    public void BindingNegativeMatrix_FailsClosedBeforeAdmissionOrExecution(string mutation)
    {
        CompilerEmissionPackage package = CompilerPhase05CompilerToIseParityHarnessTests.CreateScalarPackage();
        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
            Rf066CompilerParityProjector.Capture(package, entries => Mutate(entries, mutation)));
        Assert.Contains("No admission or execution is permitted", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DescriptorSchemaHashOpcodeAnnotationAndProviderFamilyDrift_FailClosed()
    {
        CompilerEmissionPackage package = CompilerPhase05CompilerToIseParityHarnessTests.CreateScalarPackage();
        Rf066CompilerParityProjection baseline = Rf066CompilerParityProjector.Capture(package);
        Rf066CompilerParitySlot slot = Assert.Single(baseline.Slots);

        AssertDrift(baseline, baseline with
        {
            CatalogSchemaVersion = baseline.CatalogSchemaVersion + 1
        });
        AssertDrift(baseline, baseline with
        {
            CatalogVersion = baseline.CatalogVersion + ".mutated"
        });
        AssertDrift(baseline, baseline with
        {
            CatalogSha256 = "mutated-catalog-hash"
        });
        AssertDrift(baseline, baseline with
        {
            Bundles = baseline.Bundles.SetItem(
                0,
                baseline.Bundles[0] with { BundleSidebandSha256 = "mutated-bundle-sideband-hash" })
        });
        AssertDrift(baseline, ReplaceSlot(baseline, slot with
        {
            OperandSchema = "mutated-operand-schema"
        }));
        AssertDrift(baseline, ReplaceSlot(baseline, slot with
        {
            DescriptorFingerprint = "mutated-descriptor-fingerprint"
        }));
        AssertDrift(baseline, ReplaceSlot(baseline, slot with
        {
            Opcode = slot.Opcode + 1
        }));
        AssertDrift(baseline, ReplaceSlot(baseline, slot with
        {
            OperandFingerprint = slot.OperandFingerprint + ";mutated"
        }));
        AssertDrift(baseline, ReplaceSlot(baseline, slot with
        {
            SlotSidebandSha256 = "mutated-sideband-hash"
        }));
        AssertDrift(baseline, ReplaceSlot(baseline, slot with
        {
            StaticBinding = slot.StaticBinding with
            {
                RuntimeExecutionProviderId = new RuntimeExecutionProviderId(
                    "compiler.lowering.NativeVliwScalarProductionProvider")
            }
        }));
    }

    [Fact]
    public void RegistryOrderMutation_DoesNotChangeCanonicalParityIdentity()
    {
        CompilerEmissionPackage package = CompilerPhase05CompilerToIseParityHarnessTests.CreateScalarPackage();
        Rf066CompilerParityProjection baseline = Rf066CompilerParityProjector.Capture(package);
        Rf066CompilerParityProjector.AssertEquivalentIgnoringRegistryOrder(package, baseline);
    }

    [Fact]
    public void SourceBoundary_UsesPublicCanonicalHandoffAndDoesNotMaterializeOrLookupBindingByOpcode()
    {
        string root = CompatFreezeScanner.FindRepoRoot();
        string source = File.ReadAllText(Path.Combine(
            root,
            "HybridCPU_ISE.Tests",
            "TestHelpers",
            "Rf066CompilerParityProjection.cs"));
        Assert.Contains("new VliwDecoderV4()", source, StringComparison.Ordinal);
        Assert.Contains("decoded.CanonicalBundle", source, StringComparison.Ordinal);
        Assert.Contains("canonical.StaticBinding", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedStaticBinding.TryFromOpcode", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InstructionRegistry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DecodedBundleTransportProjector", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InternalOpBuilder", source, StringComparison.Ordinal);
    }

    private static ImmutableArray<Rf066CompilerBindingEvidence> Mutate(
        ImmutableArray<Rf066CompilerBindingEvidence> entries,
        string mutation)
    {
        Rf066CompilerBindingEvidence first = entries[0];
        return mutation switch
        {
            "missing" => entries.RemoveAt(0),
            "duplicate" => entries.Add(first),
            "inactive" => entries.SetItem(0, first with { IsActive = false }),
            "unreferenced" => entries.Add(first with
            {
                Binding = first.Binding with
                {
                    Opcode = uint.MaxValue,
                    DescriptorFingerprint = "unreferenced"
                }
            }),
            "schema" => entries.SetItem(0, first with { Schema = "stale-schema" }),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
    }

    private static Rf066CompilerParityProjection ReplaceSlot(
        Rf066CompilerParityProjection projection,
        Rf066CompilerParitySlot replacement) =>
        projection with { Slots = projection.Slots.SetItem(0, replacement) };

    private static void AssertDrift(
        Rf066CompilerParityProjection baseline,
        Rf066CompilerParityProjection mutated)
    {
        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => Rf066CompilerParityProjector.AssertEquivalent(baseline, mutated));
        Assert.Contains("No admission or execution is permitted", failure.Message, StringComparison.Ordinal);
    }
}
