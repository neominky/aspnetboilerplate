using Abp.Dependency.CompileTime;
using Abp.Interception.Benchmarks.Fork;
using Abp.Modules;

namespace Abp.Interception.Benchmarks.Fork;

public partial class ForkBenchmarkModule : AbpModule
{
    public override void PreInitialize()
    {
        Configuration.Auditing.IsEnabled = false;
    }

    public override void Initialize()
    {
        IocManager.RegisterAssemblyByConvention(typeof(ForkBenchmarkModule));
    }
}
