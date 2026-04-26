using System;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public class IOPorts : System.IO.MemoryStream
        {
            public static IOPorts[] Ports = new IOPorts[65536];

            private ulong ulong_PortID;
            private ulong ulong_PortData;

            public IOPorts(ulong portID) : base()
            {
                this.ulong_PortID = portID;
                this.ulong_PortData = 0;
            }

            public ulong PortID
            {
                get { return ulong_PortID; }
            }

            public ulong PioDataRegister
            {
                get { return ulong_PortData; }
                set { ulong_PortData = value; }
            }

            public ulong PortData
            {
                get
                {
                    return ulong_PortData;
                }
                set
                {
                    ulong_PortData = value;
                }
            }

            public void WriteByCPU(ulong data)
            {
                this.PioDataRegister = data;
            }

            public ulong ReadByCPU()
            {
                return this.PioDataRegister;
            }

            public ulong DMAReadFromSystemMemory(ulong ioVirtualAddress, byte[] buffer, ulong offset, ulong count)
            {
                return 0;
            }

            public ulong DMAWriteToSystemMemory(ulong ioVirtualAddress, byte[] buffer, ulong offset, ulong count)
            {
                return 0;
            }

            public void PortInput(ulong PortID, ulong Data)
            {
                this.ulong_PortID = PortID;
                this.ulong_PortData = Data;
            }

            public ulong PortOutput()
            {
                return this.ulong_PortData;
            }
        }
    }
}
