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

        IAbpMethodDescriptor MethodDescriptor { get; }

        /// <summary>
        /// The method being invoked. Replaces <c>MethodInfo</c> on compile-time and reflection paths.
        /// </summary>
        AbpMethodInfo MethodInvocationTarget { get; }

        MethodInfo Method { get; }

        object?[] Arguments { get; }

        object? ReturnValue { get; set; }

        void Proceed();

        IAbpProceedInfo CaptureProceedInfo();

        MethodInfo GetConcreteMethod();
    }
}
