using System;
using System.Linq;
using System.Reflection;
using Abp.Dependency;

namespace Abp.Domain.Uow
{
    /// <summary>
    /// This class is used to register interceptor for needed classes for Unit Of Work mechanism.
    /// Castle DynamicProxy implementation moved to <c>src/Abp.Interception.Castle/Domain/Uow/UnitOfWorkRegistrar.cs</c>.
    /// </summary>
    internal static class UnitOfWorkRegistrar
    {
        internal static Action<IIocManager>? InitializeHandler { get; set; }

        /// <summary>
        /// Initializes the registerer.
        /// </summary>
        /// <param name="iocManager">IOC manager</param>
        public static void Initialize(IIocManager iocManager)
        {
            InitializeHandler?.Invoke(iocManager);
        }
    }
}
