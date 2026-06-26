using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Dependency.CompileTime;

namespace Abp.Interception.CompileTime.Host.Interceptors;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class StructTaggedAttribute : Attribute
{
    public string Tag { get; }

    public StructTaggedAttribute(string tag)
    {
        Tag = tag;
    }
}

/// <summary>
/// Allocation-free compile-time interceptor via <c>protected override Internal*</c> struct methods only.
/// </summary>
[AbpInterceptor(typeof(StructTaggedAttribute))]
public sealed class StructTaggedCompileTimeInterceptor : AbpInterceptorBaseAllocationFree, ITransientDependency
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
        var tag = ApplyTag(invocation.Method, invocation.InvocationTarget);
        invocation.Proceed();

        if (tag != null && invocation.ReturnValue is string text)
        {
            invocation.ReturnValue = $"{text} [struct-tag:{tag}]";
        }
    }

    protected override Task InternalInterceptAsynchronous(ref AbpInvocationStruct<Task> invocation)
    {
        ApplyTag(invocation.Method, invocation.InvocationTarget);
        return invocation.Proceed();
    }

    protected override Task<TResult> InternalInterceptAsynchronous<TResult>(ref AbpInvocationStruct<Task<TResult>> invocation)
    {
        var tag = ApplyTag(invocation.Method, invocation.InvocationTarget);
        return TagResultIfNeededAsync(invocation.Proceed(), tag);
    }

    protected override ValueTask InternalInterceptAsynchronous(ref AbpInvocationStruct<ValueTask> invocation)
    {
        ApplyTag(invocation.Method, invocation.InvocationTarget);
        return invocation.Proceed();
    }

    protected override ValueTask<TResult> InternalInterceptAsynchronous<TResult>(ref AbpInvocationStruct<ValueTask<TResult>> invocation)
    {
        var tag = ApplyTag(invocation.Method, invocation.InvocationTarget);
        return new ValueTask<TResult>(TagResultIfNeededAsync(
            invocation.Proceed().AsTask(),
            tag));
    }

    private static string? ApplyTag(MethodInfo methodInvocationTarget, object invocationTarget)
    {
        InvocationCount++;

        var tag = TryGetTag(methodInvocationTarget, invocationTarget);
        if (tag != null)
        {
            LastTag = tag;
        }

        return tag;
    }

    private static async Task<TResult> TagResultIfNeededAsync<TResult>(Task<TResult> task, string? tag)
    {
        var result = await task.ConfigureAwait(false);

        if (tag != null && result is string text)
        {
            return (TResult)(object)$"{text} [struct-tag:{tag}]";
        }

        return result;
    }

    private static string? TryGetTag(MethodInfo methodInvocationTarget, object invocationTarget)
    {
        var taggedAttribute = methodInvocationTarget
            .GetCustomAttributes(typeof(StructTaggedAttribute), inherit: true)
            .OfType<StructTaggedAttribute>()
            .FirstOrDefault();
        if (taggedAttribute != null)
        {
            return taggedAttribute.Tag;
        }

        var targetType = invocationTarget.GetType();
        var method = targetType.GetMethod(methodInvocationTarget.Name);
        return method?.GetCustomAttribute<StructTaggedAttribute>()?.Tag;
    }
}
