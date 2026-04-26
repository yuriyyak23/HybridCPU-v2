using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Arch
{
    public partial struct VLIW_Instruction
    {
            // --- HLS-compatible register packing for the flat architectural Word1 contour ---
            // Canonical decode decides whether Word1 is interpreted through this packed ABI
            // from published opcode semantics, not from StreamLength-derived scalar heuristics.
            //   Word1: [15:0] Rd | [31:16] Rs1 | [47:32] Rs2
            // Word2 is used for memory address or auxiliary data (jump target, immediate, etc.)

            /// <summary>
            /// Sentinel register ID meaning "no register" / "register field unused".
            /// <para>
            /// Blueprint §3.69 / §5: "Возможно, явно запретить RegID=-1, а генерировать NoReg."
            /// Callers that formerly passed <c>-1</c> to <see cref="PackRegisters"/> to indicate
            /// an absent operand now receive this value from <see cref="Reg1ID"/> / <see cref="Reg2ID"/>
            /// / <see cref="Reg3ID"/> and should guard with <c>!= NoReg</c> before treating the
            /// field as an architectural register index.
            /// </para>
            /// </summary>
            public const ushort NoReg = ushort.MaxValue; // 0xFFFF

            /// <summary>
            /// Sentinel architectural register field meaning "operand not present".
            /// Encoded as <see cref="NoReg"/> in Word1.
            /// </summary>
            public const byte NoArchReg = byte.MaxValue; // 0xFF

            /// <summary>
            /// Packs up to 3 flat architectural register ids into Word1 for the packed-arch register contour.
            /// Register operands must be in [0, 31], or <see cref="NoArchReg"/> for an unused field.
            /// No VT/global-register information is permitted in this encoding.
            /// </summary>
            public static ulong PackArchRegs(byte rd, byte rs1, byte rs2)
            {
                static ulong PackField(byte regId, string paramName)
                {
                    if (regId == NoArchReg)
                    {
                        return NoReg;
                    }

                    if (regId > ArchRegId.MaxValue)
                    {
                        throw new ArgumentOutOfRangeException(paramName, regId,
                            $"Architectural register id must be in [0, {ArchRegId.MaxValue}] or NoArchReg.");
                    }

                    return regId;
                }

                ulong packed = 0;
                packed |= PackField(rd, nameof(rd));
                packed |= PackField(rs1, nameof(rs1)) << 16;
                packed |= PackField(rs2, nameof(rs2)) << 32;
                return packed;
            }

            /// <summary>
            /// Attempts to unpack flat architectural register ids from Word1.
            /// Returns false if any field contains a legacy/global register id or other invalid value.
            /// Unused fields decode as <see cref="NoArchReg"/>.
            /// </summary>
            public static bool TryUnpackArchRegs(ulong packedRegisters, out byte rd, out byte rs1, out byte rs2)
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

                    regId = default;
                    return false;
                }

                ushort rawRd = (ushort)(packedRegisters & 0xFFFF);
                ushort rawRs1 = (ushort)((packedRegisters >> 16) & 0xFFFF);
                ushort rawRs2 = (ushort)((packedRegisters >> 32) & 0xFFFF);

                if (!TryDecodeField(rawRd, out rd) ||
                    !TryDecodeField(rawRs1, out rs1) ||
                    !TryDecodeField(rawRs2, out rs2))
                {
                    rd = default;
                    rs1 = default;
                    rs2 = default;
                    return false;
                }

                return true;
            }

            /// <summary>
            /// Extracts the raw Word1 destination field.
            /// Use <see cref="TryUnpackArchRegs"/> to validate that it contains a flat architectural register id.
            /// </summary>
            public ushort Reg1ID
            {
                get { return (ushort)(word1 & 0xFFFF); }
            }

            /// <summary>
            /// Extracts the raw Word1 source-1 field.
            /// Use <see cref="TryUnpackArchRegs"/> to validate that it contains a flat architectural register id.
            /// </summary>
            public ushort Reg2ID
            {
                get { return (ushort)((word1 >> 16) & 0xFFFF); }
            }

            /// <summary>
            /// Extracts the raw Word1 source-2 field.
            /// Use <see cref="TryUnpackArchRegs"/> to validate that it contains a flat architectural register id.
            /// </summary>
            public ushort Reg3ID
            {
                get { return (ushort)((word1 >> 32) & 0xFFFF); }
            }
    }
}
