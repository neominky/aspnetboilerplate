using System;
using System.Linq;
using System.Reflection;
using Abp.Application.Features;
using Abp.Dependency;
using Castle.MicroKernel;

namespace Abp.Authorization
{
    /// <summary>
    /// This class is used to register interceptors on the Application Layer.
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
