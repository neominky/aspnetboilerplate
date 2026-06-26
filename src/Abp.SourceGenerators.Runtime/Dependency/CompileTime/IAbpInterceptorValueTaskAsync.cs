using System.Threading.Tasks;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Compile-time allocation-free async interception for <see cref="ValueTask"/> and <see cref="ValueTask{TResult}"/>.
    /// </summary>
    public interface IAbpInterceptorValueTaskAsync
    {
        void InterceptAsynchronous(ref AbpInvocationStruct<ValueTask> invocation);

        void InterceptAsynchronous<TResult>(ref AbpInvocationStruct<ValueTask<TResult>> invocation);
    }
}
