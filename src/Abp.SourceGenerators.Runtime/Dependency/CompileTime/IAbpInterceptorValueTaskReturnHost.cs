using System.Threading.Tasks;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Typed return slot for built-in interceptor Task compatibility on ValueTask methods.
    /// </summary>
    public interface IAbpInterceptorValueTaskReturnHost
    {
        ValueTask ValueTaskReturnValue { get; set; }
    }

    /// <summary>
    /// Typed return slot for built-in interceptor Task compatibility on ValueTask&lt;TResult&gt; methods.
    /// </summary>
    public interface IAbpInterceptorValueTaskReturnHost<TResult>
    {
        ValueTask<TResult> ValueTaskReturnValue { get; set; }
    }

    /// <summary>
    /// Exposes typed ValueTask return slots to built-in Task compatibility adapters.
    /// </summary>
    public interface IAbpInterceptorValueTaskReturnSource
    {
        object? GetValueTaskReturnForCompatibility();
    }
}
