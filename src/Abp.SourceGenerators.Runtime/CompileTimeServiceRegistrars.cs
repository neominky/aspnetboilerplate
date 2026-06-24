using Abp.Auditing;
using Abp.Authorization;
using Abp.Dependency;
using Abp.Domain.Uow;
using Abp.EntityHistory;
using Abp.Runtime.Validation.Interception;

namespace Abp.Dependency.CompileTime
{
    internal static class CompileTimeServiceRegistrars
    {
        public static void Initialize(IIocManager iocManager)
        {
            iocManager.Register<IAuditingHelper, CompileTimeAuditingHelper>(DependencyLifeStyle.Transient);
            iocManager.Register<IAuthorizationHelper, CompileTimeAuthorizationHelper>(DependencyLifeStyle.Transient);
            iocManager.Register<IEntityHistoryUseCaseDescriptionProvider, CompileTimeEntityHistoryUseCaseDescriptionProvider>(DependencyLifeStyle.Transient);
            iocManager.Register<IMethodInvocationValidator, CompileTimeMethodInvocationValidator>(DependencyLifeStyle.Transient);
        }
    }
}
