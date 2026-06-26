using System;
using System.Linq;
using System.Reflection;
using Abp.Dependency;
using Abp.Domain.Uow;

namespace Abp.EntityHistory
{
    /// <summary>
    /// Castle DynamicProxy implementation moved to <c>src/Abp.Interception.Castle/EntityHistory/EntityHistoryInterceptorRegistrar.cs</c>.
    /// </summary>
    internal static class EntityHistoryInterceptorRegistrar
    {
        internal static Action<IIocManager>? InitializeHandler { get; set; }

        public static void Initialize(IIocManager iocManager)
        {
            InitializeHandler?.Invoke(iocManager);
        }
    }
}
