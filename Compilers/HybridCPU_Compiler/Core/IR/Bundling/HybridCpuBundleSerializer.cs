using System;
using System.Collections.Generic;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// Serializes lowered backend `HybridCpuInstructionBundle` instances into fetch-ready byte images.
    /// </summary>
    public sealed class HybridCpuBundleSerializer
    {
        /// <summary>
        /// Number of bytes in one physical HybridCPU bundle.
        /// </summary>
        public const int BundleSizeBytes = 256;

        /// <summary>
        /// Serializes one backend `HybridCpuInstructionBundle` into a 256-byte image.
        /// </summary>
        public byte[] SerializeBundle(HybridCpuInstructionBundle bundle)
        {
            byte[] serializedBundle = new byte[BundleSizeBytes];
            if (!bundle.TryWriteBytes(serializedBundle))
            {
                throw new InvalidOperationException("Backend bundle serialization failed because the destination buffer was smaller than one physical bundle.");
            }

            return serializedBundle;
        }

        /// <summary>
        /// Serializes a program-order bundle stream into one contiguous fetch-ready image.
        /// </summary>
        public byte[] SerializeProgram(IReadOnlyList<HybridCpuInstructionBundle> bundles)
        {
            ArgumentNullException.ThrowIfNull(bundles);
            if (bundles.Count == 0)
            {
                return Array.Empty<byte>();
            }

            byte[] programImage = new byte[bundles.Count * BundleSizeBytes];
            for (int bundleIndex = 0; bundleIndex < bundles.Count; bundleIndex++)
            {
                if (!bundles[bundleIndex].TryWriteBytes(programImage.AsSpan(bundleIndex * BundleSizeBytes, BundleSizeBytes)))
                {
                    throw new InvalidOperationException($"Backend bundle serialization failed for bundle index {bundleIndex}.");
                }
            }

            return programImage;
        }
    }
}

