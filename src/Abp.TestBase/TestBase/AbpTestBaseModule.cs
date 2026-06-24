using Abp.Interception.Castle;
using Abp.Modules;
using Abp.Reflection.Extensions;

namespace Abp.TestBase
{
    [DependsOn(typeof(AbpKernelModule), typeof(AbpCastleInterceptionModule))]
    public class AbpTestBaseModule : AbpModule
    {
        public override void PreInitialize()
        {
            Configuration.EventBus.UseDefaultEventBus = false;
            Configuration.DefaultNameOrConnectionString = "Default";
        }

        public override void Initialize()
        {
            IocManager.RegisterAssemblyByConvention(typeof(AbpTestBaseModule).GetAssembly());
        }
    }
}