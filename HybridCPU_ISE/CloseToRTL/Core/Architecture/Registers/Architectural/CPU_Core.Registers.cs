
using System;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {


            public struct IntRegister
            {
                ushort archRegisterId;
                public ushort ArchRegisterID
                {
                    get
                    {
                        return archRegisterId;
                    }
                    set
                    {
                        archRegisterId = value;
                    }
                }

                ushort coreOwnerId;
                public ushort CoreOwnerID
                {
                    get
                    {
                        return coreOwnerId;
                    }
                    set
                    {
                        coreOwnerId = value;
                    }
                }

                public IntRegister(ushort init_RegisterID, ushort init_CoreOwnerID)
                {
                    coreOwnerId = init_CoreOwnerID;
                    archRegisterId = init_RegisterID;
                    RegisterMap = new byte[8];
                    RegisterData = new byte[8];
                }

                public byte[] RegisterMap;

                public ulong ToUInt64()
                {
                    return BitConverter.ToUInt64(this.RegisterMap, 0);
                }
                public ulong Value
                {
                    get
                    {
                        return ToUInt64();
                    }
                    set
                    {
                        BitConverter.GetBytes(value).CopyTo(RegisterMap, 0);
                    }
                }

                public byte[] RegisterData
                {
                    get
                    {
                        return RegisterMap;
                    }
                    set
                    {
                        value.CopyTo(RegisterMap, 0);
                    }
                }
            }

            public struct FlagsRegister
            {
                [Flags]
                private enum FlagBits : ushort
                {
                    None = 0,
                    Zero = 1 << 0,
                    Sign = 1 << 1,
                    EnableInterrupt = 1 << 2,
                    OverFlow = 1 << 3,
                    IOPrivilege = 1 << 4,
                    OSKernel = 1 << 5,
                    Direction = 1 << 6,
                    Parity = 1 << 7,
                    Jump = 1 << 8,
                    JumpOutsideVliw = 1 << 9
                }

                private FlagBits packedBits;

                public FlagsRegister(ushort coreId)
                {
                    _ = coreId;

                    packedBits = FlagBits.None;
                    Offset_VLIWOpCode = 0;
                    VLIW_Begin_MemPtr = 0;
                    VLIW_End_MemPtr = 0;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                private bool ReadFlag(FlagBits flag) => (packedBits & flag) != 0;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                private void WriteFlag(FlagBits flag, bool enabled)
                {
                    if (enabled)
                    {
                        packedBits |= flag;
                    }
                    else
                    {
                        packedBits &= ~flag;
                    }
                }

                public byte Offset_VLIWOpCode;

                public ulong VLIW_Begin_MemPtr;
                public ulong VLIW_End_MemPtr;

                public bool Zero_Flag
                {
                    get => ReadFlag(FlagBits.Zero);
                    set => WriteFlag(FlagBits.Zero, value);
                }

                public bool Sign_Flag
                {
                    get => ReadFlag(FlagBits.Sign);
                    set => WriteFlag(FlagBits.Sign, value);
                }

                public bool EnableInterrupt_Flag
                {
                    get => ReadFlag(FlagBits.EnableInterrupt);
                    set => WriteFlag(FlagBits.EnableInterrupt, value);
                }

                public bool OverFlow_Flag
                {
                    get => ReadFlag(FlagBits.OverFlow);
                    set => WriteFlag(FlagBits.OverFlow, value);
                }

                public bool IOPrivilege_Flag
                {
                    get => ReadFlag(FlagBits.IOPrivilege);
                    set => WriteFlag(FlagBits.IOPrivilege, value);
                }

                public bool OSKernel_Flag
                {
                    get => ReadFlag(FlagBits.OSKernel);
                    set => WriteFlag(FlagBits.OSKernel, value);
                }

                public bool Direction_Flag
                {
                    get => ReadFlag(FlagBits.Direction);
                    set => WriteFlag(FlagBits.Direction, value);
                }

                public bool Parity_Flag
                {
                    get => ReadFlag(FlagBits.Parity);
                    set => WriteFlag(FlagBits.Parity, value);
                }

                public bool Jump_Flag
                {
                    get => ReadFlag(FlagBits.Jump);
                    set => WriteFlag(FlagBits.Jump, value);
                }

                public bool JumpOutsideVLIW_Flag
                {
                    get => ReadFlag(FlagBits.JumpOutsideVliw);
                    set => WriteFlag(FlagBits.JumpOutsideVliw, value);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte RequireArchRegister(IntRegister register, string paramName)
            {
                ushort regId = register.ArchRegisterID;

                if (regId > ArchRegId.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(paramName, regId,
                        $"Architectural register id must be in [0, {ArchRegId.MaxValue}].");
                }

                return (byte)regId;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static ArchRegId RequireArchRegId(IntRegister register, string paramName) =>
                (ArchRegId)RequireArchRegister(register, paramName);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static ulong PackIntRegistersAsArchRegs(IntRegister reg1)
            {
                return ArchRegisterTripletEncoding.Pack(
                    RequireArchRegister(reg1, nameof(reg1)),
                    ArchRegisterTripletEncoding.NoArchReg,
                    ArchRegisterTripletEncoding.NoArchReg);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static ulong PackIntRegistersAsArchRegs(IntRegister reg1, IntRegister reg2)
            {
                return ArchRegisterTripletEncoding.Pack(
                    RequireArchRegister(reg1, nameof(reg1)),
                    RequireArchRegister(reg2, nameof(reg2)),
                    ArchRegisterTripletEncoding.NoArchReg);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static ulong PackIntRegistersAsArchRegs(IntRegister reg1, IntRegister reg2, IntRegister reg3)
            {
                return ArchRegisterTripletEncoding.Pack(
                    RequireArchRegister(reg1, nameof(reg1)),
                    RequireArchRegister(reg2, nameof(reg2)),
                    RequireArchRegister(reg3, nameof(reg3)));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static ulong PackArchRegistersAsArchRegs(ArchRegId reg1)
            {
                return ArchRegisterTripletEncoding.Pack(
                    (byte)reg1,
                    ArchRegisterTripletEncoding.NoArchReg,
                    ArchRegisterTripletEncoding.NoArchReg);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static ulong PackArchRegistersAsArchRegs(ArchRegId reg1, ArchRegId reg2)
            {
                return ArchRegisterTripletEncoding.Pack(
                    (byte)reg1,
                    (byte)reg2,
                    ArchRegisterTripletEncoding.NoArchReg);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static ulong PackArchRegistersAsArchRegs(ArchRegId reg1, ArchRegId reg2, ArchRegId reg3)
            {
                return ArchRegisterTripletEncoding.Pack(
                    (byte)reg1,
                    (byte)reg2,
                    (byte)reg3);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private ulong ReadActiveArchValue(ArchRegId regId)
            {
                return ReadArch(ReadActiveVirtualThreadId(), (byte)regId);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteActiveArchValue(ArchRegId regId, ulong value)
            {
                RetireCoordinator.Retire(
                    RetireRecord.RegisterWrite(
                        ReadActiveVirtualThreadId(),
                        (byte)regId,
                        value));
            }

            // ===== Phase 05 (tech.md §5): Clustered Pod CSR Registry =====

            /// <summary>CSR address: Pod XY coordinates in NoC (X &lt;&lt; 8 | Y)</summary>
            public const int CSR_POD_ID = 0xB00;

            /// <summary>CSR address: Bitmask of cores participating in local FSP / barrier</summary>
            public const int CSR_POD_AFFINITY_MASK = 0xB01;

            /// <summary>CSR address: Singularity-style domain certificate for SafetyVerifier</summary>
            public const int CSR_MEM_DOMAIN_CERT = 0xB02;

            /// <summary>CSR address: QoS / priority configuration for NoC DMA traffic</summary>
            public const int CSR_NOC_ROUTE_CFG = 0xB03;

            /// <summary>Pod ID: XY coordinates (X &lt;&lt; 8 | Y). Read-only at runtime, set by firmware.</summary>
            public ulong CsrPodId;

            /// <summary>Pod affinity mask: which of the 16 local cores participate in FSP and barriers.</summary>
            public ulong CsrPodAffinityMask;

            /// <summary>Memory domain certificate for Singularity-style isolation (64 bits).</summary>
            public ulong CsrMemDomainCert;

            /// <summary>NoC route configuration: QoS priority and routing hints for DMA traffic.</summary>
            public ulong CsrNocRouteCfg;

            /// <summary>
            /// Read a Pod/NoC CSR by address (tech.md §5).
            /// </summary>
            /// <param name="csrAddr">CSR address (0xB00–0xB03)</param>
            /// <returns>CSR value for the authoritative pod-plane address</returns>
            public ulong ReadPodCSR(int csrAddr)
            {
                return csrAddr switch
                {
                    CSR_POD_ID => CsrPodId,
                    CSR_POD_AFFINITY_MASK => CsrPodAffinityMask,
                    CSR_MEM_DOMAIN_CERT => CsrMemDomainCert,
                    CSR_NOC_ROUTE_CFG => CsrNocRouteCfg,
                    _ => throw new CsrUnknownAddressException((ushort)csrAddr)
                };
            }

            /// <summary>
            /// Write a Pod/NoC CSR by address (tech.md §5).
            /// POD_ID is read-only in normal mode; writes are silently ignored.
            /// </summary>
            /// <param name="csrAddr">CSR address (0xB00–0xB03)</param>
            /// <param name="value">Value to write</param>
            public void WritePodCSR(int csrAddr, ulong value)
            {
                switch (csrAddr)
                {
                    case CSR_POD_ID:
                        // Read-only at runtime — only firmware/reset can set this
                        break;
                    case CSR_POD_AFFINITY_MASK:
                        CsrPodAffinityMask = value;
                        break;
                    case CSR_MEM_DOMAIN_CERT:
                        CsrMemDomainCert = value;
                        break;
                    case CSR_NOC_ROUTE_CFG:
                        CsrNocRouteCfg = value;
                        break;
                }
            }
        }
    }
}
