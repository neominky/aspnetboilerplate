using System;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Uow;
using Abp.EntityHistory;
using Abp.Runtime.Validation.Interception;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Global switch for compile-time IoC registration and interception.
    /// When enabled, Castle DynamicProxy interceptors are not registered.
    /// </summary>
    public static class CompileTimeInterceptionConfiguration
    {
        public static bool IsEnabled { get; private set; }

        public static void Enable()
        {
            if (IsEnabled)
            {
                return;
            }

            IsEnabled = true;
            DisableCastleDynamicProxyHandlers();
        }

        public static void Disable()
        {
            IsEnabled = false;
        }

        private static void DisableCastleDynamicProxyHandlers()
        {
            ValidationInterceptorRegistrar.InitializeHandler = _ => { };
            AuditingInterceptorRegistrar.InitializeHandler = _ => { };
            EntityHistoryInterceptorRegistrar.InitializeHandler = _ => { };
            UnitOfWorkRegistrar.InitializeHandler = _ => { };
            AuthorizationInterceptorRegistrar.InitializeHandler = _ => { };
        }
    }
}
