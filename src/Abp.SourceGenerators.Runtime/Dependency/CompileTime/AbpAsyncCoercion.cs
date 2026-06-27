using System.Threading.Tasks;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Converts between void async return types and <see cref="AbpUnit"/> result types for compile-time interception.
    /// </summary>
    public static class AbpAsyncCoercion
    {
        public static async Task<AbpUnit> FromVoidTask(Task task)
        {
            await task.ConfigureAwait(false);
            return default;
        }

        public static async ValueTask<AbpUnit> FromVoidValueTask(ValueTask valueTask)
        {
            await valueTask.ConfigureAwait(false);
            return default;
        }

        public static Task ToVoidTask(Task<AbpUnit> task) => AwaitAndDiscard(task);

        public static ValueTask ToVoidValueTask(ValueTask<AbpUnit> valueTask) => AwaitAndDiscard(valueTask);

        private static async Task AwaitAndDiscard(Task<AbpUnit> task)
        {
            await task.ConfigureAwait(false);
        }

        private static async ValueTask AwaitAndDiscard(ValueTask<AbpUnit> valueTask)
        {
            await valueTask.ConfigureAwait(false);
        }
    }
}
