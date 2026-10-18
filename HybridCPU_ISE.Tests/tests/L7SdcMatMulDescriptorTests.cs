using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcMatMulDescriptorTests
{
    [Fact]
    public void L7SdcMatMulDescriptor_ValidShapeAcceptedAfterGuard()
    {
        MatMulDescriptor descriptor = CreateMatMulDescriptor();
        AcceleratorCommandDescriptor commandDescriptor =
            CreateCommandDescriptor(descriptor);
        var validator = new MatMulDescriptorValidator();

        MatMulDescriptorValidationResult result =
            validator.Validate(
                descriptor,
                commandDescriptor);

        Assert.True(result.IsValid, result.Message);
        Assert.NotNull(result.Footprint);
        Assert.Equal(new AcceleratorMemoryRange(0x1000, 16), result.Footprint!.ARange);
        Assert.Equal(new AcceleratorMemoryRange(0x2000, 16), result.Footprint.BRange);
        Assert.Equal(new AcceleratorMemoryRange(0x9000, 16), result.Footprint.CRange);
        Assert.False(result.GrantsCommandSubmissionAuthority);
        Assert.False(result.GrantsExecutionAuthority);
        Assert.False(result.GrantsCommitAuthority);
    }

    [Fact]
    public void L7SdcMatMulDescriptor_UnsupportedShapeRejects()
    {
        var validator = new MatMulDescriptorValidator();
        MatMulDescriptor invalid =
            CreateMatMulDescriptor() with
            {
                M = 0
            };

        MatMulDescriptorValidationResult result =
            validator.ValidateShape(invalid);

        Assert.True(result.IsRejected);
        Assert.Equal(AcceleratorDescriptorFault.UnsupportedShape, result.Fault);
    }

    [Fact]
    public void L7SdcMatMulDescriptor_OutputShapeBeyondProviderLimitRejects()
    {
        var validator = new MatMulDescriptorValidator();
        MatMulDescriptor invalid =
            CreateMatMulDescriptor() with
            {
                M = 65,
                N = 64,
                K = 1,
                Lda = 1,
                Ldb = 64,
                Ldc = 64,
                TileM = 1,
                TileN = 64,
                TileK = 1
            };

        MatMulDescriptorValidationResult result =
            validator.ValidateShape(invalid);

        Assert.True(result.IsRejected);
        Assert.Equal(AcceleratorDescriptorFault.UnsupportedShape, result.Fault);
    }

    [Fact]
    public void L7SdcMatMulDescriptor_UnsupportedDatatypeTripleRejects()
    {
        var validator = new MatMulDescriptorValidator();
        MatMulDescriptor invalid =
            CreateMatMulDescriptor() with
            {
                Datatypes = new MatMulDatatypeTriple(
                    AcceleratorDatatype.Float32,
                    AcceleratorDatatype.Float64,
                    AcceleratorDatatype.Float32)
            };

        MatMulDescriptorValidationResult result =
            validator.ValidateDatatypes(invalid);

        Assert.True(result.IsRejected);
        Assert.Equal(AcceleratorDescriptorFault.UnsupportedDatatype, result.Fault);
    }

    [Fact]
    public void L7SdcMatMulDescriptor_InvalidStrideOrLayoutRejects()
    {
        var validator = new MatMulDescriptorValidator();
        MatMulDescriptor invalidStride =
            CreateMatMulDescriptor() with
            {
                Lda = 1
            };
        MatMulDescriptor invalidLayout =
            CreateMatMulDescriptor() with
            {
                LayoutFlags = MatMulLayoutFlags.RowMajorA |
                              MatMulLayoutFlags.RowMajorB |
                              MatMulLayoutFlags.RowMajorC |
                              MatMulLayoutFlags.TransposeA
            };

        MatMulDescriptorValidationResult stride =
            validator.ValidateStrides(invalidStride);
        MatMulDescriptorValidationResult layout =
            validator.ValidateStrides(invalidLayout);

        Assert.True(stride.IsRejected);
        Assert.Equal(AcceleratorDescriptorFault.UnsupportedShape, stride.Fault);
        Assert.True(layout.IsRejected);
        Assert.Equal(AcceleratorDescriptorFault.UnsupportedShape, layout.Fault);
    }

    [Fact]
    public void L7SdcMatMulDescriptor_NonAllOrNonePartialPolicyRejects()
    {
        var validator = new MatMulDescriptorValidator();
        MatMulDescriptor invalid =
            CreateMatMulDescriptor() with
            {
                PartialPolicy = (AcceleratorPartialCompletionPolicy)99
            };

        MatMulDescriptorValidationResult result =
            validator.ValidateDatatypes(invalid);

        Assert.True(result.IsRejected);
        Assert.Equal(AcceleratorDescriptorFault.UnsupportedPartialCompletionPolicy, result.Fault);
    }

    [Fact]
    public void L7SdcMatMulDescriptor_AliasAmbiguousFootprintRejects()
    {
        var validator = new MatMulDescriptorValidator();
        MatMulDescriptor aliasAmbiguous =
            CreateMatMulDescriptor() with
            {
                BBase = 0x1008
            };

        MatMulDescriptorValidationResult result =
            validator.NormalizeFootprints(aliasAmbiguous);

        Assert.True(result.IsRejected);
        Assert.Equal(AcceleratorDescriptorFault.AliasAmbiguousFootprint, result.Fault);
    }

    [Fact]
    public void L7SdcMatMulDescriptor_CommandElementCountMismatchRejects()
    {
        var validator = new MatMulDescriptorValidator();
        MatMulDescriptor descriptor = CreateMatMulDescriptor();
        AcceleratorCommandDescriptor commandDescriptor =
            CreateCommandDescriptor(
                descriptor,
                elementCount: 3);

        MatMulDescriptorValidationResult result =
            validator.Validate(
                descriptor,
                commandDescriptor);

        Assert.True(result.IsRejected);
        Assert.Equal(AcceleratorDescriptorFault.UnsupportedShape, result.Fault);
    }

    internal static MatMulDescriptor CreateMatMulDescriptor(
        ulong aBase = 0x1000,
        ulong bBase = 0x2000,
        ulong cBase = 0x9000) =>
        new()
        {
            ABase = aBase,
            BBase = bBase,
            CBase = cBase,
            M = 2,
            N = 2,
            K = 2,
            Lda = 2,
            Ldb = 2,
            Ldc = 2,
            TileM = 2,
            TileN = 2,
            TileK = 2,
            Datatypes = new MatMulDatatypeTriple(
                AcceleratorDatatype.Float32,
                AcceleratorDatatype.Float32,
                AcceleratorDatatype.Float32),
            LayoutFlags = MatMulLayoutFlags.RowMajorA |
                          MatMulLayoutFlags.RowMajorB |
                          MatMulLayoutFlags.RowMajorC
        };

    internal static AcceleratorCommandDescriptor CreateCommandDescriptor(
        MatMulDescriptor descriptor,
        ulong? elementCount = null)
    {
        var validator = new MatMulDescriptorValidator();
        MatMulDescriptorValidationResult footprint =
            validator.NormalizeFootprints(descriptor);
        Assert.True(footprint.IsValid, footprint.Message);

        byte[] bytes = L7SdcTestDescriptorFactory.BuildDescriptor(
            sourceRanges: footprint.Footprint!.SourceRanges,
            destinationRanges: footprint.Footprint.DestinationRanges,
            datatype: descriptor.Datatypes.OutputDatatype,
            elementCount: elementCount ?? ((ulong)descriptor.M * descriptor.N));

        return L7SdcTestDescriptorFactory.ParseWithGuard(bytes).RequireDescriptor();
    }
}
