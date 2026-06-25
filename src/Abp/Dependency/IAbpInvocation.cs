using System;
using System.Reflection;

namespace Abp.Dependency
{
    /// <summary>
    /// ABP invocation abstraction. Replaces <c>Castle.DynamicProxy.IInvocation</c> for compile-time
    /// interception and unifies runtime interceptors.
    /// </summary>
    public interface IAbpInvocation
    {
        object InvocationTarget { get; }

        Type TargetType { get; }

        /// <summary>
        /// The method being invoked, optionally wrapped as <see cref="AbpMethodInfo"/> when baked metadata exists.
        /// </summary>
        MethodInfo MethodInvocationTarget { get; }

        MethodInfo Method { get; }

        object?[] Arguments { get; }

        object? ReturnValue { get; set; }

        void Proceed();

        IAbpProceedInfo CaptureProceedInfo();

        MethodInfo GetConcreteMethod();
    }
}
