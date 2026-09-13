using System.Collections.Immutable;
using HybridCPU.Compiler.Analyzers;
using HybridCPU.Compiler.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

static class Program
{
    public static async Task Main()
    {
        MetadataReference[] references =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(HybridCPU.Compiler.Profiles.HybridCpuKernelNoHeapAttribute).Assembly.Location)
        ];
        const string source = """
            using HybridCPU.Compiler.Profiles;
            [HybridCpuStaticContract]
            interface IClock
            {
                void Zed(long payload);
                int Read(int channel);
                byte Alpha();
            }
            class Kernel
            {
                [HybridCpuKernelNoHeap]
                static void Tick() { var values = new int[4]; System.GC.Collect(); }
            }
            """;
        CSharpCompilation compilation = CSharpCompilation.Create("profile-smoke",
            [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var analyzer = new KernelNoHeapAnalyzer();
        ImmutableArray<Diagnostic> diagnostics = await compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer)).GetAnalyzerDiagnosticsAsync();
        if (!diagnostics.Select(static diagnostic => diagnostic.Id).OrderBy(static id => id)
                .SequenceEqual(new[] { "HC0004", "HC0005" }))
            throw new InvalidOperationException("Kernel.NoHeap analyzer must reject allocation and GC APIs deterministically.");
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new StaticContractGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation generated, out ImmutableArray<Diagnostic> generationDiagnostics);
        GeneratorDriver repeatedDriver = CSharpGeneratorDriver.Create(new StaticContractGenerator());
        repeatedDriver = repeatedDriver.RunGeneratorsAndUpdateCompilation(compilation, out _, out ImmutableArray<Diagnostic> repeatedDiagnostics);
        string generatedText = driver.GetRunResult().GeneratedTrees.Single().GetText().ToString();
        string repeatedText = repeatedDriver.GetRunResult().GeneratedTrees.Single().GetText().ToString();
        if (generationDiagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) ||
            repeatedDiagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) ||
            generated.SyntaxTrees.Count() != compilation.SyntaxTrees.Count() + 1 ||
            !string.Equals(generatedText, repeatedText, StringComparison.Ordinal) ||
            !generatedText.Contains("ContractDigest", StringComparison.Ordinal) ||
            !generatedText.Contains("ChannelManifestRows", StringComparison.Ordinal) ||
            !generatedText.Contains("serializer=unqualified:deserializer=unqualified:dispatch=declaration-only:ownership=unqualified", StringComparison.Ordinal) ||
            !generatedText.Contains("WireIds", StringComparison.Ordinal) ||
            generatedText.IndexOf("::Alpha(", StringComparison.Ordinal) >= generatedText.IndexOf("::Read(", StringComparison.Ordinal) ||
            generatedText.IndexOf("::Read(", StringComparison.Ordinal) >= generatedText.IndexOf("::Zed(", StringComparison.Ordinal))
            throw new InvalidOperationException("Static contract generator must emit one deterministic contract manifest without errors.");
        Console.WriteLine("PASS HybridCPU source profile analyzer and static contract generator");
    }
}
