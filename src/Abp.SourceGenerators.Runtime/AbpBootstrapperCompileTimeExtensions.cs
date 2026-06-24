using System;

namespace Abp.Dependency.CompileTime
{
    public static class AbpBootstrapperCompileTimeExtensions
    {
        /// <summary>
        /// Creates a bootstrapper with Castle DynamicProxy interceptors disabled.
        /// Must be used at startup; module <see cref="Abp.Modules.AbpModule.Initialize"/> is too late.
        /// </summary>
        public static AbpBootstrapper CreateWithCompileTimeInterception<TStartupModule>(
            Action<AbpBootstrapperOptions>? configure = null)
            where TStartupModule : Abp.Modules.AbpModule
        {
            return AbpBootstrapper.Create<TStartupModule>(options =>
            {
                CompileTimeInterceptionConfiguration.Enable();
                configure?.Invoke(options);
            });
        }
    }
}
