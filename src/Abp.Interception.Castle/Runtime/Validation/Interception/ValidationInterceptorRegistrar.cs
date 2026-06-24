using System.Reflection;
using Abp.Dependency;
using Abp.Runtime.Validation;
using Abp.Runtime.Validation.Interception;
using Castle.Core;

namespace Abp.Interception.Castle
{
    /// <summary>
    /// Castle DynamicProxy registration for validation.
    /// Moved from <c>src/Abp/Runtime/Validation/Interception/ValidationInterceptorRegistrar.cs</c>.
    /// </summary>
    internal static class ValidationInterceptorRegistrar
    {
        public static void Initialize(IIocManager iocManager)
        {
            iocManager.IocContainer.Kernel.ComponentRegistered += (key, handler) =>
            {
                var implementationType = handler.ComponentModel.Implementation.GetTypeInfo();
            
                if (!iocManager.IsRegistered<IAbpValidationDefaultOptions>())
                {
                    return;
                }
                
                var validationOptions = iocManager.Resolve<IAbpValidationDefaultOptions>();

                if (validationOptions.IsConventionalValidationClass(implementationType.AsType()))
                {
                    handler.ComponentModel.Interceptors.Add(new InterceptorReference(typeof(AbpAsyncDeterminationInterceptor<ValidationInterceptor>)));
                }
            };
        }
    }
}
