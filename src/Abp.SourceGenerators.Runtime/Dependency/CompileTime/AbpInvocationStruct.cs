using System;
using System.Reflection;
using System.Threading.Tasks;
using Abp.Dependency;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Stack-based invocation state for compile-time synchronous interception.
    /// Struct counterpart of <see cref="IAbpInvocation"/> without heap allocation.
    /// </summary>
    public struct AbpInvocationStruct
    {
        private Func<Task<object?>>? _proceedAsync;

        public object InvocationTarget { get; private set; }

        public Type TargetType { get; private set; }

        public AbpInvocationMethod InvocationMethod { get; private set; }

        public MethodInfo Method => InvocationMethod.Method;

        public AbpMethodInterceptionMetadata? Metadata => InvocationMethod.Metadata;

        public object?[] Arguments { get; private set; }

        public object? ReturnValue { get; set; }

        public void Initialize(
            object invocationTarget,
            in AbpInvocationMethod invocationMethod,
            object?[] arguments)
        {
            InvocationTarget = invocationTarget;
            InvocationMethod = invocationMethod;
            TargetType = invocationMethod.Method.DeclaringType!;
            Arguments = arguments;
        }

        public void SetSyncProceed(Func<Task<object?>> proceed)
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
    }

    /// <summary>
    /// Stack-based invocation state for compile-time asynchronous interception.
    /// Use <see cref="ValueTask"/>, <see cref="ValueTask{TResult}"/>, <see cref="Task"/>, or <see cref="Task{TResult}"/>
    /// as <typeparamref name="TAsync"/>.
    /// </summary>
    public struct AbpInvocationStruct<TAsync>
    {
        private Func<TAsync>? _proceed;

        public object InvocationTarget { get; private set; }

        public Type TargetType { get; private set; }

        public AbpInvocationMethod InvocationMethod { get; private set; }

        public MethodInfo Method => InvocationMethod.Method;

        public AbpMethodInterceptionMetadata? Metadata => InvocationMethod.Metadata;

        public object?[] Arguments { get; private set; }

        public TAsync ReturnValue { get; set; }

        public void Initialize(
            object invocationTarget,
            in AbpInvocationMethod invocationMethod,
            object?[] arguments)
        {
            InvocationTarget = invocationTarget;
            InvocationMethod = invocationMethod;
            TargetType = invocationMethod.Method.DeclaringType!;
            Arguments = arguments;
        }

        public void SetProceed(Func<TAsync> proceed)
        {
            _proceed = proceed;
        }

        public TAsync Proceed()
        {
            if (_proceed == null)
            {
                throw new InvalidOperationException("Proceed is not configured.");
            }

            return _proceed();
        }
    }
}
