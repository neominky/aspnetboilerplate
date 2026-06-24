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
    /// Moved from <c>AbpKernelModule.RegisterInterceptors</c> in <c>src/Abp/AbpKernelModule.cs</c>
    /// and conventional <c>ITransientDependency</c> helper types under <c>src/Abp</c>.
    /// </summary>
    internal static class CastleInterceptorRegistrars
    {
        public static void RegisterKernelInterceptors(IIocManager iocManager)
        {
            iocManager.Register<IAuditingHelper, AuditingHelper>(DependencyLifeStyle.Transient);
            iocManager.Register<IAuthorizationHelper, AuthorizationHelper>(DependencyLifeStyle.Transient);
            iocManager.Register<IEntityHistoryUseCaseDescriptionProvider, EntityHistoryUseCaseDescriptionProvider>(DependencyLifeStyle.Transient);
            iocManager.Register<IMethodInvocationValidator, MethodInvocationValidator>(DependencyLifeStyle.Transient);

            iocManager.Register(typeof(AbpAsyncDeterminationInterceptor<UnitOfWorkInterceptor>), DependencyLifeStyle.Transient);
            iocManager.Register(typeof(AbpAsyncDeterminationInterceptor<AuditingInterceptor>), DependencyLifeStyle.Transient);
            iocManager.Register(typeof(AbpAsyncDeterminationInterceptor<AuthorizationInterceptor>), DependencyLifeStyle.Transient);
            iocManager.Register(typeof(AbpAsyncDeterminationInterceptor<ValidationInterceptor>), DependencyLifeStyle.Transient);
            iocManager.Register(typeof(AbpAsyncDeterminationInterceptor<EntityHistoryInterceptor>), DependencyLifeStyle.Transient);
        }
    }
}
