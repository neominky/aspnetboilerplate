using System.Threading.Tasks;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Compile-time allocation-free async interception for <see cref="Task{TResult}"/>.
    /// Void-returning <see cref="Task"/> methods use <see cref="AbpUnit"/> as <c>TResult</c>.
    /// </summary>
    public interface IAbpInterceptorTaskAsync
    {
        Task<TResult> InterceptAsynchronous<TResult>(AbpInvocationStruct<Task<TResult>> invocation);
    }
}
