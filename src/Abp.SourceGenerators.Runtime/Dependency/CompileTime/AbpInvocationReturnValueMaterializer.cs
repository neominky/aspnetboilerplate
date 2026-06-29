using System;
using System.Threading.Tasks;
using Abp.Dependency;

namespace Abp.Dependency.CompileTime
{
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
