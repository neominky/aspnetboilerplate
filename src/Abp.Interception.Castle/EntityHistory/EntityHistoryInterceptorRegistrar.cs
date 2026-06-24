using System;
using System.Linq;
using System.Reflection;
using Abp.Dependency;
using Abp.Domain.Uow;
using Abp.EntityHistory;
using Castle.Core;

namespace Abp.Interception.Castle
{
    /// <summary>
    /// Castle DynamicProxy registration for entity history.
    /// Moved from <c>src/Abp/EntityHistory/EntityHistoryInterceptorRegistrar.cs</c>.
    /// </summary>
    internal static class EntityHistoryInterceptorRegistrar
    {
        public static void Initialize(IIocManager iocManager)
        {
            iocManager.IocContainer.Kernel.ComponentRegistered += (key, handler) =>
            {
                if (!iocManager.IsRegistered<IEntityHistoryConfiguration>())
                {
                    return;
                }

                var entityHistoryConfiguration = iocManager.Resolve<IEntityHistoryConfiguration>();

                if (ShouldIntercept(entityHistoryConfiguration, handler.ComponentModel.Implementation))
                {
                    handler.ComponentModel.Interceptors.Add(new InterceptorReference(typeof(AbpAsyncDeterminationInterceptor<EntityHistoryInterceptor>)));
                }
            };
        }
        
        private static bool ShouldIntercept(IEntityHistoryConfiguration entityHistoryConfiguration, Type type)
        {
            if (type.GetTypeInfo().IsDefined(typeof(UseCaseAttribute), true))
            {
                return true;
            }

            if (type.GetMethods().Any(m => m.IsDefined(typeof(UseCaseAttribute), true)))
            {
                return true;
            }

            return false;
        }
    }
}
