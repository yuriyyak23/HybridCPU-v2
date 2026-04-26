using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.ControlFlow;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public struct EntryPoint
        {
            bool bool_EntryPointAddressAlreadyDefined;
            public bool EntryPointAddressAlreadyDefined
            {
                get
                {
                    return bool_EntryPointAddressAlreadyDefined;
                }
                set
                {
                    bool_EntryPointAddressAlreadyDefined = value;
                }
            }

            ulong ulong_EntryPoint_Address, ulong_Call_Address, ulong_JumpAddress, ulong_Interrupt_Address;

            public ulong EntryPoint_Address
            {
                get
                {
                    return ulong_EntryPoint_Address;
                }
                set
                {
                    if (EntryPointAddressAlreadyDefined == true && value != ulong_EntryPoint_Address)
                    {
                        throw new EntryPointDefinitionException(
                            nameof(EntryPoint_Address),
                            ulong_EntryPoint_Address,
                            value,
                            "EntryPoint address is already defined for this descriptor.");
                    }

                    EntryPointAddressAlreadyDefined = true;

                    ulong_EntryPoint_Address = value;
                }
            }
            public ulong Call_Address
            {
                get
                {
                    return ulong_Call_Address;
                }
                set
                {
                    ulong_Call_Address = value;
                }
            }
            public ulong Jump_Address
            {
                get
                {
                    return ulong_JumpAddress;
                }
                set
                {
                    ulong_JumpAddress = value;
                }
            }
            public ulong Interrupt_Address
            {
                get
                {
                    return ulong_Interrupt_Address;
                }
                set
                {
                    ulong_Interrupt_Address = value;
                }
            }

            public List<ulong> List_Call_Addresses;
            public List<ulong> List_Jump_Addresses;
            public List<ulong> List_Interrupt_Addresses;
            List<RelocationEntry> List_RelocationEntries;

            string string_SymbolName;

            public string SymbolName
            {
                get
                {
                    return string_SymbolName ?? string.Empty;
                }
                set
                {
                    string_SymbolName = value ?? string.Empty;
                }
            }

            public IReadOnlyList<RelocationEntry> RelocationEntries
            {
                get
                {
                    if (List_RelocationEntries == null)
                    {
                        return Array.Empty<RelocationEntry>();
                    }

                    return List_RelocationEntries;
                }
            }

            public void AddAddress(ulong Address)
            {
                if (List_Call_Addresses == null) List_Call_Addresses = new List<ulong>();
                if (List_Jump_Addresses == null) List_Jump_Addresses = new List<ulong>();
                if (List_Interrupt_Addresses == null) List_Interrupt_Addresses = new List<ulong>();

                if (Type == EntryPointType.EntryPoint && bool_EntryPointAddressAlreadyDefined == false)
                {
                    EntryPoint_Address = Address;

                    bool_EntryPointAddressAlreadyDefined = true;

                    return;
                }

                if (bool_EntryPointAddressAlreadyDefined == true)
                {
                    if (Type == EntryPointType.Return)
                    {
                        throw new UnsupportedEntryPointOperationException(
                            nameof(EntryPointType.Return),
                            nameof(AddAddress));
                    }

                    if (Type == EntryPointType.InterruptReturn)
                    {
                        throw new UnsupportedEntryPointOperationException(
                            nameof(EntryPointType.InterruptReturn),
                            nameof(AddAddress));
                    }
                }

                if (bool_EntryPointAddressAlreadyDefined == false)
                {
                    if (Type == EntryPointType.Return)
                    {
                        throw new UnsupportedEntryPointOperationException(
                            nameof(EntryPointType.Return),
                            nameof(AddAddress));
                    }

                    if (Type == EntryPointType.InterruptReturn)
                    {
                        throw new UnsupportedEntryPointOperationException(
                            nameof(EntryPointType.InterruptReturn),
                            nameof(AddAddress));
                    }
                }

                if (Type == EntryPointType.Call)
                {
                    List_Call_Addresses.Add(Address);
                }

                if (Type == EntryPointType.Jump)
                {
                    List_Jump_Addresses.Add(Address);
                }

                if (Type == EntryPointType.Interrupt)
                {
                    List_Interrupt_Addresses.Add(Address);
                }
            }

            public void AddRelocation(RelocationKind kind, ulong emissionCursor)
            {
                if (!EntryPointAddressAlreadyDefined)
                {
                    throw new EntryPointDefinitionException(
                        nameof(AddRelocation),
                        ulong_EntryPoint_Address,
                        null,
                        "EntryPoint relocation cannot be resolved before target address is defined.");
                }

                if (List_RelocationEntries == null)
                {
                    List_RelocationEntries = new List<RelocationEntry>();
                }

                List_RelocationEntries.Add(
                    RelocationEntry.CreateLegacyAbsolute64(
                        kind,
                        emissionCursor,
                        SymbolName,
                        EntryPoint_Address));
            }

            EntryPointType EntryPoint_CurrentType;
            public EntryPointType Type
            {
                get
                {
                    return EntryPoint_CurrentType;
                }

                set
                {
                    EntryPoint_CurrentType = value;
                }
            }

            public enum EntryPointType
            {
                EntryPoint,
                Jump,
                Call,
                Return,
                Interrupt,
                InterruptReturn
            }

        }
    }
}
