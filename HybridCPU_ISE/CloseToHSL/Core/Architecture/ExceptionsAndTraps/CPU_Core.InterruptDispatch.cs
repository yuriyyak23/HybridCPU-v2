using System;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public sealed partial class CPU_Core
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte DefaultInterruptDispatcher(
                Processor.DeviceType deviceType,
                ushort interruptId,
                ulong coreId) =>
                Processor.InterruptData.CallInterrupt(deviceType, interruptId, coreId);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal byte DispatchInterrupt(Processor.DeviceType deviceType, ushort interruptId)
            {
                Func<Processor.DeviceType, ushort, ulong, byte> interruptDispatcher =
                    _interruptDispatcher ?? DefaultInterruptDispatcher;

                return interruptDispatcher(deviceType, interruptId, CoreID);
            }
        }
    }
}
