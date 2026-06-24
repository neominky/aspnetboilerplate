using System;
using Abp.Dependency;

namespace Abp.EntityHistory
{
    internal static class EntityHistoryInterceptorRegistrar
    {
        internal static Action<IIocManager>? InitializeHandler { get; set; }

        public static void Initialize(IIocManager iocManager)
        {
            InitializeHandler?.Invoke(iocManager);
        }
    }
}
