using System.Text.Json;
using HybridCpu.ShadowFrontend;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace ShadowFrontend.Tests;

// This assembly is the only outward dependency on production ISE. No CPU instance,
// scheduler, memory, GUI or mutable materializer is involved.
internal sealed class OracleBridge : ICanonicalDecoder
{
    public long Calls;
    public VliwBundleAnnotations? Annotations { get; set; }
    public Action? AfterDecode;
    public bool ThrowInvariant;
    public CanonicalBundle? LastBundle;
    public DecodeResult Decode(FrozenBytes raw, BundleRequest request, DecodeContext context)
    {
        Calls++;
        if (ThrowInvariant) throw new InvalidOperationException("Injected oracle invariant failure");
        var slots = ReadRaw(raw);
        bool ok = DeclarativeDecoderPipeline.TryDecodeBundle(slots, Annotations, request.Pc,
            request.TransportSerial, NativeContext(context), out var decoded, out var failure);
        LastBundle = decoded?.CanonicalBundle;
        var result = ok ? Project(decoded!.CanonicalBundle) : DecodeResult.Illegal(raw,
            new((ushort)failure!.Code, failure.SlotIndex, failure.Field, failure.RawHash, failure.Message));
        AfterDecode?.Invoke();
        return result;
    }
    public static VLIW_Instruction[] ReadRaw(FrozenBytes raw)
    {
        var slots = new VLIW_Instruction[8]; var bytes = raw.Copy();
        for (int i = 0; i < 8; i++)
            if (!slots[i].TryReadBytes(bytes, i * 32)) throw new InvalidOperationException("Raw oracle fixture malformed");
        return slots;
    }
    public static FrozenBytes Bytes(VLIW_Instruction[] slots)
    {
        var bytes = new byte[256];
        for (int i = 0; i < 8; i++) slots[i].TryWriteBytes(bytes.AsSpan(i * 32, 32));
        return new(bytes);
    }
    public static FrozenBytes Fixture(int occupancy = 2, ushort opcode = IsaOpcodeValues.ADD, int offset = 0)
    {
        var slots = new VLIW_Instruction[8];
        for (int i = 0; i < occupancy; i++) slots[(i + offset) % 8] = new()
        { OpCode = opcode, DestSrc1Pointer = VLIW_Instruction.PackArchRegs(3, 1, 2) };
        return Bytes(slots);
    }
    public static Snapshot Snap(CanonicalPayloadSnapshot value) => new(value.Kind, new(value.Content.Span));
    public static Snapshot Object(string kind, object? value) => new(kind, new(JsonSerializer.SerializeToUtf8Bytes(value, value?.GetType() ?? typeof(object))));
    public static DecodeResult Project(CanonicalBundle bundle) => DecodeResult.Success(
        new(bundle.SemanticKey.RawBundleBytes.Span), bundle.Slots.Select(s => new SemanticSlot(
            s.SlotIndex, s.IsOccupied, s.Opcode, (int?)s.InstructionClass, (int?)s.SerializationClass,
            s.Rd, s.Rs1, s.Rs2, s.Immediate, s.CsrAddress, s.AcquireOrdering, s.ReleaseOrdering,
            Snap(s.RawSlot), Snap(s.InstructionPayload), Snap(s.SlotSideband),
            Object("GeneratedStaticBinding", s.StaticBinding), Object("CanonicalScalarLoadAddressPlan", s.ScalarLoadAddressPlan))),
        Snap(bundle.BundleSideband));
    public static CanonicalDecodeContext NativeContext(DecodeContext c) => new()
    {
        ManifestVersion = c.ManifestVersion, ManifestHash = c.ManifestHash,
        ExtensionConfigurationFingerprint = c.Extensions, DecoderEpoch = c.DecoderEpoch,
        DecoderVersion = c.DecoderVersion, PrivilegeContext = c.Privilege,
        DomainIdentity = c.Domain, AddressSpaceIdentity = c.AddressSpace,
        VectorConfigurationFingerprint = c.VectorConfiguration,
        ExecutableMemoryInvalidationEpoch = c.ExecutableEpoch, CodeGenerationEpoch = c.CodeEpoch,
        IsReplayEligible = c.ReplayEligible
    };
    // Exercises actual production semantic LoopBuffer API. Live carriers are inspected
    // only by the existing API; nulls represent no donor observations in this fixture.
    public static LoopBuffer Load(CanonicalBundle bundle, ulong iterations, DecodeContext? context = null)
    {
        var buffer = new LoopBuffer(); buffer.Initialize();
        buffer.BeginSemanticLoad(bundle.BundleAddress, iterations, ReplayEntry.Create(bundle),
            new MicroOp?[8], NativeContext(context ?? Context()));
        buffer.CommitLoad(); return buffer;
    }
    public static DecodeContext Context() => new("1", "test-manifest", "unconditional", "oracle-v1",
        "VliwDecoderV4-canonical", "machine", "domain-a", "space-a", "vector-default", 0, 0,
        true, "incarnation-a", "default-sideband");
}
internal sealed class TestSource : ICanonicalSource
{
    public FrozenBytes Raw = OracleBridge.Fixture();
    public long Calls, AccessCalls;
    public bool Allowed = true;
    public Func<BundleRequest, FrozenBytes>? Select { get; set; }
    public string? FetchFault { get; set; }
    public HashSet<ulong> DeniedExecutablePages { get; } = [];
    public Action? OnAccess;
    public AccessDecision CheckAccess(BundleRequest r, DecodeContext c)
    {
        AccessCalls++; OnAccess?.Invoke();
        if (!Allowed || DeniedExecutablePages.Contains(r.Pc / 4096) || DeniedExecutablePages.Contains((r.Pc + 255) / 4096))
            return new(false, "execute-permission");
        return AccessDecision.Permit;
    }
    public CanonicalFetchResult FetchFullBundle(BundleRequest r, DecodeContext c)
    { Calls++; return FetchFault is null ? new(Select?.Invoke(r) ?? Raw, null) : new(null, FetchFault); }
}
