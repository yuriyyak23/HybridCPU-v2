namespace HybridCPU.NonRTL.NominalSlotCorpus;

public class NominalPayload { public int Value; }
public class NominalBase
{
    public virtual int Read(NominalPayload payload) => 99;
}
public class NominalFactory
{
    public virtual NominalPayload? Create(int value)
    {
        var payload = new NominalPayload();
        payload.Value = 99;
        return payload;
    }
}

public static class ForeignStatics
{
    public static NominalPayload Held;
    public static int Count;
    static ForeignStatics()
    {
        Count = Count + 1;
        Held = new NominalPayload();
        Held.Value = 40;
    }
    public static int ReadAfterAllocation()
    {
        var scratch = new int[1];
        scratch[0] = 1;
        return Held.Value + Count + scratch[0];
    }
}

public static class FailedStatics
{
    public static int Value;
    static FailedStatics()
    {
        var values = new int[1];
        values[1] = 42;
        Value = 40;
    }
}

public static class MultiFirst
{
    public static NominalPayload Held;
    public static int Count;
    static MultiFirst() { Count = Count + 1; Held = new NominalPayload(); Held.Value = 20; }
}
public static class MultiSecond
{
    public static NominalPayload Held;
    public static int Count;
    static MultiSecond()
    {
        Count = Count + 1;
        Held = new NominalPayload();
        Held.Value = MultiFirst.Held.Value + 20;
    }
}
public static class MultiFailed
{
    public static int Value;
    static MultiFailed()
    {
        Value = MultiFirst.Held.Value;
        var values = new int[1];
        values[1] = 42;
    }
}
public static class MultiRemaining
{
    public static int Value;
    static MultiRemaining() { Value = MultiFailed.Value + 1; }
}
