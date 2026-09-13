using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Arch.Generated;

internal static class Program
{
    private const int CatalogSchemaVersion = 1;
    private const int LockSchemaVersion = 1;
    private const int ManifestInputSchemaVersion = 2;
    private const int CompatibilityMapSchemaVersion = 1;
    private const string CatalogVersion = "4.0.0-csharp-catalog";

    private static int Main(string[] args)
    {
        try
        {
            var options = GeneratorOptions.Parse(args);
            var root = options.Root ?? FindRepositoryRoot(Directory.GetCurrentDirectory());
            var lockPath = Path.Combine(root, "isa", "hybridcpu-isa.lock.json");
            var manifestPath = Path.Combine(root, "isa", "hybridcpu-isa.manifest.json");
            var compatibilityMapPath = Path.Combine(root, "isa", "hybridcpu-isa.csharp-compatibility.json");
            var outputPath = Path.Combine(root, "HybridCPU_ISE", "NonRTL", "Arch", "Generated", "GeneratedIsaShadowCatalog.g.cs");
            var compatibilityOutputPath = Path.Combine(root, "HybridCPU_ISE", "NonRTL", "Arch", "Generated", "GeneratedIsaOpcodeValues.g.cs");

            if (options.ValidateManifestPath is not null)
            {
                var validationPath = Path.GetFullPath(options.ValidateManifestPath, root);
                var manifestCatalog = ReadStrictManifest(validationPath);
                Console.WriteLine($"HybridCPU ISA manifest is valid ({manifestCatalog.Instructions.Count} rows, {manifestCatalog.Hash}).");
                return 0;
            }

            var catalog = ReadStrictManifest(manifestPath);
            if (options.ValidateCompatibilityMapPath is not null)
            {
                var validationPath = Path.GetFullPath(options.ValidateCompatibilityMapPath, root);
                var validatedCompatibilityMap = ReadStrictCompatibilityMap(validationPath, catalog);
                Console.WriteLine($"HybridCPU ISA C# compatibility map is valid ({validatedCompatibilityMap.Entries.Count} rows, catalog {validatedCompatibilityMap.CatalogSha256}).");
                return 0;
            }
            var compatibilityMap = ReadStrictCompatibilityMap(compatibilityMapPath, catalog);
            var generated = RenderGeneratedCatalog(catalog);
            var compatibilityGenerated = RenderGeneratedCompatibilitySidecar(compatibilityMap);
            var lockFile = RenderLockFile(catalog);

            if (options.SelfTest)
            {
                RunSelfTests(catalog, generated, compatibilityGenerated, lockFile);
                return 0;
            }

            if (options.VerifyRegistryParity)
            {
                VerifyGeneratedCatalogParity(catalog);
                return 0;
            }

            if (options.Check)
            {
                AssertMatches(outputPath, generated);
                AssertMatches(compatibilityOutputPath, compatibilityGenerated);
                AssertMatches(lockPath, lockFile);
                Console.WriteLine($"HybridCPU ISA manifest catalog is current ({catalog.Instructions.Count} rows, {catalog.Hash}).");
                return 0;
            }

            WriteDeterministic(outputPath, generated);
            WriteDeterministic(compatibilityOutputPath, compatibilityGenerated);
            WriteDeterministic(lockPath, lockFile);
            Console.WriteLine($"Generated HybridCPU ISA projections from manifest ({catalog.Instructions.Count} rows, {catalog.Hash}).");
            return 0;
        }
        catch (IsaGeneratorException exception)
        {
            Console.Error.WriteLine($"HybridCPU ISA generator error: {exception.Message}");
            return 1;
        }
    }

    private static void ValidateCatalog(
        IReadOnlyList<InstructionDeclaration> instructions,
        IReadOnlyDictionary<string, IReadOnlyList<string>> policies)
    {
        if (instructions.Count == 0)
        {
            throw new IsaGeneratorException("The ISA catalog is empty.");
        }

        if (instructions.Select(instruction => instruction.Opcode).Distinct().Count() != instructions.Count ||
            instructions.Select(instruction => instruction.Mnemonic).Distinct(StringComparer.Ordinal).Count() != instructions.Count)
        {
            throw new IsaGeneratorException("The ISA catalog contains duplicate opcode or mnemonic declarations.");
        }

        var requiredPolicies = new[]
        {
            nameof(IsaV4Surface.MandatoryCoreClasses), nameof(IsaV4Surface.MandatoryCoreOpcodes),
            nameof(IsaV4Surface.SystemDeviceCommandOpcodes), nameof(IsaV4Surface.CarrierOnlyOpcodes),
            nameof(IsaV4Surface.MandatoryInteger64RepairOpcodes), nameof(IsaV4Surface.DescriptorOnlyOpcodes),
            nameof(IsaV4Surface.ParserOnlyOpcodes), nameof(IsaV4Surface.OptionalEnabledOpcodes),
            nameof(IsaV4Surface.OptionalDisabledOpcodes), nameof(IsaV4Surface.ReservedOpcodes),
            nameof(IsaV4Surface.ProhibitedOpcodes), nameof(IsaV4Surface.OptionalExtensions),
            nameof(IsaV4Surface.PipelineClassMap),
        };
        if (policies.Count != requiredPolicies.Length || requiredPolicies.Any(policy => !policies.ContainsKey(policy)))
        {
            throw new IsaGeneratorException("The ISA static policy catalog is incomplete.");
        }

        foreach (var policy in policies)
        {
            if (policy.Value.Any(string.IsNullOrWhiteSpace) || policy.Value.Distinct(StringComparer.Ordinal).Count() != policy.Value.Count)
            {
                throw new IsaGeneratorException($"ISA static policy '{policy.Key}' contains an empty or duplicate value.");
            }
        }
    }

    // Generator-only input seam. Runtime code consumes generated C#, never this JSON parser.
    private static CatalogDocument ReadStrictManifest(string path)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (FileNotFoundException exception)
        {
            throw new IsaGeneratorException($"Manifest is missing: {path}.", exception);
        }
        catch (IOException exception)
        {
            throw new IsaGeneratorException($"Manifest cannot be read: {path}.", exception);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            RequireKind(root, JsonValueKind.Object, "manifest root");
            RejectDuplicateProperties(root, "manifest root");
            RequireExactProperties(root, "manifest root", ManifestProperties);

            RequireExactInt32(root, "manifestSchemaVersion", ManifestInputSchemaVersion);
            RequireExactString(root, "sourceOfTruth", "declared-static-isa-manifest");
            if (RequireProperty(root, "isGeneratorInput").ValueKind != JsonValueKind.True)
            {
                throw new IsaGeneratorException("Manifest 'isGeneratorInput' must be true for the RF-13 input schema.");
            }
            RequireExactInt32(root, "catalogSchemaVersion", CatalogSchemaVersion);
            RequireExactString(root, "catalogVersion", CatalogVersion);
            var declaredHash = RequireString(root, "catalogSha256");
            if (declaredHash.Length != 64 || declaredHash.Any(character => !Uri.IsHexDigit(character)) ||
                !string.Equals(declaredHash, declaredHash.ToLowerInvariant(), StringComparison.Ordinal))
            {
                throw new IsaGeneratorException("Manifest 'catalogSha256' must be a lowercase SHA-256 value.");
            }

            var policies = ReadPolicies(RequireProperty(root, "staticPolicies"));
            var instructions = ReadInstructions(RequireProperty(root, "instructions"));
            var instructionCount = RequireInt32(root, "instructionCount");
            if (instructionCount != instructions.Count)
            {
                throw new IsaGeneratorException($"Manifest instructionCount {instructionCount} does not match instructions array length {instructions.Count}.");
            }

            ValidateCatalog(instructions, policies);
            var canonical = JsonSerializer.Serialize(new
            {
                catalogSchemaVersion = CatalogSchemaVersion,
                catalogVersion = CatalogVersion,
                staticPolicies = policies.OrderBy(policy => policy.Key, StringComparer.Ordinal)
                    .Select(policy => new { policy.Key, Values = policy.Value }),
                instructions = instructions.OrderBy(instruction => instruction.Opcode)
                    .ThenBy(instruction => instruction.Mnemonic, StringComparer.Ordinal),
            });
            var actualHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
            if (!string.Equals(declaredHash, actualHash, StringComparison.Ordinal))
            {
                throw new IsaGeneratorException($"Manifest hash drift: declared {declaredHash}, canonical {actualHash}.");
            }

            return new CatalogDocument(
                instructions.OrderBy(instruction => instruction.Opcode).ThenBy(instruction => instruction.Mnemonic, StringComparer.Ordinal).ToArray(),
                new Dictionary<string, IReadOnlyList<string>>(policies, StringComparer.Ordinal),
                actualHash);
        }
        catch (JsonException exception)
        {
            throw new IsaGeneratorException($"Manifest is malformed JSON: {path}.", exception);
        }
    }

    // RF-13.45 validation-only seam. Normal generation does not read this compatibility input.
    private static CompatibilityMapDocument ReadStrictCompatibilityMap(string path, CatalogDocument catalog)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (FileNotFoundException exception)
        {
            throw new IsaGeneratorException($"Compatibility map is missing: {path}.", exception);
        }
        catch (IOException exception)
        {
            throw new IsaGeneratorException($"Compatibility map cannot be read: {path}.", exception);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            RequireKind(root, JsonValueKind.Object, "compatibility-map root");
            RejectDuplicateProperties(root, "compatibility-map root");
            RequireExactProperties(root, "compatibility-map root", CompatibilityMapProperties);
            RequireExactInt32(root, "compatibilityMapSchemaVersion", CompatibilityMapSchemaVersion);
            RequireExactString(root, "sourceOfTruth", "csharp-compatibility-facade-map");
            var catalogHash = RequireString(root, "catalogSha256");
            if (!string.Equals(catalogHash, catalog.Hash, StringComparison.Ordinal))
            {
                throw new IsaGeneratorException($"Compatibility map catalog hash drift: expected {catalog.Hash}, received {catalogHash}.");
            }

            var entriesElement = RequireProperty(root, "entries");
            RequireKind(entriesElement, JsonValueKind.Array, "compatibility-map entries");
            var entries = new List<CompatibilityMapEntry>();
            foreach (var entry in entriesElement.EnumerateArray())
            {
                RequireKind(entry, JsonValueKind.Object, "compatibility-map entry");
                RejectDuplicateProperties(entry, "compatibility-map entry");
                RequireExactProperties(entry, "compatibility-map entry", CompatibilityMapEntryProperties);
                var name = RequireString(entry, "Name");
                if (!IsValidCSharpIdentifier(name))
                {
                    throw new IsaGeneratorException($"Compatibility map entry '{name}' is not a valid C# identifier.");
                }
                if (!RequireProperty(entry, "Opcode").TryGetUInt16(out var opcode))
                {
                    throw new IsaGeneratorException("Compatibility map entry Opcode must be an unsigned 16-bit integer.");
                }
                entries.Add(new CompatibilityMapEntry(name, opcode));
            }

            ValidateCompatibilityMap(entries, catalog);
            // Entry order is part of the public enum reflection contract (Enum.GetNames/GetValues).
            return new CompatibilityMapDocument(entries.ToArray(), catalogHash);
        }
        catch (JsonException exception)
        {
            throw new IsaGeneratorException($"Compatibility map is malformed JSON: {path}.", exception);
        }
    }

    private static void ValidateCompatibilityMap(IReadOnlyList<CompatibilityMapEntry> entries, CatalogDocument catalog)
    {
        if (entries.Select(entry => entry.Name).Distinct(StringComparer.Ordinal).Count() != entries.Count ||
            entries.Select(entry => entry.Opcode).Distinct().Count() != entries.Count)
        {
            throw new IsaGeneratorException("Compatibility map contains duplicate name or opcode.");
        }

        var sentinel = entries.Where(entry => entry.Opcode == 0).ToArray();
        if (sentinel.Length != 1 || !string.Equals(sentinel[0].Name, "Nope", StringComparison.Ordinal))
        {
            throw new IsaGeneratorException("Compatibility map must contain exactly the Nope = 0 sentinel.");
        }

        var expectedOpcodes = catalog.Instructions.Select(instruction => instruction.Opcode).Append(0u).OrderBy(opcode => opcode).ToArray();
        var actualOpcodes = entries.Select(entry => (uint)entry.Opcode).OrderBy(opcode => opcode).ToArray();
        if (!expectedOpcodes.SequenceEqual(actualOpcodes))
        {
            throw new IsaGeneratorException("Compatibility map opcode set must equal the declared catalog plus Nope = 0.");
        }
    }

    private static bool IsValidCSharpIdentifier(string value) =>
        value.Length != 0 && (char.IsLetter(value[0]) || value[0] == '_') &&
        value.Skip(1).All(character => char.IsLetterOrDigit(character) || character == '_');

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ReadPolicies(JsonElement element)
    {
        RequireKind(element, JsonValueKind.Array, "staticPolicies");
        var policies = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var policy in element.EnumerateArray())
        {
            RequireKind(policy, JsonValueKind.Object, "static policy");
            RejectDuplicateProperties(policy, "static policy");
            RequireExactProperties(policy, "static policy", StaticPolicyProperties);
            var key = RequireString(policy, "Key");
            var values = ReadStringArray(RequireProperty(policy, "Values"), $"static policy '{key}' Values");
            if (!policies.TryAdd(key, values))
            {
                throw new IsaGeneratorException($"Manifest contains duplicate static policy '{key}'.");
            }
        }
        return policies;
    }

    private static IReadOnlyList<InstructionDeclaration> ReadInstructions(JsonElement element)
    {
        RequireKind(element, JsonValueKind.Array, "instructions");
        var instructions = new List<InstructionDeclaration>();
        foreach (var instruction in element.EnumerateArray())
        {
            RequireKind(instruction, JsonValueKind.Object, "instruction");
            RejectDuplicateProperties(instruction, "instruction");
            RequireExactProperties(instruction, "instruction", InstructionProperties);
            if (!RequireProperty(instruction, "Opcode").TryGetUInt32(out var opcode))
            {
                throw new IsaGeneratorException("Manifest instruction Opcode must be an unsigned 32-bit integer.");
            }
            if (!RequireProperty(instruction, "InstructionFlags").TryGetUInt16(out var flags) ||
                !RequireProperty(instruction, "ExecutionLatency").TryGetByte(out var latency) ||
                !RequireProperty(instruction, "MemoryBandwidth").TryGetByte(out var bandwidth))
            {
                throw new IsaGeneratorException("Manifest instruction numeric field has an invalid range or type.");
            }

            var aliases = ReadStringArray(RequireProperty(instruction, "Aliases"), "instruction Aliases");
            if (aliases.Distinct(StringComparer.Ordinal).Count() != aliases.Count)
            {
                throw new IsaGeneratorException("Manifest instruction contains duplicate aliases.");
            }
            instructions.Add(new InstructionDeclaration(
                opcode,
                RequireString(instruction, "Mnemonic"),
                aliases,
                RequireString(instruction, "OpcodeCategory"),
                flags,
                latency,
                bandwidth,
                RequireString(instruction, "EncodingForm"),
                RequireString(instruction, "OperandSchema"),
                RequireString(instruction, "StaticClass"),
                RequireString(instruction, "SlotConstraints"),
                RequireString(instruction, "Serialization"),
                RequireString(instruction, "Privilege"),
                RequireString(instruction, "Extension"),
                RequireString(instruction, "ProviderId"),
                RequireString(instruction, "MaterializerId"),
                RequireString(instruction, "StaticEffectContract"),
                RequireString(instruction, "LatencyModelId")));
        }
        return instructions;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string context)
    {
        RequireKind(element, JsonValueKind.Array, context);
        var values = new List<string>();
        foreach (var value in element.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            {
                throw new IsaGeneratorException($"Manifest {context} must contain only non-empty strings.");
            }
            values.Add(value.GetString()!);
        }
        return values;
    }

    private static JsonElement RequireProperty(JsonElement objectElement, string name) =>
        objectElement.TryGetProperty(name, out var value)
            ? value
            : throw new IsaGeneratorException($"Manifest is missing required property '{name}'.");

    private static string RequireString(JsonElement objectElement, string name)
    {
        var value = RequireProperty(objectElement, name);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new IsaGeneratorException($"Manifest property '{name}' must be a non-empty string.");
        }
        return value.GetString()!;
    }

    private static int RequireInt32(JsonElement objectElement, string name)
    {
        var value = RequireProperty(objectElement, name);
        if (!value.TryGetInt32(out var result))
        {
            throw new IsaGeneratorException($"Manifest property '{name}' must be a 32-bit integer.");
        }
        return result;
    }

    private static void RequireExactInt32(JsonElement objectElement, string name, int expected)
    {
        if (RequireInt32(objectElement, name) != expected)
        {
            throw new IsaGeneratorException($"Manifest property '{name}' has an unsupported value.");
        }
    }

    private static void RequireExactString(JsonElement objectElement, string name, string expected)
    {
        if (!string.Equals(RequireString(objectElement, name), expected, StringComparison.Ordinal))
        {
            throw new IsaGeneratorException($"Manifest property '{name}' has an unsupported value.");
        }
    }

    private static void RequireKind(JsonElement element, JsonValueKind expected, string context)
    {
        if (element.ValueKind != expected)
        {
            throw new IsaGeneratorException($"Manifest {context} must be {expected}.");
        }
    }

    private static void RejectDuplicateProperties(JsonElement objectElement, string context)
    {
        var duplicates = objectElement.EnumerateObject()
            .GroupBy(property => property.Name, StringComparer.Ordinal)
            .Where(group => group.Skip(1).Any())
            .Select(group => group.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (duplicates.Length != 0)
        {
            throw new IsaGeneratorException($"Manifest {context} contains duplicate property '{duplicates[0]}'.");
        }
    }

    private static void RequireExactProperties(JsonElement objectElement, string context, IReadOnlySet<string> expected)
    {
        var actual = objectElement.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        var missing = expected.Where(name => !actual.Contains(name)).OrderBy(name => name, StringComparer.Ordinal).FirstOrDefault();
        var unknown = actual.Where(name => !expected.Contains(name)).OrderBy(name => name, StringComparer.Ordinal).FirstOrDefault();
        if (missing is not null || unknown is not null)
        {
            throw new IsaGeneratorException(missing is not null
                ? $"Manifest {context} is missing required property '{missing}'."
                : $"Manifest {context} contains unknown property '{unknown}'.");
        }
    }

    private static readonly IReadOnlySet<string> ManifestProperties = new HashSet<string>(StringComparer.Ordinal)
    {
        "manifestSchemaVersion", "sourceOfTruth", "isGeneratorInput", "catalogSchemaVersion", "catalogVersion", "catalogSha256", "staticPolicies", "instructionCount", "instructions",
    };
    private static readonly IReadOnlySet<string> StaticPolicyProperties = new HashSet<string>(StringComparer.Ordinal) { "Key", "Values" };
    private static readonly IReadOnlySet<string> InstructionProperties = new HashSet<string>(StringComparer.Ordinal)
    {
        "Opcode", "Mnemonic", "Aliases", "OpcodeCategory", "InstructionFlags", "ExecutionLatency", "MemoryBandwidth", "EncodingForm", "OperandSchema", "StaticClass", "SlotConstraints", "Serialization", "Privilege", "Extension", "ProviderId", "MaterializerId", "StaticEffectContract", "LatencyModelId",
    };
    private static readonly IReadOnlySet<string> CompatibilityMapProperties = new HashSet<string>(StringComparer.Ordinal)
    {
        "compatibilityMapSchemaVersion", "sourceOfTruth", "catalogSha256", "entries",
    };
    private static readonly IReadOnlySet<string> CompatibilityMapEntryProperties = new HashSet<string>(StringComparer.Ordinal) { "Name", "Opcode" };

    private static string RenderGeneratedCatalog(CatalogDocument catalog)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("// Generated by tools/HybridCPU.IsaGen from typed C# ISA declarations. Do not edit by hand.");
        builder.AppendLine($"// catalog-version: {CatalogVersion}");
        builder.AppendLine($"// catalog-sha256: {catalog.Hash}");
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine("using YAKSys_Hybrid_CPU.Arch;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine();
        builder.AppendLine("namespace YAKSys_Hybrid_CPU.Arch.Generated;");
        builder.AppendLine();
        builder.AppendLine("public readonly record struct GeneratedIsaDescriptor(");
        builder.AppendLine("    uint Opcode, string Mnemonic, string[] Aliases, OpcodeCategory OpcodeCategory, InstructionFlags InstructionFlags,");
        builder.AppendLine("    byte ExecutionLatency, byte MemoryBandwidth, string EncodingForm, string OperandSchema, InstructionClass StaticClass,");
        builder.AppendLine("    string SlotConstraints, SerializationClass Serialization, string Privilege, string Extension, string ProviderId,");
        builder.AppendLine("    string MaterializerId, string StaticEffectContract, string LatencyModelId);");
        builder.AppendLine();
        builder.AppendLine("public static class GeneratedIsaCatalog");
        builder.AppendLine("{");
        builder.AppendLine($"    public const int CatalogSchemaVersion = {CatalogSchemaVersion};");
        builder.AppendLine($"    public const string CatalogVersion = \"{CatalogVersion}\";");
        builder.AppendLine($"    public const string CatalogSha256 = \"{catalog.Hash}\";");
        builder.AppendLine("    // Compatibility names carry the C# catalog identity into existing replay contracts.");
        builder.AppendLine("    public const string ManifestVersion = CatalogVersion;");
        builder.AppendLine("    public const string ManifestSha256 = CatalogSha256;");
        builder.AppendLine("    public static readonly GeneratedIsaDescriptor[] Descriptors =");
        builder.AppendLine("    [");
        foreach (var instruction in catalog.Instructions)
        {
            var aliases = instruction.Aliases.Count == 0 ? "[]" : "[" + string.Join(", ", instruction.Aliases.Select(alias => $"\"{Escape(alias)}\"")) + "]";
            builder.AppendLine($"        new({instruction.Opcode}u, \"{Escape(instruction.Mnemonic)}\", {aliases}, OpcodeCategory.{instruction.OpcodeCategory}, (InstructionFlags){instruction.InstructionFlags}, {instruction.ExecutionLatency}, {instruction.MemoryBandwidth}, \"{Escape(instruction.EncodingForm)}\", \"{Escape(instruction.OperandSchema)}\", InstructionClass.{instruction.StaticClass}, \"{Escape(instruction.SlotConstraints)}\", SerializationClass.{instruction.Serialization}, \"{Escape(instruction.Privilege)}\", \"{Escape(instruction.Extension)}\", \"{Escape(instruction.ProviderId)}\", \"{Escape(instruction.MaterializerId)}\", \"{Escape(instruction.StaticEffectContract)}\", \"{Escape(instruction.LatencyModelId)}\"),");
        }
        builder.AppendLine("    ];");
        builder.AppendLine();
        builder.AppendLine("    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> StaticPolicies = CreateStaticPolicies();");
        builder.AppendLine("    public static readonly IReadOnlyDictionary<string, string> PipelineClassMap = CreatePipelineClassMap();");
        builder.AppendLine();
        builder.AppendLine("    public static IReadOnlySet<string> GetStaticPolicy(string policyId) => StaticPolicies.TryGetValue(policyId, out var policy) ? policy : throw new ArgumentOutOfRangeException(nameof(policyId), policyId, \"Unknown generated ISA static policy.\");");
        builder.AppendLine();
        builder.AppendLine("    private static IReadOnlyDictionary<string, IReadOnlySet<string>> CreateStaticPolicies() => new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)");
        builder.AppendLine("    {");
        foreach (var policy in catalog.StaticPolicies.Where(policy => policy.Key != nameof(IsaV4Surface.PipelineClassMap)).OrderBy(policy => policy.Key, StringComparer.Ordinal))
        {
            builder.AppendLine($"        [\"{Escape(policy.Key)}\"] = new HashSet<string>(StringComparer.Ordinal) {{ {string.Join(", ", policy.Value.Select(value => $"\"{Escape(value)}\""))} }},");
        }
        builder.AppendLine("    };");
        builder.AppendLine();
        builder.AppendLine("    private static IReadOnlyDictionary<string, string> CreatePipelineClassMap() => new Dictionary<string, string>(StringComparer.Ordinal)");
        builder.AppendLine("    {");
        foreach (var mapping in catalog.StaticPolicies[nameof(IsaV4Surface.PipelineClassMap)])
        {
            var separator = mapping.IndexOf('=');
            builder.AppendLine($"        [\"{Escape(mapping[..separator])}\"] = \"{Escape(mapping[(separator + 1)..])}\",");
        }
        builder.AppendLine("    };");
        builder.AppendLine();
        builder.AppendLine("    public static bool TryGetDescriptor(uint opcode, out GeneratedIsaDescriptor descriptor)");
        builder.AppendLine("    {");
        builder.AppendLine("        foreach (var candidate in Descriptors) if (candidate.Opcode == opcode) { descriptor = candidate; return true; }");
        builder.AppendLine("        descriptor = default; return false;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public static readonly OpcodeInfo[] Opcodes = CreateOpcodeInfos();");
        builder.AppendLine("    private static OpcodeInfo[] CreateOpcodeInfos()");
        builder.AppendLine("    {");
        builder.AppendLine("        var opcodes = new OpcodeInfo[Descriptors.Length];");
        builder.AppendLine("        for (var index = 0; index < Descriptors.Length; index++) { var descriptor = Descriptors[index]; opcodes[index] = new OpcodeInfo(descriptor.Opcode, descriptor.Mnemonic, descriptor.OpcodeCategory, descriptor.OperandSchema == \"operands-0\" ? (byte)0 : byte.Parse(descriptor.OperandSchema.AsSpan(9)), descriptor.InstructionFlags, descriptor.ExecutionLatency, descriptor.MemoryBandwidth, descriptor.StaticClass, descriptor.Serialization); }");
        builder.AppendLine("        return opcodes;");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string RenderLockFile(CatalogDocument catalog) => JsonSerializer.Serialize(new
    {
        lockSchemaVersion = LockSchemaVersion,
        catalogSchemaVersion = CatalogSchemaVersion,
        catalogVersion = CatalogVersion,
        catalogSha256 = catalog.Hash,
        staticPolicies = catalog.StaticPolicies.OrderBy(policy => policy.Key, StringComparer.Ordinal).Select(policy => new { policy.Key, Values = policy.Value }),
        instructionCount = catalog.Instructions.Count,
        instructions = catalog.Instructions,
    }, new JsonSerializerOptions { WriteIndented = true }) + "\n";

    private static string RenderManifestInput(CatalogDocument catalog) => JsonSerializer.Serialize(new
    {
        manifestSchemaVersion = ManifestInputSchemaVersion,
        sourceOfTruth = "declared-static-isa-manifest",
        isGeneratorInput = true,
        catalogSchemaVersion = CatalogSchemaVersion,
        catalogVersion = CatalogVersion,
        catalogSha256 = catalog.Hash,
        staticPolicies = catalog.StaticPolicies.OrderBy(policy => policy.Key, StringComparer.Ordinal).Select(policy => new { policy.Key, Values = policy.Value }),
        instructionCount = catalog.Instructions.Count,
        instructions = catalog.Instructions,
    }, new JsonSerializerOptions { WriteIndented = true }) + "\n";

    private static string RenderCompatibilityMapInput(CatalogDocument catalog) => JsonSerializer.Serialize(new
    {
        compatibilityMapSchemaVersion = CompatibilityMapSchemaVersion,
        sourceOfTruth = "csharp-compatibility-facade-map",
        catalogSha256 = catalog.Hash,
        entries = catalog.Instructions.Select(instruction => new
        {
            Name = GetCSharpCompatibilityName(instruction.Mnemonic),
            Opcode = instruction.Opcode,
        }).Append(new { Name = "Nope", Opcode = 0u }).OrderBy(entry => entry.Opcode),
    }, new JsonSerializerOptions { WriteIndented = true }) + "\n";

    private static string GetCSharpCompatibilityName(string mnemonic) =>
        string.Equals(mnemonic, "CSRCLR", StringComparison.Ordinal) ? "CSR_CLEAR" : mnemonic.Replace('.', '_');

    private static string RenderGeneratedCompatibilitySidecar(CompatibilityMapDocument compatibilityMap)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("// Generated by tools/HybridCPU.IsaGen from the validated C# compatibility map. Do not edit by hand.");
        builder.AppendLine($"// catalog-sha256: {compatibilityMap.CatalogSha256}");
        builder.AppendLine();
        builder.AppendLine("namespace YAKSys_Hybrid_CPU");
        builder.AppendLine("{");
        builder.AppendLine("    public partial struct Processor");
        builder.AppendLine("    {");
        builder.AppendLine("        public sealed partial class CPU_Core");
        builder.AppendLine("        {");
        builder.AppendLine("            public static partial class IsaOpcodeValues");
        builder.AppendLine("            {");
        foreach (var entry in compatibilityMap.Entries)
        {
            builder.AppendLine($"                public const ushort {entry.Name} = {entry.Opcode};");
        }
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            public enum InstructionsEnum : ushort");
        builder.AppendLine("            {");
        foreach (var entry in compatibilityMap.Entries)
        {
            builder.AppendLine($"                {entry.Name} = {entry.Opcode},");
        }
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void VerifyGeneratedCatalogParity(CatalogDocument catalog)
    {
        if (GeneratedIsaCatalog.Descriptors.Length != catalog.Instructions.Count)
        {
            throw new IsaGeneratorException("Generated C# catalog row count differs from the typed registry.");
        }
        for (var index = 0; index < catalog.Instructions.Count; index++)
        {
            var expected = catalog.Instructions[index];
            var actual = GeneratedIsaCatalog.Descriptors[index];
            if (actual.Opcode != expected.Opcode || !string.Equals(actual.Mnemonic, expected.Mnemonic, StringComparison.Ordinal) ||
                !actual.Aliases.SequenceEqual(expected.Aliases, StringComparer.Ordinal) || actual.OpcodeCategory.ToString() != expected.OpcodeCategory ||
                actual.InstructionFlags != (InstructionFlags)expected.InstructionFlags || actual.ExecutionLatency != expected.ExecutionLatency ||
                actual.MemoryBandwidth != expected.MemoryBandwidth || !string.Equals(actual.EncodingForm, expected.EncodingForm, StringComparison.Ordinal) ||
                !string.Equals(actual.OperandSchema, expected.OperandSchema, StringComparison.Ordinal) || actual.StaticClass.ToString() != expected.StaticClass ||
                !string.Equals(actual.SlotConstraints, expected.SlotConstraints, StringComparison.Ordinal) || actual.Serialization.ToString() != expected.Serialization ||
                !string.Equals(actual.Privilege, expected.Privilege, StringComparison.Ordinal) || !string.Equals(actual.Extension, expected.Extension, StringComparison.Ordinal) ||
                !string.Equals(actual.ProviderId, expected.ProviderId, StringComparison.Ordinal) || !string.Equals(actual.MaterializerId, expected.MaterializerId, StringComparison.Ordinal) ||
                !string.Equals(actual.StaticEffectContract, expected.StaticEffectContract, StringComparison.Ordinal) || !string.Equals(actual.LatencyModelId, expected.LatencyModelId, StringComparison.Ordinal))
            {
                throw new IsaGeneratorException($"Generated C# catalog differs at opcode {expected.Opcode} ({expected.Mnemonic}). Regenerate and review the static declaration delta.");
            }
        }
        Console.WriteLine($"HybridCPU C# ISA parity passed ({catalog.Instructions.Count} registry rows).");
    }

    private static void RunSelfTests(CatalogDocument catalog, string generated, string compatibilityGenerated, string lockFile)
    {
        if (catalog.Hash.Length != 64 || !generated.Contains("catalog-sha256", StringComparison.Ordinal) || !lockFile.Contains("catalogSha256", StringComparison.Ordinal))
        {
            throw new IsaGeneratorException("Self-test did not produce deterministic C# catalog projections.");
        }
        if (!compatibilityGenerated.Contains("public static partial class IsaOpcodeValues", StringComparison.Ordinal) ||
            !compatibilityGenerated.Contains("public const ushort Nope = 0;", StringComparison.Ordinal))
        {
            throw new IsaGeneratorException("Self-test did not produce the compatibility sidecar preview.");
        }
        if (!compatibilityGenerated.Contains("public enum InstructionsEnum : ushort", StringComparison.Ordinal) ||
            !compatibilityGenerated.Contains("Nope = 0,", StringComparison.Ordinal))
        {
            throw new IsaGeneratorException("Self-test did not produce the generated InstructionsEnum declaration.");
        }
        ValidateCatalog(catalog.Instructions, catalog.StaticPolicies);
        VerifyGeneratedCatalogParity(catalog);
        VerifyStrictManifestReader(catalog);
        VerifyStrictCompatibilityMapReader(catalog);
        Console.WriteLine("HybridCPU C# ISA generator self-test passed.");
    }

    private static void VerifyStrictManifestReader(CatalogDocument catalog)
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"hybridcpu-isagen-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            var validPath = Path.Combine(temporaryDirectory, "valid.json");
            var manifest = RenderManifestInput(catalog);
            File.WriteAllText(validPath, manifest, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            var parsed = ReadStrictManifest(validPath);
            if (parsed.Instructions.Count != catalog.Instructions.Count || !string.Equals(parsed.Hash, catalog.Hash, StringComparison.Ordinal))
            {
                throw new IsaGeneratorException("Strict manifest reader did not preserve the canonical catalog.");
            }

            AssertManifestRejected(Path.Combine(temporaryDirectory, "missing.json"), "missing");
            var malformedPath = Path.Combine(temporaryDirectory, "malformed.json");
            File.WriteAllText(malformedPath, "{", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            AssertManifestRejected(malformedPath, "malformed JSON");

            var duplicatePropertyPath = Path.Combine(temporaryDirectory, "duplicate-property.json");
            var duplicateProperty = manifest.Replace(
                $"\"catalogVersion\": \"{CatalogVersion}\",",
                $"\"catalogVersion\": \"{CatalogVersion}\",\n  \"catalogVersion\": \"{CatalogVersion}\",",
                StringComparison.Ordinal);
            if (string.Equals(duplicateProperty, manifest, StringComparison.Ordinal))
            {
                throw new IsaGeneratorException("Self-test could not construct duplicate-property manifest.");
            }
            File.WriteAllText(duplicatePropertyPath, duplicateProperty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            AssertManifestRejected(duplicatePropertyPath, "duplicate property");

            var duplicateOpcodePath = Path.Combine(temporaryDirectory, "duplicate-opcode.json");
            var firstOpcode = catalog.Instructions[0].Opcode;
            var secondOpcode = catalog.Instructions[1].Opcode;
            var secondOpcodeText = $"\"Opcode\": {secondOpcode}";
            var secondOpcodeIndex = manifest.IndexOf(secondOpcodeText, StringComparison.Ordinal);
            if (secondOpcodeIndex < 0)
            {
                throw new IsaGeneratorException("Self-test could not locate a distinct opcode declaration.");
            }
            var duplicateOpcode = manifest.Remove(secondOpcodeIndex, secondOpcodeText.Length).Insert(secondOpcodeIndex, $"\"Opcode\": {firstOpcode}");
            File.WriteAllText(duplicateOpcodePath, duplicateOpcode, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            AssertManifestRejected(duplicateOpcodePath, "duplicate opcode or mnemonic");

            var hashDriftPath = Path.Combine(temporaryDirectory, "hash-drift.json");
            var driftedHash = (catalog.Hash[0] == '0' ? "1" : "0") + catalog.Hash[1..];
            File.WriteAllText(hashDriftPath, manifest.Replace(catalog.Hash, driftedHash, StringComparison.Ordinal), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            AssertManifestRejected(hashDriftPath, "hash drift");
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static void AssertManifestRejected(string path, string expectedDiagnostic)
    {
        try
        {
            _ = ReadStrictManifest(path);
        }
        catch (IsaGeneratorException exception) when (exception.Message.Contains(expectedDiagnostic, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        catch (IsaGeneratorException exception)
        {
            throw new IsaGeneratorException($"Strict manifest self-test expected '{expectedDiagnostic}' but received '{exception.Message}'.", exception);
        }
        throw new IsaGeneratorException($"Strict manifest self-test accepted an invalid manifest: {path}.");
    }

    private static void VerifyStrictCompatibilityMapReader(CatalogDocument catalog)
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"hybridcpu-isagen-compat-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            var map = RenderCompatibilityMapInput(catalog);
            var validPath = Path.Combine(temporaryDirectory, "valid.json");
            File.WriteAllText(validPath, map, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            var parsed = ReadStrictCompatibilityMap(validPath, catalog);
            if (parsed.Entries.Count != catalog.Instructions.Count + 1 || !string.Equals(parsed.CatalogSha256, catalog.Hash, StringComparison.Ordinal))
            {
                throw new IsaGeneratorException("Strict compatibility-map reader did not preserve the expected map.");
            }

            AssertCompatibilityMapRejected(Path.Combine(temporaryDirectory, "missing.json"), catalog, "missing");
            var malformedPath = Path.Combine(temporaryDirectory, "malformed.json");
            File.WriteAllText(malformedPath, "{", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            AssertCompatibilityMapRejected(malformedPath, catalog, "malformed JSON");

            var duplicatePath = Path.Combine(temporaryDirectory, "duplicate.json");
            var duplicate = map.Replace("\"Name\": \"ADD\"", "\"Name\": \"SUB\"", StringComparison.Ordinal);
            File.WriteAllText(duplicatePath, duplicate, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            AssertCompatibilityMapRejected(duplicatePath, catalog, "duplicate name");

            var hashDriftPath = Path.Combine(temporaryDirectory, "hash-drift.json");
            var driftedHash = (catalog.Hash[0] == '0' ? "1" : "0") + catalog.Hash[1..];
            File.WriteAllText(hashDriftPath, map.Replace(catalog.Hash, driftedHash, StringComparison.Ordinal), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            AssertCompatibilityMapRejected(hashDriftPath, catalog, "hash drift");

            var sentinelPath = Path.Combine(temporaryDirectory, "sentinel.json");
            File.WriteAllText(sentinelPath, map.Replace("\"Name\": \"Nope\"", "\"Name\": \"Nop\"", StringComparison.Ordinal), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            AssertCompatibilityMapRejected(sentinelPath, catalog, "Nope = 0 sentinel");

            var setPath = Path.Combine(temporaryDirectory, "set.json");
            File.WriteAllText(setPath, map.Replace("\"Opcode\": 39", "\"Opcode\": 52", StringComparison.Ordinal), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            AssertCompatibilityMapRejected(setPath, catalog, "opcode set");
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static void AssertCompatibilityMapRejected(string path, CatalogDocument catalog, string expectedDiagnostic)
    {
        try
        {
            _ = ReadStrictCompatibilityMap(path, catalog);
        }
        catch (IsaGeneratorException exception) when (exception.Message.Contains(expectedDiagnostic, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        catch (IsaGeneratorException exception)
        {
            throw new IsaGeneratorException($"Strict compatibility-map self-test expected '{expectedDiagnostic}' but received '{exception.Message}'.", exception);
        }
        throw new IsaGeneratorException($"Strict compatibility-map self-test accepted an invalid map: {path}.");
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string FindRepositoryRoot(string currentDirectory)
    {
        for (var directory = new DirectoryInfo(currentDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")) && Directory.Exists(Path.Combine(directory.FullName, "isa"))) return directory.FullName;
        }
        throw new IsaGeneratorException("Cannot locate repository root; pass --root <path>.");
    }

    private static void WriteDeterministic(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void AssertMatches(string path, string expected)
    {
        if (!File.Exists(path) || !string.Equals(File.ReadAllText(path), expected, StringComparison.Ordinal)) throw new IsaGeneratorException($"Generated artifact is stale: {path}. Run dotnet run --project ./tools/HybridCPU.IsaGen.");
    }

    private sealed record CatalogDocument(IReadOnlyList<InstructionDeclaration> Instructions, IReadOnlyDictionary<string, IReadOnlyList<string>> StaticPolicies, string Hash);
    private sealed record InstructionDeclaration(uint Opcode, string Mnemonic, IReadOnlyList<string> Aliases, string OpcodeCategory, ushort InstructionFlags, byte ExecutionLatency, byte MemoryBandwidth, string EncodingForm, string OperandSchema, string StaticClass, string SlotConstraints, string Serialization, string Privilege, string Extension, string ProviderId, string MaterializerId, string StaticEffectContract, string LatencyModelId);
    private sealed record CompatibilityMapDocument(IReadOnlyList<CompatibilityMapEntry> Entries, string CatalogSha256);
    private sealed record CompatibilityMapEntry(string Name, ushort Opcode);
    private sealed class IsaGeneratorException : Exception
    {
        public IsaGeneratorException(string message) : base(message) { }
        public IsaGeneratorException(string message, Exception innerException) : base(message, innerException) { }
    }

    private sealed record GeneratorOptions(bool Check, bool SelfTest, bool VerifyRegistryParity, string? ValidateManifestPath, string? ValidateCompatibilityMapPath, string? Root)
    {
        public static GeneratorOptions Parse(IReadOnlyList<string> args)
        {
            var check = false; var selfTest = false; var verifyRegistryParity = false; string? validateManifestPath = null; string? validateCompatibilityMapPath = null; string? root = null;
            for (var index = 0; index < args.Count; index++)
            {
                switch (args[index])
                {
                    case "--check": check = true; break;
                    case "--self-test": selfTest = true; break;
                    case "--verify-registry-parity": verifyRegistryParity = true; break;
                    case "--validate-manifest" when index + 1 < args.Count: validateManifestPath = args[++index]; break;
                    case "--validate-compatibility-map" when index + 1 < args.Count: validateCompatibilityMapPath = args[++index]; break;
                    case "--root" when index + 1 < args.Count: root = Path.GetFullPath(args[++index]); break;
                    default: throw new IsaGeneratorException($"Unknown generator option '{args[index]}'.");
                }
            }
            var operationCount = (check ? 1 : 0) + (selfTest ? 1 : 0) + (verifyRegistryParity ? 1 : 0) + (validateManifestPath is null ? 0 : 1) + (validateCompatibilityMapPath is null ? 0 : 1);
            if (operationCount > 1) throw new IsaGeneratorException("Only one generator operation may be selected.");
            return new GeneratorOptions(check, selfTest, verifyRegistryParity, validateManifestPath, validateCompatibilityMapPath, root);
        }
    }
}
