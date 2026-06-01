namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            public enum InstructionsEnum : ushort
            {
                Nope = 0,

                // ═══════════════════════════════════════════════════
                // Retained Scalar Instructions (16–24, 35–42, 46–51)
                // ═══════════════════════════════════════════════════

                Interrupt = 16,
                InterruptReturn = 17,

                JumpIfEqual = 19,
                JumpIfNotEqual = 20,
                JumpIfBelow = 21,
                JumpIfBelowOrEqual = 22,
                JumpIfAbove = 23,
                JumpIfAboveOrEqual = 24,

                Move_Num = 35,
                Move = 36,

                Store = 37,
                Load = 38,

                Addition = 39,
                Subtraction = 40,
                Multiplication = 41,
                Division = 42,

                Modulus = 46,

                ShiftLeft = 47,
                ShiftRight = 48,

                XOR = 49,
                OR = 50,
                AND = 51,

                // ═══════════════════════════════════════════════════
                // Phase 03A: optional scalar branchless zero-select.
                // Slot 52 remains unallocated in the canonical enum; legacy raw
                // NOT handling there is not an executable ISA allocation.
                // ═══════════════════════════════════════════════════

                /// <summary>Conditional zero if rs2 == 0: rd = (rs2 == 0) ? 0 : rs1</summary>
                CZERO_EQZ = 53,

                // ═══════════════════════════════════════════════════
                // Phase 03B: optional scalar bitmanip first slice.
                // ═══════════════════════════════════════════════════

                /// <summary>Count leading zero bits in a 64-bit scalar operand</summary>
                CLZ = 54,

                // ═══════════════════════════════════════════════════
                // Phase 03C: optional scalar address-generation first slice.
                // Slot 55 remains the unsupported legacy XFMAC raw contour.
                // ═══════════════════════════════════════════════════

                /// <summary>Shift-left-one add: rd = (rs1 &lt;&lt; 1) + rs2, modulo 2^64</summary>
                SH1ADD = 56,

                // ═══════════════════════════════════════════════════
                // Phase 03D: optional scalar system counter first slice.
                // ═══════════════════════════════════════════════════

                /// <summary>Read hardware cycle counter CSR 0xC00 into rd</summary>
                RDCYCLE = 57,

                // ═══════════════════════════════════════════════════
                // Phase 03E: optional scalar carry-less first slice.
                // ═══════════════════════════════════════════════════

                /// <summary>Carry-less multiply low half over GF(2): rd = clmul(rs1, rs2)[63:0]</summary>
                CLMUL = 58,

                // ═══════════════════════════════════════════════════
                // Non-VMX CloseToRTL Iteration 03A: scalar bitmanip CTZ.
                // ═══════════════════════════════════════════════════

                /// <summary>Count trailing zero bits in a 64-bit scalar operand</summary>
                CTZ = 59,

                // ═══════════════════════════════════════════════════
                // Non-VMX CloseToRTL Iteration 03B: scalar byte/half extension.
                // ═══════════════════════════════════════════════════

                /// <summary>Sign-extend low 8 bits to a 64-bit scalar operand</summary>
                SEXT_B = 60,
                /// <summary>Sign-extend low 16 bits to a 64-bit scalar operand</summary>
                SEXT_H = 61,
                /// <summary>Zero-extend low 16 bits to a 64-bit scalar operand</summary>
                ZEXT_H = 62,

                // ═══════════════════════════════════════════════════
                // Non-VMX CloseToRTL Iteration 03C: scalar rotates.
                // ═══════════════════════════════════════════════════

                /// <summary>Rotate left: rd = rol64(rs1, rs2 &amp; 0x3F)</summary>
                ROL = 63,
                /// <summary>Rotate right: rd = ror64(rs1, rs2 &amp; 0x3F)</summary>
                ROR = 64,

                // ═══════════════════════════════════════════════════
                // Non-VMX CloseToRTL Phase 01: scalar boolean-invert.
                // ═══════════════════════════════════════════════════

                /// <summary>Bitwise and-not: rd = rs1 &amp; ~rs2</summary>
                ANDN = 65,
                /// <summary>Bitwise or-not: rd = rs1 | ~rs2</summary>
                ORN = 66,
                /// <summary>Bitwise xnor: rd = ~(rs1 ^ rs2)</summary>
                XNOR = 67,

                // ═══════════════════════════════════════════════════
                // ISA v2: Vector Core — Clean names (70–96, 100–121)
                // Naming: opcode = operation, DataType = element format,
                //         Flags = mode (immediate-present, saturating, etc.)
                // ═══════════════════════════════════════════════════

                // Vector Arithmetic
                /// <summary>Vector Add: vd[i] = vs1[i] + vs2[i]. Use Saturating flag for clamp.</summary>
                VADD = 70,
                /// <summary>Vector Subtract: vd[i] = vs1[i] - vs2[i]</summary>
                VSUB = 71,
                /// <summary>Vector Multiply: vd[i] = vs1[i] * vs2[i]</summary>
                VMUL = 72,
                /// <summary>Vector Divide: vd[i] = vs1[i] / vs2[i]</summary>
                VDIV = 73,

                // Vector Memory
                /// <summary>Vector Load: contiguous load from memory</summary>
                VLOAD = 75,
                /// <summary>Vector Store: contiguous store to memory</summary>
                VSTORE = 76,

                // Vector Logical
                /// <summary>Vector XOR: vd[i] = vs1[i] ^ vs2[i]</summary>
                VXOR = 77,
                /// <summary>Vector OR: vd[i] = vs1[i] | vs2[i]</summary>
                VOR = 78,
                /// <summary>Vector AND: vd[i] = vs1[i] &amp; vs2[i]</summary>
                VAND = 79,
                /// <summary>Vector NOT: vd[i] = ~vs1[i]</summary>
                VNOT = 80,
                /// <summary>Vector Square Root: vd[i] = sqrt(vs1[i])</summary>
                VSQRT = 81,
                /// <summary>Vector Modulus: vd[i] = vs1[i] % vs2[i]</summary>
                VMOD = 82,

                // Vector Comparison (generate predicate masks)
                /// <summary>Vector Compare Equal: vd[i] = (vs1[i] == vs2[i]) ? 1 : 0</summary>
                VCMPEQ = 83,
                /// <summary>Vector Compare Not Equal</summary>
                VCMPNE = 84,
                /// <summary>Vector Compare Less Than</summary>
                VCMPLT = 85,
                /// <summary>Vector Compare Less Than or Equal</summary>
                VCMPLE = 86,
                /// <summary>Vector Compare Greater Than</summary>
                VCMPGT = 87,
                /// <summary>Vector Compare Greater Than or Equal</summary>
                VCMPGE = 88,

                // Predicate Mask Manipulation
                /// <summary>Mask AND: md = ms1 &amp; ms2</summary>
                VMAND = 89,
                /// <summary>Mask OR: md = ms1 | ms2</summary>
                VMOR = 90,
                /// <summary>Mask XOR: md = ms1 ^ ms2</summary>
                VMXOR = 91,
                /// <summary>Mask NOT: md = ~ms</summary>
                VMNOT = 92,
                /// <summary>Mask Population Count: count set bits in mask</summary>
                VPOPC = 93,

                // Vector Shift (immediate-present flag selects VI mode)
                /// <summary>Vector Shift Left Logical: vd[i] = vs1[i] &lt;&lt; vs2[i] (or imm)</summary>
                VSLL = 94,
                /// <summary>Vector Shift Right Logical: vd[i] = vs1[i] &gt;&gt; vs2[i] (or imm)</summary>
                VSRL = 95,
                /// <summary>Vector Shift Right Arithmetic: sign-extended</summary>
                VSRA = 96,

                // Vector FMA (Fused Multiply-Add)
                /// <summary>Vector FMA: vd[i] = (vd[i] * vs1[i]) + vs2[i]</summary>
                VFMADD = 100,
                /// <summary>Vector FMS: vd[i] = (vd[i] * vs1[i]) - vs2[i]</summary>
                VFMSUB = 101,
                /// <summary>Vector Negative FMA: vd[i] = -(vd[i] * vs1[i]) + vs2[i]</summary>
                VFNMADD = 102,
                /// <summary>Vector Negative FMS: vd[i] = -(vd[i] * vs1[i]) - vs2[i]</summary>
                VFNMSUB = 103,

                // Vector Min/Max
                /// <summary>Vector Minimum (signed): vd[i] = min(vs1[i], vs2[i])</summary>
                VMIN = 104,
                /// <summary>Vector Maximum (signed): vd[i] = max(vs1[i], vs2[i])</summary>
                VMAX = 105,
                /// <summary>Vector Minimum Unsigned</summary>
                VMINU = 106,
                /// <summary>Vector Maximum Unsigned</summary>
                VMAXU = 107,

                // Vector Reductions (collapse to scalar)
                /// <summary>Vector Reduce Sum: vd[0] = sum(vs1[0:vl-1])</summary>
                VREDSUM = 108,
                /// <summary>Vector Reduce Max (signed)</summary>
                VREDMAX = 109,
                /// <summary>Vector Reduce Min (signed)</summary>
                VREDMIN = 110,
                /// <summary>Vector Reduce Max Unsigned</summary>
                VREDMAXU = 111,
                /// <summary>Vector Reduce Min Unsigned</summary>
                VREDMINU = 112,
                /// <summary>Vector Reduce AND</summary>
                VREDAND = 113,
                /// <summary>Vector Reduce OR</summary>
                VREDOR = 114,
                /// <summary>Vector Reduce XOR</summary>
                VREDXOR = 115,

                // Vector Configuration (VLA / strip-mining)
                /// <summary>Set Vector Length: rd = vsetvl(rs1=AVL, rs2=vtype)</summary>
                VSETVL = 116,
                /// <summary>Set Vector Length Immediate: rd = vsetvli(rs1=AVL, imm=vtype)</summary>
                VSETVLI = 117,
                /// <summary>Set Vector Length Immediate AVL: rd = vsetivli(imm=AVL, imm=vtype)</summary>
                VSETIVLI = 118,

                // Dot Product (ML/DSP)
                /// <summary>Vector Dot Product (signed): scalar = sum(vs1[i] * vs2[i])</summary>
                VDOT = 119,
                /// <summary>Vector Dot Product Unsigned</summary>
                VDOTU = 120,
                /// <summary>Vector Dot Product Float (FP32/FP64)</summary>
                VDOTF = 121,

                // Vector mask prefix (Phase 04)
                /// <summary>Mask set-before-first: md[i] = true before the first set source bit within VL</summary>
                VMSBF = 122,

                // Vector widen/narrow/convert (Phase 05)
                /// <summary>Vector zero-extend: widen unsigned source elements into the next unsigned element width</summary>
                VZEXT = 123,

                // Vector scan / prefix (Phase 05A)
                /// <summary>Vector inclusive prefix sum: vd[i] = sum(vs[0:i]) over active lanes</summary>
                VSCAN_SUM = 124,

                // Predicative Movement (ARM SVE style)
                /// <summary>Vector Compress: pack active elements left, skip masked-off</summary>
                VCOMPRESS = 136,
                /// <summary>Vector Expand: unpack elements according to mask</summary>
                VEXPAND = 137,

                // Bit Manipulation
                /// <summary>Vector Reverse bits in each element</summary>
                VREVERSE = 138,
                /// <summary>Vector Population Count per element</summary>
                VPOPCNT = 139,
                /// <summary>Vector Count Leading Zeros per element</summary>
                VCLZ = 140,
                /// <summary>Vector Count Trailing Zeros per element</summary>
                VCTZ = 141,
                /// <summary>Vector Byte Reverse per element (endianness swap)</summary>
                VBREV8 = 142,

                // Permutation / Shuffle
                /// <summary>Vector Permute: vd[i] = vs1[vs2[i]] (indexed permutation)</summary>
                VPERMUTE = 143,
                /// <summary>Vector Slide Up by immediate</summary>
                VSLIDEUP = 144,
                /// <summary>Vector Slide Down by immediate</summary>
                VSLIDEDOWN = 145,
                /// <summary>Vector Register Gather: vd[i] = vs1[vs2[i]]</summary>
                VRGATHER = 146,

                // CSR (Control & Status Register) Instructions
                CSR_CLEAR = 149,    // Clear exception counters

                // Vector Exception Control Instructions
                VSETVEXCPMASK = 150,  // Set vector exception mask: writes rs1[4:0] в†’ VEXCPMASK CSR
                VSETVEXCPPRI = 151,   // Set vector exception priorities

                // ═══════════════════════════════════════════════════
                // ISA v2: RISC-VV Extension — Scalar Immediate (152–160)
                // ISA_V4_AUDIT: "RISC-VV" naming predates ISA v4; these instructions are canonical ISA v4 scalar immediates. Section header will be cleaned up in Phase 03.
                // ═══════════════════════════════════════════════════

                /// <summary>rd = rs1 + sign_extend(imm)</summary>
                ADDI = 152,
                /// <summary>rd = rs1 &amp; sign_extend(imm)</summary>
                ANDI = 153,
                /// <summary>rd = rs1 | sign_extend(imm)</summary>
                ORI = 154,
                /// <summary>rd = rs1 ^ sign_extend(imm)</summary>
                XORI = 155,
                /// <summary>rd = (rs1 &lt; sign_extend(imm)) ? 1 : 0 (signed)</summary>
                SLTI = 156,
                /// <summary>rd = (rs1 &lt; sign_extend(imm)) ? 1 : 0 (unsigned)</summary>
                SLTIU = 157,
                /// <summary>rd = rs1 &lt;&lt; imm[5:0]</summary>
                SLLI = 158,
                /// <summary>rd = rs1 &gt;&gt; imm[5:0] (logical, zero-fill)</summary>
                SRLI = 159,
                /// <summary>rd = rs1 &gt;&gt; imm[5:0] (arithmetic, sign-fill)</summary>
                SRAI = 160,

                // ═══════════════════════════════════════════════════
                // ISA v2: Compare/Set (161–162)
                // ═══════════════════════════════════════════════════

                /// <summary>rd = (rs1 &lt; rs2) ? 1 : 0 (signed)</summary>
                SLT = 161,
                /// <summary>rd = (rs1 &lt; rs2) ? 1 : 0 (unsigned)</summary>
                SLTU = 162,

                // ═══════════════════════════════════════════════════
                // ISA v2: Upper Immediate / PC-relative (163–164)
                // ═══════════════════════════════════════════════════

                /// <summary>rd = imm20 &lt;&lt; 12 (load upper immediate)</summary>
                LUI = 163,
                /// <summary>rd = PC + (imm20 &lt;&lt; 12) (add upper immediate to PC)</summary>
                AUIPC = 164,

                // ═══════════════════════════════════════════════════
                // ISA v2: Typed Loads (165–171) — sign/zero extension
                // ═══════════════════════════════════════════════════

                /// <summary>Load byte, sign-extend to 64-bit</summary>
                LB = 165,
                /// <summary>Load byte, zero-extend</summary>
                LBU = 166,
                /// <summary>Load halfword (16-bit), sign-extend</summary>
                LH = 167,
                /// <summary>Load halfword, zero-extend</summary>
                LHU = 168,
                /// <summary>Load word (32-bit), sign-extend</summary>
                LW = 169,
                /// <summary>Load word, zero-extend</summary>
                LWU = 170,
                /// <summary>Load doubleword (64-bit)</summary>
                LD = 171,

                // ═══════════════════════════════════════════════════
                // ISA v2: Typed Stores (172–175)
                // ═══════════════════════════════════════════════════

                /// <summary>Store byte: mem[rs1+off] = rs2[7:0]</summary>
                SB = 172,
                /// <summary>Store halfword: mem[rs1+off] = rs2[15:0]</summary>
                SH = 173,
                /// <summary>Store word: mem[rs1+off] = rs2[31:0]</summary>
                SW = 174,
                /// <summary>Store doubleword: mem[rs1+off] = rs2[63:0]</summary>
                SD = 175,

                // ═══════════════════════════════════════════════════
                // ISA v2: Control Flow — RISC-V style (176–183)
                // ISA_V4_AUDIT: "RISC-V style" label — these are canonical ISA v4 control flow instructions. Section header will be cleaned up in Phase 03.
                // ═══════════════════════════════════════════════════

                /// <summary>Jump And Link: rd = PC+4; PC = PC + offset</summary>
                JAL = 176,
                /// <summary>Jump And Link Register: rd = PC+4; PC = (rs1 + offset) &amp; ~1</summary>
                JALR = 177,
                /// <summary>Branch if Equal: if (rs1 == rs2) PC += offset</summary>
                BEQ = 178,
                /// <summary>Branch if Not Equal: if (rs1 != rs2) PC += offset</summary>
                BNE = 179,
                /// <summary>Branch if Less Than (signed)</summary>
                BLT = 180,
                /// <summary>Branch if Greater or Equal (signed)</summary>
                BGE = 181,
                /// <summary>Branch if Less Than (unsigned)</summary>
                BLTU = 182,
                /// <summary>Branch if Greater or Equal (unsigned)</summary>
                BGEU = 183,

                // ═══════════════════════════════════════════════════
                // ISA v2: Atomic Extension (184–196) — LR/SC + AMO
                // ═══════════════════════════════════════════════════

                /// <summary>Load-Reserved Word: rd = mem[rs1]; reserve address</summary>
                LR_W = 184,
                /// <summary>Store-Conditional Word: mem[rs1] = rs2 if reserved; rd = 0 success, 1 fail</summary>
                SC_W = 185,
                /// <summary>Load-Reserved Doubleword</summary>
                LR_D = 186,
                /// <summary>Store-Conditional Doubleword</summary>
                SC_D = 187,
                /// <summary>Atomic Add Word: rd = mem[rs1]; mem[rs1] += rs2</summary>
                AMOADD_W = 188,
                /// <summary>Atomic Swap Word: rd = mem[rs1]; mem[rs1] = rs2</summary>
                AMOSWAP_W = 189,
                /// <summary>Atomic OR Word: rd = mem[rs1]; mem[rs1] |= rs2</summary>
                AMOOR_W = 190,
                /// <summary>Atomic AND Word</summary>
                AMOAND_W = 191,
                /// <summary>Atomic XOR Word</summary>
                AMOXOR_W = 192,
                /// <summary>Atomic MIN Word (signed)</summary>
                AMOMIN_W = 193,
                /// <summary>Atomic MAX Word (signed)</summary>
                AMOMAX_W = 194,
                /// <summary>Atomic MIN Word (unsigned)</summary>
                AMOMINU_W = 195,
                /// <summary>Atomic MAX Word (unsigned)</summary>
                AMOMAXU_W = 196,

                // ═══════════════════════════════════════════════════
                // ISA v4: Scalar M-Extension — MUL high-half / unsigned divide+remainder
                // These opcodes were missing from v3 baseline and are required for full
                // ISA v4 M-extension compliance. Enum slots 220–226.
                // ═══════════════════════════════════════════════════

                /// <summary>Multiply High-Half (signed×signed): rd = (rs1 * rs2)[127:64]</summary>
                MULH = 220,
                /// <summary>Multiply High-Half (unsigned×unsigned): rd = (rs1 * rs2)[127:64]</summary>
                MULHU = 221,
                /// <summary>Multiply High-Half (signed×unsigned): rd = (rs1 * rs2)[127:64]</summary>
                MULHSU = 222,
                /// <summary>Unsigned Divide: rd = rs1 / rs2 (unsigned). Returns 2^XLEN-1 if rs2==0.</summary>
                DIVU = 223,
                /// <summary>Signed Remainder: rd = rs1 % rs2 (signed). Returns rs1 if rs2==0.</summary>
                REM = 224,
                /// <summary>Unsigned Remainder: rd = rs1 % rs2 (unsigned). Returns rs1 if rs2==0.</summary>
                REMU = 225,

                // ═══════════════════════════════════════════════════
                // ISA v4: Atomic Doubleword Extension — AMO*_D family
                // Completes the full 64-bit atomic operation plane.
                // All operations are atomic with respect to LR/SC on the same address.
                // Enum slots 230–238.
                // ═══════════════════════════════════════════════════

                /// <summary>Atomic Add Doubleword: rd = mem[rs1]; mem[rs1] += rs2</summary>
                AMOADD_D = 230,
                /// <summary>Atomic Swap Doubleword: rd = mem[rs1]; mem[rs1] = rs2</summary>
                AMOSWAP_D = 231,
                /// <summary>Atomic OR Doubleword: rd = mem[rs1]; mem[rs1] |= rs2</summary>
                AMOOR_D = 232,
                /// <summary>Atomic AND Doubleword: rd = mem[rs1]; mem[rs1] &amp;= rs2</summary>
                AMOAND_D = 233,
                /// <summary>Atomic XOR Doubleword: rd = mem[rs1]; mem[rs1] ^= rs2</summary>
                AMOXOR_D = 234,
                /// <summary>Atomic MIN Doubleword (signed): rd = mem[rs1]; mem[rs1] = min(signed)(mem[rs1], rs2)</summary>
                AMOMIN_D = 235,
                /// <summary>Atomic MAX Doubleword (signed): rd = mem[rs1]; mem[rs1] = max(signed)(mem[rs1], rs2)</summary>
                AMOMAX_D = 236,
                /// <summary>Atomic MIN Doubleword (unsigned)</summary>
                AMOMINU_D = 237,
                /// <summary>Atomic MAX Doubleword (unsigned)</summary>
                AMOMAXU_D = 238,

                // ═══════════════════════════════════════════════════
                // ISA v4: SMT / VT Synchronisation — mandatory core
                // Enum slots 240–244.
                // ═══════════════════════════════════════════════════

                /// <summary>Yield virtual thread: surrender remainder of time slot</summary>
                YIELD = 240,
                /// <summary>Wait For Event: suspend until SEV is issued on this pod</summary>
                WFE = 241,
                /// <summary>Send Event: wake all threads waiting on WFE in this pod</summary>
                SEV = 242,
                /// <summary>Pod Barrier: synchronise all VTs in the same pod before proceeding</summary>
                POD_BARRIER = 243,
                /// <summary>VT Barrier: synchronise a specified subset of VTs</summary>
                VT_BARRIER = 244,

                // ═══════════════════════════════════════════════════
                // ISA v4: Lane6 DMA/Stream Compute - descriptor-backed native path
                // Enum slot 245.
                // ═══════════════════════════════════════════════════

                /// <summary>Descriptor-backed lane6 DMA/stream memory-memory compute.</summary>
                DmaStreamCompute = 245,
                /// <summary>Lane6 queue/control status query for a store-owned DMA/stream compute token.</summary>
                DSC_STATUS = 246,
                /// <summary>Lane6 capability query for the current DMA/stream compute runtime surface.</summary>
                DSC_QUERY_CAPS = 247,

                // ═══════════════════════════════════════════════════
                // L7-SDC: Lane7 System Device Command carriers.
                // Enum slots 260-266. Phase 08A opens ACCEL_STATUS only beyond
                // the Phase 08 current six-command contour.
                // ═══════════════════════════════════════════════════

                /// <summary>Query external accelerator capability metadata through the lane7 system-device command surface.</summary>
                ACCEL_QUERY_CAPS = 260,
                /// <summary>Submit an external accelerator command descriptor through the lane7 system-device command surface.</summary>
                ACCEL_SUBMIT = 261,
                /// <summary>Poll an external accelerator token/status through the lane7 system-device command surface.</summary>
                ACCEL_POLL = 262,
                /// <summary>Wait for an external accelerator token/status through the lane7 system-device command surface.</summary>
                ACCEL_WAIT = 263,
                /// <summary>Cancel an external accelerator command through the lane7 system-device command surface.</summary>
                ACCEL_CANCEL = 264,
                /// <summary>Fence external accelerator command visibility through the lane7 system-device command surface.</summary>
                ACCEL_FENCE = 265,
                /// <summary>Query guarded external accelerator token status through the lane7 system-device command surface.</summary>
                ACCEL_STATUS = 266,

                // ═══════════════════════════════════════════════════
                // ISA v4: VMX Instruction Plane — mandatory core
                // Enum slots 250–257.
                // ═══════════════════════════════════════════════════

                /// <summary>VMX On: enter VMX operation; VMX8 reserves and ignores the encoded operand.</summary>
                VMXON = 250,
                /// <summary>VMX Off: leave VMX operation</summary>
                VMXOFF = 251,
                /// <summary>VMX Launch: launch a new guest VM</summary>
                VMLAUNCH = 252,
                /// <summary>VMX Resume: resume a suspended guest VM</summary>
                VMRESUME = 253,
                /// <summary>VMX Read: rd = VMCS[field]</summary>
                VMREAD = 254,
                /// <summary>VMX Write: VMCS[rs1 field selector] = rs2</summary>
                VMWRITE = 255,
                /// <summary>VMX Clear: clear VMCS pointed to by rs1</summary>
                VMCLEAR = 256,
                /// <summary>VMX Pointer Load: load VMCS pointer from rs1</summary>
                VMPTRLD = 257,
                /// <summary>VMX Pointer Store: rd = current VMCS pointer</summary>
                VMPTRST = 258,
                /// <summary>VMX Call: guest-to-root VMExit with typed qualification</summary>
                VMCALL = 259,
                /// <summary>Invalidate extended page-translation context by scope</summary>
                INVEPT = 267,
                /// <summary>Invalidate virtual processor identifier context by scope</summary>
                INVVPID = 268,
                /// <summary>Invoke a policy-controlled VMX function leaf</summary>
                VMFUNC = 269,
                /// <summary>Save VMCS-owned vector/stream extended guest state by typed mask</summary>
                VMSAVEX = 270,
                /// <summary>Restore VMCS-owned vector/stream extended guest state by typed mask</summary>
                VMRESTX = 271,

                // ═══════════════════════════════════════════════════
                // ISA v2: Memory Ordering (197–198)
                // ═══════════════════════════════════════════════════

                /// <summary>Memory fence: order predecessor ops before successor ops</summary>
                FENCE = 197,
                /// <summary>Instruction fence: sync instruction cache with memory writes</summary>
                FENCE_I = 198,

                // ═══════════════════════════════════════════════════
                // ISA v2: Trap / Privileged (199–203)
                // ═══════════════════════════════════════════════════

                /// <summary>Environment Call: trap to OS/kernel (syscall number in a7)</summary>
                ECALL = 199,
                /// <summary>Environment Break: debug breakpoint trap</summary>
                EBREAK = 200,
                /// <summary>Machine Return: return from machine-mode trap handler</summary>
                MRET = 201,
                /// <summary>Supervisor Return: return from supervisor-mode trap handler</summary>
                SRET = 202,
                /// <summary>Wait For Interrupt: halt until IRQ (low-power idle)</summary>
                WFI = 203,

                // ═══════════════════════════════════════════════════
                // ISA v2: CSR Extension (204–209)
                // ═══════════════════════════════════════════════════

                /// <summary>CSR Atomic Read-Write: rd = CSR; CSR = rs1</summary>
                CSRRW = 204,
                /// <summary>CSR Read and Set Bits: rd = CSR; CSR |= rs1</summary>
                CSRRS = 205,
                /// <summary>CSR Read and Clear Bits: rd = CSR; CSR &amp;= ~rs1</summary>
                CSRRC = 206,
                /// <summary>CSR Write Immediate: rd = CSR; CSR = zero_ext(imm5)</summary>
                CSRRWI = 207,
                /// <summary>CSR Set Bits Immediate: rd = CSR; CSR |= zero_ext(imm5)</summary>
                CSRRSI = 208,
                /// <summary>CSR Clear Bits Immediate</summary>
                CSRRCI = 209,

                // ═══════════════════════════════════════════════════
                // ISA v2: Stream Engine (210–212)
                // ═══════════════════════════════════════════════════

                /// <summary>Configure stream descriptor: rs1=address, rs2=length</summary>
                STREAM_SETUP = 210,
                /// <summary>Start DMA transfer: rs1=stream descriptor ID</summary>
                STREAM_START = 211,
                /// <summary>Wait for stream completion: rs1=stream descriptor ID</summary>
                STREAM_WAIT = 212,

                // ═══════════════════════════════════════════════════
                // ISA v2: New Vector/Matrix from analysis (213–219)
                // Freed obsolete slots reused for new capability
                // ═══════════════════════════════════════════════════

                /// <summary>Vector Gather: vd[i] = mem[base + vs_index[i] * stride]. Indexed memory read.</summary>
                VGATHER = 213,
                /// <summary>Vector Scatter: mem[base + vs_index[i] * stride] = vs_data[i]. Indexed memory write.</summary>
                VSCATTER = 214,
                /// <summary>Vector Dot Product FP8: scalar = sum(DecodeFP8(vs1[i]) * DecodeFP8(vs2[i])). DataType selects E4M3/E5M2/NVFP8.</summary>
                VDOT_FP8 = 215,
                /// <summary>Matrix Tile Load: load 2D tile from memory into tile register file</summary>
                MTILE_LOAD = 216,
                /// <summary>Matrix Tile Store: store 2D tile from tile register file to memory</summary>
                MTILE_STORE = 217,
                /// <summary>Matrix Tile Multiply-Accumulate: tile_acc += tile_a * tile_b. DataType/descriptor control shape/dtype.</summary>
                MTILE_MACC = 218,
                /// <summary>Matrix Transpose: transpose tile in tile register file</summary>
                MTRANSPOSE = 219,

                // ═══════════════════════════════════════════════════
                // Instructions Refactor: mandatory scalar integer64 repair
                // Candidate allocation block 300-319.
                // ═══════════════════════════════════════════════════

                /// <summary>Arithmetic right shift: rd = (int64)rs1 >> (rs2 &amp; 0x3F)</summary>
                SRA = 300,
                /// <summary>Add immediate word: rd = sign_extend_64((uint32)rs1 + sign_extend(imm))</summary>
                ADDIW = 301,
                /// <summary>Add word: rd = sign_extend_64((uint32)rs1 + (uint32)rs2)</summary>
                ADDW = 302,
                /// <summary>Subtract word: rd = sign_extend_64((uint32)rs1 - (uint32)rs2)</summary>
                SUBW = 303,
                /// <summary>Shift left word: rd = sign_extend_64((uint32)rs1 &lt;&lt; (rs2 &amp; 0x1F))</summary>
                SLLW = 304,
                /// <summary>Logical right shift word: rd = sign_extend_64((uint32)rs1 &gt;&gt; (rs2 &amp; 0x1F))</summary>
                SRLW = 305,
                /// <summary>Arithmetic right shift word: rd = sign_extend_64((int32)rs1 &gt;&gt; (rs2 &amp; 0x1F))</summary>
                SRAW = 306,
                /// <summary>Shift left immediate word: rd = sign_extend_64((uint32)rs1 &lt;&lt; (imm &amp; 0x1F))</summary>
                SLLIW = 307,
                /// <summary>Logical right shift immediate word: rd = sign_extend_64((uint32)rs1 &gt;&gt; (imm &amp; 0x1F))</summary>
                SRLIW = 308,
                /// <summary>Arithmetic right shift immediate word: rd = sign_extend_64((int32)rs1 &gt;&gt; (imm &amp; 0x1F))</summary>
                SRAIW = 309,
                /// <summary>Multiply word: rd = sign_extend_64(low32(rs1) * low32(rs2))</summary>
                MULW = 310,
                /// <summary>Divide word: rd = sign_extend_64((int32)low32(rs1) / (int32)low32(rs2))</summary>
                DIVW = 311,
                /// <summary>Unsigned divide word: rd = sign_extend_64((uint32)low32(rs1) / (uint32)low32(rs2))</summary>
                DIVUW = 312,
                /// <summary>Remainder word: rd = sign_extend_64((int32)low32(rs1) % (int32)low32(rs2))</summary>
                REMW = 313,
                /// <summary>Unsigned remainder word: rd = sign_extend_64((uint32)low32(rs1) % (uint32)low32(rs2))</summary>
                REMUW = 314,

                // ═══════════════════════════════════════════════════
                // Explicit extension op block 320-330.
                // ═══════════════════════════════════════════════════

                /// <summary>Sign-extend word: rd = sign_extend_64(low32(rs1))</summary>
                SEXT_W = 320,
                /// <summary>Zero-extend word: rd = zero_extend_64(low32(rs1))</summary>
                ZEXT_W = 321,

                /// <summary>Vector slide-up by exactly one element over a packed 1D single-surface stream.</summary>
                VSLIDE1UP = 322,
                /// <summary>Wide vector dot product: FP16/BF16 inputs accumulate and publish one FP32 scalar result.</summary>
                VDOT_WIDE = 323,
                /// <summary>Vector slide-down by exactly one element over a packed 1D single-surface stream.</summary>
                VSLIDE1DOWN = 324,
                /// <summary>Two-source two-lane vector permute over a packed 1D pair.</summary>
                VPERM2 = 325,
                /// <summary>In-place 2x2 vector transpose over a packed 1D single-surface stream.</summary>
                VTRANSPOSE = 326,

                // Non-VMX CloseToRTL Phase 01C: scalar min/max.
                /// <summary>Signed minimum: rd = ((int64)rs1 &lt;= (int64)rs2) ? rs1 : rs2</summary>
                MIN = 327,
                /// <summary>Signed maximum: rd = ((int64)rs1 &gt;= (int64)rs2) ? rs1 : rs2</summary>
                MAX = 328,
                /// <summary>Unsigned minimum: rd = (rs1 &lt;= rs2) ? rs1 : rs2</summary>
                MINU = 329,
                /// <summary>Unsigned maximum: rd = (rs1 &gt;= rs2) ? rs1 : rs2</summary>
                MAXU = 330,

                // Non-VMX CloseToRTL Phase 01D: scalar byte/bit reverse.
                /// <summary>Reverse byte order across XLEN=64: byte 0 moves to byte 7.</summary>
                REV8 = 331,
                /// <summary>Reverse bit order inside each byte while preserving byte positions.</summary>
                BREV8 = 332,

                // Non-VMX CloseToRTL Phase 01E: scalar zeroing select.
                /// <summary>Conditional zero if rs2 != 0: rd = (rs2 != 0) ? 0 : rs1</summary>
                CZERO_NEZ = 333,

                // Non-VMX CloseToRTL Phase 01A closure: canonical scalar popcount.
                /// <summary>Population count over XLEN=64: rd = popcount(rs1)</summary>
                CPOP = 334,

                // Non-VMX CloseToRTL Phase 02: scalar rotate-immediate.
                /// <summary>Rotate left by imm6: rd = rol(rs1, imm6)</summary>
                ROLI = 335,
                /// <summary>Rotate right by imm6: rd = ror(rs1, imm6)</summary>
                RORI = 336,

                // Non-VMX CloseToRTL Phase 02: scalar register-indexed bitfield.
                /// <summary>Set indexed bit: rd = rs1 | (1UL &lt;&lt; (rs2 &amp; 0x3F))</summary>
                BSET = 337,
                /// <summary>Clear indexed bit: rd = rs1 &amp; ~(1UL &lt;&lt; (rs2 &amp; 0x3F))</summary>
                BCLR = 338,
                /// <summary>Invert indexed bit: rd = rs1 ^ (1UL &lt;&lt; (rs2 &amp; 0x3F))</summary>
                BINV = 339,
                /// <summary>Extract indexed bit: rd = (rs1 &gt;&gt; (rs2 &amp; 0x3F)) &amp; 1</summary>
                BEXT = 340,

                // Non-VMX CloseToRTL Phase 02: scalar immediate-indexed bitfield.
                /// <summary>Set immediate-indexed bit: rd = rs1 | (1UL &lt;&lt; imm6)</summary>
                BSETI = 341,
                /// <summary>Clear immediate-indexed bit: rd = rs1 &amp; ~(1UL &lt;&lt; imm6)</summary>
                BCLRI = 342,
                /// <summary>Invert immediate-indexed bit: rd = rs1 ^ (1UL &lt;&lt; imm6)</summary>
                BINVI = 343,
                /// <summary>Extract immediate-indexed bit: rd = (rs1 &gt;&gt; imm6) &amp; 1</summary>
                BEXTI = 344,

                // Non-VMX CloseToRTL Phase 03: scalar address-generation and .UW.
                /// <summary>Shift-left-two add: rd = (rs1 &lt;&lt; 2) + rs2, modulo 2^64</summary>
                SH2ADD = 345,
                /// <summary>Shift-left-three add: rd = (rs1 &lt;&lt; 3) + rs2, modulo 2^64</summary>
                SH3ADD = 346,
                /// <summary>Add unsigned word: rd = zero_extend_64(low32(rs1)) + rs2, modulo 2^64</summary>
                ADD_UW = 347,
                /// <summary>Shift-left-one add unsigned word: rd = (zero_extend_64(low32(rs1)) &lt;&lt; 1) + rs2, modulo 2^64</summary>
                SH1ADD_UW = 348,
                /// <summary>Shift-left-two add unsigned word: rd = (zero_extend_64(low32(rs1)) &lt;&lt; 2) + rs2, modulo 2^64</summary>
                SH2ADD_UW = 349,
                /// <summary>Shift-left-three add unsigned word: rd = (zero_extend_64(low32(rs1)) &lt;&lt; 3) + rs2, modulo 2^64</summary>
                SH3ADD_UW = 350,
                /// <summary>Shift-left logical unsigned word immediate: rd = zero_extend_64(low32(rs1)) &lt;&lt; imm6, modulo 2^64</summary>
                SLLI_UW = 351,

                // Non-VMX CloseToRTL Phase 04: scalar carry-less high/reversed windows.
                /// <summary>Carry-less multiply high half over GF(2): rd = clmul(rs1, rs2)[127:64]</summary>
                CLMULH = 352,
                /// <summary>Carry-less multiply reversed high window over GF(2): rd = clmul(rs1, rs2)[126:63]</summary>
                CLMULR = 353
            }

            public static class IsaOpcodeValues
            {
                public const ushort Nope = (ushort)InstructionsEnum.Nope;
                public const ushort Interrupt = (ushort)InstructionsEnum.Interrupt;
                public const ushort InterruptReturn = (ushort)InstructionsEnum.InterruptReturn;
                public const ushort JumpIfEqual = (ushort)InstructionsEnum.JumpIfEqual;
                public const ushort JumpIfNotEqual = (ushort)InstructionsEnum.JumpIfNotEqual;
                public const ushort JumpIfBelow = (ushort)InstructionsEnum.JumpIfBelow;
                public const ushort JumpIfBelowOrEqual = (ushort)InstructionsEnum.JumpIfBelowOrEqual;
                public const ushort JumpIfAbove = (ushort)InstructionsEnum.JumpIfAbove;
                public const ushort JumpIfAboveOrEqual = (ushort)InstructionsEnum.JumpIfAboveOrEqual;
                public const ushort Move_Num = (ushort)InstructionsEnum.Move_Num;
                public const ushort Move = (ushort)InstructionsEnum.Move;
                public const ushort Addition = (ushort)InstructionsEnum.Addition;
                public const ushort Subtraction = (ushort)InstructionsEnum.Subtraction;
                public const ushort Multiplication = (ushort)InstructionsEnum.Multiplication;
                public const ushort Division = (ushort)InstructionsEnum.Division;
                public const ushort Modulus = (ushort)InstructionsEnum.Modulus;
                public const ushort ShiftLeft = (ushort)InstructionsEnum.ShiftLeft;
                public const ushort ShiftRight = (ushort)InstructionsEnum.ShiftRight;
                public const ushort SRA = (ushort)InstructionsEnum.SRA;
                public const ushort ADDIW = (ushort)InstructionsEnum.ADDIW;
                public const ushort ADDW = (ushort)InstructionsEnum.ADDW;
                public const ushort SUBW = (ushort)InstructionsEnum.SUBW;
                public const ushort SLLW = (ushort)InstructionsEnum.SLLW;
                public const ushort SRLW = (ushort)InstructionsEnum.SRLW;
                public const ushort SRAW = (ushort)InstructionsEnum.SRAW;
                public const ushort SLLIW = (ushort)InstructionsEnum.SLLIW;
                public const ushort SRLIW = (ushort)InstructionsEnum.SRLIW;
                public const ushort SRAIW = (ushort)InstructionsEnum.SRAIW;
                public const ushort MULW = (ushort)InstructionsEnum.MULW;
                public const ushort DIVW = (ushort)InstructionsEnum.DIVW;
                public const ushort DIVUW = (ushort)InstructionsEnum.DIVUW;
                public const ushort REMW = (ushort)InstructionsEnum.REMW;
                public const ushort REMUW = (ushort)InstructionsEnum.REMUW;
                public const ushort SEXT_W = (ushort)InstructionsEnum.SEXT_W;
                public const ushort ZEXT_W = (ushort)InstructionsEnum.ZEXT_W;
                public const ushort XOR = (ushort)InstructionsEnum.XOR;
                public const ushort OR = (ushort)InstructionsEnum.OR;
                public const ushort AND = (ushort)InstructionsEnum.AND;
                public const ushort CZERO_EQZ = (ushort)InstructionsEnum.CZERO_EQZ;
                public const ushort CLZ = (ushort)InstructionsEnum.CLZ;
                public const ushort SH1ADD = (ushort)InstructionsEnum.SH1ADD;
                public const ushort RDCYCLE = (ushort)InstructionsEnum.RDCYCLE;
                public const ushort CLMUL = (ushort)InstructionsEnum.CLMUL;
                public const ushort CTZ = (ushort)InstructionsEnum.CTZ;
                public const ushort SEXT_B = (ushort)InstructionsEnum.SEXT_B;
                public const ushort SEXT_H = (ushort)InstructionsEnum.SEXT_H;
                public const ushort ZEXT_H = (ushort)InstructionsEnum.ZEXT_H;
                public const ushort ROL = (ushort)InstructionsEnum.ROL;
                public const ushort ROR = (ushort)InstructionsEnum.ROR;
                public const ushort ANDN = (ushort)InstructionsEnum.ANDN;
                public const ushort ORN = (ushort)InstructionsEnum.ORN;
                public const ushort XNOR = (ushort)InstructionsEnum.XNOR;
                public const ushort MIN = (ushort)InstructionsEnum.MIN;
                public const ushort MAX = (ushort)InstructionsEnum.MAX;
                public const ushort MINU = (ushort)InstructionsEnum.MINU;
                public const ushort MAXU = (ushort)InstructionsEnum.MAXU;
                public const ushort REV8 = (ushort)InstructionsEnum.REV8;
                public const ushort BREV8 = (ushort)InstructionsEnum.BREV8;
                public const ushort CZERO_NEZ = (ushort)InstructionsEnum.CZERO_NEZ;
                public const ushort CPOP = (ushort)InstructionsEnum.CPOP;
                public const ushort ROLI = (ushort)InstructionsEnum.ROLI;
                public const ushort RORI = (ushort)InstructionsEnum.RORI;
                public const ushort BSET = (ushort)InstructionsEnum.BSET;
                public const ushort BCLR = (ushort)InstructionsEnum.BCLR;
                public const ushort BINV = (ushort)InstructionsEnum.BINV;
                public const ushort BEXT = (ushort)InstructionsEnum.BEXT;
                public const ushort BSETI = (ushort)InstructionsEnum.BSETI;
                public const ushort BCLRI = (ushort)InstructionsEnum.BCLRI;
                public const ushort BINVI = (ushort)InstructionsEnum.BINVI;
                public const ushort BEXTI = (ushort)InstructionsEnum.BEXTI;
                public const ushort SH2ADD = (ushort)InstructionsEnum.SH2ADD;
                public const ushort SH3ADD = (ushort)InstructionsEnum.SH3ADD;
                public const ushort ADD_UW = (ushort)InstructionsEnum.ADD_UW;
                public const ushort SH1ADD_UW = (ushort)InstructionsEnum.SH1ADD_UW;
                public const ushort SH2ADD_UW = (ushort)InstructionsEnum.SH2ADD_UW;
                public const ushort SH3ADD_UW = (ushort)InstructionsEnum.SH3ADD_UW;
                public const ushort SLLI_UW = (ushort)InstructionsEnum.SLLI_UW;
                public const ushort CLMULH = (ushort)InstructionsEnum.CLMULH;
                public const ushort CLMULR = (ushort)InstructionsEnum.CLMULR;

                public const ushort VADD = (ushort)InstructionsEnum.VADD;
                public const ushort VSUB = (ushort)InstructionsEnum.VSUB;
                public const ushort VMUL = (ushort)InstructionsEnum.VMUL;
                public const ushort VDIV = (ushort)InstructionsEnum.VDIV;
                public const ushort VLOAD = (ushort)InstructionsEnum.VLOAD;
                public const ushort VSTORE = (ushort)InstructionsEnum.VSTORE;
                public const ushort VXOR = (ushort)InstructionsEnum.VXOR;
                public const ushort VOR = (ushort)InstructionsEnum.VOR;
                public const ushort VAND = (ushort)InstructionsEnum.VAND;
                public const ushort VNOT = (ushort)InstructionsEnum.VNOT;
                public const ushort VSQRT = (ushort)InstructionsEnum.VSQRT;
                public const ushort VMOD = (ushort)InstructionsEnum.VMOD;
                public const ushort VCMPEQ = (ushort)InstructionsEnum.VCMPEQ;
                public const ushort VCMPNE = (ushort)InstructionsEnum.VCMPNE;
                public const ushort VCMPLT = (ushort)InstructionsEnum.VCMPLT;
                public const ushort VCMPLE = (ushort)InstructionsEnum.VCMPLE;
                public const ushort VCMPGT = (ushort)InstructionsEnum.VCMPGT;
                public const ushort VCMPGE = (ushort)InstructionsEnum.VCMPGE;
                public const ushort VMAND = (ushort)InstructionsEnum.VMAND;
                public const ushort VMOR = (ushort)InstructionsEnum.VMOR;
                public const ushort VMXOR = (ushort)InstructionsEnum.VMXOR;
                public const ushort VMNOT = (ushort)InstructionsEnum.VMNOT;
                public const ushort VPOPC = (ushort)InstructionsEnum.VPOPC;
                public const ushort VSLL = (ushort)InstructionsEnum.VSLL;
                public const ushort VSRL = (ushort)InstructionsEnum.VSRL;
                public const ushort VSRA = (ushort)InstructionsEnum.VSRA;
                public const ushort VFMADD = (ushort)InstructionsEnum.VFMADD;
                public const ushort VFMSUB = (ushort)InstructionsEnum.VFMSUB;
                public const ushort VFNMADD = (ushort)InstructionsEnum.VFNMADD;
                public const ushort VFNMSUB = (ushort)InstructionsEnum.VFNMSUB;
                public const ushort VMIN = (ushort)InstructionsEnum.VMIN;
                public const ushort VMAX = (ushort)InstructionsEnum.VMAX;
                public const ushort VMINU = (ushort)InstructionsEnum.VMINU;
                public const ushort VMAXU = (ushort)InstructionsEnum.VMAXU;
                public const ushort VREDSUM = (ushort)InstructionsEnum.VREDSUM;
                public const ushort VREDMAX = (ushort)InstructionsEnum.VREDMAX;
                public const ushort VREDMIN = (ushort)InstructionsEnum.VREDMIN;
                public const ushort VREDMAXU = (ushort)InstructionsEnum.VREDMAXU;
                public const ushort VREDMINU = (ushort)InstructionsEnum.VREDMINU;
                public const ushort VREDAND = (ushort)InstructionsEnum.VREDAND;
                public const ushort VREDOR = (ushort)InstructionsEnum.VREDOR;
                public const ushort VREDXOR = (ushort)InstructionsEnum.VREDXOR;
                public const ushort VDOT = (ushort)InstructionsEnum.VDOT;
                public const ushort VDOTU = (ushort)InstructionsEnum.VDOTU;
                public const ushort VDOTF = (ushort)InstructionsEnum.VDOTF;
                public const ushort VMSBF = (ushort)InstructionsEnum.VMSBF;
                public const ushort VZEXT = (ushort)InstructionsEnum.VZEXT;
                public const ushort VSCAN_SUM = (ushort)InstructionsEnum.VSCAN_SUM;
                public const ushort VCOMPRESS = (ushort)InstructionsEnum.VCOMPRESS;
                public const ushort VEXPAND = (ushort)InstructionsEnum.VEXPAND;
                public const ushort VREVERSE = (ushort)InstructionsEnum.VREVERSE;
                public const ushort VPOPCNT = (ushort)InstructionsEnum.VPOPCNT;
                public const ushort VCLZ = (ushort)InstructionsEnum.VCLZ;
                public const ushort VCTZ = (ushort)InstructionsEnum.VCTZ;
                public const ushort VBREV8 = (ushort)InstructionsEnum.VBREV8;
                public const ushort VPERMUTE = (ushort)InstructionsEnum.VPERMUTE;
                public const ushort VSLIDEUP = (ushort)InstructionsEnum.VSLIDEUP;
                public const ushort VSLIDEDOWN = (ushort)InstructionsEnum.VSLIDEDOWN;
                public const ushort VRGATHER = (ushort)InstructionsEnum.VRGATHER;
                public const ushort VSLIDE1UP = (ushort)InstructionsEnum.VSLIDE1UP;
                public const ushort VSLIDE1DOWN = (ushort)InstructionsEnum.VSLIDE1DOWN;
                public const ushort VPERM2 = (ushort)InstructionsEnum.VPERM2;
                public const ushort VTRANSPOSE = (ushort)InstructionsEnum.VTRANSPOSE;

                public const ushort ADDI = (ushort)InstructionsEnum.ADDI;
                public const ushort ANDI = (ushort)InstructionsEnum.ANDI;
                public const ushort ORI = (ushort)InstructionsEnum.ORI;
                public const ushort XORI = (ushort)InstructionsEnum.XORI;
                public const ushort SLTI = (ushort)InstructionsEnum.SLTI;
                public const ushort SLTIU = (ushort)InstructionsEnum.SLTIU;
                public const ushort SLLI = (ushort)InstructionsEnum.SLLI;
                public const ushort SRLI = (ushort)InstructionsEnum.SRLI;
                public const ushort SRAI = (ushort)InstructionsEnum.SRAI;
                public const ushort SLT = (ushort)InstructionsEnum.SLT;
                public const ushort SLTU = (ushort)InstructionsEnum.SLTU;
                public const ushort LUI = (ushort)InstructionsEnum.LUI;
                public const ushort AUIPC = (ushort)InstructionsEnum.AUIPC;

                public const ushort Load = (ushort)InstructionsEnum.Load;
                public const ushort Store = (ushort)InstructionsEnum.Store;
                public const ushort LB = (ushort)InstructionsEnum.LB;
                public const ushort LBU = (ushort)InstructionsEnum.LBU;
                public const ushort LH = (ushort)InstructionsEnum.LH;
                public const ushort LHU = (ushort)InstructionsEnum.LHU;
                public const ushort LW = (ushort)InstructionsEnum.LW;
                public const ushort LWU = (ushort)InstructionsEnum.LWU;
                public const ushort LD = (ushort)InstructionsEnum.LD;
                public const ushort SB = (ushort)InstructionsEnum.SB;
                public const ushort SH = (ushort)InstructionsEnum.SH;
                public const ushort SW = (ushort)InstructionsEnum.SW;
                public const ushort SD = (ushort)InstructionsEnum.SD;

                public const ushort VGATHER = (ushort)InstructionsEnum.VGATHER;
                public const ushort VSCATTER = (ushort)InstructionsEnum.VSCATTER;
                public const ushort MTILE_LOAD = (ushort)InstructionsEnum.MTILE_LOAD;
                public const ushort MTILE_STORE = (ushort)InstructionsEnum.MTILE_STORE;

                public const ushort JAL = (ushort)InstructionsEnum.JAL;
                public const ushort JALR = (ushort)InstructionsEnum.JALR;
                public const ushort BEQ = (ushort)InstructionsEnum.BEQ;
                public const ushort BNE = (ushort)InstructionsEnum.BNE;
                public const ushort BLT = (ushort)InstructionsEnum.BLT;
                public const ushort BGE = (ushort)InstructionsEnum.BGE;
                public const ushort BLTU = (ushort)InstructionsEnum.BLTU;
                public const ushort BGEU = (ushort)InstructionsEnum.BGEU;

                public const ushort LR_W = (ushort)InstructionsEnum.LR_W;
                public const ushort SC_W = (ushort)InstructionsEnum.SC_W;
                public const ushort LR_D = (ushort)InstructionsEnum.LR_D;
                public const ushort SC_D = (ushort)InstructionsEnum.SC_D;
                public const ushort AMOADD_W = (ushort)InstructionsEnum.AMOADD_W;
                public const ushort AMOSWAP_W = (ushort)InstructionsEnum.AMOSWAP_W;
                public const ushort AMOOR_W = (ushort)InstructionsEnum.AMOOR_W;
                public const ushort AMOAND_W = (ushort)InstructionsEnum.AMOAND_W;
                public const ushort AMOXOR_W = (ushort)InstructionsEnum.AMOXOR_W;
                public const ushort AMOMIN_W = (ushort)InstructionsEnum.AMOMIN_W;
                public const ushort AMOMAX_W = (ushort)InstructionsEnum.AMOMAX_W;
                public const ushort AMOMINU_W = (ushort)InstructionsEnum.AMOMINU_W;
                public const ushort AMOMAXU_W = (ushort)InstructionsEnum.AMOMAXU_W;
                public const ushort MULH = (ushort)InstructionsEnum.MULH;
                public const ushort MULHU = (ushort)InstructionsEnum.MULHU;
                public const ushort MULHSU = (ushort)InstructionsEnum.MULHSU;
                public const ushort DIVU = (ushort)InstructionsEnum.DIVU;
                public const ushort REM = (ushort)InstructionsEnum.REM;
                public const ushort REMU = (ushort)InstructionsEnum.REMU;
                public const ushort AMOADD_D = (ushort)InstructionsEnum.AMOADD_D;
                public const ushort AMOSWAP_D = (ushort)InstructionsEnum.AMOSWAP_D;
                public const ushort AMOOR_D = (ushort)InstructionsEnum.AMOOR_D;
                public const ushort AMOAND_D = (ushort)InstructionsEnum.AMOAND_D;
                public const ushort AMOXOR_D = (ushort)InstructionsEnum.AMOXOR_D;
                public const ushort AMOMIN_D = (ushort)InstructionsEnum.AMOMIN_D;
                public const ushort AMOMAX_D = (ushort)InstructionsEnum.AMOMAX_D;
                public const ushort AMOMINU_D = (ushort)InstructionsEnum.AMOMINU_D;
                public const ushort AMOMAXU_D = (ushort)InstructionsEnum.AMOMAXU_D;

                public const ushort FENCE = (ushort)InstructionsEnum.FENCE;
                public const ushort FENCE_I = (ushort)InstructionsEnum.FENCE_I;
                public const ushort ECALL = (ushort)InstructionsEnum.ECALL;
                public const ushort EBREAK = (ushort)InstructionsEnum.EBREAK;
                public const ushort MRET = (ushort)InstructionsEnum.MRET;
                public const ushort SRET = (ushort)InstructionsEnum.SRET;
                public const ushort WFI = (ushort)InstructionsEnum.WFI;
                public const ushort VSETVL = (ushort)InstructionsEnum.VSETVL;
                public const ushort VSETVLI = (ushort)InstructionsEnum.VSETVLI;
                public const ushort VSETIVLI = (ushort)InstructionsEnum.VSETIVLI;
                public const ushort CSRRW = (ushort)InstructionsEnum.CSRRW;
                public const ushort CSRRS = (ushort)InstructionsEnum.CSRRS;
                public const ushort CSRRC = (ushort)InstructionsEnum.CSRRC;
                public const ushort CSRRWI = (ushort)InstructionsEnum.CSRRWI;
                public const ushort CSRRSI = (ushort)InstructionsEnum.CSRRSI;
                public const ushort CSRRCI = (ushort)InstructionsEnum.CSRRCI;
                public const ushort CSR_CLEAR = (ushort)InstructionsEnum.CSR_CLEAR;
                public const ushort VSETVEXCPMASK = (ushort)InstructionsEnum.VSETVEXCPMASK;
                public const ushort VSETVEXCPPRI = (ushort)InstructionsEnum.VSETVEXCPPRI;
                public const ushort STREAM_SETUP = (ushort)InstructionsEnum.STREAM_SETUP;
                public const ushort STREAM_START = (ushort)InstructionsEnum.STREAM_START;
                public const ushort STREAM_WAIT = (ushort)InstructionsEnum.STREAM_WAIT;
                public const ushort YIELD = (ushort)InstructionsEnum.YIELD;
                public const ushort WFE = (ushort)InstructionsEnum.WFE;
                public const ushort SEV = (ushort)InstructionsEnum.SEV;
                public const ushort POD_BARRIER = (ushort)InstructionsEnum.POD_BARRIER;
                public const ushort VT_BARRIER = (ushort)InstructionsEnum.VT_BARRIER;
                public const ushort DmaStreamCompute = (ushort)InstructionsEnum.DmaStreamCompute;
                public const ushort DSC_STATUS = (ushort)InstructionsEnum.DSC_STATUS;
                public const ushort DSC_QUERY_CAPS = (ushort)InstructionsEnum.DSC_QUERY_CAPS;
                public const ushort ACCEL_QUERY_CAPS = (ushort)InstructionsEnum.ACCEL_QUERY_CAPS;
                public const ushort ACCEL_SUBMIT = (ushort)InstructionsEnum.ACCEL_SUBMIT;
                public const ushort ACCEL_POLL = (ushort)InstructionsEnum.ACCEL_POLL;
                public const ushort ACCEL_WAIT = (ushort)InstructionsEnum.ACCEL_WAIT;
                public const ushort ACCEL_CANCEL = (ushort)InstructionsEnum.ACCEL_CANCEL;
                public const ushort ACCEL_FENCE = (ushort)InstructionsEnum.ACCEL_FENCE;
                public const ushort ACCEL_STATUS = (ushort)InstructionsEnum.ACCEL_STATUS;

                public const ushort VMXON = (ushort)InstructionsEnum.VMXON;
                public const ushort VMXOFF = (ushort)InstructionsEnum.VMXOFF;
                public const ushort VMLAUNCH = (ushort)InstructionsEnum.VMLAUNCH;
                public const ushort VMRESUME = (ushort)InstructionsEnum.VMRESUME;
                public const ushort VMREAD = (ushort)InstructionsEnum.VMREAD;
                public const ushort VMWRITE = (ushort)InstructionsEnum.VMWRITE;
                public const ushort VMCLEAR = (ushort)InstructionsEnum.VMCLEAR;
                public const ushort VMPTRLD = (ushort)InstructionsEnum.VMPTRLD;
                public const ushort VMPTRST = (ushort)InstructionsEnum.VMPTRST;
                public const ushort VMCALL = (ushort)InstructionsEnum.VMCALL;
                public const ushort INVEPT = (ushort)InstructionsEnum.INVEPT;
                public const ushort INVVPID = (ushort)InstructionsEnum.INVVPID;
                public const ushort VMFUNC = (ushort)InstructionsEnum.VMFUNC;
                public const ushort VMSAVEX = (ushort)InstructionsEnum.VMSAVEX;
                public const ushort VMRESTX = (ushort)InstructionsEnum.VMRESTX;
                public const ushort MTILE_MACC = (ushort)InstructionsEnum.MTILE_MACC;
                public const ushort MTRANSPOSE = (ushort)InstructionsEnum.MTRANSPOSE;
                public const ushort VDOT_FP8 = (ushort)InstructionsEnum.VDOT_FP8;
                public const ushort VDOT_WIDE = (ushort)InstructionsEnum.VDOT_WIDE;
            }

            /// <summary>
            /// Dedicated canonical opcode identity carried by decode/runtime IR.
            /// This separates canonical opcode storage from the mixed legacy
            /// <see cref="InstructionsEnum"/> plane while migration is still in progress.
            /// </summary>
            public readonly record struct IsaOpcode(ushort Value)
            {
                public static IsaOpcode FromInstructionsEnum(InstructionsEnum opcode) =>
                    new((ushort)opcode);

                public static IsaOpcode FromRawValue(uint rawOpcode)
                {
                    if (rawOpcode > ushort.MaxValue)
                    {
                        throw new System.ArgumentOutOfRangeException(
                            nameof(rawOpcode),
                            rawOpcode,
                            $"Opcode value must fit in {nameof(UInt16)}.");
                    }

                    return new((ushort)rawOpcode);
                }

                public InstructionsEnum ToInstructionsEnum() => (InstructionsEnum)Value;

                public override string ToString() => Arch.OpcodeRegistry.GetMnemonicOrHex(Value);

                public static implicit operator IsaOpcode(InstructionsEnum opcode) =>
                    FromInstructionsEnum(opcode);

                public static explicit operator InstructionsEnum(IsaOpcode opcode) =>
                    opcode.ToInstructionsEnum();

                public static explicit operator ushort(IsaOpcode opcode) => opcode.Value;

                public static explicit operator uint(IsaOpcode opcode) => opcode.Value;
            }
        }
    }
}
