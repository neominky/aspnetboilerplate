using System.Runtime.CompilerServices;
using Abp.Dependency;

namespace Abp.Interception.Castle
{
    internal static class AbpCastleInterceptionInitializer
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            global::Abp.Runtime.Validation.Interception.ValidationInterceptorRegistrar.InitializeHandler =
                ValidationInterceptorRegistrar.Initialize;
            global::Abp.Auditing.AuditingInterceptorRegistrar.InitializeHandler =
                AuditingInterceptorRegistrar.Initialize;
            global::Abp.EntityHistory.EntityHistoryInterceptorRegistrar.InitializeHandler =
                EntityHistoryInterceptorRegistrar.Initialize;
            global::Abp.Domain.Uow.UnitOfWorkRegistrar.InitializeHandler =
                UnitOfWorkRegistrar.Initialize;
            global::Abp.Authorization.AuthorizationInterceptorRegistrar.InitializeHandler =
                AuthorizationInterceptorRegistrar.Initialize;
            AbpKernelModule.RegisterInterceptors = CastleInterceptorRegistrars.RegisterKernelInterceptors;
        }
    }
}
