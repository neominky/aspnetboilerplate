using System;
using System.Reflection;
using System.Threading.Tasks;
using Abp.Dependency;
using Castle.DynamicProxy;

namespace Abp.Dependency
{
    /// <summary>
    /// Bridges Castle DynamicProxy <see cref="IInvocation"/> to <see cref="IAbpInvocation"/>.
    /// Moved from <c>src/Abp/Dependency/AbpCastleInterceptorBridge.cs</c>.
    /// </summary>
    internal sealed class AbpCastleInterceptorBridge : IAsyncInterceptor
    {
        private readonly AbpInterceptorBase _interceptor;

        public AbpCastleInterceptorBridge(AbpInterceptorBase interceptor)
        {
            _interceptor = interceptor;
        }

        public void InterceptSynchronous(IInvocation invocation)
        {
            _interceptor.InterceptSynchronous(new CastleAbpInvocation(invocation));
        }

        public void InterceptAsynchronous(IInvocation invocation)
        {
            _interceptor.InterceptAsynchronous(new CastleAbpInvocation(invocation));
        }

        public void InterceptAsynchronous<TResult>(IInvocation invocation)
        {
            _interceptor.InterceptAsynchronous<TResult>(new CastleAbpInvocation(invocation));
        }
    }

    internal sealed class CastleAbpInvocation : IAbpInvocation
    {
        private readonly IInvocation _invocation;
        private readonly ReflectionAbpMethodInfo _method;

        public CastleAbpInvocation(IInvocation invocation)
        {
            _invocation = invocation;
            _method = ReflectionAbpMethodInfo.From(GetMethodInvocationTarget(invocation));
        }

        private static MethodInfo GetMethodInvocationTarget(IInvocation invocation)
        {
            try
            {
                return invocation.MethodInvocationTarget;
            }
            catch
            {
                return invocation.GetConcreteMethod();
            }
        }

        public object InvocationTarget => _invocation.InvocationTarget;

        public Type TargetType => _invocation.TargetType;

        public IAbpMethodDescriptor MethodDescriptor => _method;

        public AbpMethodInfo MethodInvocationTarget => _method;

        public MethodInfo Method => _invocation.Method;

        public object?[] Arguments => _invocation.Arguments;

        public object? ReturnValue
        {
            get => _invocation.ReturnValue;
            set => _invocation.ReturnValue = value;
        }

        public void Proceed() => _invocation.Proceed();

        public IAbpProceedInfo CaptureProceedInfo() => new CastleAbpProceedInfo(_invocation.CaptureProceedInfo());

        public MethodInfo GetConcreteMethod() => _invocation.GetConcreteMethod();
    }

    internal sealed class CastleAbpProceedInfo : IAbpProceedInfo
    {
        private readonly IInvocationProceedInfo _proceedInfo;

        public CastleAbpProceedInfo(IInvocationProceedInfo proceedInfo)
        {
            _proceedInfo = proceedInfo;
        }

        public void Invoke() => _proceedInfo.Invoke();
    }
}
