namespace YAKSys_Hybrid_CPU.Core.Vmx
{
    public enum VmxOperandForm : byte
    {
        None = 0,
        ReservedIgnoredRegister = 1,
        FieldSelectorToRegister = 2,
        FieldSelectorAndValueRegisters = 3,
        VmcsPointerRegister = 4,
        CurrentVmcsPointerToRegister = 5,
        HypercallLeafAndDescriptor = 6,
        InvalidationScopeAndDescriptor = 7,
        FunctionLeafAndDescriptor = 8,
        ExtendedStateMaskAndDescriptor = 9,
    }
}
