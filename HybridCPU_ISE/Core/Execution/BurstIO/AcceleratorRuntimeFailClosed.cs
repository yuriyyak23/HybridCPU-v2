using System;
using System.Diagnostics.CodeAnalysis;

namespace YAKSys_Hybrid_CPU.Execution
{
    internal static class AcceleratorRuntimeFailClosed
    {
        private const string RegistrationMessage =
            "Custom accelerator DMA registration is not implemented in the current runtime. " +
            "The contour must fail closed instead of silently accepting the device.";

        private const string TransferMessage =
            "Custom accelerator DMA transfers are not implemented in the current runtime. " +
            "The contour must fail closed instead of returning a completed transfer token.";

        [DoesNotReturn]
        public static void ThrowRegistrationNotSupported() =>
            throw new NotSupportedException(RegistrationMessage);

        [DoesNotReturn]
        public static DMATransferToken ThrowTransferNotSupported() =>
            throw new NotSupportedException(TransferMessage);
    }
}
