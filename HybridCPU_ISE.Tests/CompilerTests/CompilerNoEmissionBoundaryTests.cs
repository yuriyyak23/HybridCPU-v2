using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HybridCPU.Compiler.Core.API.Facade;
using HybridCPU.Compiler.Core.Threading;
using Xunit;

namespace HybridCPU_ISE.Tests.CompilerTests;

public sealed class CompilerNoEmissionBoundaryTests
{
    private static readonly string[] ClosedHelperNameFragments =
    [
        "Atomic",
        "Amo",
        "LoadReserved",
        "StoreConditional",
        "Fence",
        "Sfence",
        "DCache",
        "ICache",
        "Tlb",
        "Matrix",
        "Mtile",
        "Cache"
    ];

    [Fact]
    public void PublicFacadeSurfaces_DoNotExposeClosedAtomicFenceMatrixCacheOrTlbHelpers()
    {
        string[] publicFacadeMethods =
        [
            .. PublicMethodNames(typeof(IAppAsmFacade)),
            .. PublicMethodNames(typeof(AppAsmFacade)),
            .. PublicMethodNames(typeof(IPlatformAsmFacade)),
            .. PublicMethodNames(typeof(PlatformAsmFacade))
        ];

        foreach (string methodName in publicFacadeMethods.Distinct(StringComparer.Ordinal))
        {
            Assert.DoesNotContain(
                ClosedHelperNameFragments,
                fragment => methodName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void PlatformVectorFacade_RemainsRawTransportPlusScopedVsetvliOnly()
    {
        string[] platformMethods = PublicMethodNames(typeof(IPlatformAsmFacade));

        Assert.Contains(nameof(IPlatformAsmFacade.VectorOp), platformMethods);
        Assert.Contains(nameof(IPlatformAsmFacade.VectorOpImm), platformMethods);
        Assert.Contains(nameof(IPlatformAsmFacade.VSetVli), platformMethods);

        Assert.DoesNotContain(platformMethods, name => name.Contains("Gather", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(platformMethods, name => name.Contains("Scatter", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(platformMethods, name => name.Contains("Indexed", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(platformMethods, name => name.Contains("2D", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(platformMethods, name => name.Contains("VectorLoad", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(platformMethods, name => name.Contains("VectorStore", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ThreadCompilerPublicSurface_KeepsLane6AndLane7DescriptorCarrierBoundaries()
    {
        string[] threadMethods = PublicMethodNames(typeof(HybridCpuThreadCompilerContext));

        string[] lane6Methods = threadMethods
            .Where(name => name.Contains("DmaStreamCompute", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            [nameof(HybridCpuThreadCompilerContext.CompileDmaStreamCompute),
             nameof(HybridCpuThreadCompilerContext.CompileDmaStreamComputeDescriptor)],
            lane6Methods);

        string[] lane7Methods = threadMethods
            .Where(name => name.Contains("Accelerator", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal([nameof(HybridCpuThreadCompilerContext.CompileAcceleratorSubmit)], lane7Methods);

        Assert.DoesNotContain(threadMethods, name => name.Contains("Production", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("Fallback", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("Backend", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("Execute", StringComparison.OrdinalIgnoreCase));
    }

    private static string[] PublicMethodNames(Type type)
    {
        return type
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(static method => !method.IsSpecialName)
            .Select(static method => method.Name)
            .ToArray();
    }
}
