namespace ManagedPortSmoke.Dependency;

public static class MetadataOnlyCallee
{
    public static int Read() => 42;
    public static T Identity<T>(T value) => value;
    public static int CallIdentity(int value) => Identity<int>(value);

    public static int CatchDivide(int value)
    {
        try { return 42 / value; }
        catch (System.DivideByZeroException) { return -1; }
    }
}
