namespace Abp.Interception.Benchmarks.Contracts;

public static class BenchmarkInterceptorCounters
{
    private static long _nuGet1;
    private static long _nuGet2;
    private static long _nuGet3;
    private static long _classBridge1;
    private static long _classBridge2;
    private static long _classBridge3;
    private static long _allocationFree1;
    private static long _allocationFree2;
    private static long _allocationFree3;

    public static void RecordNuGet1() => Interlocked.Increment(ref _nuGet1);
    public static void RecordNuGet2() => Interlocked.Increment(ref _nuGet2);
    public static void RecordNuGet3() => Interlocked.Increment(ref _nuGet3);
    public static void RecordClassBridge1() => Interlocked.Increment(ref _classBridge1);
    public static void RecordClassBridge2() => Interlocked.Increment(ref _classBridge2);
    public static void RecordClassBridge3() => Interlocked.Increment(ref _classBridge3);
    public static void RecordAllocationFree1() => Interlocked.Increment(ref _allocationFree1);
    public static void RecordAllocationFree2() => Interlocked.Increment(ref _allocationFree2);
    public static void RecordAllocationFree3() => Interlocked.Increment(ref _allocationFree3);

    public static void Reset()
    {
        Interlocked.Exchange(ref _nuGet1, 0);
        Interlocked.Exchange(ref _nuGet2, 0);
        Interlocked.Exchange(ref _nuGet3, 0);
        Interlocked.Exchange(ref _classBridge1, 0);
        Interlocked.Exchange(ref _classBridge2, 0);
        Interlocked.Exchange(ref _classBridge3, 0);
        Interlocked.Exchange(ref _allocationFree1, 0);
        Interlocked.Exchange(ref _allocationFree2, 0);
        Interlocked.Exchange(ref _allocationFree3, 0);
    }

    public static long NuGet1 => Interlocked.Read(ref _nuGet1);
    public static long NuGet2 => Interlocked.Read(ref _nuGet2);
    public static long NuGet3 => Interlocked.Read(ref _nuGet3);
    public static long ClassBridge1 => Interlocked.Read(ref _classBridge1);
    public static long ClassBridge2 => Interlocked.Read(ref _classBridge2);
    public static long ClassBridge3 => Interlocked.Read(ref _classBridge3);
    public static long AllocationFree1 => Interlocked.Read(ref _allocationFree1);
    public static long AllocationFree2 => Interlocked.Read(ref _allocationFree2);
    public static long AllocationFree3 => Interlocked.Read(ref _allocationFree3);
}
