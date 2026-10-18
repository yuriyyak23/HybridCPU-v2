using System;
using System.Diagnostics.CodeAnalysis;

namespace YAKSys_Hybrid_CPU.Execution
{
    internal static class AcceleratorRuntimeFailClosed
    {
        private const string RegistrationMessage =
            "Retained custom-accelerator DMA registration is not implemented in the current runtime. " +
            "The legacy contour must fail closed instead of silently accepting the device as authority.";

        private const string TransferMessage =
            "Retained custom-accelerator DMA transfers are not implemented in the current runtime. " +
            "The legacy contour must fail closed instead of returning a completed transfer token.";

        [DoesNotReturn]
        public static void ThrowRegistrationNotSupported() =>
            throw new NotSupportedException(RegistrationMessage);

        [DoesNotReturn]
        public static DMATransferToken ThrowTransferNotSupported() =>
            throw new NotSupportedException(TransferMessage);
    }
}
