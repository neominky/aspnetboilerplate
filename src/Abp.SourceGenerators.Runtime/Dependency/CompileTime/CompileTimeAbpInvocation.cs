using System;
using System.Reflection;
using System.Threading.Tasks;
using Abp.Dependency;

namespace Abp.Dependency.CompileTime
{
    public sealed class CompileTimeAbpInvocation : IAbpInvocation, ICompileTimeInvocationProceedHost
    {
        private readonly MethodInfo _method;
        private Func<Task<object?>>? _proceedAsync;

        public CompileTimeAbpInvocation(
            object invocationTarget,
            MethodInfo method,
            object?[] arguments)
        {
            InvocationTarget = invocationTarget;
            _method = method;
            MethodInvocationTarget = AbpMethodInfo.GetInvocationMethod(method);
            TargetType = method.DeclaringType!;
            Arguments = arguments;
        }

        public object InvocationTarget { get; }

        public Type TargetType { get; }

        public MethodInfo MethodInvocationTarget { get; }

        public MethodInfo Method => _method;

        public object?[] Arguments { get; }

        public object? ReturnValue { get; set; }

        public void SetProceed(Func<Task<object?>> proceed)
        {
            _proceedAsync = proceed;
        }

        public void Proceed()
        {
            if (_proceedAsync == null)
            {
                throw new InvalidOperationException("Proceed is not configured.");
            }

            _proceedAsync().GetAwaiter().GetResult();
        }

        public IAbpProceedInfo CaptureProceedInfo() => new CompileTimeAbpProceedInfo(this);

        public MethodInfo GetConcreteMethod() => _method;

        internal async Task ProceedAsync()
        {
            if (_proceedAsync == null)
            {
                throw new InvalidOperationException("Proceed is not configured.");
            }

            await _proceedAsync().ConfigureAwait(false);
        }
    }

    public sealed class CompileTimeAbpProceedInfo : IAbpProceedInfo
    {
        private readonly CompileTimeAbpInvocation _invocation;

        public CompileTimeAbpProceedInfo(CompileTimeAbpInvocation invocation)
        {
            _invocation = invocation;
        }

        public void Invoke()
        {
            _invocation.Proceed();
        }
    }
}
