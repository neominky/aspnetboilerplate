using System.Text;

namespace Abp.Interception.Benchmarks.Contracts;

public static class InterceptionChainVerifier
{
    /// <summary>Each of the 3 user interceptors should run once per app-service method call.</summary>
    public const int ExpectedInvocationsPerInterceptor = 1;

    public static void VerifyNuGet(IBenchmarkComparisonAppService appService, string scenarioName, bool requireCastleProxy)
    {
        Verify(
            appService,
            scenarioName,
            requireCastleProxy,
            () => (BenchmarkInterceptorCounters.NuGet1, BenchmarkInterceptorCounters.NuGet2, BenchmarkInterceptorCounters.NuGet3));
    }

    public static void VerifyClassBridge(IBenchmarkComparisonAppService appService, string scenarioName)
    {
        Verify(
            appService,
            scenarioName,
            requireCastleProxy: false,
            () => (BenchmarkInterceptorCounters.ClassBridge1, BenchmarkInterceptorCounters.ClassBridge2, BenchmarkInterceptorCounters.ClassBridge3));
    }

    public static void VerifyAllocationFree(IBenchmarkComparisonAppService appService, string scenarioName)
    {
        Verify(
            appService,
            scenarioName,
            requireCastleProxy: false,
            () => (BenchmarkInterceptorCounters.AllocationFree1, BenchmarkInterceptorCounters.AllocationFree2, BenchmarkInterceptorCounters.AllocationFree3));
    }

    private static void Verify(
        IBenchmarkComparisonAppService appService,
        string scenarioName,
        bool requireCastleProxy,
        Func<(long Interceptor1, long Interceptor2, long Interceptor3)> readCounts)
    {
        var errors = new StringBuilder();

        if (requireCastleProxy && !IsCastleProxy(appService))
        {
            errors.AppendLine(
                $"[{scenarioName}] Resolved service is not a Castle proxy (type: {appService.GetType().FullName}). " +
                "DynamicProxy interceptors will not run.");
        }

        BenchmarkInterceptorCounters.Reset();

        var syncResult = appService.GetMessage();
        if (syncResult != "benchmark")
        {
            errors.AppendLine($"[{scenarioName}] Sync call returned '{syncResult}', expected 'benchmark'.");
        }

        var syncCounts = readCounts();
        if (!CountsMatch(syncCounts, ExpectedInvocationsPerInterceptor))
        {
            errors.AppendLine(
                $"[{scenarioName}] After sync GetMessage(), expected each of the 3 interceptors to run {ExpectedInvocationsPerInterceptor} time(s). " +
                $"Actual: #1={syncCounts.Interceptor1}, #2={syncCounts.Interceptor2}, #3={syncCounts.Interceptor3}.");
        }

        BenchmarkInterceptorCounters.Reset();

        var taskResult = appService.GetMessageTaskAsync().GetAwaiter().GetResult();
        if (taskResult != "benchmark")
        {
            errors.AppendLine($"[{scenarioName}] Task call returned '{taskResult}', expected 'benchmark'.");
        }

        var taskCounts = readCounts();
        if (!CountsMatch(taskCounts, ExpectedInvocationsPerInterceptor))
        {
            errors.AppendLine(
                $"[{scenarioName}] After GetMessageTaskAsync(), expected each of the 3 interceptors to run {ExpectedInvocationsPerInterceptor} time(s). " +
                $"Actual: #1={taskCounts.Interceptor1}, #2={taskCounts.Interceptor2}, #3={taskCounts.Interceptor3}.");
        }

        if (errors.Length > 0)
        {
            throw new InvalidOperationException(errors.ToString().TrimEnd());
        }
    }

    public static bool IsCastleProxy(object instance)
    {
        var type = instance.GetType();
        return type.Namespace?.StartsWith("Castle.Proxies", StringComparison.Ordinal) == true
               || type.Name.EndsWith("Proxy", StringComparison.Ordinal);
    }

    private static bool CountsMatch((long Interceptor1, long Interceptor2, long Interceptor3) counts, int expected)
        => counts.Interceptor1 == expected
           && counts.Interceptor2 == expected
           && counts.Interceptor3 == expected;
}
