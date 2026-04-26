using HybridCPU_ISE.Core;
using Xunit;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09ReplayTokenDeferredProofTests
{
    [Fact]
    public void GetReplayToken_WhenLiveVerificationStateMutates_ThenPublishesFreshVectorAndExceptionConfig()
    {
        ConfigureReplayVerificationRuntime(bankSize: 0x2000UL);
        ref Processor.CPU_Core core = ref Processor.CPU_Cores[0];

        Processor.SetRandomSeed(0x1111UL);
        ApplyVectorConfig(ref core, vl: 6, vsew: 32, lmul: 2, tailAgnostic: false, maskAgnostic: false);
        Assert.True(core.ExceptionStatus.SetRoundingMode(1));
        Assert.True(core.ExceptionStatus.SetExceptionMode(0));

        ReplayToken first = Assert.IsType<ReplayToken>(ReplayToken.FromJson(Processor.GetReplayToken()));

        Processor.SetRandomSeed(0x2222UL);
        ApplyVectorConfig(ref core, vl: 11, vsew: 64, lmul: 4, tailAgnostic: true, maskAgnostic: true);
        Assert.True(core.ExceptionStatus.SetRoundingMode(3));
        Assert.True(core.ExceptionStatus.SetExceptionMode(2));

        ReplayToken second = Assert.IsType<ReplayToken>(ReplayToken.FromJson(Processor.GetReplayToken()));

        Assert.Equal((ulong)0x1111, first.RandomSeed);
        Assert.Equal((uint)6, first.VL);
        Assert.Equal((byte)32, first.VSEW);
        Assert.Equal((byte)2, first.LMUL);
        Assert.False(first.TailAgnostic);
        Assert.False(first.MaskAgnostic);
        Assert.Equal((byte)1, first.RoundingMode);
        Assert.Equal((byte)0, first.ExceptionMode);

        Assert.Equal((ulong)0x2222, second.RandomSeed);
        Assert.Equal((uint)11, second.VL);
        Assert.Equal((byte)64, second.VSEW);
        Assert.Equal((byte)4, second.LMUL);
        Assert.True(second.TailAgnostic);
        Assert.True(second.MaskAgnostic);
        Assert.Equal((byte)3, second.RoundingMode);
        Assert.Equal((byte)2, second.ExceptionMode);
        Assert.Equal((ulong)Processor.MainMemory.Length, second.MemorySize);
    }

    [Theory]
    [InlineData((ulong)((2UL << 3) | 5UL), "fractional LMUL = 1/8")]
    [InlineData((ulong)(4UL << 3), "unsupported VTYPE SEW encoding 4")]
    public void GetReplayToken_WhenVectorConfigIsNonRepresentable_ThenFailsClosed(
        ulong vtype,
        string expectedMessage)
    {
        ConfigureReplayVerificationRuntime(bankSize: 0x2000UL);
        ref Processor.CPU_Core core = ref Processor.CPU_Cores[0];
        core.VectorConfig.VL = 7;
        core.VectorConfig.VTYPE = vtype;
        core.VectorConfig.TailAgnostic = 0;
        core.VectorConfig.MaskAgnostic = 0;

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => Processor.GetReplayToken());

        Assert.Contains("GetReplayToken()", ex.Message, StringComparison.Ordinal);
        Assert.Contains(expectedMessage, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InitializeFromReplayToken_WhenTokenIsRepresentable_ThenRestoresLiveVerificationCoreState()
    {
        ConfigureReplayVerificationRuntime(bankSize: 0x2000UL);
        ref Processor.CPU_Core core = ref Processor.CPU_Cores[0];
        ApplyVectorConfig(ref core, vl: 3, vsew: 32, lmul: 1, tailAgnostic: false, maskAgnostic: false);
        Assert.True(core.ExceptionStatus.SetRoundingMode(0));
        Assert.True(core.ExceptionStatus.SetExceptionMode(0));

        ReplayToken token = ReplayToken.CreateFromConfig(
            seed: 0xABCDEFUL,
            vl: 9,
            vlmax: (uint)Processor.CPU_Core.RVV_Config.VLMAX,
            vsew: 64,
            lmul: 4,
            tailAgnostic: true,
            maskAgnostic: true,
            roundingMode: 4,
            exceptionMode: 1,
            memorySize: (ulong)Processor.MainMemory.Length);

        Processor.InitializeFromReplayToken(token.ToJson());

        Assert.Equal((ulong)0xABCDEF, Processor.RandomSeed);
        Assert.Equal((ulong)9, core.VectorConfig.VL);
        Assert.Equal((byte)1, core.VectorConfig.TailAgnostic);
        Assert.Equal((byte)1, core.VectorConfig.MaskAgnostic);
        Assert.Equal((byte)4, core.ExceptionStatus.RoundingMode);
        Assert.Equal((byte)1, core.ExceptionStatus.ExceptionMode);
        Assert.Equal(EncodeVType(vsew: 64, lmul: 4, tailAgnostic: true, maskAgnostic: true), core.VectorConfig.VTYPE);
    }

    [Theory]
    [InlineData("vl", "VL = 33 above VLMAX = 32")]
    [InlineData("vlmax", "token VLMAX = 33")]
    [InlineData("memory", "memorySize = ")]
    public void InitializeFromReplayToken_WhenTokenCrossesRuntimeBoundary_ThenFailsClosed(
        string boundaryCase,
        string expectedMessage)
    {
        ConfigureReplayVerificationRuntime(bankSize: 0x2000UL);

        ReplayToken token = ReplayToken.CreateFromConfig(
            seed: 7,
            vl: 8,
            vlmax: (uint)Processor.CPU_Core.RVV_Config.VLMAX,
            vsew: 32,
            lmul: 1,
            tailAgnostic: false,
            maskAgnostic: false,
            roundingMode: 0,
            exceptionMode: 0,
            memorySize: (ulong)Processor.MainMemory.Length);

        switch (boundaryCase)
        {
            case "vl":
                token.VL = token.VLMAX + 1;
                break;
            case "vlmax":
                token.VLMAX = token.VLMAX + 1;
                break;
            case "memory":
                token.MemorySize = token.MemorySize + 0x1000UL;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(boundaryCase), boundaryCase, null);
        }

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => Processor.InitializeFromReplayToken(token.ToJson()));

        Assert.Contains("InitializeFromReplayToken()", ex.Message, StringComparison.Ordinal);
        Assert.Contains(expectedMessage, ex.Message, StringComparison.Ordinal);
    }

    private static void ConfigureReplayVerificationRuntime(ulong bankSize)
    {
        Processor.ClearVerificationData();
        Processor.CPU_Cores = new Processor.CPU_Core[1024];
        Processor.CPU_Cores[0] = new Processor.CPU_Core(0);
        Processor.MainMemory = new Processor.MultiBankMemoryArea(4, bankSize);
        Processor.Ready_Flag = true;
    }

    private static void ApplyVectorConfig(
        ref Processor.CPU_Core core,
        ulong vl,
        byte vsew,
        byte lmul,
        bool tailAgnostic,
        bool maskAgnostic)
    {
        core.VectorConfig.VL = vl;
        core.VectorConfig.TailAgnostic = tailAgnostic ? (byte)1 : (byte)0;
        core.VectorConfig.MaskAgnostic = maskAgnostic ? (byte)1 : (byte)0;
        core.VectorConfig.VTYPE = EncodeVType(vsew, lmul, tailAgnostic, maskAgnostic);
    }

    private static ulong EncodeVType(
        byte vsew,
        byte lmul,
        bool tailAgnostic,
        bool maskAgnostic)
    {
        ulong value = lmul switch
        {
            1 => 0UL,
            2 => 1UL,
            4 => 2UL,
            8 => 3UL,
            _ => throw new ArgumentOutOfRangeException(nameof(lmul), lmul, null)
        };

        value |= (ulong)(vsew switch
        {
            8 => 0,
            16 => 1,
            32 => 2,
            64 => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(vsew), vsew, null)
        }) << 3;

        if (tailAgnostic)
        {
            value |= 1UL << 6;
        }

        if (maskAgnostic)
        {
            value |= 1UL << 7;
        }

        return value;
    }
}
