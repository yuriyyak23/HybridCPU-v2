using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core.ControlFlow
{
    /// <summary>
    /// Applies serialized entry-point relocations to an emitted bundle image.
    /// Keeps control-flow patching on an explicit linker-like surface instead of
    /// reopening direct MainMemory side effects in runtime helpers.
    /// </summary>
    public static class EntryPointRelocationLinker
    {
        public static byte[] ApplyRelocations(
            byte[] programImage,
            ulong imageBaseAddress,
            IReadOnlyList<RelocationEntry> relocations)
        {
            ArgumentNullException.ThrowIfNull(programImage);
            ArgumentNullException.ThrowIfNull(relocations);

            byte[] linkedImage = (byte[])programImage.Clone();
            ApplyRelocationsInPlace(linkedImage, imageBaseAddress, relocations);
            return linkedImage;
        }

        public static void ApplyRelocationsInPlace(
            byte[] programImage,
            ulong imageBaseAddress,
            IReadOnlyList<RelocationEntry> relocations)
        {
            ArgumentNullException.ThrowIfNull(programImage);
            ArgumentNullException.ThrowIfNull(relocations);

            Span<byte> imageBytes = programImage;
            for (int index = 0; index < relocations.Count; index++)
            {
                ApplyRelocation(imageBytes, imageBaseAddress, relocations[index]);
            }
        }

        private static void ApplyRelocation(
            Span<byte> programImage,
            ulong imageBaseAddress,
            in RelocationEntry relocation)
        {
            switch (relocation.EncodingKind)
            {
                case RelocationEncodingKind.LegacyAbsolute64:
                    ApplyLegacyAbsolute64(programImage, imageBaseAddress, relocation);
                    return;

                default:
                    throw new UnsupportedRelocationApplicationException(
                        relocation.EncodingKind,
                        relocation.PatchWidth);
            }
        }

        private static void ApplyLegacyAbsolute64(
            Span<byte> programImage,
            ulong imageBaseAddress,
            in RelocationEntry relocation)
        {
            if (relocation.PatchWidth != RelocationEntry.Absolute64PatchWidth)
            {
                throw new UnsupportedRelocationApplicationException(
                    relocation.EncodingKind,
                    relocation.PatchWidth);
            }

            int patchOffset = ResolvePatchOffset(programImage.Length, imageBaseAddress, relocation);
            BitConverter.TryWriteBytes(
                programImage.Slice(patchOffset, RelocationEntry.Absolute64PatchWidth),
                relocation.ResolvedTargetAddress);
        }

        private static int ResolvePatchOffset(
            int imageLength,
            ulong imageBaseAddress,
            in RelocationEntry relocation)
        {
            if (relocation.PatchAddress < imageBaseAddress)
            {
                throw CreatePatchOutOfRangeException(imageBaseAddress, imageLength, relocation);
            }

            ulong patchOffset = relocation.PatchAddress - imageBaseAddress;
            ulong patchEndExclusive = patchOffset + relocation.PatchWidth;
            if (patchEndExclusive > (ulong)imageLength)
            {
                throw CreatePatchOutOfRangeException(imageBaseAddress, imageLength, relocation);
            }

            return checked((int)patchOffset);
        }

        private static RelocationPatchOutOfRangeException CreatePatchOutOfRangeException(
            ulong imageBaseAddress,
            int imageLength,
            in RelocationEntry relocation)
        {
            return new RelocationPatchOutOfRangeException(
                imageBaseAddress,
                imageLength,
                relocation.PatchAddress,
                relocation.PatchWidth,
                relocation.TargetSymbol);
        }
    }
}
