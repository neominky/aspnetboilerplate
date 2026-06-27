using System;
using System.Threading.Tasks;
using Abp.Dependency;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Presents <see cref="ValueTask"/> proceed results as <see cref="Task"/> for existing Task-based interceptors
    /// without casting <see cref="ValueTask"/> to <see cref="Task"/>.
    /// </summary>
    public sealed class AbpInvocationCompileTimeTaskCompatible : IAbpInvocation
    {
        private readonly IAbpInvocation _inner;

        public AbpInvocationCompileTimeTaskCompatible(IAbpInvocation inner)
        {
            _inner = inner;
        }

        public object InvocationTarget => _inner.InvocationTarget;

        public Type TargetType => _inner.TargetType;

        public System.Reflection.MethodInfo MethodInvocationTarget => _inner.MethodInvocationTarget;

        public System.Reflection.MethodInfo Method => _inner.Method;

        public object?[] Arguments => _inner.Arguments;

        public object? ReturnValue
        {
            get => AbpInvocationReturnValueMaterializer.MaterializeForTaskCompatibility(_inner);
            set => _inner.ReturnValue = value;
        }

        public void Proceed() => _inner.Proceed();

        public IAbpProceedInfo CaptureProceedInfo() => _inner.CaptureProceedInfo();

        public System.Reflection.MethodInfo GetConcreteMethod() => _inner.GetConcreteMethod();
    }

    internal static class AbpInvocationReturnValueMaterializer
    {
        public static object? MaterializeForTaskCompatibility(IAbpInvocation invocation)
        {
            if (invocation.ReturnValue != null)
            {
                return MaterializeForTaskCompatibility(invocation.ReturnValue);
            }

            if (invocation is IAbpInterceptorGenericValueTaskBridge genericBridge)
            {
                return genericBridge.MaterializeReturnAsTask();
            }

            if (invocation is IAbpInterceptorValueTaskReturnSource source)
            {
                return MaterializeForTaskCompatibility(source.GetValueTaskReturnForCompatibility());
            }

            return null;
        }

        public static object? MaterializeForTaskCompatibility(object? returnValue)
        {
            if (returnValue == null)
            {
                return null;
            }

            if (returnValue is Task)
            {
                return returnValue;
            }

            if (returnValue is ValueTask valueTask)
            {
                return AsTask(valueTask);
            }

            throw new AbpException(
                $"ReturnValue must be Task or ValueTask, but was: {returnValue.GetType().FullName}.");
        }

        public static Task AsTask(ValueTask valueTask) => AwaitValueTaskCore(valueTask);

        public static Task<T> AsTask<T>(ValueTask<T> valueTask) => AwaitValueTaskGenericAsync(valueTask);

        private static async Task AwaitValueTaskCore(ValueTask valueTask)
        {
            await valueTask.ConfigureAwait(false);
        }

        private static async Task<T> AwaitValueTaskGenericAsync<T>(ValueTask<T> valueTask)
        {
            return await valueTask.ConfigureAwait(false);
        }
    }
}
