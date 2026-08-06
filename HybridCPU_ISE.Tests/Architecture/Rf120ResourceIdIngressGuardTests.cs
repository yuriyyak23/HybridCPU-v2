using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.CloseToHSL.Memory.Timing;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.0 closed-world inventory guards. These tests freeze source surfaces and
/// known compatibility/normalization seams; they do not approve a taxonomy or
/// change invalid-input behavior.
/// </summary>
public sealed class Rf120ResourceIdIngressGuardTests
{
    private const BindingFlags PublicConstructors =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;

    private static readonly InventoryFamily[] Families =
    [
        new("virtual-thread-owner",
            @"\b(?:VtId|VtID|VirtualThreadId|VirtualThreadID|OwnerThreadId|OwnerVirtualThreadId|CarrierVirtualThreadId|DonorVirtualThreadId|TargetVirtualThreadId|SourceVirtualThreadId|ActiveVirtualThreadId|OriginalThreadId|ThreadId)\b",
            2252, "af435c7eb7ac453456a31312d92af669d511e67adf2db9e4c5c87b049343c847"),
        new("register-resource",
            @"\b(?:ArchRegId|PhysRegId|RegId|RegID|RegisterId|RegisterID|DestRegID|SrcRegID|Src1RegID|Src2RegID|BaseRegID|DestRegId|SourceRegisterId|DestinationRegisterId)\b",
            961, "a77e73c218f9ddd252dc79c164919209f270aa708fc90139d486156d20e4f3b8"),
        new("slot-lane-pinning",
            @"\b(?:SlotId|SlotID|SlotIndex|slotIndex|WorkingSlotIndex|SourceSlotIndex|LaneId|LaneID|LaneIndex|laneIndex|PhysicalLane|physicalLane|PinnedLaneId|PinnedSlot|IssueSlot|OccupiedLaneIndex)\b",
            2685, "8dd9ad7a9803f8bec480b4ecc38796f1e3f7ce106fb02c472653c57f2b77979f"),
        new("memory-bank",
            @"\b(?:MemoryBankId|BankId|BankID|bankId|BankIndex|bankIndex|ResolveSchedulerVisibleBankId|IsResolvedSchedulerVisibleBankId|ResolveBankId|UninitializedSchedulerVisibleBankId)\b",
            500, "c4769e86fafea26800b750fc6c10c5864a1a5cc4da7a17914f3b0373109fd59d"),
        new("dma-stream-device-queue",
            @"\b(?:ChannelId|ChannelID|channelId|channelID|StreamId|StreamID|streamId|EngineId|engineId|DeviceId|DeviceID|deviceId|deviceID|AcceleratorDeviceId|QueueId|QueueID|queueId|GuestQueueId|VirtualQueueId|DmaStreamComputeDsc2CapabilityId)\b",
            905, "710991d9e5487224d2ddff31d2789f40a71a2fc4d6cb98fe0664977e6e4cc633"),
        new("domain-context-tag",
            @"\b(?:DomainId|DomainID|domainId|DomainTag|domainTag|OwnerContext|OwnerContextId|ContextId|CertificateId|ActiveDomainCertificate|AddressSpaceId|AddressSpaceTag|IoDomainKey|IoDomainTag|IotlbTag|NestedTlbTag|SecurityContext|VerificationContext)\b",
            2184, "bb728b220719bf0de4226f2ef75cbccb61b2a07ec2364c741e2a6d47acbbdb3b"),
        new("token-generation-request",
            @"\b(?:TokenId|TokenID|tokenId|TokenHandle|tokenHandle|AcceleratorTokenHandle|DmaStreamComputeTokenHandle|MemoryRequestId|MemoryRequestToken|DMATransferToken|GuestTokenId|VirtualTokenId|Generation|generation|BackendGeneration|QueueEpoch|FenceEpoch|TokenEpoch|CompletionEpoch|MappingEpoch|DomainEpoch|RuntimeEpoch|CodeGenerationEpoch)\b",
            928, "a4ec15c54e11cfdd209850e134eb5abe0726cf1d08018063d6dc7db794f3cca3"),
        new("replay-certificate-identity",
            @"\b(?:ReplayToken|ReplayPhaseKey|ReplayPhaseContext|PhaseCertificateTemplateKey|SemanticInstructionKey|VliwOperationId|PostStageBIssuedAttempt|BundleResourceCertificateIdentity|BundleResourceCertificateIdentity4Way|PipelineContourCertificate|MemoryRequestId|OperationAttempt|BundleSerial|WorkingBundleSequence|ReplayEpoch|EvidenceIdentity|DescriptorIdentityHash)\b",
            1391, "635f63f16dee45ca282ed792e1ed0c28eaa512f3dffcc5018e5dae0019d3dabd"),
        new("serialization-reflection-testsupport",
            @"\b(?:JsonSerializer|Serialize|Deserialize|BinaryWriter|BinaryReader|GetField|GetFields|GetProperty|GetProperties|SetValue|Activator\.CreateInstance|TestSet|TestPrime|TestReload|TestInvalidate|TestLoad|TestSeed|TestExecute|TestRetire|TestConsume|TestRead)\b",
            725, "0cc1c7339bfc877e260dfe85773cc26f27e4241af3e9aefb55c2f4948716125a")
    ];


    [Fact]
    public void ExistingCheckedTypesAndUncheckedPublicConstructionSeamsRemainExplicit()
    {
        Assert.Equal(3, VtId.MaxValue);
        Assert.Throws<ArgumentOutOfRangeException>(() => VtId.Create(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => VtId.Create(4));
        Assert.Equal(16, MemoryBankId.BankCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => new MemoryBankId(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MemoryBankId(16));

        Assert.True(new AcceleratorTokenHandle(0).Equals(AcceleratorTokenHandle.Invalid));
        Assert.False(new AcceleratorTokenHandle(0).IsValid);
        Assert.Single(typeof(AcceleratorTokenHandle).GetConstructors(PublicConstructors));

        Assert.False(new MemoryRequestId(0).IsValid);
        Assert.Single(typeof(MemoryRequestId).GetConstructors(PublicConstructors));

        DmaStreamComputeTokenHandle defaultDscHandle = new(0, 0, 0, 0, 0, 0, 0, 0);
        Assert.True(defaultDscHandle.IsDefault);
        Assert.Single(typeof(DmaStreamComputeTokenHandle).GetConstructors(PublicConstructors));

        Assert.Empty(typeof(VliwOperationId).GetConstructors(PublicConstructors));
    }

    [Fact]
    public void AmbiguousAliasesFallbacksAndCrossFamilySeamsRemainInventoried()
    {
        string root = FindRepositoryRoot();
        string production = ReadProductionAndCompilerSources(root);

        Assert.DoesNotMatch(
            @"\b(?:record\s+struct|struct|class)\s+VirtualThreadId\b",
            production);
        Assert.DoesNotMatch(
            @"\b(?:record\s+struct|struct|class)\s+(?:ChannelId|DomainId|TokenId)\b",
            production);

        Assert.Contains("microOp?.VirtualThreadId ?? 0", production, StringComparison.Ordinal);
        Assert.Contains("microOp?.OwnerThreadId ?? 0", production, StringComparison.Ordinal);
        Assert.Contains("trapMicroOp.VirtualThreadId != 0", production, StringComparison.Ordinal);
        Assert.Contains("ForMemoryDomain(OwnerThreadId)", production, StringComparison.Ordinal);
        Assert.Contains("ForMemoryDomain(slot.OwnerThreadId)", production, StringComparison.Ordinal);

        Assert.Contains("UninitializedSchedulerVisibleBankId = -1", production, StringComparison.Ordinal);
        Assert.Contains("bankWidthBytes > 0", production, StringComparison.Ordinal);
        Assert.Contains("numBanks > 0", production, StringComparison.Ordinal);
        Assert.Contains("DefaultBankWidthBytes", production, StringComparison.Ordinal);
        Assert.Contains("DefaultNumBanks", production, StringComparison.Ordinal);

        Assert.Contains("guestQueueId == 0 ? BuildDefaultGuestQueueId", production, StringComparison.Ordinal);
        Assert.Contains("tokenId == 0 ? descriptor.DescriptorIdentityHash : tokenId", production,
            StringComparison.Ordinal);
        Assert.Contains("Generation = generation == 0 ? 1 : generation", production,
            StringComparison.Ordinal);
        Assert.Contains("FirstTokenId = firstTokenId == 0 ? 1 : firstTokenId", production,
            StringComparison.Ordinal);
        Assert.Contains("mappingEpoch == 0 ? 1 : mappingEpoch", production,
            StringComparison.Ordinal);
        Assert.Contains("backendGeneration == 0 ? bindingEpoch : backendGeneration", production,
            StringComparison.Ordinal);

        Assert.Contains("TokenId = token?.TokenId ?? 0", production, StringComparison.Ordinal);
        Assert.Contains("TokenHandle = token?.Handle.Value ?? 0", production, StringComparison.Ordinal);
        Assert.Contains("public int OwnerThreadId { get; set; } = 0", production, StringComparison.Ordinal);
        Assert.Contains("public byte PinnedLaneId { get; }", production, StringComparison.Ordinal);
        Assert.Contains("public int MemoryBankId", production, StringComparison.Ordinal);
    }

    [Fact]
    public void Rf121PaperAuthorityDefinesFamiliesAbsenceAndBankResolutionWithoutAuthorityInflation()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains("### 3.7 Checked Identifier Families and Absence Boundaries", paper,
            StringComparison.Ordinal);
        Assert.Contains("The existing `VtId` is the sole SMT identity", paper,
            StringComparison.Ordinal);
        Assert.Contains("`SlotId` denotes only a canonical or working bundle position `0..7`", paper,
            StringComparison.Ordinal);
        Assert.Contains("`LaneId` denotes only a post-Stage-B physical lane `0..7`", paper,
            StringComparison.Ordinal);
        Assert.Contains("`MemoryBankResolution` is a three-way result", paper,
            StringComparison.Ordinal);
        Assert.Contains("`Resolved(MemoryBankId)`, `UnavailableTopology`, or `InvalidGeometry`", paper,
            StringComparison.Ordinal);
        Assert.Contains("`DmaChannelId` is local to the persistent DMA controller", paper,
            StringComparison.Ordinal);
        Assert.Contains("`StreamEngineId` is local to stream-resource selection", paper,
            StringComparison.Ordinal);
        Assert.Contains("no universal `DomainId` is introduced", paper,
            StringComparison.Ordinal);
        Assert.Contains("Valid-input signature migration precedes any changed invalid-input", paper,
            StringComparison.Ordinal);
        Assert.Contains("not grant legality, ownership, admission, execution, completion", paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentLedgerRetainsClosedSlicesAndReopensOnlyLaneIdPinningInventory()
    {
        string root = FindRepositoryRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "11_RF12",
            "00_ENTRY_STATUS_AND_ROADMAP.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
            "rf12.4p-csr-execution-source-owner-vt-valid-input-cutover.md");

        Assert.Contains("RF-12.0 | closed inventory/freeze", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12 is closed at RF-12.12h", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.1 | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.2a | closed valid-input contract", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.2b | closed valid-input contract", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.2c | closed valid-input contract", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.3a | closed valid-input contract", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.3b | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3c | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3d | closed invalid-input behavior", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3e | closed invalid-input behavior", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3f | closed compatibility removal", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3g | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3h | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3i | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3j | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3k | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3l | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3m | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3n | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3o | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3p | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3q | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3r | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3s | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3t | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3u | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3v | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3w | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3x | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3y | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3z | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3aa | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3ab | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3ac | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3ad | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3ae | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3af | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3ag | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3ah | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3ai | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3aj | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3ak | closed caller closure audit", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4 | closed inventory/freeze", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4a | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4b | closed valid-input contract", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4c | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4d | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4e | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4f | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4g | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4h | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4i | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4j | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4k | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4l | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4m | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4n | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4o | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4p | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.5 | closed inventory/freeze", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.5a | closed valid-input contract", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.5b | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.6a | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.6b | closed valid-input contract", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.6c | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.6d | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.12b | closed reconciliation", ledger,
            StringComparison.Ordinal);
        Assert.Contains("## Preserved invariants and limitations", evidence,
            StringComparison.Ordinal);
        Assert.Contains("ActiveResolverUsesCheckedProjectionWithExactRawInvalidArm",
            Read(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf124pCsrExecutionSourceOwnerVtValidInputCutoverTests.cs"),
            StringComparison.Ordinal);
        Assert.Contains("The single next task is **RF-12.5", evidence,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Rf122aVtIdCoreValidInputSignaturesPreserveAllValidRepresentations()
    {
        for (int rawValue = VtId.MinValue; rawValue <= VtId.MaxValue; rawValue++)
        {
            Assert.True(VtId.IsRepresentable(rawValue));

            VtId fromConstructor = new((byte)rawValue);
            VtId fromCreate = VtId.Create(rawValue);
            VtId fromWire = VtId.FromRawValue((byte)rawValue);
            VtId fromCast = (VtId)rawValue;

            Assert.True(VtId.TryCreate(rawValue, out VtId fromTryCreate));
            Assert.Equal(fromConstructor, fromCreate);
            Assert.Equal(fromConstructor, fromWire);
            Assert.Equal(fromConstructor, fromCast);
            Assert.Equal(fromConstructor, fromTryCreate);
            Assert.Equal((byte)rawValue, fromWire.ToRawValue());
            Assert.Equal(rawValue, (int)fromWire);

            string json = JsonSerializer.Serialize(fromWire);
            Assert.Equal(fromWire, JsonSerializer.Deserialize<VtId>(json));
        }
    }

    [Fact]
    public void Rf122aVtIdExistingInvalidBehaviorAndCallerBoundaryRemainUnchanged()
    {
        Assert.False(VtId.IsRepresentable(-1));
        Assert.False(VtId.IsRepresentable(VtId.SmtWayCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VtId((byte)VtId.SmtWayCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => VtId.Create(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => VtId.Create(VtId.SmtWayCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => VtId.FromRawValue((byte)VtId.SmtWayCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => (VtId)(-1));

        Assert.False(VtId.TryCreate(-1, out VtId negativeResult));
        Assert.Equal(default, negativeResult);
        Assert.Equal(VtId.MinValue, negativeResult.Value);
        Assert.False(VtId.TryCreate(VtId.SmtWayCount, out VtId highResult));
        Assert.Equal(default, highResult);
        Assert.Equal(VtId.MinValue, highResult.Value);

        string root = FindRepositoryRoot();
        string productionAndCompiler = ReadProductionAndCompilerSources(root);
        Assert.Equal(0, Count(productionAndCompiler, "VtId.FromRawValue("));
        Assert.Equal(1, Count(productionAndCompiler, ".ToRawValue()"));
        Assert.DoesNotMatch(
            @"\b(?:record\s+struct|struct|class)\s+VirtualThreadId\b",
            productionAndCompiler);
    }

    [Fact]
    public void Rf122bArchRegIdCoreValidInputSignaturesPreserveAllValidRepresentations()
    {
        Assert.Equal(ArchRegId.MinValue, ArchRegId.Zero.Value);

        for (int rawValue = ArchRegId.MinValue; rawValue <= ArchRegId.MaxValue; rawValue++)
        {
            Assert.True(ArchRegId.IsRepresentable(rawValue));

            ArchRegId fromConstructor = new((byte)rawValue);
            ArchRegId fromCreate = ArchRegId.Create(rawValue);
            ArchRegId fromWire = ArchRegId.FromRawValue((byte)rawValue);
            ArchRegId fromCast = (ArchRegId)rawValue;

            Assert.True(ArchRegId.TryCreate(rawValue, out ArchRegId fromTryCreate));
            Assert.Equal(fromConstructor, fromCreate);
            Assert.Equal(fromConstructor, fromWire);
            Assert.Equal(fromConstructor, fromCast);
            Assert.Equal(fromConstructor, fromTryCreate);
            Assert.Equal((byte)rawValue, fromWire.ToRawValue());
            Assert.Equal(rawValue, (int)fromWire);
            Assert.Equal($"x{rawValue}", fromWire.ToString());
        }
    }

    [Fact]
    public void Rf122bArchRegIdInvalidSemanticsAndRf123fReviewedCallersRemainFrozen()
    {
        Assert.False(ArchRegId.IsRepresentable(-1));
        Assert.False(ArchRegId.IsRepresentable(ArchRegId.RegisterCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArchRegId((byte)ArchRegId.RegisterCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArchRegId.Create(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArchRegId.Create(ArchRegId.RegisterCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArchRegId.FromRawValue((byte)ArchRegId.RegisterCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => (ArchRegId)(-1));

        Assert.False(ArchRegId.TryCreate(-1, out ArchRegId negativeResult));
        Assert.Equal(default, negativeResult);
        Assert.Equal(ArchRegId.MinValue, negativeResult.Value);
        Assert.False(ArchRegId.TryCreate(ArchRegId.RegisterCount, out ArchRegId highResult));
        Assert.Equal(default, highResult);
        Assert.Equal(ArchRegId.MinValue, highResult.Value);

        string root = FindRepositoryRoot();
        string productionAndCompiler = ReadProductionAndCompilerSources(root);
        Assert.Equal(3, Count(productionAndCompiler, "ArchRegId.FromRawValue("));
        Assert.Equal(1, Count(productionAndCompiler, "ArchRegId.IsRepresentable("));
        Assert.Equal(1, Count(productionAndCompiler, ".ToRawValue()"));
        Assert.Contains("NoArchReg", productionAndCompiler, StringComparison.Ordinal);
        Assert.Contains("encoded <= ArchRegId.MaxValue", productionAndCompiler, StringComparison.Ordinal);
    }

    [Fact]
    public void Rf122cPhysRegIdCoreValidInputSignaturesPreserveAllValidRepresentations()
    {
        Assert.Equal(PhysRegId.MinValue, PhysRegId.Zero.Value);
        Assert.Equal(PhysicalRegisterFile.TotalPhysRegs - 1, PhysRegId.MaxValue);

        for (int rawValue = PhysRegId.MinValue; rawValue <= PhysRegId.MaxValue; rawValue++)
        {
            Assert.True(PhysRegId.IsRepresentable(rawValue));

            PhysRegId fromConstructor = new((ushort)rawValue);
            PhysRegId fromCreate = PhysRegId.Create(rawValue);
            PhysRegId fromWire = PhysRegId.FromRawValue((ushort)rawValue);
            PhysRegId fromCast = (PhysRegId)rawValue;

            Assert.True(PhysRegId.TryCreate(rawValue, out PhysRegId fromTryCreate));
            Assert.Equal(fromConstructor, fromCreate);
            Assert.Equal(fromConstructor, fromWire);
            Assert.Equal(fromConstructor, fromCast);
            Assert.Equal(fromConstructor, fromTryCreate);
            Assert.Equal((ushort)rawValue, fromWire.ToRawValue());
            Assert.Equal(rawValue, (int)fromWire);
            Assert.Equal($"p{rawValue}", fromWire.ToString());
        }
    }

    [Fact]
    public void Rf122cPhysRegIdExistingInvalidBehaviorAndCallerBoundaryRemainUnchanged()
    {
        Assert.False(PhysRegId.IsRepresentable(-1));
        Assert.False(PhysRegId.IsRepresentable(PhysRegId.RegisterCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PhysRegId((ushort)PhysRegId.RegisterCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhysRegId.Create(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhysRegId.Create(PhysRegId.RegisterCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhysRegId.FromRawValue((ushort)PhysRegId.RegisterCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => (PhysRegId)(-1));

        Assert.False(PhysRegId.TryCreate(-1, out PhysRegId negativeResult));
        Assert.Equal(default, negativeResult);
        Assert.Equal(PhysRegId.MinValue, negativeResult.Value);
        Assert.False(PhysRegId.TryCreate(PhysRegId.RegisterCount, out PhysRegId highResult));
        Assert.Equal(default, highResult);
        Assert.Equal(PhysRegId.MinValue, highResult.Value);

        string root = FindRepositoryRoot();
        string productionAndCompiler = ReadProductionAndCompilerSources(root);
        Assert.Equal(0, Count(productionAndCompiler, "PhysRegId.FromRawValue("));
        Assert.Equal(0, Count(productionAndCompiler, "PhysRegId.IsRepresentable("));
        Assert.Equal(1, Count(productionAndCompiler, ".ToRawValue()"));
        Assert.Contains("if (_head == 0) return -1", productionAndCompiler, StringComparison.Ordinal);
        Assert.Contains("physReg = default", productionAndCompiler, StringComparison.Ordinal);
        Assert.Contains("physReg <= 0 || physReg >= PhysicalRegisterFile.TotalPhysRegs",
            productionAndCompiler, StringComparison.Ordinal);
    }

    [Fact]
    public void Rf123aArchitecturalRegisterMaskEntryPointsPreserveEveryValidMask()
    {
        for (int rawValue = ArchRegId.MinValue; rawValue <= ArchRegId.MaxValue; rawValue++)
        {
            ArchRegId regId = ArchRegId.Create(rawValue);

            ResourceBitset rawRead = ResourceMaskBuilder.ForRegisterRead(rawValue);
            ResourceBitset rawWrite = ResourceMaskBuilder.ForRegisterWrite(rawValue);
            ResourceBitset checkedRead = ResourceMaskBuilder.ForArchitecturalRegisterRead(regId);
            ResourceBitset checkedWrite = ResourceMaskBuilder.ForArchitecturalRegisterWrite(regId);

            Assert.Equal(rawRead, checkedRead);
            Assert.Equal(rawWrite, checkedWrite);
            Assert.Equal(1UL << (rawValue / 4), checkedRead.Low);
            Assert.Equal(1UL << (16 + (rawValue / 4)), checkedWrite.Low);
            Assert.Equal(0UL, checkedRead.High);
            Assert.Equal(0UL, checkedWrite.High);

            SafetyMask128 rawRead128 = ResourceMaskBuilder.ForRegisterRead128(rawValue);
            SafetyMask128 rawWrite128 = ResourceMaskBuilder.ForRegisterWrite128(rawValue);
            SafetyMask128 checkedRead128 = ResourceMaskBuilder.ForArchitecturalRegisterRead128(regId);
            SafetyMask128 checkedWrite128 = ResourceMaskBuilder.ForArchitecturalRegisterWrite128(regId);

            Assert.Equal(rawRead128, checkedRead128);
            Assert.Equal(rawWrite128, checkedWrite128);

            for (int vtId = 0; vtId < Processor.CPU_Core.SmtWays; vtId++)
            {
                ResourceBitset rawVtRead = ResourceMaskBuilder.ForRegisterRead(rawValue, vtId);
                ResourceBitset rawVtWrite = ResourceMaskBuilder.ForRegisterWrite(rawValue, vtId);
                ResourceBitset checkedVtRead =
                    ResourceMaskBuilder.ForArchitecturalRegisterRead(regId, vtId);
                ResourceBitset checkedVtWrite =
                    ResourceMaskBuilder.ForArchitecturalRegisterWrite(regId, vtId);
                int group = (vtId * 4) + (rawValue / 8);

                Assert.Equal(rawVtRead, checkedVtRead);
                Assert.Equal(rawVtWrite, checkedVtWrite);
                Assert.Equal(1UL << group, checkedVtRead.Low);
                Assert.Equal(1UL << (16 + group), checkedVtWrite.Low);
                Assert.Equal(0UL, checkedVtRead.High);
                Assert.Equal(0UL, checkedVtWrite.High);
            }
        }
    }

    [Fact]
    public void Rf123aRawInvalidBehaviorAndCheckedSurfaceRemainFrozen()
    {
        Assert.Equal(
            ResourceMaskBuilder.ForRegisterRead(0),
            ResourceMaskBuilder.ForRegisterRead(-1));
        Assert.Equal(
            ResourceMaskBuilder.ForRegisterWrite(0),
            ResourceMaskBuilder.ForRegisterWrite(-1));
        Assert.Equal(1UL << 15, ResourceMaskBuilder.ForRegisterRead(int.MaxValue).Low);
        Assert.Equal(1UL << 31, ResourceMaskBuilder.ForRegisterWrite(int.MaxValue).Low);
        Assert.Equal(
            ResourceMaskBuilder.ForRegisterRead(0, 0),
            ResourceMaskBuilder.ForRegisterRead(-1, 0));
        Assert.Equal(
            ResourceMaskBuilder.ForRegisterWrite(0, 0),
            ResourceMaskBuilder.ForRegisterWrite(-1, 0));
        Assert.Equal(1UL << 15, ResourceMaskBuilder.ForRegisterRead(int.MaxValue, 0).Low);
        Assert.Equal(1UL << 31, ResourceMaskBuilder.ForRegisterWrite(int.MaxValue, 0).Low);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ResourceMaskBuilder.ForArchitecturalRegisterRead(ArchRegId.Zero, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ResourceMaskBuilder.ForArchitecturalRegisterWrite(
                ArchRegId.Zero,
                Processor.CPU_Core.SmtWays));

        MethodInfo[] sameNameReadOverloads = typeof(ResourceMaskBuilder)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == nameof(ResourceMaskBuilder.ForRegisterRead))
            .Where(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(ArchRegId)))
            .ToArray();
        MethodInfo[] sameNameWriteOverloads = typeof(ResourceMaskBuilder)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == nameof(ResourceMaskBuilder.ForRegisterWrite))
            .Where(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(ArchRegId)))
            .ToArray();
        Assert.Empty(sameNameReadOverloads);
        Assert.Empty(sameNameWriteOverloads);

        string root = FindRepositoryRoot();
        string productionAndCompiler = ReadProductionAndCompilerSources(root);
        Assert.Contains("if (group >= 16) group = 15", productionAndCompiler, StringComparison.Ordinal);
    }

    [Fact]
    public void Rf123bCheckedEntryPointsAndPublicRawApisRemainAfterRf123ajCutover()
    {
        string root = FindRepositoryRoot();
        string productionAndCompiler = ReadProductionAndCompilerSources(root);
        string testAssembler = ReadTree(root, "TestAssemblerConsoleApps");
        string callableSources = productionAndCompiler + testAssembler;
        string projection = Read(root, "HybridCPU_ISE", "Legacy", "CloseToHSL", "Core", "Decoder",
            "Rf06ScalarLegacyProjection.cs");
        string canonicalContracts = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder",
            "CanonicalDecodedContracts.cs");
        string registerPacking = Read(root, "HybridCPU_ISE", "NonRTL", "Arch", "Compat",
            "VLIW_Instruction.RegisterPacking.cs");

        Assert.Equal(27, Count(callableSources,
            "ResourceMaskBuilder.ForArchitecturalRegisterRead("));
        Assert.Equal(14, Count(callableSources,
            "ResourceMaskBuilder.ForArchitecturalRegisterWrite("));
        Assert.Equal(0, Count(callableSources,
            "ResourceMaskBuilder.ForArchitecturalRegisterRead128("));
        Assert.Equal(0, Count(callableSources,
            "ResourceMaskBuilder.ForArchitecturalRegisterWrite128("));

        Assert.Equal(2, Count(projection,
            "ResourceMaskBuilder.ForArchitecturalRegisterRead("));
        Assert.Equal(1, Count(projection,
            "ResourceMaskBuilder.ForArchitecturalRegisterWrite("));
        Assert.Equal(3, Count(projection, "ArchRegId.FromRawValue(canonical."));
        Assert.Equal(0, Count(projection, "ArchRegId.TryCreate(canonical."));
        Assert.Equal(0, Count(projection, "ForRegisterRead(canonical.Rs1)"));
        Assert.Equal(0, Count(projection, "ForRegisterRead(canonical.Rs2)"));
        Assert.Equal(0, Count(projection, "ForRegisterWrite(canonical.Rd)"));

        Assert.Equal(24, Count(callableSources, "ResourceMaskBuilder.ForRegisterRead("));
        Assert.Equal(13, Count(callableSources, "ResourceMaskBuilder.ForRegisterWrite("));
        Assert.Contains("public ArchRegId Rs1", productionAndCompiler, StringComparison.Ordinal);
        Assert.Contains("byte Rd,\r\n    byte Rs1,\r\n    byte Rs2,", canonicalContracts
                .Replace("\n", "\r\n", StringComparison.Ordinal)
                .Replace("\r\r\n", "\r\n", StringComparison.Ordinal),
            StringComparison.Ordinal);
        Assert.Contains("public const byte NoArchReg = byte.MaxValue", registerPacking,
            StringComparison.Ordinal);
        Assert.Contains("if (encoded == NoReg)", registerPacking, StringComparison.Ordinal);
        Assert.DoesNotContain("Changing this fallback belongs to a separate", projection,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Rf123dPaperAuthorityAndSourceGuardMatchPackedIngressBoundary()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");
        string projection = Read(root, "HybridCPU_ISE", "Legacy", "CloseToHSL", "Core", "Decoder",
            "Rf06ScalarLegacyProjection.cs");
        string decoder = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder",
            "DeclarativeDecoderStages.cs");

        Assert.Contains("#### 3.7.1 Required architectural-register operands at RF-06 scalar ingress",
            paper, StringComparison.Ordinal);
        Assert.Contains("requires all three operand fields: destination `rd` and sources", paper,
            StringComparison.Ordinal);
        Assert.Contains("Architectural register x0 is present and valid in every role", paper,
            StringComparison.Ordinal);
        Assert.Contains("`NoArchReg`/`NoReg` wire value is only an explicit absence state", paper,
            StringComparison.Ordinal);
        Assert.Contains("`ArchRegId`, does not alias x0", paper, StringComparison.Ordinal);
        Assert.Contains("`DecodeFailure` with code `OperandEncoding`, field `Word1`", paper,
            StringComparison.Ordinal);
        Assert.Contains("existing `InvalidOperationException` contract-failure form", paper,
            StringComparison.Ordinal);
        Assert.Contains("existing `NotScalarFamily` rejected result", paper,
            StringComparison.Ordinal);
        Assert.Contains("before any register-group shift or clamp", paper,
            StringComparison.Ordinal);
        Assert.Contains("later zero-caller/reachability action", paper, StringComparison.Ordinal);

        Assert.Equal(3, Count(projection, "ArchRegId.FromRawValue(canonical."));
        Assert.Equal(0, Count(projection, "ArchRegId.TryCreate(canonical."));
        Assert.Equal(0, Count(projection, "ResourceMaskBuilder.ForRegisterRead(canonical."));
        Assert.Equal(0, Count(projection, "ResourceMaskBuilder.ForRegisterWrite(canonical."));
        Assert.Contains("TryValidateRequiredRf06RegisterOperands", decoder,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Rf123dActivePackedIngressRejectsEveryRequiredNoArchRegRoleBeforePublication()
    {
        foreach (ushort opcode in Rf06RegisterRegisterOpcodes())
        {
            foreach ((string AbsentRole, byte Rd, byte Rs1, byte Rs2) operands in new[]
                     {
                         ("rd", VLIW_Instruction.NoArchReg, (byte)2, (byte)3),
                         ("rs1", (byte)1, VLIW_Instruction.NoArchReg, (byte)3),
                         ("rs2", (byte)1, (byte)2, VLIW_Instruction.NoArchReg),
                     })
            {
                var instruction = new VLIW_Instruction
                {
                    OpCode = opcode,
                    Word1 = VLIW_Instruction.PackArchRegs(operands.Rd, operands.Rs1, operands.Rs2),
                };
                RawSlot slot = RawSlotReader.Read(in instruction, 3);
                Assert.True(OpcodeDescriptorLookup.TryLookup(in slot, out var descriptor, out _));

                Assert.False(OperandDecoder.TryDecode(
                    in slot,
                    in descriptor,
                    out DecodedOperandFields decodedOperands,
                    out DecodeFailure? operandFailure));
                Assert.Equal(default, decodedOperands);
                Assert.NotNull(operandFailure);
                Assert.Equal(DecodeFailureCode.OperandEncoding, operandFailure!.Code);
                Assert.Equal(3, operandFailure.SlotIndex);
                Assert.Equal("Word1", operandFailure.Field);
                Assert.False(string.IsNullOrWhiteSpace(operandFailure.RawHash));
                Assert.Contains(OpcodeRegistry.GetMnemonicOrHex(opcode), operandFailure.Message,
                    StringComparison.Ordinal);
                Assert.Contains("NoArchReg", operandFailure.Message, StringComparison.Ordinal);
                Assert.Contains(operands.AbsentRole, operandFailure.Message, StringComparison.Ordinal);

                Assert.False(DeclarativeDecoderPipeline.TryDecodeOccupiedSlot(
                    in slot,
                    InstructionSlotMetadata.Default,
                    out DeclarativeDecodedSlot? decoded,
                    out DecodeFailure? pipelineFailure));
                Assert.Null(decoded);
                Assert.Equal(operandFailure, pipelineFailure);

                var facade = new VliwDecoderV4();
                InvalidOpcodeException facadeFailure = Assert.Throws<InvalidOpcodeException>(
                    () => facade.Decode(in instruction, 3));
                Assert.Equal(3, facadeFailure.SlotIndex);
                Assert.Equal(OpcodeRegistry.GetMnemonicOrHex(opcode), facadeFailure.OpcodeIdentifier);
                Assert.False(facadeFailure.IsProhibited);
                Assert.Contains("NoArchReg", facadeFailure.Message, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Rf123dValidBoundaryOperandsRemainPreservedAfterLaterFallbackRemoval()
    {
        foreach (ushort opcode in Rf06RegisterRegisterOpcodes())
        {
            foreach (byte register in new byte[] { ArchRegId.MinValue, ArchRegId.MaxValue })
            {
                var instruction = new VLIW_Instruction
                {
                    OpCode = opcode,
                    Word1 = VLIW_Instruction.PackArchRegs(register, register, register),
                };
                RawSlot slot = RawSlotReader.Read(in instruction, 2);

                Assert.True(DeclarativeDecoderPipeline.TryDecodeOccupiedSlot(
                    in slot,
                    InstructionSlotMetadata.Default,
                    out DeclarativeDecodedSlot? decoded,
                    out DecodeFailure? failure));
                Assert.Null(failure);
                Assert.NotNull(decoded);
                Assert.Equal(register, decoded!.CanonicalInstruction.Rd);
                Assert.Equal(register, decoded.CanonicalInstruction.Rs1);
                Assert.Equal(register, decoded.CanonicalInstruction.Rs2);
            }
        }

        string root = FindRepositoryRoot();
        string projection = Read(root, "HybridCPU_ISE", "Legacy", "CloseToHSL", "Core", "Decoder",
            "Rf06ScalarLegacyProjection.cs");
        Assert.Equal(3, Count(projection, "ArchRegId.FromRawValue(canonical."));
        Assert.Equal(0, Count(projection, "ArchRegId.TryCreate(canonical."));
        Assert.Equal(0, Count(projection, "ResourceMaskBuilder.ForRegisterRead(canonical."));
        Assert.Equal(0, Count(projection, "ResourceMaskBuilder.ForRegisterWrite(canonical."));
        Assert.DoesNotContain("TryValidateRequiredRf06RegisterOperands", projection,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Rf123dScopeAndPreexistingFailureWinnerRemainFrozen()
    {
        var nonRf06 = new VLIW_Instruction
        {
            OpCode = Processor.CPU_Core.IsaOpcodeValues.ADDI,
            Word1 = VLIW_Instruction.PackArchRegs(
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg),
        };
        RawSlot nonRf06Slot = RawSlotReader.Read(in nonRf06, 1);
        Assert.True(OpcodeDescriptorLookup.TryLookup(in nonRf06Slot, out var descriptor, out _));
        Assert.True(OperandDecoder.TryDecode(
            in nonRf06Slot,
            in descriptor,
            out DecodedOperandFields nonRf06Operands,
            out DecodeFailure? nonRf06Failure));
        Assert.Null(nonRf06Failure);
        Assert.Equal(VLIW_Instruction.NoArchReg, nonRf06Operands.Rd);
        Assert.Equal(VLIW_Instruction.NoArchReg, nonRf06Operands.Rs1);
        Assert.Equal(VLIW_Instruction.NoArchReg, nonRf06Operands.Rs2);

        var competingFailure = new VLIW_Instruction
        {
            OpCode = Processor.CPU_Core.IsaOpcodeValues.ADD,
            Acquire = true,
            Word1 = VLIW_Instruction.PackArchRegs(VLIW_Instruction.NoArchReg, 2, 3),
        };
        RawSlot competingSlot = RawSlotReader.Read(in competingFailure, 4);
        Assert.False(DeclarativeDecoderPipeline.TryDecodeOccupiedSlot(
            in competingSlot,
            InstructionSlotMetadata.Default,
            out DeclarativeDecodedSlot? decoded,
            out DecodeFailure? winner));
        Assert.Null(decoded);
        Assert.NotNull(winner);
        Assert.Equal(DecodeFailureCode.ReservedEncoding, winner!.Code);
        Assert.Equal("AcquireRelease", winner.Field);
    }

    [Fact]
    public void Rf123fPrivateRawFallbackHasZeroCallerReflectionAndTestSupportReachabilityAndIsRemoved()
    {
        string root = FindRepositoryRoot();
        string projection = Read(root, "HybridCPU_ISE", "Legacy", "CloseToHSL", "Core", "Decoder",
            "Rf06ScalarLegacyProjection.cs");
        string routing = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Scheduling", "Rf06ScalarSchedulerRouting.cs");
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");

        Assert.Equal(1, Count(projection, "ArchRegId.IsRepresentable(rawValue)"));
        Assert.Equal(3, Count(projection, "IsPresentArchitecturalRegister(canonical."));
        Assert.Contains("requires present rd, rs1 and rs2 architectural registers in x0..x31",
            projection, StringComparison.Ordinal);
        Assert.Equal(3, Count(projection, "ArchRegId.FromRawValue(canonical."));
        Assert.Equal(0, Count(projection, "ArchRegId.TryCreate(canonical."));
        Assert.Equal(0, Count(projection, "ResourceMaskBuilder.ForRegisterRead(canonical."));
        Assert.Equal(0, Count(projection, "ResourceMaskBuilder.ForRegisterWrite(canonical."));
        Assert.Equal(2, Count(projection, "BuildRegisterResourceMask(canonical)"));

        MethodInfo privateBuilder = Assert.Single(typeof(Rf06ScalarLegacyProjection)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(method => method.Name == "BuildRegisterResourceMask"));
        Assert.True(privateBuilder.IsPrivate);
        Assert.False(privateBuilder.IsPublic);
        Assert.Empty(privateBuilder.GetCustomAttributes(inherit: false));

        string externalSources = ReadSourceTreesExcept(
            root,
            [
                Path.Combine("HybridCPU_ISE", "Legacy", "CloseToHSL", "Core", "Decoder",
                    "Rf06ScalarLegacyProjection.cs"),
                Path.Combine("HybridCPU_ISE.Tests", "Architecture",
                    "Rf120ResourceIdIngressGuardTests.cs"),
            ],
            "HybridCPU_ISE", "HybridCPU_Compiler", "HybridCPU_ISE.Tests",
            "TestAssemblerConsoleApps");
        Assert.DoesNotContain("BuildRegisterResourceMask", externalSources,
            StringComparison.Ordinal);
        Assert.DoesNotContain("typeof(Rf06ScalarLegacyProjection)", externalSources,
            StringComparison.Ordinal);

        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Core", "CPU_Core.TestSupport.cs");
        Assert.DoesNotContain("Rf06ScalarLegacyProjection", testSupport,
            StringComparison.Ordinal);
        Assert.DoesNotContain("BuildRegisterResourceMask", testSupport,
            StringComparison.Ordinal);

        int createStart = projection.IndexOf(
            "internal static ExecutionContract CreateContract(", StringComparison.Ordinal);
        int projectStart = projection.IndexOf(
            "internal static CheckedScalarLegacyProjection Project(", StringComparison.Ordinal);
        int buildStart = projection.IndexOf(
            "private static ResourceBitset BuildRegisterResourceMask(", StringComparison.Ordinal);
        int validateStart = projection.IndexOf(
            "private static void ValidateCanonicalFamily(", StringComparison.Ordinal);
        Assert.True(createStart >= 0 && projectStart > createStart && buildStart > projectStart &&
                    validateStart > buildStart);

        string createBody = projection[createStart..projectStart];
        Assert.True(createBody.IndexOf("ValidateCanonicalFamily(canonical", StringComparison.Ordinal) <
                    createBody.IndexOf("int[] reads", StringComparison.Ordinal));
        Assert.True(createBody.IndexOf("ValidateCanonicalFamily(canonical", StringComparison.Ordinal) <
                    createBody.IndexOf("ExecutionContract.Create(", StringComparison.Ordinal));

        string projectBody = projection[projectStart..buildStart];
        Assert.True(projectBody.IndexOf("ValidateCanonicalFamily(canonical", StringComparison.Ordinal) <
                    projectBody.IndexOf("ArgumentNullException.ThrowIfNull(contract)", StringComparison.Ordinal));
        Assert.True(projectBody.IndexOf("ValidateCanonicalFamily(canonical", StringComparison.Ordinal) <
                    projectBody.IndexOf("BuildRegisterResourceMask(canonical)", StringComparison.Ordinal));

        string validateBody = projection[validateStart..];
        Assert.True(validateBody.IndexOf("outside the approved scalar family", StringComparison.Ordinal) <
                    validateBody.IndexOf("requires present rd, rs1 and rs2", StringComparison.Ordinal));

        Assert.Contains("catch (InvalidOperationException)", routing, StringComparison.Ordinal);
        Assert.Contains("Rf06ScalarRoutingRejectReason.NotScalarFamily", routing,
            StringComparison.Ordinal);
        Assert.Contains("catch (InvalidOperationException)", fsp, StringComparison.Ordinal);
        Assert.Contains("candidate.PostStageBIdentityTemplate = null", fsp,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReflectionDiagnosticsAndTestSupportMutationSeamsRemainVisible()
    {
        string root = FindRepositoryRoot();
        string tests = ReadTree(root, "HybridCPU_ISE.Tests");
        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Core",
            "CPU_Core.TestSupport.cs");
        string telemetry = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Diagnostics",
            "TelemetryExporter.cs");
        string replayToken = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps",
            "Replay", "ReplayToken.cs");

        Assert.Contains("field.SetValue(core.Runtime.Scratch, value);", tests, StringComparison.Ordinal);
        Assert.Contains("Activator.CreateInstance(microOpType)", tests, StringComparison.Ordinal);
        Assert.True(Count(testSupport, "internal void Test") >= 50);
        Assert.Contains("SetLane(laneIndex, lane)", testSupport, StringComparison.Ordinal);
        Assert.Contains("JsonSerializer.Serialize(profile", telemetry, StringComparison.Ordinal);
        Assert.Contains("JsonSerializer.Deserialize(json", telemetry, StringComparison.Ordinal);
        Assert.Contains("JsonSerializer.Serialize(this, options)", replayToken, StringComparison.Ordinal);
        Assert.Contains("JsonSerializer.Deserialize<ReplayToken>(json)", replayToken, StringComparison.Ordinal);
    }

    private static InventoryFingerprint Capture(string root, string pattern)
    {
        var regex = new Regex(pattern, RegexOptions.CultureInvariant);
        var entries = new List<string>();
        var guardSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf120ResourceIdIngressGuardTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123gScalarLoadRegisterMaskInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123hScalarLoadRegisterMaskValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123iBundleLegalityRegisterAggregationInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123jBundleLegalityRegisterAggregationValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123kBranchMicroOpRegisterMaskInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123lBranchMicroOpRegisterMaskValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123mCsrMicroOpRegisterMaskInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123nCsrMicroOpRegisterMaskValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123oSystemDeviceCommandRegisterMaskInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123pSystemDeviceCommandRegisterMaskValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123qLoadMicroOpRegisterMaskInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123rLoadMicroOpRegisterMaskValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123sStoreMicroOpRegisterMaskInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123tStoreMicroOpRegisterMaskValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123uScalarAluRegisterMaskInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123vScalarAluRegisterMaskValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123wSystemEventEcallRegisterMaskInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123xSystemEventEcallRegisterMaskValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123yAtomicMicroOpRegisterMaskInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123zAtomicMicroOpRegisterMaskValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123aaVmxMicroOpRegisterMaskInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123abVmxMicroOpRegisterMaskValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123acCustomAcceleratorRegisterMaskInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123adCustomAcceleratorRegisterMaskValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123aeMoveMicroOpRegisterMaskInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123afMoveMicroOpRegisterMaskValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123agVectorAdmissionRegisterMaskInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123ahVectorAdmissionRegisterMaskValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123aiVConfigRegisterMaskInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123ajVConfigRegisterMaskValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf123akArchitecturalRegisterResourceMaskCallerClosureAuditTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf124VtPipelineScoreboardIdentityInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf124aVtRoleContourArchitectureDecisionTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf124bMicroOpOwnerVtProjectionValidInputContractTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf124cAtomicExecuteOwnerVtInventoryDecisionTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf124dAtomicExecuteOwnerVtValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf124eScalarAluExecuteOwnerVtInventoryDecisionTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf124fScalarAluExecuteOwnerVtValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf124gScalarAluRetireOwnerVtInventoryDecisionTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf124hScalarAluRetireOwnerVtValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf124iBranchRetireOwnerVtInventoryDecisionTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf124jBranchRetireOwnerVtValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf124kLoadRetireOwnerVtInventoryDecisionTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf124lLoadRetireOwnerVtValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf124mCsrRetireOwnerVtInventoryDecisionTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf124nCsrRetireOwnerVtValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf125SlotPhysicalLanePinningInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf125aBundleSlotIdCoreValidInputContractTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf125bSourceOperationProvenanceSlotInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf125cSourceOperationProvenanceSlotValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf125jRawPinningCompatibilityRetentionDecisionTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf1210aCompleteBridgeMatrixInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf1211aExpandedBridgeDeletionEligibilityReconciliationTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf1212cFinalPostReconciliationExitAuditTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126aMemoryBankGeometryResolutionInventoryDecisionTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126bMemoryBankResolutionCoreValidInputContractTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126cSchedulerVisibleBankResolverProducerInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126dSpecializedCapabilityBankResolutionValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126eSpecializedCapabilityNonResolvedOutcomeInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126fSpecializedCapabilityNonResolvedResultCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126gLoadStoreComputedBankCarrierInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126hShadowLegacyCarrierBankResolutionValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126iShadowLegacyCarrierNonResolvedOutcomeInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126jShadowLegacyCarrierNonResolvedResultCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126kAssistCachedMemoryBankCarrierInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126lAssistCapabilityBankResolutionValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126mPhysicalMemoryBankQueueIndexInventoryDecisionTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126nPhysicalMemoryBankGeometryLifetimeArchitectureDecisionTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126oPhysicalMemoryBankIndexCoreValidInputContractTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126pPhysicalMemoryBankIndexProducerConsumerRevalidationTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126qPhysicalMemoryBankIndexProducerValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126rPhysicalMemoryBankInvalidGeometryFallbackInventoryDecisionTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126sPhysicalMemoryBankRejectionCarrierArchitectureDecisionTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126tMemoryBankGeometryGenerationRepresentationArchitectureDecisionTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126uMemoryBankGeometryGenerationCoreValidInputContractTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126vPhysicalMemoryBankGeometrySnapshotRevalidationTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126wPhysicalMemoryBankGeometryCoreValidInputContractTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126xPhysicalMemoryBankBindingCoreValidInputContractTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126yPhysicalMemoryBankResolutionCoreValidInputContractTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126zMemoryBankGeometryUpdateResultCoreValidInputContractTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126aaMemoryBankGeometryPublicationProducerConsumerInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126abMemoryBankGeometryLifecycleQuiescenceArchitectureDecisionTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126acMemoryBankGeometryLifecycleSerializationFoundationTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126adMemoryBankGeometryAuthoritativeReplacementValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126aeAcceptedRequestPhysicalBankBindingInventoryTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126afControllerNativeAcceptedRequestBindingStorageTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126agControllerStoredBindingConsumerRevalidationTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126ahControllerOrdinaryReadStoredBindingValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126aiCanonicalVectorPhysicalBankEnvelopeArchitectureDecisionTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126ajCanonicalVectorPhysicalBankEnvelopeCoreValidInputContractTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126akCanonicalEnvelopeAdmissionStorageServiceRevalidationTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126alCanonicalEnvelopeCaptureAndPrivateStorageValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126amCanonicalStoredEnvelopeServiceConsumptionValidInputCutoverTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126anCanonicalEnvelopeMismatchInvalidBehaviorArchitectureDecisionTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126aoCanonicalSourceBaseBindingRemovalEligibilityDecisionTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126apCanonicalSourceBaseBindingCompatibilityRemovalTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126aqScalarStoreStoredBindingRetentionValidationBoundaryDecisionTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126arControllerCompletionCancellationBindingAuthorityRevalidationTests.cs")),
            Path.GetFullPath(Path.Combine(root, "HybridCPU_ISE.Tests", "Architecture",
                "Rf126asLegacyAsyncCancellationBindingCarrierDecisionTests.cs")),
        };

        foreach (string sourceRoot in new[]
                 {
                     "HybridCPU_ISE", "HybridCPU_Compiler", "HybridCPU_ISE.Tests", "TestAssemblerConsoleApps"
                 })
        {
            string absoluteRoot = Path.Combine(root, sourceRoot);
            foreach (string path in Directory.EnumerateFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories)
                         .Where(path => !IsBuildOutput(path))
                         .Where(path => !guardSources.Contains(Path.GetFullPath(path)))
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                string relative = NormalizeRf131LegacyRelocationPath(
                    Path.GetRelativePath(root, path).Replace('\\', '/'));
                foreach (string line in File.ReadLines(path))
                {
                    int count = regex.Matches(line).Count;
                    for (int occurrence = 0; occurrence < count; occurrence++)
                    {
                        entries.Add($"{relative}:{line.Trim()}");
                    }
                }
            }
        }

        entries.Sort(StringComparer.Ordinal);
        string joined = string.Join("\n", entries);
        string sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(joined)))
            .ToLowerInvariant();
        return new InventoryFingerprint(entries.Count, sha256);
    }

    // RF-13.1 relocates these six legacy files without changing their source
    // surface. This RF-12 guard freezes identifier content, while RF-13.1 owns
    // and validates their physical Legacy/ placement separately.
    private static string NormalizeRf131LegacyRelocationPath(string relative) => relative switch
    {
        "HybridCPU_ISE/Legacy/CloseToHSL/Core/Decoder/Rf06ScalarLegacyProjection.cs" =>
            "HybridCPU_ISE/CloseToHSL/Core/Decoder/Rf06ScalarLegacyProjection.cs",
        "HybridCPU_ISE/Legacy/CloseToHSL/Core/State/Compat/LegacyCpuStateAdapter.cs" =>
            "HybridCPU_ISE/CloseToHSL/Core/State/Compat/LegacyCpuStateAdapter.cs",
        "HybridCPU_ISE/Legacy/CloseToHSL/Core/State/LegacyCompatibilityState.cs" =>
            "HybridCPU_ISE/CloseToHSL/Core/State/LegacyCompatibilityState.cs",
        "HybridCPU_ISE/Legacy/NonRTL/Legacy/LegacyMachineStateReadException.cs" =>
            "HybridCPU_ISE/NonRTL/Legacy/LegacyMachineStateReadException.cs",
        "HybridCPU_ISE/Legacy/NonRTL/Legacy/LegacyObservationServiceFactory.cs" =>
            "HybridCPU_ISE/NonRTL/Legacy/LegacyObservationServiceFactory.cs",
        "HybridCPU_ISE/Legacy/NonRTL/Legacy/LegacyProcessorMachineStateSource.cs" =>
            "HybridCPU_ISE/NonRTL/Legacy/LegacyProcessorMachineStateSource.cs",
        _ => relative,
    };

    private static string ReadProductionAndCompilerSources(string root) =>
        string.Join("\n", new[]
        {
            ReadTree(root, "HybridCPU_ISE"),
            ReadTree(root, "HybridCPU_Compiler")
        });

    private static string ReadTree(string root, string relativeRoot) =>
        string.Join("\n", Directory.EnumerateFiles(Path.Combine(root, relativeRoot), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));

    private static string ReadSourceTreesExcept(
        string root,
        IEnumerable<string> excludedRelativePaths,
        params string[] relativeRoots)
    {
        var excluded = excludedRelativePaths
            .Select(path => Path.GetFullPath(Path.Combine(root, path)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return string.Join("\n", relativeRoots
            .SelectMany(relativeRoot => Directory.EnumerateFiles(
                Path.Combine(root, relativeRoot), "*.cs", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(path))
            .Where(path => !excluded.Contains(Path.GetFullPath(path)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));
    }

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

    private static int Count(string text, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
                return current;

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("HybridCPU repository root was not found.");
    }

    private sealed record InventoryFamily(string Name, string Pattern, int MatchCount, string Sha256);

    private readonly record struct InventoryFingerprint(int MatchCount, string Sha256);

    private static ushort[] Rf06RegisterRegisterOpcodes() =>
    [
        Processor.CPU_Core.IsaOpcodeValues.ADD,
        Processor.CPU_Core.IsaOpcodeValues.SUB,
        Processor.CPU_Core.IsaOpcodeValues.AND,
        Processor.CPU_Core.IsaOpcodeValues.OR,
        Processor.CPU_Core.IsaOpcodeValues.XOR,
    ];
}
