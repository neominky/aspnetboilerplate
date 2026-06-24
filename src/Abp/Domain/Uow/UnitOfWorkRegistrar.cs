using System;
using Abp.Dependency;

namespace Abp.Domain.Uow
{
    /// <summary>
    /// Registration facade for <see cref="AbpBootstrapper"/>.
    /// Castle DynamicProxy implementation moved to <c>src/Abp.Interception.Castle/Domain/Uow/UnitOfWorkRegistrar.cs</c>.
    /// </summary>
    internal static class UnitOfWorkRegistrar
    {
        internal static Action<IIocManager>? InitializeHandler { get; set; }

        public static void Initialize(IIocManager iocManager)
        {
            InitializeHandler?.Invoke(iocManager);
        }
    }
}
