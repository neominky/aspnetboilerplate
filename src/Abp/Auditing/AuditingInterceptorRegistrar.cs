using System;
using Abp.Dependency;

namespace Abp.Auditing
{
    /// <summary>
    /// Registration facade for <see cref="AbpBootstrapper"/>.
    /// Castle DynamicProxy implementation moved to <c>src/Abp.Interception.Castle/Auditing/AuditingInterceptorRegistrar.cs</c>.
    /// </summary>
    internal static class AuditingInterceptorRegistrar
    {
        internal static Action<IIocManager>? InitializeHandler { get; set; }

        public static void Initialize(IIocManager iocManager)
        {
            InitializeHandler?.Invoke(iocManager);
        }
    }
}
