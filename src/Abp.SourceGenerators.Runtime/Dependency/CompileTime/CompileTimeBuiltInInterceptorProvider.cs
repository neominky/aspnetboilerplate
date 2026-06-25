using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Uow;
using Abp.EntityHistory;
using Abp.Runtime.Validation.Interception;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Resolves built-in ABP interceptors for compile-time generated decorators.
    /// </summary>
    public static class CompileTimeBuiltInInterceptorProvider
    {
        public static AbpInterceptorBase Validation(IIocResolver iocResolver)
        {
            return iocResolver.Resolve<ValidationInterceptor>();
        }

        public static AbpInterceptorBase Auditing(IIocResolver iocResolver)
        {
            return iocResolver.Resolve<AuditingInterceptor>();
        }

        public static AbpInterceptorBase EntityHistory(IIocResolver iocResolver)
        {
            return iocResolver.Resolve<EntityHistoryInterceptor>();
        }

        public static AbpInterceptorBase UnitOfWork(IIocResolver iocResolver)
        {
            return iocResolver.Resolve<UnitOfWorkInterceptor>();
        }

        public static AbpInterceptorBase Authorization(IIocResolver iocResolver)
        {
            return iocResolver.Resolve<AuthorizationInterceptor>();
        }
    }
}
