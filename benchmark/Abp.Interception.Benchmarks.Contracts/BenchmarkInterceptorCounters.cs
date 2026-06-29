namespace Abp.Interception.Benchmarks.Contracts;

public static class BenchmarkInterceptorCounters
{
    private static long _nuGet1;
    private static long _nuGet2;
    private static long _nuGet3;
    private static long _classInvocation1;
    private static long _classInvocation2;
    private static long _classInvocation3;
    private static long _allocationFree1;
    private static long _allocationFree2;
    private static long _allocationFree3;

    public static void RecordNuGet1() => Interlocked.Increment(ref _nuGet1);
    public static void RecordNuGet2() => Interlocked.Increment(ref _nuGet2);
    public static void RecordNuGet3() => Interlocked.Increment(ref _nuGet3);
    public static void RecordClassInvocation1() => Interlocked.Increment(ref _classInvocation1);
    public static void RecordClassInvocation2() => Interlocked.Increment(ref _classInvocation2);
    public static void RecordClassInvocation3() => Interlocked.Increment(ref _classInvocation3);
    public static void RecordAllocationFree1() => Interlocked.Increment(ref _allocationFree1);
    public static void RecordAllocationFree2() => Interlocked.Increment(ref _allocationFree2);
    public static void RecordAllocationFree3() => Interlocked.Increment(ref _allocationFree3);

    public static void Reset()
    {
        Interlocked.Exchange(ref _nuGet1, 0);
        Interlocked.Exchange(ref _nuGet2, 0);
        Interlocked.Exchange(ref _nuGet3, 0);
        Interlocked.Exchange(ref _classInvocation1, 0);
        Interlocked.Exchange(ref _classInvocation2, 0);
        Interlocked.Exchange(ref _classInvocation3, 0);
        Interlocked.Exchange(ref _allocationFree1, 0);
        Interlocked.Exchange(ref _allocationFree2, 0);
        Interlocked.Exchange(ref _allocationFree3, 0);
    }

    public static long NuGet1 => Interlocked.Read(ref _nuGet1);
    public static long NuGet2 => Interlocked.Read(ref _nuGet2);
    public static long NuGet3 => Interlocked.Read(ref _nuGet3);
    public static long ClassInvocation1 => Interlocked.Read(ref _classInvocation1);
    public static long ClassInvocation2 => Interlocked.Read(ref _classInvocation2);
    public static long ClassInvocation3 => Interlocked.Read(ref _classInvocation3);
    public static long AllocationFree1 => Interlocked.Read(ref _allocationFree1);
    public static long AllocationFree2 => Interlocked.Read(ref _allocationFree2);
    public static long AllocationFree3 => Interlocked.Read(ref _allocationFree3);
}
