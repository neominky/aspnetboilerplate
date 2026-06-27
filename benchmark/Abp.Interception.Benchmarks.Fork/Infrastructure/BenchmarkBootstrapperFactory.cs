using Abp;
using Abp.Dependency.CompileTime;

namespace Abp.Interception.Benchmarks.Fork.Infrastructure;

internal static class BenchmarkBootstrapperFactory
{
    public static AbpBootstrapper Create()
    {
        return AbpBootstrapperCompileTimeExtensions.CreateWithCompileTimeInterception<ForkBenchmarkModule>(
            DisableBuiltInInterceptors);
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
