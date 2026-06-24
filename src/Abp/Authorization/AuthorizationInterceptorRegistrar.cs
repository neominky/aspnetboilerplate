using System;
using Abp.Dependency;

namespace Abp.Authorization
{
    /// <summary>
    /// Registration facade for <see cref="AbpBootstrapper"/>.
    /// Castle DynamicProxy implementation moved to <c>src/Abp.Interception.Castle/Authorization/AuthorizationInterceptorRegistrar.cs</c>.
    /// </summary>
    internal static class AuthorizationInterceptorRegistrar
    {
        internal static Action<IIocManager>? InitializeHandler { get; set; }

        public static void Initialize(IIocManager iocManager)
        {
            InitializeHandler?.Invoke(iocManager);
        }
    }
}
