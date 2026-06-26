using System.Threading.Tasks;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Compile-time allocation-free async interception for <see cref="Task"/> and <see cref="Task{TResult}"/>.
    /// </summary>
    public interface IAbpInterceptorTaskAsync
    {
        void InterceptAsynchronous(ref AbpInvocationStruct<Task> invocation);

        void InterceptAsynchronous<TResult>(ref AbpInvocationStruct<Task<TResult>> invocation);
    }
}
