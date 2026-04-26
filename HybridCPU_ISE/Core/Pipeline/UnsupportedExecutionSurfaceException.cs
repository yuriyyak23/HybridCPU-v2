using System;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Thrown when a decoded instruction is legal but cannot be routed to a supported
    /// runtime execution surface in the current mainline pipeline configuration.
    /// </summary>
    public sealed class UnsupportedExecutionSurfaceException : Exception
    {
        public UnsupportedExecutionSurfaceException(
            string surfaceName,
            int slotIndex,
            uint opCode,
            ulong bundlePc)
            : this(
                surfaceName,
                slotIndex,
                opCode,
                bundlePc,
                innerException: null)
        {
        }

        public UnsupportedExecutionSurfaceException(
            string surfaceName,
            int slotIndex,
            uint opCode,
            ulong bundlePc,
            Exception? innerException)
            : base(
                ExecutionFaultContract.FormatMessage(
                    ExecutionFaultCategory.UnsupportedExecutionSurface,
                    $"{surfaceName} slot {slotIndex} (opcode 0x{opCode:X}) at PC 0x{bundlePc:X} is legal decode but has no supported mainline runtime execution surface. " +
                    "Reject it before execute until an explicit retire/apply contour lands."),
                innerException)
        {
            SurfaceName = surfaceName;
            SlotIndex = slotIndex;
            OpCode = opCode;
            BundlePc = bundlePc;
            ExecutionFaultContract.Stamp(this, Category);
        }

        public string SurfaceName { get; }
        public int SlotIndex { get; }
        public uint OpCode { get; }
        public ulong BundlePc { get; }
        public ExecutionFaultCategory Category => ExecutionFaultCategory.UnsupportedExecutionSurface;

        public static UnsupportedExecutionSurfaceException CreateForAtomic(
            int slotIndex,
            uint opCode,
            ulong bundlePc)
        {
            return new UnsupportedExecutionSurfaceException(
                surfaceName: "Atomic",
                slotIndex,
                opCode,
                bundlePc);
        }

        public static UnsupportedExecutionSurfaceException CreateForMemory(
            int slotIndex,
            uint opCode,
            ulong bundlePc)
        {
            return new UnsupportedExecutionSurfaceException(
                surfaceName: "Memory",
                slotIndex,
                opCode,
                bundlePc);
        }

        public static UnsupportedExecutionSurfaceException CreateForControlFlow(
            int slotIndex,
            uint opCode,
            ulong bundlePc)
        {
            return new UnsupportedExecutionSurfaceException(
                surfaceName: "ControlFlow",
                slotIndex,
                opCode,
                bundlePc);
        }

        public static UnsupportedExecutionSurfaceException CreateForStreamControl(
            int slotIndex,
            uint opCode,
            ulong bundlePc)
        {
            return new UnsupportedExecutionSurfaceException(
                surfaceName: "StreamControl",
                slotIndex,
                opCode,
                bundlePc);
        }
    }
}
