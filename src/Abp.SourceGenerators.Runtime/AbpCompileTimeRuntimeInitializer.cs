using System.Runtime.CompilerServices;
using Abp;
using Abp.Dependency;

namespace Abp.Dependency.CompileTime
{
    internal static class AbpCompileTimeRuntimeInitializer
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            var previousRegistration = AbpKernelModule.RegisterInterceptors;
            AbpKernelModule.RegisterInterceptors = iocManager =>
            {
                if (CompileTimeInterceptionConfiguration.IsEnabled)
                {
                    CompileTimeServiceRegistrars.Initialize(iocManager);
                    return;
                }

                previousRegistration?.Invoke(iocManager);
            };
        }
    }
}
