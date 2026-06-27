using System.Reflection;
using Abp.Dependency;
using Abp.Interception.Benchmarks.Contracts;
using Abp.Interception.Benchmarks.NuGet.Interceptors;
using Castle.Core;

namespace Abp.Interception.Benchmarks.NuGet.Infrastructure;

internal static class BenchmarkUserInterceptorRegistrar
{
    public static void Initialize(IIocManager iocManager)
    {
        iocManager.Register(typeof(AbpAsyncDeterminationInterceptor<NuGetBenchmarkInterceptor1>), DependencyLifeStyle.Transient);
        iocManager.Register(typeof(AbpAsyncDeterminationInterceptor<NuGetBenchmarkInterceptor2>), DependencyLifeStyle.Transient);
        iocManager.Register(typeof(AbpAsyncDeterminationInterceptor<NuGetBenchmarkInterceptor3>), DependencyLifeStyle.Transient);

        iocManager.IocContainer.Kernel.ComponentRegistered += (_, handler) =>
        {
            if (!ShouldIntercept(handler.ComponentModel.Implementation))
            {
                return;
            }

            handler.ComponentModel.Interceptors.Add(
                new InterceptorReference(typeof(AbpAsyncDeterminationInterceptor<NuGetBenchmarkInterceptor1>)));
            handler.ComponentModel.Interceptors.Add(
                new InterceptorReference(typeof(AbpAsyncDeterminationInterceptor<NuGetBenchmarkInterceptor2>)));
            handler.ComponentModel.Interceptors.Add(
                new InterceptorReference(typeof(AbpAsyncDeterminationInterceptor<NuGetBenchmarkInterceptor3>)));
        };
    }

    private static bool ShouldIntercept(Type type)
    {
        return SelfOrMethodsDefinesAttribute<BenchmarkTrigger1Attribute>(type)
               || SelfOrMethodsDefinesAttribute<BenchmarkTrigger2Attribute>(type)
               || SelfOrMethodsDefinesAttribute<BenchmarkTrigger3Attribute>(type);
    }

    private static bool SelfOrMethodsDefinesAttribute<TAttr>(Type type)
        where TAttr : Attribute
    {
        if (type.GetTypeInfo().IsDefined(typeof(TAttr), inherit: true))
        {
            return true;
        }

        return type
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(method => method.IsDefined(typeof(TAttr), inherit: true));
    }
}
