using Abp.Auditing;
using Abp.Authorization;
using Abp.Dependency;
using Abp.Domain.Uow;
using Abp.EntityHistory;
using Abp.Runtime.Validation.Interception;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Resolves built-in ABP interceptors for compile-time generated decorators.
    /// Honors <see cref="CompileTimeInterceptionConfiguration.InterceptorOptions"/> at runtime.
    /// </summary>
    public static class CompileTimeBuiltInInterceptorProvider
    {
        public static AbpInterceptorBase Validation(IIocResolver iocResolver)
        {
            if (CompileTimeInterceptionConfiguration.InterceptorOptions.DisableValidationInterceptor)
            {
                return CompileTimeNoOpInterceptor.Instance;
            }

            return iocResolver.Resolve<ValidationInterceptor>();
        }

        public static AbpInterceptorBase Auditing(IIocResolver iocResolver)
        {
            if (CompileTimeInterceptionConfiguration.InterceptorOptions.DisableAuditingInterceptor)
            {
                return CompileTimeNoOpInterceptor.Instance;
            }

            return iocResolver.Resolve<AuditingInterceptor>();
        }

        public static AbpInterceptorBase EntityHistory(IIocResolver iocResolver)
        {
            if (CompileTimeInterceptionConfiguration.InterceptorOptions.DisableEntityHistoryInterceptor)
            {
                return CompileTimeNoOpInterceptor.Instance;
            }

            return iocResolver.Resolve<EntityHistoryInterceptor>();
        }

        public static AbpInterceptorBase UnitOfWork(IIocResolver iocResolver)
        {
            if (CompileTimeInterceptionConfiguration.InterceptorOptions.DisableUnitOfWorkInterceptor)
            {
                return CompileTimeNoOpInterceptor.Instance;
            }

            return iocResolver.Resolve<UnitOfWorkInterceptor>();
        }

        public static AbpInterceptorBase Authorization(IIocResolver iocResolver)
        {
            if (CompileTimeInterceptionConfiguration.InterceptorOptions.DisableAuthorizationInterceptor)
            {
                return CompileTimeNoOpInterceptor.Instance;
            }

            return iocResolver.Resolve<AuthorizationInterceptor>();
        }
    }
}
