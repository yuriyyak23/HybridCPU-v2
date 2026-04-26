using System.ComponentModel;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Event kinds for VMX diagnostic trace recording.
    /// Each VMX operation generates one or more events that are
    /// recorded by the <see cref="IVmxEventSink"/> for diagnostics and replay
    /// observability. These kinds are not a production semantic authority.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal enum VmxEventKind : byte
    {
        /// <summary>VMXON: VMX operation enabled.</summary>
        VmxOn,

        /// <summary>VMXOFF: VMX operation disabled.</summary>
        VmxOff,

        /// <summary>VM-Entry via VMLAUNCH.</summary>
        VmEntry,

        /// <summary>VM-Entry via VMRESUME.</summary>
        VmResume,

        /// <summary>VM-Exit back to host.</summary>
        VmExit,

        /// <summary>VMCLEAR cleared or invalidated the active VMCS pointer.</summary>
        VmClear,

        /// <summary>VMPTRLD loaded a VMCS pointer.</summary>
        VmPtrLd,

        /// <summary>VMREAD read from a VMCS field.</summary>
        VmRead,

        /// <summary>VMWRITE wrote to a VMCS field.</summary>
        VmWrite,
    }
}
