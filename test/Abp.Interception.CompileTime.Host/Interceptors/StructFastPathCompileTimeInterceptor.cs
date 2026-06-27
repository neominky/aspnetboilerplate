using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Dependency.CompileTime;

namespace Abp.Interception.CompileTime.Host.Interceptors;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class StructFastPathTaggedAttribute : Attribute
{
    public string Tag { get; }

    public StructFastPathTaggedAttribute(string tag)
    {
        Tag = tag;
    }
}

/// <summary>
/// Allocation-free interceptor using the fast path: <see cref="AbpInvocationStruct.Proceed"/> /
/// <see cref="AbpInvocationStruct{TAsync}.Proceed"/>.
/// </summary>
[AbpInterceptor(typeof(StructFastPathTaggedAttribute))]
public sealed class StructFastPathCompileTimeInterceptor : AbpInterceptorBaseAllocationFree, ITransientDependency
{
    public static int InvocationCount { get; private set; }

    public static string? LastTag { get; private set; }

    public static void ResetForTest()
    {
        InvocationCount = 0;
        LastTag = null;
    }

    protected override void InternalInterceptSynchronous(ref AbpInvocationStruct invocation)
    {
        RecordTagIfPresent(invocation.Method, invocation.InvocationTarget);
        invocation.Proceed();
    }

    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(AbpInvocationStruct<Task<TResult>> invocation)
    {
        RecordTagIfPresent(invocation.Method, invocation.InvocationTarget);
        return await invocation.Proceed().ConfigureAwait(false);
    }

    protected override async ValueTask<TResult> InternalInterceptAsynchronous<TResult>(AbpInvocationStruct<ValueTask<TResult>> invocation)
    {
        RecordTagIfPresent(invocation.Method, invocation.InvocationTarget);
        return await invocation.Proceed().ConfigureAwait(false);
    }

    private static void RecordTagIfPresent(MethodInfo methodInvocationTarget, object invocationTarget)
    {
        var tag = TryGetTag(methodInvocationTarget, invocationTarget);
        if (tag == null)
        {
            return;
        }

        InvocationCount++;
        LastTag = tag;
    }

    private static string? TryGetTag(MethodInfo methodInvocationTarget, object invocationTarget)
    {
        var taggedAttribute = methodInvocationTarget
            .GetCustomAttributes(typeof(StructFastPathTaggedAttribute), inherit: true)
            .OfType<StructFastPathTaggedAttribute>()
            .FirstOrDefault();
        if (taggedAttribute != null)
        {
            return taggedAttribute.Tag;
        }

        var targetType = invocationTarget.GetType();
        var method = targetType.GetMethod(methodInvocationTarget.Name);
        return method?.GetCustomAttribute<StructFastPathTaggedAttribute>()?.Tag;
    }
}
