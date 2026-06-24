using Abp;
using Abp.Modules;

namespace Abp.Interception.Castle
{
    /// <summary>
    /// Marker module for Castle DynamicProxy interception.
    /// IoC registration is performed from <see cref="CastleInterceptorRegistrars"/> via facade <c>*Registrar</c> classes and <see cref="AbpKernelModule.RegisterInterceptors"/>.
    /// </summary>
    [DependsOn(typeof(AbpKernelModule))]
    public class AbpCastleInterceptionModule : AbpModule
    {
    }
}
