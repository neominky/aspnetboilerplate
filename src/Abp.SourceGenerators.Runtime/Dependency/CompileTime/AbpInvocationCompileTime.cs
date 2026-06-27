using System;
using System.Reflection;
using System.Threading.Tasks;
using Abp.Dependency;

namespace Abp.Dependency.CompileTime
{
    public class AbpInvocationCompileTime : IAbpInvocation,
        IAbpInterceptorValueTaskReturnHost, IAbpInterceptorValueTaskReturnSource
    {
        private readonly MethodInfo _method;
        private Func<Task<object?>>? _proceedAsync;

        public AbpInvocationCompileTime(
            object invocationTarget,
            MethodInfo method,
            object?[] arguments,
            bool wrapMethodWithMetadata = true)
        {
            InvocationTarget = invocationTarget;
            _method = method;
            MethodInvocationTarget = wrapMethodWithMetadata
                ? AbpMethodInfo.GetInvocationMethod(method)
                : method;
            TargetType = method.DeclaringType!;
            Arguments = arguments;
        }

        public object InvocationTarget { get; }

        public Type TargetType { get; }

        public MethodInfo MethodInvocationTarget { get; }

        public MethodInfo Method => _method;

        public object?[] Arguments { get; }

        public object? ReturnValue { get; set; }

        public ValueTask ValueTaskReturnValue { get; set; }

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

        public IAbpProceedInfo CaptureProceedInfo() => new AbpInvocationCompileTimeProceedInfo(this);

        public MethodInfo GetConcreteMethod() => _method;

        public object? GetValueTaskReturnForCompatibility() => ValueTaskReturnValue;
    }

    public sealed class AbpInvocationCompileTime<TResult> : AbpInvocationCompileTime,
        IAbpInterceptorValueTaskReturnHost<TResult>, IAbpInterceptorGenericValueTaskBridge
    {
        public AbpInvocationCompileTime(
            object invocationTarget,
            MethodInfo method,
            object?[] arguments,
            bool wrapMethodWithMetadata = true)
            : base(invocationTarget, method, arguments, wrapMethodWithMetadata)
        {
        }

        public new ValueTask<TResult> ValueTaskReturnValue { get; set; }

        public Task MaterializeReturnAsTask()
        {
            if (ReturnValue is Task<TResult> task)
            {
                return task;
            }

            return AbpInvocationReturnValueMaterializer.AsTask(ValueTaskReturnValue);
        }

        public static AbpInvocationCompileTime<TResult> EnsureClassBridge(
            ref AbpInvocationStruct<ValueTask<TResult>> structInvocation,
            ref AbpInvocationCompileTime<TResult>? classBridge)
        {
            classBridge ??= new AbpInvocationCompileTime<TResult>(
                structInvocation.InvocationTarget,
                structInvocation.Method,
                structInvocation.Arguments,
                wrapMethodWithMetadata: false);

            classBridge.ValueTaskReturnValue = structInvocation.ReturnValue;
            return classBridge;
        }

        public static AbpInvocationCompileTime<TResult> EnsureClassBridge(
            ref AbpInvocationStruct<Task<TResult>> structInvocation,
            ref AbpInvocationCompileTime<TResult>? classBridge)
        {
            classBridge ??= new AbpInvocationCompileTime<TResult>(
                structInvocation.InvocationTarget,
                structInvocation.Method,
                structInvocation.Arguments,
                wrapMethodWithMetadata: false);

            classBridge.ReturnValue = structInvocation.ReturnValue;
            return classBridge;
        }
    }

    public sealed class AbpInvocationCompileTimeProceedInfo : IAbpProceedInfo
    {
        private readonly AbpInvocationCompileTime _invocation;

        public AbpInvocationCompileTimeProceedInfo(AbpInvocationCompileTime invocation)
        {
            _invocation = invocation;
        }

        public void Invoke()
        {
            _invocation.Proceed();
        }
    }
}
