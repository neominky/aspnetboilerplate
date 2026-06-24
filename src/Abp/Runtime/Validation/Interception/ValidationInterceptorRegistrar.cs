using System;
using Abp.Dependency;

namespace Abp.Runtime.Validation.Interception
{
    internal static class ValidationInterceptorRegistrar
    {
        internal static Action<IIocManager>? InitializeHandler { get; set; }

        public static void Initialize(IIocManager iocManager)
        {
            InitializeHandler?.Invoke(iocManager);
        }
    }
}
