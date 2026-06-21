using System;

namespace YAKSys_Hybrid_CPU.Core.ControlFlow
{
    public abstract class ControlFlowContractException : InvalidOperationException
    {
        protected ControlFlowContractException(string message)
            : base(message)
        {
        }
    }

    public sealed class EntryPointDefinitionException : ControlFlowContractException
    {
        public EntryPointDefinitionException(
            string operation,
            ulong existingAddress,
            ulong? requestedAddress,
            string message)
            : base(message)
        {
            Operation = operation ?? string.Empty;
            ExistingAddress = existingAddress;
            RequestedAddress = requestedAddress;
        }

        public string Operation { get; }

        public ulong ExistingAddress { get; }

        public ulong? RequestedAddress { get; }
    }

    public sealed class UnsupportedEntryPointOperationException : ControlFlowContractException
    {
        public UnsupportedEntryPointOperationException(string entryPointKind, string operation)
            : base($"{entryPointKind} entry point does not support {operation}.")
        {
            EntryPointKind = entryPointKind ?? string.Empty;
            Operation = operation ?? string.Empty;
        }

        public string EntryPointKind { get; }

        public string Operation { get; }
    }

    public sealed class ControlFlowStackUnderflowException : ControlFlowContractException
    {
        public ControlFlowStackUnderflowException(string stackName)
            : base($"{stackName} control-flow stack is empty; returning PC=0 would alias a valid address.")
        {
            StackName = stackName ?? string.Empty;
        }

        public string StackName { get; }
    }

    public sealed class RelocationEncodingException : ControlFlowContractException
    {
        public RelocationEncodingException(ulong emissionCursor, ulong requiredBackOffsetBytes)
            : base(
                $"Cannot encode legacy absolute relocation at cursor 0x{emissionCursor:X}; " +
                $"the encoding requires a {requiredBackOffsetBytes} byte back-offset.")
        {
            EmissionCursor = emissionCursor;
            RequiredBackOffsetBytes = requiredBackOffsetBytes;
        }

        public ulong EmissionCursor { get; }

        public ulong RequiredBackOffsetBytes { get; }
    }

    public sealed class UnsupportedRelocationApplicationException : ControlFlowContractException
    {
        public UnsupportedRelocationApplicationException(
            RelocationEncodingKind encodingKind,
            byte patchWidth)
            : base(
                $"Relocation application only supports {nameof(RelocationEncodingKind.LegacyAbsolute64)} " +
                $"with an {RelocationEntry.Absolute64PatchWidth}-byte patch width. " +
                $"Received {encodingKind} with patch width {patchWidth}.")
        {
            EncodingKind = encodingKind;
            PatchWidth = patchWidth;
        }

        public RelocationEncodingKind EncodingKind { get; }

        public byte PatchWidth { get; }
    }

    public sealed class RelocationPatchOutOfRangeException : ControlFlowContractException
    {
        public RelocationPatchOutOfRangeException(
            ulong imageBaseAddress,
            int imageLength,
            ulong patchAddress,
            byte patchWidth,
            string targetSymbol)
            : base(CreateMessage(imageBaseAddress, imageLength, patchAddress, patchWidth, targetSymbol))
        {
            ImageBaseAddress = imageBaseAddress;
            ImageLength = imageLength;
            PatchAddress = patchAddress;
            PatchWidth = patchWidth;
            TargetSymbol = targetSymbol ?? string.Empty;
        }

        public ulong ImageBaseAddress { get; }

        public int ImageLength { get; }

        public ulong PatchAddress { get; }

        public byte PatchWidth { get; }

        public string TargetSymbol { get; }

        private static string NormalizeTargetSymbol(string targetSymbol)
        {
            return string.IsNullOrWhiteSpace(targetSymbol) ? "<anonymous>" : targetSymbol;
        }

        private static string CreateMessage(
            ulong imageBaseAddress,
            int imageLength,
            ulong patchAddress,
            byte patchWidth,
            string targetSymbol)
        {
            ulong imageLengthUlong = (ulong)imageLength;
            string endAddressText = ulong.MaxValue - imageBaseAddress < imageLengthUlong
                ? "0xFFFFFFFFFFFFFFFF+"
                : $"0x{imageBaseAddress + imageLengthUlong:X}";

            return
                $"Relocation patch for symbol '{NormalizeTargetSymbol(targetSymbol)}' at 0x{patchAddress:X} " +
                $"with width {patchWidth} byte(s) falls outside the emitted image " +
                $"[0x{imageBaseAddress:X}, {endAddressText}).";
        }
    }
}
