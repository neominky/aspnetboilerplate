using System;
using System.Reflection;
using Abp.Dependency;

namespace Abp.Runtime.Validation.Interception
{
    /// <summary>
    /// Castle DynamicProxy implementation moved to <c>src/Abp.Interception.Castle/Runtime/Validation/Interception/ValidationInterceptorRegistrar.cs</c>.
    /// </summary>
    internal static class ValidationInterceptorRegistrar
    {
        internal static Action<IIocManager>? InitializeHandler { get; set; }

        public static void Initialize(IIocManager iocManager)
        {
            InitializeHandler?.Invoke(iocManager);
        }
    }
}
