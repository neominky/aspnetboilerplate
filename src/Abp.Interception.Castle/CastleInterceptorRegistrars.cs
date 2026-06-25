using Abp;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Dependency;
using Abp.Domain.Uow;
using Abp.EntityHistory;
using Abp.Runtime.Validation.Interception;

namespace Abp.Interception.Castle
{
    /// <summary>
    /// Castle DynamicProxy kernel IoC registration.
    /// Registers Castle proxy interceptors. Helper implementations are registered by convention via <see cref="ITransientDependency"/>.
    /// </summary>
    internal static class CastleInterceptorRegistrars
    {
        public static void RegisterKernelInterceptors(IIocManager iocManager)
        {
            iocManager.Register(typeof(AbpAsyncDeterminationInterceptor<UnitOfWorkInterceptor>), DependencyLifeStyle.Transient);
            iocManager.Register(typeof(AbpAsyncDeterminationInterceptor<AuditingInterceptor>), DependencyLifeStyle.Transient);
            iocManager.Register(typeof(AbpAsyncDeterminationInterceptor<AuthorizationInterceptor>), DependencyLifeStyle.Transient);
            iocManager.Register(typeof(AbpAsyncDeterminationInterceptor<ValidationInterceptor>), DependencyLifeStyle.Transient);
            iocManager.Register(typeof(AbpAsyncDeterminationInterceptor<EntityHistoryInterceptor>), DependencyLifeStyle.Transient);
        }
    }
}
