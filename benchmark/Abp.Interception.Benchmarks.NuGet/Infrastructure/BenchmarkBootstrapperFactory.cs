using Abp;
using Abp.Interception.Benchmarks.NuGet;

namespace Abp.Interception.Benchmarks.NuGet;

internal static class BenchmarkBootstrapperFactory
{
    public static AbpBootstrapper Create()
    {
        return AbpBootstrapper.Create<NuGetBenchmarkModule>(DisableBuiltInInterceptors);
    }

    private static void DisableBuiltInInterceptors(AbpBootstrapperOptions options)
    {
        options.InterceptorOptions.DisableValidationInterceptor = true;
        options.InterceptorOptions.DisableAuditingInterceptor = true;
        options.InterceptorOptions.DisableEntityHistoryInterceptor = true;
        options.InterceptorOptions.DisableUnitOfWorkInterceptor = true;
        options.InterceptorOptions.DisableAuthorizationInterceptor = true;
    }
}
