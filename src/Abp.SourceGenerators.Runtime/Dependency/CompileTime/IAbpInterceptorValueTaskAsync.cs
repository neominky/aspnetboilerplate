using System.Threading.Tasks;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Compile-time allocation-free async interception for <see cref="ValueTask{TResult}"/>.
    /// Void-returning <see cref="ValueTask"/> methods use <see cref="AbpUnit"/> as <c>TResult</c>.
    /// </summary>
    public interface IAbpInterceptorValueTaskAsync
    {
        ValueTask<TResult> InterceptAsynchronous<TResult>(AbpInvocationStruct<ValueTask<TResult>> invocation);
    }
}
