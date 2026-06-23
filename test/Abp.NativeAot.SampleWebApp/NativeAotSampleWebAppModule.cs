using Abp.AspNetCore;
using Abp.Dependency.CompileTime;
using Abp.Modules;

namespace Abp.NativeAot.SampleWebApp;

[DependsOn(typeof(AbpAspNetCoreModule))]
public partial class NativeAotSampleWebAppModule : AbpModule
{
    public override void Initialize()
    {
        IocManager.RegisterAssemblyByConvention(typeof(NativeAotSampleWebAppModule));
    }
}
