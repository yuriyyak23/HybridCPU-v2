using HybridCPU.ManagedRuntime;
using HybridCPU.Platform.Contracts;
using HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;

public static class GcMetadataCacheRegression
{
    public static void Verify(HybridCpuIseManagedImageLoadResultV1 load, Action<bool, string> check)
    {
        var source = load.StackMaps!.First(row => row.UnwindInfo is { Length: > 28 });
        var item = source with { GcInfo = source.GcInfo.ToArray(),
            CodeManagerMetadata = source.CodeManagerMetadata.ToArray(), UnwindInfo = source.UnwindInfo!.ToArray() };
        var bank = new ulong[64];
        HybridCpuManagedRetiredSafepointStatusV1 Probe(HybridCpuManagedStackMapRegistrationV1 registration) =>
            load.Gc!.CollectRetiredSafepoint([registration], int.MaxValue, bank, 0,
                (_, _) => null, [], load.Strings).Status;
        check(Probe(item) == HybridCpuManagedRetiredSafepointStatusV1.NotSafepoint &&
            Probe(item) == HybridCpuManagedRetiredSafepointStatusV1.NotSafepoint,
            "repeated exact metadata remains accepted");
        foreach (byte[] bytes in new[] { item.GcInfo, item.CodeManagerMetadata, item.UnwindInfo! })
        {
            bytes[^1] ^= 1;
            check(Probe(item) == HybridCpuManagedRetiredSafepointStatusV1.Rejected,
                "in-place mutation of cached GC/HCMM/HCW2 bytes is rejected");
            bytes[^1] ^= 1;
            check(Probe(item) == HybridCpuManagedRetiredSafepointStatusV1.NotSafepoint,
                "restoring exact bytes restores acceptance");
        }
        check(Probe(item with { ManagedAbiDigest = new string('0', 64) }) == HybridCpuManagedRetiredSafepointStatusV1.Rejected,
            "new registration with forged ABI tuple cannot reuse another record's cache");
        var lowBudgets = HybridCpuManagedNonMovingGcBudgetsV1.Production with { MaximumSafepoints = 1 };
        var many = load.StackMaps.First(row => BitConverter.ToInt32(row.GcInfo, 8) > 1);
        check(Probe(many) == HybridCpuManagedRetiredSafepointStatusV1.NotSafepoint,
            "production metadata parsed before lower-budget check");
        var result = load.Gc!.Collect(new([many], [], load.ProcessRoots!, load.Strings),
            HybridCpuManagedNonMovingGcOptionsV1.Create(true, lowBudgets));
        check(result.Status == HybridCpuManagedNonMovingGcStatusV1.BudgetExhausted,
            "cached production parse cannot bypass a lower safepoint budget");
    }
}
