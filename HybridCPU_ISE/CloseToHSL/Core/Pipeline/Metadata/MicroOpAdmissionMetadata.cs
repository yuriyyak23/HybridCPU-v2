using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Pipeline.Metadata;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Producer-side admission snapshot for one <see cref="MicroOp"/>.
    ///
    /// Captures the correctness-relevant facts that legality, decoded-slot projection,
    /// and certificate checks consume after decode/materialization so those consumers do
    /// not need to treat <see cref="SafetyMask128"/> as the admission authority.
    /// </summary>
    public readonly record struct MicroOpAdmissionMetadata(
        bool IsStealable,
        bool IsControlFlow,
        bool IsMemoryOp,
        bool WritesRegister,
        bool HasSideEffects,
        int OwnerContextId,
        ulong DomainTag,
        SlotPlacementMetadata Placement,
        IReadOnlyList<int> ReadRegisters,
        IReadOnlyList<int> WriteRegisters,
        IReadOnlyList<(ulong Address, ulong Length)> ReadMemoryRanges,
        IReadOnlyList<(ulong Address, ulong Length)> NormalizedReadMemoryRanges,
        IReadOnlyList<(ulong Address, ulong Length)> WriteMemoryRanges,
        AssistCoalescingDescriptor AssistCoalescingDescriptor,
        uint RegisterHazardMask,
        SafetyMask128 StructuralSafetyMask)
    {
        private const ulong RegisterBitsMaskLow32 = 0xFFFF_FFFFUL;

        public static MicroOpAdmissionMetadata Default { get; } = new(
            IsStealable: true,
            IsControlFlow: false,
            IsMemoryOp: false,
            WritesRegister: false,
            HasSideEffects: false,
            OwnerContextId: 0,
            DomainTag: 0,
            Placement: SlotPlacementMetadata.Default,
            ReadRegisters: Array.Empty<int>(),
            WriteRegisters: Array.Empty<int>(),
            ReadMemoryRanges: Array.Empty<(ulong Address, ulong Length)>(),
            NormalizedReadMemoryRanges: Array.Empty<(ulong Address, ulong Length)>(),
            WriteMemoryRanges: Array.Empty<(ulong Address, ulong Length)>(),
            AssistCoalescingDescriptor: AssistCoalescingDescriptor.None,
            RegisterHazardMask: 0,
            StructuralSafetyMask: SafetyMask128.Zero);

        public bool HasStructuralSafetyMask => StructuralSafetyMask.IsNonZero;

        public SafetyMask128 SharedStructuralMask =>
            new SafetyMask128(StructuralSafetyMask.Low & ~RegisterBitsMaskLow32, StructuralSafetyMask.High);

        public SafetyMask128 CertificateMask =>
            new SafetyMask128((StructuralSafetyMask.Low & ~RegisterBitsMaskLow32) | RegisterHazardMask, StructuralSafetyMask.High);

        public static MicroOpAdmissionMetadata Create(MicroOp microOp, SafetyMask128 structuralSafetyMask)
        {
            ArgumentNullException.ThrowIfNull(microOp);

            IReadOnlyList<int> readRegisters = microOp.ReadRegisters ?? Array.Empty<int>();
            IReadOnlyList<int> writeRegisters = microOp.WriteRegisters ?? Array.Empty<int>();
            IReadOnlyList<(ulong Address, ulong Length)> readMemoryRanges = microOp.ReadMemoryRanges ?? Array.Empty<(ulong Address, ulong Length)>();
            IReadOnlyList<(ulong Address, ulong Length)> writeMemoryRanges = microOp.WriteMemoryRanges ?? Array.Empty<(ulong Address, ulong Length)>();
            IReadOnlyList<(ulong Address, ulong Length)> normalizedReadMemoryRanges = readMemoryRanges;
            if (ReadRangeMetadataHelper.TryNormalizeContiguousReadRanges(
                readMemoryRanges,
                out IReadOnlyList<(ulong Address, ulong Length)> normalizedRanges))
            {
                normalizedReadMemoryRanges = normalizedRanges;
            }

            AssistCoalescingDescriptor assistCoalescingDescriptor =
                ReadRangeMetadataHelper.BuildAssistCoalescingDescriptor(
                    readMemoryRanges,
                    normalizedReadMemoryRanges);
            ReadRangeMetadataHelper.ValidateCoalescedRangeMetadata(
                readMemoryRanges,
                normalizedReadMemoryRanges,
                assistCoalescingDescriptor);
            uint registerHazardMask = BuildRegisterHazardMask(readRegisters, writeRegisters);

            if (structuralSafetyMask.IsZero &&
                !microOp.AllowsStructuralSafetyFallback &&
                InstructionRegistry.RequiresExplicitStructuralSafetyMask(microOp))
            {
                throw InstructionRegistry.CreateMissingExplicitStructuralSafetyMaskException(
                    microOp,
                    "MicroOpAdmissionMetadata.Create()");
            }

            return new MicroOpAdmissionMetadata(
                IsStealable: microOp.IsStealable,
                IsControlFlow: microOp.IsControlFlow,
                IsMemoryOp: microOp.IsMemoryOp,
                WritesRegister: microOp.WritesRegister,
                HasSideEffects: microOp.HasSideEffects,
                OwnerContextId: microOp.OwnerContextId,
                DomainTag: microOp.Placement.DomainTag,
                Placement: microOp.Placement,
                ReadRegisters: readRegisters,
                WriteRegisters: writeRegisters,
                ReadMemoryRanges: readMemoryRanges,
                NormalizedReadMemoryRanges: normalizedReadMemoryRanges,
                WriteMemoryRanges: writeMemoryRanges,
                AssistCoalescingDescriptor: assistCoalescingDescriptor,
                RegisterHazardMask: registerHazardMask,
                StructuralSafetyMask: structuralSafetyMask);
        }

        public static uint BuildRegisterHazardMask(
            IReadOnlyList<int> readRegisters,
            IReadOnlyList<int> writeRegisters)
        {
            uint mask = 0;

            if (readRegisters != null)
            {
                for (int i = 0; i < readRegisters.Count; i++)
                {
                    int group = ClampRegisterGroup(readRegisters[i]);
                    mask |= 1U << group;
                }
            }

            if (writeRegisters != null)
            {
                for (int i = 0; i < writeRegisters.Count; i++)
                {
                    int group = ClampRegisterGroup(writeRegisters[i]);
                    mask |= 1U << (16 + group);
                }
            }

            return mask;
        }

        private static int ClampRegisterGroup(int registerId)
        {
            if (registerId < 0)
                return 0;

            int group = registerId / 4;
            return group < 16 ? group : 15;
        }
    }
}
