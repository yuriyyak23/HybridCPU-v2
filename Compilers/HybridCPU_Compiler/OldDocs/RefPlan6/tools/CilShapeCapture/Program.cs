using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length != 2 || !File.Exists(args[0]) || string.IsNullOrWhiteSpace(args[1]))
{
    Console.Error.WriteLine("usage: CilShapeCapture <assembly> <configuration>");
    return 2;
}

byte[] image = File.ReadAllBytes(args[0]);
using var pe = new PEReader(new MemoryStream(image, writable: false));
if (!pe.HasMetadata)
{
    Console.Error.WriteLine("input has no managed metadata");
    return 3;
}

MetadataReader metadata = pe.GetMetadataReader();
var methods = new List<CilMethodShape>();
foreach (TypeDefinitionHandle typeHandle in metadata.TypeDefinitions)
{
    TypeDefinition type = metadata.GetTypeDefinition(typeHandle);
    string typeName = metadata.GetString(type.Name);
    string typeNamespace = metadata.GetString(type.Namespace);
    if (!string.Equals(typeNamespace, "HybridCPU.RefPlan6.Corpus", StringComparison.Ordinal) ||
        !string.Equals(typeName, "ControlFlowCorpus", StringComparison.Ordinal))
        continue;

    foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
    {
        MethodDefinition method = metadata.GetMethodDefinition(methodHandle);
        if (method.RelativeVirtualAddress == 0)
            continue;
        MethodBodyBlock body = pe.GetMethodBody(method.RelativeVirtualAddress);
        byte[] il = body.GetILBytes() ?? throw new BadImageFormatException("Method body has no IL bytes.");
        methods.Add(new(
            metadata.GetString(method.Name),
            $"0x{MetadataTokens.GetToken(methodHandle):x8}",
            body.MaxStack,
            body.LocalVariablesInitialized,
            body.LocalSignature.IsNil ? null : $"0x{MetadataTokens.GetToken(body.LocalSignature):x8}",
            body.ExceptionRegions.Length,
            il.Length,
            Sha256(il),
            Convert.ToHexString(il).ToLowerInvariant()));
    }
}

var artifact = new
{
    schema = "HybridCPU.RefPlan6.CilShapeCorpusV1",
    configuration = args[1],
    sdk = "10.0.204",
    roslyn = "5.3.0-2.26230.114+e7aa4b537d95ae955b5c98c25fa9b9220e7eb71e",
    assemblySha256 = Sha256(image),
    methods = methods.OrderBy(static method => method.Name, StringComparer.Ordinal)
};
Console.WriteLine(JsonSerializer.Serialize(artifact, new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
}));
return 0;

static string Sha256(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

internal sealed record CilMethodShape(
    string Name,
    string Token,
    int MaxStack,
    bool InitLocals,
    string? LocalSignatureToken,
    int ExceptionRegionCount,
    int IlBytes,
    string IlSha256,
    string IlHex);
