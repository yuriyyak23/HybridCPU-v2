using HybridCPU.Compiler.Cil;
using HybridCPU.Compiler.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;

internal static class ExternalEnumChecks
{
    public static void Run()
    {
        byte[] core = File.ReadAllBytes(typeof(object).Assembly.Location);
        byte[] Declaration(string carrier, int trap = 9) {
        var declaration = CSharpCompilation.Create("EnumDeclaration",
            [CSharpSyntaxTree.ParseText($"public enum Choice : {carrier} {{ First = 0, Second = 1 }} public static class Trap {{ public static int Read() => {trap}; }}",
                new CSharpParseOptions(LanguageVersion.CSharp13))], [MetadataReference.CreateFromImage(core)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, deterministic: true, optimizationLevel: OptimizationLevel.Release));
        using var stream = new MemoryStream(); var emitted = declaration.Emit(stream);
        if (!emitted.Success) throw new Exception(string.Join(';', emitted.Diagnostics));
        return stream.ToArray();
        }
        byte[] reference = Declaration("int");
        var request = new CompanionSourceRequestV1("ExternalEnum", [new("/src/ExternalEnum.cs",
            "public static class Fixture { public static int Read(Choice value) => (int)value + 1; }")], [core, reference]);
        var importer = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2);
        var selector = new RestrictedCilMethodSelectorV1("Fixture", "Read");
        var source = ManagedSourceCompanionV1.Compile(request, importer, selector);
        if (source.Pe is null) throw new Exception("External enum source failed");
        var graph = importer.ImportBodyWorld(new(ManagedBodyWorldModeV1.StandaloneRestrictedModule,
            source.Pe, "roslyn-companion/v1", [selector], [], MetadataOnlyModules: [new(reference, "enum-declaration")]));
        if (graph.Status != RestrictedCilImportStatusV1.Success)
            throw new Exception("External enum metadata-only ingress failed: " + string.Join(';', graph.Diagnostics));
        ManagedSourceBodyWorldResultV1 Compile(CompanionSourceRequestV1 input) =>
            ManagedSourceCompanionV1.CompileBodyWorld(input, importer, [selector], null, [1]);
        var result = Compile(request); var repeat = Compile(request);
        var helperRequest = request with { Documents = [new("/src/ExternalEnum.cs",
            "public static class Fixture { public static int Read(Choice value) { Choice local = Echo(value); return (int)local + 1; } private static Choice Echo(Choice value) => value; }")] };
        var helperResult = Compile(helperRequest);
        if (helperResult.Compilation?.Status != RestrictedCilImportStatusV1.Success)
            throw new Exception("External enum local/helper actual-path failure: " + string.Join(';', helperResult.Compilation?.Diagnostics ?? []));
        // Compare to a manual existing-importer oracle with exactly the same source identity.
        var oracle = importer.ImportBodyWorld(new(ManagedBodyWorldModeV1.StandaloneRestrictedModule,
            source.Pe, "roslyn-companion/v1", [selector], [], MetadataOnlyModules: [new(reference,
                "roslyn-metadata/v1:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(reference)).ToLowerInvariant())]));
        if (result.Compilation?.Status != RestrictedCilImportStatusV1.Success || result.SourceMaps.Count != 1 ||
            JsonSerializer.Serialize(result.Compilation) != JsonSerializer.Serialize(oracle) ||
            JsonSerializer.Serialize(result) != JsonSerializer.Serialize(repeat))
            throw new Exception("Explicit metadata source/manual graph/map/repeat mismatch");
        ScalarControlFlowV2LinkedProgramV1 Link(ManagedSourceBodyWorldResultV1 input) {
            var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(input.Compilation!, input.Compilation!.Graph!.RootIdentities.Single());
            if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success || linked.RestrictedImage is null)
                throw new Exception("External enum native object/link/image failed: " + string.Join(';', linked.Diagnostics));
            return linked;
        }
        var linked = Link(result);
        var helperLinked = Link(helperResult);
        if (helperResult.Compilation!.Methods.Count != 2 || helperResult.SourceMaps.Count != 2 ||
            !helperLinked.RestrictedImage!.PackageBytes.SequenceEqual(Link(Compile(helperRequest)).RestrictedImage!.PackageBytes))
            throw new Exception("External enum helper graph/map/image repeat failed");
        byte[] NamedDeclaration(string assemblyName, string text) {
            var named = CSharpCompilation.Create(assemblyName,
                [CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.CSharp13))],
                [MetadataReference.CreateFromImage(core)], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    deterministic: true, optimizationLevel: OptimizationLevel.Release));
            using var bytes = new MemoryStream();
            if (!named.Emit(bytes).Success) throw new Exception("Named enum declaration emit failed");
            return bytes.ToArray();
        }
        var nestedReference = NamedDeclaration("NestedDeclaration", "public class Outer { public enum Choice : int { First = 0 } } public class Other { public enum Choice : long { First = 0 } }");
        var nestedRequest = helperRequest with { ReferenceImages = [core, nestedReference], Documents = [new("/src/ExternalEnum.cs",
            "public static class Fixture { public static int Read(Outer.Choice value) => (int)Echo(value); private static Outer.Choice Echo(Outer.Choice value) => value; }")] };
        var nested = Compile(nestedRequest);
        if (nested.Compilation?.Status != RestrictedCilImportStatusV1.Success || nested.Compilation.Methods.Count != 2)
            throw new Exception("Nested enum exact helper signature failed: " + string.Join(';', nested.Compilation?.Diagnostics ?? []));
        Link(nested);
        var wrongAssembly = NamedDeclaration("WrongDeclaration", "public enum Choice : int { First = 0, Second = 1 }");
        var wrong = importer.ImportBodyWorld(new(ManagedBodyWorldModeV1.StandaloneRestrictedModule, result.Source.Pe!,
            "roslyn-companion/v1", [selector], [], MetadataOnlyModules: [new(wrongAssembly, "wrong-assembly")]));
        if (wrong.Status == RestrictedCilImportStatusV1.Success || wrong.Graph is not null)
            throw new Exception("Same-name enum from wrong assembly supplied carrier authority");
        Console.WriteLine("PASS external enum helper/local source and nested declaring-type identity; graph/maps/image repeat and same-name wrong-assembly rejection");
        if (!linked.RestrictedImage!.PackageBytes.SequenceEqual(Link(repeat).RestrictedImage!.PackageBytes))
            throw new Exception("External enum image repeat mismatch");
        var omitted = ManagedSourceCompanionV1.CompileBodyWorld(request, importer, [selector]);
        if (omitted.Compilation?.Status == RestrictedCilImportStatusV1.Success || omitted.SourceMaps.Count != 0 ||
            source.Imported?.Status == RestrictedCilImportStatusV1.Success)
            throw new Exception("Unselected metadata/standalone import gained enum declaration authority");
        var trapRequest = request with { Documents = [new("/src/ExternalEnum.cs",
            "public static class Fixture { public static int Read(Choice value) => Trap.Read(); }")] };
        var trap = Compile(trapRequest);
        if (trap.Compilation?.Status == RestrictedCilImportStatusV1.Success || trap.SourceMaps.Count != 0)
            throw new Exception("Metadata-only reference gained method-body authority");
        if (ManagedSourceCompanionV1.CompileBodyWorld(trapRequest, importer, [selector], [1]).Compilation?.Status != RestrictedCilImportStatusV1.Success)
            throw new Exception("Explicit body opt-in stopped working");
        foreach (int[] indices in new[] { new[] { -1 }, new[] { 2 }, new[] { 1, 1 } }) {
            bool denied = false;
            try { ManagedSourceCompanionV1.CompileBodyWorld(request, importer, [selector], null, indices); }
            catch (ArgumentException) { denied = true; }
            if (!denied) throw new Exception("Bad metadata selection admitted");
        }
        bool overlap = false;
        try { ManagedSourceCompanionV1.CompileBodyWorld(request, importer, [selector], [1], [1]); }
        catch (ArgumentException) { overlap = true; }
        if (!overlap) throw new Exception("Overlapping body/metadata authority admitted");
        var changed = Compile(request with { ReferenceImages = [core, Declaration("int", 8)] });
        if (changed.Compilation?.Status != RestrictedCilImportStatusV1.Success ||
            changed.Source.SourceInputDigest == result.Source.SourceInputDigest ||
            changed.Compilation.Methods.Single().Import.Provenance!.OptionsDigest == result.Compilation.Methods.Single().Import.Provenance!.OptionsDigest ||
            changed.Compilation.Graph!.GraphDigest == result.Compilation.Graph!.GraphDigest)
            throw new Exception("Full enum definition image/options/graph identity invalidation failed");
        byte[] malformed = (byte[])reference.Clone();
        using (var reader = new PEReader(new MemoryStream(reference))) {
            var metadata = reader.GetMetadataReader();
            var field = metadata.FieldDefinitions.Select(metadata.GetFieldDefinition).Single(f => metadata.GetString(f.Name) == "value__");
            int blob = reader.PEHeaders.MetadataStartOffset + metadata.GetHeapMetadataOffset(HeapIndex.Blob) + MetadataTokens.GetHeapOffset(field.Signature);
            if (malformed[blob] != 2 || malformed[blob + 1] != 6 || malformed[blob + 2] != 8) throw new Exception("Enum fixture field signature changed");
            malformed[blob + 2] = 2; // Boolean is not a qualified enum underlying carrier.
        }
        var bad = importer.ImportBodyWorld(new(ManagedBodyWorldModeV1.StandaloneRestrictedModule, source.Pe!,
            "roslyn-companion/v1", [selector], [], MetadataOnlyModules: [new(malformed, "bad-enum")]));
        if (bad.Status == RestrictedCilImportStatusV1.Success || bad.Graph is not null)
            throw new Exception("Malformed enum carrier admitted");
        bool cancelled = false;
        try { ManagedSourceCompanionV1.CompileBodyWorld(request, importer, [selector], null, [1], new CancellationToken(true)); }
        catch (OperationCanceledException) { cancelled = true; }
        if (!cancelled || JsonSerializer.Serialize(Compile(request)) != JsonSerializer.Serialize(result))
            throw new Exception("Enum metadata cancellation/reuse failed");
        foreach (string carrier in new[] { "sbyte", "byte", "short", "ushort", "int", "uint", "long", "ulong" }) {
            var typed = request with { ReferenceImages = [core, Declaration(carrier)], Documents = [new("/src/ExternalEnum.cs",
                $"public static class Fixture {{ public static {carrier} Read(Choice value) => ({carrier})value; }}")] };
            var actual = Compile(typed);
            if (actual.Compilation?.Status != RestrictedCilImportStatusV1.Success)
                throw new Exception("Enum carrier failed: " + carrier + "; " + string.Join(';', actual.Compilation?.Diagnostics ?? []));
            var primitive = typed with { Documents = [new("/src/ExternalEnum.cs",
                $"public static class Fixture {{ public static {carrier} Read({carrier} value) => value; }}")] };
            var expected = ManagedSourceCompanionV1.CompileBodyWorld(primitive, importer, [selector]);
            if (expected.Compilation?.Status != RestrictedCilImportStatusV1.Success ||
                Link(actual).MethodObjects.Single().CodeSha256 != Link(expected).MethodObjects.Single().CodeSha256 ||
                !actual.Compilation.Methods.Single().Import.Program!.Instructions.Select(i => (i.Opcode, i.DataType, i.Immediate))
                    .SequenceEqual(expected.Compilation.Methods.Single().Import.Program!.Instructions.Select(i => (i.Opcode, i.DataType, i.Immediate))))
                throw new Exception("Enum carrier physical lowering/encoded code differs from primitive oracle: " + carrier);
        }
        Console.WriteLine("PASS external enum metadata-only source/manual graph/map/image repeat; eight carriers match primitive IR/code oracle");
        Console.WriteLine("PASS enum full-image/options/graph identity changes; missing/malformed metadata, body-authority/selection and cancellation/reuse negatives");
        Console.WriteLine($"IDENTITY enum-reference artifact={result.Source.ArtifactIdentity!.Digest}; graph={result.Compilation.Graph!.GraphDigest}; options={result.Compilation.Methods.Single().Import.Provenance!.OptionsDigest}; image={linked.RestrictedImage.PackageSha256}");
    }
}
