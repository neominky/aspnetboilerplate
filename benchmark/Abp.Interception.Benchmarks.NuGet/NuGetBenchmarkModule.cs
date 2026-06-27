using Abp.Interception.Benchmarks.NuGet.Infrastructure;
using Abp.Modules;

namespace Abp.Interception.Benchmarks.NuGet;

public class NuGetBenchmarkModule : AbpModule
{
    public override void PreInitialize()
    {
        Configuration.Auditing.IsEnabled = false;
        BenchmarkUserInterceptorRegistrar.Initialize(IocManager);
    }

    public override void Initialize()
    {
        IocManager.RegisterAssemblyByConvention(typeof(NuGetBenchmarkModule).Assembly);
    }
}
