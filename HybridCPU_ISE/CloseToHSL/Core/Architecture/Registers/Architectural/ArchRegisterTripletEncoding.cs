using System;

namespace YAKSys_Hybrid_CPU.Core.Registers
{
    /// <summary>
    /// Core-owned architectural register triplet encoding used by runtime helper contours.
    /// Keeps register packing out of legacy CompilerEnv surfaces.
    /// </summary>
    internal static class ArchRegisterTripletEncoding
    {
        internal const ushort NoReg = ushort.MaxValue;
        internal const byte NoArchReg = byte.MaxValue;

        internal static ulong Pack(byte rd, byte rs1, byte rs2)
        {
            static ulong PackField(byte regId, string paramName)
            {
                if (regId == NoArchReg)
                {
                    return NoReg;
                }

                if (regId > ArchRegId.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(
                        paramName,
                        regId,
                        $"Architectural register id must be in [0, {ArchRegId.MaxValue}] or NoArchReg.");
                }

                return regId;
            }

            return PackField(rd, nameof(rd))
                | (PackField(rs1, nameof(rs1)) << 16)
                | (PackField(rs2, nameof(rs2)) << 32);
        }

        internal static bool TryUnpack(ulong packedRegisters, out byte rd, out byte rs1, out byte rs2)
        {
            static bool TryDecodeField(ushort encoded, out byte regId)
            {
                if (encoded == NoReg)
                {
                    regId = NoArchReg;
                    return true;
                }

                if (encoded <= ArchRegId.MaxValue)
                {
                    regId = (byte)encoded;
                    return true;
                }

                regId = NoArchReg;
                return false;
            }

            rd = default;
            rs1 = default;
            rs2 = default;

            return TryDecodeField((ushort)(packedRegisters & 0xFFFF), out rd)
                && TryDecodeField((ushort)((packedRegisters >> 16) & 0xFFFF), out rs1)
                && TryDecodeField((ushort)((packedRegisters >> 32) & 0xFFFF), out rs2);
        }
    }
}
